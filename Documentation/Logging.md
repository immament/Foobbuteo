# Logging

[Back to the README](../README.md) · [Configuration](Configuration.md)

`ImmLogger` resets to global `Info` and per-type `Info` at runtime startup. `Level` caps every logger;
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
  "logLevel": "Debug",
  "defaultSubLogLevel": "Debug",
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

`logLevel` sets the global cap; `defaultSubLogLevel` sets the fallback for types without
configured categories. Both accept named levels (case-insensitive), including `None`.
Legacy root `LogLevel` / `DefaultSubLogLevel` in server `appsettings.json` remain supported,
including Editor hosts; nested logging values take precedence over those legacy keys.
Server appsettings deserialize `logging` into `LoggingSettings`; invalid levels or categories
are rejected during deserialization, before configuration is applied.

For server file output, set `logging.logDirectory` in `appsettings.json` (for example,
`"logDirectory": "../Logs"`). The log writer reads `ServerConfig.Config.Logging.LogDirectory`
directly. Missing, null, empty, or whitespace-only directories disable file logging; no directory
is created and no file logging callbacks are registered. The legacy root `LogDirectory` is no
longer used. Relative paths remain relative to the server executable directory. This server-only
setting is not a browser logging override.

Browser overrides merge by field/category above the file defaults, and Editor root overrides
remain highest priority regardless of load order. Omitted fields/categories retain earlier values.
For example, a logging-only message is accepted even while connected:

```javascript
unityInstance.SendMessage(
  "AppController",
  "ReceiveClientConfig",
  JSON.stringify({
    logging: {
      logLevel: "Trace",
      defaultSubLogLevel: "Debug",
      categories: { Auth: "Trace" },
    },
  }),
);
```

Invalid logging settings reject the message without partially applying levels or categories. Normal
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
