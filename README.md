<div align="center">

# LibreSpot

**SpotX + Spicetify Unified Installer**

A single-script PowerShell GUI that installs, configures, and maintains ad-free Spotify with themes, extensions, and the Spicetify Marketplace — no command-line knowledge required.

[![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue?logo=powershell&logoColor=white)](https://github.com/PowerShell/PowerShell)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-3.0.0-brightgreen.svg)](https://github.com/SysAdminDoc/LibreSpot/releases)

</div>

---

## What It Does

LibreSpot wraps two powerful open-source projects into one polished interface:

- **[SpotX](https://github.com/SpotX-Official/SpotX)** — patches Spotify to remove ads, block telemetry, and enable experimental UI features
- **[Spicetify](https://github.com/spicetify)** — injects custom themes, extensions, and the in-app Marketplace into Spotify

Instead of running multiple scripts, editing config files, and hoping the versions are compatible, LibreSpot handles the entire workflow: clean uninstall, fresh Spotify install, SpotX patching, Spicetify CLI setup, theme installation, extension configuration, and Marketplace deployment — all in the correct order, with full error handling.

---

## Features

### Three Modes

**Easy Install** — one click, sensible defaults. Removes any existing installation, applies SpotX ad-blocking with the new UI theme, installs Spicetify CLI with Marketplace, and enables Full App Display, Shuffle+, and Trash Bin extensions.

**Custom Install** — full control over every option. Configure SpotX patching flags (ad-blocking, podcasts, lyrics, UI experiments, update blocking, cache limits), pick from 16 official Spicetify themes with per-theme color schemes, select individual extensions, and choose between clean or overlay install.

**Maintenance** — manage an existing installation without reinstalling. Backup and restore Spicetify configs, reapply patches after Spotify updates, restore vanilla Spotify, uninstall Spicetify, check for dependency updates, or perform a full system reset.

### Supply Chain Integrity

Every dependency is pinned to a specific version with SHA256 hash verification:

| Component | Pinned Version |
|---|---|
| SpotX | v1.9 |
| Spicetify CLI | v2.42.8 |
| Marketplace | v1.0.8 |
| Themes | Commit `9af41cf` |

Downloads are verified against known hashes before execution. The built-in update checker compares pinned versions against the latest GitHub releases and reports available upgrades — you decide when to update.

### Comprehensive Uninstaller

The built-in 8-phase uninstaller handles every trace of Spotify and Spicetify:

1. Process termination (with retry logic)
2. Microsoft Store / AppX removal
3. Native silent uninstaller
4. File system cleanup (Roaming, Local, Temp, cache, shortcuts, glob patterns)
5. Registry cleanup (uninstall keys, protocol handlers, app paths, startup entries)
6. Scheduled task removal
7. Firewall rule removal
8. Verification sweep with retry

### 16 Themes, 150+ Color Schemes

Sleek, Dribbblish, Ziro, text, StarryNight, Turntable, Blackout, Blossom, BurntSienna, Default, Dreary, Flow, Matte, Nightlight, Onepunch, and SharkBlue — each with their full set of official color schemes. Or skip the theme and use the Marketplace to browse and install themes from within Spotify.

### 10 Built-in Extensions

| Extension | Description |
|---|---|
| Full App Display | Full-screen album art with blur and playback controls |
| Shuffle+ | True Fisher-Yates shuffle instead of Spotify's weighted algorithm |
| Trash Bin | Auto-skip songs and artists you've marked as unwanted |
| Keyboard Shortcuts | Vim-style navigation bindings |
| Bookmark | Save and recall pages, tracks, albums, and timestamps |
| Loopy Loop | Set A-B loop points on any track |
| Pop-up Lyrics | Synchronized lyrics in a separate resizable window |
| Auto Skip Video | Skip canvas videos and region-locked content |
| Auto Skip Explicit | Skip tracks marked as explicit |
| Web Now Playing | Expose now-playing data for Rainmeter widgets |

### Other Details

- **Threaded UI** — installation runs in background runspaces; the GUI stays responsive with a live log, elapsed timer, and progress bar
- **Window management** — Spotify and installer windows are automatically hidden during installation; LibreSpot stays on top until finished
- **Settings persistence** — your Custom Install configuration is saved to `%APPDATA%\LibreSpot\config.json` and restored next launch
- **Config backup** — up to 5 rotating Spicetify config backups stored in `%USERPROFILE%\LibreSpot_Backups`
- **Architecture support** — x64, x86, and ARM64 with per-architecture hash verification
- **Dual download methods** — falls back to BITS transfer if `Invoke-WebRequest` fails
- **Self-elevating** — auto-requests admin privileges when needed

---

## Requirements

- **Windows 10 / 11**
- **PowerShell 5.1+** (built into Windows)
- Internet connection for initial download

---

## Usage

### Option 1: Direct Run

```powershell
irm https://raw.githubusercontent.com/SysAdminDoc/LibreSpot/main/LibreSpot.ps1 | iex
```

### Option 2: Download and Run

1. Download `LibreSpot.ps1` from the [latest release](https://github.com/SysAdminDoc/LibreSpot/releases)
2. Right-click the file and select **Run with PowerShell**, or open a PowerShell window and run:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\LibreSpot.ps1
```

The GUI will launch and prompt for administrator privileges if needed.

---

## FAQ

**Will this break if Spotify updates?**
SpotX blocks Spotify auto-updates by default. If you manually update Spotify, use Maintenance > Reapply After Update to re-patch.

**Can I use this with a Premium account?**
Yes. Enable "Premium user (skip ad-blocking)" in Custom Install to skip ad-related patches while keeping all other modifications.

**How do I change my theme later?**
Use the Spicetify Marketplace (installed by default) to browse and apply themes from within Spotify, or re-run LibreSpot in Custom mode.

**How do I go back to stock Spotify?**
Use Maintenance > Full Reset. This removes all modifications, uninstalls Spotify, and cleans up every trace.

**Is this safe?**
Every download is verified against pinned SHA256 hashes. LibreSpot doesn't host or redistribute any code — it downloads directly from the official SpotX and Spicetify GitHub repositories.

---

## Credits

LibreSpot is a wrapper and installer — the real work is done by these projects:

- **[SpotX](https://github.com/SpotX-Official/SpotX)** — Spotify ad-blocking and patching
- **[Spicetify CLI](https://github.com/spicetify/cli)** — Spotify theming and extension framework
- **[Spicetify Marketplace](https://github.com/spicetify/marketplace)** — In-app store for themes and extensions
- **[Spicetify Themes](https://github.com/spicetify/spicetify-themes)** — Official community theme collection

---

## License

[MIT](LICENSE)
