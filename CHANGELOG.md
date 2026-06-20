# Changelog

All notable changes to LibreSpot will be documented in this file.

## [Unreleased]

### Added
- Upstream dependency freshness check in CI. A new non-blocking step
  compares pinned Spicetify CLI and Marketplace versions against the latest
  GitHub releases and emits warning annotations when any pin falls behind.
  Results appear in the CI summary.
- Marketplace framed as optional with direct-install-first messaging. The
  Custom Install UI now labels Marketplace as "(optional)" with clear copy
  that themes and extensions are installed directly by LibreSpot regardless
  of the Marketplace checkbox. A health warning about the upstream reset-on-
  close bug (spicetify/cli#3837) appears when Marketplace is enabled. README
  FAQ updated with a workaround for users experiencing the reset issue.
- SpotX/Spicetify version compatibility warning badge in the footer of Easy
  Install and Custom Install modes. When the SpotX-targeted Spotify version
  exceeds Spicetify's max-tested range, a visible warning appears near the
  install button. The Maintenance mode snapshot also flags the gap. The
  `Update-CompatibilityWarningBadge` function reads the existing
  `Get-LibreSpotCompatibilityWarnings` data and surfaces it visually.
- Catalog refresh checklist (`schemas/catalog-refresh-checklist.json`) with 8
  weighted evaluation criteria (popularity, maintenance, license, install method,
  Spotify compatibility, Marketplace availability, security posture, user value),
  accept/reject/defer/marketplace-only decisions, evaluation records for all 7
  shipped community assets, and 5 rejection examples covering no-license,
  archived, obfuscated, duplicate, and build-required candidates.
- Async theme preview loading in the PowerShell GUI. Preview images now
  download on a ThreadPool thread instead of blocking the UI with synchronous
  WebClient.DownloadData + DoEvents. Stale requests are cancelled via a
  monotonic request ID so fast theme switching never overwrites the current
  selection with an older download. Downloads are size-bounded (4 MB),
  decoded to 640px thumbnails, streams are properly disposed, and 404/timeout
  errors show a placeholder without freezing navigation.
- Keyboard and focus contract schema (`schemas/keyboard-focus-contract.json`)
  documenting tab order, default/cancel buttons, Escape behavior, focus trap/
  restoration, and custom focus ring strategy for all WPF interactive surfaces.
  Regression tests validate XAML keyboard bindings, overlay TabNavigation=Cycle,
  IsCancel/IsDefault on prompt buttons, focusable activity root, custom focus
  ring styles, focus save/restore in code-behind, and contract schema coverage.
- Localization extraction infrastructure: `Properties/Strings.resx` with 50+
  extracted UI strings covering app titles, navigation labels, activity status,
  health severity, maintenance actions, search, buttons, progress states,
  install options, and config status. Auto-generates a `Strings.Designer.cs`
  accessor via `PublicResXFileCodeGenerator`. Tests validate .resx structure,
  key uniqueness, non-empty values, translator comments, and core key presence.
  This is the first step toward satellite assembly localization.
- Preflight plan action (`Plan`) in the WPF backend that emits structured
  JSON plan entries for every operation an install would perform — downloads,
  SpotX patching, Spicetify CLI, themes, extensions, Marketplace, config saves,
  and watcher tasks — without mutating disk, PATH, or scheduled tasks. Each
  entry carries category, target, wouldChange, safetyDecision, reversible,
  requiresElevation, and source fields. This is the foundation for `--dry-run`
  in the fleet CLI and the WPF confirmation summary.
- Legacy PowerShell GUI accessibility gate: 16 AutomationProperties.Name
  attributes on the main window, titlebar icon-only buttons, navigation
  RadioButtons, StackPanel-content maintenance/action buttons, and destructive
  action controls. Screen readers can now identify every interactive control.
  Regression tests lock the minimum accessibility contract.
- NDJSON log format specification (`schemas/ndjson-log-format.json`) defining
  the newline-delimited JSON line schema for fleet CLI output, log files, and
  receipt event references. Each line carries schemaVersion, eventId (cross-
  referencing diagnostic-event-ids.json), timestamp, level, component, verb,
  operationId, correlationId, target, message, and optional payload. Includes
  output mode specs for stdout and file rotation, redaction rules matching the
  support bundle service, and example log lines.
- Hash mismatch diagnostic classification in both PowerShell lanes.
  `Get-NetworkDiagnosticCode` now returns `HashMismatch` for SHA256
  verification failures, and `Get-DownloadFailureHint` provides actionable
  recovery guidance. `Confirm-FileHash` includes the keyword in its error
  message for classifier detection.
- Operation journal coverage for all ShouldProcess-enabled functions. Config
  saves, scheduled task register/unregister, PATH entry changes, cache
  clearing, and config quarantine now write structured JSONL journal entries
  with planned/result phases, reversibility flags, and rollback hints in both
  the stable script and WPF backend (13 journal calls per script).
- Reversible operation token registry (`schemas/operation-token-types.json`)
  with 15 token types covering config writes, PATH changes, scheduled tasks,
  shortcuts, update blocking, Spicetify apply, SpotX patches, and destructive
  operations. Each type declares reversibility, previous-state capture, undo
  action, admin requirement, and risk level.
- Run receipt format (`schemas/run-receipt-format.json`) defining post-run
  receipt structure with metadata, operation tokens, undo availability, and
  status values for success, failed, canceled, dry-run, and partial results.
- `.librespot` profile format schema (`schemas/librespot-profile.schema.json`)
  for user-facing export/import. Includes metadata (generator, version, creation
  time, dependency pins, OS/arch hints), a `settings` object matching config
  properties, and security invariants (no credentials, no RiskAcknowledged
  export, unknown schema versions rejected, import opens preview). Tests
  validate required fields, consent field exclusion, settings structure, and
  differentiation from the fleet answer file.
- Shared theme preview manifest (`schemas/theme-preview-manifest.json`) with 22
  entries covering all 16 official themes, 5 community themes, and Marketplace-
  only mode. Each entry records source repo, commit SHA, scheme list, JS
  injection requirement, preview URL with status (available/unavailable/broken/
  placeholder), and support state. Official themes use commit-pinned URLs;
  community themes are marked unavailable until their preview URLs are verified.
  Tests validate field completeness, uniqueness, source/status enums, commit-
  pinned URL enforcement, JS requirement consistency with the script, and
  community theme coverage.
- Publish footprint budget with compressed artifact size tracking and build-mode
  rationale (`schemas/publish-footprint-budget.json`). Release CI now records
  compressed size and compression ratio alongside raw size, and documents why
  WPF trimming is disabled (unsupported by Microsoft) and ReadyToRun is
  deferred (startup dominated by WPF/PS init, not JIT).
- Fleet CLI verb and flag contract (`schemas/fleet-cli-contract.json`) defining
  12 verbs (install, reapply, detect, status, validate, plan, repair, watcher
  install/remove, uninstall, export-support, version) with elevation requirements,
  mutation flags, output format support, applicable flags, exit code references,
  and parser behavior rules for typo suggestions and conflict detection.
- Stable diagnostic event IDs (`schemas/diagnostic-event-ids.json`) with 44
  events across 13 categories (lifecycle, download, SpotX, Spicetify,
  Marketplace, watcher, health, journal, config, PATH, task, network, security).
  Each event carries a stable LS-prefixed ID, severity, and payload fields so
  log meaning stays decoupled from display copy.
- Fleet exit code taxonomy (`schemas/fleet-exit-codes.json`) mapping LibreSpot
  domain outcomes onto Intune/SCCM/PDQ/WinRM return-code categories with 14
  documented exit codes for success, validation, drift, network, trust, and
  permission failures.
- Fleet answer file schema (`schemas/librespot-answer.schema.json`) defining
  strict-validation JSON Schema for silent/fleet deployments with required
  consent fields, install mode, SpotX/Spicetify options, watcher, repair,
  logging, and reboot policies.
- Pester 5.x test infrastructure for the PowerShell script lane. 108 tests
  cover 7 pure functions extracted from the monolith: `Get-NormalizedPathString`,
  `ConvertTo-ConfigInt`, `ConvertTo-ConfigBoolean`,
  `Get-LibreSpotConfigSchemaVersion`, `Assert-LibreSpotConfigSchemaSupported`,
  `Normalize-LibreSpotConfig`, and `Compare-LibreSpotVersions`. Tests use
  regex-based function extraction to avoid sourcing the WPF bootstrap.
- PowerShell `-WhatIf` and `-Confirm` support on mutating helpers.
  `Remove-PathSafely`, `Save-LibreSpotConfig`, `Set-PathEntries`,
  `Register-AutoReapplyTask`, `Unregister-AutoReapplyTask`,
  `Clear-LibreSpotCache`, and `Move-ConfigFileToQuarantine` now declare
  `SupportsShouldProcess` and gate actual mutations behind
  `$PSCmdlet.ShouldProcess()` in both the stable script and WPF backend.
  Regression tests lock the contract for all 14 function instances.
- Verification-first bootstrap in README Quick Start. The primary install
  command now downloads `LibreSpot.ps1` and `checksums.txt` to a local path,
  validates SHA256 before execution, removes the script on mismatch, and saves
  the verified file to `%LOCALAPPDATA%\LibreSpot\bootstrap` for reusable
  launches. The original `irm | iex` one-liner is preserved as a labeled
  lower-trust advanced option. Regression tests ensure the bootstrap references
  valid release assets and downloads before executing.
- Structured operation journal foundation in both PowerShell lanes. Install and
  maintenance runs now write JSONL entries with operation IDs, planned/complete
  results, safe-removal decisions, targets, dry-run flags, and rollback hints;
  WPF support bundles include the new `operation-journal.jsonl` tail.
- Post-upload contract audit in the release workflow. After asset upload, CI now verifies every required artifact exists exactly once, checksums.txt covers all expected assets, prerelease flags match the tag channel, and build provenance attestations exist for each subject.
- Support-bundle and Spicetify version fields in GitHub issue templates for bug reports and compatibility breakage. Templates now guide users to attach the WPF export bundle first, with raw log paste as a fallback.
- Community asset license policy enforcement. Manifest validation now fails if NOASSERTION or review-required license assets have `easyModeDefault=true` without an explicit `policyOverride`. Beautiful Lyrics, Hazy, and Hide Podcasts carry operator-approved overrides. Tests validate all licenses are known to policy and that overrides have required fields.
- Architecture-aware Spotify target validation. The WPF version picker now tags each manifest entry with its architecture (`any`, `x64`, `x86`, `legacy-os`) and shows a mismatch warning in the selection insights panel when an incompatible target is selected for the host architecture.
- Local data security and retention inventory (`schemas/data-inventory.json`) covering config, logs, crash reports, watcher state, asset cache, backups, and runtime copies with sensitivity, retention, redaction rules, and export behavior.
- Curated custom-apps catalog tier in `schemas/community-assets.json` with `stats` and `new-releases` from harbassan/spicetify-apps (MIT, opt-in only). Five new manifest tests enforce field requirements, uniqueness, license gating, and appId/assetPath consistency.
- PSScriptAnalyzer lint gate in CI with curated 4-rule set (warning-only): `PSAvoidUsingCmdletAliases`, `PSAvoidUsingInvokeExpression`, `PSAvoidUsingPlainTextForPassword`, `PSUseApprovedVerbs`. Results appear as GitHub Actions annotations and step summary.
- Verified local asset cache for offline/degraded installs. Successfully hash-verified downloads are now cached under `%APPDATA%\LibreSpot\cache\` keyed by SHA256. Before network fetches, the cache is checked first; on network failure, a verified cached copy is used as fallback with clear logging. All 15 download sites in both the script and WPF backend are wired through the cache. `Clear-LibreSpotCache` clears the cache directory.
- WPF post-Spotify-update triage in the typed health report. Maintenance now compares current Spotify, last patched Spotify, watcher tick/outcome, last successful apply, Spicetify apply rollback, and Marketplace readiness, then recommends targeted actions such as close Spotify, reapply, repair Marketplace, restore vanilla, or open logs without jumping straight to full reset.
- Privacy-safe WPF support bundle export from Maintenance. The new local-only zip includes a redacted typed health report, runtime/version and catalog pin metadata, operation journal slices, selected log windows, and selected crash-report windows; the UI previews selected file windows, estimated size, and redaction rules before writing.
- WPF Maintenance now reuses the typed health report for stable backup, Marketplace, active-theme, and five-component readiness diagnostics. Maintenance actions are hidden unless the current health state makes them relevant, and Marketplace can be opened directly as a no-admin read-only action when its files and `custom_apps` registration are ready.
- WPF stack health report for the v4 dashboard. `EnvironmentSnapshotService` now emits typed component records for Spotify, SpotX patch markers, Spicetify CLI/config, Marketplace, active theme, backups, auto-reapply watcher state, logs, crash reports, and the saved LibreSpot profile, with severity groups and recommended repair IDs rendered in the sidebar. Fixture tests cover ready, clean-slate, partial install, Marketplace missing, theme injection mismatch, missing backup, stale watcher, and recent crash states.
- WPF-UI 4.3.0 runtime package selected as the v4 shell control library, with `WPF-UI` explicitly documented as the correct NuGet ID and a WPF smoke test proving `TitleBar`, `InfoBar`, `NumberBox`, `SplitButton`, and `Snackbar` load under LibreSpot's existing theme resources. Third-party notices now include WPF-UI and its abstractions package.
- OpenSSF Scorecard workflow (`.github/workflows/scorecard.yml`): a weekly + push-to-`main` supply-chain hygiene scan that publishes to the public Scorecard API (new README badge) and uploads SARIF/JSON/triage artifacts. Actions are full-SHA pinned; `schemas/scorecard-baseline.json` records accepted single-maintainer risks, the workflow fails on unaccepted score regressions, and `SECURITY.md` documents the policy that low scores become roadmap items rather than silent warnings. Regression tests lock the workflow's triggers, publish configuration, triage gate, and baseline shape.
- Network-behavior disclosure for every community asset. `schemas/community-assets.json` now carries a `networkBehavior` (`local-only` / `third-party-service`) plus a `networkDetail` field on each extension and theme, a CI test enforces it (third-party assets must explain what they contact), the Custom Install catalog flags networked extensions, and the README trust claim is scoped to LibreSpot itself with an explicit note that opt-in extensions like Beautiful Lyrics contact their own services. Closes the gap where the "only GitHub and Spotify" claim was falsifiable by enabling a bundled extension.
- Opt-in Spicetify-layer ad-block fallback (`adblock.js`, rxri/spicetify-extensions, MIT) selectable in Custom Install. When SpotX patching breaks on a newer Spotify build (SpotX issue #760), ad-blocking can keep working at the Spicetify layer through the existing commit-pinned + SHA256-verified community-extension pipeline. It is documented as a fallback (not a SpotX replacement), is not an Easy-mode default, and the post-install SpotX verification check now suggests enabling it when patching cannot be confirmed. Wired through both backends, the WPF catalog, the config schema, and the community-asset supply manifest.
- SpotX post-patch effectiveness verification (`Get-SpotXPatchVerification`) in both the script and WPF backend: a clean SpotX exit code no longer counts as proof the patch landed. The installer now asserts the on-disk markers SpotX leaves (`Apps\xpui.spa` plus the pre-patch `Apps\xpui.spa.bak` backup) and surfaces "patched and verified" vs "ran but unverified" with a recovery hint referencing SpotX signature-protection issue #760, instead of always logging success.
- High-contrast and reduced-motion theme contract for the WPF shell: detects system settings, swaps the palette to SystemColors-mapped resources, disables shadows/glows/gradients, and zeroes motion durations.
- Script/WPF/backend parity manifest (`schemas/parity-manifest.json`) with CI-enforced tests: every config key, default value, backend action, and maintenance UI entry is tracked across lanes, and adding a key or action without updating the manifest fails CI.
- Distribution channel matrix (`schemas/distribution-matrix.json`) covering GitHub Releases, PowerShell one-liner, winget, Scoop, Chocolatey, Velopack, and PSGallery with per-channel target audience, artifact role, signing requirement, update owner, and blocking decisions.
- Community asset supply manifest (`schemas/community-assets.json`) tracking every community extension and theme with commit SHA, SHA256, source repo, SPDX license slot, support state, and fallback behavior; CI tests validate manifest data against live script entries.
- Third-party notices manifest (`schemas/third-party-notices.json`) covering NuGet packages, SpotX, Spicetify CLI, Marketplace, themes archive, PS2EXE, and CycloneDX with SPDX license, redistribution posture, and license policy tiers; CI tests validate versions and license coverage against live project files.
- Release artifact contract (`schemas/release-artifact-contract.json`) defining expected assets, checksums, signing state, and attestation requirements per release channel with tag-pattern validation and historical release exemptions; CI tests verify the contract matches the actual workflow.
- Security policy (`SECURITY.md`) with supported versions, private vulnerability reporting, scope definitions, and upstream dependency guidance.
- GitHub issue templates for bug reports, compatibility breakage, and feature requests with structured fields for OS, Spotify version, LibreSpot variant, and sanitized diagnostics; blank issues disabled in favor of forms.
- Structured release notes configuration (`.github/release.yml`) categorizing PRs into breaking changes, security, features, bug fixes, compatibility, performance, dependencies, and docs; Dependabot PRs excluded from the main changelog.
- Draft package manifests for winget (3-file YAML), Scoop (JSON with `checkver`/`autoupdate`), and Chocolatey (nuspec + install script) under `packaging/`; all gated behind signing readiness with placeholder hashes.
- Trust and risk disclosure section in README covering what LibreSpot does/does not do, account risk context with ToS reference, and recovery instructions.
- Elevation boundary matrix (`schemas/elevation-boundary.json`) classifying every action as no-admin, prompts-for-admin, admin-only, or scheduled-task with mutating/destructive/toast-compatible flags; CI tests validate against live backend AllowedActions and AppCatalog.

- Added pull-request dependency review workflow that blocks vulnerable dependencies (moderate+) and disallowed licenses (AGPL-3.0-only, GPL-3.0-only) before merge.
- Added `-RemoveSelfData` CLI flag and "Remove LibreSpot data" WPF maintenance action that unregisters the watcher scheduled task and removes all LibreSpot-owned config, backups, logs, and crash reports without affecting Spotify or Spicetify.

### Changed
- WPF section frames now use the shared 12 px radius token instead of a
  hardcoded 18 px corner, keeping the shell within the documented radius system.
- WPF desktop shell second polish pass: softened global scrollbars, moved hero and sidebar micro-labels to title case, replaced backend-centric activity overlay copy with product-level run-log language, fixed log-count pluralization, and cleaned up support-bundle preview wording.
- WPF desktop shell polish pass: normalized the radius system to 6-12 px, removed pill-shaped badge/progress treatments, shortened the first-run rail copy, hid non-actionable informational health details from the sidebar, fixed Custom option-card title wrapping, forced dark native DWM caption colors, and made activity log empty/count states bind directly to the log collection. The UIA smoke harness now checks stable visible landmarks and named actionable controls.
- Community theme downloads are now commit-pinned and SHA256-verified, matching the existing integrity model for community extensions and the official themes archive. All five community themes (Catppuccin, Comfy, Bloom, Lucid, Hazy) use immutable commit-SHA archive URLs with `Confirm-FileHash` verification instead of mutable branch-based downloads. CI tests enforce that no branch-pinned archive URLs remain and that commit SHAs and hashes stay consistent across the script, WPF backend, and community-assets manifest.
- Dependency update checks (`Check-ForUpdates`) now use `Invoke-GitHubApiSafe` which reads `x-ratelimit-remaining` and `x-ratelimit-reset` headers, warns when rate limits are nearly exhausted, and provides actionable error messages with reset times for HTTP 403/429 responses instead of generic failure messages.

- ConfigurationService: handle `FileNotFoundException` separately from corrupt-config in `LoadResultAsync` — a config file deleted between the existence check and the open now correctly returns `Missing` instead of quarantining a non-existent file as corrupt.
- ThemeManager: fixed palette dictionary search using fragile `Contains("Palette.xaml")` that matched both palette filenames — now uses explicit `EndsWith` checks for each known palette.
- ThemeManager: reduced-motion `MotionFast/Med/Slow` double overrides are now cleared when the user re-enables animations, preventing permanently zeroed motion values for the session.
- Extracted hardcoded terminal colors (`#080B0A` background, `#D6E4DB` foreground) from MainWindow.xaml and Controls.xaml into `TerminalBgBrush`/`TerminalFgBrush` palette tokens with proper SystemColors mapping in high-contrast mode.
- Added `AutomationProperties.Name` to the settings search clear button and installation progress bar.

### Fixed
- WPF health diagnostics now reject path-like Spicetify extension entries before
  probing the Extensions folder, preventing corrupt config values from escaping
  the intended diagnostic boundary.
- WPF support bundles now redact JSON-escaped and slash-normalized local paths
  and omit non-UTF-8 diagnostic payloads instead of replacement-decoding them.
- CodeQL workflow job permissions now explicitly retain `contents: read` while
  granting `security-events: write`, keeping checkout reliable under narrowed
  job permissions.
- WPF health diagnostics no longer report post-update "No drift" when Spotify
  is missing or no watcher history exists; those states now show clear
  informational guidance instead.
- WPF confirmation prompts now make Enter activate the safe default action only
  for non-destructive prompts; destructive prompts keep focus on cancel and no
  longer expose confirm as the implicit default.
- Operation journal writes now cap the local JSONL history and insert a
  structured retention marker before trimming old entries, preventing long-lived
  installs from growing `%APPDATA%\LibreSpot\operation-journal.jsonl` without
  bound.
- WPF backend "Remove LibreSpot data" no longer recreates `%APPDATA%\LibreSpot`
  while reporting success after deleting that profile; it removes the active
  config profile last and switches final reporting to the event protocol.
- Network preflight dialogs now report classified DNS, TLS/certificate,
  proxy-auth, GitHub block/rate-limit, timeout, and HTTP failures instead of a
  generic offline message; GitHub update checks also route non-rate-limit
  failures through the same classifier.
- Download failures in both PowerShell lanes now classify common DNS, TLS/certificate, proxy-auth, GitHub block/rate-limit, and timeout causes before falling back to BITS or reporting final failure.
- WPF UI automation smoke coverage now asserts that the activity run-status live region is exposed as polite for assistive technologies.
- BackendScriptService now holds the verified `.run.ps1` execution copy open with read-only sharing until the backend process exits, closing the local swap window between hash verification and `powershell.exe` startup.
- BackendScriptService cleans stale `.run.ps1` execution copies on WPF startup so crashed previous runs do not accumulate in the runtime directory.
- EnvironmentSnapshotService now drains `schtasks.exe` stdout and stderr before the bounded wait, preventing pipe-buffer deadlocks in the auto-reapply task probe.
- Watcher state writes in both PowerShell lanes now use temp-file replace/move semantics, preventing truncated `watcher-state.json` after a killed watcher tick.
- WPF async commands now route awaited exceptions through the activity/log surface instead of fire-and-forget task handling.
- Forward-incompatible saved profiles now show a specific "newer LibreSpot build" recovery notice instead of generic corrupt-profile copy.
- ThemeManager: `ClearReducedMotionOverrides` now clears all 6 motion resource keys (including `MotionFastDuration`, `MotionMedDuration`, `MotionSlowDuration`) that `ApplyReducedMotion` sets. Previously only the 3 Double keys were cleared, leaving Duration overrides permanently stuck at 1ms until app restart.
- MainViewModel: `RefreshSnapshotAsync` wrapped in try/catch so an environment probe failure (schtasks timeout, WMI error, permission denied) does not crash the app when the user clicks Refresh.
- EnvironmentSnapshotService: `IsSpotifyRunning` now disposes Process handles returned by `GetProcessesByName` instead of leaking native handles on every snapshot probe.
- AppCatalog: `CheckArchitectureCompatibility` now warns when an x64 Spotify build is selected on an ARM64 host, noting that SpotX/Spicetify patches are untested under x64 emulation.
- PrettifyConverter: `ConvertBack` now throws `NotSupportedException` instead of returning the input, preventing silent data corruption if the converter is accidentally used on a two-way binding.
- SupportBundleService: `ExportAsync` now writes to a temporary `.tmp` file and moves to the final path only after the ZIP is complete, preventing orphaned partial/corrupt ZIP files on cancellation or error.
- ConfigurationService: `QuarantineCorruptConfig` fallback path now uses `overwrite: true` for consistency with the primary quarantine path.
- LibreSpot.ps1: `Test-SafeRemovalTarget` blocklist expanded from 7 to 17 entries to match the backend — adds ProgramData, ALLUSERSPROFILES, PUBLIC, OneDrive paths, Desktop, Documents, CommonDesktopDirectory, and CommonStartMenu.
- LibreSpot.ps1: community theme install now uses explicit `-LiteralPath` in 4 path operations to prevent wildcard glob expansion on paths with bracket/wildcard characters.
- BackendScriptService: fixed temp `.run.ps1` file leak when `process.Start()` throws — execution copy is now cleaned up on launch failure.
- BackendScriptService: moved cancellation token registration before `process.Start()` so cancellation works even if the process hangs during startup.
- BackendScriptService: drain async output pumps on the cancellation path to prevent stale callbacks after `RunAsync` returns.
- CrashReporter: wrapped `Directory.CreateDirectory` for log/crash directories in try/catch — previously an unhandled exception here silently disabled all crash handling for the entire session.
- CrashReporter: disposed `Process` handle returned by `Process.Start` when opening the crash folder.
- EnvironmentSnapshotService: guarded null/blank `configPath` in `GetSnapshot` to prevent `ArgumentNullException` on `File.Exists`.
- EnvironmentSnapshotService: added `GetSnapshotAsync` and made the view-model refresh await it, so the `schtasks.exe` auto-reapply probe (up to 1500ms) runs on the thread pool instead of blocking the UI dispatcher. Snapshot refreshes from the dashboard button, startup, and post-run no longer cause a visible hang.
- `Remove-PathSafely`: quoted the `$Path` argument passed to `icacls.exe` in both scripts — previously broke silently on paths containing spaces.
- `Expand-ArchiveSafely`: appended trailing separator to the destination path before `StartsWith` comparison to prevent a prefix-collision bypass (e.g., destination `C:\foo` incorrectly allowing extraction to `C:\foobar`).

### Security
- Added safe archive extraction helper (`Expand-ArchiveSafely`) that validates all ZIP entries for path traversal, absolute paths, destination escapes, entry count limits, and expanded size limits before extracting. All 8 extraction sites in both the stable script and WPF backend now use this helper instead of raw `ExtractToDirectory`.
- Hardened backend runtime directory with explicit ACLs (current user + Administrators only, inheritance disabled), per-process immutable execution copies to eliminate TOCTOU race between hash validation and process start, and SHA256 sidecar verification for the watcher scheduled task entry point.
- Stopped tracking build artifacts (`LibreSpot.exe`, `checksums.txt`) in git. The committed `checksums.txt` had drifted out of sync with `LibreSpot.ps1` — exactly the mismatch the README tells users to treat as tampering — and the committed `.exe` predated current source. Integrity now comes solely from CI-attested release assets (fresh SHA256 checksums + SBOM + provenance generated per tag), and the README verification steps point at release-downloaded copies rather than anything in a source checkout.
- Added PowerShell execution-policy / language-mode / application-control diagnostics. At run start LibreSpot now logs its PowerShell edition, version, language mode, and execution-policy scopes (`Get-PowerShellSecurityContext`), warns when the host already enforces ConstrainedLanguage, and classifies AppLocker/WDAC blocks in spawned-process output separately from ordinary errors (`Test-IsLanguageModeOrAppControlError`) — in both the script and WPF backend. `SECURITY.md` documents that execution policy is a safety feature, not a security boundary, and that `-ExecutionPolicy Bypass` does not defeat application control; the guidance is always to ask an administrator, never to weaken enterprise controls. Locked by regression tests on both paths.
- Documented and locked the SpotX external-process execution contract. `SECURITY.md` now spells out, per executable, the allowed argument sources, quoting/execution strategy, timeout, output capture, and exit handling. Regression tests (both PowerShell paths) prove `Normalize-LibreSpotConfig` constrains every interpolated SpotX field to an allowlist or integer and that `Build-SpotXParams` interpolates nothing outside the known-safe set — so a crafted `config.json` cannot inject an extra command, and a future free-form argument fails CI until it is normalized. Verified against 16 injection payloads (quotes, semicolons, pipes, ampersands, newlines): zero leaks.
- Documented and preflight-gated CVE-2025-54100 (Windows PowerShell 5.1 web-content RCE, CVSS 7.8, fixed in the December 2025 Windows cumulative updates). `SECURITY.md` and the README trust section now name the two mitigations — SHA256 pinning (payload integrity) and Windows patch level (closing the parse-time vector) — and clarify that hash pinning alone does not remove the vector. A non-blocking downloader preflight (`Get-DownloaderCveExposure`) in both the script and WPF backend logs a `WARN` once per run when a Windows PowerShell 5.1 host predates the December 2025 patch wave; PowerShell 7+ is unaffected and skipped.

### Fixed
- Repaired community extension downloads by replacing dead branch/path URLs with commit-pinned, SHA256-verified assets, switching Beautiful Lyrics to its `.mjs` build, and removing the deleted Song Stats catalog entry.
- Clarified pre-patched Spotify detection for BlockTheSpot-family migration artifacts and added user-facing migration guidance.
- Let the WPF Maintenance compatibility/update check run without administrator elevation because it only reads upstream release metadata.
- Added Marketplace health detection plus a repair/open action for missing, hidden, legacy-path, or incomplete Marketplace custom-app states.

### Changed
- Consolidated planning docs onto the allowed root set: active work lives in `ROADMAP.md`, shipped work in `CHANGELOG.md` plus git history, and research conclusions in `RESEARCH.md` (with the full legacy research pass archived under `docs/archive/research/`).
- Corrected README dependency and verification copy so the Spicetify CLI pin matches code (`v2.43.2`) and current v3.7.2 assets are not described as if they already include future SBOM/provenance artifacts.
- Added a release lifecycle gate for the .NET 8 WPF support window, bumped release tooling to PS2EXE `1.0.18` and CycloneDX `6.2.0`, and added workflow regression tests.
- Added `ConfigSchemaVersion = 1` to saved profiles, a strict `schemas/librespot-config.schema.json`, and recovery messaging for configs written by a newer LibreSpot build.
- Added PR/push CI coverage for Windows PowerShell 5.1 and PowerShell 7 syntax, XAML parsing, .NET tests, and NuGet vulnerability audit, plus monthly grouped Dependabot updates for runtime and test NuGet packages.
- Bumped WPF runtime logging dependencies to Serilog `4.3.1` and Serilog.Sinks.File `7.0.0`, with the runtime lock file regenerated under the NuGet Audit gate.
- Updated WPF test tooling to Microsoft.NET.Test.Sdk `18.6.0`, xunit runner `3.1.5`, and coverlet.collector `10.0.1` while keeping the test project lock-free.
- Refreshed the SpotX pin to commit `3284673d` with SHA256 verification and updated the current Spotify baseline to `1.2.92`; demoted `1.2.90.451` to previous-fallback.
- Refreshed the Spicetify themes archive pin to commit `df033493` with SHA256 verification.
- Fixed release workflow validation by moving SignPath secret checks out of direct `if: secrets.*` expressions.
- Added a Maintenance compatibility matrix that reports SpotX, Spicetify CLI, Marketplace, and themes separately, including a warning when the SpotX target is newer than Spicetify CLI's max-tested Windows Spotify baseline.
- Replaced the last native self-elevation `MessageBox` fallback with a dark themed bootstrap notice and added a regression guard.
- Added a runtime NuGet lock file, project-reference-safe restore in CI/release, and a moderate-or-higher NuGet Audit gate with `NuGetAuditSuppress` as the documented exception path.
- Hardened release-channel creation so stable, preview, and RC tags are validated, missing tags are rejected, preview/RC releases are not marked latest, and duplicate/empty releases are guarded.
- Pinned GitHub Actions workflow dependencies to full commit SHAs with version comments, added Dependabot batching for workflow actions, and added a regression guard against mutable `uses:` refs.

## [v3.7.2] - 2026-04-28

**Hotfix.** The Easy-mode confirmation dialog was crashing the script the moment users clicked **Install recommended setup**.

### Fixed
- `Show-ThemedDialog` runs as a separate `Window` with its own here-string XAML, so it does NOT inherit the main window's resource dictionary. v3.7.0's blanket `Foreground="#FFE7EDF3"` → `Foreground="{StaticResource FgPrimaryBrush}"` sweep caught three references inside that dialog markup. When the install button fired the "Start Recommended Setup" confirmation, `XamlReader::Load` threw `Cannot find resource named 'FgPrimaryBrush'`, which propagated out of `Show-ThemedDialog` and tore down the install flow before any work started. Reverted those three to inline hex (`#FFE7EDF3`) — the dialog renders as before and Easy/Custom installs proceed.
- Logged the rule in `CLAUDE.md` Gotchas: every standalone XAML here-string (`$dlgXaml`, scheduled-task templates, future popouts) defines its own resource scope. Resource-token sweeps must explicitly skip them.

### Why this slipped past v3.7.0/v3.7.1 validation
`XamlReader::Load` ran clean on the main `$xaml` because the main window declares those brushes inline. The dialog only loads at click time — never exercised by my static checks. Lesson: when the script holds multiple XAML strings, each one needs its own `[XamlReader]::Load` round-trip in pre-flight validation.

---

## [v3.7.1] - 2026-04-28

**Density pass + logo.png brand source.** Cuts the vertical footprint of every panel so the configuration options fit without scrolling on a 1080-tall window. Brand image now sources `logo.png` for crisper rendering at the sidebar's 44-px tile and dialog headers.

### Changed
- **Brand source**: `Get-LibreSpotBrandFrame` now prefers `logo.png` (BitmapImage) over the multi-resolution `.ico`. PNG renders crisper at the actual draw sizes used in the UI. `.ico` remains a fallback when `logo.png` is absent.
- **Default font sizes**: Hero headlines 22 → 17, sub-headlines 21 → 16, card titles 15 → 13.5, tile values 14.5 → 13. CheckBox font 13 → 12.5. ActionButton font 13.25 → 12.5. ComboBox/TextBox font 13 → 12.5.
- **Control heights**: ActionButton 48 → 40, ComboBox 40 → 32, TextBox 42 → 32, MaintButton min-height 82 → 58.
- **Card padding**: SurfaceCard 20 → 14, InsetPanel 16 → 12, StatusCard 16 → 12 + min-height 92 → 68. Panel container Border padding 26 → 16, corner radius 14 → 12.
- **CheckBox**: spacing 8 → 5 above each, min-height 28 → 22, indicator box 22×22 → 18×18, check-mark path resized accordingly.
- **Inter-section gaps** (replace-all sweeps): `0,14,0,0` → `0,8,0,0`, `0,8,0,14` → `0,4,0,8`, `0,0,0,18` → `0,0,0,10`, `0,0,0,20` → `0,0,0,12`, `34,4,0,8` → `30,2,0,4`, `34,4,0,0` → `30,2,0,0`. Two-column gap lanes `Width="20"` → `Width="14"`.
- **Title bar**: Padding 32,22,18,16 → 28,12,16,10. Mode-headline FontSize matches the new Hero-down-tier (18). Summary FontSize 12.25 → 11.75 with 6-px → 3-px gap above.
- **PageContainer outer margin**: 32,0,32,28 → 24,0,24,16.
- **Footer Grid above Install button**: top margin 18 → 10, summary card padding 18,14 → 14,10, gap column 20 → 14.

### Net result
Easy panel hero card + "What we take care of" + "Before you start" cards now fit a 980-px tall content area without scrolling. Custom panel snapshot bar + Spotify-behavior + Themes/Extensions all visible above the fold on a 1080-px screen at default Windows scaling. Maintenance dashboard (status row + metric tiles + actions) fits the same envelope.

### Why
v3.7.0 nailed the chrome but the original v3.6.0 paddings carried over into the panels. With the sidebar eating 252 px of horizontal space, vertical needed to give back. Going 30-40% tighter on padding/margin/font without dropping below readable thresholds (12.5-px body remains comfortable at 100% scale) recovers ~250 px of vertical content per panel.

---

## [v3.7.0] - 2026-04-28

**Premium UI overhaul.** The setup script keeps every behavior from v3.6.0 but now reads as polished product instead of dev tool. Sidebar navigation, Win11 Mica backdrop, semantic design tokens, hover-lift micro-interactions, and a shimmering install progress bar.

### Added
- **Win11 Mica backdrop** via `DwmSetWindowAttribute` P/Invoke (`DWMWA_SYSTEMBACKDROP_TYPE` = 38, `DWMSBT_MAINWINDOW` = 2). Combined with `DWMWA_USE_IMMERSIVE_DARK_MODE` and `DWMWA_WINDOW_CORNER_PREFERENCE` (rounded). Applied at `SourceInitialized`. Quietly degrades to the solid `SurfaceBase` (`#FF0B0E14`) baked into `Window.Background` on Windows 10 / pre-22H2.
- **Sidebar navigation** replacing the three-radio top tab bar. 252-px rail with brand block, Lucide icon nav items (Sparkles / Sliders / Wrench), update banner slot, and footer link tray (GitHub icon + SpotX/Spicetify hyperlinks).
- **Compact title bar** in the main column carries the mode headline + summary alongside minimize/close. The drag handle stays scoped to the title bar so ScrollViewer interactions in Custom/Maintenance keep working.
- **Design token resource dictionary** — `SurfaceBase/Elevated/Elevated2/Overlay/Sidebar`, `Border Subtle/Strong/Hover`, `Accent / AccentHover / AccentPressed / AccentSoft / AccentMuted`, `Info / Warning / Danger` (each with soft bg/border pair), `FgPrimary / FgSecondary / FgMuted / FgInverse`, plus a `ShimmerOverlayBrush`. Inline hex codes for foreground primary/secondary/muted swept to `{StaticResource}` references throughout the panels.
- **Type tokens**: `TypeHeroH1` (32px), `TypeH1` (22px), `TypeH2` (15.5px), `TypeBody` (13px), `TypeCaption` (11.5px). Default font upgraded to `Segoe UI Variable Display` with Segoe UI Variable / Segoe UI fallbacks. ClearType rendering forced.
- **Lucide icon set** as XAML `Geometry` resources — Home, Sliders, Wrench, Shield, Sparkle, Check, Download, Clock, External, Dot, Refresh — usable from any `Path`.
- **Hover-lift micro-interactions** on `ActionButton`: `TranslateTransform.Y` animates to `-1.5` over 120ms on hover, plus accent-colored `DropShadowEffect` glow on focus and hover. Pressed state dims to 0.84 opacity.
- **Shimmering install progress bar** — `RoundProgress` template now layers an animated `LinearGradientBrush` over the indicator using a forever-repeating `DoubleAnimation` translating from `-140` to `900` X over 1.6s. Indicator itself gets an accent-colored DropShadow for depth.

### Changed
- PowerShell script: v3.6.0 → **v3.7.0**.
- Window: `AllowsTransparency=True` + manual rounded Border + drop-shadow → `AllowsTransparency=False` + `WindowChrome` (no caption, 6px resize border) + DWM-managed Mica + DWM-rounded corners. The fake outer shadow is gone; DWM provides the system shadow.
- `MinWidth` 980 → 1120 to give the sidebar + content layout breathing room.
- `ModeRadio` style repurposed as `NavItem`: full-width sidebar row, accent rail on left when checked, `SurfaceElevated2` background when active. `ContentPresenter` now renders icon + label/description composed per radio.
- `PageConfig` row count went from 4 (mode headline / mode bar / panels / footer) to 2 (panels / footer). Mode headline + summary moved into the title bar; mode bar disappeared into the sidebar.
- `ProgressBar` indicator gains an accent-colored `DropShadowEffect` (BlurRadius 14) for the lift cue.

### Removed
- Outer 14-px margin Grid + faux drop-shadow rounded Border. Mica + DWM rounded corners replace both.
- Top mode tab bar (now sidebar nav).
- The "TitleSubtext" tagline at the top — moved into the sidebar brand block as "Premium Spotify toolkit".

### Why
v3.6.0's UI was already dark, card-based, and accent-tinted, but read as "developer tool" because of flat 1px borders, scattered hex codes, text-bullet lists, and a top tab bar that felt like a form control. Premium installers (Linear, Vercel, 1Password) lean on Mica/Acrylic backdrops, sidebar nav, semantic color tokens, and motion. v3.7.0 picks up all four without changing a single install behavior or breaking any existing PowerShell-side `$ui[name]` reference.

---

## [v3.6.0] / [v4.0.0-preview.6] - 2026-04-17

**Track 4.2 — auto-reapply watcher.** LibreSpot now notices when Spotify auto-updates itself and silently re-runs the saved SpotX patch so you don't come back to ads. Off by default — enable it from Maintenance > Protect and repair.

### Added
- **"Auto-reapply when Spotify updates itself"** toggle in Maintenance. Checking it registers a per-user scheduled task (`\LibreSpot\ReapplyWatcher`) that fires at logon, then every 30 minutes. Unchecking it removes the task. Status label underneath reflects the actual task state from `schtasks.exe /Query`, so the UI stays honest even if the task was deleted out-of-band.
- **Headless `-Watch` entry point.** The scheduled task invokes LibreSpot with `-Watch`. That path skips all WPF/XAML loading and runs only:
  1. Read `%APPDATA%\LibreSpot\watcher-state.json` for last-known Spotify version.
  2. On first ever run, just record the current version and exit (never clobber a fresh Spotify).
  3. If the version is unchanged, log "nothing to do" and exit.
  4. If Spotify is currently running, defer to next tick (reapplying while audio is playing would kill the session).
  5. If there's no saved LibreSpot config, exit with a message in the log.
  6. Otherwise download + **SHA256-verify** the pinned SpotX script, run it under the saved config, and silently reapply Spicetify if the CLI is present.
- **CLI flags**: `-InstallWatcher` and `-UninstallWatcher` for users who prefer to manage the scheduled task from a script without opening the GUI. Both exit with a useful message.
- **`AutoReapply_Enabled`** config key wired end-to-end (defaults → normalization → fingerprint → `Get-InstallConfig` → `Apply-ConfigToUi` → WPF backend Backend.ps1 → C# `InstallConfiguration` with Clone + Normalize). Preference round-trips between PowerShell and WPF saves.
- **`watcher.log`** under `%APPDATA%\LibreSpot\` captures every tick (skip, reapply, defer, error). Auto-trims at ~1 MB to the last 500 lines so an unattended machine can't fill disk.
- **Regression tests** (`PowerShellRegressionTests.cs`) lock in the critical invariants:
  - `-Watch` exit branch is placed AFTER `Build-SpotXParams` definition.
  - Every CLI entry point explicitly `exit`s.
  - Scheduled task XML uses the correct Task Scheduler namespace and UTF-16 encoding.
  - `Invoke-HeadlessReapply` verifies the SpotX hash before running.
  - First-run initialization doesn't immediately reapply.
  - Running Spotify defers instead of clobbering.
  - `AutoReapply_Enabled` is on the boolean-normalization list.

### Changed
- PowerShell script: v3.5.1 → **v3.6.0**.
- WPF desktop shell: v4.0.0-preview.5 → **v4.0.0-preview.6**.

### Differentiator
None of the other Spicetify/SpotX installers ship this — BlockTheSpot-Installer, SpotX-Spicetify-Universal-Installer, and Spicetify Manager all require the user to manually click "Reapply After Update" after every Spotify auto-update. This closes that loop.

## [v3.5.1] - 2026-04-17

Hardening + release-pipeline pass. Fixes bugs introduced in v3.5.0, tightens the release workflow, and adds regression guards so the issues we just fixed can't silently creep back.

### Release pipeline (.github/workflows/release.yml)
- **Preflight job** runs before build. Resolves the tag, asserts `LibreSpot.ps1:$global:VERSION == Backend.ps1:$global:VERSION` (the exact invariant v3.5.1 breaks), asserts the right version file matches the tag (`PS1` for stable tags, `csproj` for `-preview.N` tags), parses both PowerShell files with `[Parser]::ParseFile` so a syntax error fails the tag before PS2EXE runs, and enforces a regression guard that forbids `chrome_elf.dll` / `xpui.spa.bak` from re-entering `Get-ExistingSpotifyPatchSignature`.
- **PS2EXE pinned** to `1.0.15` so a breaking upstream release can't corrupt a tagged build.
- **Unit tests run before WPF publish**. A red AppCatalog/Configuration/PowerShellRegression test fails the tag.
- **Release assets now include raw `LibreSpot.ps1`** — the README's `irm .../releases/latest/download/LibreSpot.ps1 | iex` one-liner was 404'ing because only the `.exe` was ever uploaded. Also attested for provenance.
- **`gh release create` fallback** — if the release doesn't exist yet for the tag, one is auto-created with generated notes before assets upload.
- **Explicit checksum list** replaces the previous `sha256sum *.exe *.json` glob that would silently skip a missing asset.

### Regression tests (tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs)
- Parses `LibreSpot.ps1` as text and asserts `Get-ExistingSpotifyPatchSignature`'s function body does not reference `chrome_elf.dll` or `xpui.spa.bak`.
- Asserts `LibreSpot.ps1:$global:VERSION` and `Backend.ps1:$global:VERSION` stay in sync.
- Asserts `Compare-LibreSpotVersions` still uses `[Version]` parsing and strips `-preview.*` / `-rc.*` suffixes.
- Asserts `Compare-LibreSpotVersions` remains on the worker-runspace export list (or `Check-ForUpdates` hits a "command not found" at runtime).
- Asserts `Start-SelfUpdateBannerRefresh` uses `ThreadPool.QueueUserWorkItem` — catches any revert that would reintroduce the 5-second UI freeze on launch.

### Defensive fixes (src/LibreSpot.Desktop/ViewModels/MainViewModel.cs)
- `CancelRunningBackend()` and the cancel-prompt confirm handler now swallow `ObjectDisposedException` explicitly. Other exceptions still propagate — they'd indicate a real programming bug. `Dispose()` stays idempotent.

### Fixes carried over from the earlier v3.5.1 commit

### Fixed
- **Foreign-patch detection fired on every launch** (introduced in v3.5.0). The previous signature list checked for `chrome_elf.dll` (part of every Spotify install — LibreSpot itself throws if it is *missing*) and `xpui.spa.bak` (created by SpotX's own backup step on every successful run). Revised to only match files BlockTheSpot-style injectors drop: `dpapi.dll`, `config.ini`, `version.dll`, `winmm.dll` next to `Spotify.exe`.
- **Backend.ps1 stamped the wrong version** in its HTTP User-Agent and internal log lines (`LibreSpot/3.3.0` instead of the real shell version). Synced to `3.5.0` with a comment noting the release workflow should fail a build when these drift.
- **Self-update check blocked the UI thread for up to 5 seconds on launch** when GitHub was slow. Refactored to a pure-.NET `HttpWebRequest` running on a ThreadPool thread, with cache write + UI update marshaled back through `Dispatcher.BeginInvoke` at idle priority. The cache read path is still synchronous (filesystem-only) and returns instantly on a warm cache.
- **`Check-ForUpdates` used lexical string comparison** for Spicetify CLI, Marketplace, and LibreSpot versions. That would have reported `v2.43.10` as *older* than `v2.43.9`. Replaced with a new `Compare-LibreSpotVersions` helper that parses the numeric prefix via `[Version]`, strips `-preview.*`/`-rc.*`, and treats stable as newer than a pre-release with the same prefix.
- **`$sender` parameter in `Window.Add_Closing`** shadowed PowerShell's automatic `$Sender` variable (PSAvoidAssignmentToAutomaticVariable). Renamed to `$closingSource`.

### Changed
- Exported `Compare-LibreSpotVersions` into the worker runspace so `Check-ForUpdates` (which runs there) can call it.
- `Save-SelfUpdateCache` is invoked only from the dispatcher thread so `ConvertTo-Json` / `Set-Content` never run concurrently with the main runspace from a ThreadPool thread.
- `Invoke-SelfUpdateHttp` parses the GitHub response with pure regex (no `ConvertFrom-Json`) and inlines the version compare, so the ThreadPool path never re-enters the main runspace.

### Out of scope for this pass (tracked for later)
- ~35 PSScriptAnalyzer `PSUseApprovedVerbs` warnings on private helpers (`Normalize-`, `Module-*`, `Load-`, `Apply-`, `Capture-`, `Build-`, `Download-`, `Check-`, `Reapply-`). Renaming cascades across the worker-runspace function-export list.
- Monolith → module extraction for the ~400 lines of config logic duplicated between `LibreSpot.ps1` and `Backend.ps1`.
- Maintenance action dispatch table (currently a ~300-line `if/elseif` chain in the worker block).

## [v3.5.0] / [v4.0.0-preview.5] - 2026-04-17

Competitor-parity release. Four items from the ROADMAP Track 4 shipped end-to-end (PowerShell monolith + WPF backend + C# model).

### Added
- **Self-update check** — on launch, async-queries `api.github.com/repos/SysAdminDoc/LibreSpot/releases/latest`, shows a subtle green "Update available →" hyperlink in the title bar when a newer release exists. Result cached 24h in `%APPDATA%\LibreSpot\update-check.json` to stay under the 60 req/hr anonymous API limit. Zero telemetry — single GET, nothing else sent.
- **Pre-patched Spotify detection** — scans Spotify's install directory for BlockTheSpot-style injectors (`dpapi.dll`, `config.ini`, `version.dll`, `winmm.dll` next to `Spotify.exe`) and shows a themed warning dialog once per session before the user starts patching. Tells them to run **Maintenance > Full Reset** first if they want a clean slate.
- **Spotify version dropdown** in Custom Install > Advanced — inline manifest of 5 known-good Spotify builds (`auto`, `1.2.86.502`, `1.2.85.519`, `1.2.53.440.x86`, `1.2.5.1006.win7`) with per-entry hint text. Emits SpotX's `-version <string>` when non-default. Config key: `SpotX_SpotifyVersionId`.
- **`-Clean` CLI flag** — `irm URL | iex -clean` (or `powershell.exe -File LibreSpot.ps1 -clean`) pre-ticks Easy mode + CleanInstall for a one-shot nuke-and-rebuild flow.

### Changed
- PowerShell script: v3.4.0 → **v3.5.0**.
- WPF desktop shell: v4.0.0-preview.4 → **v4.0.0-preview.5**.
- `InstallConfiguration` C# model gains `SpotX_SpotifyVersionId` property with Clone + Normalize support.
- `AppCatalog.SpotifyVersionManifest` exposes the version list to the WPF shell (record type `SpotifyVersionEntry`).

## [v4.0.0-preview.4] - 2026-04-17 (pre-release)

### Added
- **Mica backdrop** on Windows 11 build 22621+ via `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_MAINWINDOW)`, paired with `DWMWA_USE_IMMERSIVE_DARK_MODE` so the title bar matches the dark shell ([Services/Win11ShellIntegration.cs](src/LibreSpot.Desktop/Services/Win11ShellIntegration.cs)). Older Windows falls back silently to the flat canvas brush.
- **TaskbarItemInfo progress mirroring** — the Windows taskbar icon now tracks the run state (`None`/`Indeterminate`/`Normal`/`Paused`/`Error`) so users see progress even when LibreSpot is minimized. `ProgressValue` is kept in sync with the in-app 0–100 scale.
- **Serilog crash reporter** ([Services/CrashReporter.cs](src/LibreSpot.Desktop/Services/CrashReporter.cs)) — structured daily rolling log under `%LOCALAPPDATA%\LibreSpot\logs\` (14-day retention), full crash dumps under `%LOCALAPPDATA%\LibreSpot\crashes\`, and a crash dialog that offers "copy path + open folder" so users can file issues without the app needing to phone home. Hooks `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, `Dispatcher.UnhandledException`.
- **Accessibility pass** — `AutomationProperties.Name` + `HelpText` on previously unlabeled icon buttons (Refresh status, Copy log header variant), `AutomationProperties.LiveSetting="Polite"` on the activity badge so screen readers announce state transitions.
- **GitHub Actions release workflow** ([.github/workflows/release.yml](.github/workflows/release.yml)) — triggered on `v*` tags. Builds PS2EXE + .NET 8 self-contained WPF EXE, emits SHA256 `checksums.txt` + CycloneDX SBOM, attests build provenance + SBOM via `actions/attest-build-provenance@v2` and `actions/attest-sbom@v2` (SLSA L3). Consumers verify with `gh attestation verify`.

### Changed
- WPF desktop shell: v4.0.0-preview.3 → **v4.0.0-preview.4**.
- New NuGet dependencies: `Serilog 4.2.0`, `Serilog.Sinks.File 6.0.0`.

## [v3.4.0] - 2026-04-17

### Added
Six new SpotX flags surfaced end-to-end (Custom Install UI + config persistence + fingerprint + `Build-SpotXParams`):
- **Privacy**: `-sendversion_off` (default **on** — blocks SpotX's outbound version notification introduced in the April 2026 SpotX update).
- **Core behavior**: `-start_spoti` (auto-launch Spotify after install).
- **Advanced**:
  - `-devtools` — enable Spotify Chromium Developer Tools (Spicetify extension authors).
  - `-mirror` — use GitHub.io mirror for SpotX assets when `raw.githubusercontent.com` is blocked.
  - `-confirm_spoti_recomended_uninstall` — force SpotX's uninstall-then-reinstall flow.
  - `-download_method {curl|webclient}` — force SpotX's downloader choice (ComboBox in PowerShell GUI; WPF shell defers custom XAML binding to a later preview).
- New **Privacy** and **Advanced** inset panels in the PowerShell Custom Install view.
- Matching `OptionDefinition` entries in the WPF shell (`Core`/`Advanced` sections) auto-render via the shared `OptionTemplate`.

### Changed
- PowerShell script: v3.3.1 → **v3.4.0**.
- WPF desktop shell: v4.0.0-preview.2 → **v4.0.0-preview.3**.
- `InstallConfiguration` C# model gains 6 new properties with `Clone()` + `NormalizeConfiguration()` support.
- `Build-SpotXParams` (both PowerShell monolith and WPF Backend) extended to emit the new flags.

### Verified
- All 22 existing `Build-SpotXParams` flag emissions cross-checked against SpotX `run.ps1` param block on 2026-04-17 — spellings correct.
- Six truly-missing flags above identified as the only net-new additions worth shipping today; `-version`, `-CustomPatchesPath`, `-language`, `-urlform_goofy`, `-idbox_goofy`, `-err_ru` intentionally deferred (they feed into future roadmap tracks or are niche).

## [v3.3.1] - 2026-04-17

### Fixed
- **Silent no-op**: `-new_fullscreen_mode` corrected to `-newFullscreenMode` (real SpotX flag is camelCase). The "Experimental fullscreen mode" GUI toggle never actually passed through to SpotX on v3.3.0. Fixed in both `LibreSpot.ps1:Build-SpotXParams` and `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`.
- Re-verified every flag in `Build-SpotXParams` against SpotX `run.ps1` param block (2026-04-17) — all other flags correct.

### Changed
- `-SpotifyPath` gotcha softened in CLAUDE.md to a historical note; SpotX `run.ps1` accepts it as a supported parameter.
- WPF desktop shell bumped to v4.0.0-preview.2 (csproj now declares `<Version>`/`<AssemblyVersion>`/`<FileVersion>`).

## [v4.0.0-preview.1] - 2026-04-16 (pre-release)

### Added
- Native WPF desktop shell (.NET 8, MVVM) replacing the PS2EXE GUI wrapper
- Token-based design system: surface elevation, semantic intent, motion, easing, radius, and spacing scales read from a single source of truth
- Focus rings as overlay borders (no 1px layout jitter on keyboard focus)
- Button hover-tint via Opacity animation + tactile 0.985× press-scale
- Indeterminate progress shimmer, rotating ComboBox chevron, fade-in checkbox checkmarks
- Overlay cards (activity + prompt) fade + scale-in on every show via DataTrigger EnterActions
- State-aware activity badge — accent pulse while running, Danger + "Needs attention" on failure, "Run complete" on success, "Working…" during indeterminate runs
- Staggered accent-dot empty state for the log panel
- Structured stdout protocol between WPF shell and embedded PowerShell backend
- Embedded backend script extracted to LocalAppData and SHA-verified before each run
- Action allow-list validation before any PowerShell dispatch
- Cancellation chain tears down the child process tree on window close

### Changed
- Backend flows hardened for the new shell integration (install/maintenance pipelines preserved)
- Desktop UX polish across controls, states, and transitions
- Single-file self-contained .NET 8 executable (no runtime dependency)

## [v3.3.0] - 2026-04-05

### Added
- Five new SpotX GUI options: Plus features (`-plus`), experimental fullscreen (`-newFullscreenMode`), humorous progress bar (`-funnyprogressBar`), experimental Spotify features (`-exp_spotify`), lyrics block (`-lyrics_block`)
- Full config pipeline wiring for new options: XAML, load/save, normalization, fingerprint, Build-SpotXParams, summary toggles
- Mutual-exclusivity enforcement between `-lyrics_block` and `-old_lyrics` via dependency-aware UI

### Changed
- SpotX pinned to `0abf98a3` (targets Spotify 1.2.86.502)
- Spicetify CLI bumped to v2.43.1

## [v3.2.0] - 2026-04-15

### Added
- Robust self-elevation that handles .ps1, .exe, and inline scriptblock launch contexts
- Config normalization with type-safe boolean/int parsing and corrupt config quarantine
- Custom themed dark dialogs replacing native MessageBox throughout the app
- Safe file removal system with blocklist protection against accidental deletion of system directories
- Spicetify backup/restore with staged copy and automatic rollback on failure
- Streaming process output capture for real-time SpotX log display
- Unsaved changes detection with config fingerprinting and close-window guard
- Comprehensive 8-phase Spotify uninstaller (processes, Store app, native uninstaller, filesystem, registry, scheduled tasks, firewall rules, verification)
- Centralized Spicetify CLI wrapper with consistent error handling
- Declarative extension/custom-app sync that preserves user-installed items
- PATH management utilities for clean Spicetify install/uninstall
- Per-maintenance-action context messages and completion summaries
- Dialog icon branding for the main window and themed dialogs
- Icon assets (icon.ico, icon.png, icon.svg, banner.png, multi-size icons/)

### Changed
- Rewrote install and maintenance flows for resilience (near-complete script rewrite)
- All maintenance actions now use themed confirmation dialogs with descriptive context
- Install page shows per-step labels and contextual descriptions
- Runspace infrastructure uses explicit ISS function/variable exports instead of dot-sourcing

### Fixed
- Maintenance buttons now disable correctly based on what is actually installed
- Config save uses atomic write-then-replace to prevent corruption on crash
- Close-window handler warns about in-progress setup or unsaved custom changes

## [v3.1.1] - 2026-03-27

- Fixed theme preview crash: use synchronous download
- Fixed theme preview: TLS 1.2 + ThreadPool instead of WebClient async
- Reverted custom apps/packs, kept theme preview
- Removed Statistics and Lyrics Plus custom apps (broken)

## [v3.1.0] - 2026-03-27

- Audit fixes: anti-hang, apply recovery, new options
- Fixed blank screen: let SpotX manage Spotify version compatibility
- Added live theme preview with async image loading

## [v3.0.6] - 2026-03-27

- Updated SpotX to 6070bbcf to fix blank screen on Spotify 1.2.85.519
- Compiled v3.0.6 executable
