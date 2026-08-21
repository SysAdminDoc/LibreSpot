# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P1: RD-126: Describe recovery actions by what they actually restore
  Why: The current **Restore vanilla Spotify** title leaves SpotX patches in place, and **Everything is reversible** overstates a stock restore path that is still blocked.
  Evidence: `src/LibreSpot.Desktop/Properties/Strings.resx:81`, `:388-390`, `:827`; `Roadmap_Blocked.md:866-876`; `RESEARCH.md` Security, Privacy, and Reliability.
  Touches: `src/LibreSpot.Desktop/Properties/Strings*.resx`, generated resource accessors, localization and maintenance-action tests, `assets/screenshots`, `README.md`.
  Acceptance: Every locale calls action ID `RestoreVanilla` **Remove Spicetify customizations** or an equivalent accurate translation; its body and prompt explicitly say SpotX remains; no visible string promises that every change is reversible; the rail uses a factual backup or recovery statement; resource-key parity, maintenance-action tests, and refreshed screenshots pass; the backend action ID and behavior do not change.
  Complexity: S

- [ ] P1: RD-127: Make Home choose the next safe action from current health state
  Why: Home changes its message for loading and critical states, but its only primary button always runs Recommended Setup, including after the managed stack is already healthy.
  Evidence: `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:363-385`; `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml:364-387`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs:23-80`; BetterDiscord Installer and UniGetUI in `RESEARCH.md` Competitive Landscape.
  Touches: `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`, `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`, `src/LibreSpot.Desktop/Services/SpotifyProcessService.cs`, localized resources, desktop tests, screenshots, `README.md`.
  Acceptance: A single state-derived model owns Home title, body, primary label, command, enabled state, automation name, and help text. Loading disables the action; snapshot failure offers Retry; an unmanaged valid machine offers Recommended Setup; a snapshot with Spotify present, SpotX verified, Spicetify installed, and no actionable critical or warning issue offers Open Spotify without rerunning setup. Otherwise, Home takes the first non-destructive action in existing critical-then-warning issue order; a state with only destructive recovery available navigates to Maintenance without executing it. Table-driven tests cover every state and command, rapid snapshot refresh cannot leave stale text or command bindings, UI Automation properties match the visible action, and the Home capture is refreshed.
  Complexity: M

- [ ] P1: RD-128: Put Maintenance status and safe repair before diagnostics
  Why: A degraded machine currently shows environment cards and the compatibility matrix before the user can reach any repair action, even though typed issue-specific actions already exist.
  Evidence: `assets/screenshots/wpf-maintenance.png`; `src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml:50-180`, `:423-450`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:657-660`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs:23-80`; Microsoft Repair-before-Reset and progressive-disclosure sources in `RESEARCH.md`.
  Touches: `src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`, shared health-issue templates currently in `src/LibreSpot.Desktop/MainWindow.xaml`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`, localized resources, UI and view-model tests, screenshots, `README.md`.
  Acceptance: At 1280 by 800, the first viewport contains overall status, the highest-priority issue in plain language, and one safe action when available. The Marketplace-degraded fixture shows Repair Marketplace without scrolling; a healthy fixture says no action is needed; a snapshot error shows Retry. Environment cards, compatibility details, support-bundle inventory, watcher details, and auto-reapply controls remain available under one labeled diagnostics expander. Reset stays visually separate in a collapsed danger section and never becomes the recommended action while a safe action exists. Focus order, automation names, 24-pixel minimum targets, localization parity, view-model state tests, and the Maintenance screenshot pass.
  Complexity: L

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
