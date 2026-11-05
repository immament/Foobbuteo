# Footstar client authentication

Android and WebGL clients can log in through the UI Toolkit form. Credentials go
to the Footstar website over HTTPS; the website validates them and issues a game
token in the existing database-backed format. The Unity server receives only that
token through connection approval and `foobbuteo_api.consume_login_token`.

For eligible manager teams and MvM creation/joining, see
[MvM database contracts](MvM-Sql-Contract.md).

Website endpoints and database changes are external prerequisites. This repository
does not implement or deploy them.

## Build configuration

Public HTTPS authentication URLs are bundled from the `dev` and `prod` sections
of `Assets/Resources/ClientSettings.json`. Update the relevant environment before
building when deployment URLs change. Each section uses these fields:

```json
{
  "webSessionTokenUrl": "https://<footstar-host>/<website-session-token-endpoint>",
  "loginUrl": "https://<footstar-host>/<login-endpoint>",
  "refreshUrl": "https://<footstar-host>/<refresh-endpoint>",
  "revokeUrl": "https://<footstar-host>/<revoke-endpoint>",
  "signupUrl": "https://<footstar-host>/default.asp"
}
```

Build profiles do not select the client environment. Both environments are
bundled in the same artifacts; changing their URLs still requires rebuilding.
Editor authentication uses the active profile in ignored `editor.settings.json`;
see [Editor configuration](Configuration.md#configuration-files).

API URLs cannot contain credentials, query parameters, or fragments. The HTTPS signup
link may contain a query or registration fragment, but no credentials. URLs are bundled
into the client; incoming browser configuration cannot change them. Missing or invalid
endpoint URLs prevent authentication requests to those endpoints. Do not add
passwords, tokens, or database connection strings to this file.

## Runtime environment selection

### WebGL hosting page

The hosting server must render an explicit environment before loading Unity:

```html
<script>
  window.foobbuteoClientConfig = {
    environment: "prod",
  };
</script>
```

Render `'dev'` on the development server. The custom template forwards this object
through `unityInstance.SendMessage('AppController', 'ReceiveClientConfig', JSON.stringify(...))`
after Unity loads. A custom hosting page may call that receiver directly. There
is no hostname mapping or default environment in the shared template/build.
Deploy the same WebGL artifacts to both servers and update both hosting pages
alongside deployment; old pages that omit the environment cannot authenticate.

Unity displays the lobby background with centered Foobbuteo, Loading ..., and a
spinner while waiting. An environment-only message initializes authentication and
displays the login form. A complete token message connects without showing the form.
The environment is locked after selection until page reload. Later connection
messages may omit `environment` or repeat the selected value. Supplying any of
`token`, `host`, `port`, `webSocketPath`, or `useEncryption` requires valid
connection transport fields: host, port, and WebSocket path;
encryption retains its existing default/validation behavior. An absent, null, or
empty token displays login when the rest of startup configuration is valid.
A nonempty token starts authentication. For example:

```javascript
unityInstance.SendMessage(
  "AppController",
  "ReceiveClientConfig",
  JSON.stringify({
    environment: "prod",
    token: "<single-use-game-token>",
    host: "<game-server-hostname>",
    port: 443,
    webSocketPath: "/game",
    useEncryption: true,
  }),
);
```

The entire message is validated before selecting settings or connecting. Invalid
messages report a configuration error and can be corrected by sending a valid
message. Messages received during authentication or networking are discarded.
Messages received before initialization are retained and processed afterward.
Incoming configuration selects bundled URLs; it cannot replace them.

### Android developer override

Android defaults to `prod`. Developers can place this optional file at
`Path.Combine(Application.persistentDataPath, "client-environment.json")`:

```json
{ "environment": "dev" }
```

The startup log prints the resolved override path. On most Android devices it is
`/storage/emulated/0/Android/data/<application-identifier>/files/client-environment.json`;
use the logged value instead of assuming that layout. This is app-specific external
storage, and modern Android restricts access by other apps and file managers. Use
a developer device with suitable ADB access; do not request broad storage permissions
just to support this override. See [Unity persistentDataPath](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-persistentDataPath.html)
and [Android app-specific storage](https://developer.android.com/training/data-storage/app-specific).

Read the file once at startup. Restart the app after editing it; deleting it
restores `prod` on the next launch. Only exact `prod`/`dev` names are accepted.
The optional UI diagnostics flag is described under
[UI layout diagnostics](Configuration.md#ui-layout-diagnostics).
No URLs or credentials belong in this file. Existing but unreadable,
malformed, or invalid files block authentication with a configuration error rather
than silently switching to production. There is no environment-selector UI.

Remembered Android sessions carry their environment. A legacy session without it,
or one belonging to another environment, is cleared before authentication can refresh
it. Switching environments therefore requires signing in again; a token is never
sent to the other environment's refresh or revoke endpoint. Other native players
use production without reading the Android override.

The Android secure-storage bridge ships as a Java source plugin. If minification
is enabled, include the shipped `Assets/Plugins/Android/proguard-user.txt` keep rule
in the Gradle configuration. Remember Me is always requested on Android and has no
visible checkbox. WebGL form login never requests remembered credentials.

## HTTPS request and response contract

All requests use POST with UTF-8 `application/x-www-form-urlencoded` bodies.
UnityWebRequest uses the platform certificate trust store, disables redirects,
and applies a 15-second timeout. Login and refresh responses must be JSON; the client
rejects JSON strings longer than 16,384 characters. The same limit applies to the
website-session token endpoint below. For credential login, refresh, and revoke,
the website must ignore browser cookies. There is no embedded client secret or API key.

| Operation | Body                                                     | Success              |
| --------- | -------------------------------------------------------- | -------------------- |
| Login     | `login=<encoded>&password=<encoded>&rememberMe=0` or `1` | HTTP 200 JSON        |
| Refresh   | `refreshToken=<encoded>`                                 | HTTP 200 JSON        |
| Revoke    | `refreshToken=<encoded>`                                 | HTTP 204, empty body |

Passwords are sent exactly as entered. Form encoding preserves spaces, `+`, `&`,
`=`, and Unicode. The UI limits login to 256 characters and password to 1024;
the website must enforce bounded request sizes and field validation too. The
password field is cleared on submission. Credentials and request/response bodies
are never logged by the client.

### WebGL website-session token endpoint

The `webSessionTokenUrl` field in each bundled environment selects the website-session
token endpoint. An empty or invalid URL prevents requests to it.
This is a separate endpoint from the three credential-based operations above.
It accepts POST with an empty form body and authenticates using the existing website
session cookie. It must share the game HTML page's origin (scheme, hostname, port);
Unity build files may be served from a CDN. The cookie path must cover this endpoint.
UnityWebRequest uses browser networking, so the browser attaches applicable cookies;
Unity never reads the cookie or receives a website refresh token.

For a valid, eligible website session, create and save a fresh database-backed
single-use game token and return HTTP 200 with application/json:

```json
{
  "token": "<single-use-game-token>",
  "tokenExpiresAtUtc": "2026-10-27T12:00:50Z"
}
```

Return only these two fields. Apply the same secret and 60-second expiry rules as
normal game tokens. Unity reuses host, port, WebSocket path, and encryption settings
from the accepted ReceiveClientConfig message. Do not return a refresh token.
Use HTTP 401 with `{"error":"invalid_session"}` when website authentication has
expired or been revoked, 429 with `rate_limited`, and 503 with `service_unavailable`.
Never redirect to an HTML login page. Require an exact allowed Origin on browser POSTs
and reject cross-site requests as CSRF protection. Use Secure/HttpOnly session cookies
with an appropriate SameSite policy and return Cache-Control: no-store on every response.

Only sessions started with a JavaScript-supplied token use this endpoint. The presence
of a website cookie must never enable recovery after a WebGL login-form login.
WebGL has no Logout button. No JavaScript recovery bridge is required.

### Credential login and refresh responses

Successful login without Remember Me:

```json
{
  "token": "<single-use-game-token>",
  "expiresAtUtc": "2026-10-02T12:01:00Z",
  "host": "<game-server-hostname>",
  "port": 443,
  "webSocketPath": "/game",
  "useEncryption": true
}
```

Remembered login and refresh add the following fields to the same object:

```json
{
  "refreshToken": "<random-remembered-session-secret>",
  "refreshExpiresAtUtc": "2026-12-31T12:00:00Z"
}
```

Use strings for secrets/dates, an integer for port, and a Boolean for encryption.
Dates must be ISO 8601 UTC strings ending in `Z`. Game tokens must expire 60 seconds
after issuance. The client rejects expired tokens or expiry more than 61 seconds
in the future, allowing one second of clock tolerance; client and website clocks
must be synchronized. Secrets must be nonempty, have no whitespace/control
characters, and be at most 256 characters to match the existing SQL parameter limit.

Refresh fields must be absent for `rememberMe=0`; WebGL always sends that value.
For `rememberMe=1`, create a session with a fixed 90-day expiry. Every refresh
atomically replaces the secret and returns the same expiry instant. The client
rejects reused secrets or extended expiry. Return a fresh one-time game token
on every login/refresh; never use a refresh secret as a game token.

Errors use `Content-Type: application/json`:

| Status | Body                              | Client behavior                                                         |
| ------ | --------------------------------- | ----------------------------------------------------------------------- |
| 401    | `{"error":"invalid_credentials"}` | Generic invalid login/password message                                  |
| 401    | `{"error":"invalid_session"}`     | Delete remembered session; show form                                    |
| 429    | `{"error":"rate_limited"}`        | Show wait message; retain credentials; background recovery uses backoff |
| 503    | `{"error":"service_unavailable"}` | Show retry message; retain remembered session                           |

Return JSON directly, without HTML login pages or redirects. Revocation must be
idempotent and return 204 for an already revoked session. Set `Cache-Control:
no-store` on all responses. Enable CORS only for deployed WebGL origins, allowing
POST and Content-Type/Accept/Cache-Control headers, including OPTIONS preflight.

## Website and database requirements

- Generate independent game/refresh secrets using a cryptographically secure random
  generator, with at least 256 random bits for refresh secrets. Store only refresh
  hashes with session family, user, fixed expiry, and revocation state.
- Rotate refresh secrets atomically. Detect reuse and revoke the session family.
  Logout revokes the family, including rotated descendants. Reject expired,
  revoked, or ineligible-account sessions; revoke after password/security changes.
  Apply authentication throttling without revealing account existence.
- Game tokens must match the existing repository contract, expire after 60 seconds,
  and be consumed atomically exactly once. Existing private SQL development
  exceptions must not apply to API-issued tokens. Verify concurrent consumption
  cannot approve two clients in the deployment database.
- Do not log credentials or form bodies in website, proxy, or SQL tracing. Keep
  database access confined to stored-procedure permissions.

## Client lifecycle and recovery

Authentication and server connectivity are separate states. Startup shows loading
while authentication is pending, login when credentials are required, or an error
with Retry connection after a transient automatic-login failure. Approval, rejection, failed startup, transport failure, and
shutdown erase the game token from configuration and network payload while keeping
connection settings. A new token is required for every subsequent connection.

Android stores only the refresh secret and expiry, encrypted with AES-256-GCM using
a nonexportable Android Keystore key, fresh nonce, and authenticated context.
Ciphertext is written atomically to `Context.getNoBackupFilesDir()`, excluding it
from cloud backup and device transfer. Passwords/game tokens are never persisted
in PlayerPrefs or storage. Hardware-backed protection depends on the device;
Keystore uses the available platform protection without requiring biometrics.

Startup attempts refresh once without showing the login form. The replacement is saved before connecting.
Expired/invalid/unreadable sessions are deleted. Outages, service errors, and
timeouts retain local remembered state and offer explicit retry. After a successful
connection, unexpected disconnects trigger background recovery: an immediate attempt,
then delays of 2, 4, 8, 16, and at most 30 seconds with jitter. One request runs at a
time; recovery waits for transport shutdown, pauses in the background, and resumes
when foregrounded. Healthy connections are retained. If the response is lost after server rotation, a later
refresh may require password login; do not reuse a consumed secret to avoid that
prompt.

Storage failure clears local remembered state, attempts to revoke the issued
session, warns the user, and still allows the returned game token to connect.
Lobby/game presentation and identity remain visible while disconnected, with a red
dot and Disconnected text. Server-dependent actions are blocked and pending commands
are not replayed. Recovery restores the previous room and fetches current authoritative
state before gameplay resumes. Missing or inaccessible rooms fall back to the lobby.
WebGL form-login sessions remain disconnected without automatically reconnecting.
Machine-readable rejection reasons distinguish invalid tokens, temporary service
failures, replacement logins, and administrative kicks. Kicks, replacement logins,
and unknown explicit server rejections stop retries and preserve the displayed screen.
Definitively invalid refresh/website authentication opens the login form. Rejection
of a temporary game token preserves renewable credentials; a fresh token is needed.
An invalid initial JavaScript token without an established session opens login.

Android Log Out is in the profile menu opened by the lobby username. It clears local credentials/identity, cancels pending login and recovery, and shuts down
networking immediately. If server revocation fails the UI reports that it could
not be confirmed. Late responses cannot reconnect or save credentials after
logout. WebGL persists no authentication secret.
