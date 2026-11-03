const { test } = require('node:test');
const assert = require('node:assert/strict');
const { selectTargets, reserveVersion } = require('./release.cjs');

const context = { repo: { owner: 'owner', repo: 'game' }, runId: 123, runAttempt: 1, sha: 'a'.repeat(40) };
const config = { allowedBranch: 'build', major: 0 };

function fakeGit() {
  const refs = new Map();
  const tags = new Map();
  let nextId = 0;
  const missing = () => Object.assign(new Error('Not found'), { status: 404 });
  const git = {
    async getRef({ ref }) {
      if (!refs.has(ref)) throw missing();
      return { data: refs.get(ref) };
    },
    async getTag({ tag_sha }) { return { data: tags.get(tag_sha) }; },
    async createTag(data) {
      const sha = `tag-${++nextId}`;
      tags.set(sha, { ...data, sha, object: { type: data.type, sha: data.object } });
      return { data: { sha } };
    },
    async createRef({ ref, sha }) {
      const key = ref.replace(/^refs\//, '');
      if (refs.has(key)) throw Object.assign(new Error('Reference already exists'), { status: 422 });
      const data = { ref, object: { type: 'tag', sha } };
      refs.set(key, data);
      return { data };
    },
    async listMatchingRefs({ ref }) {
      return { data: [...refs.entries()].filter(([key]) => key.startsWith(ref)).map(([, value]) => value) };
    },
  };
  return { rest: { git }, paginate: async (method, args) => (await method(args)).data, refs, tags };
}

test('only exact head markers on the allowed branch request automatic builds', () => {
  const event = { head_commit: { message: 'release [BUILD_WEBGL]' }, commits: [{ message: '[BUILD_ALL]' }] };
  assert.deepEqual(selectTargets('push', 'refs/heads/build', event, config), ['webgl']);
  assert.deepEqual(selectTargets('push', 'refs/heads/main', event, config), []);
  assert.deepEqual(selectTargets('push', 'refs/tags/build', event, config), []);
  assert.deepEqual(selectTargets('pull_request', 'refs/heads/build', event, config), []);
  assert.deepEqual(selectTargets('push', 'refs/heads/build', { head_commit: { message: 'BUILD_ALL' } }, config), []);
});

test('both markers or all request both targets; manual selection uses the same branch gate', () => {
  for (const message of ['[BUILD_WEBGL] [BUILD_SERVER]', '[BUILD_ALL]']) {
    assert.deepEqual(selectTargets('push', 'refs/heads/build', { head_commit: { message } }, config), ['webgl', 'server']);
  }
  assert.deepEqual(selectTargets('workflow_dispatch', 'refs/heads/build', { inputs: { target: 'server' } }, config), ['server']);
  assert.deepEqual(selectTargets('workflow_dispatch', 'refs/heads/main', { inputs: { target: 'all' } }, config), []);
  assert.throws(() => selectTargets('workflow_dispatch', 'refs/heads/build', { inputs: { target: 'android' } }, config));
});

test('UTC reservation is shared across targets and reused by run ID after midnight', async () => {
  const github = fakeGit();
  const first = await reserveVersion(github, context, 0, new Date('2026-11-03T23:30:00-02:00'));
  assert.equal(first.version, '0.26.1104.1');
  assert.equal(first.sha, context.sha);
  assert.equal((await reserveVersion(github, context, 0, new Date('2026-11-05T00:00:00Z'))).version, first.version);
  assert.equal(github.refs.size, 2, 'Reruns must reuse their reservation, not create another release version.');
});

test('competing runs reserve distinct indices and ignore unrelated tags', async () => {
  const github = fakeGit();
  github.refs.set('tags/v0.26.1103.99-preview', { ref: 'refs/tags/v0.26.1103.99-preview', object: { type: 'commit', sha: context.sha } });
  const reservations = await Promise.all([123, 124].map(runId => reserveVersion(github, { ...context, runId }, 0, new Date('2026-11-03T12:00Z'))));
  assert.deepEqual(reservations.map(x => x.version).sort(), ['0.26.1103.1', '0.26.1103.2']);
});

test('retry recovers a version tag created before its run lookup tag', async () => {
  const github = fakeGit();
  const createRef = github.rest.git.createRef;
  let interrupt = true;
  github.rest.git.createRef = async args => {
    if (args.ref.includes('ci-runs/') && interrupt) {
      interrupt = false;
      throw Object.assign(new Error('Interrupted'), { status: 503 });
    }
    return createRef(args);
  };
  await assert.rejects(reserveVersion(github, context, 0, new Date('2026-11-03T12:00Z')), /Interrupted/);
  const recovered = await reserveVersion(github, { ...context, runAttempt: 2 }, 0, new Date('2026-11-04T12:00Z'));
  assert.equal(recovered.version, '0.26.1103.1');
});

test('permission errors and non-collision validation errors fail without endless retries', async () => {
  for (const status of [403, 422]) {
    const github = fakeGit();
    let calls = 0;
    github.rest.git.createRef = async () => { calls++; throw Object.assign(new Error('Denied'), { status }); };
    await assert.rejects(reserveVersion(github, context, 0, new Date('2026-11-03T12:00Z')), /Denied/);
    assert.equal(calls, 1);
  }
});

test('an existing run reservation must point to the same source commit', async () => {
  const github = fakeGit();
  await reserveVersion(github, context, 0, new Date('2026-11-03T12:00Z'));
  await assert.rejects(reserveVersion(github, { ...context, sha: 'b'.repeat(40) }, 0), /source commit/);
});

test('fresh runs do not read every historical annotation for retry recovery', async () => {
  const github = fakeGit();
  await reserveVersion(github, context, 0, new Date('2026-11-03T12:00Z'));
  let reads = 0;
  const getTag = github.rest.git.getTag;
  github.rest.git.getTag = async args => { reads++; return getTag(args); };
  await reserveVersion(github, { ...context, runId: 124 }, 0, new Date('2026-11-04T12:00Z'));
  assert.equal(reads, 0, 'Fresh allocations should only list the current UTC date, not fetch historical tag metadata.');
});

test('interrupted-run recovery ignores unrelated JSON primitive tag messages', async () => {
  const github = fakeGit();
  github.refs.set('tags/v0.26.1102.1', { ref: 'refs/tags/v0.26.1102.1', object: { type: 'tag', sha: 'unrelated' } });
  github.tags.set('unrelated', { message: 'null', object: { type: 'commit', sha: context.sha } });
  const reservation = await reserveVersion(github, { ...context, runAttempt: 2 }, 0, new Date('2026-11-03T12:00Z'));
  assert.equal(reservation.version, '0.26.1103.1');
});
