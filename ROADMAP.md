# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Remaining after 2026-08-21 evening audit

Highest ID: RD-106. Issue tracker: zero open issues/PRs; closed #1-#5 predate v4 and stay closed. Discussions #20 and #21 are operator announcements, not in-repo bugs.

Shipped this pass (deleted from this file): RD-78 search watermarks, RD-79 disabled rail contrast, RD-82 AtomicFile, RD-85 dead members, RD-87 LibreSpotPaths, RD-88 PowerShell host path, RD-89 DWM palette colors, RD-99 snapshot-error glyph, RD-100 screenshot contract, RD-101 WPF-UI template leak gate, RD-102 CycloneDX local tool, RD-103 changelog HEAD-only bullets, RD-104 Jump List labels. Filled/secondary/checkbox/combo disabled states now use DisabledTextBrush; CardListBoxItemStyle remains under RD-105. Bloom checklist/manifest decision drift (the RD-81 note) is aligned; the gh-pages gate below remains.

### P2

- [ ] P2 — RD-119: Forced high contrast for QA captures gets dark-mode chrome
  Why: `--uia-theme=high-contrast` makes ThemeManager load the high-contrast palette while `SystemParameters.HighContrast` stays false, so `Win11ShellIntegration.ApplyChrome` takes the non-high-contrast branch: it sets the immersive dark-mode hint, requests the Mica backdrop, and assigns the transparent `MicaCanvasBrush` as the window background. The caption colors happen to come out right because they are read from the swapped palette, but the backdrop and background do not.
  Where: src/LibreSpot.Desktop/Services/Win11ShellIntegration.cs; Services/ThemeManager.cs `_forceHighContrast`; App.xaml.cs
  Problem: every forced-high-contrast QA capture shows a window a real high-contrast user never sees, so the captures cannot be trusted for that state.
  Fix: Have the chrome ask ThemeManager whether high contrast is in effect (forced or real) rather than reading `SystemParameters.HighContrast` directly.
  Acceptance: A `--uia-theme=high-contrast` capture has an opaque window background and no Mica backdrop.

### P3

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

