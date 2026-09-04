# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P3: RD-180: Reconcile the exit-13 category with what the code does with it
  Why: `schemas/fleet-exit-codes.json` files `AssetsNotInstalled` as `"category": "failure"` with
  `"intuneBehavior": "failure"`, while the desktop and CLI both treat it as a completed run that carries a warning.
  The taxonomy already has `success`, `blocked`, `retry` and `reboot` to choose from. The README endpoint table
  also still stops at 12.
  Evidence: `schemas/fleet-exit-codes.json`; `src/LibreSpot.Core/BackendScriptService.cs` maps 13 to a successful
  `BackendRunResult`; `README.md` return-code table. Raised by an adversarial review on 2026-09-03.
  Touches: `schemas/fleet-exit-codes.json`, `README.md`.
  Acceptance: WHEN an admin reads the taxonomy, the category and Intune behaviour recorded for 13 SHALL match how
  the desktop and CLI actually treat it, and the README table SHALL list it.
  Complexity: S
