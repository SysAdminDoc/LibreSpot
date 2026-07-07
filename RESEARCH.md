# Research — LibreSpot

## Executive Summary
LibreSpot is a Windows-first, local-only SpotX + Spicetify orchestrator with a modern WPF command center, the original PowerShell GUI, and a .NET fleet CLI that share pinned download, health, support-bundle, and operation-journal contracts. Its strongest current shape is trust and recovery: SHA256-pinned upstream downloads, asset-cache health, Marketplace visibility evidence, redacted support bundles, Intune/PDQ-friendly exit codes, and explicit legal/risk copy. Highest-value direction: keep pins current while turning long-running and failed installs into self-explaining, exportable, retryable operations. Top opportunities: refresh the July 2026 SpotX/Spicetify/Marketplace pins; add a WPF backend host stall watchdog; expose one-click failure-bundle export from the activity dialog; gate README screenshots against current WPF version drift; complete existing xUnit v3, localization, log virtualization, high-contrast, and package/signing roadmap rows without duplicating them.

## Product Map
- Core workflows: recommended install, custom SpotX/Spicetify profile editing, local profile import/export, maintenance repair/reapply/restore/reset, support-bundle export, auto-reapply watcher, and fleet CLI status/detect/validate/plan/install/reapply/repair/uninstall/export-support.
- User personas: non-technical Windows Spotify users, power users maintaining theme/extension profiles, endpoint admins using silent NDJSON flows and answer files, and maintainers managing upstream drift, signing, release, and legal risk.
- Platforms and distribution: Windows 10/11, Windows PowerShell 5.1+, PowerShell 7.6 LTS, .NET 10 WPF/CLI `win-x64`, PS2EXE script artifact, WPF desktop artifact, CLI artifact, and draft winget/Scoop/Chocolatey/Velopack lanes blocked by signing/package identity.
- Key integrations and data flows: SpotX `run.ps1`, Spicetify CLI, Marketplace, theme/extension/custom-app archives, GitHub release/tag metadata, `%APPDATA%` and `%LOCALAPPDATA%` Spotify/Spicetify/LibreSpot state, `%ProgramData%\LibreSpot` fleet logs, scheduled tasks, release manifest/checksums/SBOM, schemas, and redacted support zips.

## Competitive Landscape
- SpotX: owns Windows Spotify patching, Microsoft Store exclusion guidance, mirror/download modes, and current Spotify `1.2.93` targeting. LibreSpot should consume its updates through pinned hashes and compatibility smoke, while avoiding raw `iex` install behavior and generic child-script failures.
- Spicetify CLI, Marketplace, and themes: provide the core customization ecosystem, `backup`/`apply`/`restore`, Marketplace installation, css-map churn, and signed release/attestation patterns. LibreSpot should track v2.44.0/v1.0.9 and keep direct bundled choices as the reliable baseline.
- BlockTheSpot, xManager, and ReVanced-family Spotify patchers: show high enforcement and archive risk around modified binaries/mobile premium-unlock positioning. LibreSpot should keep the desktop-only wrapper posture, no patched-binary redistribution, and explicit restore/legal copy.
- Spotube, ncspot, and librespot-org/librespot: useful adjacent clients for packaging, plugin, and cross-platform matrix ideas, but they use different playback/auth/account models and should not become LibreSpot install targets.
- Ninite Pro, Patch My PC, PDQ Deploy, and Intune Win32 apps: set expectations for background operation, offline/cache behavior, return codes, detection rules, retry queues/heartbeat, and collectable logs. LibreSpot already matches much of this via CLI; WPF failure export and host-level stall handling are the next parity gaps.
- WPF UI, Microsoft WPF accessibility, and Windows contrast-theme docs: reinforce the existing WPF direction: native controls, UI Automation, live regions, high-contrast validation, and virtualization for large logs.
- Windhawk: a Windows customization-marketplace analogue; learn provenance/review/state tracking, but do not expand LibreSpot into hosted mod distribution before local profile and Marketplace-state boundaries are settled.

## Security, Privacy, and Reliability
- Verified: local docs and code still pin SpotX `3284673d` / Spotify `1.2.92`, Spicetify CLI `2.43.2`, Marketplace `1.0.8`, and themes `df033493` in `README.md`, `ROADMAP.md`, `src/LibreSpot.Desktop/Models/AppCatalog.cs`, and `schemas/community-assets.json`; live upstream evidence now shows Spicetify CLI `v2.44.0` with Spotify `1.2.93` support and Marketplace `v1.0.9`.
- Verified: the previous SpotX child-process failure gap is closed in `src/powershell/shared/Get-SpotXChildFailureClassification.ps1`, `src/powershell/shared/Invoke-ExternalScriptIsolated.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`, `LibreSpot.ps1`, and `tests/powershell/LibreSpot.Tests.ps1`.
- Verified: the WPF backend host in `src/LibreSpot.Desktop/Services/BackendScriptService.cs` has cancellation/kill-tree support but no host-level idle watchdog or hard action budget; a backend script that stops emitting events can still leave the activity surface waiting until the user cancels.
- Verified: activity failure UI already shows copy-log/open-folder controls and the Support Bundle workspace can export redacted diagnostics, but the failed activity dialog does not expose a one-click failure bundle action tied to the current run.
- Verified: README WPF screenshots are visually stale relative to `v4.0.0-preview.9`; the captured images display `v4.0.0-preview.8` while README badges and WPF/CLI version strings show preview.9.
- Verified: `dotnet list package --vulnerable --include-transitive` found no vulnerable NuGet packages for WPF or tests; `dotnet list package --deprecated --include-transitive` still reports xUnit v2 legacy packages, already tracked in `ROADMAP.md`.
- Verified: all current PowerShell `Invoke-WebRequest` call sites found in `LibreSpot.ps1`, `src/powershell/shared`, and the WPF backend include `-UseBasicParsing`; CVE-2025-54100 remains correctly documented as patch-level risk, not just a hash-integrity issue.
- Likely: Spotify CEF/css-map churn and upstream downloader outages will continue; LibreSpot should favor compatibility evidence, clear retry guidance, cache fallback, and exportable diagnostics over broad feature expansion.

## Architecture Assessment
- Keep health, drift, cache, Marketplace, and support-bundle signals centered on `EnvironmentSnapshotService`, `UpstreamDriftService`, `CommunityAssetDriftService`, and `SupportBundleService`; avoid adding another status plane.
- Pin refresh should update `AppCatalog`, PowerShell globals, backend copies, fixture expectations, README compatibility matrix, package templates if versioned URLs change, and tests in one logical batch.
- Backend liveness belongs in `BackendScriptService` and `MainViewModel` activity state, not inside every PowerShell function. The backend event protocol can remain stable if the host emits desktop-side heartbeat/stall messages.
- Failure export should reuse `SupportBundleService` with current-run context rather than inventing a second diagnostic archive format.
- Screenshot drift is a release hygiene gap: the existing `--uia-smoke`/`--uia-capture` path should produce current screenshots and tests/release checks should catch version mismatch before README claims freshness.
- Test gaps: backend host no-output/stall fixture, current-run support bundle export tests, screenshot/version drift guard, xUnit v3 migration proof, and focused pin-refresh install/reapply smoke.

## Rejected Ideas
- Mobile patch manager from xManager/ReVanced: Windows desktop architecture mismatch and higher patched-APK/premium-unlock enforcement risk.
- BlockTheSpot migration: archived upstream and DLL-injection workflow conflict with LibreSpot's SpotX+Spicetify wrapper/trust model.
- Alternative client installer from Spotube/ncspot/librespot-org/librespot: different playback/account/auth model; keep as comparison matrix only.
- Hosted telemetry: conflicts with local-only privacy posture; opt-in redacted support bundles are enough.
- Public package-manager submission now: already blocked by package identity, signing, updater ownership, and channel trust copy in `Roadmap_Blocked.md`.
- Marketplace cloud backup/sync: already blocked on policy because Marketplace state can include unmanaged IndexedDB/browser data outside LibreSpot's owned schema.
- Full Spicetify v3 implementation now: no stable v3 contract found; keep migration readiness fixture-backed until upstream module metadata stabilizes.

## Sources

### Project and Upstream
- https://github.com/SpotX-Official/SpotX
- https://github.com/spicetify/cli
- https://github.com/spicetify/cli/releases/tag/v2.44.0
- https://github.com/spicetify/marketplace
- https://github.com/spicetify/marketplace/releases/tag/v1.0.9
- https://github.com/spicetify/spicetify-themes

### Competitors and Analogues
- https://github.com/mrpond/BlockTheSpot
- https://github.com/Team-xManager/xManager
- https://github.com/TheWinner02/ReVancedXposed_Spotify
- https://github.com/KRTirtho/spotube
- https://github.com/hrkfdn/ncspot
- https://github.com/librespot-org/librespot
- https://github.com/ramensoftware/windhawk
- https://ninite.com/pro
- https://ninite.com/help/features/cache.html
- https://patchmypc.com/product/home-updater/
- https://docs.pdq.com/current-version/Deploy/heartbeat.htm
- https://learn.microsoft.com/en-us/intune/app-management/deployment/add-win32
- https://learn.microsoft.com/en-us/intune/app-management/deployment/troubleshoot-win32

### Platform, Security, and Standards
- https://www.spotify.com/us/legal/user-guidelines/
- https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security
- https://nvd.nist.gov/vuln/detail/CVE-2025-54100
- https://support.microsoft.com/en-us/topic/powershell-5-1-invoke-webrequest-preventing-script-execution-from-web-content-7cb95559-655e-43fd-a8bd-ceef2406b705
- https://learn.microsoft.com/en-us/windows/package-manager/package/manifest
- https://docs.velopack.io/
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes

## Open Questions
- Package identity/signing remains operator-gated: final display name, package IDs, publisher string, signing provider, update ownership, and migration policy block public package-manager channels.
- Windows lifecycle posture remains operator-gated: warn-only versus block behavior for Windows 10 Home/Pro after end of support affects README/package metadata and downloader warnings.
