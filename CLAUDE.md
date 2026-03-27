# LibreSpot - Working Notes

## Overview
SpotX + Spicetify unified installer. Single PowerShell script (~1600 lines) with WPF GUI.
Three modes: Easy Install, Custom Install, Maintenance.

## Version
v3.1.0

## Tech Stack
- PowerShell 5.1+ with WPF GUI (XamlReader::Parse, single-quoted here-string)
- Async install via `[PowerShell]::Create()` + `BeginInvoke()` with `DispatcherTimer` polling
- SHA256 hash verification on all downloads
- Dual download: `Invoke-WebRequest` with BITS fallback

## Key Files
- `LibreSpot.ps1` — entire application (single-file)
- `LibreSpot.exe` — PS2EXE compiled executable
- `LibreSpot.ico` — application icon

## Pinned Dependencies (update these together)
| Component | Version/Commit | Location in script |
|---|---|---|
| SpotX | `6070bbcf` (Spotify 1.2.85.519) | `$global:PinnedReleases.SpotX` (~line 58) |
| Spicetify CLI | v2.42.14 | `$global:PinnedReleases.SpicetifyCLI` (~line 64) |
| Marketplace | v1.0.8 | `$global:PinnedReleases.Marketplace` (~line 71) |
| Themes | `9af41cf` | `$global:PinnedReleases.Themes` (~line 76) |

## Architecture
1. **Uninstaller** (8-phase): process kill, AppX removal, silent uninstall, filesystem cleanup, registry cleanup, scheduled tasks, firewall rules, verification sweep
2. **SpotX patching**: downloads pinned run.ps1, passes `-SpotifyPath` to patch in-place (bypasses SpotX's own version check)
3. **Spicetify**: CLI install, theme copy, extension config, marketplace deploy, then `backup apply`
4. **Window watcher**: separate runspace hides Spotify windows during install via `ShowWindowAsync`

## Key Functions
- `Build-SpotXParams` (~line 671) — maps GUI checkboxes to SpotX CLI flags
- `Module-InstallSpotX` (~line 1337) — SpotX download + hash verify + invoke
- `Module-ApplySpicetify` (~line 1451) — `spicetify backup apply --bypass-admin`
- `Module-InstallSpicetifyCLI` (~line 1384) — CLI zip extract + PATH setup
- `Module-InstallThemes` (~line 1409) — theme archive extract + config
- `Module-InstallMarketplace` (~line 1437) — marketplace zip into CustomApps

## Gotchas
- **NEVER use `-SpotifyPath` with SpotX.** It bypasses SpotX's version compatibility check, allowing patches meant for version X to be applied to version Y — causing blank screen. Use `-confirm_spoti_recomended_over` instead so SpotX manages versions.
- **Blank screen = version mismatch.** SpotX patches target specific Spotify versions. If the installed version doesn't match, patches corrupt the UI. SpotX must control the Spotify download to ensure compatibility.
- **Don't pre-install Spotify separately.** SpotX downloads the correct version itself via its own CDN (`broad-pine-bbc0.amd64fox1.workers.dev`). Pre-installing from `download.spotify.com` gets the latest (possibly unsupported) version.
- **Always pass `-confirm_uninstall_ms_spoti`.** SpotX prompts via Read-Host if MS Store Spotify is found. Since LibreSpot redirects stdout, the prompt hangs forever.
- **SpotX stdin hangs.** Any Read-Host prompt in SpotX will hang because `Invoke-ExternalScriptIsolated` uses `CreateNoWindow=$true`. All interactive prompts must be pre-answered via flags.
- **Spicetify apply failure = auto-restore.** If `backup apply` exits non-zero, the script now calls `spicetify restore` to prevent blank screen.
- `spicetify backup apply` must run AFTER SpotX patching, not before.
- Themes needing JS injection: Dribbblish, StarryNight, Turntable (`inject_theme_js 1`).
- Settings persist to `%APPDATA%\LibreSpot\config.json`.
- Up to 5 rotating Spicetify config backups in `%USERPROFILE%\LibreSpot_Backups`.

## Build
No build system — single .ps1 file. The .exe is compiled via PS2EXE separately.
Version string is at `$global:VERSION` (~line 53). README badge must match.

## Version History
- v3.1.0 — Audit release: anti-hang (-confirm_uninstall_ms_spoti), Spicetify apply failure recovery (auto-restore), 27 lyrics themes, old lyrics/collab icon options, removed dead pre-install code, fixed duplicate ComboBox population.
- v3.0.6 — Fixed blank screen: removed `-SpotifyPath` (bypassed version check), added `-confirm_spoti_recomended_over`, let SpotX manage Spotify version. Updated SpotX pin to `6070bbcf`. Fixes #5.
- v3.0.5 — Bug fixes, edge cases, hardening
- v3.0.3 — Bug fixes, logging, polish
