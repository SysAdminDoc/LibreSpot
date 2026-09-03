# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

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
