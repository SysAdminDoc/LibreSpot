# LibreSpot Roadmap

Active roadmap for forward-looking work only. Completed work lives in git
history and `CHANGELOG.md`. Research conclusions live in `RESEARCH.md`.

Last consolidated: 2026-06-01.
Last researched: 2026-06-06, Cycle 22.

## Implementer Instructions (for the build machine)

This roadmap is fed continuously by the research machine. On every pass, the
build machine should:

1. `git pull --rebase` to get the latest researched items before starting.
2. Work the open implementer-actionable items top-down by priority (P0 -> P3).
   Build them properly: multi-file structure, real error handling, no runtime
   auto-install hacks, version strings synced, docs/CHANGELOG updated in the
   same commit.
3. In addition to building items, run a full UX audit each pass. Walk every
   screen, page, dialog, form, table, and empty/loading/error/disabled state
   across light, dark, and high-contrast themes. Check onboarding, navigation
   clarity, spacing, contrast, alignment, clipping, overflow, hierarchy,
   microcopy, destructive-action guards, keyboard and screen-reader
   accessibility, and trust signals. Fix what you find, or file it back as a
   new implementer-actionable roadmap item if it is larger than a pass.
4. Check off each item you complete, leave it in place, commit per logical
   change with a "why" message, and push.
5. Never edit this Implementer Instructions block or the Researcher Queue
   headings. Never force-push.

## Current State

- Public latest stable release: v3.7.2 (verified 2026-07-14).
- Current script source line: v3.7.4 (not yet the public stable release).
- Native WPF shell line: v4.0.0-preview.17.
- Release pipeline now builds PS2EXE and WPF artifacts with checksums, SBOMs, and
  build provenance attestations.
- Auto-reapply watcher, self-update checks, pre-patched Spotify detection,
  Spotify version selection, and the v3.7 UI refresh have shipped.
- Point-in-time dependency pins (synced 2026-07-08):
  - SpotX `550bc72c` for Spotify `1.2.93`
  - Spicetify CLI v2.44.0
  - Marketplace v1.0.9
  - Spicetify themes `df033493`

## Next Release Queue

| Priority | Track | Work | Exit criteria |
|---|---|---|---|

## Structural Cleanup (July 8, 2026)

Items surfaced by the July 8, 2026 structural audit. Focus: eliminate
silent-drift sync bugs, reduce monolith file sizes, and improve testability.

## Distribution And Trust

Distribution work is sequenced behind the rebrand and signing decisions (see
`Roadmap_Blocked.md`). Once those are resolved:

1. Publish winget manifests for portable assets.
2. Add Velopack packaging for the WPF shell.
3. Create a Scoop bucket with `checkver` and `autoupdate`.
4. Submit Chocolatey only after signing and checksum automation have settled.

Microsoft Store/MSIX remains a poor fit because the app needs to patch files in
the user's Spotify installation and interact with classic desktop locations.

## Research Backlog

These require fresh research before implementation:

- Whether the April 2026 SpotX and Spicetify pin guidance is still current.
- Spotify Connect regression test harness.
- Spicetify v3 readiness and migration risk.

## 🔬 Researcher Queue (Cycle 11 - 2026-06-04)

Cycle 11 inspects maintainability risks in the PowerShell and WPF code shape.
It does not implement refactors; it turns measured duplication and file size
into implementation-ready extraction and quality-gate work. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where release sequencing decisions are required.

## 🔬 Researcher Queue (Cycle 17 - 2026-06-06)

Cycle 17 inspects theme selection and preview reliability across the stable
PowerShell GUI and native WPF shell. Cycle 4 already covers community asset
supply-chain pinning for install-time downloads; this pass focuses on the
user-facing browse/preview surface, stale screenshots, blocking image loads, and
WPF parity before v4 stable. Tags: 🔬 = researcher-added this cycle; 🤖 =
implementer-actionable now; 🔧 = operator-needed where catalog policy decisions
are required.


## 🔬 Researcher Queue (Cycle 18 - 2026-06-06)

Cycle 18 narrows the broad Community Sharing release-queue row into profile
export/import, local preset management, and Marketplace-state boundaries. Cycle
3 already covers schema versioning for the internal `config.json`; this pass
focuses on the separate user-facing file/URI experience and the safety preview
required before imported settings can mutate Spotify or Spicetify. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where hosted sharing or cloud policy decisions are required.

## 🔬 Researcher Queue (Cycle 19 - 2026-06-06)

Cycle 19 turns the earlier operation-journal backlog item into the concrete
undo/dry-run product contract needed for v4 stable and fleet deployment. The
core finding is that LibreSpot already has several guardrails, but the backend
still mutates files, PATH entries, scheduled tasks, and Spotify/Spicetify state
without a shared preflight plan, reversible operation token, or post-run undo
receipt. Any undo UI must start by making reversibility explicit instead of
implying that every cleanup path can be rolled back.

### Findings

- The v4 stable scope already calls for an undo-selected-actions pane for
  reversible operations such as update blocking, shortcuts, scheduled tasks,
  and config changes, while the Fleet CLI section calls for `--dry-run` and
  PowerShell `-WhatIf` parity (`ROADMAP.md:52`, `ROADMAP.md:72`,
  `ROADMAP.md:107`).
- Cycle 3 already asks for a destructive-action operation journal, including
  planned action, target, safety decision, result, rollback hint, and dry-run
  output (`ROADMAP.md:539`). Cycle 19 should not replace that item; it should
  define the contracts the journal and UI must expose.
- The WPF backend action surface is still a string `ValidateSet` with install,
  restore, uninstall, reset, and watcher actions, with no `DryRun`,
  `ShouldProcess`, or journal parameter (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1`).
- The backend config writer creates a temp file and transient backup, then
  deletes the backup after replacement, so it is atomic but not a user-visible
  rollback point (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:681`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:700`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:710`).
- Watcher enable/disable mutates scheduled-task state and then saves the
  config preference, but it does not capture the previous task XML or previous
  `AutoReapply_Enabled` value as an undo token
  (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:716`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2268`).
- `UninstallSpicetify` and `FullReset` remove config directories, CLI
  directories, PATH entries, Spotify packages/files, and scheduled tasks through
  helper functions such as `Remove-PathSafely`, `Remove-PathEntry`, and
  `Module-NukeSpotify`, but there is no preflight JSON plan that the UI or CLI
  can show before mutation (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1222`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1301`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1657`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1749`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2240`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2253`).
- Current rollback behavior is narrow: Spicetify apply failures attempt
  `spicetify restore`, and the stable PowerShell backup-restore path can copy a
  temporary snapshot back after a failed restore. These are failure recovery
  paths, not a general undo model
  (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2128`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2139`,
  `LibreSpot.ps1:4117`, `LibreSpot.ps1:5393`, `LibreSpot.ps1:5405`,
  `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:605`).
- The WPF confirmation dialog uses static summaries for maintenance actions and
  saves install configuration before running the backend, so users cannot yet
  review a computed list of intended file, registry, PATH, task, and config
  changes (`src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1386`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1434`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1479`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1901`).
- Microsoft PowerShell guidance says `CmdletBinding(SupportsShouldProcess)`
  adds `-Confirm` and `-WhatIf`, and that code should call
  `$PSCmdlet.ShouldProcess(...)` close to the actual change. PSScriptAnalyzer
  flags functions that declare `SupportsShouldProcess` without calling
  `ShouldProcess`, or vice versa:
  https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_functions_cmdletbindingattribute,
  https://learn.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-shouldprocess,
  https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/rules/shouldprocess.
- Windows Installer rollback research reinforces the same constraint: rollback
  works because an installer creates rollback scripts and saves deleted files as
  it processes the install, while direct custom actions require explicit
  rollback custom actions and still may not be fully reversible:
  https://learn.microsoft.com/en-us/windows/win32/msi/rollback-installation,
  https://learn.microsoft.com/en-us/windows/win32/msi/rollback-custom-actions.

## 🔬 Researcher Queue (Cycle 20 - 2026-06-06)

Cycle 20 narrows the broad diagnostics/repair queue into a native-WPF health
model. The key gap is not that LibreSpot lacks status text: both shells already
show useful status. The gap is that the WPF shell's status model is still a
small boolean snapshot, while the stable PowerShell shell already inspects a
larger five-component maintenance state and offers backup/restore affordances.
v4 stable should convert those checks into typed issues with targeted repair
actions, support-bundle output, and clear boundaries around what LibreSpot can
diagnose automatically.

### Findings

- The release queue calls for status-at-a-glance and repair flows for Spotify,
  SpotX, Spicetify, backups, scheduled task state, and last patch time
  (`ROADMAP.md:56`, `ROADMAP.md:75`).
- The WPF `EnvironmentSnapshot` currently tracks only Spotify installed,
  Spicetify installed, saved config, config folder, and auto-reapply task
  booleans, then derives a broad `Stack ready` / `Partial setup` / `Clean slate`
  summary (`src/LibreSpot.Desktop/Models/AppCatalog.cs:143`).
- `EnvironmentSnapshotService.GetSnapshot` checks `%APPDATA%\Spotify\Spotify.exe`,
  `%LOCALAPPDATA%\spicetify\spicetify.exe`, the supplied config path, the config
  directory, and one scheduled task probe. It does not inspect Spotify version,
  SpotX patch state, Spicetify config values, Marketplace files, theme status,
  backup count, last run result, watcher state age, last patch time, or log/crash
  health (`src/LibreSpot.Desktop/Services/EnvironmentSnapshotService.cs:16`).
- The WPF dashboard binds that compact snapshot into three status rows and a
  freshness card, plus a separate watcher panel; it has refresh and folder-open
  affordances but no issue list or per-issue repair actions
  (`src/LibreSpot.Desktop/MainWindow.xaml:470`,
  `src/LibreSpot.Desktop/MainWindow.xaml:593`,
  `src/LibreSpot.Desktop/MainWindow.xaml:1428`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:994`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1627`).
- The stable PowerShell maintenance view already computes a richer state:
  Marketplace file/config presence, active theme injection, backup count, a
  5-component readiness count, next-step guidance, and enablement/tooltips for
  backup, restore, reapply, restore vanilla, uninstall, and reset
  (`LibreSpot.ps1:3904`, `LibreSpot.ps1:3935`, `LibreSpot.ps1:3952`,
  `LibreSpot.ps1:3991`, `LibreSpot.ps1:4004`).
- WPF `CrashReporter` writes rolling logs under `%LOCALAPPDATA%\LibreSpot\logs`
  and crash reports under `%LOCALAPPDATA%\LibreSpot\crashes`, retains logs for
  14 days and crash reports for 30 days, and offers copy/open buttons in the
  crash dialog; it is not yet integrated with the maintenance dashboard or a
  sanitized support bundle (`src/LibreSpot.Desktop/Services/CrashReporter.cs:14`,
  `src/LibreSpot.Desktop/Services/CrashReporter.cs:51`,
  `src/LibreSpot.Desktop/Services/CrashReporter.cs:110`,
  `src/LibreSpot.Desktop/Services/CrashReporter.cs:369`,
  `src/LibreSpot.Desktop/Services/CrashReporter.cs:385`).
- The backend has a narrow `Get-SpicetifyDiagnosticSnapshot` around apply
  failures, logging Spicetify `spotify_path`, `prefs_path`, `xpui.spa`, and
  Spotify executable existence, but those checks are not surfaced as reusable
  dashboard diagnostics (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2064`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2094`).
- Tests currently cover the snapshot config directory and auto-reapply probe
  only; there are no fixture-backed tests for Marketplace missing, stale
  Spicetify config, missing backups, broken watcher logs, log redaction, or
  repair-action selection (`tests/LibreSpot.Desktop.Tests/EnvironmentSnapshotServiceTests.cs:10`,
  `tests/LibreSpot.Desktop.Tests/EnvironmentSnapshotServiceTests.cs:40`).
- Spicetify's own CLI docs position `backup`, `apply`, and `restore` as core
  operations; they also document `spicetify backup apply` after Spotify updates
  and `spicetify restore backup apply` for full restore/reapply:
  https://spicetify.app/docs/cli,
  https://spicetify.app/docs/cli/commands.
- Current upstream/community evidence still shows Marketplace-specific pain:
  the Marketplace issue list has recent open items for extensions/themes
  disappearing, black screen, and the Marketplace button not appearing; a
  Spicetify CLI issue from April 2026 reports Marketplace missing on Spotify
  `1.2.87.414` with Spicetify `2.43.1`:
  https://github.com/spicetify/marketplace/issues,
  https://github.com/spicetify/cli/issues/3816.
- Microsoft .NET diagnostics docs describe `EventSource` as a structured
  logging mechanism useful for diagnostic tasks, with explicit event IDs and
  stable event contracts; Windows diagnostics/privacy guidance reinforces that
  crash dumps and enhanced error data require additional permission. LibreSpot
  support export should stay local, opt-in, and reviewable:
  https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource,
  https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource-instrumentation,
  https://support.microsoft.com/en-us/windows/diagnostics-feedback-and-privacy-in-windows-28808a2b-a31b-dd73-dcd3-4559a5199319.

## 🔬 Researcher Queue (Cycle 21 - 2026-06-06)

Cycle 21 turns the Fleet Deployment row into a command and artifact contract.
The main finding is that LibreSpot already has useful headless building blocks,
but not a fleet-ready CLI surface. The stable PowerShell script has a few
watcher flags and the WPF backend has a structured action protocol, yet both
released EXE artifacts are GUI-first. Fleet support should therefore start with
a console-capable entrypoint and a stable machine contract instead of trying to
bolt `--silent` onto the current WPF button actions.

### Findings

- The roadmap already names Fleet Deployment as a P1 track: silent/quiet flags,
  JSON answer files, `--detect --json`, explicit exit codes, NDJSON logs,
  `uninstall --silent --purge --yes --keep-spotify`, validate, `--dry-run`,
  and deployment examples for WinRM, PSRemoting over SSH, PDQ Deploy, Intune
  Win32 apps, and SCCM-style return codes (`ROADMAP.md:96`).
- The stable PowerShell script only parses `-clean`, `-watch`,
  `-installwatcher`, and `-uninstallwatcher` from raw `$args`; there is no
  `install`, `detect`, `status`, `validate`, `uninstall`, `repair`, `--json`,
  `--ndjson`, `--answer-file`, `--silent`, or `--dry-run` parser
  (`LibreSpot.ps1:118`, `LibreSpot.ps1:124`).
- The stable script watcher path is truly headless and exits before WPF loads,
  but only for watcher tasks. Regression tests explicitly protect the watcher
  exit branches so they do not fall through into XAML (`LibreSpot.ps1:3196`,
  `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:194`,
  `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:212`).
- The WPF backend script exposes a `ValidateSet` of GUI/maintenance actions and
  one config path parameter, not a user-facing CLI verb model
  (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1`).
- `BackendScriptService` validates actions, writes the embedded backend to a
  runtime directory, starts Windows PowerShell hidden, passes `-Action` and
  `-ConfigPath` using `ArgumentList`, parses `@@LS@@|kind|level|payload`
  messages, and only returns success/failure plus a string error. This is a
  good internal substrate, but it lacks a public stdout/stderr contract,
  schema version, status JSON, exit-code taxonomy, answer-file input, log
  directory choice, or dry-run mode (`src/LibreSpot.Desktop/Services/BackendScriptService.cs:20`,
  `src/LibreSpot.Desktop/Services/BackendScriptService.cs:37`,
  `src/LibreSpot.Desktop/Services/BackendScriptService.cs:105`,
  `src/LibreSpot.Desktop/Services/BackendScriptService.cs:199`).
- The WPF maintenance catalog currently lists Check Updates, Reapply, Restore
  Vanilla, Uninstall Spicetify, and Full Reset. It does not model fleet-only
  verbs such as detect, status, validate, export diagnostics, or repair issue
  by health-action id (`src/LibreSpot.Desktop/Models/AppCatalog.cs:275`).
- The release workflow builds `LibreSpot.exe` with PS2EXE `-NoConsole
  -RequireAdmin` and publishes `LibreSpot-Desktop.exe` as a self-contained WPF
  executable. Those are appropriate GUI artifacts, but they are poor primary
  fleet CLIs because console output and headless exit-code behavior are not the
  first-class artifact contract (`.github/workflows/release.yml:165`,
  `.github/workflows/release.yml:249`, `.github/workflows/release.yml:341`).
- Microsoft Intune Win32 app docs make return codes and detection rules first
  class: admins configure success/failure/retry/soft-reboot/hard-reboot return
  codes, and detection rules determine whether an app is present. Intune
  troubleshooting docs also show a detection script pattern that writes the
  detected version to STDOUT and exits `0`, or exits nonzero when detection
  fails:
  https://learn.microsoft.com/en-us/intune/app-management/deployment/add-win32,
  https://learn.microsoft.com/en-us/intune/app-management/deployment/troubleshoot-win32.
- WinGet docs require usable silent install behavior for package submissions,
  define manifest metadata/installer SHA fields, document `winget install
  --silent`, local manifests, agreement acceptance for scripts, and common
  silent switch expectations:
  https://learn.microsoft.com/en-us/windows/package-manager/winget/install,
  https://learn.microsoft.com/en-us/windows/package-manager/package/manifest.
- Windows Installer exit-code docs are still the lingua franca for many admin
  tools: `0` is success, `3010` is success with reboot required, `1641` is
  success with reboot initiated, `1602` is user cancel, `1603` is fatal failure,
  and `1618` is another install already in progress:
  https://learn.microsoft.com/en-us/windows/win32/msi/error-codes.
- PowerShell docs confirm why LibreSpot must explicitly use `exit`: scripts
  invoked through `pwsh -File` return `1` for terminating exceptions, an
  explicit `exit` value when used, and `0` when the script completes
  successfully:
  https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_automatic_variables.
- `ConvertTo-Json` can produce compact JSON and warns about depth truncation in
  newer PowerShell versions, which matters for stable status/receipt output;
  NDJSON's public spec requires each JSON text to be followed by a newline:
  https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/convertto-json,
  https://github.com/ndjson/ndjson-spec.

### New / Refined Backlog Items


## 🔬 Researcher Queue (Cycle 22 - 2026-06-06)

Cycle 22 inspects package-manager distribution and update channels after the
Cycle 21 Fleet CLI contract. The key conclusion is that LibreSpot needs a
distribution channel matrix before any more manifest files are added. The
roadmap already names winget, Scoop, Chocolatey, and Velopack, but those
channels should not all install the same artifact or own updates in the same
way. Package work should consume a signed release artifact manifest and choose
one updater per artifact, otherwise users can end up with duplicate install
roots, mismatched package IDs, and stale checksums.

### Findings

- The top-level distribution order already says: finish the rebrand decision,
  complete SignPath enrollment, publish winget manifests for portable assets,
  add Velopack for the WPF shell, create a Scoop bucket, then consider
  Chocolatey after signing/checksum automation settles (`ROADMAP.md:86`).
- Existing roadmap items already cover draft winget/Scoop/Chocolatey manifests,
  a Velopack app identity/update feed, package identity before public manifests,
  generated release artifact contracts, and separating generated release assets
  from source files (`ROADMAP.md:196`, `ROADMAP.md:398`, `ROADMAP.md:460`,
  `ROADMAP.md:1448`).
- The repo does not currently contain package-manager manifests, a `docs/deployment`
  directory, a Velopack config, or a package bucket. The only local distribution
  files found by targeted scan are `SIGNPATH.md`, `.github/workflows/release.yml`,
  and `src/LibreSpot.Desktop/app.manifest`.
- `SIGNPATH.md` still describes two signed PE artifacts, `LibreSpot.exe` and
  `LibreSpot-Desktop.exe`. Cycle 21 adds a required future
  `LibreSpot.Cli.exe`, so the SignPath artifact configuration, README
  verification instructions, release workflow, checksum list, SBOM/provenance
  subjects, and package-manager manifest templates all need a third-artifact
  update before public distribution (`SIGNPATH.md:28`, `SIGNPATH.md:76`,
  `SIGNPATH.md:102`).
- The release workflow currently builds PS2EXE, WPF, checksums, SBOM, and
  attestations for `LibreSpot.exe`, `LibreSpot.ps1`, `LibreSpot-Desktop.exe`,
  and `LibreSpot.sbom.cdx.json`; it has no generated release manifest JSON that
  downstream package templates can consume (`.github/workflows/release.yml:165`,
  `.github/workflows/release.yml:249`, `.github/workflows/release.yml:341`,
  `.github/workflows/release.yml:362`, `.github/workflows/release.yml:380`).
- README still leads with the `irm ... LibreSpot.ps1 | iex` one-liner and says
  signing is pending for two artifacts. Broader package-manager distribution
  should not make that one-liner the only documented path once signed GUI,
  desktop, and CLI artifacts exist (`README.md:18`, `README.md:181`).
- WinGet manifest docs require package metadata, installer URL, installer SHA,
  architecture, installer type, and package identifiers. WinGet can install from
  local manifests and has a `portable` installer type, while `winget install`
  exposes `--silent`, `--manifest`, `--installer-type`, and `--rename` for
  portable packages:
  https://learn.microsoft.com/en-us/windows/package-manager/package/manifest,
  https://learn.microsoft.com/windows/package-manager/winget/install.
- Scoop manifests are JSON and use fields such as `url`, `hash`, `bin`,
  `shortcuts`, `checkver`, and `autoupdate`; the wiki documents using
  `checkver.ps1` and autoupdate definitions to update manifests from upstream
  release pages:
  https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests,
  https://github.com/ScoopInstaller/Scoop/wiki/App-Manifest-Autoupdate.
- Chocolatey packaging is a PowerShell package wrapper around installers or
  embedded files. `Install-ChocolateyPackage` explicitly models `silentArgs`,
  `validExitCodes`, `checksum`, and `checksumType`; Chocolatey's community feed
  has validator/verifier moderation services that check package quality and
  installability:
  https://docs.chocolatey.org/en-us/create/functions/install-chocolateypackage/,
  https://docs.chocolatey.org/en-us/create/create-packages/,
  https://docs.chocolatey.org/en-us/community-repository/moderation/package-validator/,
  https://docs.chocolatey.org/en-us/community-repository/moderation/package-verifier/.
- Velopack packages a compiled app with `vpk pack`, requires identity inputs
  such as `--packId`, `--packVersion`, `--packDir`, and `--mainExe`, and update
  discovery uses release feeds like `releases.{channel}.json`. On Windows the
  default install root is `%LocalAppData%\{packId}`, the `current` directory is
  replaced during updates, and Velopack recommends code signing because unsigned
  apps may be flagged:
  https://docs.velopack.io/packaging/overview,
  https://docs.velopack.io/packaging/operating-systems/windows,
  https://docs.velopack.io/packaging/installer,
  https://docs.velopack.io/integrating/update-sources.

### New / Refined Backlog Items

(Moved to `Roadmap_Blocked.md`: Split package-manager targets by artifact role;
Add package-channel validation to release preflight — both blocked on package
identity and signing decisions.)

## Audit-Driven Additions (June 30, 2026)

Items below were surfaced by the June 30, 2026 deep engineering audit
and not resolved during that pass.

## Audit-Driven Additions (July 12, 2026)

Items below were surfaced by the July 12, 2026 deep engineering + UX audit.
Fixed items from that pass are in `CHANGELOG.md` under `[Unreleased]`; the
items below were deferred because they need runtime verification the audit
could not do headlessly, carry regression risk, or are systemic changes
larger than a single fix.

## Research-Driven Additions

Items below were added by the June 9, 2026 research pass. They cover
ecosystem changes, legal landscape shifts, and catalog freshness gaps
not addressed by earlier cycles.

(Moved to `Roadmap_Blocked.md`: Decide the v4 theming base before the
.NET 10 migration — blocked on operator architecture/design decision.)

## Research-Driven Additions (June 19, 2026)

Items below were added by the June 19, 2026 research pass. They address
the schema-runtime disconnect, upstream version gaps, Marketplace
reliability, localization follow-through, legacy GUI contrast, and
dependency freshness surfaced during exhaustive ecosystem research.


## Research-Driven Additions (June 27, 2026)

Items below were added by the June 27, 2026 research pass. They address
AV trust barriers, Smart App Control compatibility, Spotify enforcement
risk, accessibility compliance, testing quality, and PowerShell runtime
compatibility surfaced during exhaustive competitive and ecosystem research.


## Research-Driven Additions (June 28, 2026)

Items below were added by the June 28, 2026 research pass. They address
upstream version fragility, legal risk documentation, Spicetify v3
migration readiness, AppCatalog localization gap, and runtime upstream
health monitoring surfaced during exhaustive competitive and community
sentiment research.

Note on existing items: the P1 shared-core extraction (Cycle 11) is
reinforced by community evidence that SpotX+Spicetify ordering fragility
(SpotX Discussion #402) and Spotify 1.2.86 CSS breakage (Spicetify
X/Twitter Mar 2026) both amplify the cost of drifted functions. The
Cycle 20 diagnostics item is reinforced by user reports of "SpotX
stopped working" (issue #849) as the #1 complaint — the WPF dashboard
needs to detect and surface this state, not just show booleans.

## Research-Driven Additions

### P1

## Audit-Driven Additions (July 7, 2026)

Items surfaced by the July 7, 2026 deep audit and not resolved during that
pass.

## Research-Driven Additions (2026-07-08)

Items from the 2026-07-08 exhaustive research pass. Full evidence in
`RESEARCH.md`. IDs use the RD- scheme (no prior active scheme in this file).
Not duplicated: supply-chain payload pinning (done), MS Store removal (done),
signing/Velopack/winget (blocked — see `Roadmap_Blocked.md`).

### P1

### P3

## Research-Driven Additions (2026-07-09)

Items from the 2026-07-09 exhaustive research pass. Full evidence is in
`RESEARCH.md`. Existing signing, package identity, Velopack, winget,
Windows lifecycle, Mica, native launcher, and stock-restore decisions remain
in `Roadmap_Blocked.md`; the rows below are implementer-actionable.

### P1

### P2

## Audit Backlog (July 9, 2026)

Items surfaced by the July 9, 2026 deep audit pass but not fixed in-session.

## Research-Driven Additions

### P1

### P2

## Research-Driven Additions

### P1

### P2

## Research-Driven Additions (2026-07-22)

Items from the 2026-07-22 exhaustive research pass. Full evidence in
`RESEARCH.md`. IDs continue the `RD-` scheme (highest prior: RD-31). Not
duplicated: Intune/PDQ/WinRM deployment samples (done — `samples/deployment/`),
Defender `-defender_exclusions_off` gate (done), foreign-patcher detection
(done), package-manager manifests (blocked on package identity —
`Roadmap_Blocked.md`), native PS2EXE launcher (blocked), Stryker.NET (blocked,
now unblockable by RD-35).

### P1

- [ ] P1 — RD-32: Ship self-contained builds on the latest patched .NET 10 runtime
  Why: Both projects publish self-contained `win-x64`, so 2026 bundled-runtime CVEs (CVE-2026-32175 crafted-file arbitrary write, CVE-2026-26127 Base64Url OOB read, CVE-2026-45490, CVE-2026-50526) are only fixed by rebuilding against a patched runtime, not by the host's servicing.
  Evidence: `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:8` and `src/LibreSpot.Cli/LibreSpot.Cli.csproj:7` (`<RuntimeIdentifiers>win-x64`, no `<TargetLatestRuntimePatch>`); dotnet/announcements #396/#403/#415, dotnet/runtime#125393.
  Touches: both `.csproj` (add `<TargetLatestRuntimePatch>true</TargetLatestRuntimePatch>`), `Build-Scripts.ps1` dependency-health/preflight, README verification note.
  Acceptance: self-contained artifacts embed the newest .NET 10 servicing runtime the build SDK ships; a build/preflight check fails when the resolved runtime pack predates the documented CVE-patched floor; dependency-health output records the embedded runtime version. (Needs live validation: confirm the release publish path is self-contained; if it moves to framework-dependent this reduces to a documented minimum-host-runtime note.)
  Complexity: S

### P2

- [ ] P2 — RD-33: Guard against Spicetify v3's changed on-disk contract
  Why: Spicetify v3 (spicetify/cli #3038, unreleased as of 2026-07-22) replaces xpui injection with a symlink xpui→config + patched `index.html` "hooks" + generalized "modules" model, which would simultaneously break the three 2.x-filename patch-detection sites and make a healthy Spotify report as broken/unpatched.
  Evidence: github.com/spicetify/cli/issues/3038; `src/LibreSpot.Desktop/Services/EnvironmentSnapshotService.cs` (BuildSpotXComponent), `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1` (`Get-SpotXPatchVerification`), `LibreSpot.ps1` detection sites; `CLAUDE.md` three-site detection contract.
  Touches: `EnvironmentSnapshotService.cs`, `Backend/LibreSpot.Backend.ps1`, `LibreSpot.ps1`, `Properties/Strings*.resx`, xUnit + Pester detection tests.
  Acceptance: when the installed Spicetify CLI reports major version ≥ 3, all detection sites surface a localized "Spicetify 3.x is not yet supported — LibreSpot targets 2.x" state instead of a false broken/unpatched verdict; synthetic v3 version-string fixtures cover WPF, backend, and PS-GUI.
  Complexity: M

- [ ] P2 — RD-34: Re-verify upstream pins and record the deliberate pre-Defender SpotX hold
  Why: As of 2026-07-22 SpotX `main` targets Spotify 1.2.94 and, since commit `afb4c3f` (2026-07-11), adds Microsoft Defender exclusions by default (opt-out `-defender_exclusions_off`); Spicetify CLI 2.44.0 still caps at 1.2.93. The pinned SpotX `550bc72c`/1.2.93 predates `afb4c3f` and matches Spicetify's ceiling, so holding is the safest choice — but neither the verified date (2026-07-08) nor the compatibility copy records this reasoning or the advance trigger.
  Evidence: github.com/SpotX-Official/SpotX/commit/afb4c3f + /commits/main; github.com/spicetify/cli/releases/tag/v2.44.0; `src/LibreSpot.Desktop/Models/AppCatalog.cs:621-629`, `schemas/community-assets.json`. Answers the "is the April 2026 SpotX/Spicetify pin guidance still current?" Research Backlog question.
  Touches: `AppCatalog.cs` (`UpstreamPinsLastVerifiedAtUtc` + a hold-rationale note/const), `schemas/community-assets.json` verified date, README compatibility-matrix section.
  Acceptance: pins re-verified date updated to 2026-07-22; a documented note states LibreSpot deliberately holds at the pre-Defender SpotX commit and will advance the SpotX pin + Spotify target only once Spicetify declares 1.2.94+ support (at which point the refreshed adapter sets `defenderMutations=true` and passes `-defender_exclusions_off`); drift check and release-truth gate stay green.
  Complexity: S

### P3

- [ ] P3 — RD-35: Extract non-UI logic into a `LibreSpot.Core` class library
  Why: `MainViewModel.cs` is 4,871 lines with pure logic entangled in WPF types, bloating the god-ViewModel and blocking the filed Stryker.NET item (Stryker cannot analyze a `net10.0-windows`/`UseWPF` target). A WPF-free `net10.0` library both shrinks the ViewModel and unblocks mutation testing.
  Evidence: `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`; `Roadmap_Blocked.md` "Add Stryker.NET mutation testing" (blocked on this extraction).
  Touches: new `src/LibreSpot.Core/` (`net10.0`, no `-windows`/WPF), `MainViewModel.cs`, `Services/*`, tests project reference.
  Acceptance: pure logic (snapshot derivation, plan building, undo-policy evaluation, drift comparison) moves to `LibreSpot.Core` with direct unit tests; the library builds without WPF; the WPF ViewModel consumes it; the existing xUnit + FlaUI suites stay green; the Stryker blocked item can now target `LibreSpot.Core`.
  Complexity: L

- [ ] P3 — RD-36: Decompose `MainWindow.xaml` into per-screen UserControls
  Why: `MainWindow.xaml` is 5,509 lines holding all six nav screens (Home/Setup/Unblock/Tools/Settings/About) + the inspector in one file, slowing edits and raising merge/regression risk on the shipping shell.
  Evidence: `src/LibreSpot.Desktop/MainWindow.xaml`.
  Touches: `MainWindow.xaml`, new `Views/*.xaml` UserControls, `Themes/Controls.xaml`, FlaUI smoke + rendered-QA tests (AutomationIds/x:Names must be preserved).
  Acceptance: each nav screen becomes a UserControl under `Views/`; `MainWindow` composes them; every `AutomationId`/`x:Name` referenced by tests is preserved byte-for-byte; the rendered-WPF QA capture and FlaUI suite pass unchanged across dark/high-contrast and English/Spanish.
  Complexity: L

- [ ] P3 — RD-37: Add German and French WPF locales
  Why: the localization framework, runtime language selector, and strict validation gate (`tools/Sync-Localization.ps1`) already support five locales (en/es/pt-BR/ru/zh-Hans); de/fr are large Spotify-modding audiences and low-risk given the gate.
  Evidence: `src/LibreSpot.Desktop/Properties/Strings.*.resx` (five locales, no de/fr); `tools/Sync-Localization.ps1`.
  Touches: new `Strings.de.resx` / `Strings.fr.resx`, language-selector list, localization validation allowlist, health-component/scrollbar automation-name coverage.
  Acceptance: de and fr resource sets pass `Sync-Localization` (placeholder parity, no English carry-over, protected product/file tokens, no truncation); the language selector lists both; hidden long-text prompt rendering covers them.
  Complexity: L

- [ ] P3 — RD-38: Verify Spicetify CLI build attestations, not just SHA256
  Why: Spicetify publishes GitHub build-provenance attestations on its releases; LibreSpot pins only the SHA256, so the download's provenance (who/how built) is unverified — checking the attestation is a leapfrog trust signal over every competitor and hardens against an upstream build-pipeline compromise that keeps the same tag.
  Evidence: github.com/spicetify/cli/releases/tag/v2.44.0 (attestations); `src/LibreSpot.Desktop/Services/UpstreamDriftService.cs`, `schemas/community-assets.json` provenance model.
  Touches: the Spicetify CLI download/verify path, `schemas/community-assets.json` (attestation reference), dependency-health output, xUnit tests.
  Acceptance: the pinned Spicetify CLI download optionally verifies its GitHub attestation bundle (offline, against a cached signer identity) in addition to SHA256; a mismatch surfaces as a trust warning; when the attestation tooling/cert is unavailable it degrades to SHA256-only rather than failing closed.
  Complexity: M
