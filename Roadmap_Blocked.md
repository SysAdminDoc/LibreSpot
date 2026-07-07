# LibreSpot Roadmap - Blocked Items

Items moved here from `ROADMAP.md` because they require operator decisions,
credentials, or policy calls that an implementer cannot resolve autonomously.
Return items to `ROADMAP.md` once the blocking decision is made.

Last updated: 2026-06-29.

---

## P0 - Rebrand decision

| Field | Value |
|---|---|
| Source | Next Release Queue |
| Blocker | Brand/naming decision - operator only |

Decide whether to keep LibreSpot, rename before package-manager distribution,
or keep the repo name but rename the app. Decision must be recorded before
winget/scoop/choco work begins.

## P0 - Signing (SignPath Foundation enrollment)

| Field | Value |
|---|---|
| Source | Next Release Queue |
| Blocker | Credential enrollment - operator must complete SignPath Foundation application |

Complete SignPath Foundation enrollment and wire Authenticode signing into tagged
releases. Exit criteria: release assets are signed and verification docs are
current.

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

Touches: product decision record, package manifests, SignPath docs, README,
shell integration docs, future protocol/file associations.

Acceptance: operator records one canonical identity set: display name,
package IDs, executable names, publisher string, config folder names,
protocol URI, and whether old `%APPDATA%\LibreSpot` paths stay forever or
migrate.

Verify: repeat winget search; search Chocolatey and Scoop; check GitHub and
crates.io name collision notes; review SignPath and package manifests before
first submission.

## P1 - Define the Velopack app identity and update feed before packaging

| Field | Value |
|---|---|
| Source | Cycle 2 |
| Blocker | Package identity, install identity, update channel, and signing policy decisions |

Why: distribution planning names Velopack, but the repo currently has no
Velopack package, app ID, update channel, or `RELEASES` feed. Velopack
1.2.0 is current, and its docs make the release feed the discovery point
for updates; identity and install location decisions must be settled before
the WPF shell moves from portable release asset to installed app.

Evidence: `ROADMAP.md:89`, `.github/workflows/release.yml:242`,
`src/LibreSpot.Desktop/app.manifest:3`,
https://docs.velopack.io/distributing/overview,
https://github.com/velopack/velopack/releases/tag/1.2.0,
https://www.nuget.org/packages/Velopack/1.2.0

Touches: packaging docs, release workflow, WPF csproj, app manifest, update
check UX, installer/uninstaller docs.

Acceptance: a packaging design note chooses package ID, display name,
update channel names, GitHub Releases vs external feed hosting, install
root, Start Menu shortcut behavior, state migration from portable builds,
and the rule for preserving Authenticode signatures across updates.

Verify: after implementation, run `vpk pack` / `vpk upload` dry-runs in a
temp release folder and verify update discovery against a local feed.

## P1 - Define Velopack update ownership and state migration before the first installed WPF package

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | Package identity, update-channel ownership, and signing policy decisions |

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

## P2 - Write the shell-integration registration design before implementing protocol, toasts, jump lists, or file associations

| Field | Value |
|---|---|
| Source | Cycle 4 |
| Blocker | Package identity and shell ownership policy decisions |

Why: the roadmap already calls for `librespot://`, `.librespot` import
association, jump lists, taskbar thumbnail buttons, tray minimize, and
persistent toasts. Current planning does not yet define AppUserModelID,
Start Menu shortcut ownership, protocol registry keys, package vs portable
behavior, toast activation arguments, or uninstall cleanup. Those decisions
need to align with the package identity and elevation boundary.

Evidence: `ROADMAP.md:58`, `ROADMAP.md:70`,
https://learn.microsoft.com/en-us/windows/win32/shell/appids,
https://learn.microsoft.com/en-us/windows/win32/shell/links,
https://learn.microsoft.com/en-us/windows/win32/shell/fa-intro,
https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/toast-desktop-apps

Touches: design doc, installer/updater, uninstall cleanup, app activation,
notification service, diagnostics.

Acceptance: design specifies canonical AppUserModelID, shortcut path,
protocol URI, `.librespot` ProgID, toast activation payloads, jump-list
categories, portable-mode behavior, and uninstall rollback. It states
which registrations are per-user vs machine-wide and how package-manager /
Velopack installs differ from portable ZIP usage.

Verify: implementation checklist has registry/shortcut before-and-after
captures; uninstall removes only LibreSpot-owned registrations; portable
mode does not write shell registrations unless the user opts in.

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
recently pushed but still describing itself as early and requiring Premium;
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

## P1 - Define the stable script support and retirement boundary

| Field | Value |
|---|---|
| Source | Cycle 12 |
| Blocker | Release channel / support lifecycle policy - operator must decide |

Why: README describes LibreSpot as a single-script PowerShell GUI, and the
latest stable release is still `v3.7.2` with `LibreSpot.ps1` and
`LibreSpot.exe` assets. At the same time, the roadmap makes v4 stable the
native WPF shell. Without a channel policy, users will not know whether the
PowerShell GUI continues receiving accessibility fixes, security fixes,
dependency pin updates, watcher fixes, or only critical hotfixes after WPF
stabilizes.

Evidence: `README.md:7`, `README.md:18`,
`README.md:38`, `README.md:181`,
`gh release view v3.7.2 --json assets,isPrerelease` on 2026-06-04,
`gh release view v4.0.0-preview.1 --json assets,isPrerelease` on
2026-06-04

Touches: README channel table, release notes, roadmap v4 stable scope,
support docs, self-update messaging.

Acceptance: repo documents whether the PowerShell GUI is active stable,
maintenance-only LTS, or deprecated after WPF stable. The policy names what
still lands in the script lane, how long critical fixes are backported, how
users migrate saved `config.json`, whether PS2EXE continues shipping, and
which release channel `/latest` should point at during and after the v4
transition.

Verify: README and release notes show one stable recommendation; release
workflow enforces the channel policy; self-update/check-update messaging
does not suggest preview WPF builds as stable unless maintainers have made
that decision.

## P1 - Define the architecture support matrix and release artifact lanes

| Field | Value |
|---|---|
| Source | Cycle 14 |
| Blocker | Architecture support policy - operator must define supported vs unsupported |

Why: README currently advertises x64 and ARM64 support with
per-architecture hash verification, and both PowerShell backends choose
Spicetify CLI `arm64` on ARM64 hosts and `x64` otherwise. The native WPF
release workflow, however, publishes only one self-contained single-file
`win-x64` artifact, and the desktop project has no `RuntimeIdentifiers`
matrix. Microsoft documents Windows RIDs such as `win-x64`, `win-x86`, and
`win-arm64`, and notes that single-file apps are OS- and
architecture-specific, so LibreSpot needs an explicit support matrix before
package-manager distribution repeats a broader claim than the release
artifacts prove.

Evidence: `README.md:154`,
`.github/workflows/release.yml:247`,
`src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:3`,
`LibreSpot.ps1:5151`,
`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1855`,
https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

Touches: release workflow, README architecture section, package-manager
manifests, checksum/SBOM/attestation naming, WPF publish docs, support
policy.

Acceptance: document one table that names support status for the stable
`.ps1`, PS2EXE, WPF `win-x64`, WPF `win-arm64`, and any `win-x86`
decision. For each row, state whether the lane is native, emulated,
unsupported, or legacy-only; which artifact name and RID it uses; which
Spicetify CLI architecture/hash is expected; whether package-manager
manifests may install it; and what manual or automated smoke test proves
the claim.

Verify: release CI either builds and uploads every supported RID or fails
when docs claim an unsupported RID. Artifact names, checksums, SBOMs, and
attestations include the RID where multiple native WPF artifacts exist.
README/package-manager manifests no longer imply ARM64 WPF support unless a
`win-arm64` artifact is produced and smoke-tested.

## P1 - Move signing and publishing through protected GitHub environments

| Field | Value |
|---|---|
| Source | Cycle 15 |
| Blocker | Repository admin access + SignPath credentials required |

Why: the release workflow already uses least-privilege defaults and
escalates the final release job to `contents: write`, `id-token: write`,
and `attestations: write`, but the repo has zero GitHub environments and no
workflow job declares an `environment`. SignPath submission steps will use
repository secrets/variables once configured, so the signing credential
boundary should be explicit before Authenticode signing becomes the normal
release path. GitHub environments can hold environment-scoped secrets and
add required reviewers or wait timers before jobs access those secrets.

Evidence: `.github/workflows/release.yml:19`,
live `gh api repos/SysAdminDoc/LibreSpot/environments` on 2026-06-04
returned `total_count: 0`,
https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments

Touches: repository environments, release workflow, SignPath setup docs,
operator release checklist, `SIGNPATH.md`.

Acceptance: create named environments such as `release-signing` and
`github-release`, move SignPath secrets/variables into the signing
environment, require a documented maintainer review before signing or
publishing, and attach the relevant workflow jobs to those environments.
Manual `workflow_dispatch` releases must resolve an existing validated tag
and pass the same environment gates as tag-push releases.

Verify: `gh api repos/SysAdminDoc/LibreSpot/environments` lists the release
environments with required reviewers configured; release workflow runs show
protected deployment gates before any SignPath or release-upload step; a
dry-run or temporary test tag proves the workflow cannot access signing
secrets without environment approval.

## P1 - Add branch and tag rulesets for release-critical paths

| Field | Value |
|---|---|
| Source | Cycle 15 |
| Blocker | Repository admin access + operator signing/commit policy required |

Why: live GitHub API data shows `main` is protected, admin enforcement and
conversation resolution are enabled, force-pushes and deletions are
disabled, but required status checks are off, required signatures are off,
and repository rulesets are empty. Since the release workflow runs on
`v*.*.*` tags and can also be triggered manually, workflow-internal checks
should be backed by repository rules that protect `main`, release tags, and
high-risk files such as the release workflow, signing docs, backend script,
and package manifests.

Evidence: `.github/workflows/release.yml:10`,
live `gh api repos/SysAdminDoc/LibreSpot/branches/main` on 2026-06-04
reported `protected: true` with required status check enforcement `off`,
live `gh api repos/SysAdminDoc/LibreSpot/rulesets` on 2026-06-04 returned
`[]`,
https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets

Touches: repository rulesets/branch protection, release workflow, future CI
workflow from Cycle 8, maintainer docs, release checklist.

Acceptance: define one branch ruleset for `main` and one tag ruleset for
`v*.*.*` releases. The `main` ruleset requires the release/CI status checks
that matter for changed paths, blocks bypass except documented maintainers,
and requires signed commits or a recorded exception policy. The tag ruleset
blocks deletion/force movement of release tags and limits who can create
tags that match the release pattern. High-risk file changes either require
CODEOWNERS once Cycle 8 lands or a temporary manual reviewer policy.

Verify: GitHub API returns non-empty rulesets with the expected branch and
tag targets; a test branch cannot merge a release-workflow change without
the required checks; a non-authorized actor or test token cannot create,
move, or delete a `v*.*.*` tag.

## P1 - Separate LibreSpot-managed profile sharing from Marketplace-state backup

| Field | Value |
|---|---|
| Source | Cycle 18 |
| Blocker | Cloud/sharing policy decision + Marketplace data boundary - operator only |

Why: LibreSpot can share the settings it owns: SpotX flags, selected
Spotify target, selected Spicetify theme/scheme, curated extensions, and
Marketplace install preference. It does not currently model arbitrary
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

## P2 - Write a bad-release, signing, and rollback runbook

| Field | Value |
|---|---|
| Source | Cycle 5 |
| Blocker | SignPath credentials + incident response policy - operator only |

Why: SignPath enrollment is pending, existing releases are mutable, and the
workflow currently uses `--clobber` for asset upload. There is no documented
operator path for a bad checksum, missing asset, compromised token,
unsigned fallback, SmartScreen false positive, SignPath failure, or release
that must be marked unsafe after publication.

Evidence: `SIGNPATH.md:76`, `SIGNPATH.md:106`,
`.github/workflows/release.yml:181`,
`.github/workflows/release.yml:286`

Touches: release docs, `SECURITY.md`, SignPath docs, release workflow
policy comments, support templates.

Acceptance: runbook defines when to yank, mark prerelease, edit release
notes, delete assets, revoke/reissue signing credentials, rotate GitHub
secrets, publish a superseding hotfix, and notify users. It distinguishes
historical mutable releases from future immutable releases and says whether
`--clobber` is allowed only for draft releases.

Verify: tabletop exercise against one hypothetical missing-SBOM release and
one compromised-signing-token scenario; checklist includes exact `gh` and
SignPath dashboard commands without requiring destructive execution.

## P2 - Keep Chocolatey behind signing plus silent uninstall evidence

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | Signing, CLI artifact, uninstall behavior, and public-channel policy decisions |

Why: Chocolatey's verifier and community moderation surface install/uninstall
failures publicly. LibreSpot's current GUI-heavy artifacts, pending signing,
and destructive cleanup scope make Chocolatey riskier than winget/Scoop CLI
drafts or Velopack WPF previews.

Touches: Chocolatey package templates, CLI exit-code contract, uninstall
behavior, docs.

Acceptance: Chocolatey package remains draft/internal until the signed CLI
artifact exists, `uninstall --silent --purge --yes --keep-spotify` is
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

## P2 - Decide the v4 theming base before the .NET 10 migration lands

| Field | Value |
|---|---|
| Source | Research-Driven Additions (June 9, 2026) |
| Blocker | Architecture/design decision - operator must choose theming strategy |

Why: three options now overlap: the current hand-rolled
Themes/Palette.xaml + Controls.xaml, the planned WPF-UI 4.3.0
evaluation (Cycle 2 "De-risk Wpf.Ui adoption"), and the native WPF
Fluent theme via ThemeMode that shipped in .NET 9 and improves in
.NET 10 — which did not exist when the WPF-UI item was written.
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

## P2 - Evaluate PowerShell Gallery (Install-Script) as a distribution channel

| Field | Value |
|---|---|
| Source | Research-Driven Additions |
| Blocker | PSGallery account creation + publisher trust decision - operator only |

The distribution matrix (`schemas/distribution-matrix.json`) already has a
PSGallery row with draft status. Remaining work is account creation, script
metadata headers, CI publish step in release.yml, and a go/no-go decision.
The row is ready — operator needs to create the account and decide.

## P1 - Split package-manager targets by artifact role

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | Package identity decision + signing enrollment must complete first |

Why: Cycle 21 concludes that the fleet CLI needs a console-capable artifact.
That artifact is a better first target for winget portable and Scoop than
the GUI EXEs, while Velopack is the better owner for an installed WPF shell
with shortcuts and auto-update.

Touches: package channel matrix, release workflow, future CLI project,
winget/Scoop/Chocolatey templates, README install docs.

Acceptance: initial package sequence is explicit: GitHub Releases remains
canonical for all assets; winget portable and Scoop target the signed CLI
artifact first; Velopack targets the WPF shell after state migration and
update-feed decisions; Chocolatey waits until signing, silent install,
uninstall, and verifier-friendly behavior are proven. If a GUI package is
published through winget/Chocolatey, the matrix documents why it will not
conflict with Velopack's own updater.

Verify: package templates cannot reference GUI artifacts until the channel
matrix says the GUI artifact is eligible; validation tests fail on two
channels claiming to auto-update the same install root.

## P1 - Add package-channel validation to release preflight without publishing

| Field | Value |
|---|---|
| Source | Cycle 22 |
| Blocker | Package identity decision + signing enrollment required for validation targets |

Why: existing roadmap items ask for manifests, but package-manager drift is
most damaging when a tag ships with invalid hashes, wrong silent switches,
or stale package IDs. Validation should run before public submission.

Touches: release workflow, package templates, `docs/distribution.md`,
CI artifacts.

Acceptance: release preflight can generate draft winget YAML, Scoop JSON,
Chocolatey nuspec/tools scripts, and Velopack packaging metadata from the
release manifest into a temp folder. It runs `winget validate` where
available, Scoop `checkver`/manifest parse checks, `choco pack`, and
`vpk pack` dry runs for eligible channels. Draft outputs upload as CI
artifacts for review but do not publish unless an explicit release channel
flag is enabled.

Verify: a test tag/dry-run proves invalid SHA, missing silent switch,
missing package ID, unsupported artifact role, or unsigned-gated channel
fails before upload.

## P3 - Add Stryker.NET mutation testing to identify undertested code

| Field | Value |
|---|---|
| Source | Research-Driven Additions (June 27, 2026) |
| Blocker | Requires non-UI runtime logic extracted into a class library first |

Why: the project has many C# tests that validate JSON schema structure rather
than runtime behavior. Stryker.NET can identify code paths where tests pass even
when logic is mutated.

Blocked: Stryker cannot mutate the WPF project directly because the desktop
target uses `net10.0-windows` with `UseWPF=true`. After non-UI logic is
extracted into a class library, Stryker can target that library without WPF
analysis failures.

Acceptance: Stryker runs against the extracted non-UI library, produces a
mutation score report locally, and documents a ratchet threshold that catches
untested behavioral branches without requiring WPF mutation.
