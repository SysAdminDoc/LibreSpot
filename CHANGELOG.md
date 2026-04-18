# Changelog

All notable changes to LibreSpot will be documented in this file.

## [v3.5.1] - 2026-04-17

Hardening pass. Fixes bugs introduced in v3.5.0 plus some latent issues uncovered during the audit.

### Fixed
- **Foreign-patch detection fired on every launch** (introduced in v3.5.0). The previous signature list checked for `chrome_elf.dll` (part of every Spotify install — LibreSpot itself throws if it is *missing*) and `xpui.spa.bak` (created by SpotX's own backup step on every successful run). Revised to only match files BlockTheSpot-style injectors drop: `dpapi.dll`, `config.ini`, `version.dll`, `winmm.dll` next to `Spotify.exe`.
- **Backend.ps1 stamped the wrong version** in its HTTP User-Agent and internal log lines (`LibreSpot/3.3.0` instead of the real shell version). Synced to `3.5.0` with a comment noting the release workflow should fail a build when these drift.
- **Self-update check blocked the UI thread for up to 5 seconds on launch** when GitHub was slow. Refactored to a pure-.NET `HttpWebRequest` running on a ThreadPool thread, with cache write + UI update marshaled back through `Dispatcher.BeginInvoke` at idle priority. The cache read path is still synchronous (filesystem-only) and returns instantly on a warm cache.
- **`Check-ForUpdates` used lexical string comparison** for Spicetify CLI, Marketplace, and LibreSpot versions. That would have reported `v2.43.10` as *older* than `v2.43.9`. Replaced with a new `Compare-LibreSpotVersions` helper that parses the numeric prefix via `[Version]`, strips `-preview.*`/`-rc.*`, and treats stable as newer than a pre-release with the same prefix.
- **`$sender` parameter in `Window.Add_Closing`** shadowed PowerShell's automatic `$Sender` variable (PSAvoidAssignmentToAutomaticVariable). Renamed to `$closingSource`.

### Changed
- Exported `Compare-LibreSpotVersions` into the worker runspace so `Check-ForUpdates` (which runs there) can call it.
- `Save-SelfUpdateCache` is invoked only from the dispatcher thread so `ConvertTo-Json` / `Set-Content` never run concurrently with the main runspace from a ThreadPool thread.

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
