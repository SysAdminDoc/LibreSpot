# LibreSpot - Working Notes

## Overview
SpotX + Spicetify unified installer. Single PowerShell script (~1600 lines) with WPF GUI.
Three modes: Easy Install, Custom Install, Maintenance.

## Version
v3.0.6

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
- **Blank screen = outdated SpotX pin.** SpotX patches target specific Spotify versions. When Spotify's installer updates past the pinned version, patches corrupt the UI. Fix: update SpotX commit + SHA256 hash.
- `-SpotifyPath` bypasses SpotX's version check — LibreSpot assumes the just-downloaded Spotify matches the SpotX pin. If they drift, blank screen.
- `spicetify backup apply` must run AFTER SpotX patching, not before.
- Themes needing JS injection: Dribbblish, StarryNight, Turntable (`inject_theme_js 1`).
- Settings persist to `%APPDATA%\LibreSpot\config.json`.
- Up to 5 rotating Spicetify config backups in `%USERPROFILE%\LibreSpot_Backups`.

## Build
No build system — single .ps1 file. The .exe is compiled via PS2EXE separately.
Version string is at `$global:VERSION` (~line 53). README badge must match.

## Version History
- v3.0.6 — Updated SpotX pin to `6070bbcf` (supports Spotify 1.2.85.519). Fixes blank screen (issue #5).
- v3.0.5 — Bug fixes, edge cases, hardening
- v3.0.3 — Bug fixes, logging, polish
