# Research: LibreSpot

Date: 2026-08-21. Replaces all prior research.

## Executive Summary

LibreSpot is a Windows desktop orchestrator for a pinned Spotify, SpotX, Spicetify, and Marketplace stack, with a WPF app for individuals and a separate CLI for managed deployment (`README.md`, `src/LibreSpot.Desktop`, `src/LibreSpot.Cli`). Its strongest current shape is the three-destination Home, Maintenance, and Settings shell shown in `assets/screenshots/wpf-recommended.png`, `assets/screenshots/wpf-maintenance.png`, and `assets/screenshots/wpf-custom.png`. Supply-chain checks, rollback records, health probes, localization, support bundles, and automated tests are already present across `src/LibreSpot.Core`, `src/powershell/shared`, `schemas`, and `tests`. The highest-value direction is therefore not another feature layer. It is making the simplified shell tell a common user the next safe action before exposing implementation detail.

Priority order:

1. **Now, P1, fit 5/5, impact 4/5, S:** correct recovery wording. The action called **Restore vanilla Spotify** only removes Spicetify customizations and leaves SpotX in place, while the rail claims **Everything is reversible** (`src/LibreSpot.Desktop/Properties/Strings.resx:388-390`, `:827`; `Roadmap_Blocked.md:866-876`). Risk is low, there is no prerequisite, and this closes a trust-parity gap.
2. **Now, P1, fit 5/5, impact 5/5, M:** make Home select one safe action from current state. `SimpleHomeTitle` and `SimpleHomeBody` distinguish loading, load failure, critical health, and whether Spotify exists, but the primary button always runs `ApplyRecommendedCommand` (`src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:363-385`; `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml:364-387`). Risk is medium because stale state must never invoke the wrong command; it depends only on existing typed health actions and can move beyond competitors' static install and repair buttons.
3. **Now, P1, fit 5/5, impact 5/5, L:** put Maintenance triage before diagnostics. The captured degraded state makes the user scroll through six environment cards and a compatibility matrix before any repair action (`assets/screenshots/wpf-maintenance.png`; `src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml:50-180`, `:423-450`). Risk is medium because destructive actions must remain separated; existing typed issue actions remove the need for a new backend and bring LibreSpot to repair-first parity (`src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs:23-80`).
4. **Next, P2, fit 4/5, impact 3/5, M:** request a normal Spotify close before using a bounded force-kill fallback. Both desktop and PowerShell paths currently terminate processes immediately (`src/LibreSpot.Desktop/Services/SpotifyProcessService.cs:44-56`; `src/powershell/shared/Stop-SpotifyProcesses.ps1:1-10`). Risk is medium because process races must stay bounded; an adapter plus current orchestration tests can contain it, and the result matches Windows servicing behavior ([Restart Manager](https://learn.microsoft.com/en-us/windows/win32/rstmgr/about-restart-manager)).
5. **Next, P2, fit 5/5, impact 4/5, L:** reduce Settings to four common choices in one column, with the existing expert controls behind labeled expanders. The page currently renders seven sections beside a separately scrolling profile rail (`src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml:20-91`; `assets/screenshots/wpf-custom.png`). Risk is medium because search, profiles, focus, and localization must survive the reflow; the configuration model can remain unchanged. Microsoft recommends four or five common settings, a single column, and one-level disclosure for less-used options ([Windows app settings guidance, updated 2026-04-15](https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings)).

Confidence is **Verified** for repository state, visible UI, tracker state, upstream releases, and the five code gaps on 2026-08-21. Marketplace IndexedDB durability, Spicetify v3 adoption, and full stock restoration remain **Needs live validation** because public sources and the present test rig cannot prove those behaviors (`Roadmap_Blocked.md:866-876`, `:927-956`; [Marketplace PR #1212](https://github.com/spicetify/marketplace/pull/1212)).

## Product Map

### Core workflows

- **Recommended setup:** inspect the machine, present four readiness checks, install the validated stack, relaunch Spotify, and refresh health (`src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`).
- **Custom setup:** choose installation posture, appearance, SpotX behavior, Marketplace items, extensions, apps, and local profiles (`src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml`; `src/LibreSpot.Core/AppCatalog.cs:1023-1067`).
- **Maintenance:** inspect environment and compatibility, reapply or repair components, restore backups, export support evidence, and reset when safer recovery fails (`src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`).
- **Managed deployment:** provide versioned JSON and NDJSON contracts, answer files, dry runs, receipts, and stable exit behavior through a separate CLI (`src/LibreSpot.Cli`; `schemas/fleet-cli-contract.json`; `schemas/parity-manifest.json`).

### Users

- A common Windows user who wants one guided setup and clear repair guidance (`README.md`; `assets/screenshots/wpf-recommended.png`).
- A customization user who manages themes, patches, Marketplace items, and reusable profiles (`src/LibreSpot.Desktop/Views/CustomAppearanceSection.xaml`; `src/LibreSpot.Desktop/Views/CustomProfileSummarySection.xaml`).
- An operator who needs deterministic unattended runs, machine-readable evidence, and support bundles (`src/LibreSpot.Cli`; `src/LibreSpot.Core/SupportBundleService.cs`).
- The maintainer who must validate a moving Spotify, SpotX, Spicetify, and Marketplace tuple (`src/LibreSpot.Core/CompatibilityVerdict.cs`; `src/LibreSpot.Core/UpstreamDriftService.cs`).

### Platform and distribution

- The desktop and CLI target `net10.0-windows`; the packaged WPF lane is x64 and the PowerShell lane supports Windows PowerShell 5.1 and PowerShell 7 (`src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`; `src/LibreSpot.Cli/LibreSpot.Cli.csproj`; `README.md`).
- GitHub Releases is the active distribution channel. Signing, additional package managers, ARM64 WPF, and the next public release remain explicit operator decisions in `Roadmap_Blocked.md`.
- The validated tuple remains Spotify 1.2.93, SpotX 2.0, Spicetify 2.44.0, and Marketplace 1.0.9 (`src/LibreSpot.Core/AppCatalog.cs:827-842`). Spicetify 2.44.0's release page now lists Windows compatibility through 1.2.96, but that statement alone does not validate LibreSpot's SpotX pairing ([Spicetify v2.44.0](https://github.com/spicetify/cli/releases/tag/v2.44.0)).

### Integrations and data

- Downloads use pinned source metadata, SHA256 verification, cache quarantine, and a BITS fallback (`src/LibreSpot.Core/AppCatalog.cs`; `src/powershell/shared/Download-FileSafe.ps1:18-41`).
- Archives pass entry-count, expanded-size, and path checks before extraction (`src/powershell/shared/Expand-ArchiveSafely.ps1`; [OWASP path traversal](https://owasp.org/www-community/attacks/Path_Traversal)).
- State is local and inventoried; logs and support bundles are redacted before export (`schemas/data-inventory.json`; `src/LibreSpot.Core/SupportBundleService.cs`).
- Marketplace persistence moved to IndexedDB in v1.0.9. LibreSpot records that storage boundary but does not claim a validated backup for it (`schemas/data-inventory.json:343-364`; `src/powershell/shared/Get-MarketplaceHealth.ps1:30-42`; [Marketplace v1.0.9](https://github.com/spicetify/marketplace/releases/tag/v1.0.9); [PR #1212](https://github.com/spicetify/marketplace/pull/1212)).

## Competitive Landscape

- [Spicetify CLI](https://github.com/spicetify/cli) and [Marketplace](https://github.com/spicetify/marketplace) define the compatibility and persistence ceiling. LibreSpot should continue consuming their explicit support data and release evidence. It should avoid adopting v3 while the latest public build is still beta.9 from 2026-08-19 and the schema-v2 allowlist ends at Spotify 1.2.94 ([beta.9](https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.9); [supported versions](https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json)).
- [spicetify-easyinstall](https://github.com/ohitstom/spicetify-easyinstall) makes compatible Spotify selection visible and filters choices by architecture. LibreSpot should keep compatibility choices understandable, but avoid an always-latest installer path that bypasses its reviewed tuple.
- [SpicetifyManager](https://github.com/Israleche/SpicetifyManager) treats quick repair and full restore as first-class actions. LibreSpot should copy that action hierarchy, but retain its stronger receipts, rollback gates, and pinned versions.
- [BlockTheSpot Installer](https://github.com/Nuzair46/BlockTheSpot-Installer), [BlockTheSpot Resilient](https://github.com/thomas-quant/BlockTheSpot-Resilient), and [SpotX Bash](https://github.com/SpotX-Official/SpotX-Bash) put the target Spotify build beside the patch action. LibreSpot should keep detected, pinned, and supported versions visible in diagnostics, while Home should translate those details into one safe next step.
- [BetterDiscord Installer](https://github.com/BetterDiscord/Installer) and [Vencord Installer](https://github.com/Vencord/Installer) lead with install, repair, and uninstall instead of a diagnostic inventory. LibreSpot should use the same front-loaded recovery hierarchy in Maintenance, but preserve its compatibility evidence under disclosure.
- [ReVanced Manager](https://github.com/ReVanced/revanced-manager) centers the patch task, collapses secondary patch detail, and retains actionable error state. LibreSpot should adopt that progressive task reveal, while avoiding arbitrary patch combinations that its curated catalog cannot test.
- [r2modmanPlus](https://github.com/ebkr/r2modmanPlus) proves the value of named profiles and importable configuration. LibreSpot already has `.librespot` profiles and share links, so another profile subsystem would duplicate shipped work (`src/LibreSpot.Desktop/ViewModels/MainViewModel.Profiles.cs`).
- [UniGetUI](https://github.com/marticliment/UniGetUI) separates available work, installed state, and updates. LibreSpot should copy the state-to-action distinction on Home, but not add package-manager breadth that would weaken its Spotify-specific validation.
- [Ninite](https://ninite.com/) and [Patch My PC Home Updater](https://patchmypc.com/home-updater) reduce maintenance to status plus a primary action. LibreSpot should match that clarity, but must not update Spotify or patch components independently of compatibility checks.
- [Spotify Premium](https://www.spotify.com/us/premium/) is the supported path for Spotify's paid features. LibreSpot should preserve explicit capability boundaries and avoid copy that implies Premium parity because Spotify's [User Guidelines](https://www.spotify.com/us/legal/user-guidelines/) prohibit circumventing or blocking advertisements.

## Reported Issues

The LibreSpot tracker had **0 open issues and 0 open pull requests on 2026-08-21** ([issues](https://github.com/SysAdminDoc/LibreSpot/issues); [pull requests](https://github.com/SysAdminDoc/LibreSpot/pulls)). The five closed issues all predate the v4 WPF shell and map to implemented behavior, so none should be reopened as roadmap work:

- [#1](https://github.com/SysAdminDoc/LibreSpot/issues/1) requested choices before installation; Settings and profiles now provide them (`src/LibreSpot.Desktop/Views/CustomWorkspaceView.xaml`).
- [#2](https://github.com/SysAdminDoc/LibreSpot/issues/2) reported a CSS header gap; current visual regression captures cover the rebuilt WPF shell (`tests/LibreSpot.Desktop.Tests/ReadmeScreenshotTests.cs`; `assets/screenshots`).
- [#3](https://github.com/SysAdminDoc/LibreSpot/issues/3) reported a Spicetify download 403; the current downloader has pinned URLs, WebRequest diagnostics, and BITS fallback (`src/powershell/shared/Download-FileSafe.ps1`).
- [#4](https://github.com/SysAdminDoc/LibreSpot/issues/4) reported `Expand-Archive` module failure; shared extraction and composition tests now exercise the active script lane (`src/powershell/shared/Expand-ArchiveSafely.ps1`; `tests/powershell`).
- [#5](https://github.com/SysAdminDoc/LibreSpot/issues/5) reported a blank screen; snapshot failures now leave loading state and expose Retry (`src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:363-385`; `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`).

Discussions [#20](https://github.com/SysAdminDoc/LibreSpot/discussions/20) and [#21](https://github.com/SysAdminDoc/LibreSpot/discussions/21) had 0 comments on 2026-08-21. They are maintainer announcements, not user-demand evidence. Their compatibility-breakage premise supports repair-first Maintenance, but their old `preview.24` and PowerShell-GUI wording needs an operator edit on GitHub rather than a repository feature.

The strongest external report is Marketplace [#1201](https://github.com/spicetify/marketplace/issues/1201), where extensions disappeared after reboot. It closed on 2026-08-03, while the persistence-before-reload fix in [PR #1212](https://github.com/spicetify/marketplace/pull/1212) remained open on 2026-08-21. LibreSpot should keep this in live-validation status instead of promising IndexedDB backup or adding a speculative repair path.

## Security, Privacy, and Reliability

- **Misleading recovery scope, Verified:** `Maintenance_RestoreVanilla_Title` says stock Spotify, but its own description says SpotX remains; `Roadmap_Blocked.md:873-876` confirms the action only runs `spicetify restore` (`src/LibreSpot.Desktop/Properties/Strings.resx:388-390`). Rename the action and remove the unconditional reversible claim from every locale.
- **Abrupt process termination, Verified:** the WPF service calls `Kill(entireProcessTree: true)` before any close request, and the shared PowerShell function always uses `Stop-Process -Force` (`src/LibreSpot.Desktop/Services/SpotifyProcessService.cs:44-56`; `src/powershell/shared/Stop-SpotifyProcesses.ps1:1-10`). Add normal close, bounded wait, logged fallback, and cancellation tests. Native Restart Manager interop is unnecessary for the first fix ([Restart Manager](https://learn.microsoft.com/en-us/windows/win32/rstmgr/about-restart-manager)).
- **Supply-chain guardrails already present, Verified:** hashes, reviewed catalog metadata, private-network URL checks, bounded archive extraction, cache quarantine, SBOM generation, redacted support bundles, and receipt-backed undo are implemented (`src/LibreSpot.Core/AppCatalog.cs`; `src/LibreSpot.Core/CommunityAssetDriftService.cs`; `src/powershell/shared/Expand-ArchiveSafely.ps1`; `src/LibreSpot.Core/OperationJournalUndoService.cs`; `Build-Scripts.ps1`). Preserve them rather than adding another downloader or catalog.
- **Privacy posture already present, Verified:** data stays local unless the user exports a support bundle; the inventory and redaction boundary are explicit (`schemas/data-inventory.json`; `src/LibreSpot.Core/SupportBundleService.cs`). Cloud sync and telemetry would conflict with that design.
- **Recovery ladder:** Home should choose setup, safe repair, retry, or open Spotify from health state. Maintenance should show safe repair before Reset, matching Microsoft's Repair-before-Reset guidance ([Repair apps and programs in Windows](https://support.microsoft.com/en-US/Windows/Apps/repair-apps-and-programs-in-windows)). Full stock restore stays blocked until it can restore SpotX backups on a real patched client (`Roadmap_Blocked.md:866-876`).
- **Policy boundary:** do not add ad-blocking claims, Premium entitlement claims, or external script paste flows. Spotify's policy and ClickFix campaigns make precise capability copy and checksum-verified downloads part of product safety ([Spotify User Guidelines](https://www.spotify.com/us/legal/user-guidelines/); [ClickFix threat analysis](https://rhisac.org/threat-intelligence/current-clickfix-threat-landscape-developments/)).

## Architecture Assessment

- **Create one state-derived Home action model.** Add a small view model or record with title, body, button text, automation text, command, severity, and enabled state. Build it from snapshot load state plus ordered non-destructive `HealthIssueActionViewModel` entries (`src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:363-385`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs:23-80`). Keep destructive work in Maintenance.
- **Reuse health issues in Maintenance.** `CriticalHealthIssues`, `WarningHealthIssues`, and `InfoHealthIssues` already exist, and `src/LibreSpot.Desktop/MainWindow.xaml:211-302` already defines issue templates. Move or extract those templates so `MaintenanceWorkspaceView.xaml` can render the highest-priority issue and its safe action before `StatusDashboardItems` and `CompatibilityVerdictItems`.
- **Use disclosure only in the presentation layer.** Keep `InstallConfiguration`, `AppCatalog.OptionDefinitions`, search filtering, and profile serialization unchanged (`src/LibreSpot.Core/AppCatalog.cs:1023-1053`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.CustomInstall.cs:149-199`). Recompose Settings into one column with four common choices: Spotify build on Auto, theme, Marketplace, and open Spotify when finished. Put every other existing control under one-level labeled expanders. A search match must expand and reveal its group. This follows [Windows settings guidance](https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings) and [progressive disclosure guidance](https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-progressive-disclosure-controls).
- **Make process control testable.** Keep `ISpotifyProcessService`, but isolate process enumeration, close requests, waiting, and forced fallback behind an injectable adapter. Tests can then prove ordering and time bounds without killing a real application (`src/LibreSpot.Desktop/Services/SpotifyProcessService.cs`; `tests/LibreSpot.Desktop.Tests`).
- **Tests and docs belong to each item.** Add state-matrix view-model tests, UI Automation names and focus checks, localization key-parity checks, screenshot assertions, and PowerShell parity tests with the feature that needs them (`tests/LibreSpot.Desktop.Tests`; `tests/LibreSpot.Core.Tests`; `tests/powershell`; `Build-Scripts.ps1`). Refresh all three README screenshots after UI work (`assets/screenshots`; `README.md`).
- **Category review:** security and recovery are addressed by truthful copy and staged shutdown; accessibility is part of each UI acceptance and WCAG 2.2 target/focus review ([WCAG 2.2](https://www.w3.org/TR/WCAG22/)); i18n must preserve all current satellite resources; observability stays local with a logged force-kill fallback; tests and docs travel with each change. Distribution, signing, ARM64, and extra locales remain in `Roadmap_Blocked.md`. Marketplace remains the plugin surface. Mobile ports, cloud sync, cross-user state, and a second migration system would create parallel products or duplicate existing contracts. Offline download resilience already has cache plus BITS (`src/powershell/shared/Download-FileSafe.ps1`). Spicetify v3 migration remains guarded until its stable contract exists (`src/LibreSpot.Core/SpicetifyV3ConflictDetector.cs`; `Roadmap_Blocked.md:927-956`).

## Rejected Ideas

- **Adopt Spicetify v3 now:** beta.9 was still a prerelease on 2026-08-21, and the published schema-v2 support data ends at Spotify 1.2.94 ([release](https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.9); [support data](https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json)).
- **Raise LibreSpot's tested ceiling to 1.2.96 from release notes alone:** Spicetify 2.44.0 lists that Windows range, but LibreSpot's tested SpotX pairing remains 1.2.93 and pin advancement already requires live evidence (`src/LibreSpot.Core/AppCatalog.cs:827-842`; `Roadmap_Blocked.md:927-956`).
- **Back up Marketplace IndexedDB now:** issue #1201 is closed, PR #1212 is still open, and a copied Chromium LevelDB has not been restored successfully in the available rig ([issue](https://github.com/spicetify/marketplace/issues/1201); [pull request](https://github.com/spicetify/marketplace/pull/1212); `Roadmap_Blocked.md`).
- **Implement full stock restore during this pass:** the action would overwrite patched binaries and needs a real SpotX installation to prove safe rollback (`Roadmap_Blocked.md:866-876`). Correct the label now; implement the binary restore only after that validation exists.
- **Add package managers, signing, ARM64 WPF, or more locales:** each already has a named operator or hardware dependency in `Roadmap_Blocked.md`; duplicating it in the actionable queue would misstate readiness.
- **Add another plugin framework:** Spicetify Marketplace, built-in extensions, custom apps, and community catalog already provide the extension surface (`src/LibreSpot.Core/AppCatalog.cs`; [Marketplace](https://github.com/spicetify/marketplace)).
- **Add cloud profile sync, telemetry, or shared-machine state:** these conflict with the local-only inventory and per-user paths (`schemas/data-inventory.json`; `src/LibreSpot.Core/LibreSpotPaths.cs`). Exportable profiles already cover deliberate transfer.
- **Port the shell to mobile, macOS, or Linux:** SpotX and the present process, registry, scheduled-task, and WPF integrations are Windows-specific (`src/LibreSpot.Core/LibreSpot.Core.csproj`; `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`). A port would not reuse the main product boundary.
- **Return to the dense command-center mockups:** the current Home is clearer than `assets/mockups/premium-command-center.png`, `premium-command-center-v2.png`, and `librespot-command-center-v5.png`. The remaining defects are state and hierarchy, not missing cards.
- **Add a second download engine or broad dependency refresh:** BITS fallback and pinned dependencies already exist, and the reviewed dependency changelogs exposed no user-facing capability that outranks the five gaps (`src/powershell/shared/Download-FileSafe.ps1`; project files; dependency sources below).
- **Promise ad-free Premium parity:** Spotify's official Premium page and User Guidelines make that claim inaccurate and policy-sensitive ([Premium](https://www.spotify.com/us/premium/); [User Guidelines](https://www.spotify.com/us/legal/user-guidelines/)).

## Sources

### Repository and upstream

- https://github.com/SysAdminDoc/LibreSpot
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
- https://github.com/spicetify/marketplace/issues/1201
- https://github.com/spicetify/marketplace/pull/1212
- https://github.com/SpotX-Official/SpotX
- https://github.com/SpotX-Official/SpotX-Bash
- https://github.com/spicetify/spicetify-themes

### Competitors and adjacent products

- https://github.com/ohitstom/spicetify-easyinstall
- https://github.com/Israleche/SpicetifyManager
- https://github.com/Nuzair46/BlockTheSpot-Installer
- https://github.com/thomas-quant/BlockTheSpot-Resilient
- https://github.com/BetterDiscord/Installer
- https://github.com/Vencord/Installer
- https://github.com/ReVanced/revanced-manager
- https://github.com/ReVanced/revanced-manager/releases
- https://github.com/ebkr/r2modmanPlus
- https://github.com/marticliment/UniGetUI
- https://ninite.com/
- https://patchmypc.com/home-updater
- https://www.spotify.com/us/premium/
- https://www.spotify.com/us/legal/user-guidelines/
- https://www.spotify.com/us/legal/end-user-agreement/
- https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security

### UX, accessibility, and Windows platform

- https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings
- https://learn.microsoft.com/en-us/windows/win32/uxguide/ctrl-progressive-disclosure-controls
- https://learn.microsoft.com/en-us/windows/apps/design/design-principles
- https://learn.microsoft.com/en-us/windows/win32/uxguide/vis-layout
- https://support.microsoft.com/en-US/Windows/Apps/repair-apps-and-programs-in-windows
- https://learn.microsoft.com/en-us/windows/win32/rstmgr/about-restart-manager
- https://learn.microsoft.com/en-us/windows/win32/bits/background-intelligent-transfer-service-portal
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview
- https://www.w3.org/TR/WCAG22/
- https://www.nngroup.com/articles/progressive-disclosure/
- https://www.nngroup.com/articles/ten-usability-heuristics/

### Security, integrity, and standards

- https://owasp.org/www-community/attacks/Path_Traversal
- https://www.cisa.gov/resources-tools/resources/2026-minimum-elements-software-bill-of-materials-sbom
- https://cyclonedx.org/specification/overview/
- https://slsa.dev/spec/v1.2/
- https://github.blog/changelog/2025-10-28-immutable-releases-are-now-generally-available/
- https://cli.github.com/manual/gh_release_verify-asset
- https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/
- https://github.com/dotnet/wpf/security/advisories/GHSA-gg8c-3338-xw2f
- https://rhisac.org/threat-intelligence/current-clickfix-threat-landscape-developments/
- https://www.helpnetsecurity.com/2026/06/11/vidar-infostealer-tiktok-instagram-reels-malware-campaigns/

### Dependencies, discovery, and community

- https://www.nuget.org/packages/wpf-ui/
- https://www.nuget.org/packages/CommunityToolkit.Mvvm/
- https://github.com/codebude/QRCoder/releases
- https://github.com/serilog/serilog/releases
- https://github.com/icsharpcode/AvalonEdit/releases
- https://xunit.net/releases/v3/4.0.0
- https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro
- https://github.com/pester/Pester/releases/tag/5.9.1
- https://github.com/CycloneDX/cyclonedx-dotnet/releases/tag/v6.2.0
- https://github.com/topics/spicetify
- https://www.libhunt.com/topic/spicetify
- https://www.reddit.com/r/spicetify/

## Open Questions

- **When will Spicetify v3 publish a stable support and coexistence contract?** Until a stable release freezes the schema, exit behavior, and migration path, LibreSpot cannot safely replace its v2 pin ([v3 beta releases](https://github.com/spicetify/cli/releases); `Roadmap_Blocked.md:927-956`).
- **Can Marketplace IndexedDB be copied and restored while Spotify is closed without corrupting or losing installed-item state?** The open persistence PR does not answer backup semantics, so this needs a live patched-client test ([PR #1212](https://github.com/spicetify/marketplace/pull/1212)).
