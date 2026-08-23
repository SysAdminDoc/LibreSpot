# Research: LibreSpot

Date: 2026-08-23. Replaces all prior research.

## Executive Summary

LibreSpot v4.0.0 is now a stable Windows desktop product, not a PowerShell tool with a window added on top. It already has strong compatibility pins, local rollback evidence, redacted support bundles, an unattended CLI, and a tested WPF shell (`README.md`, `src/LibreSpot.Core`, `src/LibreSpot.Desktop`, `src/LibreSpot.Cli`). The remaining common-user problem is trust and hierarchy. The README still starts by telling people to paste a long PowerShell block, while the stable desktop executable is secondary (`README.md:16-53`). More urgently, the product classifies a normal Defender configuration as degraded, prints `Add-MpPreference`, tells users to turn off Smart App Control, and describes detections as definite false positives (`src/LibreSpot.Core/EnvironmentSnapshotService.cs:387-410`; `src/LibreSpot.Desktop/Properties/Strings.resx:1108`, `:1189`; `README.md:398-405`). Microsoft states that every Defender exclusion reduces protection and that users should allow detected content only when they are confident it is safe ([Defender exclusions](https://learn.microsoft.com/en-us/defender-endpoint/configure-contextual-file-folder-exclusions-microsoft-defender-antivirus); [Protection History](https://support.microsoft.com/en-us/windows/security/windows-security-protection-history-in-the-windows-security-app)). The next product pass should first remove that unsafe advice, then make the desktop download the obvious entry point. Existing Home, Maintenance, shutdown, and Settings work remains well supported by the wider evidence.

| Rank | Opportunity | Timing | User impact | Effort | Confidence |
|---|---|---|---:|---:|---|
| 1 | RD-131: Remove endpoint-protection exception advice and the false degraded state | Now, P0 | 5/5 | M | Verified |
| 2 | RD-132: Make the stable desktop download the default common-user installation path | Now, P1 | 5/5 | S | Verified |
| 3 | RD-127: Make Home choose the next safe action from current health state | Now, P1 | 5/5 | M | Verified |
| 4 | RD-128: Put Maintenance status and safe repair before diagnostics | Now, P1 | 5/5 | L | Verified |
| 5 | RD-129: Close Spotify normally before forcing remaining processes to exit | Next, P2 | 3/5 | M | Verified |
| 6 | RD-130: Recompose Settings as essentials first with one-level disclosure | Next, P2 | 4/5 | L | Verified |
| 7 | RD-133: Show a quiet LibreSpot version notice on Home | Next, P2 | 3/5 | M | Likely |
| 8 | RD-134: Align support and planning documents with the stable v4 release | Now, P2 | 3/5 | S | Verified |

No direct NuGet dependency was outdated, deprecated, or reported vulnerable by the local dependency checks on 2026-08-23. The test project has newer transitive platform packages, but the direct owner is FlaUI and there is no demonstrated product defect. Dependency churn is not a roadmap priority.

## Product Map

### Core workflows

- **Recommended setup:** inspect the machine, explain readiness, apply the reviewed Spotify, SpotX, Spicetify, and Marketplace tuple, then launch Spotify (`src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`).
- **Settings and profiles:** adjust the supported installation choices, save local profiles, import or preview `.librespot` files, and share an inert local configuration card (`src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Profiles.cs`).
- **Maintenance and recovery:** inspect health, repair components, reapply after Spotify changes, restore a backup, export support evidence, or use the separated destructive reset (`src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`).
- **Managed deployment:** run versioned JSON and NDJSON contracts with dry-run support, answer files, receipts, and stable exit behavior (`src/LibreSpot.Cli`; `schemas/fleet-cli-contract.json`; `schemas/parity-manifest.json`).

### Users

- A common Windows user who wants one trustworthy download and one obvious next action.
- A customization user who manages themes, extensions, Marketplace apps, and reusable profiles.
- An operator who needs unattended runs, machine-readable evidence, and local support bundles.
- A maintainer who validates a moving four-part compatibility tuple and publishes immutable release evidence.

### Platform and distribution

- The desktop and CLI target `net10.0-windows`; the release publishes self-contained x64 executables. The PowerShell script remains a compatibility and automation lane (`src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`; `src/LibreSpot.Cli/LibreSpot.Cli.csproj`; `README.md:439-464`).
- GitHub Releases is the supported channel. v4.0.0 was published as the stable release on 2026-08-22 with seven immutable assets, including the desktop executable, checksums, release manifest, and SBOM ([v4.0.0 release](https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.0.0)).
- The reviewed tuple remains Spotify 1.2.93, SpotX 2.0, Spicetify 2.44.0, and Marketplace 1.0.9 (`src/LibreSpot.Core/AppCatalog.cs:827-842`). SpotX moved to Spotify 1.2.97 on 2026-08-16, while Spicetify v3 remained beta.9 on 2026-08-23. Those facts do not replace live tuple validation ([SpotX](https://github.com/SpotX-Official/SpotX); [Spicetify beta.9](https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.9)).

### Integrations and data

- Downloads are tied to reviewed source metadata and SHA256 values. The implementation rejects unsafe URLs, quarantines bad cache entries, bounds archive extraction, and can fall back to BITS (`src/LibreSpot.Core/AppCatalog.cs`; `src/powershell/shared/Download-FileSafe.ps1`; `src/powershell/shared/Expand-ArchiveSafely.ps1`).
- State remains local. Logs and support bundles are redacted before export, and the data inventory documents every owned path (`schemas/data-inventory.json`; `src/LibreSpot.Core/SupportBundleService.cs`).
- Marketplace v1.0.9 moved important state to IndexedDB. LibreSpot detects that boundary but does not claim a proven backup and restore path (`schemas/data-inventory.json:343-364`; [Marketplace v1.0.9](https://github.com/spicetify/marketplace/releases/tag/v1.0.9); [Marketplace PR #1212](https://github.com/spicetify/marketplace/pull/1212)).
- GitHub supplies release metadata and immutable-release attestation. A future version notice can reuse the current GitHub API and cache patterns without downloading or executing anything ([Releases API](https://docs.github.com/en/rest/releases/releases); [REST API best practices](https://docs.github.com/en/enterprise-cloud@latest/rest/using-the-rest-api/best-practices-for-using-the-rest-api)).

## Competitive Landscape

- [Spicetify CLI](https://github.com/spicetify/cli) and [Marketplace](https://github.com/spicetify/marketplace) define the customization and compatibility ceiling. LibreSpot should consume their explicit release evidence, but should not move to v3 while the public line is prerelease and its schema-v2 allowlist ends at Spotify 1.2.94 ([supported versions](https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json)).
- [SpotX](https://github.com/SpotX-Official/SpotX) and [SpotX Bash](https://github.com/SpotX-Official/SpotX-Bash) move quickly with Spotify releases. Their speed is useful upstream evidence. LibreSpot should keep its slower reviewed tuple and its guard against upstream Defender mutations.
- [spicetify-easyinstall](https://github.com/ohitstom/spicetify-easyinstall) makes installation and compatible Spotify selection visible. LibreSpot should match that plain-language path while retaining stronger hashes, receipts, and rollback gates.
- [SpicetifyManager](https://github.com/Israleche/SpicetifyManager) makes quick repair and full restore first-class actions. That supports RD-128. LibreSpot should not collapse safe repair and destructive reset into one visual tier.
- [BlockTheSpot Installer](https://github.com/Nuzair46/BlockTheSpot-Installer) puts a direct executable and a recommended version beside the main action. This is good common-user distribution evidence for RD-132.
- [BlockTheSpot Resilient](https://github.com/thomas-quant/BlockTheSpot-Resilient) avoids crashes when runtime offsets do not match and publishes generated patches rapidly. The no-op-on-mismatch lesson is sound. Its high-frequency patch channel would weaken LibreSpot's curated compatibility model.
- [BetterDiscord Installer](https://github.com/BetterDiscord/Installer) and [Vencord Installer](https://github.com/Vencord/Installer) lead with install, repair, and uninstall. LibreSpot should copy their action hierarchy, while keeping diagnostics available under disclosure.
- [ReVanced Manager](https://github.com/ReVanced/revanced-manager) centers one patch task and makes managed updates visible. LibreSpot should use a quiet update notice, not add an automatic executable updater before its packaging and identity decisions are resolved.
- [r2modmanPlus](https://github.com/ebkr/r2modmanPlus) shows the value of named profiles, import, export, and clear warnings about fake distribution sites. LibreSpot already ships equivalent local profile concepts, so a second profile system would be duplicate work.
- [UniGetUI](https://github.com/Devolutions/UniGetUI) separates installed state, available work, and updates. That supports a state-derived Home action and a visible own-version notice. Its package-manager breadth does not fit a curated Spotify stack.
- [Ninite](https://ninite.com/help/how-ninite-works/) automatically chooses architecture and language, validates publisher signatures or hashes, and retries failed installers. Its strongest lesson is that a common user should not need to understand the delivery mechanism. [Ninite Pro](https://ninite.com/help/features/) also shows that offline caches and fleet audit features belong to managed tooling, which LibreSpot already separates into its CLI.
- [Patch My PC Home Updater](https://patchmypc.com/product/home-updater/) begins with scan status and an action instead of raw diagnostic detail. This supports RD-127 and RD-128, but LibreSpot must keep component updates behind tuple validation.
- [Nexus Mods App](https://github.com/Nexus-Mods/NexusMods.App), [Heroic Games Launcher](https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher), and [Stability Matrix](https://github.com/LykosAI/StabilityMatrix) keep specialist content behind a task-oriented desktop shell. Their broader ecosystems do not justify another plugin layer here.
- Spotify remains the supported source for Premium entitlements. LibreSpot copy should not promise Premium parity or imply that customization removes account and policy risk ([Premium benefits](https://support.spotify.com/us/article/your-premium-benefits/); [User Guidelines](https://www.spotify.com/us/legal/user-guidelines/)).

## Reported Issues

LibreSpot had 0 open issues and 0 open pull requests on 2026-08-23 ([issues](https://github.com/SysAdminDoc/LibreSpot/issues); [pull requests](https://github.com/SysAdminDoc/LibreSpot/pulls)). Closed issues #1 through #5 describe the old script-era UI or failures that now have direct tests and recovery paths. Discussions [#20](https://github.com/SysAdminDoc/LibreSpot/discussions/20) and [#21](https://github.com/SysAdminDoc/LibreSpot/discussions/21) had no comments and still described preview.24 or a PowerShell GUI. They are stale maintainer announcements, not evidence of user demand.

External reports do show a repeated support pattern:

- Spotify updates can leave Spicetify unapplied or visually missing, and users fall back to terminal repair instructions ([update recovery thread](https://www.reddit.com/r/spicetify/comments/1sot0tx/how_to_fix_spicetify_after_an_update_windows/); [recent update failure](https://www.reddit.com/r/spicetify/comments/1umtw10/spicetify_not_working_after_update/); [another update failure](https://www.reddit.com/r/spicetify/comments/1rgasch/spicetify_no_longer_working_after_recent_spotify/)). This supports Home state selection, safe repair, and an own-version notice.
- Marketplace items and customization state can disappear after restart or update ([Marketplace state thread](https://www.reddit.com/r/spicetify/comments/1u6rcxr/spicetify_keeps_losing_installedmarketplace/); [reset thread](https://www.reddit.com/r/spicetify/comments/1t2wgj5/spicetify_resetting_not_just_on_spotify_update/); [Marketplace issue #1201](https://github.com/spicetify/marketplace/issues/1201)). The available evidence does not prove that copying IndexedDB is safe, so this remains a live-validation question.
- New users describe the repair cycle as too technical ([new-user thread](https://www.reddit.com/r/spicetify/comments/1t47rml/im_a_new_user_and_spicetify_is_becoming_too/)). This is directional community evidence, not a product specification. The repository state and Windows guidance provide the stronger basis for RD-127, RD-128, and RD-132.
- Stack Overflow, Hacker News, and Lobsters produced sparse or incidental discussion rather than a stable LibreSpot demand signal ([Hacker News mention](https://news.ycombinator.com/item?id=34795179); [Stack Overflow search](https://stackoverflow.com/search?q=spicetify); [Lobsters search](https://lobste.rs/search?q=spicetify&what=stories&order=newest)). No roadmap item relies on those searches alone.

## Security, Privacy, and Reliability

- **Endpoint-protection advice, Verified:** a missing Defender exclusion creates a warning-level `antivirus-exclusion` health component, localized resources print an elevated `Add-MpPreference` command, the FAQ says detections are definitely false positives, and Smart App Control guidance tells personal users to switch it off (`src/LibreSpot.Core/EnvironmentSnapshotService.cs:387-410`; `src/LibreSpot.Desktop/Properties/Strings.resx:1108`, `:1189`; `README.md:398-405`; `src/powershell/shared/Write-PowerShellSecurityContext.ps1:9`). This contradicts `README.md:61-69` and `SECURITY.md:113`, which correctly warn users not to disable Defender or add exclusions. Microsoft states that each exclusion lowers protection and that Smart App Control blocks untrusted or unsigned code by design ([Defender exclusions](https://learn.microsoft.com/en-us/defender-endpoint/configure-contextual-file-folder-exclusions-microsoft-defender-antivirus); [Smart App Control](https://support.microsoft.com/en-US/Windows/Security/Windows-Security/app-browser-control-in-the-windows-security-app); [SAC developer guidance](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/test-your-app-with-smart-app-control)). RD-131 should remove the warning state and unsafe instructions while preserving actual quarantine recovery through Protection History and vendor submission.
- **PowerShell-paste entry point, Verified:** the first install path asks a user to paste a 17-line PowerShell block, and a lower-trust `irm | iex` command remains in the same Quick Start (`README.md:16-53`). Microsoft tracks ClickFix campaigns that condition users to paste commands into Run, Terminal, or PowerShell, often using `iwr`, `irm`, or `iex` ([Microsoft ClickFix analysis](https://www.microsoft.com/en-us/security/blog/2025/08/21/think-before-you-clickfix-analyzing-the-clickfix-social-engineering-technique/); [CrashFix variant](https://www.microsoft.com/en-us/security/blog/2026/02/05/clickfix-variant-crashfix-deploying-python-rat-trojan/); [Microsoft malware encyclopedia](https://www.microsoft.com/en-us/wdsi/threats/malware-encyclopedia-description?Name=Behavior%3AWin32%2FClickFix); [CISA Interlock advisory](https://www.cisa.gov/sites/default/files/2025-07/aa25-203a-stopransomware-interlock-072225.pdf)). Windows PowerShell 5.1 also required a December 2025 fix because `Invoke-WebRequest` could execute page script during parsing, before a later hash comparison ([Microsoft CVE guidance](https://support.microsoft.com/en-us/servicing/os/windows/2025/12/powershell-5-1-invoke-webrequest-preventing-script-execution-from-web-content); [CVE-2025-54100](https://nvd.nist.gov/vuln/detail/CVE-2025-54100)). RD-132 should make the stable desktop asset the first path and retain scripts for advanced use.
- **Release integrity, Verified:** v4.0.0 includes checksums, a release manifest, an SBOM, and immutable GitHub release evidence (`README.md:439-464`; [immutable releases](https://github.blog/changelog/2025-10-28-immutable-releases-are-now-generally-available/); [release verification](https://cli.github.com/manual/gh_release_verify)). These controls prove artifact identity. They do not prove that an antivirus detection is harmless, so the FAQ must preserve uncertainty and tell users to stop on any identity mismatch.
- **Abrupt process termination, Verified:** desktop and PowerShell paths currently force Spotify processes to exit (`src/LibreSpot.Desktop/Services/SpotifyProcessService.cs:44-56`; `src/powershell/shared/Stop-SpotifyProcesses.ps1:1-10`). RD-129 remains justified. A bounded normal-close request before survivor-only force matches Windows servicing behavior without requiring full Restart Manager interop ([Restart Manager](https://learn.microsoft.com/en-us/windows/win32/rstmgr/about-restart-manager)).
- **Recovery hierarchy, Verified:** the Maintenance screenshot puts environment cards and compatibility data ahead of repair (`assets/screenshots/wpf-maintenance.png`; `src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml:50-180`, `:423-450`). Microsoft's repair-before-reset guidance supports RD-128 ([Repair apps](https://support.microsoft.com/en-US/Windows/Apps/repair-apps-and-programs-in-windows)).
- **Privacy posture, Verified:** the product is local-first, inventories owned data, and redacts support bundles before export (`schemas/data-inventory.json`; `src/LibreSpot.Core/SupportBundleService.cs`). Cloud sync, telemetry, and shared-machine state would weaken this boundary without solving a reported problem.
- **Dependency health, Verified:** direct desktop, Core, CLI, and test package checks on 2026-08-23 found no known vulnerable or deprecated package and no direct update. The .NET floor already includes the August 2026 servicing fixes (`schemas/dependency-health-allowlist.json`; [.NET August 2026 servicing](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/); [WPF advisories](https://github.com/dotnet/wpf/security)).
- **Warning quality:** research on warning habituation shows that repeated or poorly timed warnings lose effectiveness. LibreSpot should reserve warnings for actual unsafe state, not for a secure Defender default ([warning habituation study](https://www.sciencedirect.com/science/article/pii/S0167923616301592); [USENIX warning study](https://www.usenix.org/sites/default/files/sec13_proceedings_interior.pdf)).

## Architecture Assessment

- **Security state should report evidence, not a preferred Defender configuration.** Remove `antivirus-exclusion` as a warning producer. Keep the existing upstream SpotX mutation scan that rejects `Add-MpPreference` and related tokens because that is a supply-chain guard, not user advice (`src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:336-342`). Actual quarantine recovery should continue through `Get-QuarantineGuidance.ps1`, Windows Security Protection History, and a vendor false-positive submission ([Microsoft false-positive guidance](https://learn.microsoft.com/en-us/defender-endpoint/defender-endpoint-false-positives-negatives); [Microsoft file submission](https://www.microsoft.com/en-us/wdsi/filesubmission)).
- **Distribution should follow product architecture.** The stable WPF executable should be the first README path. Checksums and same-release provenance stay adjacent. The PowerShell source, PS2EXE compatibility artifact, and CLI remain available under advanced and managed sections. This changes hierarchy, not release mechanics.
- **Create one state-derived Home action model.** A small model should own title, body, button text, command, enabled state, automation text, and severity. Build it from snapshot load state plus ordered non-destructive health actions (`src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:363-385`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs:23-80`). RD-127 already captures the tests and failure states.
- **Reuse typed health issues in Maintenance.** `CriticalHealthIssues`, `WarningHealthIssues`, and `InfoHealthIssues` already exist. Extract the current issue templates and render the highest-priority safe action before diagnostics. Keep Reset in its separate danger tier. RD-128 should not add another backend.
- **Keep Settings simplification in the view layer.** `InstallConfiguration`, catalog definitions, search, and profile serialization can stay unchanged. RD-130 should recompose them into one column with one-level expanders. Search must reveal a matching hidden option. Microsoft recommends no more than four or five common settings and a single-column layout ([Windows app settings](https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings); [progressive disclosure](https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-progressive-disclosure-controls)). A 2025 layered-interface study also found that staged detail can improve novice performance without removing expert access ([layered-interface study](https://journals.sagepub.com/doi/10.1177/10648046241273291)).
- **Own-version checks should remain notice-only.** A small cached service can call the latest stable GitHub Release endpoint, compare semantic versions, and expose a link only when newer. Reuse the existing GitHub API client and cache conventions in `UpstreamDriftService`. Do not download or launch an updater. Conditional requests and a 24-hour cache keep it quiet and rate-limit friendly ([GitHub REST best practices](https://docs.github.com/en/enterprise-cloud@latest/rest/using-the-rest-api/best-practices-for-using-the-rest-api)).
- **Make process control testable.** Keep `ISpotifyProcessService`, but isolate enumeration, close requests, waiting, and force fallback behind an injectable adapter. Tests can prove ordering and a fixed upper bound without touching a real user process.
- **Accessibility and localization travel with each UI item.** UI work must retain keyboard focus, AutomationProperties, target size, contrast, and all five satellite resources. WCAG 2.2 remains the baseline ([WCAG 2.2](https://www.w3.org/TR/WCAG22/)).
- **Stable-release truth needs one contract.** `SECURITY.md:8` still calls v4.0.x preview and best-effort. `Roadmap_Blocked.md` still contains pending SignPath enrollment, v3.7.2 stable-channel assumptions, preview release work, and pre-.NET 10 decisions that conflict with the shipped v4.0.0 state. `README.md`, `SECURITY.md`, `SIGNPATH.md`, and the blocked plan should agree that v4 is stable and unsigned by design. RD-134 should also add a cheap release-truth regression check.

## Rejected Ideas

- **Adopt Spicetify v3 now:** beta.9 was still a prerelease on 2026-08-23, and the schema-v2 support data ended at Spotify 1.2.94 ([release](https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.9); [support data](https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json); [architecture issue](https://github.com/spicetify/cli/issues/3038)).
- **Advance the Spotify and SpotX pins from upstream headlines:** SpotX targeted Spotify 1.2.97 on 2026-08-16 and Spicetify 2.44.0 documents a wider range, but neither fact proves the combined LibreSpot path. Existing drift gates and live validation should continue to own pin advancement.
- **Back up Marketplace IndexedDB now:** issue #1201 is closed and PR #1212 remained open on 2026-08-23. Neither proves that copying Chromium state is recoverable (`Roadmap_Blocked.md`; [issue](https://github.com/spicetify/marketplace/issues/1201); [pull request](https://github.com/spicetify/marketplace/pull/1212)).
- **Add automatic executable self-update:** a visible notice solves version awareness without creating a second downloader and replacement mechanism. Velopack, package identity, and channel migration already have explicit blocked decisions in `Roadmap_Blocked.md`.
- **Resume SignPath or package-manager work:** unsigned-by-design is the current decision in `SIGNPATH.md`. Winget, Scoop, Chocolatey, Velopack, and signing depend on separate product or operator decisions already recorded in `Roadmap_Blocked.md`.
- **Add another plugin framework:** Marketplace, built-in extensions, custom apps, themes, and the reviewed community catalog already supply the extension surface (`src/LibreSpot.Core/AppCatalog.cs`; [Marketplace](https://github.com/spicetify/marketplace)).
- **Add cloud sync, telemetry, or multi-user state:** these conflict with the local data inventory and per-user ownership model. Exportable profiles already cover deliberate transfer.
- **Port the product to mobile, macOS, or Linux:** WPF, SpotX, registry checks, scheduled tasks, and Spotify process control are Windows-specific. A port would become a separate product rather than simplify this one.
- **Add a light theme or more locales before the shell reflow:** both have existing product decisions in `Roadmap_Blocked.md`. The current accessibility and localization burden should move with RD-127, RD-128, RD-130, and RD-133 first.
- **Adopt every new dependency release:** WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, Serilog 4.4.0, xUnit v3, Pester 6, and CycloneDX .NET 6.2 were reviewed. Local checks found no direct package gap that outranks user safety and task hierarchy ([WPF-UI releases](https://github.com/lepoco/wpfui/releases); [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm/); [Serilog releases](https://github.com/serilog/serilog/releases); [xUnit releases](https://xunit.net/releases/); [Pester releases](https://github.com/pester/Pester/releases); [CycloneDX .NET releases](https://github.com/CycloneDX/cyclonedx-dotnet/releases)).
- **Copy BlockTheSpot Resilient's generated update cadence:** its mismatch handling is useful, but daily generated patch releases and automatic movement do not fit LibreSpot's reviewed tuple and immutable release model.
- **Add background notifications for updates:** Home is already the common-user status surface. A quiet inline notice is less intrusive and easier to test than shell notifications.

## Sources

### Repository and upstream

- https://github.com/SysAdminDoc/LibreSpot
- https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.0.0
- https://github.com/SysAdminDoc/LibreSpot/issues
- https://github.com/SysAdminDoc/LibreSpot/pulls
- https://github.com/SysAdminDoc/LibreSpot/discussions/20
- https://github.com/SysAdminDoc/LibreSpot/discussions/21
- https://github.com/spicetify/cli
- https://spicetify.app/docs/getting-started/
- https://spicetify.app/docs/advanced-usage/customizations/
- https://github.com/spicetify/cli/releases/tag/v2.44.0
- https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.9
- https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/marketplace
- https://github.com/spicetify/marketplace/releases/tag/v1.0.9
- https://github.com/spicetify/marketplace/issues/111
- https://github.com/spicetify/marketplace/issues/273
- https://github.com/spicetify/marketplace/issues/1186
- https://github.com/spicetify/marketplace/issues/1201
- https://github.com/spicetify/marketplace/pull/1212
- https://github.com/SpotX-Official/SpotX
- https://github.com/SpotX-Official/SpotX-Bash
- https://github.com/spicetify/spicetify-themes

### Direct competitors and adjacent products

- https://github.com/ohitstom/spicetify-easyinstall
- https://github.com/Israleche/SpicetifyManager
- https://github.com/Nuzair46/BlockTheSpot-Installer
- https://github.com/thomas-quant/BlockTheSpot-Resilient
- https://github.com/BetterDiscord/Installer
- https://github.com/Vencord/Installer
- https://github.com/ReVanced/revanced-manager
- https://github.com/ebkr/r2modmanPlus
- https://github.com/Devolutions/UniGetUI
- https://github.com/Nexus-Mods/NexusMods.App
- https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher
- https://github.com/LykosAI/StabilityMatrix
- https://ninite.com/help/how-ninite-works/
- https://ninite.com/help/features/
- https://patchmypc.com/product/home-updater/
- https://support.spotify.com/us/article/your-premium-benefits/
- https://www.spotify.com/us/premium/
- https://www.spotify.com/us/legal/user-guidelines/

### Community, forums, and discovery

- https://www.reddit.com/r/spicetify/comments/1sot0tx/how_to_fix_spicetify_after_an_update_windows/
- https://www.reddit.com/r/spicetify/comments/1u6rcxr/spicetify_keeps_losing_installedmarketplace/
- https://www.reddit.com/r/spicetify/comments/1rgasch/spicetify_no_longer_working_after_recent_spotify/
- https://www.reddit.com/r/spicetify/comments/1umtw10/spicetify_not_working_after_update/
- https://www.reddit.com/r/spicetify/comments/1t2wgj5/spicetify_resetting_not_just_on_spotify_update/
- https://www.reddit.com/r/spicetify/comments/1t47rml/im_a_new_user_and_spicetify_is_becoming_too/
- https://news.ycombinator.com/item?id=34795179
- https://stackoverflow.com/search?q=spicetify
- https://lobste.rs/search?q=spicetify&what=stories&order=newest
- https://github.com/topics/spicetify
- https://www.libhunt.com/topic/spicetify

### Windows, UX, accessibility, and release platform

- https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings
- https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-progressive-disclosure-controls
- https://support.microsoft.com/en-US/Windows/Apps/repair-apps-and-programs-in-windows
- https://learn.microsoft.com/en-us/windows/win32/rstmgr/about-restart-manager
- https://learn.microsoft.com/en-us/windows/win32/bits/background-intelligent-transfer-service-portal
- https://www.w3.org/TR/WCAG22/
- https://docs.github.com/en/rest/releases/releases
- https://docs.github.com/en/enterprise-cloud@latest/rest/using-the-rest-api/best-practices-for-using-the-rest-api
- https://github.blog/changelog/2025-10-28-immutable-releases-are-now-generally-available/
- https://cli.github.com/manual/gh_release_verify
- https://support.microsoft.com/en-US/Windows/Security/Windows-Security/app-browser-control-in-the-windows-security-app
- https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/test-your-app-with-smart-app-control
- https://support.microsoft.com/en-us/windows/security/windows-security-protection-history-in-the-windows-security-app
- https://learn.microsoft.com/en-us/defender-endpoint/configure-contextual-file-folder-exclusions-microsoft-defender-antivirus
- https://learn.microsoft.com/en-us/defender-endpoint/defender-endpoint-false-positives-negatives
- https://www.microsoft.com/en-us/wdsi/filesubmission

### Security, research, and conference material

- https://www.microsoft.com/en-us/security/blog/2025/08/21/think-before-you-clickfix-analyzing-the-clickfix-social-engineering-technique/
- https://www.microsoft.com/en-us/security/blog/2026/02/05/clickfix-variant-crashfix-deploying-python-rat-trojan/
- https://www.microsoft.com/en-us/wdsi/threats/malware-encyclopedia-description?Name=Behavior%3AWin32%2FClickFix
- https://www.cisa.gov/sites/default/files/2025-07/aa25-203a-stopransomware-interlock-072225.pdf
- https://support.microsoft.com/en-us/servicing/os/windows/2025/12/powershell-5-1-invoke-webrequest-preventing-script-execution-from-web-content
- https://nvd.nist.gov/vuln/detail/CVE-2025-54100
- https://journals.sagepub.com/doi/10.1177/10648046241273291
- https://www.sciencedirect.com/science/article/pii/S0167923616301592
- https://www.usenix.org/sites/default/files/sec13_proceedings_interior.pdf
- https://www.sciencedirect.com/science/article/abs/pii/S0747563215003854
- https://www.usenix.org/conference/15th-usenix-security-symposium/secure-software-updates-not-really
- https://www.virusbulletin.com/conference/vb2025/abstracts/clickfix-exploiting-clipboard-multi-stage-payload-delivery-across-os-platforms/
- https://www.usenix.org/conference/usenixsecurity26/steindler

### Dependencies and engineering

- https://github.com/lepoco/wpfui/releases
- https://www.nuget.org/packages/CommunityToolkit.Mvvm/
- https://github.com/Shane32/QRCoder/releases
- https://github.com/serilog/serilog/releases
- https://www.nuget.org/packages/Serilog.Sinks.File/
- https://github.com/icsharpcode/AvalonEdit/blob/master/ChangeLog.md
- https://xunit.net/releases/
- https://www.nuget.org/packages/xunit.v3
- https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-migration-from-v1-to-v2
- https://github.com/pester/Pester/releases
- https://github.com/CycloneDX/cyclonedx-dotnet/releases
- https://www.nuget.org/packages/coverlet.collector
- https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/
- https://github.com/dotnet/wpf/security
- https://github.com/dotnet/wpf/security/advisories/GHSA-jqhp-238x-qhgf
- https://nvd.nist.gov/vuln/detail/CVE-2026-50523

## Open Questions

- **When will Spicetify v3 publish a stable support and coexistence contract?** Until a stable release fixes its schema, exit behavior, and migration path, LibreSpot cannot safely replace its v2 pin ([v3 releases](https://github.com/spicetify/cli/releases); `Roadmap_Blocked.md:927-956`).
- **Can Marketplace IndexedDB be copied and restored while Spotify is closed without corrupting or losing installed-item state?** The current persistence work does not establish backup semantics, so this needs a live patched-client restore test ([Marketplace PR #1212](https://github.com/spicetify/marketplace/pull/1212)).
- **What do Defender, SmartScreen, and Smart App Control show for each v4.0.0 artifact on a clean stock Windows 11 machine?** The secure copy change does not depend on this answer. Future format or signing decisions do need isolated clean-machine evidence rather than assumptions from the maintainer machine.
