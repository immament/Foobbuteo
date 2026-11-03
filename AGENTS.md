# Repository Guidelines

## Project and tools

- Use the Editor pinned in `ProjectSettings/ProjectVersion.txt`. Preserve the active profile unless changes are required; CLI updates do not authorize Editor upgrades.
- Under `Assets/Scripts`: Client owns presentation/client networking; Server owns authoritative gameplay/persistence/authorization; SharedAssembly owns contracts/models; Main owns startup/role selection. Client checks aid usability; server validation establishes trust.
- Assets: `Assets/{Scenes,Prefabs,Resources,Vendor,Settings/Build Profiles}`. Packages: `Packages/manifest.json`; Unity-wide configuration: `ProjectSettings`.
- Read referenced documentation only when relevant to the task; read the needed section rather than recursively loading reference trees.
- When asked to add or revise instructions, check related guidance for overlap or conflicts first; update the existing rule where possible and place new rules in the relevant section. Keep guidance concise and durable, without task history. Put repository rules in `AGENTS.md` and cross-project personal preferences in memory only when requested; avoid duplicating the same guidance in both.
- PowerShell scripts in `Build-Scripts`: `build-android.ps1`, `build-server.ps1`, and `build-web-{dev,fast,release}.ps1`. They use the pinned Editor; install its target build module.
- Scripts fetch/pull upstream with `--ff-only`; use `-SkipGitUpdate` for local-checkout builds. Outputs/logs: `../Foobbuteo-Builds`. Player builds reserve tracked `ProjectSettings/BuildVersion.json` counters, consumed even on failure/cancellation after reservation; never reset them. Details: `Build-Scripts/README.md`.

## Design and C#

- Do not introduce obsolete APIs. Use supported replacements compatible with the pinned Unity Editor and installed packages. When replacing obsolete APIs, follow official migration guidance and preserve intended behavior; do not merely suppress warnings.
- Prefer simple solutions within the existing architecture. Preserve behavior/contracts during refactoring unless changes are requested; add characterization tests where needed. Preserve unrelated edits; avoid unrelated cleanup.
- Apply Clean Code/SOLID/DDD pragmatically: cohesive responsibilities, domain names, composition over inheritance, reason/constraint comments. Add domain models/services, interfaces, or patterns for concrete complexity/reuse/test isolation; avoid speculative abstractions, excessive extraction, arbitrary method-size limits, and mandatory DDD layers. Deduplicate shared concepts, not unrelated behavior; keep invariants with their owners.
- Group related types by feature/domain within assembly boundaries; avoid generic Managers/Helpers/Utilities folders and deep hierarchies. Share genuinely reusable code; multiple consumers alone do not justify `SharedAssembly`. Use `.asmdef` boundaries where practical for dependencies/recompilation.
- When platform or Editor implementations require repeated or nested preprocessor directives, prefer separate platform/Editor files with shared logic kept together. Use partial classes for one Unity component or separate types/assemblies where responsibilities warrant them; keep compilation guards at file or assembly boundaries where practical. Keep simple local directives when splitting would add unnecessary complexity.
- Keep substantial rules independent of Unity/network/database infrastructure where practical. Use plain C# for algorithms, `MonoBehaviour` for lifecycle/presentation/input/integration, and `ScriptableObject` for configuration/shared assets, not global state.
- Prefer constructor injection in plain C# and explicit initialization/serialized references in Unity components. Wire dependencies at entry points; avoid service locators, excessive singletons, hidden dependencies, and unjustified DI frameworks.
- Own/clean up resources, subscriptions, callbacks, and async side effects explicitly, including Unity destruction, UI reload, and disabled Domain Reload. Handle boundary failures; never swallow exceptions. Avoid repeated work/allocations/lookups in `Update`/`FixedUpdate`. Preserve server/WebGL/mobile compatibility.
- Follow `.editorconfig`: UTF-8 C#, four spaces, trimmed trailing whitespace, 120-character target; `PascalCase` types/public members, `camelCase` locals/parameters, surrounding private-field style. Format changed C# only with CSharpier pinned in `.config/dotnet-tools.json`; use Prettier configuration for USS.
- Put each meaningful top-level type in its own matching `.cs` file; the primary `MonoBehaviour`/`ScriptableObject` filename must match exactly. Supplemental partial-class files may use responsibility/platform suffixes such as `TypeName.Editor.cs` or `TypeName.Android.cs`. Exceptions: private nested helpers, small enums/records exclusive to one class, cohesive small DTOs, test fixtures/local helpers, short examples/temporary prototypes. When uncertain, separate files.
- For `UnityEngine.Object`, use `== null`/`!= null`, not `is null`/`is not null`, to handle destroyed objects.
- Name unexplained runtime/test values; keep obvious literals and avoid value-restating names. Include useful units and reuse limits/identifiers/configuration keys. Use configuration/serialized fields for tunables, constants/enums for fixed rules/protocol values.
- Split independent validation rules with clear failures. Use locals or purpose-named condition/parsing helpers when clearer, even for one use; keep simple related checks inline. Preserve order, short-circuiting, and null safety.
- Preserve `.meta` files and move assets through Unity when practical.

## UI Toolkit

- Use lowercase kebab-case UXML names/USS classes and BEM `block-name__element-name--modifier-name`. Name identifiers, including C# types, for their full responsibility: general Settings remains Settings even with only volume.
- Keep names stable/unique within the queried container; template instances may repeat names. Add names for queries/identification, class modifiers for states/variants; preserve Unity's built-in names/classes.
- Use `#name` for unique-element styles, semantic classes for reusable styles; never duplicate a unique name as an identical class solely for styling. Keep rules in the owning stylesheet; share only for actual reuse.
- Apply naming rules to new identifiers. Rename existing ones only when requested or required by the task, updating UXML, USS, C# queries, and tests together.
- Prefer Data Binding with `INotifyBindablePropertyChanged`; separate application/business state from UI controllers and avoid redundant refresh callbacks.
- Start/resume periodic UI work/animations on visibility/active-state events; pause while hidden/inactive without polling visibility. Pause/release scheduled items on unbind/reload/disposal; bindings own visibility, controllers own scheduling.

## Tests and verification

- NUnit `[Test]` methods: `Assets/Tests_Editor`, `Feature.Test.cs`. Cover gameplay rules, authorization, networking, and persistence changes.
- Match production boundaries with `Client`, `Server`, `SharedAssembly`, or `Main` test folders and `Tests.Editor.<boundary>` namespaces; do not reorganize unrelated tests.
- Shared helpers: `Utils`/`Tests.Editor.Utils`; general methods in `TestUtils`, specialized reusable helpers in matching type files. Keep fixture-only helpers local; avoid hypothetical reuse.
- Use Arrange-Act-Assert and focused tests with scenario/outcome names. Multiple assertions may verify one outcome; small helpers/parameterized cases reduce repetition without hiding scenarios.
- Give every assertion an expected-behavior message with scenario context, not a restated expression. Use installed NUnit APIs: `Assert.IsTrue`/`Assert.IsFalse` for direct booleans when clearer, comparisons for expected/actual values.
- Keep tests deterministic/independent: no order dependencies, shared mutable state, real-time delays, or external services except explicit integration tests. Restore globals/clean up resources. Test observable behavior and meaningful boundaries/failures, not implementation details.
- Wait for UI bindings with `TestUtils.WaitForUi`, not fixed frame-count loops. Improve affected test methods without unrelated cleanup.
- Run Edit Mode tests via Test Runner/available Unity tooling. For affected Play Mode flows, load `Assets/Scenes/BootstrapScene.unity`; verify relevant host, separate client/server, dedicated-server, WebGL, or Android roles. Report omitted checks; focused tests do not establish device/full-build correctness.

## Delivery and security

- Do not commit or push unless explicitly requested. After every file-changing task, propose a focused Conventional Commit message based only on the actual diff; add a body when useful, including verification.
- PRs explain changes/reasons, link issues, record local automated/manual checks, include screenshots for visible changes, and note limitations/migrations.
- For GitHub release builds, version reservations, caches, and runner upgrades, read `.github/README.md`; preserve reservation tags and the pinned Editor version.
- Use `appsettings.tmpl.json` for local configuration. Never commit real `appsettings.json`, credentials/tokens/private keys, `UserSettings`, or generated `Library`, `Temp`, and `Logs`. Do not log secrets. Follow `SECURITY.md` for private vulnerability reports; verify asset/dependency licenses.

## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.
  If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
