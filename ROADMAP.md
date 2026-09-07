# LibreSpot Roadmap

Incomplete, implementer-actionable work only. Operator-dependent decisions remain in Roadmap_Blocked.md.

## Research-Driven Additions

### P2: Next

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
