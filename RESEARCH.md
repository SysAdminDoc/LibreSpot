# Research — LibreSpot

Date: 2026-07-24 — replaces all prior research.

## Executive Summary

LibreSpot is a Windows-only, local-only orchestrator that applies a verified
SpotX + Spicetify setup to the Spotify desktop client through three surfaces: a
mature single-file PowerShell GUI (`LibreSpot.ps1`, stable line v3.7.x), a
.NET 10 WPF shell (`src/LibreSpot.Desktop/`, v4.0.0-preview.19), and a fleet CLI
(`src/LibreSpot.Cli/`). Its strongest current shape is trust, recovery, and
observability, not feature breadth. Since the 2026-07-22 pass it has shipped a
Spicetify-v3 detection guard, the `TargetLatestRuntimePatch` + `dotnetRuntimeFloor`
CVE gate (floor already at 10.0.10), the pre-Defender SpotX pin hold, and — most
importantly — a full fix for the blank Marketplace store page
(`Repair-SpicetifyCustomAppWiring` + `RouteNotWired` health state, verified live
on Spotify 1.2.93.667). Community research confirms the niche is essentially
uncontested: no actively-maintained, polished Windows GUI wraps **both** SpotX
(ad-block) and Spicetify (theming) with a test suite; the only analogs
(spicetify-easyinstall, ModifySpotify) are stalled or tiny.

The highest-value direction remains keeping the trust wrapper current against two
fast-moving upstreams — not a broader catalog. The single most important new
signal this pass: **Spicetify's `main` merged "hard-fail on unsupported Spotify
version" (PRs #3894/#3895/#3896, 2026-07-20/21), not yet in the v2.44.0 release.**
Today Spicetify best-effort patches above its 1.2.93 ceiling, which is exactly
what lets LibreSpot's post-apply route re-wiring work on the current Spotify
stable (1.2.94.583). The next Spicetify release will likely **refuse** to apply
above its declared ceiling — the one upstream change most likely to break
LibreSpot, and it is imminent rather than released.

Top opportunities, in priority order:

1. **[Verified] Guard against Spicetify's hard-fail-on-unsupported release** — distinct from the shipped v3 guard; a v2.4x-line change that turns today's best-effort apply into a hard refuse (RD-41).
2. **[Verified] Correct the code-signing docs** — README/SECURITY/SIGNPATH still promise SignPath Authenticode signing "once the cert arrives," contradicting the project's actual unsigned-by-design posture and stranding users who wait for it (RD-43).
3. **[Verified] Extract non-UI logic into `LibreSpot.Core`** — shrinks the god-ViewModel and unblocks Stryker.NET (RD-35, carried).
4. **[Verified] Decompose `MainWindow.xaml` into per-screen UserControls** (RD-36, carried).
5. **[Verified] Add German/French locales** — framework + gate already exist (RD-37, carried).
6. **[Verified] Verify Spicetify build attestations, not just SHA256** (RD-38, carried).
7. **[Verified] Verify Spicetify applies over a stock (non-SpotX) backup** (RD-40, carried).

## Product Map

- **Core workflows:** inspect compatibility/readiness → install or reapply a
  Recommended/Custom SpotX+Spicetify setup → repair/restore/back up/remove managed
  state → create/import/apply local `.librespot` profiles → export redacted
  diagnostics. The fleet CLI automates the same lifecycle noninteractively
  (`status`, `detect`, `validate`, `install`, `reapply`, `repair`, `undo`,
  `uninstall`, `export-support`, `watcher install/remove`).
- **Personas:** individual Windows users wanting guided ad-removal/theming; power
  users choosing exact SpotX/Spicetify options; support contributors diagnosing
  failed patch state; endpoint admins deploying via answer files, NDJSON,
  receipts, and exit codes (Intune/PDQ/WinRM samples under `samples/deployment/`).
- **Platforms/distribution:** Windows 10/11; Windows PowerShell 5.1 and
  PowerShell 7; .NET 10 WPF + CLI published self-contained `win-x64`; portable
  GitHub-release assets (`LibreSpot.ps1`, PS2EXE `LibreSpot.exe`,
  `LibreSpot-Desktop.exe`, `LibreSpot.Cli.exe`, checksums, SBOM, release
  manifest). Package-manager channels remain operator-blocked on package identity.
- **Integrations/data flow:** Spotify desktop state → environment snapshot →
  preflight plan → pinned SpotX (`run.ps1`) / Spicetify CLI / Marketplace / theme
  archive downloads → SHA256 cache → local mutation → JSONL events/journal/receipt
  → optional redacted support ZIP. No credentials or telemetry service.

## Upstream State (verified 2026-07-24)

- **SpotX** `main` targets Spotify **1.2.94**; latest run.ps1 commit `3d1ddd68`
  (2026-07-14, `handle BinaryScanner failures`). Default-on Defender exclusions
  persist, opt-out `-defender_exclusions_off`. New nuance: the applier now branches
  on elevation — **silent when admin, interactive `Read-Host` y/n prompt when
  non-elevated**. Because LibreSpot's WPF backend runs `asInvoker` and hidden, any
  future advance to a Defender-mutating pin would hit that prompt and hang the
  hidden host; LibreSpot's fail-closed Defender gate (which refuses mutating pins
  without the declared opt-out) already prevents this — keep it.
- **Spicetify CLI** latest release still **v2.44.0** (2026-07-04), ceiling
  1.2.93, attestations still published. **`main` merged hard-fail-on-unsupported-
  version work: PRs #3894/#3895/#3896 (2026-07-20/21), plus a new `doctor`
  diagnostic (PR #3884, 2026-07-05)** — none released yet. The v3 rewrite (#3038)
  is still stale/unreleased (last touched 2024-05-21).
- **Marketplace** still v1.0.9; **themes** repo active (tracking Spotify UI churn,
  latest commit 2026-07-14). No route-injection contract change.
- **.NET 10** latest servicing is **10.0.10 (2026-07-14, 17 CVEs fixed)**; no
  advisories after that (next Patch Tuesday 2026-08-11). LibreSpot's floor is
  already 10.0.10, so shipped self-contained artifacts carry the fixes; only the
  gate's documented CVE rationale is stale (RD-42).
- **Spotify desktop** stable is **1.2.94.583** (~2026-07-18) — one minor above
  Spicetify's ceiling, the exact gap LibreSpot's route re-wiring bridges.

## Competitive Landscape

- **SpotX** (SpotX-Official/SpotX, 21.8k★) — the ad-block patcher LibreSpot wraps,
  not a rival. **Learn:** explicit compatibility windows, `-block_update_on`.
  **Avoid:** its `curl|iex` bootstrapper and default-on Defender exclusions.
- **Spicetify CLI** (spicetify/cli, 23.8k★) — theming engine LibreSpot wraps.
  **Learn:** build-provenance attestations, hard compatibility ceilings, the new
  `doctor` diagnostic verb. **Risk:** the imminent hard-fail-on-unsupported release
  (RD-41).
- **Spicetify Marketplace** (spicetify/marketplace, 1.5k★, 47 open issues) — the
  47 open issues are dominated by "marketplace won't show / blank store," which is
  LibreSpot's headline differentiator (fixed in preview.18/.19). Upstream treats it
  as "not our bug" because it only manifests when SpotX and Spicetify are combined
  — LibreSpot's exact use case.
- **spicetify-easyinstall** (ohitstom, 155★) — the closest historical analog (a
  GUI Spicetify+adblock installer); **stalled** (author stepped back, logic folded
  into Spicetify's own installer). **Learn:** the niche has real demand. **Avoid:**
  single-maintainer fragility — LibreSpot's 3-lane + test-suite shape is more durable.
- **BlockTheSpot** (mrpond, archived 2026-02-12) — legacy DLL-injection ad-block,
  now unmaintained; consolidates the ecosystem onto SpotX+Spicetify. LibreSpot's
  foreign-patcher detection should keep recognizing archived-BTS footprints.
- **EeveeSpotify / xManager** (not competitors, cautionary) — EeveeSpotify died
  under Spotify's 2025 DMCA + the encrypted-protobuf shift; xManager (Android
  premium-unlock) is repeatedly broken by server checks and is the exact category
  Spotify DMCAs. LibreSpot's script-injection-only, no-redistribution posture sits
  on the safe side of the enforcement line.

## Security, Privacy, and Reliability

- **[Verified] Spicetify hard-fail is the top forward risk.** `Get-LibreSpotCompatibilityWarnings`
  emits only a soft CSS-drift warning when the SpotX target exceeds Spicetify's
  max-tested version; there is no detection of a Spicetify build that would *hard-
  refuse* `backup apply` above its ceiling. The pin (2.44.0) insulates today, but
  a user-installed newer Spicetify, or a pin advance to gain 1.2.94 support (which
  may ship *with* the hard-fail gate), would break apply outright (RD-41).
- **[Verified] .NET CVE coverage is intact, docs are stale.** Floor is 10.0.10 in
  `schemas/dependency-health-allowlist.json:159`; the `reason` string enumerates
  only 4 of the relevant CVEs and omits the 2026-07-14 batch (RCE CVE-2026-50646/
  -50649, bypass CVE-2026-47304, +13). Security is covered by the floor; the
  enumeration should be refreshed for auditability (RD-42).
- **[Verified] Code-signing docs contradict policy.** README (`README.md:337`,
  `README.md:374`) and `SIGNPATH.md` state SignPath Authenticode signing is
  "pending" and that the "Unknown publisher" warning will disappear "once the cert
  arrives." The project ships unsigned by design; this copy strands users waiting
  for a cert that will not come and misframes the verify-by-checksum path as a
  stopgap rather than the permanent posture (RD-43).
- **[Verified] Inherited supply-chain risk is mitigated** — SpotX `run.ps1` is
  pinned by commit + SHA256, not `curl|iex`. Keep it.
- **[Likely] PS1 is the worst AV-FP surface.** `LibreSpot.ps1` (610 KB) draws
  PowerShell-heuristic (Powdow-class) Defender flags that the compiled WPF/CLI
  EXEs largely avoid; steer users to the EXE and proactively submit release hashes
  to the Microsoft Defender FP portal (RD-45).
- **Recovery posture:** transactional profile activation, allowlisted undo, and
  Marketplace snapshot/restore shipped; "restore stock Spotify binary" stays
  correctly blocked (needs a real SpotX-patched test rig — see RD-40).

## Architecture Assessment

- **Two WPF god-files.** `MainWindow.xaml` (~5,500 lines, all six nav screens +
  inspector) and `MainViewModel.cs` (~4,900 lines) are the standing maintainability
  outliers. Decompose XAML into per-screen UserControls (RD-36) and extract pure
  logic to a WPF-free `LibreSpot.Core` library (RD-35); the latter also unblocks
  Stryker.NET mutation testing, which cannot analyze a `net10.0-windows`/`UseWPF`
  target.
- **PowerShell monoliths are managed** — generated from ~130 one-function shared
  modules under `src/powershell/shared/` via the composition contract
  (`Build-Scripts.ps1 -ComposeHosts`); further splitting is low ROI.
- **Docs are version-synced** — README badge / csproj `<Version>` / CHANGELOG top
  all agree at 4.0.0-preview.19; `Test-LocalReleaseTruth` enforces it.

## Rejected Ideas

- **Aggressive SpotX bump to a Spotify 1.2.94 target now** — Spicetify 2.44.0 caps
  at 1.2.93 and the newer SpotX commit adds Defender exclusions; holding the
  pre-Defender pin `550bc72c` is safer. Source: SpotX run.ps1 `main`, spicetify/cli
  v2.44.0. Folded into the RD-41 pin-advance-trigger logic.
- **Auto-upgrading the pinned Spicetify to `main`/next** — `main` now carries the
  unreleased hard-fail gate; upgrading blindly would break apply on the current
  Spotify stable. Source: spicetify/cli PRs #3894/#3895/#3896.
- **Android/mobile support (xManager territory)** — contradicts the Windows-only
  philosophy; premium-unlock is the DMCA blast radius. Source: xmanager.app.
- **Freeze-loop / in-Spotify ad-block detection** — requires hooks inside the
  running Spotify client; unverifiable from an external process. Source: getblockify.com.
- **Bundled offline Spotify redistributable kit** — moves LibreSpot into the DMCA
  blast radius (2026 enforcement targets binary redistribution, premium unlock, and
  DRM circumvention only). Source: github/dmca 2026-03-17/05-28/07-09.
- **Code signing to clear SmartScreen** — the user's standing no-signing policy;
  also, Microsoft removed EV's default-trust in 2024, so signing no longer buys
  instant reputation. Source: learn.microsoft.com SmartScreen reputation docs.
- **winget/Scoop/Chocolatey/Velopack manifests** — blocked on package identity
  (name collision with `librespot-org/librespot`), an operator decision in
  `Roadmap_Blocked.md`.

## Sources

Upstreams:
- https://github.com/SpotX-Official/SpotX/commits/main/run.ps1
- https://github.com/SpotX-Official/SpotX/blob/main/run.ps1
- https://github.com/spicetify/cli/releases/tag/v2.44.0
- https://github.com/spicetify/cli/pull/3894
- https://github.com/spicetify/cli/pull/3895
- https://github.com/spicetify/cli/pull/3896
- https://github.com/spicetify/cli/pull/3884
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/marketplace/releases/latest
- https://github.com/spicetify/marketplace/issues/1135
- https://github.com/spicetify/spicetify-themes/commits

Community pain:
- https://github.com/SpotX-Official/SpotX/issues/876
- https://github.com/SpotX-Official/SpotX/issues/877
- https://github.com/SpotX-Official/SpotX/issues/875
- https://github.com/SpotX-Official/SpotX/issues/870
- https://github.com/spicetify/cli/issues/3816
- https://github.com/ohitstom/spicetify-easyinstall

.NET / security / trust:
- https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-july-2026-servicing-updates/
- https://support.microsoft.com/en-us/servicing/dotnet/net-10/2026/net-10-0-update-july-14-2026
- https://github.com/dotnet/announcements/issues/420
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
- https://learn.microsoft.com/en-us/defender-endpoint/defender-endpoint-false-positives-negatives

Legal:
- https://github.com/github/dmca/blob/master/2026/03/2026-03-17-spotify.md
- https://github.com/github/dmca/blob/master/2026/05/2026-05-28-spotify.md
- https://github.com/github/dmca/blob/master/2026/07/2026-07-09-spotify.md

## Open Questions

- **When does the Spicetify release ship the hard-fail-on-unsupported gate, and at
  what Spotify ceiling?** Governs the RD-41 detection threshold and the pin-advance
  trigger. Answerable only by watching spicetify/cli releases after v2.44.0.
- **Does the WPF shell stay self-contained, or move to framework-dependent?** If it
  moves to framework-dependent, the runtime-floor gate collapses to "document the
  minimum host runtime." Resolvable by the operator's distribution decision.
