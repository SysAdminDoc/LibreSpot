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
| P0 | v4.0 stable | Finish WPF shell polish: Wpf.Ui controls, visual QA, completion toasts, undo-selected-actions pane, status dashboard, and repair/diagnostic flow. | Stable WPF build has parity with the script shell and passes release preflight. |
| P1 | Fleet CLI | Add silent install, JSON presets, detect/status JSON, NDJSON logs, validate, uninstall, dry-run, and deployment docs. | Admins can deploy LibreSpot idempotently through scripts or endpoint tools. |
| P1 | Diagnostics | Add status-at-a-glance and repair flows for Spotify, SpotX, Spicetify, backups, scheduled task state, and last patch time. | Common broken states are detected and expose one-click fixes. |
| P2 | Ecosystem catalog | Reconcile shipped theme/extension catalog against research; add remaining high-value themes/extensions and custom apps. | Catalog data and README agree with the actual installer behavior. |
| P2 | Windows shell integration | Add jump list, taskbar thumbnail buttons, tray minimize, `librespot://` protocol, `.librespot` import association, and actionable persistent toasts. | Shell affordances work for installed and portable scenarios. |
| P2 | Community sharing | Add local preset profiles, shareable URIs, bundled preset gallery, secure import preview, QR cards, changelog viewer, community links, and `COMPARISON.md`. | Users can save, import, share, and compare presets without a hosted service. |
| P3 | Custom patches editor | Add AvalonEdit JSON authoring for SpotX `patches.json`, schema linting, regex safety checks, dry-run matching, and import-from-URL review. | Power users can validate and stage custom patch sets safely. |
| P3 | Localization | Introduce resource-based UI strings, runtime culture switching, CI checks for raw strings, machine-translation prefill, and Crowdin sync. | EN/RU/ZH-Hans/PT-BR/ES can ship without hardcoded UI text. |

## v4.0 Stable Scope

The v4.0 stable cut should stay focused on the native shell and release trust:

- Adopt Wpf.Ui `TitleBar`, `Snackbar`/`InfoBar`, `NumberBox`, and `SplitButton`
  where they remove custom control code.
- Add completion toasts once COM activation/notification registration is
  reliable for the unpackaged app.
- Add the undo-selected-actions pane after a successful run for reversible
  operations such as update blocking, shortcuts, scheduled tasks, and config
  changes.
- Add a launch dashboard showing Spotify version, Spicetify version, SpotX
  state, last patch timestamp, watcher status, and backup count.
- Add repair/diagnostic buttons with per-issue fixes instead of a single broad
  reset path.
- Keep the release workflow's parse, version-sync, regression-test, checksum,
  SBOM, and attestation gates mandatory.

## Distribution And Trust

Distribution work is sequenced behind the rebrand and signing decisions (see
`Roadmap_Blocked.md`). Once those are resolved:

1. Publish winget manifests for portable assets.
2. Add Velopack packaging for the WPF shell.
3. Create a Scoop bucket with `checkver` and `autoupdate`.
4. Submit Chocolatey only after signing and checksum automation have settled.

Microsoft Store/MSIX remains a poor fit because the app needs to patch files in
the user's Spotify installation and interact with classic desktop locations.

## Fleet Deployment

Fleet work targets sysadmin use cases that competing Spotify customization
tools do not cover:

- `--silent`, `--quiet`, `--no-restart`, `--accept-eula`, and `/S` aliases.
- JSON answer files with schema validation.
- `--detect --json` with explicit compliant/not-installed/drift exit codes.
- NDJSON logs under `%ProgramData%\LibreSpot\logs\` with rotation.
- `uninstall --silent --purge --yes --keep-spotify`.
- `validate` with typo suggestions.
- `--dry-run` and PowerShell `-WhatIf` parity.
- Deployment examples for WinRM, PSRemoting over SSH, PDQ Deploy, Intune Win32
  apps, and SCCM-style return codes.

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

- [ ] 🔬 🤖 P1 - Extract shared
  PowerShell core logic into a generated script-module source.
  - Why: `LibreSpot.ps1` is 5,769 lines, the embedded WPF backend is 2,318
    lines, and a function inventory on 2026-06-04 found 58 function names shared
    between them. Shared functions include config normalization, SpotX parameter
    building, downloads, path management, Spicetify runners, watcher state,
    archive/install modules, and safety helpers. Hand-copying those functions
    across two scripts makes every future SpotX flag, download hardening, safe
    extraction, config migration, and process contract change vulnerable to
    one-lane drift.
  - Evidence: local function inventory on 2026-06-04,
    `LibreSpot.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`,
    `CHANGELOG.md:148`,
    `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:7`,
    https://learn.microsoft.com/en-us/powershell/scripting/developer/module/how-to-write-a-powershell-script-module,
    https://learn.microsoft.com/en-us/powershell/scripting/developer/module/how-to-write-a-powershell-module-manifest
  - Touches: PowerShell source layout, backend embedding step, release build,
    regression tests, developer docs.
  - Acceptance: repo has a shared PowerShell source module or source-fragment
    layout that generates both `LibreSpot.ps1` and the embedded backend script.
    Shared functions have one source of truth; lane-specific wrappers own only
    UI, event protocol, elevation, and host dispatch. Generated outputs are
    deterministic and include a banner naming their source commit.
  - Verify: build step regenerates both scripts and fails on dirty generated
    output; parser tests run against generated scripts; function inventory
    proves shared functions are not manually duplicated; release workflow uses
    generated artifacts only after tests pass.

- [ ] 🔬 🤖 P2 - Split WPF view-model
  state domains before adding more v4 stable UI.
  - Why: `MainViewModel.cs` currently coordinates environment snapshots,
    install/custom option editing, settings search, maintenance commands,
    activity streaming, prompt state, cancellation, log display, and external
    folder launching. Upcoming roadmap items add diagnostics, undo actions,
    toasts, preset sharing, localization, accessibility tests, parity manifests,
    and shell integration. Without state-domain boundaries, each UI feature
    increases the risk of broad property-change churn and hard-to-test command
    interactions.
  - Evidence: `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:106`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1365`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1436`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1562`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1945`,
    https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/
  - Touches: WPF view models, command services, tests, UI automation harness,
    localization work.
  - Acceptance: define sub-view-model or service ownership for environment
    summary, option editor, maintenance actions, activity/log stream, prompts,
    settings search, and release/update status. `MainViewModel` becomes a
    coordinator rather than the owner of every state transition. Public
    properties stay stable for XAML or are migrated with focused tests.
  - Verify: existing 72 desktop tests still pass; new unit tests cover each
    state domain without launching the full window; UI automation snapshots
    prove data context and command bindings still populate all three main tabs,
    activity overlay, and prompt overlay.

## 🔬 Researcher Queue (Cycle 17 - 2026-06-06)

Cycle 17 inspects theme selection and preview reliability across the stable
PowerShell GUI and native WPF shell. Cycle 4 already covers community asset
supply-chain pinning for install-time downloads; this pass focuses on the
user-facing browse/preview surface, stale screenshots, blocking image loads, and
WPF parity before v4 stable. Tags: 🔬 = researcher-added this cycle; 🤖 =
implementer-actionable now; 🔧 = operator-needed where catalog policy decisions
are required.

- [ ] 🔬 🤖 P1 - Replace theme ComboBoxes
  with a preview-backed WPF theme gallery before v4 stable.
  - Why: the stable script has 21 themes and hundreds of schemes plus a preview
    card, while the WPF shell currently exposes only `SelectedTheme` and
    `SchemeOptions` ComboBoxes and a text summary. Microsoft's control guidance
    recommends list/grid-style surfaces instead of ComboBoxes when choices
    carry images or multi-line detail, and Spicetify positions Marketplace as a
    visual way to discover and install themes. LibreSpot's WPF Custom flow
    should let users compare theme appearance, scheme count, JS-injection
    requirement, Marketplace-only fallback, and support state without opening
    Spotify first.
  - Evidence: `LibreSpot.ps1:684`,
    `LibreSpot.ps1:1858`,
    `LibreSpot.ps1:1861`,
    `LibreSpot.ps1:2253`,
    `src/LibreSpot.Desktop/MainWindow.xaml:904`,
    `src/LibreSpot.Desktop/MainWindow.xaml:911`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:421`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs:198`,
    https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/combo-box,
    https://spicetify.app/docs/customization,
    https://spicetify.app/docs/customization/themes
  - Touches: WPF catalog model, `MainWindow.xaml`, theme/scheme view models,
    settings search, accessibility tests, PowerShell/WPF parity manifest.
  - Acceptance: WPF Custom mode offers a searchable/filterable theme gallery
    with thumbnails, scheme chips, "requires theme.js" badges, Marketplace-only
    card, unavailable/disabled states from the catalog manifest, and keyboard
    navigation. Selecting a gallery card updates the same configuration fields
    as the current ComboBoxes, preserves the selection summary, and still
    supports power-user typed search for theme and scheme names.
  - Verify: UIA/FlaUI smoke tests navigate gallery cards by keyboard, assert
    every interactive card has an automation name/help text, select at least
    one official theme, one community theme, and Marketplace-only mode, and
    confirm the saved config contains the expected `Spicetify_Theme` and
    `Spicetify_Scheme`.

## 🔬 Researcher Queue (Cycle 18 - 2026-06-06)

Cycle 18 narrows the broad Community Sharing release-queue row into profile
export/import, local preset management, and Marketplace-state boundaries. Cycle
3 already covers schema versioning for the internal `config.json`; this pass
focuses on the separate user-facing file/URI experience and the safety preview
required before imported settings can mutate Spotify or Spicetify. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where hosted sharing or cloud policy decisions are required.

- [ ] 🔬 🤖 P1 - Add a local preset
  gallery with named profiles instead of one mutable remembered config.
  - Why: the WPF shell explains "saved profile" well, but the product still has
    one active config and one "load recommended defaults" action. Community
    sharing asks for local preset profiles and a bundled preset gallery; without
    a profile manager, export/import will feel like overwriting a hidden file.
    VS Code profiles and PowerToys backup/restore show users expect named
    settings sets, a visible management surface, and a restore path that does
    not require hand-editing AppData.
  - Evidence: `README.md:152`,
    `LibreSpot.ps1:1666`,
    `LibreSpot.ps1:2973`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:302`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:385`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:551`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1945`,
    `tests/LibreSpot.Desktop.Tests/ConfigurationServiceTests.cs:89`,
    https://code.visualstudio.com/docs/configure/profiles,
    https://learn.microsoft.com/en-us/windows/powertoys/general
  - Touches: configuration storage, WPF profile manager, PowerShell Custom mode,
    README, migration docs, backup/restore tests.
  - Acceptance: LibreSpot stores named local profiles under a dedicated profile
    directory, keeps one active profile pointer, and ships bundled templates
    such as Recommended, Minimal/Marketplace-only, Visual Theme, Lyrics Focus,
    Premium Account, and Recovery/Reapply. Users can duplicate, rename, delete,
    export, import, and set a default profile. Applying a profile writes the
    active `config.json` only after preview/confirmation and preserves the
    previous active profile for rollback.
  - Verify: tests cover creating profiles from recommended/current/imported
    configs, preventing duplicate names or invalid filenames, deleting the
    active profile, migrating the existing single `config.json` into the active
    profile store, and round-tripping profile display metadata without changing
    install behavior.

- [ ] 🔬 🤖 P2 - Design shareable
  URIs and QR cards as inert previews, not install commands.
  - Why: the roadmap calls for shareable URIs and QR cards, while Cycle 4
    separately asks for a `librespot://` protocol design. A share link that can
    immediately trigger patching would be too risky for an elevated Spotify
    modifier. Share URIs should only load a profile preview, verify schema and
    optional signature/hash metadata, and require a local confirmation before
    saving or applying.
  - Evidence: `ROADMAP.md:58`,
    `ROADMAP.md:59`,
    `ROADMAP.md:768`,
    `src/LibreSpot.Desktop/app.manifest:3`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1795`,
    https://code.visualstudio.com/docs/configure/profiles
  - Touches: protocol/file association design, import preview UI, QR generation
    helper, docs, tests.
  - Acceptance: `librespot://profile?...` or equivalent links open the import
    preview only. Profiles may be embedded only below a strict size cap or
    referenced by a HTTPS URL that must be fetched, size-limited, schema
    validated, and shown to the user before any config write. QR cards encode
    the same inert preview link plus a human-readable profile name and creation
    date.
  - Verify: protocol activation tests cover local file, HTTPS URL, malformed
    URL, oversized payload, unsupported schema, and cancel/confirm flows.
    Confirmed imports write config; canceled imports leave the active profile
    untouched and do not start the backend.

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

- [ ] 🔬 🤖 P1 - Ship a console-capable LibreSpot CLI artifact before fleet
  documentation.
  - Why: current release artifacts are GUI-first (`PS2EXE -NoConsole` and WPF
    self-contained). Admin tools need deterministic stdout, stderr, and process
    exit codes. A hidden GUI EXE with bolted-on flags will be harder to support
    than a dedicated CLI host.
  - Touches: release workflow, project structure, backend service boundary,
    signing/checksum/SBOM steps, README, package-manager manifests.
  - Acceptance: tagged releases publish a signed `LibreSpot.Cli.exe` or
    equivalent console-capable artifact alongside `LibreSpot.exe`,
    `LibreSpot.ps1`, and `LibreSpot-Desktop.exe`. The CLI can run without WPF,
    uses the same backend/version/catalog code as the GUI, supports Windows
    PowerShell 5.1-hosted backend execution, and writes only machine-readable
    output when `--json` or `--ndjson` is requested.
  - Verify: CI runs `LibreSpot.Cli.exe --version`, `status --json`, `detect
    --json`, `validate --answer-file samples/minimal.json`, and a dry-run
    install fixture on a Windows runner; release checksums, SBOM, provenance,
    and SignPath signing include the CLI artifact.

- [ ] 🔬 🤖 P1 - Build `detect` and `status` on the Cycle 20 health report.
  - Why: fleet detection should not invent separate logic. The same health
    model that powers the WPF dashboard should drive admin-facing JSON and
    detection exit codes, otherwise GUI and fleet state will drift.
  - Touches: `EnvironmentSnapshotService`/health report, CLI, watcher state,
    tests, Intune docs.
  - Acceptance: `status --json` emits a versioned health report with component
    statuses, versions, paths, last patch time, last watcher outcome, backup
    count, Marketplace/theme state, issue IDs, and recommended repair IDs.
    `detect --json` emits a smaller compliance object with `state` values such
    as `compliant`, `notInstalled`, `partial`, `drifted`, `needsRepair`,
    `blocked`, and `unknown`. Intune-oriented `detect --intune` maps only
    compliant state to STDOUT plus exit `0`; all noncompliant states exit
    nonzero without mutating the machine.
  - Verify: fixture tests assert JSON schema validity and exit codes for clean
    slate, compliant, Spotify-only, Spicetify-only, Marketplace missing,
    watcher stale, and post-update drift states.

- [ ] 🔬 🤖 P2 - Publish deployment runbooks and package-manager validation
  samples only after the CLI contract is real.
  - Why: winget, Intune, PDQ, and SCCM docs are easy to write prematurely, but
    they become harmful if they depend on switches or exit codes the executable
    does not actually support.
  - Touches: `docs/deployment`, winget/Scoop/Chocolatey manifests, README,
    release workflow, CI smoke tests.
  - Acceptance: docs include tested examples for Intune Win32 app packaging,
    detection script, return-code table, uninstall command, PDQ Deploy, WinRM,
    PSRemoting over SSH, SCCM-style return codes, winget local manifest
    validation, Scoop manifest validation, and Chocolatey packaging. Every
    command in the docs is either exercised in CI or marked manual-only with a
    reason.
  - Verify: docs command snippets run through a PowerShell parser/smoke harness;
    winget local manifest validation, Scoop manifest install/checkver, and
    Chocolatey package validation are represented in release preflight once the
    CLI artifact exists.

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

- [x] P1 — Wire .resx localization strings into WPF runtime code
  Why: `Properties/Strings.resx` was created with 47 extracted strings and
  `PublicResXFileCodeGenerator` configured, but `MainViewModel.cs` and
  `MainWindow.xaml` still contain ~265 hardcoded English strings with zero
  references to `Strings.` — the infrastructure exists but is inert.
  Evidence: `grep -c "Strings\." src/LibreSpot.Desktop/ViewModels/MainViewModel.cs` = 0;
  `src/LibreSpot.Desktop/Properties/Strings.resx` has 47 `<data>` entries;
  `src/LibreSpot.Desktop/MainWindow.xaml` has ~65 hardcoded `Text=` strings.
  Touches: MainViewModel.cs, MainWindow.xaml, AppCatalog.cs, Strings.resx
  Acceptance: all user-visible strings in the WPF shell reference `Strings.`
  or a XAML markup extension backed by .resx; hardcoded English string count
  outside resource references drops to zero or has explicit exceptions.
  Complexity: L

- [ ] P2 — Consume operation-token and run-receipt schemas at runtime
  Why: `schemas/operation-token-types.json` (15 token types) and
  `schemas/run-receipt-format.json` exist with test coverage but zero
  runtime references. The v4.0 stable scope calls for an undo-selected-
  actions pane, which needs operation tokens to function.
  Evidence: `grep -c "operation-token" src/ LibreSpot.ps1` = 0;
  `schemas/operation-token-types.json`; ROADMAP v4.0 scope line 66.
  Touches: Backend action model, BackendScriptService event parsing,
  MainViewModel maintenance pane, operation journal writer.
  Acceptance: mutating actions emit typed operation tokens with
  `previousStateRef` for reversible operations; the WPF maintenance pane
  renders a post-run receipt showing which actions can be undone.
  Complexity: L

- [ ] P2 — Consume fleet CLI schemas when building the CLI artifact
  Why: `fleet-exit-codes.json`, `fleet-cli-contract.json`, `diagnostic-
  event-ids.json`, `ndjson-log-format.json`, and `librespot-answer.
  schema.json` define a complete fleet contract but are not referenced
  by any runtime code. The CLI artifact (existing P1 roadmap item) should
  consume these schemas rather than re-inventing the contracts.
  Evidence: `grep -c "fleet-exit-codes\|fleet-cli-contract\|diagnostic-
  event-ids\|ndjson-log-format" src/ LibreSpot.ps1` = 0.
  Touches: future LibreSpot.Cli project, BackendScriptService, exit
  code handling, NDJSON log writer.
  Acceptance: the CLI artifact's verb parser, exit codes, and log format
  are derived from or validated against the existing schemas. Schema
  changes fail CI if the CLI implementation diverges.
  Complexity: M (sequenced after CLI artifact exists)

- [ ] P3 — Adopt CommunityToolkit.Mvvm during the view-model split
  Why: the project uses hand-rolled `ObservableObject` and `RelayCommand`
  (22-line + 50-line custom implementations) instead of CommunityToolkit.
  Mvvm 8.4.2 which provides source generators, `[ObservableProperty]`,
  `[RelayCommand]`, partial property support, and analyzers.
  Evidence: `src/LibreSpot.Desktop/ViewModels/ObservableObject.cs`;
  `src/LibreSpot.Desktop/ViewModels/RelayCommand.cs`;
  https://www.nuget.org/packages/CommunityToolkit.Mvvm.
  Touches: ViewModels/*.cs, LibreSpot.Desktop.csproj.
  Acceptance: custom MVVM plumbing replaced with CommunityToolkit.Mvvm;
  source generators eliminate boilerplate property/command declarations.
  Complexity: M (best done during the existing P2 view-model split)

## Audit Findings (June 20, 2026)

Items below were found during the engineering audit but could not be
fixed due to .NET 10 SDK requirements or requiring broader design
decisions.

- [ ] P2 — Add journal entries for registry and AppxPackage removals in Module-NukeSpotify
  Why: registry key deletions (backend lines 2555-2571), Remove-AppxPackage
  (lines 2498-2512), and scheduled task removal in the nuker (lines 2573-2588)
  do not write operation journal entries, unlike the dedicated watcher task
  functions. This creates a gap in the audit trail for destructive operations.
  Where: `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`, `LibreSpot.ps1`

- [ ] P2 — Address 52 drifted shared functions between the two PowerShell scripts
  Why: `Build-Scripts.ps1 -Validate` confirms 52 of 86 shared functions have
  divergent implementations. Each drift is a silent regression risk for every
  future change. The drift validator CI step catches new divergence but the
  existing 52 need reconciliation as part of the shared-core extraction (P1).
  Where: `LibreSpot.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`

- [ ] P3 — Mitigate potential deadlock in Clear-CompletedRunspaceResources
  Why: if the worker runspace is blocked inside Dispatcher.Invoke() while the
  UI thread calls Clear-CompletedRunspaceResources from Add_Closing, the
  Start-Sleep(150) followed by runspace Dispose() can deadlock both threads.
  The fix should set IsRunning=$false, skip the sleep, and use non-blocking
  disposal or a background thread for cleanup.
  Where: `LibreSpot.ps1` (Clear-CompletedRunspaceResources, ~line 3211)
