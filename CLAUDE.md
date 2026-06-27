# LibreSpot - Working Notes

## Overview
SpotX + Spicetify unified installer. Single PowerShell script (~5021 lines) with WPF GUI.
Three modes: Easy Install, Custom Install, Maintenance.
21 themes (16 official + 5 community), 15 extensions (10 built-in + 5 community).

## Version
PowerShell script: v3.7.2
WPF desktop shell: v4.0.0-preview.6

## Tech Stack
- PowerShell 5.1+ with WPF GUI (XamlReader::Load, here-string XAML)
- Async install via runspace ISS with explicit function/variable exports + DispatcherTimer polling
- SHA256 hash verification on all downloads
- Dual download: Invoke-WebRequest with BITS fallback
- Atomic config saves (write temp, replace original)
- Custom themed dark WPF dialogs (no native MessageBox)

## Key Files
- `LibreSpot.ps1` — entire application (single-file)
- `LibreSpot.exe` — PS2EXE compiled executable
- `LibreSpot.ico` — application icon (used by ps2exe)
- `icon.ico` — window/dialog icon (used at runtime)
- `icon.png` — README branding
- `icons/` — multi-size PNG icons (16-512px)

## Pinned Dependencies (update these together)
| Component | Version/Commit | Location in script |
|---|---|---|
| SpotX | `3284673d` (Spotify 1.2.92) | `$global:PinnedReleases.SpotX` (~line 140) |
| Spicetify CLI | v2.43.2 | `$global:PinnedReleases.SpicetifyCLI` (~line 147) |
| Marketplace | v1.0.8 | `$global:PinnedReleases.Marketplace` (~line 157) |
| Themes | `df033493` | `$global:PinnedReleases.Themes` (~line 162) |

## Architecture
1. **Self-elevation** (`Get-SelfElevationLaunchTarget`): handles .ps1, .exe, inline scriptblock, temp bootstrap
2. **Config system**: type-safe normalization, corrupt config quarantine, atomic saves, fingerprint-based dirty tracking
3. **Uninstaller** (8-phase): process kill, AppX removal, silent uninstall, filesystem cleanup, registry cleanup, scheduled tasks, firewall rules, verification sweep
4. **Safe removal**: `Test-SafeRemovalTarget` blocklist prevents accidental deletion of system dirs
5. **SpotX patching**: downloads pinned run.ps1, verifies SHA256, streams stdout via `Read-ProcessOutputDelta`
6. **Spicetify**: CLI install, theme copy, declarative extension sync, marketplace deploy, then `backup apply` with auto-restore on failure
7. **Backup/restore**: `Restore-SpicetifyBackupSnapshot` uses staged copy with automatic rollback
8. **Window watcher**: separate runspace hides Spotify windows during install via `ShowWindowAsync`
9. **Themed dialogs**: `Show-ThemedDialog` replaces MessageBox with dark WPF dialogs matching app theme

## Key Functions
- `Get-SelfElevationLaunchTarget` (~line 114) — robust admin re-launch detection
- `Normalize-LibreSpotConfig` (~line 325) — type-safe config validation
- `Show-ThemedDialog` (~line 2345) — custom dark WPF dialog
- `Build-SpotXParams` (~line 1813) — maps GUI checkboxes to SpotX CLI flags
- `Module-NukeSpotify` (~line 2965) — 8-phase comprehensive uninstaller
- `Module-InstallSpotX` (~line 3106) — SpotX download + hash verify + invoke
- `Module-ApplySpicetify` (~line 3253) — `spicetify backup apply` with auto-restore
- `Invoke-SpicetifyCli` (~line 1868) — centralized CLI wrapper
- `Sync-SpicetifyListSetting` (~line 1886) — declarative extension/custom-app sync
- `Test-SafeRemovalTarget` (~line 2905) / `Remove-PathSafely` (~line 2947) — safe deletion
- `Read-ProcessOutputDelta` (~line 2658) — streaming file-based process output
- `Set-InstallStageLabels` (~line 1350) — action-specific progress stage labels
- `Update-ModePresentation` (~line 1585) — mode-aware headline, summary, snapshot, footer
- `Apply-ConfigToUi` (~line 1496) — preset reapplication for Custom mode reset-to-defaults
- `Set-SelectionSnapshotState` (~line 1305) — snapshot badge/footer tone and messaging

## Roadmap Hygiene
- `ROADMAP.md` contains only implementer-actionable items.
- `Roadmap_Blocked.md` holds items blocked on operator decisions, credentials,
  or policy calls (tagged 🔧 in research cycles). Move items there when they
  cannot be resolved by an implementer. Move them back to `ROADMAP.md` once the
  blocking decision is made.
- Do not leave blocked work in `ROADMAP.md`; move it to `Roadmap_Blocked.md`
  so the live roadmap remains actionable-only.

## Gotchas
- **Historical: `-SpotifyPath` caused blank screens.** Reviewed 2026-04-17 — SpotX `run.ps1` accepts `-SpotifyPath` as a supported string parameter. Current best practice: we don't pass it (let SpotX locate Spotify itself), but the flag is NOT inherently broken. Only re-evaluate if a user reports the blank-screen symptom.
- **Blank screen = version mismatch.** SpotX patches target specific Spotify versions.
- **Don't pre-install Spotify separately.** SpotX downloads the correct version itself.
- **Always pass `-confirm_uninstall_ms_spoti`.** Prevents Read-Host hang when stdout is redirected.
- **Always pass `-confirm_spoti_recomended_over`.** Lets SpotX manage Spotify version compatibility.
- **Spicetify apply failure = auto-restore.** If `backup apply` exits non-zero, calls `spicetify restore`.
- `spicetify backup apply` must run AFTER SpotX patching, not before.
- Themes needing JS injection: controlled by `$global:ThemesNeedingJS` — Dribbblish, StarryNight, Turntable, Catppuccin, Comfy, Bloom, Lucid, Hazy.
- **Community themes** download from individual GitHub repos (not the official themes archive). Download URLs use `main` branch — should be pinned to specific commits for production releases.
- **Community extensions** are downloaded to `$SPICETIFY_CONFIG_DIR\Extensions\` before sync. If download fails, the extension is skipped with a warning — install continues.
- **Community extension URLs are commit-pinned and SHA256 verified.** Beautiful Lyrics registers as `beautiful-lyrics.mjs`; legacy config names `beautifulLyrics.js` and `playlistIcons.js` migrate to current filenames, and deleted `songStats.js` is kept only as a managed cleanup name.
- **Marketplace health is not just directory existence.** `Get-MarketplaceHealth` checks required `extension.js` + `manifest.json` files and Spicetify `custom_apps` registration, then the repair action reinstalls, reapplies, and opens `spotify:app:marketplace`.
- **Worker runspace exports** must include `Download-CommunityExtensions` and new globals `CommunityExtensions`, `CommunityThemeRepos`, `ThemesNeedingJS`.
- Settings persist to `%APPDATA%\LibreSpot\config.json`.
- **Config schema versioning:** saved profiles carry `ConfigSchemaVersion = 1`. Update `LibreSpot.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`, `InstallConfiguration`, and `schemas/librespot-config.schema.json` together. Future schema versions are quarantined with a recovery reason instead of silently rewritten.
- Up to 5 rotating Spicetify config backups in `%USERPROFILE%\LibreSpot_Backups`.
- `icon.ico` must be in `$PSScriptRoot` for window/dialog icons to load at runtime.
- **Worker runspace exports** (`$functionNamesForWorker`): any function called inside `$installBlock` or `$maintBlock` (including transitive calls) must be listed. `Read-ProcessOutputDelta` was missing prior to this QA pass and would crash every install.
- **DragMove** is scoped to the `TitleBar` element only. Attaching it to the whole window interferes with ScrollViewer interactions in Custom Install and Maintenance panels.
- **Theme preview DoEvents re-entrancy**: `Update-ThemePreview` uses `DoEvents()` before a sync download. The `$script:previewLoading` guard prevents stacked calls if the user clicks the theme combo during download.
- **Invoke-ExternalScriptIsolated has a 600s default timeout.** Processes that exceed it are killed. Adjust `$TimeoutSeconds` if a step legitimately takes longer.
- **ISS objects are safe to reuse** across multiple `CreateRunspace()` calls. Not a bug despite appearances.
- **`-confirm_spoti_recomended_over` is in `Build-SpotXParams`** now, not added separately per flow. Any new flow calling `Build-SpotXParams` gets it automatically.
- **`-lyrics_block` and `-old_lyrics` are mutually exclusive.** Both require lyrics to be enabled. The UI enforces this via `Update-DependentControlState`.
- **Mica needs `AllowsTransparency=False`.** WPF can't both software-render transparent content AND let DWM composite a Mica backdrop. v3.7.0 switched off transparency and uses `WindowChrome` (CaptionHeight=0, ResizeBorderThickness=6) so DWM gets to draw the rounded chrome + Mica blur. Don't flip `AllowsTransparency` back on — Mica will silently disappear.
- **Mica enable timing.** `DwmSetWindowAttribute` must run after the window has an HWND. Hook `Window.Add_SourceInitialized` (NOT `Loaded`); on Loaded the HWND exists but the backdrop is sometimes overwritten by the initial render.
- **Native caption colors are pinned.** `Win11ShellIntegration.ApplyMicaAndDarkChrome` sets `DWMWA_CAPTION_COLOR`, `DWMWA_TEXT_COLOR`, and `DWMWA_BORDER_COLOR` so Windows accent colors do not turn the active title bar blue. If the dark palette changes, update the COLORREF values in that service together.
- **WPF UIA smoke uses visible landmarks.** WPF's TabControl automation peer reliably exposes tab headers and workspace headings, but not every selected-content descendant as a ControlView child. Keep smoke assertions on stable visible landmarks and named actionable controls, with overlay buttons checked directly.
- **Color tokens are reused by PS dynamic chrome.** `Set-MaintenanceCardTone`, `Set-SelectionSnapshotState`, `Set-InstallStageState` build SolidColorBrushes from hex literals matching the soft accent/info/warning/danger token values. If you change a `*SoftBgColor`/`*SoftBorderColor` resource in XAML, mirror the hex in those PS functions or the dynamic chrome will diverge from the static cards.
- **Each XAML here-string has its own resource scope.** The script holds three: scheduled-task XML (line ~329), the main `$xaml` (line ~1113), and `$dlgXaml` inside `Show-ThemedDialog` (line ~4303). Resources defined in `Window.Resources` of one are NOT visible to another. v3.7.2 was a hotfix because v3.7.0's `Foreground="#FFE7EDF3"` → `Foreground="{StaticResource FgPrimaryBrush}"` replace_all swept three references inside `$dlgXaml`, which then crashed on Easy-mode confirmation. Rule: replace_all on Foreground/Background tokens MUST be bounded to the main `$xaml` here-string or each external XAML must inline the brush resource itself.
- **Pre-flight validation runs both XAML strings.** When validating XAML changes, load `$xaml` AND `$dlgXaml` through `[XamlReader]::Load` separately. Loading only the main one missed the dialog regression in v3.7.0/v3.7.1.
- **Localization uses `Properties/Strings.resx` with `PublicResXFileCodeGenerator`.** XAML references use `{x:Static props:Strings.KeyName}` (namespace `xmlns:props="clr-namespace:LibreSpot.Desktop.Properties"`); C# code uses `Strings.KeyName` with `using LibreSpot.Desktop.Properties`. `Strings.Designer.cs` must be regenerated when .resx entries change — it is NOT auto-generated by `dotnet build` (VS generator only). Remaining exception: AppCatalog option/extension/maintenance definitions still use inline strings (catalog data for the P3 localization track).

## Build
No build system — single .ps1 file. The .exe is compiled via PS2EXE:
```powershell
Invoke-PS2EXE -InputFile LibreSpot.ps1 -OutputFile LibreSpot.exe -IconFile LibreSpot.ico -Title LibreSpot -NoConsole -RequireAdmin
```
Version string is at `$global:VERSION` (~line 105). README badge must match.
Release workflow pins PS2EXE `1.0.18` and CycloneDX `6.2.0`. The workflow lifecycle gate targets `net10.0-windows` (.NET 10 LTS, supported through 2028-11-14).
NuGet restore locks the runtime WPF project. CI/release use `--locked-mode -p:AuditPipeline=true` on `src/LibreSpot.Desktop`, then restore tests with `--no-dependencies`; accepted security advisories must be explicit `NuGetAuditSuppress` items with the advisory URL.
The test project opts out of `packages.lock.json` because its project-reference transitive entries duplicate the runtime lock and make Dependabot runtime PRs fail on stale test locks.
Runtime WPF dependencies are WPF-UI `4.3.0` (NuGet ID `WPF-UI`, not older `Wpf.Ui`), Serilog `4.3.1`, and Serilog.Sinks.File `7.0.0`; update `src/LibreSpot.Desktop/packages.lock.json` in the same commit.
Current test tooling is Microsoft.NET.Test.Sdk `18.6.0`, xunit `2.9.3`, xunit.runner.visualstudio `3.1.5`, and coverlet.collector `10.0.1`; the test project deliberately remains lock-free.
Release tags must be `vN.N.N`, `vN.N.N-preview.N`, or `vN.N.N-rc.N`. The release job verifies the pushed tag, rejects mismatched manual-dispatch commits, and marks preview/RC releases with `--prerelease --latest=false`.
GitHub Actions workflows pin remote actions to full commit SHAs with a preceding `# owner/action v...` comment; `DependencyAutomationTests.Workflows_PinRemoteActionsToFullCommitShas` is the guardrail.

## Version History
- v4.0.0-preview.6 polish second pass - softened WPF scrollbars, changed hero/rail micro-labels to title case, replaced backend-centric activity copy with product-level run-log language, fixed log-count pluralization, and cleaned support-bundle preview wording. Keep UI copy product-facing; reserve backend/PowerShell terms for logs, diagnostics, and code comments.
- v4.0.0-preview.6 polish follow-up — tightened the WPF shell radius system to 6-12 px, removed pill-shaped badge/progress styling, shortened first-run rail copy, hid informational-only health details from the sidebar, fixed Custom option-card title wrapping, pinned DWM caption/text/border colors to the dark palette, and made activity log empty/count states collection-driven. UIA smoke coverage now targets visible workspace landmarks plus named actionable controls.
- v3.7.0 — **Premium UI overhaul.** Win11 Mica backdrop via `DwmSetWindowAttribute` P/Invoke (DWMWA_SYSTEMBACKDROP_TYPE/CornerPreference/ImmersiveDarkMode), graceful degrade to solid `#FF0B0E14` on older Windows. Top mode-tab bar replaced with 252-px sidebar nav (RadioButtons styled as nav rows with Lucide icon Path data + accent rail when active). Mode headline + summary moved to the compact in-content title bar. Window switched off `AllowsTransparency` to a `WindowChrome`-driven, DWM-managed window so Mica composites; manual outer drop-shadow Border + 14-px margin removed. Resource dictionary now carries `Surface*/Border*/Accent*/Info/Warning/Danger/Fg*` brushes plus `TypeHeroH1/H1/H2/Body/Caption`; primary/secondary/muted text foreground refs swept from inline hex to `{StaticResource}` throughout the panels. `ActionButton` gained hover-lift (`TranslateTransform.Y -> -1.5` 120ms) + accent-glow focus ring. Install `RoundProgress` template now layers a perpetually-animating `LinearGradientBrush` shimmer over the indicator. Default font moved to Segoe UI Variable Display, `MinWidth` raised 980 → 1120.
- v3.6.0 / v4.0.0-preview.6 — **Auto-reapply watcher** (Track 4.2). Maintenance toggle registers a scheduled task (`\LibreSpot\ReapplyWatcher`, logon + 30 min repetition, least-privilege, 30-min timeout) that invokes LibreSpot with `-Watch`. Headless watcher diffs `Spotify.exe` FileVersion against last-known (`%APPDATA%\LibreSpot\watcher-state.json`), reapplies hash-verified SpotX + Spicetify when Spotify is closed, skips when running, initializes without reapplying on first tick. CLI flags `-Watch` / `-InstallWatcher` / `-UninstallWatcher`. `AutoReapply_Enabled` config key round-trips through both backends and the C# model. 7 new regression tests in PowerShellRegressionTests.cs lock in the critical invariants (especially that the `-Watch` exit lands AFTER `Build-SpotXParams` is defined — moving it back to the top re-breaks the feature).
- v3.5.1 — Hardening pass on top of v3.5.0. Fixes: foreign-patch detection false positives (removed `chrome_elf.dll` / `xpui.spa.bak`, kept `dpapi.dll` / `config.ini` / `version.dll` / `winmm.dll`), Backend.ps1 stale `$global:VERSION`, self-update UI-freeze (pure-.NET HttpWebRequest on ThreadPool with dispatcher marshal), string-compare bug in Check-ForUpdates (new `Compare-LibreSpotVersions` semver helper), `$sender` auto-var shadowing in Window.Add_Closing.
- v3.5.0 / v4.0.0-preview.5 — Competitor parity (Track 4). Self-update check (title-bar banner, 24h cache, zero telemetry). Pre-patched Spotify detection (warns if BlockTheSpot/SpotX artifacts found). Spotify version dropdown (inline manifest, emits `-version` to SpotX). `-Clean` CLI flag for `iex -clean` one-shot rebuild. All four wired through PS1 + WPF Backend + AppCatalog.cs.
- v4.0.0-preview.4 (WPF shell) — Win11 Mica backdrop + dark title bar via `DwmSetWindowAttribute` P/Invoke (Services/Win11ShellIntegration.cs). TaskbarItemInfo progress mirroring (VM: `TaskbarProgressState` + `TaskbarProgressFraction`). Serilog crash reporter (Services/CrashReporter.cs) with daily rolling log + per-crash dump + "copy path + open folder" dialog, hooks AppDomain/TaskScheduler/Dispatcher. Accessibility pass (AutomationProperties.Name + LiveSetting=Polite). GitHub Actions release workflow (.github/workflows/release.yml) with SBOM + SLSA attestations. New NuGet deps: Serilog 4.2.0, Serilog.Sinks.File 6.0.0.
- v3.4.0 — SpotX flag expansion. Added 6 new flags end-to-end (XAML + config + fingerprint + Build-SpotXParams + WPF model): `-sendversion_off` (privacy, default on), `-start_spoti`, `-devtools`, `-mirror`, `-confirm_spoti_recomended_uninstall`, `-download_method {curl|webclient}`. New Privacy + Advanced panels in PowerShell GUI; WPF shell auto-surfaces via reflection on `InstallConfiguration` properties. WPF shell bumped to v4.0.0-preview.3. Deferred: Version-picker dropdown (→ Track 4), CustomPatchesPath editor (→ Track 10), `-language` passthrough (→ Track 11).
- v3.3.1 — Critical fix. Corrected silent no-op `-new_fullscreen_mode` → `-newFullscreenMode` (real SpotX flag is camelCase) in both `LibreSpot.ps1:Build-SpotXParams` and `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`. Re-verified every other flag in `Build-SpotXParams` against SpotX `run.ps1` param block (2026-04-17) — all correct. `-SpotifyPath` gotcha softened to historical note (SpotX accepts it as a real string param; we just don't use it). Also bumped WPF shell to v4.0.0-preview.2 (csproj now declares Version/AssemblyVersion/FileVersion).
- v3.3.0 — Dependency update + new SpotX features. SpotX pinned to `0abf98a3` (Spotify 1.2.86.502), Spicetify CLI bumped to v2.43.1. Added 5 new SpotX GUI options: Plus features (-plus), experimental fullscreen (-newFullscreenMode), humorous progress bar (-funnyprogressBar), experimental Spotify features (-exp_spotify), lyrics block (-lyrics_block). Full config pipeline wiring: XAML, config load/save, normalization, fingerprint, Build-SpotXParams, summary toggles. ChkLyricsBlock/ChkOldLyrics mutual exclusivity with dependency-aware enable/disable.
- v3.2.0 — Major hardening: robust self-elevation, config normalization/quarantine, themed dark dialogs, safe file removal with blocklist, backup/restore with rollback, streaming process output, unsaved-changes detection, 8-phase uninstaller, declarative extension sync, PATH management, icon branding. UX polish pass: grouped Custom Install sections with live snapshot, dashboard-style Maintenance with status cards, action-aware progress stage labels, dependency-aware UI, saved/unsaved state messaging, "Load recommended defaults" action.
- v3.2.0-qapatch — Fix `Read-ProcessOutputDelta` missing from worker runspace exports (install crash). Scope DragMove to title bar only. Add TextWrapping to snapshot value TextBlocks.
- v3.2.0-audit — Deep engineering audit pass: StreamReader leak fix in Read-ProcessOutputDelta (leaveOpen constructor + dual dispose), log trimming math fix (subtract trimNotice length from budget), Copy Log reads actual log file instead of trimmed UI text, BtnBackToConfig full state reset, external process timeout with kill (Invoke-ExternalScriptIsolated), Spicetify version check non-blocking WaitForExit(5000), Save-LibreSpotConfig atomic fallback (File.Move instead of Move-Item), theme preview DoEvents re-entrancy guard.
- v3.2.0-audit2 — Second deep audit: WebClient dispose in theme preview, Reapply gracefully skips Spicetify when CLI missing, timer stopped on BtnBackToConfig, null-guard $p in Invoke-ExternalScriptIsolated finally block, consolidate `-confirm_spoti_recomended_over` into Build-SpotXParams (DRY), shared BrushConverter instance, Get-FileHash -LiteralPath, Spicetify version temp file cleanup in finally, log file BOM-free UTF-8 consistency.
- v3.1.1 — Reverted broken custom apps/extension packs. Kept theme preview.
- v3.1.0 — Audit: anti-hang, Spicetify apply failure recovery, lyrics themes, old lyrics/collab icon options.
- v3.0.6 — Fixed blank screen: removed -SpotifyPath, added -confirm_spoti_recomended_over.
- v3.0.5 — Bug fixes, edge cases, hardening.
- v3.0.3 — Bug fixes, logging, polish.
