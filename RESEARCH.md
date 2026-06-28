# Research - LibreSpot

## Executive Summary

LibreSpot is a Windows-only SpotX + Spicetify orchestrator with a stable PowerShell WPF script, a .NET 10 WPF preview shell, and a new fleet CLI preview. Its strongest current shape is trust-first orchestration: hash-verified upstream downloads, compatibility warnings, a typed health dashboard, issue-level repair actions, local support bundles, dry-run planning, and schema-backed release/fleet contracts. Highest-value direction: make public contracts match runtime behavior before broad distribution. Priority opportunities: finish CLI output/schema conformance before mutating verbs; repair stale release-trust docs after the local-only build policy; guard draft package manifests; define Windows 10/11 support policy before package publication; keep Spicetify behind a v3-aware integration boundary; complete local profiles and inert imports; replace source-string WPF tests with rendered UI automation; and refresh stale test tooling.

## Product Map

- Core workflows: Recommended install, Custom SpotX/Spicetify selection, Maintenance repair/reset, support bundle export, auto-reapply watcher, WPF health/activity/undo views, and CLI status/detect/validate/plan/dry-run.
- User personas: non-technical Windows Spotify users, power users selecting themes/extensions/SpotX flags, and endpoint admins who need deterministic output, exit codes, answer files, logs, and noninteractive behavior.
- Platforms and distribution: Windows 10/11, Windows PowerShell 5.1 and PowerShell 7.x, .NET 10 `net10.0-windows`, GitHub Releases, PS2EXE script artifact, WPF desktop artifact, CLI artifact, and draft winget/Scoop/Chocolatey manifests blocked by identity/signing.
- Key integrations and data flows: SpotX `run.ps1`, Spicetify CLI/Marketplace/themes, `%APPDATA%` and `%LOCALAPPDATA%` config/log/cache/crash paths, scheduled tasks, release checksums/SBOM/signing metadata, and schemas under `schemas/`.

## Competitive Landscape

- SpotX: Active Windows PowerShell patcher with fast Spotify-version response and a direct flag surface. LibreSpot should keep mirroring compatible flags and patch-state diagnostics, but continue to add hash verification, UI safeguards, and recovery flows around the raw script.
- Spicetify CLI and Marketplace: Large theme/extension ecosystem with backup/apply/restore conventions. LibreSpot should keep direct-install catalog flows and hide CLI/config/directory assumptions behind a facade before Spicetify v3 changes module loading.
- BlockTheSpot, xManager, and ReVanced: Show the enforcement risk of DLL/mobile/patched-binary modification. LibreSpot should avoid mobile modding, patched-binary redistribution, and premium-unlock positioning.
- Spotube, ncspot, Psst, and librespot-org/librespot: Useful alternative-client references, but they are separate playback clients with different account/support risks, not drop-in replacements for local Windows Spotify patch orchestration.
- Ninite Pro, PDQ Deploy, Microsoft Intune, MSI, and Chocolatey: Establish admin expectations for silent operation, return-code mapping, detection scripts, stderr discipline, uninstall behavior, and package validation. This reinforces the fleet CLI contract.
- WinGet, Scoop, Velopack, and SignPath: Require stable package identity, signed or checksum-verifiable artifacts, manifest generation from release metadata, and one update owner per artifact. Draft package files should remain guarded until identity/signing are resolved.
- Windhawk and VS Code profiles: Good analogues for transparent customization and local named profile management. Adopt preview-before-apply, local-first profiles, and rollback semantics before hosted sharing.

## Security, Privacy, and Reliability

- Verified: `Build-Scripts.ps1 -Validate` reports all 74 generated shared PowerShell functions in sync; this reduces the preexisting monolith/backend drift risk.
- Verified: runtime dependencies have no updates and no vulnerable packages from `dotnet list src\LibreSpot.Desktop\LibreSpot.Desktop.csproj package --outdated --include-transitive` and `--vulnerable --include-transitive`.
- Verified: test dependencies have no vulnerable packages, but `Microsoft.NET.Test.Sdk` is behind at 18.6.0 vs 18.7.0, with related test-platform transitives also behind. The existing roadmap item remains valid.
- Verified: CLI runtime and schema contracts diverge. `src/LibreSpot.Cli/Program.cs` emits dry-run NDJSON with `eventName`, `timestampUtc`, and `payload`, while `schemas/ndjson-log-format.json` requires `eventId`, `timestamp`, `level`, `component`, and `message`. The schema advertises `version --json`, `repair`, `watcher install/remove`, and `export-support`; the CLI currently implements text `--version`, status, detect, validate, dry-run install/reapply/uninstall, and plan.
- Verified: release-trust docs remain stale and already have a roadmap item. `.github/workflows` is absent, but `SECURITY.md`, `SIGNPATH.md`, `README.md`, and `schemas/release-artifact-contract.json` still describe workflow/provenance/attestation behavior that does not match local-only release policy.
- Verified: draft package manifests exist under `packaging/` with `PLACEHOLDER_SHA256`, version `3.7.2`, `LibreSpot.exe`, and `-Easy` silent switches. Until a real silent CLI/install contract is wired, tests should prevent accidental publication or README promotion.
- Verified: Windows support language is too broad for package publication. README says Windows 10/11 and winget uses `MinimumOSVersion: 10.0.17763.0`, while Windows 10 Home/Pro support ended on October 14, 2025. LibreSpot can still run on LTSC/ESU/current patched hosts, but package metadata and support docs need a precise policy.
- Recovery and rollback needs: keep dry-run, operation-token/run-receipt, profile-import preview, support bundles, and rollback wording tied to captured state. Do not imply destructive cleanup is reversible unless the operation token records prior state.

## Architecture Assessment

- `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs` still owns many UI domains; the existing view-model split remains the right prerequisite for more UI, localization, testing, and Stryker work.
- Spicetify integration is still direct CLI/config/directory manipulation in `Invoke-SpicetifyCli`, `Sync-SpicetifyListSetting`, install modules, and backend mirrors; the existing Spicetify v3 abstraction item is high leverage.
- `src/LibreSpot.Cli/Program.cs` needs contract tests against `schemas/fleet-cli-contract.json`, `schemas/fleet-exit-codes.json`, `schemas/diagnostic-event-ids.json`, and `schemas/ndjson-log-format.json` before mutating fleet verbs ship.
- `schemas/distribution-matrix.json`, `schemas/release-artifact-contract.json`, and `packaging/*` should be validated together so package manifests cannot drift from artifact names, hashes, package roles, blocked state, or OS support policy.
- WPF has high-contrast resources, WPF-UI controls, UIA names, and smoke tests, but source/XAML checks cannot fully exercise rendered theme switching, focus, prompts, and overlays. The existing FlaUI roadmap item remains appropriate.
- Documentation gaps: release verification, signing status, Scorecard/provenance claims, draft package status, CLI command examples, and Windows support policy need a single local-release source of truth.

## Rejected Ideas

- Mobile/Android mod support: xManager/ReVanced-style flows conflict with LibreSpot's Windows architecture and current Spotify enforcement risk.
- Premium-unlock features: Spotify policy and DMCA history make this a legal/trust risk; LibreSpot should stay focused on ad-blocking/theming/recovery orchestration.
- Alternative-client installer: Spotube, ncspot, Psst, and librespot-org/librespot are separate clients with separate support models, not a LibreSpot workflow.
- Hosted preset marketplace now: local profiles, schema validation, inert URI/file previews, and rollback semantics must land first.
- Reintroduce GitHub Actions for builds/releases: current repo policy is local builds; fix docs/schemas/local checks instead of reviving workflow claims.
- Automatic telemetry: conflicts with local-only support and privacy posture; local support bundles and explicit logs are sufficient.
- MSIX-first distribution: elevated Spotify modification, scheduled tasks, and classic desktop paths make portable/package-manager/Velopack channels more realistic.
- More design-only schemas: existing schemas need runtime enforcement before adding more contracts.
- Publishing current package manifests as-is: placeholders, blocked identity/signing, broad Windows support language, and unproven silent switches make them draft-only.

## Sources

### Project and Upstream
- https://github.com/SysAdminDoc/LibreSpot
- https://github.com/SpotX-Official/SpotX
- https://github.com/SpotX-Official/SpotX/issues/849
- https://github.com/spicetify/cli
- https://github.com/spicetify/cli/releases/tag/v2.43.2
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/cli/issues/3837
- https://github.com/spicetify/spicetify-themes

### Competitors and Analogues
- https://github.com/mrpond/BlockTheSpot
- https://github.com/Team-xManager/xManager
- https://github.com/ReVanced/revanced-manager
- https://github.com/KRTirtho/spotube
- https://github.com/hrkfdn/ncspot
- https://github.com/librespot-org/librespot
- https://github.com/ramensoftware/windhawk
- https://ninite.com/help/features/silent.html
- https://www.pdq.com/pdq-deploy/

### Packaging, Platform, and Dependencies
- https://learn.microsoft.com/en-us/intune/app-management/deployment/add-win32
- https://learn.microsoft.com/en-us/windows/win32/msi/error-codes
- https://learn.microsoft.com/en-us/windows/package-manager/package/manifest
- https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests
- https://docs.chocolatey.org/en-us/create/functions/install-chocolateypackage/
- https://docs.velopack.io/packaging/overview
- https://www.nuget.org/packages/Microsoft.NET.Test.Sdk
- https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro
- https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview

### Security and Policy
- https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security
- https://github.com/github/dmca/blob/master/2025/08/2025-08-14-spotify.md
- https://www.spotify.com/legal/end-user-agreement/
- https://www.spotify.com/legal/user-guidelines/

## Open Questions

- Package identity/signing remains operator-gated: final package IDs, publisher string, SignPath enrollment, and signed artifact names still block public package-channel work.
- Windows 10 policy needs a product decision: whether unsupported Home/Pro hosts are blocked, warned, or documented as best-effort when not on LTSC/ESU.
