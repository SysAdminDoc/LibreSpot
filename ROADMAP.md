# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Remaining after 2026-08-21 evening audit

Highest ID: RD-106. Issue tracker: zero open issues/PRs; closed #1-#5 predate v4 and stay closed. Discussions #20 and #21 are operator announcements, not in-repo bugs.

Shipped this pass (deleted from this file): RD-78 search watermarks, RD-79 disabled rail contrast, RD-82 AtomicFile, RD-85 dead members, RD-87 LibreSpotPaths, RD-88 PowerShell host path, RD-89 DWM palette colors, RD-99 snapshot-error glyph, RD-100 screenshot contract, RD-101 WPF-UI template leak gate, RD-102 CycloneDX local tool, RD-103 changelog HEAD-only bullets, RD-104 Jump List labels. Filled/secondary/checkbox/combo disabled states now use DisabledTextBrush; CardListBoxItemStyle remains under RD-105. Bloom checklist/manifest decision drift (the RD-81 note) is aligned; the gh-pages gate below remains.

### P1

- [ ] P1 — RD-120: `-CatalogTruth` never enumerates the published side
  Why: the comparison loops over locally generated filenames only, so anything present on gh-pages but no longer generated is invisible.
  Where: Build-Scripts.ps1 Test-CommunityCatalogTruth
  Repro: stop the generator emitting `404.html` and the gate reports "matches (3 files)" and exits 0 while gh-pages still serves the stale page. Committing an unrelated `install.html` to gh-pages also passes.
  Fix: enumerate `git ls-tree --name-only $publishedRef` as well and fail on any file that is published but not generated.
  Acceptance: dropping a generated file fails the gate; a file on gh-pages the generator never produced fails the gate.

- [ ] P1 — RD-121: The guard on shell-launching tests keys on the wrong markers
  Why: it looks for `--uia-smoke` and `new MainWindow`. `new MainWindow` appears nowhere outside the guard's own file, and the real launch primitive is `Path.Combine(AppContext.BaseDirectory, "LibreSpot.exe")` plus `Process.Start`.
  Where: tests/LibreSpot.Desktop.Tests/DependencyAutomationTests.cs ShellLaunchingTests_AllSitInClassesTheClassFilterExcludes
  Problem: a new class that starts `LibreSpot.exe` without `--uia-smoke` passes the guard and then opens the production shell during the documented local run. The guard also checks the file name, while `--filter-not-class` matches the type name, and it only scans the top directory.
  Fix: key on the launch primitive, read the declared class names out of each file, and recurse.
  Acceptance: a class that starts LibreSpot.exe from a non-`Wpf*` type name fails the guard.

### P2

- [ ] P2 — RD-122: `WpfUiIntegrationTests` is skipped by name although it opens no window
  Why: 15 cases, no process and no window; `EnsureApplication` builds a WPF `Application` without calling `Run()` and the heavy cases render offscreen to a RenderTargetBitmap.
  Where: tests/LibreSpot.Desktop.Tests/WpfUiIntegrationTests.cs
  Problem: it cannot simply be renamed into the default suite, because a WPF `Application` is process-global and the default suite runs in parallel.
  Fix: give it a non-parallel collection, then rename it off the `Wpf` prefix so the class filter stops skipping it.
  Acceptance: the documented local command runs those 15 cases and the suite stays stable across repeated runs.

- [ ] P2 — RD-123: The typography gate reads 2 of 17 XAML files
  Why: it hardcodes MainWindow.xaml and Themes/Controls.xaml, while AutomationNameContractTests and KeyboardFocusContractTests already enumerate every `Views/*.xaml`.
  Where: tests/LibreSpot.Desktop.Tests/ThemeManagerTests.cs WpfTypography_UsesTheTenStepProductTypeScale, XamlCornerRadii_DoNotExceedDocumentedRadiusMaximum
  Problem: `Views/RecommendedWorkspaceView.xaml` carries nine sizes outside the ten-step scale (15, 19, 15, 78, 46, 17, 25, 19, 15) and the gate has never seen them. The 78 and 46 are deliberate hero display type; the rest look like drift.
  Fix: widen both gates to every XAML file, name the hero display sizes as explicit scale steps, and move the small off-scale sizes onto the nearest step.
  Acceptance: the gate reads every XAML file under src/LibreSpot.Desktop and passes.

### P3

- [ ] P3 — RD-124: `-Validate` cannot see the published catalog on a single-branch clone
  Why: `-CatalogTruth` fetches into `refs/librespot/catalog-truth`, but `-Validate` reads `origin/gh-pages`, which a `--single-branch` clone never creates. Running `-CatalogTruth` first does not prime it.
  Where: Build-Scripts.ps1 Test-CommunityCatalogTruth
  Problem: on that clone shape the `-Validate` catalog check warns and passes regardless of the manifest. A full clone is fine: git's opportunistic tracking update keeps `origin/gh-pages` fresh.
  Fix: have the non-fetching path fall back to `refs/librespot/catalog-truth` when `origin/gh-pages` is missing.
  Acceptance: after one `-CatalogTruth`, `-Validate` on a single-branch clone compares against the published catalog instead of warning.

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

- [ ] P3 — RD-98: Areas this audit did not reach
  Why: Honest coverage so the next pass does not assume these were cleared.
  Where: LibreSpot.ps1 WinForms/WPF-in-PS GUI event flow and Module-* orchestration bodies; OperationJournalUndoService undo token execution beyond the CLI guard; Marketplace export/restore archive internals; AvalonEdit custom-patch editor runtime; Crowdin round-trip
  Fix: Focused pass per area; PS GUI flow and the undo executor have the most user-facing risk.

