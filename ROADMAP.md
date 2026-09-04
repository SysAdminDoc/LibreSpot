# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P3: RD-155: Show the asset digest and the verify command beside the Update LibreSpot link
  Why: the latest-release payload already carries per-asset SHA256 digests, and the README teaches `gh release verify-asset`; the Home notice could show the desktop asset's digest and the one command so a user verifies before running, matching the release-trust story.
  Evidence: https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/; https://cli.github.com/manual/gh_attestation_verify; `src/LibreSpot.Core/ReleaseNoticeService.cs`; `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml` update-notice row.
  Touches: `src/LibreSpot.Core/ReleaseNoticeService.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`, `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`, six resx files, `tests/LibreSpot.Desktop.Tests/MainViewModelReleaseNoticeTests.cs`, QA matrix row `home-update`.
  Acceptance: WHEN a newer stable release is cached, the notice SHALL expose the `LibreSpot-Desktop.exe` digest and a copyable `gh release verify-asset` line through a disclosure, with automation names and all five locales; WHEN the payload has no digest, nothing extra SHALL render; the `home-update` capture SHALL stay within its window.
  Complexity: S
