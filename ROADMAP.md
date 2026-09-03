# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P2: RD-130: Recompose Settings as essentials first with one-level disclosure
  Why: Settings renders seven full sections next to a separately scrolling profile rail, making the expert configuration surface the default reading path.
  Evidence: `src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml:20-91`; `src/LibreSpot.Core/AppCatalog.cs:1023-1053`; `assets/screenshots/wpf-custom.png`; Microsoft app-settings guidance dated 2026-04-15 in `RESEARCH.md` Architecture Assessment.
  Touches: `src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml`, `src/LibreSpot.Desktop/Views/Custom*Section.xaml`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.CustomInstall.cs`, disclosure-state persistence, localized resources, UI and view-model tests, screenshots, `README.md`.
  Acceptance: The default single-column view exposes exactly four common choices: Spotify build set to Auto, theme, Marketplace, and Open Spotify when finished. Installation details, playback and interface patches, advanced SpotX flags, extensions, apps, and profile tools remain reachable in clearly named one-level expanders with no nested expanders. Changing any option still round-trips through the existing `InstallConfiguration` and profile format. Searching for a hidden option expands and scrolls its group into view; clearing search restores the user's disclosure state. At 1280 by 800 there is one page scrollbar, no independent profile scrollbar, no clipped apply action, and all controls preserve automation names, focus visibility, localization parity, and existing import/export behavior. Update Settings screenshots and regression tests.
  Complexity: L

- [ ] P3: Tests read `SIGNPATH.md`, but `.gitignore:33` ignores it
  Why: `ReleaseArtifactContractTests.ReleaseTrustDocs_DescribeLocalReleaseEvidenceOnly` and `ReleaseTruthTests.SupportAndSigningDocsMatchTheStableReleaseLine` read `SIGNPATH.md` from the repo root, and `SECURITY.md` treats it as the signing decision record, yet the file is gitignored and absent from a fresh clone, so those tests fail anywhere but this machine.
  Acceptance: WHEN the repository is cloned fresh, the release trust tests SHALL find the signing decision record. Either track `SIGNPATH.md` (remove the ignore entry and commit it) or move the decision record into `SECURITY.md` and drop the file reads. Whichever is chosen, the README and SECURITY references point at a tracked file.
  Complexity: S
