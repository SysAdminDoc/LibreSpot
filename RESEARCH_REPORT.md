# LibreSpot Research Report

Research summary for planning. The full April 2026 research corpus is archived
at [docs/archive/research/RESEARCH.md](docs/archive/research/RESEARCH.md).

Last consolidated: 2026-06-01.
Last researched: 2026-06-04, Cycle 4.

## Executive Summary

LibreSpot is a Windows-first SpotX + Spicetify installer with a mature
PowerShell GUI, a .NET 8 WPF shell preview, verified downloads, config recovery,
auto-reapply, crash logs, release checksums, SBOM output, and GitHub artifact
attestations. The highest-value direction is still trust and reliability rather
than more theme volume: refresh upstream compatibility pins, remove doc/code
version drift, make update status component-specific, harden Marketplace repair,
and prepare package-manager distribution with transparent legal and safety
disclosures.

Top opportunities:

1. Refresh SpotX and Spotify compatibility pins against upstream 1.2.90 work.
2. Single-source dependency pins so code, tests, README, CLAUDE notes, and
   roadmap do not disagree.
3. Split Check Updates into SpotX, Spicetify, Marketplace, and theme
   compatibility states.
4. Add a Marketplace visibility and repair path.
5. Turn distribution into concrete winget, Scoop, and Chocolatey manifest
   drafts with validation commands.
6. Add trust-and-risk documentation before broader distribution.
7. Add dependency-refresh automation for NuGet and release tooling.
8. Convert catalog expansion into a measured inventory process.
9. Add OpenSSF Scorecard reporting to complement SBOM/provenance.
10. Decide whether non-Windows SpotX-Bash support belongs in core, docs, or a
    sibling project.

## Evidence Reviewed

Local repository evidence:

- Top-level docs: `README.md`, `ROADMAP.md`, `RESEARCH_REPORT.md`,
  `COMPLETED.md`, `CHANGELOG.md`, `SIGNPATH.md`, `CLAUDE.md`, `AGENTS.md`.
- Archived research: `docs/archive/research/RESEARCH.md`.
- Main script: `LibreSpot.ps1`.
- WPF shell: `src/LibreSpot.Desktop/**`.
- Tests: `tests/LibreSpot.Desktop.Tests/**`.
- Release workflow: `.github/workflows/release.yml`.
- Git history: latest 100+ commits through `6499c5a docs: consolidate roadmap
  planning`.

Local verification:

- `git pull --rebase`: already up to date on 2026-06-04.
- `dotnet test tests\LibreSpot.Desktop.Tests\LibreSpot.Desktop.Tests.csproj -c Release --nologo`:
  passed 72 tests.
- `dotnet list ... package --vulnerable --include-transitive`: no vulnerable
  packages for the app or test project from NuGet sources.
- `dotnet list ... package --outdated`: Serilog, Serilog.Sinks.File, coverlet,
  Microsoft.NET.Test.Sdk, xUnit, and xUnit runner have newer versions.
- GitHub API metadata for LibreSpot, SpotX, SpotX-Bash, Spicetify CLI,
  Marketplace, spicetify-themes, and direct competitors.

External sources reviewed:

- Official upstream repositories and release pages for SpotX, SpotX-Bash,
  Spicetify CLI, Marketplace, and spicetify-themes.
- GitHub topic searches for `spicetify-extensions`, `spicetify-themes`,
  `spicetify-custom-apps`, `spotx`, `spotify-ad-free`, and
  `spotify-adblocker`.
- Windows distribution docs for winget, Scoop, Chocolatey, Velopack, SignPath,
  GitHub attestations, CycloneDX, and OpenSSF Scorecard.
- Spotify legal/developer terms and user guidelines.
- Recent Reddit/community threads for missing Marketplace, broken Spicetify
  after Spotify updates, and ad-blocking alternatives.

Areas not verified:

- No live Spotify install/reapply flow was executed on this machine.
- No SignPath signing request was submitted because project secrets/operator
  enrollment are required.
- No package-manager manifest was submitted to winget, Scoop, or Chocolatey.
- No UI screenshot audit was run in this research lane.

## Cycle 2 Delta - Release, UI, Packaging, and Identity

Cycle 2 intentionally avoided repeating the Cycle 1 upstream SpotX/Spicetify
pin research. It focused on implementation prerequisites that can block the
next build machine even if feature work is otherwise clear.

### New Evidence Collected

- The repo remains current at `8698492 docs: refresh roadmap research cycle`;
  `git pull --rebase` reported already up to date.
- The only untracked repo file remains `AGENTS.md`; this research cycle did not
  stage or edit it.
- `winget search LibreSpot --source winget` found no matching Windows package
  on 2026-06-04. Chocolatey and Scoop CLIs were not installed locally, so those
  package indexes were not live-verified from this machine.
- WPF project target: `net8.0-windows`; release workflow installs .NET `8.0.x`.
- Microsoft's current .NET support table shows .NET 8 in maintenance with end
  of support on 2026-11-10, while .NET 10 is active LTS through 2028-11-14.
- PowerShell 7.6 is the current LTS line and is built on .NET 10, but LibreSpot
  still needs Windows PowerShell 5.1 coverage because that is the built-in host
  promised in README and explicitly selected for SpotX isolation.
- Release workflow action refs are tag-based (`actions/checkout@v4`,
  `actions/setup-dotnet@v4`, artifact actions v4, attestation actions v2).
  GitHub's security guidance recommends full-length commit SHA pinning.
- Current action release streams have moved: checkout v6.0.3, setup-dotnet
  v5.3.0, upload-artifact v7.0.1, download-artifact v8.0.1, attestation actions
  v4.1.0.
- PS2EXE is pinned at 1.0.15 in CI, while PSGallery reports 1.0.17.
- CycloneDX is pinned at 3.0.8 in CI, while `cyclonedx-dotnet` reports 6.2.0.
- The v4.0 scope mentions Wpf.Ui controls, but the WPF project currently has no
  Wpf.Ui package reference. The active package line is `WPF-UI` 4.3.0; the
  similarly named `Wpf.Ui` NuGet ID is an older 3.4.2.7 line.
- The WPF shell has many automation names, help text bindings, live regions, and
  focus restoration hooks, but no UI Automation or Accessibility Insights test
  lane.
- Velopack is current at 1.2.0 and its docs center update discovery on release
  feed metadata, but LibreSpot has no app identity, installed app layout, update
  channel, or feed design yet.
- CrashReporter writes local logs and crash reports and encourages users to copy
  report paths/open folders; a support-bundle redaction policy does not yet
  exist.

### Cycle 2 Conclusions

- The next release should not treat .NET 8 as a set-and-forget base. The project
  can stay on .NET 8 for now, but it needs a visible support-phase gate before
  November 2026 so v4 stable is not born near an EOL cliff.
- Release supply-chain posture is strong for checksums, SBOM, and attestations,
  but tag-based action refs remain a distinct risk from missing Scorecard
  reporting. SHA pinning and a bot-managed update process are separate
  implementation work.
- Wpf.Ui adoption needs a package-ID decision before code work. The correct
  current package is `WPF-UI`, not the older-looking `Wpf.Ui` ID.
- The WPF accessibility work is promising but under-verified. A UI Automation
  smoke harness would turn manual visual/accessibility audit expectations into
  repeatable evidence.
- Velopack should start with package identity and state migration design, not
  with a quick package command. Otherwise package-manager, signing, shortcuts,
  update feed, and future protocol names can diverge.
- The name collision risk is broader than package availability. `LibreSpot` has
  no current winget hit, but `librespot` is already a well-known Spotify client
  project and crate; distribution work needs canonical package/display/protocol
  naming before first submission.
- Diagnostic export should be designed before support/fleet docs tell users to
  share logs, because local logs and crash reports can include paths and command
  output that should be reviewed or redacted.

### Cycle 2 Added Roadmap Items

- P0 - Add a runtime and build-tool lifecycle gate.
- P1 - Harden GitHub Actions supply-chain pinning separately from Scorecard.
- P1 - De-risk Wpf.Ui adoption with the correct package identity.
- P1 - Add a WPF UI automation and accessibility regression harness.
- P1 - Define the Velopack app identity and update feed before packaging.
- P1 - Add a Windows PowerShell 5.1 and PowerShell 7 compatibility lane.
- P2 - Design a privacy-safe diagnostic export bundle.
- P0/operator - Finalize package identity before any public distribution
  manifest.

## Cycle 3 Delta - Reliability Architecture and Supportability

Cycle 3 inspected implementation surfaces that become risky once LibreSpot adds
fleet deployment, sharing, package updates, and broader diagnostics. The focus
was not new end-user feature volume; it was evidence that future changes can be
tested without touching real user machines.

### New Evidence Collected

- `git pull --rebase` again reported already up to date after Cycle 2.
- `config.json` is normalized in both the PowerShell monolith and WPF code, but
  no explicit schema version or JSON schema file exists.
- WPF `ConfigurationService` preserves corrupt config files and writes atomically,
  while PowerShell has parallel quarantine and normalization behavior.
- `AppCatalog` is the nearest WPF source of truth for options, Spotify version
  entries, themes, extensions, and download methods; PowerShell still carries
  parallel manifests and normalizers.
- Network readiness is a HEAD request to `https://raw.githubusercontent.com`.
  Downloads use `Invoke-WebRequest`, then BITS fallback. No current code path
  exposes proxy-required, GitHub-rate-limited, DNS/TLS, or hash-mismatch states
  as separate user-facing diagnoses.
- GitHub REST documentation describes primary and secondary rate limits and the
  `x-ratelimit-remaining` / `x-ratelimit-reset` headers that LibreSpot could use
  to avoid blind retries.
- Microsoft BITS docs expose proxy usage controls, proxy lists, proxy bypass,
  and proxy credentials; LibreSpot's BITS fallback does not yet surface those
  dimensions to the user.
- Destructive operations use safety helpers and Spicetify apply has rollback,
  but there is no durable per-run operation journal to back future undo or
  fleet dry-run behavior.
- `winget show Spotify.Spotify --source winget` currently reports Spotify
  `1.2.90.451.gb094aab0`, installer type `exe`, URL
  `https://download.scdn.co/SpotifyFullSetupX64.exe`, SHA256
  `7701d124417f9c93b2861f5a4904674ab8d49667dc1587f5468f79c25bffd43e`, and
  offline distribution support.
- LibreSpot reset/uninstall code handles Microsoft Store package
  `SpotifyAB.SpotifyMusic`, AppData paths, Start Menu/Desktop shortcuts,
  registry keys, scheduled tasks, cached installers, and legacy Spotify builds.
- Microsoft Defender docs describe restoring quarantined files through Windows
  Security Protection History or `MpCmdRun`; LibreSpot should detect and guide,
  not disable or bypass AV.
- PSScriptAnalyzer 1.25.0 and Pester 5.7.1 are current PowerShell testing tools
  that could complement the existing .NET regression tests.

### Cycle 3 Conclusions

- Config and preset work should become schema-first before fleet JSON, shared
  profiles, or package migration ship. Without `schemaVersion`, old and future
  configs cannot be reasoned about precisely.
- Network failures need typed diagnostics. "No Internet Connection" is too broad
  for enterprise users behind proxies, users hitting GitHub limits, and users
  whose downloads succeeded but hashes failed.
- The future undo-selected-actions pane and `--dry-run` mode need an operation
  journal before UI work begins. Journaling planned vs actual mutations is the
  lowest-risk foundation.
- Spotify install-source handling deserves fixture tests. Current winget Spotify
  is already newer than LibreSpot's pinned SpotX baseline, and different install
  sources exercise different cleanup and repair paths.
- Antivirus and endpoint-security interference is plausible for patching and
  custom app files. LibreSpot should explain likely causes and manual review
  steps without weakening the user's security posture.
- PowerShell-native analysis and tests would catch script-lane regressions that
  C# text assertions cannot model cleanly.

### Cycle 3 Added Roadmap Items

- P0 - Add a versioned config schema and migration contract.
- P1 - Add network, proxy, and GitHub rate-limit diagnostics.
- P1 - Add an operation journal for destructive actions.
- P1 - Build a Spotify install-source compatibility fixture set.
- P2 - Add Defender quarantine and antivirus-interference diagnostics.
- P2 - Add PowerShell static analysis and Pester coverage for the script lane.

## Cycle 4 Delta - Distribution Readiness, Notices, and Shell Policy

Cycle 4 inspected the support surfaces that become binding once LibreSpot is
installed through package managers, signed updaters, shell integrations, and a
larger public issue queue. This cycle extends the earlier trust, package
identity, and catalog work with concrete gates for third-party assets,
vulnerability intake, OS support, elevation, and Windows shell registration.

### New Evidence Collected

- `git ls-files` shows only `LICENSE` and archived research docs for tracked
  legal/support files; there is no tracked `NOTICE`, `THIRD_PARTY_NOTICES.md`,
  `SECURITY.md`, `COPYING`, or `.github/ISSUE_TEMPLATE/*`.
- `LICENSE` is MIT for LibreSpot itself, but the curated community asset graph
  contains multiple external license postures that are not represented in a
  tracked notices manifest.
- Live HEAD checks on 2026-06-04 returned `404` for every current community
  extension raw URL in both catalogs:
  `hidePodcasts.js`, `beautifulLyrics.js`, `playlistIcons.js`,
  `songStats.js`, and `volumePercentage.js`.
- GitHub repository metadata collected on 2026-06-04 shows community extension
  and theme sources with mixed license states: GPL-3.0
  (`theRealPadster/spicetify-hide-podcasts`), blank/unknown
  (`surfbryce/beautiful-lyrics`), MIT
  (`jeroentvb/spicetify-playlist-icons`, `daksh2k/spicetify-stuff`,
  Catppuccin, Bloom, official `spicetify-themes`), WTFPL
  (`Comfy-Themes/Spicetify`), AGPL-3.0 (`sanoojes/Spicetify-Lucid`), and
  blank/unknown (`Astromations/Hazy`). `Shinyhero36/spicetify-song-stats`
  returns GitHub API `404`.
- REUSE 3.3 defines a machine-readable licensing method for files, license
  files, and SPDX expressions; SPDX License List 3.28.0 provides current
  standardized license identifiers.
- GitHub security policy documentation expects a `SECURITY.md` file with
  supported versions and vulnerability reporting instructions. GitHub issue
  template documentation expects templates under `.github/ISSUE_TEMPLATE` for
  structured public issue intake.
- README requirements still state Windows 10/11, while Microsoft says Windows
  10 Home/Pro reached end of support on 2025-10-14. The WPF pitch also
  describes Windows 11 Mica with Windows 10 fallback, and the app manifest
  carries legacy supportedOS GUIDs plus `requestedExecutionLevel
  level="asInvoker"`.
- The PowerShell script self-elevates through `Verb = 'RunAs'`, and the PS2EXE
  release workflow builds an admin-required executable. The WPF shell is
  `asInvoker` and routes admin-sensitive work through backend commands.
- Microsoft app notification docs state that app notifications are not
  supported for elevated apps. That makes the elevation boundary a design
  dependency for completion toasts and future actionable notifications.
- Microsoft shell docs cover AppUserModelIDs, shell links, file associations,
  and desktop app notification activation. The roadmap lists `librespot://`,
  `.librespot` import association, persistent toasts, jump lists, and taskbar
  affordances, but no current design doc fixes the registration model.

### Cycle 4 Conclusions

- Community extensions cannot remain live install choices until their source
  URLs, commit pins, hashes, licenses, and support states are represented in a
  tracked manifest. Five active `404` URLs mean this is already a release
  blocker, not just future hardening.
- The catalog and package/distribution tracks need a third-party notice gate.
  The repo can remain MIT, but curated/retrieved third-party assets still need
  SPDX/license data and an operator policy for GPL/AGPL/unknown entries.
- Security intake should exist before larger distribution. A project that
  downloads code, runs elevated actions, and modifies a local application needs
  a clear vulnerability reporting path and issue templates that keep sensitive
  logs out of public bug reports.
- Windows support labels need to be separated. Host OS support, best-effort OS
  compatibility, PowerShell runtime compatibility, and Spotify target-version
  support are different dimensions and should not be collapsed into "Windows
  10/11".
- App notifications and elevation are coupled. If the UI runs elevated by
  default, notification support is constrained; if WPF stays `asInvoker`,
  mutating work needs a clean per-action elevation path.
- Shell integration needs a design record before implementation. App identity,
  shortcuts, protocol handlers, file associations, uninstall cleanup, and
  portable-vs-installed behavior all cross package identity and elevation
  decisions.

### Cycle 4 Added Roadmap Items

- P0 - Add a community asset supply manifest and disable broken catalog
  entries.
- P0 - Add a third-party notices and license policy gate.
- P1 - Add `SECURITY.md` and public intake templates before broader
  distribution.
- P1 - Decide the Windows support lifecycle after Windows 10 Home/Pro end of
  support.
- P1 - Define a least-privilege elevation and notification boundary.
- P2 - Write the shell-integration registration design before implementing
  protocol, toasts, jump lists, or file associations.

## Current Product Map

User personas:

- Casual Windows users who want a one-click SpotX + Spicetify setup.
- Power users choosing SpotX flags, themes, lyrics colors, and extensions.
- Maintenance users repairing a broken Spotify/Spicetify state after updates.
- Future admin/fleet users who need silent install, status JSON, and logs.

Core workflows:

- Easy install: clean setup with recommended SpotX, Spicetify CLI,
  Marketplace, and starter extensions.
- Custom install: SpotX flags, Spotify version selection, lyrics themes,
  Spicetify themes/schemes, Marketplace, and extension selection.
- Maintenance: check versions, reapply, restore vanilla, uninstall Spicetify,
  full reset, backup/restore, and auto-reapply watcher management.
- Headless watcher: scheduled task calls `-Watch`, compares Spotify version,
  waits for Spotify to be closed, replays saved config, and logs to AppData.

Key storage and integrations:

- `%APPDATA%\LibreSpot\config.json`, `install.log`, `watcher-state.json`,
  `watcher.log`.
- `%USERPROFILE%\LibreSpot_Backups` for rotating Spicetify backups.
- `%APPDATA%\Spotify` and `%APPDATA%\spicetify` / `%LOCALAPPDATA%\spicetify`.
- Task Scheduler task `LibreSpot\ReapplyWatcher`.
- GitHub releases for raw script, PS2EXE artifact, WPF artifact, checksums,
  SBOM, and attestations.

## Feature Inventory

| Feature | User value | Entry point | Main code | Maturity | Coverage |
|---|---|---|---|---|---|
| Easy install | Fast recommended setup | Easy mode install button | `LibreSpot.ps1`, WPF VM/backend | Complete | 72-test suite covers critical script invariants |
| Custom install | Fine-grained SpotX/Spicetify choices | Custom mode | `Build-SpotXParams`, `AppCatalog` | Complete but pin-sensitive | Config and PowerShell regression tests |
| Maintenance dashboard | Repair and status surface | Maintenance mode | `LibreSpot.ps1:3867`, WPF services | Partial diagnostics | Tests cover snapshot service, not live Spotify states |
| Auto-reapply watcher | Repairs after Spotify updates | Maintenance toggle, CLI flags | `Invoke-HeadlessReapply`, scheduled task XML | Shipped | Regression tests cover task and watcher invariants |
| Version checking | Shows stale pins | Maintenance > Check pinned versions | `Check-ForUpdates` | Useful but too coarse | Script regression coverage |
| Marketplace install | In-app discovery of mods | install pipeline | `Module-InstallMarketplace` | Shipped, needs visibility repair | Marketplace path guard tests |
| Theme/extension catalog | Curated customization | Custom install | `AppCatalog`, script manifests | Shipped, drift-prone | Catalog tests |
| Config persistence | Recoverable user settings | load/save | config services and PS functions | Strong | corrupt config and atomic save tests |
| Release trust | Download integrity | GitHub releases | release workflow | Strong but unsigned pending | CI workflow, checksums, SBOM, attestations |
| WPF shell | Native future UI | `src/LibreSpot.Desktop` | Preview | WPF tests only; manual UX audit still needed |

## Competitive and Ecosystem Research

| Product/source | Current signal | What LibreSpot should learn | Avoid |
|---|---|---|---|
| SpotX | 21k+ stars, active main commits through 2026-05-30, latest release tag still old; main supports newer Spotify work. | Treat main commit pinning as an active compatibility process, not a release-tag process. | Blindly tracking main without hash/test gating. |
| SpotX-Bash | 5k+ stars, active through 2026-06-03, supports macOS/Linux and 1.2.90. | Non-Windows demand exists, but it is a different product architecture. | Mixing Bash platform support into WPF/PowerShell without a decision record. |
| Spicetify CLI | v2.43.2 on 2026-04-20; Windows compatibility listed through Spotify 1.2.88. | Separate Spicetify max-tested client version from SpotX recommended version. | Reporting "all current" when SpotX is newer than Spicetify CSS maps. |
| Marketplace | v1.0.8 release; active main commits and many open issues. | Add visibility/repair diagnostics for missing sidebar/custom app states. | Assuming installed files mean users can find the Marketplace UI. |
| spicetify-themes | Active theme fixes through 2026-05-31. | Refresh theme pins and schemes before catalog releases. | Shipping stale screenshots or theme schemes. |
| BlockTheSpot | Archived 2026-02-14; still large install base. | Position LibreSpot as maintained and recovery-focused. | Copying DLL-injection trust model or ambiguous safety claims. |
| Spicetify EasyInstall | Archived GUI with about 150 stars. | GUI installers are valued but maintenance is the differentiator. | Feature additions without update repair paths. |
| ModifySpotify | Small C# GUI, last pushed 2025-10-20. | A native GUI alone is not enough; release trust and diagnostics matter. | Treating small GUI parity as the target. |
| spicetify-nix | Declarative Linux/Home Manager model. | Presets-as-data and reproducible config are attractive for advanced users. | Pulling Linux/Nix scope into the Windows installer without strategy. |

## Highest-Value New Features

### Component-Specific Compatibility Matrix

- User problem solved: users need to know whether SpotX, Spicetify CLI,
  Marketplace, and themes are each compatible with a Spotify build.
- Evidence: Spicetify v2.43.2 supports Windows through 1.2.88, while SpotX main
  has 1.2.90 work.
- Proposed behavior: Maintenance shows separate statuses and warns on mixed
  states such as "SpotX newer than Spicetify-tested Spotify."
- Implementation areas: `Check-ForUpdates`, `AppCatalog.SpotifyVersionManifest`,
  README compatibility table, tests.
- Verification: mock upstream API data; live Check Updates with and without
  network.
- Complexity: M.
- Priority: P1.

### Marketplace Visibility Repair

- User problem solved: users frequently report Marketplace missing or showing
  incomplete content after Spotify/Spicetify updates.
- Evidence: recent r/spicetify threads and Marketplace open issue volume.
- Proposed behavior: verify CustomApps Marketplace files, offer an open
  `spotify:app:marketplace` action/fallback guidance, and repair when files are
  missing or stale.
- Implementation areas: maintenance diagnostics, Marketplace install/verify,
  WPF action model, FAQ.
- Verification: simulate missing and stale Marketplace states.
- Complexity: M.
- Priority: P1.

### Trust and Risk Page

- User problem solved: users can distinguish file safety from account/terms
  risk before installing.
- Evidence: Spotify user guidelines and developer terms prohibit modifying or
  reverse engineering except where law limits that restriction.
- Proposed behavior: docs and installer copy describe no credential collection,
  no bundled Spotify binaries, direct upstream downloads, hash/provenance
  verification, and stock restore path.
- Implementation areas: README, `docs/trust-and-risk.md`, installer "Is this
  safe?" text.
- Verification: doc review.
- Complexity: S.
- Priority: P1.

### Package-Manager Manifest Kit

- User problem solved: users and admins need install/update channels that do not
  rely on manual GitHub downloads.
- Evidence: winget requires manifests with installer URL and SHA256; Scoop uses
  `checkver`/`autoupdate`; Chocolatey uses nuspec and install scripts.
- Proposed behavior: keep draft package manifests in repo until signing is
  ready, with local validation commands.
- Implementation areas: `publish/`, release workflow, packaging docs.
- Verification: winget validation, Scoop local install/checkver, Chocolatey
  pack/install in a clean temp environment.
- Complexity: L.
- Priority: P1.

## Existing Feature Improvements

### Pin Drift Cleanup

- Current behavior: code pins Spicetify 2.43.2, but docs still reference 2.43.1
  in several places.
- Recommended change: centralize dependency pin display and update all public
  docs in the same commit as any pin change.
- Backward compatibility: no behavior change if docs only are corrected.
- Verification: `rg` over docs/source/tests for stale versions.
- Priority: P0.

### Catalog Refresh Discipline

- Current behavior: curated catalog is useful but manually maintained.
- Recommended change: add a catalog candidate checklist with popularity,
  maintenance, license, install path, breakage risk, and Marketplace overlap.
- Backward compatibility: no removal without migration notes.
- Verification: checklist run against existing community entries and rejected
  candidates.
- Priority: P2.

### Dependency Maintenance

- Current behavior: no vulnerabilities found, but app/test NuGet packages are
  behind latest releases.
- Recommended change: add Dependabot or a monthly dependency-refresh workflow,
  with tests and hold notes for risky jumps.
- Backward compatibility: keep release workflow stable and pin tooling where
  breakage risk is high.
- Verification: `dotnet list package --outdated`; full test suite.
- Priority: P2.

## Reliability, Security, Privacy, and Data Safety

- Verified downloads and hash pins are a current strength.
- Config save/quarantine and rotating backups reduce user-data loss.
- Release workflow already emits checksums, CycloneDX SBOM, and GitHub
  artifact attestations.
- No NuGet vulnerabilities were reported from current sources on 2026-06-04.
- Signing remains pending and should block package-manager expansion.
- Trust docs should distinguish "file integrity" from Spotify account/terms
  risk.
- OpenSSF Scorecard would add continuous supply-chain hygiene evidence.

## UX, Accessibility, and Trust

- Existing WPF preview has automation names/live region work and taskbar
  progress.
- Manual UI audit was not performed in this cycle, so the standing implementer
  instructions explicitly require a full UX/accessibility pass each build pass.
- Missing Marketplace visibility is the strongest current UX friction signal
  from recent community research.
- Distribution trust needs clear install/update channels and signed artifacts.
- Docs should explain the difference between Easy mode, Custom mode, and
  Maintenance in recovery terms rather than only feature terms.

## Architecture and Maintainability

- The PowerShell monolith and WPF backend duplicate important behavior, but
  regression tests now guard several shared invariants.
- `AppCatalog` is the closest source of truth for WPF choices; the PowerShell
  script still has parallel manifest data.
- Release workflow has solid preflight gates, but dependency-refresh and
  package-manifest validation are not yet automated.
- Future fleet CLI work should reuse the config normalization and backend
  action allowlist rather than creating a separate command surface.

## Prioritized Roadmap

### Now

- [ ] P0 - Refresh SpotX and Spotify compatibility pins.
  - Why: upstream SpotX moved past the pinned 1.2.86-era commit.
  - Evidence: SpotX commits from 2026-05-20 through 2026-05-30.
  - Touches: pins, hashes, Spotify version manifest, docs, tests.
  - Acceptance: code, tests, and docs agree on tested pins.
  - Verify: full tests, PowerShell parse checks, Maintenance > Check Updates.

- [ ] P0 - Single-source dependency state across code and docs.
  - Why: Spicetify 2.43.2 is in code/tests, but several docs mention 2.43.1.
  - Evidence: `LibreSpot.ps1:143`, Spicetify v2.43.2 release page.
  - Touches: README, CLAUDE, ROADMAP, RESEARCH_REPORT.
  - Acceptance: no stale pin strings in public docs.
  - Verify: `rg` over source/docs/tests.

### Next

- [ ] P1 - Add component-specific compatibility matrix.
- [ ] P1 - Add Marketplace visibility repair.
- [ ] P1 - Draft and validate winget, Scoop, and Chocolatey packages.
- [ ] P1 - Add trust and legal disclosure page.

### Later

- [ ] P2 - Add NuGet dependency refresh automation.
- [ ] P2 - Add measured catalog refresh process.
- [ ] P2 - Add OpenSSF Scorecard reporting.
- [ ] P3 - Decide non-Windows support scope.

### Rejected or Under Consideration

- Mobile Spotify modding: rejected for this repo because enforcement and DMCA
  pressure are higher and the product is Windows desktop focused.
- Bundling Spotify binaries: rejected because it worsens legal and trust risk.
- Auto-tracking SpotX main without a pin: rejected because tested hashes are a
  core trust feature.
- Adding many themes/extensions without a catalog checklist: rejected because
  catalog drift is already a risk.

## Quick Wins

- Update stale Spicetify 2.43.1 references to 2.43.2 where source already pins
  2.43.2.
- Add README note that Spicetify v2.43.2 supports Windows Spotify through 1.2.88
  while current LibreSpot SpotX pin still targets an older tested baseline.
- Add a short "open Marketplace manually" FAQ entry until a real repair action
  ships.
- Add package validation commands to the roadmap or packaging doc before
  manifest files exist.

## Larger Bets

- A proper fleet CLI with silent install, JSON presets, status JSON, NDJSON logs,
  uninstall, dry-run, and endpoint-management docs.
- A package-manager release lane with signing, Velopack, winget, Scoop, and
  Chocolatey.
- A shared data manifest that generates PowerShell and WPF catalog/pin state.
- A non-Windows sister project or docs-only strategy for SpotX-Bash.

## Open Questions

- Should the app keep the LibreSpot name before package-manager distribution,
  given the existing Rust `librespot` project?
- Does the operator want package-manager work to wait for SignPath approval, or
  should unsigned draft manifests live in-repo first?
- Should non-Windows support be a sister repo, a docs page, or an explicit
  non-goal?

## Appendix - Sources

Local repository:

- `README.md`
- `ROADMAP.md`
- `RESEARCH_REPORT.md`
- `COMPLETED.md`
- `CHANGELOG.md`
- `CLAUDE.md`
- `SIGNPATH.md`
- `docs/archive/research/RESEARCH.md`
- `LibreSpot.ps1`
- `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`
- `src/LibreSpot.Desktop/Models/AppCatalog.cs`
- `src/LibreSpot.Desktop/Services/BackendScriptService.cs`
- `src/LibreSpot.Desktop/Services/ConfigurationService.cs`
- `src/LibreSpot.Desktop/Services/EnvironmentSnapshotService.cs`
- `tests/LibreSpot.Desktop.Tests/*.cs`
- `.github/workflows/release.yml`

Upstream and competitors:

- https://github.com/SysAdminDoc/LibreSpot
- https://github.com/SpotX-Official/SpotX
- https://github.com/SpotX-Official/SpotX/commit/13ef73f820afad845637bc81a56052ce390f615c
- https://github.com/SpotX-Official/SpotX/commit/b53956f71d2ee0ce585e475b8a4d6fa8d814b579
- https://github.com/SpotX-Official/SpotX/commit/95882aa5b308832102ac8a206d300bf6f5436bfb
- https://github.com/SpotX-Official/SpotX-Bash
- https://github.com/SpotX-Official/SpotX-Bash/commit/fa8730d16e7acfb70744be677ac9b7aa3e3eaf3c
- https://github.com/spicetify/cli
- https://github.com/spicetify/cli/releases/tag/v2.43.2
- https://github.com/spicetify/marketplace
- https://github.com/spicetify/spicetify-themes
- https://github.com/mrpond/BlockTheSpot
- https://github.com/ohitstom/spicetify-easyinstall
- https://github.com/Spinchies/ModifySpotify
- https://github.com/librespot-org/librespot
- https://github.com/surfbryce/beautiful-lyrics
- https://github.com/rxri/spicetify-extensions
- https://github.com/Comfy-Themes/Spicetify
- https://github.com/catppuccin/spicetify
- https://github.com/nimsandu/spicetify-bloom
- https://github.com/TheRandomLabs/Scoop-Spotify

Ecosystem discovery:

- https://github.com/topics/spicetify-extensions
- https://github.com/topics/spicetify-themes
- https://github.com/topics/spicetify-custom-apps
- https://github.com/topics/spotx
- https://github.com/topics/spotify-ad-free
- https://github.com/topics/spotify-adblocker
- https://spicetify.app/
- https://spicetify.app/docs/getting-started/
- https://spicetify.app/docs/customization
- https://spicetify.app/docs/customization/themes.html
- https://spicetify.app/docs/customization/config-file/
- https://spicetify.app/docs/cli.html

Community signal:

- https://www.reddit.com/r/spicetify/comments/1sleiz4/spicetify_marketplace_icon_not_showing_up/
- https://www.reddit.com/r/spicetify/comments/1th8vhv/marketplace_only_shows_themes_no_extensions/
- https://www.reddit.com/r/spicetify/comments/1spyvxz/marketplace_fix/
- https://www.reddit.com/r/spicetify/comments/1s5ouwa/spicetify_new_update_help/
- https://www.reddit.com/r/spicetify/comments/1s5r874/what_happened_to_my_spicetify_after_latest_update/
- https://www.reddit.com/r/spicetify/comments/1t2wgj5/spicetify_resetting_not_just_on_spotify_update/
- https://www.reddit.com/r/Piracy/comments/1rq6zeu/pc_spotify_blockthespot_is_currently_archived_on/
- https://www.reddit.com/r/Adblock/comments/1q8enwb/how_to_block_spotify_ad_in_2026/

Distribution, signing, and supply chain:

- https://learn.microsoft.com/en-us/windows/package-manager/package/manifest
- https://learn.microsoft.com/windows/package-manager/winget/
- https://github.com/microsoft/winget-create
- https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests
- https://github.com/ScoopInstaller/Scoop/wiki/App-Manifest-Autoupdate
- https://docs.chocolatey.org/en-us/create/create-packages/
- https://docs.chocolatey.org/en-us/choco/commands/install
- https://docs.velopack.io/
- https://signpath.org/
- https://signpath.org/terms.html
- https://docs.signpath.io/
- https://github.com/actions/attest
- https://github.com/CycloneDX/cyclonedx-dotnet
- https://github.com/ossf/scorecard-action
- https://openssf.org/scorecard/

Platform and legal:

- https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/toast-desktop-apps
- https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/send-local-toast
- https://wpfui.lepo.co/api/Wpf.Ui.Controls.html
- https://wpfui.lepo.co/api/Wpf.Ui.Controls.Snackbar.html
- https://www.spotify.com/us/legal/user-guidelines//
- https://developer.spotify.com/terms/
- https://developer.spotify.com/policy

Cycle 2 release, UI, and packaging sources:

- https://dotnet.microsoft.com/en-us/platform/support/policy
- https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
- https://devblogs.microsoft.com/powershell/announcing-powershell-7-6/
- https://learn.microsoft.com/en-us/powershell/scripting/install/install-powershell-on-windows
- https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions
- https://github.com/actions/checkout/releases/tag/v6.0.3
- https://github.com/actions/setup-dotnet/releases/tag/v5.3.0
- https://github.com/actions/upload-artifact/releases/tag/v7.0.1
- https://github.com/actions/download-artifact/releases/tag/v8.0.1
- https://github.com/actions/attest-build-provenance/releases/tag/v4.1.0
- https://github.com/actions/attest-sbom/releases/tag/v4.1.0
- https://github.com/MScholtes/PS2EXE
- https://www.powershellgallery.com/packages/ps2exe/
- https://github.com/CycloneDX/cyclonedx-dotnet/releases/tag/v6.2.0
- https://github.com/lepoco/wpfui/releases/tag/4.3.0
- https://www.nuget.org/packages/WPF-UI
- https://www.nuget.org/packages/Wpf.Ui
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing
- https://github.com/microsoft/accessibility-insights-windows
- https://github.com/FlaUI/FlaUI/releases/tag/v5.0.0
- https://docs.velopack.io/distributing/overview
- https://github.com/velopack/velopack/releases/tag/1.2.0
- https://www.nuget.org/packages/Velopack/1.2.0
- https://github.com/librespot-org/librespot
- https://crates.io/crates/librespot

Cycle 3 reliability and supportability sources:

- https://json-schema.org/draft/2020-12
- https://json-schema.org/specification
- https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api
- https://learn.microsoft.com/en-us/powershell/module/bitstransfer/start-bitstransfer
- https://learn.microsoft.com/en-us/defender-endpoint/restore-quarantined-files-microsoft-defender-antivirus
- https://github.com/PowerShell/PSScriptAnalyzer/releases/tag/1.25.0
- https://github.com/pester/Pester/releases/tag/5.7.1
- https://github.com/microsoft/winget-pkgs/tree/master/manifests/s/Spotify/Spotify
- https://www.spotify.com/download/windows/

Cycle 4 distribution-readiness and shell policy sources:

- https://api.github.com/repos/theRealPadster/spicetify-hide-podcasts
- https://api.github.com/repos/surfbryce/beautiful-lyrics
- https://api.github.com/repos/jeroentvb/spicetify-playlist-icons
- https://api.github.com/repos/Shinyhero36/spicetify-song-stats
- https://api.github.com/repos/daksh2k/spicetify-stuff
- https://api.github.com/repos/catppuccin/spicetify
- https://api.github.com/repos/Comfy-Themes/Spicetify
- https://api.github.com/repos/nimsandu/spicetify-bloom
- https://api.github.com/repos/sanoojes/Spicetify-Lucid
- https://api.github.com/repos/Astromations/Hazy
- https://api.github.com/repos/spicetify/spicetify-themes
- https://raw.githubusercontent.com/theRealPadster/spicetify-hide-podcasts/main/dist/hidePodcasts.js
- https://raw.githubusercontent.com/surfbryce/beautiful-lyrics/main/dist/beautifulLyrics.js
- https://raw.githubusercontent.com/jeroentvb/spicetify-playlist-icons/main/dist/playlistIcons.js
- https://raw.githubusercontent.com/Shinyhero36/spicetify-song-stats/main/dist/songStats.js
- https://raw.githubusercontent.com/daksh2k/spicetify-stuff/main/Extensions/volumePercentage.js
- https://reuse.software/spec-3.3/
- https://spdx.org/licenses/
- https://opensource.guide/legal/
- https://docs.github.com/en/code-security/getting-started/adding-a-security-policy-to-your-repository
- https://docs.github.com/articles/creating-an-issue-template-for-your-repository
- https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro
- https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/send-local-toast
- https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/toast-desktop-apps
- https://learn.microsoft.com/en-us/windows/win32/shell/appids
- https://learn.microsoft.com/en-us/windows/win32/shell/links
- https://learn.microsoft.com/en-us/windows/win32/shell/fa-intro
- https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests
- https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/how-it-works
