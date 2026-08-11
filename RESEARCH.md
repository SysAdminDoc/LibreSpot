# Research — LibreSpot
Date: 2026-08-11 — replaces all prior research.

## Executive Summary

LibreSpot is a Windows 10/11, MIT-licensed orchestrator that installs and maintains SpotX plus Spicetify, with unusually strong local safety controls: pinned and SHA256-verified assets, optional upstream provenance checks, dry-run/undo boundaries, backups, redacted support bundles, operation correlation, localized WPF diagnostics, and a fleet CLI. The current strongest shape is a tested Core/PowerShell/WPF/CLI boundary with Marketplace file recovery, catalog freshness enforcement, multi-user isolation coverage, a fixture-backed upstream compatibility contract, and an unsigned-by-design release truth model. The highest-value remaining work is blocked at external seams: package identity/signing policy, live Spotify/Marketplace validation, the released Spicetify v3 refusal contract, and human-reviewed translations.

Priority opportunities:

1. **P1 — Establish supported Marketplace browser-state recovery.** LibreSpot now archives validated configuration and CustomApps files, but embedded Spotify storage remains explicitly non-portable until a live rig identifies a safe, stable format (README.md, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`, Roadmap_Blocked.md).
2. **P1 — Resolve package identity, channel ownership, and signing policy before public manifests.** Existing Velopack, winget, Scoop, and Chocolatey work is operator-gated by identity, trust, signing, and update-state decisions (Roadmap_Blocked.md, schemas/distribution-matrix.json).
3. **P2 — Target the released Spicetify v3 refusal signature.** The pinned v2.44.0 release still caps Windows/Microsoft Store Spotify at 1.2.93; runtime refusal detection must wait for a released machine-readable contract (schemas/compatibility-baseline.json, Roadmap_Blocked.md).
4. **P2 — Build a Spotify Connect regression harness.** The repository has no Connect client/device fixture, so discovery and transfer recovery require a disposable live account/device plus a deterministic mock contract (Roadmap_Blocked.md).
5. **P3 — Add German and French locales after linguistic review.** The localization gate can validate structure and protected tokens, but the requested no-English-carry-over/no-truncation acceptance needs a native-language reviewer (Roadmap_Blocked.md, `tools/Sync-Localization.ps1`).

Delivered against the earlier research: test compilation and the Windows CI gate, unsigned release-contract truth, fixture-backed compatibility validation, catalog freshness enforcement, multi-user isolation coverage, and behavior-preserving extraction of the three actual WPF workspace tabs. Confidence: Verified for repository findings and current release contracts; Needs live validation for Marketplace browser storage, Spotify Connect, and future Spicetify v3 behavior.

## Product Map

### Core workflows

- Recommended setup detects Spotify and foreign patch state, installs pinned SpotX and Spicetify assets, optionally installs Marketplace/themes/extensions, applies changes, and verifies post-launch health (README.md, src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1).
- Custom Install exposes SpotX flags/custom patches, theme schemes, Marketplace, community assets, profiles, and preview/plan behavior through WPF (src/LibreSpot.Desktop/MainWindow.xaml, src/LibreSpot.Desktop/ViewModels/MainViewModel.cs).
- Maintenance detects drift, re-applies after Spotify updates, repairs Marketplace, restores vanilla state, clears verified cache, exports support evidence, and performs narrowly allow-listed undo (README.md, src/LibreSpot.Core/EnvironmentSnapshotService.cs, src/LibreSpot.Core/OperationJournalUndoService.cs).
- Fleet CLI supports read-only JSON, answer-file install/reapply, NDJSON operations, dry-run, repair, uninstall, watcher control, and support export (src/LibreSpot.Cli/Program.cs, schemas/fleet-cli-contract.json).

### Personas

- A Windows Spotify user who wants a reversible, guided setup.
- A power user who wants custom patches, themes, extensions, profiles, and Marketplace access.
- An endpoint/fleet operator who needs deterministic exit codes, JSON/NDJSON, no interactive elevation, and support evidence.
- A maintainer who must track upstream Spotify/SpotX/Spicetify drift without shipping unverified third-party code.

### Platforms and distribution

- Supported product scope is Windows 10/11 with Windows PowerShell 5.1+ or PowerShell 7.6 LTS; the WPF shell and CLI target net10.0-windows and self-contained Windows builds (README.md, src/LibreSpot.Core/LibreSpot.Core.csproj, src/LibreSpot.Desktop/LibreSpot.Desktop.csproj, src/LibreSpot.Cli/LibreSpot.Cli.csproj).
- Stable distribution is the PowerShell script/executable; the WPF/CLI line is preview. Winget, Scoop, Chocolatey, and Velopack are draft or roadmap work (README.md, schemas/distribution-matrix.json, ROADMAP.md).
- The WPF manifest is asInvoker; legacy PowerShell/PS2EXE paths have separate elevation requirements (src/LibreSpot.Desktop/app.manifest, schemas/elevation-boundary.json).

### Integrations and data flows

- GitHub-hosted SpotX, Spicetify CLI, Marketplace, and theme archives flow through pinned URLs/commits, SHA256 checks, cache/quarantine, safe extraction, and optional gh attestation verification (src/powershell/shared, src/LibreSpot.Core/AppCatalog.cs, schemas/community-assets.json).
- Local state flows through per-user configuration, Spicetify config-xpui.ini/CustomApps, LibreSpot profiles, operation journals, rotating logs, watcher state, backups, and redacted support bundles (schemas/data-inventory.json, src/LibreSpot.Core/SupportBundleService.cs).
- Shell integration is per-user (librespot://, .librespot, jump lists, tray); LibreSpot does not upload telemetry. Local EventPipe/ETW is opt-in and local (README.md, src/LibreSpot.Desktop/Services/ShellIntegrationService.cs).

## Competitive Landscape

- **SpotX** — Strong Windows-only functional coverage, install/uninstall behavior, update-blocking options, and a broad operator-oriented flag surface. LibreSpot should keep its verified, transactional wrapper and expose clearly bounded options; avoid SpotX’s direct curl|iex/mirror trust model and dependence on mutable upstream script behavior. Source: [SpotX](https://github.com/SpotX-Official/SpotX).
- **Spicetify CLI and Marketplace** — Strong customization ecosystem, documented backup apply recovery, themes/extensions/custom apps, and in-app discovery. LibreSpot should make compatibility and state recovery first-class around those layers; avoid treating unstable Spotify internal APIs or browser localStorage as durable backup without an explicit export contract. Sources: [Spicetify CLI](https://github.com/spicetify/cli), [Spicetify Marketplace](https://github.com/spicetify/marketplace), [Marketplace wiki](https://github.com/spicetify/marketplace/wiki).
- **BlockTheSpot** — Demonstrates demand for a narrow ad-blocking path and explicit restore instructions. LibreSpot should continue detecting foreign DLL/config state before mutation; avoid archived, process-injected binaries and the associated false-positive/trust burden. Source: [BlockTheSpot](https://github.com/mrpond/BlockTheSpot).
- **SpotX-Bash** — Shows that rollback, uninstall, custom paths, and multi-platform operator flows are valuable. LibreSpot should borrow the rollback clarity for Windows recovery; macOS/Linux expansion is a scope misfit for the current Windows-specific architecture. Source: [SpotX-Bash](https://github.com/SpotX-Official/SpotX-Bash).
- **Spotify Premium and AdGuard for Windows** — Set commercial expectations for ad-free/offline playback and system-wide filtering, and both document update/reinstall or removal recovery. LibreSpot should prioritize truthful recovery and explain its narrower local patch scope; it should not become a subscription/account service or global network filter. Sources: [Spotify Premium](https://www.spotify.com/us/premium/?mobile=true), [Spotify troubleshooting](https://support.spotify.com/us/article/spotify-not-playing/), [AdGuard for Windows](https://adguard.com/kb/adguard-for-windows/).
- **Microsoft PowerToys** — Demonstrates modular Windows utilities, broad distribution, and discoverable extensions. LibreSpot should keep the Core/host boundary and deterministic CLI modular; avoid adding telemetry or a broad utility suite to a Spotify-specific tool. Source: [PowerToys](https://github.com/microsoft/PowerToys).
- **CSSLoader-Desktop** — Demonstrates theme profiles, load order, updates, and shareable theme bundles. LibreSpot should extend its existing .librespot profile model only where it preserves reproducibility and local ownership; avoid a second general-purpose theme runtime. Sources: [CSSLoader-Desktop](https://github.com/DeckThemes/CSSLoader-Desktop), [CSSLoader profiles](https://docs.deckthemes.com/CSSLoader/Profiles/).
- **BetterDiscord** — Its publishing rules make source availability, opt-in network behavior, cleanup on disable, and prohibition of remote libraries explicit. LibreSpot should apply the same governance to curated Spicetify assets; avoid an unreviewed remote plugin marketplace. Sources: [plugin guidelines](https://docs.betterdiscord.app/plugins/publishing/guidelines), [plugin distribution](https://docs.betterdiscord.app/themes/publishing/distribution).

## Security, Privacy, and Reliability

- **Resolved P0 boundary defect (verified 2026-08-11):** the desktop test project now references `src/LibreSpot.Core/LibreSpot.Core.csproj`; the non-WPF suite passes 886/886, and `Build-Scripts.ps1 -Lint`/`-Validate` run in the Windows quality gate.
- **Resolved trust inconsistency (verified 2026-08-11):** release-artifact, distribution-matrix, and Scorecard metadata now describe unsigned-by-design verification through SHA256 checksums, the release manifest, and the SBOM; the release truth validator and CI gate protect the contract.
- **Upstream compatibility is a reliability boundary:** Spicetify documents that Spotify updates can require backup apply, update, or restore backup apply; Marketplace issue #1201 and community reports describe extensions/themes disappearing after restart or Spotify updates. LibreSpot’s own README acknowledges the narrower loss boundary: CustomApps and config are backed up, Marketplace browser storage is not (README.md, src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1, [Spicetify getting started](https://spicetify.app/docs/getting-started), [Marketplace issue #1201](https://github.com/spicetify/marketplace/issues/1201)).
- **Existing guardrails worth preserving:** SHA256 pinning, safe archive limits, cache quarantine, private-network guards, no credentials/telemetry, redacted support bundles, path-free receipts, asInvoker WPF execution, exact-state PATH undo, and explicit destructive confirmations (schemas/data-inventory.json, schemas/elevation-boundary.json, src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1).
- **PowerShell 5.1 risk is documented, not solved by hashing:** SECURITY.md correctly treats CVE-2025-54100 as a parse-time command-injection concern and requires patch-level preflight; PS7 and compiled distribution remain separate mitigations. Do not weaken that preflight or imply SHA256 removes the risk. Source: [NVD CVE-2025-54100](https://nvd.nist.gov/vuln/detail/CVE-2025-54100).
- **Recovery model:** keep the current stock-restore, retained backup, operation-journal, dry-run, and post-launch-exit warning paths. Add Marketplace browser-state export/recovery only after a live Spotify rig establishes a supported location and format; otherwise present an explicit pre-mutation export/warning rather than silently claiming full rollback. Confidence: Needs live validation.

## Architecture Assessment

- **Boundary repair:** resolved by the direct Core test reference and 886-test non-WPF verification; keep the project-reference contract covered as Core evolves (tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj, src/LibreSpot.Core/LibreSpot.Core.csproj, src/LibreSpot.Desktop/LibreSpot.Desktop.csproj, src/LibreSpot.Cli/LibreSpot.Cli.csproj).
- **Shell decomposition:** the behavior-preserving RD-36 extraction moved the three actual workspace tabs into `src/LibreSpot.Desktop/Views/*.xaml` and retained UIA/focus/localization contracts. `MainWindow.xaml` remains a large composition root and `MainViewModel.cs` remains a future refactor seam.
- **PowerShell composition remains a maintenance seam:** canonical shared functions are manually mirrored into generated GUI/backend/host lanes, with Build-Scripts.ps1 -Validate checking byte-level parity. Keep the generated-host contract and add CI coverage rather than introducing another runtime abstraction during feature work (Build-Scripts.ps1, schemas/parity-manifest.json, tests/LibreSpot.Desktop.Tests/PowerShellCompositionTests.cs).
- **Testing and CI:** broad unit, contract, property, PowerShell, and WPF/UIA test files now have a Windows workflow for lint/validation, Pester, build, and non-WPF .NET tests. Keep WPF smoke runs headless/non-activating or isolated from operator displays, and treat the 886-test non-WPF suite as the required baseline.
- **Category coverage:** security/privacy and observability are strong; accessibility/high contrast/UIA and five locales are implemented, with de/fr blocked on linguistic review; testing/CI and release documentation truth are gated; the curated plugin/community-asset ecosystem is governed by manifests and review rules; offline verified-cache/degraded-mode behavior, profiles/future-version rejection, migration detection, package planning, and multi-user isolation are covered. Marketplace browser storage, live Connect behavior, the released Spicetify v3 contract, and public package identity remain blocked. Mobile and macOS/Linux are consciously excluded because the product and Core APIs are Windows-specific (README.md, schemas/data-inventory.json, schemas/librespot-profile.schema.json, Roadmap_Blocked.md).
- **Dependency posture:** current core UI/runtime packages are pinned and audited by Directory.Build.props; WPF-UI 4.3.0, QRCoder 1.8.0, xUnit 3.2.2, Microsoft.NET.Test.Sdk 18.8.1, and coverlet 10.0.1 are current or intentionally pinned at the researched snapshot. FsCheck.Xunit.v3 is 3.3.3 while NuGet lists 3.3.4; upgrade only after the test graph is repaired and the property suite is green. Sources: [WPF-UI](https://www.nuget.org/packages/wpf-ui/), [FsCheck.Xunit.v3](https://www.nuget.org/packages/FsCheck.Xunit.v3), [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk), [coverlet.collector](https://www.nuget.org/packages/coverlet.collector/).
- **Distribution/upgrade strategy:** package-channel work is blocked in Roadmap_Blocked.md; signing is an operator decision and the active product policy is unsigned-by-design. Any future updater must retain hash/provenance verification, rollback, downgrade resistance, and explicit channel identity; desktop updater research found these are recurring vulnerability classes ([UpdSight](https://www.usenix.org/conference/usenixsecurity26/presentation/wan), [GitHub artifact attestations](https://docs.github.com/en/actions/concepts/security/artifact-attestations), [Velopack](https://docs.velopack.io/)).

## Rejected Ideas

- **Make Authenticode/SignPath mandatory now:** conflicts with the active unsigned-by-design policy and is an operator decision already isolated in Roadmap_Blocked.md; reconcile stale schemas without reopening the decision. Sources: SECURITY.md, SIGNPATH.md, schemas/release-artifact-contract.json.
- **Expand to macOS, Linux, Android, or alternative Spotify clients:** adjacent projects prove demand, but LibreSpot’s Windows PowerShell/WPF/registry/elevation design and stated scope do not transfer without a separate product. Sources: [SpotX-Bash](https://github.com/SpotX-Official/SpotX-Bash), [open-source alternatives](https://github.com/takomine/Open-source-alternatives).
- **Add a global hosts/DNS ad blocker:** AdGuard and Spotify-Ads-Skipper show the pattern, but it would broaden permissions and network blast radius beyond LibreSpot’s SpotX/Spicetify integration. Sources: [AdGuard](https://adguard.com/en/adguard-windows/overview.html?source=ag_products_page), [Spotify Ads Skipper](https://github.com/DEV-industry/Spotify-Ads-Skipper).
- **Ship a cloud-synced profile/plugin service or telemetry:** contradicts local-only data handling and adds account, privacy, and supply-chain scope; profile files are intentionally inert and shareable locally. Sources: schemas/data-inventory.json, schemas/librespot-profile.schema.json, [malicious plugin study](https://www.usenix.org/system/files/sec22summer_kasturi.pdf).
- **Create a broad uncurated plugin marketplace:** the project already has a curated, opt-in catalog and review checklist; plugin research recommends source, permission, cleanup, and network controls instead. Sources: schemas/catalog-refresh-checklist.json, schemas/community-assets.json, [BetterDiscord plugin guidelines](https://docs.betterdiscord.app/plugins/publishing/guidelines).
- **Adopt Stryker mutation testing immediately:** Roadmap_Blocked.md records a concrete xUnit v3/Microsoft.Testing.Platform interoperability blocker; re-evaluate after the test toolchain can run the Core target. Source: Roadmap_Blocked.md.
- **Prioritize cosmetic shell polish before reliability:** the palette, high-contrast palette, focus rings, reduced motion, localization gate, and activity/recovery surfaces are already substantial (src/LibreSpot.Desktop/Themes, tests/LibreSpot.Desktop.Tests); remaining work should first resolve the blocked live compatibility and distribution contracts.

## Sources

### Repository and project policy

- https://github.com/SysAdminDoc/LibreSpot
- https://github.com/SysAdminDoc/LibreSpot/blob/main/README.md
- https://github.com/SysAdminDoc/LibreSpot/blob/main/SECURITY.md
- https://github.com/SysAdminDoc/LibreSpot/blob/main/CONTRIBUTING.md
- https://github.com/SysAdminDoc/LibreSpot/blob/main/schemas/release-artifact-contract.json
- https://github.com/SysAdminDoc/LibreSpot/blob/main/schemas/distribution-matrix.json
- https://github.com/SysAdminDoc/LibreSpot/blob/main/schemas/catalog-refresh-checklist.json
- https://github.com/SysAdminDoc/LibreSpot/blob/main/schemas/data-inventory.json

### Direct OSS projects and upstream documentation

- https://github.com/SpotX-Official/SpotX
- https://github.com/mrpond/BlockTheSpot
- https://github.com/SpotX-Official/SpotX-Bash
- https://github.com/spicetify/cli
- https://github.com/spicetify/marketplace
- https://github.com/spicetify/marketplace/issues
- https://github.com/spicetify/marketplace/wiki
- https://spicetify.app/docs/getting-started
- https://spicetify.app/docs/faq
- https://spicetify.app/docs/customization/themes
- https://github.com/spicetify/cli/issues/3871
- https://github.com/spicetify/cli/issues/3874
- https://github.com/spicetify/cli/releases

### Commercial and adjacent products

- https://www.spotify.com/us/premium/?mobile=true
- https://support.spotify.com/us/article/spotify-not-playing/
- https://adguard.com/kb/adguard-for-windows/
- https://github.com/microsoft/PowerToys
- https://github.com/DeckThemes/CSSLoader-Desktop
- https://docs.deckthemes.com/CSSLoader/Profiles/
- https://docs.betterdiscord.app/plugins/publishing/guidelines
- https://docs.betterdiscord.app/themes/publishing/distribution

### Community and discovery signal

- https://github.com/thechampagne/awesome-windows
- https://github.com/takomine/Open-source-alternatives
- https://news.ycombinator.com/item?id=39775011
- https://stackoverflow.com/questions/76954356/i-had-previously-installed-spicetify-but-now-while-reinstalling-it-im-having-t
- https://www.reddit.com/r/spicetify/comments/1t2wgjf/spicetify_resetting_not_just_on_spotify_update/
- https://www.reddit.com/r/spicetify/comments/1vjr1of/a_few_non_obvious_points_about_keeping_spicetify/

### Standards, platform, and distribution

- https://docs.github.com/en/actions/concepts/security/artifact-attestations
- https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations
- https://learn.microsoft.com/en-us/windows/package-manager/package/manifest
- https://learn.microsoft.com/en-us/windows/package-manager/package/repository
- https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview
- https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview
- https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing
- https://dotnet.microsoft.com/en-us/platform/support/policy
- https://docs.velopack.io/
- https://developer.spotify.com/documentation/web-api/concepts/spotify-connect

### Security and engineering research

- https://nvd.nist.gov/vuln/detail/CVE-2025-54100
- https://www.usenix.org/conference/usenixsecurity26/presentation/wan
- https://www.usenix.org/system/files/sec22summer_kasturi.pdf
- https://arxiv.org/abs/2104.06020
- https://arxiv.org/abs/2203.15592

### Dependency release pages

- https://github.com/CommunityToolkit/dotnet/releases
- https://www.nuget.org/packages/wpf-ui/
- https://www.nuget.org/packages/QRCoder/
- https://www.nuget.org/packages/FsCheck.Xunit.v3
- https://www.nuget.org/packages/Microsoft.NET.Test.Sdk
- https://www.nuget.org/packages/coverlet.collector/
- https://www.nuget.org/packages/avalonedit
- https://www.nuget.org/packages/serilog.sinks.file/
- https://github.com/velopack/velopack/releases
- https://github.com/spicetify/cli/pull/3357

## Open Questions

- **Spicetify v3:** What released version, exit code, and machine-readable refusal message should the compatibility gate target? Roadmap_Blocked.md correctly defers this until the upstream contract is released.
- **Marketplace browser state:** Which embedded-Spotify storage locations can be exported/imported safely and consistently across the supported Spotify distributions? A live patchable Windows rig is required before implementing recovery.
- **CI UI lane:** Should the future Windows CI run WPF/UIA smoke tests, or only non-activating/headless contract tests? This affects runner isolation and must be decided before making UI smoke a required check.
