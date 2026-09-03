# How SpotX and Spicetify alter Spotify

Reference for maintainers and for any automated run that needs to reason about what LibreSpot installs. Last verified 2026-09-01 against SpotX commit `550bc72c` (the pinned adapter, read from the local asset cache) and SpotX `main` (version 1.2.99, 2026-08-31), Spicetify CLI `v2.44.0` source, Spicetify `v3-beta` source, Spicetify Marketplace 1.0.11 source, BlockTheSpot and BlockTheSpot-Resilient source, and a live Spotify 1.2.93.667 install patched by both tools. Where a claim was not verified in code it says so.

Sections:

1. The client being patched
2. SpotX: native binary patches
3. SpotX: web bundle patches
4. SpotX: switches, version flow, Defender
5. Spicetify: backup and preprocess
6. Spicetify: apply and injection
7. Themes: format and mechanics
8. Extensions, custom apps, and the API surface
9. Experimental feature flags
10. Spicetify v3
11. Marketplace
12. BlockTheSpot, spotify-adblock, Windhawk
13. How the tools coexist and collide, and what LibreSpot does about it
14. Spotify countermeasures and breakage history
15. Policy, enforcement, and the safe design space
16. Verification signals LibreSpot relies on
17. Sources

## 1. The client being patched

Spotify for Windows 1.2.9x is a native shell hosting a web app. In `%APPDATA%\Spotify\` you find `Spotify.exe` (launcher), `Spotify.dll` (the core since about 1.2.70), `libcef.dll` and `chrome_elf.dll` (Chromium Embedded Framework), `v8_context_snapshot.bin`, `Apps\xpui.spa`, `Apps\login.spa`, and `prefs`. Per-user state, `offline.bnk`, the update folder, and storage live under `%LOCALAPPDATA%\Spotify\`.

`xpui.spa` is a plain ZIP. It contains `index.html`, the main bundle (`xpui.js` on older builds, `xpui-snapshot.js` plus `xpui-snapshot.css` on newer ones), route chunks such as `xpui-routes-*.js`, `home-v2.js`, `xpui-desktop-modals.js`, `xpui-routes-desktop-settings.js`, the `i18n/*.json` strings, and the debug window assets. There is no integrity check on this archive. Both patchers open it with `System.IO.Compression` (SpotX) or Go's zip reader (Spicetify), rewrite entries, and the client loads whatever is there.

Since roughly 1.2.64 Spotify bakes the webpack module table into `v8_context_snapshot.bin` and ships only `xpui-snapshot.js` as the entry. Both patchers read that binary as UTF-16LE, find the text between `var __webpack_modules__={` and `//# sourceMappingURL=xpui-modules.js.map`, and write it back out as JavaScript. SpotX prepends it to the snapshot entry and renames the pair to `xpui.js` and `xpui.css` (`Extract-WebpackModules` and `Update-ZipEntry` in `run.ps1`), rewriting `index.html` to match. Spicetify writes it as a separate `xpui-modules.js` and inserts a script tag for it before `xpui-snapshot.js`. That difference is the root of the store-route collision described in section 13.

The `.sig` files next to the binaries (`Spotify.exe.sig`, `libcef.dll.sig`, `Spotify.dll.sig`) are Widevine VMP signatures used for DRM playback, not anti-tamper for the app. Patching `Spotify.exe` or `libcef.dll` breaks DRM, so both patcher families leave those two alone and target `Spotify.dll` plus the ZIP.

Useful `prefs` keys (plain `key=value`, strings quoted): `app.autostart-mode`, `app.enable-developer-mode`, `app.last-launched-version` (Spicetify reads the Spotify version from this), `app.browser.zoom-level`, `app.player.volume`, `ui.hide_hpto`, `ui.system_media_controls_enabled`, `ui.minimize_to_tray`, `ui.show_friend_feed`, `audio.crossfade_v2`, `audio.normalize_v2`, `audio.silence_trimmer_v2`, `audio.play_bitrate_enumeration`, `storage.size`, `network.proxy.mode`. Developer mode is not a prefs key; it is a product-state byte in `offline.bnk` (`app-developer`), which is why Spicetify's `always_enable_devtools` patches that file and SpotX's `-devtools` instead calls `productStateApi.putOverridesValues`.

Launch flags that matter: `--remote-debugging-port=N` (needs `--remote-allow-origins` on 1.2.8+), `--show-console`, `--log-file`, `--mu=` (parallel clients), `--disable-update-restarts`, `--update-endpoint-override=`, `--transparent-window-controls`, `--minimized`, `--enable-features=`.

## 2. SpotX: native binary patches

SpotX runs entirely offline against files on disk. For builds at or above 1.2.70.253 the target is `Spotify.dll`; below that it is `Spotify.exe` (`run.ps1` around line 3325). Backups are written before patching: `Spotify.bak`, `Spotify.dll.bak`, `chrome_elf.dll.bak` beside the binaries, and `Apps\xpui.bak` for the bundle. The presence of those `.bak` files is the durable evidence that SpotX ran (section 16).

### 2.1 String patches

`extract -counts 'exe' -helper 'Binary'` reads the whole binary as code page 1251 text, applies the `patches.json > others.binary` table with `-replace`, and writes it back. The table at the pinned commit:

| Key | Match (regex) | Replace | Purpose |
|---|---|---|---|
| `block_update` | `(?<=desktop-update/.)7(/update)` on the `add` side; the live string is `desktop-update/v2/update` | `7/update` | Rewrites the update path to a version that does not exist, so the update check fails. |
| `block_slots` | `slot}(?=.{3,8}(override_url\|queued_ads))` | `slot}` | Corrupts the `slots` token near the ad-slot request so the client never resolves ad slots. |
| `block_slots_2` | `slot}(?=.{25,35}state)` | `slot}` | Second ad-slot site. Skipped with `-premium`. |
| `block_slots_3` | `}(?=payload=)` | `}` | Turns `?payload=` into `}payload=` so the ad telemetry query is malformed. Skipped with `-premium`. |
| `block_gabo` | `dodo(?=.receiver-service)` | `dodo` | Turns `gabo-receiver-service` into `dodo-receiver-service`, killing the event pipeline. Builds up to 1.2.73. |
| `block_gabo2` | `dodo(?=.receiver-service(?:/public\|[^/]))` | `dodo` | Same for 1.2.74 and later where the path changed. |

The `add` values above are what the script checks for to decide whether the binary is already patched (`$old -notmatch ...` around line 3598); the actual replacements come from the `match`/`replace` pairs. On the live 1.2.93 machine the byte diff between `Spotify.dll` and `Spotify.dll.bak` shows exactly these: `gabo`->`dodo` at `0x19e8dd8`, `?payload=`->`}payload=` at `0x1a05898`, `slots`->`slot}` at `0x1a05b14` and `0x24c0d6c`, and `v2/update`->`v7/update` at `0x18a2380`.

### 2.2 Byte patches

Three real code patches exist, all against `Spotify.dll` on x64 (ARM64 variants exist for the first):

- **Signature check stub** (`Reset-Dll-Sign`). Finds the ASCII string `Check failed: sep_pos != std::wstring::npos.`, walks the `.text` section for a `lea rdx,[rip+disp]` (`48 8D 15`) that resolves to that string, walks back to the function prologue, and overwrites it with `B8 01 00 00 00 C3` (`mov eax,1; ret`). ARM64 uses `20 00 80 52 C0 03 5F D6`. This neutralises the in-DLL module verification that Spotify turned on at 1.2.70. On the live machine this landed at `0x5e5b0`.
- **Certificate table zeroing** (`Remove-Sign`, `Remove-Signature-FromFiles`). Parses the PE header and zeroes the 8-byte Security data-directory entry (index 4) in `Spotify.dll`, `Spotify.exe`, and `chrome_elf.dll`, so no stale Authenticode signature remains to fail. On the live machine that is the 4-byte diff at `0x121` in `Spotify.exe` and `chrome_elf.dll` and at `0x1e9` in `Spotify.dll`.
- **Crossfade** (`Set-CrossfadeEnabledBinaryPatch`, 1.2.89+ free accounts). Finds the `crossfade_enabled` string, its RIP-relative reference, the enclosing function via `.pdata`, then the single `call` site that consumes it, and writes `B0 01 90 90 90` (`mov al,1; nop nop nop`). On the live machine: `0x4daeb3`.

SpotX `main` (not the pinned commit) adds a fourth for 1.2.94+: `Set-BlockSlotsBinaryPatch` locates `slot_is_disabled`, resolves its enum value from the mapper function's compare sequence, finds the predicate through `.pdata`, verifies the prologue and branch shape, and flips the guard with a 3-byte `EB xx 90`. It exists because the `slots` string trick stopped working at 1.2.94.

The pinned script also contains an `Initialize-BinaryScanner` C# helper (`FindBytes`, `FindRipRefs`, `FindCallsToRva`, `FindFunctionRange`, ARM64 `FindXrefArm64`) that the byte patches use. Order of operations in the script: back up the binaries, `Reset-Dll-Sign`, strip signatures, string patches, crossfade patch, then bundle patches.

### 2.3 Other native-side changes

- Blocks updates a second way by removing Deny ACLs on `%LOCALAPPDATA%\Spotify\Update` only when asked to unblock (`Unlock-Folder`), and, historically, by making that folder a read-only file. LibreSpot's `Unlock-SpotifyUpdateFolder.ps1` mirrors that action.
- `-DisableStartup` deletes the `Spotify` Run key and writes `app.autostart-mode="off"` into `prefs`.
- Uninstalls the Microsoft Store package `SpotifyAB.SpotifyMusic` because the Store sandbox cannot be patched.
- Creates desktop and Start Menu shortcuts unless `-no_shortcut`.

## 3. SpotX: web bundle patches

All bundle work goes through `extract -counts one|more -method zip -name <entry> -helper <table>` which reads a ZIP entry, runs the named `patches.json` table against it, and writes it back. A `Helper` invocation walks a JSON object: each entry has `version` (`fr`/`to` gates checked by `Test-PatchVersionMatch`), `match` (one regex or an array), `replace` (same shape), and optionally `add` (text appended to the file) or `disable`. Missing matches print "Didn't find variable" but do not abort.

### 3.1 The tables at the pinned commit

`free` (only without `-premium`, applied to `xpui.js`): `fullscreen`, `audioads`, `emptyblock`, `playlistsponsor`, `connectold`, `downloadquality`. What they do: enable fullscreen mode for free accounts, rewrite the audio-ad path so the client fast-forwards past audio ads, set `adsEnabled:!0` to `!1`, strip playlist sponsorship blocks, and hide download-quality UI.

`VariousJs` (applied to `xpui.js`): `product_state` (injects `putOverridesValues({pairs:{ads:'0',catalogue:'premium',product:'premium',type:'premium',name:'Spotify',unrestricted:'1'}})` unless `-premium`, plus `storage-size-config` when `-cache_limit`), `dev-tools`, `banner_home` (forces `isPremium` and `isHptoHidden` true around `ADS_PREMIUM`), `sentry` (`864e5<30` to `<0`), `disablelog` (`sp://logging/v3/...`), `hidemerchsidebar`, `offrujs`, `goofyhistory`, `similarplaylist`, `sidebar_fix`, `filtertags_locale_fix`, `lyrics-old-on`, `lyrics-block`, `fixTitlebarHeight`, `mock` (rewrites leavebehind and sponsored-playlist endpoints to `/localhost/`), `upgradeButton` (media query set to a width that never matches), `upgradeMenu`, `hideEmptyYourEpisodes`, `GenreHubHashFix`.

`others`: `discriptions` (adds SpotX links to the About dialog in `xpui-desktop-modals.js`), `ForcedExp` (section 9), `DisableExp`, `EnableExp`, `CustomExp`, `binary` (section 2), `themelyrics` (24 static lyric colour themes), `collaboration` (hides the collaborators icon in `xpui-routes-playlist.js`), `byspotx` (appends the `// Patched by SpotX` marker), `disablesentry`, `cssmin`, `htmlmin`, `blankmin`, `minjs`, `minjson`, `block_subfeeds`, `downloadquality`, `downloadicon`, `submenudownload`, `veryhighstream` (CSS `add` blocks that hide download and quality surfaces for free accounts), `fix-scrollbar`, `fix-old-theme`, `searchFixes`, `fixHomeV2EmptyResponseCheck`.

### 3.2 Injected helper files

`injection -p <spa> -f spotx-helper -n <files> -c <contents>` writes files into a `spotx-helper/` folder inside the ZIP and inserts `<script defer>` or `<link>` tags into `index.html`. Files:

- `sectionBlock.js` (with `-podcasts_off`, `-adsections_off`, or `-canvashome_off`). Monkey-patches `window.fetch`, intercepts responses from `api-partner.spotify.com/pathfinder` and `api.spotify.com/v1/views/personalized-recommendations`, and splices out home sections by a hard-coded list of section IDs (Party, Chill, Charts, Workout, and so on) plus any item whose `__typename` is Podcast, Audiobook, or Episode. This is how "hide podcasts" and "hide ad-like sections" work: response filtering, not CSS.
- `checkVersion.js` (unless `-sendversion_off`). Reports newly seen Spotify versions to a Cloudflare worker.
- `goofyHistory.js` (opt-in). Scrobbles to a Google Form.
- `lyrics-color/rules.css` and `colors.css` (with `-lyrics_stat <theme>`). Static lyric colours, with `{{past}}`, `{{current}}`, `{{next}}`, `{{hover}}`, `{{background}}`, `{{musixmatch}}` substituted from `themelyrics`.

On the live machine `spotx-helper/` holds `sectionBlock.js` plus the two lyric CSS files, and `index.html` references `/spotx-helper/lyrics-color/rules.css` in `<head>` and `/spotx-helper/sectionBlock.js` before `/xpui.js`.

### 3.3 Everything else the bundle pass does

Minifies all `*.js`, `*.css`, `*.json`, `ui-licenses.html` (or `licenses.html` before 1.2.93), and `blank.html`; removes RTL rules; with `-ru` deletes every locale except `en` and `ru` from `i18n/` and `locales/`; for 1.1.87 to 1.2.5 re-downloads a known-good `login.spa`. The `xpui.css` tail gets the `add` blocks listed above. The `// Patched by SpotX` marker at the end of `xpui.js` is what SpotX itself checks on a re-run: if present and `xpui.bak` exists it restores the backup first, and if `xpui.bak` is missing it refuses ("Backup copy not found, reinstall Spotify").

## 4. SpotX: switches, version flow, Defender

Switches LibreSpot maps in `Build-SpotXParams.ps1`: `-new_theme`, `-podcasts_off`/`-podcasts_on`, `-adsections_off`, `-block_update_on`/`-block_update_off`, `-premium`, `-DisableStartup`, `-no_shortcut`, `-start_spoti`, `-lyrics_stat <theme>` with `-lyrics_block` or `-old_lyrics`, `-topsearchbar`, `-rightsidebar_off`, `-rightsidebarcolor`, `-canvashome_off`, `-homesub_off`, `-hide_col_icon_off`, `-plus`, `-newFullscreenMode`, `-funnyprogressBar`, `-exp_spotify`, `-sendversion_off`, `-devtools`, `-mirror`, `-confirm_spoti_recomended_uninstall`, `-download_method`, `-version`, `-cache_limit` (clamped 500 to 20000), `-language`. LibreSpot always passes `-confirm_uninstall_ms_spoti` and `-confirm_spoti_recomended_over` so the script never blocks on stdin, and passes `-CustomPatchesPath` when the user supplied custom patches. Upstream also has `-cache`, `-urlform_goofy`/`-idbox_goofy`, `-no_pause`, and `-defender_exclusions_off`.

`-premium` does not grant Premium access. It removes the ad-related patches (`block_slots_2`, `block_slots_3`, `mock`, `upgradeButton`, `upgradeMenu`, the `product_state` fake, the free-tier CSS) because a paying account does not need them.

Version flow: the script pins `$latest_full` (1.2.99 on `main`), resolves a short version to a full `x.y.z.b.g<hash>` build through `raw.githubusercontent.com/LoaderSpot/table/main/table/versions.json`, and downloads through a Cloudflare Worker (`loadspot.amd64fox1.workers.dev/download/spotify_installer-<ver>-<arch>.exe`) rather than straight from `upgrade.scdn.co`, with curl first and WebClient as fallback. Installers accept `/silent /skip-app-launch` and `/extract <dir>`. It compares the installed FileVersion with the target and offers to install over or uninstall and reinstall. Versions below 1.1.59 and above the tested ceiling are refused unless overridden.

Defender: upstream SpotX after commit `afb4c3fc` adds Defender exclusions for the Spotify folders and its own process by default because the patcher trips HackTool heuristics. LibreSpot's pinned commit predates that boundary and must not receive `-defender_exclusions_off`; `Test-SpotXPinAdvanceSecurityPolicy.ps1` enforces that a post-boundary candidate declares the flag and that the pre-boundary pin does not. Any pin advance runs `Build-Scripts.ps1 -SpotXSecurityPolicy` first.

## 5. Spicetify: backup and preprocess

`spicetify backup` (`src/cmd/backup.go`, `src/backup/backup.go`) copies every `*.spa` from `Apps` into `%APPDATA%\spicetify\Backup`, unzips `xpui.spa` and `login.spa` into `Extracted/Raw/<app>`, runs `preprocess.Start()`, copies `Raw` to `Extracted/Themed` (only `.html`, `.js`, `.css`), runs the colour-replacement pass on `Themed`, and writes `[Backup] version=<spotify version>` and `with=<cli version>` into `config-xpui.ini`. The Spotify version comes from `prefs` `app.last-launched-version`. If that field is blank every later `apply` reports "version mismatched" and `backup` refuses ("cannot be backed up at this state") because the bundle is already extracted; writing the prefs value back fixes it.

`preprocess.Start()` (`src/preprocess/preprocess.go`):

1. Fetches `css-map.json` from `raw.githubusercontent.com/spicetify/cli/<tag>/css-map.json` (falls back to the local copy next to the binary). It is a flat map from hashed class names to semantic ones, about 150 KB.
2. Validates the build with `(Master|Release|PR|Local) Build...` against the binary and refuses non-Release builds.
3. Extracts `xpui-modules.js` from the V8 snapshot as described in section 1.
4. Walks every `.js`, `.css`, `.html` under `xpui` and applies, by file type:

`.js`: `disableSentry` on `xpui.js`/`xpui-snapshot.js` (`/864e5<30` becomes `<0`); `disableLogging` on all JS (removes `sp://logging/v3/\w+` and `[^"\/]+\/[^"\/]+\/(public\/)?v3\/events`, then injects early returns into `registerEventListeners`, `logImpression`, `logNonAuthImpression`, `logNavigation`, `handleBackgroundStates`, `createLoggingParams`, `initSendingEvents`, `flush`, `addItemInEventsStorage`, `addEventsToESSData`, `sendEvents`, `storeEvent`, and makes `logInteraction` return `{interactionId:null,pageInstanceId:null}` and `lastFlush` return a resolved promise); `exposeAPIs_main` and `exposeAPIs_vendor` by file (section 8); `additionalPatches` (captures GraphQL persisted-query definitions into `Spicetify.GraphQL.Definitions`); a fix for `e.state.cinemaState` in the `dwp-*` bars; the css-map replacer (`key:` to `"value":`, then bare keys); and `colorVariableReplaceForJS` (`"#1db954"`, `"#b3b3b3"`, `"#ffffff"` become `getComputedStyle` lookups of `--spice-button`, `--spice-subtext`, `--spice-text`).

`.css`: css-map, `removeRTL` (a set of `[dir=rtl]` and `[lang=ar]` strip rules), and on `xpui.css`/`xpui-snapshot.css` an appended block of legacy card rules. In `Themed` only, `colorVariableReplace` maps Spotify's literal colours to variables: `#181818|#212121` to `--spice-player`, `#282828` to `--spice-card`, `#242424|#1f1f1f` to `--spice-main-elevated`, `#121212` to `--spice-main`, `#1a1a1a` to `--spice-highlight`, `#2a2a2a` to `--spice-highlight-elevated`, `#000|#000000` to `--spice-sidebar`, `white|#fff|#ffffff|#f8f8f8` to `--spice-text`, `#b3b3b3|#a7a7a7` to `--spice-subtext`, `#1db954|#1877f2` to `--spice-button`, `#1ed760|#1fdf64|#169c46` to `--spice-button-active`, `#535353` to `--spice-button-disabled`, `#333|#333333` to `--spice-tab-active`, `#7f7f7f` to `--spice-misc`, `#4687d6|#2e77d0` to `--spice-notification`, `#e22134|#cd1a2b` to `--spice-notification-error`, and the `rgba(18,18,18,x)`, `rgba(40,40,40,x)`, `rgba(0,0,0,x)`, `hsla(0,0%,100%,x)` forms to the matching `--spice-rgb-*` variables. `pip-mini-player*.css` is skipped, which is why the PiP window never picks up theme colours (cli issue #2836).

`.html`: after `<body>` inserts `<link rel='stylesheet' class='userCSS' href='colors.css'>`, the same for `user.css`, and, when `expose_apis` is on, `<script src='helper/spicetifyWrapper.js'></script>` followed by the `<!-- spicetify helpers -->` marker.

## 6. Spicetify: apply and injection

`spicetify apply` (`src/cmd/apply.go`, `src/apply/apply.go`) refuses if `[Backup] with` does not equal the CLI version, then copies `Raw` (or `Themed` when `replace_colors=1`) over `Apps\xpui`, and runs:

- `RefreshTheme`: writes `xpui/colors.css` as `:root { --spice-<k>: #hex; ... --spice-rgb-<k>: r,g,b; }` with missing keys filled from the default list, `xpui/user.css`, `xpui/spicetify-config.json` (`theme_name`, `scheme_name`, `schemes`), copies `theme.js` to `xpui/extensions/theme.js` when `inject_theme_js=1`, and copies the theme's `assets/` over `xpui/` when `overwrite_assets=1`.
- Copies `spicetifyWrapper.js` into `xpui/helper/`.
- `AdditionalOptions` on `index.html`: rewrites the snapshot script tag to load `/xpui-modules.js` then `/xpui-snapshot.js`; after the helpers marker inserts `helper/sidebarConfig.js`, `helper/homeConfig.js`, `helper/expFeatures.js` (all `defer`) and an inline `Spicetify.Config={version,current_theme,color_scheme,extensions,custom_apps,check_spicetify_update}`; before `</body>` inserts `extensions/theme.js`, each `extensions/<ext>.js` (`type='module'` for `.mjs`), and each custom app's `subfiles_extension`.
- On `xpui.js` and `xpui-modules.js`: `insertExpFeatures` wraps the remote-config resolver so `Spicetify.expFeatureOverride` sees every flag (`(function \w+\((\w+)\)\{)(\w+ \w+=\w\.name;if\("internal")` gains `$2=Spicetify.expFeatureOverride($2);`) and captures the resolver as `Spicetify.RemoteConfigResolver`; `insertSidebarConfig`; `insertHomeConfig` (also `home-v2.js`), which routes the home section list through `SpicetifyHomeConfig.arrange`.
- On `xpui-desktop-modals.js`: adds a Spicetify details block to the About dialog.
- Custom apps: `findCustomAppTarget` tries `xpui-modules.js`, `xpui-snapshot.js`, then `xpui.js`, and needs both a `React.lazy` loader match and a route-table match (`{path:"/settings...` on 1.2.78+ or `{path:"/collection"` earlier). It then emits `spicetifyApp<i>=R.lazy(()=>W.e("spicetify-routes-<app>").then(W.bind(W,"spicetify-routes-<app>")))`, a `Route` with `path:"/<app>/*"`, an entry in the chunk-name map at `{(\d+:"xpui)`, a `,"spicetify-routes-<app>":1` in the `miniCss` enable map, and calls `insertNavLink` with version-specific patterns for the Library X sidebar and the global nav bar (`>=1.2.87` uses a `(?s)` pattern around `"global-nav-bar"`).
- `RefreshApps` writes `xpui/spicetify-routes-<app>.js` as a webpack (or rspack on 1.2.93+) chunk push wrapping the app's `index.js` plus subfiles, `spicetify-routes-<app>.css`, and `spicetify-routes-<app>.json` (the manifest); assets go to `xpui/assets/<app>/`.
- `[Patch]` section: `<file>_find_<n>` / `<file>_repl_<n>` (first match) or `<file>_repl_all_<n>` Go regexes applied to `xpui/<file>` last. This is the CLI's own custom-patch hook.
- `.mjs` extensions support `// spicetify_map{A}{B}` rewrite comments; `Extensions/node_modules` is junctioned into `xpui/extensions/node_modules`.

Runtime helpers: `expFeatures.js` (section 9), `homeConfig.js` (sticks or lowers home sections via `localStorage` keys `spicetify-home-config:stick` and `:low`), `sidebarConfig.js` (reorders `.main-yourLibraryX-navItems`; refuses under the global nav bar with a snackbar).

`spicetify restore` copies the `Backup/*.spa` files back into `Apps` and deletes the extracted folders. `spicetify watch` re-applies on file change.

## 7. Themes: format and mechanics

A theme is a folder under the Spicetify `Themes` directory with `color.ini` (required), `user.css` (required), optional `theme.js`, and optional `assets/`.

`color.ini` is loaded case-insensitively. Each `[Section]` is a colour scheme; `color_scheme` empty selects the first. Keys and the regions they control, with Spotify's defaults: `text` (main text; ffffff), `subtext` (secondary text; b3b3b3), `main` (page background; 121212), `main-elevated` (raised surfaces; 242424), `highlight` (hover; 1a1a1a), `highlight-elevated` (2a2a2a), `sidebar` (000000), `player` (now-playing bar; 181818), `card` (282828), `shadow` (000000), `selected-row` (ffffff), `button` (accent; 1db954), `button-active` (1ed760), `button-disabled` (535353), `tab-active` (333333), `notification` (4687d6), `notification-error` (e22134), `misc` (7f7f7f). Values accept hex (short form allowed), `r,g,b`, `${ENV}`, and `${xrdb:name[:fallback]}` on Linux. Any extra key becomes `--spice-<key>` and `--spice-rgb-<key>` too, which is how a theme can add its own variables.

Config keys that govern theming (`[Setting]` in `config-xpui.ini`): `current_theme`, `color_scheme`, `inject_css`, `replace_colors` (use `Themed`, that is, Spotify's literal colours swapped for variables), `overwrite_assets`, `inject_theme_js`, `spotify_launch_flags`, `check_spicetify_update`, `always_enable_devtools`. `[Preprocesses]`: `disable_sentry`, `disable_ui_logging`, `remove_rtl_rule`, `expose_apis`. `[AdditionalOptions]`: `extensions`, `custom_apps`, `sidebar_config`, `home_config`, `experimental_features`.

What themes target. Spotify ships hashed CSS-module class names. The css-map rewrites them to semantic names at apply time (`n8Bz0c0v17whD3KfMdOk` becomes `album-albumPage-sectionWrapper`), so themes write against `.Root__main-view`, `.Root__nav-bar`, `.Root__now-playing-bar`, `.Root__right-sidebar`, `.Root__globalNav`, `.Root__top-container`, `.main-nowPlayingBar-*`, `.main-yourLibraryX-*`, `.main-globalNav-*`, `.main-topBar-*`, `.main-trackList-*`, `.main-entityHeader-*`, `.main-nowPlayingView-*`. Spotify's own design tokens (`--background-base`, `--background-elevated-*`, `--background-tinted-*`, `--text-base`, `--text-subdued`, `--essential-*`, `--encore-*`; about 208 of them on 1.2.93) are used sparingly because Spotify recomputes them per view. The live `xpui.css` references 36 `--spice-*` variables after preprocessing, `--spice-text` alone 679 times.

Why themes break: when Spotify renames or re-hashes classes the css-map lags. Spotify 1.2.86 shortened hashed names from 20 to 16 characters and every theme broke until Spicetify shipped new maps across 2.43.0 to 2.44.0.

Dynamic colour: three generations exist in the ecosystem. Vibrant.js bundled as an extension (JulienMaille), raw canvas sampling of the cover (Hazy, Bloom), and `Spicetify.colorExtractor(uri)`, which calls Spotify's own `colorextractor/v1/extract-presets` service and returns named presets (`VIBRANT`, `LIGHT_VIBRANT`, `DARK_VIBRANT`, `DESATURATED`, `PROMINENT`, `VIBRANT_NON_ALARMING`; `PROMINENT` has been unreliable since cli issue #3120). Marketplace uses it for "change colours based on album art".

Forced dark mode: Spotify launches CEF with `--force-dark-mode`, so `prefers-color-scheme` and `matchMedia` always report dark inside the client. Any "follow the OS" theme needs a binary patch or an external helper (the Windhawk CEF mod exposes a switch). A time-scheduled switch inside `theme.js` needs neither; that is what the bundled Prism theme in `resources/themes/Prism` does.

Window chrome and transparency are outside CSS reach. Real transparency, Mica, or a native title bar needs Ingan121's Windhawk "CEF/Spotify Tweaks" mod (patches CEF vtable entries per CEF version and exposes a JS bridge), which is what WMPotify builds on.

## 8. Extensions, custom apps, and the API surface

`expose_apis` makes Spicetify's wrapper (`jsHelper/spicetifyWrapper/*`, bundled to `helper/spicetifyWrapper.js`) steal `__webpack_require__` by pushing a fake chunk into `webpackChunkclient_web` or `rspackChunkclient_web`, inventory the module table, and build the `Spicetify` global. Surface: `Player`, `addToQueue`, `removeFromQueue`, `Queue`, `CosmosAsync`, `getAudioData` (deprecated; the analysis endpoint was shut off 2024-11-27), `Keyboard`, `URI`, `LocalStorage`, `showNotification`, `Menu`, `ContextMenu`, `ContextMenuV2`, `React`, `ReactDOM`, `ReactDOMServer`, `ReactJSX`, `ReactComponent` (menus, tooltips, buttons, `Slider`, `ConfirmDialog`, `Navigation`, `ScrollableContainer`, `PlatformProvider`, and more), `ReactHook`, `Mousetrap`, `Locale`, `Topbar`, `Playbar`, `Panel`, `PopupModal`, `SVGIcons`, `colorExtractor`, `extractColorPreset`, `Platform`, `_platform`, `Config`, `expFeatureOverride`, `createInternalMap`, `RemoteConfigResolver`, `Tippy`, `GraphQL`, `AppTitle`, `Snackbar`, `Events`, `_renderNavLinks`, `_getStyledClassName`.

`Spicetify.Platform` is built by calling every `get*()` on the exposed platform object and, on 1.2.38+, resolving every symbol ending in `API` from `Platform.Registry._map`. The live 1.2.93 bundle carries roughly ninety of these, including `PlayerAPI`, `PlaybackAPI`, `LibraryAPI`, `PlaylistAPI`, `RootlistAPI`, `LocalFilesAPI`, `LocalStorageAPI`, `ClipboardAPI`, `EqualizerAPI`, `ExclusiveModeAPI`, `ForceVolumeAPI`, `ConnectAPI`, `ShowAPI`, `UserAPI`, `SettingsAPI`, `RemoteConfigDebugAPI`, `PlayHistoryAPI`, `RecentlyPlayedAPI`, `OfflineAPI`, `VideoAPI`, `MidiAPI`, `IndexedDbAPI`, `UpdateAPI`, `ZoomAPI`. `CosmosAsync` proxies `sp://` and `wg://` internally, adds a bearer token for `api.spotify.com` and `spclient` hosts, and sends anything else through a CORS proxy. Known internal endpoints: `sp://desktop/v1/version`, `sp://desktop/v1/restart`, `sp://core-playlist/v1/rootlist`, `sp://player/v2/main/skip_next`, `sp://scrobble/v1/incognito`.

What the sandbox cannot do: no raw audio (playback is native; JS never sees PCM), no file system or process access (CEF without Node; `file://` blocked), no Web Serial, HID, or Bluetooth. WebGL2, Gamepad, `localStorage`, and IndexedDB work. Anything that changes what the server streams (ad slots, quality tiers, Jam, Enhance) is enforced server-side and cannot be unlocked from JS; adblockify and SpotX both say so.

Custom apps: a folder with `manifest.json` (`name`, `icon`, `active-icon`, `subfiles`, `subfiles_extension`, `assets`) and an `index.js` defining `render()` returning a React element. The route is `/<folder-name>/*`. Navigation entries are rendered by `_renderNavLinks` as either a Library X sidebar item or a global-nav `ButtonTertiary` (capped by `--max-custom-navlink-count: 4`); if the required React components are not found the link renders nothing, which is the classic "Marketplace not in the sidebar" symptom. LibreSpot ships `Install-MarketplaceNavFallbackExtension.ps1` and the `librespot-marketplace-button.js` extension for that case.

### LibreSpot's live engine

LibreSpot 4.2.0 installs `CustomApps\librespot` plus the `librespot-engine.js` companion through the same reviewed custom-app path used by the other catalog apps. The custom app supplies the six-panel `/librespot/*` surface. The companion runs on every route, owns the live state in Spicetify local storage, and adds a guarded top-bar entry if Spotify does not render the normal custom-app navigation link.

Palette changes never call the CLI. `ManagedRuntimeStyles` parses `color.ini`, derives missing Spicetify keys, writes `--spice-*` and `--spice-rgb-*` variables into one managed `:root.librespot-layer-palette` rule, and removes stale values before the next write. The extra root class gives the managed rule enough specificity to win when `colors.css` and `user.css` load later. Layout, effects, palette, and accessibility stay independent through classes on `<html>`. Glass uses blur only in its explicit tier, eco keeps translucent surfaces without blur, and flat removes blur and transitions. Reduced-motion state forces the flat behavior.

The engine applies client flags through `Spicetify.expFeatureOverride` and `Platform.RemoteConfigDebugAPI`, with the resolver fallback used by the pinned wrapper. Snippets live in a separate managed style element. Dynamic color calls `Spicetify.colorExtractor` on song changes, while the Material option uses the vendored Lucid color path. Time schedules, frame-rate step-down, and optional desktop signals all update the same state object. Theme-folder export writes standard `color.ini`, `user.css`, and `theme.js`; profile export writes the same `.librespot` fields the desktop importer already understands.

The self-test runs after load and navigation. Required anchors are the main view, navigation, playbar, and page scroll container. Each has a stable-root selector plus a `data-testid`, ARIA, or scrollbar fallback. The right sidebar is optional because Spotify can close it. Separate checks cover `/librespot`, `/marketplace`, and the Spotify 1.2.93 pin. A missing required anchor or installed route is named in Health, and a missing route carries the existing `repair-custom-app-routes` action. An absent optional sidebar stays quiet.

## 9. Experimental feature flags

Spotify gates features behind remote-config properties named like `enableEqualizer`. The pinned 1.2.93 bundle declares 348 of them with Spotify's own description strings (for example `enableSleepTimer: Enable Sleep timer`, `enablePiPMiniPlayer: Enable the PiP Mini Player`, `enableFullscreenMode`, `enableTracklistColumnsSorting`, `enableHomePin`, `enableRightSidebarLyrics`, `enableAmbientModeTimer`, `enableTiltable3DArtwork`, `enableSnake`). Two override mechanisms exist:

- **Spicetify**: `insertExpFeatures` routes every declared flag through `Spicetify.expFeatureOverride`; `expFeatures.js` persists user choices in `localStorage["spicetify-exp-features"]`, pushes them through `Platform.RemoteConfigDebugAPI.setOverride` (or `RemoteConfigResolver.setOverrides`), and adds an "Experimental features" entry to the profile menu.
- **SpotX**: the `ForcedExp` patch matches the resolver construction (`(?:{configuration|{resolver|instance):(.).(?:getRemoteConfig|getUrlDispenserServiceClient).+?;`) and injects a shim that walks `experiments={enable:[...],disable:[...],custom:[...]}` and writes each value straight into `config.values` or `config.activeProperties`, exposing the map as `window.Spotx.RemoteExp`. The three lists come from `patches.json > others.EnableExp/DisableExp/CustomExp`, filtered by version gate in `run.ps1` around lines 2014 to 2180. `-exp_spotify` skips the enable list; a handful of entries are hard-removed by version (search suggestions, the new scrollbar, right-sidebar collapse, subfeed chips, old versus new theme).

What SpotX forces at the pinned commit. Disabled (about 90): every ad and sponsorship flag (`enableHpto`, `enableHomeAds`, `enableCanvasAds`, `enableSaxLeaderboardAds`, `enableEmbeddedAdsCarousel`, `enableSponsoredPlaylistV2`, `enableDesktopMusicLeavebehinds`, the `enable_ad_feedback_*` set, `podcastads-ads_npb`), the fraud and verification set (`enableUserFraudSignals`, `enableUserFraudVerification`, `enableFraudLoadSignals`), impression and telemetry logging, DSA and age-assurance surfaces, `enableInAppMessaging`, `enableLyricsUpsell`, `enableMadeForYouEntryPoint`, `bypassApplyUpdateCheck`. Enabled (about 135): `enableEqualizer`, `enableLyrics`, `enableFullscreenMode`, `enablePiPMiniPlayer` and its variants, `enableSleepTimer`, `enableSilenceTrimmer`, `enableDynamicNormalizer`, `enableOtfn`, `enableRightSidebarLyrics`, `enableRightSidebarExtractedColors`, `enableSmartShuffle`, `enableViewMode`, `enableTracklistColumnsSorting`, `enableResizableTracklistColumns`, `enableHomePin`, `enableGlobalNavBar`, `enableGlobalCreateButton`, `enableAlbumReleaseAnniversaries`, `enableTiltable3DArtwork`, `enableExclusiveModeSetting`, `enableSeekWithArrowKeys`, `enableContextMenuShortcuts`, `enableYlxMultiSelect`, `enableBanArtistAction`, `enableLikedSongsFilterTags`, `enableAlignedCuration` (the `-plus` heart button), `crossfade_enabled`, and the easter eggs. Custom (enum values): `NavAlt`, `GlobalNavBar`, `CreateButton`, `AdsDismissTimeInterval`, `AdsRefreshTimeInterval`, `SearchResultsAsList`, and others.

The `patches.json` `EnableExp`/`DisableExp` tables are the best public catalogue of flag names, descriptions, and version ranges. Flags that only change UI flip locally; flags that gate streaming behaviour are also checked by the server.

## 10. Spicetify v3

The `v3-beta` branch is a Rust workspace (`rust/crates/cli`, `spicetify`, `daemon`, `tui`) with a TypeScript `modularLoader`. Commands: `apply`, `config`, `daemon start|stop|install|uninstall|status`, `dev`, `restore`, `init`, `pkg list|install|delete|enable`, `protocol <uri>` (the `spicetify://` handler), `spotify-updates block|unblock|status`, `self-update`. Config is `config.toml` (`mirror`, `daemon`, `spotify_data_dir`, `spotify_exec`, `offline_bnk_dir`, `block_spotify_updates`).

`apply` takes a lock, stops Spotify, extracts `xpui.spa` to `xpui.tmp`, renames `xpui.spa` to `xpui.spa.backup`, inserts a non-deferred `hooks/spicetifyWrapper.js` and `hooks/modularLoader.js` at the top of `<body>` and strips the stock snapshot tag (the loader re-injects the bundle after mixins), extracts `xpui-modules.js`, runs a Rust port of the expose patches with per-patch miss reporting, seeds the `stdlib`, `store`, and `manager` system modules, stages modules with the classmap rewrite (Aho-Corasick over an embedded map plus a per-version overlay from `spicetify/classmaps`, keyed like `1020094` for 1.2.94), swaps the folder in, and starts a local daemon (axum: `/health`, `/rpc` websocket, `/proxy/{*url}` locked to the `https://xpui.app.spotify.com` origin) that re-applies after Spotify updates.

Modules live in `Modules/<id>/` with `metadata.json` (`name`, `kind` extension|theme|snippet|app|lib, `version`, `authors`, `entries:{js,css}`, `hasMixins`, `dependencies`, `compat`). `entries.js` exports `mixin`, `preload`, `load`. Themes are CSS-only modules; the loader parses a classic `color.ini` into schemes and applies `--spice-*` on `:root` at runtime; `palette-manager` is the scheme switcher. Registry is `spicetify/modules/vault.json`. `supported-versions.json` (schema 2) allowed 1.2.70 to 1.2.94 on 2026-09-01.

Coexistence with v2 is not workable: after a v3 apply the `Apps` folder has `xpui/` plus `xpui.spa.backup` and no `.spa`, so v2 `backup` refuses; after a v2 apply v3 refuses with `foreign-apply`. Both rewrite `index.html` and the v3 daemon keeps re-applying. `Get-SpicetifyV3Conflict.ps1` recognises `Apps\xpui.spa.backup`, `modules`, `hooks`, and a newer CLI major and stops every mutating LibreSpot operation with a `spicetify restore` recovery path.

## 11. Marketplace

Marketplace is a custom app (`spicetify/marketplace`, 1.0.11 pinned). Discovery: GitHub search on the topics `spicetify-extensions`, `spicetify-themes`, `spicetify-apps`, filtered by `resources/blacklist.json`, then each repo's `manifest.json` validated against a zod schema (`name`, `description`, `main`, `usercss`, `authors`, `preview`, `readme`, `tags`, `branch`, `schemes`, `include`). `main` present means extension, `usercss` present means theme, neither means app (listed, not installable). Snippets are one-line CSS entries in `resources/snippets.json` (93 on 2026-09-01, PR-only).

Runtime: installed extensions are re-fetched every launch as `<script defer src="https://cdn.jsdelivr.net/gh/<user>/<repo>@<branch>/<main>?time=...">`; themes inject `<style class="marketplaceCSS marketplaceScheme">` with `--spice-*` variables and `<style class="marketplaceCSS marketplaceUserCSS">` with the fetched `user.css`, remove the `user.css` link, and require `current_theme` to be `marketplace` or refuse to load (that is why LibreSpot's Marketplace-only setup installs the placeholder theme and keeps `inject_css` on). Scheme switching is instant because it is runtime injection, not a re-apply.

Storage: since 1.0.9 a Dexie database `spicetify-marketplace` with a `settings` table keyed by string, migrated from `localStorage` keys starting `marketplace:` (`installed-extensions`, `installed-snippets`, `installed-themes`, `active-tab`, `tabs`, `sort`, `theme-installed`, `local-theme`, `albumArtBasedColors...`, `colorShift`, per-item `marketplace:installed:<user>/<repo>/<main>`). LibreSpot detects that database but does not back it up; recovery archives hold filesystem state only, and users export from Marketplace itself before a repair or reset. `window.Marketplace.reset()` and `.export()` exist.

## 12. BlockTheSpot, spotify-adblock, Windhawk

These are the in-process alternatives to SpotX's on-disk patching.

**BlockTheSpot** (mrpond, archived 2026-02-14; maintained continuations are thomas-quant's BlockTheSpot-Resilient and Nuzair46's fork). A decoy-and-payload DLL proxy: the repo's `chrome_elf.dll` replaces Spotify's and forwards every export to the real one renamed `chrome_elf_required.dll`, while `dllmain` also loads `blockthespot.dll`. That payload IAT-hooks `GetProcAddress` in the main module and `spotify.dll` so requests for `cef_urlrequest_create` and `cef_zip_reader_create` get its stubs: the URL hook returns `nullptr` for URLs matching `/ads/`, `/ad-logic/`, `/gabo-receiver-service/`, `/desktop-update/`; the zip-reader hook byte-patches `xpui-snapshot.js` and `xpui-pip-mini-player.js` in memory after decompression using hex signatures with `??` wildcards from `config.ini` (`adsEnabled:!0` to `!1`, leaderboard kill, `skipsentry`, `disable_metric`). It also IAT-hooks `WinVerifyTrust` and redirects verification of `chrome_elf.dll` to the original signed file, which is how it passes the 1.2.70 signature check without stripping anything. Earlier versions proxied `dpapi.dll`; Spotify 1.2.81 started loading its own, causing a black screen, which forced the move. It broke repeatedly because CEF struct offsets and JS signatures were hard-coded. Resilient resolves the CEF offsets from `libcef.dll`'s version info at runtime, validates every function pointer lands inside libcef's image so a bad offset degrades to "ads not blocked" instead of a crash, and runs a daily watcher that installs the newest Spotify and re-verifies signatures.

**spotify-adblock** (abba23, Linux). An `LD_PRELOAD` Rust library that interposes exactly one function, `cef_urlrequest_create`, and returns null for URLs matching a `config.toml` denylist (`spclient.spotify.com/ads/`, `/ad-logic/`, `gabo-receiver-service`). It never touches the bundle, so upsell UI stays.

**Windhawk "CEF/Spotify Tweaks"** (Ingan121). Not an ad blocker. Patches CEF vtable entries (`is_frameless`, `add_child_view`, `get_window_handle`, `set_background_color`) per CEF version and exposes a JS bridge (`cancelEsperantoCall('ctewh')`) with `setBackdrop("mica"|"acrylic"|"tabbed")`, `extendFrame`, `setLayered`, `setTransparent`, and a forced-dark-mode switch. WMPotify is the theme built on it. This is the only working route to real window transparency and native chrome; it needs `--no-sandbox` and tracks CEF versions.

## 13. How the tools coexist and collide, and what LibreSpot does about it

Required order: stock Spotify, then SpotX, then `spicetify backup apply`. SpotX refuses to run if it finds an extracted `Apps\xpui\xpui.js` (Spicetify's working layout) and points at its FAQ. Spicetify's backup copies whatever `xpui.spa` it finds, so the backup is the SpotX-patched bundle; `spicetify restore` therefore restores a SpotX-patched client, not a stock one. SpotX's binary patches are in separate files and are unaffected by anything Spicetify does. On the live machine `Backup\xpui.spa` contains the `// Patched by SpotX` marker and the three `spotx-helper` files.

The collision: SpotX repoints `index.html` at the combined `/xpui.js` it built from the snapshot. Spicetify 2.44.0's custom-app injection prefers `xpui-modules.js` and puts the chunk map into `xpui-snapshot.js`. With both installed the Marketplace route can end up only in files the page never executes, and `/marketplace` renders blank with no console error (React.lazy suspends forever). `Test-SpicetifyCustomAppRouteWiring.ps1` detects it (`Wired`, `NotWired`, `NotApplicable`, `Unknown`) and `Repair-SpicetifyCustomAppWiring.ps1` ports the CLI's own `insertCustomApp` and `insertCustomAppChunkMap` onto the bundle `index.html` actually loads, keeping a `xpui.js.librespot.bak` beside it. Each app needs its own lazy identifier. The repair scans existing `spicetifyAppN` names before adding the next one, and its CSS gate accepts the quoted route chunks already added for earlier apps. This keeps Marketplace on `spicetifyApp0`, LibreSpot on `spicetifyApp1`, and both route styles in the gate. Every raw `spicetify apply` undoes that repair, so the repair must follow every apply; `Module-ApplySpicetify` does this.

Spicetify also refuses `backup apply` when a complete current backup already exists. `Get-SpicetifyApplyPlan.ps1` reuses that backup with `apply --no-restart --bypass-admin` when `Backup`, `Extracted`, and version evidence agree. It uses `backup apply --bypass-admin` for a new, incomplete, or stale setup. Spotify.exe reports `1.2.93.667`, while Spicetify records `1.2.93.667.g7b5cc0ce`, so the comparison removes the optional `.g<hash>` suffix first.

Other LibreSpot behaviours that exist because of these mechanics: it hash-verifies the pinned SpotX script and runs it in an isolated process (`Invoke-ExternalScriptIsolated.ps1`); it never passes the Defender opt-out to the pre-boundary pin; it launches Spotify hidden once after SpotX to generate `prefs`; `Get-SpotXPatchVerification.ps1` treats `Apps\xpui.bak` or the binary `.bak` files as proof SpotX ran and never checks `xpui.spa.bak` alone; `Get-SpicetifyV3Conflict.ps1` stops mutations when v3 artefacts are present; Marketplace-only installs create the placeholder theme and keep `inject_css` on; `Install-MarketplaceNavFallbackExtension.ps1` restores a visible Marketplace button when nav-link injection fails; the auto-reapply watcher reruns the saved config only when Spotify's version changed and Spotify is closed.

## 14. Spotify countermeasures and breakage history

- 2023: Library X left sidebar (1.2.7), right-sidebar Now Playing View, then the global nav bar (1.2.45+, mandatory from 1.2.47). Each moved the anchors themes and nav-link injection depend on.
- 2024-11-27: the Web API audio-analysis endpoint was shut off; `getAudioData` and every visualiser or BPM-synced extension built on it degraded.
- 1.2.62 to 1.2.64 (mid 2025): webpack modules moved into the V8 snapshot and some React components were removed; both patchers grew snapshot extraction.
- 1.2.70 (August 2025): logic moved into a signed `Spotify.dll` and CEF module code-signing was enabled (CEF issue #3935). SpotX issue #760: "all binary patches broke." Fixed 2025-10-03 by the signature stub and cert-table zeroing described in section 2.
- 1.2.78: `React.lazy` changed to an async form; Spicetify 2.42.4 added new patterns.
- 1.2.81: Spotify started loading its own `dpapi.dll`; BlockTheSpot moved to the `chrome_elf` proxy.
- 1.2.86 (March 2026): hashed class names shortened from 20 to 16 characters; every theme broke until 2.43.0 to 2.44.0 shipped new css-maps.
- 1.2.93: bundler switched from webpack to rspack (`rspackChunkclient_web`); routing moved to `xpui-modules.js`. Spicetify 2.44.0 handles it.
- 1.2.94 (July 2026): the `slots` string trick stopped blocking ad slots; SpotX added the control-flow patch. SpotX issue #876.
- 1.2.97: custom apps throw `useNavigateStable must be used within a StableUseNavigateProvider` on mount (marketplace #1222); 1.2.98 reports a zip checksum error on backup (cli #3914).
- Ad delivery has also shifted toward home cards, leavebehinds, embedded now-playing ads, and server-side insertion in the audio stream, which no client patch can remove.

## 15. Policy, enforcement, and the safe design space

Spotify's User Guidelines forbid circumventing or blocking advertisements, reverse-engineering or modifying the client, and circumventing any technology Spotify uses; the sanction is suspension or termination. The Terms of Use (2025-08-26) add that paid features may not be accessed without paying.

Observed enforcement 2025 to 2026: server-side "Banshee" broke modded Android clients (tracks skip after about ten seconds) and xManager archived its repo on 2026-04-06; GitHub DMCA notices from Spotify's counsel took down re-unplayplay forks, SpotifyDL, EeveeSpotify and about 520 forks, ReVanced patches claiming Premium access, and spotify-dl-cli, almost always on a copyright theory ("copies code that controls how Spotify limits access to paid features"). SpotX, SpotX-Bash, BlockTheSpot, Spicetify, spotify-adblock, and every theme repository have not been hit. No documented mass ban of desktop users exists; SpotX's FAQ says it knows of no ban caused by the mod, and isolated reports remain unconfirmed.

What that means for LibreSpot: the ad-blocking it already performs through SpotX is the higher-risk part. Net-new work should stay on the theming, layout, window-chrome, and local UI-flag side; should never redistribute patched binaries or bundle contents (patches ship as transforms applied on the user's machine, with pristine backups kept); and should avoid product-state spoofing (`employee`, `use.ucs.product.state`), paid-feature spoofing, and key extraction, which is exactly the pattern every takedown targeted.

## 16. Verification signals LibreSpot relies on

- SpotX applied: `Apps\xpui.bak` or `Spotify.bak`/`Spotify.dll.bak`/`chrome_elf.dll.bak` present; `// Patched by SpotX` at the end of the live `xpui.js`; `dodo-receiver-service` present in `Spotify.dll`; `spotx-helper/` inside the bundle. After Spicetify consumes `xpui.bak`, the binary `.bak` files and the extracted `Apps\xpui` folder are the durable markers.
- Spicetify applied: `Apps\xpui\` folder with `index.html` carrying the `userCSS` links, `helper/spicetifyWrapper.js`, and the inline `Spicetify.Config`; `config-xpui.ini` `[Backup] with` equal to the CLI version and `version` non-empty and equal to prefs `app.last-launched-version`.
- Theme applied: `xpui\colors.css` first `--spice-main` value equals the scheme's `main`; `xpui\user.css` is the theme's file; `xpui\extensions\theme.js` present when `inject_theme_js=1`; `spicetify-config.json` names the theme.
- Marketplace usable: `xpui\xpui.js` contains `spicetify-routes-marketplace` (the live bundle, not only `xpui-modules.js`); `spicetify-routes-marketplace.js/.css/.json` present; `current_theme` is `marketplace` or the placeholder for Marketplace themes to load.
- LibreSpot engine installed: `CustomApps\librespot\manifest.json` registers `librespot-engine.js`, its archive SHA256 matches `CommunityCustomApps.ps1`, `config-xpui.ini` contains the `librespot` custom app, and the live bundle contains `spicetify-routes-librespot` with a distinct `spicetifyAppN` identifier and CSS-gate entry.
- LibreSpot engine healthy: the Health report matches the main view, navigation, playbar, and page-scroll anchors; treats a closed right sidebar as inactive; reports both LibreSpot and Marketplace routes as wired; and reads a Spotify version beginning with `1.2.93` from `Spicetify.Platform`.
- v3 conflict: `Apps\xpui.spa.backup`, a `modules` or `hooks` folder under the Spicetify config root, or a CLI major above 2.

## 17. Sources

SpotX: https://github.com/SpotX-Official/SpotX (`run.ps1`, `patches/patches.json`, `js-helper/*`, discussions/50, issues #407, #760, #876), https://github.com/SpotX-Official/SpotX-Bash (`spotx.sh`), https://telegra.ph/SpotX-FAQ-09-19, https://github.com/LoaderSpot/LoaderSpot, https://loadspot.pages.dev/.

Spicetify: https://github.com/spicetify/cli at `v2.44.0` (`src/preprocess/preprocess.go`, `src/apply/apply.go`, `src/cmd/apply.go`, `src/cmd/backup.go`, `src/cmd/patch.go`, `src/utils/color.go`, `src/utils/config.go`, `jsHelper/expFeatures.js`, `src/jsHelper/spicetifyWrapper/*`, `globals.d.ts`, `css-map.json`), the `v3-beta` branch (`docs/v3-modules.md`, `docs/supported-versions.md`, `rust/crates/spicetify/src/commands/apply.rs`, `module/expose.rs`, `module/stage.rs`, `src/jsHelper/modularLoader/index.ts`), https://github.com/spicetify/modules, https://github.com/spicetify/classmaps, https://github.com/spicetify/hooks, https://spicetify.app/docs/development/themes, https://spicetify.app/docs/customization/config-file, https://spicetify.app/docs/development/spotify-cli-flags, https://spicetify.app/docs/development/api-wrapper/methods/platform, issues #1095, #860, #2836, #2657, #3038, #3120, #3435, #3834, #3914, releases v2.42.4 through v3.0.0-beta.10.

Marketplace: https://github.com/spicetify/marketplace (`src/logic/Storage.ts`, `src/logic/FetchRemotes.ts`, `src/logic/Schemas.ts`, `src/extensions/extension.tsx`, `src/constants.ts`, `resources/snippets.json`, wiki Publishing-to-Marketplace, issues #1161, #1201, #1222, PR #1212).

In-process patchers and native mods: https://github.com/mrpond/BlockTheSpot (`Hook/`, `Loader/`, `config.ini`, issues #650, #652), https://github.com/thomas-quant/BlockTheSpot-Resilient (`Hook/cef_offsets.cpp`, `funct_pointer.h`, `tools/verify_patches.py`), https://github.com/Nuzair46/BlockTheSpot, https://github.com/abba23/spotify-adblock (`src/hook.rs`, `config.toml`), https://github.com/ramensoftware/windhawk-mods/blob/main/mods/cef-titlebar-enabler-universal.wh.cpp, https://github.com/Ingan121/WMPotify, https://github.com/chromiumembedded/cef/issues/3935, https://github.com/chromiumembedded/cef/issues/3404, https://gist.github.com/rxri/e561fff9a7a681565312c71829cb5184, https://bool3max.win/posts/spotify_ui_hacking/, https://github.com/amd64fox/Enable-devtools-Spotify.

Themes and ecosystem: https://github.com/spicetify/spicetify-themes, https://github.com/Comfy-Themes/Spicetify, https://github.com/sanoojes/spicetify-lucid, https://github.com/Astromations/Hazy, https://github.com/nimsandu/spicetify-bloom, https://github.com/catppuccin/spicetify, https://github.com/JulienMaille/spicetify-dynamic-theme, https://github.com/hoeci/sort-play, https://github.com/rxri/spicetify-extensions, https://github.com/theRealPadster/spicetify-hide-podcasts.

Policy: https://www.spotify.com/us/legal/user-guidelines/, https://www.spotify.com/us/legal/end-user-agreement/, https://github.com/github/dmca (2025-08-14, 2025-09-11, 2026-03-17, 2026-05-28 Spotify notices), https://torrentfreak.com/revanced-complies-with-spotify-takedown-but-explores-options-to-fight-back/.

Local evidence used: `%APPDATA%\LibreSpot\cache\863cd194...` (the pinned SpotX `run.ps1`), the live `%APPDATA%\Spotify` install (binary diffs against the `.bak` files, the extracted `Apps\xpui` bundle, `prefs`), `%APPDATA%\spicetify\` (config, `Backup\xpui.spa`, `Extracted\`), and the Prism theme at `resources/themes/Prism`, which moved into this repository from `C:\repos\LibreSpot-Prism` on 2026-09-03.
