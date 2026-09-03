# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P0: RD-142: Pin the live engine download to an immutable per-release source
  Why: the installer downloads `librespot-engine.zip` from `raw.githubusercontent.com/.../main/...` and checks it against a SHA256 frozen at release time, so the public v4.1.2 release (pin `c30ea64c...`) now fails its own hash check on any machine whose asset cache is empty because `main` serves the 4.2.0 build (`e280fbdf...`); `Bundled = $true` is set and never read.
  Evidence: `src/powershell/data/CommunityCustomApps.ps1:17-25`; `src/powershell/shared/Module-InstallCustomApps.ps1:40-50`; `git show v4.1.2:src/powershell/data/CommunityCustomApps.ps1`; `sha256sum resources/custom-apps/librespot-engine.zip`; RESEARCH.md "Security, Privacy, and Reliability".
  Touches: `src/powershell/data/CommunityCustomApps.ps1`, `src/powershell/shared/Module-InstallCustomApps.ps1`, `src/LibreSpot.Core/AppCatalog.cs`, both composed hosts, `schemas/release-artifact-contract.json`, `Build-Scripts.ps1 -GenerateReleaseManifest`, `tests/LibreSpot.Desktop.Tests/CommunityAssetsManifestTests.cs`, `README.md` release procedure.
  Acceptance: WHEN a tagged release installs the LibreSpot custom app, the system SHALL fetch it either from `https://github.com/SysAdminDoc/LibreSpot/releases/download/v<version>/librespot-engine.zip` or from the archive embedded in the release, never from a branch URL; the release manifest and `checksums.txt` list `librespot-engine.zip`; a Core test fails when any catalog entry URL contains `/main/` or a `ReleaseTag` of `main`; a Pester test proves the bundled-first path installs with the network unavailable.
  Complexity: M

- [ ] P1: RD-143: Advance the Marketplace pin from 1.0.9 to 1.0.11 with a live persistence proof
  Why: 1.0.10 added manifest validation and persistence before reload and 1.0.11 "properly migrate keys"; those target the "installed extensions and settings vanish after restart" reports that are the third most common complaint upstream, and LibreSpot is two releases behind while pinning the release that moved storage to IndexedDB.
  Evidence: https://github.com/spicetify/marketplace/releases (v1.0.10 2026-08-29, v1.0.11 2026-09-02); https://github.com/spicetify/marketplace/issues/1201; https://github.com/spicetify/cli/issues/3861; `src/powershell/data/PinnedReleases.ps1:35`; `src/LibreSpot.Core/AppCatalog.cs:836`.
  Touches: `src/powershell/data/PinnedReleases.ps1`, `src/LibreSpot.Core/AppCatalog.cs`, `schemas/compatibility-baseline.json`, `schemas/community-assets.json`, `docs/how-spotx-and-spicetify-alter-spotify.md`, both composed hosts, `CHANGELOG.md`, `README.md` compatibility table.
  Acceptance: WHEN the pinned Marketplace version and SHA256 move to 1.0.11, `Build-Scripts.ps1 -Validate` and `CommunityAssetsManifestTests` SHALL pass; a hidden CDP run against the installed 1.2.93 client SHALL show the Marketplace route loading, an installed extension surviving a full client restart, and the LibreSpot store route still wired (`Repair-SpicetifyCustomAppWiring` reports `Patched`); the `Marketplace v1.0.9` data-inventory note is updated.
  Complexity: M

- [ ] P1: RD-144: Make README feature claims lane-accurate and test the uninstaller phase count
  Why: the README describes script-lane behaviour as product-wide: an "8-phase uninstaller" (the script logs seven phases since the native phase was removed), hidden Spotify windows and a topmost LibreSpot (`Hide-SpotifyWindows` is a no-op stub and the desktop has no `Topmost`), "x64 and ARM64", "Self-elevating", "Dual download methods", and "runspaces"; it counts 27 lyrics options where the catalog has 28; it labels the SpotX pin "2.0" although no such upstream tag exists; and the 4.2.0 changelog says a profile opens "from global search" while the only search box is inside the collapsed legacy shell.
  Evidence: `README.md:347,358-360,418-436`; `LibreSpot.ps1:9134-9171`; `src/powershell/backend/lane-functions.ps1:415`; `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:8`; `src/LibreSpot.Core/AppCatalog.cs:962-968`; `src/powershell/data/PinnedReleases.ps1:3`; `src/LibreSpot.Desktop/MainWindow.xaml:1408-1410,3046`; https://github.com/SpotX-Official/SpotX/releases.
  Touches: `README.md`, `CHANGELOG.md` (4.2.0 Fixed entry wording), `tests/LibreSpot.Desktop.Tests/ReleaseArtifactContractTests.cs` or `ReleaseTruthTests.cs`, `src/LibreSpot.Core/AppCatalog.cs` (SpotX label) and `src/powershell/data/PinnedReleases.ps1`.
  Acceptance: WHEN the README describes uninstall phases, window handling, elevation, download fallback, or architecture, each claim SHALL name the lane it applies to (desktop, CLI, script) or be removed; a test SHALL read the phase count from `LibreSpot.ps1` and fail when README states a different number; the lyrics count in README SHALL equal `AppCatalog.LyricsThemes.Count` under test; the SpotX pin SHALL be presented by commit and date with the "2.0" label removed or explained; the 4.2.0 changelog line SHALL mention only the `.librespot` file and `librespot://` paths.
  Complexity: S

- [ ] P1: RD-145: Re-verify the reviewed community catalog against Spotify 1.2.93 and gate pin advances on verification dates
  Why: every `lastVerifiedDate` in the community catalog is 2026-06-15 or earlier (Stats 2026-06-29), before the Spotify pin moved to 1.2.93 on 2026-07-04; Comfy, a catalog theme, has not been pushed since 2026-01-04 and carries an open "playbar disappeared" regression; the pinned Stats app was last released in 2025-12 and its author's repo shows no 2026 activity.
  Evidence: `schemas/community-assets.json` (`supportState`, `lastVerifiedDate` per asset); https://github.com/Comfy-Themes/Spicetify/issues/256; https://github.com/harbassan/spicetify-apps/releases; https://github.com/Xndr2/listening-stats; `src/powershell/data/CommunityCustomApps.ps1:5`.
  Touches: `schemas/community-assets.json`, `tools/Build-CommunityCatalog.ps1`, `Build-Scripts.ps1 -Validate`, `src/LibreSpot.Core/CommunityAssetDriftService.cs`, `README.md` catalog copy, `tests/LibreSpot.Desktop.Tests/CommunityAssetsManifestTests.cs`.
  Acceptance: WHEN `-Validate` runs, it SHALL fail if any active asset's `lastVerifiedDate` predates the pinned Spotify release date recorded in `schemas/compatibility-baseline.json`; every catalog asset SHALL carry a `lastVerifiedDate` on or after 2026-09-03 with a hidden-client check recorded in the commit (theme applies without a blank playbar, extension registers, Stats route renders); any asset that fails SHALL move to `supportState` `degraded` with the upstream issue link, and the generated catalog page SHALL show the state and date.
  Complexity: M

- [ ] P1: RD-146: Script the release publish in `Build-Scripts.ps1` and make the desktop and CLI builds reproducible
  Why: the README release procedure names every step except the `dotnet publish` invocation, and no csproj sets `Deterministic`, `ContinuousIntegrationBuild`, `EmbedUntrackedSources`, or `PublishRepositoryUrl`, so nobody, the maintainer included, can rebuild a released asset and compare it; the release manifest cannot record what it was built with.
  Evidence: `README.md:562-570`; `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`; `src/LibreSpot.Cli/LibreSpot.Cli.csproj`; `Directory.Build.props`; `Build-Scripts.ps1:27-59`; https://github.com/dotnet/reproducible-builds; https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props.
  Touches: `Build-Scripts.ps1` (new `-PublishRelease`), `Directory.Build.props`, both app csproj files, `schemas/release-artifact-contract.json`, `Build-Scripts.ps1 -GenerateReleaseManifest`, `README.md` release procedure, `tests/powershell/LibreSpot.Tests.ps1`.
  Acceptance: WHEN `Build-Scripts.ps1 -PublishRelease` runs, it SHALL clean `publish`, publish both projects self-contained for `win-x64` with `Deterministic`, `ContinuousIntegrationBuild=true`, `EmbedUntrackedSources`, and `PublishRepositoryUrl` set, and write the property set, SDK version, and commit into the release manifest; WHEN the command runs twice on the same commit, the two `LibreSpot.Cli.exe` outputs SHALL be byte-identical and the two desktop outputs SHALL differ only in fields the manifest lists as non-deterministic; the README release procedure SHALL show the one command.
  Complexity: M

- [ ] P1: RD-147: Add a one-click backup file for live engine and Marketplace state, importable by the desktop and restorable in the client
  Why: engine state lives in browser localStorage under `librespot:engine-state`, the same store whose wipes fill the Spicetify tracker, and Marketplace state lives in IndexedDB; Marketplace already exports its settings as a JSON file from its Backup modal, so a LibreSpot Health-panel action can write one file holding both, and the desktop can fold it into a `.librespot` profile without copying Chromium files.
  Evidence: `src/LibreSpot.App/src/core/store.ts:8-23`; https://github.com/spicetify/marketplace/blob/main/src/components/Modals/BackupModal/index.tsx; https://github.com/spicetify/cli/issues/3861; https://github.com/spicetify/marketplace/issues/1201; `Roadmap_Blocked.md` "Separate LibreSpot-managed profile sharing from Marketplace-state backup".
  Touches: `src/LibreSpot.App/src/panels/health.ts`, `src/LibreSpot.App/src/core/store.ts`, `src/LibreSpot.App/src/core/profile.ts`, `schemas/librespot-profile.schema.json`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.Profiles.cs`, `schemas/data-inventory.json`, `README.md`.
  Acceptance: WHEN the user chooses Back up in the Health panel, the system SHALL save one JSON file containing the engine state and the Marketplace export in Marketplace's own format with a schema version and timestamp; WHEN that file is imported in the desktop, it SHALL become a `.librespot` profile that preserves unknown fields; WHEN the user chooses Restore in the client with a file, the engine state and Marketplace keys SHALL be written back and a Vitest round-trip test SHALL prove equality after export, wipe, and restore; nothing SHALL be sent off the machine.
  Complexity: M

- [ ] P2: RD-148: Enable single-file compression for the desktop and CLI executables and record measured size and cold start
  Why: the desktop executable is 175.7 MiB against a 180 MiB warning line and the CLI is 72.2 MiB; the publish budget documents trimming, ReadyToRun, self-contained, single-file, and native extraction but not `EnableCompressionInSingleFile`, and its cold-start section says the metrics were never measured with a rationale that still refers to GitHub Actions runners.
  Evidence: https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.1.2 (asset sizes); `schemas/publish-footprint-budget.json`; `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:17`; https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview.
  Touches: both app csproj files, `schemas/publish-footprint-budget.json`, `Build-Scripts.ps1 -GenerateReleaseManifest`, `README.md` footprint copy.
  Acceptance: WHEN the release builds with compression on, the desktop executable SHALL be smaller than 120 MiB and the manifest SHALL record the size; cold-start-to-main-window and first-refresh SHALL be measured offscreen (`--uia-smoke` timing) before and after on this machine and written into `coldStartMetrics` with budgets; if the compressed build starts more than 1.5 seconds slower, compression SHALL stay off and the budget file SHALL record the numbers and the decision.
  Complexity: S

- [ ] P2: RD-149: Trigger the auto-reapply watcher from a Spotify file change instead of only the 30-minute poll
  Why: the watcher is a logon-triggered scheduled task repeating every 30 minutes, so a Spotify update that gets past the SpotX block leaves the client unpatched for up to half an hour; "Spotify updated and my setup vanished" is the single most common complaint upstream and the reason people write their own daily scripts.
  Evidence: `src/powershell/gui/lane-functions.ps1:106-186`; https://www.reddit.com/r/spicetify/comments/1p29rqh/; https://www.reddit.com/r/spicetify/comments/1vw95y7/; https://github.com/spicetify/cli/issues/3869; https://github.com/BetterDiscord/Installer#faq (loader survives host updates).
  Touches: `src/powershell/gui/lane-functions.ps1`, `src/powershell/backend/lane-functions.ps1`, `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`, `src/powershell/shared/Write-WatcherLog.ps1`, `tests/powershell/LibreSpot.Tests.ps1`, `tests/powershell` watcher harness, `README.md` Auto-Reapply section.
  Acceptance: WHEN the watcher task body starts, it SHALL watch `%APPDATA%\Spotify\Spotify.exe` and `%LOCALAPPDATA%\Spotify\Update` for changes and run the existing reapply within 60 seconds of a version change while the 30-minute poll remains as a backstop; a Pester test SHALL simulate a version change against a fake Spotify folder and assert one reapply and one watcher log line; the task SHALL keep `LeastPrivilege`, battery flags off, and `IgnoreNew`.
  Complexity: M

- [ ] P2: RD-150: Add per-flag revert and a non-default marker to the Features panel
  Why: the Features panel offers one "Reset custom flags" action for everything; Windhawk 2.0 marks each non-default row and offers revert-to-default per row, which is the pattern users of a 348-flag catalog need to undo one experiment without losing the rest.
  Evidence: `src/LibreSpot.App/src/panels/features.ts:300-307`; https://github.com/ramensoftware/windhawk/releases (2.0 alpha 3 settings UI); `CHANGELOG.md` v4.1.2 "one reset action when any custom values are present".
  Touches: `src/LibreSpot.App/src/panels/features.ts`, `src/LibreSpot.App/src/core/feature-overrides.ts`, `src/LibreSpot.App/src/app.css`, `src/LibreSpot.App/tests`.
  Acceptance: WHEN a flag holds a value different from its catalog default, its row SHALL show a visible non-default marker with an accessible name and a Revert control; WHEN Revert is used, the override SHALL be removed and the remote value restored through the existing restore path; group summaries SHALL keep their custom-value counts; a Vitest test SHALL cover marker, revert, and count.
  Complexity: S

- [ ] P2: RD-151: Audit the live engine's JavaScript dependencies in the local gates and retire spicetify-creator
  Why: `Build-Scripts.ps1 -DependencyHealth` audits NuGet only; `spicetify-creator` 1.0.17 has no releases and pins esbuild `^0.14`, so the lockfile carries esbuild 0.14.54 beside the direct 0.28.2, inside an advisory range that no local gate can see; the repository already has its own esbuild build script.
  Evidence: `src/LibreSpot.App/package.json`; `src/LibreSpot.App/pnpm-lock.yaml`; `Build-Scripts.ps1`; https://registry.npmjs.org/spicetify-creator/latest; https://github.com/vitejs/vite/issues/19428; https://github.com/evanw/esbuild/security/advisories; `src/LibreSpot.App/scripts/build.mjs`.
  Touches: `Build-Scripts.ps1 -DependencyHealth`, `schemas/dependency-health-allowlist.json`, `src/LibreSpot.App/package.json`, `src/LibreSpot.App/scripts/build.mjs`, `src/LibreSpot.App/THIRD_PARTY_NOTICES.md`, `README.md` local validation.
  Acceptance: WHEN `-DependencyHealth` runs, it SHALL run `pnpm audit --prod` and `pnpm audit` for `src/LibreSpot.App` and fail on any advisory not listed in the allowlist with an expiry; WHEN the custom app builds, it SHALL use the repository's esbuild 0.28.x pipeline with the required global `render` binding validated by the existing build check, and `spicetify-creator` SHALL be absent from the lockfile; the deterministic ZIP hash pins SHALL be updated and the 45-test app suite SHALL pass.
  Complexity: M

- [ ] P2: RD-152: Run an Axe.Windows rule scan inside the UIA smoke suite
  Why: 341 `AutomationProperties` uses and contract tests exist, but no automated UIA rule engine checks the rendered shell; Axe.Windows runs the same rules as Accessibility Insights and can scan the offscreen shell the smoke tests already launch.
  Evidence: https://github.com/microsoft/axe-windows; `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`; `tests/LibreSpot.Desktop.Tests/AutomationNameContractTests.cs`.
  Touches: `tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj`, `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`, `schemas/scorecard-baseline.json`, `README.md` local validation.
  Acceptance: WHEN the UIA smoke suite runs, an Axe.Windows scan of the Home, Settings, and Maintenance states SHALL run offscreen and fail on any violation not recorded in a baseline file; the baseline SHALL start at the current violation set with each entry justified; a planted unnamed button in a test-only state SHALL be detected (positive control).
  Complexity: M

- [ ] P2: RD-153: Cover the desktop executable in the Smart App Control and SmartScreen guidance
  Why: the README FAQ entry says "Smart App Control blocks the script from running" and answers for the script, but the desktop executable is now the first install path and Smart App Control blocks unsigned executables outright with no per-app bypass; SmartScreen reputation restarts from zero for each unsigned release.
  Evidence: `README.md:476-480`; `SECURITY.md`; https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions; https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation.
  Touches: `README.md` FAQ and Trust sections, `SECURITY.md`, `src/LibreSpot.Desktop/Properties/Strings.resx` and the five satellites if the in-app application-control copy names only the script, `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs`.
  Acceptance: WHEN a reader reaches the Smart App Control entry, it SHALL state that `LibreSpot-Desktop.exe`, `LibreSpot.Cli.exe`, `LibreSpot.exe`, and the script are all blocked while Smart App Control is on, that no per-app bypass exists, and that the supported answer is a device where it is off or in evaluation mode; the SmartScreen entry SHALL explain that the "Unknown publisher" warning recurs for every release; the release-truth test SHALL fail if the FAQ names only the script.
  Complexity: S

- [ ] P3: RD-154: Correct the release-notice rate-limit assumption and prove one request per day
  Why: the code comment says an unchanged release "costs no rate limit" because of `If-None-Match`, but GitHub only exempts conditional 304s on authenticated requests; the anonymous budget is 60 per hour and the 24-hour cache is the real protection.
  Evidence: `src/LibreSpot.Core/ReleaseNoticeService.cs:50,345`; https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api; https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api.
  Touches: `src/LibreSpot.Core/ReleaseNoticeService.cs`, `tests/LibreSpot.Desktop.Tests/ReleaseNoticeServiceTests.cs`.
  Acceptance: WHEN the comment is read, it SHALL state the anonymous budget and that the cache, not the ETag, bounds requests; a test SHALL prove that two `GetNoticeAsync` calls inside the cache lifetime make exactly one HTTP request even when the server would answer 304, and that a 403 or 429 extends the cache before retrying.
  Complexity: S

- [ ] P3: RD-155: Show the asset digest and the verify command beside the Update LibreSpot link
  Why: the latest-release payload already carries per-asset SHA256 digests, and the README teaches `gh release verify-asset`; the Home notice could show the desktop asset's digest and the one command so a user verifies before running, matching the release-trust story.
  Evidence: https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/; https://cli.github.com/manual/gh_attestation_verify; `src/LibreSpot.Core/ReleaseNoticeService.cs`; `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml` update-notice row.
  Touches: `src/LibreSpot.Core/ReleaseNoticeService.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`, `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`, six resx files, `tests/LibreSpot.Desktop.Tests/MainViewModelReleaseNoticeTests.cs`, QA matrix row `home-update`.
  Acceptance: WHEN a newer stable release is cached, the notice SHALL expose the `LibreSpot-Desktop.exe` digest and a copyable `gh release verify-asset` line through a disclosure, with automation names and all five locales; WHEN the payload has no digest, nothing extra SHALL render; the `home-update` capture SHALL stay within its window.
  Complexity: S

- [ ] P3: RD-156: Offer an opt-in local minidump toggle for crash diagnosis
  Why: crash reporting is log-based; the runtime can write a Triage minidump (paths and secrets stripped) with three environment variables and no elevation, which gives a support bundle real crash evidence without any telemetry.
  Evidence: `src/LibreSpot.Desktop/Services/CrashReporter.cs`; https://learn.microsoft.com/en-us/dotnet/core/diagnostics/collect-dumps-crash; `schemas/data-inventory.json`.
  Touches: `src/LibreSpot.Desktop/Services/CrashReporter.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`, `src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`, `src/LibreSpot.Core/SupportBundleService.cs`, `schemas/data-inventory.json`, six resx files.
  Acceptance: WHEN the user turns the toggle on under Maintenance diagnostics, the next launch SHALL set `DOTNET_DbgEnableMiniDump=1`, `DOTNET_DbgMiniDumpType=3`, and a `%e-%p-%t` name under the crashes folder; the support bundle SHALL include the newest dump only when the toggle is on and SHALL list it in the redaction report; the data inventory SHALL document the path and retention (keep 2).
  Complexity: M

- [ ] P3: RD-157: Name the star-farmed decoy repositories pattern in the download verification section
  Why: two repositories with nonsense READMEs and off-GitHub download links ("spicetify-elite", "spotify-adblock-studio") outrank LibreSpot in stars and appear in the same searches; the verification section already teaches hash and attestation checks and can add one sentence on what a decoy looks like.
  Evidence: https://github.com/NeedChandlerMonitor/spicetify-elite; https://github.com/SecretBarber/spotify-adblock-studio; `README.md` "Check the file" section.
  Touches: `README.md` verification and FAQ sections, `SECURITY.md`.
  Acceptance: WHEN a reader reaches the verification section, it SHALL state that LibreSpot is only published at `github.com/SysAdminDoc/LibreSpot/releases`, that any page offering it elsewhere or asking for a "template" or "activation" download is not LibreSpot, and how to check the release attestation; the release-truth test SHALL assert the canonical URL sentence exists.
  Complexity: S

- [ ] P3: RD-158: Audit the reviewed extensions and custom apps for Spotify Web API client IDs under the 2026 developer-access rules
  Why: since 2026-02-06 Spotify's Development Mode requires Premium and caps each client ID at five authorised users, so any bundled or catalog extension that calls the Web API with its own client ID will fail for the sixth user; extensions that use the client's internal Platform APIs are unaffected.
  Evidence: https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security; https://developer.spotify.com/blog/2026-07-23-web-api-quota-updates; `schemas/community-assets.json`; `src/powershell/data/CommunityCustomApps.ps1`.
  Touches: `schemas/community-assets.json`, `tools/Build-CommunityCatalog.ps1`, `README.md` catalog copy.
  Acceptance: WHEN the audit completes, every catalog asset SHALL carry a recorded `webApiUse` value (`none`, `platform-api`, `client-id`) with the file and line that proves it; any `client-id` asset SHALL show a plain-language note in the catalog page and the Extensions panel, or be moved to `degraded`.
  Complexity: S

- [ ] P3: RD-159: Add a safe-mode launch that starts Spotify once with LibreSpot-managed extensions and apps disabled
  Why: when an extension breaks Spotify's startup, the only recovery paths are Repair (reinstalls the same set) or Restore vanilla (removes everything); BetterDiscord's most-requested recovery feature is a crash screen that lets users disable addons, and a one-session safe mode gives the same result without a UI inside a broken client.
  Evidence: https://github.com/BetterDiscord/BetterDiscord/issues/1920; https://github.com/BetterDiscord/BetterDiscord/issues/2237; `src/powershell/shared/Get-SpicetifyApplyPlan.ps1`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`.
  Touches: `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`, `src/powershell/backend/lane-functions.ps1`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`, `src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`, `src/LibreSpot.Cli/Program.cs` (`repair --safe-mode`), `schemas/fleet-cli-contract.json`, six resx files.
  Acceptance: WHEN the user chooses Start Spotify in safe mode, the system SHALL snapshot `config-xpui.ini` and `CustomApps`, apply with extensions and custom apps disabled, launch Spotify, and offer one action that restores the snapshot and re-applies; the operation journal SHALL record it as reversible; a Pester test SHALL prove the config round trip and the CLI verb SHALL report it in JSON.
  Complexity: M

- [ ] P3: RD-160: Export and import a verified asset-cache bundle for offline and mirrored fleet installs
  Why: SpotX's download chain is blocked by Cloudflare "Suspected Phishing" flags, some ISPs, and Spotify's token-gated installer links, and Chocolatey and Scoop users expect an internalized package that installs with no network; LibreSpot already keeps a verified asset cache with source, hash, and size metadata.
  Evidence: https://github.com/SpotX-Official/SpotX/issues/836; https://github.com/SpotX-Official/SpotX/issues/844; https://github.com/SpotX-Official/SpotX/issues/829; https://docs.chocolatey.org/en-us/features/package-internalizer/; `README.md` "Asset-cache inventory"; `src/powershell/shared/*AssetCache*.ps1`.
  Touches: `src/LibreSpot.Cli/Program.cs` (`cache export`, `cache import`), `schemas/fleet-cli-contract.json`, `src/powershell/shared` asset-cache functions, both composed hosts, `README.md` Managed section.
  Acceptance: WHEN `cache export --output <zip>` runs on a machine with a complete cache, it SHALL write every verified entry with its metadata and a manifest; WHEN `cache import <zip>` runs on a machine with no network, a following `install --answer-file` SHALL complete for every LibreSpot-fetched asset without a download, and Spotify itself SHALL be reported as the one asset that still needs SpotX's chain; hashes SHALL be re-verified on import.
  Complexity: M
