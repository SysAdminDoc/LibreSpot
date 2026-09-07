# LibreSpot Roadmap

Incomplete, implementer-actionable work only. Operator-dependent decisions remain in Roadmap_Blocked.md.

## Research-Driven Additions

### P2: Next

- [ ] P2: RD-252. Cover companion startup and retry with integration fixtures
  Why: Impact 3/5. Lifecycle tests assert source text and helpers but do not exercise listener setup, cleanup, or a retry after a failed bootstrap.
  Evidence: Fresh verifier review of tests/surface.test.ts, app-readiness.test.ts, and performance.test.ts.
  Touches: App/extension startup seams and isolated companion fixture tests.
  Acceptance: A staged companion fixture proves startup waits, a failed start cleans up claimed globals and listeners, retry succeeds, and background performance probing never blocks readiness; tests run offscreen without Spotify UI automation.
  Complexity: M
