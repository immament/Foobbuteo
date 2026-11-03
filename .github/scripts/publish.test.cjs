const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createHash } = require('node:crypto');
const { collectPackages, publish } = require('./publish.cjs');

const options = {
  version: '0.26.1103.1', tag: 'v0.26.1103.1', requested: ['webgl', 'server'], unityVersion: '6000.6.4f1',
};
const context = { repo: { owner: 'owner', repo: 'game' }, runId: 123, runAttempt: 2, sha: 'a'.repeat(40), serverUrl: 'https://github.com' };

function fixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'foobbuteo-release-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  return root;
}

function artifact(root, target = 'webgl', attempt = 1) {
  const folder = path.join(root, `release-${target}-123-${attempt}`);
  fs.mkdirSync(folder);
  const file = `Foobbuteo-${target === 'webgl' ? 'WebGL' : 'Server'}-${options.version}.zip`;
  const bytes = Buffer.from(`validated-${target}-${attempt}`);
  fs.writeFileSync(path.join(folder, file), bytes);
  const manifest = { target, version: options.version, sha: context.sha, runId: '123', attempt, unityVersion: options.unityVersion, file, sha256: createHash('sha256').update(bytes).digest('hex') };
  fs.writeFileSync(path.join(folder, 'manifest.json'), JSON.stringify(manifest));
  return folder;
}

test('partial rerun selects latest target artifact and retains another target from a prior attempt', t => {
  const root = fixture(t);
  artifact(root, 'webgl', 1);
  artifact(root, 'webgl', 2);
  artifact(root, 'server', 1);
  const packages = collectPackages(root, context, options);
  assert.deepEqual(packages.map(p => [p.target, p.attempt]), [['webgl', 2], ['server', 1]]);
});

test('tampered bytes, wrong source, and wrong versions cannot be published', t => {
  for (const change of ['bytes', 'sha', 'version']) {
    const root = fixture(t);
    const folder = artifact(root);
    const file = path.join(folder, 'manifest.json');
    const manifest = JSON.parse(fs.readFileSync(file));
    if (change === 'bytes') fs.appendFileSync(path.join(folder, manifest.file), 'tampered');
    else { manifest[change] = 'wrong'; fs.writeFileSync(file, JSON.stringify(manifest)); }
    assert.throws(() => collectPackages(root, context, options), /Invalid|Checksum/);
  }
});

test('no successful packages creates no Release', async t => {
  let created = false;
  const github = { rest: { repos: { getReleaseByTag: async () => { throw { status: 404 }; }, createRelease: async () => { created = true; } } } };
  const core = { info() {} };
  await publish({ github, context, core }, { ...options, root: fixture(t) });
  assert.equal(created, false);
});

test('already published Release is left unchanged even when artifacts have expired', async () => {
  const github = { rest: { repos: { getReleaseByTag: async () => ({ data: { draft: false, html_url: 'release-url' } }) } } };
  await publish({ github, context, core: { info() {} } }, { ...options, root: 'nonexistent' });
});

test('one successful target publishes a downloadable Release and names the missing target', async t => {
  const root = fixture(t);
  artifact(root);
  const uploads = [];
  let published;
  const github = { rest: { repos: {
    getReleaseByTag: async () => { throw { status: 404 }; },
    createRelease: async args => { assert.equal(args.draft, true); assert.match(args.body, /server.*failed|failed.*server/i); return { data: { id: 42 } }; },
    listReleaseAssets: async () => ({ data: [] }),
    uploadReleaseAsset: async args => {
      const expectedType = args.name.endsWith('.zip') ? 'application/zip' : args.name.endsWith('.json') ? 'application/json' : 'text/plain';
      assert.equal(args.headers?.['content-type'], expectedType, 'Release downloads must have the correct MIME type.');
      uploads.push(args.name);
      return { data: { id: uploads.length } };
    },
    updateRelease: async args => { published = args; return { data: { html_url: 'release-url' } }; },
  } }, paginate: async (method, args) => (await method(args)).data };
  await publish({ github, context, core: { info() {}, summary: { addRaw() { return this; }, async write() {} } } }, { ...options, root });
  assert.equal(published.draft, false);
  assert.equal(published.make_latest, 'false');
  assert.deepEqual(uploads.sort(), ['Foobbuteo-WebGL-0.26.1103.1.zip', 'SHA256SUMS.txt', 'build-manifest.json'].sort());
});
