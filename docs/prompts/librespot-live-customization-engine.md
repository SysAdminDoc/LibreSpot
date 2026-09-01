# Prompt: build the LibreSpot live customization engine for Spotify

Paste this whole file into a fresh Codex session opened at `C:\repos\LibreSpot`. It is self-contained. Work autonomously through every phase, do not stop to ask, and do not stop early.

## Mission

Build something that does not exist yet: a dedicated **LibreSpot** surface inside the Spotify desktop client that gathers every customization LibreSpot can offer into one organized place, and a **live theming engine** behind it, so palette, layout, effects, feature flags, snippets, and presets change on screen the moment the user touches them, with no `spicetify apply` and no Spotify restart for the common cases. Ship it as a LibreSpot update: the desktop app installs and manages it, and the in-Spotify surface owns the live state.

Today the ecosystem forces users to assemble a look by hand from a theme repo, a colour-scheme file, a pile of extensions, a Marketplace tab, and a settings dialog that Spotify hides. Every theme is monolithic, most break on each Spotify update, none follow the OS, few think about performance or accessibility, and applying anything means a CLI round trip. You are replacing that experience.

Use the existing projects as research and as a parts bin. Read their code, take their working patterns, and take their code. Do not reinvent what a maintained project already solved. The goal is the best Spotify customization experience that exists, built from everything the ecosystem has already figured out, using every source of data and code available. "Licenses" below tells you how to take all of it without handing anyone a reason to file a takedown that stalls the repo.

## Read these first, in this order

1. `docs/how-spotx-and-spicetify-alter-spotify.md`. The verified reference for how SpotX and Spicetify change the client at the byte and regex level, the theme format, the exposed API surface, the flag mechanisms, and the route collision LibreSpot repairs. Its policy section is background reading, not a rule for this build. Everything you build sits on top of what this describes.
2. `CLAUDE.md`. Working notes, gotchas, verification commands, and the file rules. The mixed line-ending warnings and the `Wpf*` test filter note will save you an hour each.
3. `RESEARCH.md`, section "Customization and Theming Deep-Dive (2026-09-01)". Why these features, what users asked for, and what was rejected and why.
4. `ROADMAP.md`, items RD-135 through RD-141. Those are the roadmap slices of this same work: a curated experimental-feature picker, a LibreSpot-original theme, one-click presets, reviewed CSS snippets, a post-apply version-resilience self-test, per-item catalog health, and a cosmetic custom-patch library. This prompt builds the engine and the in-Spotify surface that make all seven coherent. Drain them as you go; delete each from `ROADMAP.md` when it ships.
5. `C:\repos\LibreSpot-Prism`. A working proof-of-concept theme (`color.ini`, `user.css`, `theme.js`) that already does time-scheduled light and dark (which sidesteps Spotify's forced dark mode), album-art accent through `Spicetify.colorExtractor`, a frame-rate probe that steps glass effects down, and a high-contrast scheme. Absorb it into the engine as the first built-in theme and the reference for the layer model. Its `prism-spotx-patches.json` is a validated example of the SpotX custom-patch format.
6. `src/powershell/shared/Module-InstallThemes.ps1`, `Module-InstallExtensions.ps1`, `Module-InstallCustomApps.ps1`, `Repair-SpicetifyCustomAppWiring.ps1`, `Test-SpicetifyCustomAppRouteWiring.ps1`, `New-SpotXCustomPatchesFile.ps1`, `Build-SpotXParams.ps1`, and `src/LibreSpot.Core/AppCatalog.cs`. This is the machinery you extend. LibreSpot already pins and hash-verifies community assets, already accepts custom SpotX patches, already wires custom-app routes into the bundle SpotX serves. Do not build a second copy of any of it.

## Non-negotiables

- **Surface everything, reimplement nothing that upstream maintains.** The LibreSpot panel exposes every capability LibreSpot already applies: every SpotX switch (ad blocking, podcast and section hiding, update blocking, lyrics, cache, the lot), every SpotX-forced feature flag including the ad and telemetry families, every Spicetify option, every theme, extension, snippet, and preset. Nothing is hidden from the user on principle. The one engineering rule: do not rewrite SpotX's ad, premium, or binary patches yourself. Upstream ships them weekly and they break on every Spotify release; a copy in this repo is stale within a month and becomes a support burden. Pin SpotX, pass its switches and custom patches through, and put the controls in the panel. Spotify's terms and the reference doc's policy section are context for the reader, not constraints on this build.
- **Live means live.** Palette, scheme, layers, effects tier, feature flags that are client-side, snippets, and presets apply at runtime through CSS variable injection, class toggles, and `expFeatureOverride`. The CLI path (`color.ini`, `user.css`, `spicetify apply`) is the export and persistence format, not the interaction model. Only things that genuinely need a bundle rewrite (installing a new custom app, a SpotX custom patch) go through the desktop app, and the UI must say so.
- **Survive the update treadmill.** Target stable anchors (`.Root__*` layout roots, `data-testid`, ARIA roles, Spotify's own `--background-*`/`--text-*` tokens) before hashed `.main-*` classes. Ship a self-test that runs after every load, checks every anchor and route the engine depends on, and shows a named warning in the LibreSpot surface when one is missing, instead of letting the UI silently break the way the 1.2.86 class-hash change broke every theme. Every raw `spicetify apply` undoes LibreSpot's route repair; the engine must detect that and offer the repair.
- **Performance and accessibility are features, not afterthoughts.** No `:has()` in hot paths, no unconditional `backdrop-filter`, honour `prefers-reduced-motion`, keep a flat tier, keep the frame-rate probe, keep WCAG AA contrast on every scheme, keep every control keyboard-reachable with a visible focus ring, and expose per-region scale variables.
- **Repo rules apply in full.** No AI authorship anywhere in git (author and committer are `SysAdminDoc <matt_parker@outlook.com>`, no `Co-Authored-By`, no AI names in code, commits, docs, or changelog). No GitHub Actions. No Dependabot. Every public-facing sentence in human voice: no em dashes, no en dashes, contractions welcome, vary sentence length, no rule-of-three lists, none of the banned vocabulary (`robust`, `seamless`, `leverage`, `elevate`, `streamline`, `delve`, and the rest). Bump the version on delivery and keep every version string in sync. Build the release artifacts locally. Commit and push after each completed logical change with `rtk git`.
- **Pins do not move.** Spotify 1.2.93, SpotX `550bc72c`, Spicetify CLI 2.44.0, Marketplace 1.0.9, themes commit `df033493`. Build against the API surface those expose. Do not adopt Spicetify v3; the reference doc explains the incompatibility, and `Get-SpicetifyV3Conflict.ps1` will block you anyway.
- **Use your eyes, not the user's mouse.** You can see the screen. Use that constantly: after every UI change, look at Spotify with the change applied, judge it against the intent, fix, and look again. Run the visual audit below at the end of every phase. What you must not do is send input to the user's active desktop (no Computer Use clicks, no `SendInput`, no foreground window automation). Drive interaction over `--remote-debugging-port` from a helper and observe the result visually.

## Visual audit (run it after every UI change, and fully at each phase end)

Look at the real Spotify window and check, in each of the four built-in schemes and in each effects tier:

- Nothing clips, overflows, or scrolls horizontally at 1280 by 800, at 1440 by 900, and maximized.
- Text contrast reads as AA everywhere, including hover and active states, disabled controls, snackbars, and the LibreSpot rail. If you are unsure, measure it.
- Spacing and type match Spotify's own panels; the LibreSpot surface should look like it shipped with the client, not bolted on.
- Every control shows a visible focus ring when reached by keyboard (drive the focus over the debugging port and watch it).
- Playbar, sidebar, right panel, Marketplace, Settings, Search, and a playlist page still render correctly with the theme active; open each and look.
- The glass tier shows no seams or double borders; the eco tier keeps translucency without blur; the flat tier has no blur or transitions at all; `prefers-reduced-motion` flattens everything.
- A scheme switch, a layer toggle, a flag flip, a snippet toggle, and a preset apply each change the screen immediately, with no flash of unstyled UI.
- Compare a before-and-after screenshot pair for every change you keep, and keep the pairs for the README.

Fix what you see before moving on. A green test suite does not excuse a UI that looks wrong.

## Licenses (how to take everything)

The maintainer's position is simple: use every line of code and every byte of data the ecosystem has, whatever gets the best result. The only thing that can actually slow that down is a public takedown or license complaint against the repo, and there is a one-line move that makes every licensed project fair game: **license the engine component AGPL-3.0** (`src/LibreSpot.App/LICENSE`, noted in the root README). AGPL is the strictest license in the pile, so anything licensed MIT, WTFPL, LGPL, GPL, or AGPL can be vendored into it verbatim. Do that in the first commit of the parts-bin phase. SPDX identifiers below were read from GitHub on 2026-09-01.

- **Take verbatim, from anything that carries a license:** `spicetify/cli` (LGPL-2.1: `expFeatures.js`, `homeConfig.js`, `sidebarConfig.js`, the wrapper's `Platform` resolution, the custom-app injection code), `sanoojes/spicetify-lucid` (AGPL-3.0: the Material colour engine seeded from artwork, the settings modal, the version-pinned manifest pattern; the best colour engine in the ecosystem, take the whole thing), `theRealPadster/spicetify-hide-podcasts` (GPL-3.0: DOM-stable hiding of podcast and audiobook surfaces), `ohitstom/spicetify-extensions` (GPL-3.0: `sleepTimer`, `quickQueue`, `volumePercentage`, `playbarClock`, `immersiveView`), `alexk218/tagify` (GPL-3.0: tag and rate tracks, bulk tag to playlist), `NMWplays/Liquify` (AGPL-3.0: glass and refraction effects; it is 651 KB, take the pieces you need), `Spikerko/spicy-lyrics` (AGPL-3.0: the lyrics surface), `Ingan121/WMPotify` (non-standard license text; read it once, the mini-player and high-contrast mode are worth it), SpotX (`patches.json` is data, take the tables; the script stays the pinned adapter LibreSpot already runs), and the permissive set: `spicetify/marketplace` (MIT: the runtime scheme injection in `injectColourScheme`, the Dexie storage layer, the `colorExtractor` vibrancy and ColorApi modes, the Theme Dev Tools editor, `resources/snippets.json` as data), `spicetify/spicetify-themes` (MIT: Dribbblish's sidebar and folder-image logic, StarryNight's turntable, the `text` theme's scheme catalog), `catppuccin/spicetify` (MIT: the accent picker built on `Spicetify.React`, palette export as extra `color.ini` keys), `JulienMaille/spicetify-dynamic-theme` (MIT: Vibrant swatch selection by background lightness, the light/dark sentinel), `hoeci/sort-play` (MIT: the pattern for adding columns, context-menu entries, and a settings surface to Spotify's own tracklist), `rxri/spicetify-extensions` (MIT: `wikify`, `songstats`, `phraseToPlaylist`, and `adblockify` as the Spicetify-layer ad fallback LibreSpot already offers), `Konsl/spicetify-extensions` (MIT: waveform seekbar, context switcher), `Theblockbuster1/spicetify-extensions` (MIT: queue duration, cover ambience), `Xndr2/listening-stats` (MIT: local listening tracker without an external backend), `wSoltani/syncify` (MIT: Marketplace state backup and restore), `Comfy-Themes/Spicetify` (WTFPL: in-app settings UI, font loader, panel-width variables, the `wal16` pywal scheme).
- **No license on file (`Astromations/Hazy`, `surfbryce/beautiful-lyrics`, `harbassan/spicetify-apps`):** read them completely and take the approach, the selectors, the class names, the data flows, and the UI decisions; write the code yourself. Their ideas are what matter and none of that is protected.
- **Research only, because it is native and outside this feature, not because of any license:** BlockTheSpot, BlockTheSpot-Resilient, the Windhawk CEF mod.

Keep a `THIRD_PARTY_NOTICES.md` next to the vendored code listing every source, license, and commit. It costs nothing, it is what the GPL family asks for, and it is what lets you tell anyone who complains that the code sits in a compatible project with attribution.

## What to build

### 1. The in-Spotify surface: a `librespot` custom app plus a companion extension

A Spicetify custom app (React through `Spicetify.React`, no JSX, `render()` returning an element, route `/librespot/*`) that appears in Spotify's navigation as **LibreSpot**, plus a small companion extension (`librespot-engine.js`) that loads on every page and owns the live state, the self-test, and the persistent keyboard-free menu entry. The existing `librespot-marketplace-button.js` fallback shows how LibreSpot already places a button when nav-link injection fails; fold that behaviour in so the LibreSpot entry is always reachable.

Organise it as one workspace with a left rail and these panels, in this order, because this is the order users think in:

1. **Look.** Theme picker (built-in engine themes plus any installed Spicetify theme), scheme picker with live preview on hover, layer toggles (palette, layout, effects, accessibility), effects tier (glass, eco, flat) with the frame-rate probe result shown, dynamic accent (album art, fixed, OS accent when a helper is present), font and per-region scale, rounded-corner radius, auto light/dark schedule with a clock editor. Every control applies live.
2. **Tweaks.** The reviewed CSS snippet catalog (start from Marketplace's `resources/snippets.json` categories: hide things, rounded UI, cover-art shapes, sidebar behaviour, lyric styling, window-control fixes), each with a preview, a last-verified Spotify version, a source link, and a live toggle. Also the home-section and sidebar arrangement that Spicetify's `homeConfig`/`sidebarConfig` do, reimplemented so it works under the global nav bar.
3. **Features.** The experimental-feature picker (RD-135), covering every flag the client declares (about 330 on 1.2.93) plus SpotX's own switches. Group them by what they change (Playback, Now Playing, Library, Home, Lyrics, Layout, Ads and tracking, Fun, Everything else). Show Spotify's own description string for each and, where SpotX already forces a value, show that as the default with the source named. Apply client-side flags through `expFeatureOverride` and `RemoteConfigDebugAPI` live and persist them; route SpotX switches through the desktop handoff since they need a re-patch. Mark server-gated flags (Enhance, quality tiers, Jam) as "does nothing on a free account" rather than hiding them; the user decides.
4. **Extensions.** What is installed, what is enabled, per-item health (last-verified version, known issues), and live enable/disable for anything that supports it. Where a change needs the desktop app (installing something new, a SpotX custom patch), say so and hand off (see section 4).
5. **Presets.** Named bundles of theme, scheme, layers, flags, snippets (OLED, Accessibility, Compact, Performance, plus user-saved). Applying a preset fills every control so the user can adjust afterwards. Presets serialize to the `.librespot` profile format so the desktop app can import them.
6. **Health.** The self-test results: every anchor the engine depends on, the Marketplace route wiring state, whether a raw `spicetify apply` has undone the route repair, the Spotify version against the pinned tuple, and a one-click "copy diagnostics" for a support bundle. Healthy state is quiet.

Design language: dark by default, Spotify-native spacing and type, no confirmation dialogs (immediate action plus a snackbar through `Spicetify.showNotification`), every control keyboard-reachable, visible focus, and it must look right in all four built-in schemes including high contrast.

### 2. The live theming engine

A runtime layer, not a CLI wrapper.

- **Palette at runtime.** Parse `color.ini` sections into schemes (reimplement Marketplace's approach), inject `--spice-*` and `--spice-rgb-*` on `:root` through one managed `<style>` element, and derive any missing key the way the CLI's default list does, so switching schemes never restarts Spotify. Keep `colors.css` in sync on export so the CLI path still works.
- **Layers.** Palette, layout, effects, and accessibility are independent CSS modules toggled by classes on `<html>` (Prism's `prism-glass`, `prism-eco`, `prism-flat`, `prism-contrast` are the seed). A user can stack a Catppuccin palette on a Dribbblish-style layout with Hazy-style glass. That composition is the thing no theme offers today.
- **Dynamic accent.** `Spicetify.colorExtractor` on `songchange` with the preset choice (vibrant, light vibrant, prominent) and a scheme fallback when the service fails. Add Lucid's Material-style derivation as an option for full-palette tinting.
- **Schedules and signals.** Time-of-day scheme switching (Prism), and a small local signal interface so a future desktop helper can push the OS accent and light/dark state; the engine reads a value if present and stays silent if not.
- **Performance tiers.** The one-second frame-rate probe at load, automatic step-down, and manual override, plus `prefers-reduced-motion` handling that flattens everything.
- **Self-test.** A list of required anchors and routes checked after load and after every navigation, reported to the Health panel and to the console.
- **Export.** Any live state exports to a standard Spicetify theme folder (`color.ini`, `user.css`, `theme.js`) and to a `.librespot` profile, so nothing the user builds is trapped in the engine.

### 3. Built-in themes

Ship at least Prism (absorbed) plus two more built on the layer model that answer the ranked demand in `RESEARCH.md`: a **Compact** layout (denser rows, thinner sidebar, playbar at the bottom edge, mini-player-like density) and a **Reading** or **Accessibility** theme (large targets, dyslexia-friendly font option, thick focus rings, no transparency, high-contrast palette). Each must pass the contrast gate on every scheme.

### 4. Desktop app integration

- The desktop installs the custom app and companion extension through the existing verified custom-app path (pinned archive, SHA256, `Module-InstallCustomApps.ps1`), registers them, and runs the route repair after apply exactly as it does for Marketplace.
- Custom Install gains the RD-135 flag picker and the RD-138 snippet catalog as data-driven sections that write the same state the in-Spotify surface reads, so both places agree. The catalog source of truth lives in one place (`src/LibreSpot.Core/AppCatalog.cs` plus a JSON data file under `schemas/` that both the desktop and the custom app consume).
- Handoff: the in-Spotify surface exports a `.librespot` profile to the clipboard through `Spicetify.Platform.ClipboardAPI` and tells the user to import it in LibreSpot; the desktop's existing import and preview handle the rest. Do not build a daemon or a local server for this.
- Maintenance gains the engine's health output in its existing status model so a broken anchor or an undone route repair shows up as a named issue with a repair action.
- The auto-reapply watcher must reinstall the custom app and rerun the route repair after a Spotify update.

### 5. Tests and gates

- Custom app and engine: a minimal Node workspace under `src/LibreSpot.App/` (or a name that fits the repo) with `vitest` and a DOM (`happy-dom`; keep fixture windows to one per file, the suite has a memory ceiling), tests for scheme parsing and derivation, layer composition, the flag catalog matching what the pinned bundle actually declares (parse the flag names out of the real `xpui.js` so a stale catalog fails), snippet catalog validation, the self-test against a fixture bundle with a removed anchor, and export round-trips (`color.ini` in, engine state, `color.ini` out, compared against the input, not against a second trip). Run the linter explicitly; a green suite does not cover it.
- PowerShell: Pester coverage for the new catalog files, the custom-app install path, and the repair-after-apply behaviour (the existing fake-root harness in `tests/powershell` shows how).
- .NET: Core tests for catalog validation and the profile round-trip; Desktop tests for the new sections, localization parity in all five locales, and the WPF QA matrix rows the new sections add. Use the verification commands in `CLAUDE.md` exactly (`--filter-not-class "*Wpf*"`, never the method filter).
- Live check before calling anything done: install on this machine through the desktop app, open Spotify, drive it over `--remote-debugging-port` from a helper, and prove that scheme switching, a layer toggle, a flag flip, a snippet toggle, and a preset apply all change the DOM without a restart, that the Marketplace route still works, and that the self-test reports healthy. Watch each of those happen on screen and run the full visual audit. Keep the screenshot pairs for the README. Reapply LibreSpot's own saved setup afterwards.
- Gates: `Build-Scripts.ps1 -Lint`, `-Validate`, `-DependencyHealth`, `-CatalogTruth`, the non-WPF .NET suites, Pester, and the new JS suite, all green, before every push.

### 6. Documentation and release

- Update `docs/how-spotx-and-spicetify-alter-spotify.md` with a section on the engine's runtime injection and its self-test anchors, so the next run knows what the engine depends on.
- README: a new section for the in-Spotify LibreSpot surface with fresh offscreen screenshots, the built-in themes, and an honest note about what still needs the desktop app. Keep the badges and version in sync.
- CHANGELOG under `[Unreleased]`, then the version bump. Bump the desktop, CLI, and Core version strings together; `-Validate` checks them.
- Vault and memory: record durable findings (what anchors held, what broke, which vendored code came from where) in `CLAUDE.md`, and remove shipped items from `ROADMAP.md`.
- Build the release artifacts locally (Desktop and CLI self-contained win-x64, PS2EXE, SBOM, checksums, release manifest) following the README's local release procedure. Do not create a GitHub release without the maintainer's go-ahead; leave a draft.

## Phases

Work in this order. Each phase ends with a commit that builds and passes its gates.

1. **Recon.** Read the six items above. Inventory every function the feature touches. Write a one-page design in the commit body of the first commit, not in a new markdown file.
2. **Parts bin.** Add the AGPL-3.0 license to the engine component, clone every repo in the Licenses section into a scratch folder, take the exact pieces you will reuse, record source, license, and commit in `THIRD_PARTY_NOTICES.md`, and rewrite the three unlicensed ones from their approach.
3. **Engine core.** Scheme parsing, runtime injection, layers, export, self-test, tests.
4. **Surface.** The custom app, the six panels, the companion extension, the always-reachable entry.
5. **Catalogs.** Flags (all of them), snippets, presets, built-in themes, the catalog-matches-the-bundle test.
6. **Desktop wiring.** Install path, Custom Install sections, Maintenance health, watcher, profile handoff, localization in five locales.
7. **Live proof.** The remote-debugging drive, the full visual audit in every scheme and tier, screenshot pairs, reapply the saved setup.
8. **Docs, version, artifacts, push.**

## Definition of done

- A user opens Spotify, clicks **LibreSpot** in the nav, and can change theme, scheme, layers, effects, flags, snippets, and presets and see each change immediately, with no restart and no CLI.
- Everything they build exports to a standard Spicetify theme folder and to a `.librespot` profile, and the desktop app can import it.
- The desktop app installs, repairs, and reapplies the whole thing through its existing verified paths, and Maintenance shows the engine's health.
- The route to Marketplace still works after every apply; the self-test proves it.
- Every SpotX switch and every declared flag is reachable from the LibreSpot panel; nothing the tools can do is hidden from the user.
- All gates green, version bumped everywhere, README and CHANGELOG updated, artifacts built, pushed to `main` with the correct author and no AI trace.
- The reference doc and `CLAUDE.md` tell the next run what you learned.

## Report format

At the end, report in this order and nothing else: what shipped (one line per panel and per engine capability), what was vendored and from where, what was verified live and how, what did not work and why, and what is left. Plain prose, no marketing voice.
