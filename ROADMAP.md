# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Research-Driven Additions

Added by the 2026-08-20 research pass (RESEARCH.md). IDs continue the RD-nn scheme (highest prior: RD-44).

### P0

- [ ] P0 — RD-46: Spicetify v3 coexistence guard (detect and refuse before damage)
  Why: Spicetify v3 beta renames `xpui.spa` → `xpui.spa.backup` in place and by upstream's own notice bricks a v2-patched client; users self-installing the beta over a LibreSpot-managed install hit undefined behavior.
  Evidence: spicetify/cli `src/cmd/v3notice.go`, v3-beta releases 2026-08-10..19; existing generic v3 guard (2026-07) predates the beta's concrete artifacts.
  Touches: `src/powershell/shared/` detection functions, `LibreSpot.ps1`, `Backend/LibreSpot.Backend.ps1`, `src/LibreSpot.Core/EnvironmentSnapshotService.cs`, fixtures in tests
  Acceptance: With a fixture layout containing `xpui.spa.backup` or a v3 binary/module directory, install/apply/repair refuse with a specific localized message naming the v3 conflict and the safe path (spicetify restore); health model reports the state; all three detection sites agree.
  Complexity: M

### P1

- [ ] P1 — RD-47: PowerShell 7 security floor ≥ 7.6.5 preflight
  Why: CVE-2026-50523 (command injection, CVSS 7.8) plus four more 2026-08-11 CVEs affect pwsh 7.6.0–7.6.4; the 5.1 lane already has this pattern for CVE-2025-54100.
  Evidence: NVD CVE-2026-50523; Rapid7 August 2026 Patch Tuesday roundup; SECURITY.md host-advisories section.
  Touches: shared preflight functions, SECURITY.md, both PS hosts, Pester tests
  Acceptance: Running under pwsh 7.6.0–7.6.4 produces a non-blocking warning naming the CVE and the fixed version, mirroring the 5.1 preflight; 7.6.5+ is silent.
  Complexity: S

- [ ] P1 — RD-48: Bump dotnetRuntimeFloor to 10.0.11
  Why: The 2026-08-11 .NET servicing fixes 10 CVEs including two RCEs; the floor still says 10.0.10.
  Evidence: Microsoft .NET 10.0.11 servicing notes 2026-08-11; `schemas/dependency-health-allowlist.json`.
  Touches: `schemas/dependency-health-allowlist.json` (floor + rationale), dependency-health tests
  Acceptance: `-DependencyHealth` reports floor 10.0.11 with the August 2026 CVE rationale; tests updated.
  Complexity: S

- [ ] P1 — RD-49: Encode SpotX Defender-exclusion default and `-defender_exclusions_off` into pin-advance policy
  Why: The pin froze at pre-Defender `550bc72c` because upstream began adding Defender exclusions by default (2026-07-11); upstream now ships an opt-out flag, restoring a safe advance path — but the policy and regression tests predate the flag.
  Evidence: SpotX commit `afb4c3fc` (2026-07-11), run.ps1 `-defender_exclusions_off` (verified 2026-08-20), SpotX supports Spotify 1.2.97 (commit `f4cf592b` 2026-08-16); `SpotXDefenderPolicyRegressionTests.cs`.
  Touches: `-SpotXSecurityPolicy` in Build-Scripts.ps1, pin-advance guardrail logic, `SpotXDefenderPolicyRegressionTests.cs`, SECURITY.md pin-conservatism rationale
  Acceptance: Policy check accepts a candidate SpotX commit that honors `-defender_exclusions_off`, requires the flag in any invocation of a post-`afb4c3fc` pin, and fails if exclusions would be written; docs state the updated rationale. (The actual pin advance stays gated on Spicetify's 1.2.93 cap — see RESEARCH.md open question.)
  Complexity: M

- [ ] P1 — RD-50: Spicetify v3 compatibility-contract fixtures (readies blocked RD-41)
  Why: The machine-readable refusal contract RD-41 waited for now exists in v3-beta: `supported-versions.json` schema v2, exit-1 hard refuse, documented fail-open rules, `spicetify support` command.
  Evidence: raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json; docs/supported-versions.md; `src/cmd/version_gate.go`, `src/cmd/support.go` (verified 2026-08-20).
  Touches: `schemas/compatibility-baseline.json` (v3 contract shape), new fixture files, `CompatibilityBaselineTests.cs`/`SpicetifyVersionSupportTests.cs`, shared PS detection behind feature-detection (no behavior change while pinned to v2.44.0)
  Acceptance: Fixture-backed tests parse a schema-v2 supported-versions document, classify allowlisted/degraded/refused versions matching upstream semantics including fail-open cases, and the runtime path activates only when a v3 CLI is detected; pinned v2.44.0 behavior unchanged.
  Complexity: M

- [ ] P1 — RD-51: Enable GitHub Immutable Releases and document Sigstore verification
  Why: GA since 2025-10-28, it locks release assets/tags and attaches Sigstore attestations to locally built, manually uploaded binaries — no GitHub Actions — materially strengthening unsigned-by-design.
  Evidence: GitHub changelog 2025-10-28; docs on `gh release verify-asset`; `schemas/release-artifact-contract.json` verification states.
  Touches: repo settings (immutable releases), README verification section, SECURITY.md, `schemas/release-artifact-contract.json`, `ReleaseTruthTests.cs`
  Acceptance: Repo has immutable releases enabled; README/SECURITY document `gh release verify-asset` alongside SHA256; the release contract records the attestation verification state; first release published under it (pairs with RD-45).
  Complexity: S

- [ ] P1 — RD-52: Purge dead release-infrastructure references and document the local release procedure
  Why: README, `.gitignore:16-18`, CHANGELOG, and Roadmap_Blocked.md cite `.github/workflows/release.yml`, `scorecard.yml`, and a `packaging/` directory — none exist — so the actual (local) release procedure is undocumented and the docs lie about provenance.
  Evidence: repo scan 2026-08-20 (only workflow is `ci.yml`); README:397 and README `packaging\Invoke-ValidationSamples.ps1` reference.
  Touches: README.md, .gitignore comments, SECURITY.md if it echoes the claim, a documented step list for the local release pass (Build-Scripts.ps1 switches)
  Acceptance: No tracked file references release.yml/scorecard.yml/packaging/; README describes the real local build+release procedure; `ReleaseWorkflowTests`/`ReleaseTruthTests` pass against the corrected story.
  Complexity: M

- [ ] P1 — RD-53: Remove Dependabot residue
  Why: 13 stale `dependabot/*` branches sit on origin with no open PRs and no `.github/dependabot.yml`; operator policy is no Dependabot, alerts disabled.
  Evidence: `git ls-remote` branch list 2026-08-20; operator global policy.
  Touches: origin branches (delete), repo settings via `gh api repos/SysAdminDoc/LibreSpot/vulnerability-alerts -X DELETE`, `.github/release.yml` dependabot exclusion line (optional cleanup)
  Acceptance: Zero dependabot branches on origin; vulnerability alerts/security updates disabled; no dependabot config anywhere.
  Complexity: S

### P2

- [ ] P2 — RD-54: Test-stack upgrade as a unit (xunit.v3 4.0.0, FsCheck.Xunit.v3 3.4.0, Test.Sdk 18.9.0)
  Why: A coherent set released 2026-08-14/20; xunit.v3 4.0 bundles MTP 2.3.3 and changes parallelization APIs; FsCheck 3.4.0 exists specifically for xunit.v3 4.x.
  Evidence: xunit.net/releases/v3/4.0.0; fscheck releases 2026-08-20; NuGet queries 2026-08-20.
  Touches: `tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj`, any ParallelMode/config adjustments, `packages.lock.json` regeneration
  Acceptance: Full non-WPF suite (883) plus property tests green on the new stack locally; CI gate green.
  Complexity: M

- [ ] P2 — RD-55: Stryker.NET pilot with the preview MTP runner (un-parks blocked mutation-testing item)
  Why: Stryker 4.13+ ships `--test-runner mtp` for xunit.v3 — the exact blocker recorded in Roadmap_Blocked; a bounded pilot answers whether the item can leave the blocked list.
  Evidence: stryker-mutator.io MTP runner announcement (2026-03-13); stryker-net#3117/#3094 open (preview quality); Roadmap_Blocked Stryker entry with 3-step recipe.
  Touches: Stryker config, possibly a Core-only test target per the blocked-item recipe; run before RD-54 (pilot on xunit.v3 3.2.2 first — the 6-day-old 4.0 interop is unproven)
  Acceptance: A documented pilot run against LibreSpot.Core reporting a real mutation score (not the 0-killed failure mode); result recorded and the Roadmap_Blocked entry updated to reflect the new tooling state.
  Complexity: M

- [ ] P2 — RD-56: PSScriptAnalyzer 1.25.0 upgrade and new-rule triage
  Why: First PSSA release in a year (2026-03-20) adds four rules relevant to a 10 k-line script plus formatter changes; the lint gate should run current rules.
  Evidence: PSScriptAnalyzer 1.25.0 release notes; `.psscriptanalyzerrc.psd1`.
  Touches: lint config, both PS hosts and shared functions for any new findings, Build-Scripts.ps1 `-Lint`
  Acceptance: `-Lint` runs clean on 1.25.0 with any suppressions justified in the config.
  Complexity: M

- [ ] P2 — RD-57: Marketplace IndexedDB persistence-model refresh (docs, health, data inventory)
  Why: Marketplace v1.0.9 (already the pin) moved state localStorage → IndexedDB and closed the #1201 reset-complaint class; LibreSpot's recovery docs and health model still describe the localStorage-era non-portable boundary and miss the built-in export/import.
  Evidence: spicetify/marketplace PR #1181 (merged 2026-07-04), issue #1201 closed 2026-08-03.
  Touches: README recovery boundary text, `schemas/data-inventory.json` (IndexedDB location, detection-only), Marketplace health/guidance strings, backend recovery messaging
  Acceptance: Docs and health output describe IndexedDB persistence and point users to Marketplace's own export/import; data inventory lists the storage location as detected-not-backed-up; file-level backup remains explicitly out pending live validation.
  Complexity: M

- [ ] P2 — RD-58: Surface a user-facing compatibility verdict matrix
  Why: The #1 ecosystem failure mode is version drift (Spotify 1.2.96/97 shipping vs pinned Spicetify cap 1.2.93); LibreSpot has the data (`-CheckSpotifyVersionDrift`, compatibility-baseline) but no single UI surface rendering supported/degraded/unsupported with guidance — the ReVanced compatibility-matrix pattern.
  Evidence: ReVanced Manager patch-compatibility UX; spicetify/cli breakage-issue pattern; r/spicetify update-treadmill threads 2026-08.
  Touches: `MainViewModel`/maintenance workspace, `EnvironmentSnapshotService`, localization resources, WPF QA matrix captures
  Acceptance: Maintenance surface shows detected Spotify/SpotX/Spicetify/Marketplace versions against the pinned tuple with an explicit verdict and next-step guidance per state; localized; screenshot gate updated.
  Complexity: M

- [ ] P2 — RD-59: Structural debt round 2 — MainViewModel and custom workspace view
  Why: `MainViewModel.cs` regrew to 4,871 lines (larger than before the 2026-07-08 extraction); `Views/custom-workspace-view.xaml` is 87 KB — bigger than the remaining MainWindow — and all three view files use kebab-case names against PascalCase codebehind, defying WPF pairing conventions.
  Evidence: line counts 2026-08-20; `src/LibreSpot.Desktop/Views/` listing; RD-36 extraction history.
  Touches: `ViewModels/MainViewModel.cs` (extract maintenance/custom-install satellites), `Views/*` (rename to PascalCase, split custom workspace by section), `WorkspaceViewCompositionTests.cs`, csproj DependentUpon wiring
  Acceptance: MainViewModel under ~3,000 lines with behavior-preserving extraction; view files PascalCase-paired and nested; custom workspace split into per-section UserControls; UIA/localization/focus contracts and full suite green.
  Complexity: L

- [ ] P2 — RD-60: "Spotting fakes" section in README and SECURITY
  Why: A coordinated fake-star wave (repos created 2026-08-06 impersonating SpotX-style tools) now outranks legitimate projects in GitHub search; LibreSpot's provenance story is the direct counter and the section will rank for the queries victims make.
  Evidence: SecretBarber/spotify-adblock-studio (267 fraudulent stars) and lockstep-created siblings, verified 2026-08-20.
  Touches: README.md trust section, SECURITY.md
  Acceptance: A short section lists concrete authenticity checks (official repo path, pinned hashes, no Telegram/rehosted builds, release attestations) without naming-and-shaming specific repos; human-voice rules applied.
  Complexity: S

- [ ] P2 — RD-61: Doc-truth alignment for tracked markdown and stale metadata
  Why: `.gitignore:28`'s "Markdown local-only" comment and AGENTS.md's "README is the ONLY tracked .md" claim contradict the tracked RESEARCH.md/ROADMAP.md/Roadmap_Blocked.md; `schemas/parity-manifest.json` generatorVersion is 16 previews stale; repo CLAUDE.md "Current State" says preview.17; stray near-duplicate `Roadmap_Blocks.md` shadows `Roadmap_Blocked.md`.
  Evidence: `git ls-files` vs `.gitignore` analysis 2026-08-20; parity-manifest.json:4; CLAUDE.md:17.
  Touches: .gitignore comment, AGENTS.md, CLAUDE.md current-state line, parity manifest regeneration via `ParityManifestTests`, delete/merge `Roadmap_Blocks.md`
  Acceptance: Stated tracking policy matches `git ls-files` reality; parity manifest regenerated at the current version; one blocked-roadmap file remains; CLAUDE.md current-state matches the shipped version.
  Complexity: S

### P3

- [ ] P3 — RD-62: Publish the curated community-asset catalog as a GitHub Pages site
  Why: No canonical awesome-spicetify exists; LibreSpot's reviewed, hash-pinned catalog already fills that niche, and a browsable page with verified badges captures discovery traffic and counters the fake-repo wave.
  Evidence: awesome-list survey 2026-08-20 (gap confirmed); `schemas/community-assets.json` + catalog-refresh-checklist review model; r2modman web-catalog pattern.
  Touches: a static generator reading `schemas/community-assets.json` + `theme-preview-manifest.json`, `gh-pages` branch (branch-based Pages, built locally, no Actions), README link
  Acceptance: A Pages site lists every catalog asset with provenance, license, verification badge, and review date, generated from the schemas so it cannot drift; deployed via gh-pages branch push.
  Complexity: L
