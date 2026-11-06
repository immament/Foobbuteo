# GitHub release builds

The release workflow builds the existing WebGL Release, Windows Server, and
`Android™.asset` profiles on standard `ubuntu-24.04` GitHub-hosted runners. Windows
Server uses the Windows Mono cross-compilation image; Android uses the Android
image and its non-development profile, not `Android-dbg.asset`.

## Setup and requests

1. Make the workflow and its scripts available on `main`, the currently allowed
   release branch. GitHub requires the workflow on the repository's default branch
   before it offers **Run workflow**; select `main` when starting a release run.
2. Configure repository Actions secrets `UNITY_LICENSE`, `UNITY_EMAIL`, and
   `UNITY_PASSWORD`. Verify successful Unity Personal activation on a hosted runner.
3. Allow GitHub Actions to create tags and Releases. Repository/organization policy
   must permit the workflow's `contents: write` permissions and the pinned actions.
   Tag rules must allow creation of `v*` and `ci-runs/*` tags by this workflow.
4. Keep the repository public and use standard runners for free hosted compute.
   Keep the repository Actions cache limit at **10 GB** (do not opt into paid cache
   capacity). Old caches can be evicted; a missing cache only makes a build slower.

Automatic runs start only for pushes to `main`, as configured by `push.branches`
in [release.yml](workflows/release.yml). The `allowedBranch` value in
[build-config.json](build-config.json) also restricts release requests, including
manual runs. To change the allowed branch, update both `push.branches` and
`allowedBranch` together. Pull requests and tag pushes do not trigger builds.

Use one of these exact markers in the **head commit message** of a push:

| Marker | Requested downloads |
| --- | --- |
| `[BUILD_WEBGL]` | WebGL Release |
| `[BUILD_SERVER]` | Windows dedicated server |
| `[BUILD_ANDROID]` | Android APK |
| `[BUILD_ALL]` | All three |

Combine individual markers to request those targets. Markers on earlier commits in a
multi-commit push are ignored. Alternatively, open **Actions → Release builds →
Run workflow**, select the allowed branch, and choose `webgl`, `server`, `android`, or `all`.
The workflow always builds the event's exact commit, even if newer commits arrive.

## Versions and retries

Release versions use `major.yy.MMdd.dailyBuildIndex`, for example `0.26.1103.1`,
without suffixes. `major` is configured in `build-config.json`; the date is UTC at
reservation time. The index starts at 1 each day and is shared by the targets in
one workflow run. Local development builds keep their separate local counter.

Android CI derives `versionCode` as days since 2020-01-01 multiplied by 10,000,
plus the reserved daily index (1–9,999). Codes increase across release dates and
indices, and retries reuse the same code even after midnight. Android CI does not
consume the tracked local Android counter. Keep the release major stable within
a UTC day: the reservation index is scoped to major/date, while Android codes are
scoped to date. Local and CI APKs use separate code sequences.

Before Unity runs, the workflow reserves an annotated `v<version>` tag pointing at
the source commit. A `ci-runs/<run-id>` tag points at the same annotated object for
durable retry lookup. These tags must not be deleted or moved: they are the version
ledger, including failed/cancelled builds. Nothing commits counters back to Git.

**Re-run all jobs** and **Re-run failed jobs** reuse the run's reserved version,
including after UTC midnight. Packages are separated by target/run/attempt and
retained for seven days. A failed-job retry can reuse the successful other target's
package from an earlier attempt. After artifacts expire, rerun all jobs or start a
new run. A new manual run always reserves a new version.

If a Release is already published, retries leave its files and notes unchanged.
For example, if only WebGL succeeded and was published, retrying Server will not
add it to that existing Release. Start a new run to publish a new Release with all requested targets.

## Downloads and failure handling

Successful builds appear under the repository's **Releases**, with complete
`Foobbuteo-WebGL-<version>.zip`, `Foobbuteo-Server-<version>.zip`, and/or
`Foobbuteo-Android-<version>.zip` downloads,
`SHA256SUMS.txt`, and `build-manifest.json`. Notes identify missing/failed targets,
the source commit, Unity version, UTC date, and workflow run.

One target failing does not cancel the others. Successful targets are published
while the overall workflow still reports the failure. No successful targets means
no published Release. Failed uploads leave a draft that can be resumed on retry.
Releases are published with `make_latest: false`; this workflow does not change
the Latest designation, so an older run cannot displace a newer release. Set Latest
manually if desired.

The server ZIP includes `appsettings.tmpl.json`. Copy it to `appsettings.json` next
to the executable and supply the deployment's settings. Unity directories ending in
`_BackUpThisFolder_ButDontShipItWithYourGame` are excluded from the server ZIP;
their original build files remain untouched. Actual local configuration,
license files and private-key/keystore files are rejected during packaging. Each
ZIP must be below GitHub's 2 GiB per-file limit. WebGL hosting/deployment is separate.

The Android ZIP includes `Foobbuteo.apk`. The existing profile disables development
builds, script debugging, and App Bundle export. Its default signing configuration
is preserved (`androidUseCustomKeystore: 0`); these are not store-signed APKs.

## Caching, tests and upgrades

Each target has its own Library cache, isolated by runner OS, exact Unity version
and target. Keys also include package/settings/profile hashes and source/run/attempt;
fallbacks reuse compatible older caches. Cache restore/save failures do not turn a
successful build into a failure. Matrix jobs run one at a time to avoid simultaneous
activations within a run; concurrent workflow runs still share the Personal account,
so avoid launching overlapping releases until license behavior is verified.

Workflow unit tests run before allocation. Focused Edit Mode
`Tests.Editor.Main.BuildVersionTests` run before each target build; failures block
that target. This is not the full gameplay/UI test suite. Diagnostics are retained
for three days, packages for seven days; finished Release assets are independent
of Actions artifact retention.

Unity is pinned by `ProjectSettings/ProjectVersion.txt`. The matching image version
and digest live in `build-config.json`; update both after verifying image availability
when upgrading the Editor. The workflow fails early if they disagree. Actions and
images are pinned; review updates rather than replacing pins with floating tags.

Local checks:

```powershell
node --test .github/scripts/*.test.cjs
python -m unittest discover -s .github/scripts -p test_package.py
```

Before relying on releases, verify on GitHub: Personal activation, each target's
complete cold build, a warm-cache build, download contents/checksums, retry version
reuse, and an intentional single-target failure that still publishes the other
target. Hosted compatibility and speedup are not established by local unit tests.
