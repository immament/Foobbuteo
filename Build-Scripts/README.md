# Build scripts

Run these scripts from PowerShell. Each build fetches from Git and pulls the current
branch's upstream with `--ff-only` before reading the project's Unity version and
starting the build. A failed fetch or pull stops the build; divergent branches
must be resolved separately.

| Script | Build profile | Output under `../Foobbuteo-Builds` |
| --- | --- | --- |
| `build-web-dev.ps1` | Web - Desktop - Development | `web-dev/` |
| `build-web-fast.ps1` | Web - Desktop - Fast | `web-fast-dev/` |
| `build-web-release.ps1` | Web - Desktop - Release | `web-release/` |
| `build-server.ps1` | Windows Server | `server-release/Foobbuteo.exe` |
| `build-android.ps1` | Android™ | `android-release/foobbuteo.apk` |

Logs are written to `../Foobbuteo-Builds/BuildLogs/<build-name>.log` (the build name
is the output folder name). The script checks Unity's exit code, build report,
failure messages, and expected artifact; a failed validation returns a nonzero
exit code even if Unity exited with zero. Install the Editor pinned in
`ProjectSettings/ProjectVersion.txt` and its target build support module through
Unity Hub.

The entry scripts call the shared `build.ps1`, which also accepts a target directly:

```powershell
./Build-Scripts/build.ps1 -Target android
./Build-Scripts/build-web-dev.ps1 -SkipGitUpdate
```

Use `-SkipGitUpdate` on any entry script, or on `build.ps1`, to build the current
local checkout without fetching or pulling. For example:

```powershell
./Build-Scripts/build-android.ps1 -SkipGitUpdate
```

This skips only Git updates; the build still reserves a version as described below.

## Automatic build versions

Both these scripts and Unity Editor Player builds reserve a version automatically
before the build starts. `ProjectSettings/BuildVersion.json` is the Git-tracked
source for all profiles. Change only `major` manually for a major release.

Versions use `major.yy.MMdd.dailyBuildIndex`, for example `0.26.1027.3`.
The build computer's local date supplies the year, month and day. The daily index
is shared by Android, server and Web builds; it increments on the same date and
resets to `1` on a new date. Web profiles retain their existing `-d`, `-f` and `-r`
suffixes. The callback assigns `PlayerSettings.bundleVersion` and, for Android,
`PlayerSettings.Android.bundleVersionCode`.

Android builds also increment the separate integer `androidVersionCode`, keeping
it above both the tracked value and the current Android Player Settings value.
Other platforms leave this Android counter unchanged.

Failed or cancelled builds consume a number once the callback reserves it.
Include the updated counter file when committing release changes to preserve
numbering in Git; commit or push only when explicitly requested. The build does
neither automatically. Do not reset the tracked counters.
A missing or invalid configuration, or a local date earlier than `lastBuildDate`,
stops the build with an error. Builds from independent checkouts need the latest
counter file before building to avoid duplicate reservations.
