

# LibreSpot

**SpotX + Spicetify Unified Installer**

A single-script PowerShell GUI that installs, configures, and maintains ad-free Spotify with themes, extensions, and the Spicetify Marketplace — no command-line knowledge required.

[![PowerShell](https://img.shields.io/badge/PowerShell-5.1%2B-blue?logo=powershell&logoColor=white)](https://github.com/PowerShell/PowerShell)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Version](https://img.shields.io/badge/Version-4.0.0--preview.6-brightgreen.svg)](https://github.com/SysAdminDoc/LibreSpot/releases)
[![Stable](https://img.shields.io/badge/Stable-3.7.2-blue.svg)](https://github.com/SysAdminDoc/LibreSpot/releases)
[![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/SysAdminDoc/LibreSpot/badge)](https://securityscorecards.dev/viewer/?uri=github.com/SysAdminDoc/LibreSpot)

</div>

## Quick Start

**One-liner install** — paste into PowerShell and hit Enter:

```powershell
irm https://github.com/SysAdminDoc/LibreSpot/releases/latest/download/LibreSpot.ps1 | iex
```

Or [download LibreSpot.ps1](https://github.com/SysAdminDoc/LibreSpot/releases/latest) and right-click **Run with PowerShell**.

> **Requirements:** Windows 10/11, PowerShell 5.1+ (built-in), internet connection

<div align="center">

<img width="1150" height="950" alt="2026-02-02 19_59_24-LibreSpot" src="https://github.com/user-attachments/assets/fb3d007b-f28a-4ecc-8a16-b0b866c03a4c" />

<img width="1150" height="950" alt="2026-02-02 20_16_59-LibreSpot" src="https://github.com/user-attachments/assets/83f99ae8-88b8-4f36-a207-4ffa9a163281" />

</div>

---

## What's New in v3.7

**Premium UI overhaul.** Win11 Mica backdrop (with graceful fallback on Windows 10), a left sidebar navigation rail with Lucide icons replacing the old top tab bar, semantic design tokens, hover-lift micro-interactions, and a shimmering install progress bar. Compact density pass means every panel fits a 1080-px screen without scrolling. Same install behavior, polished chrome.

The v4 desktop preview continues that polish with a sharper 6-12 px radius system, quieter scrollbars, cleaner first-run guidance, readable Custom setting cards, forced dark native window chrome, and calmer activity/support-bundle feedback for assistive technology.

---

## What It Does

LibreSpot wraps two powerful open-source projects into one polished interface:

- **[SpotX](https://github.com/SpotX-Official/SpotX)** — patches Spotify to remove ads, block telemetry, and enable experimental UI features
- **[Spicetify](https://github.com/spicetify)** — injects custom themes, extensions, and the in-app Marketplace into Spotify

Instead of running multiple scripts, editing config files, and hoping the versions are compatible, LibreSpot handles the entire workflow: clean uninstall, fresh Spotify install, SpotX patching, Spicetify CLI setup, theme installation, extension configuration, and Marketplace deployment — all in the correct order, with full error handling.

---

## Spotify Compatibility

> **Note:** Spotify frequently updates its client, which can break SpotX and Spicetify patches. LibreSpot blocks Spotify auto-updates by default (via SpotX) to keep your installation stable.
>
> If you manually update Spotify and patches stop working, use **Maintenance > Reapply After Update** to re-patch. The WPF Maintenance dashboard also flags **After Spotify update** drift and recommends targeted recovery steps before a full reset.

**Pinned dependency versions (v3.7.2):**

| Component | Pinned Version |
|---|---|
| SpotX | `3284673d` (Spotify 1.2.92) |
| Spicetify CLI | v2.43.2 |
| Marketplace | v1.0.8 |
| Themes | Commit `df033493` |

**Compatibility matrix:** Maintenance > Check matrix reports SpotX, Spicetify CLI, Marketplace, and theme archive status separately. The current SpotX target is Spotify `1.2.92`, while Spicetify CLI v2.43.2 declares Windows/Microsoft Store compatibility through Spotify `1.2.88`; LibreSpot warns about that gap so you can distinguish "SpotX can patch this build" from "Spicetify CSS maps are max-tested on this build."

---

## Features

### Three Modes

**Easy Install** — one click, sensible defaults. Removes any existing installation, applies SpotX ad-blocking with the new UI theme, installs Spicetify CLI with Marketplace, and enables Full App Display, Shuffle+, and Trash Bin extensions.

**Custom Install** — full control over every option. Configure SpotX patching flags (ad-blocking, podcasts, lyrics, UI experiments, update blocking, cache limits), pick from 21 themes (16 official + 5 community) with per-theme color schemes, select from 15 extensions (10 built-in + 5 community), and choose between clean or overlay install.

**Maintenance** — manage an existing installation without reinstalling. Backup and restore Spicetify configs, reapply patches after Spotify updates, export a redacted local support bundle, restore vanilla Spotify, uninstall Spicetify, check for dependency updates, or perform a full system reset.

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

### 27 Lyrics Color Themes

Custom Install exposes all 27 SpotX static lyrics color options: spotify, blueberry, blue, discord, forest, fresh, github, lavender, orange, pumpkin, purple, red, strawberry, turquoise, yellow, oceano, royal, krux, pinkle, zing, radium, sandbar, postlight, relish, drot, default, and spotify#2.

### 21 Themes, 200+ Color Schemes

**16 official themes:** Sleek, Dribbblish, Ziro, text, StarryNight, Turntable, Blackout, Blossom, BurntSienna, Default, Dreary, Flow, Matte, Nightlight, Onepunch, and SharkBlue.

**5 community themes:** Catppuccin (4 flavors), Comfy, Bloom (Fluent Design), Lucid (dynamic album-art backgrounds), and Hazy (glassmorphism). Downloaded directly from their GitHub repos.

Each theme ships with its full set of color schemes. **Live theme previews** load inline when selecting a theme in Custom Install. Or skip the theme and use the Marketplace to browse and install themes from within Spotify.

### 15 Extensions (10 Built-in + 5 Community)

**Built-in** (ship with Spicetify CLI):

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

**Community** (downloaded from GitHub during install):

| Extension | Description |
|---|---|
| [Hide Podcasts](https://github.com/theRealPadster/spicetify-hide-podcasts) | Remove podcast, episode, and audiobook UI elements |
| [Beautiful Lyrics](https://github.com/surfbryce/beautiful-lyrics) | Immersive synced lyrics with dynamic backgrounds and blur |
| [Playlist Icons](https://github.com/jeroentvb/spicetify-playlist-icons) | Custom icons and folder images for playlists |
| [Volume Percentage](https://github.com/daksh2k/spicetify-stuff) | Exact volume percentage next to the slider |
| [Ad-block (Spicetify fallback)](https://github.com/rxri/spicetify-extensions) | Spicetify-layer ad blocking for when SpotX patching fails on a newer Spotify build — **a fallback, not a SpotX replacement** |

### Auto-Reapply (new in v3.6.0)

Spotify auto-updates roughly every 1-2 weeks and overwrites the SpotX patches every time. Manually reapplying after every update gets old fast.

**Maintenance > Protect and repair > "Auto-reapply when Spotify updates itself"** registers a per-user scheduled task that fires at logon and every 30 minutes. It silently does nothing unless Spotify's version actually changed; when it changes, it hash-verifies the pinned SpotX script and reruns your saved config — but only when Spotify is closed, so it never interrupts playback. Every action gets logged to `%APPDATA%\LibreSpot\watcher.log` for audit.

You can also manage the task from the command line if you prefer:

```powershell
LibreSpot.ps1 -InstallWatcher      # register the scheduled task
LibreSpot.ps1 -UninstallWatcher    # remove it
LibreSpot.ps1 -Watch               # run one tick manually (what the task invokes)
```

### Other Details

- **Threaded UI** — installation runs in background runspaces; the GUI stays responsive with a live log, elapsed timer, and progress bar
- **Window management** — Spotify and installer windows are automatically hidden during installation; LibreSpot stays on top until finished
- **Settings persistence** — your Custom Install configuration is saved to `%APPDATA%\LibreSpot\config.json` and restored next launch
- **Config backup** — up to 5 rotating Spicetify config backups stored in `%USERPROFILE%\LibreSpot_Backups`
- **Architecture support** — x64 and ARM64 with per-architecture hash verification
- **Dual download methods** — falls back to BITS transfer if `Invoke-WebRequest` fails
- **Self-elevating** — auto-requests admin privileges when needed

---

## FAQ

**Will this break if Spotify updates?**
SpotX blocks Spotify auto-updates by default. If you manually update Spotify, use Maintenance > Reapply After Update to re-patch.

**What should I do after Spotify updates?**
Open Maintenance and check the After Spotify update note. LibreSpot compares the current Spotify version with the last patched version, watcher status, Spicetify apply result, and Marketplace state, then points to the safest next action: close Spotify, reapply the saved profile, repair Marketplace, restore vanilla Spotify, or open logs.

**Can I use this with a Premium account?**
Yes. Enable "Premium user (skip ad-blocking)" in Custom Install to skip ad-related patches while keeping all other modifications.

**How do I change my theme later?**
Use the Spicetify Marketplace (installed by default) to browse and apply themes from within Spotify, or re-run LibreSpot in Custom mode.

**Marketplace is installed but I do not see it.**
Use Maintenance > Repair and open Marketplace. LibreSpot reinstalls the custom app, re-enables `custom_apps`, reapplies Spicetify, and opens `spotify:app:marketplace` directly.

**How do I collect diagnostics without leaking local paths or secrets?**
Use Maintenance > Support bundle. LibreSpot previews the selected health report, operation journal, log, and crash-report windows, redacts local user/machine paths, GitHub headers, proxy credentials, tokens, passwords, and command-line secret arguments, then writes a local zip. It does not upload the bundle.

**How do I go back to stock Spotify?**
Use Maintenance > Full Reset. This removes all modifications, uninstalls Spotify, and cleans up every trace.

**Can I migrate from BlockTheSpot?**
BlockTheSpot archived its repository in February 2026. LibreSpot warns when it sees BlockTheSpot-family DLL or config artifacts next to Spotify. A normal install can replace them, and Maintenance > Full Reset is the fallback if you see blank screens or playback failures after patching.

**Is this safe?**
Every download is verified against pinned SHA256 hashes. LibreSpot doesn't host or redistribute any code — it downloads directly from the official SpotX and Spicetify GitHub repositories. See [Trust & risk disclosure](#trust--risk-disclosure) below for enforcement context and account risk details.

---

## Trust & risk disclosure

**What LibreSpot does:**
- Downloads SpotX and Spicetify CLI directly from their official GitHub repositories using commit-pinned URLs with SHA256 verification
- Patches the local Spotify installation to remove ads and apply themes/extensions
- Optionally registers a scheduled task for automatic reapplication after Spotify updates

**Downloader hardening (CVE-2025-54100):** LibreSpot fetches with PowerShell's `Invoke-WebRequest`. [CVE-2025-54100](https://nvd.nist.gov/vuln/detail/CVE-2025-54100) is a Windows PowerShell 5.1 web-content RCE fixed in the December 2025 Windows cumulative updates. The two mitigations are **SHA256 pinning** (guarantees payload integrity) and **patch level** (keeping Windows updated closes the parse-time vector); SHA256 alone does not remove the vector on an unpatched host. LibreSpot adds a non-blocking preflight that warns when the host predates the December 2025 patch wave. See [SECURITY.md](SECURITY.md#cve-2025-54100--windows-powershell-51-web-content-rce) for details.

**What LibreSpot does NOT do:**
- Collect, transmit, or store any credentials, tokens, or account data
- Bundle, host, or redistribute Spotify binaries or any upstream project code
- Communicate, *as LibreSpot itself*, with any server other than GitHub (for downloads) and Spotify (normal app traffic)
- Modify Spotify's authentication, payment, or account systems

> **Note on community extensions:** the bullet above covers LibreSpot itself. Some *opt-in* community extensions you can enable in Custom Install do contact their own services — for example, [Beautiful Lyrics](https://github.com/surfbryce/beautiful-lyrics) fetches lyrics from a third-party backend and uses an external API for optional Discord features. Extensions that talk to a third-party service are flagged in the Custom Install catalog and recorded in [`schemas/community-assets.json`](schemas/community-assets.json) under `networkBehavior`. They are off by default.

**Account risk:**
Spotify's [Terms of Service](https://www.spotify.com/legal/end-user-agreement/) and [User Guidelines](https://www.spotify.com/legal/user-guidelines/) prohibit circumventing ads and modifying the client. While enforcement against individual users of tools like SpotX has not been publicly documented, using LibreSpot is at your own risk. LibreSpot provides a "Full Reset" option in Maintenance mode to return Spotify to its unmodified state at any time.

**Enforcement landscape:**
Spotify has increased enforcement against client modification tools. In September 2025, Spotify DMCA'd ReVanced (which redistributed patched Spotify APKs). In February 2026, Spotify tightened Developer Platform access (Premium required for Dev Mode, 1 Client ID per developer, 5 authorized users). BlockTheSpot, which injected DLLs into the Spotify process, archived its repository in February 2026. LibreSpot's model differs from these tools: it does not redistribute patched binaries, does not inject DLLs, does not use Spotify API Client IDs, and downloads only from official upstream GitHub repositories with hash verification. Users should review [Spotify's User Guidelines](https://www.spotify.com/legal/user-guidelines/) and make their own informed decisions.

**Returning to stock Spotify:**
Use Maintenance > Full Reset. This removes all modifications, uninstalls Spotify, and cleans up every trace. You can also manually run `spicetify restore` followed by a clean Spotify reinstall.

---

## Signing & verification

Releases ship unsigned today. [SignPath Foundation](https://signpath.org/) OSS enrollment is pending. Once the cert arrives, tagged releases will Authenticode-sign both `LibreSpot.exe` and `LibreSpot-Desktop.exe` via the workflow in [.github/workflows/release.yml](.github/workflows/release.yml) and users will stop seeing the "Unknown publisher" SmartScreen warning.

The current latest stable release, v3.7.2, ships `LibreSpot.ps1`, `LibreSpot.exe`, and `checksums.txt` **as GitHub release assets**. The repository itself does not track build artifacts — `LibreSpot.exe` and `checksums.txt` are generated fresh per tag by [.github/workflows/release.yml](.github/workflows/release.yml), so always verify against the copies you downloaded from the [Releases page](https://github.com/SysAdminDoc/LibreSpot/releases), not against anything in a source checkout. Workflow-built tags also add `LibreSpot-Desktop.exe`, CycloneDX SBOM output, and GitHub provenance attestations.

Verify a downloaded release asset against that release's `checksums.txt` (run this in the folder you downloaded the assets into):

```powershell
# Compare the hash of each downloaded asset to its line in checksums.txt
Get-FileHash .\LibreSpot.exe  -Algorithm SHA256
Get-FileHash .\LibreSpot.ps1  -Algorithm SHA256
Get-Content  .\checksums.txt
```

For workflow-built assets that include GitHub attestations, verify provenance with:

```powershell
gh attestation verify .\LibreSpot.exe          -R SysAdminDoc/LibreSpot
gh attestation verify .\LibreSpot-Desktop.exe  -R SysAdminDoc/LibreSpot
```

## Project planning

Development planning is maintained in local working-tree docs. `ROADMAP.md` is the only active queue for incomplete work; completed work is represented by Git history and release notes.

## Credits

LibreSpot is a wrapper and installer — the real work is done by these projects:

- **[SpotX](https://github.com/SpotX-Official/SpotX)** — Spotify ad-blocking and patching
- **[Spicetify CLI](https://github.com/spicetify/cli)** — Spotify theming and extension framework
- **[Spicetify Marketplace](https://github.com/spicetify/marketplace)** — In-app store for themes and extensions
- **[Spicetify Themes](https://github.com/spicetify/spicetify-themes)** — Official community theme collection

---

## License

[MIT](LICENSE)
