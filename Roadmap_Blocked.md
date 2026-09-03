# LibreSpot Roadmap - Blocked Items

Items moved here from `ROADMAP.md` because they require operator decisions,
credentials, or policy calls that an implementer cannot resolve autonomously.
Return items to `ROADMAP.md` once the blocking decision is made.

Last updated: 2026-09-03.

Entries whose blocker was resolved or whose premise was overtaken by v4.0.0 stable, local-only releases, immutable release assets, the unsigned-by-design decision, or .NET 10 are kept under an `Archived` heading with the resolution, so the decision record survives without steering new work.

---

## P0 - Decide the fate of the collapsed legacy shell surfaces

| Field | Value |
|---|---|
| Source | preview.26 verification pass, 2026-08-21 |
| Blocker | Product/design decision - operator only |

`Simplify the desktop experience` (4dbded0) set `ShellWorkspaceHost` to
`Visibility="Collapsed"` and collapsed the top-bar brand block and the global
search box. The simplified Home, Maintenance, and Settings shell replaced them
without carrying three surfaces across:

- The pinned-asset provenance card (`ShellProvenanceItemsControl`, the
  `Vm_ShellProvenanceOpenSource` action) is unreachable.
- Global search is unreachable in both directions: the search box and the
  `GlobalSearchResultsPanel` are both collapsed, while `GlobalSearchText`,
  `GlobalSearchResults`, and their commands stay live in `MainViewModel`.
- About 1,600 lines of legacy shell XAML still ship inside the released binary.

The language selector was the fourth casualty and has already been restored to
the simplified rail, because five reviewed locales are unusable without it.

Decide one of two directions and record it here:

1. Port the provenance card and global search into the simplified shell, then
   delete the legacy host, or
2. Delete the legacy host, the global-search view-model members, and the
   `provenance` and `global-search` UI automation smoke states.

Until then `--uia-smoke=provenance` and `--uia-smoke=global-search` still start
the app and silently render Home, and `ScrollProvenanceIntoView` scrolls a
control nobody can see. The two smoke rows that asserted those surfaces were
removed from `WpfUiAutomationSmokeTests` in the same pass, so no test guards
them today.

## P0 - Rebrand decision

| Field | Value |
|---|---|
| Source | Next Release Queue |
| Blocker | Brand/naming decision - operator only |

Decide whether to keep LibreSpot, rename before package-manager distribution,
or keep the repo name but rename the app. Decision must be recorded before
winget/scoop/choco work begins.

Research note (2026-09-03): `gh search issues LibreSpot` returns only `librespot-org/librespot` (the Rust Spotify client) and its consumers; this project does not appear at all, so the name collision now measurably hides it from search.

## Archived - Signing (SignPath Foundation enrollment)

| Field | Value |
|---|---|
| Source | Next Release Queue |
| Resolution | Unsigned by design. SignPath Foundation OSS signing was evaluated and set aside; there is no enrollment in progress and no certificate to wait for. |

The evaluation and the answers that would be submitted are kept in
`SIGNPATH.md` for the record. `schemas/release-artifact-contract.json`
records `unsigned-by-design`, and release identity is proven with
`checksums.txt`, the release manifest, the SBOM, and the GitHub release
attestation. Reopen only if the unsigned-by-design decision is reversed.

## P0 - Finalize package identity before any public distribution manifest

| Field | Value |
|---|---|
| Source | Cycle 2 |
| Blocker | Naming/identity/branding decision - operator only |

Why: `winget search LibreSpot --source winget` found no existing Windows
package on 2026-06-04, but the broader `librespot` name is already an
established open-source Spotify client/library with distro and crates.io
package identity. The existing roadmap has a rebrand decision, but package
IDs, display names, executable names, protocol names, and support burden
need one concrete decision before winget/Scoop/Chocolatey/Velopack files
exist.

Evidence: local `winget search LibreSpot --source winget` on 2026-06-04,
https://github.com/librespot-org/librespot,
https://crates.io/crates/librespot,
https://github.com/microsoft/winget-pkgs,
`src/LibreSpot.Desktop/app.manifest:3`,
`SIGNPATH.md:3`

Touches: product decision record, package manifests, `SIGNPATH.md`, README,
shell integration docs, future protocol/file associations.

Acceptance: operator records one canonical identity set: display name,
package IDs, executable names, publisher string, config folder names,
protocol URI, and whether old `%APPDATA%\LibreSpot` paths stay forever or
migrate.

Verify: repeat winget search; search Chocolatey and Scoop; check GitHub and
crates.io name collision notes; review package manifests before first
submission.

## P1 - Define the Velopack app identity and update feed before packaging

| Field | Value |
|---|---|
| Source | Cycle 2 |
| Blocker | Package identity, install identity, and update channel decisions |

Why: distribution planning names Velopack, but the repo currently has no
Velopack package, app ID, update channel, or `RELEASES` feed. Velopack
1.2.0 is current, and its docs make the release feed the discovery point
for updates; identity and install location decisions must be settled before
the WPF shell moves from portable release asset to installed app.

Evidence: `ROADMAP.md:89`, the retired release automation,
`src/LibreSpot.Desktop/app.manifest:3`,
https://docs.velopack.io/distributing/overview,
https://github.com/velopack/velopack/releases/tag/1.2.0,
https://www.nuget.org/packages/Velopack/1.2.0

Touches: packaging docs, the local release procedure, WPF csproj, app manifest,
update check UX, installer/uninstaller docs.

Acceptance: a packaging design note chooses package ID, display name,
update channel names, GitHub Releases vs external feed hosting, install
root, Start Menu shortcut behavior, and state migration from portable
builds. Releases are unsigned by design, so no signature preservation rule
is needed.

Verify: after implementation, run `vpk pack` / `vpk upload` dry-runs in a
temp release folder and verify update discovery against a local feed.

## P1 - Define Velopack update ownership and state migration before the first installed WPF package

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | Package identity and update-channel ownership decisions |

Why: Velopack replaces the app's `current` directory on update and installs
under `%LocalAppData%\{packId}` by default. LibreSpot already stores config,
logs, crash reports, watcher state, and backups outside the app folder, but
shortcuts, protocol registration, and portable-vs-installed identity still
need explicit migration rules.

Touches: WPF project, app manifest, shortcut/protocol code, watcher
registration, support bundle, package docs.

Acceptance: Velopack design chooses `packId`, stable/preview channels,
feed hosting, `mainExe`, CLI sidecar behavior, shortcut names, protocol/file
association ownership, update check UI, and migration from portable
`LibreSpot-Desktop.exe` to installed WPF. It documents that user state lives
outside `current` and verifies hooks do not overwrite `%APPDATA%\LibreSpot`,
`%LOCALAPPDATA%\LibreSpot`, or `%USERPROFILE%\LibreSpot_Backups`.

Verify: local Velopack install/update/uninstall dry runs preserve config,
logs, watcher state, and backups; update checks against a local feed choose
the correct stable or preview channel.

## P1 - Decide the Windows support lifecycle after Windows 10 Home/Pro end of support

| Field | Value |
|---|---|
| Source | Cycle 4 |
| Blocker | OS support policy decision - operator must define supported vs best-effort |

Why: README requirements still say Windows 10/11, the WPF pitch promises
Windows 10 fallback for Windows 11 Mica, and the app manifest lists legacy
supportedOS GUIDs from Vista through Windows 10. Microsoft lifecycle data
says Windows 10 Home/Pro reached end of support on 2025-10-14. LibreSpot
also exposes legacy Spotify installer choices, so OS support, Spotify
target version support, and best-effort compatibility need separate labels.

Evidence: `README.md:26`, `README.md:40`,
`src/LibreSpot.Desktop/app.manifest:3`,
`src/LibreSpot.Desktop/app.manifest:7`,
https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro

Touches: README requirements, compatibility matrix, installer docs,
diagnostics, app manifest support notes.

Acceptance: operator records one support policy for Windows 11, Windows 10
Home/Pro after 2025-10-14, LTSC/ESU environments, Windows 7/8.1 Spotify
target versions, ARM64, and PowerShell 5.1/7 lanes. Docs distinguish
"supported host OS", "best-effort host OS", and "Spotify target version".

Verify: compatibility matrix and diagnostics report the same labels; WPF
and PowerShell startup warnings do not contradict README/package metadata;
release checklist requires one manual smoke test on each supported host OS.

## P1 - Build an alternative-client capability and compliance matrix before adding cards

| Field | Value |
|---|---|
| Source | Cycle 9 |
| Blocker | Legal disclaimer and support boundary approval - operator only |

Why: Spotube, Psst, and Ncspot are not interchangeable alternatives to the
patched Windows Spotify flow. Live GitHub checks on 2026-06-04 showed
Spotube as active with v5.1.1 published 2026-02-24 and 46k+ stars; Psst as
having 2026 commit activity while still describing itself as early and requiring Premium;
Ncspot as active with v1.3.4 published 2026-05-22 and Premium-only terminal
UX. Spotify's February 2026 developer-platform update added Premium and
user-count limits for Development Mode, and the Developer Policy restricts
streaming, replacement clients, branding, data use, and integrations with
content from another service. LibreSpot needs a factual matrix and legal
disclaimer before any UI suggests these are safe drop-in replacements.

Evidence: `ROADMAP.md:62`,
live GitHub API checks for `KRTirtho/spotube`, `jpochyla/psst`, and
`hrkfdn/ncspot` on 2026-06-04,
Spotify developer policy/terms

Touches: roadmap docs, README comparison table, future WPF cards, support
docs, legal/trust copy.

Acceptance: matrix lists each client name, upstream URL, latest release,
last push, license/SPDX status, platform support, package-manager channels,
Premium requirement, playback source, account/auth model, Spotify Connect
support, offline/download claims, lyrics support, telemetry claims, package
signatures/checksums, and known policy/support caveats. UI cards link out
only after maintainers approve the disclaimer and support boundary.

Verify: regenerate the matrix from GitHub API plus checked README snippets;
cards cannot show install buttons until every row has a support state,
verified source URL, and policy note; docs state that LibreSpot does not
endorse, bundle, modify, or support third-party clients.

## Archived - Define the stable script support and retirement boundary

| Field | Value |
|---|---|
| Source | Cycle 12 |
| Resolution | Superseded by v4.0.0 stable (2026-08-22). |

The question assumed a `v3.7.2` script channel competing with a preview
desktop channel. Every v4 release now ships `LibreSpot.ps1`, `LibreSpot.exe`,
`LibreSpot-Desktop.exe`, and `LibreSpot.Cli.exe` together, `/latest` points
at that single stable line, and `SECURITY.md` names v4.0.x and later as
supported with v3.7.x superseded. The script reads the same `config.json`,
so there is no migration step.

## P1 - Define the architecture support matrix and release artifact lanes

| Field | Value |
|---|---|
| Source | Cycle 14 |
| Blocker | Architecture support policy - operator must define supported vs unsupported |

Why: README currently advertises x64 and ARM64 support with
per-architecture hash verification, and both PowerShell backends choose
Spicetify CLI `arm64` on ARM64 hosts and `x64` otherwise. The native WPF
local release build, however, publishes only one self-contained single-file
`win-x64` artifact, and the desktop project has no `RuntimeIdentifiers`
matrix. Microsoft documents Windows RIDs such as `win-x64`, `win-x86`, and
`win-arm64`, and notes that single-file apps are OS- and
architecture-specific, so LibreSpot needs an explicit support matrix before
package-manager distribution repeats a broader claim than the release
artifacts prove.

Evidence: `README.md:154`,
the local release procedure in `README.md`,
`src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:3`,
`LibreSpot.ps1:5151`,
`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1855`,
https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

Touches: the local release procedure, README architecture section,
package-manager manifests, checksum/SBOM/attestation naming, WPF publish
docs, support policy.

Acceptance: document one table that names support status for the stable
`.ps1`, PS2EXE, WPF `win-x64`, WPF `win-arm64`, and any `win-x86`
decision. For each row, state whether the lane is native, emulated,
unsupported, or legacy-only; which artifact name and RID it uses; which
Spicetify CLI architecture/hash is expected; whether package-manager
manifests may install it; and what manual or automated smoke test proves
the claim.

Verify: the local release build either publishes every supported RID or
the release truth check fails when docs claim an unsupported RID. Artifact names, checksums, SBOMs, and
attestations include the RID where multiple native WPF artifacts exist.
README/package-manager manifests no longer imply ARM64 WPF support unless a
`win-arm64` artifact is produced and smoke-tested.

Research note (2026-09-03): the technical blocker is gone. SpotX added ARM64 binary patches on 2026-09-01 (https://github.com/SpotX-Official/SpotX/issues/888) and Spicetify 2.44.0 ships a `windows-arm64` archive, so an ARM64 lane is buildable once this policy call is made. The desktop and CLI still publish `win-x64` only.

## Archived - Move signing and publishing through protected GitHub environments

| Field | Value |
|---|---|
| Source | Cycle 15 |
| Resolution | Superseded. Releases are built and uploaded locally, GitHub Actions do not publish anything, and signing is unsigned by design, so there are no signing secrets or workflow jobs to put behind an environment. |

## Archived - Add branch and tag rulesets for release-critical paths

| Field | Value |
|---|---|
| Source | Cycle 15 |
| Resolution | Superseded. The entry protected a tag-triggered release automation that no longer exists. `main` keeps branch protection with admin enforcement, and published releases are immutable, so release tags cannot be moved or deleted after publication. |

A tag ruleset for `v*.*.*` would still be optional hardening; open a fresh
item with current evidence if it is wanted.

## P1 - Separate LibreSpot-managed profile sharing from Marketplace-state backup

| Field | Value |
|---|---|
| Source | Cycle 18 |
| Blocker | Cloud/sharing policy decision + Marketplace data boundary - operator only |

Why: LibreSpot can share the settings it owns: SpotX flags, selected
Spotify target, selected Spicetify theme/scheme, curated extensions, and
Marketplace install preference. It does not currently back up arbitrary
Marketplace-installed themes, snippets, IndexedDB state, or cloud sync.
Recent Spicetify community threads report themes/extensions disappearing
and users wanting a backup/restore path for Marketplace installs. LibreSpot
should address that pain without implying it can safely export hidden
Spotify browser storage, usernames, or third-party cloud data as part of a
simple `.librespot` profile.

Evidence: `src/LibreSpot.Desktop/Models/AppCatalog.cs:41`,
`LibreSpot.ps1:757`,
`README.md:124`,
https://spicetify.app/docs/cli/commands,
Reddit threads on Marketplace extension loss

Touches: profile export schema, Marketplace diagnostics, backup/restore
docs, support copy, future preset gallery, trust/risk documentation.

Acceptance: export UI labels profiles as "LibreSpot-managed settings" and
separately reports detected unmanaged Marketplace state. If a broader
Marketplace backup is added, it is an explicit advanced action with a
preview of included paths, a no-credentials guarantee, local-only storage by
default, and clear restore limits. Profile import never silently copies
Spotify IndexedDB or Marketplace browser state.

Verify: tests prove a `.librespot` export includes only managed settings by
default; diagnostics can mention unmanaged Marketplace state without
copying it; import of a profile with unknown marketplace sections is shown
as unsupported unless the advanced backup feature exists and is explicitly
enabled.

Research note (2026-09-03): Marketplace's own Backup modal exports and imports a `marketplace-settings-<date>.json` file (https://github.com/spicetify/marketplace/blob/main/src/components/Modals/BackupModal/index.tsx), and Marketplace 1.0.11 (2026-09-02) fixed key migration. RD-147 in ROADMAP.md uses that JSON format from inside the client, which needs no Chromium file copy; the file-copy question here stays open.

## P2 - Write a bad-release and rollback runbook

| Field | Value |
|---|---|
| Source | Cycle 5 |
| Blocker | Incident response policy - operator only |

Why: published releases are immutable, so a bad asset cannot be replaced in
place, and releases ship unsigned by design, so there is no certificate to
revoke. There is still no documented operator path for a bad checksum, a
missing asset, a compromised GitHub token, a SmartScreen or antivirus false
positive, or a release that must be marked unsafe after publication.

Evidence: `SECURITY.md`, the local release procedure in `README.md`,
`schemas/release-artifact-contract.json`.

Touches: release docs, `SECURITY.md`, support templates.

Acceptance: runbook defines when to mark a release as unsafe in its notes,
publish a superseding hotfix, rotate GitHub credentials, and notify users.
It states that immutable assets are never edited and that a draft release
is the only place an asset can be replaced before publication.

Verify: tabletop exercise against one hypothetical missing-SBOM release and
one compromised-token scenario; checklist includes exact `gh` commands
without requiring destructive execution.

## P2 - Keep Chocolatey behind silent uninstall evidence

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | CLI artifact, uninstall behavior, and public-channel policy decisions |

Why: Chocolatey's verifier and community moderation surface install/uninstall
failures publicly. LibreSpot's current GUI-heavy artifacts, unsigned-by-design
executables, and destructive cleanup scope make Chocolatey riskier than
winget/Scoop CLI drafts or Velopack WPF packages.

Touches: Chocolatey package templates, CLI exit-code contract, uninstall
behavior, docs.

Acceptance: Chocolatey package remains draft/internal until the CLI
artifact is the package payload, `uninstall --silent --purge --yes --keep-spotify` is
implemented, valid exit codes are documented, checksums come from the
release manifest, and a clean Windows VM verifier run proves install,
upgrade, and uninstall. Package scripts use explicit `silentArgs`,
`validExitCodes`, `checksum`, and `checksumType`, and do not ask users for
input.

Verify: `choco pack` and a local install/upgrade/uninstall smoke pass run in
a disposable Windows environment before any community-feed push.

## P2 - Add package-manager trust copy that matches each channel

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | Package identity, channel ownership, and legal/trust copy approval |

Why: once users install via winget, Scoop, Chocolatey, or Velopack, the
README one-liner and two-artifact verification section will be incomplete.
Trust copy should explain which channel owns updates, how to verify the
downloaded asset, and when to avoid mixing channels.

Touches: README, `docs/distribution.md`, package descriptions, release
notes, support FAQ.

Acceptance: docs include a channel selection guide, "do not mix these
update owners" warning, package ID table, uninstall instructions per
channel, verification commands per artifact, and a compatibility note for
the raw PowerShell one-liner. Package descriptions avoid overpromising that
package managers distribute Spotify, SpotX, Spicetify, or Marketplace code.

Verify: docs review confirms every package ID and artifact name matches the
release manifest and channel matrix; package descriptions pass a legal/trust
review before public submission.

## P2 - Add repository community-health and contributor intake files

| Field | Value |
|---|---|
| Source | Cycle 8 |
| Blocker | CODEOWNERS maintainer routing + code of conduct policy - operator decision |

Why: GitHub's community-profile API reported 42% health for the repository
on 2026-06-04. README and MIT license are present, but code of conduct,
contributing guide, issue template, and pull request template are null, and
there is no tracked CODEOWNERS file. Cycle 4 already covers security
intake; this item covers ordinary bugs, compatibility reports, feature
requests, roadmap-only contributions, and ownership routing for sensitive
areas such as release, signing, backend scripts, and package manifests.

Evidence: local `.github` tree on 2026-06-04,
`gh api repos/SysAdminDoc/LibreSpot/community/profile` on 2026-06-04

Touches: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SUPPORT.md`,
`.github/ISSUE_TEMPLATE/*`, `.github/PULL_REQUEST_TEMPLATE.md`,
`.github/CODEOWNERS`, roadmap contribution docs.

Acceptance: contributors can file bug, compatibility, release, packaging,
feature, and documentation reports with fields for Windows version,
Spotify install source/version, Spicetify version, LibreSpot version,
selected profile, logs, and reproduction steps. PR template requires scope,
risk, tests, screenshots for UI work, release-note impact, and whether
roadmap/research docs changed. CODEOWNERS routes release/signing, scripts,
WPF shell, docs, and package manifests to explicit maintainers or a
documented placeholder until teams exist.

Verify: GitHub community-profile API health rises after files land; issue
forms render without schema errors; CODEOWNERS syntax validates; a sample
bug report contains enough data to reproduce a Spotify version mismatch
without asking the reporter for basic environment details.

## P2 - Define a safe handoff policy for alternative-client install links

| Field | Value |
|---|---|
| Source | Cycle 9 |
| Blocker | Legal/support policy decision about external links - operator only |

Why: the current product is a Windows Spotify patcher, while alternative
clients may be cross-platform, terminal-only, Premium-only, use different
playback sources, or have their own update channels. Automatically
installing or deep-linking to binaries would expand LibreSpot's support and
legal surface beyond its signed artifacts. A safer first implementation is
an informational card with user-controlled external links, source/status
metadata, and a clear "not managed by LibreSpot" boundary.

Evidence: `ROADMAP.md:62`, `README.md:7`,
Spotube/Psst/Ncspot README data,
Spotify developer policy

Touches: WPF card UI, README, support docs, trust/legal disclosure,
telemetry-free external-link handling.

Acceptance: first release of alternative-client cards is docs/link-only:
no automatic download, no bundled installer, no package-manager invocation,
no account-token handling, and no support promise beyond showing current
upstream metadata. Cards open verified upstream project/release/package
pages in the browser, include a support boundary, and distinguish GUI,
terminal, mobile, desktop, Premium-only, and non-Spotify-audio-source
behaviors.

Verify: UI tests prove cards cannot execute installers; external-link
allow-list contains only approved upstream URLs; support docs include a
sample response for users asking LibreSpot to troubleshoot a third-party
client.

## P3 - Decide whether macOS/Linux belongs in core, docs-only, or a sibling project

| Field | Value |
|---|---|
| Source | Cycle 1 |
| Blocker | Product strategy / platform scope decision - operator only |

Why: SpotX-Bash is active and supports 1.2.90, while LibreSpot's product
architecture is Windows PowerShell/WPF with Windows-specific scheduled task,
registry, and AppData assumptions.

Evidence: https://github.com/SpotX-Official/SpotX-Bash,
`LibreSpot.ps1:334`, `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`

Touches: product strategy docs; no feature code until the decision is made.

Acceptance: decision record chooses one: Windows-only with links, sibling
repo, or staged cross-platform CLI; it names unsupported assumptions and
distribution consequences.

Verify: review decision against install flow, watcher, package, and support
burden.

## P2 - Decide the v4 theming base

| Field | Value |
|---|---|
| Source | Research-Driven Additions (June 9, 2026); the .NET 10 migration this entry once waited for has landed |
| Blocker | Architecture/design decision - operator must choose theming strategy |

Why: three options now overlap: the current hand-rolled
Themes/Palette.xaml + Controls.xaml, the planned WPF-UI 4.3.0
evaluation (Cycle 2 "De-risk Wpf.Ui adoption"), and the native WPF
Fluent theme via ThemeMode that shipped in .NET 9 and improves in
.NET 10, which did not exist when the WPF-UI item was written.
Picking the base before/with the .NET 10 retarget avoids restyling
the shell twice. Constraints to weigh: ThemeMode is still
experimental (WPF0001 suppression required), Fluent parity gaps are
tracked in dotnet/wpf#10387, dark-only design per project philosophy,
and existing Mica integration in Services/Win11ShellIntegration.cs.

Evidence: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90;
https://github.com/dotnet/wpf/discussions/10387;
src/LibreSpot.Desktop/Themes/Palette.xaml;
src/LibreSpot.Desktop/Themes/Controls.xaml

Touches: LibreSpot.Desktop.csproj (TargetFramework), App.xaml,
Themes/*, MainWindow.xaml, Services/Win11ShellIntegration.cs

Acceptance: a recorded decision (custom tokens / WPF-UI / native
Fluent / hybrid) with a spike branch proving Mica + dark mode + the
existing control styles render correctly on the chosen base under
.NET 10; the losing options are closed out in the roadmap.

Verify: spike branch builds and renders correctly on at least one
Win11 and one Win10 machine before the decision is finalized.

---

## Research Backlog (blocked items)

### Spotify Linux/macOS patching scope
Blocked on: product strategy decision (covered by Cycle 1 macOS/Linux item above).

### DMCA/availability contingency if SpotX distribution changes
Blocked on: legal/policy decision. Operator must define the contingency plan.

## P2 - Spotify Connect regression test fixture

| Field | Value |
|---|---|
| Source | Research-Driven Additions (2026-08-11) |
| Blocker | Live Spotify account, reachable Spotify Connect device, and a disposable playback test environment |

Why: the repository documents Spotify Connect as a capability to compare in the
alternative-client matrix, but LibreSpot has no Connect client, device fixture,
or protocol mock. A trustworthy regression fixture needs a real authenticated
Spotify session and a reachable device to prove discovery, transfer, pause,
resume, and recovery behavior; none is available in this development
environment and fabricating the protocol would not validate Spotify behavior.

Evidence: `Roadmap_Blocked.md` alternative-client matrix,
`src/LibreSpot.Core/AppCatalog.cs`,
https://developer.spotify.com/documentation/web-api/concepts/spotify-connect

Acceptance (once the rig exists): add a deterministic mocked contract suite for
timeouts, device disappearance, transfer failure, and recovery, plus a gated
live smoke test that uses a disposable account/device, never stores credentials,
and proves the product remains non-mutating when Connect is unavailable.

## P3 - Add German and French WPF locales

| Field | Value |
|---|---|
| Source | Research-Driven Additions (2026-08-11) |
| Blocker | Native-language review required for user-facing translations |

Why: the code can add resource files mechanically, but the requested
acceptance explicitly requires no English carry-over, protected product/file
tokens, and no truncation across 1,230 strings and long prompt states. The
repository has no approved German/French translation source or reviewer, so an
autonomous implementation cannot establish linguistic correctness from code or
public technical documentation alone.

Evidence: `src/LibreSpot.Desktop/Properties/Strings.resx`,
`tools/Sync-Localization.ps1`,
`tests/LibreSpot.Desktop.Tests/LocalizationTests.cs`,
`ROADMAP.md` RD-37 acceptance criteria.

Acceptance (once review is available): add `Strings.de.resx` and
`Strings.fr.resx`, register both cultures in the language selector and
validation allowlist, and pass placeholder/protected-token/English-carry-over
checks plus hidden long-text rendering review for both locales.

## P2 - Evaluate PowerShell Gallery (Install-Script) as a distribution channel

| Field | Value |
|---|---|
| Source | Research-Driven Additions |
| Blocker | PSGallery account creation + publisher trust decision - operator only |

The distribution matrix (`schemas/distribution-matrix.json`) already has a
PSGallery row with draft status. Remaining work is account creation, script
metadata headers, the former publish step, and a go/no-go decision.
The row is ready. The operator needs to create the account and decide.

## P1 - Split package-manager targets by artifact role

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | Package identity decision must come first |

Why: Cycle 21 concludes that the fleet CLI needs a console-capable artifact.
That artifact is a better first target for winget portable and Scoop than
the GUI EXEs, while Velopack is the better owner for an installed WPF shell
with shortcuts and auto-update.

Touches: package channel matrix, the local release procedure, future CLI project,
winget/Scoop/Chocolatey templates, README install docs.

Acceptance: initial package sequence is explicit: GitHub Releases remains
canonical for all assets; winget portable and Scoop target the CLI
artifact first; Velopack targets the WPF shell after state migration and
update-feed decisions; Chocolatey waits until silent install, uninstall,
and verifier-friendly behavior are proven. Every artifact ships unsigned by
design, so no channel waits on a certificate. If a GUI package is
published through winget/Chocolatey, the matrix documents why it will not
conflict with Velopack's own updater.

Verify: package templates cannot reference GUI artifacts until the channel
matrix says the GUI artifact is eligible; validation tests fail on two
channels claiming to auto-update the same install root.

## P1 - Add package-channel validation to release preflight without publishing

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | Package identity decision required for validation targets |

Why: existing roadmap items ask for manifests, but package-manager drift is
most damaging when a tag ships with invalid hashes, wrong silent switches,
or stale package IDs. Validation should run before public submission.

Touches: the local release procedure, package templates, `docs/distribution.md`,
CI artifacts.

Acceptance: release preflight can generate draft winget YAML, Scoop JSON,
Chocolatey nuspec/tools scripts, and Velopack packaging metadata from the
release manifest into a temp folder. It runs `winget validate` where
available, Scoop `checkver`/manifest parse checks, `choco pack`, and
`vpk pack` dry runs for eligible channels. Draft outputs upload as CI
artifacts for review but do not publish unless an explicit release channel
flag is enabled.

Verify: a test tag/dry-run proves invalid SHA, missing silent switch,
missing package ID, or unsupported artifact role fails before upload.

## P2 - Decide backend status localization contract before translating activity events

| Field | Value |
|---|---|
| Source | Audit-Driven Additions (July 7, 2026) |
| Blocker | Product/protocol decision - operator must choose localized protocol keys vs English backend events |

Why: `Update-BackendState` status and step strings are English-only by design
but render inside a fully localized WPF shell. Translating them requires a
stable decision about whether the backend event protocol carries localization
keys, localized strings, or English diagnostic messages only.

Touches: `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`,
`schemas/backend-event-protocol.json`,
`src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`,
`src/LibreSpot.Desktop/Properties/Strings*.resx`.

Acceptance: operator records one protocol rule for backend activity text; the
implementation either maps stable backend event keys through WPF resources or
documents backend output as English diagnostic text outside localization scope.

## Archived - Publish the v3.7.4 / v4.0.0-preview.9 GitHub release with the full artifact contract

| Field | Value |
|---|---|
| Source | Audit-Driven Additions (July 7, 2026) |
| Resolution | Superseded by the v4.0.0 stable release (2026-08-22), which was published with the full artifact contract: PS2EXE, desktop, CLI, SBOM, manifest, and checksums. |

## P3 - Verify HotTrack-based Warning/Info/Danger contrast in HC #1 and HC #2 themes on-device

| Field | Value |
|---|---|
| Source | Audit-Driven Additions (July 7, 2026) |
| Blocker | On-device high-contrast visual validation required |

Why: the high-contrast palette maps attention colors to
`SystemColors.HotTrackColorKey`; contrast against Window/Control is not
guaranteed by every Windows high-contrast scheme and cannot be proven from
static resource inspection alone.

Touches: `src/LibreSpot.Desktop/Themes/HighContrastPalette.xaml`.

Acceptance: HC #1 and HC #2 are manually/device verified or a deterministic
capture runner records contrast-safe Warning/Info/Danger rendering in both.

## P3 - Decide Mica backdrop: make it visible or remove the machinery

| Field | Value |
|---|---|
| Source | Audit-Driven Additions (July 7, 2026) |
| Blocker | Design/operator decision |

Why: the DWM backdrop is set but the opaque root Grid covers the entire client
area, so Mica cannot render. The project needs a design decision to expose Mica
or remove the unused machinery.

Touches: `src/LibreSpot.Desktop/Services/Win11ShellIntegration.cs`,
`src/LibreSpot.Desktop/MainWindow.xaml`,
`src/LibreSpot.Desktop/Themes/Palette.xaml`.

Acceptance: operator chooses visible Mica or removal; implementation follows
that decision without leaving dead backdrop plumbing.

## P3 - Reword the "Premium Spotify toolkit" subtitle

| Field | Value |
|---|---|
| Source | Audit-Driven Additions (July 7, 2026) |
| Blocker | Branding/legal copy decision |

Why: for an ad-removal tool, "Premium" in always-visible branding can read as
"makes Spotify Premium." Suggested replacement direction is "Spotify setup &
recovery toolkit," but operator approval is needed for branding/legal wording.

Touches: `LibreSpot.ps1`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`.

Acceptance: operator approves the replacement subtitle and related preset copy;
the implementation updates both shells consistently.

## P3 - Native .NET launcher to replace the PS2EXE artifact

| Field | Value |
|---|---|
| Source | Research-Driven Additions (2026-07-08) |
| Blocker | Operator release pass; deliverable is a decision record + AV-flag comparison, not autonomous code |

Why: PS2EXE output (`LibreSpot.exe`, the packed standalone script) inherits the
packer's chronic HackTool/loader AV reputation. A native .NET launcher hosting
the script would shed that, but the acceptance is a decision record plus a
prototype with a before/after AV-flag comparison, which requires building and
antivirus-scanning release artifacts across an operator release pass. Signing
is not part of the comparison: releases are unsigned by design, so both sides
of the comparison ship unsigned.

Evidence: Win-PS2EXE #4, Malwarebytes PS2EXE false-positive thread; RESEARCH.md
architecture note. The fleet CLI (`LibreSpot.Cli.exe`) is already a native .NET
executable; this concerns the PS2EXE-packed `LibreSpot.exe` GUI artifact.

Touches: the local release procedure, a possible native launcher project,
`schemas/release-artifact-contract.json`.

Acceptance: a decision record that compares the PS2EXE artifact with a native
launcher prototype on AV flags and startup behavior, and either ships the
launcher as the standalone entrypoint or records why PS2EXE stays.

## P2 - One-click "restore stock Spotify client" escape hatch

| Field | Value |
|---|---|
| Source | Research-Driven Additions (2026-07-08) |
| Blocker | Destructive binary restore that needs live validation against a real SpotX-patched Spotify to implement safely |

Why: `RestoreVanilla` only runs `spicetify restore`, leaving SpotX's native
binary patches in place, so it does not return a stock client. The only
un-patch mechanism is restoring SpotX's own durable backups
(`Spotify.bak` -> `Spotify.exe`, `chrome_elf.dll.bak` -> `chrome_elf.dll`,
`Apps\xpui.bak` -> `Apps\xpui.spa`). That is a destructive overwrite of the
Spotify install, is SpotX-version-fragile (packed-vs-extracted bundle states,
backup-name drift), and LibreSpot keeps no structured pre-patch snapshot of its
own (`CreateBackup`/`RestoreBackup` are Spicetify-only). The acceptance requires
"verifies the result," which for a binary restore can only be trusted after
exercising it against a real patched Spotify and confirming the restored client
launches as stock, a live validation this environment cannot provide, and
shipping it unverified risks bricking a user's Spotify.

Evidence: `Get-SpotXPatchVerification` backup scheme (`LibreSpot.Backend.ps1`
~3347-3369); RESEARCH.md "Enforcement trajectory" + its Open Question on scope
(restore-from-backup vs re-download the official installer).

Touches: a new restore module + backend action, `Module-NukeSpotify` neighbours,
WPF maintenance catalog, CLI `repair`.

Acceptance (once a test rig with a real SpotX-patched Spotify is available):
a single action strips Spicetify, restores the SpotX binary backups, and
verifies via `Get-SpotXPatchVerification` that no patch markers remain; when
backups are absent it reports that a clean reinstall is required rather than
overwriting anything.

## P1 - RD-23: Exercise auto-reapply through Task Scheduler and process boundaries

| Field | Value |
|---|---|
| Source | Research-Driven Additions (2026-07-14) |
| Blocker | Local Task Scheduler starts but indefinitely suspends every disposable action process |

Why: the repeatable `Build-Scripts.ps1 -WatcherIntegration` test runner now
registers and exports a unique least-privilege task, isolates watcher state,
defines seven boundary scenarios, captures Scheduler evidence on failure, and
always removes its task/temp data. On 2026-07-14 this machine registered the
task successfully, but the Scheduler service left both the watcher host and an
independent minimal `cmd.exe /c echo` probe running indefinitely before either
process executed its first instruction. The Operational event log also
returned no matching events. Function-level coverage passes, so only live
Scheduler execution remains blocked by the host environment.

Touches: `Build-Scripts.ps1`,
`tests/powershell/Invoke-WatcherIntegration.ps1`,
`tests/powershell/WatcherIntegrationHost.ps1`, Windows Task Scheduler.

Acceptance (on a machine where an on-demand limited task can execute): run
`powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Scripts.ps1
-WatcherIntegration`; all success, disabled/corrupt config, unavailable
network, active Spotify, cancellation, and interrupted state-write assertions
pass, and no `LibreSpot-WatcherIntegration-*` task or temp directory remains.

---

## P2 - Runtime detection of Spicetify's hard-refuse gate (RD-41 residual; covers v3 readiness)

| Field | Value |
|---|---|
| Source | Research-Driven Additions (2026-07-24), RD-41 |

Why: this is the blocked runtime portion of the active Spicetify v3 readiness
and migration item. The actionable half of RD-41 shipped. The pin-advance guardrail
(`AppCatalog.PinnedSpotXHoldRationale`, README compatibility note, and the
compatibility-matrix warning) now requires confirming a newer Spicetify build
does not hard-refuse `backup apply` before advancing the pin. The remaining
half, detecting at runtime that an installed Spicetify build *carries* the
hard-fail gate and would refuse on the resolved Spotify target, and surfacing a
distinct localized WPF stack-health state for it, needs the released gated
Spicetify build to know the gate's exact runtime signature (declared-ceiling
query / exit code / message). LibreSpot also always installs and uses its own
pinned 2.44.0 (clearing any existing install), so the detection only matters
for a future pin advance, not for a user-installed newer CLI. Speculating the
signature now risks false hard-blocks, which the item explicitly forbids.

Touches: `Get-LibreSpotCompatibilityWarnings.ps1`,
`Test-SpicetifyCliVersionSupported.ps1`, `Get-SpicetifyDiagnosticSnapshot.ps1`,
WPF stack-health Spicetify component (all six locales), xUnit + Pester tests.

Acceptance (once a gated Spicetify release exists): LibreSpot feature-detects
the gate from the installed CLI, and when the resolved Spotify target exceeds
that build's declared ceiling it surfaces a distinct localized "Spicetify will
refuse to apply on this Spotify version" health state pointing at holding the
tested build; unknown/unparseable ceilings degrade to the existing soft
warning, never a false hard block.

Research note (2026-09-03): Spicetify v3 reached 3.0.0-beta.11 on 2026-09-02 with empty release notes; the beta refuses Spotify older than 1.2.80 and replaces custom apps with modules (https://github.com/spicetify/cli/issues/3038). The v2 line ended at 2.44.0 with declared support to Spotify 1.2.96.

---

## P2 - Verify Spicetify applies over a stock (non-SpotX) backup (RD-40)

| Field | Value |
|---|---|
| Source | Audit-Driven Additions (2026-07-23), RD-40 |

Why: the detection half of RD-40 shipped in v4.0.0-preview.19. The
`RouteNotWired` stack-health state plus `Repair-SpicetifyCustomAppWiring`
already flag and repair the silent post-apply render failure (store chunk not
referenced by the live bundle). The residual, empirically determining whether
re-backing-up after SpotX (or backing up a clean client before SpotX) changes
the Marketplace/theme render outcome on Spotify 1.2.93, requires a live
SpotX-patched Spotify install to observe render results, which this build
environment cannot produce (same live-rig blocker as "restore stock Spotify
binary"). Reordering the SpotX/Spicetify backup steps without a live render
signal would be an unverifiable guess.

Touches: `src/powershell/shared/Module-InstallSpotX.ps1`, `Module-ApplySpicetify`,
`Invoke-LibreSpotInstall` ordering.

Acceptance (on a machine with a live patchable Spotify 1.2.93): determine
whether the SpotX-first backup ordering affects Marketplace render; document the
ordering decision; confirm the shipped `RouteNotWired` gate catches any silent
render failure it introduces.

---

## P3 - Surface Spicetify's `doctor` diagnostic in the health model (RD-44)

| Field | Value |
|---|---|
| Source | Research-Driven Additions (2026-07-24), RD-44 |

Why: the `doctor` command was merged to `spicetify/cli` `main` (PR #3884) but is
NOT in the released v2.44.0 that LibreSpot pins and installs (the installer
clears any existing CLI and lays down 2.44.0). A "run doctor if present" path
would therefore be permanently dead code against LibreSpot's own managed CLI,
and its output contract cannot be parsed or tested without a released `doctor`
build. Implementing it now would be speculative against an unreleased,
unspecified output format.

Touches: `Get-SpicetifyDiagnosticSnapshot.ps1`, stack-health Spicetify component,
dependency-health output, xUnit tests.

Acceptance (once a Spicetify release ships `doctor`): feature-detect `doctor`
from the installed CLI, run it non-interactively, fold its result into the
diagnostic snapshot, and surface failures as a health signal; skip silently when
absent.

---

## P3 - Extend the bounded Stryker.NET pilot beyond the Core baseline

| Field | Value |
|---|---|
| Source | Research-Driven Additions; bounded pilot completed by RD-55 |
| Blocker | MTP runner is still preview and the current 24.32% baseline covers only selected Core files |

Why: many C# tests validate JSON schema structure; mutation testing surfaces
logic branches where tests pass even when the code is mutated. The RD-35
extraction satisfied the library-target prerequisite (`LibreSpot.Core` builds
`net10.0-windows` with no `UseWPF`, which Stryker can analyze where the desktop
`UseWPF` project cannot).

Resolved for a bounded pilot on 2026-08-20: Stryker.NET 4.16.0's preview MTP
runner collected xUnit v3 results from the dedicated Core-only test project.
The run tested 1,476 mutants and produced a 24.32% score with 355 killed,
4 timed out, 4,804 survived, and 329 compile-error mutants. The checked-in
configuration uses a 24% break threshold and restricts mutation scoring to
AppCatalog, CommunityAssetDriftService, OperationCorrelation, and
UpstreamDriftService. The MTP runner does not yet provide the final per-test
coverage behavior, so the baseline is a ratchet and not a release gate.

Reproducible pilot recipe:
1. Restore the pinned local tool from `.config/dotnet-tools.json` with
   `dotnet tool restore`.
2. Run from `src/LibreSpot.Core` with `dotnet stryker --test-runner mtp
   --concurrency 1`. The dedicated `tests/LibreSpot.Core.Tests` project
   references only Core and links the selected behavioral tests.
3. Keep `coverage-analysis` set to `off` until the MTP runner supports the
   per-test coverage behavior needed for a stronger score.

Next acceptance: expand the selected Core files only after a later MTP release
improves coverage reporting, then raise the break threshold from the measured
baseline instead of treating the current pilot as a whole-repository score.
