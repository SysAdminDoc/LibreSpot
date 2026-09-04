# Changelog

All notable changes to LibreSpot will be documented in this file.

## [Unreleased]

## [v4.4.0] (2026-09-04)

### Added

- Six page-specific visual references now define the in-Spotify settings experience. Store, Look, Tweaks, Features, Presets, and Health each have a matching production capture from the installed Spotify client.
- Look has a live appearance workbench with a large Spotify preview and direct theme, scheme, and effects controls. Its scheme strip uses real previews for every built-in palette.
- Tweaks has search, category filters, a before-and-after spotlight, and preview art for every reviewed rule. Presets use real outcome previews and list the changes each profile makes before it is applied.
- Health groups its eight checks into the live engine, route wiring, and compatibility. Each row names the part that was tested and its current result.

### Changed

- Store is the default catalog for themes, extensions, and custom apps. Theme cards expose all 24 preview images and their schemes. Extension and app cards explain their behavior, installed state, source, review status, and setup path.
- The LibreSpot rail is shorter and easier to scan. Its six entries use one icon family and concise descriptions, while the settings cog in Spotify's top bar opens Look directly.
- Features uses source filters and a persistent group index for all 348 Spotify flags and 31 SpotX controls. Search, live-only, desktop-reapply, and customized views update the counts and visible groups together.
- The settings layout responds to the width Spotify leaves between its Library and Now Playing panels. At narrow desktop widths, dense sections reflow without hiding search, preview, or health controls.

### Fixed

- The documented local release sequence now generates `checksums.txt` before the release manifest consumes it.

- The live engine workspace now pins pnpm 11.25.0, replacing the retired audit endpoint used by pnpm 10.

- Lucide is pinned to the reviewed 1.39.0 build so clean installs retain pnpm 11's 24-hour package-age protection.

- pnpm's dependency build allowlist now names only esbuild and Parcel's file watcher.

- JavaScript advisory requests now have a bounded 30-second network timeout and one retry, so a registry outage fails the gate instead of hanging it.

- Desktop release restores now run in locked mode, including the SDK's single-file linker task, so publishing and SBOM generation cannot rewrite reviewed dependency state. The linker task is recorded as a build-only dependency in the third-party notices.

- Stable executable builds now load the pinned local PS2EXE module when the current user's PowerShell execution policy blocks unsigned module scripts.

- Cold Spotify reloads now let both companion script copies converge on one ready runtime, so Settings won't remain stuck on its loading screen.
- Tweak cards no longer squeeze their title between the category label and switch. Categories now sit with the version and source metadata, matching the visual hierarchy used by the reference design.
- Stable keys are used for arrangement rows, removing the duplicate-key warning that appeared when Spotify rendered two items with the same label.
- Store tabs, theme preview and restore, live appearance controls, tweak switches, feature overrides, preset save and apply, Health refresh, and the top-bar settings route were exercised in Spotify 1.2.93.667 with Spicetify 2.44.0. All 24 theme images loaded, and the live route reported healthy.

## [v4.3.0] (2026-09-03)

### Added

- LibreSpot is now the default store inside Spotify. Store sits first in the rail and combines all 24 supported themes, 16 extensions, and two apps in searchable tabs. Theme cards include real screenshots, available schemes, source details, compatibility state, and a focused preview. The profile menu opens Store, while a separate settings cog in Spotify's top bar opens LibreSpot's Look panel.
- Prism, Compact, and Accessibility can be previewed live without changing the saved profile. Ending the preview restores the previous runtime state. Community items use a validated `librespot://store` handoff that opens the exact theme, extension, or app in the desktop setup screen for review.
- Store tabs follow the standard keyboard pattern, expose their panel relationship to assistive technology, and retain visible focus. Reduced motion and Windows forced colors use the existing LibreSpot accessibility contracts.

- Prism is now a LibreSpot theme, not a separate project. It ships inside the package and installs from disk, so picking it needs no network and there is no release asset that can drift away from an older build's pin. Four schemes come with it: Dark, Light, OLED, and a genuine high-contrast one. It switches between light and dark on a clock you set, which no other theme manages because Spotify forces dark mode on its own browser and every theme that asks the OS gets the wrong answer. It takes the accent for the play button, progress bar, and highlights from the album art, and falls back to the scheme's own accent when Spotify's colour service is unavailable. It measures the frame rate once at startup and drops to cheaper effects by itself on a machine that cannot keep up, and reduced-motion users get the flat version straight away. Everything is in one dialog in the profile menu. Nothing in it touches ads, Premium state, telemetry, or any binary, and removing the theme puts everything back.

  Each of the theme's files is pinned by SHA256 in the script, the desktop backend, and the shared data source, so a truncated or edited copy is refused rather than half-installed. `samples/spotx-custom-patches-prism.json` ships alongside it: two cosmetic tweaks in SpotX's own `patches.json` format that can be pasted into Custom Install > Custom Patches.

### Fixed

- Concurrent profile and settings saves now take turns replacing the same destination. Each write still uses its own temporary file, but Windows can no longer reject two simultaneous final moves with an intermittent access-denied error.
- Health no longer reports a broken Marketplace route when Marketplace is not selected. It shows the optional route as inactive and keeps an otherwise healthy LibreSpot setup green.
- Store switches to its horizontal navigation before Spotify's own side panels can squeeze the center column. At compact desktop sizes the heading, catalog summary, search, and theme preview stay readable without horizontal overflow, and the scrollable rail no longer shows a scrollbar track.

- Bundled assets were invisible to the install that needed them. The script hands the install work to a background runspace seeded with a fixed list of variables, and the folder the script is running from was not on it, so the lookup for a bundled copy fell through to whatever folder `powershell.exe` itself lives in and never found one. The custom app hid this by downloading instead; a bundled theme has nothing to fall back on. The folder is now published and handed to the runspace with the rest.

### Changed

- New configurations install LibreSpot Store with Prism and the Dark scheme, while the separate Spicetify Marketplace is off by default. Existing configurations that selected Marketplace still work. The former Extensions and Marketplace paths now redirect into Store.

- Every reviewed extension, theme and app now records whether it talks to Spotify's Web API, and the catalog page shows it. Since February 2026 an add-on that calls that API under its own developer registration works for five people and then stops working for everyone after them, so this is the difference between an add-on that keeps working and one that quietly dies. None of the reviewed assets is in that position: they either stay inside the Spotify app's own interfaces or make no Spotify call at all.

  Checking each one against its pinned source turned up two entries describing themselves too kindly. Bloom was listed as running entirely locally while it pulls two JavaScript libraries off public CDNs and runs them inside Spotify every time it loads, one of them from an address with no version in it, so it can change underneath you. Beautiful Lyrics was described as fetching lyrics from a service; the file that gets installed is a small loader whose real body is downloaded at startup from a server that chooses the version, which means the checksum covers almost none of what runs. Both now say so plainly.

- The accessibility of the desktop shell is now checked by a rule engine, not just by hand. Every build scans the Home, Settings and Maintenance screens with the same engine Accessibility Insights uses, with the window hidden so it never interrupts anything. One known finding is recorded with its reason, and anything new fails the build. A deliberately broken control is planted in a test-only screen so a scan that has quietly stopped working cannot be mistaken for a clean result.

- A changed Spotify flag now says so on its own row and can be put back on its own. Any flag holding a value you set is marked Custom and carries a Revert control that names the flag it undoes; reverting one restores the value Spotify sent for it and leaves every other change alone. The count of customized flags in the summary follows.

- Auto-reapply reacts to Spotify updating instead of waiting for the next check. The task still runs at logon and repeats every 30 minutes, but between repeats it now watches Spotify's own folders, so an update that lands a minute after a check is picked up in about a minute rather than up to half an hour later. If watching is not possible the repeat still covers it, and the task keeps running as a standard user with a single instance.

- The desktop download is less than half the size it was. Turning on single-file compression takes `LibreSpot-Desktop.exe` from about 176 MB to about 76 MB and the fleet CLI from about 76 MB to about 38 MB. It also starts faster the first time, not slower: the first launch has far less to read from disk than it costs to decompress, and later launches are about a quarter of a second slower. The measurements and the method behind that decision are recorded alongside the size budget.

- The live customization engine builds on the workspace's own bundler. Dropping `spicetify-creator`, an unreleased package with no repository that pinned a six-year-old esbuild, removes a second copy of the bundler from the lockfile and 807 lines of dependency graph with it. The bundle Spotify loads is unchanged: the build reproduces the same mount contract and checks for it, and the resulting archive is verified against the live client. Dependency health now audits the JavaScript tree as well as the NuGet one, and an accepted advisory needs an owner, a reason and a recheck date the same way a lagging package does.

### Fixed

- A theme or custom app that fails to install no longer finishes as a successful run. The run now names the asset and says it was not installed: the desktop shows the finish screen as needing attention with the asset on it, and the fleet CLI exits 13 and writes the same line to stderr. Everything else the run did still applied, so this reads as a warning and not a failure. This is how three reviewed catalog themes stayed broken without anyone noticing. The archive downloaded, its checksum verified, and only the copy failed, which was logged as a warning and then reported as a completed run.

- Spotify 1.2.94 through 1.2.96 no longer reads as unsupported for a reason that was not true. Two different limits had been collapsed into one number. Spicetify CLI 2.44.0 declares Windows and Microsoft Store support from Spotify 1.2.14 through 1.2.96; 1.2.93 is the newest build LibreSpot has checked with the rest of the pinned set. LibreSpot was reporting its own 1.2.93 ceiling as Spicetify's tested maximum, so anyone on a newer client was told their Spotify was unsupported and pointed at the wrong cause. The two are now separate values everywhere they are read, including the compatibility matrix, the install gate and the support bundle. A client inside what Spicetify declares but past what LibreSpot has checked reads as degraded with a next step, and only a client past 1.2.96 reads as unsupported.

- Screen readers no longer trip over the icons. Every icon in the desktop shell reached assistive technology as a text element whose entire content was one character from a symbol font, which reads as nothing at all, and there were ten of them across the three screens that get scanned. The icons are decorative. The control beside each one already carries the real name, so the glyphs are now kept out of the accessibility tree rather than named. They look exactly the same as before.

- A Microsoft Store install of Spotify is detected reliably on a busy machine. The check asked PowerShell for the answer in its output, and reading that output was capped at half a second measured after the command had already finished. On a loaded machine the read did not land in time, which looked exactly like no package being installed, so the heads-up about the Store build silently did not appear. The answer now comes back in the exit code, which nothing has to read, and the output cap scales with the time the command itself is given.

- The Lucid theme is marked as not installable rather than offered as working. Its pinned archive holds an empty `color.ini` stub at the root and keeps the real files under `remote/`, so no theme folder has a usable set and picking it did nothing. Comfy, Hazy and Lucid are also no longer described as local-only: each ships a loader whose CSS and script are fetched from a content network at a moving address every time Spotify starts, so the pinned commit and checksum cover the loader and not the code that runs. The catalog page now shows each asset's support state and the date it was last checked against the pinned Spotify build.
- The ad-block pin advance reached the customization catalog too. One copy still named the build that fails on Spotify 1.2.93, and that copy is what the desktop app and the in-Spotify panel read.
- Building a release no longer deletes whatever folder it is pointed at. `-PublishRelease` refuses a drive root, the repository, and any folder holding files a release build did not produce.

### Added

- The Health panel in Spotify can now back up and restore in one file. Back up copies out this profile together with the settings Marketplace keeps in its own database, which is what a cleared Spotify profile takes with it. Restore reads that file back and writes both halves. The same file imports in the desktop app as an ordinary profile, so one export covers both places. Nothing leaves the machine.

- `Build-Scripts.ps1 -PublishRelease` builds the whole release root in one command: it empties `publish`, publishes the desktop app and the fleet CLI as self-contained single-file executables, copies in the script and the live customization archive, and prints the size and checksum of each. The build pins the properties that make the output reproducible, and the release manifest records the SDK version, the commit, and that property set, so anyone can rebuild the same commit and compare. Publishing twice was checked: both .NET executables come out byte-identical. `LibreSpot.exe` is the exception because ps2exe does not build reproducibly, and the manifest says so.

### Fixed

- Three of the five reviewed community themes could not be installed at all. Catppuccin, Comfy, and Bloom keep their theme files in a subfolder, but the catalog said the files sat at the root of the archive, so LibreSpot downloaded the archive, verified its checksum, then found nothing to copy and carried on reporting success. Picking one of those themes did nothing and said it worked. All three now install and apply on the pinned client.
- The Spicetify-layer ad-block fallback was pinned to a build from before Spotify 1.2.93 and threw 138 errors on the client LibreSpot installs. It now uses the upstream fix for that release and loads cleanly.

### Changed

- The desktop app and the fleet CLI now report Marketplace 1.0.11 in their health guidance, matching the script. Six resource files and the UI smoke fixture still said 1.0.9.
- The README now says which lane each behaviour belongs to. Hiding Spotify windows happens in the script and the desktop app, keeping LibreSpot's own window on top is script-only, background runspaces are the script's mechanism while the desktop app uses a separate process, self-elevation belongs to the script and the compiled exe, and the per-architecture hashes cover the Spicetify download rather than LibreSpot's own x64 executables. Full removal is described as the seven phases the script actually logs, without the native-uninstaller step that was removed. The SpotX pin is identified by commit and date, with a note that `2.0` is LibreSpot's own adapter version because upstream's newest tag is 1.9. Two upstream facts were out of date: SpotX `main` now recommends Spotify 1.2.99, and Spicetify 2.44.0 declares Windows support through 1.2.96 while 1.2.93 stays the build LibreSpot has verified.

- The reviewed stack now installs Spicetify Marketplace 1.0.11 instead of 1.0.9. The two releases in between fixed the key migration and the settings write that ran too late during a reload, which is the failure people describe as losing their installed themes and extensions after a restart. Verified against the pinned Spotify 1.2.93.667 client: the new archive downloads and matches its checksum, the store page and the LibreSpot page both load, and the registered extensions, custom apps, engine state, route wiring, and Marketplace's own saved settings are byte-identical after a full client restart.

### Fixed

- A bundled archive that cannot be read, because antivirus or a parallel run is holding it open, no longer abandons the custom-app install. It falls through to the cache and the download like any other miss. The compiled `LibreSpot.exe` now finds an archive sitting beside it, which it could not before because PS2EXE leaves the script-root variable empty.
- The live customization app now installs from the release that carries it, or from the copy LibreSpot brings with it, instead of a file on the main branch. The old branch address served whatever the branch held at that moment, so rebuilding the archive broke the pinned checksum for every release already published and a fresh install refused to add the app. The desktop app and the fleet CLI now unpack their own copy and install it with no download at all, the script lane looks for `librespot-engine.zip` beside itself, and a local copy whose bytes do not match the pinned checksum is ignored rather than trusted. Releases now publish `librespot-engine.zip` with its own checksum line.

## [v4.2.0] (2026-09-02)

### Fixed

- Double-clicking a `.librespot` file or following a `librespot://` link now opens the Profile tools group so the selected card and any failure message are visible. The Update LibreSpot link only ever opens an https GitHub page for this repository; any other address in the release response or the local cache falls back to the releases page. Searching Settings scrolls to a newly opened group once instead of on every keystroke.
- `SIGNPATH.md`, the record of why LibreSpot ships unsigned, is now tracked. It was listed in `.gitignore`, so a fresh clone failed the two release-trust tests that read it and the security policy pointed at a file that only existed on the maintainer's machine.

### Added

- Settings now opens with the four choices most people change: the Spotify build (Auto by default), the theme, the Marketplace, and whether Spotify opens when the run finishes. Installation details, appearance details, playback and interface patches, advanced SpotX flags, live customization, extensions, apps, and profile tools each sit behind one labeled group that is one click away, with no nested groups. The page is a single column with one scrollbar, the apply action lives in a footer that is never clipped, and searching for a hidden option opens its group and scrolls it into view while clearing the search restores the groups you had opened. Every option still round-trips through the existing configuration and profile format.
- Home shows a quiet Update LibreSpot link when a newer stable release exists. The check runs in the background after startup without delaying the system snapshot, reads the latest stable GitHub release with a conditional request, keeps a 24-hour local cache, and never selects a prerelease, downloads anything, raises a toast, or takes focus. Offline, malformed, missing, and rate-limited responses use a valid cache or stay silent. The link sits below the primary action and never replaces it.

### Changed

- The security policy names v4.0.x and later as the supported stable line, with the desktop app, the fleet CLI, and the shipped `LibreSpot.ps1` covered together, and marks the standalone v3.7.x script releases as superseded. `SIGNPATH.md` and the blocked plan no longer describe signing as pending: entries built on a pending SignPath enrollment, a tag-triggered release workflow, a `v3.7.2` stable channel, or the old preview release are archived with their resolution, and the remaining blocked entries were reworded to match local, unsigned, immutable releases. A release-truth test fails if stable version metadata coexists with preview-only support wording or a pending signing claim.
- LibreSpot now closes Spotify the way Windows expects. Both the desktop restart and the PowerShell paths ask every Spotify window to close normally, wait up to five seconds, and only then force whatever is still running along with the windowless helper processes. Helpers and renderer children that follow the window down on their own are never counted as forced. Each fallback is written to the run log with the process name, PID, elapsed time, and reason, and the total wait stays bounded even when nothing responds. Table-driven fake-process tests cover close-before-force ordering, survivor-only forcing, cancellation, and parity between the desktop and PowerShell implementations.
- Quick Start now leads with the desktop app. The first path links straight to `LibreSpot-Desktop.exe` and `checksums.txt` from the latest stable release and explains the same-release hash and release attestation checks in plain language, with no command to paste. The verified PowerShell script and the fleet CLI stay documented under labeled Advanced and Managed disclosures, and the unverified `irm | iex` one-liner is gone from the README and the security policy. A regression test keeps the desktop path first, keeps command blocks behind the advanced section, checks that every Quick Start link targets the official repository, and matches the named assets against the release contract.

## [v4.1.2] (2026-09-01)

### Added

- The in-Spotify Tweaks panel now reads the current Home sections and Your Library rows from Spotify. It stores stable identities, moves the real elements, preserves their natural order, updates row indexes, and reapplies the saved order after navigation or a Spotify redraw.
- Fixed-accent mode now includes a native color picker. Its value updates the profile and the live `--librespot-accent` variable without a restart.

### Changed

- Snippet and preset previews now use current Spotify artwork with Spotify's own icon set as the fallback. The old CSS drawings and text symbols are gone.
- Cover-art shapes are mutually exclusive. The reviewed snippet selectors now match Spotify 1.2.93 for track rows, entity art, sidebar art, the Now Playing cards, progress bars, and top-bar controls.
- Form controls carry explicit accessible names, slider targets are 32 pixels tall, source links have a 24-pixel target, and the internal `HighContrast` identifier is shown as `High contrast`.

### Fixed

- React form events no longer get rejected as non-browser events. Feature search, preset naming, selects, sliders, schedule times, disclosures, saved presets, profile copy, and staged Spicetify options now retain and apply their values in the production custom app.
- Removing a live feature override restores the value Spotify received from its remote configuration. The Features panel also provides one reset action when any custom values are present.
- LibreSpot notifications now use Spotify's Snackbar API and coalesce rapid updates. Prism waits for the React, menu, and modal APIs before registering its settings entry, which removes the startup race seen after a full client reload.
- Light mode keeps playlist headers readable over Spotify's dynamic art color. Action-bar controls, the playbar, the About the artist heading, and default notifications now use the correct palette tokens.
- Health checks now recognize extensions bundled with the verified Spicetify CLI as installed. The dashboard no longer asks for a reapply when those files are correctly present in Spicetify's program folder instead of its user configuration folder.
- Fleet plans now honor the effective `installMode` from the selected answer-file profile. A reapply plan is labeled and targeted as reapply instead of being reported as a fresh install.
- Config schema 2 migrates legacy Easy profiles to include the LibreSpot live custom app. Existing Custom profiles keep their explicit app choices, including an intentionally empty list.

## [v4.1.1] (2026-09-01)

### Added

- The shared customization catalog now carries every one of the 348 flags found in the pinned Spotify 1.2.93 `xpui.js`, including descriptions, defaults, value types, bounds, choices, groups, server-gated markers, and 104 SpotX-forced defaults. It also records all 31 SpotX controls, 21 supported Spicetify settings, 12 reviewed CSS snippets, three built-in themes, four presets, and the reviewed extension, app, and theme catalogs. The Spotify app renders the complete data set, saves user presets, and keeps unknown profile fields during import and export. Catalog truth checks exact source hashes and extracted metadata from both JavaScript and the root build gate.
- LibreSpot now builds as a Spicetify custom app with a companion extension that keeps the live engine present on every route. Its six-panel rail covers Look, reviewed tweaks, captured Spotify feature flags, staged SpotX switches, installed extensions and apps, whole-profile presets, and named health checks. Scheme previews, four independent style layers, effects tiers, dynamic accents, per-region scale, schedules, snippet toggles, profile copy, route repair handoff, and diagnostics work through native Spotify controls. The production build checks the manifest, extension registration, render contract, and minified assets. An offscreen Chromium runner exercises Dark, Light, OLED, and high-contrast surfaces from 700 to 1920 pixels.
- The in-Spotify customization engine now has a typed runtime core. It parses and exports multi-scheme `color.ini` files, derives missing Spicetify colors, injects one managed live palette, composes palette, layout, effects, and accessibility layers, responds to schedule and accent signals, applies client feature overrides through Spotify's debug API, and reports missing anchors or custom-app routes by name. A single-worker Vitest suite covers direct color round trips, live DOM changes, layer cleanup, Material palettes, profile export, schedules, feature flags, and self-test failures.
- The desktop app installs the LibreSpot custom app and companion extension through its reviewed asset path. Custom Install reads the same flags and snippets as Spotify, profiles carry live engine state without dropping unknown fields, Maintenance names engine health failures, and the update watcher reinstalls the app before repairing both managed routes.
- Prism, Compact, and Accessibility ship as composable layer themes. OLED, Accessibility, Compact, and Performance presets fill the same editable state used by manual controls and `.librespot` profiles.

### Changed

- The Spotify workspace now uses one compact corner scale across buttons, badges, switches, and cards. Selected rows carry an accent edge, small labels are larger, and controls have larger targets. The narrow layout switches to a horizontal rail at 780 pixels, so header actions no longer collapse into tall pills.
- The Tweaks catalog uses two columns at desktop widths, which shortens the page while keeping each preview and its source metadata readable. Built-in presets use quieter secondary actions so Save current remains the clear primary action.
- The in-app brand mark now comes from the same reviewed SVG used by the custom-app manifest. The build stages that source as text for Spicetify Creator, embeds it as an accent-aware mask, and removes the temporary file after bundling.
- The Spotify Features panel now opens as a compact group index instead of rendering all 351 flags in one 29,000-pixel page. Search opens matching groups, each summary shows its count and any custom values, and SpotX settings remain available in their own collapsible group.
- The Extensions panel keeps its 21 advanced Spicetify options behind one native disclosure. Installed extensions and custom apps stay visible first, while the full configuration remains one click away.
- Home now chooses one action from the latest environment snapshot. A new machine gets Recommended Setup, a healthy managed stack gets Open Spotify, and a degraded stack gets its first safe repair in critical-then-warning order. If recovery is destructive, Home opens Maintenance without running the action. Loading disables the button, Retry owns the failed-check state, and overlapping refreshes cannot restore stale copy or commands.
- Maintenance now leads with overall status, the most important issue, and one safe repair. Diagnostics stay available in a collapsed section, reset actions have their own collapsed danger section, healthy systems say no action is needed, and failed checks offer Retry.
- Reapply now chooses its Spicetify command from verified local state. A complete current-version backup uses `apply --no-restart --bypass-admin`; a new or stale setup uses `backup apply --bypass-admin`. Spotify's four-part file version is compared with Spicetify's matching `.g<hash>` form.
- The live audit used Spotify 1.2.93.667 and Spicetify 2.44.0 through the real desktop backend. Scheme, layer, flag, snippet, preset, Marketplace, and eight health checks changed or loaded without a restart. Thirty-six scheme, tier, and viewport combinations had no horizontal overflow or clipped LibreSpot controls.

### Fixed

- The bundled live engine now writes v4.1.1 into its shipped manifest and validates that value during both build and ZIP packaging. A release-integrity test hashes the tracked archive and compares it with all three installer pins, so rebuilding the custom app can no longer leave installs pointing at a stale SHA256.
- Extension and custom-app totals now count only IDs represented by LibreSpot's managed catalogs. An unrelated registered app such as Marketplace no longer makes an available card look installed.
- Reversible changes and the run log now use separate columns in the activity dialog. The log keeps a 160-pixel minimum height, and an offscreen UI test fails if its rendered bounds collapse again.
- The scheduled dependency review now reflects the September scan. It found no vulnerable packages and no outdated direct dependencies; the accepted version lag remains limited to test-only packages that are not present in release artifacts.
- The run activity dialog no longer squeezes recovery guidance and long log paths beside four action buttons. Guidance and the path span the dialog above a wrapping action row, which keeps translated button labels intact.
- Secure Windows defaults are no longer reported as degraded health. LibreSpot no longer probes for or recommends Defender exclusions, does not tell users to turn off Smart App Control or bypass SmartScreen, and treats an antivirus detection as unresolved until the official source and same-release hash are verified. Evidence-based quarantine guidance still points to Protection History and vendor analysis, while the separate SpotX supply-chain guard continues to reject scripts that can change Defender settings.
- Managed custom-app repair now assigns a different `spicetifyAppN` identifier to each route and extends the existing CSS gate after quoted route chunks appear. Marketplace and LibreSpot can load together without a duplicate JavaScript declaration or missing second-app styles.
- Live palette rules use a root class so late Spicetify styles no longer hide a selected scheme. The Light scheme now changes the computed main surface to `#FFFFFF` immediately.
- Engine health reads the client version from `Spicetify.Platform` before local fallback state, which makes the pinned Spotify check accurate on a real client.

## [v4.0.0] (2026-08-22)

### Changed

- v4.0.0 takes the v4 desktop app and the fleet CLI out of preview and becomes the public latest stable release. The code is what shipped in v4.0.0-preview.29; what changes here is the identity. Version strings across the desktop, CLI, and Core projects drop the preview suffix, the release manifest is generated on the stable channel, and the README badges, release claims, and verification commands name v4.0.0. The release ships the same seven assets, including the source v3.7.4 PowerShell script and its PS2EXE build.

## [v4.0.0-preview.29] (2026-08-22)

### Fixed

- The `RestoreVanilla` maintenance action now says what it actually does in every supported locale: it removes active Spicetify customizations while keeping SpotX in place. Its description and prompt also say that it does not restore an unpatched Spotify client.
- The Home rail now says eligible changes have backups instead of promising that every change can be reversed.
- Backend progress and success text now use the same factual recovery language without changing the `RestoreVanilla` action ID or its behavior.

## [v4.0.0-preview.28] (2026-08-21)

### Added

- `Build-Scripts.ps1 -CatalogTruth` regenerates the community catalog and compares it with `catalog.json` on the published `gh-pages` branch, so a revoked review or a changed pin cannot keep advertising stale trust evidence on the public page. The published catalog now carries a SHA256 of each source schema, so an edit to a manifest field the page does not render is caught as well. `-Validate` runs the same check against whatever `origin/gh-pages` the clone already has and warns rather than failing when that ref is missing, so offline runs still pass.

### Fixed

- The desktop shell shows its version again. The simplified shell had collapsed every surface that displayed it, so the app reported its own version nowhere; it now sits under the LibreSpot name in the navigation rail. Crash reports and the startup log line also record the full preview version instead of the shorter numeric assembly version.

- Home now offers a Retry button when the environment check fails. The failure text pointed at a control that did not exist, and the command behind it was reachable only through the undocumented F5 accelerator, so restarting the app was the only obvious way out.
- Maintenance uses the same unavailable copy and Retry control when that check fails, instead of looking like a PC that simply does not have Spotify.
- A bad shared-profile link no longer pretends the environment check failed. Opening a malformed `librespot://profile` URI after startup threw into the window-load catch, which flipped Home into the snapshot-error state even though the PC probe had already succeeded.
- Crash reports fall back to the temp directory when `%LocalAppData%\LibreSpot\crashes` cannot be created or written, so the crash dialog still appears under Controlled Folder Access or a full disk. Opening the folder from that dialog now targets the directory that actually received the report.
- Home no longer clips at the minimum window size. The readiness checks were pinned wider than the available space, so both edges were cut off with horizontal scrolling disabled.
- Five failure messages now name something you can do next instead of stopping at the failure: a refused undo, an unknown backend failure, a failed support-bundle export, a failed configuration save, and an unavailable profile comparison. The crash dialog leads with "what went wrong" rather than "exception summary", and its no-message fallback points at the report folder. All five locales.
- Every format string now tells a translator what each `{n}` stands for. Around half the resource comments were the same two boilerplate lines, and the keys carrying placeholders are exactly the ones a translation can break.
- Punctuation in the interface is consistent. Progress and status text used three dots where the rest of the app used a real ellipsis, and eleven strings joined two clauses with a spaced hyphen standing in for a dash. All five locales.
- One term per concept: "Spotify build" and "Spotify version" named the same thing on different screens, as did "Premium account patch posture" and "Premium account mode". Both now use the second form everywhere.
- Corrected user-facing copy on the primary paths: the risk prompt and first-run notes named a reset action that does not exist (in English by case, and in Portuguese and Spanish by using a different word than the action's own title), the attention headline claimed a single item when several can be waiting, and a possessive was missing from the remove-data prompt.
- The taskbar Jump List now uses Home and Settings, matching the rail. It still opened those workspaces, but the labels were leftover Recommended and Custom names from before the shell was simplified.
- Settings search and theme gallery search now show placeholder text inside the empty boxes. The theme box had sat under a "Theme pack" label with no hint that it filters the gallery.
- Disabled rail buttons keep a readable label. They used to fade already-muted text down to about 1.6:1 contrast, including in high contrast where GrayText was then dimmed again.
- The Home failure hero shows an information glyph instead of a stray "Z". The critical-attention hero uses a working error glyph at the same size.
- Profile and config saves no longer share a process-id temp file. Concurrent saves of the same profile could clobber each other, and undo receipts could be truncated on a crash because they skipped a disk flush.
- Environment probes now launch Windows PowerShell from System32, matching the backend launcher, instead of whatever `powershell.exe` is first on PATH.
- The window title bar reads caption, text, and border colors from the palette, so it tracks the shell canvas instead of leftover green-era hex values.
- Aligned the Bloom catalog checklist with the reviewed manifest. The checklist still accepted it after the review deferred it for stale maintenance.
- Blocked WPF-UI implicit styles from leaking into the snackbar close button, scrollbar page buttons, and the Home details expander toggle. Those were the same class of leak that collapsed every dropdown in preview.26.
- Disabled primary, secondary, checkbox, and dropdown controls keep a readable label. They used to fade the whole control with opacity, which dropped filled-button text well below the 3:1 floor and dimmed GrayText again in high contrast.
- Export and activation staging writes go through the same atomic helper as config and profile saves, so a crash mid-export cannot leave a truncated `plan.json`.
- The community catalog generator decodes the asset schemas as UTF-8. Under Windows PowerShell 5.1 it read them in the ANSI codepage, so every em dash in a review note reached the published page as mojibake.
- Spotify version strings are read the same way everywhere. Five parsers in Core disagreed about `v` prefixes, prerelease suffixes, and what to do with a fourth component, so the same detected build could be judged supported by one surface and out of range by another.
- A Spicetify or Spotify version reported with a trailing note, such as `1.2.3 rc1` from a vendor file-version field, is read again instead of being treated as unreadable. Trailing junk past the fourth component now invalidates the whole string rather than being ignored, so the compatibility verdict and the release-tag ordering agree about what is a version.
- A partial Spicetify report like `3.0` is called unsupported rather than unknown. The major-version guard could already read it; the compatibility verdict could not, so the two surfaces disagreed about the same string.
- Turning Windows high contrast on while the app is running now flattens the card shadows and the readiness glow and recolors the focus ring. Those were resolved once at load, so they kept their dark-palette instances until the next restart.
- Screenshots captured with the forced high-contrast flag show the window a high-contrast user actually sees. The chrome read the Windows setting rather than the loaded palette, so those captures came out with the Mica backdrop and a transparent background.
- The catalog generator carries a UTF-8 byte order mark, so the interpunct in the page footer publishes correctly from Windows PowerShell 5.1 instead of as mojibake. The two hosts now produce the same page.
- Disabled card lists and the activity dock toggle mute their label instead of fading the whole control. The colour-scheme chips were the last surface still using a 0.45 opacity composite, which multiplied a caption that was already near the floor and dimmed GrayText a second time in high contrast.
- The local profiles list fades at its bottom edge, so a card that runs past the viewport reads as scrollable rather than as a title cut in half. High contrast leaves the edge flat and relies on the scrollbar.
- The documented local test command no longer skips its own lints. `--filter-not-method "*Wpf*"` was there to avoid the tests that launch the shell, but it matched on name, so twelve file-reading gates never ran, one of them red. Every shell-launching test already lives in a `Wpf*` class, so the class filter alone does the job and a new gate keeps it that way.
- Twenty-one more tests joined that run. The template contract gate only reads files, and the UI integration tests open no window, but both were named `Wpf*` and so were skipped by the class filter too. The suite goes from 1,011 to 1,032.
- Two font sizes in the navigation rail were off the product type scale. The app title read 23 and the reversible-setup shield glyph 22; both are now 24.
- Seven more sizes on the Home workspace were off the scale as well: three labels at 15, a subtitle at 17, two labels at 19, and a play glyph at 25. The design gates that were meant to catch this only read two of the seventeen XAML files; they read all of them now.

### Changed

- PowerShell regressions now execute all eight install, apply, and reset orchestration bodies against an isolated Spotify tree. The same Pester fixture invokes the real Recommended Setup, Reapply, and Full Reset click-handler bodies without opening WPF or touching an installed Spotify copy. The suite grows from 203 to 213 tests.
- Uninstall help now says `--purge` needs `--yes` or `--silent`, matching the actual CLI guard.
- Removed the `--accept-eula` fleet CLI flag. It was parsed but never read, while the CLI contract called it required for silent installs. Consent is recorded where it always actually was: `eulaAccepted` and `riskAcknowledged` in the answer file, both still required. Scripts passing the flag now fail with exit code 2 and an unsupported-flag message, so drop it from the command line.
- Release builds now enforce what the schemas already documented. The publish footprint budget is measured and recorded in the release manifest, the stable script executable is compiled by `Build-Scripts.ps1 -CompileStableExe` with pinned flags and its file version checked against the script, `Build-Scripts.ps1 -GenerateSbom` restores the pinned CycloneDX 6.2.0 local tool, and the README screenshot gate verifies capture size, theme, and language instead of version metadata alone.
- Dropped 13 unused localized strings left over from the removed elevation-relaunch flow.
- Removed unused ViewModel and Core members that had no callers, and derived the Stats custom-app release tag from its version so the two strings cannot drift.

## [v4.0.0-preview.27] (2026-08-21)

### Fixed

- Repaired every themed dropdown in the shell. Loading the Fluent controls dictionary at startup in preview.26 introduced an implicit toggle-button style that leaked into the custom combo-box template, so each dropdown collapsed to a content-sized pill with its text spilling past the edge. The template now blocks implicit styling, and the Spotify build, download path, theme, and language dropdowns render at full width with their chevrons again.
- Restored the language picker to the desktop. The simplified shell introduced in preview.26 collapsed the only two language selectors, so none of the five translated interfaces could be selected from the running app. The picker now sits in the navigation rail above the reversible-changes note, and the unused collapsed selector in the window command bar was removed.

### Changed

- Corrected the UI automation smoke contract to the shipped navigation taxonomy. The `recommended` and `custom` states now expect Home and Settings, matching the rail labels and the FlaUI smoke tests that were already updated in preview.26. The `provenance` and `global-search` rows were removed because preview.26 collapsed both surfaces; the decision to port them into the simplified shell or delete them is recorded in the blocked roadmap.
- Pointed the custom-patch-editor and nested-scroll UI tests at the extracted section views. Both still asserted against `MainWindow.xaml` after the structural split moved that markup into `Views/CustomPatchesSection.xaml`, `Views/CustomAppearanceSection.xaml`, `Views/CustomProfileSummarySection.xaml`, and `Views/WorkspaceViewInteraction.cs`.
- Removed a QA-matrix assertion that required the reduced-motion state to run only in the dark theme. The same file generates reduced-motion captures for both themes, so the assertion contradicted its own matrix and failed every high-contrast row.

## [v4.0.0-preview.26] (2026-08-20)

### Security

- Added a non-blocking PowerShell 7 security-floor preflight. PowerShell 7.6.0 through 7.6.4 now warn with CVE-2026-50523 and the fixed 7.6.5 version, while 7.6.5 and later remain silent.
- Raised the self-contained .NET runtime floor to 10.0.11 and refreshed the dependency-health rationale with the August 11, 2026 ten-CVE servicing batch, including two remote-code-execution fixes.
- Added an explicit SpotX post-Defender pin policy at upstream commit `afb4c3fc`. Candidate review now requires the declared and passed `-defender_exclusions_off` switch before a changed pin can be accepted.
- Added a schema-v2 Spicetify v3 support contract fixture. Detected v3 CLIs now classify allowlisted, degraded, refused, and unavailable states while the pinned v2.44.0 path remains unchanged.
- Changed missing or malformed v3 support data to fail closed. Install, reapply, repair, and direct Spicetify mutation paths now stop with the `spicetify restore` recovery path until a valid allowlist is available.
- Removed the remaining Dependabot branches and release-note exclusions. Repository vulnerability alerts remain disabled by policy.
- Updated both .NET test projects to Microsoft.NET.Test.Sdk 18.9.0, xUnit.v3 4.0.0, and the xUnit Visual Studio runner 4.0.0. The desktop project also uses FsCheck.Xunit.v3 3.4.0. Both projects now opt into the Microsoft Testing Platform v2 runner through the repository's global SDK configuration.
- Pinned the local PSScriptAnalyzer lint gate to 1.25.0 and recorded the explicit decision to keep `PSUseConstrainedLanguageMode` disabled after a zero-diagnostic triage run against both hosts.
- Added a localized Maintenance compatibility verdict matrix. It compares detected Spotify, SpotX, Spicetify CLI, and Marketplace state with the pinned tuple, labels supported/degraded/unsupported/unknown conditions, and gives a next step for each state. The Core report, view-model projection, and WPF screenshot matrix are covered by regression tests.

### Changed

- Rebuilt the desktop around a three-item Home, Maintenance, and Settings rail. Home now shows one readiness summary, four plain-language checks, one recommended action, and optional Details instead of the command bar, inspector, dashboard cards, and activity dock.
- Removed global keyboard shortcuts from the desktop shell. Every common action remains available through a named on-screen control.
- Unified the desktop workspace vocabulary around Recommended, Custom, and Maintenance. Jump List entries and per-user `librespot://` and `.librespot` registration descriptions now use localized resources after the saved interface culture is loaded.
- Updated `LibreSpot.Cli --help` to list every flag in `schemas/fleet-cli-contract.json` per verb, with the `--purge` and `--yes` uninstall requirements stated explicitly.
- Split the Custom workspace into PascalCase-paired per-section UserControls and moved maintenance, custom-install, and profile members into view-model partials. Existing UIA, localization, focus, and behavior contracts remain unchanged.
- Added a short fake-installer warning and download-verification checklist to README and SECURITY. It points users to the official repository, same-release SHA256 checksums, and away from Telegram, rehosted files, Defender changes, and pasted commands.
- Aligned markdown tracking guidance with the files actually versioned in the repository, refreshed the parity manifest version to preview.26, and removed the duplicate ignored `Roadmap_Blocks.md` file in favor of `Roadmap_Blocked.md`.
- Renamed the shell's stack-status bindings and replaced release-freshness copy with localized detected/not-detected maintenance guidance.
- Expanded the WPF QA matrix with destructive-prompt, active-run cancellation, and dark-theme reduced-motion states, with capture metadata proving motion suppression.
- Derived the shell display version from the desktop assembly informational version so project version bumps update the chrome automatically.
- Aligned the fleet distribution matrix with the implemented CLI contract and removed the obsolete blocked shell-integration design item.
- Added a localized Recommended first-run checklist with setup contents, update blocking, risk confirmation, duration, and Full Reset recovery guidance above the environment tables.
- Added an explicit capability boundary to README and SECURITY. LibreSpot does not grant Premium or provide Spotify's account-controlled catalog features; Premium users can still use the skip-ad-blocking path.
- Added a locally generated community catalog site that lists every reviewed asset with provenance, license, SHA256 verification, review date, and evidence links.
- Published the generated community catalog to [GitHub Pages](https://sysadmindoc.github.io/LibreSpot/) from the reviewed `gh-pages` branch.

### Fixed

- Loaded the Fluent control resources and shared workspace templates at application startup so icons, Custom settings, and Maintenance cards render reliably when the workspaces are hosted as separate controls.
- Updated the WPF QA launcher for the Microsoft Testing Platform v2 runner so its state filter executes the matrix instead of returning a zero-test result.
- Added a Spicetify v3 coexistence guard. LibreSpot now detects the v3 backup artifact, module and hook directories, and newer CLI majors before install, apply, repair, or reapply operations. The WPF health report records the conflict and points to `spicetify restore`; the recovery command remains available.
- Added a pinned Core-only Stryker.NET 4.16.0 mutation pilot using the preview MTP runner with xUnit v3. The reproducible baseline is 24.32% across 1,476 tested mutants, with a 24% break threshold; the report records 355 killed mutants and the run command is documented in the repository.

## [v4.0.0-preview.25] (2026-08-11)

### Fixed

- Restored the direct LibreSpot.Core project reference in the desktop test project so the shared models, services, and localization resources compile after the Core extraction.
- Aligned the release-artifact, distribution-matrix, and Scorecard metadata with the unsigned-by-design policy: release trust is represented by SHA256 checksums, the release manifest, and the SBOM rather than a pending Authenticode enrollment.

### Changed

- Added a Windows GitHub Actions quality gate for PowerShell lint/validation, Pester regressions, the desktop build, and non-WPF .NET tests; release and UI automation workflows remain separate concerns.
- Added Marketplace state export/recovery actions: Reapply and Repair Marketplace create a retained, validated local archive before mutation, while the CLI can restore missing Marketplace files from the newest archive. Marketplace 1.0.9 IndexedDB state is now reported as detected but not backed up, with Marketplace's own export/import controls named as the recovery path.
- Added catalog-review enforcement for community assets: manifest entries now carry dated last-push/archive/evidence decisions, stale or invalid easy-mode entries are rejected by deterministic Core policy, deferred assets remain opt-in, and drift/health output includes the review reason.
- Added a fixture-backed compatibility baseline for the pinned SpotX/Spotify, Spicetify CLI, Marketplace, and theme tuple; `Build-Scripts.ps1 -Validate` now rejects drift between the PowerShell pins, Core catalog constants, and the supported Spotify range.
- Extracted the three actual WPF workspace tabs into dedicated UserControls while preserving automation IDs, keyboard-focus contracts, localized resources, and navigation behavior.
- Added multi-user isolation regression coverage for per-user registry state, configuration, profiles, backups, logs, crash reports, elevation boundaries, and executable paths.

## [v4.0.0-preview.24] (2026-07-24)

### Changed

- Completed the `LibreSpot.Core` extraction (RD-35): all non-UI logic shared by the desktop shell and the fleet CLI, the environment-snapshot derivation, upstream/community drift comparison, undo-policy evaluation, backend orchestration, support-bundle projection, the app catalog/models, and the `Strings` localization resources (with their culture satellites), now lives once in the WPF-free `LibreSpot.Core` library instead of being compiled into both assemblies. The CLI carries zero source-file links and consumes everything through a project reference, and the test project's per-assembly type aliasing is gone. Because Core builds `net10.0-windows` with no `UseWPF`, it is a valid Stryker.NET mutation-testing target (the desktop project is not), which unblocks the filed Stryker item. Verified with the full xUnit suite, the offscreen UI-automation shell smoke suite (real rendered shell resolving Core-owned localized resources across cultures), a CLI smoke run, and the localization/release gates.

## [v4.0.0-preview.23] (2026-07-24)

### Changed

- Began extracting non-UI logic into a new WPF-free `LibreSpot.Core` class library (net10.0-windows, no `UseWPF`) shared by the desktop shell and the fleet CLI via project reference. `OperationCorrelation` (ETW/EventSource operation-id correlation) and `OperationJournalUndoService` (undo-policy evaluation, with its `operation-token-types.json` / `run-receipt-format.json` schemas) now live in Core; namespaces are preserved so all call sites and the xUnit suite are unchanged. Because Core sets no `UseWPF`, it is a valid Stryker.NET mutation-testing target, which the `UseWPF` desktop project is not, the prerequisite the filed Stryker item was blocked on. Remaining service migrations (drift comparison, snapshot derivation) depend on relocating the shared `Strings` localization ownership and stay tracked in ROADMAP.

## [v4.0.0-preview.22] (2026-07-24)

### Security

- Documented GitHub immutable-release verification alongside SHA256 checksums. New releases record the GitHub release attestation contract and can be checked with `gh release verify` plus `gh release verify-asset`.
- The pinned Spicetify CLI download now optionally verifies GitHub build-provenance attestations in addition to the mandatory SHA256 hash. After the hash check, when the GitHub CLI is present LibreSpot runs `gh attestation verify` against a cached signer identity (`spicetify/cli`, the workflow cert-identity regex, and the GitHub Actions OIDC issuer, pinned in `PinnedReleases`). A genuine provenance failure surfaces as a trust warning; when the tooling, network, or authentication is unavailable the check degrades silently to SHA256-only and never fails the install closed. New shared modules `Test-SpicetifyCliAttestation` (orchestrator) and `Get-SpicetifyAttestationVerdict` (pure exit-code/output classifier) with Pester coverage.

## [v4.0.0-preview.21] (2026-07-24)

### Documentation

- Aligned the code-signing docs with the unsigned-by-design posture. The README SmartScreen/signing FAQ, the "Signing & verification" section, `SECURITY.md`, and `SIGNPATH.md` no longer tell users a signed build is "pending" or coming "once the cert arrives", SignPath Foundation OSS signing was evaluated and set aside, so releases ship unsigned and the SHA256 `checksums.txt` is the permanent verification path. `SIGNPATH.md` is reframed as a historical evaluation record rather than an active enrollment plan.
- Hardened the antivirus false-positive guidance: the README AV FAQ now recommends the compiled `LibreSpot-Desktop.exe` over the raw `LibreSpot.ps1` (the Powdow-class heuristic surface) and shows how to confirm any asset by matching its `checksums.txt` SHA256 on VirusTotal; `SECURITY.md` documents submitting each shipped artifact hash to the Microsoft Defender false-positive portal as a post-upload release step.

### Changed

- The SpotX pin-advance guardrail now accounts for Spicetify's hard-fail-on-unsupported-version gate. `spicetify/cli` `main` merged a change after 2.44.0 (PRs #3894/#3895/#3896) that makes `backup apply` refuse, rather than best-effort patch, any Spotify version above the CLI's declared ceiling, which permits LibreSpot's post-apply route re-wiring to work on Spotify 1.2.94. `AppCatalog.PinnedSpotXHoldRationale`, the README compatibility note, and the compatibility-matrix warning now require a pin advance to confirm the newer Spicetify build still applies (does not hard-refuse) on the new Spotify target, not just re-validate CSS class maps. The pinned 2.44.0 predates the gate, so current behavior is unchanged.
- Refreshed the `dotnetRuntimeFloor` rationale in `schemas/dependency-health-allowlist.json` to enumerate the 2026-07-14 .NET 10.0.10 servicing batch it actually clears (RCE CVE-2026-50646/-50649, signature-bypass CVE-2026-47304, EoP CVE-2026-50650, and CVE-2026-50526, alongside the earlier CVE-2026-32175/-26127/-45490 fixes). The floor version is unchanged at 10.0.10.

## [v4.0.0-preview.20] (2026-07-24)

### Fixed

- The Microsoft Store Spotify presence probe and the Windows Defender exclusion-status probe (`EnvironmentSnapshotService`) could block with no time bound. Both started an async `ReadToEnd` on the child process's stdout, then read `Task.Result` unconditionally after a 500 ms `Task.WaitAll` cap. If a grandchild process inherited the stdout handle and kept it open, the read never completed and the illusory cap was bypassed, hanging the environment snapshot. Both probes now only read the drained output when the tasks actually completed and treat a timed-out drain as the safe default (not present / unavailable).

### Accessibility

- Added a WCAG AA contrast regression gate for the text-on-fill token pairs (`TextOnAccent`/`AccentColor`, `TextOnDanger`/`DangerFillColor`, `TextOnWarning`/`WarningFillColor`). The palette documented these primary-CTA, destructive, and caution button contrasts as clearing 4.5:1, but only text-over-surface tiers were tested; a future edit to a fill or its on-fill text could have silently dropped a button below the accessibility floor. The pairing is now verified in `ThemeManagerTests`.

## [v4.0.0-preview.19] (2026-07-23)

### Changed

- The end-of-install launch now burns a hidden ~15-second warm-up session before the visible Spotify launch. The first patched session performs heavy one-time initialization (fresh xpui extraction, CEF/GPU caches, Spicetify wrapper warmup) and could sit frozen for about ten seconds right as users signed in, until a manual restart cleared it. LibreSpot now performs that close-and-reopen automatically. The warm-up window stays hidden (WPF window watcher / monolith Hide-SpotifyWindows loop), so the session users actually see and sign in to starts responsive.

### Fixed

- Fixed the blank Marketplace store page. Root cause: SpotX serves the combined `Apps/xpui/xpui.js` bundle (its patches only take effect when `index.html` loads it), while Spicetify v2.44.0 wires custom-app routes into `xpui-modules.js` and the chunk map into `xpui-snapshot.js`. The page never loads those files in that layout. The `/marketplace` route therefore mounted a `React.lazy` chunk the live webpack runtime could not start, suspending forever behind a `fallback:null` Suspense with zero console errors. New `Repair-SpicetifyCustomAppWiring` ports the Spicetify CLI's own injection (route element, lazy chunk loader, chunk-name maps, and the miniCss gate from `src/apply/apply.go`) onto the bundle `index.html` actually references, with a pre-patch backup, strict anchor matching (no write unless every required anchor matched), and idempotent re-runs. Every successful `spicetify backup apply` in both hosts now re-wires the route automatically when Marketplace is enabled. Verified live on Spotify 1.2.93.667, the store page now renders the full catalog.
- Added a `RouteNotWired` Marketplace health state across the shared PowerShell health model and the WPF stack-health snapshot (all six locales): when the live Spotify bundle never references the store chunk, health now says the store page is not wired and points at Repair Marketplace instead of reporting a healthy install with a blank store.

## [v4.0.0-preview.18] (2026-07-23)

### Security

- Guarded every patch-detection surface against a future Spicetify v3 (spicetify/cli#3038), whose symlink + hooks + modules on-disk contract LibreSpot's 2.x detection does not understand. When the installed Spicetify CLI reports a major newer than 2, the WPF/CLI stack-health Spicetify component now shows a localized "Unsupported version" warning (all six locales), and the shared PowerShell diagnostic snapshot reports `spicetify_cli_supported=false`, instead of misreporting a healthy Spotify as broken/unpatched. Unknown or unparseable versions are treated as supported so a missing probe never raises a false warning.
- Re-verified upstream pins (2026-07-22) and recorded the deliberate hold at the pre-Defender SpotX commit `550bc72c`/Spotify 1.2.93: SpotX `main` now targets 1.2.94 and adds Microsoft Defender exclusions by default (commit `afb4c3f`), while Spicetify CLI 2.44.0 still caps at 1.2.93. `AppCatalog.PinnedSpotXHoldRationale` and the README compatibility matrix document the advance trigger (Spicetify 1.2.94+ support) and the required `-defender_exclusions_off` opt-out.
- Set `TargetLatestRuntimePatch` on the self-contained .NET 10 desktop and CLI projects and added a `dotnetRuntimeFloor` gate to `Build-Scripts.ps1 -DependencyHealth`, so release preflight records the resolved `Microsoft.NETCore.App` / `Microsoft.WindowsDesktop.App` patch level and fails when the build host is below the documented CVE-patched floor (CVE-2026-32175/26127/45490/50526). Self-contained artifacts embed the runtime, so this ensures shipped builds carry current .NET servicing fixes.

- Added a fail-closed executable-undo policy: only explicitly selected, low-risk user-PATH additions with an exact protected before/after snapshot can run; stale, unknown, elevated, destructive, reparse-point, missing-state, and non-allowlisted tokens are refused without mutation.
- Omitted previous-state references and nested previous/old-value payloads from operation-journal support exports, while retaining independently useful redacted evidence.
- Added a fail-closed external-script gate for Microsoft Defender preference/exclusion mutations: the current safe SpotX pin remains argument-compatible, while any future mutating pin must declare and receive the exact upstream opt-out across interactive, backend, watcher, repair, and cached execution lanes.
- Bounded CLI answer files, desktop/CLI configuration JSON, run receipts, operation-journal recovery, and support-bundle diagnostic windows; oversized or tampered local state now fails safely or reads only a 1 MiB tail instead of driving unbounded parsing or full-file scans.
- Redacted every string value in support-bundle JSON at the serializer boundary, so newly added diagnostic fields cannot leak local paths, credentials, or command-line secrets when a projection omits a field-specific redaction call.
- Preserved raw expandable user-PATH tokens and `REG_EXPAND_SZ` typing across Spicetify installs/removals, broadcast PATH changes to running shells, and replaced recursive ACL/delete operations with a depth-first removal engine that unlinks nested junctions without traversing their targets.
- Guarded the two "import from an HTTPS URL" surfaces (custom patches and shared profiles) against SSRF: both fetches now validate the resolved IP at socket-connect time and refuse loopback, link-local, RFC1918, CGNAT, unique-local, and cloud-metadata addresses across redirect hops. These fetches can be triggered without confirmation via `librespot://` protocol activation, so the guard runs before any preview.
- Restricted the upstream-drift `git ls-remote` fallback to HTTPS transports so a tampered dependency manifest cannot hand git a remote-helper URL (`ext::`, `file://`) that executes commands or reads local paths.
- Hardened standalone `-removeselfdata` to delete its data directories with a reparse-point-aware walk that unlinks nested junctions/symlinks instead of traversing them, closing a delete-anything vector for anyone who can plant a link under `%APPDATA%\LibreSpot`.

### Accessibility

- Completed the advertised Spanish, Brazilian Portuguese, Russian, and Simplified Chinese WPF resource sets; each locale now has reviewed user-facing translations, preserves product/file/format tokens, and participates in hidden long-text prompt rendering.
- Added a visible, keyboard-accessible LibreSpot-wide search (`Ctrl+K`) with localized category, count, empty-state, action, and shortcut text across all six resource sets.
- Added an AA-safe danger-text token (at least 5.17:1 across every dark raised surface), migrated destructive/error copy to it, and retained system attention colours in Windows high-contrast themes.
- Localized every stack-health component name, status, evidence template, fallback action label, and shared scrollbar automation name through the runtime culture resources; the first Spanish health and scrollbar translations now prove the end-to-end non-English path.
- Made all 38 WPF storyboard animations respond to the live OS animation/high-contrast setting through freeze-safe motion-aware clocks; one-shot transitions snap to their final state and the repeating progress shimmer holds still when motion is suppressed.
- The dependency-status rows now change glyph shape per severity (check / dash / exclamation / cross) instead of relying on ring colour alone, so warning and critical states stay distinguishable in high-contrast mode where both can map to the same system colour.
- Raised the snackbar dismiss button to the app's 32px minimum touch-target size.

### Changed

- Re-imagined the WPF shell from a new implementation mockup: global search now lives in the top command bar, the Recommended hero is compact and task-led, status cards track Spotify/Spicetify/Marketplace instead of generic host trivia, the readiness panel reads as system health, and the activity timeline can collapse to return working space.
- Refined the graphite/emerald design tokens with calmer tonal elevation, quieter borders and shadows, stronger editorial spacing, compact action and readiness surfaces, and a timeline treatment that remains coherent in dark, high-contrast, full-width, and inspector-free compact layouts.
- Added one categorized search surface for setup modes, SpotX settings, themes and extensions, local profiles, maintenance actions, support evidence, and health/provenance issues; opening a result navigates to the existing detailed surface without bypassing its safety controls.
- Refreshed GitHub issue and pull-request intake around Recommended/Custom/Maintenance/Fleet surfaces, current versions, operation IDs, reviewed support bundles, and the repository's actual local validation commands.
- Added a stable per-run operation GUID across WPF activity, PowerShell backend events and journals, CLI JSON/plain output, rolling logs, crash evidence, and support-bundle manifests, plus an opt-in local `LibreSpot-Operations` EventSource for ETW/EventPipe collection without uploads.
- Added receipt-backed undo preview and confirmation to the WPF activity pane plus `LibreSpot.Cli undo`; successful and failed attempts emit new operation evidence while retaining source provenance and snapshots for idempotent retries.
- Added one migration-safe customization ownership report across WPF health, CLI status schema v3, support bundles, legacy PowerShell warnings, and backend plans. LibreSpot now distinguishes its own SpotX/Spicetify state from raw SpotX, standalone Spicetify, and likely BlockTheSpot-family injectors; standalone Spicetify state is preserved before setup, and foreign state is journaled before maintenance instead of being changed silently.
- Replaced separate shared-function sync commands with one composition contract and `Build-Scripts.ps1 -ComposeHosts`; both executable PowerShell hosts now consume canonical shared modules, host-specific wrapper sets, and pinned-release data, and release-manifest generation refuses stale hosts.
- Turned the local-data inventory into an enforceable 29-location contract covering user and machine configuration, profiles, activation recovery, undo snapshots, journals/receipts, caches, evidence, logs, backups, runtime files, temporary workspaces, support archives, and the watcher task; RemoveSelfData now also clears machine-scope Fleet data.
- Consolidated the WPF shell onto a ten-step product type scale and a shared 2px extra-small radius token, removing one-off 5px checkbox corners and 11.5/12.5/13.5/14.5/15/15.5/17/22/23/25/27px text sizes that caused subtle visual drift.
- Updated Serilog to 4.4.0 and Microsoft.NET.Test.Sdk to 18.8.1, refreshing runtime/test transitive pins and clearing the live dependency-health drift gate with no known vulnerable NuGet packages.
- Added pinned-asset provenance to the WPF readiness inspector, Fleet CLI `status --json` schema v2, and redacted support bundles: each core/community asset now carries its version or commit, source URL, last-verification date, changelog/release link, and current/stale/indeterminate freshness state.
- Marketplace repair and profile reapply now make a bounded, reparse-safe snapshot of `config-xpui.ini` and `CustomApps`, restore only files that the refreshed packages leave missing, retain the recovery snapshot, expose success/failure evidence in support bundles, and warn that Marketplace's IndexedDB state is outside the recoverable boundary.
- Release guidance now distinguishes the current v3.7.4 script source from GitHub's public latest stable v3.7.2 and validates preview parity locally plus stable tag/assets against the live GitHub latest-release channel.

### Fixed

- Fixed Marketplace theme and snippet installs silently doing nothing in the default "(None - Marketplace Only)" setup. LibreSpot now follows the official Marketplace install contract: it creates the upstream placeholder theme (`Themes\marketplace\color.ini`), activates it when no theme is selected, and keeps `inject_css`/`replace_colors` on. It also removed a pre-apply step that actively zeroed those settings in Marketplace-only mode (with an empty theme the Spicetify CLI already disables injection on its own, so the zeroing only broke the store contract). Disabling Marketplace cleanly restores the previous state, clearing the config reference before deleting the placeholder so an interrupted removal can never break later applies.
- Restored a visible way to open the Marketplace when Spotify's global-nav redesigns silently break Spicetify's injected nav link (upstream spicetify/marketplace #1133/#1185/#1194: the /marketplace route works but no button renders). LibreSpot now ships a small managed extension that waits for the app to settle and registers a Topbar "Marketplace" button only when no native Marketplace entry rendered; it is local-only and is removed when Marketplace is disabled.
- Marketplace health now verifies the theme contract on every surface: the PowerShell health model reports a `ThemeInactive` repairable state, and the WPF/CLI stack-health Marketplace component shows a localized "Theme support inactive" warning (all six locales) with Repair Marketplace as the recommended action, instead of reporting Ready while store installs no-op. A failed Spicetify apply still outranks the theme-contract warning.
- Marketplace install now also removes the pre-1.0 `spicetify-marketplace` custom-app registration, matching the official installer's legacy cleanup, and the post-install log explains that an empty store page usually means GitHub is rate-limiting the catalog fetch and recovers on its own within about a minute.
- Guaranteed the post-install Spotify launch starts a fresh, patched session so Marketplace, extensions, and themes appear immediately without a manual restart. Because Spotify is single-instance, a stale or respawned Spotify process previously caused the final launch to focus the old un-patched window; both hosts now force-stop all Spotify processes right before the final launch.
- Made profile activation crash-consistent across the WPF and stable PowerShell hosts: both now share a cross-process lock and durable old/new fingerprint marker, recover interrupted commits to one complete state, and preserve the previous-profile rollback pointer.
- Made activity-log severity colours follow live theme changes by replacing frozen converter brushes with dynamic semantic-resource triggers; already-realized rows now update immediately when high contrast is toggled.
- Recovered malformed, unsupported, or dangling active-profile pointers without losing the current configuration: LibreSpot now preserves it as a uniquely named recovery profile, rewrites a valid pointer, and treats malformed previous-profile metadata as unavailable instead of crashing the gallery.
- Made upstream and community drift caches tolerate null or duplicate records and replace cache JSON atomically, preserving the last valid health snapshot across concurrent writers, process interruption, or malformed local state.
- Made watcher state replacement fail cleanly when another process interrupts the atomic update instead of leaking a non-terminating `Move-Item` error before recovery.
- Restored `.librespot` Explorer imports from arbitrary local folders: file-association activations now enter the validated preview/confirm flow directly instead of being converted into a store-confined protocol URI, while malformed, missing, oversized, and wrong-extension inputs fail without crashing startup.
- Removed the WPF shell's obsolete whole-app UAC relaunch: Recommended, Custom, and maintenance actions now run in the current standard-user token, readiness no longer treats a standard session as blocked, and the elevation boundary tests require every desktop backend action to remain no-admin.
- Fixed `Unlock-SpotifyUpdateFolder` throwing "collection modified" and clearing nothing when the Update folder carried more than one Deny ACE, the exact multi-ACE case it exists to handle; Deny rules are now snapshotted before removal.
- Fixed the in-app "what's new" preview going blank whenever the changelog's leading `[Unreleased]` section was empty (the normal state right after a release); the preview now falls through to the newest section that actually has content.
- Stopped run-receipt undo entries from mislabelling the operation token kind as the operation "phase" in the undo history; receipt entries have no phase, so the field now reads as unknown instead of showing the token kind.
- Relaxed the archive-extraction traversal guard so legitimate entry names that merely begin or end with two dots (e.g. `..gitkeep`) are no longer rejected, while the authoritative resolved-destination prefix check still blocks real path traversal.

### Tests

- Added a hidden `activity-collapsed` WPF state and shell contracts for command-bar search placement, product-specific status cards, responsive activity reclamation, and the real populated-query rendering path; verified the redesigned shell at 1600x1000 and the inspector-free 1280x760 breakpoint without activating a window.
- Made localization validation fail on unreviewed English carry-over, stale allowlist entries, placeholder/access-key drift, protected product or file-token changes, translation truncation, and known terminology regressions; per-locale translated/reviewed counts are printed on every validation run.
- Added search-domain, navigation, empty-state, shortcut-focus, multi-token matching, localized-resource, UI Automation, and hidden rendered-WPF coverage for the global search surface.
- Added correlation regression coverage for caller-supplied PowerShell IDs, legacy/new backend protocols, mismatch refusal, WPF activity and bundle evidence, CLI plans, and local EventSource start/message/completion events.
- Added policy-refusal, stale-state, missing-value, registry-type, idempotent CLI retry, partial-failure recovery, receipt-provenance, WPF selection, Fleet-contract, and PowerShell composition coverage for executable undo.
- Added synthetic C# and Pester ownership fixtures for raw SpotX, standalone Spicetify, LibreSpot-managed state, mixed BlockTheSpot residue, redacted support export, backend plan disclosure, and migration preservation.
- Added deterministic byte-generation, stale-host, missing/duplicate export, invalid-order, and Windows PowerShell 5.1/PowerShell 7.6 import/parse composition coverage.
- Added data-inventory write-site, deletion-root, retention-policy, support-export, and private-profile exclusion contracts plus an end-to-end machine-data removal fixture.
- Added before/after SpotX fixtures plus live pinned-entrypoint hash/policy validation so Defender mutations, missing opt-outs, unsupported safe-pin arguments, and lane adapter drift fail the build.
- Added fault injection at every profile-activation write boundary plus hidden-process WPF/PowerShell concurrency and cross-host recovery coverage.
- Stabilized FlaUI interaction checks for virtualized maintenance controls by scrolling only offscreen elements, waiting for the UIA layout update, and rejecting disabled targets before invocation instead of silently dropping clicks.
- Added a single non-activating rendered-WPF QA command covering 13 shell/overlay states plus the nested crash dialog across dark/high-contrast and English/Spanish; every capture verifies localized primary bounds, accessible action names, focus-ring rendering, metadata, dimensions, and render-dropout retries.
- Expanded localization scanning from `MainWindow.xaml` to all production XAML and health-component construction, with Spanish snapshot and shared-control automation-name regression coverage.
- Added direct Pester coverage for the lane-specific watcher launch, registration, first-run, preference, active-Spotify, failed-reapply, missing-config, and interrupted-state-write contracts.
- Added `Build-Scripts.ps1 -WatcherIntegration`, which registers and exports a unique least-privilege task, runs seven isolated watcher process-boundary scenarios, captures Task Scheduler evidence on failure, and always removes the disposable task and temp root.

## [v4.0.0-preview.17] (2026-07-09)

Premium command-center parity and resilience release.

### Changed

- Refined the v2 command-center design with truthful primary-vs-quick-link rail hierarchy, quieter secondary navigation, chevron-backed inspector actions, consistent action-row help text, and an updated implementation mockup.
- Moved the inspector breakpoint above the cramped Custom-layout range and added a short-window activity/workspace rhythm so dense screens retain usable editor width and vertical space.
- Replaced the readiness inspector's repeated aggregate result with four independent system, Spotify, permission, and dependency states plus a passed-check percentage; loading, unavailable, warning, and critical states now change the hero artwork and readiness ring instead of retaining a success check.
- Made activity clearing explicit that it only clears the visible activity view, documented the cycling severity filter for assistive technology, and replaced layout-dependent Custom-conflict copy with the two setting names users need to resolve.
- Rebuilt the crash/recovery window on the shared palette, typography, radius, input, and button resources; it now uses dark native chrome, system-color high-contrast fallbacks, localized copy, wrapped automation-named actions, and resizable work-area-constrained scrolling instead of a hardcoded legacy theme.

### Fixed

- Restored setting-card press scale after pointer release, and removed the ComboBox slide animation that bypassed the reduced-motion token system.
- Stabilized dense offscreen WPF captures by draining layout and the full card-animation window before rendering, preventing intermittently incomplete Custom screenshots.
- Startup and snapshot failures now leave checking state, expose a visible Refresh environment recovery action, keep setup disabled, and replace the Recommended hero's success copy with actionable failure guidance.
- Live regions now announce their changing profile/run content (including the current activity step), translated prompts wrap and scroll within the work area, title-bar language selection is disabled behind modals, and an untranslated backend-failure fallback now comes from localization resources.

### Tests

- Expanded premium-shell contracts for breakpoint behavior, capture settling, primary/quick-link navigation separation, inspector action affordances, motion-safe popups, and restored press transforms.
- Added contracts and runtime UIA assertions for per-check readiness, dynamic live-region names, retryable initialization failure, localized activity announcements, prompt bounds, and corrected visible-label navigation names.
- Removed the crash reporter from the C# hardcoded-color allowlist and added a non-activating crash-preview capture path plus contracts for shared theming, localization, scrolling, dark chrome, and action semantics.
- Added deterministic compact-window and real high-contrast capture switches so the responsive and system-palette variants can be rendered and reviewed without activating the app on the desktop.

## [v4.0.0-preview.16] (2026-07-09)

Premium desktop command-center release.

### Changed

- Reimagined the WPF shell from an image-generated premium concept with a deeper graphite workspace, cyan/emerald hierarchy, separate stack-health cards, a slim active-navigation rail, restrained card gradients, cleaner button geometry, and selection states that no longer expose the platform's pale list chrome.
- Made the shell responsive to the Windows work area: rail density, workspace gutters, inspector visibility, and activity-dock height now adapt at compact widths and heights while the UI-automation mode remains non-activating and tray-free.
- Replaced static XAML resource lookups with live localization bindings throughout the main window, and refreshed maintenance card copy when the culture changes.
- Kept successful setup results reviewable until explicit dismissal instead of closing the shell automatically.

### Fixed

- Readiness now exposes explicit checking and retry states, disables setup until the environment snapshot is verified, and carries warning/error state through the inspector ring, summary rows, and status labels.
- The activity dock now reflects real log entries, cycles through all/warnings/errors filters, provides a truthful empty state, and uses a valid scheduled-task undo token in its smoke fixture.
- Prompt and activity overlays now disable the underlying workspace so keyboard and assistive-technology focus cannot interact through a modal surface.
- Reduced-motion mode now disables the indeterminate progress sweep in addition to the existing transitions.

### Tests

- Added premium-shell source contracts covering live localization, modal isolation, readiness/activity states, compact work-area behavior, result retention, and visual-system tokens.
- Recaptured the four README WPF screenshots from deterministic background smoke states.

## [v4.0.0-preview.15] (2026-07-09)

Deep audit release.

### Fixed

- The close-while-running prompt, run-pipeline log entries, and prompt fallback summaries used 18 hardcoded English strings that bypassed the runtime localization system, leaving those surfaces in English when the UI culture was set to RU, ZH-Hans, PT-BR, or ES. All strings moved to `Strings.resx` with `Vm_` keys and translated across all 5 satellite cultures.
- `Process.Start` calls in `OpenExternalUri`, `OpenLibreSpotFolder`, `RelaunchAsAdministrator`, and `SpotifyProcessService.StartThroughShell` did not dispose the returned `Process` handle, leaking native OS handles on every invocation.
- `PromptStateViewModel.Show` fallback summary strings ("What happens next", destructive/non-destructive body text) were hardcoded English instead of using runtime localization resources.

### Tests

- Expanded the localization regression guard to cover `PromptStateViewModel.cs` in the `ViewModels_RuntimeLocalizationKeysExist` check and added 14 removed English phrases to the `ViewModels_UserFacingComputedTextUsesResources` regression list.

## [v4.0.0-preview.14] (2026-07-09)

### Changed

- Refined the WPF command-center hierarchy across Recommended, Custom, Maintenance, prompt, and activity surfaces so secondary workspaces fill the viewport cleanly without repeating the recommended setup CTA.
- Rebalanced failed-run activity recovery: error/canceled progress now uses warning/error tone and copy, and Export failure is the primary action while Close becomes secondary.
- Tightened desktop rail accessibility names and mapped About to the repository action instead of switching to Maintenance.

### Tests

- Recaptured WPF smoke screenshots for the preview.14 desktop shell and added a failed-run progress-label regression.

## [v4.0.0-preview.13] (2026-07-09)

Deep audit release after the v4.0.0-preview.12 tag.

### Added

- Automatic single retry through the SpotX mirror when SpotX's own downloader hits a classified outage (connection timeout / curl exit 28, or a Cloudflare-worker endpoint failure); a mirror flagged upstream as phishing instead retries once without the mirror. Timeouts and worker failures are the dominant recoverable SpotX install failure, and previously surfaced as a hard error even though a mirror retry usually succeeds.
- Antivirus exclusion health signal: when Windows Defender real-time protection is on and the Spotify install folder is not excluded, the readiness inspector and CLI `detect`/`status` now surface a warning with a copy-paste `Add-MpPreference -ExclusionPath` command, because SpotX-patched files are commonly quarantined as a HackTool false positive (which code-signing cannot clear). LibreSpot only reports and suggests, it never changes antivirus settings. Third-party AV, disabled protection, an already-excluded folder, or an uninspectable Defender all stay silent.
- Maintainer drift check `Build-Scripts.ps1 -CheckSpotifyVersionDrift`: compares the pinned Spotify target (the "current pinned" entry in `$global:SpotifyVersionManifest`) against the community-canonical SpotX-Bash `spotx.sh` `buildVer` and flags staleness. Report-only, it never auto-bumps the pin; network/parse failures are treated as indeterminate so the check is not flaky.

- Microsoft Store Spotify heads-up: when the Store version of Spotify is installed, the readiness inspector and CLI `detect`/`status` now show a one-line informational note explaining that SpotX will replace it with the standard desktop build during setup (it was already auto-removed, but silently, which read as "where did my Spotify go"). Read-only detection, LibreSpot does not remove the package itself.

### Changed

- Reworked the WPF home shell to match the premium mockup: compact left navigation, readiness hero, summary tiles, centered setup action, split environment/dependency panel, right-side readiness/next-action cards, and the docked activity table with an always-visible cancel affordance during active runs.

- Relaunching as administrator from a confirmed setup now resumes that setup automatically in the elevated window instead of dropping the user back at "Run recommended setup." The standard-mode session stages the confirmed configuration, passes `--shell-action=resume-install` to the elevated relaunch, and the elevated instance runs it directly, removing the second click. It only auto-runs a setup that was already risk-acknowledged, and only when actually elevated.
- During install, the first (config-generation) Spotify launch now force-closes any Spotify the user or SpotX left running before reopening it, so config is generated by a clean, freshly patched process. Applies to all three lanes.
- The WPF shell now closes itself automatically after a completed setup/change run (the same operations that restart Spotify: Install, Reapply, Repair Marketplace, Safe Mode, Restore Backup, Restore Vanilla). Read-only actions like Check Updates and continue-working toggles keep the window open. The UI-automation screenshot mode never auto-closes.

### Fixed

- The WPF language selector is now reachable from both the sidebar and title bar instead of being bound in the ViewModel but hidden in XAML.
- WPF profile management, share/comparison cards, readiness insights, activity status, and failure text now use runtime localization resources so changing the UI culture refreshes secondary ViewModel-computed strings instead of leaving English fragments behind.
- WPF support-bundle export feedback, custom-patch notices, setup prompts, maintenance confirmations, auto-reapply prompts, administrator relaunch guidance, and risk acknowledgment text now use runtime localization resources instead of hardcoded ViewModel copy.
- UI automation smoke setup now writes both current (`Apps\xpui.bak`) and legacy (`Apps\xpui.spa.bak`) SpotX backup markers so smoke states exercise the same readiness path as current SpotX installs.
- The WPF activity log now coalesces auto-scroll requests during high-volume backend output instead of queueing one dispatcher scroll per appended log row.
- WPF UIA/FlaUI smoke harnesses now use a longer main-window startup budget while keeping interaction waits tight, fixing custom-search and sequential smoke-state timeouts on loaded desktops.
- Fleet CLI `--scope machine` now resolves the default config under `%ProgramData%\LibreSpot\config.json` instead of silently using the per-user config; invalid scope values fail before reads or mutations.
- `detect --intune --json` now emits the JSON detection document while preserving the Intune exit code, and the fleet contract lists the reachable blocked exit code `20`.
- Fleet answer-file validation now rejects consumed schema enum/range/type errors (culture, SpotX lyrics/download/cache settings, Spicetify extension lists, profiles, watcher/logging/reboot policy) before backend runs can normalize or drop bad intent.
- Backend process exit codes now propagate through CLI operations and NDJSON events for retry, permission, canceled, installer-busy, and reboot outcomes instead of collapsing to exit `1`.
- Standalone PowerShell verification no longer depends on `Get-FileHash`; SHA256 checks use the shared .NET fallback in normal, worker-runspace, backend, and README bootstrap paths.
- Standalone auto-reapply scheduled tasks now split executable and arguments into separate Task Scheduler XML elements, so quoted paths are registered as the executable path rather than a single malformed command.
- Community extension downloads now verify HTML/error-page and SHA256 checks in a temporary file before moving the asset into the live Spicetify Extensions folder.
- The standalone watcher now uses LibreSpot's guarded downloader for SpotX, keeping CVE/download diagnostics consistent with user-triggered install and reapply.
- The WinRM reapply deployment sample now exits with the remote `LibreSpot.Cli.exe` exit code instead of hiding remote failures behind a successful local PowerShell invocation.
- The read-only log/terminal `TextBox` (`LogTextBoxStyle`) is a keyboard tab stop but its restyled template dropped the platform focus visual, so sighted keyboard users got no focus indicator when they tabbed into it (WCAG 2.2 SC 2.4.7). It now shows an accent focus ring when keyboard-focused.

- Localization sync gate (`tools/Sync-Localization.ps1`, also run by `Build-Scripts.ps1 -Validate`) now rejects format-placeholder mismatches: a translated string whose set of `{0}`/`{1}`/… indices differs from the English source is caught at build time instead of crashing `string.Format` at runtime. Placeholders may be reordered for grammar but not dropped, added, or renumbered. Documented the translation workflow in `.github/CONTRIBUTING.md`.

### Tests

- Recaptured the README WPF screenshots from the current smoke states and added integration guards that keep language selectors visible and the UIA fixture aligned with current SpotX backup markers.
- Expanded localization regression coverage to scan activity overlay ViewModel text and guard profile/runtime status strings against hardcoded English regressions.
- Expanded the ViewModel localization guard to cover secondary support-bundle, custom-patch, setup, administrator, and risk-prompt strings.
- Extended the WPF virtualization guard to require coalesced activity-log auto-scroll scheduling.
- Added focused regression coverage for CLI scope resolution, Intune JSON detection, strict answer-file validation, backend exit-code propagation, WinRM exit propagation, worker-runspace hash exports, watcher Task Scheduler XML, guarded watcher downloads, and temp-file community extension verification.
- Added an accessibility guard (`AutomationNameContractTests`) that fails the build if any interactive control in `MainWindow.xaml` loses its UIA-discoverable name (`AutomationProperties.Name`/`Content`/`Header`/`LabeledBy`), and that pins the `LiveRegionContentControl` polite live-region peer. All 77 interactive controls currently comply; the test locks that in against regression (WCAG 2.2 4.1.2).
- Added focus-visibility guards to `KeyboardFocusContractTests`: broadened the custom-focus-ring theory to TextBox/TabItem/ComboBoxItem, a targeted check that the read-only log textbox keeps a focus ring, and an invariant that the number of keyboard-focus triggers is at least the number of templates that null the default focus visual (so no restyled control can silently drop its focus ring).

## [v4.0.0-preview.12] (2026-07-08)

### Fixed

- Made active WPF runs cancellable directly from the activity panel instead of routing through a second prompt that could leave Continue as the only enabled action.
- Restarted Spotify after successful patch, reapply, repair, restore-backup, safe-mode, or restore-vanilla runs so completed changes load in a fresh client session.
- Removed main-page warning and repair-note blocks from the WPF readiness sidebar while keeping detailed diagnostics in Maintenance and support bundles.
- Refined the WPF shell with a premium command-center hero, larger readiness meter, stronger status hierarchy, and an active-run Cancel affordance that remains visible while work is running.

## [v3.7.4] / [v4.0.0-preview.11] (2026-07-08)

### Fixed

- Bounded local `.librespot` profile import files to the same diagnostic-size envelope as remote profile links, and made shared-profile export atomic so cancellation or write failures cannot leave a corrupt final profile file.
- Hardened support-bundle export temp-file handling so stale or concurrent `<destination>.tmp` files are not overwritten while preparing diagnostic zips.
- Validated fleet answer-file custom SpotX patch JSON in the CLI before backend startup, including profile overrides, invalid JSON, empty enabled payloads, and the 64 KB limit.
- Fixed the Custom workspace smoke state so screenshots and UI automation open with no hidden settings-search filter applied.
- Hardened release smoke-test timing around backend watchdog output and prompt-overlay readiness so full-suite runs remain deterministic under local UI test load.
- Fixed a false "SpotX ran but the patch could not be verified" warning on every successful install. SpotX names its pre-patch bundle backup `Apps\xpui.bak` (older builds used `xpui.spa.bak`), but the verifier only looked for `xpui.spa.bak`, which SpotX no longer writes. Patch verification now recognizes `xpui.bak`, the Spicetify-extracted `Apps\xpui` directory, and SpotX's durable patched-binary backups (`Spotify.bak`/`chrome_elf.dll.bak`), across the PowerShell verifier, the Maintenance status card, and the desktop stack-health/`status --json` (`EnvironmentSnapshotService`) detection.
- Hardened local profile loading so malformed or spoofed profile documents are skipped instead of breaking the profile gallery, duplicate share-URI sources are rejected, invalid embedded profile payloads get a deliberate error, and exported profiles record the preview informational version.
- Localized the WPF environment freshness/status card through the runtime resource system so secondary shell status text follows the active UI culture instead of bypassing localization.
- Hardened custom patch/profile provenance so redirected patch imports record the final HTTPS document, non-HTTPS final URLs are rejected at the service boundary, shared profiles strip URL credentials/query/fragment secrets, and malformed share-URI percent encoding gets a deliberate error.
- Gave profile share/comparison clipboard actions the same retry path as run-log copying, and moved their success/failure messages plus the log-copy fallback warning into runtime localization resources.
- Hardened isolated external-process (SpotX) execution: when Windows PowerShell drops the child exit code under redirected output, LibreSpot now surfaces any failure its output already classified (download outage, phishing mirror, patch abort) instead of masking every unknown-exit-code run as success.
- Sped up the standalone native-uninstaller wait: it now waits on the actual uninstaller process handle it launched instead of polling only for a guessed `SpotifyUninstall` process name, so a name mismatch no longer burns the full timeout before file cleanup continues.
- Fixed the post-SpotX verification crash under Windows PowerShell 5.1 by replacing the incompatible `Split-Path -LiteralPath ... -Parent` call with a .NET parent-directory resolver.
- Improved WPF cleanup progress during native Spotify uninstall: the backend now logs that it is continuing after the Microsoft Store check, emits heartbeat/status updates while the native uninstaller is still running, and no longer reports a timed-out uninstaller as completed.
- Refreshed the pinned upstream compatibility set to SpotX commit `550bc72c` for Spotify `1.2.93`, Spicetify CLI `v2.44.0`, and Marketplace `v1.0.9`, including SHA256 pins for the current Windows assets.
- Added a WPF backend host watchdog that warns when a run stops emitting output and stops silent stalled backend processes with a categorized error instead of leaving the activity panel pending indefinitely.
- Kept WPF cancellation and close responsive during active backend runs, including a second close after cancel is already requested, and delayed install success until Spotify survives the post-patch launch stability check; failed checks now attempt a Spicetify restore before reporting failure.
- Added one-click failure-bundle export from the WPF activity panel after failed or canceled runs, including the current run log, operation journal, health snapshot, and backend result metadata in the redacted zip.
- Migrated the desktop test project from deprecated xUnit v2 packages to xUnit v3/FsCheck v3 packages and refreshed test-only dependency-health policy for the Microsoft Testing Platform transitives.
- Virtualized the WPF activity run log with a recycling list so busy runs no longer realize all 2,000 retained log rows at once.
- Re-routed mouse wheel events from nested WPF scroll regions at their boundaries so settings panes continue scrolling from theme, profile, and custom-patch editor areas.
- Cleaned up Russian and Simplified Chinese maintenance microcopy, including reapply labels, watcher terminology, and Spicetify spelling in the localized WPF shell.
- Hardened safe archive extraction so expanded-byte limits are enforced while streaming actual decompressed bytes, with temp-file cleanup on capped or failed entries.
- Hardened SpotX and elevation temp-file execution by verifying payload hashes immediately before launch and holding read locks on scripts while child PowerShell processes start.
- Moved upstream freshness checks to a runspace-safe async path that keeps cmdlet-heavy cache and UI work on the dispatcher instead of raw ThreadPool delegates.
- Implemented Spotify restart detection in the standalone launch-after stability probe so PID replacement during the post-patch wait is surfaced as an unstable session.
- Unified standalone shell and README naming with the WPF shell by using "Recommended setup" and "True Shuffle" consistently for user-facing defaults.
- Localized resource-backed WPF ViewModel text for profile management, shell readiness, Custom summaries, Maintenance cards, support-bundle previews, and the activity overlay.
- Closed WPF color-lint blind spots so short XAML hex colors, named XAML colors, and unallowlisted C# color construction fail local tests instead of bypassing palette review.
- Regenerated README WPF screenshots from the preview.9 shell and added PNG metadata validation so stale screenshots fail local release validation.

## [v4.0.0-preview.8] (2026-07-07)

### Changed

- Rebuilt the WPF desktop preview into a three-column command center: left setup/recovery rail, compact stack-health row, center workspace, right readiness/trust inspector, and persistent activity/log footer.
- Refreshed the desktop palette with deeper graphite surfaces, brighter green action affordances, quieter strokes, and stronger status contrast.
- Replaced visible workspace tabs with rail navigation while keeping keyboard/UIA workspace switching covered by smoke tests.
- Recaptured all README WPF screenshots from deterministic smoke states.

## [v3.7.3] / [v4.0.0-preview.7] (2026-07-07)

Deep end-to-end audit pass: correctness, security, accessibility, theming,
localization, and release hardening across the stable script, the WPF shell,
and the fleet CLI.

### Fixed: critical

- **The standalone script did not parse at all under Windows PowerShell 5.1.**
  `LibreSpot.ps1` was BOM-less UTF-8; PS 5.1 reads such files in the ANSI
  codepage, and a `U+2139` glyph in the update banner corrupted the token
  stream into 14 cascading parse errors, every `Run with PowerShell` /
  `powershell -File` launch failed outright. Both runnable scripts now carry
  a UTF-8 BOM, `-Lint` gates on BOM presence plus a clean `ParseFile`, and
  the shared-function sync writes the backend with a BOM.
- **Every WPF-shell and CLI install/reapply/repair failed at archive
  extraction.** `Expand-ArchiveSafely` loaded only `System.IO.Compression`;
  on .NET Framework `ZipFile` lives in `System.IO.Compression.FileSystem`,
  which nothing in the backend loaded. Fixed in all three script surfaces
  and re-verified by executing the function in a clean `powershell.exe`.
- **Successful GUI installs rolled themselves back.** The monolith's worker
  runspaces were missing `Write-MarketplaceVisibilityEvidence`, so the
  Spicetify apply success path threw `CommandNotFound`, was misread as an
  apply failure, and triggered `spicetify restore`. The worker allow-list
  also gained the journal-retention helper (operation journal was silently
  dead in workers) and the Spotify version manifest globals (a pinned
  Spotify version silently degraded to `auto`).
- **The auto-reapply watcher never worked from the WPF shell.** The
  scheduled task pointed at the ephemeral `LibreSpot.Backend.<guid>.run.ps1`
  execution copy that is deleted right after each run; it now targets the
  canonical runtime script. In the standalone script, the `-watch` handler
  ran before the reapply pipeline's dependencies were defined (real reapply
  ticks died with `CommandNotFound`) and was forwarded through the UAC
  self-elevation gate (a consent prompt every 30 minutes); the watcher now
  runs non-elevated after all definitions.
- **The Spotify killer/hider watcher killed the wrong Spotify.** It
  force-closed the user's running Spotify during read-only Check for
  Updates, killed the Spotify that `LaunchAfter` had just started during
  the 20-second stability window (self-inflicting the "server-side
  enforcement" warning), and killed the Marketplace window Repair had just
  opened.
- **The run-completion snackbar never rendered.** WPF-UI ships no
  generic.xaml and its dictionaries were never merged, so the `Snackbar`
  control had no template. A palette-token implicit style now renders
  Success/Caution/Danger completion feedback in both palettes, with
  offscreen render coverage.

### Fixed: data safety

- Watcher-state saves from the standalone script destroyed the WPF lane's
  extended fields (`LastAppliedSpotifyVersion`, `LastSuccessfulApplyAt`, …);
  every lane now merges over the existing `watcher-state.json`.
- Full Reset left the auto-reapply scheduled task registered forever on a
  machine with no Spotify; both lanes now unregister it.
- Plan summaries ran against `config.json` before the user confirmed Apply,
  cancelling the prompt left the unconfirmed profile (with
  `RiskAcknowledged=true`) live for the watcher. Plans now use a temp config;
  the real save happens only on confirmation.
- UI-driven config saves rebuilt `config.json` purely from controls, wiping
  config-only settings (`RiskAcknowledged`, `UiCulture`, `SpotX_Language`,
  custom SpotX patches); saves now merge over the loaded config.
- Corrupt-config quarantine aborted at startup before the journal helper was
  defined, leaving the corrupt file in place and repeating the settings-reset
  dialog every launch.
- `Remove-PathSafely` deletes reparse points as links instead of recursing
  into them, PS 5.1 `Remove-Item -Recurse` follows directory junctions and
  `icacls /T` reset ACLs on the target tree, an elevated delete-anything
  primitive for anyone able to plant a link in a removal root.
- The stale shared sources for `Set-WatcherState`/`Save-LibreSpotConfig`
  still carried the delete-then-move data-loss window; refreshed to the
  crash-safe rescue-move fallback.

### Fixed: security

- `librespot://profile?file=` (launchable by any web page) accepted arbitrary
  local paths; it now only reads from the LibreSpot profiles folder.
- HTTPS profile and custom-patch imports re-validate the scheme after
  redirects.
- Support-bundle redaction regexes carry a 2-second match timeout and omit
  the content window on timeout instead of shipping it un-redacted.
- Backend admin gate aligned with the shell: `CreateBackup`,
  `OpenMarketplace`, `RemoveSelfData`, and `Plan` no longer throw a bogus
  "needs administrator" error in non-elevated sessions; read-only `Plan` is
  exempt from the risk-acknowledgment gate.

### Fixed: UX, accessibility, and theming

- Maintenance Safe Mode was dead code twice over (a guard on a variable from
  another function's scope, and no worker branch); it now actually disables
  customizations.
- Cancelling a backend run showed the error badge and "Run needs attention";
  it now reports Canceled.
- The Remove LibreSpot data confirmation described the wrong action (generic
  "deeper reset path" scare copy); it now states exactly what is deleted and
  that Spotify/SpotX/Spicetify are untouched. Full reset copy says plainly
  that the Spotify app itself is uninstalled.
- Destructive buttons had invisible text in every high-contrast scheme and
  3.43:1 contrast in the dark palette; new `DangerFill`/`TextOnDanger` token
  pairs render 6.1:1 in dark and flatten to normal HC control surfaces. HC
  danger text maps to HotTrack so error and success states are
  distinguishable.
- MainWindow brushes converted to `DynamicResource` (251 references) so the
  runtime high-contrast palette swap actually restyles the window instead of
  producing a half-swapped UI; window chrome skips/clears custom DWM colors
  under high contrast and re-applies on toggle.
- ComboBox dropdown keyboard highlight was invisible (1.01:1); keyboard focus
  rings added to ComboBox items and the theme/scheme/profile lists; invisible
  scrollbar page buttons removed from the tab order; overlay storyboards use
  the motion tokens so reduced-motion actually flattens them.
- Sidebar profile status marker no longer shows green while the status line
  reports recovered defaults.

### Fixed: localization

- Nine translated strings in all four locales had lost every sentence after
  the first (including the beautiful-lyrics third-party privacy disclosure
  and the "Spotify and Spicetify are not affected" reassurance); all
  retranslated, and the localization lint now fails truncated translations.
- Machine-translation howlers corrected across es/pt-BR/ru/zh-Hans nav,
  buttons, and status text ("Costumbre", "Claro", "Cerca", "Despedir",
  "Mercado abierto", "Quitar especiado", "Correr necesita atención", …),
  including a meaning-inverted safety hint in ru/zh that told users to keep
  the riskiest options enabled.
- Tray menu and balloon text localized; backend stdout is UTF-8 so non-ASCII
  event payloads stop garbling; three dead drifted resx keys removed.

### Added

- SpotX child-download outage classification: timeouts (`curl exit code
  28`, `ERR_CONNECTION_TIMED_OUT`), the loadspot Cloudflare worker endpoint,
  and phishing-flagged mirrors now map to stable failure categories with
  sanitized guidance, recorded in the operation journal for fleet logs and
  support bundles, instead of a bare process exit code.
- CLI `--help` documents the implemented `reapply`, `uninstall`, and
  `repair` verbs and common flags.

### Changed

- `-Validate` excludes the intentional backend `Hide-SpotifyWindows` stub
  from drift comparison (it exited 1 on every run).
- Drift services share one `HttpClient`; built-in profile ID checks are
  cached; the Spicetify version probe uses a randomized temp file.

### Also in this release

Accumulated since v3.7.2 / v4.0.0-preview.6.

### Added
- Added offscreen high-contrast WPF rendering smoke coverage for representative
  buttons, disabled actions, checkbox, ComboBox, TextBox, health card, log,
  prompt, and snackbar surfaces, plus a XAML lint that rejects hardcoded colors
  outside palette dictionaries.
- Added offline asset-cache regression coverage for SpotX, Spicetify CLI,
  Marketplace, official themes, and the Stats custom app. The tests simulate
  network failure, require warning-level verified-cache fallback logs, and prove
  missing or corrupt cached assets stop before install side effects.
- Added temp-root RemoveSelfData regression coverage that seeds LibreSpot-owned
  config, profiles, journals, logs, crashes, cache, backups, and watcher state
  with canaries, then proves self-erasure leaves Spotify and Spicetify files
  untouched and support bundles do not leak the seeded paths or tokens.
- Added CommunityToolkit.Mvvm 8.4.2 to the WPF shell, replacing the local
  observable/command helpers with Toolkit commands and source-generated
  observable state properties.
- Added WPF runtime localization with resource-backed strings, a persisted
  language selector for EN/RU/ZH-Hans/PT-BR/ES, machine-prefilled RESX files,
  Crowdin CLI mapping, and local validation for resource completeness plus raw
  XAML user-facing strings.
- Added a WPF Custom mode SpotX `patches.json` editor with AvalonEdit syntax
  highlighting, JSON formatting, regex and match/replace dry-run validation,
  HTTPS import review, CLI answer-file support, and backend temp-file staging
  through SpotX `-CustomPatchesPath`.
- Added deterministic custom SpotX patch import provenance: imported
  `patches.json` payloads now record source URL, fetch timestamp, byte count,
  and SHA256 in config/profile metadata and redacted support bundles, with
  injectable transport coverage for network edge cases.
- Added rendered localization and accessibility smoke coverage for EN, RU,
  ZH-Hans, PT-BR, and ES, with culture-aware WPF UIA launch hooks and stable
  automation IDs for workspace, prompt, activity, and maintenance controls.
- Added profile sharing cards in WPF Custom mode with local QR rendering,
  share-link copying, selected-profile comparison text, embedded changelog
  preview, and direct community links for repository, Spicetify extensions, and
  theme catalog discovery.
- Added Windows shell integration for the WPF desktop preview: per-user
  `librespot://` protocol and `.librespot` file associations, jump-list tasks,
  taskbar thumbnail actions, tray minimize/restore, and clickable tray
  completion notifications.
- Added opt-in Spicetify custom-app support for the verified Stats release,
  including Custom mode UI, CLI answer-file schema support, pinned SHA256
  catalog metadata, and Last.fm network-behavior disclosure.
- Added package-manifest safety tests so draft winget, Scoop, Chocolatey, and
  package-channel metadata stay visibly blocked until release-manifest-generated
  hashes, signing, and package identity decisions are ready.
- Added local release-manifest generation through `Build-Scripts.ps1
  -GenerateReleaseManifest`, including checksum verification, artifact roles,
  runtime identifiers, signing state, and package-validation preflight checks.
- Added upstream drift monitoring for SpotX, Spicetify CLI, Marketplace,
  themes, and Stats pins with GitHub REST, `git ls-remote` fallback, cached
  offline metadata, and structured CLI `status --json` output.
- Added community asset drift and trust-review health for curated extensions,
  themes, and custom apps. Maintenance health, CLI `status --json`, and
  support bundles now show current/behind/missing/degraded state, pinned
  commit/hash, license, support state, fallback, and network behavior without
  failing offline.
- Added Marketplace visibility evidence for reapply and repair flows. CLI
  `status --json`, Maintenance health, and support bundles now distinguish
  files-installed from likely-visible Marketplace state using manifest,
  `custom_apps`, Spicetify apply, URI-open, and Spotify process observations.
- Added asset-cache inventory diagnostics. Verified cache writes now maintain
  source labels, URLs, byte size, first-seen, last-used, and last-verified
  metadata; corrupt cache hits are quarantined with journal receipts; and
  Maintenance health, CLI `status --json`, and support bundles expose cache
  count, size, stale, corrupt, and clear-cache state.
- Added local dependency-health validation. `Build-Scripts.ps1
  -DependencyHealth` emits a JSON report for vulnerable packages, outdated
  direct packages, outdated transitive packages, and accepted test-only
  transitive lag, with direct drift and expired allowlist entries failing the
  local check.
- Added schema-backed operation receipts: backend runs now write typed
  operation-token entries and `run-receipt.latest.json`, while the WPF undo
  pane consumes the embedded token and receipt schemas before showing
  reversible post-run actions.
- Added live FlaUI UIA3 smoke tests for WPF workspace tab navigation,
  settings search/clear, maintenance confirmation-to-activity flow, activity
  overlay dismissal, and prompt confirm/cancel behavior.
- Added inert `librespot://profile?...` share-link previews for local file,
  embedded, and HTTPS profile payloads, plus share-card payload generation that
  does not write config or start setup until the user confirms import.
- Added tested fleet deployment sample scripts, a standard answer-file sample,
  and a local package-validation runner for draft winget/Scoop/Chocolatey
  manifests without advertising those channels as publishable.
- Added a version-aware Spicetify integration context so CLI, config, theme,
  extension, Marketplace, backup, restore, and uninstall paths route through
  one facade ahead of Spicetify v3 migration work.
- Added a dedicated `LibreSpot.Cli.exe` console project for fleet tooling with
  `--version`, `status --json`, `detect --json`, `detect --intune`, and
  `validate --answer-file` support backed by the existing health report model,
  plus non-mutating `install --dry-run --ndjson` and `plan --json` output for
  fleet dry-run fixtures. Status/detect JSON includes structured backup count,
  last patch time, watcher outcome, issue IDs, recommended repairs, and
  documented fleet exit-code states.
- `LibreSpot.Cli.exe export-support --output <path>` now writes the existing
  redacted local support bundle format for endpoint tools without launching the
  WPF shell.
- `LibreSpot.Cli.exe watcher install/remove --silent` now maps to the existing
  backend auto-reapply scheduled-task actions for endpoint tooling.
- `LibreSpot.Cli.exe install`, `reapply`, and `uninstall` now execute the
  shared backend after answer-file validation, config persistence, and explicit
  uninstall consent while preserving dry-run NDJSON planning.
- `LibreSpot.Cli.exe repair --repair-id <id>` now runs allowlisted health-report
  repair actions, including the watcher repair alias, with dry-run NDJSON output.
- Fleet CLI NDJSON runs now write rotating `.ndjson` log files to `--log-dir`
  or `%ProgramData%\LibreSpot\logs` for mutating endpoint operations.
- Fleet answer files now support named `profiles`, and `--profile <name>` is
  validated before install/reapply persists the selected preset to `config.json`.
- README now includes tested fleet deployment examples for Intune detection,
  PDQ/SCCM install and repair, WinRM, PSRemoting over SSH, and uninstall.
- Added a local profile store for the WPF preview that migrates the current
  `config.json`, ships bundled profile templates, tracks active/previous
  profile pointers, and round-trips safe `.librespot` share files.
- WPF Custom mode now includes a local profile manager for bundled templates,
  create-from-current saves, preview-before-write, active profile selection,
  duplicate/rename/delete, and safe `.librespot` import/export.
- Stable PowerShell Custom mode now reads the same local profile store, previews
  bundled templates, saves current Custom selections as a named profile, and
  can set a selected profile active without starting setup.

### Changed
- WPF prompt, activity/log/undo, environment snapshot/freshness, maintenance
  action grouping, and Custom option editor/search state now live in dedicated
  state-domain view models while preserving the existing `MainViewModel`
  binding surface.
- WPF Custom profile management now has clearer active/template/local card
  states, a refresh action, selected-profile guidance, live status feedback,
  and safer edit/import/export command grouping.
- Stable PowerShell Custom profile management now pins the active profile first,
  separates preview/save secondary actions from the primary Set active action,
  and uses clearer status copy for preview, save, empty, and rollback states.
- WPF local profiles now pin the active profile first and use broader profile
  terminology when bundled templates are mixed with local presets.
- WPF content panes now disable horizontal workspace scrolling so long labels,
  profile notes, and maintenance text wrap inside the intended layout.
- Release-trust documentation now reflects the local-only release process:
  checksums, release manifests, SBOM output, and pending SignPath signing are
  documented as current evidence, while absent GitHub workflow/provenance
  claims are guarded by desktop regression tests.
- WPF Custom mode now replaces theme and scheme ComboBoxes with a searchable
  theme gallery, source/theme.js badges, scheme chips, UIA names, and a
  refreshed screenshot for the preview shell.
- WPF activity overlay now shows a reversible-changes pane after successful
  backend runs, sourced from the latest successful operation journal entries
  with manual undo notes and covered by parser plus UIA smoke tests.
- WPF health issue cards now surface mapped repair and diagnostic buttons
  directly on the issue, including Marketplace repair, reapply/safe-mode
  actions, watcher enablement, and local log-folder inspection.
- WPF Maintenance now exposes a six-card status dashboard for Spotify version,
  Spicetify version, SpotX patch state, last patch timestamp, watcher status,
  and backup count, all sourced from the existing environment snapshot.
- WPF shell backend completions now surface a WPF-UI snackbar notification
  with success, warning, or error tone while keeping the existing activity log
  panel available for full review.

### Fixed
- Fixed install crash from undefined `Hide-SpotifyWindows` and
  `Clear-DirectoryContentsSafely` in the backend script. Both functions
  existed in the monolith but were never synced to the embedded backend,
  causing terminating errors under `$ErrorActionPreference = 'Stop'` during
  the SpotX post-patch launch and Spicetify CLI installation steps.
- Fixed monolith and shared watcher ignoring the `AutoReapply_Enabled`
  preference, the scheduled task always reapplied regardless of the user's
  setting. Now gates on the preference before invoking headless reapply,
  matching the backend version.
- Fixed `Compare-LibreSpotVersions` misordering multi-digit pre-release
  suffixes (e.g. `-preview.10` sorted before `-preview.9`) by extracting
  the trailing numeric suffix for proper numeric comparison.
- Closed TOCTOU gap in `Expand-ArchiveSafely`: the function previously
  validated entries then disposed the zip handle and re-opened with
  `ExtractToDirectory`. Now validates and extracts within a single open
  handle using per-entry `ExtractToFile`.
- Fixed `PrimaryButtonStyle` and `SecondaryButtonStyle` ContentPresenter
  not consuming the `Padding` property, padding values set on buttons
  using these styles were silently ignored.
- Fixed Spotify playback failure after install: the backend script's
  `Normalize-LibreSpotConfig` referenced undefined `$global:ThemeData` and
  `$global:BuiltInExtensions` globals, causing PowerShell to silently strip
  all theme and extension selections during config normalization. The backend
  now derives both hashtables from `$global:ThemeSchemes` and
  `$global:BuiltInExtensionNames` at startup.
- Hardened the `librespot://profile?file=` URI handler to reject paths without
  the `.librespot` extension, preventing arbitrary local file reads through
  crafted protocol links.
- Replaced `cmd /c` uninstall invocations in the monolith and shared module
  with `Start-Process`, eliminating command injection risk from usernames
  containing shell metacharacters (e.g., `&`, `|`, `^`).
- Fixed socket exhaustion from per-call `HttpClient` creation in remote profile
  import; the service now uses a static shared instance.
- Profile pointer writes now use atomic temp-file-then-move to prevent data
  loss on crash during write.
- CLI `ReadConfigurationOrDefault` now logs config read failures to stderr
  instead of silently swallowing them.
- WinRM deployment sample rewritten to pass parameters via `-ArgumentList`
  instead of `[scriptblock]::Create()` string interpolation, eliminating
  remote command injection through the `$ProfileName` parameter.
- Desktop csproj now pins `InformationalVersion` to match the CLI project,
  preventing git-hash suffix drift in support bundle version strings.
- Custom patches CheckBox now uses `SettingCheckBoxStyle`, consistent with all
  other checkboxes in the Custom workspace.
- Fixed high-contrast palette crash: all Color key definitions used invalid
  `{x:Static}` XAML syntax in element content, which would throw at load time
  when the system high-contrast theme was active. Replaced with valid hex
  fallback values; Brush keys (which correctly use DynamicResource for real
  system colors) handle actual rendering.
- Boosted SubtleTextColor contrast from 4.1:1 to 5.4:1 against the dark canvas,
  passing WCAG AA 4.5:1 for caption-size text used throughout the sidebar and
  maintenance panels.
- Activity error/cancel detection now uses a typed outcome enum instead of
  parsing localized status strings for English keywords. The previous approach
  broke IsActivityError and IsActivityCanceled in non-English locales.
- Profile listing no longer crashes when a single profile JSON file is malformed;
  corrupt files are skipped with a debug trace.
- Profile file writes use atomic temp-file + rename pattern, matching the safety
  level of ConfigurationService.SaveAsync.
- Save-LibreSpotConfig and Set-WatcherState fallback paths no longer risk losing
  the original file: the File.Replace catch now renames to `.rescue` before
  attempting File.Move, restoring the original if the move fails.
- Simplified duplicate MultiDataTriggers in OptionTemplate and ExtensionTemplate
  badge borders. Both templates had two identical triggers (recommended+checked vs
  non-recommended+checked) producing the same accent tint; collapsed to a single
  DataTrigger on IsChecked.
- AssetCacheInventoryReport computed properties (PresentCount, MissingCount,
  CorruptCount, UnindexedCount, TotalBytes) are now cached at construction instead
  of re-enumerating the collection on every WPF binding access.
- FilteredThemeGalleryItems is cached with invalidation instead of allocating a
  new array on every property access from WPF bindings.
- CustomPatchValidationResult.Findings is now computed once at construction.
- LogLevelToBrushConverter uses case-insensitive comparison instead of allocating
  an uppercase string copy on every log entry.
- CommunityAssetDriftService manifest load catch narrowed to exclude fatal CLR
  exceptions and now emits a debug trace on failure.
- CollectPlanSummaryAsync catch narrowed with exception filter and debug trace.
- EnvironmentSnapshotStateViewModel time format now respects the user's locale and
  clock convention instead of forcing 12-hour AM/PM via InvariantCulture.
- Added TerminalBgColor/TerminalFgColor keys to the dark palette so the terminal
  brush pattern matches every other brush in the palette (Color key → Brush key).
- Added Clone_CoversEveryPublicSettableProperty test that fails immediately if a
  new InstallConfiguration property is added without updating Clone().
- Resolved xUnit2031, xUnit1031, and xUnit2013 analyzer warnings across the test
  suite.
- RemoveSelfData now writes a path-free irreversible receipt under
  `%TEMP%\LibreSpot\remove-self-data-receipt.latest.json`, no longer requires
  readable persisted config before erasing it, and avoids recreating
  `%APPDATA%\LibreSpot` with a final file-log write after cleanup.
- Fleet CLI schema conformance now covers `version --json`, schema-shaped
  dry-run NDJSON with stable `LS` event IDs, Windows alias parsing, and
  tests that fail when implemented verbs diverge from `fleet-cli-contract.json`.
- Shared PowerShell validation now syncs generated backend functions with
  explicit UTF-8 reads, excludes documented host-specific wrappers, and passes
  `Build-Scripts.ps1 -Validate` with 74 generated shared functions in sync.
- Local desktop tests now enforce the no-GitHub-Actions/no-dependency-bot
  repository policy, keep the release artifact contract tied to the local
  post-upload audit, and accept both single-quoted and double-quoted theme JS
  lists when comparing the theme manifest to the installer scripts.
- Backend script `RemoveSelfData` action now correctly defines
  `$global:BACKUP_ROOT` so backup directory cleanup is no longer silently
  skipped. Previously the variable was undefined, causing `Test-Path` to
  receive `$null` and always skip the backup removal step.
- Config restoration no longer silently swallows exceptions. If saved
  settings fail to apply to the UI (e.g., due to a renamed control), the
  error is now logged so users understand why defaults appeared instead of
  their saved choices.
- `Invoke-SpicetifyCli` now calls `WaitForExit(5000)` after `Kill()` on
  timeout, matching the pattern in `Invoke-ExternalScriptIsolated`. Prevents
  zombie process handles when Spicetify exceeds the hard timeout.
- Backend maintenance switch now has a `default` case that throws on
  unhandled actions, preventing silently-successful no-ops if a new action
  is added to `ValidateSet` but not to the dispatch switch.
- `Plan` action correctly excluded from operation journal `WouldChange`
  tracking, a dry-run plan no longer logs that mutations occurred.
- `Build-Scripts.ps1` function body extraction now uses `[regex]::Escape()`
  on function names so hyphens are treated as literal characters rather than
  regex metacharacters.

### Added
- Shared function drift validator (`Build-Scripts.ps1 -Validate`) that
  compares 86 functions shared between `LibreSpot.ps1` and the WPF backend
  script and reports any implementation mismatches. The `-Inventory` flag
  shows the full function distribution. CI runs this as a non-blocking
  warning step on every push. Currently 52 of 86 shared functions have
  drifted and need reconciliation as part of the shared-core extraction.
- Windows high-contrast mode support in the PowerShell GUI. When high-
  contrast is active, key surface, border, accent, and text brushes are
  overridden with SystemColors equivalents so controls remain readable.
  Mica backdrop is disabled under high-contrast because the transparent
  background would make text invisible.
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
  JSON plan entries for every operation an install would perform, downloads,
  SpotX patching, Spicetify CLI, themes, extensions, Marketplace, config saves,
  and watcher tasks, without mutating disk, PATH, or scheduled tasks. Each
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
- Former OpenSSF Scorecard automation: a weekly and push-to-`main` supply-chain hygiene scan that published to the public Scorecard API and uploaded SARIF, JSON, and triage artifacts. `schemas/scorecard-baseline.json` records accepted single-maintainer risks, and `SECURITY.md` documents the policy that low scores become roadmap items rather than silent warnings.
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
- Structured release-note categories covering breaking changes, security, features, bug fixes, compatibility, performance, dependencies, and docs; Dependabot PRs excluded from the main changelog.
- Draft package-manager manifests for winget, Scoop, and Chocolatey were removed before publication; distribution remains gated on signing and identity decisions.
- Trust and risk disclosure section in README covering what LibreSpot does/does not do, account risk context with ToS reference, and recovery instructions.
- Elevation boundary matrix (`schemas/elevation-boundary.json`) classifying every action as no-admin, prompts-for-admin, admin-only, or scheduled-task with mutating/destructive/toast-compatible flags; CI tests validate against live backend AllowedActions and AppCatalog.

- Added pull-request dependency review workflow that blocks vulnerable dependencies (moderate+) and disallowed licenses (AGPL-3.0-only, GPL-3.0-only) before merge.
- Added `-RemoveSelfData` CLI flag and "Remove LibreSpot data" WPF maintenance action that unregisters the watcher scheduled task and removes all LibreSpot-owned config, backups, logs, and crash reports without affecting Spotify or Spicetify.

### Changed
- WPF section frames now use the shared 12 px radius token instead of a
  hardcoded 18 px corner, keeping the shell within the documented radius system.
- WPF desktop shell second polish pass: softened global scrollbars, moved hero and sidebar micro-labels to title case, replaced backend-centric activity overlay copy with product-level run-log language, fixed log-count pluralization, and cleaned up support-bundle preview wording.
- WPF desktop shell polish pass: normalized the radius system to 6-12 px, removed pill-shaped badge/progress treatments, shortened the first-run rail copy, hid non-actionable informational health details from the sidebar, fixed Custom option-card title wrapping, forced dark native DWM caption colors, and made activity log empty/count states bind directly to the log collection. The UIA smoke runner now checks stable visible landmarks and named actionable controls.
- Community theme downloads are now commit-pinned and SHA256-verified, matching the existing integrity model for community extensions and the official themes archive. All five community themes (Catppuccin, Comfy, Bloom, Lucid, Hazy) use immutable commit-SHA archive URLs with `Confirm-FileHash` verification instead of mutable branch-based downloads. CI tests enforce that no branch-pinned archive URLs remain and that commit SHAs and hashes stay consistent across the script, WPF backend, and community-assets manifest.
- Dependency update checks (`Check-ForUpdates`) now use `Invoke-GitHubApiSafe` which reads `x-ratelimit-remaining` and `x-ratelimit-reset` headers, warns when rate limits are nearly exhausted, and provides actionable error messages with reset times for HTTP 403/429 responses instead of generic failure messages.

- ConfigurationService: handle `FileNotFoundException` separately from corrupt-config in `LoadResultAsync`, a config file deleted between the existence check and the open now correctly returns `Missing` instead of quarantining a non-existent file as corrupt.
- ThemeManager: fixed palette dictionary search using fragile `Contains("Palette.xaml")` that matched both palette filenames, now uses explicit `EndsWith` checks for each known palette.
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
- LibreSpot.ps1: `Test-SafeRemovalTarget` blocklist expanded from 7 to 17 entries to match the backend, adds ProgramData, ALLUSERSPROFILES, PUBLIC, OneDrive paths, Desktop, Documents, CommonDesktopDirectory, and CommonStartMenu.
- LibreSpot.ps1: community theme install now uses explicit `-LiteralPath` in 4 path operations to prevent wildcard glob expansion on paths with bracket/wildcard characters.
- BackendScriptService: fixed temp `.run.ps1` file leak when `process.Start()` throws, execution copy is now cleaned up on launch failure.
- BackendScriptService: moved cancellation token registration before `process.Start()` so cancellation works even if the process hangs during startup.
- BackendScriptService: drain async output pumps on the cancellation path to prevent stale callbacks after `RunAsync` returns.
- CrashReporter: wrapped `Directory.CreateDirectory` for log/crash directories in try/catch, previously an unhandled exception here silently disabled all crash handling for the entire session.
- CrashReporter: disposed `Process` handle returned by `Process.Start` when opening the crash folder.
- EnvironmentSnapshotService: guarded null/blank `configPath` in `GetSnapshot` to prevent `ArgumentNullException` on `File.Exists`.
- EnvironmentSnapshotService: added `GetSnapshotAsync` and made the view-model refresh await it, so the `schtasks.exe` auto-reapply probe (up to 1500ms) runs on the thread pool instead of blocking the UI dispatcher. Snapshot refreshes from the dashboard button, startup, and post-run no longer cause a visible hang.
- `Remove-PathSafely`: quoted the `$Path` argument passed to `icacls.exe` in both scripts, previously broke silently on paths containing spaces.
- `Expand-ArchiveSafely`: appended trailing separator to the destination path before `StartsWith` comparison to prevent a prefix-collision bypass (e.g., destination `C:\foo` incorrectly allowing extraction to `C:\foobar`).

### Security
- Added safe archive extraction helper (`Expand-ArchiveSafely`) that validates all ZIP entries for path traversal, absolute paths, destination escapes, entry count limits, and expanded size limits before extracting. All 8 extraction sites in both the stable script and WPF backend now use this helper instead of raw `ExtractToDirectory`.
- Hardened backend runtime directory with explicit ACLs (current user + Administrators only, inheritance disabled), per-process immutable execution copies to eliminate TOCTOU race between hash validation and process start, and SHA256 sidecar verification for the watcher scheduled task entry point.
- Stopped tracking build artifacts (`LibreSpot.exe`, `checksums.txt`) in git. The committed `checksums.txt` had drifted out of sync with `LibreSpot.ps1`, exactly the mismatch the README tells users to treat as tampering, and the committed `.exe` predated current source. Integrity now comes solely from CI-attested release assets (fresh SHA256 checksums + SBOM + provenance generated per tag), and the README verification steps point at release-downloaded copies rather than anything in a source checkout.
- Added PowerShell execution-policy / language-mode / application-control diagnostics. At run start LibreSpot now logs its PowerShell edition, version, language mode, and execution-policy scopes (`Get-PowerShellSecurityContext`), warns when the host already enforces ConstrainedLanguage, and classifies AppLocker/WDAC blocks in spawned-process output separately from ordinary errors (`Test-IsLanguageModeOrAppControlError`), in both the script and WPF backend. `SECURITY.md` documents that execution policy is a safety feature, not a security boundary, and that `-ExecutionPolicy Bypass` does not defeat application control; the guidance is always to ask an administrator, never to weaken enterprise controls. Locked by regression tests on both paths.
- Documented and locked the SpotX external-process execution contract. `SECURITY.md` now spells out, per executable, the allowed argument sources, quoting/execution strategy, timeout, output capture, and exit handling. Regression tests (both PowerShell paths) prove `Normalize-LibreSpotConfig` constrains every interpolated SpotX field to an allowlist or integer and that `Build-SpotXParams` interpolates nothing outside the known-safe set, so a crafted `config.json` cannot inject an extra command, and a future free-form argument fails CI until it is normalized. Verified against 16 injection payloads (quotes, semicolons, pipes, ampersands, newlines): zero leaks.
- Documented and preflight-gated CVE-2025-54100 (Windows PowerShell 5.1 web-content RCE, CVSS 7.8, fixed in the December 2025 Windows cumulative updates). `SECURITY.md` and the README trust section now name the two mitigations, SHA256 pinning (payload integrity) and Windows patch level (closing the parse-time vector), and clarify that hash pinning alone does not remove the vector. A non-blocking downloader preflight (`Get-DownloaderCveExposure`) in both the script and WPF backend logs a `WARN` once per run when a Windows PowerShell 5.1 host predates the December 2025 patch wave; PowerShell 7+ is unaffected and skipped.

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

## [v3.7.2] (2026-04-28)

**Hotfix.** The Easy-mode confirmation dialog was crashing the script the moment users clicked **Install recommended setup**.

### Fixed
- `Show-ThemedDialog` runs as a separate `Window` with its own here-string XAML, so it does NOT inherit the main window's resource dictionary. v3.7.0's blanket `Foreground="#FFE7EDF3"` → `Foreground="{StaticResource FgPrimaryBrush}"` sweep caught three references inside that dialog markup. When the install button fired the "Start Recommended Setup" confirmation, `XamlReader::Load` threw `Cannot find resource named 'FgPrimaryBrush'`, which propagated out of `Show-ThemedDialog` and tore down the install flow before any work started. Reverted those three to inline hex (`#FFE7EDF3`), the dialog renders as before and Easy/Custom installs proceed.
- Gotcha: every standalone XAML here-string (`$dlgXaml`, scheduled-task templates, future popouts) defines its own resource scope. Resource-token sweeps must explicitly skip them.

### Why this slipped past v3.7.0/v3.7.1 validation
`XamlReader::Load` ran clean on the main `$xaml` because the main window declares those brushes inline. The dialog only loads at click time, never exercised by my static checks. Lesson: when the script holds multiple XAML strings, each one needs its own `[XamlReader]::Load` round-trip in pre-flight validation.

---

## [v3.7.1] (2026-04-28)

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

## [v3.7.0] (2026-04-28)

**Premium UI overhaul.** The setup script keeps every behavior from v3.6.0 but now reads as polished product instead of dev tool. Sidebar navigation, Win11 Mica backdrop, semantic design tokens, hover-lift micro-interactions, and a shimmering install progress bar.

### Added
- **Win11 Mica backdrop** via `DwmSetWindowAttribute` P/Invoke (`DWMWA_SYSTEMBACKDROP_TYPE` = 38, `DWMSBT_MAINWINDOW` = 2). Combined with `DWMWA_USE_IMMERSIVE_DARK_MODE` and `DWMWA_WINDOW_CORNER_PREFERENCE` (rounded). Applied at `SourceInitialized`. Quietly degrades to the solid `SurfaceBase` (`#FF0B0E14`) baked into `Window.Background` on Windows 10 / pre-22H2.
- **Sidebar navigation** replacing the three-radio top tab bar. 252-px rail with brand block, Lucide icon nav items (Sparkles / Sliders / Wrench), update banner slot, and footer link tray (GitHub icon + SpotX/Spicetify hyperlinks).
- **Compact title bar** in the main column carries the mode headline + summary alongside minimize/close. The drag handle stays scoped to the title bar so ScrollViewer interactions in Custom/Maintenance keep working.
- **Design token resource dictionary**, `SurfaceBase/Elevated/Elevated2/Overlay/Sidebar`, `Border Subtle/Strong/Hover`, `Accent / AccentHover / AccentPressed / AccentSoft / AccentMuted`, `Info / Warning / Danger` (each with soft bg/border pair), `FgPrimary / FgSecondary / FgMuted / FgInverse`, plus a `ShimmerOverlayBrush`. Inline hex codes for foreground primary/secondary/muted swept to `{StaticResource}` references throughout the panels.
- **Type tokens**: `TypeHeroH1` (32px), `TypeH1` (22px), `TypeH2` (15.5px), `TypeBody` (13px), `TypeCaption` (11.5px). Default font upgraded to `Segoe UI Variable Display` with Segoe UI Variable / Segoe UI fallbacks. ClearType rendering forced.
- **Lucide icon set** as XAML `Geometry` resources, Home, Sliders, Wrench, Shield, Sparkle, Check, Download, Clock, External, Dot, Refresh, usable from any `Path`.
- **Hover-lift micro-interactions** on `ActionButton`: `TranslateTransform.Y` animates to `-1.5` over 120ms on hover, plus accent-colored `DropShadowEffect` glow on focus and hover. Pressed state dims to 0.84 opacity.
- **Shimmering install progress bar**, `RoundProgress` template now layers an animated `LinearGradientBrush` over the indicator using a forever-repeating `DoubleAnimation` translating from `-140` to `900` X over 1.6s. Indicator itself gets an accent-colored DropShadow for depth.

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
- The "TitleSubtext" tagline at the top, moved into the sidebar brand block as "Premium Spotify toolkit".

### Why
v3.6.0's UI was already dark, card-based, and accent-tinted, but read as "developer tool" because of flat 1px borders, scattered hex codes, text-bullet lists, and a top tab bar that felt like a form control. Premium installers (Linear, Vercel, 1Password) lean on Mica/Acrylic backdrops, sidebar nav, semantic color tokens, and motion. v3.7.0 picks up all four without changing a single install behavior or breaking any existing PowerShell-side `$ui[name]` reference.

---

## [v3.6.0] / [v4.0.0-preview.6] (2026-04-17)

**Track 4.2, auto-reapply watcher.** LibreSpot now notices when Spotify auto-updates itself and silently re-runs the saved SpotX patch so you don't come back to ads. Off by default, enable it from Maintenance > Protect and repair.

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
None of the other Spicetify/SpotX installers ship this, BlockTheSpot-Installer, SpotX-Spicetify-Universal-Installer, and Spicetify Manager all require the user to manually click "Reapply After Update" after every Spotify auto-update. This closes that loop.

## [v3.5.1] (2026-04-17)

Hardening + release-pipeline pass. Fixes bugs introduced in v3.5.0, tightens the release workflow, and adds regression guards so the issues we just fixed can't silently creep back.

### Historical release automation
- **Preflight job** runs before build. Resolves the tag, asserts `LibreSpot.ps1:$global:VERSION == Backend.ps1:$global:VERSION` (the exact invariant v3.5.1 breaks), asserts the right version file matches the tag (`PS1` for stable tags, `csproj` for `-preview.N` tags), parses both PowerShell files with `[Parser]::ParseFile` so a syntax error fails the tag before PS2EXE runs, and enforces a regression guard that forbids `chrome_elf.dll` / `xpui.spa.bak` from re-entering `Get-ExistingSpotifyPatchSignature`.
- **PS2EXE pinned** to `1.0.15` so a breaking upstream release can't corrupt a tagged build.
- **Unit tests run before WPF publish**. A red AppCatalog/Configuration/PowerShellRegression test fails the tag.
- **Release assets now include raw `LibreSpot.ps1`**, the README's `irm .../releases/latest/download/LibreSpot.ps1 | iex` one-liner was 404'ing because only the `.exe` was ever uploaded. Also attested for provenance.
- **`gh release create` fallback**, if the release doesn't exist yet for the tag, one is auto-created with generated notes before assets upload.
- **Explicit checksum list** replaces the previous `sha256sum *.exe *.json` glob that would silently skip a missing asset.

### Regression tests (tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs)
- Parses `LibreSpot.ps1` as text and asserts `Get-ExistingSpotifyPatchSignature`'s function body does not reference `chrome_elf.dll` or `xpui.spa.bak`.
- Asserts `LibreSpot.ps1:$global:VERSION` and `Backend.ps1:$global:VERSION` stay in sync.
- Asserts `Compare-LibreSpotVersions` still uses `[Version]` parsing and strips `-preview.*` / `-rc.*` suffixes.
- Asserts `Compare-LibreSpotVersions` remains on the worker-runspace export list (or `Check-ForUpdates` hits a "command not found" at runtime).
- Asserts `Start-SelfUpdateBannerRefresh` uses `ThreadPool.QueueUserWorkItem`, catches any revert that would reintroduce the 5-second UI freeze on launch.

### Defensive fixes (src/LibreSpot.Desktop/ViewModels/MainViewModel.cs)
- `CancelRunningBackend()` and the cancel-prompt confirm handler now swallow `ObjectDisposedException` explicitly. Other exceptions still propagate, they'd indicate a real programming bug. `Dispose()` stays idempotent.

### Fixes carried over from the earlier v3.5.1 commit

### Fixed
- **Foreign-patch detection fired on every launch** (introduced in v3.5.0). The previous signature list checked for `chrome_elf.dll` (part of every Spotify install, LibreSpot itself throws if it is *missing*) and `xpui.spa.bak` (created by SpotX's own backup step on every successful run). Revised to only match files BlockTheSpot-style injectors drop: `dpapi.dll`, `config.ini`, `version.dll`, `winmm.dll` next to `Spotify.exe`.
- **Backend.ps1 stamped the wrong version** in its HTTP User-Agent and internal log lines (`LibreSpot/3.3.0` instead of the real shell version). Synced to `3.5.0` with a comment noting the release workflow should fail a build when these drift.
- **Self-update check blocked the UI thread for up to 5 seconds on launch** when GitHub was slow. Refactored to a pure-.NET `HttpWebRequest` running on a ThreadPool thread, with cache write + UI update marshaled back through `Dispatcher.BeginInvoke` at idle priority. The cache read path is still synchronous (filesystem-only) and returns instantly on a warm cache.
- **`Check-ForUpdates` used lexical string comparison** for Spicetify CLI, Marketplace, and LibreSpot versions. That would have reported `v2.43.10` as *older* than `v2.43.9`. Replaced with a new `Compare-LibreSpotVersions` helper that parses the numeric prefix via `[Version]`, strips `-preview.*`/`-rc.*`, and treats stable as newer than a pre-release with the same prefix.
- **`$sender` parameter in `Window.Add_Closing`** shadowed PowerShell's automatic `$Sender` variable (PSAvoidAssignmentToAutomaticVariable). Renamed to `$closingSource`.

### Changed
- Exported `Compare-LibreSpotVersions` into the worker runspace so `Check-ForUpdates` (which runs there) can call it.
- `Save-SelfUpdateCache` is invoked only from the dispatcher thread so `ConvertTo-Json` / `Set-Content` never run concurrently with the main runspace from a ThreadPool thread.
- `Invoke-SelfUpdateHttp` parses the GitHub response with pure regex (no `ConvertFrom-Json`) and inlines the version compare, so the ThreadPool path never re-enters the main runspace.

### Deferred from this pass (tracked for later)
- ~35 PSScriptAnalyzer `PSUseApprovedVerbs` warnings on private helpers (`Normalize-`, `Module-*`, `Load-`, `Apply-`, `Capture-`, `Build-`, `Download-`, `Check-`, `Reapply-`). Renaming cascades across the worker-runspace function-export list.
- Monolith → module extraction for the ~400 lines of config logic duplicated between `LibreSpot.ps1` and `Backend.ps1`.
- Maintenance action dispatch table (currently a ~300-line `if/elseif` chain in the worker block).

## [v3.5.0] / [v4.0.0-preview.5] (2026-04-17)

Competitor-parity release. Four items from the ROADMAP Track 4 shipped end-to-end (PowerShell monolith + WPF backend + C# model).

### Added
- **Self-update check**, on launch, async-queries `api.github.com/repos/SysAdminDoc/LibreSpot/releases/latest`, shows a subtle green "Update available →" hyperlink in the title bar when a newer release exists. Result cached 24h in `%APPDATA%\LibreSpot\update-check.json` to stay under the 60 req/hr anonymous API limit. Zero telemetry, single GET, nothing else sent.
- **Pre-patched Spotify detection**, scans Spotify's install directory for BlockTheSpot-style injectors (`dpapi.dll`, `config.ini`, `version.dll`, `winmm.dll` next to `Spotify.exe`) and shows a themed warning dialog once per session before the user starts patching. Tells them to run **Maintenance > Full Reset** first if they want a clean slate.
- **Spotify version dropdown** in Custom Install > Advanced, inline manifest of 5 known-good Spotify builds (`auto`, `1.2.86.502`, `1.2.85.519`, `1.2.53.440.x86`, `1.2.5.1006.win7`) with per-entry hint text. Emits SpotX's `-version <string>` when non-default. Config key: `SpotX_SpotifyVersionId`.
- **`-Clean` CLI flag**, `irm URL | iex -clean` (or `powershell.exe -File LibreSpot.ps1 -clean`) pre-ticks Easy mode + CleanInstall for a one-shot nuke-and-rebuild flow.

### Changed
- PowerShell script: v3.4.0 → **v3.5.0**.
- WPF desktop shell: v4.0.0-preview.4 → **v4.0.0-preview.5**.
- `InstallConfiguration` C# model gains `SpotX_SpotifyVersionId` property with Clone + Normalize support.
- `AppCatalog.SpotifyVersionManifest` exposes the version list to the WPF shell (record type `SpotifyVersionEntry`).

## [v4.0.0-preview.4] (2026-04-17, pre-release)

### Added
- **Mica backdrop** on Windows 11 build 22621+ via `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE, DWMSBT_MAINWINDOW)`, paired with `DWMWA_USE_IMMERSIVE_DARK_MODE` so the title bar matches the dark shell ([Services/Win11ShellIntegration.cs](src/LibreSpot.Desktop/Services/Win11ShellIntegration.cs)). Older Windows falls back silently to the flat canvas brush.
- **TaskbarItemInfo progress mirroring**, the Windows taskbar icon now tracks the run state (`None`/`Indeterminate`/`Normal`/`Paused`/`Error`) so users see progress even when LibreSpot is minimized. `ProgressValue` is kept in sync with the in-app 0-100 scale.
- **Serilog crash reporter** ([Services/CrashReporter.cs](src/LibreSpot.Desktop/Services/CrashReporter.cs)), structured daily rolling log under `%LOCALAPPDATA%\LibreSpot\logs\` (14-day retention), full crash dumps under `%LOCALAPPDATA%\LibreSpot\crashes\`, and a crash dialog that offers "copy path + open folder" so users can file issues without the app needing to phone home. Hooks `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, `Dispatcher.UnhandledException`.
- **Accessibility pass**, `AutomationProperties.Name` + `HelpText` on previously unlabeled icon buttons (Refresh status, Copy log header variant), `AutomationProperties.LiveSetting="Polite"` on the activity badge so screen readers announce state transitions.
- A former GitHub Actions release workflow was triggered on `v*` tags. It built a PS2EXE and .NET 8 WPF asset, emitted checksums and an SBOM, and attempted build-provenance attestations. It was retired in favor of the documented local release procedure and GitHub immutable-release attestations.

### Changed
- WPF desktop shell: v4.0.0-preview.3 → **v4.0.0-preview.4**.
- New NuGet dependencies: `Serilog 4.2.0`, `Serilog.Sinks.File 6.0.0`.

## [v3.4.0] (2026-04-17)

### Added
Six new SpotX flags surfaced end-to-end (Custom Install UI + config persistence + fingerprint + `Build-SpotXParams`):
- **Privacy**: `-sendversion_off` (default **on**, blocks SpotX's outbound version notification introduced in the April 2026 SpotX update).
- **Core behavior**: `-start_spoti` (auto-launch Spotify after install).
- **Advanced**:
  - `-devtools`, enable Spotify Chromium Developer Tools (Spicetify extension authors).
  - `-mirror`, use GitHub.io mirror for SpotX assets when `raw.githubusercontent.com` is blocked.
  - `-confirm_spoti_recomended_uninstall`, force SpotX's uninstall-then-reinstall flow.
  - `-download_method {curl|webclient}`, force SpotX's downloader choice (ComboBox in PowerShell GUI; WPF shell defers custom XAML binding to a later preview).
- New **Privacy** and **Advanced** inset panels in the PowerShell Custom Install view.
- Matching `OptionDefinition` entries in the WPF shell (`Core`/`Advanced` sections) auto-render via the shared `OptionTemplate`.

### Changed
- PowerShell script: v3.3.1 → **v3.4.0**.
- WPF desktop shell: v4.0.0-preview.2 → **v4.0.0-preview.3**.
- `InstallConfiguration` C# model gains 6 new properties with `Clone()` + `NormalizeConfiguration()` support.
- `Build-SpotXParams` (both PowerShell monolith and WPF Backend) extended to emit the new flags.

### Verified
- All 22 existing `Build-SpotXParams` flag emissions cross-checked against SpotX `run.ps1` param block on 2026-04-17, spellings correct.
- Six truly-missing flags above identified as the only net-new additions worth shipping in that release; `-version`, `-CustomPatchesPath`, `-language`, `-urlform_goofy`, `-idbox_goofy`, `-err_ru` intentionally deferred (they feed into future roadmap tracks or are niche).

## [v3.3.1] (2026-04-17)

### Fixed
- **Silent no-op**: `-new_fullscreen_mode` corrected to `-newFullscreenMode` (real SpotX flag is camelCase). The "Experimental fullscreen mode" GUI toggle never actually passed through to SpotX on v3.3.0. Fixed in both `LibreSpot.ps1:Build-SpotXParams` and `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`.
- Re-verified every flag in `Build-SpotXParams` against SpotX `run.ps1` param block (2026-04-17), all other flags correct.

### Changed
- `-SpotifyPath` gotcha softened to a historical note; SpotX `run.ps1` accepts it as a supported parameter.
- WPF desktop shell bumped to v4.0.0-preview.2 (csproj now declares `<Version>`/`<AssemblyVersion>`/`<FileVersion>`).

## [v4.0.0-preview.1] (2026-04-16, pre-release)

### Added
- Native WPF desktop shell (.NET 8, MVVM) replacing the PS2EXE GUI wrapper
- Token-based design system: surface elevation, semantic intent, motion, easing, radius, and spacing scales read from a single source of truth
- Focus rings as overlay borders (no 1px layout jitter on keyboard focus)
- Button hover-tint via Opacity animation + tactile 0.985× press-scale
- Indeterminate progress shimmer, rotating ComboBox chevron, fade-in checkbox checkmarks
- Overlay cards (activity + prompt) fade + scale-in on every show via DataTrigger EnterActions
- State-aware activity badge, accent pulse while running, Danger + "Needs attention" on failure, "Run complete" on success, "Working…" during indeterminate runs
- Staggered accent-dot empty state for the log panel
- Structured stdout protocol between WPF shell and embedded PowerShell backend
- Embedded backend script extracted to LocalAppData and SHA-verified before each run
- Action allow-list validation before any PowerShell dispatch
- Cancellation chain tears down the child process tree on window close

### Changed
- Backend flows hardened for the new shell integration (install/maintenance pipelines preserved)
- Desktop UX polish across controls, states, and transitions
- Single-file self-contained .NET 8 executable (no runtime dependency)

## [v3.3.0] (2026-04-05)

### Added
- Five new SpotX GUI options: Plus features (`-plus`), experimental fullscreen (`-newFullscreenMode`), humorous progress bar (`-funnyprogressBar`), experimental Spotify features (`-exp_spotify`), lyrics block (`-lyrics_block`)
- Full config pipeline wiring for new options: XAML, load/save, normalization, fingerprint, Build-SpotXParams, summary toggles
- Mutual-exclusivity enforcement between `-lyrics_block` and `-old_lyrics` via dependency-aware UI

### Changed
- SpotX pinned to `0abf98a3` (targets Spotify 1.2.86.502)
- Spicetify CLI bumped to v2.43.1

## [v3.2.0] (2026-04-15)

### Added
- Self-elevation that handles .ps1, .exe, and inline scriptblock launch contexts
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

## [v3.1.1] (2026-03-27)

- Fixed theme preview crash: use synchronous download
- Fixed theme preview: TLS 1.2 + ThreadPool instead of WebClient async
- Reverted custom apps/packs, kept theme preview
- Removed Statistics and Lyrics Plus custom apps (broken)

## [v3.1.0] (2026-03-27)

- Audit fixes: anti-hang, apply recovery, new options
- Fixed blank screen: let SpotX manage Spotify version compatibility
- Added live theme preview with async image loading

## [v3.0.6] (2026-03-27)

- Updated SpotX to 6070bbcf to fix blank screen on Spotify 1.2.85.519
- Compiled v3.0.6 executable

## Roadmap archive, 2026-08-10, ROADMAP.md

<details>
<summary>Original roadmap snapshot</summary>

```markdown
# LibreSpot Roadmap

Active roadmap for forward-looking work only. Completed work lives in git
history and `CHANGELOG.md`. Research conclusions live in `RESEARCH.md`.

Last consolidated: 2026-06-01.
Last researched: 2026-06-06, Cycle 22.

## Implementer Instructions (for the build machine)

This roadmap is fed continuously by the research machine. On every pass, the
build machine should:

1. `git pull --rebase` to get the latest researched items before starting.
2. Work the open implementer-actionable items top-down by priority (P0 -> P3).
   Build them properly: multi-file structure, real error handling, no runtime
   auto-install hacks, version strings synced, docs/CHANGELOG updated in the
   same commit.
3. In addition to building items, run a full UX audit each pass. Walk every
   screen, page, dialog, form, table, and empty/loading/error/disabled state
   across light, dark, and high-contrast themes. Check onboarding, navigation
   clarity, spacing, contrast, alignment, clipping, overflow, hierarchy,
   microcopy, destructive-action guards, keyboard and screen-reader
   accessibility, and trust signals. Fix what you find, or file it back as a
   new implementer-actionable roadmap item if it is larger than a pass.
4. Check off each item you complete, leave it in place, commit per logical
   change with a "why" message, and push.
5. Never edit this Implementer Instructions block or the Researcher Queue
   headings. Never force-push.

## Current State

- Public latest stable release: v3.7.2 (verified 2026-07-14).
- Current script source line: v3.7.4 (not yet the public stable release).
- Native WPF shell line: v4.0.0-preview.17.
- Release pipeline now builds PS2EXE and WPF artifacts with checksums, SBOMs, and
  build provenance attestations.
- Auto-reapply watcher, self-update checks, pre-patched Spotify detection,
  Spotify version selection, and the v3.7 UI refresh have shipped.
- Point-in-time dependency pins (synced 2026-07-08):
  - SpotX `550bc72c` for Spotify `1.2.93`
  - Spicetify CLI v2.44.0
  - Marketplace v1.0.9
  - Spicetify themes `df033493`

## Next Release Queue

| Priority | Track | Work | Exit criteria |
|---|---|---|---|

## Structural Cleanup (July 8, 2026)

Items surfaced by the July 8, 2026 structural audit. Focus: eliminate
silent-drift sync bugs, reduce monolith file sizes, and improve testability.

## Distribution And Trust

Distribution work is sequenced behind the rebrand and signing decisions (see
`Roadmap_Blocked.md`). Once those are resolved:

1. Publish winget manifests for portable assets.
2. Add Velopack packaging for the WPF shell.
3. Create a Scoop bucket with `checkver` and `autoupdate`.
4. Submit Chocolatey only after signing and checksum automation have settled.

Microsoft Store/MSIX remains a poor fit because the app needs to patch files in
the user's Spotify installation and interact with classic desktop locations.

## Research Backlog

These require fresh research before implementation:

- Whether the April 2026 SpotX and Spicetify pin guidance is still current.
- Spotify Connect regression test fixture.
- Spicetify v3 readiness and migration risk.

## 🔬 Researcher Queue (Cycle 11, 2026-06-04)

Cycle 11 inspects maintainability risks in the PowerShell and WPF code shape.
It does not implement refactors; it turns measured duplication and file size
into implementation-ready extraction and quality-gate work. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where release sequencing decisions are required.

## 🔬 Researcher Queue (Cycle 17, 2026-06-06)

Cycle 17 inspects theme selection and preview reliability across the stable
PowerShell GUI and native WPF shell. Cycle 4 already covers community asset
supply-chain pinning for install-time downloads; this pass focuses on the
user-facing browse/preview surface, stale screenshots, blocking image loads, and
WPF parity before v4 stable. Tags: 🔬 = researcher-added this cycle; 🤖 =
implementer-actionable now; 🔧 = operator-needed where catalog policy decisions
are required.


## 🔬 Researcher Queue (Cycle 18, 2026-06-06)

Cycle 18 narrows the broad Community Sharing release-queue row into profile
export/import, local preset management, and Marketplace-state boundaries. Cycle
3 already covers schema versioning for the internal `config.json`; this pass
focuses on the separate user-facing file/URI experience and the safety preview
required before imported settings can mutate Spotify or Spicetify. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where hosted sharing or cloud policy decisions are required.

## 🔬 Researcher Queue (Cycle 19, 2026-06-06)

Cycle 19 turns the earlier operation-journal backlog item into the concrete
undo/dry-run product contract needed for v4 stable and fleet deployment. The
core finding is that LibreSpot already has several guardrails, but the backend
still mutates files, PATH entries, scheduled tasks, and Spotify/Spicetify state
without a shared preflight plan, reversible operation token, or post-run undo
receipt. Any undo UI must start by making reversibility explicit instead of
implying that every cleanup path can be rolled back.

### Findings

- The v4 stable scope already calls for an undo-selected-actions pane for
  reversible operations such as update blocking, shortcuts, scheduled tasks,
  and config changes, while the Fleet CLI section calls for `--dry-run` and
  PowerShell `-WhatIf` parity (`ROADMAP.md:52`, `ROADMAP.md:72`,
  `ROADMAP.md:107`).
- Cycle 3 already asks for a destructive-action operation journal, including
  planned action, target, safety decision, result, rollback hint, and dry-run
  output (`ROADMAP.md:539`). Cycle 19 should not replace that item; it should
  define the contracts the journal and UI must expose.
- The WPF backend action surface is still a string `ValidateSet` with install,
  restore, uninstall, reset, and watcher actions, with no `DryRun`,
  `ShouldProcess`, or journal parameter (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1`).
- The backend config writer creates a temp file and transient backup, then
  deletes the backup after replacement, so it is atomic but not a user-visible
  rollback point (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:681`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:700`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:710`).
- Watcher enable/disable mutates scheduled-task state and then saves the
  config preference, but it does not capture the previous task XML or previous
  `AutoReapply_Enabled` value as an undo token
  (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:716`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2268`).
- `UninstallSpicetify` and `FullReset` remove config directories, CLI
  directories, PATH entries, Spotify packages/files, and scheduled tasks through
  helper functions such as `Remove-PathSafely`, `Remove-PathEntry`, and
  `Module-NukeSpotify`, but there is no preflight JSON plan that the UI or CLI
  can show before mutation (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1222`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1301`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1657`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1749`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2240`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2253`).
- Current rollback behavior is narrow: Spicetify apply failures attempt
  `spicetify restore`, and the stable PowerShell backup-restore path can copy a
  temporary snapshot back after a failed restore. These are failure recovery
  paths, not a general undo model
  (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2128`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2139`,
  `LibreSpot.ps1:4117`, `LibreSpot.ps1:5393`, `LibreSpot.ps1:5405`,
  `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:605`).
- The WPF confirmation dialog uses static summaries for maintenance actions and
  saves install configuration before running the backend, so users cannot yet
  review a computed list of intended file, registry, PATH, task, and config
  changes (`src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1386`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1434`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1479`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1901`).
- Microsoft PowerShell guidance says `CmdletBinding(SupportsShouldProcess)`
  adds `-Confirm` and `-WhatIf`, and that code should call
  `$PSCmdlet.ShouldProcess(...)` close to the actual change. PSScriptAnalyzer
  flags functions that declare `SupportsShouldProcess` without calling
  `ShouldProcess`, or vice versa:
  https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_functions_cmdletbindingattribute,
  https://learn.microsoft.com/en-us/powershell/scripting/learn/deep-dives/everything-about-shouldprocess,
  https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/rules/shouldprocess.
- Windows Installer rollback research reinforces the same constraint: rollback
  works because an installer creates rollback scripts and saves deleted files as
  it processes the install, while direct custom actions require explicit
  rollback custom actions and still may not be fully reversible:
  https://learn.microsoft.com/en-us/windows/win32/msi/rollback-installation,
  https://learn.microsoft.com/en-us/windows/win32/msi/rollback-custom-actions.

## 🔬 Researcher Queue (Cycle 20, 2026-06-06)

Cycle 20 narrows the broad diagnostics/repair queue into a native-WPF health
model. The key gap is not that LibreSpot lacks status text: both shells already
show useful status. The gap is that the WPF shell's status model is still a
small boolean snapshot, while the stable PowerShell shell already inspects a
larger five-component maintenance state and offers backup/restore affordances.
v4 stable should convert those checks into typed issues with targeted repair
actions, support-bundle output, and clear boundaries around what LibreSpot can
diagnose automatically.

### Findings

- The release queue calls for status-at-a-glance and repair flows for Spotify,
  SpotX, Spicetify, backups, scheduled task state, and last patch time
  (`ROADMAP.md:56`, `ROADMAP.md:75`).
- The WPF `EnvironmentSnapshot` currently tracks only Spotify installed,
  Spicetify installed, saved config, config folder, and auto-reapply task
  booleans, then derives a broad `Stack ready` / `Partial setup` / `Clean slate`
  summary (`src/LibreSpot.Desktop/Models/AppCatalog.cs:143`).
- `EnvironmentSnapshotService.GetSnapshot` checks `%APPDATA%\Spotify\Spotify.exe`,
  `%LOCALAPPDATA%\spicetify\spicetify.exe`, the supplied config path, the config
  directory, and one scheduled task probe. It does not inspect Spotify version,
  SpotX patch state, Spicetify config values, Marketplace files, theme status,
  backup count, last run result, watcher state age, last patch time, or log/crash
  health (`src/LibreSpot.Desktop/Services/EnvironmentSnapshotService.cs:16`).
- The WPF dashboard binds that compact snapshot into three status rows and a
  freshness card, plus a separate watcher panel; it has refresh and folder-open
  affordances but no issue list or per-issue repair actions
  (`src/LibreSpot.Desktop/MainWindow.xaml:470`,
  `src/LibreSpot.Desktop/MainWindow.xaml:593`,
  `src/LibreSpot.Desktop/MainWindow.xaml:1428`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:994`,
  `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1627`).
- The stable PowerShell maintenance view already computes a richer state:
  Marketplace file/config presence, active theme injection, backup count, a
  5-component readiness count, next-step guidance, and enablement/tooltips for
  backup, restore, reapply, restore vanilla, uninstall, and reset
  (`LibreSpot.ps1:3904`, `LibreSpot.ps1:3935`, `LibreSpot.ps1:3952`,
  `LibreSpot.ps1:3991`, `LibreSpot.ps1:4004`).
- WPF `CrashReporter` writes rolling logs under `%LOCALAPPDATA%\LibreSpot\logs`
  and crash reports under `%LOCALAPPDATA%\LibreSpot\crashes`, retains logs for
  14 days and crash reports for 30 days, and offers copy/open buttons in the
  crash dialog; it is not yet integrated with the maintenance dashboard or a
  sanitized support bundle (`src/LibreSpot.Desktop/Services/CrashReporter.cs:14`,
  `src/LibreSpot.Desktop/Services/CrashReporter.cs:51`,
  `src/LibreSpot.Desktop/Services/CrashReporter.cs:110`,
  `src/LibreSpot.Desktop/Services/CrashReporter.cs:369`,
  `src/LibreSpot.Desktop/Services/CrashReporter.cs:385`).
- The backend has a narrow `Get-SpicetifyDiagnosticSnapshot` around apply
  failures, logging Spicetify `spotify_path`, `prefs_path`, `xpui.spa`, and
  Spotify executable existence, but those checks are not surfaced as reusable
  dashboard diagnostics (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2064`,
  `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2094`).
- Tests currently cover the snapshot config directory and auto-reapply probe
  only; there are no fixture-backed tests for Marketplace missing, stale
  Spicetify config, missing backups, broken watcher logs, log redaction, or
  repair-action selection (`tests/LibreSpot.Desktop.Tests/EnvironmentSnapshotServiceTests.cs:10`,
  `tests/LibreSpot.Desktop.Tests/EnvironmentSnapshotServiceTests.cs:40`).
- Spicetify's own CLI docs position `backup`, `apply`, and `restore` as core
  operations; they also document `spicetify backup apply` after Spotify updates
  and `spicetify restore backup apply` for full restore/reapply:
  https://spicetify.app/docs/cli,
  https://spicetify.app/docs/cli/commands.
- Current upstream/community evidence still shows Marketplace-specific pain:
  the Marketplace issue list has recent open items for extensions/themes
  disappearing, black screen, and the Marketplace button not appearing; a
  Spicetify CLI issue from April 2026 reports Marketplace missing on Spotify
  `1.2.87.414` with Spicetify `2.43.1`:
  https://github.com/spicetify/marketplace/issues,
  https://github.com/spicetify/cli/issues/3816.
- Microsoft .NET diagnostics docs describe `EventSource` as a structured
  logging mechanism useful for diagnostic tasks, with explicit event IDs and
  stable event contracts; Windows diagnostics/privacy guidance reinforces that
  crash dumps and enhanced error data require additional permission. LibreSpot
  support export should stay local, opt-in, and reviewable:
  https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource,
  https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource-instrumentation,
  https://support.microsoft.com/en-us/windows/diagnostics-feedback-and-privacy-in-windows-28808a2b-a31b-dd73-dcd3-4559a5199319.

## 🔬 Researcher Queue (Cycle 21, 2026-06-06)

Cycle 21 turns the Fleet Deployment row into a command and artifact contract.
The main finding is that LibreSpot already has useful headless building blocks,
but not a fleet-ready CLI surface. The stable PowerShell script has a few
watcher flags and the WPF backend has a structured action protocol, yet both
released EXE artifacts are GUI-first. Fleet support should therefore start with
a console-capable entrypoint and a stable machine contract instead of trying to
bolt `--silent` onto the current WPF button actions.

### Findings

- The roadmap already names Fleet Deployment as a P1 track: silent/quiet flags,
  JSON answer files, `--detect --json`, explicit exit codes, NDJSON logs,
  `uninstall --silent --purge --yes --keep-spotify`, validate, `--dry-run`,
  and deployment examples for WinRM, PSRemoting over SSH, PDQ Deploy, Intune
  Win32 apps, and SCCM-style return codes (`ROADMAP.md:96`).
- The stable PowerShell script only parses `-clean`, `-watch`,
  `-installwatcher`, and `-uninstallwatcher` from raw `$args`; there is no
  `install`, `detect`, `status`, `validate`, `uninstall`, `repair`, `--json`,
  `--ndjson`, `--answer-file`, `--silent`, or `--dry-run` parser
  (`LibreSpot.ps1:118`, `LibreSpot.ps1:124`).
- The stable script watcher path is truly headless and exits before WPF loads,
  but only for watcher tasks. Regression tests explicitly protect the watcher
  exit branches so they do not fall through into XAML (`LibreSpot.ps1:3196`,
  `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:194`,
  `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:212`).
- The WPF backend script exposes a `ValidateSet` of GUI/maintenance actions and
  one config path parameter, not a user-facing CLI verb model
  (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1`).
- `BackendScriptService` validates actions, writes the embedded backend to a
  runtime directory, starts Windows PowerShell hidden, passes `-Action` and
  `-ConfigPath` using `ArgumentList`, parses `@@LS@@|kind|level|payload`
  messages, and only returns success/failure plus a string error. This is a
  good internal substrate, but it lacks a public stdout/stderr contract,
  schema version, status JSON, exit-code taxonomy, answer-file input, log
  directory choice, or dry-run mode (`src/LibreSpot.Desktop/Services/BackendScriptService.cs:20`,
  `src/LibreSpot.Desktop/Services/BackendScriptService.cs:37`,
  `src/LibreSpot.Desktop/Services/BackendScriptService.cs:105`,
  `src/LibreSpot.Desktop/Services/BackendScriptService.cs:199`).
- The WPF maintenance catalog currently lists Check Updates, Reapply, Restore
  Vanilla, Uninstall Spicetify, and Full Reset. It does not model fleet-only
  verbs such as detect, status, validate, export diagnostics, or repair issue
  by health-action id (`src/LibreSpot.Desktop/Models/AppCatalog.cs:275`).
- The release workflow builds `LibreSpot.exe` with PS2EXE `-NoConsole
  -RequireAdmin` and publishes `LibreSpot-Desktop.exe` as a self-contained WPF
  executable. Those are appropriate GUI artifacts, but they are poor primary
  fleet CLIs because console output and headless exit-code behavior are not the
  first-class artifact contract in the retired release automation.
- Microsoft Intune Win32 app docs make return codes and detection rules first
  class: admins configure success/failure/retry/soft-reboot/hard-reboot return
  codes, and detection rules determine whether an app is present. Intune
  troubleshooting docs also show a detection script pattern that writes the
  detected version to STDOUT and exits `0`, or exits nonzero when detection
  fails:
  https://learn.microsoft.com/en-us/intune/app-management/deployment/add-win32,
  https://learn.microsoft.com/en-us/intune/app-management/deployment/troubleshoot-win32.
- WinGet docs require usable silent install behavior for package submissions,
  define manifest metadata/installer SHA fields, document `winget install
  --silent`, local manifests, agreement acceptance for scripts, and common
  silent switch expectations:
  https://learn.microsoft.com/en-us/windows/package-manager/winget/install,
  https://learn.microsoft.com/en-us/windows/package-manager/package/manifest.
- Windows Installer exit-code docs are still the lingua franca for many admin
  tools: `0` is success, `3010` is success with reboot required, `1641` is
  success with reboot initiated, `1602` is user cancel, `1603` is fatal failure,
  and `1618` is another install already in progress:
  https://learn.microsoft.com/en-us/windows/win32/msi/error-codes.
- PowerShell docs confirm why LibreSpot must explicitly use `exit`: scripts
  invoked through `pwsh -File` return `1` for terminating exceptions, an
  explicit `exit` value when used, and `0` when the script completes
  successfully:
  https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_automatic_variables.
- `ConvertTo-Json` can produce compact JSON and warns about depth truncation in
  newer PowerShell versions, which matters for stable status/receipt output;
  NDJSON's public spec requires each JSON text to be followed by a newline:
  https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/convertto-json,
  https://github.com/ndjson/ndjson-spec.

### New / Refined Backlog Items


## 🔬 Researcher Queue (Cycle 22, 2026-06-06)

Cycle 22 inspects package-manager distribution and update channels after the
Cycle 21 Fleet CLI contract. The key conclusion is that LibreSpot needs a
distribution channel matrix before any more manifest files are added. The
roadmap already names winget, Scoop, Chocolatey, and Velopack, but those
channels should not all install the same artifact or own updates in the same
way. Package work should consume a signed release artifact manifest and choose
one updater per artifact, otherwise users can end up with duplicate install
roots, mismatched package IDs, and stale checksums.

### Findings

- The top-level distribution order already says: finish the rebrand decision,
  complete SignPath enrollment, publish winget manifests for portable assets,
  add Velopack for the WPF shell, create a Scoop bucket, then consider
  Chocolatey after signing/checksum automation settles (`ROADMAP.md:86`).
- Existing roadmap items already cover draft winget/Scoop/Chocolatey manifests,
  a Velopack app identity/update feed, package identity before public manifests,
  generated release artifact contracts, and separating generated release assets
  from source files (`ROADMAP.md:196`, `ROADMAP.md:398`, `ROADMAP.md:460`,
  `ROADMAP.md:1448`).
- The repo does not currently contain package-manager manifests, a `docs/deployment`
  directory, a Velopack config, or a package bucket. The only local distribution
  files found by targeted scan are `SIGNPATH.md` and the retired release automation,
  and `src/LibreSpot.Desktop/app.manifest`.
- `SIGNPATH.md` still describes two signed PE artifacts, `LibreSpot.exe` and
  `LibreSpot-Desktop.exe`. Cycle 21 adds a required future
  `LibreSpot.Cli.exe`, so the SignPath artifact configuration, README
  verification instructions, release workflow, checksum list, SBOM/provenance
  subjects, and package-manager manifest templates all need a third-artifact
  update before public distribution (`SIGNPATH.md:28`, `SIGNPATH.md:76`,
  `SIGNPATH.md:102`).
- The release workflow currently builds PS2EXE, WPF, checksums, SBOM, and
  attestations for `LibreSpot.exe`, `LibreSpot.ps1`, `LibreSpot-Desktop.exe`,
  and `LibreSpot.sbom.cdx.json`; it has no generated release manifest JSON that
  downstream package templates can consume.
- README still leads with the `irm ... LibreSpot.ps1 | iex` one-liner and says
  signing is pending for two artifacts. Broader package-manager distribution
  should not make that one-liner the only documented path once signed GUI,
  desktop, and CLI artifacts exist (`README.md:18`, `README.md:181`).
- WinGet manifest docs require package metadata, installer URL, installer SHA,
  architecture, installer type, and package identifiers. WinGet can install from
  local manifests and has a `portable` installer type, while `winget install`
  exposes `--silent`, `--manifest`, `--installer-type`, and `--rename` for
  portable packages:
  https://learn.microsoft.com/en-us/windows/package-manager/package/manifest,
  https://learn.microsoft.com/windows/package-manager/winget/install.
- Scoop manifests are JSON and use fields such as `url`, `hash`, `bin`,
  `shortcuts`, `checkver`, and `autoupdate`; the wiki documents using
  `checkver.ps1` and autoupdate definitions to update manifests from upstream
  release pages:
  https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests,
  https://github.com/ScoopInstaller/Scoop/wiki/App-Manifest-Autoupdate.
- Chocolatey packaging is a PowerShell package wrapper around installers or
  embedded files. `Install-ChocolateyPackage` explicitly models `silentArgs`,
  `validExitCodes`, `checksum`, and `checksumType`; Chocolatey's community feed
  has validator/verifier moderation services that check package quality and
  installability:
  https://docs.chocolatey.org/en-us/create/functions/install-chocolateypackage/,
  https://docs.chocolatey.org/en-us/create/create-packages/,
  https://docs.chocolatey.org/en-us/community-repository/moderation/package-validator/,
  https://docs.chocolatey.org/en-us/community-repository/moderation/package-verifier/.
- Velopack packages a compiled app with `vpk pack`, requires identity inputs
  such as `--packId`, `--packVersion`, `--packDir`, and `--mainExe`, and update
  discovery uses release feeds like `releases.{channel}.json`. On Windows the
  default install root is `%LocalAppData%\{packId}`, the `current` directory is
  replaced during updates, and Velopack recommends code signing because unsigned
  apps may be flagged:
  https://docs.velopack.io/packaging/overview,
  https://docs.velopack.io/packaging/operating-systems/windows,
  https://docs.velopack.io/packaging/installer,
  https://docs.velopack.io/integrating/update-sources.

### New / Refined Backlog Items

(Moved to `Roadmap_Blocked.md`: Split package-manager targets by artifact role;
Add package-channel validation to release preflight, both blocked on package
identity and signing decisions.)

## Audit-Driven Additions (June 30, 2026)

Items below were surfaced by the June 30, 2026 deep engineering audit
and not resolved during that pass.

## Audit-Driven Additions (July 12, 2026)

Items below were surfaced by the July 12, 2026 deep engineering + UX audit.
Fixed items from that pass are in `CHANGELOG.md` under `[Unreleased]`; the
items below were deferred because they need runtime verification the audit
could not do headlessly, carry regression risk, or are systemic changes
larger than a single fix.

## Research-Driven Additions

Items below were added by the June 9, 2026 research pass. They cover
ecosystem changes, legal landscape shifts, and catalog freshness gaps
not addressed by earlier cycles.

(Moved to `Roadmap_Blocked.md`: Decide the v4 theming base before the
.NET 10 migration, blocked on operator architecture/design decision.)

## Research-Driven Additions (June 19, 2026)

Items below were added by the June 19, 2026 research pass. They address
the schema-runtime disconnect, upstream version gaps, Marketplace
reliability, localization follow-through, legacy GUI contrast, and
dependency freshness surfaced during exhaustive ecosystem research.


## Research-Driven Additions (June 27, 2026)

Items below were added by the June 27, 2026 research pass. They address
AV trust barriers, Smart App Control compatibility, Spotify enforcement
risk, accessibility compliance, testing quality, and PowerShell runtime
compatibility surfaced during exhaustive competitive and ecosystem research.


## Research-Driven Additions (June 28, 2026)

Items below were added by the June 28, 2026 research pass. They address
upstream version fragility, legal risk documentation, Spicetify v3
migration readiness, AppCatalog localization gap, and runtime upstream
health monitoring surfaced during exhaustive competitive and community
sentiment research.

Note on existing items: the P1 shared-core extraction (Cycle 11) is
reinforced by community evidence that SpotX+Spicetify ordering fragility
(SpotX Discussion #402) and Spotify 1.2.86 CSS breakage (Spicetify
X/Twitter Mar 2026) both amplify the cost of drifted functions. The
Cycle 20 diagnostics item is reinforced by user reports of "SpotX
stopped working" (issue #849) as the #1 complaint, the WPF dashboard
needs to detect and surface this state, not just show booleans.

## Research-Driven Additions

### P1

## Audit-Driven Additions (July 7, 2026)

Items surfaced by the July 7, 2026 deep audit and not resolved during that
pass.

## Research-Driven Additions (2026-07-08)

Items from the 2026-07-08 exhaustive research pass. Full evidence in
`RESEARCH.md`. IDs use the RD- scheme (no prior active scheme in this file).
Not duplicated: supply-chain payload pinning (done), MS Store removal (done),
signing/Velopack/winget (blocked, see `Roadmap_Blocked.md`).

### P1

### P3

## Research-Driven Additions (2026-07-09)

Items from the 2026-07-09 exhaustive research pass. Full evidence is in
`RESEARCH.md`. Existing signing, package identity, Velopack, winget,
Windows lifecycle, Mica, native launcher, and stock-restore decisions remain
in `Roadmap_Blocked.md`; the rows below are implementer-actionable.

### P1

### P2

## Audit Backlog (July 9, 2026)

Items surfaced by the July 9, 2026 deep audit pass but not fixed in-session.

## Research-Driven Additions

### P1

### P2

## Research-Driven Additions

### P1

### P2

## Research-Driven Additions (2026-07-22)

Items from the 2026-07-22 exhaustive research pass. Full evidence in
`RESEARCH.md`. IDs continue the `RD-` scheme (highest prior: RD-31). Not
duplicated: Intune/PDQ/WinRM deployment samples (done, `samples/deployment/`),
Defender `-defender_exclusions_off` gate (done), foreign-patcher detection
(done), package-manager manifests (blocked on package identity,
`Roadmap_Blocked.md`), native PS2EXE launcher (blocked), Stryker.NET (blocked,
now unblockable by RD-35).

### P3

- [ ] P3, RD-36: Decompose `MainWindow.xaml` into per-screen UserControls
  Why: `MainWindow.xaml` is 5,509 lines holding all six nav screens (Home/Setup/Unblock/Tools/Settings/About) + the inspector in one file, slowing edits and raising merge/regression risk on the shipping shell.
  Evidence: `src/LibreSpot.Desktop/MainWindow.xaml`.
  Touches: `MainWindow.xaml`, new `Views/*.xaml` UserControls, `Themes/Controls.xaml`, FlaUI smoke + rendered-QA tests (AutomationIds/x:Names must be preserved).
  Acceptance: each nav screen becomes a UserControl under `Views/`; `MainWindow` composes them; every `AutomationId`/`x:Name` referenced by tests is preserved byte-for-byte; the rendered-WPF QA capture and FlaUI suite pass unchanged across dark/high-contrast and English/Spanish.
  Complexity: L

- [ ] P3, RD-37: Add German and French WPF locales
  Why: the localization framework, runtime language selector, and strict validation gate (`tools/Sync-Localization.ps1`) already support five locales (en/es/pt-BR/ru/zh-Hans); de/fr are large Spotify-modding audiences and low-risk given the gate.
  Evidence: `src/LibreSpot.Desktop/Properties/Strings.*.resx` (five locales, no de/fr); `tools/Sync-Localization.ps1`.
  Touches: new `Strings.de.resx` / `Strings.fr.resx`, language-selector list, localization validation allowlist, health-component/scrollbar automation-name coverage.
  Acceptance: de and fr resource sets pass `Sync-Localization` (placeholder parity, no English carry-over, protected product/file tokens, no truncation); the language selector lists both; hidden long-text prompt rendering covers them.
  Complexity: L


## Audit-Driven Additions (2026-07-23)

Surfaced by the 2026-07-23 Marketplace deep-dive. The theme contract, missing
store button, and health reporting were fixed in-session (see CHANGELOG); the
items below need runtime state this environment cannot fully produce.


## Research-Driven Additions (2026-07-24)

Items from the 2026-07-24 exhaustive research pass. Full evidence in
`RESEARCH.md`. IDs continue the `RD-` scheme (highest prior: RD-40). Not
duplicated: v3 detection guard (done, preview.18), `TargetLatestRuntimePatch` +
`dotnetRuntimeFloor` gate at 10.0.10 (done, preview.18), Marketplace route
re-wiring (done, preview.19), package-manager manifests / native launcher /
Stryker.NET (blocked, `Roadmap_Blocked.md`).
```

</details>
