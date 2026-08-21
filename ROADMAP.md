# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Remaining after 2026-08-21 evening audit

Highest ID: RD-106. Issue tracker: zero open issues/PRs; closed #1-#5 predate v4 and stay closed. Discussions #20 and #21 are operator announcements, not in-repo bugs.

Shipped this pass (deleted from this file): RD-78 search watermarks, RD-79 disabled rail contrast, RD-82 AtomicFile, RD-85 dead members, RD-87 LibreSpotPaths, RD-88 PowerShell host path, RD-89 DWM palette colors, RD-99 snapshot-error glyph, RD-100 screenshot contract, RD-101 WPF-UI template leak gate, RD-102 CycloneDX local tool, RD-103 changelog HEAD-only bullets, RD-104 Jump List labels. Filled/secondary/checkbox/combo disabled states now use DisabledTextBrush; CardListBoxItemStyle remains under RD-105. Bloom checklist/manifest decision drift (the RD-81 note) is aligned; the gh-pages gate below remains.

### P1

- [ ] P1 — RD-109: `-CatalogTruth` passes silently on a shallow or single-branch clone
  Why: `git fetch origin gh-pages` exits 0 on a `--depth 1` clone but writes only `FETCH_HEAD`; `refs/remotes/origin/gh-pages` is never created, so the gate takes the warn-and-return path and exits 0.
  Where: Build-Scripts.ps1 Test-CommunityCatalogTruth
  Repro: `git clone --depth 1 file:///C:/repos/LibreSpot t`, tamper `extensions[0].sha256`, run `-CatalogTruth`. Reports "unverified", exits 0, and tells the user to run the command that just ran.
  Fix: In `-CatalogTruth` (the fetching mode), fetch into an explicit local ref (`git fetch origin gh-pages:refs/librespot/gh-pages-truth`) and read that, and fail rather than warn when the fetch itself succeeded. Warn-only stays correct for `-Validate` and for an unreachable remote.
  Acceptance: The gate fails on a tampered manifest in a `--depth 1` clone; a genuinely offline run still warns and passes.

- [ ] P1 — RD-110: `-CatalogTruth` only compares fields that reach catalog.json
  Why: The gate regenerates catalog.json and diffs it, so manifest fields the generator does not emit are invisible.
  Where: Build-Scripts.ps1 Test-CommunityCatalogTruth; tools/Build-CommunityCatalog.ps1
  Repro: change `extensions[0].assetPath` to `TAMPERED-hidePodcasts.js` and the gate passes. Same blind spot for `legacyAliases`, `easyModeDefault`, `policyOverride`, `releaseTag`, `requiresJsInjection`, and the top-level `deprecatedExtensions`, `officialThemesArchive`, `policy`, `manifestVersion`. `assetPath` is the file actually fetched, so it is trust-relevant.
  Fix: Publish a manifest digest alongside catalog.json and compare that, or emit the omitted trust fields into the catalog.
  Acceptance: Editing any trust-relevant manifest field without republishing fails the gate.

### P2

- [ ] P2 — RD-111: `SpotifyVersion` accepts trailing garbage past the component cap
  Why: `TryParseComponents` only validates the first `maximumComponents` pieces, so what is rejected depends on where the junk lands.
  Where: src/LibreSpot.Core/SpotifyVersion.cs
  Repro: `TryParse("1.2.96.518 (Release)")` is true (1.2.96) while `TryParse("1.2.3 (build 4)")` is false; `TryParseReleaseTag("1.2.3.4.garbage", null)` is true (1.2.3.4) where the old `ParseSortableVersion` returned 0.0.0 and sorted it last.
  Fix: Validate every piece, then take the first `maximumComponents`.
  Acceptance: Both entry points agree on the same string; a tag with a non-numeric trailing piece is rejected; the sort-order case is covered by a test.

- [ ] P2 — RD-112: `SpicetifySupportContract` no longer reads a version terminated by whitespace
  Why: The old regex accepted whitespace as a terminator; the shared parser does not, so a vendor `FileVersion` like `1.2.3 rc1` or `1.2.3 (build 4)` now fails to parse and pushes `Evaluate` into the "cannot read version" refusal branch.
  Where: src/LibreSpot.Core/SpotifyVersion.cs; SpicetifySupportContract.TryNormalizeVersion; EnvironmentSnapshotService reads `FileVersionInfo.FileVersion`, which is free-form vendor text.
  Fix: Treat whitespace as a component terminator, matching the regex the parser replaced.
  Acceptance: `1.2.3 rc1` and `1.2.3 (build 4)` parse to 1.2.3; a test covers both.

- [ ] P2 — RD-113: Window chrome and background still need a restart on a high-contrast toggle
  Why: RD-90 fixed the resource-bound surfaces. `Win11ShellIntegration.ApplyMicaAndDarkChrome` runs once from `SourceInitialized` and unsubscribes, and `ThemeManager.ApplyTheme` never re-invokes it, so the DWM caption/text/border colors, the Mica backdrop, and the hard `window.Background = micaBrush` assignment all keep the dark palette.
  Where: src/LibreSpot.Desktop/Services/Win11ShellIntegration.cs; Services/ThemeManager.cs; MainWindow.xaml.cs SourceInitialized
  Fix: Re-run the chrome application (or `ClearCustomChrome`, which exists for this case and never fires) from the ThemeManager change notification, and set the window background by resource reference.
  Acceptance: Toggling high contrast at runtime repaints the caption and window background without a restart.

- [ ] P2 — RD-114: The static-palette lint misses colors, the ResourceKey spelling, and imperative lookups
  Why: The RD-90 lint only matches `{StaticResource *Brush|Shadow|Glow}`.
  Where: tests/LibreSpot.Desktop.Tests/ColorLintTests.cs StaticPaletteReferencePattern
  Problem: `Color="{StaticResource AccentColor}"` on a GradientStop or ColorAnimation passes, as do `{StaticResource ResourceKey=OverlayShadow}` and `<StaticResource ResourceKey="OverlayShadow"/>`. The C# side never looks at `FindResource`/`TryFindResource`, which is why RD-113 is invisible to it. None of these forms exists in the tree today, so the lint is incomplete rather than wrong.
  Fix: Match every key defined in Palette.xaml rather than a name suffix, cover both `StaticResource` spellings, and lint imperative resource lookups that assign a theme brush.
  Acceptance: Each listed form is rejected by a positive control test.

- [ ] P2 — RD-115: `-CatalogTruth` does not compare the published HTML
  Why: Only catalog.json is diffed, but gh-pages also serves index.html, 404.html, and README.md from the same generator.
  Where: Build-Scripts.ps1 Test-CommunityCatalogTruth
  Problem: A change to `New-CatalogHtml` leaves catalog.json byte-identical and passes while the live page is stale. In sync today, so latent.
  Fix: Compare every generated file, not just catalog.json.
  Acceptance: Editing the HTML generator without republishing fails the gate.

- [ ] P2 — RD-116: `SpicetifyVersionSupport.TryGetMajor` is a second version reader on the same string
  Why: RD-86 unified five parsers, but `CompatibilityVerdict.BuildSpicetify` still runs the detected version through both `SpotifyVersion.TryParse` and `TryGetMajor`, which disagree.
  Where: src/LibreSpot.Core/AppCatalog.cs SpicetifyVersionSupport; CompatibilityVerdict.BuildSpicetify
  Repro: for a reported Spicetify version of `3.0`, `TryParse` fails so the verdict is Unknown, while `IsUnsupportedMajor("3.0")` is true.
  Fix: Route `TryGetMajor` through the shared parser, or make it explicit that a major-only reading is a different question and name it so.
  Acceptance: One reading of a given version string drives both the verdict and the unsupported-major check.

### P3

- [ ] P3 — RD-117: `Invoke-GitCommand` drains sequentially and can block on a credential prompt
  Why: It reads stdout to the end, then stderr, then waits, which deadlocks if the child fills the stderr pipe. Stdin is not redirected and `GIT_TERMINAL_PROMPT=0` is not set, so a credential-less remote can prompt instead of failing.
  Where: Build-Scripts.ps1 Invoke-GitCommand
  Fix: Drain both pipes concurrently, redirect stdin, and set `GIT_TERMINAL_PROMPT=0`. Not reproduced; the output volumes involved are small.
  Acceptance: A git call that writes heavily to stderr still returns, and a prompting remote fails instead of hanging.

- [ ] P3 — RD-118: `-Validate` fails hard on a stale gh-pages tracking ref
  Why: It deliberately does not fetch, so a clone whose `origin/gh-pages` predates a publish from another machine reports drift that is not real.
  Where: Build-Scripts.ps1 Test-CommunityCatalogTruth
  Fix: Name `git fetch origin gh-pages` in the failure text as the first thing to try, or compare the ref's age and downgrade to a warning when it is older than the working tree's last catalog change.
  Acceptance: A stale-ref run says so instead of reporting a drift the maintainer cannot reproduce.

- [ ] P3 — RD-91: Terminology and punctuation drift across live UI strings
  Why: "Spotify build" vs "Spotify version", "Premium account mode" vs "patch posture", mixed ellipsis and hyphen-as-dash, mixed quote styles.
  Where: src/LibreSpot.Desktop/Properties/Strings.resx and the four satellites
  Fix: One term per concept; `...` to `…`; no spaced hyphens as dashes; imperative Option descriptions; mirror satellites.
  Acceptance: Localization gate passes; grep for sentence-position ` - ` and ASCII `...` in user-facing values is clean.

- [ ] P3 — RD-92: Dead-end failure strings and crash-dialog jargon
  Why: Several failure strings state a failure without a next step; the crash dialog leads with EXCEPTION SUMMARY.
  Where: Vm_UndoStateChanged, Vm_UnknownBackendFailure, Vm_SupportBundleExportFailedFormat, Vm_ConfigSaveFailed, Vm_ProfileComparisonUnavailable, CrashExceptionSummaryLabel, CrashNoExceptionMessage
  Fix: Name an action available on that surface; retitle the crash section in plain language.
  Acceptance: Each listed key names a next step; all five locales.

- [ ] P3 — RD-93: Half the resx carries boilerplate translator comments
  Why: 625 of 1,278 comments are "MainViewModel localized runtime text." or "User-facing WPF text"; placeholder keys are the ones a translator can break.
  Where: src/LibreSpot.Desktop/Properties/Strings.resx
  Fix: Backfill comments for `{n}` keys first, naming each placeholder.
  Acceptance: Every key whose value contains `{0}` has a comment naming the placeholder.

- [ ] P3 — RD-95: Profiles list viewport bisects a card mid-title
  Why: ListBox MaxHeight 260 over ~140px cards shows ~1.8 cards and cuts the next title, reading as an overlap.
  Where: src/LibreSpot.Desktop/Views/CustomProfileSummarySection.xaml
  Fix: Size the viewport to whole cards, or add a bottom fade so a partial card reads as scrollable.
  Acceptance: Custom-state capture shows whole cards or a clear fade, not a bisected title.

- [ ] P3 — RD-97: WpfQaMatrix capture wait can time out spuriously in full-suite runs
  Why: One home-navigation/dark/en row timed out in a full `*Wpf*` run, then passed in isolation. Do not raise the global timeout blindly.
  Where: tests/LibreSpot.Desktop.Tests/WpfQaMatrixTests.cs WaitForCapture
  Fix: Retry once by relaunching that row before failing.
  Acceptance: Bounded retry is logged; a single capture timeout no longer fails the suite.

- [ ] P3 — RD-98: Areas this audit did not reach
  Why: Honest coverage so the next pass does not assume these were cleared.
  Where: LibreSpot.ps1 WinForms/WPF-in-PS GUI event flow and Module-* orchestration bodies; OperationJournalUndoService undo token execution beyond the CLI guard; Marketplace export/restore archive internals; AvalonEdit custom-patch editor runtime; Crowdin round-trip
  Fix: Focused pass per area; PS GUI flow and the undo executor have the most user-facing risk.

