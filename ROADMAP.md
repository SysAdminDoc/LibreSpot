# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Research-Driven Additions

IDs continue the RD-nn scheme. Highest ID in this file: RD-72. RD-47–RD-50 are absent because they already landed on `main` (CHANGELOG `[Unreleased]`); do not re-file them.

Existing items RD-51–RD-57 and RD-62 below are the same work items as the prior pass, with evidence corrections in-place (especially RD-54). New items start at RD-63.

### P0

- [ ] P0 — RD-63: Publish preview.26 artifacts that include the six post-tag commits
  Why: Tag `v4.0.0-preview.25` does not contain the v3 coexistence guard, schema-v2 support contract, PS 7.6.5 floor, .NET 10.0.11 floor, or SpotX Defender pin policy; published Desktop/CLI binaries therefore lack them, while CLAUDE.md claims they shipped in preview.25.
  Evidence: `git describe` = `v4.0.0-preview.25-6-g5d60ab7`; CHANGELOG `[Unreleased]`; HEAD `5d60ab7`; GitHub release `v4.0.0-preview.25` published 2026-08-20.
  Touches: `Build-Scripts.ps1 -GenerateReleaseManifest` / `-ReleaseTruth`, CHANGELOG (move Unreleased → preview.26), CLAUDE.md Current State, README version badge, `MainViewModel.ShellDisplayVersion`, csproj versions, `schemas/parity-manifest.json` generatorVersion
  Acceptance: A tagged `v4.0.0-preview.26` (or equivalent) GitHub prerelease contains the Unreleased security/coexistence commits; CLAUDE.md/README describe that tag, not preview.25, as the source of those guards; `-ReleaseTruth` passes; no doc claims unpublished `main` work is in preview.25.
  Complexity: M

### P1

### P2

- [ ] P2 — RD-68: Gate remaining WPF smoke states in the QA matrix
  Why: `prompt-destructive`, `activity-running`, and reduced-motion-only already have smoke seeds but are not in `WpfQaMatrixTests.SurfaceMatrix`, so those contracts can regress silently.
  Evidence: `MainViewModel.cs` smoke seeds; `WpfQaMatrixTests.cs` surface list; `ThemeManager.ShouldSuppressMotion`.
  Touches: `WpfQaMatrixTests.cs`, `tools/Invoke-WpfQaMatrix.ps1`, possibly `MainViewModel` smoke IDs
  Acceptance: Full matrix run includes destructive prompt, running activity (cancel band), and a non-HC reduced-motion capture; unnamed-control and focus assertions apply.
  Complexity: S

- [ ] P2 — RD-69: Derive `ShellDisplayVersion` from the assembly, not a literal
  Why: `MainViewModel.ShellDisplayVersion => "v4.0.0-preview.25"` will drift on every bump (already wrong relative to untagged `main`).
  Evidence: `MainViewModel.cs`; csproj `Version` `4.0.0-preview.25`; `LibreSpot.Cli --version`.
  Touches: `MainViewModel.cs`, any test asserting the chrome version, localization if the `v` prefix is a format string
  Acceptance: Chrome version matches `AssemblyInformationalVersion` (or the same source CLI uses); bumping csproj Version updates the shell without a second edit; test locked to that.
  Complexity: S

- [ ] P2 — RD-70: Repair stale distribution and blocked-list claims
  Why: `distribution-matrix.json` still says mutating CLI verbs “need backend wiring” while the fleet contract marks them implemented; Roadmap_Blocked still has “write shell-integration design” after protocol/jump list/tray shipped.
  Evidence: `schemas/distribution-matrix.json`; `schemas/fleet-cli-contract.json` `implementationStatus: implemented`; README Windows shell integration section; `Roadmap_Blocked.md` shell-integration item.
  Touches: `distribution-matrix.json`, `Roadmap_Blocked.md` (move or rewrite the shell-integration row), any tests asserting matrix notes
  Acceptance: Distribution matrix matches implemented fleet verbs; blocked list no longer asks for a design of features that already ship; tests/docs agree.
  Complexity: S

- [ ] P2 — RD-71: Give Recommended workspace a first-run narrative
  Why: Recommended is only an environment/dependency table; the CTA lives in the shell run band, so first-run has no duration, risk, or checklist copy in the page the user is looking at.
  Evidence: `Views/recommended-workspace-view.xaml`; BetterDiscord/Vencord installer wizards; WPF QA `recommended` surface.
  Touches: `recommended-workspace-view.xaml`, `Strings*.resx`, maybe a small VM projection, WPF QA
  Acceptance: Recommended shows a short localized checklist (what will be installed, that updates will be blocked, that the action is reversible via Full Reset) plus the existing env rows; empty/loading still covered; UIA names present.
  Complexity: M

- [ ] P2 — RD-72: Publish an honest capability matrix (what LibreSpot does not unlock)
  Why: adblockify documents that it will not unlock lyrics, downloads, Very High, or Jams; LibreSpot users (and fake-installer victims) need the same boundary next to Premium-account skip-ads.
  Evidence: rxri/spicetify-extensions adblock README; Spotify Premium paywall (offline, lossless, unlimited skips on mobile); README “Premium account (skip ad-blocking)” FAQ.
  Touches: README Features or Trust section, SECURITY.md, optionally a Custom-mode disclaimer string
  Acceptance: Docs state explicitly that LibreSpot does not unlock Premium-only catalog features (downloads, lossless, mobile on-demand); Premium skip-ads remains the supported path; no new product surface that implies otherwise.
  Complexity: S

### P3

- [ ] P3 — RD-62: Publish the curated community-asset catalog as a GitHub Pages site
  Why: No canonical awesome-spicetify exists; LibreSpot's reviewed, hash-pinned catalog already fills that niche, and a browsable page with verified badges captures discovery traffic and counters fake-installer search.
  Evidence: awesome-list survey 2026-08-20 (gap confirmed); `schemas/community-assets.json` + catalog-refresh-checklist review model; r2modman web-catalog pattern.
  Touches: a static generator reading `schemas/community-assets.json` + `theme-preview-manifest.json`, `gh-pages` branch (branch-based Pages, built locally, no Actions), README link
  Acceptance: A Pages site lists every catalog asset with provenance, license, verification badge, and review date, generated from the schemas so it cannot drift; deployed via gh-pages branch push.
  Complexity: L
