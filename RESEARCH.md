# Research: LibreSpot

Date: 2026-09-03. Replaces all prior research.

## Executive Summary

LibreSpot v4.2.0 (local candidate; v4.1.2 is the public latest release, 2026-09-02) is a finished-feeling Windows product: a WPF desktop app, a fleet CLI, and a composed PowerShell script that install a hash-pinned tuple of Spotify 1.2.93, SpotX at commit `550bc72c`, Spicetify 2.44.0, and Marketplace 1.0.9, plus an AGPL live customization app that runs inside Spotify. The 2026-08-23 plan (RD-127 through RD-134) and the 2026-09-01 customization plan (RD-135 through RD-141) both shipped in full, the tracker has no open issues, and every local gate is green at `09238af` (`CHANGELOG.md`; `ROADMAP.md`; `git log`).

The strongest current shape is the trust story: immutable releases with checksums, SBOM, release manifest, and a GitHub attestation, unsigned by design and documented as such (`SECURITY.md`; `SIGNPATH.md`; [v4.1.2 release](https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.1.2)). The highest-value direction is to make that story true at the edges the last two release cycles created. One edge is already broken in production: the bundled live engine downloads from a mutable `main` URL, so the public v4.1.2 release now fails its own SHA256 check on a fresh install (`src/powershell/data/CommunityCustomApps.ps1:17-25`; see Security). The other edges are drift: the reviewed community catalog was last verified on 2026-06-15, before the 1.2.93 pin existed; Marketplace is two releases behind the fix for the "settings vanish" complaint; Spotify is on 1.2.98 with 1.2.99 staged while Spicetify 2.44.0 declares support only to 1.2.96; and several README claims describe the script lane as if they were product-wide.

| Rank | Opportunity | Timing | User impact | Effort | Confidence |
|---|---|---|---:|---:|---|
| 1 | RD-142: Pin the live engine download to an immutable per-release source | Now, P0 | 5/5 | M | Verified |
| 2 | RD-143: Advance Marketplace to 1.0.11 with a live persistence proof | Now, P1 | 4/5 | M | Verified |
| 3 | RD-144: Make README feature claims lane-accurate and test the phase count | Now, P1 | 3/5 | S | Verified |
| 4 | RD-145: Re-verify the reviewed community catalog against Spotify 1.2.93 | Now, P1 | 4/5 | M | Verified |
| 5 | RD-146: Script the release publish and make the build reproducible | Now, P1 | 3/5 | M | Verified |
| 6 | RD-147: One-click backup file for engine and Marketplace state | Next, P1 | 4/5 | M | Likely |
| 7 | RD-148: Compress the single-file executable and measure cold start | Next, P2 | 3/5 | S | Verified |
| 8 | RD-149: Trigger reapply from a file change instead of a 30-minute poll | Next, P2 | 4/5 | M | Likely |
| 9 | RD-151: Audit the engine's JavaScript dependencies and retire spicetify-creator | Next, P2 | 2/5 | M | Verified |
| 10 | RD-152: Run Axe.Windows inside the UIA smoke suite | Later, P2 | 2/5 | M | Likely |

Direct NuGet dependencies are current as of 2026-09-03 (WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, xunit.v3 4.0.0, FlaUI 5.0.0, Serilog 4.4.0). The .NET floor of 10.0.11 already carries the July WPF XAML RCE fixes and the August runtime fixes. The only dependency gap worth roadmap time is on the JavaScript side, where `spicetify-creator` 1.0.17 is unmaintained and drags in esbuild 0.14.54, and no JavaScript audit runs in `Build-Scripts.ps1 -DependencyHealth` (`src/LibreSpot.App/package.json`; `Build-Scripts.ps1`).

## Product Map

### Core workflows

- **Recommended setup.** Snapshot the machine, choose one Home action from health state, install the reviewed tuple, open Spotify (`src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs:76-121`; `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`).
- **Settings and profiles.** Four essentials first, eight one-level groups behind them, `.librespot` files, `librespot://` links, share cards, and global search (the last of which is unreachable in the simplified shell; see Reported Issues) (`src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.CustomInstall.cs`).
- **Maintenance and recovery.** Overall status, one safe repair, diagnostics under disclosure, a separate danger tier, journal-backed undo, redacted support bundles (`src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`; `src/LibreSpot.Core/OperationJournalUndoService.cs`; `src/LibreSpot.Core/SupportBundleService.cs`).
- **Live customization inside Spotify.** Look, Tweaks, Features, Extensions, Presets, and Health panels at `/librespot/*`, with a companion extension that keeps the engine present on every route; state lives in browser localStorage under `librespot:engine-state` (`src/LibreSpot.App/src/surface/navigation.ts`; `src/LibreSpot.App/src/core/store.ts:8`).
- **Managed deployment.** `status`, `detect`, `validate`, `install`, `reapply`, `uninstall`, `repair`, `undo`, `plan`, `export-support`, `version`, and `watcher` verbs with JSON and NDJSON contracts, answer files, dry run, and Intune detection guidance (`src/LibreSpot.Cli/Program.cs:153-166`; `schemas/fleet-cli-contract.json`; `README.md:291-329`).

### Users

- A common Windows user who wants one download, one action, and a client that keeps working after Spotify updates.
- A customization user who wants live control over colour, layout, feature flags, and extensions, and who is afraid of losing that setup.
- An operator who needs unattended runs, receipts, and detection scripts.
- The maintainer, who keeps a four-part compatibility tuple honest with local gates and immutable evidence.

### Platform and distribution

- Desktop and CLI are `net10.0-windows`, `win-x64` only, self-contained single-file (175.7 MiB desktop, 72.2 MiB CLI), uncompressed, no trimming, no ReadyToRun, no deterministic-build properties (`src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:8-17`; `schemas/publish-footprint-budget.json`; [v4.1.2 assets](https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.1.2)).
- The script lane self-elevates, uses runspaces, falls back to BITS, and verifies x64 and ARM64 hashes for Spicetify. The desktop and CLI lanes do none of that, yet `README.md:418-436` lists these as product-wide features.
- Releases are local builds uploaded to immutable GitHub releases with `checksums.txt`, a CycloneDX SBOM, a release manifest, and a Sigstore release attestation. Smart App Control blocks unsigned executables with no per-app bypass, and SmartScreen reputation restarts at zero for every unsigned file ([Smart App Control FAQ](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions); [SmartScreen reputation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)).
- Windows on ARM runs the x64 build under emulation. SpotX gained ARM64 binary patches on 2026-09-01 and Spicetify ships an arm64 archive, so the ARM lane is now technically possible; the decision remains in `Roadmap_Blocked.md` ([SpotX #888](https://github.com/SpotX-Official/SpotX/issues/888); [Spicetify 2.44.0 assets](https://github.com/spicetify/cli/releases/tag/v2.44.0)).

### Upstream state on 2026-09-03

| Component | LibreSpot pin | Upstream now | Gap |
|---|---|---|---|
| Spotify (Windows) | 1.2.93.667 | 1.2.98.301 public; SpotX `run.ps1` recommends 1.2.99 since 2026-08-31 | Five to six minors ([Uptodown history](https://spotify.en.uptodown.com/windows/versions); [SpotX commit](https://github.com/SpotX-Official/SpotX/commit/1d19a68f58abbb298d8095c310aa81e45055c833)) |
| SpotX | commit `550bc72c`, labelled "2.0" | `main` at `fd064f54` (2026-09-01); last release tag is 1.9 (2025-01-03) | Pin lacks the 1.2.94+ ad-slot binary patch (#876), ARM64 patches (#888), and the crossfade DLL patch; the "2.0" label does not match any upstream tag ([SpotX releases](https://github.com/SpotX-Official/SpotX/releases); `%LOCALAPPDATA%\LibreSpot\upstream-drift-cache.json`) |
| Spicetify | 2.44.0 | 2.44.0 is the last v2; v3.0.0-beta.11 (2026-09-02) refuses Spotify older than 1.2.80 and replaces custom apps with modules | Declared v2 support ends at Spotify 1.2.96 ([v2.44.0](https://github.com/spicetify/cli/releases/tag/v2.44.0); [v3 architecture](https://github.com/spicetify/cli/issues/3038)) |
| Marketplace | 1.0.9 | 1.0.11 (2026-09-02) | 1.0.10 added manifest validation and persistence before reload; 1.0.11 "properly migrate keys" ([releases](https://github.com/spicetify/marketplace/releases)) |
| Community catalog | verified 2026-06-15 | Comfy last pushed 2026-01-04 with an open playbar regression; Stats last released 2025-12 | Never re-verified against the 1.2.93 pin (`schemas/community-assets.json`; [Comfy #256](https://github.com/Comfy-Themes/Spicetify/issues/256)) |

### Integrations and data

- Every download is tied to reviewed metadata and a SHA256, with an asset cache, quarantine, bounded extraction, and a `mirror` option that SpotX honours (`src/LibreSpot.Core/AppCatalog.cs:824-842`; `src/powershell/shared/Download-FileSafe.ps1`; `src/powershell/shared/Module-InstallCustomApps.ps1:40-50`).
- Upstream drift is checked with `git ls-remote` and cached locally; the cache on this machine already reports SpotX "behind" (`src/LibreSpot.Core/UpstreamDriftService.cs`; `%LOCALAPPDATA%\LibreSpot\upstream-drift-cache.json`).
- The Home update notice reads the latest stable release with an ETag and a 24-hour cache. The code comment says a 304 costs no rate limit; for anonymous calls it costs one of sixty per hour, so the cache is the real protection (`src/LibreSpot.Core/ReleaseNoticeService.cs:50`; [GitHub rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)).
- The auto-reapply watcher is a logon-triggered scheduled task that repeats every 30 minutes with battery flags off and `StartWhenAvailable` on (`src/powershell/gui/lane-functions.ps1:106-186`).
- How SpotX and Spicetify alter the client, including the 1.2.70 signature wall, the 1.2.86 class-name shortening, and the route collision LibreSpot repairs, is documented in `docs/how-spotx-and-spicetify-alter-spotify.md`. The 2026-09-01 customization deep-dive that produced RD-135 through RD-141 lives in the vault note "Spotify Customization & Patching 2026-09-01"; all seven of those items shipped in v4.1.1 and v4.1.2.

## Competitive Landscape

- **[Spicetify CLI](https://github.com/spicetify/cli) and [Marketplace](https://github.com/spicetify/marketplace).** The ceiling for customization. v3 is a Rust rewrite with an in-client module store, side-by-side module versions, and a daemon that repairs an unapplied client; it has empty release notes, refuses clients older than 1.2.80, and its `supported-versions.json` still ends at 1.2.94 (last touched 2026-07-21). Learn: per-build classmaps as an auditable ABI ([classmaps](https://github.com/spicetify/classmaps)), "say which part is degraded". Avoid: chasing v3 before it has release notes and a support ceiling newer than what LibreSpot pins.
- **[SpotX](https://github.com/SpotX-Official/SpotX).** Ships a `patches.json` bump within days of every Spotify release and now does real binary patching (ad slots, crossfade, ARM64). Learn: nothing new; LibreSpot already exposes Premium mode, cache limit, mirror, `sendversion` and custom patches (`src/powershell/shared/Build-SpotXParams.ps1`). Avoid: its self-added Defender exclusions and the `raw.githack` distribution that Cloudflare flags as phishing ([#836](https://github.com/SpotX-Official/SpotX/issues/836)).
- **[BlockTheSpot-Resilient](https://github.com/thomas-quant/BlockTheSpot-Resilient)** and [Nuzair46/BlockTheSpot-Installer](https://github.com/Nuzair46/BlockTheSpot-Installer). mrpond's original is archived. Learn: the "degrade to ads-not-blocked, never a dead client" contract and a per-release signature match report. Avoid: `chrome_elf.dll` sideloading and daily generated releases.
- **[WMPotify](https://github.com/Ingan121/WMPotify) and the Windhawk CEF/Spotify Tweaks mod.** The only working path to native frames, transparency, and forced-dark-mode removal. Avoid: it requires Windhawk and CEF hooks with their own update treadmill; the earlier rejection stands.
- **[spicetify-easyinstall](https://github.com/ohitstom/spicetify-easyinstall) and [SpicetifyManager](https://github.com/Israleche/SpicetifyManager).** Three separate "SpicetifyManager" repos appeared between June and August 2026, and a Reddit post for one drew 188 points; the demand for a GUI over the CLI is real and LibreSpot already meets it ([r/spicetify thread](https://www.reddit.com/r/spicetify/comments/1uk6vs7/)). Learn: easyinstall's installer fallback chain (CDN, mirror, Wayback) after Spotify's direct links died in March 2026 ([easyinstall 3.7](https://github.com/ohitstom/spicetify-easyinstall/releases/tag/3.7); [Soggfy #159](https://github.com/Rafiuth/Soggfy/issues/159)).
- **Themes and apps that overlap the live engine.** [Lucid](https://github.com/sanoojes/spicetify-lucid) (AGPL, active) and [DefaultDynamic](https://github.com/JulienMaille/spicetify-dynamic-theme) cover artwork palettes and auto light/dark; [Comfy](https://github.com/Comfy-Themes/Spicetify) is effectively unmaintained; [Xndr2/listening-stats](https://github.com/Xndr2/listening-stats) is the active local-only successor to the pinned Stats app. Presets, health checks, and a fixture-backed compatibility tuple remain unique to LibreSpot.
- **Decoys.** `NeedChandlerMonitor/spicetify-elite` (317 stars) and `SecretBarber/spotify-adblock-studio` (290) are star-farmed repos with nonsense READMEs and off-GitHub downloads that outrank LibreSpot in search. Likely malware droppers; worth a sentence in the verification section.
- **[BetterDiscord Installer 2.0](https://github.com/BetterDiscord/Installer/releases/tag/v2.0.0), [Vesktop](https://github.com/Vencord/Vesktop), [ReVanced Manager](https://github.com/ReVanced/revanced-manager), [Windhawk 2.0](https://github.com/ramensoftware/windhawk/releases), [Vortex 2.6](https://github.com/Nexus-Mods/Vortex/releases/tag/v2.6.0).** The patterns worth copying: a loader that survives the host's update (BD), a one-word `--repair` (Vesktop, which LibreSpot has as the `repair` verb), a recommended version by default with "other supported" behind a toggle (ReVanced), a non-default marker with per-row revert (Windhawk 2.0), and a health page that never reads green while offline (Vortex). Avoid: cloud sync, short-lived share codes through a relay, premium-gated fixes, rolling channels as the default, and analytics.
- **Fleet conventions.** PSADT reserves 69000 to 69999 for custom exit codes; Intune detection is exit 0 plus STDOUT and anything on STDERR means "not installed"; Chocolatey and Scoop pin by hash and support offline internalized packages ([PSADT exit codes](https://github.com/PSAppDeployToolkit/website/blob/main/docs/reference/exit-codes.mdx); [Intune Win32](https://learn.microsoft.com/en-us/intune/intune-service/apps/apps-win32-add); [Chocolatey internalizer](https://docs.chocolatey.org/en-us/features/package-internalizer/)). LibreSpot already documents the detection command and receipt; its custom codes (10 to 60) collide with common Windows error numbers but changing them would break the published contract, so that stays rejected.

## Reported Issues

The tracker has zero open issues and zero open pull requests. The five closed issues are the old script-era failures (#3 GitHub 403 on the Spicetify download, #4 `Expand-Archive` in a runspace, #5 blank screen from a bypassed SpotX version check), each with a direct test and recovery path today. Discussions #20 and #21 are owner announcements with no replies. Fourteen Dependabot pull requests are closed and Dependabot alerts are disabled on the repository ([issues](https://github.com/SysAdminDoc/LibreSpot/issues?q=is%3Aissue); [#5](https://github.com/SysAdminDoc/LibreSpot/issues/5); `gh api repos/SysAdminDoc/LibreSpot/vulnerability-alerts` returns 404). Repository traffic is 21 views in 14 days with a 377-clone spike on 2026-08-31 that looks like scanner traffic, so an empty tracker is no data.

Defects found in this pass by reading the code and the release:

- **Fresh v4.1.2 installs cannot install the live engine.** The custom-app pin points at `raw.githubusercontent.com/.../main/resources/custom-apps/librespot-engine.zip` with `ReleaseTag = 'main'`, `Bundled = $true` is set but never read, and the installer downloads that URL and checks it against the pinned SHA256. v4.1.2 pins `c30ea64c...`; `main` now serves the 4.2.0 build `e280fbdf...`. Any machine without the archive already in its asset cache fails the hash check (`src/powershell/data/CommunityCustomApps.ps1:17-25`; `src/powershell/shared/Module-InstallCustomApps.ps1:40-50`; `git show v4.1.2:src/powershell/data/CommunityCustomApps.ps1`; `sha256sum resources/custom-apps/librespot-engine.zip`). RD-142.
- **README claims that only the script lane fulfils.** "8-phase uninstaller" (the script logs seven phases since the native phase was removed in `2a0b2ee`), "Spotify and installer windows are automatically hidden" and "LibreSpot stays on top" (`Hide-SpotifyWindows` is a no-op stub in the backend lane and the desktop has no `Topmost`), "x64 and ARM64", "Self-elevating", "Dual download methods", and "Threaded UI, runspaces" (`README.md:347,418-436`; `LibreSpot.ps1:9134`; `src/powershell/backend/lane-functions.ps1:415`; `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:8`). The lyrics list says 27 options and the catalog holds 28 (`src/LibreSpot.Core/AppCatalog.cs:962-968`). RD-144.
- **The 4.2.0 changelog describes opening a profile "from global search", but global search is unreachable.** The only search box lives inside `ShellWorkspaceHost`, which is `Visibility="Collapsed"` (`src/LibreSpot.Desktop/MainWindow.xaml:1408-1410,3046`). The view-model path is real and tested; the user path is not. This belongs to the P0 decision already in `Roadmap_Blocked.md`; the changelog line should say the file and link paths only. Folded into RD-144.
- **The release-notice comment is wrong about rate limits.** An anonymous conditional request that returns 304 still counts against the 60-per-hour budget (`src/LibreSpot.Core/ReleaseNoticeService.cs:50`; [best practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api)). RD-154.
- **The "SpotX 2.0" label has no upstream referent.** SpotX's last release tag is 1.9; the pinned commit is what matters and is already recorded (`src/powershell/data/PinnedReleases.ps1:3-4`; [SpotX releases](https://github.com/SpotX-Official/SpotX/releases)). Folded into RD-144.

What the wider community reports, ranked by how often it appears, with the LibreSpot answer:

1. **A Spotify update breaks the mod stack** ([r/spicetify, 716 points](https://www.reddit.com/r/spicetify/comments/1p29rqh/); [spicetify #3873](https://github.com/spicetify/cli/issues/3873); [#3914](https://github.com/spicetify/cli/issues/3914)). LibreSpot blocks updates with SpotX's binary patch by default and runs a 30-minute reapply poll; the poll can be event-driven (RD-149).
2. **Update blocking is unreliable and the reapply chore is manual** ([spicetify #3869](https://github.com/spicetify/cli/issues/3869), maintainer: "Won't be fixed"; [r/Piracy](https://www.reddit.com/r/Piracy/comments/1qjpwmg/)). Same answer.
3. **Extensions and themes vanish on startup** ([spicetify #3861](https://github.com/spicetify/cli/issues/3861); [marketplace #1201](https://github.com/spicetify/marketplace/issues/1201); [r/spicetify 2026-08-22](https://www.reddit.com/r/spicetify/comments/1vvklzn/)). Marketplace 1.0.11 is the upstream fix attempt (RD-143); the engine's own state has the same exposure (RD-147).
4. **Defender flags SpotX and leaves installs half done** ([SpotX #873](https://github.com/SpotX-Official/SpotX/discussions/873); [#846](https://github.com/SpotX-Official/SpotX/issues/846); [Soggfy #152](https://github.com/Rafiuth/Soggfy/issues/152)). LibreSpot's pinned commit predates SpotX's self-exclusion policy and the README already handles detections honestly; the Smart App Control FAQ still talks only about the script (RD-153).
5. **Marketplace missing from the sidebar** ([spicetify #3816](https://github.com/spicetify/cli/issues/3816); [marketplace #1194](https://github.com/spicetify/marketplace/issues/1194)). Already covered by the route repair and the "Store page not wired" health state.
6. **Installer plumbing: blocked CDNs, Cloudflare, ISP filters** ([SpotX #829](https://github.com/SpotX-Official/SpotX/issues/829); [#836](https://github.com/SpotX-Official/SpotX/issues/836); [#870](https://github.com/SpotX-Official/SpotX/issues/870)). LibreSpot lets SpotX fetch Spotify and passes `mirror`; the pinned 1.2.93 installer link is a single point of failure that no gate watches (Open Questions).
7. **PowerShell, admin, and opaque errors** ([spicetify #3846](https://github.com/spicetify/cli/issues/3846); [#3854](https://github.com/spicetify/cli/issues/3854)). Already answered by the desktop lane.
8. **Ban fear** ([r/spicetify 2026-08-19](https://www.reddit.com/r/spicetify/comments/1vsllxg/); [spicetify #3841](https://github.com/spicetify/cli/issues/3841)). No verified 2026 desktop suspension wave exists; LibreSpot's Premium mode and capability-boundary copy already address it.
9. **"Which version do I need, how do I update"** ([SpotX #879](https://github.com/SpotX-Official/SpotX/discussions/879); [#889](https://github.com/SpotX-Official/SpotX/discussions/889)). Maintenance's compatibility card exists; no new item.
10. **Microsoft Store build silently incompatible** ([Spicetify FAQ](https://spicetify.app/docs/faq)). Already a named health component (`Strings.resx:1110`).
11. **Feature chasing** (Smart Shuffle removal in 1.2.89, Jam, Mix, lossless). Server-side; rejected.
12. **ARM64** ([SpotX #888](https://github.com/SpotX-Official/SpotX/issues/888)). Blocked decision; evidence updated below.

## Security, Privacy, and Reliability

- **Mutable download source for a hash-pinned asset, Verified.** Described above. Beyond breaking old releases, a `main` URL means the reviewed asset can change between the review and the install; the SHA256 catches it, but the failure lands on the user as a refused install rather than on the maintainer as a release gate. The fix is a per-tag release asset URL or installing from the tracked archive, plus a test that the URL cannot change after tagging (RD-142). Related: `resources/custom-apps/librespot-engine.zip` is not among the seven release assets, so the release manifest does not cover the one file the installer fetches at run time (`schemas/release-artifact-contract.json`; [v4.1.2 assets](https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.1.2)).
- **Runtime servicing, Verified.** The 10.0.11 floor carries the fixes for the WPF XAML RCEs CVE-2026-50646 and CVE-2026-50649 (fixed 10.0.10) and the August runtime set including two RCEs. Self-contained executables only receive these fixes when rebuilt, which the README states; a release is due whenever the floor moves ([announcement 418](https://github.com/dotnet/announcements/issues/418); [10.0.11 notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.11/10.0.11.md); `README.md:520`).
- **JavaScript toolchain, Verified.** `spicetify-creator` 1.0.17 has no GitHub releases, depends on esbuild `^0.14`, and the lockfile resolves esbuild 0.14.54 beside the direct 0.28.2. The old version sits inside GHSA-67mh-4wv8-2f99 (dev server only, so no shipped exposure), but no JavaScript audit runs anywhere in the local gates, so the next real advisory would also go unseen (`src/LibreSpot.App/pnpm-lock.yaml`; `Build-Scripts.ps1`; [advisory](https://github.com/vitejs/vite/issues/19428)). RD-151.
- **Application control copy, Verified.** Smart App Control has no per-app bypass and blocks unsigned executables outright; the README FAQ entry covers only the script ("Smart App Control blocks the script from running"), while the desktop executable is the recommended path (`README.md:479-480`; [SAC FAQ](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions)). RD-153.
- **Reproducibility, Verified.** No `Deterministic`, `ContinuousIntegrationBuild`, `EmbedUntrackedSources`, or `PublishRepositoryUrl` property exists, and the README release procedure never states the `dotnet publish` invocation, so no second party can rebuild an asset and compare (`src/*/*.csproj`; `Directory.Build.props`; `README.md:562-570`; [reproducible builds](https://github.com/dotnet/reproducible-builds)). RD-146.
- **State durability, Likely.** Engine state lives in the browser profile's localStorage, the same store whose wipes fill the Spicetify tracker. Profiles carry that state only when the user copies it; nothing captures it automatically (`src/LibreSpot.App/src/core/store.ts:8-23`; [spicetify #3861](https://github.com/spicetify/cli/issues/3861)). RD-147.
- **Reapply latency, Verified.** A logon trigger with a 30-minute repetition leaves a client unpatched for up to half an hour after an update slips through; Task Scheduler power flags are already correct (`src/powershell/gui/lane-functions.ps1:130-175`). RD-149.
- **PowerShell 5.1 parse-time execution, Verified as mitigated.** All `Invoke-WebRequest` calls in `LibreSpot.ps1` use `-UseBasicParsing` or `-OutFile`, and the script comments on the KB5074204 behaviour (`LibreSpot.ps1:7960`). No item.
- **Privacy posture, Verified.** Local only, no telemetry, redacted bundles, data inventory (`schemas/data-inventory.json`). Every adjacent product that added cloud sync also acquired a "sync wiped my config" failure mode; the rejection stands.

## Architecture Assessment

- **Release-time ownership of run-time downloads.** Whatever the installer fetches after the release is published must be frozen at tag time. The custom-app catalog needs a per-version URL (`https://github.com/SysAdminDoc/LibreSpot/releases/download/v{version}/librespot-engine.zip` or the embedded resource), the release manifest should list the archive, and a Core test should fail when any catalog entry's URL contains `/main/` (`src/powershell/data/CommunityCustomApps.ps1`; `src/LibreSpot.Core/AppCatalog.cs`; `tests/LibreSpot.Desktop.Tests/CommunityAssetsManifestTests.cs`).
- **Catalog verification date must move with the pin.** `schemas/community-assets.json` records `supportState` and `lastVerifiedDate` per asset; every date is 2026-06-15 or earlier while the Spotify pin moved to 1.2.93 on 2026-07-04. A validate-time gate should refuse a pin advance when any active asset's verification predates the pinned Spotify release, and the catalog page should show the date.
- **Publish belongs in `Build-Scripts.ps1`.** The script already compiles the PS2EXE artifact, generates the SBOM, and measures the footprint. Adding `-PublishRelease` that runs `dotnet publish` for both projects with recorded properties closes the last undocumented step and lets the manifest record them (`Build-Scripts.ps1:27-59`; `schemas/publish-footprint-budget.json`).
- **Single-file compression is an unrecorded decision.** The budget file documents trimming, ReadyToRun, self-contained, single-file, and native extraction, but not `EnableCompressionInSingleFile`; the desktop executable sits at 175.7 MiB against a 180 MiB warning line. Cold-start metrics are recorded as never measured with a rationale that still mentions GitHub Actions runners ([single-file docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)).
- **The watcher can react instead of poll.** A second trigger on a change to `%APPDATA%\Spotify\Spotify.exe` or `%LOCALAPPDATA%\Spotify\Update` (Task Scheduler cannot watch files, so this is a lightweight logon-started watcher process or a `FileSystemWatcher` in the existing task body) keeps the 30-minute poll as a backstop. RD-23's live verification blocker still applies to the scheduler path, so the new trigger needs a Pester test that simulates the version change without the scheduler.
- **The legacy shell still ships.** About 1,600 lines of collapsed XAML, two orphan smoke states (`provenance`, `global-search`), and live view-model members wait on the P0 decision in `Roadmap_Blocked.md`. Until then, changelog and README text must not describe those paths as reachable.
- **Accessibility gates exist; automated rule scanning does not.** 341 `AutomationProperties` uses, contract tests, offscreen captures, and a 116-row matrix are strong. Axe.Windows can scan the same offscreen shell for UIA rule violations and fail on new ones ([axe-windows](https://github.com/microsoft/axe-windows)). WCAG2ICT applies 2.4.11 (focus not obscured) to desktop software, which matters with the new sticky Settings footer ([WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/)).
- **Smaller items and where their evidence sits.** RD-150 (per-flag revert) comes from the Windhawk 2.0 settings pattern and the single reset action in `src/LibreSpot.App/src/panels/features.ts:300-307`; RD-155 (asset digest beside the update link) from GitHub's per-asset digests; RD-156 (opt-in Triage minidumps) from the runtime dump variables and the log-only `CrashReporter.cs`; RD-157 (decoy repositories) from the Competitive Landscape; RD-158 (Web API client-ID audit) from Spotify's 2026-02-06 developer-access change; RD-159 (safe-mode launch) from BetterDiscord's most-requested recovery feature; RD-160 (asset-cache bundle) from the SpotX download blocks and Chocolatey's internalizer.
- **Test and documentation gaps.** No JavaScript dependency audit; no test that README's uninstaller phase count matches the script; the release procedure lacks a literal publish command; `docs/prompts/` is an empty tracked directory; `THIRD_PARTY_NOTICES.md` for the engine records a GPL-3.0 versus MIT mismatch for `spicetify-hide-podcasts` that should be resolved with the upstream author or by dropping the pattern (`src/LibreSpot.App/THIRD_PARTY_NOTICES.md:12`).

## Rejected Ideas

- **Adopt Spicetify v3 or prototype the engine as a v3 module now.** beta.11 has empty release notes, refuses clients older than 1.2.80, and its support file has not moved since 2026-07-21; the module model will replace custom apps, so the spike is worth doing only once a stable v3 exists ([releases](https://github.com/spicetify/cli/releases); [#3038](https://github.com/spicetify/cli/issues/3038)).
- **Advance the Spotify and SpotX pins from headlines.** SpotX targets 1.2.99 and Spicetify 2.44.0 declares 1.2.96 as its ceiling, so the next reviewed tuple is at most 1.2.96; the existing drift and security-policy gates own that move, and it needs live validation the way 1.2.93 got it (`Build-Scripts.ps1 -CheckSpotifyVersionDrift`; `src/powershell/shared/Test-SpotXPinAdvanceSecurityPolicy.ps1`).
- **Copy Marketplace IndexedDB files.** Still unproven; the blocked entry stands. The new fact is that Marketplace's own Backup modal exports a JSON file, which RD-147 uses instead ([BackupModal](https://github.com/spicetify/marketplace/blob/main/src/components/Modals/BackupModal/index.tsx)).
- **Self-updating executable, Velopack, NetSparkle.** The notice ships; Velopack has a blocked design entry; NetSparkle's Ed25519 app cast is the better fit for an unsigned app if that decision ever reopens, and that is a note for the blocked entry, not a roadmap item ([NetSparkle](https://github.com/NetSparkleUpdater/NetSparkle)).
- **Windhawk or CEF hooks for native frames and transparency.** Same rejection as 2026-09-01; WMPotify proves it works and proves the maintenance cost ([WMPotify](https://github.com/Ingan121/WMPotify)).
- **Move custom exit codes into 69000 to 69999.** PSADT's convention is sound, but the published contract and every Intune example use 10 to 60; the break is not worth it (`schemas/fleet-exit-codes.json`).
- **Cloud sync, share codes through a relay, telemetry, analytics.** All conflict with local-only state; every adjacent product that added them acquired a wipe or privacy failure ([Vivaldi forum, Stylus sync wipe](https://forum.vivaldi.net/topic/83187/sync-wiped-out-all-my-stylus-csses/7)).
- **Alternative client or WebView2 wrapper.** Widevine and the DRM licence path make it dead on arrival (vault note "Spotify Customization & Patching 2026-09-01").
- **Chase server-side features (lossless, Jam, Mix, Smart Shuffle).** Server gated; SpotX says so itself ([#775](https://github.com/SpotX-Official/SpotX/discussions/775)).
- **TypeScript 7, Vitest 5, pnpm 12, Pester 6 upgrades.** TS 7 has no stable programmatic API for typescript-eslint until 7.1, Vitest 5 is a release candidate, pnpm 12 rewrites the lockfile, Pester 5.9.1 is still serviced; none fixes a defect here ([TS 7](https://github.com/microsoft/TypeScript/releases/tag/v7.0.2); [pnpm 12](https://github.com/pnpm/pnpm/releases/tag/v12.0.0)).
- **Port to macOS, Linux, Android.** Same as before; SpotX-Bash and the Android requests are real demand for a different product ([spicetify #3741](https://github.com/spicetify/cli/issues/3741)).

## Sources

### Repository and releases

- https://github.com/SysAdminDoc/LibreSpot
- https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.1.2
- https://github.com/SysAdminDoc/LibreSpot/issues?q=is%3Aissue
- https://github.com/SysAdminDoc/LibreSpot/issues/5
- https://github.com/SysAdminDoc/LibreSpot/discussions/21

### Upstream projects

- https://github.com/spicetify/cli/releases/tag/v2.44.0
- https://github.com/spicetify/cli/releases
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/cli/issues/3861
- https://github.com/spicetify/cli/issues/3869
- https://github.com/spicetify/cli/issues/3873
- https://github.com/spicetify/cli/issues/3816
- https://github.com/spicetify/cli/issues/3914
- https://github.com/spicetify/classmaps
- https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json
- https://github.com/spicetify/marketplace/releases
- https://github.com/spicetify/marketplace/issues/1201
- https://github.com/spicetify/marketplace/issues/1194
- https://github.com/spicetify/marketplace/blob/main/src/components/Modals/BackupModal/index.tsx
- https://github.com/SpotX-Official/SpotX
- https://github.com/SpotX-Official/SpotX/releases
- https://github.com/SpotX-Official/SpotX/commit/1d19a68f58abbb298d8095c310aa81e45055c833
- https://github.com/SpotX-Official/SpotX/issues/876
- https://github.com/SpotX-Official/SpotX/issues/888
- https://github.com/SpotX-Official/SpotX/issues/836
- https://github.com/SpotX-Official/SpotX/issues/829
- https://github.com/SpotX-Official/SpotX/discussions/873
- https://github.com/SpotX-Official/SpotX/discussions/879
- https://spotify.en.uptodown.com/windows/versions
- https://spicetify.app/docs/faq

### Competitors and adjacent products

- https://github.com/thomas-quant/BlockTheSpot-Resilient
- https://github.com/Nuzair46/BlockTheSpot-Installer/releases/tag/v0.3.0
- https://github.com/mrpond/BlockTheSpot
- https://github.com/Ingan121/WMPotify
- https://github.com/ohitstom/spicetify-easyinstall/releases/tag/3.7
- https://github.com/Israleche/SpicetifyManager
- https://github.com/sanoojes/spicetify-lucid
- https://github.com/Comfy-Themes/Spicetify/issues/256
- https://github.com/Xndr2/listening-stats
- https://github.com/wSoltani/syncify
- https://github.com/BetterDiscord/Installer
- https://github.com/BetterDiscord/Installer/releases/tag/v2.0.0
- https://github.com/Vencord/Vesktop/releases/tag/v1.6.7
- https://github.com/ReVanced/revanced-manager/releases
- https://github.com/ramensoftware/windhawk/releases
- https://github.com/Nexus-Mods/Vortex/releases/tag/v2.6.0
- https://github.com/ebkr/r2modmanPlus/blob/develop/src/model/exports/ExportMod.ts
- https://github.com/PrismLauncher/PrismLauncher/releases/tag/11.0.3

### Community signal

- https://www.reddit.com/r/spicetify/comments/1p29rqh/
- https://www.reddit.com/r/spicetify/comments/1umtw10/
- https://www.reddit.com/r/spicetify/comments/1sot0tx/
- https://www.reddit.com/r/spicetify/comments/1vvklzn/
- https://www.reddit.com/r/spicetify/comments/1vsllxg/
- https://www.reddit.com/r/spicetify/comments/1uk6vs7/
- https://www.reddit.com/r/Piracy/comments/1qjpwmg/
- https://github.com/Rafiuth/Soggfy/issues/159
- https://github.com/Rafiuth/Soggfy/issues/152
- https://community.spotify.com/t5/Live-Ideas/Desktop-An-option-to-disable-automatic-updates/idi-p/5208626
- https://www.spotify.com/us/legal/user-guidelines/plain/
- https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security

### Platform, security, and supply chain

- https://github.com/dotnet/announcements/issues/418
- https://github.com/dotnet/announcements/issues/420
- https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.11/10.0.11.md
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net100
- https://github.com/lepoco/wpfui/releases
- https://xunit.net/releases/v3/4.0.0
- https://registry.npmjs.org/spicetify-creator/latest
- https://github.com/vitest-dev/vitest/security/advisories
- https://github.com/vitejs/vite/issues/19428
- https://github.com/PowerShell/Announcements/issues/82
- https://support.microsoft.com/en-us/topic/kb5074204-security-update-for-windows-powershell-os-builds-26100-7392-and-26200-7392-05eec772-59fd-484d-a7e6-45e0a84580ab
- https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
- https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart
- https://github.blog/changelog/2025-10-28-immutable-releases-are-now-generally-available/
- https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/
- https://cli.github.com/manual/gh_attestation_verify
- https://github.com/dotnet/reproducible-builds
- https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview
- https://learn.microsoft.com/en-us/dotnet/core/diagnostics/collect-dumps-crash
- https://github.com/microsoft/axe-windows
- https://www.w3.org/TR/wcag2ict-22/
- https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api
- https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api
- https://github.com/NetSparkleUpdater/NetSparkle
- https://github.com/PSAppDeployToolkit/website/blob/main/docs/reference/exit-codes.mdx
- https://learn.microsoft.com/en-us/intune/intune-service/apps/apps-win32-add
- https://learn.microsoft.com/en-us/dotnet/communitytoolkit/windows/settingscontrols/settingsexpander

## Open Questions

- **Is the pinned Spotify 1.2.93 installer still downloadable through SpotX's chain, and for how long?** Spotify gated direct installer links with an `fauth` token in March 2026 and Cloudflare flags SpotX's worker. No LibreSpot gate probes the 1.2.93 source, and the answer decides whether the next tuple move is optional or urgent ([SpotX #829](https://github.com/SpotX-Official/SpotX/issues/829); [#836](https://github.com/SpotX-Official/SpotX/issues/836)).
- **When does the public release that fixes the engine pin ship?** RD-142 fixes future releases; v4.1.2 users can only be helped by publishing v4.2.0 (or a v4.1.3) promptly, which is the operator's release call.
- **Does the localStorage wipe reported upstream also hit `librespot:engine-state` on real machines?** The cause upstream is disputed (cleaners versus a Spicetify bug). A machine that reproduces it would decide whether RD-147's user-driven export is enough or an automatic guard is needed ([spicetify #3861](https://github.com/spicetify/cli/issues/3861)).
- **Will the ARM64 lane be built now that SpotX and Spicetify both support it?** The architecture support matrix entry in `Roadmap_Blocked.md` is the only blocker left; the technical one is gone ([SpotX #888](https://github.com/SpotX-Official/SpotX/issues/888)).
