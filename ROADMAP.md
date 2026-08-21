# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Research-Driven Additions

IDs continue the RD-nn scheme. Highest ID in this file: RD-72. RD-47–RD-50 are absent because they already landed on `main` (CHANGELOG `[Unreleased]`); do not re-file them.

Existing items RD-51–RD-57 below are the same work items as the prior pass, with evidence corrections in-place (especially RD-54). New items start at RD-63.

### P0

- [ ] P0 — RD-63: Publish preview.26 artifacts that include the six post-tag commits
  Why: Tag `v4.0.0-preview.25` does not contain the v3 coexistence guard, schema-v2 support contract, PS 7.6.5 floor, .NET 10.0.11 floor, or SpotX Defender pin policy; published Desktop/CLI binaries therefore lack them, while CLAUDE.md claims they shipped in preview.25.
  Evidence: `git describe` = `v4.0.0-preview.25-6-g5d60ab7`; CHANGELOG `[Unreleased]`; HEAD `5d60ab7`; GitHub release `v4.0.0-preview.25` published 2026-08-20.
  Touches: `Build-Scripts.ps1 -GenerateReleaseManifest` / `-ReleaseTruth`, CHANGELOG (move Unreleased → preview.26), CLAUDE.md Current State, README version badge, `MainViewModel.ShellDisplayVersion`, csproj versions, `schemas/parity-manifest.json` generatorVersion
  Acceptance: A tagged `v4.0.0-preview.26` (or equivalent) GitHub prerelease contains the Unreleased security/coexistence commits; CLAUDE.md/README describe that tag, not preview.25, as the source of those guards; `-ReleaseTruth` passes; no doc claims unpublished `main` work is in preview.25.
  Complexity: M

### P1

### P2

### P3

  Complexity: L
