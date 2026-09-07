# LibreSpot Roadmap

Incomplete, implementer-actionable work only. Operator-dependent decisions remain in Roadmap_Blocked.md.

## Research-Driven Additions

### P1: Now

- [ ] P1: RD-247. Reject cache indexes that omit existing entries
  Why: Impact 4/5. A parseable index without an `entries` array is treated as empty, so importing a bundle silently drops every existing cache entry from the index while leaving the files on disk.
  Evidence: Fresh verifier reproduction against src/powershell/shared/Import-LibreSpotAssetCacheBundle.ps1; Core rejects the same malformed shape in src/LibreSpot.Core/AssetCacheBundleService.cs.
  Touches: PowerShell bundle import and cache-index validation; AssetCacheBundle.Tests.ps1 and composed hosts.
  Acceptance: A schema-valid-looking index with a missing, null, or non-array `entries` field is rejected before mutation, with a regression test proving existing files and the index remain unchanged.
  Complexity: S

- [ ] P1: RD-248. Contain cache writes beneath the configured root
  Why: Impact 4/5. A cache-root junction lets object and index writes escape the configured cache directory despite file-level atomic checks.
  Evidence: Fresh verifier junction reproduction against src/powershell/shared/Save-ToAssetCache.ps1 and Write-LibreSpotAssetCacheFileAtomically.ps1.
  Touches: PowerShell cache-root validation, recovery, and write helpers; Core parity checks and cache fixtures.
  Acceptance: Existing or newly introduced reparse points at the cache root or object directory are rejected before any write or recovery, while ordinary directories continue to work on Windows.
  Complexity: M

### P2: Next

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

- [ ] P2: RD-249. Flush pre-existing cache objects during bundle publication
  Why: Impact 3/5. Existing files copied into a replacement cache are not durably flushed, weakening the transaction guarantee during power loss.
  Evidence: Fresh verifier review of src/LibreSpot.Core/AssetCacheBundleService.cs and src/powershell/shared/Import-LibreSpotAssetCacheBundle.ps1.
  Touches: Core and PowerShell cache replacement copy paths; durability fixtures.
  Acceptance: Every copied or imported object is flushed before publication, and a test or instrumented stream proves the existing-file path uses the same durability contract as new files.
  Complexity: M

- [ ] P2: RD-250. Exercise cache recovery across real process termination
  Why: Impact 3/5. Core tests named for process death only move directories in one process, leaving rename and marker boundaries untested under termination.
  Evidence: Fresh verifier review of tests/LibreSpot.Core.Tests/AssetCacheBundleServiceTests.cs and the five-boundary PowerShell fixture.
  Touches: Core recovery test fixtures and subprocess harness; AssetCacheBundleService recovery hooks.
  Acceptance: A disposable helper process is terminated at each publication boundary, then a fresh process recovers and verifies the cache and index without relying on in-process observers.
  Complexity: M

- [ ] P2: RD-251. Require callable companion APIs before bootstrap
  Why: Impact 4/5. Truthy placeholder objects can pass readiness while required React, History, LocalStorage, or Player methods are still unavailable.
  Evidence: Fresh verifier review of src/LibreSpot.App/src/extensions/companion-readiness.ts and companion-readiness.test.ts.
  Touches: Companion readiness predicates and startup tests.
  Acceptance: Readiness remains false until every bootstrap-used method is callable, accepts the existing fully initialized companion, and waits through staged API publication without starting a partial engine.
  Complexity: S

- [ ] P2: RD-252. Cover companion startup and retry with integration fixtures
  Why: Impact 3/5. Lifecycle tests assert source text and helpers but do not exercise listener setup, cleanup, or a retry after a failed bootstrap.
  Evidence: Fresh verifier review of tests/surface.test.ts, app-readiness.test.ts, and performance.test.ts.
  Touches: App/extension startup seams and isolated companion fixture tests.
  Acceptance: A staged companion fixture proves startup waits, a failed start cleans up claimed globals and listeners, retry succeeds, and background performance probing never blocks readiness; tests run offscreen without Spotify UI automation.
  Complexity: M
