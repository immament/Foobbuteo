const fs = require('node:fs');
const path = require('node:path');
const { createHash } = require('node:crypto');

function collectPackages(root, context, options) {
  if (!fs.existsSync(root)) return [];
  const runAttempt = Number(context.runAttempt ?? process.env.GITHUB_RUN_ATTEMPT ?? 1);
  const latest = new Map();
  const candidates = [];
  // download-artifact extracts a single match directly into the download root.
  if (fs.existsSync(path.join(root, 'manifest.json'))) {
    candidates.push({ folder: root, label: 'download root' });
  }
  for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
    const match = /^release-(webgl|server|android)-(\d+)-(\d+)$/.exec(entry.name);
    if (!entry.isDirectory() || !match || match[2] !== String(context.runId)) continue;
    const [, target, , attemptText] = match;
    const attempt = Number(attemptText);
    if (!options.requested.includes(target) || attempt > runAttempt) continue;
    candidates.push({ folder: path.join(root, entry.name), target, attempt, label: entry.name });
  }
  for (const candidate of candidates) {
    const { folder, label } = candidate;
    const manifest = JSON.parse(fs.readFileSync(path.join(folder, 'manifest.json'), 'utf8'));
    const target = candidate.target ?? manifest?.target;
    const attempt = candidate.attempt ?? manifest?.attempt;
    if (!['webgl', 'server', 'android'].includes(target) || !options.requested.includes(target) ||
        !Number.isSafeInteger(attempt) || attempt < 1 || attempt > runAttempt) {
      throw new Error(`Invalid release target or attempt in ${label}.`);
    }
    const expectedFile = `Foobbuteo-${{ webgl: 'WebGL', server: 'Server', android: 'Android' }[target]}-${options.version}.zip`;
    if (manifest.target !== target || manifest.version !== options.version || manifest.sha !== context.sha ||
        manifest.runId !== String(context.runId) || manifest.attempt !== attempt ||
        manifest.unityVersion !== options.unityVersion || manifest.file !== expectedFile) {
      throw new Error(`Invalid release provenance in ${label}.`);
    }
    const archive = path.join(folder, manifest.file);
    if (!fs.statSync(archive).isFile() || fs.statSync(archive).size >= 2 * 1024 ** 3) throw new Error('Invalid Release asset.');
    const checksum = createHash('sha256').update(fs.readFileSync(archive)).digest('hex');
    if (checksum !== manifest.sha256) throw new Error(`Checksum mismatch in ${label}.`);
    if (!latest.has(target) || latest.get(target).attempt < attempt) latest.set(target, { ...manifest, archive });
  }
  return options.requested.filter(target => latest.has(target)).map(target => latest.get(target));
}

async function publish({ github, context, core }, options = {
  root: 'release-downloads', version: process.env.RELEASE_VERSION, tag: process.env.RELEASE_TAG,
  requested: JSON.parse(process.env.RELEASE_TARGETS), unityVersion: process.env.UNITY_VERSION,
}) {
  const repo = context.repo;
  let release;
  try { release = (await github.rest.repos.getReleaseByTag({ ...repo, tag: options.tag })).data; }
  catch (error) { if (error.status !== 404) throw error; }
  if (release && !release.draft) {
    core.info(`Release already published; leaving it unchanged: ${release.html_url}`);
    return;
  }
  const packages = collectPackages(options.root, context, options);
  if (!packages.length) { core.info('No successful requested targets; no Release published.'); return; }
  const missing = options.requested.filter(target => !packages.some(pkg => pkg.target === target));
  const workflow = `${context.serverUrl}/${repo.owner}/${repo.repo}/actions/runs/${context.runId}`;
  const body = [
    `Source: ${context.sha}`, `Unity: ${options.unityVersion}`, `UTC date: 20${options.version.split('.')[1]}-${options.version.split('.')[2].slice(0, 2)}-${options.version.split('.')[2].slice(2)}`,
    `Successful targets: ${packages.map(pkg => pkg.target).join(', ')}`,
    `Missing/failed targets: ${missing.join(', ') || 'none'}`, `Workflow: ${workflow}`,
  ].join('\n\n');
  if (!release) {
    release = (await github.rest.repos.createRelease({
      ...repo, tag_name: options.tag, target_commitish: context.sha,
      name: `Foobbuteo ${options.version}`, body, draft: true, make_latest: 'false',
    })).data;
  }
  const assets = await github.paginate(github.rest.repos.listReleaseAssets, { ...repo, release_id: release.id, per_page: 100 });
  const manifests = packages.map(({ archive, ...manifest }) => manifest);
  const metadata = [
    { name: 'SHA256SUMS.txt', data: Buffer.from(packages.map(pkg => `${pkg.sha256}  ${pkg.file}\n`).join('')) },
    { name: 'build-manifest.json', data: Buffer.from(JSON.stringify({ version: options.version, sha: context.sha, runId: String(context.runId), requested: options.requested, missing, builds: manifests }, null, 2) + '\n') },
  ];
  // Only drafts are mutable. Failed uploads can be resumed without touching a public Release.
  for (const asset of assets) {
    await github.rest.repos.deleteReleaseAsset({ ...repo, asset_id: asset.id });
  }
  for (const pkg of packages) {
    await github.rest.repos.uploadReleaseAsset({ ...repo, release_id: release.id, name: pkg.file, data: fs.readFileSync(pkg.archive), headers: { 'content-type': 'application/zip' } });
  }
  for (const file of metadata) {
    await github.rest.repos.uploadReleaseAsset({ ...repo, release_id: release.id, ...file, headers: { 'content-type': file.name.endsWith('.json') ? 'application/json' : 'text/plain' } });
  }
  const published = (await github.rest.repos.updateRelease({ ...repo, release_id: release.id, body, draft: false, make_latest: 'false' })).data;
  core.info(`Release published: ${published.html_url}`);
  core.summary.addRaw(`[Download Foobbuteo ${options.version}](${published.html_url})\n\nSuccessful: ${packages.map(pkg => pkg.target).join(', ')}. Missing/failed: ${missing.join(', ') || 'none'}.`);
  await core.summary.write();
}

module.exports = { collectPackages, publish };
