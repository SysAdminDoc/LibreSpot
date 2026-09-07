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

- [ ] P2: RD-259: Reconcile the extension count between the README, the Store header and the gate
  Why: the Store counts 16 extensions because it includes the first-party companion `librespot-engine.js`, the README's gated sentence says 15 (ten built in and five community), and the README places the Store screenshot showing 16 three lines from that prose; the gate pins 15 against a source that excludes the companion, so it endorses the mismatch.
  Evidence: `schemas/librespot-customization.json` extensions (16, including `librespot-engine.js`); `README.md:326`; `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs:206`; `assets/screenshots/spotify-librespot-store.png` header pills.
  Touches: `README.md`, `src/LibreSpot.App/src/panels/store.ts`, `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs`, `src/LibreSpot.App/tests/surface.test.ts`.
  Acceptance: one counting rule SHALL be chosen and written down in the catalog schema comment (companion counted or not); the README sentence, the Store header pill and the gate SHALL all derive from that rule, and a surface test SHALL fail when the Store header count differs from the catalog count under the rule.
  Complexity: S

- [ ] P2: RD-260: Add a catalog refresh proposal tool that stages candidate pins for review without applying them
  Why: the theme pin shipped Blackout for seven weeks after upstream removed it, the pin is five commits behind, and the only re-pin tooling is the SpotX policy review; spicetify-nix re-pins its whole set on a weekly automated commit, and LibreSpot's drift services detect but never propose.
  Evidence: spicetify/spicetify-themes #1283 (2026-07-14) against pin `df033493` (2026-05-31); `src/LibreSpot.Core/CommunityAssetDriftService.cs`; `Build-Scripts.ps1:3194` (`Test-SpotifyVersionDrift`); Gerg-L/spicetify-nix commit history; `schemas/community-assets.json` provenance fields.
  Touches: `Build-Scripts.ps1` (new `-ProposeCatalogRefresh` in the network lane beside `-CatalogTruth`), `tools/`, `schemas/community-assets.json`, `tests/LibreSpot.Desktop.Tests/CommunityAssetsManifestTests.cs`.
  Acceptance: WHEN run with network, the tool SHALL list, for every pinned extension, theme and custom app, the pinned commit, the upstream head, the commits between them with subjects, whether the asset still exists at head, and the SHA256 of the head asset, then run the existing archived, stale and evidence policies against the candidates and write a review file under `work/`; it SHALL change no pin; WHEN run offline it SHALL exit non-zero with the reason; a test SHALL feed a fixture where an asset was deleted upstream and require the tool to flag it.
  Complexity: M

- [ ] P2: RD-262: Open the README with the community's own failure list, each mapped to the feature that prevents it
  Why: the most-reported Spicetify and SpotX failures of 2025 to 2026 are all shipped LibreSpot features, and the maintainers of both upstreams now tell users not to combine them; the README explains the mechanism at line 316 but never states this on its first screen, and the project has 12 stars, 30 views in two weeks and 3 downloads of the current release.
  Evidence: RESEARCH.md Reported Issues ranking with thread URLs; SpotX #892; cli #3922; Marketplace #111, #273, #12, #438; `README.md:1-30` (no positioning section); GitHub traffic read 2026-09-07.
  Touches: `README.md`, `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs`.
  Acceptance: a section above Quick Start SHALL list at least five failure modes, each with one public thread URL from the research and the LibreSpot feature and README anchor that addresses it, quoting issue text rather than characterizing the maintainers; a test SHALL fail when any listed README anchor does not resolve to a heading.
  Complexity: M

### P3: Later

- [ ] P3: RD-263: Cite the Smart App Control update that shipped, not the one that was pulled
  Why: the README names KB5079391 for the Smart App Control toggle, and Microsoft withdrew that preview update for install failure 0x80073712 and replaced it with out-of-band KB5086672 on 2026-03-31; a reader searching for the cited update finds a removed one.
  Evidence: `README.md:592`; KB5079391 (2026-03-26, builds 26200.8116 and 26100.8116) superseded by KB5086672 (2026-03-31, builds 26200.8117 and 26100.8117); Microsoft's consumer Smart App Control article now states no clean install is required.
  Touches: `README.md`.
  Acceptance: the FAQ SHALL name KB5086672 as the shipping update and the two builds, and SHALL keep the statement that a clean install is no longer needed; the sentence SHALL carry the 2026-03-31 date.
  Complexity: S

- [ ] P3: RD-264: State that Blackout is retained from the pinned commit after upstream removed it
  Why: the README lists Blackout among the sixteen official themes, upstream deleted it on 2026-07-14, and the pin advance that would drop it is a blocked decision (RD-208); until that decision the README should say the theme is retained deliberately rather than imply it is current upstream.
  Evidence: `README.md:464`; spicetify/spicetify-themes #1283 and commit `33ab071b`; `Roadmap_Blocked.md` RD-208; `schemas/theme-preview-manifest.json:199-208` (Blackout at the pinned commit).
  Touches: `README.md`, `schemas/theme-preview-manifest.json`.
  Acceptance: the theme list SHALL mark Blackout as retained from the pinned `df033493` snapshot after its upstream removal on 2026-07-14, and the preview manifest entry SHALL carry a `retainedAfterUpstreamRemoval` date that a test requires to match the README note.
  Complexity: S

- [ ] P3: RD-265: Show both licenses on the badge line
  Why: the License badge says MIT while the desktop executable embeds the AGPL-3.0-only in-Spotify engine; the prose explains the split three times, but the badge is what a reader sees first and what other tools scrape.
  Evidence: `README.md:10` (badge) against `:246`, `:581`, `:872`; `src/LibreSpot.App/package.json` (`AGPL-3.0-only`); `src/LibreSpot.App/LICENSE`.
  Touches: `README.md`, `tests/LibreSpot.Desktop.Tests/DocumentationContractTests.cs`.
  Acceptance: the badge line SHALL show MIT for the hosts and AGPL-3.0-only for the in-client engine, each linking to its LICENSE file, and the documentation contract test SHALL require both badges while `LICENSE` and `src/LibreSpot.App/LICENSE` differ.
  Complexity: S

- [ ] P3: RD-266: Remove the CODEOWNERS rule for a workflows directory that does not exist
  Why: `.github/CODEOWNERS` assigns `.github/workflows/` while the repository intentionally tracks no workflows; the rule is dead and contradicts the no-CI statement in the README and the footprint budget.
  Evidence: `.github/CODEOWNERS` line 5; `schemas/publish-footprint-budget.json` ("This repository has no build CI"); no `.github/workflows/` in the tree.
  Touches: `.github/CODEOWNERS`.
  Acceptance: the rule SHALL be removed and a test SHALL fail when CODEOWNERS names a path that does not exist in the tree.
  Complexity: S

- [ ] P3: RD-267: Delete the four remaining Dependabot branches on the remote
  Why: the repository policy is no Dependabot, the configuration is gone and no PRs are open, but four `dependabot/*` branches still exist on origin and reappear in every `git branch -r`.
  Evidence: `git branch -r` on 2026-09-07 lists `origin/dependabot/github_actions/github/codeql-action/analyze-...`, `.../init-...`, `.../workflow-actions-major-ac9b5ffc60`, `origin/dependabot/nuget/tests/LibreSpot.Desktop.Tests/test-dependencies-407341980e`; global policy in `CLAUDE.md`.
  Touches: remote branches only.
  Acceptance: `git ls-remote --heads origin 'dependabot/*'` SHALL return nothing, and the eight local `dependabot/*` branches SHALL be pruned.
  Complexity: S

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

- [ ] P3: RD-270: Rewrite the Settings footer line that leaks an implementation term
  Why: the Settings action footer reads "LibreSpot saves this profile to config.json, then applies it through the original backend"; "the original backend" means nothing to a user and names a component the README never mentions.
  Evidence: `src/LibreSpot.Desktop/Properties/Strings.resx:560` (`Vm_CustomApplyReady`); `assets/screenshots/wpf-custom.png` footer.
  Touches: `src/LibreSpot.Desktop/Properties/Strings.resx` and its four satellites, `tools/Sync-Localization.ps1`.
  Acceptance: the string SHALL say what happens in user terms (the profile is saved, then Spotify is patched and Spicetify applied) with no reference to a backend; all five locales SHALL carry the reviewed translation and `-Validate`'s localization check SHALL pass.
  Complexity: S
