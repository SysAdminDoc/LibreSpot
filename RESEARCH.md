# Research: LibreSpot

Date: 2026-09-04. Replaces all prior research.

## Executive Summary

LibreSpot v4.4.0 is the latest tagged release (2026-09-04) and a v4.5.0 bump is staged uncommitted in the working tree. It is a Windows WPF desktop app, a fleet CLI, and a composed PowerShell script that install a hash-pinned tuple of Spotify 1.2.93.667, SpotX commit `550bc72c`, Spicetify 2.44.0, and Marketplace 1.0.11, plus an AGPL live customization app that runs inside Spotify. Every research item from 2026-09-03 (RD-142 through RD-180) shipped, the tracker has zero open issues and zero pull requests, and every local gate was green at `5789733` (`CHANGELOG.md`; `CLAUDE.md`; [tracker](https://github.com/SysAdminDoc/LibreSpot/issues?q=is%3Aissue)).

The strongest current shape is the trust and recovery story: immutable releases with checksums, SBOM, manifest and attestation, journal-backed undo, DPAPI-sealed safe mode, verified offline cache bundles, and a 24-theme in-client Store. The highest-value direction now is durability at the edges that story has not reached: the in-Spotify engine deletes its own saved state on any parse failure, the auto-reapply watcher retries a failing reapply every 30 minutes forever, and the README documents eight of the fifteen exit codes the fleet schema defines. Upstream, the pinned tuple still works and still downloads (SpotX's worker served the 1.2.93 installer on 2026-09-04), but Spicetify v2 is frozen at 2.44.0 while v3 betas ship every few days with a live module store, and public Spotify is five minors ahead at 1.2.98.301 with 1.2.99 staged on the CDN.

| Rank | Opportunity | Timing | User impact | Effort | Confidence |
|---|---|---|---|---|---|
| 1 | RD-181: Quarantine unreadable engine state instead of deleting it | Now, P1 | 5/5 | S | Verified |
| 2 | RD-182: Hold the watcher after repeated reapply failures on one build | Now, P1 | 4/5 | M | Verified |
| 3 | RD-184: Document all fifteen fleet exit codes and gate the README table | Now, P2 | 3/5 | S | Verified |
| 4 | RD-183: Advance the SpotX pin for the manifest-aware mirror and download fallback | Next, P2 | 4/5 | L | Verified |
| 5 | RD-185: Show the pinned build's embedded Chromium version and age | Next, P2 | 3/5 | M | Verified |
| 6 | RD-186: One scrollbar in Settings, no clipped theme gallery | Next, P2 | 3/5 | M | Verified |
| 7 | RD-187: Pin the PS2EXE compiler version in the release build | Next, P2 | 2/5 | S | Verified |
| 8 | RD-188: Validate runtime outputs against the four untested schemas | Next, P2 | 3/5 | M | Verified |
| 9 | RD-189: Compute the next reviewable Spotify tuple from all three upstream sources | Next, P2 | 3/5 | M | Verified |
| 10 | RD-196: Reset Marketplace storage from the engine's Health panel with a pre-export | Later, P3 | 3/5 | M | Likely |

Direct dependencies are current: every NuGet pin equals the latest listed version on 2026-09-04 (WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, Serilog 4.4.0, FlaUI 5.0.0, Axe.Windows 2.4.2, xunit.v3 4.0.0, FsCheck.Xunit.v3 3.4.0), PSScriptAnalyzer 1.25.0 and Pester 5.9.1 are the latest of their lines, and pnpm 11.25.0 is still `latest` ([nuget.org](https://www.nuget.org/packages/CommunityToolkit.Mvvm); [PSGallery](https://www.powershellgallery.com/packages/PSScriptAnalyzer); [pnpm 12 blog](https://pnpm.io/blog/releases/12.0)). .NET 10.0.11 (2026-08-11) is the latest runtime; the next Patch Tuesday is 2026-09-08 ([release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)).

## Product Map

### Core workflows

- **Recommended setup.** One state-derived Home action installs the reviewed tuple, then opens Spotify (`src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`; `assets/screenshots/wpf-recommended.png`).
- **Settings and profiles.** Four essentials, eight disclosed groups, `.librespot` files, `librespot://` links, share cards (`src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml`; `src/LibreSpot.Desktop/Views/CustomAppearanceSection.xaml`).
- **Maintenance and recovery.** One safe repair first, diagnostics and reset behind disclosures, journal-backed undo, reversible safe mode, redacted support bundles with opt-in Triage dumps (`src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`; `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`; `src/LibreSpot.Core/SupportBundleService.cs`).
- **Live customization inside Spotify.** Store, Look, Tweaks, Features, Presets and Health panels at `/librespot/*`; state in `localStorage["librespot:engine-state"]` with schema version 1 (`src/LibreSpot.App/src/core/store.ts:8`; `src/LibreSpot.App/src/core/state.ts:3`).
- **Managed deployment.** `status`, `detect`, `validate`, `install`, `reapply`, `uninstall`, `repair`, `undo`, `plan`, `export-support`, `version`, `watcher`, `cache` verbs with JSON and NDJSON contracts (`src/LibreSpot.Cli/Program.cs:159-196`; `schemas/fleet-cli-contract.json`).

### Users

- A Windows user who wants one download and a client that survives Spotify updates.
- A customization user who is afraid of losing a saved look.
- An operator who needs unattended runs, receipts, exit codes and offline bundles.
- The maintainer, who keeps a four-part tuple honest with local gates.

### Platform and distribution

- Desktop and CLI are `net10.0-windows`, `win-x64`, self-contained single-file; ARM64 runs under emulation with a warning (`src/LibreSpot.Core/AppCatalog.cs:1173`). Releases are local builds uploaded as immutable GitHub releases with eight assets; 12 stars, 0 forks, and single-digit downloads per release ([releases](https://github.com/SysAdminDoc/LibreSpot/releases)). The name still collides with the Rust `librespot` library and LibreSpot has zero mentions on Reddit, Hacker News or Lobsters; the rebrand stays a blocked decision (`Roadmap_Blocked.md`).
- Version strings live in three `.csproj` files, `src/LibreSpot.App/package.json`, `LibreSpot.ps1:105` and the README badge; `-Validate` checks that they agree, but there is no single source of truth (`Directory.Build.props` carries no version).
- README "What's New" sections exist for v4.1.0 and v4.2.0 though no such tags exist; only v4.1.2, v4.3.0 and v4.4.0 were tagged after v4.0.0 (`git tag`; `README.md:107-160`).

### Upstream state on 2026-09-04

| Component | LibreSpot pin | Upstream now | Gap |
|---|---|---|---|
| Spotify (Windows) | 1.2.93.667 (libcef 146.0.10, Chromium 146.0.7680.179) | 1.2.98.301 public (2026-09-02); six 1.2.99 builds staged on the CDN up to 1.2.99.317 (2026-09-02) | Five to six minors; 1.2.98 restyled the playback timestamp spans and later builds put classes on `<body>` ([Uptodown](https://spotify.en.uptodown.com/windows/versions); [LoaderSpot manifest](https://raw.githubusercontent.com/LoaderSpot/table/refs/heads/main/table/versions.json); [spicetify-themes #1291](https://github.com/spicetify/spicetify-themes/pull/1291); [spicetify ffaa9ef6](https://github.com/spicetify/cli/commit/ffaa9ef6)) |
| SpotX | `550bc72c` (2026-07-06) | `9d344658` (2026-09-03): `-mirror` now rewrites the LoaderSpot manifest URL; `fd064f54` (2026-09-01) ARM binary patches; `-download_method curl\|webclient`; full-build `-v` skips the manifest | `patches.json` changes since the pin only cap entries at 1.2.93/1.2.94 or add `fr >= 1.2.94`, so patch behaviour on 1.2.93 is unchanged; the download plumbing is what moved ([commits](https://github.com/SpotX-Official/SpotX/commits/main); [#891](https://github.com/SpotX-Official/SpotX/issues/891)) |
| Spicetify | 2.44.0 (2026-07-04) | No v2 release since; v3.0.0-beta.12 (2026-09-03), Rust rewrite, refuses Spotify older than 1.2.80, module store live (`store@1.7.2`, `stdlib@1.10.3`), fourteen classic themes already published as modules, empty release notes, no stable date | `supported-versions.json` on `v3-beta` still ends at 1.2.94 (updated 2026-07-21); `main` has none ([releases](https://github.com/spicetify/cli/releases); [modules](https://github.com/spicetify/modules); [supported-versions](https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json)) |
| Classmaps | n/a | 1020097 inherits 1020096 (2026-08-24); no 1020098 or 1020099; `expose.json` (2026-09-03) makes v3 exposure regexes data-driven | The highest build covered by SpotX, a classmap and Spicetify's declared range is 1.2.96 or 1.2.97 ([classmaps](https://github.com/spicetify/classmaps/commits/main)) |
| Marketplace | 1.0.11 | 1.0.11 is newest (2026-09-02); #1231 (2026-09-02) shows stale storage restoring themes after a full reinstall, closed not planned | None to advance; a storage reset is the remaining user need ([#1231](https://github.com/spicetify/marketplace/issues/1231)) |
| Community catalog | re-verified 2026-09-03 | Lucid fixed styles for 1.2.96 on 2026-08-29; Comfy last pushed 2026-01-04; Catppuccin 2025-10-22; Bloom 2025-05-20 | None at 1.2.93 ([Lucid](https://github.com/sanoojes/spicetify-lucid); [Comfy](https://github.com/Comfy-Themes/Spicetify)) |

### Integrations and data

- Every download is tied to a SHA256, an asset cache, quarantine and bounded extraction; SpotX's `-mirror` is passed on a classified download failure (`src/powershell/shared/Module-InstallSpotX.ps1:40-60`).
- The 1.2.93 installer is downloadable today: a range request to SpotX's worker returned HTTP 206 for `spotify_installer-1.2.93.667.g7b5cc0ce-x64.exe` on 2026-09-04, and no `fauth` token exists in the chain. A SpotX #836 comment the same morning got a 403 on the same worker from another region, so the failure is regional Cloudflare classification, not a dead source ([#836](https://github.com/SpotX-Official/SpotX/issues/836)).
- The auto-reapply task is a logon trigger repeating every 30 minutes plus the event trigger RD-149 added; on a failed reapply the watcher keeps `LastKnownVersion` and retries every tick with no hold (`LibreSpot.ps1:667-785`; `LibreSpot.ps1:971-1030`).
- Engine state is loaded with `parseProfile`; any exception, including a schema-version mismatch or corrupt JSON, removes the key and returns defaults, with no copy kept (`src/LibreSpot.App/src/core/store.ts:34-45`; `src/LibreSpot.App/src/core/profile.ts:59-63`). The desktop config has a real 1 to 2 migration (`src/powershell/shared/Normalize-LibreSpotConfig.ps1:136`); the engine has none.

## Competitive Landscape

- **[Spicetify v3 beta](https://github.com/spicetify/cli/tree/v3-beta) and the [module vault](https://github.com/spicetify/modules).** The direction of the whole ecosystem: modules replace custom apps, a daemon repairs an unapplied client in the background, an `update_policy` of `gate|block|allow` "remembers the block so a Spotify update cannot erase it" (2026-08-24), checksummed self-update, and side-by-side module versions for rollback. Learn: the hold-and-report behaviour, and the fact that fourteen of LibreSpot's official themes already exist as v3 modules. Avoid: switching before there are release notes, a stable tag and a supported-versions file past 1.2.94.
- **[SpotX](https://github.com/SpotX-Official/SpotX).** Ships the same patch set on 1.2.93 as the pin, but its download chain gained a manifest-aware mirror, curl and WebClient retries and a full-build `-v` form after regional blocks (#891, #836). Learn: expose those escape hatches by advancing the pin. Avoid: its self-added Defender exclusions; `Test-SpotXPinAdvanceSecurityPolicy` already enforces that boundary.
- **[SpiceManager](https://github.com/EliasOnsihuay/SpiceManager) (Rust/Tauri, 2026-08-12).** Has a "compatibility hold mode" that stores the last-known-good Spotify plus Spicetify state and stops re-running `apply` after repeated failures. Learn: that exact behaviour for the watcher (RD-182). Avoid: its self-updater; LibreSpot's update notice is deliberate.
- **[interceptify](https://github.com/mattebin/interceptify) (2026-08-05).** Unelevated tray ad-blocker with self-heal tasks at logon and twice daily, a status dot inside Spotify, and a "corroboration guard" so short real tracks are never blocked. LibreSpot already runs unelevated, at logon, every 30 minutes and on the update event; nothing to copy.
- **[spicetify-pm](https://github.com/KamilWachnicki/spicetify-pm) 0.4.0 (2026-08-23).** Lockfile that restores an exact Marketplace set anywhere, with local-drift detection. LibreSpot's profiles and `validate` verb cover the managed set; the unmanaged Marketplace set stays a blocked decision.
- **[Dalbouh02/SpicetifyManager](https://github.com/Dalbouh02/SpicetifyManager) (2026-08-26), [FIREPAWER07/SpicetifyInstaller](https://github.com/FIREPAWER07/SpicetifyInstaller) 2.0.5 (2026-08-25), [itourboy-OG/Spicetify-Manager](https://github.com/itourboy-OG/Spicetify-Manager) 2.4.0.** Four GUI managers appeared or updated between July and August 2026. None has health checks, undo, offline bundles or a fleet lane; the demand for a GUI is real and LibreSpot already exceeds them. Avoid: nothing to copy beyond itourboy's Large Text mode, which WPF already provides through system scaling.
- **[spotivoid](https://github.com/qiraxyz/spotivoid) (2026-08-18).** C++ patcher that NOPs the native ad-slot branch and refuses unknown builds by exact bytes. Learn: the refuse-unknown-build posture, which SpotX's binary patches and LibreSpot's pin already embody. Avoid: a second native patcher.
- **[Vortex 2.6](https://github.com/Nexus-Mods/Vortex/releases) (2026-08-25).** Health Check that tells the user when a service is down "instead of pretending everything's normal." LibreSpot's release notice reports 403 and 429 (RD-176) and drift lookups have `offline` and `unavailable` states (`src/LibreSpot.Core/UpstreamDriftService.cs:36-43`); already covered.
- **[Windhawk 2.0 alpha.3](https://github.com/ramensoftware/windhawk/releases) (2026-08-07).** Per-row non-default marker and revert; shipped in LibreSpot as RD-150 (`fd6217f`).
- **[r2modmanPlus 3.2.19](https://github.com/ebkr/r2modmanPlus/releases) and [PrismLauncher 11.1.0](https://github.com/PrismLauncher/PrismLauncher/releases).** Both added "package of concern" warnings in August 2026. LibreSpot's remote-loader policy (RD-169) is the same idea for Beautiful Lyrics.
- **Native clients ([fastpotify](https://github.com/crmne/fastpotify), 2,900 stars in a month).** "Leave the official client" is the other answer to ads. Rejected again: Widevine and the licence path.
- **Decoys.** `NeedChandlerMonitor/spicetify-elite` (231 stars, off-GitHub download) is still up and pushed 2026-09-03; `demonclimberrouse/spotx-works` (created 2026-08-06, 42 stars) is new; `spotify-adblock-studio` is gone. RD-157 shipped the README lookalike section; keep it current.

## Reported Issues

The tracker has zero open issues and zero open pull requests. The one new closed issue since 2026-09-03 is #22 "Make LibreSpot the in-client store", owner-filed and closed with v4.3.0 on 2026-09-04. Discussions #20 and #21 have no replies. Fourteen-day traffic is 27 views from 18 visitors; the 377-clone spike on 2026-08-31 has no matching views and reads as crawler traffic ([issue #22](https://github.com/SysAdminDoc/LibreSpot/issues/22); `gh api traffic`). An empty tracker at this adoption is not evidence of an absence of problems.

Defects found in this pass by reading the code, the release and the screenshots:

- **Engine state is deleted on any load failure, Verified.** `EngineStore.load` catches every exception from `parseProfile`, removes `librespot:engine-state` and returns defaults. A corrupt write, a future schema bump, or a profile shape change destroys the user's saved theme, tweaks and presets with nothing to recover (`src/LibreSpot.App/src/core/store.ts:34-45`; `src/LibreSpot.App/src/core/profile.ts:59-63`). This is the same wipe the Spicetify tracker fills up with, reproduced by LibreSpot's own code path ([spicetify #3861](https://github.com/spicetify/cli/issues/3861)). RD-181.
- **The watcher never holds, Verified.** After "Reapply failed" it keeps the old version so it retries on the next tick, forever, with no counter, no hold state and no Maintenance surface naming the failing step (`LibreSpot.ps1:1025-1030`). RD-182.
- **The README exit-code table is incomplete, Verified.** README lists 0, 1, 2, 10, 11, 12, 13 and 20; `schemas/fleet-exit-codes.json` also defines 30, 40, 50, 60, 1618, 3010 and 1641, including the retry and reboot classes Intune has to handle differently (`README.md:383-392`). RD-184.
- **Settings still has two scroll regions, Verified.** The theme gallery is capped at `MaxHeight="340"` with its own vertical scrollbar inside the page scroll, and the shipped screenshot shows the Prism card cut mid-content (`src/LibreSpot.Desktop/Views/CustomAppearanceSection.xaml:64-67`; `assets/screenshots/wpf-custom.png`; README:141 claims one scrollbar). RD-186.
- **The PS2EXE compiler is unpinned, Verified.** `-CompileStableExe` runs `Import-Module ps2exe -ErrorAction Stop` with no version, so the bytes of the `LibreSpot.exe` release asset depend on whatever module version the build machine holds; 1.0.18 (2026-06-07) changed behaviour by adding `$ScriptRoot` (`Build-Scripts.ps1:1971`; [PSGallery](https://www.powershellgallery.com/packages/ps2exe)). RD-187.
- **README counts drifted, Verified.** "16 extensions" (README:304) against ten built-in plus five community (`LibreSpot.ps1:1435`, `:1452`, README:438); "45 tests" for the Spotify surface (README:157) against 62 `it(` blocks in `src/LibreSpot.App/tests`; a `gh release verify v4.1.2` example two releases old (README:616); "eight assets" (README:594) beside "six contract-covered artifacts" (README:675). RD-190.
- **Four schemas have no test that reads them by name, Verified.** `asset-cache-bundle.json`, `ndjson-log-format.json`, `operation-token-types.json` and `run-receipt-format.json` are user-data and fleet contracts; the last two are embedded into Core and consumed by undo (`src/LibreSpot.Core/LibreSpot.Core.csproj:30-31`). RD-188.
- **Failure-path writes are silenced, Verified.** A run-receipt write failure is only WARN-logged, so a run that mutated the machine can leave the undo surface empty (`src/powershell/shared/Complete-OperationJournalRun.ps1:63-66`); a failed config rollback moves the rescue file back with `-ErrorAction SilentlyContinue` (`src/powershell/shared/Install-LibreSpotStagedConfig.ps1:23`); the undo service's failure-path journal and receipt writes are each `catch { }` (`src/LibreSpot.Core/OperationJournalUndoService.cs:345-346`). RD-191.
- **Culture-sensitive timestamps in diagnostics, Verified.** `ToString("yyyy-MM-dd HH:mm")` with no culture at `src/LibreSpot.Core/AppCatalog.cs:693` and `src/LibreSpot.Core/EnvironmentSnapshotService.cs:2504`; local-time crash filenames at `src/LibreSpot.Desktop/Services/CrashReporter.cs:171`. RD-192.
- **`design-qa.md` was tracked while ignored, Verified; fixed 2026-09-04.** It matched `.gitignore:34` and sat outside the root document set `AGENTS.md` permits. RD-194 untracked it and added a gate over the root document set.

Claims from the reconnaissance that did not survive verification: the profile activation lock has a 30-second deadline (`src/powershell/shared/Enter-LibreSpotProfileActivationLock.ps1:6`); the SpotX retry loop is bounded by its attempt counter (`src/powershell/shared/Module-InstallSpotX.ps1:44-60`); the PATH undo temp name carries a GUID (`src/powershell/shared/Set-PathEntries.ps1:69-70`); the config schema 1 to 2 migration exists (`Normalize-LibreSpotConfig.ps1:136`); the undo Preview and Execute buttons get their UIA name from string `Content` (`src/LibreSpot.Desktop/MainWindow.xaml:3596-3604`).

What the wider community reports since 2026-08-15, ranked by frequency, with the LibreSpot answer:

1. **A Spotify update breaks the stack and users reinstall from scratch** ([r/spicetify 1vw95y7](https://reddit.com/r/spicetify/comments/1vw95y7/); [1vjr1of](https://reddit.com/r/spicetify/comments/1vjr1of/)). Update blocking, the 30-minute poll, the RD-149 event trigger and safe mode answer it; the missing piece is a hold that says which build failed (RD-182).
2. **Ban fear** ([r/spicetify 1vsllxg](https://reddit.com/r/spicetify/comments/1vsllxg/), 11 points, 10 comments). No sourced 2026 report of desktop-mod account action exists; the last SpotX ban issue is from January 2024. README's trust disclosure covers account risk; no new item.
3. **Ads escalating on Free** ([r/truespotify 1vz34eu](https://reddit.com/r/truespotify/comments/1vz34eu/), 228 points). Demand signal only.
4. **Which tool on Windows** ([r/Piracy 1viok7d](https://reddit.com/r/Piracy/comments/1viok7d/), 1,408 points, 138 comments): Spicetify 17 mentions, SpotX 13, LibreSpot 0. Discovery is the rebrand decision, not a code item.
5. **Lyrics extensions with heavy API use** ([r/spicetify 1w3vcfl](https://reddit.com/r/spicetify/comments/1w3vcfl/)). Beautiful Lyrics is already opt-in with a third-party-service label (RD-169); the specific extension in the thread was not named in a fetchable form (Open Questions).
6. **Stale Marketplace storage** ([marketplace #1231](https://github.com/spicetify/marketplace/issues/1231)). RD-196.
7. **Regional download blocks** ([SpotX #836](https://github.com/SpotX-Official/SpotX/issues/836); [#891](https://github.com/SpotX-Official/SpotX/issues/891)). RD-183.
8. **Data loss in the official client** ([r/Piracy "240GB of downloads gone"](https://reddit.com/r/Piracy/), 1,471 points). Fuel for the backup story; no item.

## Security, Privacy, and Reliability

- **Pinning keeps users on an old browser engine, Verified.** Spotify 1.2.93.667 on this machine ships `libcef.dll` 146.0.10 with Chromium 146.0.7680.179; Chrome 152.0.7977.82 (2026-09-04) fixes CVE-2026-85046, a V8 bug with an exploit in the wild, and CEF does not backport. Nothing in the health model or README says which Chromium a pinned build carries ([The Hacker News, 2026-09-04](https://thehackernews.com/2026/09/google-releases-chrome-update-to-patch.html); `grep -ri libcef src/` returns nothing). RD-185 is disclosure, not a fix; the fix is the tuple advance the drift gates already govern.
- **Runtime servicing, Verified.** .NET 10.0.11 (2026-08-11) carries ten CVE fixes and is current; 10.0.12 is expected 2026-09-08 and needs a rebuild plus a matching `Microsoft.NET.ILLink.Tasks` bump ([releases.json](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json); `CLAUDE.md` locked-restore note).
- **npm supply chain, Verified.** The 2026-08-04 CHAINDROP wave compromised `keyv` 6.0.0, `flat-cache` 6.1.24 and `file-entry-cache` 11.1.6 with `preinstall` payloads; the lockfile pins `keyv` 4.5.4, `flat-cache` 4.0.1 and `file-entry-cache` 8.0.0, and pnpm 11's build allowlist names only esbuild and Parcel's watcher, so install scripts cannot run ([Elastic](https://www.elastic.co/security-labs/shai-hulud-chaindrop-npm-supply-chain); `CLAUDE.md` 2026-09-04 note). No test asserts the allowlist stays exact (RD-197).
- **PS2EXE reproducibility, Verified.** Unpinned module version (RD-187). AV false positives on PS2EXE output remain a standing upstream issue; the README's detection FAQ already refuses to call a detection harmless ([PS2EXE](https://github.com/MScholtes/PS2EXE)).
- **Application control copy, Verified.** KB5079391 (2026-03-27) lets Windows 11 24H2 and 25H2 users turn Smart App Control off without a clean install; the README FAQ still says to "run LibreSpot on a device where Smart App Control is off or still in evaluation mode" without naming that path (`README.md:553-556`; [BleepingComputer](https://www.bleepingcomputer.com/news/microsoft/windows-11-kb5079391-update-rolls-out-smart-app-control-improvements/)). RD-195.
- **Windows 10 lifecycle, Verified.** Consumer ESU now runs to 2027-10-12 at no cost with settings sync or 1,000 Rewards points ([BleepingComputer, 2026-06-25](https://www.bleepingcomputer.com/news/microsoft/microsoft-quietly-extends-free-windows-10-esu-support-to-october-2027/)). Recorded as a note on the blocked support-lifecycle item.
- **State durability, Verified.** The engine wipe (RD-181), the watcher retry loop (RD-182) and the silenced receipt write (RD-191) are the three places a user can lose something without being told.
- **Privacy posture, Verified.** Local only, no telemetry, DPAPI-sealed safe mode, opt-in dumps excluded from bundles by default (`schemas/data-inventory.json`; `CLAUDE.md`). Unchanged.

## Architecture Assessment

- **The worker export list is the load-bearing invariant.** Three of the last six commits fixed functions missing from `$functionNamesForWorker`; the list is a hand-maintained string array and the closure test now guards it with an empty baseline (`LibreSpot.ps1:12158-12196`; `tests/powershell/LibreSpot.Tests.ps1` "Worker runspace function closure"). Any new shared function that the install or maintenance path calls must be added there first.
- **Engine state needs a migration seam.** `PROFILE_SCHEMA_VERSION` is 1 with no upgrade path and a delete-on-mismatch loader; the desktop lane already has the pattern (`Normalize-LibreSpotConfig.ps1`). RD-181 adds quarantine now; a version-2 migrator is the follow-on when the schema first moves.
- **The watcher has no memory of failure.** `Get-WatcherState` records `LastOutcome` as a string; a hold needs a failure count keyed by Spotify version and a surface in `Update-AutoReapplyStatusLabel` (`LibreSpot.ps1:538-560`, `:7254`). RD-182.
- **The legacy shell still ships and still binds.** The collapsed `TabControl` at `src/LibreSpot.Desktop/MainWindow.xaml:2168-2192` hosts the same three workspace types as the live shell, so the selected legacy view is constructed and bound on every launch with duplicate automation ids. This is the P0 decision in `Roadmap_Blocked.md`; nothing here changes it.
- **Core is tested through Desktop.** `tests/LibreSpot.Core.Tests` has 14 tests in two files; the other ~900 reach Core through the Desktop project. The Stryker pilot's 24.32% baseline reflects that. Not a roadmap item on its own; RD-188 adds Core-level schema tests where the contracts live.
- **Next reviewable tuple.** Three sources bound the next pin: SpotX `patches.json` (1.2.99), a published classmap (1020097 by inheritance) and Spicetify's declared range (1.2.96). `-CheckSpotifyVersionDrift` reports drift but does not compute that bound (`Build-Scripts.ps1`; `src/LibreSpot.Core/UpstreamDriftService.cs`). RD-189.
- **Test and documentation gaps.** README exit table (RD-184); README counts (RD-190); four unreferenced schemas (RD-188); `docs/backend-event-protocol.json` is not linked from README though `BackendEventProtocolTests.cs` covers it; `design-qa.md` tracked while ignored (RD-194); `docs/archive/research/RESEARCH.md` is a second research file that `.gitignore:35` half-ignores.
- **Screens inspected.** The four WPF captures and seven in-Spotify captures render cleanly in dark; contrast, type scale and hard-coded colours are gated by tests (`ColorLintTests.cs`; `WpfTypography` gate). The only visible defect is the clipped theme gallery (RD-186). Eight fixed-width `TextBlock`s without `TextTrimming` are a clipping risk under Russian and Portuguese expansion (`MaintenanceWorkspaceView.xaml:240,524,603`; `RecommendedWorkspaceView.xaml:370`; `CustomWorkspaceView.xaml:54`); folded into RD-186's acceptance.

## Rejected Ideas

- **Adopt Spicetify v3 or ship the engine as a v3 module now.** beta.12 (2026-09-03) still has empty release notes, no stable date, a supported-versions file ending at 1.2.94, and Comfy, Lucid, Catppuccin and Bloom are absent from the vault. The blocked RD-41 residual covers runtime detection ([releases](https://github.com/spicetify/cli/releases); [supported-versions](https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json)).
- **Advance the Spotify pin from headlines.** 1.2.98 breaks Text's progress bar and later builds changed `<body>`; the next reviewed tuple is bounded at 1.2.96 or 1.2.97 by the three sources above, and needs the live validation 1.2.93 got. RD-189 makes the bound visible; the move itself stays with the drift and security-policy gates ([spicetify-themes #1290](https://github.com/spicetify/spicetify-themes/issues/1290)).
- **Vitest 5.0.0 (2026-09-03), TypeScript 7, pnpm 12, Pester 6, FlaUI 6, MTP 2.4.0.** Vitest 5 flips `clearMocks` and moves reporter output under `.vitest/`; TypeScript 7 has no programmatic API until 7.1, which typescript-eslint needs; pnpm 12 errors on unknown workspace keys; Pester 5.9.1 is still serviced; FlaUI 6.0.0 is in the changelog but not on NuGet; xunit.v3 4.0.0 targets MTP 2.3.3. None fixes a defect here ([Vitest 5](https://vitest.dev/blog/vitest-5.html); [TypeScript 7](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/); [pnpm 12](https://pnpm.io/blog/releases/12.0); [FlaUI changelog](https://github.com/FlaUI/FlaUI/blob/main/CHANGELOG.md); [MTP changelog](https://github.com/microsoft/testfx/blob/main/docs/Changelog-Platform.md)).
- **cosign keyless bundles for local builds.** `gh attestation verify` expects the Actions OIDC issuer and repository certificate extensions, so a personal-identity bundle would not pass it; immutable releases already produce a Sigstore-verifiable release attestation that `gh release verify` accepts ([attestation REST](https://docs.github.com/en/rest/repos/attestations); [gh attestation verify](https://cli.github.com/manual/gh_attestation_verify); `README.md:616`).
- **Lockfile-style export of the unmanaged Marketplace set, self-updater, compatibility hold as a separate mode switch, self-heal schedules.** Profiles plus `validate` cover the managed set; the Marketplace boundary is a blocked policy item; the update notice is deliberate; the watcher already runs at logon, every 30 minutes and on the update event ([spice-pm](https://github.com/KamilWachnicki/spicetify-pm); [interceptify](https://github.com/mattebin/interceptify)).
- **Native ad-slot patcher, Windhawk or CEF hooks, native clients, WebView2 wrapper.** SpotX's binary patches already cover the ad slot; the rest are the same rejections as 2026-09-03 ([spotivoid](https://github.com/qiraxyz/spotivoid); [WMPotify](https://github.com/Ingan121/WMPotify); [fastpotify](https://github.com/crmne/fastpotify)).
- **A "which tool" or "will I be banned" marketing section.** The FAQ already answers Premium use and links account risk from the trust disclosure; the discovery problem is the blocked rebrand ([r/Piracy 1viok7d](https://reddit.com/r/Piracy/comments/1viok7d/)).
- **Cloud sync, share relays, telemetry, ports to other platforms, server-side features, exit codes in 69000 to 69999.** Unchanged from 2026-09-03.

## Sources

### Repository and releases

- https://github.com/SysAdminDoc/LibreSpot
- https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.4.0
- https://github.com/SysAdminDoc/LibreSpot/issues/22
- https://github.com/SysAdminDoc/LibreSpot/issues?q=is%3Aissue

### Upstream projects

- https://spotify.en.uptodown.com/windows/versions
- https://raw.githubusercontent.com/LoaderSpot/table/refs/heads/main/table/versions.json
- https://raw.githubusercontent.com/SpotX-Official/SpotX/main/run.ps1
- https://github.com/SpotX-Official/SpotX/commits/main
- https://github.com/SpotX-Official/SpotX/compare/550bc72c...main
- https://github.com/SpotX-Official/SpotX/issues/836
- https://github.com/SpotX-Official/SpotX/issues/888
- https://github.com/SpotX-Official/SpotX/issues/891
- https://github.com/spicetify/cli/releases
- https://github.com/spicetify/cli/tree/v3-beta
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/cli/commit/ffaa9ef6
- https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json
- https://github.com/spicetify/modules
- https://github.com/spicetify/classmaps/commits/main
- https://github.com/spicetify/marketplace/releases/tag/v1.0.11
- https://github.com/spicetify/marketplace/issues/1231
- https://github.com/spicetify/spicetify-themes/issues/1290
- https://github.com/spicetify/spicetify-themes/pull/1291
- https://github.com/sanoojes/spicetify-lucid
- https://github.com/thomas-quant/BlockTheSpot-Resilient
- https://github.com/Ingan121/WMPotify

### Competitors and adjacent products

- https://github.com/EliasOnsihuay/SpiceManager
- https://github.com/mattebin/interceptify
- https://github.com/KamilWachnicki/spicetify-pm
- https://github.com/Dalbouh02/SpicetifyManager
- https://github.com/FIREPAWER07/SpicetifyInstaller
- https://github.com/itourboy-OG/Spicetify-Manager
- https://github.com/qiraxyz/spotivoid
- https://github.com/crmne/fastpotify
- https://github.com/ramensoftware/windhawk/releases
- https://github.com/Nexus-Mods/Vortex/releases
- https://github.com/BetterDiscord/Installer/releases
- https://github.com/Vencord/Vesktop/releases
- https://github.com/ebkr/r2modmanPlus/releases
- https://github.com/PrismLauncher/PrismLauncher/releases
- https://github.com/NeedChandlerMonitor/spicetify-elite

### Community signal

- https://reddit.com/r/spicetify/comments/1vw95y7/
- https://reddit.com/r/spicetify/comments/1vjr1of/
- https://reddit.com/r/spicetify/comments/1vsllxg/
- https://reddit.com/r/spicetify/comments/1w3vcfl/
- https://reddit.com/r/truespotify/comments/1vz34eu/
- https://reddit.com/r/Piracy/comments/1viok7d/
- https://news.ycombinator.com/item?id=47428391
- https://www.spotify.com/us/legal/user-guidelines/
- https://musictech.com/news/industry/spotify-premium-price-hike-2026/

### Platform, security, and supply chain

- https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net110
- https://github.com/lepoco/wpfui/releases
- https://xunit.net/releases/v3/4.0.0
- https://github.com/microsoft/testfx/blob/main/docs/Changelog-Platform.md
- https://github.com/FlaUI/FlaUI/blob/main/CHANGELOG.md
- https://www.powershellgallery.com/packages/ps2exe
- https://github.com/MScholtes/PS2EXE
- https://www.powershellgallery.com/packages/PSScriptAnalyzer
- https://github.com/pester/Pester/releases
- https://vitest.dev/blog/vitest-5.html
- https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/
- https://pnpm.io/blog/releases/12.0
- https://github.com/advisories/GHSA-g7r4-m6w7-qqqr
- https://www.elastic.co/security-labs/shai-hulud-chaindrop-npm-supply-chain
- https://nodejs.org/en/blog/vulnerability/july-2026-security-releases
- https://github.com/PowerShell/PowerShell/releases/tag/v7.6.5
- https://thehackernews.com/2026/09/google-releases-chrome-update-to-patch.html
- https://www.bleepingcomputer.com/news/microsoft/windows-11-kb5079391-update-rolls-out-smart-app-control-improvements/
- https://www.bleepingcomputer.com/news/microsoft/microsoft-quietly-extends-free-windows-10-esu-support-to-october-2027/
- https://learn.microsoft.com/en-us/windows/security/application-security/application-control/administrator-protection
- https://docs.github.com/en/rest/repos/attestations
- https://cli.github.com/manual/gh_attestation_verify
- https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api
- https://www.w3.org/TR/wcag2ict-22/
- https://github.com/microsoft/axe-windows/blob/main/docs/RulesDescription.md

## Open Questions

- **Which lyrics extension does the 2026-09-01 r/spicetify API-abuse post name?** Reddit is not fetchable from this machine's search tools; if it is one of the five pinned community extensions, RD-169's policy needs a rate-limit clause ([thread](https://reddit.com/r/spicetify/comments/1w3vcfl/)).
- **When will Spicetify v3 have a stable tag and a supported-versions file past 1.2.94?** That date decides whether the next tuple move is a v2 pin advance to 1.2.96 or a v3 migration; nothing public states it ([releases](https://github.com/spicetify/cli/releases)).
- **Does stale Marketplace storage on real machines block uninstall the way #1231 describes?** A reproduction would decide whether RD-196 needs the full IndexedDB delete or only a settings-key reset ([#1231](https://github.com/spicetify/marketplace/issues/1231)).
- **Will the peer session's v4.5.0 bump land before or after these items are picked up?** The version strings, engine pin and screenshots are staged uncommitted in the tree on 2026-09-04; RD-190's README fixes should be made against the committed v4.5.0 README, not the v4.4.0 one.
