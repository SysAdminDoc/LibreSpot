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

- Stable script release: v3.7.2.
- Native WPF shell line: v4.0.0-preview.6.
- Release pipeline now builds PS2EXE and WPF artifacts with checksums, SBOMs, and
  build provenance attestations.
- Auto-reapply watcher, self-update checks, pre-patched Spotify detection,
  Spotify version selection, and the v3.7 UI refresh have shipped.
- Point-in-time dependency pins from the latest repo docs:
  - SpotX `3284673d` for Spotify `1.2.92`
  - Spicetify CLI v2.43.2
  - Marketplace v1.0.8
  - Spicetify themes `df033493`

## Next Release Queue

| Priority | Track | Work | Exit criteria |
|---|---|---|---|

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

- [ ] P1 — Add rendered localization and accessibility smoke coverage for every supported culture
  Why: Resource parity is tested, but rendered WPF clipping, focus order, and UI Automation names can still regress across Russian, Simplified Chinese, Brazilian Portuguese, and Spanish strings.
  Evidence: `src/LibreSpot.Desktop/Services/LocalizationService.cs`, `tests/LibreSpot.Desktop.Tests/LocalizationTests.cs`, `tests/LibreSpot.Desktop.Tests/WpfFlaUiSmokeTests.cs`, Microsoft WPF localization and UI Automation docs.
  Touches: WPF startup/test culture hook, `tests/LibreSpot.Desktop.Tests/WpfFlaUiSmokeTests.cs`, `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`, `src/LibreSpot.Desktop/MainWindow.xaml`.
  Acceptance: FlaUI launches the shell in `en`, `ru`, `zh-Hans`, `pt-BR`, and `es`, walks Recommended, Custom, Maintenance, confirmation prompt, and activity/status surfaces, and asserts visible primary controls, non-empty UIA names, keyboard focus targets, and no critical button/label clipping at the default window size.
  Complexity: M

- [ ] P2 — Add asset-cache inventory and health diagnostics
  Why: Verified cache fallback is a reliability primitive, but cache entries are hash-named files without source labels, age/size history, corruption summary, or support-bundle visibility.
  Evidence: `src/powershell/shared/Get-FromAssetCache.ps1`, `src/powershell/shared/Save-ToAssetCache.ps1`, `src/powershell/shared/Clear-LibreSpotCache.ps1`, Environment snapshot/support-bundle flows.
  Touches: `src/powershell/shared/*AssetCache*.ps1`, `src/LibreSpot.Desktop/Services/EnvironmentSnapshotService.cs`, support bundle export, operation journal tests, PowerShell regression tests.
  Acceptance: Cache writes maintain an index with hash, label, source URL when known, byte size, first seen, last used, and last verified; corrupt entries are removed or quarantined with a journal/log entry; Maintenance and support bundles show cache count, total size, stale/corrupt status, and clear-cache receipt.
  Complexity: M

- [ ] P2 — Add offline cached-install simulation tests
  Why: Download modules claim verified cache fallback, but the end-to-end behavior is not covered for network failure after a valid cached asset exists.
  Evidence: `src/powershell/shared/Module-InstallSpotX.ps1`, `Module-InstallSpicetifyCLI.ps1`, `Module-InstallMarketplace.ps1`, `Module-InstallThemes.ps1`, `Module-InstallCustomApps.ps1`, Intune/PDQ offline deployment expectations.
  Touches: PowerShell test fixtures, cache helpers, backend protocol fixtures, `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs`.
  Acceptance: Tests seed valid cached SpotX, Spicetify CLI, Marketplace, theme, and Stats/custom-app archives, simulate network failure, verify the backend uses only SHA256-verified cached assets with warning-level logs, and fail when the cached hash is missing or wrong.
  Complexity: L

- [ ] P1 — Add community asset drift and trust review health
  Why: Core upstream drift now covers SpotX, Spicetify CLI, Marketplace, themes archive, and Stats, but reviewed community extensions/themes/custom apps can still disappear, move, or change license/network behavior without a health signal.
  Evidence: `schemas/community-assets.json`, `tests/LibreSpot.Desktop.Tests/CommunityAssetsManifestTests.cs`, Spicetify Marketplace issues, Windhawk mod provenance model.
  Touches: `src/LibreSpot.Desktop/Models/AppCatalog.cs`, `src/LibreSpot.Desktop/Services/UpstreamDriftService.cs`, `src/LibreSpot.Desktop/Services/EnvironmentSnapshotService.cs`, `src/LibreSpot.Desktop/Services/SupportBundleService.cs`, `tests/LibreSpot.Desktop.Tests/CommunityAssetsManifestTests.cs`, `tests/LibreSpot.Desktop.Tests/UpstreamDriftServiceTests.cs`.
  Acceptance: A fixture-backed checker reports every community extension, theme, and custom app as current/behind/missing/degraded with source URL, pinned commit/hash, license, support state, fallback, and network behavior; Maintenance, CLI status JSON, and support bundles show degraded/missing/review-required assets without failing offline; tests fail when a reviewed asset lacks drift metadata.
  Complexity: M

- [ ] P1 — Add Marketplace post-apply visibility and repair evidence
  Why: LibreSpot verifies Marketplace files and `custom_apps`, but upstream Marketplace reports still include black-screen and no-apply states that need clearer post-apply evidence and recovery guidance.
  Evidence: `src/powershell/shared/Repair-Marketplace.ps1`, `src/LibreSpot.Desktop/Services/EnvironmentSnapshotService.cs`, Spicetify Marketplace issues.
  Touches: `src/powershell/shared/Repair-Marketplace.ps1`, `src/powershell/shared/Module-ApplySpicetify.ps1`, `src/LibreSpot.Desktop/Services/EnvironmentSnapshotService.cs`, `src/LibreSpot.Cli/Program.cs`, support bundle export, backend protocol fixtures, environment snapshot tests.
  Acceptance: Reapply and RepairMarketplace record file presence, manifest version, `custom_apps` registration, Spicetify apply output stage, `spotify:app:marketplace` open attempt, and last observed Spotify process/session result; CLI status and Maintenance distinguish "files installed" from "likely visible"; fixture tests cover missing files, config-only registration, apply failure, open-URI failure, and likely-visible success.
  Complexity: M

- [ ] P2 — Add high-contrast rendered theme smoke coverage
  Why: High-contrast palette parity is tested statically, but no rendered smoke proves focus rings, disabled states, dialogs, snackbars, log rows, and health cards remain readable when high-contrast resources are active.
  Evidence: `src/LibreSpot.Desktop/Services/ThemeManager.cs`, `src/LibreSpot.Desktop/Themes/HighContrastPalette.xaml`, `tests/LibreSpot.Desktop.Tests/ThemeManagerTests.cs`, Microsoft high contrast and UI Automation docs.
  Touches: `src/LibreSpot.Desktop/Services/ThemeManager.cs`, `src/LibreSpot.Desktop/Themes/*.xaml`, `src/LibreSpot.Desktop/MainWindow.xaml`, `tests/LibreSpot.Desktop.Tests/WpfFlaUiSmokeTests.cs`, `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`, `tests/LibreSpot.Desktop.Tests/ThemeManagerTests.cs`.
  Acceptance: Test hook launches the WPF shell with high-contrast palette active, walks Recommended, Custom, Maintenance, prompt, activity, snackbar/log, and support-bundle surfaces, asserts focusable controls have visible UIA names and resolved high-contrast brushes, and static lint rejects hardcoded colors outside palette files.
  Complexity: M

- [ ] P2 — Add RemoveSelfData privacy-erasure regression coverage
  Why: LibreSpot documents local data retention and exposes RemoveSelfData, but there is no end-to-end temp-root proof that config, profiles, logs, crashes, cache, backups, and watcher state are erased without later leaking raw paths or tokens.
  Evidence: `schemas/data-inventory.json`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`, `src/LibreSpot.Cli/Program.cs`, `tests/LibreSpot.Desktop.Tests/SupportBundleServiceTests.cs`.
  Touches: backend temp-root hooks, `src/powershell/shared/*`, `src/LibreSpot.Cli/Program.cs`, `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs`, `tests/LibreSpot.Desktop.Tests/CliApplicationTests.cs`, support bundle tests.
  Acceptance: A temp-root test seeds config, local profiles, operation journal, logs, crashes, cache, backups, and watcher state with path/token canaries; running RemoveSelfData or CLI uninstall `--purge` deletes LibreSpot-owned data, writes an irreversible receipt, leaves Spotify/Spicetify untouched, and subsequent status/support-bundle output contains no seeded raw paths or tokens.
  Complexity: M

- [ ] P2 — Add dependency freshness and advisory ratchet
  Why: NuGet audit is clean today, but `dotnet list package --outdated --include-transitive` shows stale test transitives that should be visible before they turn into advisory-driven work.
  Evidence: `Directory.Build.props`, `tests/LibreSpot.Desktop.Tests/DependencyAutomationTests.cs`, local `dotnet list package --vulnerable --include-transitive`, local `dotnet list package --outdated --include-transitive`, NuGet Audit docs.
  Touches: `Build-Scripts.ps1`, `Directory.Build.props`, `schemas/third-party-notices.json`, `tests/LibreSpot.Desktop.Tests/DependencyAutomationTests.cs`, package lock files.
  Acceptance: Local validation can emit a JSON dependency-health report with vulnerable packages, outdated direct packages, and outdated transitive packages; moderate+ vulnerabilities fail under `AuditPipeline`; direct package drift fails unless intentionally updated; accepted test-only transitive lag is recorded in a JSON allowlist with owner, reason, and recheck date.
  Complexity: S
