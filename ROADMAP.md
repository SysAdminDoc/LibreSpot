# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

Added 2026-09-05 from RESEARCH.md. IDs continue the RD scheme; RD-200 was the last used.

- [ ] P2: RD-205: Read the classmap bound from the upstream index instead of inferring it from a directory listing
  Why: the reviewable-bound calculation lists the classmaps repository contents over the GitHub API, matches directory names against a regex, takes the highest and rebuilds a three-part version by string arithmetic. Upstream now publishes `index.json`, a 2.9 KB file giving each key its exact `spotifyVersion` (1020097 is 1.2.97.270) and a `status` field, with a sha256 for every referenced file. A directory listing cannot tell a verified classmap from an inherited one, which is the exact distinction RD-189 exists to reason about, and it burns unauthenticated GitHub API quota that the raw URL does not.
  Evidence: `Build-Scripts.ps1:2854` (the classmaps API URL pointing at `/contents`) and `:2877-2884` (the name regex and version arithmetic); https://raw.githubusercontent.com/spicetify/classmaps/main/index.json read on 2026-09-05 showing 1020084 through 1020097 with `spotifyVersion` and `status: verified`; classmaps commit `5259db3630` (2026-09-03) publishing the exposure patch set through the index.
  Touches: `Build-Scripts.ps1` (`Get-LibreSpotUpstreamTargets`, `Get-LibreSpotReviewableSpotifyBound`), `src/LibreSpot.Core/UpstreamDriftService.cs`, `schemas/spicetify-supported-versions-v2.json`, the drift tests.
  Acceptance: the bound SHALL be computed from index entries whose `status` is `verified`, SHALL carry the full four-part `spotifyVersion` rather than a reconstructed three-part string, SHALL treat a non-verified or status-less key as not counting toward the bound, and SHALL report a clear failure when the index cannot be fetched instead of silently falling back to the highest directory name; a test SHALL pin a captured copy of the index and assert the bound resolves to 1.2.97.270.
  Complexity: M

- [ ] P2: RD-206: Decide what the UIA smoke surface is doing in the Release build and record the answer
  Why: there is no `#if DEBUG` or `Conditional("DEBUG")` anywhere under `src/LibreSpot.Desktop`, so 43 fabricated UI states, `--uia-smoke=`, `--uia-background`, `--uia-size`, `--uia-capture` and the `LIBRESPOT_UIA_ROOT` data-root override all ship in the release executable. Anyone can start the genuine binary with `--uia-smoke=home-healthy` or `--uia-smoke=maintenance-danger` and get a fabricated readiness report from it. No privilege boundary is crossed and the root override keeps writes off the real config, so this is a presentation and contract question rather than a compromise, but `schemas/release-artifact-contract.json` records nothing about it while recording everything else about the artifact.
  Evidence: `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:2598` (the 43 states); `src/LibreSpot.Desktop/MainWindow.xaml.cs:26,32` (argument prefixes) and `:1139` (`LIBRESPOT_UIA_ROOT`); `schemas/publish-footprint-budget.json:56` (the flags in release measurement); a grep for `#if DEBUG` under `src/LibreSpot.Desktop` returns nothing.
  Touches: `src/LibreSpot.Desktop/MainWindow.xaml.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`, `schemas/release-artifact-contract.json`, `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs`, `SECURITY.md`.
  Acceptance: either the smoke surface SHALL be excluded from the Release configuration and a test SHALL fail when a Release build still accepts `--uia-smoke`, or `release-artifact-contract.json` SHALL record it as an intentional shipped surface with its stated limits and a test SHALL assert that no smoke state can write outside `LIBRESPOT_UIA_ROOT`; whichever path is taken, the footprint measurement recipe SHALL keep working.
  Complexity: M

- [ ] P2: RD-207: Bring the in-Spotify surface into the localization story, or state that it is English only
  Why: `.crowdin.yml` scopes exactly one file, the WPF `Strings.resx`, which carries 1358 strings across five locales with a validation gate and eighteen tests behind it. The in-Spotify custom app holds roughly 238 hardcoded English UI strings with no externalization and no Crowdin entry, and the standalone script has no localization machinery at all. A Russian or Chinese user gets a fully localized shell and an English in-client panel, and nothing in the README says so.
  Evidence: `.crowdin.yml` (single source entry); `src/LibreSpot.Desktop/Properties/Strings.resx` and its four satellites at 1358 entries each; a scan of quoted title-case literals across `src/LibreSpot.App/src/` returns about 238 matches with no lookup layer; `LibreSpot.ps1` has no reference to `CurrentUICulture`, `Get-Culture` or `Import-LocalizedData`; gate at `Build-Scripts.ps1:3313`.
  Touches: `src/LibreSpot.App/src/surface/labels.ts` and the panel modules, `.crowdin.yml`, `tools/Sync-Localization.ps1`, `src/LibreSpot.App/tests/`, `README.md`.
  Acceptance: either the in-Spotify strings SHALL be externalized behind a lookup keyed the same way the resx is, with the file added to `.crowdin.yml` and a test failing on a hardcoded user-facing literal in `src/LibreSpot.App/src/panels/` and `src/surface/`, or the README SHALL state that the in-client panel and the standalone script are English only while the desktop shell ships five locales, with a test asserting the README says so as long as the app has no lookup layer.
  Complexity: L

- [ ] P2: RD-209: Gate the two README numbers RD-190 left ungated
  Why: RD-190 gated the extension count and the lyrics theme count and left two behind. The README states the Stryker baseline as 24.32 percent over 1,476 tested mutants and calls it current, but that run was 2026-08-20, twenty commits have since touched the four files the Stryker config mutates, `StrykerOutput/` is gitignored so nothing in the tree can confirm it, and no test references the figure. The 24 supported themes claim is correct today and equally ungated. Both are the exact drift RD-190 was raised to stop.
  Evidence: `README.md:791-792` (the Stryker sentence); `Roadmap_Blocked.md:1005-1008` (the 2026-08-20 run); `src/LibreSpot.Core/stryker-config.json` (the four mutated files); `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs:188` and `:234` (the two gates that exist); `README.md:121,131,304` against `schemas/theme-preview-manifest.json`.
  Touches: `README.md`, `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs`, `schemas/theme-preview-manifest.json`.
  Acceptance: the README SHALL carry the date the Stryker baseline was measured rather than calling it current, and a test SHALL fail when the recorded date is older than the newest commit touching any file named in `stryker-config.json`; a second test SHALL assert the README theme count equals the `theme-preview-manifest.json` entry count minus the Marketplace-only placeholder, and SHALL fail when a theme is added without the README changing.
  Complexity: S

- [ ] P3: RD-211: Collapse the duplicate root icon to one tracked file
  Why: `LibreSpot.ico` and `icon.ico` are byte-identical and both tracked, and they are consumed by different build paths, so a future icon change can update one and ship two artifacts with different icons. The standalone script already probes both as a fallback pair, which is the workaround rather than the fix, and it costs 68 KB of duplicate history on every change.
  Evidence: both files hash to `2939774dfcc00dce91fa551588bdbc76d0833688a2f467120545c441e2497c30`; `Build-Scripts.ps1:1952` uses `LibreSpot.ico`; `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:15,27` uses `icon.ico`; `LibreSpot.ps1:3782-3783` probes both.
  Touches: `Build-Scripts.ps1`, `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`, `LibreSpot.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`, `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs`.
  Acceptance: exactly one `.ico` SHALL be tracked at the repository root, every build path SHALL reference it, the WPF executable and the PS2EXE executable SHALL both carry that icon, and a test SHALL fail if a second `.ico` is added at the root.
  Complexity: S
