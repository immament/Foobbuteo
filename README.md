<p align="center">
  <img src="Assets/ClientAssets/Images/fs_logo.png" alt="Footstar logo" width="640">
</p>

# Foobbuteo 2D

**Plan the move. Play the pass. Shape the match.**

Foobbuteo 2D is an open-source, turn-based multiplayer football tactics game built with Unity.
Spend action points to move players, pass, cross, shoot, and choose defensive roles while the
server validates and resolves each turn.

[![Release builds](https://github.com/immament/Foobbuteo/actions/workflows/release.yml/badge.svg)](https://github.com/immament/Foobbuteo/actions/workflows/release.yml)
[![Unity 6000.6.8f1](https://img.shields.io/badge/Unity-6000.6.8f1-222222?logo=unity&logoColor=white)](ProjectSettings/ProjectVersion.txt)

🚀 [Quick start](#getting-started) · ⚽ [How to play](#how-to-play) ·
📚 [Documentation](#documentation) · 🤝 [Contributing](#contributing)

The project is under active development and is not yet production-ready. Host mode runs in the
Unity Editor; separate clients, dedicated servers, Android, and WebGL are also supported.

## Current features

- ⚽ **Football tactics:** movement with and without the ball, low passes, crosses, shots, and defensive roles
- 🧠 **Turn-based decisions:** action points, skill-dependent ranges and costs, and adaptive bot formations
- 🥅 **Contested play:** possession battles, pass interception, and goalkeeper behavior
- 🌐 **Multiplayer:** lobby, game creation and joining, player claiming, and server-authoritative commands
- 🛠️ **Local development:** in-memory mock repositories, Editor authentication, and Edit Mode tests
- 💾 **Optional persistence:** SQL Server integration through a private database API

## Getting started

**No database or Footstar account is required for local Editor development with mock data.**

### Prerequisites

- Unity Editor **`6000.6.8f1`**, pinned in [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt)
- Git

Install target build-support modules in Unity Hub only when building for those platforms.
SQL Server is optional and requires a compatible private database API; its schema is not included.

### Run a local host

1. Clone the repository:

   ```bash
   git clone https://github.com/immament/Foobbuteo.git
   cd Foobbuteo
   ```

2. Open the repository root in Unity `6000.6.8f1` and allow Package Manager to restore dependencies.
3. Copy `appsettings.tmpl.json` to the ignored `appsettings.json`. In its `DataAccess` section, set:

   ```json
   "DataAccess": {
     "Provider": "Mock",
     "LoadInitialGames": true
   }
   ```

   The template currently selects `SqlServer`, so change the provider explicitly. If
   `FOOBBUTEO_DATA_PROVIDER` is set in your environment, it overrides this setting; use `Mock`
   or clear that override for this walkthrough.
4. The Editor creates the ignored `editor.settings.json` from its template on first use,
   generating local development tokens. Its default `local` profile connects to `127.0.0.1:7777`.
   If you already have this file, select `local` via `currentProfile` before starting Play Mode.
5. Open `Assets/Scenes/BootstrapScene.unity`. Use both client and server multiplayer roles
   (`ClientAndServer`) for a local host, then enter Play Mode. The Editor starts the selected role
   automatically.
6. Use the lobby to select a game and claim a player. Mock data stays in memory.

For multiple local players, use Unity Multiplayer Play Mode with separate client/server instances.
See [configuration and multiplayer roles](Documentation/Configuration.md) for profiles,
authentication, SQL Server, and environment overrides.

## How to play

Foobbuteo is controlled primarily with the mouse:

1. Select a player you control.
2. Choose an action such as run, low pass, high pass/cross, or shoot.
3. Double-click a valid destination on the pitch, within your player's available action points.

Action ranges and costs depend on player skills. Passes target zones on the pitch; possession
can be contested at the destination. Player modes let you choose between confrontation,
pass interception, and goalkeeper behavior.

Read the [full player guide](Documentation/How-To-Play.md) for each action, crossing rules,
possession contests, and player modes. It follows the instructions shown in the in-game help.

## Technology and architecture

Unity `6000.6.8f1` · URP 17.6 · Netcode for GameObjects 2.13 · Unity Transport 6.6 ·
Input System 1.20 · C# · optional SQL Server

See [Packages/manifest.json](Packages/manifest.json) for package versions.

```text
Assets/
├── Scenes/                Bootstrap, lobby, game, and development scenes
├── Scripts/
│   ├── Client/            Presentation, UI, client networking, and playback
│   ├── Main/              Application bootstrap and role selection
│   ├── Server/            Authoritative gameplay, persistence, and authorization
│   └── SharedAssembly/    Shared models, contracts, and network state
├── Tests_Editor/          Edit Mode test suite
├── Prefabs/               Networked gameplay and UI prefabs
├── Resources/             Runtime resources and settings
├── ClientAssets/          Client images and other presentation assets
├── Settings/Build Profiles/
│                          Android, Windows, Windows Server, and WebGL profiles
└── Vendor/                Third-party source and editor tools
```

The runtime flow is `BootstrapScene` → `LobbyScene` → `GameScene`. The server owns gameplay
state; clients handle presentation and turn playback.

## Tests and builds

Run the full Edit Mode suite from **Window → General → Test Runner** in Unity before opening
a pull request. It covers formations, bot turns, client state, command authorization,
RPC rate limiting, user registration, and other game behavior. Also verify affected flows in Play Mode.

The committed [release workflow](.github/workflows/release.yml) runs workflow-script tests and
focused `Tests.Editor.Main.BuildVersionTests` before requested WebGL/Windows Server builds.
It does **not** run the full gameplay/UI suite or build pull requests. See
[release-build documentation](.github/RELEASE_BUILDS.md) for triggers, setup, and limitations.

Build profiles are included for Android, Windows desktop, Windows dedicated server, and
WebGL desktop. Select a profile under **File → Build Profiles**, or use the
[PowerShell build scripts](Build-Scripts/README.md). Pass `-SkipGitUpdate` to build your current
local checkout without fetching or pulling. Player builds reserve version numbers even if they fail.

Android and WebGL support in-game Footstar login. WebGL requires runtime configuration from its
hosting page. Configure trusted authentication endpoints before deployment; see the
[authentication guide](Documentation/Footstar-Authentication.md).

## Documentation

| Guide | Contents |
| --- | --- |
| [How to play](Documentation/How-To-Play.md) | Controls, actions, possession contests, and player/goalkeeper modes |
| [Configuration](Documentation/Configuration.md) | Mock/SQL providers, Editor profiles, authentication, and multiplayer roles |
| [Logging](Documentation/Logging.md) | Log levels, categories, runtime overrides, and platform output |
| [Authentication](Documentation/Footstar-Authentication.md) | Android/WebGL login, runtime environments, and deployment checks |
| [Local builds](Build-Scripts/README.md) | PowerShell scripts, build modules, output paths, and version counters |
| [GitHub release builds](.github/RELEASE_BUILDS.md) | Workflow requests, release versions, downloads, and diagnostics |
| [Contributing](CONTRIBUTING.md) | Development conventions, verification, and pull requests |

## Contributing

Bug reports, documentation fixes, tests, and focused gameplay improvements are welcome.
Read the [contribution guidelines](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md).
Open an issue before significant behavior or architecture changes; small fixes can go straight
to a pull request. Conventional Commits are encouraged.

For bugs and feature requests, use the [issue tracker](https://github.com/immament/Foobbuteo/issues).
Security vulnerabilities should be reported privately according to the [Security Policy](SECURITY.md).

## License

Foobbuteo 2D's project-authored source code is licensed under the
[GNU Affero General Public License version 3](LICENSE), identified as
`AGPL-3.0-only`. The additional permission in
[`UNITY-LINKING-EXCEPTION.md`](UNITY-LINKING-EXCEPTION.md) permits that code to
be linked or combined with unmodified Unity components without placing those
components under the AGPL. Modifications to the Foobbuteo source code remain
subject to the AGPL.

Copyright © 2024–2026 Dariusz Juńczyk.

Project-specific FootStar artwork is covered by the publication authorization
described in [`ASSET_RIGHTS.md`](ASSET_RIGHTS.md) and is not automatically
covered by the source-code license. Generated files, Unity materials, fonts,
and third-party components remain subject to their own licenses and required
notices, documented in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) and
[`DEPENDENCY_LICENSES.md`](DEPENDENCY_LICENSES.md). Do not remove their license
or attribution files.
