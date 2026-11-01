# Dependency and asset licenses

This file summarizes license obligations for third-party materials used by
Foobbuteo 2D. The accompanying license texts govern each component.
Dependency versions are recorded in `Packages/packages-lock.json` and embedded
`Packages/*/package.json` files; the Unity Editor version is recorded in
`ProjectSettings/ProjectVersion.txt`.

## Project code and artwork

Project-authored code is licensed under [AGPL-3.0-only](LICENSE), with the
additional permission in [UNITY-LINKING-EXCEPTION.md](UNITY-LINKING-EXCEPTION.md).
Keep the applicable copyright, license, additional-permission, and warranty
notices when redistributing it. Modified source must carry modification notices
and dates. Distributing binaries requires providing Corresponding Source by a
method allowed by AGPL section 6. Modified versions supporting remote network
interaction must prominently offer users their Corresponding Source under
section 13. Preserve or provide interactive legal notices where section 5
requires them.

Artwork and separately licensed materials are governed by their own terms.
See [ASSET_RIGHTS.md](ASSET_RIGHTS.md) for the FootStar artwork restrictions and
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for vendored MIT software notices.

## Liberation Sans

The font at `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` and its derived
TextMesh Pro font assets are covered by the SIL Open Font License 1.1.
The copyright notices and full license are in
[LiberationSans - OFL.txt](Assets/TextMesh%20Pro/Fonts/LiberationSans%20-%20OFL.txt).

Redistributed copies, including modified font software, must include those
copyright notices and the OFL text in an accessible form and remain under the
OFL. Do not sell the font by itself. Modified fonts must respect the Reserved
Font Names listed in the notice unless the relevant copyright holder grants
permission. Do not use the holders' or authors' names to endorse modified fonts
without permission. See the [OFL conditions](https://openfontlicense.org/open-font-license-official-text/).

## Unity materials

Unity packages, other TextMesh Pro essential resources, and the Unity-derived
WebGL template at `Assets/WebGLTemplates/DefaultCustom` remain subject to their
applicable Unity licenses and third-party notices. The project's AGPL license
and linking exception do not grant additional rights to redistribute them.

Check the terms supplied with each component before redistributing its source
or binaries:

- The [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license)
  requires use in connection with content authored or distributed under a valid
  Unity Engine license. Section 5 requires the license and associated copyright
  notice with substantial portions or derivatives, allowing a customary notice
  location where inclusion is impracticable. Accompanying third-party terms
  also apply.
- The [Unity Package Distribution License](https://unity.com/legal/licenses/unity-package-distribution-license)
  limits distribution to binaries integrated with project content under its
  terms. It does not grant a general right to publish package source.
- Components supplied under Unity's Terms of Service or another license must
  follow those particular terms. Unity branding has separate trademark limits.

Package license and notice files are normally found in the resolved
`Library/PackageCache`, the Editor installation, or embedded `Packages`
directories. Preserve the notices applicable to materials actually included
in a source distribution or build, including transitive components.
Editor-only packages fetched by recipients do not need to be bundled with a
player release merely because they appear in the dependency manifest.

Unity Transport is embedded in `Packages/com.unity.transport`. Its committed
[license notice](Packages/com.unity.transport/LICENSE.md) identifies the Unity
Companion License and copyright © 2023 Unity Technologies; retain it when
redistributing that package and include the applicable license text.

## NuGet libraries

`System.Data.SqlClient` and its resolved `org.nuget.*` dependencies use the MIT
license. Preserve the upstream copyright notices and MIT permission text with
redistributed copies or substantial portions, including binaries. See the
[MIT license condition](https://opensource.org/license/mit).

The embedded [converter-generated license](Packages/org.nuget.system.data.sqlclient/License.md)
contains placeholder copyright fields. Obtain the actual notices from the
corresponding upstream packages before redistribution; a generic attribution
is not a substitute for their copyright notices.

## Publication and release checks

- Retain the license and attribution files accompanying committed third-party
  materials, and confirm permission to publish those materials as source.
- For each build, include the required license texts and notices for the
  components it distributes. A package inventory or a link to this summary
  alone does not replace notices that must accompany a copy.
- Provide Corresponding Source and the required AGPL notices for the version
  distributed or offered over the network, as applicable.

The resolved Unity package license files were unavailable during this cleanup.
Their exact terms and bundled third-party notices still require review before
redistributing those packages; this summary is not a completed release audit.
