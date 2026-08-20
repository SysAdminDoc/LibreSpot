# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Research-Driven Additions

IDs continue the RD-nn scheme. RD-47 through RD-50 shipped on `main` after tag `v4.0.0-preview.25` (see CHANGELOG `[Unreleased]`) and were removed from this file on 2026-08-20. Highest remaining prior ID before this pass: RD-62. New items start at RD-63.

Notes on surviving 2026-08-20 morning items (do not duplicate):

- RD-51 — still valid. Immutable Releases remain the unsigned-by-design strengthening path.
- RD-52 — still valid. `packaging/` is still absent; `.gitignore` still cites `release.yml`.
- RD-53 — still valid. 8 `dependabot/*` heads remain on origin (was 13 in the morning pass).
- RD-54 — **modified:** FsCheck.Xunit.v3 3.4.0 does not exist (NuGet latest 3.3.4, 2026-07-25). Do not upgrade xunit.v3 4.0.0 as a unit with a nonexistent FsCheck adapter. Test.Sdk 18.9.0 is a safe solo bump.
- RD-55 — still valid. Addendum: coverlet.collector 10.0.1 is incompatible with MTP; a Core-only Stryker pilot must not assume coverage.
- RD-56 — still valid. Addendum: triage new `PSUseConstrainedLanguageMode` (off by default in 1.25.0).
- RD-57 — still valid. Addendum: Marketplace PR #1212 (persist-before-reload race) is still open as of 2026-08-12.
- RD-58 — still valid. Stronger evidence: SpotX-Bash prints supported version in the banner; ReVanced computes compatibility from selected patches; Wikipedia/SpotX Windows is 1.2.96.518 / SpotX HEAD 1.2.97 vs pin 1.2.93.
- RD-59 — still valid. Line counts unchanged (MainViewModel 4,871; custom-workspace-view ~87 KB).
- RD-60 — **modified evidence:** use the Jun 2026 ClickFix/Vidar “free Premium” PowerShell campaign, not unverified named GitHub stubs. Do not name-and-shame specific repos.
- RD-61 — still valid. Addendum: CLAUDE.md Current State now also falsely attributes Unreleased floors/guards to preview.25 (pairs with RD-63).
- RD-62 — still valid.

### P0

- [ ] P0 — RD-63: Publish preview.26 artifacts that include the six post-tag commits
  Why: Tag `v4.0.0-preview.25` does not contain the v3 coexistence guard, schema-v2 support contract, PS 7.6.5 floor, .NET 10.0.11 floor, or SpotX Defender pin policy; published Desktop/CLI binaries therefore lack them, while CLAUDE.md claims they shipped in preview.25.
  Evidence: `git describe` = `v4.0.0-preview.25-6-g5d60ab7`; CHANGELOG `[Unreleased]`; HEAD `5d60ab7`; GitHub release `v4.0.0-preview.25` published 2026-08-20.
  Touches: `Build-Scripts.ps1 -GenerateReleaseManifest` / `-ReleaseTruth`, CHANGELOG (move Unreleased → preview.26), CLAUDE.md Current State, README version badge, `MainViewModel.ShellDisplayVersion`, csproj versions, `schemas/parity-manifest.json` generatorVersion
  Acceptance: A tagged `v4.0.0-preview.26` (or equivalent) GitHub prerelease contains the Unreleased security/coexistence commits; CLAUDE.md/README describe that tag, not preview.25, as the source of those guards; `-ReleaseTruth` passes; no doc claims unpublished `main` work is in preview.25.
  Complexity: M

### P1

- [ ] P1 — RD-51: Enable GitHub Immutable Releases and document Sigstore verification
  Why: GA since 2025-10-28, it locks release assets/tags and attaches Sigstore attestations to locally built, manually uploaded binaries — no GitHub Actions — materially strengthening unsigned-by-design.
  Evidence: GitHub changelog 2025-10-28; docs on `gh release verify-asset`; `schemas/release-artifact-contract.json` verification states.
  Touches: repo settings (immutable releases), README verification section, SECURITY.md, `schemas/release-artifact-contract.json`, `ReleaseTruthTests.cs`
  Acceptance: Repo has immutable releases enabled; README/SECURITY document `gh release verify-asset` alongside SHA256; the release contract records the attestation verification state; first release published under it (pairs with RD-63).
  Complexity: S

- [ ] P1 — RD-52: Purge dead release-infrastructure references and document the local release procedure
  Why: README, `.gitignore`, CHANGELOG, and Roadmap_Blocked.md cite `.github/workflows/release.yml`, `scorecard.yml`, and a `packaging/` directory — none exist — so the actual (local) release procedure is undocumented and the docs lie about provenance.
  Evidence: repo scan 2026-08-20 (only workflow is `ci.yml`); README `packaging\Invoke-ValidationSamples.ps1` reference; `.gitignore` `release.yml` comment.
  Touches: README.md, .gitignore comments, SECURITY.md if it echoes the claim, a documented step list for the local release pass (`Build-Scripts.ps1` switches)
  Acceptance: No tracked file references release.yml/scorecard.yml/packaging/; README describes the real local build+release procedure; `ReleaseWorkflowTests`/`ReleaseTruthTests` pass against the corrected story.
  Complexity: M

- [ ] P1 — RD-53: Remove Dependabot residue
  Why: Stale `dependabot/*` branches sit on origin with no open PRs and no `.github/dependabot.yml`; operator policy is no Dependabot, alerts disabled.
  Evidence: `git ls-remote` 2026-08-20 — 8 live heads (`attest-sbom-4.1.0`, `checkout-6.0.3`, `download-artifact-8.0.1`, `Serilog.Sinks.File-7.0.0`, `Test.Sdk-18.6.0`, `coverlet.collector-10.0.1`, `test-dependencies-a8b09c1528`, `xunit.runner.visualstudio-3.1.5`).
  Touches: origin branches (delete), repo settings via `gh api repos/SysAdminDoc/LibreSpot/vulnerability-alerts -X DELETE`, `.github/release.yml` dependabot exclusion line (optional cleanup)
  Acceptance: Zero dependabot branches on origin; vulnerability alerts/security updates disabled; no dependabot config anywhere.
  Complexity: S

- [ ] P1 — RD-58: Surface a user-facing compatibility verdict matrix
  Why: The #1 ecosystem failure mode is version drift (Spotify 1.2.96.518 / SpotX HEAD 1.2.97 vs pinned Spicetify 1.2.93 vs v3-beta allowlist 1.2.70–1.2.94); LibreSpot has the data but no single UI surface rendering supported/degraded/unsupported with next-step guidance.
  Evidence: SpotX-Bash banner pattern; ReVanced Manager patch-compatibility UX; `schemas/compatibility-baseline.json`; `Build-Scripts.ps1 -CheckSpotifyVersionDrift`; Wikipedia template 2026-08-13; SpotX commit `f4cf592` 2026-08-16.
  Touches: `MainViewModel`/maintenance workspace, `EnvironmentSnapshotService`, localization resources, WPF QA matrix captures
  Acceptance: Maintenance surface shows detected Spotify/SpotX/Spicetify/Marketplace versions against the pinned tuple with an explicit verdict and next-step guidance per state; localized; screenshot gate updated.
  Complexity: M

- [ ] P1 — RD-64: Unify workspace taxonomy and localize Jump List / protocol strings
  Why: Visible nav is Home/Setup/Unblock while heroes, tabs, and Jump List say Recommended/Custom/Maintenance; Jump List and `librespot://` registry copy are hardcoded English, so a localized UI plus an English taskbar is a trust/a11y defect.
  Evidence: `MainWindow.xaml` nav bindings; `Strings.resx` `NavHome`/`NavSetup`/`NavUnblock`; `ShellIntegrationService.cs` `BuildJumpTaskDefinitions` / `BuildProtocolRegistryValues`; WPF QA unnamed-control gate does not cover Jump List.
  Touches: `Strings*.resx`, `MainWindow.xaml`, `ShellIntegrationService.cs`, Jump List registration tests, `Invoke-WpfQaMatrix.ps1` if new AutomationIds
  Acceptance: One vocabulary (Recommended / Custom / Maintenance) across rail, heroes, Jump List, and protocol; Jump List/protocol strings come from `Strings` for the persisted UI culture; satellites pass `-Validate`; no leftover Home/Setup/Unblock user-facing labels.
  Complexity: M

- [ ] P1 — RD-65: Make `LibreSpot.Cli --help` match `fleet-cli-contract.json`
  Why: Fleet operators copy `--help`; it currently omits flags the parser already accepts (`--profile`, `--scope`, `--purge`, `--quiet`, `--correlation-id`, `--no-restart`), so answer-file installs fail for reasons the usage text cannot explain.
  Evidence: `src/LibreSpot.Cli/Program.cs` `WriteUsage` vs `ValueFlags` and `schemas/fleet-cli-contract.json` verb flag lists; README fleet examples already document several omitted flags.
  Touches: `Program.cs` `WriteUsage`, `CliApplicationTests`, `FleetSchemaTests`, README if usage snippets are generated
  Acceptance: `--help` lists every contract flag per verb; a test fails if `WriteUsage` drifts from `fleet-cli-contract.json`; `--purge`/`--yes` uninstall requirements are explicit.
  Complexity: S

- [ ] P1 — RD-66: Fail closed when a v3 CLI is detected but `supported-versions.json` is missing
  Why: Current contract copies upstream fail-open so a package-manager v3 without the file still patches; LibreSpot already pins hashes and otherwise refuses unknown Spotify — fail-open is the remaining way a v3 CLI can mutate an unsupported client. This contradicts pin-and-refuse philosophy (flagged in RESEARCH.md).
  Evidence: `SpicetifySupportContract.cs` fail-open messages; `Get-SpicetifyV3SupportContract.ps1`; SECURITY.md “missing list fails open”; upstream `supported-versions.md`; ReVanced fail-closed-unless-forced pattern.
  Touches: `SpicetifySupportContract.cs`, `Get-SpicetifyV3SupportContract.ps1`, both PS hosts via `-ComposeHosts`, Pester + `SpicetifyV3ConflictDetectorTests` / support-contract tests, SECURITY.md, README v3 paragraph
  Acceptance: With a v3 CLI present and no/malformed allowlist, install/reapply/repair refuse with a restore path; v2.44.0 path unchanged; `--force`/documented override remains explicit if one exists; tests cover missing, malformed, and allowlisted documents.
  Complexity: M

### P2

- [ ] P2 — RD-54: Bump Microsoft.NET.Test.Sdk to 18.9.0; park xunit.v3 4.0.0 until FsCheck ships a 4.x adapter
  Why: Test.Sdk 18.9.0 (2026-08-14) is a coherent, non-breaking bump. xunit.v3 4.0.0 is a breaking MTP-v2 jump and FsCheck.Xunit.v3 latest is 3.3.4 (2026-07-25) targeting extensibility.core ≥ 3.0.1, not 4.x.
  Evidence: xunit.net/releases/v3/4.0.0; nuget.org/packages/FsCheck.Xunit.v3 (no 3.4.0); `tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj` currently xunit.v3 3.2.2 / FsCheck 3.3.3 / Test.Sdk 18.8.1.
  Touches: test csproj, `packages.lock.json` if regenerated, property-test discovery
  Acceptance: Non-WPF suite green on Test.Sdk 18.9.0 and FsCheck.Xunit.v3 3.3.4; a comment or allowlist note records why xunit 4.0.0 is parked; CI gate green.
  Complexity: S

- [ ] P2 — RD-55: Stryker.NET pilot with the preview MTP runner (un-parks blocked mutation-testing item)
  Why: Stryker 4.13+ ships `--test-runner mtp` for xunit.v3 — the exact blocker recorded in Roadmap_Blocked; a bounded Core-only pilot answers whether the item can leave the blocked list. coverlet.collector 10.0.1 cannot provide MTP coverage.
  Evidence: stryker-mutator.io MTP runner announcement (2026-03-13); coverlet v10.0.1 notes; Roadmap_Blocked Stryker entry.
  Touches: Stryker config, possibly a Core-only test target per the blocked-item recipe; run on current xunit.v3 3.2.2 (not 4.0.0)
  Acceptance: A documented pilot run against LibreSpot.Core reporting a real mutation score (not the 0-killed failure mode); result recorded and the Roadmap_Blocked entry updated to reflect the new tooling state.
  Complexity: M

- [ ] P2 — RD-56: PSScriptAnalyzer 1.25.0 upgrade and new-rule triage
  Why: First PSSA release in a year (2026-03-20) adds four relevant rules plus `PSUseConstrainedLanguageMode`; the lint gate should run current rules.
  Evidence: PSScriptAnalyzer 1.25.0 release notes; `.psscriptanalyzerrc.psd1`; `-Lint` currently installs PSSA unpinned.
  Touches: lint config, both PS hosts and shared functions for any new findings, `Build-Scripts.ps1 -Lint`
  Acceptance: `-Lint` pins or records 1.25.0 and runs clean with any suppressions justified in the config, including an explicit decision on `PSUseConstrainedLanguageMode`.
  Complexity: M

- [ ] P2 — RD-57: Marketplace IndexedDB persistence-model refresh (docs, health, data inventory)
  Why: Marketplace v1.0.9 moved state localStorage → IndexedDB (PR #1181) and closed #1201; recovery docs still describe the localStorage-era non-portable boundary and miss Marketplace’s own export/import. PR #1212 (persist-before-reload race) is still open, so file-level backup stays out of scope.
  Evidence: spicetify/marketplace PR #1181; issue #1201 closed 2026-08-03; PR #1212 open 2026-08-12; `schemas/data-inventory.json` Marketplace entries.
  Touches: README recovery boundary text, `schemas/data-inventory.json` (IndexedDB location, detection-only), Marketplace health/guidance strings, backend recovery messaging
  Acceptance: Docs and health output describe IndexedDB persistence and point users to Marketplace’s own export/import; data inventory lists the storage location as detected-not-backed-up; file-level backup remains explicitly out pending live validation and #1212.
  Complexity: M

- [ ] P2 — RD-59: Structural debt round 2 — MainViewModel and custom workspace view
  Why: `MainViewModel.cs` regrew to 4,871 lines (larger than before the 2026-07-08 extraction); `Views/custom-workspace-view.xaml` is ~87 KB — bigger than the remaining MainWindow — and all three view files use kebab-case names against PascalCase codebehind.
  Evidence: line counts 2026-08-20; `src/LibreSpot.Desktop/Views/` listing.
  Touches: `ViewModels/MainViewModel.cs` (extract maintenance/custom-install satellites), `Views/*` (rename to PascalCase, split custom workspace by section), `WorkspaceViewCompositionTests.cs`, csproj DependentUpon wiring
  Acceptance: MainViewModel under ~3,000 lines with behavior-preserving extraction; view files PascalCase-paired and nested; custom workspace split into per-section UserControls; UIA/localization/focus contracts and full suite green.
  Complexity: L

- [ ] P2 — RD-60: "How to verify LibreSpot" / fake-installer section in README and SECURITY
  Why: Jun 2026 ClickFix/Vidar campaigns instruct victims to paste PowerShell for “free Spotify Premium” and add Defender exclusions; LibreSpot’s checksum-verified Quick Start is the counter and will rank for the queries victims make.
  Evidence: Malwarebytes 2026-06; HelpNetSecurity Vidar/Reels writeup; README Quick Start vs `irm | iex` advanced path.
  Touches: README.md trust section, SECURITY.md
  Acceptance: A short section lists authenticity checks (official repo path, SHA256 `checksums.txt`, no Telegram/rehosted builds, never paste commands from videos) without naming-and-shaming specific GitHub stubs; human-voice rules applied.
  Complexity: S

- [ ] P2 — RD-61: Doc-truth alignment for tracked markdown and stale metadata
  Why: `.gitignore` “Markdown local-only” and AGENTS.md “README is the ONLY tracked .md” contradict tracked RESEARCH.md/ROADMAP.md/Roadmap_Blocked.md; `parity-manifest.json` generatorVersion is 16 previews stale; CLAUDE.md Current State attributes Unreleased work to preview.25; stray `Roadmap_Blocks.md` shadows `Roadmap_Blocked.md`.
  Evidence: `git ls-files` vs `.gitignore` 2026-08-20; `schemas/parity-manifest.json:4`; CLAUDE.md Current State; untracked `Roadmap_Blocks.md`.
  Touches: .gitignore comment, AGENTS.md, CLAUDE.md current-state line, parity manifest regeneration via `ParityManifestTests`, delete/merge `Roadmap_Blocks.md`
  Acceptance: Stated tracking policy matches `git ls-files` reality; parity manifest regenerated at the current version; one blocked-roadmap file remains; CLAUDE.md current-state matches the latest tagged release (not untagged `main`).
  Complexity: S

- [ ] P2 — RD-67: Stop calling stack presence an “update status”
  Why: `ShellUpdateStatusTitle`/`Detail` flip on “Spotify or Spicetify installed,” not on a LibreSpot release check, so the chrome overclaims (“LibreSpot is up to date”).
  Evidence: `MainViewModel.cs` `ShellUpdateStatusTitle`/`ShellUpdateStatusDetail`; `Strings.resx` `Vm_ShellUpdate*`; Velopack/update channels remain operator-blocked.
  Touches: `MainViewModel.cs`, `Strings*.resx`, `MainWindow.xaml` update card, WPF QA captures
  Acceptance: Copy describes detected stack / maintenance availability, never LibreSpot release freshness, unless a real updater exists; localized; screenshot gate updated if the string is gated.
  Complexity: S

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
