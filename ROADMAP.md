# LibreSpot Roadmap

Incomplete, implementer-actionable work only. Operator-dependent decisions remain in Roadmap_Blocked.md.

## Research-Driven Additions

Added 2026-09-07 from RESEARCH.md. IDs continue the RD scheme; RD-252 was the last used.

### P2: Next

- [ ] P2: RD-255: Check WCAG 2.2 Focus Not Obscured in the offscreen scan, starting with the Settings action bar
  Why: Axe.Windows 2.4.2 (2024-11-01) has added no rule since 2023-01-27 and cannot check SC 2.4.11, and the Settings view keeps a sticky "Apply custom profile" bar over scrolling content, so a control that receives keyboard focus below the fold can sit behind the bar; the scan already added its own target-size check for the same reason.
  Evidence: `assets/screenshots/wpf-custom.png` (third expander partly behind the bar); WCAG 2.2 SC 2.4.11 (Level AA, Recommendation 2024-12-12); Axe.Windows rule history at `src/Rules/Library`; RD-193 in `CHANGELOG.md` (the 24 by 24 check).
  Touches: `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`, `src/LibreSpot.Desktop/MainWindow.xaml`, `schemas/keyboard-focus-contract.json`.
  Acceptance: WHEN the offscreen scan tabs through Settings at the default and minimum window sizes, every focused element's bounding rectangle SHALL not be fully covered by the sticky action bar or any other always-on-top surface, with the scroll viewer brought into view as WPF does for a user; the check SHALL be planted red once by pinning a focusable control under the bar; the contract file SHALL name the surfaces that may overlay content.
  Complexity: M

- [ ] P2: RD-257: Recapture the README screenshots after the version bump and gate what the reader sees, not only the metadata
  Why: the three WPF captures committed on 2026-09-07 render "v4.5.0" in the navigation rail while carrying `LibreSpotCaptureAssemblyVersion` 4.5.1, and the gate passes because it reads the PNG text chunk; the in-client captures date from 2026-09-04 and predate the 4.5.1 companion status, error boundaries and control descriptions; the Store hero publishes the maintainer's real library and a face.
  Evidence: `assets/screenshots/wpf-recommended.png`, `wpf-custom.png`, `wpf-maintenance.png` (rail text versus embedded chunk, read 2026-09-07); `Build-Scripts.ps1:1316-1405` (`Test-ReadmeWpfScreenshotMetadata`); `src/LibreSpot.Desktop/MainWindow.xaml.cs:1079` (stamp) and `ViewModels/MainViewModel.cs:546-550` (rail); capture mtime 05:02 against bump commit `cc4fb16` at 05:06; `assets/screenshots/spotify-librespot-*.png` mtimes 2026-09-04.
  Touches: `src/LibreSpot.Desktop/MainWindow.xaml.cs`, `Build-Scripts.ps1`, `tests/LibreSpot.Desktop.Tests/ReadmeScreenshotTests.cs`, `assets/screenshots/`, `README.md`.
  Acceptance: the capture path SHALL stamp `LibreSpotCaptureRailVersion` from the value the rail's `SimpleShellVersionLabel` actually exposes through UI Automation at capture time; the gate SHALL fail when that value differs from the assembly version or from the README's Version badge; all eleven captures SHALL be retaken from the released 4.5.1 build with a synthetic Spotify library (Open Questions in RESEARCH.md), and the seven in-client captures SHALL show the 4.5.1 companion status surface.
  Complexity: S

- [ ] P2: RD-260: Add a catalog refresh proposal tool that stages candidate pins for review without applying them
  Why: the theme pin shipped Blackout for seven weeks after upstream removed it, the pin is five commits behind, and the only re-pin tooling is the SpotX policy review; spicetify-nix re-pins its whole set on a weekly automated commit, and LibreSpot's drift services detect but never propose.
  Evidence: spicetify/spicetify-themes #1283 (2026-07-14) against pin `df033493` (2026-05-31); `src/LibreSpot.Core/CommunityAssetDriftService.cs`; `Build-Scripts.ps1:3194` (`Test-SpotifyVersionDrift`); Gerg-L/spicetify-nix commit history; `schemas/community-assets.json` provenance fields.
  Touches: `Build-Scripts.ps1` (new `-ProposeCatalogRefresh` in the network lane beside `-CatalogTruth`), `tools/`, `schemas/community-assets.json`, `tests/LibreSpot.Desktop.Tests/CommunityAssetsManifestTests.cs`.
  Acceptance: WHEN run with network, the tool SHALL list, for every pinned extension, theme and custom app, the pinned commit, the upstream head, the commits between them with subjects, whether the asset still exists at head, and the SHA256 of the head asset, then run the existing archived, stale and evidence policies against the candidates and write a review file under `work/`; it SHALL change no pin; WHEN run offline it SHALL exit non-zero with the reason; a test SHALL feed a fixture where an asset was deleted upstream and require the tool to flag it.
  Complexity: M

### P3: Later

- [ ] P3: RD-268: Reproduce the light-scheme context-menu defect on the pinned client and mitigate it in Prism if it shows
  Why: Spicetify 2.44.0's `replace_colors` leaves Spotify's alpha whites untouched, giving light schemes white-on-white context menus; the fix merged upstream is past the pin, and Prism's Light and HighContrast schemes are the ones that would show it.
  Evidence: spicetify/cli #3918 (2026-09-05) and PR #3917; `resources/themes/Prism/color.ini` `[Light]` section; `README.md:149` (Recommended uses Dark).
  Touches: `resources/themes/Prism/user.css`, `src/powershell/data/BundledThemes.ps1` and both composed hosts (pin), `tests/LibreSpot.Desktop.Tests/BundledThemeTests.cs`.
  Acceptance: WHEN Prism Light is applied on the pinned 1.2.93 client, the track context menu SHALL render text with at least 4.5:1 contrast, verified with a captured screenshot; if the defect reproduces, `user.css` SHALL override the affected context-menu surface variables for the light schemes only and the three theme hashes SHALL move together. Needs live validation.
  Complexity: M

- [ ] P3: RD-269: Verify the Settings theme gallery shows whole cards at the README capture viewport
  Why: the 2026-09-07 Settings capture still shows the Prism card clipped at the gallery's bottom edge with an inner scrollbar, after the fix that was meant to let the gallery grow with the page.
  Evidence: `assets/screenshots/wpf-custom.png`; commit `ca619e7` (RD-186, 2026-09-04); `CHANGELOG.md` entry "The theme gallery in Settings no longer scrolls inside the page".
  Touches: `src/LibreSpot.Desktop/MainWindow.xaml`, `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`.
  Acceptance: WHEN the custom smoke state renders at 1440 by 1024 logical pixels, every theme card's bounding rectangle SHALL lie fully inside the gallery's rectangle and no inner scroll viewer SHALL be scrollable; the UIA smoke SHALL assert both. Needs live validation.
  Complexity: S
