# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Remaining after 2026-08-21 evening audit

Highest ID: RD-106. Issue tracker: zero open issues/PRs; closed #1-#5 predate v4 and stay closed. Discussions #20 and #21 are operator announcements, not in-repo bugs.

Shipped this pass (deleted from this file): RD-78 search watermarks, RD-79 disabled rail contrast, RD-82 AtomicFile, RD-85 dead members, RD-87 LibreSpotPaths, RD-88 PowerShell host path, RD-89 DWM palette colors, RD-99 snapshot-error glyph, RD-100 screenshot contract, RD-101 WPF-UI template leak gate, RD-102 CycloneDX local tool, RD-103 changelog HEAD-only bullets, RD-104 Jump List labels. Filled/secondary/checkbox/combo disabled states now use DisabledTextBrush; CardListBoxItemStyle remains under RD-105. Bloom checklist/manifest decision drift (the RD-81 note) is aligned; the gh-pages gate below remains.

### P3

- [ ] P3 — RD-125: The eight `Module-*` orchestration bodies and the script GUI event flow have no direct tests
  Why: RD-98 listed five unaudited areas. Four were checked and are covered: the undo executor has four `ExecuteUndoAsync` tests spanning policy refusal, idempotency, injected partial failure, and restoring a missing value, and PATH is its only token kind; Marketplace and the custom-patch service are exercised across hundreds of assertions; the Crowdin config has a mapping test. This one is real.
  Where: LibreSpot.ps1 (8 `Module-*` functions, 61 `Add_*` event handlers); tests/powershell/LibreSpot.Tests.ps1 (27 Describe blocks, zero references to any `Module-*` function)
  Problem: the orchestration bodies that actually install, reapply, and reset are only reached through the composition and parity gates, which compare text rather than behaviour. A logic change inside one of them breaks nothing that runs locally.
  Fix: Pester coverage per `Module-*` function against a temp Spotify tree with the download and elevation boundaries stubbed, starting with the reapply and reset paths. Then the GUI handlers, which mostly marshal to those functions.
  Acceptance: Each `Module-*` function has at least one Pester test that exercises its body against a fake install root; the suite still runs without touching a real Spotify installation.

