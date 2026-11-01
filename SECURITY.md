# Security Policy

## Supported versions

Foobbuteo 2D is under active development and has not reached a production-ready
release. Security fixes are applied to the latest code on the repository's
default branch. Older commits, forks, unofficial builds, and abandoned release
artifacts are not supported.

The development authentication shortcuts mentioned in the README are not
intended for public deployment.

## Reporting a vulnerability

Please do not disclose a suspected vulnerability in a public issue, discussion,
pull request, or chat.

Use GitHub's private vulnerability reporting option on the repository's
**Security** tab when it is available. Include:

- the affected component and revision or build;
- steps to reproduce the issue or a minimal proof of concept;
- the impact and conditions required for exploitation;
- any suggested remediation; and
- whether the issue has been disclosed anywhere else.

If private vulnerability reporting is unavailable, use the repository owner's
contact options on the [maintainer's GitHub profile](https://github.com/immament).
As a last resort, open a public issue asking the maintainer to establish a
private channel. Do not include vulnerability details, secrets, personal data,
or exploit code in that issue.

Reports made in good faith will be handled respectfully. The maintainer aims to
acknowledge a report within seven days and provide an initial assessment or
request for more information within fourteen days. Resolution time depends on
severity and complexity. Please allow a reasonable remediation window before
public disclosure and coordinate disclosure timing with the maintainer.

## Scope

Security reports may include, but are not limited to:

- authentication or authorization bypasses;
- ways for a client to modify server-authoritative game state;
- unsafe RPC handling, denial-of-service vectors, or rate-limit bypasses;
- SQL injection, credential exposure, or insecure persistence behavior;
- remote code execution, unsafe deserialization, or path traversal;
- unintended disclosure or modification of player data; and
- vulnerable dependency usage with a demonstrated impact on this project.

Gameplay bugs, balance issues, crashes without a security impact, and general
support requests should be reported through the normal public issue tracker.

## Sensitive configuration

Never commit a real `appsettings.json`, database credentials, access tokens, or
private keys. Use `appsettings.tmpl.json` as the public configuration example.
If a secret is accidentally committed, revoke or rotate it immediately; deleting
it from a later commit is not sufficient.

Operators are responsible for replacing development authentication shortcuts,
restricting database and server access, using appropriate transport security,
and keeping Unity, packages, the operating system, and deployment dependencies
patched before exposing a build to untrusted networks.
