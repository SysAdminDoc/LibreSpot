# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P1: RD-132: Make the stable desktop download the default common-user installation path
  Why: The stable v4 desktop executable is built for common users, but Quick Start first asks them to paste a long PowerShell block and retains a lower-trust `irm | iex` command. This conditions users to follow the same interaction pattern used by ClickFix campaigns and makes the expert delivery lane look like the product default.
  Evidence: `README.md:16-53`; the v4.0.0 immutable release and Microsoft ClickFix, CISA Interlock, Windows PowerShell CVE-2025-54100, BetterDiscord Installer, BlockTheSpot Installer, Ninite, and r2modman sources in `RESEARCH.md`.
  Touches: `README.md`, release-truth and documentation tests, common-user download and verification copy, advanced PowerShell and managed CLI sections.
  Acceptance: The first Quick Start path links directly to `LibreSpot-Desktop.exe` and `checksums.txt` from the official latest stable release, explains same-release SHA256 and release-attestation verification in concise language, and does not ask the common user to paste a command. PowerShell source, the PS2EXE compatibility artifact, and the CLI remain documented under clearly labeled advanced or managed paths. Remove `irm | iex` from common-user documentation. A regression test confirms the desktop path appears first, no paste instruction occurs before advanced documentation, all links target the official repository, and release asset names match the release contract.
  Complexity: S

- [ ] P2: RD-129: Close Spotify normally before forcing remaining processes to exit
  Why: Both active process-control paths kill Spotify immediately, which skips the normal Windows application shutdown path.
  Evidence: `src/LibreSpot.Desktop/Services/SpotifyProcessService.cs:44-56`; `src/powershell/shared/Stop-SpotifyProcesses.ps1:1-10`; Microsoft Restart Manager guidance in `RESEARCH.md` Security, Privacy, and Reliability.
  Touches: `src/LibreSpot.Desktop/Services/SpotifyProcessService.cs`, a testable process adapter under `src/LibreSpot.Desktop/Services`, `src/powershell/shared/Stop-SpotifyProcesses.ps1`, desktop and Pester tests, local operation logs.
  Acceptance: The desktop and PowerShell paths request normal close from Spotify processes that own windows, wait for a documented bounded interval, then force only surviving Spotify processes and helpers. The fallback is logged with process name, ID, elapsed wait, and reason without user data. Cancellation and already-exited races finish cleanly; total wait is bounded; table-driven fake-process tests prove close-before-kill ordering, survivor-only force, and parity between both paths. One installed-client smoke test confirms LibreSpot can still patch and relaunch Spotify before release.
  Complexity: M

- [ ] P2: RD-130: Recompose Settings as essentials first with one-level disclosure
  Why: Settings renders seven full sections next to a separately scrolling profile rail, making the expert configuration surface the default reading path.
  Evidence: `src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml:20-91`; `src/LibreSpot.Core/AppCatalog.cs:1023-1053`; `assets/screenshots/wpf-custom.png`; Microsoft app-settings guidance dated 2026-04-15 in `RESEARCH.md` Architecture Assessment.
  Touches: `src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml`, `src/LibreSpot.Desktop/Views/Custom*Section.xaml`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.CustomInstall.cs`, disclosure-state persistence, localized resources, UI and view-model tests, screenshots, `README.md`.
  Acceptance: The default single-column view exposes exactly four common choices: Spotify build set to Auto, theme, Marketplace, and Open Spotify when finished. Installation details, playback and interface patches, advanced SpotX flags, extensions, apps, and profile tools remain reachable in clearly named one-level expanders with no nested expanders. Changing any option still round-trips through the existing `InstallConfiguration` and profile format. Searching for a hidden option expands and scrolls its group into view; clearing search restores the user's disclosure state. At 1280 by 800 there is one page scrollbar, no independent profile scrollbar, no clipped apply action, and all controls preserve automation names, focus visibility, localization parity, and existing import/export behavior. Update Settings screenshots and regression tests.
  Complexity: L

- [ ] P2: RD-133: Show a quiet LibreSpot version notice on Home
  Why: The legacy PowerShell shell can report that LibreSpot itself is outdated, but the stable WPF Home has no equivalent. Users facing a Spotify or Spicetify break can spend time repairing with an old LibreSpot build even when a newer stable release already contains the fix.
  Evidence: `LibreSpot.ps1:6431`; `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:430`; `src/powershell/shared/Check-ForUpdates.ps1`; GitHub Releases API and conditional-request guidance, UniGetUI, r2modman, ReVanced Manager, and community update-recovery reports in `RESEARCH.md`.
  Touches: a cached release-notice service in `src/LibreSpot.Core`, existing GitHub API and cache helpers, `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`, `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`, all localized resources, Core and desktop tests, Home screenshot, `README.md`.
  Acceptance: Home checks the latest stable GitHub release asynchronously without delaying snapshot load, uses conditional requests and a 24-hour local cache, and shows an inline Update LibreSpot link only when a newer stable semantic version exists. It never selects a prerelease, auto-downloads, executes, raises a toast, or takes focus. Offline, malformed, missing, and rate-limited responses use a valid cache or remain silent. Tests cover semantic-version ordering, current and newer versions, prerelease exclusion, cache expiry, conditional responses, rate limits, cancellation, and all localized states. Coordinate the notice with RD-127 so it cannot replace the primary health action.
  Complexity: M

- [ ] P2: RD-134: Align support and planning documents with the stable v4 release
  Why: v4.0.0 is the stable release, but the security policy still marks v4.0.x-preview as best-effort and the blocked plan retains pending-signing, v3.7.2 stable-channel, preview-release, workflow, and pre-.NET 10 assumptions. These contradictions can mislead users and send future work toward decisions that have already been made.
  Evidence: `SECURITY.md:6-9`, `:132`; `SIGNPATH.md:1-5`, `:74-76`; `Roadmap_Blocked.md:57-88`, `:230-262`, `:425-449`, `:604-607`, `:773-790`; `README.md:12`, `:439-464`; v4.0.0 stable release evidence in `RESEARCH.md`.
  Touches: `SECURITY.md`, `SIGNPATH.md`, `Roadmap_Blocked.md`, `README.md`, release-truth validation and documentation tests.
  Acceptance: The support table names v4.0.x desktop and CLI as the supported stable line and states the intended status of v3.7.x. The blocked plan removes or clearly archives entries whose blockers were resolved or whose assumptions were superseded by v4.0.0, local-only releases, immutable assets, unsigned-by-design, and .NET 10. Remaining blocked entries retain real external dependencies and current exit criteria. README, SECURITY, SIGNPATH, and the blocked plan tell one consistent stable-channel and signing story. A release-truth check fails if current stable version metadata coexists with preview-only support wording or a pending SignPath claim.
  Complexity: S

### Customization and theming pass (2026-09-01)

- [ ] P1: RD-135: Add a curated experimental-feature picker to Custom Install
  Why: LibreSpot exposes about a dozen SpotX switches, but SpotX's own EnableExp/DisableExp catalog and the live client carry roughly 330 client-side enable* flags with Spotify's own descriptions. The most-requested features (Equalizer, Lyrics UI, Sleep Timer, PiP mini-player, Fullscreen mode, right-sidebar variants, tracklist sorting, ambient mode) flip locally and are simply not surfaced. This is the highest-value customization LibreSpot can add and it reuses the existing custom-patch pipeline.
  Evidence: `src/powershell/shared/Build-SpotXParams.ps1`; `src/powershell/shared/New-SpotXCustomPatchesFile.ps1`; SpotX patches.json others.EnableExp/DisableExp and the ForcedExp shim; Spicetify jsHelper/expFeatures.js and expFeatureOverride; feature-flag catalog and demand ranking in `RESEARCH.md` Customization deep-dive.
  Touches: a reviewed flag catalog in `src/powershell/data` and `src/LibreSpot.Core/AppCatalog.cs`, `Build-SpotXParams.ps1` or a new flag-to-ForcedExp writer, `src/LibreSpot.Desktop/Views/CustomAdvancedSection.xaml`, `InstallConfiguration`, profile serialization, Core and Pester tests, README.
  Acceptance: Custom Install offers a searchable list of reviewed client-side flags as labeled toggles that round-trip through `InstallConfiguration` and the `.librespot` profile. Each flag names what it does. Server-gated flags (Enhance, quality unlock, Jam) are shown as unavailable and never written. Selected flags are applied through the existing SpotX custom-patch path or a Spicetify override, verified present after apply on an installed client, and a regression test asserts the catalog only contains flags on the reviewed allowlist. No ad, premium-state, or telemetry flags are added by this item.
  Complexity: L

- [ ] P1: RD-136: Ship a LibreSpot-original theme in the reviewed community catalog
  Why: The theme ecosystem has no cross-platform auto light/dark (Spotify's forced dark mode blinds prefers-color-scheme), no performance-tiered glass despite it being the top complaint on every glass theme, and almost no accessibility theme. LibreSpot pins and hash-verifies community themes already, so it can lead with one that answers these directly.
  Evidence: unmet demand in `RESEARCH.md` Customization deep-dive (spicetify/cli issues #1095, #860, #2836; Hazy #141; catppuccin #29); `src/powershell/shared/Module-InstallThemes.ps1` and the CommunityThemeRepos catalog pattern (owner/commit/SHA256); working proof of concept at `C:\repos\LibreSpot-Prism` (Prism v0.1.0: color.ini with Dark/Light/OLED/HighContrast, user.css layered glass/eco/flat/contrast, theme.js time-scheduled light/dark plus colorExtractor accent plus FPS probe plus settings menu).
  Touches: a new public GitHub repo for the theme, the community-theme catalog entry (owner, repo, commit, SHA256, theme folder), `ThemeSchemes` in `src/LibreSpot.Core/AppCatalog.cs`, `ThemesNeedingJS`, catalog-truth and Core tests, README theme table and screenshots. Note this is a Spicetify theme LibreSpot installs for Spotify, distinct from the app's own WPF light-theme decision recorded in `Roadmap_Blocked.md`.
  Acceptance: The theme installs through the existing pinned-and-hashed community path and appears in the gallery with its schemes. Time-scheduled light/dark switches without a Spotify restart, the accent tracks album art with a scheme fallback when colorExtractor fails, the effect tier drops on a slow frame-rate probe, and the high-contrast scheme keeps WCAG AA text contrast. A catalog-truth test covers the new pin; the theme is verified applying against an installed client before release.
  Complexity: L

- [ ] P2: RD-137: Add one-click customization presets
  Why: The ecosystem ships monolithic themes and leaves users to assemble a look from a theme, a scheme, extensions, and flags by hand. LibreSpot's profile format can already carry all of that, so named presets are a small UI over existing state.
  Evidence: `.librespot` profile format and `src/LibreSpot.Desktop/ViewModels/MainViewModel.Profiles.cs`; preset demand and adjacent-platform precedent (Plexamp, Cider) in `RESEARCH.md`.
  Touches: a preset definition set in `src/LibreSpot.Core/AppCatalog.cs`, the profile apply path, `src/LibreSpot.Desktop/Views/CustomInstallSection.xaml` or Recommended, localized resources, tests.
  Acceptance: At least four presets (OLED, Accessibility, Compact, Performance) each set a theme, scheme, a reviewed flag set, and any snippets, applied through the existing profile mechanism with no new backend. Selecting a preset fills the Custom Install controls so the user can still adjust before applying. A test asserts each preset resolves only to catalog-known themes, schemes, and reviewed flags.
  Complexity: M

- [ ] P2: RD-138: Install reviewed CSS snippets alongside themes
  Why: Spicetify Marketplace ships 93 one-line CSS snippets (hide podcasts or play counts, rounded UI, cover-art shapes, window-control fixes) with no install counts and no version resilience. LibreSpot can pin a reviewed set and inject them the way it injects themes, giving small tweaks the same hash-verified, version-aware treatment.
  Evidence: https://github.com/spicetify/marketplace/blob/main/resources/snippets.json; `Module-InstallThemes.ps1` injection model; `RESEARCH.md` Customization deep-dive.
  Touches: a reviewed snippet catalog in `src/powershell/data`, a snippet injector that appends to the user CSS or a managed snippet file, `src/LibreSpot.Desktop/Views/CustomBuiltInExtensionsSection.xaml` or a new section, tests, README.
  Acceptance: A user can enable reviewed snippets that are applied as managed CSS and cleanly removed when disabled, with each snippet's source and last-verified Spotify version recorded. A test asserts snippets come only from the reviewed catalog and that disabling removes them.
  Complexity: M

- [ ] P2: RD-139: Add a post-apply version-resilience self-test
  Why: A Spotify class-hash change (1.2.86 shortened hashed classnames and broke every theme for weeks) currently surfaces to users as a silently blank page. LibreSpot already ports the CLI's route injection in `Repair-SpicetifyCustomAppWiring.ps1`, so it can probe the same anchors after apply and report a named warning instead.
  Evidence: 1.2.86 class-hash break documented in `RESEARCH.md`; `src/powershell/shared/Repair-SpicetifyCustomAppWiring.ps1`; `Test-SpicetifyCustomAppRouteWiring.ps1`.
  Touches: a bundle-probe function in `src/powershell/shared`, Maintenance status wiring in `Update-MaintenanceStatus` and `EnvironmentSnapshotService`, localized resources, Pester and Core tests.
  Acceptance: After an apply, LibreSpot checks that the marketplace route, the theme CSS variables, and a small set of layout anchors are present in the live bundle and reports each miss as a specific, actionable warning with a repair path. Healthy state is silent. A Pester test proves the probe fails on a bundle with a removed anchor and passes on a wired one.
  Complexity: M

- [ ] P3: RD-140: Give the built-in extension and community catalog per-item health metadata
  Why: LibreSpot ships 15 extensions and 5 community themes, but several popular community extensions are half-working on current Spotify (Stats rate-limits, Beautiful Lyrics needs a flaky backend, some are abandoned). Users get no signal about which are healthy. A last-verified field per item makes the curation honest.
  Evidence: extension inventory with maintenance status in `RESEARCH.md`; `src/powershell/shared/Module-InstallExtensions.ps1` and the community catalog.
  Touches: the extension and theme catalog entries (add a last-verified Spotify version and status), the gallery and extension section views, catalog-truth test, README tables.
  Acceptance: Each catalog extension and theme records a last-verified Spotify version and a status, shown in the UI, and the catalog-truth test fails if an item's recorded verification is older than the pinned Spotify target by more than one minor version without an explicit acknowledgement.
  Complexity: M

- [ ] P3: RD-141: Add a reviewed cosmetic custom-patch library to the Custom Patches section
  Why: LibreSpot already accepts arbitrary SpotX patches.json (64 KB, validated), but authoring one requires knowing the schema and a working regex. A small library of reviewed cosmetic patches (compact rows, rounded art, hide specific upsell surfaces) lowers the barrier and demonstrates the safe, SpotX-layer customization path. A validated example exists at `C:\repos\LibreSpot-Prism\prism-spotx-patches.json`.
  Evidence: `src/powershell/shared/New-SpotXCustomPatchesFile.ps1`; `src/LibreSpot.Desktop/Views/CustomPatchesSection.xaml`; the reverse-engineered patches.json schema (version, match, replace, add) in `RESEARCH.md`; the working sample patch in `C:\repos\LibreSpot-Prism`.
  Touches: a reviewed patch-template set in `src/powershell/data` or `src/LibreSpot.Core`, the Custom Patches section view and its validator, tests, README.
  Acceptance: The Custom Patches section offers reviewed cosmetic templates a user can insert and edit, each version-gated and cosmetic-only, validated by the existing JSON and regex-safety checks before apply. A test asserts every shipped template parses, stays under the size limit, and contains no ad, premium, or telemetry patch.
  Complexity: M
