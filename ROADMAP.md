# LibreSpot Roadmap

Incomplete, implementer-actionable work only. Operator-dependent decisions remain in Roadmap_Blocked.md.

## Research-Driven Additions

### P1: Now

- [ ] P1: RD-224. Commit live edits only after validation and report failed application
  Why: Impact 5/5. Clearing a schedule time saves invalid state before apply throws, so an ordinary edit can break the next startup.
  Evidence: RESEARCH.md; core/engine.ts update/replace save before apply; panels/look.ts immediately commits empty time; runtime update callers discard rejected promises and ignore unavailable flag application.
  Touches: src/LibreSpot.App/src/core/engine.ts and schedule.ts; src/LibreSpot.App/src/extensions/librespot-engine.ts; src/LibreSpot.App/src/panels/look.ts; tests/engine.test.ts and surface.test.ts within the component.
  Acceptance: After RD-223, incomplete time edits remain local drafts and never reach saved state. Inject validation, style/apply and storage failures; previous persisted state and active appearance remain usable. Every failed operation produces one visible error without an unhandled rejection or success toast. When settings save but a host flag API is unavailable, report saved but not applied and retain a retry path. Valid edits still apply.
  Complexity: M

- [ ] P1: RD-225. Redact structured, quoted and prefixed secrets in support exports
  Why: Impact 5/5. Synthetic JSON tokens, multiword quoted passwords and timestamp-prefixed bearer headers survive the current redactor.
  Evidence: RESEARCH.md, Support-export privacy; src/LibreSpot.Core/SupportBundleService.cs RedactText; tests/LibreSpot.Desktop.Tests/SupportBundleServiceTests.cs.
  Touches: SupportBundleService redaction and text-export paths; SupportBundleServiceTests.cs.
  Acceptance: Cover JSON secret properties, escaped quoted arguments, prefixed authorization headers and existing simple forms. Synthetic canaries must be absent from the final exported text entries, while neighboring harmless text stays intact. Exercise the export path as well as the helper; do not claim arbitrary binary content can be redacted.
  Complexity: S

- [ ] P1: RD-226. Enforce the declared minidump privacy policy before export
  Why: Impact 5/5. A synthetic minidump carrying the full-memory flag passes the same check as a Triage-flagged fixture.
  Evidence: RESEARCH.md; SupportBundleService.IsValidMinidump accepts fixtures with header flags 0, MiniDumpWithFullMemory and the runtime Triage flags; Microsoft MINIDUMP_HEADER/MINIDUMP_TYPE documentation.
  Touches: src/LibreSpot.Core/SupportBundleService.cs; tests/LibreSpot.Desktop.Tests/SupportBundleServiceTests.cs; schemas/data-inventory.json; SECURITY.md.
  Acceptance: Derive the accepted flags/stream policy from the positive Windows Triage artifact produced by RD-245 and retain regression coverage. Inspect the complete header flags and relevant stream kinds, reject full/private-memory policy violations and unknown unsupported combinations, and retain structural bounds checks. Full-memory-flagged fixtures with otherwise valid directories must not export; legitimate Triage stack-memory streams must remain accepted. Describe accepted dumps as diagnostic memory that may contain sensitive data; flags are policy checks, not proof every byte is anonymous.
  Complexity: S

- [ ] P1: RD-227. Use junction-safe deletion for every installed-theme replacement
  Why: Impact 5/5. Three theme branches bypass the removal helper that already protects Windows PowerShell 5.1 from nested-junction traversal.
  Evidence: RESEARCH.md; src/powershell/shared/Module-InstallThemes.ps1; Remove-PathSafely.ps1 documents and implements the safer behavior.
  Touches: Module-InstallThemes.ps1; tests/powershell theme fixtures; tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs; composed hosts.
  Acceptance: Bundled, community and official replacements use the approved removal boundary. Temporary NTFS trees containing nested junctions and external canary files must preserve every external byte on both supported PowerShell hosts. Exercise the actual installer branches, including cleanup, rather than checking only the helper's source.
  Complexity: S

- [ ] P1: RD-228. Make backup restoration recoverable across engine and Marketplace stores
  Why: Impact 5/5. Marketplace commits first, so a later engine failure leaves a partially restored setup with misleading failure feedback.
  Evidence: RESEARCH.md; src/LibreSpot.App/src/extensions/librespot-engine.ts restoreState; core/backup.ts writeAll performs a key merge.
  Touches: Component backup/store abstractions and restoreState; tests/backup.test.ts, engine.test.ts and surface.test.ts.
  Acceptance: After RD-223 and RD-224, validate all input and retain both pre-restore states before writing. Keep the existing Marketplace merge semantics explicit. On either store's failure, restore the exact previous affected keys, including removal of newly introduced keys, and the prior engine state. If compensation fails, retain recovery data and name the incomplete half. Reload after each injected failure and verify the resulting bytes.
  Complexity: M

- [ ] P1: RD-229. Retain a durable recovery record before resetting Marketplace
  Why: Impact 5/5. Reset deletes data after a clipboard copy, but replacing the clipboard removes the only reset recovery artifact.
  Evidence: RESEARCH.md; src/LibreSpot.App/src/extensions/librespot-engine.ts resetMarketplaceStorage; Syncify's retained-backup precedent.
  Touches: Component core/backup.ts and store.ts; resetMarketplaceStorage; Health recovery controls; backup and surface tests.
  Acceptance: Persist and read back an owned recovery record outside the namespace being reset before deletion. Failure to retain it stops reset. After reset, replace the clipboard and recreate the runtime; Health must still restore or export the record. Bound retained copies and retain them until successful replacement or explicit dismissal. Integrate both Marketplace backends when RD-235 lands.
  Complexity: M

- [ ] P1: RD-230. Serialize mutating operations across all LibreSpot hosts
  Why: Impact 5/5. Desktop, CLI and watcher can modify the same per-user installation concurrently; task IgnoreNew only excludes another instance of that task.
  Evidence: RESEARCH.md; src/powershell/backend/lane-functions.ps1 watcher; existing locks cover narrower profile/undo/in-process boundaries.
  Touches: Shared PowerShell operation entry points; GUI/backend lane functions; src/LibreSpot.Core/BackendScriptService.cs; CLI mutation dispatch; cross-process tests.
  Acceptance: Key a shared lease to the Windows user and canonical Spotify/Spicetify mutation targets, not the configurable LibreSpot data root. Acquire it before snapshots or Spotify shutdown. Different data roots pointing to the same installation must contend; genuinely separate target installations may proceed independently. Test desktop-backend versus CLI versus watcher contenders, owner termination and nested calls. Contenders defer or report busy without mutation. A successor must not start while a dead launcher's installer descendants still write; coordinate ownership recovery with RD-231 and lock ordering with RD-236.
  Complexity: M

- [ ] P1: RD-231. Own and terminate external installer process trees
  Why: Impact 5/5. Killing only the immediate PowerShell process can leave descendants writing while the operation reports failure and begins recovery.
  Evidence: RESEARCH.md; Invoke-ExternalScriptIsolated.ps1 timeout; BackendScriptService.TryKillTree only covers cancellation/watchdog; Windows Job Object documentation.
  Touches: src/powershell/shared/Invoke-ExternalScriptIsolated.ps1; backend process lifecycle; PowerShell/process fixture tests.
  Acceptance: Use a PS5.1-compatible owned process-tree mechanism. A harmless hidden child/grandchild fixture must leave no owned process alive after timeout, cancellation or launcher exit, before rollback/cleanup proceeds. Exercise standalone and backend lanes. Preserve unrelated processes and fail explicitly if containment cannot be established.
  Complexity: M

- [ ] P1: RD-232. Stage and verify installed packages before replacing working copies
  Why: Impact 5/5. CLI, theme and custom-app installers remove working directories before extraction/copy/bootstrap has succeeded.
  Evidence: RESEARCH.md; Module-InstallSpicetifyCLI.ps1, Module-InstallThemes.ps1 and Module-InstallCustomApps.ps1; preservation snapshots do not restore exact CLI/theme bytes.
  Touches: Those shared installers; package staging/swap helper; installer regression fixtures; composed hosts.
  Acceptance: After RD-227, RD-230 and RD-231, stage each pinned package and verify its complete required contents. Commit the package, companion Extensions and affected config as one recoverable operation. Inject extraction, copy, bootstrap, rename and post-rename configuration failures; the original package, companions and configuration fingerprints must remain intact, with no mixed version. Keep staging on the target volume; validate owned recovery paths and reparse boundaries before recovering abandoned commits on the next operation.
  Complexity: M

### P2: Next

- [ ] P2: RD-233. Preserve the watcher failure stage until it is recorded
  Why: Impact 3/5. A finally block clears the stage before the caller reads it, replacing specific patch failures with generic reapply.
  Evidence: RESEARCH.md; exercised Invoke-HeadlessReapply with no-op dependencies; both src/powershell/backend/lane-functions.ps1 and gui/lane-functions.ps1 share the ordering defect.
  Touches: Both lane functions; shared failure result/exception data; tests/powershell/LibreSpot.Tests.ps1; composed hosts.
  Acceptance: Fail download, parameter construction, patching and Spicetify application through the actual watcher call chain. Each retained diagnostic must name the originating stage; clear transient state only after recording it. Preserve the existing retry/hold policy.
  Complexity: S

- [ ] P2: RD-234. Track IndexedDB operations through late completion
  Why: Impact 4/5. A timed-out open leaks a later connection; a blocked delete can finish after the UI declares that nothing was reset.
  Evidence: RESEARCH.md; exercised late-success open fixture; core/backup.ts open/deleteAll; IndexedDB deletion algorithm waits for other connections before continuing.
  Touches: src/LibreSpot.App/src/core/backup.ts; reset status contract and Health; tests/backup.test.ts and surface.test.ts.
  Acceptance: Close every connection delivered after a settled open attempt. Keep blocked deletion visibly pending until terminal completion, prevent duplicate reset requests, and handle eventual success after another connection closes. A timeout must not claim cancellation. Test blocked-then-success, timeout-then-success and terminal-error paths with unrelated storage canaries.
  Complexity: M

- [ ] P2: RD-235. Include Marketplace legacy and fallback storage in recovery
  Why: Impact 4/5. Upstream v1.0.11 can migrate surviving legacy keys back after reset and uses localStorage when IndexedDB is unavailable.
  Evidence: Marketplace #1231 and pinned v1.0.11 src/logic/Storage.ts; LibreSpot core/backup.ts reads/deletes IndexedDB only.
  Touches: Component Marketplace storage adapter, backup schema/parser, reset/restore paths and Health coverage text.
  Acceptance: Inspect and preserve only Marketplace-owned keys from both backends, with explicit precedence matching pinned upstream migration. After RD-228, RD-229 and RD-234, reset and reload through a migration fixture must not resurrect old themes. Restore the captured active/fallback state; unrelated Spotify and LibreSpot keys remain byte-identical. Keep prior backup files readable and distinguish unavailable storage from an empty store.
  Complexity: M

- [ ] P2: RD-236. Atomically publish cache objects and serialize index updates
  Why: Impact 4/5. Direct object overwrites and unlocked whole-index writes can truncate files or lose another writer's entries.
  Evidence: RESEARCH.md; Save-ToAssetCache.ps1 and Update-AssetCacheIndexEntry.ps1; C#/PowerShell import and cache-clear entry points.
  Touches: Shared cache helpers; src/LibreSpot.Core/AssetCacheBundleService.cs; cache inventory/clear entry points in AppCatalog.cs and the PowerShell lanes; cache fixture tests.
  Acceptance: Stage and flush objects before publication. Re-read and update the index under a shared cache lease used by both languages, import and clear-cache. Two distinct concurrent inserts must retain both verified objects and entries. Interrupted writes preserve a parseable prior index; corrupt input is retained/reported instead of silently replaced by an empty inventory. Respect RD-230 lock ordering.
  Complexity: M

- [ ] P2: RD-237. Recover cache directory swaps after process death
  Why: Impact 4/5. Existing catch-based rollback cannot run when the importer terminates between its two renames.
  Evidence: RESEARCH.md; AssetCacheBundleService.CommitPreparedCache; PowerShell bundle import; existing interruption tests throw exceptions rather than terminate a process.
  Touches: src/LibreSpot.Core/AssetCacheBundleService.cs; PowerShell cache import; tests/LibreSpot.Core.Tests/AssetCacheBundleServiceTests.cs; tests/powershell/AssetCacheBundle.Tests.ps1.
  Acceptance: After RD-236, persist a bounded transaction record before renaming and recover it under the same lease on the next invocation. Terminate a fixture process before/after each rename and commit marker. Restart must yield the byte-identical prior cache, including its unindexed files, or the complete committed replacement with every imported asset verified. Validate recorded owned sibling paths and reparse boundaries before replay or cleanup; preserve unrelated directories and account for rollback/staging paths.
  Complexity: M

- [ ] P2: RD-238. Bound engine readiness and background frame measurement
  Why: Impact 4/5. Failed companion startup leaves endless polling or a cached failed runtime; frame measurement can also wait forever.
  Evidence: RESEARCH.md; app.ts useRuntime; companion waitForApi; core/performance.ts; engine.start awaits the frame probe.
  Touches: Component startup/readiness contract, app.ts, core/performance.ts and engine.ts; engine/surface tests.
  Acceptance: Missing APIs, failed startup and no animation frames reach a persistent, accessible error or deferred-measurement state within a defined deadline. Test a runtime published before start fails: invalidate the captured object, clean up and bind the replacement on retry. Retry can recover when APIs arrive. Hidden windows skip/defer FPS measurement and do not permanently lower effects based on a background sample. No pending measurement may prevent normal listener setup.
  Complexity: M

- [ ] P2: RD-239. Contain panel render failures within the LibreSpot surface
  Why: Impact 4/5. A panel exception currently has no local error boundary or persistent repair route.
  Evidence: RESEARCH.md; src/LibreSpot.App/src/app.ts mounts panels without a boundary; React Component error-boundary documentation.
  Touches: Component app shell and panel wrapper; spicetify-globals declarations as needed; tests/surface.test.ts.
  Acceptance: Throw from each panel fixture and retain navigation, a concise accessible error, and retry/Health access. Valid panels remain usable. Do not clear saved settings or expose raw sensitive data in the error view. Verify using the React version supplied by the host.
  Complexity: M

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
