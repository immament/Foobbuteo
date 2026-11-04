# Contributing to Foobbuteo 2D

Thank you for helping improve Foobbuteo 2D. Bug reports, documentation fixes,
tests, gameplay improvements, and focused refactors are welcome.

By participating, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).
Please report security issues privately as described in the
[Security Policy](SECURITY.md), rather than opening a public issue.

## Before you start

- Search existing issues and pull requests before starting work.
- Open an issue before making a significant gameplay, networking, persistence,
  or architecture change. This gives maintainers and contributors a chance to
  agree on the direction before substantial work begins.
- Keep changes focused. Unrelated cleanup is easier to review in a separate
  pull request.

Small bug fixes, tests, and documentation corrections generally do not require
an issue first.

## Development setup

Foobbuteo 2D currently uses Unity `6000.6.8f1`. Follow the setup instructions
in the [README](README.md#getting-started). The local mock setup needs no database
or Footstar account. See [configuration](Documentation/Configuration.md) for optional
SQL Server integration and additional Editor/client settings.

Create a branch from the repository's default branch. Use a short descriptive
name such as `fix/pass-interception` or `feat/lobby-filtering`.

Never commit:

- `appsettings.json`, connection strings, access tokens, or other credentials;
- generated Unity directories such as `Library`, `Temp`, `Logs`, or local
  `UserSettings`;
- generated IDE project files unless the repository explicitly starts tracking
  them; or
- third-party assets whose redistribution terms are unknown or incompatible.

## Making changes

Follow the existing project structure and keep responsibilities separated:

- `Assets/Scripts/Client` contains client networking, presentation, UI, and
  playback.
- `Assets/Scripts/Server` contains authoritative state, persistence,
  authorization, and turn processing.
- `Assets/Scripts/SharedAssembly` contains shared models, contracts, commands,
  and network state.
- `Assets/Scripts/Main` contains application bootstrap and role selection.

When changing Unity assets, include their matching `.meta` files. Move and
rename assets inside the Unity Editor when practical so GUID references remain
valid. Avoid broad scene, prefab, or serialized-asset rewrites that are not
required by the contribution.

Match the style of nearby C# code and the repository's `.editorconfig`. Prefer
clear names and small, reviewable changes over speculative abstractions. Use
Conventional Commits where practical, for example:

```text
feat(game): add pass interception preview
fix(server): reject commands from unclaimed players
docs: clarify dedicated server setup
```

## Tests and manual verification

Add or update Edit Mode tests for gameplay, networking, authorization,
persistence, and other testable logic. Run the suite from **Window → General →
Test Runner** in Unity and report the result in the pull request.

Also test the affected user flow in Play Mode. Depending on the change, verify
host mode, separate client/server roles, a dedicated-server build, or WebGL.
The committed [release workflow](.github/RELEASE_BUILDS.md) runs workflow-script
tests and focused build-versioning Edit Mode tests for requested release builds.
It does not run the full gameplay/UI suite or validate pull requests, so include
a clear record of local verification.

If a relevant test cannot be run, explain why and describe the alternative
checks you performed.

## Pull requests

Before opening a pull request:

1. Rebase or merge the latest default branch as appropriate for your workflow.
2. Review the diff for credentials, generated files, unrelated formatting, and
   accidental Unity serialization changes.
3. Run the relevant automated and manual checks.
4. Update documentation when behavior, setup, configuration, or controls
   change.
5. Confirm that new dependencies and assets have compatible licenses and that
   required notices are included.

In the pull request, describe:

- what changed and why;
- any linked issue;
- how the change was tested;
- screenshots or recordings for visible changes; and
- known limitations, follow-up work, or migration steps.

Maintainers may ask for changes to keep the project secure, maintainable, and
consistent with its architecture. Reviews should be constructive and must
follow the Code of Conduct.

## Licensing

By submitting a contribution, you agree that it may be distributed under the
repository's [GNU AGPL version 3 license](LICENSE) and applicable additional
permissions. Do not submit code, artwork, fonts, audio, or other material unless
you have the right to contribute it under compatible terms.

Review [ASSET_RIGHTS.md](ASSET_RIGHTS.md),
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), and
[DEPENDENCY_LICENSES.md](DEPENDENCY_LICENSES.md) before adding or modifying
licensed material.
