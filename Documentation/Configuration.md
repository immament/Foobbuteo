# Configuration

[Back to the README](../README.md#getting-started) · [Logging](Logging.md)

## Server persistence

The server reads `appsettings.json` from the project root when the file is present. The real file is ignored by Git so credentials are not committed. Without the file, the server uses the mock repositories.

1. Copy `appsettings.tmpl.json` to `appsettings.json` when you need local overrides.
2. Set `DataAccess.Provider` to `Mock` for local development or `SqlServer` for a private database deployment. The template currently selects `SqlServer`; change it explicitly for mock mode.
   Set `DataAccess.LoadInitialGames` to `true` to populate the mock lobby with sample games. Mock data is kept in memory.
3. For SQL Server, inject the connection string through the `FOOBBUTEO_SQL_CONNECTION_STRING` environment variable. A local connection string in the ignored file is supported as a development fallback.
4. Never commit `appsettings.json` or real credentials.

`FOOBBUTEO_DATA_PROVIDER` can override the configured provider with `Mock` or `SqlServer`. The SQL adapter calls only the stored-procedure contracts in schema `foobbuteo_api`; this public repository intentionally contains no Footstar table names, schema, migrations, or procedure bodies. Production connections should use a trusted SQL Server certificate with `Encrypt=True;TrustServerCertificate=False` and an application login that can execute the API procedures but cannot access base tables.

The server template contains placeholder values only. Editor authentication uses randomly generated user tokens stored in ignored `editor.settings.json`. Editor authentication code is excluded from player and server builds. Optional mock users can be placed in `DataAccess.MockUsers` in the ignored local configuration; mock tokens are one-time tokens and must never be used as production credentials.

To exercise SQL authentication in the Editor, select `SqlServer` in `appsettings.json` and place a current one-time database token in the active `devAuth.users` profile in the ignored `editor.settings.json`. Local development approval is bypassed when the SQL provider is selected, so the token is validated and consumed by `foobbuteo_api.consume_login_token`.

## Configuration files

- `appsettings.json`: server settings, including the server role running in Editor. Copy `appsettings.tmpl.json` for local overrides; keep credentials out of Git.
- `editor.settings.json`: Editor-only `currentProfile`, `profiles`, and shared `devAuth` sections. Each configuration profile has a unique `name`, `authentication`, and `connection`. Created from `editor.settings.tmpl.json` on first use, generating development tokens for empty template user tokens. Existing files and tokens are not rewritten; old flat configuration and `FoobbuteoDevAuth.json` imports are no longer supported.
- `Assets/Resources/ClientSettings.json`: public authentication URLs in `dev` and `prod` sections. Both sections are bundled; never put secrets here. WebGL selects its environment from the hosting page at runtime. Android defaults to `prod` and supports a developer JSON override; other native players default to `prod`. Build profiles do not select the environment. See [runtime configuration](Footstar-Authentication.md#runtime-environment-selection).

Set `currentProfile` to `local` or `dev` to select a configuration by its exact name. The template defaults to `local` (`127.0.0.1:7777`, WebSocket path `/`, no encryption); `dev` uses the remote encrypted connection. The template currently uses the same Footstar HTTPS authentication URLs for all three profiles; their connection settings differ. `devAuth.hostProfile`, `devAuth.clientProfile`, and `devAuth.users` remain shared across configurations.

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

## UI layout diagnostics

Android and WebGL accept optional Boolean `uiLayoutDiagnostics` in their startup
configuration (see [runtime environment selection](Footstar-Authentication.md#runtime-environment-selection)).
It defaults to `false` and controls `UiLayoutDiagnostics` on
LobbyScene's `PanelRenderer` and GameScene's `MainUI`, including after scene changes.
Use `true` to enable layout snapshots; use `false` or omit it to disable them.

For the Android developer override (`client-environment.json`):

```json
{ "environment": "dev", "uiLayoutDiagnostics": true }
```

For a custom WebGL hosting page:

```javascript
unityInstance.SendMessage(
  "AppController",
  "ReceiveClientConfig",
  JSON.stringify({
    environment: "prod",
    uiLayoutDiagnostics: true,
  }),
);
```

The flag is locked with the startup environment. Later connection messages may
omit it or repeat the selected value. A different value is rejected; reload the
page or restart Android to change it. Diagnostics-only WebGL messages are not
startup configuration: the initial message still requires an environment.

## Multiplayer roles

Enter Play Mode from `Assets/Scenes/BootstrapScene.unity`. The Editor automatically starts the instance according to its active multiplayer role:

- **Host** — server and client in one process; the simplest local setup
- **Server** — server-only instance
- **Client** — connects using the selected Editor profile (`127.0.0.1:7777` for `local`)

When enabled, the developer network panel also provides start/stop controls. It is hidden by default.

For multiple local players, use Unity Multiplayer Play Mode to assign client/server roles to separate editor instances. After connecting, use the lobby to select a game and claim a player.
