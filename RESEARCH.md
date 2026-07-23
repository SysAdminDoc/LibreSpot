# Research — LibreSpot

Date: 2026-07-22 — replaces all prior research.

## Executive Summary

LibreSpot is a Windows-only, local-only orchestrator that applies a verified
SpotX + Spicetify setup to the Spotify desktop client, exposed through three
surfaces: a mature single-file PowerShell GUI (`LibreSpot.ps1`, stable line
v3.7.2), a .NET 10 WPF shell (`src/LibreSpot.Desktop/`, v4.0.0-preview.17), and
a fleet CLI (`src/LibreSpot.Cli/`). Its strongest current shape is trust,
recovery, and observability: the just-completed audit (`CHANGELOG.md`
`[Unreleased]`) shipped transactional profile activation, allowlisted executable
undo, cross-surface operation correlation, foreign-patcher detection, a
fail-closed Microsoft Defender mutation gate, a 29-location local-data contract,
SSRF guards on both HTTPS import paths, and pinned + SHA256-verified upstream
downloads. The codebase is unusually clean — no TODO/FIXME/stub debt in feature
paths, 662 xUnit facts + 157 Pester `It` blocks, a strict localization gate, and
high-contrast + reduced-motion handling.

The highest-value direction is **not** a broader feature catalog; it is keeping
the trust wrapper current and safe against two upstreams that make host-weakening
or contract-breaking changes on live `main`, plus paying down the two WPF
god-files. Top opportunities, in priority order:

1. **[Verified] Ship self-contained builds on the latest patched .NET 10 runtime** — the bundled runtime carries 2026 .NET CVEs until rebuilt (RD-32).
2. **[Verified] Guard against Spicetify v3's changed on-disk contract** — an unreleased rewrite that would break all three patch-detection sites at once (RD-33).
3. **[Verified] Re-verify pins and record the deliberate pre-Defender SpotX hold** — SpotX `main` now adds Defender exclusions by default and targets Spotify 1.2.94, but Spicetify caps at 1.2.93 (RD-34).
4. **[Verified] Extract non-UI logic into a `LibreSpot.Core` library** — shrinks the 4,871-line ViewModel and unblocks the filed Stryker.NET item (RD-35).
5. **[Verified] Decompose the 5,509-line `MainWindow.xaml`** into per-screen UserControls (RD-36).
6. **[Verified] Add German/French locales** — the framework and gate already exist (RD-37).
7. **[Verified] Verify Spicetify build attestations, not just SHA256** — a leapfrog trust signal (RD-38).

## Product Map

- **Core workflows:** inspect compatibility/readiness → install or reapply a
  Recommended/Custom SpotX+Spicetify setup → repair/restore/back up/remove
  managed state → create/import/apply local `.librespot` profiles → export
  redacted diagnostics; the fleet CLI automates the same lifecycle
  noninteractively (`status`, `detect`, `validate`, `install`, `reapply`,
  `repair`, `undo`, `uninstall`, `export-support`, `watcher install/remove`).
- **Personas:** individual Windows users wanting guided ad-removal/theming;
  power users choosing exact SpotX/Spicetify options; support contributors
  diagnosing failed patch state; endpoint admins deploying via answer files,
  NDJSON, receipts, and exit codes (Intune/PDQ/WinRM samples exist under
  `samples/deployment/`).
- **Platforms/distribution:** Windows 10/11; Windows PowerShell 5.1 and
  PowerShell 7; .NET 10 WPF + CLI published self-contained `win-x64`; portable
  GitHub-release assets (`LibreSpot.ps1`, PS2EXE `LibreSpot.exe`,
  `LibreSpot-Desktop.exe`, `LibreSpot.Cli.exe`, checksums, SBOM, release
  manifest). Package-manager channels (winget/Scoop/Choco/Velopack) remain
  operator-blocked on package identity.
- **Integrations/data flow:** Spotify desktop state → environment snapshot →
  preflight plan → pinned SpotX (`run.ps1`) / Spicetify CLI / Marketplace /
  theme archive downloads → SHA256 cache → local mutation → JSONL
  events/journal/receipt → optional redacted support ZIP. No credentials or
  telemetry service.

## Competitive Landscape

- **SpotX** (SpotX-Official/SpotX) — owns fast Spotify-version support (main
  targets **1.2.94** as of 2026-07-14) and expert flags. **Learn:** explicit
  compatibility windows, keep known-good targets. **Avoid:** its remote
  `curl|iex` bootstrapper and its new **default-on Microsoft Defender
  exclusions** (commit `afb4c3f`, 2026-07-11; opt-out `-defender_exclusions_off`;
  it even excludes the running PowerShell host process). LibreSpot's fail-closed
  Defender gate already refuses any pin that mutates Defender without proving the
  opt-out — stay the stricter wrapper.
- **Spicetify CLI** (spicetify/cli) — **v2.44.0** (2026-07-05), Spotify support
  ceiling **1.2.93**, publishes GitHub build attestations. **Learn:** provenance
  verification, hard compatibility ceilings. **Avoid:** exposing command-order
  fragility. **Risk:** **v3** (issue #3038, unreleased) moves to symlink
  xpui→config + `index.html` hooks + generalized "modules", which would break
  LibreSpot's three 2.x-filename detection sites simultaneously.
- **Spicetify Marketplace** (spicetify/marketplace) — **v1.0.9**, storage moved
  to **IndexedDB**. **Learn:** clear trust metadata + removal state. **Avoid:**
  treating Marketplace IndexedDB/browser state as backable profile data
  (validates the existing decision to keep `.librespot` to managed settings). The
  disappearing-Marketplace-button pattern (mkt #1133/#1004) is a support magnet.
- **BlockTheSpot** (mrpond) — **archived read-only 2026-02-14**; the main native
  chrome_elf DLL patcher is now unmaintained, consolidating the ecosystem onto
  SpotX+Spicetify (LibreSpot's exact stack). LibreSpot's foreign-patcher
  detection (shipped) should keep recognizing archived-BTS footprints.
- **spicetify-easyinstall** (ohitstom) — one-click GUI; **broke when Spotify
  blocked its `scdn` download endpoint** and has a history of deleted configs.
  Validates LibreSpot's verified offline SHA256 cache reuse as real resilience.
- **Intune / PDQ / winget** — set fleet table-stakes: detection rules, return
  codes, supersedence, receipts, offline cache. LibreSpot already matches these
  via the CLI + `samples/deployment/intune-detection.ps1`; do **not** build a
  control plane (that class is owned by these tools).
- **EeveeSpotify** (legal signal, not a competitor) — Spotify's 2025 DMCA wave
  (2025-08-14, ~520 repos) targeted **binary redistribution / Premium unlock**,
  **not** ad-block script injection, and did not name SpotX/Spicetify/BTS.
  LibreSpot's never-redistribute posture sits on the safer side of the observed
  enforcement line.

## Security, Privacy, and Reliability

- **[Verified] .NET 10 self-contained CVE exposure.** Both projects set
  `<RuntimeIdentifiers>win-x64` and release self-contained, so bundled-runtime
  CVEs — CVE-2026-32175 (crafted-file arbitrary write), CVE-2026-26127
  (Base64Url OOB read), CVE-2026-45490, CVE-2026-50526 — are only fixed by
  rebuilding against a patched runtime. No `<TargetLatestRuntimePatch>` in
  `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj` / `LibreSpot.Cli.csproj`
  (RD-32).
- **[Verified] Inherited supply-chain risk is mitigated.** LibreSpot pins SpotX
  `run.ps1` by commit + SHA256 (`AppCatalog.cs:621-626`) instead of calling
  SpotX's `curl|iex main`; keep it that way.
- **[Verified] Defender-mutation guardrail is complete.** The exact
  `-defender_exclusions_off` opt-out is enforced deny-by-default in
  `Build-Scripts.ps1:944-951/1019`, `LibreSpot.ps1:4360/7402-7405`, and
  `Backend/LibreSpot.Backend.ps1:1797-1798`. Only the pin-cadence decision
  remains (RD-34).
- **[Verified] Spicetify v3 detection break.** A single upstream release could
  make `Get-SpotXPatchVerification` / `EnvironmentSnapshotService` report a
  healthy Spotify as broken; a version guard should fail loud, not silent
  (RD-33).
- **[Likely] PS2EXE AV false positives** persist for the packed `LibreSpot.exe`;
  the WPF/CLI native artifacts are the cleaner distribution path (already tracked
  as the blocked native-launcher decision — no new item).
- **Recovery posture:** transactional profile activation, allowlisted undo, and
  Marketplace snapshot/restore shipped; a true "restore stock Spotify binary"
  path stays correctly blocked (needs a real SpotX-patched test rig).

## Architecture Assessment

- **Two WPF god-files.** `MainWindow.xaml` (5,509 lines, all six nav screens +
  inspector) and `MainViewModel.cs` (4,871 lines) are the standing maintainability
  outliers. Decompose XAML into per-screen UserControls (RD-36) and extract pure
  logic to a WPF-free `LibreSpot.Core` library (RD-35).
- **PowerShell monoliths are managed.** `LibreSpot.ps1` (9,541) and
  `Backend/LibreSpot.Backend.ps1` (5,236) are large but now generated from ~110
  one-function shared modules under `src/powershell/shared/` via the composition
  contract (`Build-Scripts.ps1 -ComposeHosts`); further splitting is low ROI.
- **Test gaps.** Stryker.NET mutation testing is blocked purely because the only
  build target is `net10.0-windows`/`UseWPF`; RD-35's Core library unblocks it.
  The stable PS-GUI has no FlaUI-equivalent automation, but it is on the
  retirement track (support boundary is an operator-blocked item), so investment
  there is low ROI.
- **Docs are in sync.** README badge / csproj `<Version>` / CHANGELOG top all
  agree at 4.0.0-preview.17; `Test-LocalReleaseTruth` enforces it.

## Rejected Ideas

- **Android/mobile support (xManager territory)** — contradicts the Windows-only
  philosophy; xManager is a separate Android/ReVanced lane. Source: xmanager.app.
- **Freeze-loop / in-Spotify ad-block detection** — requires hooks inside the
  running Spotify client; cannot be verified from LibreSpot's external process.
  Source: getblockify.com anti-adblock notes.
- **Aggressive SpotX bump to a Spotify 1.2.94 target now** — premature: Spicetify
  2.44.0 caps at 1.2.93 and the newer SpotX commit adds Defender exclusions;
  holding the pre-Defender pin `550bc72c` is safer (folded into RD-34, not a bump).
- **Bundled offline Spotify redistributable kit** — moves LibreSpot into the
  DMCA blast radius (Spotify's 2025 enforcement targets binary redistribution).
  Source: github/dmca 2025-08-14.
- **winget/Scoop/Chocolatey/Velopack manifests** — blocked on **package
  identity** (name collision with `librespot-org/librespot` on GitHub/crates.io),
  an operator decision in `Roadmap_Blocked.md`. Note: the *signing* sub-blocker
  is effectively dissolved (no-signing policy + winget portable requires only
  `InstallerSha256`, no signature), but identity remains. Source:
  github.com/microsoft/winget-pkgs schema 1.12.0.
- **PS-GUI FlaUI automation parity** — retirement-track surface; the WPF shell
  already carries FlaUI + rendered-QA coverage.
- **Native .NET launcher replacing PS2EXE** — already a `Roadmap_Blocked.md`
  item (entangled with the operator release/publishing pass).

## Sources

Ecosystem / upstreams:
- https://github.com/SpotX-Official/SpotX
- https://github.com/SpotX-Official/SpotX/commit/afb4c3fcd13807679fc3ffdb9fbe963edc552d15
- https://github.com/SpotX-Official/SpotX/commits/main
- https://github.com/spicetify/cli/releases/tag/v2.44.0
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/cli/issues/3239
- https://github.com/spicetify/marketplace/releases/tag/v1.0.9
- https://github.com/spicetify/marketplace/issues/1133
- https://github.com/mrpond/BlockTheSpot
- https://github.com/ohitstom/spicetify-easyinstall/releases

Legal / enforcement:
- https://github.com/github/dmca/blob/master/2025/08/2025-08-14-spotify.md
- https://github.com/github/dmca/blob/master/2025/04/2025-04-22-spotify.md

.NET / security:
- https://github.com/dotnet/announcements/issues/396
- https://github.com/dotnet/announcements/issues/403
- https://github.com/dotnet/announcements/issues/415
- https://github.com/dotnet/runtime/issues/125393
- https://learn.microsoft.com/en-us/dotnet/core/deploying/
- https://forums.malwarebytes.com/topic/274920-ps2exe-false-positive/

Deployment / distribution:
- https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/schema/1.12.0/installer.md
- https://www.recastsoftware.com/resources/choosing-detection-rules-for-win32-apps-in-intune/
- https://learn.microsoft.com/en-us/dotnet/core/rid-catalog
- https://docs.velopack.io/troubleshooting/faq

## Open Questions

- **When does Spicetify declare Spotify 1.2.94+ support?** Governs the trigger to
  advance the SpotX pin + Spotify target together (RD-34). Answerable only by
  watching spicetify/cli releases.
- **Does the WPF shell remain self-contained, or move to framework-dependent?**
  If it moves to framework-dependent, RD-32 collapses to "document the minimum
  host runtime" instead of a rebuild cadence. Resolvable by the operator's
  distribution decision, not by more research.
