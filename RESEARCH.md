# Research — LibreSpot
Date: 2026-08-20 — replaces all prior research.

## Executive Summary

LibreSpot is a Windows 10/11, MIT-licensed orchestrator that installs and maintains SpotX plus Spicetify with unusually strong safety controls: pinned SHA256-verified assets, dry-run/undo boundaries, backups, redacted support bundles, a fleet CLI, five locales, and 24 machine-enforced JSON contracts backed by 883 xUnit + 185 Pester tests. Its strongest current shape is the tested Core/PowerShell/WPF/CLI boundary shipped as v4.0.0-preview.25 on 2026-08-11. The central finding of this pass: **the 2026-08-11 conclusion that all remaining work is blocked at external seams is now stale.** Between 2026-08-10 and 2026-08-19 the ecosystem moved on three of the four blocked seams — Spicetify v3 entered public beta (beta.1 2026-08-10 through beta.9 2026-08-19) carrying the machine-readable `supported-versions.json` refusal contract and a `spicetify support` command that the blocked RD-41 item was waiting for; Spicetify Marketplace v1.0.9 moved persistence from localStorage to IndexedDB and closed issue #1201, changing the shape of the blocked Marketplace-state-recovery problem; and SpotX added a `-defender_exclusions_off` opt-out (2026-07-11), which removes the exact reason LibreSpot froze its SpotX pin at pre-Defender commit `550bc72c`. Meanwhile the repo itself has a shipping problem: six release versions (preview.20–.25) are committed but untagged, the newest published tag is v4.0.0-preview.19 (2026-07-23), and the public "Latest" release is v3.7.2 from 2026-04-29.

Priority opportunities, in order:

1. **P0 — Ship v4.0.0-preview.25.** Four weeks of completed work (Core extraction, Marketplace state recovery, catalog freshness, workspace-view extraction, compatibility baseline) is invisible to users; docs describe a `release.yml` workflow that does not exist, so the release procedure must be executed and documented locally (git tags, `.github/`, Build-Scripts.ps1 `-GenerateReleaseManifest`/`-ReleaseTruth`).
2. **P0 — Guard against Spicetify v3 coexistence damage.** v3 renames `xpui.spa` to `xpui.spa.backup` in place and by its own admission bricks a client patched by v2-era tooling; users who self-install the beta over a LibreSpot-managed install will hit it (spicetify `src/cmd/v3notice.go`).
3. **P1 — Security floors:** require pwsh ≥ 7.6.5 (CVE-2026-50523 command injection plus four more August 2026 PowerShell CVEs) and bump `dotnetRuntimeFloor` to 10.0.11 (2026-08-11 servicing, two RCEs).
4. **P1 — Encode SpotX's new Defender-exclusion default and opt-out into the pin-advance policy**, restoring a path to advance past `550bc72c` (SpotX now supports Spotify 1.2.97; pinned Spicetify v2.44.0 still caps 1.2.93).
5. **P1 — Adopt GitHub Immutable Releases** — GA since 2025-10-28, it gives Sigstore-signed attestations on locally built, manually uploaded assets with no GitHub Actions involvement, exactly matching the unsigned-by-design + local-build posture.
6. **P1/P2 — Build the Spicetify v3 contract fixtures now** (supported-versions.json schema v2, `spicetify support` exit semantics, fail-open rules) so the compatibility gate is ready when v3 goes stable.
7. **P2 — Test-stack refresh as a unit** (xunit.v3 4.0.0 + FsCheck.Xunit.v3 3.4.0 + Test.Sdk 18.9.0, all released 2026-08-14/20) and a bounded Stryker.NET pilot with the preview `--test-runner mtp`, which un-parks the blocked mutation-testing item.
8. **P2 — Structural debt round 2:** `MainViewModel.cs` regrew to 4,871 lines (larger than before the July extraction); `custom-workspace-view.xaml` is an 87 KB monolith with nonstandard kebab-case naming.

Confidence: Verified for all repository findings and upstream release facts (fetched 2026-08-20); Needs live validation for Marketplace IndexedDB file-level backup and any actual SpotX pin advance.

## Product Map

- **Core workflows:** Recommended setup (detect → install pinned SpotX+Spicetify → verify post-launch health); Custom Install (SpotX flags, themes/schemes, Marketplace, community assets, profiles, preview/plan); Maintenance (drift detection, re-apply, Marketplace repair, restore, cache, support export, receipt-backed undo); Fleet CLI (JSON/NDJSON, answer files, dry-run, deterministic exit codes) (README.md, src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1, src/LibreSpot.Cli, schemas/fleet-cli-contract.json).
- **Personas:** guided Windows Spotify user; power user (patches/themes/profiles); fleet operator (Intune/PDQ/SCCM/WinRM); the maintainer tracking upstream drift.
- **Platforms/distribution:** Windows 10/11; PS 5.1+ / PS 7.6 LTS; net10.0-windows win-x64. GitHub Releases only — stable lane `LibreSpot.ps1` v3.7.4 (public latest v3.7.2), preview lane WPF/CLI at 4.0.0-preview.25. Winget/Scoop/Chocolatey/PSGallery remain operator-blocked (Roadmap_Blocked.md). ARM64 is claimed in README but no csproj has an ARM64 RID — an already-filed blocked item.
- **Integrations/data:** pinned GitHub assets → SHA256 verify → cache/quarantine → safe extraction (all .NET `ZipArchive`/`Expand-Archive`; no 7-Zip anywhere — verified 2026-08-20); local-only state governed by schemas/data-inventory.json (29 locations); no telemetry.

## Repository State (verified 2026-08-20)

- HEAD `99ec47f` (2026-08-11); zero commits since; working tree clean; `ROADMAP.md` had zero actionable items.
- **Release drift:** csproj/README/CHANGELOG all say 4.0.0-preview.25 but the newest tag local+remote is `v4.0.0-preview.19`; six `chore: release` commits (.20–.25) never got tags; GitHub "Latest" is v3.7.2 (2026-04-29). Tag skipping is chronic (.13–.15, .18 also never existed).
- **Dead references:** README documents a `packaging/` directory (`packaging\Invoke-ValidationSamples.ps1`) that does not exist; README:397, `.gitignore:16-18`, CHANGELOG, and Roadmap_Blocked.md cite `.github/workflows/release.yml` and `scorecard.yml` with line numbers — neither file exists. The only workflow is `ci.yml` (headless lint/validate/Pester/build/test gate added 2026-08-11). Note: `ci.yml`'s `dotnet build` step is a compile gate, not artifact production, so it is consistent with the operator's local-builds policy; release artifacts have no workflow and must be built locally.
- **Dependabot residue:** no `.github/dependabot.yml`, but 13 stale `dependabot/*` branches sit on origin (checkout 4→6, Test.Sdk 17→18, coverlet 6→10 era) with zero open PRs. Operator policy is no Dependabot: delete branches, disable vulnerability alerts.
- **Doc-truth drift:** `.gitignore:28` blanket `*.md` + comment "Markdown local-only" contradicts the actually-tracked `RESEARCH.md`/`ROADMAP.md`/`Roadmap_Blocked.md`; AGENTS.md claims README is "the ONLY .md tracked". `schemas/parity-manifest.json` `generatorVersion` is stale at 4.0.0-preview.9; repo CLAUDE.md "Current State" says preview.17. `Roadmap_Blocks.md` (untracked) is a near-duplicate of `Roadmap_Blocked.md`. Root `LibreSpot.exe` + `checksums.txt` on disk are 2026-04-28 debris (untracked/gitignored, but misleading); `publish/` outputs are from 2026-07-23 (pre-.24); one stale `autostash` holds CLAUDE.md edits.
- **Structure:** `MainViewModel.cs` 4,871 lines — regrew +649 since the 2026-07-08 satellite-VM extraction and now exceeds its pre-refactor size (4,815). The 2026-08-11 workspace extraction genuinely shrank `MainWindow.xaml` to 3,839, but `Views/custom-workspace-view.xaml` is 87 KB (bigger than the remaining MainWindow) and all three view XAMLs use kebab-case filenames paired with PascalCase codebehind — builds, but defies WPF convention and breaks DependentUpon nesting. `LibreSpot.ps1` is 10,364 lines / `LibreSpot.Backend.ps1` 6,076, kept parity-checked by Build-Scripts.ps1; no TODO/FIXME/HACK debt anywhere (verified scan).

## Upstream Landscape (delta since 2026-08-11)

- **Spotify for Windows:** stable 1.2.96.518 (2026-08-12); 1.2.97 rolling out (SpotX bumped 2026-08-16). No xpui.spa container-format change observed.
- **SpotX:** rolling `main` (tag 1.9 is dead); supports 1.2.97; **adds Microsoft Defender exclusions by default since 2026-07-11 (`afb4c3fc`) with new opt-out `-defender_exclusions_off`** — the exact concern that froze LibreSpot's pin at pre-Defender `550bc72c` now has an upstream-sanctioned opt-out. `Apps\xpui.bak` + `Spotify.bak`/`Spotify.dll.bak`/`chrome_elf.dll.bak` semantics unchanged (run.ps1 fetched 2026-08-20). Issue #878: BinaryScanner emits a misleading warning under PS 5.1 — relevant to the PS 5.1 lane.
- **Spicetify CLI:** stable still v2.44.0 (caps Spotify 1.2.93 — now 3–4 versions behind the shipping client; a 1.2.94 css-map fix on main was never released). **v3.0.0-beta.1–beta.9 shipped 2026-08-10 → 2026-08-19** from the `v3-beta` branch with per-OS zips + `.sha256` sidecars (hash-pinnable). v3 ships the machine-readable compatibility contract LibreSpot wanted: `supported-versions.json` (schema v2, allowlist 1.2.70–1.2.94, per-version classmap status), a documented gate (backup/apply hard-refuse with exit 1; missing list/undetectable version fail open; `--force-unsupported-spotify` and `spotify_version_check=0` overrides; nearest-lower-classmap "degrade-not-destroy" fallback), a `spicetify support [version]` command (plain text, exit 1 only on definitive not-allowlisted), and `spicetify spotify-updates block|unblock` (patches the update endpoint in Spotify.exe; re-asserted on every apply). **Breaking:** v3 renames `xpui.spa` → `xpui.spa.backup` in place and does not carry over v2 extensions/themes/custom apps (replaced by "modules"); per upstream's own `v3notice.go`, installing v3 over a v2-patched client leaves it unbootable. Tracking issue spicetify/cli#3038.
- **Spicetify Marketplace:** v1.0.9 (2026-07-04) moved persistence localStorage → IndexedDB (PR #1181) with reworked import/export/reset; issue #1201 ("extensions removed after reboot") closed 2026-08-03 as fixed-by-update. Persistence hardening still in flight (#1212 open). LibreSpot already pins v1.0.9, but its recovery docs/health model still describe the localStorage-era "browser state is non-portable" boundary.
- **Legal climate:** no enforcement against SpotX/Spicetify/BlockTheSpot to date. Spotify's 2025 actions targeted downloaders (2025-04-22 DMCA) and premium-unlock (EeveeSpotify 520-repo network takedown 2025-08-14; ReVanced premium patch 2025-09). Pattern: ad-blocking/theming of the desktop client is not the enforced category; keep distance from premium-unlock adjacency. Spotify Web API tightening (2026-02-06: Premium required for dev mode, 1 client ID, 5 users) makes Web-API-dependent extensions fragile — relevant to catalog curation, not to LibreSpot core.

## Competitive Landscape

- **spicetify-easyinstall (ohitstom, 154★, active 2026-08-20)** — biggest GUI competitor; installs Spotify itself when missing. Learn: owning the Spotify install step (pinned known-good client) would close the "client too new for pinned Spicetify" gap. Avoid: its unpinned live-download trust model.
- **SpicetifyManager (Israleche, 22★, active 2026-08-07)** — menu-driven PS manager with one-click auto-apply and prominent full-restore. Learn: make restore-to-stock as prominent as install (LibreSpot's restore exists but is buried in Maintenance).
- **BlockTheSpot-Resilient (thomas-quant, 11★, created 2026-08)** — whole pitch is "survives Spotify updates, auto-rebuilds". Confirms auto-reapply-on-update as the #1 unmet demand; LibreSpot's watcher + scheduled task already covers most of it — breakage detection with auto-rollback is the remaining leapfrog.
- **EZBlocker (Xeroday, 1,848★, dormant)** — mute-on-ad without touching files. Learn: there is a persona that refuses binary patching; LibreSpot's answer should stay "Spicetify-only profile", not a mute engine.
- **Vencord Installer / Vesktop (620★/8,318★)** — single Go binary as GUI+CLI; Vesktop is in winget because it is a standalone app, not an injector. Learn: package managers accept apps, not patchers — consistent with keeping package channels blocked for now.
- **ReVanced Manager (29,130★)** — patch bundles declare per-app-version compatibility and the manager warns on untested combos; patched-apps dashboard; keeps original APK for clean re-patch. Learn: surface LibreSpot's pinned-tuple-vs-detected-client verdict as a first-class UI matrix; the pristine-backup-for-repatch idea validates the existing backup model.
- **r2modman (2,184★)** — profile export/import codes + `ror2mm://` protocol from a web catalog. LibreSpot already has `.librespot` profiles, QR share, and `librespot://`; the missing piece is a hosted catalog page to link from.
- **Mod Organizer 2 / NexusMods.App (3,054★/2,050★)** — pristine-copy + transaction-ledger models; validates the operation-journal/undo design. Learn: "show the diff before apply" UX.
- **Violentmonkey (8,753★)** — per-asset metadata with independent per-asset update checks and pin/unpin. Learn: per-extension update posture inside the curated catalog.
- **Fake-repo wave (SecretBarber/spotify-adblock-studio 267★ etc., created 2026-08-06)** — coordinated stargazer-fraud repos impersonating SpotX-style tools now outrank legitimate projects in GitHub search. LibreSpot's provenance story is the counter; a "how to spot fakes" section would rank and build trust.
- **Packaging precedent:** Spicetify.Spicetify is in winget (2.44.0); SpotX is not (issue #339 closed — winget wants installers, not scripts); Scoop `spotx-np` manifest rotted at 1.8-2023 — a lesson in naive hash-pinning of fast-moving upstream scripts. Chocolatey has spicetify-cli/marketplace. Velopack 1.2.0 (2026-06-03) healthy. No commercial competitor in the orchestration niche; paid tools are DRM converters (different category).

## Security, Privacy, and Reliability

- **New PowerShell 7 CVEs (2026-08-11 Patch Tuesday):** CVE-2026-50523 (command injection, CVSS 7.8, affects 7.6.0–7.6.4, fixed 7.6.5) plus CVE-2026-70337 (RCE 8.8), CVE-2026-70338, CVE-2026-59119, CVE-2026-58612. LibreSpot's PS7 lane should preflight ≥ 7.6.5 the same way the 5.1 lane preflights CVE-2025-54100 (SECURITY.md pattern). CVE-2025-54100 remains the standing 5.1 item; no new 5.1-specific CVE in 2026.
- **.NET floor:** 10.0.11 servicing (2026-08-11) fixes 10 CVEs incl. two RCEs (CVE-2026-70354, CVE-2026-62897). `schemas/dependency-health-allowlist.json` `dotnetRuntimeFloor` still says 10.0.10.
- **7-Zip CVE-2026-58052 (MOTW bypass, exploited ITW, unpatched):** verified not applicable — no 7z usage in the repo; extraction is .NET `ZipArchive`/`Expand-Archive`. Keep it that way; note it in SECURITY.md's supply-chain section only if 7-Zip ever enters the toolchain.
- **Spicetify v3 coexistence hazard (new):** v3's in-place `xpui.spa` → `xpui.spa.backup` rename conflicts with the SpotX/v2 orchestration LibreSpot manages; upstream states v3-over-patched-client is unbootable. LibreSpot currently detects "an unsupported Spicetify v3" generically (2026-07 guard) but does not recognize v3's backup artifact or module layout. Detection + refuse-with-guidance is fixture-testable without a live rig.
- **SpotX Defender exclusions:** upstream default since 2026-07-11; LibreSpot's pin freeze and `SpotXDefenderPolicyRegressionTests` predate the opt-out flag. Policy should encode: any pin advance must pass `-defender_exclusions_off` (or equivalent patch-level exclusion) and verify no exclusions were written.
- **Release integrity gap:** stale root `checksums.txt` (2026-04-28) on developer disk no longer matches `LibreSpot.ps1`; README already warns against verifying from a checkout. GitHub Immutable Releases (GA 2025-10-28) would lock published assets/tags and attach Sigstore attestations to manually uploaded, locally built binaries — no Actions required — strengthening the unsigned-by-design story materially; `gh release verify-asset` becomes a documented verification path alongside SHA256. Caution: immutability changes incident response — a bad release can no longer be edited or its assets swapped, only superseded by a newer release (or the whole release deleted), which the blocked bad-release/rollback-runbook item must reflect when written.
- **Guardrails worth preserving:** SHA256 pinning, safe archive limits, cache quarantine, SSRF/private-network guards, redacted bundles, asInvoker WPF, receipt-backed undo, explicit destructive confirmations (schemas/elevation-boundary.json, schemas/data-inventory.json).

## Architecture Assessment

- **Refactor seams:** `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs` (4,871 lines, regrowing — extraction round 2 due); `src/LibreSpot.Desktop/Views/custom-workspace-view.xaml` (87 KB — decompose by section); rename the three kebab-case view files to PascalCase to restore WPF pairing conventions. `LibreSpot.ps1` (10,364) / `LibreSpot.Backend.ps1` (6,076) stay under the composition/parity contract — do not introduce a new runtime abstraction.
- **Test stack:** xunit.v3 4.0.0 (2026-08-14, bundles MTP 2.3.3, drops MTP v1, ParallelMode API change) + FsCheck.Xunit.v3 3.4.0 (2026-08-20, "updated xunit.v3 to 4.x") + Microsoft.NET.Test.Sdk 18.9.0 form a coherent upgrade set. Stryker.NET 4.16.0 with the preview `--test-runner mtp` (shipped 4.13, 2026-03-13) is the sanctioned path for xunit.v3 — the Roadmap_Blocked mutation-testing item can be piloted now (expect slow runs; no per-test coverage yet; stryker-net#3117/#3094 still open). Whether the preview runner digests xunit.v3 4.0/MTP 2.3.3 specifically is untested — pilot on current 3.2.2 first.
- **Tooling currency:** PSScriptAnalyzer 1.25.0 (2026-03-20) adds four relevant rules + formatter changes — expect a fresh findings batch on a 10 k-line script. Pester 6.1.0 is a breaking major; 5.9.1 keeps the 5.x line supported, so no urgency. WPF-UI 4.3.0, QRCoder 1.8.0, CommunityToolkit.Mvvm 8.4.2, Serilog 4.4.0/Sinks.File 7.0.0, coverlet 10.0.1, AvalonEdit 6.3.1.120 all current-stable. No GitHub security advisories exist for any dependency (queried 2026-08-20). SDK floor `global.json` 10.0.100 + rollForward is satisfied by installed 10.0.400.
- **Docs/test gaps:** local release procedure is undocumented (docs point at a nonexistent workflow); `LibreSpot.Core` has no dedicated test project (Desktop.Tests covers it via reference — acceptable, but the Stryker pilot recipe calls for a Core-only target); README screenshot gate and WPF QA matrix are healthy.

## Rejected Ideas

- **Adopt Spicetify v3 beta as the pinned version now** — nine betas in ten days with empty release notes and a breaking module system; build the contract fixtures, pin only at stable (spicetify/cli releases).
- **EZBlocker-style mute-on-ad fallback engine** — different trust/mechanism domain (audio-session heuristics), duplicates SpotX's job badly; the no-binary-patch persona is served by a Spicetify-only profile (Xeroday/Spotify-Ad-Blocker).
- **Unify CLI into the WPF exe (Vencord single-binary pattern)** — would break the frozen fleet-cli-contract.json and the deterministic exit-code taxonomy for cosmetic gain.
- **Mirror/rehost upstream assets** — direct conflict with the no-redistribution posture and the DMCA climate (EeveeSpotify takedown 2025-08-14).
- **winget/Scoop/Chocolatey manifests now** — still operator-blocked on identity/rebrand; additionally winget-manifest authoring is prohibited by standing operator policy; precedent (Vesktop) says wait for a standalone-app story anyway.
- **Serilog.Sinks.File 8.0.0-dev / Pester 6 migration now** — prerelease sink; Pester 5.9.1 remains supported (pester releases 2026-08-11).
- **Global hosts/DNS ad blocker, cloud profile sync, uncurated marketplace, macOS/Linux port** — re-affirmed rejections from 2026-08-11; nothing changed the calculus (see docs/archive/research and Roadmap_Blocked.md).
- **Delete ci.yml as a policy violation** — its build step is a compile gate producing no artifacts; the operator's no-CI rule targets release-binary production, which already (and only) happens locally.

## Sources

### Upstream
- https://github.com/SpotX-Official/SpotX/commits/main
- https://raw.githubusercontent.com/SpotX-Official/SpotX/main/run.ps1
- https://github.com/SpotX-Official/SpotX/issues (875, 876, 878, 887)
- https://github.com/spicetify/cli/releases
- https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json
- https://github.com/spicetify/cli/blob/v3-beta/docs/supported-versions.md
- https://github.com/spicetify/cli/blob/v3-beta/docs/v3-modules.md
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/marketplace/pull/1181
- https://github.com/spicetify/marketplace/issues/1201
- https://github.com/SpotX-Official/SpotX-Bash/commits
- https://en.wikipedia.org/wiki/Template:Latest_stable_software_release/Spotify

### Competitors and adjacent
- https://github.com/ohitstom/spicetify-easyinstall
- https://github.com/Israleche/SpicetifyManager
- https://github.com/thomas-quant/BlockTheSpot-Resilient
- https://github.com/Xeroday/Spotify-Ad-Blocker
- https://github.com/ReVanced/revanced-manager
- https://github.com/Vencord/Installer
- https://github.com/ebkr/r2modmanPlus
- https://github.com/violentmonkey/violentmonkey
- https://github.com/microsoft/winget-pkgs (manifests/s/Spicetify)
- https://github.com/SpotX-Official/SpotX/issues/339
- https://github.com/ScoopInstaller/Nonportable (spotx-np.json)
- https://github.com/velopack/velopack/releases/tag/1.2.0

### Community signal
- https://reddit.com/r/spicetify/comments/1vimp5w/ (update-treadmill fatigue, 2026-08-07)
- https://reddit.com/r/spicetify/comments/1vhorzv/ (block Spotify updates, 2026-08-06)
- https://reddit.com/r/spicetify/comments/1v9j8d8/ (SpotX+Spicetify coexistence, 2026-07-28)
- https://reddit.com/r/spicetify/comments/1vjr1of/ (keeping Spicetify working, 2026-08-09)
- https://news.ycombinator.com/item?id=47788074 (Spotify API removals, 2026-04-16)

### Security and platform
- https://nvd.nist.gov/vuln/detail/CVE-2026-50523
- https://www.rapid7.com/blog/post/em-patch-tuesday-august-2026/
- https://support.microsoft.com/en-us/servicing/dotnet/net-10/2026/net-10-0-update-august-11-2026
- https://github.com/advisories/ghsa-fx33-p83c-vpr5 (7-Zip CVE-2026-58052)
- https://github.blog/changelog/2025-10-28-immutable-releases-are-now-generally-available/
- https://github.com/github/dmca/blob/master/2025/08/2025-08-14-spotify.md
- https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security
- https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle

### Dependencies and tooling
- https://xunit.net/releases/v3/4.0.0
- https://github.com/fscheck/FsCheck/releases
- https://stryker-mutator.io/blog/stryker-net-mtp-runner/
- https://github.com/stryker-mutator/stryker-net/issues/3117
- https://github.com/PowerShell/PSScriptAnalyzer/releases/tag/1.25.0
- https://github.com/pester/Pester/releases
- https://github.com/MScholtes/PS2EXE

## Open Questions

- **Spicetify v3 stable timing and final contract:** the beta's `supported-versions.json` schema v2 and `spicetify support` exit semantics could still change before stable; fixtures should tolerate schema-version negotiation. Only upstream can answer.
- **Marketplace IndexedDB file-level backup:** IndexedDB (Chromium LevelDB under the Spotify profile) may be copyable at rest, but only a live patched rig can prove export/import fidelity across Spotify updates — the remaining Needs-live-validation core of the old P1.
- **SpotX pin advance end-state:** advancing past `550bc72c` with `-defender_exclusions_off` restores current-Spotify support for the SpotX half, but pinned Spicetify v2.44.0 still caps 1.2.93 — is a split pin (newer SpotX, held Spicetify, Spotify held at 1.2.93 via update blocking) acceptable product behavior, or does the pin move only when all three align? Operator call once the policy encoding (RD-49) lands.
