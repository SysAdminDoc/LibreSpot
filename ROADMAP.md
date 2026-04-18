# LibreSpot Roadmap

Living document. Consolidated from 8 research passes (SpotX upstream, Spicetify ecosystem, competitors, WPF UX, distribution channels, Windows 11 shell APIs, power-user CLI, community patterns).

Items are organized by **track** (parallel workstreams) then prioritized into **releases**. Effort is in developer-hours. ROI is `★` (nice) → `★★★★★` (game-changer).

---

## Current state

- **v3.3.0** shipped — PowerShell monolith, PS2EXE-wrapped, all current SpotX/Spicetify features wired.
- **v4.0.0-preview.1** in flight — native .NET 8 WPF MVVM shell with token design system, structured stdout protocol to embedded PS backend.
- Pins: SpotX `0abf98a3` • Spicetify CLI v2.43.1 • Marketplace v1.0.8 • Themes `9af41cf`. All upstream versions current except themes (TokyoNight/kanagawa schemes added post-pin).

---

## Track 1 — Critical fixes ✅ **COMPLETE (v3.3.1)**

| # | Item | Status |
|---|---|---|
| 1.1 | **Fix `-new_fullscreen_mode` → `-newFullscreenMode`** typo. Real SpotX flag is camelCase; prior wiring was a silent no-op. | ✅ Fixed in [LibreSpot.ps1:2050](LibreSpot.ps1#L2050) and [src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1179](src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1#L1179) |
| 1.2 | **Re-verify `-SpotifyPath` gotcha**. | ✅ SpotX accepts it as real param; gotcha softened to historical note in [CLAUDE.md](CLAUDE.md) |
| 1.3 | **Bump spicetify-themes pin** past `9af41cf`. | ✅ No-op — `9af41cf` (2026-01-18) is still HEAD |
| 1.4 | **Verify all SpotX param block** in run.ps1 vs `Build-SpotXParams`. | ✅ Every other flag in `Build-SpotXParams` verified correct against SpotX `run.ps1` param block |

---

## Track 2 — SpotX flag expansion ✅ **COMPLETE (v3.4.0)**

Cross-checked authoritative SpotX `run.ps1` param block (2026-04-17): most "missing" flags from the initial research were already wired. Net-new additions shipped in v3.4.0:

| Flag | Status | Section |
|---|---|---|
| `-sendversion_off` | ✅ Shipped (default on) | Privacy |
| `-start_spoti` | ✅ Shipped | Core behavior |
| `-devtools` | ✅ Shipped | Advanced |
| `-mirror` | ✅ Shipped | Advanced |
| `-confirm_spoti_recomended_uninstall` | ✅ Shipped | Advanced |
| `-download_method {curl\|webclient}` | ✅ Shipped in PowerShell GUI; ⏳ WPF XAML ComboBox deferred to later preview | Advanced |

**Already wired before v3.4.0 (verified correct):** `-podcasts_off`/`-on`, `-adsections_off`, `-canvashome_off`, `-homesub_off`, `-new_theme`, `-topsearchbar`, `-rightsidebar_off`, `-rightsidebarcolor`, `-hide_col_icon_off`, `-block_update_on`/`-off`, `-DisableStartup`, `-no_shortcut`, `-cache_limit`, `-plus`, `-newFullscreenMode`, `-funnyprogressBar`, `-exp_spotify`, `-lyrics_block`, `-old_lyrics`, `-premium`, `-lyrics_stat`, `-confirm_uninstall_ms_spoti`, `-confirm_spoti_recomended_over`.

**Deferred (feed into other tracks):**
- `-version <string>` → Track 4 Spotify version dropdown
- `-CustomPatchesPath` → Track 10 custom patches editor
- `-language <code>` → Track 11 i18n
- `-urlform_goofy`/`-idbox_goofy` / `-err_ru` — niche, skip

---

## Track 3 — Spicetify ecosystem expansion (v4.2)

### 3.A New themes to offer
- **Catppuccin** (4 flavors × 26 colors) — https://github.com/catppuccin/spicetify — de-facto dark-theme standard. Pair with our Catppuccin Mocha aesthetic.
- **Comfy** — https://github.com/Comfy-Themes/Spicetify — bundles Catppuccin, Rosé Pine, Nord, Everforest, Kanagawa in one theme.
- **Nord** — community favorite.
- **SpotifyNoPremium** — declutters UI; Daksh777/SpotifyNoPremium.

### 3.B New custom apps (new Custom Install accordion)
| App | Source | Purpose |
|---|---|---|
| **Statistics** | harbassan/spicetify-apps | Top artists/tracks/library analysis |
| **Library** | harbassan/spicetify-apps | Full-page library view, folder images |
| **Better Local Files** | Pithaya/spicetify-apps | Improved local file UI |
| **Eternal Jukebox** | Pithaya/spicetify-apps | Infinite seamless loop |
| **Playlist Maker** | Pithaya/spicetify-apps | Drag-and-drop builder |
| **lyrics-plus** | Spicetify CLI built-in | Already ships with CLI; just expose toggle |

### 3.C New extensions (extend existing list)
| Extension | Source | Why |
|---|---|---|
| **Spicy Lyrics** | Spikerko/spicy-lyrics | Dynamic backgrounds, immersive views |
| **spicetify-history** | nelsongillo/spicetify-history | Track history page (missing from stock Spotify) |
| **Sesh-Stats** | bojanraic/spicetify-extensions | Per-session stats |
| **Focus-Mode** | bojanraic/spicetify-extensions | Hides all UI except album art |
| **Private Session** | bojanraic/spicetify-extensions | Auto-enable every launch |
| **Made for You Shortcut**, **Convert Japanese**, **Extended Copy**, **Availability Map** | Pithaya/spicetify-apps | Power-user utilities |
| **rxri sorting/filtering pack** | rxri/spicetify-extensions | Updated Jan 2026 |

**Effort:** ~8h (theme catalog data + color schemes + live preview integration + extension JSON sync). **ROI:** ★★★★.

---

## Track 4 — Competitor-inspired features (v4.3)

Stolen from BlockTheSpot-Installer, SpotX-Spicetify-Universal-Installer, and Spicetify Manager.

| # | Feature | Source of idea | Status |
|---|---|---|---|
| 4.1 | **Spotify version dropdown** — inline manifest of 5 known-good Spotify builds; emits SpotX `-version <string>` when non-default. Config key `SpotX_SpotifyVersionId`. | BlockTheSpot-Installer | ✅ **v3.5.0** |
| 4.2 | **Auto-reapply watcher** — scheduled task detects `Spotify.exe` version bump, silently re-runs patch pipeline. | Spicetify Manager | ⏳ Pending (needs Win32 TaskScheduler + headless `--watch` flag) |
| 4.3 | **"Status at a glance" dashboard** — on launch show Spotify version, Spicetify version, SpotX state, last patch timestamp, backup count. | Spicetify Manager | ⏳ Pending |
| 4.4 | **Self-update check** — title-bar "Update available →" banner, 24h cache in `%APPDATA%\LibreSpot\update-check.json`. Zero telemetry. | BlockTheSpot-Installer | ✅ **v3.5.0** |
| 4.5 | **Repair / diagnostic button** — inspects installation health with per-issue fix buttons. | Spicetify Manager | ⏳ Pending |
| 4.6 | **Crash dump** to local folder + dialog on fatal. | BlockTheSpot-Installer | ✅ Shipped as Serilog crash reporter (v4.0.0-preview.4) |
| 4.7 | **`-Clean` CLI flag** — `irm ... \| iex -clean` pre-ticks Easy + CleanInstall for one-shot rebuild. | yodaluca23 | ✅ **v3.5.0** |
| 4.8 | **Pre-patched Spotify warning** — detects BlockTheSpot-style injector files (`dpapi.dll`, `config.ini`, `version.dll`, `winmm.dll` next to `Spotify.exe`), shows themed dialog once per session. | Fluent-Modded-Spotify issue | ✅ **v3.5.0** (signature list tightened in v3.5.1 to remove `chrome_elf.dll` / `xpui.spa.bak` false positives) |

---

## Track 5 — WPF shell polish (v4.0 stable)

Consolidating the native shell launched in v4.0.0-preview.1.

| # | Feature | Ref | Effort | Status |
|---|---|---|---|---|
| 5.1 | **Mica backdrop** via `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE=38, DWMSBT_MAINWINDOW=2)` + `DWMWA_USE_IMMERSIVE_DARK_MODE=20`. Detect `OSVersion.Build >= 22621`. | tvc-16.science/mica-wpf | 0.5h | ✅ **v4.0.0-preview.4** ([Services/Win11ShellIntegration.cs](src/LibreSpot.Desktop/Services/Win11ShellIntegration.cs)) |
| 5.2 | **Wpf.Ui 4.2** — adopt `TitleBar` (Win11 SnapLayout), `Snackbar`/`InfoBar`, `NumberBox`, `SplitButton`. | lepoco/wpfui | 3h | ⏳ Deferred (requires visual-QA session with running app) |
| 5.3 | **TaskbarItemInfo.ProgressState** — mirror job runner state (None/Normal/Paused/Error/Indeterminate) + `ProgressValue`. | Built-in WPF | 1h | ✅ **v4.0.0-preview.4** (VM computed props `TaskbarProgressState` + `TaskbarProgressFraction`) |
| 5.4 | **Toast notifications** via `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 on run completion. | MS.Toolkit.Uwp.Notifications | 2h | ⏳ Pending (needs COM activation registration) |
| 5.5 | **WinUtil-style "Undo Selected Actions" pane** — after a run, show reversible list with per-item undo. | WinUtil 2026 | 6h | ⏳ Pending (large UX feature — separate design session) |
| 5.6 | **Serilog crash reporting** — daily rolling file + `%LOCALAPPDATA%\LibreSpot\crashes\` dumps. Hooks `AppDomain.CurrentDomain.UnhandledException` + `Dispatcher.UnhandledException` + `TaskScheduler.UnobservedTaskException`. Crash dialog: Open folder / Copy to clipboard. Zero network. | Serilog | 3h | ✅ **v4.0.0-preview.4** ([Services/CrashReporter.cs](src/LibreSpot.Desktop/Services/CrashReporter.cs)) |
| 5.7 | **Accessibility pass** — `AutomationProperties.Name` on icon buttons + activity badge. `LiveSetting=Polite` on badge. | MS Learn | 2h | ✅ **v4.0.0-preview.4** |

---

## Track 6 — Windows 11 shell integration (v4.4)

| # | Feature | API | Effort | ROI |
|---|---|---|---|---|
| 6.1 | **Jump List** — `JumpTask` entries: "Run Easy Install", "Reapply patches", "Open backup folder", "Launch Spotify". | `System.Windows.Shell.JumpList` | 0.25h | ★★★★ |
| 6.2 | **Thumbnail toolbar** — `TaskbarItemInfo.ThumbButtonInfos` with Reapply / Restore / Settings. | Built-in WPF | 0.5h | ★★★ |
| 6.3 | **System tray minimize** — `H.NotifyIcon.Wpf` v2.1 (Win11 acrylic flyouts). Single-instance + close-to-tray + first-minimize balloon. | H.NotifyIcon.Wpf | 1h | ★★★★ |
| 6.4 | **URL protocol `librespot://`** — `HKCU\Software\Classes\librespot` + `URL Protocol` + `shell\open\command`. Parse in `App.OnStartup`, route via named-pipe to running instance. | Registry | 0.25h | ★★★★ |
| 6.5 | **File association `.librespot`** — register as config-profile file type; double-click opens with `--import`. | Registry | 0.25h | ★★★ |
| 6.6 | **Actionable persistent toasts** — `Microsoft.Windows.AppNotifications` (WinAppSDK 1.6+), `Scenario.Reminder`. "Spotify updated — Reapply?" with inline Reapply button, stays in Action Center. Unpackaged app via `SetActivationInfo`. | Microsoft.WindowsAppSDK | 2h | ★★★★ |
| 6.7 | **Scheduled-Task auto-reapply watcher** — `Microsoft.Win32.TaskScheduler` NuGet, logon trigger + 15-min repetition, runs `--watch` headless mode. Feeds Track 4.2. | Microsoft.Win32.TaskScheduler | 1h | ★★★★★ |
| 6.8 | **Legacy HKCR context menu on Spotify `.lnk`** — "Reapply with LibreSpot" on Spotify shortcut right-click. Lives under Win11 "Show more options". | Registry | 0.5h | ★★ |

**Skip:** MSIX sparse package (cert cost), Widgets board (<8% DAU), preview handler (COM overhead), Windows Hello (no threat), Start Menu pinning (API removed).

---

## Track 7 — Distribution & release engineering (ship with v4.0 stable)

Priority rollout order. Kick off async items (SignPath application, code signing) first.

| # | Channel / work | Effort | ROI | Notes |
|---|---|---|---|---|
| 7.1 | **SignPath Foundation free tier** application for OSS code signing. HSM-backed, GitHub Action integration. ~1-2 week approval. Removes SmartScreen "Unknown publisher" block. | 4h setup | ★★★★★ | **Pending** — external application, async |
| 7.2 | **GitHub Releases hardening** — `checksums.txt` (sha256 for every asset), CycloneDX SBOM (`dotnet CycloneDX`), `actions/attest-build-provenance@v2` + `attest-sbom@v2` (SLSA L3). Users verify via `gh attestation verify`. | 3h one-time | ★★★★ | ✅ **Shipped** ([.github/workflows/release.yml](.github/workflows/release.yml)) — triggers on `v*` tags, builds PS2EXE + WPF self-contained, emits checksums + SBOM + attestations |
| 7.3 | **winget PR** via `wingetcreate new <url>` + `wingetcreate submit`. Portable installer type. Manifest schema 1.10. Auto-bump via WingetCreate GitHub Action. | 4h first, 30min/update | ★★★★★ | Default on Win11 23H2+ |
| 7.4 | **Velopack auto-update** — replaces Squirrel/ClickOnce. Rust-based, ~2s update-and-relaunch, no UAC on update. `vpk pack` → `vpk upload github`. | 5h | ★★★★ | Essential for .NET 8 WPF shell |
| 7.5 | **Scoop personal bucket** at `SysAdminDoc/scoop-bucket` with `checkver` + `autoupdate` blocks. Nightly GitHub Action. | 2h | ★★★ | Power-user/dev audience |
| 7.6 | **Chocolatey community submission** — Nuspec XML + `tools/chocolateyInstall.ps1` with `Install-ChocolateyPackage -Checksum64 sha256`. 2-10 day moderator queue. | 5h | ★★ | MSP/enterprise |
| 7.7 | **Skip Microsoft Store / MSIX** — AppContainer sandbox incompatible with SpotX admin writes to `%APPDATA%\Spotify`. Sparse Package needs cert + rarely approved for modifiers. | — | — | Architectural mismatch |

**Total effort:** ~25-30h across two weekends for near-universal Windows reach.

---

## Track 8 — Power-user CLI & fleet deployment (v5.0)

Target audience: sysadmins deploying to fleets. No competing tool ships any of this.

| # | Flag / feature | Priority | Effort | Example |
|---|---|---|---|---|
| 8.1 | `--silent` / `--quiet` / `--no-restart` / `--accept-eula` / `/S` aliases. No UI, no console window. | MUST | 3h | `librespot.exe --silent --preset corp.json` |
| 8.2 | **JSON answer file** with bundled JSON Schema Draft-07 validation. Pins SpotX flags, Spicetify theme, color scheme, extension list, custom apps, post-install behavior. | MUST | 4h | `--preset corp.json` |
| 8.3 | **`--detect --json`** — idempotency/inventory for Ansible/Puppet. Exit `0`=compliant, `1`=not installed, `2`=drift. | MUST | 2h | JSON with versions + drift flag |
| 8.4 | **NDJSON structured logs** to `%ProgramData%\LibreSpot\logs\librespot-YYYYMMDD.log`. One event per line, `--log-format json\|text`, `--log-file path`. Rotate at 10MB, keep 7. Splunk/Loki-ready. | MUST | 3h | See research output |
| 8.5 | **Uninstall CLI** — `librespot.exe uninstall --silent --purge --yes --keep-spotify`. | MUST | 2h | |
| 8.6 | **`validate` command** — schema validation with fuzzy-match typo suggestions (Levenshtein). | MUST | 2h | `librespot validate corp.json` |
| 8.7 | **MSI wrapper** via WiX Toolset + `.intunewin`. Return codes 0/1603/3010/1618 (Windows Installer standard). Intune detection rule via `HKLM\SOFTWARE\LibreSpot\Version`. | SHOULD | 6h | For GPO/SCCM/Intune deployment |
| 8.8 | **PowerShell module** `LibreSpot` on PSGallery. Cmdlets: `Install-LibreSpot`, `Get-LibreSpotStatus`, `Uninstall-LibreSpot`, `Test-LibreSpotPreset`, `Export-LibreSpotPreset`. | SHOULD | 5h | |
| 8.9 | **Stdin/pipeline** — `-` means stdin. Also `--preset-url https://intranet/preset.json`. | SHOULD | 1h | `Get-Content x.json \| librespot install --preset -` |
| 8.10 | **Dry-run mode** — `--dry-run` / PowerShell `-WhatIf` alias. Numbered plan output, no changes. | SHOULD | 3h | |
| 8.11 | **Deployment recipes doc** — `docs/deployment/` with WinRM, PSRemoting over SSH, PDQ Deploy, Intune Win32 app examples. | SHOULD | 2h | No code, just docs |
| 8.12 | **Pre/post scripts** — `--pre-script foo.ps1 --post-script bar.ps1`. PatchMyPC parity. | SHOULD | 2h | |

**Total v5.0 effort:** ~35h. Makes LibreSpot the only fleet-deployable Spotify patcher.

---

## Track 9 — Community & sharing (v5.1)

Zero-server infrastructure. Every share carries branding; every install surfaces the gallery.

| # | Feature | Library / ref | Effort |
|---|---|---|---|
| 9.1 | **Shareable URI** — `librespot://apply?v=1&d=<base64url(brotli(json))>`. Typical 2KB preset → ~400 bytes base64url. Fits a Bluesky post. | `BrotliStream` (built-in) | 2h |
| 9.2 | **Preset gallery** — Windhawk-style. Bundle 4-5 in-app presets (`Minimalist Ad-Free`, `Premium Polish`, `Streamer-Safe`, `Tiny PC`, `Default`). `WrapPanel` card UI, lazy thumbnails. | Built-in + `HttpClient` | 5h |
| 9.3 | **Secure import gate** — `JsonSchema.Net`, whitelist theme/extension names, reject unknown/path/exec keys. Diff preview modal before apply. Blocklist JSON cached daily. | JsonSchema.Net | 3h |
| 9.4 | **QR code generation** — 1200×630 shareable card with preset summary + QR in corner. Auto-opens Explorer. | QRCoder + SkiaSharp | 3h |
| 9.5 | **Preset marketplace repo** — `SysAdminDoc/LibreSpot-Presets` with `/presets/{slug}/preset.json` + screenshot + README. CI-generated `manifest.json` on PR merge. ETag-cached client fetch. Mirrors Spicetify Marketplace pattern. | — | 5h + ongoing |
| 9.6 | **Theme showcase with screenshots** — pull from `spicetify/spicetify-themes/{theme}/screenshot.png`. 7-day cache in `%LOCALAPPDATA%\LibreSpot\thumbs\`. | Built-in | 3h |
| 9.7 | **Local multi-profile** — `%APPDATA%\LibreSpot\profiles\{work-laptop,home,gaming}\config.json` + `active.txt`. TitleBar ComboBox to switch. Export profile as `.librespot` zip. Feeds 6.5 file association. | Built-in | 4h |
| 9.8 | **In-app changelog viewer** — `GET api.github.com/repos/.../releases`, compare `lastSeenVersion`, show markdown modal on mismatch. "What's New" menu item to re-open. | Markdig + Markdig.Wpf | 2h |
| 9.9 | **Community links menu** — Help → Community → r/spicetify, Spicetify Discord, SpotX Telegram, LibreSpot Discussions. `Process.Start` with `UseShellExecute=true`. | Built-in | 0.25h |
| 9.10 | **Local-only usage insights** — `stats.json` counters, 10 unlockable badges (First Install, Theme Explorer ×5, Preset Curator, Shared ×3). Sparkline dashboard. Zero telemetry. | LiveCharts2 | 4h |

**Total v5.1 effort:** ~31h.

---

## Release plan — suggested order

| Release | Theme | Tracks | Effort |
|---|---|---|---|
| **v3.3.1** ✅ | Critical fixes | 1.1–1.4 | ~2.5h |
| **v3.4.0** ✅ | SpotX flag expansion (6 new flags) | Track 2 | ~4h |
| **v4.0.0-preview.4** ✅ | Mica + taskbar progress + Serilog + a11y + release CI | 5.1, 5.3, 5.6, 5.7, 7.2 | ~9h |
| **v3.5.0 / v4.0.0-preview.5** ✅ | Self-update + pre-patched warning + version dropdown + `-Clean` | 4.1, 4.4, 4.7, 4.8 | ~7h |
| **v4.0.0** (stable) | Wpf.Ui + toasts + undo pane + Velopack + signing + winget + remaining Track 4 | 5.2, 5.4, 5.5, 7.1, 7.3–7.5, 4.2, 4.3, 4.5 | ~25h |
| **v4.2.0** | Spicetify ecosystem expansion | 3.A–3.C | ~8h |
| **v4.3.0** | Competitor parity + differentiators | 4.1–4.8 | ~21h |
| **v4.4.0** | Windows 11 shell integration | 6.1–6.8 | ~5.75h |
| **v5.0.0** | Fleet deployment / enterprise | 8.1–8.12 | ~35h |
| **v5.1.0** | Community & sharing | 9.1–9.10 | ~31h |
| **v5.2.0** | Custom patches editor | 10.1–10.8 | ~18h |
| **v5.3.0** | Localization (EN/RU/ZH/PT-BR/ES) | 11.1–11.9 | ~17h |
| **v5.4.0** | Alternative clients tab | 13.1–13.3 | ~4h |
| **continuous** | Telemetry-free feedback | 12.1–12.7 | ~10h |

**Total tracked work:** ~180h (~22 focused dev days).

**Before-v1.0 decision**: Track 14 (rebrand) — resolve before Track 7 distribution push ships.

---

## Track 10 — Custom Patches Editor (v5.2)

For power users who want to author their own SpotX patches via `-CustomPatchesPath`.

### SpotX `patches.json` structure (reverse-engineered)

**Top-level keys:** `free` (adblock/premium), `others` (UI/branding/experiments), `VariousJs` (misc JS hooks).

**Patch object fields:** `version{fr,to}`, `match` (string or array), `replace` (string or array, parallel to match), `add`, `add2`, `name`, `description`, `native_description`, `value`, `svgtg`/`svggit`/`svgfaq` (inline SVG for `discriptions`). `DisableExp`/`EnableExp`/`CustomExp` are nested dicts of experiment toggles — no regex fields.

| # | Feature | Library | Effort |
|---|---|---|---|
| 10.1 | **AvalonEdit** JSON editor with Catppuccin-themed syntax highlighting via `HighlightingManager`. | icsharpcode/AvalonEdit | 3h |
| 10.2 | **Context-aware regex highlighting** — when cursor enters a `match` field, switch to a secondary AvalonEdit pane in regex mode. | AvalonEdit | 2h |
| 10.3 | **Parse stage** — `System.Text.Json.JsonDocument` with position-accurate errors, trailing-comma/duplicate-key detection. | Built-in | 1h |
| 10.4 | **Lint stage** — `JsonSchema.Net` (gregsdennis/json-everything) against embedded schema. Custom rules: backrefs don't exceed groups, parallel array lengths, ECMAScript regex compiles with 500ms `CancellationTokenSource` to guard catastrophic backtracking. | JsonSchema.Net | 4h |
| 10.5 | **Safety scan of `replace`** — flag `<script`, `javascript:`, `eval(`, `new Function(`, `fetch(` as high-severity. Red "trusted author" checkbox required to save. | Built-in regex | 1h |
| 10.6 | **Dry-run stage** — bundled neutered `xpui.js` sample (or user-supplied). Apply every enabled patch, report match count, byte delta, first 3 matched substrings. Zero matches → orange warning (pattern broke on current version). | Built-in | 4h |
| 10.7 | **Import from URL** — fetch remote `patches.json`, schema-validate, diff against official, stage for review. | Built-in HttpClient | 2h |
| 10.8 | **Embedded JSON schema resource** inferred from the full 48-patch set. | — | 1h |

**Total:** ~18h. Genuine differentiator — no competing tool offers in-app SpotX patch authoring.

---

## Track 11 — Localization & i18n (v5.3)

**Competitive gap:** SpotX is EN/RU only; Spicetify CLI is EN only; WinUtil is EN only. Shipping RU/ZH/PT-BR/ES day-one is a real moat.

### Stack

- **`WPFLocalizeExtension`** (v3.10+, active 2025) — `{lex:Loc KeyName}` XAML markup, runtime culture switching without restart.
- **`ResXManager`** (VS extension) — translator-friendly grid editor.
- **`Jeffijoe.MessageFormat`** — ICU MessageFormat pluralization (`.NET 8 lacks native`).
- **Crowdin OSS free tier** — GitHub sync, in-context editor, TM, glossary.
- **DeepL API Free** (500k chars/mo, best for RU/PT-BR/ES/DE/FR/JA) + **Azure Translator F0** (2M chars/mo, covers ZH-Hans/ZH-Hant) — one-shot MT prefill via `dotnet run --project tools/MTPrefill`.

### Key scheme
`<ControlType>_<Screen>_<Purpose>` + optional `_Format`/`_Tooltip`/`_Error_<Reason>` suffix. Example: `Button_Install_Tooltip`, `Message_Install_Error_NotFound`, `Label_CacheLimit_Format`.

### Scope
| # | Item | Effort |
|---|---|---|
| 11.1 | WPFLocalizeExtension + Strings.resx integration, wrap every hardcoded string. | 6h |
| 11.2 | MSBuild CI task: fail build if XAML contains raw `Content=""` / `Text=""` / `Title=""` without `{lex:Loc}` binding. | 2h |
| 11.3 | Jeffijoe.MessageFormat integration for plurals. | 2h |
| 11.4 | `tools/MTPrefill` script — DeepL + Azure fallback, writes `Strings.{lang}.resx` with `<!-- MT:DeepL -->` marker. | 3h |
| 11.5 | Crowdin project setup + GitHub sync action. | 2h |
| 11.6 | Auto-detect `CurrentUICulture` + dismissible 5-second "Language: English. Change?" banner on first run. | 1h |
| 11.7 | `-language xx-XX` passthrough to SpotX so UI and Spotify match by default. | 1h |
| 11.8 | Font fallback chain: `Segoe UI Variable, Segoe UI, Microsoft YaHei UI, Yu Gothic UI, Malgun Gothic`. | 0.25h |
| 11.9 | First-wave langs: **EN, RU, ZH-Hans, PT-BR, ES**. Second wave: DE, FR, JA, ZH-Hant. RTL (Arabic/Hebrew) deferred until post-audit. | — |

**Total:** ~17h + ongoing community translation.

---

## Track 12 — Telemetry-free feedback primitives (v4.x continuous)

Ship across versions; not a dedicated release.

| # | Feature | Effort | ROI |
|---|---|---|---|
| 12.1 | **In-app "Send Feedback" button** — builds URL `github.com/SysAdminDoc/LibreSpot/issues/new?template=bug.yml&title=...&body=<prefilled>` with OS, SpotX rev, Spicetify ver, active extensions. User reviews in browser, clicks Submit. Zero network from app. | 1h | ★★★★ |
| 12.2 | **Crash log → issue template** — after writing crash log to `%APPDATA%\LibreSpot\crashes\`, dialog offers "File report" button that opens issue template pre-populated with log path + drag-drop instructions. | 1h | ★★★ |
| 12.3 | **Release `download_count` dashboard** on GitHub Pages — static page fetches `/releases` API, renders Chart.js bar of version adoption. No user tracking. Drives deprecation decisions. | 2h | ★★ |
| 12.4 | **Pinned "Feature Request Voting" issue** with emoji reactions. Label `wishlist`. Monthly sort by reaction count; pin top 10 in README. | 0.5h + ongoing | ★★★ |
| 12.5 | **Annual "State of LibreSpot" survey** via GitHub Forms (Markdown issue forms, no JS). Link from About dialog. Publish results as repo markdown. No "State of Spicetify" exists — opportunity. | 3h/yr | ★★ |
| 12.6 | **Public `COMPARISON.md`** — LibreSpot vs BlockTheSpot-Installer, Spicetify Manager, ModifySpotify, SpotX-Spicetify-Universal-Installer. Empty cells = wishlist items community can PR. Doubles as SEO. | 3h | ★★★ |
| 12.7 | **GitHub Discussions Polls** — native since 2022, used by Zed/Tauri/Bun/Biome. Roadmap prioritization via polls instead of Canny/Fider. | 0.25h setup | ★★ |

---

## Track 13 — Alternative clients tab (v5.4, opt-in)

Strategic framing: **Spotify liberation toolkit**, not just a patcher. Adjacent audience (corporate/locked machines that can't patch official Spotify) without splitting engineering focus.

**Core stays:** patched official Spotify is the most capable free Spotify experience (Canvas, Connect, DJ, collab playlists — no alternative matches).

**Add:** secondary "Alternative Clients" tab with low-friction one-click installers.

| # | Client | Audio source | License | Effort |
|---|---|---|---|---|
| 13.1 | **Spotube** (KRTirtho/spotube, ~30k★) | YouTube/Piped/Invidious/JioSaavn (Spotify OAuth for metadata only) | BSD-4 | 1h (download+run MSIX) |
| 13.2 | **Psst** (Rust/Druid) | Real Spotify stream via librespot (Premium req) | MIT | 1h |
| 13.3 | **Ncspot** (Rust TUI) | librespot (Premium req) | BSD-3 | 1h |

**Skip:** Spotify-tui (archived 2022), Oto (low activity), YouTube Music Desktop (different ecosystem).

UI: card per client with capability matrix — Unlimited skips / 320kbps / Offline / Ads blocked / Connect / Canvas / Lyrics / DJ / Collab playlists. Users see at a glance why patched official Spotify still wins, but have an escape hatch.

**Total effort:** ~4h.

---

## Track 14 — Rebrand evaluation (decision needed, not action)

**Flag only — not scheduling work.**

**Problem:** "LibreSpot" collides with `librespot-org/librespot` — the Rust library for unofficial Spotify clients. Search confusion, false association (users expect our tool to be a librespot-based client, not a patcher). Downstream projects (Psst, Spot, ncspot) always clarify "built on librespot" to disambiguate.

**Options to consider:**
1. **Keep name + tagline** — add "SpotX + Spicetify installer for Windows" under every use. Zero-cost but SEO drag persists.
2. **Rebrand before v1.0 stable** — candidates: **SpotForge**, **UnSpot**, **Spotify Liberator**, **Patchify**, **Spotify Toolkit**. Cost: GitHub repo rename (auto-redirects), release asset name change, winget/scoop/choco manifest resubmission, icon re-render, all existing clone URLs break, existing users confused during transition.
3. **Hybrid** — rename product but keep GitHub repo as `LibreSpot` for URL stability. Confusing.

**Recommendation:** decide **before** Track 7 distribution push. Once we're on winget/scoop/choco under one name, rebrand cost multiplies. Flagged for user decision.

---

## Residual research backlog (not yet evaluated)

- **Spotify Linux/macOS patching** — Avalonia/MAUI rewrite. Current PowerShell backend is Windows-locked.
- **Anti-forensics / tamper resistance** — detect and warn when antivirus has quarantined a Spicetify extension file.
- **Spotify Connect test harness** — verify patches don't break Connect / cast-to-device after apply.
- **Linux sister project** — `LibreSpot-Linux` using spicetify + spotx-bash. Separate repo, shared preset format.
- **Mobile companion app** — QR-scan a desktop preset. Would need relay server — conflicts with zero-infra principle.

---

*Last updated: 2026-04-17 (consolidated from 8 research passes — SpotX upstream, Spicetify ecosystem, competitors, WPF UX, distribution, Windows 11 shell, power-user CLI, community patterns, custom patches internals, localization, feedback primitives, alternative clients)*
