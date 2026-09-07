# LibreSpot Roadmap

Incomplete, implementer-actionable work only. Operator-dependent decisions remain in Roadmap_Blocked.md.

## Research-Driven Additions

### P1: Now

### P2: Next

- [ ] P2: RD-240. Preserve the newest dynamic palette through reapplication
  Why: Impact 4/5. Older artwork results overwrite newer colors, and normal navigation/timer apply replaces Material colors with the base palette.
  Evidence: RESEARCH.md; exercised reversed artwork completion and apply-after-Material fixtures; core/engine.ts refreshAccent/apply and companion navigation/minute callbacks.
  Touches: Component core/engine.ts and accent.ts; preview/clearPreview behavior; tests/engine.test.ts.
  Acceptance: Late results from an old track, scheme or accent mode cannot overwrite current state. Navigation, preview cancellation and a minute tick with an unchanged effective scheme retain the current derived palette. Crossing a scheduled light/dark boundary derives the appropriate new palette. Fixed colors and normal scheme changes still work; test both promise completion orders without wall-clock sleeps.
  Complexity: M

- [ ] P2: RD-241. Detect preset edits by contents instead of name
  Why: Impact 3/5. Editing an applied preset retains its name, leaves Applied selected and disables restoring that preset.
  Evidence: RESEARCH.md; src/LibreSpot.App/src/panels/presets.ts uses state.name to select/disable preset actions; WindowBlinds editable-preset comparison.
  Touches: Component presets panel and preset identity/comparison helper; tests/surface.test.ts.
  Acceptance: Apply a preset, change one included control, then reapply it successfully. A user preset sharing a built-in title but differing in preset-owned settings must not select or disable the built-in. Compare only preset-owned settings so unrelated state does not create false differences.
  Complexity: S

- [ ] P2: RD-242. Connect in-client descriptions and result changes to accessibility APIs
  Why: Impact 4/5. Controls expose names but omit associated consequences/scope, and Store search changes are not announced.
  Evidence: RESEARCH.md; src/LibreSpot.App/src/surface/ui.ts description renderers; Store versus Features result status; WCAG 4.1.3.
  Touches: Component shared controls, Features descriptions and Store results; focused DOM accessibility tests.
  Acceptance: Description IDs and aria-describedby connect explanatory text and live/desktop application limits to their controls. Store result count and empty-state changes announce once through a status region. Preserve tab/focus behavior and validate with an isolated screen reader before claiming live accessibility completion.
  Complexity: M

- [ ] P2: RD-243. Report file-logging failures without depending on the failed sink
  Why: Impact 3/5. Serilog catches sink failures internally, while CrashReporter provides no failure-listener or SelfLog path.
  Evidence: RESEARCH.md; src/LibreSpot.Desktop/Services/CrashReporter.cs Initialize; Serilog Reliability documentation and file-sink PR #342, already included by the pinned package.
  Touches: CrashReporter configuration; local diagnostic health/status; focused logging failure tests.
  Acceptance: An injected denied-open or write failure produces a bounded in-memory status and one user-visible diagnostic, with a writable fallback where available. The app stays usable, no recursive logging occurs, and support export reports the missing log instead of implying completeness. Do not enable a network sink.
  Complexity: M

- [ ] P2: RD-244. Bound native process output and error-tail retention
  Why: Impact 3/5. A noisy or hung worker can grow the retained line buffer and full output list throughout its timeout.
  Evidence: RESEARCH.md; src/powershell/shared/Read-ProcessOutputDelta.ps1 and Invoke-SpicetifyCli.ps1; LibreSpotNativeOutputCollector in both host preambles.
  Touches: Shared process-output reader, native collector, external-script capture and Spicetify runner; composed hosts and PowerShell output fixtures.
  Acceptance: Bound buffering in the underlying stream reader and collector as well as retained byte/line/remainder and error tails; continue draining output. Emit a truncation marker. Many short lines and one oversized unterminated line must stay bounded before the first line callback, preserve exit/failure classification and not deadlock. Apply a bounded disk-capture policy to redirected logs as well as memory.
  Complexity: M

- [ ] P2: RD-245. Verify crash capture with an isolated Windows release fixture
  Why: Impact 4/5. Mocked launches and synthetic headers cannot prove that the shipped single-file model creates the intended dump.
  Evidence: RESEARCH.md; MinidumpSettingsServiceTests fake launch/environment; actual .NET 10.0.11 Windows createdump source supports checking platform behavior rather than guessing from generic documentation.
  Touches: tests/LibreSpot.Core.Tests/MinidumpSettingsServiceTests.cs; isolated console crash fixture; local release verification and dump export policy.
  Acceptance: Publish a harmless console fixture with the same win-x64 runtime/bundling settings and arm it through the production service. Record the actual flags/streams as the positive baseline for RD-226, then verify retention, disabled behavior and export eligibility under the completed policy. All settings and output remain under a temporary root; never crash Spotify, the user's running desktop, or capture their process memory.
  Complexity: M

- [ ] P2: RD-246. Reconcile public documentation with the shipped components
  Why: Impact 3/5. Redistribution, licensing, crash/privacy, navigation and validation instructions contradict the implementation.
  Evidence: RESEARCH.md, Documentation; README/SECURITY no-redistribution claims; component AGPL license/notices; current changelog contradictions and incomplete PR-template Pester command.
  Touches: README.md; SECURITY.md; CHANGELOG.md; .github/CONTRIBUTING.md and PULL_REQUEST_TEMPLATE.md; src/LibreSpot.App/README.md; current assertions in Roadmap_Blocked.md.
  Acceptance: Describe bundled versus fetched components and their licenses accurately; list three executable artifacts; use the full configured Pester suite. Correct Prism API, UIA capture, Store navigation and unpublished-release claims. Distinguish raw profile recovery from complete backups. Retain historical records as historical; remove or update only blocker premises disproved by existing code. Add narrowly targeted factual checks where a previous check missed the contradiction, and keep human-written prose conventions.
  Complexity: S
