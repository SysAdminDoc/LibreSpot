# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Audit Findings — 2026-08-21

IDs continue the RD-nn scheme (highest prior ID: RD-72). Baseline at audit time, all from the same session on this machine: PSScriptAnalyzer lint clean, `Build-Scripts.ps1 -Validate` clean, Core tests 31/31, Desktop non-WPF 920/920, full WPF 135/135, Pester 203/203, `-DependencyHealth` ok with 0 vulnerable packages. No pre-existing failures; nothing below is baseline breakage. Issue tracker: zero open issues/PRs; the five closed issues predate v4 and are all resolved.

### P2

- [ ] P2 — RD-76: Home's failure state has no visible retry control, and its copy points at one that doesn't exist
  Category: ux
  Where: src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml (snapshot-error triggers at :291/:315, no refresh button anywhere in the view); MainWindow.xaml:1847, 2385, 2650 (all RefreshSnapshotCommand bindings — every one inside the collapsed ShellWorkspaceHost); Strings.resx `Vm_SimpleHomeUnavailableBody` = "Refresh the system check or open Maintenance for help."
  Problem: When the environment snapshot fails to load, Home shows "We couldn't check this PC — Refresh the system check or open Maintenance for help." There is no control named "the system check" and no visible refresh control anywhere in the simplified shell; RefreshSnapshotCommand is reachable only via the undocumented F5 binding. The user's only discoverable recovery is restarting the app.
  Evidence: Grep shows RefreshSnapshotCommand bound at MainWindow.xaml:1847/2385/2650, all within the collapsed region (1400–2995); RecommendedWorkspaceView.xaml contains no Refresh/Verify button (grep clean). The command exists and works (MainViewModel.cs:159,255; F5 global binding per schemas/keyboard-focus-contract.json).
  Fix: In the snapshot-error state of RecommendedWorkspaceView, show a secondary button (SecondaryButtonStyle, AutomationId e.g. `RetrySystemCheckButton`) bound to RefreshSnapshotCommand, and reword `Vm_SimpleHomeUnavailableBody` to name it (e.g. "Select Try again, or open Maintenance for help."). While in that string family, fix the voice break: retitle `Vm_SimpleHomeUnavailableTitle` ("We couldn't check this PC") to match the app's third-person voice used by `Vm_ShellSnapshotUnavailableDetail` ("LibreSpot couldn't verify this PC."). Update all five locales.
  Acceptance: `--uia-smoke=snapshot-error` capture shows a focusable retry button; invoking it re-runs the snapshot; QA matrix row for snapshot-error asserts the new AutomationId as focus target.
  Confidence: Verified
  Effort: M

- [ ] P2 — RD-78: Theme search box is labeled "Theme pack" and both search fields look like empty broken controls
  Category: ux
  Where: src/LibreSpot.Desktop/Views/CustomAppearanceSection.xaml:24-34 (label `ThemePackLabel` directly above a TextBox whose real purpose — gallery search — exists only in AutomationProperties); src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml:25-37 ("Find a setting" as a label above an empty TextBox); Themes/Controls.xaml `TextBoxStylePremium`
  Problem: Neither search TextBox has an in-field watermark, so both render as bare empty boxes. The settings one at least has "Find a setting" as its label; the theme one sits under the label "Theme pack", which describes the section, not the input — sighted users see a dead-looking dropdown-sized box with no hint it filters the gallery ("Search themes and schemes" exists only for screen readers). The resx comment on `SearchPlaceholder` even says "Search box watermark text", but it is rendered as a plain label, not a watermark.
  Evidence: Observed in captures this session at both 1440x1024 and 1080x720 (custom state): both boxes render empty with no placeholder. XAML confirms no watermark mechanism in TextBoxStylePremium and no visible text tied to the theme search's purpose.
  Fix: Add watermark support to `TextBoxStylePremium` (a template TextBlock shown when Text is empty and the box is unfocused, bound to an attached property or Tag, using MutedTextBrush), then set "Find a setting" and a new localized "Search themes and schemes" watermark on the two boxes. Give the theme search its own visible label or rely on the watermark, and keep "Theme pack" as the section heading above the gallery it actually describes.
  Acceptance: Custom-state capture shows placeholder text inside both empty search boxes in dark and high-contrast themes; watermark disappears on input; localization gate passes for the new key.
  Confidence: Verified
  Effort: M

- [ ] P2 — RD-79: Disabled rail navigation buttons dim an already-muted foreground to ~1.6:1 contrast
  Category: a11y
  Where: src/LibreSpot.Desktop/Themes/Controls.xaml:474 (`Opacity="0.45"` on the IsEnabled=False trigger of ShellNavButtonStyle's template root), :418 (style Foreground = MutedTextBrush); reached via SimpleShellNavButtonStyle (MainWindow.xaml:1202, BasedOn, no Foreground override) applied to the three rail buttons at MainWindow.xaml:1255/1283/1311
  Problem: The disabled state multiplies MutedTextBrush (#AEBEC5) by 0.45 over the #070C12 rail — roughly 1.6:1, far below the WCAG 3:1 floor even for disabled-state discernibility, and the whole subtree (icon, accent rail) dims with it. In high contrast, MutedTextBrush maps to GrayText and is then dimmed a further 55%, violating the HC convention that disabled text is exactly GrayText, uncomposited. The rail buttons actually disable during runs (`IsShellInteractionEnabled` gates SimpleShellHost), so this state is routinely visible.
  Evidence: Theme sweep this session traced the style chain and computed the compound; reachability confirmed via SimpleShellHost `IsEnabled="{Binding IsShellInteractionEnabled}"` (MainWindow.xaml:1213-1214).
  Fix: Replace the opacity dim with an explicit disabled Foreground setter (e.g. a new DisabledTextBrush token defined in both palettes — Palette.xaml dark value tuned to ≥3:1 over RailPanelBrush; HighContrastPalette maps it to SystemColors.GrayTextBrushKey) and drop the root-level Opacity to at most a mild 0.85 on the icon only, or remove it entirely.
  Acceptance: Contrast of disabled rail label over the rail background measures ≥3:1 in dark theme (verify from a QA capture's pixels); in high contrast the disabled label renders pure GrayText with no opacity composite.
  Confidence: Verified
  Effort: S

- [ ] P2 — RD-81: The published community catalog can silently drift from the reviewed manifest
  Category: reliability
  Where: tools/Build-CommunityCatalog.ps1 (generator); gh-pages branch (catalog.json `generatedDate: 2026-08-20`, index.html); Build-Scripts.ps1 (no catalog gate); README.md:336 (links the catalog as the public trust surface)
  Problem: The public catalog page advertises per-asset trust evidence (pinned SHA256, review decisions, provenance) but nothing ties the published gh-pages content to the current schemas/community-assets.json. If a review is revoked or an asset's pin changes, the local gates all pass while the public page keeps advertising stale trust data indefinitely. There is also no documented step anywhere in the tracked docs for regenerating and pushing the catalog.
  Evidence: `grep -rn "Build-CommunityCatalog"` matches only the generator itself and its Pester tests — no gate, no doc reference; gh-pages fetched this session shows catalog.json generated 2026-08-20; README documents the release procedure with no catalog step.
  Fix: Add a `-Validate` (or dedicated `-CatalogTruth`) check that regenerates the catalog into a temp directory and compares the normalized catalog.json against `origin/gh-pages:catalog.json`, tri-state like the existing release-truth gates: match = pass, definitive mismatch = fail with "regenerate and push gh-pages", unreachable remote = warn-only so offline checkouts stay green. Document the regenerate-and-push step in README's local release procedure.
  Acceptance: Editing community-assets.json without republishing makes the gate fail with an actionable message; the README release procedure includes the catalog step; offline runs do not fail.
  Confidence: Verified
  Effort: M

- [ ] P2 — RD-82: Eight divergent atomic-write implementations, two with real durability gaps
  Category: reliability
  Where: src/LibreSpot.Cli/Program.cs:1013-1023; src/LibreSpot.Desktop/Services/ConfigurationService.cs:134-145; src/LibreSpot.Core/UpstreamDriftService.cs:396-413; src/LibreSpot.Desktop/Services/LocalProfileService.cs:286-293 and :950-970; src/LibreSpot.Core/OperationJournalUndoService.cs:686-690; src/LibreSpot.Core/SupportBundleService.cs:168-233; src/LibreSpot.Core/BackendScriptService.cs:510-544
  Problem: The same write-temp-then-move pattern is hand-rolled eight times with materially different guarantees. Concrete gaps: LocalProfileService's `WriteUserProfileDocumentAsync` (~:955) uses a non-unique temp name (`{path}.{pid}.tmp`) with `FileMode.Create`, so two concurrent saves of the same profile in one process clobber each other's temp file; both LocalProfileService sites and OperationJournalUndoService (`File.WriteAllTextAsync`, no temp+flush at :686) skip `Flush(flushToDisk: true)`, so a power loss can leave a truncated profile or journal while config/drift writes survive. The PowerShell side already solved this once (src/powershell/shared/Write-LibreSpotFileDurable.ps1) — the C# side never consolidated.
  Evidence: All eight sites read this session; the two LocalProfileService sites differ from each other (GUID temp at :283 vs pid-only temp at :955) proving drift has already happened.
  Fix: Add one `AtomicFile` helper in LibreSpot.Core (write to `{path}.{Guid:N}.tmp` with CreateNew, FlushAsync + Flush(flushToDisk:true), File.Move overwrite, delete temp on failure) and route the JSON/text writers through it. Zip/stream writers (SupportBundleService, BackendScriptService) can keep their specialized paths but should adopt the same temp-name convention.
  Acceptance: All listed sites call the shared helper; a Core unit test proves temp-file cleanup on write failure and uniqueness under two concurrent writes to the same target.
  Confidence: Verified
  Effort: M

- [ ] P2 — RD-83: The PS2EXE release step is undocumented and nothing pins the compiled exe's identity
  Category: reliability
  Where: README.md:470-472 ("compile `LibreSpot.ps1` with PS2EXE" — no command); Build-Scripts.ps1 (no PS2EXE switch); schemas/release-artifact-contract.json (LibreSpot.exe buildMode "ps2exe", no flags/version requirements)
  Problem: Every other artifact has an exact documented build path; LibreSpot.exe's flags (icon, -requireAdmin, -noConsole, title/product/version resource) are improvised per release. The preview.27 build set `-version 3.7.4.0` by hand; no gate compares the PE version resource against the script version, so a future release can ship LibreSpot.exe with a missing or wrong file version, no icon, or without the admin manifest — and the release manifest would still verify (it checks SHA256 of whatever was produced).
  Evidence: Grep this session: "ps2exe" appears in README prose and SECURITY notes only; no tracked script invokes Invoke-ps2exe; release manifest generation (Build-Scripts.ps1 -GenerateReleaseManifest) verifies hashes and sizes only.
  Fix: Add a `-CompileStableExe` switch to Build-Scripts.ps1 that runs Invoke-ps2exe with pinned flags (inputFile LibreSpot.ps1, outputFile publish\LibreSpot.exe, iconFile LibreSpot.ico, -requireAdmin -noConsole, title/product LibreSpot, version derived from Get-LibreSpotScriptVersion + ".0"), and extend the release-manifest generation to fail when LibreSpot.exe's FileVersionInfo does not match the script version. Replace the README prose with the one-line command.
  Acceptance: `Build-Scripts.ps1 -CompileStableExe` reproduces the artifact; `-GenerateReleaseManifest` fails when publish\LibreSpot.exe's file version disagrees with LibreSpot.ps1's version; README documents the exact command.
  Confidence: Verified
  Effort: M

### P3

- [ ] P3 — RD-85: Dead ViewModel/Core members batch (zero-reference properties and accessors)
  Category: maintainability
  Where: MainViewModel.cs:661-663 (HasWarningHealthIssues/HasInfoHealthIssues/HasAnyHealthIssues), :767 (WorkspaceRecommendationDetail), :804 (EnabledToggleCountLabel), :835 (SelectedCustomAppCountLabel), :1183/:1233 (IsOverviewWorkspaceSelected + ShowRailRunDuration pair); MainViewModel.Profiles.cs:93 (CanEditSelectedLocalProfile); ViewModels/LocalProfileCardViewModel.cs:38 (UpdatedText); Views/MaintenanceWorkspaceView.xaml.cs:15 (CompatibilityVerdictSurface accessor); Services/LocalizationService.cs:48 (IsSupportedCulture); src/LibreSpot.Core/AppCatalog.cs:378-380 (HasMissingAssets/HasReviewRequiredAssets/HasCatalogReviewIssues), :701 (StackHealthComponent.HasDetectedVersion), :855 (PinnedStatsCustomAppVersion — a hand-maintained duplicate of the version inside PinnedStatsCustomAppReleaseTag at :856 that nothing validates)
  Problem: Each member's only occurrences are its declaration and (for VM properties) its own OnPropertyChanged self-notify — no XAML binding, no caller, no test. PinnedStatsCustomAppVersion is the one with teeth: two hand-edited strings encode the stats pin version and only the tag is used, so bumping one without the other drifts silently.
  Evidence: Dead-code sweep this session confirmed zero references per member via repo-wide grep; exclusions honored for members that feed the collapsed legacy host or are pinned by tests.
  Fix: Delete each listed member and its self-notify line. For the stats pin, either delete PinnedStatsCustomAppVersion or keep it as the single source and derive the release tag from it (`$"stats-v{PinnedStatsCustomAppVersion}"`), whichever direction AppCatalog's style favors. Build + full non-WPF suite proves nothing consumed them.
  Acceptance: Build succeeds; 920 non-WPF and 135 WPF tests still pass; grep finds no remaining references to the deleted names.
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-86: Five Spotify-version parsers in Core with divergent semantics, including a same-file near-duplicate
  Category: maintainability
  Where: src/LibreSpot.Core/AppCatalog.cs:1203-1214 (NormalizeSpotifyVersion — no v-prefix handling); CompatibilityVerdict.cs:250-268 (TrimStart 'v','V'); SpicetifySupportContract.cs:361-374 (regex with optional v); UpstreamDriftService.cs:321-342 and :632-651 (two near-verbatim copies in one file, differing only in null vs 0.0.0 fallback)
  Problem: The five implementations disagree on "v" prefixes, suffix stripping, and fallback values. Today no reachable caller feeds a v-prefixed string into the strict one (AppCatalog's input is FileVersionInfo-derived, verified via MainViewModel.CustomInstall.cs:717 → HealthReport component DetectedVersion), so this is latent — but any future caller that passes tag-shaped input ("v1.2.94") gets zero warnings from AppCatalog where CompatibilityVerdict would flag it. The intra-file duplicate in UpstreamDriftService is pure copy debt.
  Evidence: All five read and compared this session; reachability of the divergence traced and found latent, not live.
  Fix: Add one `SpotifyVersion.TryParse` (or similar) in Core implementing the most lenient semantics (trim, optional v/V, strip -/+ suffix, 3-part zero-pad) and route all five sites through it; at minimum merge the two UpstreamDriftService copies immediately.
  Acceptance: One parser remains; existing Core/Desktop tests pass; a new unit test covers "v1.2.94", "1.2.96.518", and suffixed inputs through the shared parser.
  Confidence: Verified
  Effort: M

- [ ] P3 — RD-87: Well-known LibreSpot paths are string-literal triplicates across writer and reader
  Category: maintainability
  Where: logs dir: EnvironmentSnapshotService.cs:65, SupportBundleService.cs:88, CrashReporter.cs:25-26; crashes dir: EnvironmentSnapshotService.cs:68, SupportBundleService.cs:91, CrashReporter.cs:29-30 (+ temp fallback at :473); config dir: Program.cs:2071-2072, ConfigurationService.cs:39, EnvironmentSnapshotService.cs:1828
  Problem: SupportBundleService reads the exact directories CrashReporter writes; the pairing exists only by string coincidence. A rename on one side silently produces support bundles with empty logs/crashes sections — the failure mode is invisible because empty sections are also legitimate.
  Evidence: Sites enumerated and cross-checked this session; no shared constant exists (grep).
  Fix: Add a `LibreSpotPaths` static class in LibreSpot.Core (ConfigDirectory, LogsDirectory, CrashesDirectory) and route all listed sites through it.
  Acceptance: The literals appear exactly once in src/; support bundle tests still pass.
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-88: Snapshot probes launch bare "powershell.exe" while BackendScriptService deliberately resolves the absolute path
  Category: security
  Where: src/LibreSpot.Core/EnvironmentSnapshotService.cs:488 and :2460 (FileName = "powershell.exe", PATH-resolved); BackendScriptService.cs:500-503 (resolves %SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe first, private method)
  Problem: The backend launcher hardened PowerShell resolution against PATH interposition; the two Core snapshot probes did not, so they inherit whatever powershell.exe PATH serves. For an asInvoker process this is defense-in-depth rather than a boundary break (a PATH-writing attacker already runs as the user), but the inconsistency means the hardening decision is silently un-applied on two of three launch sites. Related copy debt: the timed stdout-drain boilerplate around those probes exists three times (:486-527, :2456-2497, :2557-2591) and the third copy dropped the drained-check the second one's comment says to keep.
  Evidence: All sites read this session; GetPowerShellPath() is private to BackendScriptService so the probes cannot currently reuse it.
  Fix: Promote GetPowerShellPath to `internal static` (or move to a shared helper) and use it at both probe sites; fold the three drain blocks into one `ProcessProbe.Run(fileName, args, timeout)` helper while there.
  Acceptance: No `FileName = "powershell.exe"` bare literal remains in Core; existing snapshot tests pass.
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-89: Title-bar DWM colors are hardcoded duplicates that drifted from the palette
  Category: visual
  Where: src/LibreSpot.Desktop/Services/Win11ShellIntegration.cs:63-65 (caption #0B0F0D, text #EAF2ED, border #2A3630)
  Problem: The three DWM chrome colors are green-era residue duplicating palette tokens that have since moved to the blue-family values (CanvasColor #070C12, TextColor #EDF4F6, StrokeColor #1E3542), so the title bar no longer exactly matches the shell canvas. High contrast is properly guarded (early return + ClearCustomChrome), so this is dark-theme drift only.
  Evidence: Theme sweep compared the literals against Themes/Palette.xaml values this session.
  Fix: Resolve the three colors from the application resources at apply time (the file already uses TryFindResource at :85/:116 for the re-apply path — use the same pattern in ApplyChrome) instead of hardcoding, so palette edits propagate.
  Acceptance: ApplyChrome contains no color literals; title bar pixel-matches CanvasColor in a capture.
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-90: Theme-dependent shadow/glow effects and one imperative brush lookup don't react to a runtime high-contrast toggle
  Category: a11y
  Where: StaticResource DropShadowEffects: MainWindow.xaml:1135 (ActivityCardStyle → used :3282), :1170 (PromptCardStyle → used :3901), Views/RecommendedWorkspaceView.xaml:282 (AccentGlow on the hero ring); imperative lookup: MainWindow.xaml.cs:346 (`FindResource("AccentRingBrush")` assigned to BorderBrush in the UIA focus-visual path)
  Problem: ThemeManager swaps the palette dictionary when the OS high-contrast setting changes mid-session (SystemParameters.StaticPropertyChanged), but StaticResource-resolved effects and FindResource-assigned brushes keep their dark-theme instances: the green #55D98A hero glow and the dark overlay shadows survive into high contrast (HC palette defines both effects at Opacity 0), and the focus ring keeps the translucent green instead of SystemColors.Highlight. Initial-launch HC is correct; only the runtime toggle is affected.
  Evidence: Theme sweep this session traced ThemeManager.cs:52-55 (whole-dictionary swap → every palette key is theme-dependent), confirmed the three effect references are reachable outside the collapsed host, and confirmed HighContrastPalette.xaml:177-178 zeroes both effects.
  Fix: WPF cannot DynamicResource an Effect inside a Style cleanly; instead have ThemeManager raise its existing change event and re-apply the two card styles' effects imperatively, or bind Effect via a trigger on a ThemeManager-exposed IsHighContrast property. For MainWindow.xaml.cs:346, use `control.SetResourceReference(Control.BorderBrushProperty, "AccentRingBrush")` instead of FindResource assignment.
  Acceptance: Toggling Windows high contrast while the app runs flattens the hero glow and card shadows and re-colors the focus ring without restart (manual verification note in the commit).
  Confidence: Verified
  Effort: M

- [ ] P3 — RD-91: Terminology and punctuation drift across live UI strings
  Category: ux
  Where: src/LibreSpot.Desktop/Properties/Strings.resx (en + 4 satellites), keys listed below
  Problem: Same concept, different names, live in reachable surfaces: "Spotify build" (SpotifyBuildLabel, SpotifyCompatibilityHint) vs "Spotify version" (DashboardSpotifyVersionLabel, Compatibility*); "Premium account mode" vs "Premium account patch posture" vs "Premium flag" (Option_SpotX_Premium_Title / Vm_ProfileComparisonAreaPremiumPatch / Vm_ProfileSummaryFormat — the comparison fragment is also the only capitalized member of its lowercase-fragment family); "activity view" vs "activity panel" vs "run log" (Vm_ShellClearLogLabel / Ui_CloseActivityPanel / RunLogLabel). Punctuation: five strings use ASCII "..." while StoppingBackend uses "…" (Vm_ProgressStopping, Vm_ProgressWorking, Vm_ShellCheckingSystemDetail, Vm_ProfileComparisonPreparing, Vm_ProfileSharePreparing); Vm_CustomSearch*Format use straight quotes where Vm_GlobalSearch*Format use curly; six strings use a spaced hyphen as a sentence dash (Vm_MarketplaceFilesMissing, Vm_MarketplaceHidden, Vm_SetupCompleteClosingLog, CrashSourceFolderFormat, HealthEvidenceSpicetifyUnsupportedVersionFormat — where "{1}.x -" reads as a minus sign — and Vm_ThemeSummaryFormat "{0} - {1}" vs Vm_ProfileSummaryFormat's " / " for the same pair). Voice: four Option_*_Description strings are third-person while 22 siblings are imperative (SendVersionOff, Mirror, ConfirmUninstall, DevTools); Option_SpotX_DevTools_Title is the lone Title Case member of its family; Extension_adblock_Title/Description and Extension_beautiful_lyrics_Description break their families' shapes; Maintenance_FullReset_Description is the lone third-person member of the Maintenance_*_Description family; Option_SpotX_Mirror capitalizes "GitHub.io" in the title and "github.io" in the description.
  Fix: Normalize per family: "Spotify version" everywhere (reserve "build" for the OS); "Premium account mode" in all three; "activity panel"/"run log" split (container vs contents → "Clear run log"); "…" everywhere; curly quotes everywhere; replace spaced hyphens with a period and new sentence (per the project's no-dash writing rule — do not introduce em dashes; the one existing em dash in Maintenance_SafeMode_Description should also become a period); imperative voice for the four Option descriptions and the FullReset description; sentence-case the DevTools title; align the adblock/beautiful-lyrics strings to their family shapes. Mirror all edits into the four satellites and run the localization gate.
  Evidence: Full 1,278-key microcopy sweep this session; every key re-verified live except Vm_GlobalSearch* (those bind to the collapsed global-search surface — normalize them anyway or fold them into the blocked global-search decision). Also from that sweep, only if global search returns: Vm_CustomPatchesSearchDescription is visible keyword soup ("SpotX patches.json JSON authoring regex validation dry run import URL") passed as a result description at MainViewModel.CustomInstall.cs:283 while real keywords ride the separate 4th argument.
  Acceptance: One term per concept across the resx; a grep for `\.\.\.`, ` - ` (in sentence position), and straight quotes inside *MatchesFormat values returns clean; localization gate passes.
  Confidence: Verified
  Effort: M

- [ ] P3 — RD-92: Dead-end failure strings and crash-dialog jargon
  Category: ux
  Where: Strings.resx keys Vm_UndoStateChanged ("Current state changed; undo was refused."), Vm_UnknownBackendFailure ("LibreSpot reported an unknown backend failure." — the fallback at MainViewModel.cs:1986 when the backend gives no message), Vm_SupportBundleExportFailedFormat ("Export failed: {0}"), Vm_ConfigSaveFailed (bare ActivityStep at MainViewModel.cs:1963), Vm_ProfileComparisonUnavailable; CrashExceptionSummaryLabel ("EXCEPTION SUMMARY") and CrashNoExceptionMessage ("No exception message was provided.") in the crash dialog every user can hit
  Problem: Each states a failure without a next step while direct siblings in the same surfaces do give one (Vm_UndoSelectLowRisk, Vm_ActionCouldNotFinishDetail, Vm_CustomPatchesFormatFailedDetail); the crash dialog leads with .NET type-system vocabulary at the exact moment a non-technical user needs plain language.
  Evidence: Microcopy sweep + call sites verified this session (MainViewModel.cs:1986, :1963, :2261 region).
  Fix: Give each failure string a concrete next step ("Copy the log or export a failure bundle before retrying", "Preview the selection again to see what still applies", "Check that the target folder is writable, then try again"); retitle the crash section in plain language ("What failed") and make the empty-message fallback point at the saved report path. All five locales; keep the strings calm and short.
  Acceptance: Each listed key's value names an action available on that surface; localization gate passes.
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-93: Half the resx carries boilerplate translator comments, riskiest on placeholder strings
  Category: maintainability
  Where: src/LibreSpot.Desktop/Properties/Strings.resx — 517 comments reading exactly "MainViewModel localized runtime text." and 108 reading "User-facing WPF text" (625 of 1,278)
  Problem: Comments are the only context translators get; these say nothing about surface, length budget, or placeholder meaning. The file already demonstrates the good pattern (e.g. Vm_UndoSucceededFormat documents {0}/{1}). The ~90 boilerplate keys containing format placeholders are the ones a translator can silently break.
  Evidence: Counted during the full-file microcopy sweep this session.
  Fix: Backfill comments for the placeholder-bearing boilerplate keys first (state what each {n} is), then opportunistically for high-traffic surfaces. No code change; localization gate unaffected.
  Acceptance: Every key whose value contains `{0}` has a comment naming the placeholder's meaning.
  Confidence: Verified
  Effort: M

- [ ] P3 — RD-94: keyboard-focus-contract.json describes the pre-simplification shell
  Category: docs
  Where: schemas/keyboard-focus-contract.json `pages` section ("sidebar RadioButtons", "Recommended/Custom/Maintenance" pages, run-button placement); the simplified shell's rail (Buttons, Home/Maintenance/Settings) and its new language ComboBox are absent
  Problem: The contract is the stated source of truth for tab order and focus behavior, but its pages section documents a shell that no longer exists; the tests that consume the file assert overlays/focus visuals, so nothing failed when the shell changed. An implementer following the contract would build the wrong tab order.
  Evidence: Read this session; pages prose says "sidebar RadioButtons are keyboard-navigable" while the rail uses Button styles (MainWindow.xaml:1255+); SimpleShellLanguageSelector absent from the contract.
  Fix: Rewrite the pages section for the simplified shell: rail order (Home, Maintenance, Settings, language selector, then workspace content), initial focus per state, and the F5/Escape bindings that survive. Keep overlay sections as-is (still accurate).
  Acceptance: Contract matches a manual tab-through of the running shell; KeyboardFocusContractTests still pass.
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-95: Profiles list viewport bisects a card mid-title, reading as an overlap bug
  Category: visual
  Where: src/LibreSpot.Desktop/Views/CustomProfileSummarySection.xaml:51-61 (ListBox MinHeight 220 / MaxHeight 260 over card items ~140px tall)
  Problem: The fixed viewport shows ~1.8 cards, cutting the next card mid-title with no fade or affordance; the elevated "Selected profile" panel below makes the cut look like a z-order overlap. Observed at both default and minimum window sizes ("Minimal / Marketplace-only" title bisected in captures this session).
  Evidence: Captures at 1440x1024 and 1080x720 this session; XAML rows confirm stacked (not overlapping) layout, so this is a viewport-affordance problem, not a layout bug.
  Fix: Size the viewport to whole cards (MaxHeight to a multiple of the card height + spacing, or snap scroll extents), and add a bottom fade (OpacityMask gradient on the ListBox) so a partially visible card reads as scrollable content.
  Acceptance: Custom-state capture shows either whole cards or a clear fade at the viewport edge; nothing appears bisected by the summary panel.
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-96: publish-footprint-budget.json cites a "Release CI" that doesn't exist, and no gate enforces the budget
  Category: docs
  Where: schemas/publish-footprint-budget.json (description: "Release CI records actual metrics in publish-footprint.json per build"; budget maxSizeMiB 200 / warnSizeMiB 180); no reference to the file anywhere in Build-Scripts.ps1 or tests (grep clean apart from CHANGELOG)
  Problem: The repo's policy is local builds with no CI, so the recording mechanism the schema describes cannot run, no publish-footprint.json is ever produced, and nothing checks the budget. The preview.27 desktop exe is 175.4 MiB — inside 4.6 MiB of the warn threshold — so the budget will quietly trip with no gate to notice.
  Evidence: Grep this session found zero enforcement references; publish\LibreSpot-Desktop.exe measured 183,934,130 bytes.
  Fix: Wire the budget into `-GenerateReleaseManifest`: measure publish\LibreSpot-Desktop.exe, warn at warnSizeMiB, fail at maxSizeMiB, and write the measured value into the release manifest; update the schema's description to name the local mechanism instead of CI.
  Acceptance: Release-manifest generation warns/fails per the budget; the stale CI sentence is gone.
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-97: WpfQaMatrix capture wait can time out spuriously in full-suite runs
  Category: testing
  Where: tests/LibreSpot.Desktop.Tests/WpfQaMatrixTests.cs:326 WaitForCapture / the per-row app launch around :118-:190
  Problem: In a full `--filter-class "*Wpf*"` run this session, one row (home-navigation/dark/en) failed with "Timed out waiting for WPF QA capture" after 46s, then the whole 80-row theory passed clean in isolation minutes later. A single-row flake fails the suite and costs a 10-minute rerun; under machine load the first capture after many app launches is the vulnerable one.
  Evidence: Both runs from this session (failed full run, clean isolated rerun); also recorded in the repo's working notes as a known flake.
  Fix: Make WaitForCapture retry once by relaunching the app for that row before failing (bounded, logged), or raise the first-launch timeout when the process-start-to-first-capture delta exceeds a threshold. Do not raise the global timeout blindly.
  Acceptance: Ten consecutive full WPF suite runs pass without a capture-timeout failure (or the retry path logs and recovers).
  Confidence: Verified
  Effort: S

- [ ] P3 — RD-98: Areas this audit did not reach — needs a dedicated pass
  Category: testing
  Where: LibreSpot.ps1 GUI flow (~10.8k lines — shared functions, watcher, verification, and download/extract paths were audited; the WinForms/WPF-in-PS GUI event flow, Module-* install/uninstall orchestration bodies, and Spicetify apply/restore sequencing were not line-audited this pass); src/LibreSpot.Core/OperationJournalUndoService.cs internals (undo token execution beyond the CLI guard layer); Marketplace export/restore archive internals (Export-MarketplaceState.ps1 and friends); AvalonEdit custom-patch editor runtime behavior (validate/format/import flows exercised only via tests); Crowdin round-trip (config read, no live sync check)
  Problem: Honest coverage statement so the next audit doesn't assume these were cleared.
  Fix: Schedule a focused pass per area; the PS GUI flow and the undo executor are the two with the most user-facing risk.
  Acceptance: Each listed area either audited in a future pass or consciously accepted.
  Confidence: Verified
  Effort: L
