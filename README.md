<p align="center">
  <img src="Assets/ClientAssets/Images/fs_logo.png" alt="Footstar logo" width="640">
</p>

# Foobbuteo 2D

Foobbuteo 2D is a work-in-progress, turn-based multiplayer football tactics game built with Unity. Instead of controlling a player in real time, you spend action points to move players, pass, cross, shoot, and choose defensive roles while the server validates and resolves each turn.

The project supports combined host mode, separate clients and servers, dedicated-server builds, and WebGL clients. It is being developed in the open, but is not yet considered production-ready.

[![Release builds](https://github.com/immament/Foobbuteo/actions/workflows/release.yml/badge.svg)](https://github.com/immament/Foobbuteo/actions/workflows/release.yml)

## Current features

- Turn-based matches with server-authoritative command processing
- Player movement, movement with the ball, low passes, high passes/crosses, and shots
- Action points and player skills that affect range and action cost
- Contested possession, pass interception, goalkeeper behavior, and defensive player modes
- Multiplayer lobby with game creation, joining, leaving, and player claiming
- Human- and bot-controlled players, including adaptive bot formations
- Host, client, and dedicated-server roles using Unity Netcode for GameObjects
- Desktop and mobile WebGL build profiles
- SQL Server integration for game and user data
- Editor tests for formations, bot turns, client state, authorization, RPC rate limiting, and user registration

## Technology

- Unity `6000.6.4f1`
- Universal Render Pipeline (URP) 17.6
- Unity Netcode for GameObjects 2.13
- Unity Transport 6.6
- Unity Input System 1.20
- Unity Multiplayer Play Mode
- C# and `System.Data.SqlClient`

See [`Packages/manifest.json`](Packages/manifest.json) for the complete package list.

## Getting started

### Prerequisites

- Unity Editor `6000.6.4f1`
- Git
- A SQL Server instance if you want to run the server or host against persistent data

Install the WebGL and Dedicated Server modules in Unity Hub if you intend to build those targets.

### Open the project

1. Clone the repository:

   ```bash
   git clone https://github.com/immament/Foobbuteo2D.git
   cd Foobbuteo2D
   ```

2. Open the repository root as a Unity project with Unity `6000.6.4f1`.
3. Allow Unity Package Manager to restore the packages.
4. Open `Assets/Scenes/BootstrapScene.unity`.

The editor bootstrapper can automatically load the bootstrap scene when entering Play Mode, even if you are working in another scene. This behavior is available from Unity's project menu.

### Configure the server

The server reads `appsettings.json` from the project root when the file is present. The real file is ignored by Git so credentials are not committed. Without the file, the server uses the mock repositories.

1. Copy `appsettings.tmpl.json` to `appsettings.json` when you need local overrides.
2. Leave `DataAccess.Provider` as `Mock` for public/local development, or set it to `SqlServer` for a private database deployment.
3. For SQL Server, inject the connection string through the `FOOBBUTEO_SQL_CONNECTION_STRING` environment variable. A local connection string in the ignored file is supported as a development fallback.
4. Never commit `appsettings.json` or real credentials.

`FOOBBUTEO_DATA_PROVIDER` can override the configured provider with `Mock` or `SqlServer`. The SQL adapter calls only the stored-procedure contracts in schema `foobbuteo_api`; this public repository intentionally contains no Footstar table names, schema, migrations, or procedure bodies. Production connections should use a trusted SQL Server certificate with `Encrypt=True;TrustServerCertificate=False` and an application login that can execute the API procedures but cannot access base tables.

The server template contains placeholder values only. Editor authentication uses randomly generated user tokens stored in ignored `editor.settings.json`. Editor authentication code is excluded from player and server builds. Optional mock users can be placed in `DataAccess.MockUsers` in the ignored local configuration; mock tokens are one-time tokens and must never be used as production credentials.

To exercise SQL authentication in the Editor, select `SqlServer` in `appsettings.json` and place a current one-time database token in the active `devAuth.users` profile in the ignored `editor.settings.json`. Local development approval is bypassed when the SQL provider is selected, so the token is validated and consumed by `foobbuteo_api.consume_login_token`.

### Logging

`ImmLogger` defaults to global `Trace` and per-type `Debug`. `Level` caps every logger;
`DefaultSubLogLevel` applies to types without configured category levels. These defaults
live in code; the former `FoobbuteoAppSettings` ScriptableObject is no longer needed.

Use lazy messages to skip expression evaluation and formatting when disabled:

```csharp
this.LogTrace(() => $"Screen: {Screen.width}x{Screen.height}, {Screen.currentResolution}");
```

Caller member/file/line information is preserved. Capturing factories can still allocate
delegates; in hot paths use `this.IsEnabled(ImmLogger.LogLevel.Trace)` before constructing
the message. Plain classes can use `ImmLogger.IsEnabled<MyType>(level)`. Existing eager
message methods remain supported. Instance logs use the runtime type; static generic
logs use their explicit type argument. Exceptions also respect the type level.

Add an optional logging section to server `appsettings.json`, each client `prod`/`dev`
section in `Assets/Resources/ClientSettings.json`, or the root of `editor.settings.json`:

```json
"logging": {
  "categories": {
    "Server": "Info",
    "Client": "Debug",
    "GameEngine": "Warn",
    "Auth": "Info"
  }
}
```

Tags and category names are case-insensitive. `Server`, `Client`, and `Auth` namespace
segments add matching tags automatically; the game engine and authentication repositories
also register their feature tags. Register additional tags with
`ImmLogger.AddType(typeof(MyType), "Auth")`, or use the logging categories array under a
base behaviour's Inspector **Dev options**. All instances of a type share its tags and
options. Repeated registration adds tags without replacing its configuration.

The strictest configured category wins (`Info` + `Debug` gives `Info`). Unconfigured tags
are ignored. Explicit `SetLoggerLevel`, `LoggerConfig.minLevel`, or Inspector levels override
the category result; the global level still caps output. Existing logger thresholds refresh
when configuration changes. In an Editor host, role files contribute to the same base
category map; the most recently loaded file wins for a shared category. Editor client
logging defaults use `dev` for the `dev` profile and `prod` for other profiles; authentication
and connection settings continue to use the selected Editor profile.

Browser overrides merge by category above the file defaults, and Editor root overrides
remain highest priority regardless of load order. Omitted categories retain earlier values.
For example, a logging-only message is accepted even while connected:

```javascript
unityInstance.SendMessage(
  "AppController",
  "ReceiveClientConfig",
  JSON.stringify({
    logging: { categories: { Auth: "Trace" } },
  }),
);
```

Invalid logging settings reject the message without partially applying categories. Normal
connection/environment messages retain their startup restrictions. Restart Play Mode to
reload Editor JSON; static logger defaults, overrides, and registrations reset even with
Domain Reload disabled.

`LoggerConfig.logUtcTime` adds UTC timestamps with millisecond precision (`...123Z`) and
is enabled by default in server players. `logGameTime` independently controls Unity game
time. Plain background-thread logs omit game time; Unity-object context/name logging must
run on the main thread. Initialization captures Unity's handler on the main thread before
scene startup; there are no Unity API calls in logger static constructors.

Windows server players enable virtual-terminal color processing for an attached console
without registry changes, falling back to plain text for redirected or unavailable output.
ANSI formatting and console interop compile only into server players. Editor rich text
compiles only in the Editor; WebGL, Android, and other clients use plain output and cache
only plain prefixes. The logger does not send ANSI colors to the browser console.
Editor output keeps rich text; daily files keep existing color stripping, rotation, and
immediate flushing. Buffering/asynchronous logging is deferred until profiling justifies
its additional crash-loss and lifecycle tradeoffs.

### Configuration files

- `appsettings.json`: server settings, including the server role running in Editor. Copy `appsettings.tmpl.json` for local overrides; keep credentials out of Git.
- `editor.settings.json`: Editor-only `currentProfile`, `profiles`, and shared `devAuth` sections. Each configuration profile has a unique `name`, `authentication`, and `connection`. Created from `editor.settings.tmpl.json` on first use, generating development tokens for empty template user tokens. Existing files and tokens are not rewritten; old flat configuration and `FoobbuteoDevAuth.json` imports are no longer supported.
- `Assets/Resources/ClientSettings.json`: public authentication URLs in `dev` and `prod` sections. Both sections are bundled; never put secrets here. WebGL selects its environment from the hosting page at runtime. Android defaults to `prod` and supports a developer JSON override; other native players default to `prod`. Build profiles do not select the environment. See [runtime configuration](Documentation/Footstar-Authentication.md#runtime-environment-selection).

Set `currentProfile` to `local` or `dev` to select a configuration by its exact name. The template defaults to `local` (`127.0.0.1:7777`, WebSocket path `/`, no encryption); `dev` uses the remote encrypted connection. Both initially use the dev HTTPS authentication URLs. `devAuth.hostProfile`, `devAuth.clientProfile`, and `devAuth.users` remain shared across configurations.

Each `devAuth.users` entry can optionally include `managedTeams` for Editor development authentication:

```json
"managedTeams": [
  {
    "key": { "sourceGame": "Footstar", "teamType": "Club", "teamId": 1234, "ageCategory": "Main" },
    "name": "My club"
  }
]
```

Use the real team ID when testing SQL-backed team loading. Enums accept names or numeric values:
`sourceGame`: `Footstar` (1); `teamType`: `Club` (0), `NationalTeam` (1), `CityTeam` (2);
`ageCategory`: `Main` (0), `U19` (19) for clubs, or `Main` (0), `U18` (18), `U21` (21) for national/city teams.
Omitting `managedTeams`, setting it to `null`, or using `[]` gives the user no managed teams.
These teams apply only when Editor development authentication handles approval; SQL authentication reads teams from the database.

Editor settings are reread at Play Mode startup, including when Domain Reload is disabled. The selected profile's `connection` is also reread before each Editor fallback connection. Login responses and browser-supplied connection settings take precedence. Changes to Editor JSON require no C# recompilation; restart Play Mode after switching profiles to apply authentication URLs and connection settings together. Invalid configuration reports an error rather than selecting another environment. Editor settings and tokens are outside `Assets` and are excluded from player builds.

MPPM virtual Editors under `Library/VP/<instance>` read the same `editor.settings.json` from the main project root. Root JSON files do not need to be copied into virtual projects, and refreshing a virtual project does not replace its configuration or tokens.

`UserListRefreshIntervalSeconds` in `appsettings.json` controls visible user-list polling (default: 10 seconds;
must be positive). Restart the server after changing it. Clients receive this public setting with server info.
Online users are available from the lobby; the room's Users tab also includes assigned players
who are elsewhere or disconnected. Administrator disconnect preserves claimed-player reservations and does
not ban the account. User-list network contracts require matching client and server builds.

### Run locally

Enter Play Mode from the bootstrap scene. In development builds, the on-screen network panel can start the instance as a:

- **Host** — server and client in one process; the simplest local setup
- **Server** — server-only instance
- **Client** — connects to `127.0.0.1:7777` from the Unity Editor

For multiple local players, use Unity Multiplayer Play Mode to assign client/server roles to separate editor instances. After connecting, use the lobby to select a game and claim a player.

## How to play

Foobbuteo is controlled primarily with the mouse:

1. Select a player you control.
2. Choose an action such as run, low pass, high pass/cross, or shoot.
3. Double-click a valid destination on the pitch.
4. Spend the player's available action points and submit the turn.

Action ranges and costs depend on player skills. Low passes can be intercepted, while high passes cannot be intercepted in flight but apply a penalty during the contest at their destination. Shots can be stopped by goalkeepers. The in-game help screens explain the current rules in more detail.

## Project structure

```text
Assets/
├── Scenes/                Bootstrap, lobby, game, and development scenes
├── Scripts/
│   ├── Client/            Client networking, presentation, UI, and playback
│   ├── Main/              Application bootstrap and role selection
│   ├── Server/            Server state, persistence, authorization, and turn processing
│   └── SharedAssembly/    Shared game model, contracts, commands, and network state
├── Tests_Editor/          Edit Mode test suite
├── Prefabs/               Networked gameplay and UI prefabs
├── Resources/             Runtime images, sprites, and settings
├── Settings/Build Profiles/
│                          Windows, Windows Server, and WebGL profiles
└── Vendor/                Third-party source and editor tools
```

The main runtime flow is `BootstrapScene` → `LobbyScene` → `GameScene`. Server-owned state is converted to network state for clients, while client presentation and turn playback remain separate from server turn processing.

Architecture sketches are available as PlantUML sources in [`Assets/Diagrams`](Assets/Diagrams).

## Tests

Run the Edit Mode suite from **Window → General → Test Runner** in Unity.

The current suite covers:

- adaptive formations and player AI
- bot turn processing
- client game-state updates
- player-command authorization
- server RPC rate limiting
- user registry behavior

There is not yet a committed CI workflow, so contributors should run the full Edit Mode suite before opening a pull request.

## Builds

Versioned Unity build profiles are included for:

- Windows desktop
- Windows dedicated server
- WebGL desktop: Development, Fast, and Release
- WebGL mobile: Development and Release

Select a profile under **File → Build Profiles**. Dedicated-server deployments also require an `appsettings.json` beside the built player. WebGL clients require an explicit `prod` or `dev` environment from the containing browser page. The page can also supply a complete host, port, WebSocket path, encryption preference, and authentication token, or the user can log in in-game.

## Contributing

Contributions and issue reports are welcome while the project is evolving. See
the full [contribution guidelines](CONTRIBUTING.md) and
[Code of Conduct](CODE_OF_CONDUCT.md) before participating. Report suspected
vulnerabilities privately according to the [Security Policy](SECURITY.md).

1. Open an issue for significant behavior or architecture changes.
2. Create a focused branch from the default branch.
3. Keep client, server, and shared assembly responsibilities separated.
4. Add or update Edit Mode tests for gameplay and networking logic.
5. Verify that no credentials, generated Unity folders, or local settings are included.
6. Open a pull request describing the change and how it was tested.

Please use Conventional Commits where practical, for example `feat(game): add pass interception preview`.

## Open-source readiness

Before treating a fork or release as fully redistributable, review these items:

- [x] Choose and add a root `LICENSE` for the project's original source code.
- [x] Audit the retained publishable components under `Assets/Vendor` and document their licenses and required notices.
- [x] Document the publication authorization and downstream licensing status of the FootStar artwork under `Assets/Resources`.
- [x] Remove the intentionally excluded `Assets/Vendor/ALTUSHKA` and `Assets/Vendor/ClassDiagramGenerator` directories before publication.
- [x] Audit fonts, Unity template assets, Unity packages, and embedded NuGet libraries.
- [x] Replace development authentication shortcuts before any public deployment.
- [x] Add a local mock persistence substitute while keeping the private database schema outside the repository.
- [ ] Add automated build and test workflows.
- [x] Add contributor guidelines, a code of conduct, and a security policy.

## Client login

Android and WebGL support an in-game Footstar login form. Android Remember Me uses
Keystore-protected storage and a revocable 90-day website session. Configure the
trusted HTTPS endpoints before building; see
[the authentication contract and deployment checks](Documentation/Footstar-Authentication.md).

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
