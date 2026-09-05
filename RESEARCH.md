# Research: LibreSpot

Date: 2026-09-05. Replaces all prior research.

## Executive Summary

LibreSpot patches the Windows Spotify desktop client by orchestrating SpotX, Spicetify, Marketplace and a curated theme and extension catalog, across four surfaces: a WPF desktop shell, a CLI, a single-file `LibreSpot.ps1`, and an in-Spotify custom app with a companion extension. Its real differentiator is not the patching, which several projects do. It's the evidence discipline around it: every third-party asset is pinned to a commit, hashed, license-checked and dated (`schemas/community-assets.json`), gates refuse to pass when that evidence goes stale (`Build-Scripts.ps1:760`), removal never traverses a reparse point (`Remove-PathSafely.ps1`), and the accessibility gates carry planted positive controls that prove they can fail (`WpfUiAutomationSmokeTests.cs:341,426`). Nothing else in this space comes close on that axis.

The gap is that a lot of this rigor doesn't reach the user. The single most valuable piece of logic in the product, the check that says "your installed Spotify is newer than the build we verified", runs on one of four surfaces and only on one path within it. The update check reports success when every one of its network calls failed. The accessibility scan covers three of the forty-three UI states the app can already render on demand. So the top opportunity isn't new capability. It's connecting what already exists to the surfaces users actually touch.

Top opportunities in priority order:

1. Surface the installed-Spotify compatibility verdict on the CLI, the standalone script and the WPF Recommended path, not just the Custom install preview (RD-201).
2. Stop the update check reporting "up to date" when it couldn't reach GitHub at all (RD-202).
3. Extend the Axe and target-size scans to the overlay, prompt, error and empty states the harness already launches (RD-203).
4. Run those scans at the minimum window size using the `--uia-size` flag that already exists (RD-204).
5. Read the classmap bound from upstream's `index.json` instead of inferring it from a directory listing (RD-205).
6. Decide what the UIA test scaffolding is doing in the Release build and record that decision in the artifact contract (RD-206).
7. Localize the in-Spotify surface, or say in the README that it is English only (RD-207).
8. Gate the two README numbers RD-190 left ungated, the Stryker baseline and the theme count (RD-209).
9. Advance the themes pin one commit so the update check stops warning every user forever (RD-208).
10. Add the `spotx` GitHub topic, the one free discoverability lever that is currently unused (RD-210).

## Product Map

### Core workflows

- **Recommended install.** Pin Spotify to the SpotX-targeted build, run SpotX, install Spicetify CLI, Marketplace, the Comfy theme, then apply.
- **Custom install.** Pick themes, extensions and custom apps from the curated catalog, preview the plan, apply.
- **Maintenance.** Repair Marketplace, restore vanilla Spotify, clear cache, export a support bundle, undo a prior run from the operation journal.
- **Auto-reapply.** A per-user scheduled task watches for Spotify updates and reapplies the saved Spicetify setup.
- **Unattended and fleet.** `librespot-answer.json` plus CLI verbs, with Intune and WinRM samples under `samples/deployment/`.
- **In-Spotify customization.** The `librespot-engine` custom app plus companion extension for look, tweaks, presets and health, running inside the patched client.

### Users

- A single Windows user who wants an ad-free, themed Spotify and doesn't want to learn Spicetify's CLI.
- An operator deploying to a small fleet through Intune or WinRM, who needs deterministic pins and exit codes.
- A cautious user who wants to know what was changed and be able to undo it. The operation journal and undo surface exist for this person.

### Platforms and distribution

Windows 10 and 11, x64 and arm64. Released as GitHub assets only: `LibreSpot-Desktop.exe` (80.8 MB), `LibreSpot.Cli.exe` (39.4 MB), `LibreSpot.exe` (811 KB, PS2EXE), `LibreSpot.ps1` (707 KB), `librespot-engine.zip`, `checksums.txt`, a CycloneDX SBOM and a release manifest. Unsigned by design, with SHA256 checksums as the integrity story (`SIGNPATH.md:3`). No winget, no Scoop bucket, no store listing. Builds and releases happen locally, never in CI.

### Integrations and data

Outbound: `api.github.com` for update and release checks, `raw.githubusercontent.com` for SpotX, Spicetify and pinned catalog assets, the Spicetify releases CDN. Local: `%APPDATA%\Spotify`, `%LOCALAPPDATA%\Spotify`, `%APPDATA%\spicetify`, the user PATH, one per-user scheduled task, LibreSpot's own config and journal under its data root. The in-Spotify app persists to `localStorage` and an IndexedDB store it shares with Marketplace.

### Upstream state on 2026-09-05

| Component | LibreSpot pin | Upstream now | Note |
|---|---|---|---|
| Spotify | 1.2.93.667 (SpotX target) | 1.2.98.301 public, 1.2.99.317 staged | Verified ceiling is 1.2.93; Spicetify declares 1.2.96 |
| SpotX | `550bc72c` | `9d344658` (2026-09-03) | Newer commit adds `-download_method curl|webclient` and mirror coverage. RD-183 covers the advance |
| Spicetify CLI | 2.44.0 | 2.44.0 (frozen since 2026-07-04); v3.0.0-beta.12 | v3 is Rust, refuses Spotify below 1.2.80, still no stable date |
| Marketplace | 1.0.11 | 1.0.11 | No change |
| Themes | `df033493` | `3f55a370` (2026-09-04T17:07:08Z) | Exactly one commit ahead: the merged 1.2.98 Text progress-bar fix (#1291) |
| Classmaps | not consumed as data | `index.json` lists 1020097 as `1.2.97.270`, `status: verified` | Resolves the prior "1.2.96 or 1.2.97" ambiguity to 1.2.97.270 |

## Competitive Landscape

**Spicetify CLI** (github.com/spicetify/cli). The thing LibreSpot wraps. Learn from the new classmaps split: upstream moved the per-build rewrite data out of the binary and into a sha256-indexed data repo (`index.json`, `expose.json`), so a Spotify update becomes a data commit rather than a release. LibreSpot should consume that index rather than infer from directory names. Avoid: v2 has been frozen since 2026-07-04 while v3 sits in beta with empty release notes, which is exactly the "supported range that stops being true" trap LibreSpot's own verified ceiling is meant to escape.

**SpotX** (github.com/SpotX-Official/SpotX). Ships download-method fallbacks (`curl`, `webclient`) and mirror coverage because its raw GitHub path gets regionally blocked. Learn: multiple download paths are a feature, not redundancy. Avoid: patch selection is driven by a `patches.json` whose entries are keyed loosely on version ranges, so an unsupported build degrades quietly rather than refusing.

**ReVanced Manager** (github.com/ReVanced/revanced-manager). Closest analogue: a manager that patches a vendor app pinned to exact supported versions via `compatiblePackages`. It has a version compatibility check and it is worth studying because it keeps getting it wrong in both directions. Unsupported patches remain selectable through import (#560), re-enabling the check doesn't retroactively deselect (#1389), and patches get mis-flagged as unsupported when they aren't (#2321, #2444). Learn: a compatibility verdict must be recomputed at the point of action, not cached at selection time. Avoid: an override toggle that leaves stale selections behind it.

**BetterVencordPatch** (github.com/aaronwijes/BetterVencordPatch). A companion watcher that notices Discord auto-updated and silently re-runs the injector, reporting through OS notifications rather than a dialog. Same problem shape as LibreSpot's auto-reapply task. Learn: the notification channel matters as much as the reapply; a silent retry loop that nobody sees is the failure mode RD-182 already addressed. Avoid: the silent part. LibreSpot's hold state is the better design.

**Scoop-Spotify** (github.com/TheRandomLabs/Scoop-Spotify, 187 stars, pushed 2026-09-04). Ships one manifest per vendor-plus-patch pairing: `spotify-with-blockthespot.json`, `spicetify-themes.json`, and so on, each independently versioned with `checkver`/`autoupdate` re-deriving hash on every update and hard-failing on mismatch. Learn: versioning each pairing separately avoids the monolithic-drift problem LibreSpot manages with a single tuple. Avoid: the fragmentation cost, since a user has to know which pairing they want. LibreSpot's single verified tuple is the better user experience and should stay.

**Windhawk** (github.com/ramensoftware/windhawk). Its mod repo compiles every mod against multiple compiler and target versions before publication (`compile_mod.py`), and per-mod rollback lives on the mod's own details page. Learn: pre-publication compatibility verification of third-party assets, which is the mechanized version of LibreSpot's `lastVerifiedDate` gate. Avoid: auto-update-triggered rollback is still unsolved there (#541), so don't assume the pattern is finished.

**foobar2000 component manager.** Checks each component's declared API and OS requirement against the running host and disables the incompatible ones rather than crashing. Learn: refuse-and-explain beats apply-and-hope. Avoid: no user override at all, which is too rigid for a tool whose users deliberately run unsupported builds.

**Vortex and Mod Organizer 2.** Both were checked for the "admit the upstream is broken" pattern that a prior pass credited to Vortex 2.6. The official Vortex troubleshooting wiki (last edited 2024-04-30) documents no unsupported-version warning, no outage admission and no versioned undo. MO2's rollback is informal: profiles and instances, not an undo log. Treat the earlier claim as unverified. LibreSpot's operation journal with per-run undo is genuinely ahead of both.

**The GUI clones** (Dalbouh02/SpicetifyManager, FIREPAWER07/SpicetifyInstaller, itourboy-OG/Spicetify-Manager, EliasOnsihuay/SpiceManager). All single-digit stars, all low activity, none pin asset hashes or license-check. No lesson to take. They confirm the category is unserved rather than crowded.

## Reported Issues

The tracker is empty. `SysAdminDoc/LibreSpot` has zero open issues, zero open pull requests, zero forks and 12 stars as of 2026-09-04T17:00Z. Discussions are enabled and empty. Six issues have ever been closed, the newest being #22 "Make LibreSpot the in-client store" on 2026-09-04, and the older five (#1 to #5, closed between 2026-02-03 and 2026-03-27) were install-time failures that predate the current architecture: a 403 fetching Spicetify CLI, an `Expand-Archive` module load failure, a blank screen, a CSS header gap, and a request for options before install. All five are addressed by the current download fallback chain, `Expand-ArchiveSafely.ps1`, and the Custom workspace. None warrant re-opening.

Every closed pull request was a Dependabot bump, and Dependabot has since been removed by repo policy.

So there is no user-reported evidence to prioritize from. Everything in this pass is sourced from the code, from upstream trackers, or from adjacent-product trackers. That absence is itself the finding: with 12 stars and no community mentions anywhere, LibreSpot has no feedback loop, so every defect has to be found by reading rather than reported.

Three discoverability channels are open and unused. The repository carries the `spicetify` topic and shows up in that listing, but it is absent from `github.com/topics/spotx`, which returns 18 repositories, despite LibreSpot wrapping SpotX. The Spicetify page on AlternativeTo lists nine alternatives and LibreSpot is not one of them, and submissions there are open to any registered account. Awesome-Windows accepts submissions through a published contributing file. None of these is winget, which repo policy forbids, and none is r/Piracy, which bans self-promotion outright. RD-210.

## Security, Privacy, and Reliability

**The compatibility verdict reaches one surface in four.** `AppCatalog.CheckInstalledSpotifyCompatibility` (`src/LibreSpot.Core/AppCatalog.cs:1201`) compares the installed Spotify build against both `LibreSpotVerifiedMaxSpotify` (1.2.93) and Spicetify's declared max, and returns the warning that tells a user their client is past what was tested. It has exactly one production caller: `src/LibreSpot.Desktop/ViewModels/MainViewModel.CustomInstall.cs:888`, the Custom install plan preview. The CLI's `status` document (`src/LibreSpot.Cli/Program.cs:1400`) reports the static pin tuple and never calls it. The PowerShell equivalent, `src/powershell/shared/Get-LibreSpotCompatibilityWarnings.ps1`, compares the SpotX target against Spicetify's declared range and never reads the installed version at all, so the standalone `LibreSpot.ps1` lane, the smallest and most-downloadable artifact, cannot produce this warning. Public Spotify is 1.2.98.301 against a 1.2.93 verified ceiling, so this is live for essentially every new user. Verified.

**The update check reports success when it reached nothing.** `src/powershell/shared/Check-ForUpdates.ps1` wraps each of five GitHub calls in a `catch` that only writes a WARN line (lines 18, 27, 36, 49, 63) without recording that the check failed. Line 65 then tests `$updates.Count -eq 0 -and $compatWarnings.Count -eq 0` and line 66 logs "All dependencies and compatibility baselines are up to date." at SUCCESS level. Offline, behind a proxy, on a DNS-filtered network, or once the shared unauthenticated 60-per-hour GitHub limit is spent on a NAT, all five throw and the user is told everything is current. The same composed function ships in `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:3657` and `LibreSpot.ps1:9615`, so all three hosts share it. Verified.

**Test scaffolding ships in the Release build.** There is no `#if DEBUG` and no `[Conditional("DEBUG")]` anywhere under `src/LibreSpot.Desktop`. `MainViewModel.ApplyUiAutomationSmokeState` (`src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:2598`) recognises 43 states, and `MainWindow.xaml.cs:26,32` parse `--uia-smoke=`, `--uia-background` and a `LIBRESPOT_UIA_ROOT` data-root override, with `--uia-size` and `--uia-capture` documented in `schemas/publish-footprint-budget.json:56`. Anyone can start the shipped, checksummed executable with `--uia-smoke=home-healthy` or `--uia-smoke=maintenance-danger` and get a fabricated readiness report from the genuine binary. There is no privilege boundary crossed and `LIBRESPOT_UIA_ROOT` keeps writes off the real config, so the risk is presentation, not compromise. But `schemas/release-artifact-contract.json` records nothing about it, which is out of step with how carefully this repo records everything else. Verified.

**Probed and sound, so don't redo these.** `Remove-PathSafely.ps1` deliberately refuses recursive delete and ACL operations, unlinks every reparse point without traversing it, and walks directories bottom-up, with the reasoning in a comment; a junction cannot redirect it out of the approved root. `Test-SafeRemovalTarget.ps1` resolves and refuses drive roots plus sixteen known folders. `Invoke-GitHubApiSafe.ps1` classifies 403 and 429 distinctly and reports the reset time. `Build-Scripts.ps1:760` refuses to pass when any active catalog asset was last verified before the pinned Spotify build's release date, and it discovers manifest sections structurally rather than by an enumerated list, with the reason recorded. The Axe scan asserts `WindowsScanned > 0` and `ElementsCharted > 1` so an empty scan can't go green, and both the Axe rule and the target-size rule have planted positive controls (`WpfUiAutomationSmokeTests.cs:341,426`). Repo hygiene is clean: no `work/`, `bin/`, `obj/`, `StrykerOutput/` or `publish/` path is tracked, and the tracked root matches the `AGENTS.md` document set.

## Architecture Assessment

**Accessibility coverage is three states out of forty-three.** `WpfUiAutomationSmokeTests.AxeWindowsScan_FindsNoViolationOutsideTheRecordedBaseline` (line 273) and `InteractiveTargets_AreAtLeastTwentyFourByTwentyFourDips` (line 401) both carry the same three `[InlineData]` rows: `recommended`, `custom`, `maintenance`. The same file already launches `prompt`, `activity`, `activity-running`, `activity-error` and `activity-undo` for named-control assertions at lines 47 to 81, so the harness cost of scanning them is one line each. The unscanned set includes every state where accessibility usually breaks: modal overlays, destructive confirmations, error and empty states, `snapshot-loading`, `custom-no-results`, `global-search` and `reduced-motion`. `schemas/axe-windows-baseline.json` correspondingly has only three keys, all empty. Verified.

**Every accessibility scan runs at one window size.** `LaunchSmokeState` passes `--uia-smoke`, `--uia-culture` and `--uia-background` and never `--uia-size`, though that flag exists and `schemas/publish-footprint-budget.json:56` shows it being used at `1280x800` for the footprint measurement. RD-186 establishes the minimum window as 1080x720. A responsive layout can drop an accessible name or shrink a target inside a breakpoint a single-size scan never enters. Verified.

**The classmap bound is inferred where upstream now publishes it.** `Build-Scripts.ps1:2854` lists `https://api.github.com/repos/spicetify/classmaps/contents`, matches directory names against `^10\d{5}`, takes the highest and reconstructs a three-part version by string arithmetic (lines 2877 to 2884). Upstream published `index.json` on 2026-08-06 and extended it on 2026-09-03; it is 2.9 KB, gives each key its exact `spotifyVersion` (1020097 is `1.2.97.270`) and a `status` field, and carries a sha256 for every referenced file. A directory listing cannot distinguish a verified classmap from an inherited one, which is precisely the distinction RD-189 was built to reason about. Reading `raw.githubusercontent.com/spicetify/classmaps/main/index.json` also costs no GitHub API quota. Verified.

**The in-Spotify surface is outside the localization story.** `.crowdin.yml` scopes exactly one file, `/src/LibreSpot.Desktop/Properties/Strings.resx`, which carries 1358 strings across five locales with a validation gate at `Build-Scripts.ps1:3313` and eighteen tests in `LocalizationTests.cs`. The in-Spotify app under `src/LibreSpot.App/src/` holds roughly 238 hardcoded English UI strings with no externalization and no Crowdin entry, and `LibreSpot.ps1` contains zero references to `CurrentUICulture`, `Get-Culture` or `Import-LocalizedData`. A Russian or Chinese user gets a localized shell and an English in-client panel. Verified.

**Two names for one icon.** `LibreSpot.ico` and `icon.ico` are byte-identical (sha256 `2939774d...`), both tracked at the repo root, and referenced from different build paths: `Build-Scripts.ps1:1952` uses `LibreSpot.ico` for the PS2EXE build while `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:15,27` uses `icon.ico`. `LibreSpot.ps1:3782` already probes both as a fallback pair, which is the workaround rather than the fix. Nothing asserts they stay identical, so a future icon change can ship two different icons in two artifacts. Verified.

**Two README numbers that nothing gates.** RD-190 gated the extension count (`ReleaseTruthTests.cs:188`) and the lyrics theme count (line 234), and the fleet exit-code table and CLI verb list both check out against `schemas/fleet-exit-codes.json` and `Program.cs`. Two numbers were left ungated. `README.md:791` states the Stryker baseline as "24.32% over 1,476 tested mutants" and calls it current, but the run behind it was 2026-08-20 (`Roadmap_Blocked.md:1005`), 20 commits have since touched the four files `src/LibreSpot.Core/stryker-config.json` mutates, `StrykerOutput/` is gitignored so no artifact in the tree can confirm it, and no test references the figure. `README.md:121,131,304` states "24 supported themes" against 25 entries in `schemas/theme-preview-manifest.json` minus the Marketplace-only placeholder, which is correct today and is exactly the shape of drift RD-190 fixed elsewhere. Verified.

**Repo hygiene, two small things.** `.gitignore:35` ignores `Roadmap_Blocks.md`, which is a typo for the tracked `Roadmap_Blocked.md`, so the line matches nothing. `.git/objects/pack` holds five `tmp_pack_*` files totalling 640 KiB, left by an interrupted repack. Verified.

**Test and documentation gaps.** `MultiUserIsolationTests.cs` has two real assertions; `OfflineAssetCacheRegressionTests.cs` exposes no named test method in its public surface beyond `Dispose`, which is worth a look. The 43 smoke states include roughly 23 that no test launches at all, so a chunk of that scaffolding is neither exercised nor removable without checking. Commit history shows the project already catches this class itself: `825e760` fixed a pnpm audit that ran from the wrong directory, parsed `ERR_PNPM_AUDIT_NO_LOCKFILE` as a clean result and reported no advisories every time, with an unreachable allowlist behind it. That is the standing risk in a repo with this many gates.

## Rejected Ideas

- **Adopt Spicetify v3.** Still beta.12 with empty release notes, no stable date, a supported range ending at 1.2.94 and a hard refusal below 1.2.80. Re-rejected; source: github.com/spicetify/cli releases.
- **Advance the Spotify pin from headlines.** The bound is now computable as 1.2.97.270 from classmaps `index.json`, but SpotX and Spicetify's declared range still gate it and RD-183 must land first so the download escape hatches exist. Source: classmaps `index.json`.
- **A user-supplied extension or theme slot.** The catalog's whole value is that every asset is commit-pinned, hashed, SPDX-checked and re-verified against the pinned client (`schemas/community-assets.json`). An arbitrary-URL slot voids all four properties, and Marketplace already is the escape hatch. Contradicts the stated trust model.
- **Publish a Scoop manifest or bucket.** Technically allowed, since the winget prohibition doesn't extend to Scoop, and Scoop-Spotify (187 stars) proves the channel works. Rejected for now because a bucket is an ongoing update obligation that duplicates the pin tuple in a second place, and the repo builds and releases locally by policy. Worth revisiting if the star count ever justifies the maintenance. Source: github.com/TheRandomLabs/Scoop-Spotify.
- **Post an announcement to r/Piracy.** The subreddit bans self-promotion outright. Source: r/Piracy rules.
- **Code signing.** `SIGNPATH.md:3` records unsigned-by-design as a settled decision with `release-artifact-contract.json` agreeing. Not reopening.
- **GitHub artifact attestation.** Requires a build workflow, and the repo forbids CI builds. Already rejected in a prior pass.
- **Chase the "SpotX gets accounts banned" claim.** Two SEO aggregator pages assert it; neither links a primary thread, and no 2026 primary report exists. Not worth a README change until someone produces one.

## Sources

### Repository
- Local: `src/LibreSpot.Core/AppCatalog.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.CustomInstall.cs`, `src/LibreSpot.Cli/Program.cs`, `src/powershell/shared/Check-ForUpdates.ps1`, `Get-LibreSpotCompatibilityWarnings.ps1`, `Remove-PathSafely.ps1`, `Test-SafeRemovalTarget.ps1`, `Invoke-GitHubApiSafe.ps1`, `Build-Scripts.ps1`, `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`, `schemas/community-assets.json`, `schemas/axe-windows-baseline.json`, `schemas/publish-footprint-budget.json`, `.crowdin.yml`
- https://github.com/SysAdminDoc/LibreSpot/releases

### Upstream
- https://github.com/spicetify/classmaps
- https://raw.githubusercontent.com/spicetify/classmaps/main/index.json
- https://github.com/spicetify/spicetify-themes/pull/1291
- https://github.com/spicetify/spicetify-themes/issues/1290
- https://github.com/spicetify/cli/issues/3917
- https://github.com/SpotX-Official/SpotX
- https://github.com/spicetify/marketplace/issues/1231

### Adjacent products
- https://github.com/TheRandomLabs/Scoop-Spotify
- https://github.com/aaronwijes/BetterVencordPatch
- https://github.com/ReVanced/revanced-manager/issues/560
- https://github.com/ReVanced/revanced-manager/issues/1389
- https://github.com/ReVanced/revanced-manager/issues/2321
- https://github.com/ramensoftware/windhawk/issues/541
- https://deepwiki.com/ramensoftware/windhawk-mods/2.2-mod-compatibility-verification
- https://github.com/Nexus-Mods/Vortex/wiki/Vortex-Troubleshooting
- https://www.foobar2000.org/FAQ
- https://github.com/ScoopInstaller/Scoop/wiki/App-Manifest-Autoupdate

### Discoverability
- https://alternativeto.net/software/spicetify
- https://github.com/Awesome-Windows/Awesome/blob/master/Contributing.md
- https://github.com/topics/spotx

## Coverage and Conscious Exclusions

**Not covered well this pass.** The TypeScript surface under `src/LibreSpot.App/` got a shallow read: string counting, the untracked `companion-readiness.ts` helper (17 lines, imported at `librespot-engine.ts:34` and used at `:359`, with its own test, so it is complete rather than a stub), and the `.crowdin.yml` scope question. Its storage, quota, migration and Spicetify-global-absence behavior was not audited in depth here and is the obvious target for the next pass. Likewise the WPF microcopy and visual design were not reviewed beyond the accessibility gates.

**Deliberately out of consideration, with reasons.** Mobile and cross-platform: LibreSpot patches a Windows desktop binary through Windows-only mechanisms (per-user scheduled tasks, the user PATH, `%APPDATA%`), so there is no port to plan and nothing to roadmap. Multi-user: the design is per-user by construction and `MultiUserIsolationTests.cs` asserts the shell stays as invoker with no admin actions, so no work is proposed. Migration and upgrade: the config schema carries a version with a 1-to-2 migration at `Normalize-LibreSpotConfig.ps1:136`, and `Assert-LibreSpotConfigSchemaSupported.ps1` refuses an unknown version rather than guessing, so the path is already sound and nothing is proposed.

## Open Questions

1. Is the UIA smoke surface in the Release build intentional, so it should be recorded in `release-artifact-contract.json` and given a stated threat model, or accidental, so it should be gated behind a build constant? This changes RD-206 from a documentation task into a code change.
2. Should the in-Spotify app be localized at all, given it renders inside a client that has its own language setting, or should the README state plainly that the in-client panel is English only? RD-207 offers both, and the answer is a product call, not a technical one.
3. Is `Get-LibreSpotCompatibilityWarnings.ps1` deliberately about tuple consistency rather than the installed build, with the installed-build check meant to live only in the C# lanes? If so, RD-201 shrinks to a documentation fix plus the two missing C# call sites.
