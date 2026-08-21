# Research: LibreSpot

Date: 2026-08-20. Replaces all prior research.

## Executive Summary

LibreSpot is a Windows-only MIT orchestrator for pinned SpotX plus Spicetify. The stable script lane is v3.7.4 (public "Latest" release is still v3.7.2) and the WPF plus CLI lane is v4.0.0-preview.27. Its strongest shape is the tested Core/PowerShell/WPF/CLI boundary: SHA256-pinned assets, an asInvoker desktop, receipt-backed undo, a fleet CLI, five reviewed locales, and 25 machine-enforced JSON contracts.

The shipping gap that dominated the previous two passes is closed. `v4.0.0-preview.27` is tagged at HEAD, published as an immutable GitHub prerelease, and the line contains the PowerShell 7.6.5 floor, the .NET 10.0.11 runtime floor, the SpotX post-Defender pin policy, the Spicetify v3 coexistence guard, and the schema-v2 `supported-versions` contract. Every repo defect the earlier passes filed against that tag has since landed: parity manifest version, dead `packaging/` and `release.yml` references, CLI help parity, nav taxonomy, QA-matrix coverage, the MainViewModel and workspace-view split, Dependabot branch residue, and the Marketplace IndexedDB recovery boundary. The Home, Maintenance, and Settings rail replaced the older Home/Setup/Unblock labelling in the same release.

What is left is not repo debt. It is upstream timing, and it belongs to the operator rather than the implementer:

1. The pinned tuple caps at Spotify 1.2.93 while Windows ships 1.2.96/1.2.97. Spicetify stable v2.44.0 has not moved since May, and v3 is still in beta.
2. The SpotX pin can now advance past `550bc72c` because `-defender_exclusions_off` exists upstream, but advancing it alone does not raise the Spotify ceiling that Spicetify sets.
3. Marketplace IndexedDB export and import fidelity cannot be proven without a live patched client.
4. DE and FR locales, winget and Scoop identity, Velopack auto-update, and an ARM64 RID all remain operator-blocked in `Roadmap_Blocked.md`.

Confidence: verified for repo state, tags, published release, and local test results on 2026-08-20. Upstream version facts were fetched 2026-08-20. Needs live validation for Marketplace IndexedDB backup and for any SpotX pin advance.

## Product Map

- **Core workflows:** Recommended setup (detect, install pinned SpotX and Spicetify, post-launch health); Custom Install (SpotX flags, themes, Marketplace, community catalog, profiles); Maintenance (drift, reapply, Marketplace file-archive repair, restore, cache, support bundle, PATH-token undo); Fleet CLI (JSON/NDJSON, answer files, dry-run, documented exit codes).
- **Personas:** guided Windows Spotify user; power user who wants patches, themes, and profiles; fleet operator on Intune, PDQ, SCCM, or WinRM; the maintainer tracking upstream drift.
- **Platforms and distribution:** Windows 10/11; PowerShell 5.1 or PowerShell 7.6 LTS; `net10.0-windows`, win-x64 only. GitHub Releases is the only channel. Winget, Scoop, and Chocolatey stay operator-blocked: the name collides with librespot-org/librespot and winget-pkgs rejects scripted installers. README still claims ARM64 while no csproj carries that RID, which is a filed blocked item.
- **Integrations and data:** pinned GitHub assets, SHA256 verification, cache and quarantine, then extraction through .NET `ZipArchive` and `Expand-Archive`. No 7-Zip anywhere. Local-only state is enumerated in `schemas/data-inventory.json` (33 locations, 6 invariants). No telemetry.

## Repository State (verified 2026-08-20)

- HEAD is tagged `v4.0.0-preview.27`, local and origin agree, and the release carries all seven verified assets.
- Version strings match across `Directory.Build.props`, the three csproj files, the README badge, `schemas/parity-manifest.json` (`generatorVersion` 4.0.0-preview.27), and CHANGELOG. `-Validate` reports release truth as script v3.7.4 and preview v4.0.0-preview.27.
- `.github/workflows` is empty by policy. Builds, tests, lint, and release artifacts are produced locally. Origin has exactly two heads, `main` and `gh-pages`, so the Dependabot branch residue is gone.
- Local verification on this machine: PSScriptAnalyzer 1.25.0 clean on both hosts, `-Validate` clean, Core 31 of 31, Desktop non-WPF 920 of 920, WPF 135 of 135, Pester 203 of 203.
- Structure: `MainViewModel.cs` is 2,764 lines with CustomInstall, Maintenance, and Profiles split into partials. Views use PascalCase filenames paired with their codebehind. `LibreSpot.ps1` is 10,864 lines and `LibreSpot.Backend.ps1` is 6,577, kept in sync by composition and the parity gate. Localization is 1,278 keys across five reviewed satellites.
- The reviewed community catalog is published from `gh-pages` at https://sysadmindoc.github.io/LibreSpot/.
- The simplified shell that landed in preview.26 hid three surfaces that had no replacement: the language picker, the pinned-asset provenance card, and global search. preview.27 restored the picker and repaired `ComboBoxStylePremium`, whose template inherited an implicit WPF-UI toggle-button style once the Fluent controls dictionary was merged into `App.xaml`, so every themed dropdown had been rendering as a content-sized pill. The remaining two surfaces are an operator decision in `Roadmap_Blocked.md`.

## Upstream Landscape

- **Spotify for Windows:** stable 1.2.96.518 (2026-08-12), 1.2.97 rolling out. No xpui container-format change observed.
- **SpotX:** rolling `main`; supports 1.2.97. Defender exclusions have been the default since `afb4c3fc` (2026-07-11) with the `-defender_exclusions_off` opt-out. Backup semantics are unchanged: `Apps\xpui.bak` plus durable `Spotify.bak`, `Spotify.dll.bak`, and `chrome_elf.dll.bak`.
- **Spicetify CLI:** stable is still v2.44.0, capping Spotify 1.2.93. The v3 line reached `v3.0.0-beta.9` on 2026-08-19 from the `v3-beta` branch with per-OS zips and `.sha256` sidecars. v3 carries `supported-versions.json` schema v2 (allowlist 1.2.70 to 1.2.94, per-version classmap status), a `spicetify support` command, and `spicetify spotify-updates block|unblock`. Breaking: v3 renames `xpui.spa` to `xpui.spa.backup` in place and does not carry v2 extensions, themes, or custom apps across. Upstream's own `v3notice.go` says installing v3 over a v2-patched client leaves it unbootable.
- **Spicetify Marketplace:** v1.0.9 (2026-07-04) moved persistence from localStorage to IndexedDB. Issue #1201 closed 2026-08-03; PR #1212 is still open on the persist-before-reload race.
- **Legal climate:** no enforcement against SpotX, Spicetify, or BlockTheSpot to date. The 2025 actions targeted downloaders and premium-unlock projects. Client theming and ad-blocking is not the enforced category, so keep distance from premium-unlock adjacency.

## Competitive Landscape

- **spicetify-easyinstall (ohitstom, 154 stars, commit 2026-07-08):** installs Spotify when missing, with an in-app version picker added after a broken "latest" tag. Learn: a constrained version picker. Avoid: shipping a years-stale GitHub "latest".
- **SpicetifyManager (Israleche, 22 stars, created 2026-06-30):** TUI, `-Silent`, Store to Desktop swap, abort-as-admin. Learn: Quick Repair and Full Restore as peer actions. Avoid: an always-latest unpinned CLI.
- **BlockTheSpot-Installer (Nuzair46, 130 stars):** Go GUI with a recommended-preselected version list from `config.ini`. Learn: a dropdown of known-good hosts instead of free-text latest. Avoid: `chrome_elf.dll` injection, which clashes with the SpotX backups.
- **BlockTheSpot-Resilient (thomas-quant, 11 stars):** CI tags artifacts by Spotify version and degrades instead of crashing. Learn: name artifacts after the host app version.
- **SpotX-Bash (5,922 stars, 2026-08-12):** the banner prints the latest supported Spotify build. Learn: state supported versus detected before mutating. Avoid: `curl | bash` as the documented default.
- **rxri/adblockify (590 stars):** an honest capability matrix that refuses to claim lyrics, downloads, or Very High audio. Learn: publish what a patch does not do.
- **ReVanced Manager (29,131 stars, v2.6.0):** compatibility is a function of the selected patches. Learn: compute the intersection of SpotX, the Spicetify classmap, and Marketplace, then block apply unless forced. Avoid: a single suggested Spotify version that hides a still-valid 1.2.93.
- **Vencord Installer (620 stars):** one binary for GUI and CLI, with `checksums.sha256`. Learn: keep Desktop and CLI parity. Avoid: rewriting an old tag's assets, which breaks pin-by-tag.
- **r2modmanPlus (2,184 stars, v3.2.18):** profile-as-code. LibreSpot already has `.librespot` profiles and the `librespot://` protocol, so a paste host should never become the only transport.
- **BetterDiscord Installer (1,779 stars):** a small install, repair, and uninstall wizard, signed. Learn: repair belongs on the front page.
- **EZBlocker (1,848 stars, dormant since 2022):** mute-on-ad. A persona exists that refuses binary patching, and the answer for them is a Spicetify-only profile rather than an audio heuristic.
- **AdGuard and uBlock:** system and web-player ads, a different trust domain from a client patch.
- **TunePat and ViWizard:** DRM rippers. Different legal bucket, and not a project to link alongside.
- **Spotify Premium (US $12.99 Individual after the Feb 2026 hike):** paywalls ads, unlimited skips, offline, and lossless. LibreSpot must keep saying it does not unlock any of that.

## Security, Privacy, and Reliability

- **Shipped in preview.26:** the PowerShell 7 security floor (7.6.0 through 7.6.4 warn with CVE-2026-50523 and name 7.6.5), the 10.0.11 .NET runtime floor covering the 2026-08-11 ten-CVE batch, the SpotX pin-advance policy anchored at `afb4c3fc`, the schema-v2 v3 support contract, and fail-closed behavior when v3 support data is missing or malformed.
- **No newer .NET 10 or WPF CVE after 2026-08-11.** 10.0.12 is not out; the next servicing window is September 2026. Stay on 10.0.11.
- **PowerShell lifecycle:** 7.4 and 7.5 reach end of support on 2026-11-10 and 7.6 LTS runs to 2028-11-14. CVE-2025-54100 remains the standing Windows PowerShell 5.1 item.
- **Spicetify v3 coexistence** is the live user-facing hazard. The guard classifies `Apps\xpui.spa.backup`, `modules`, `hooks`, and a newer CLI major, then stops mutating operations with the `spicetify restore` recovery path while leaving restore, full reset, and uninstall available.
- **7-Zip CVE-2026-58052** (MotW bypass, exploited in the wild) does not apply, because extraction never leaves .NET `ZipArchive` and `Expand-Archive`. Keep it that way.
- **ClickFix and Vidar (June 2026):** social-media "free Spotify Premium" posts that paste PowerShell which adds Defender exclusions and drops an infostealer. That is the real threat to LibreSpot's users, and the checksum-verified Quick Start is the counter. Say it without naming unverified repositories.
- **Release integrity:** immutable releases are enabled, so a published tag and its assets cannot be edited or swapped, only superseded. `gh release verify` and `gh release verify-asset` are the documented verification path alongside `checksums.txt`, the release manifest, and the SBOM. Any future bad-release runbook has to account for supersede-only.
- **Guardrails to preserve:** SHA256 pins, archive limits, cache quarantine, SSRF and private-network guards, redacted support bundles, the asInvoker WPF manifest, receipt-backed undo, and `schemas/elevation-boundary.json`.

## Architecture Assessment

- **Release truth:** clean. HEAD is the tag, the tag is published, and every version string agrees. The remaining truth gap is upstream-facing, not internal: the public "Latest" release is still the v3.7.2 script while the tracked script is v3.7.4, which is an operator decision about which lane represents the project.
- **Refactor:** `MainViewModel.cs` is 2,764 lines with three partials, and the workspace views are PascalCase with the Custom sections extracted. The two PowerShell hosts stay under composition and parity, and a third runtime should not appear.
- **UX and accessibility:** the rail is Home, Maintenance, and Settings, with a Tools surface behind it. Jump List tasks and their descriptions are localized through the same resource keys as the shell. `ShellDisplayVersion` derives from assembly metadata. The QA matrix covers `prompt-destructive`, `activity-running`, and reduced-motion states. Fleet `--help` lists every parsed flag including `--profile`, `--scope`, `--purge`, `--quiet`, and `--correlation-id`. The legacy `LibreSpot.ps1` GUI stays English and keeps its own self-elevation narrative, which is intentional and documented.
- **Test stack:** xunit.v3 4.0.0, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.9.0 in both projects, FsCheck.Xunit.v3 3.4.0 in the desktop project, both on the Microsoft Testing Platform v2 runner through `global.json`. The Stryker.NET 4.16.0 MTP pilot sits at a 24.32% baseline with a 24% break threshold over selected Core files. `coverlet.collector` remains incompatible with MTP, so mutation coverage runs without it. PSScriptAnalyzer is pinned at 1.25.0 with `PSUseConstrainedLanguageMode` deliberately disabled. Pester 5.9.1 is the supported line; 6.x is a breaking major and is not worth taking yet.
- **Third-party packages:** WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, QRCoder 1.8.0, Serilog 4.4.0 with Sinks.File 7.0.0, AvalonEdit 6.3.1.120, and FlaUI.UIA3 5.0.0 are all current stable with no advisories as of 2026-08-20.
- **Internationalization:** 1,278 keys across five reviewed satellites with key parity enforced by `-Validate`. DE and FR remain blocked. The fleet CLI stays English on purpose, because operators depend on stable flag names and help text.
- **Observability:** local NDJSON plus the `LibreSpot-Operations` EventSource, with no upload path. That is the intended ceiling.
- **Catalog:** `schemas/community-assets.json` with a freshness gate, published to Pages. Bloom stays deferred while its upstream is quiet.
- **Mobile, macOS, and Linux:** still out of scope. SpotX-Bash covers Unix SpotX and Spicetify is already cross-platform, so wrapping them would be a different product.

## Rejected Ideas

- **Adopt Spicetify v3 as the pin now.** Nine betas in ten days, a Rust rewrite, an allowlist that tops out at 1.2.94 while Windows ships 1.2.96/1.2.97, and coexistence that bricks v2 installs.
- **Advance the SpotX pin on its own.** The opt-out flag removes the Defender objection, but Spicetify v2.44.0 still caps the client at 1.2.93, so the tuple ceiling does not move.
- **An EZBlocker-style mute engine.** Different mechanism, dormant upstream, and a poor fit for a patch orchestrator.
- **Unify the CLI into the WPF executable.** It would break `schemas/fleet-cli-contract.json` and the exit-code taxonomy.
- **Mirror or rehost upstream assets.** It conflicts with the no-redistribution posture and the 2025 DMCA climate.
- **winget, Scoop, or Chocolatey now.** Operator-blocked on identity, and winget-pkgs bans scripted installers.
- **Unsigned Velopack auto-update.** Defender can quarantine the replacement executable mid-swap, so signing has to come first.
- **Pester 6 migration.** 5.9.1 is still supported and the migration buys nothing today.
- **Name and shame specific impersonator repositories.** Use the documented ClickFix and Vidar campaign instead, which is verifiable.
- **A global hosts or DNS ad blocker, cloud profile sync, an uncurated marketplace, or a macOS and Linux port.** Unchanged from earlier passes.
- **Localize the legacy `LibreSpot.ps1` GUI.** Duplicating 1,278 reviewed keys into a 10,000-line script serves a lane whose public release is still 3.7.2.

## Open Questions

- **Spicetify v3 stable timing and its final contract.** Schema v2 and the `spicetify support` exit semantics can still change. The fixtures already negotiate the schema version, so the answer only arrives upstream.
- **Marketplace IndexedDB file-level backup.** The Chromium LevelDB store under the Spotify profile may be copyable at rest, but PR #1212 is open on the persist-before-reload race. Only a live patched rig can prove export and import fidelity.
- **Which lane is the public "Latest" release.** The v3.7.4 script is tracked but v3.7.2 is what a visitor sees, while the v4 preview line is where development happens. That is a positioning call.
- **Whether the pinned tuple should ever split.** Holding SpotX and Spicetify together keeps the guarantee simple, and splitting them would need a per-component compatibility story the UI does not tell today.

## Sources

### Upstream
- https://github.com/spicetify/cli/releases
- https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.9
- https://github.com/spicetify/cli/releases/tag/v2.44.0
- https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json
- https://github.com/spicetify/cli/blob/v3-beta/docs/supported-versions.md
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/cli/issues/3837
- https://github.com/spicetify/marketplace/releases
- https://github.com/spicetify/marketplace/issues/1212
- https://github.com/spicetify/marketplace/issues/1126
- https://github.com/spicetify/marketplace/pull/1181
- https://github.com/SpotX-Official/SpotX/commits/main
- https://github.com/SpotX-Official/SpotX-Bash
- https://en.wikipedia.org/wiki/Template:Latest_stable_software_release/Spotify

### Competitors and adjacent
- https://github.com/ohitstom/spicetify-easyinstall
- https://github.com/Israleche/SpicetifyManager
- https://github.com/Nuzair46/BlockTheSpot-Installer
- https://github.com/thomas-quant/BlockTheSpot-Resilient
- https://github.com/rxri/spicetify-extensions
- https://github.com/ReVanced/revanced-manager
- https://github.com/Vencord/Installer
- https://github.com/BetterDiscord/Installer
- https://github.com/ebkr/r2modmanPlus
- https://github.com/Xeroday/Spotify-Ad-Blocker
- https://github.com/microsoft/winget-pkgs/blob/master/doc/Policies.md
- https://spicetify.app/docs/getting-started.html

### Community signal
- https://github.com/spicetify/cli/issues/3762
- https://github.com/spicetify/marketplace/issues/1133
- https://www.malwarebytes.com/blog/news/2026/06/free-spotify-premium-hacks-on-social-media-are-spreading-infostealers
- https://www.helpnetsecurity.com/2026/06/11/vidar-infostealer-tiktok-instagram-reels-malware-campaigns/
- https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security

### Security and platform
- https://nvd.nist.gov/vuln/detail/CVE-2026-50523
- https://github.com/advisories/GHSA-2x33-4fc7-ppvw
- https://github.com/dotnet/announcements/issues/436
- https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle
- https://github.blog/changelog/2025-10-28-immutable-releases-are-now-generally-available/
- https://cli.github.com/manual/gh_release_verify-asset
- https://www.cisa.gov/resources-tools/resources/2026-minimum-elements-software-bill-materials-sbom
- https://www.spotify.com/us/premium/

### Dependencies and tooling
- https://xunit.net/releases/v3/4.0.0
- https://www.nuget.org/packages/FsCheck.Xunit.v3
- https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/
- https://github.com/PowerShell/PSScriptAnalyzer/releases/tag/1.25.0
- https://github.com/pester/Pester/releases/tag/5.9.1
- https://stryker-mutator.io/blog/stryker-net-mtp-runner/
- https://github.com/coverlet-coverage/coverlet/releases/tag/v10.0.1
- https://docs.velopack.io/getting-started/csharp
