const fs = require('node:fs');

const versionPattern = /^(0|[1-9]\d*)\.\d{2}\.\d{4}\.[1-9]\d*$/;

function selectTargets(eventName, ref, event, config) {
  if (ref !== `refs/heads/${config.allowedBranch}`) return [];
  if (eventName === 'workflow_dispatch') {
    const target = event.inputs?.target;
    if (target === 'all') return ['webgl', 'server'];
    if (['webgl', 'server'].includes(target)) return [target];
    throw new Error('Select webgl, server, or all.');
  }
  if (eventName !== 'push') return [];
  const message = event.head_commit?.message ?? '';
  if (message.includes('[BUILD_ALL]')) return ['webgl', 'server'];
  return ['webgl', 'server'].filter(target => message.includes(`[BUILD_${target.toUpperCase()}]`));
}

async function optionalRef(github, repo, ref) {
  try { return (await github.rest.git.getRef({ ...repo, ref })).data; }
  catch (error) { if (error.status === 404) return null; throw error; }
}

async function readReservation(github, repo, ref) {
  if (ref.object.type !== 'tag') return null;
  const tag = (await github.rest.git.getTag({ ...repo, tag_sha: ref.object.sha })).data;
  let data;
  try { data = JSON.parse(tag.message); } catch { return null; }
  if (data?.kind !== 'foobbuteo-release') return null;
  if (!versionPattern.test(data.version) || data.tag !== `v${data.version}` ||
      tag.object.type !== 'commit' || tag.object.sha !== data.sha) {
    throw new Error('Invalid release reservation metadata.');
  }
  return { ...data, tagSha: ref.object.sha };
}

async function reserveVersion(github, context, major, now = new Date()) {
  if (!Number.isSafeInteger(major) || major < 0) throw new Error('major must be a nonnegative integer.');
  const repo = context.repo;
  const runId = String(context.runId);
  const lookup = `tags/ci-runs/${runId}`;
  const verify = reservation => {
    if (!reservation || reservation.runId !== runId || reservation.sha !== context.sha) {
      throw new Error('Run reservation does not match this run and source commit.');
    }
    return reservation;
  };
  const existing = await optionalRef(github, repo, lookup);
  if (existing) return verify(await readReservation(github, repo, existing));

  // Recover a crash between creating the version tag and its durable run lookup.
  // Fresh runs cannot have an interrupted reservation, so avoid a historical scan.
  if (Number(context.runAttempt ?? process.env.GITHUB_RUN_ATTEMPT ?? 1) > 1) {
    const refs = await github.paginate(github.rest.git.listMatchingRefs, { ...repo, ref: 'tags/v', per_page: 100 });
    for (const ref of refs) {
      if (!versionPattern.test(ref.ref.replace('refs/tags/v', ''))) continue;
      const reservation = await readReservation(github, repo, ref);
      if (reservation?.runId === runId) {
        verify(reservation);
        await github.rest.git.createRef({ ...repo, ref: `refs/${lookup}`, sha: reservation.tagSha });
        return reservation;
      }
    }
  }

  const date = now.toISOString().slice(0, 10);
  const prefix = `${major}.${date.slice(2, 4)}.${date.slice(5, 7)}${date.slice(8, 10)}.`;
  for (let attempt = 0; attempt < 10; attempt++) {
    const current = await github.paginate(github.rest.git.listMatchingRefs, { ...repo, ref: `tags/v${prefix}`, per_page: 100 });
    const indices = current.map(ref => ref.ref.replace('refs/tags/v', ''))
      .filter(version => versionPattern.test(version) && version.startsWith(prefix))
      .map(version => Number(version.slice(prefix.length)));
    const index = Math.max(0, ...indices) + 1;
    if (!Number.isSafeInteger(index) || index > 2147483647) throw new Error('Daily build index exhausted.');
    const version = `${prefix}${index}`;
    const data = { kind: 'foobbuteo-release', runId, version, tag: `v${version}`, sha: context.sha, date };
    const tag = (await github.rest.git.createTag({
      ...repo, tag: data.tag, message: JSON.stringify(data), object: context.sha, type: 'commit',
      tagger: { name: 'github-actions[bot]', email: '41898282+github-actions[bot]@users.noreply.github.com', date: now.toISOString() },
    })).data;
    try {
      await github.rest.git.createRef({ ...repo, ref: `refs/tags/${data.tag}`, sha: tag.sha });
    } catch (error) {
      // A 422 can also mean insufficient permissions or invalid input: verify the collision.
      if (![409, 422].includes(error.status)) throw error;
      const collision = await optionalRef(github, repo, `tags/${data.tag}`);
      if (!collision) throw error;
      continue;
    }
    await github.rest.git.createRef({ ...repo, ref: `refs/${lookup}`, sha: tag.sha });
    return { ...data, tagSha: tag.sha };
  }
  throw new Error('Could not reserve a unique version after 10 competing allocations.');
}

async function request({ context, core }) {
  const config = JSON.parse(fs.readFileSync('.github/build-config.json', 'utf8'));
  const targets = selectTargets(context.eventName, context.ref, context.payload, config);
  const unityVersion = fs.readFileSync('ProjectSettings/ProjectVersion.txt', 'utf8').match(/^m_EditorVersion:\s*(\S+)/m)?.[1];
  if (!unityVersion) throw new Error('Pinned Unity version is missing.');
  if (unityVersion !== config.unityVersion) throw new Error('Update the CI image versions/digests to match ProjectVersion.txt.');
  const profiles = {
    webgl: { platform: 'WebGL', profile: 'Assets/Settings/Build Profiles/Web - Desktop - Release.asset' },
    server: { platform: 'StandaloneWindows64', profile: 'Assets/Settings/Build Profiles/Windows Server.asset' },
  };
  core.setOutput('targets', JSON.stringify(targets));
  core.setOutput('matrix', JSON.stringify({ include: targets.map(target => ({ target, ...profiles[target], image: config.images[target] })) }));
  core.setOutput('requested', targets.length > 0 ? 'true' : 'false');
  core.setOutput('major', config.major);
  core.setOutput('unity-version', unityVersion);
  core.summary.addRaw(targets.length ? `Requested: ${targets.join(', ')} at ${context.sha}` : 'No release build requested on the allowed branch.');
  await core.summary.write();
}

module.exports = { selectTargets, reserveVersion, request, versionPattern };
