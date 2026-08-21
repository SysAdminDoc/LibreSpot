# Research — LibreSpot

Date: 2026-08-21 — replaces all prior research.

## Executive Summary

LibreSpot is a Windows-only MIT orchestrator for pinned SpotX plus Spicetify. The public GitHub "Latest" release is still the v3.7.2 script; the development line on HEAD is **v4.0.0-preview.28** (unpublished). The last immutable GitHub prerelease is **v4.0.0-preview.27** (seven attested assets, published 2026-08-21). Preview.28 restores Jump List labels to Home/Settings, search watermarks, disabled-control contrast tokens, AtomicFile persistence, LibreSpotPaths, a System32 PowerShell host probe, DWM palette chrome, a WPF-UI template leak gate, and pinned CycloneDX 6.2.0 local SBOM generation. Do not advance the SpotX or Spicetify pins. The tracker is empty (0 open issues, discussions #20/#21), which is no data, not a clean bill of health.

The 2026-08-20 claim that "what is left is not repo debt" is false. Remaining ROADMAP after the 2026-08-21 evening audit: RD-81 gh-pages catalog truth, RD-86 Spotify version parsers, RD-90 runtime high-contrast effect rebind, RD-91/92/93 copy, RD-95 profile viewport, RD-97 QA capture flake, RD-98 unaudited PS GUI / undo executor / Marketplace archives / AvalonEdit / Crowdin, RD-105 CardListBoxItem disabled opacity, RD-106 snapshot probe drain. Upstream still caps the product: Spicetify stable v2.44.0 tops out at Spotify 1.2.93 while Windows ships 1.2.96.518 and SpotX `main` is 1.2.97.

Highest-value direction, in order:

1. RD-81: gate published `gh-pages` catalog.json against the reviewed manifest.
2. RD-86: one Spotify version parser for the five Core sites that disagree on `v` prefixes.
3. RD-90: rebind high-contrast effects and the focus ring when Windows HC toggles at runtime.
4. RD-95 / RD-105: profile list viewport and disabled card opacity.
5. RD-97: bounded retry for a single WPF QA capture timeout, without raising the global wait.
6. Do not advance the SpotX or Spicetify pins. The ceiling is Spicetify's.
7. Leave winget, Scoop, Velopack, ARM64 WPF, DE/FR, SignPath, and "publish the next tag" in `Roadmap_Blocked.md`.
8. Treat ClickFix/Vidar "free Premium" PowerShell pastes as the live impersonation threat.

Confidence: verified for repo state, tags, tracker, Retry button, Jump List, and upstream tags on 2026-08-21. Needs live validation for Marketplace IndexedDB backup and any SpotX pin advance.

## Product Map

- **Core workflows:** Recommended setup (detect, install pinned SpotX and Spicetify, post-launch health); Custom Install (SpotX flags, themes, Marketplace, community catalog, profiles); Maintenance (drift, reapply, Marketplace file-archive repair, restore, cache, support bundle, PATH-token undo); Fleet CLI (JSON/NDJSON, answer files, dry-run, documented exit codes).
- **Personas:** guided Windows Spotify user; power user who wants patches, themes, and profiles; fleet operator on Intune, PDQ, SCCM, or WinRM; the maintainer tracking upstream drift.
- **Platforms and distribution:** Windows 10/11; PowerShell 5.1 or PowerShell 7.6 LTS; `net10.0-windows`, win-x64 WPF/CLI. GitHub Releases is the only channel. Winget, Scoop, and Chocolatey stay operator-blocked: the name collides with librespot-org/librespot and winget-pkgs still bans `.ps1` installers. README still claims ARM64 hash verification while no csproj carries a `win-arm64` RID (`README.md:341`).
- **Integrations and data:** SHA256-pinned GitHub assets, cache and quarantine, extraction through .NET `ZipArchive` and `Expand-Archive` only. Local-only state is enumerated in `schemas/data-inventory.json`. Marketplace IndexedDB `spicetify-marketplace` / `settings` is detected-not-backed-up. Community catalog publishes from `gh-pages`.

## Competitive Landscape

- **spicetify-easyinstall (ohitstom, 154★, pushed 2026-08-20):** 4.0-beta (2026-07-08) added architecture-filtered Spotify version dropdowns and ARM64 museum endpoints. Learn: hide host versions the current architecture cannot install. Avoid: shipping a years-stale GitHub "latest" as the default target.
- **SpicetifyManager (Israleche, 22★, last push 2026-07-11):** TUI, `-Silent`, Store-to-Desktop swap. Learn: Quick Repair and Full Restore as peer actions. Avoid: an always-latest unpinned CLI.
- **BlockTheSpot-Installer (Nuzair46, 130★, last push 2026-05-03):** recommended-preselected version list. Learn: a dropdown of known-good hosts. Avoid: `chrome_elf.dll` injection, which clashes with SpotX backups. `mrpond/BlockTheSpot` is archived; SpotX is the live Windows patcher.
- **BlockTheSpot-Resilient (thomas-quant, 11★, pushed 2026-08-13):** CI tags artifacts by Spotify version. Learn: name artifacts after the host app version.
- **SpotX-Bash (5,925★, pushed 2026-08-12):** banner prints the latest supported Spotify build. Learn: state supported versus detected before mutating. Avoid: `curl | bash` as the documented default.
- **rxri/spicetify-extensions adblockify (582★):** honest "won't unlock lyrics / downloads / Very High / Jams" matrix. The old `rxri/adblockify` repo 404s; the extension lives here. Learn: publish what a patch does not do. LibreSpot already documents Premium boundaries (`553b740`).
- **ReVanced Manager (v2.6.0, 2026-04-26):** compatibility is a function of the selected patches. Learn: the intersection of SpotX, the Spicetify classmap, and Marketplace is the real ceiling. Avoid: a single "suggested" Spotify version that hides a still-valid 1.2.93.
- **Vencord Installer (620★):** one binary, `checksums.sha256`. Learn: keep Desktop and CLI parity. Avoid: rewriting an old tag's assets.
- **r2modmanPlus (v3.2.18, 2026-06-25):** profile-as-code. LibreSpot already has `.librespot` profiles and `librespot://`.
- **BetterDiscord Installer (1,779★, pushed 2026-08-09):** small install / repair / uninstall wizard, signed. Learn: repair belongs on the front page.
- **Spotube v5.1.2 (2026-06-05), ncspot v1.3.4 (2026-05-22), Psst rolling `2026.08.18`:** alternative clients, not drop-in replacements. Cards stay blocked on legal/support policy. Psst now requires a user Spotify Developer Client ID after the 2026-02-06 platform update.
- **Spotify Premium (US $12.99 Individual, hike announced 2026-01-15, still cited 2026-05-21):** paywalls ads, skips, offline, lossless. LibreSpot must keep saying it does not unlock any of that.
- **EZBlocker:** dormant since 2022; mute-on-ad is a different product. Rejected.

## Reported Issues

Tracker enabled, discussions enabled. **Zero open issues, zero open PRs.**

Closed issues #1–#5 all predate v4 (last close 2026-03-27) and are resolved. Closed PRs are Dependabot residue from before workflows were removed.

Discussions, both opened 2026-07-30, both at 0 comments:

- [#20 Where 4.0 is heading](https://github.com/SysAdminDoc/LibreSpot/discussions/20) — still the right announcement thread, but the body says "LibreSpot is a PowerShell GUI" and "currently at preview.24". Invalid against the preview.27 WPF Home / Maintenance / Settings rail. Updating it is an operator GitHub edit, not a repo patch.
- [#21 Spotify updated and something broke](https://github.com/SysAdminDoc/LibreSpot/discussions/21) — correct intake shape. Needs a pin caveat (1.2.93 / Spicetify 2.44.0) and an IndexedDB note so "Marketplace missing" is not always treated as a Spotify bump.

Public "Latest" (`gh release view` with no tag) is v3.7.2 (2026-04-29): `LibreSpot.ps1` 80 downloads, `LibreSpot.exe` 23, `checksums.txt` 17. Preview.27 exists but is not what `/releases/latest` serves. Workflows: only `pages-build-deployment`.

Read this as *no data*, not *no problems*. Every item below is sourced from code, captures, or upstream — not from users.

## Security, Privacy, and Reliability

- **Shipped in preview.26/.27:** PowerShell 7.6.5 floor (CVE-2026-50523 on 7.6.0–7.6.4), .NET 10.0.11 floor for the 2026-08-11 ten-CVE batch (including WPF RCE CVE-2026-62897 / CVE-2026-70354), SpotX pin-advance policy at `afb4c3fc`, schema-v2 v3 support contract, fail-closed on missing v3 data, immutable-release attestations. No .NET 10.0.12 as of 2026-08-21.
- **HEAD vs published tag:** preview.27 binaries do not include the ten subsequent commits. That is a release-truth gap for the operator, not an implementable ROADMAP row (publication is already blocked).
- **`--accept-eula`:** removed on HEAD. Consent is `eulaAccepted` + `riskAcknowledged` in the answer file. Scripts passing the flag get exit 2 (`CliApplicationTests`).
- **Pins that still hold:** WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, QRCoder 1.8.0, Serilog 4.4.0, Serilog.Sinks.File 7.0.0, AvalonEdit 6.3.1.120, FlaUI.UIA3.Signed 5.0.0, xunit.v3 4.0.0, Microsoft.NET.Test.Sdk 18.9.0, FsCheck.Xunit.v3 **3.4.0 (real; published 2026-08-20 for xunit.v3 4.x)**. The 2026-08-20 vault note that "there is no 3.4.0" is stale. Pester 5.9.1 is the 5.x maintenance line; Gallery latest is 6.1.0 (breaking `Should-*` rewrite) — stay. PSScriptAnalyzer 1.25.0 current. Stryker.NET 4.16.0 current. No GHSA on WPF-UI, QRCoder, AvalonEdit, or Serilog after 2026-08-11.
- **Spicetify v3 coexistence** is the live user-facing hazard. Guard classifies `Apps\xpui.spa.backup`, `modules`, `hooks`, and a newer CLI major. v3.0.0-beta.9 (2026-08-19) is still the tip; allowlist 1.2.70–1.2.94; 1.2.96/1.2.97 are not listed. Nine betas in ten days, Rust rewrite, `xpui.spa` → `xpui.spa.backup` bricks v2 installs.
- **SpotX HEAD `f4cf592b` (2026-08-16) is 1.2.97.** Defender exclusions default since `afb4c3fc`; opt-out `-defender_exclusions_off`. LibreSpot pin stays pre-boundary `550bc72c` / Spotify 1.2.93. Advancing SpotX alone does not raise the Spicetify ceiling. Wikipedia Windows client: 1.2.96.518 (2026-08-12).
- **Marketplace v1.0.9** (2026-07-04). Issue #1212 (persist-before-reload) still open, last update 2026-08-12.
- **7-Zip CVE-2026-58052** does not apply; extraction never leaves .NET / `Expand-Archive`.
- **ClickFix / Vidar (June 2026, still current):** social "free Spotify Premium" PowerShell pastes that add Defender exclusions and drop an infostealer. Counter is the checksum-verified Quick Start.
- **Release integrity:** immutable releases; `gh release verify` / `gh release verify-asset`; `checksums.txt` + manifest + SBOM. Preview.27 SBOM is CycloneDX 1.7 from tool 6.2.0, 8 components, hashes and licenses present, **no `signature`**. CISA 2026 minimum elements want a digital signature; that collides with unsigned-by-design and stays on SignPath. HEAD pins CycloneDX 6.2.0 in `.config/dotnet-tools.json` and `Build-Scripts.ps1 -GenerateSbom`.
- **WPF-UI leak (shipped, then fixed):** `App.xaml` merges `<ui:ControlsDictionary />` first. preview.26 collapsed every premium ComboBox because the template-inner `ToggleButton` inherited WPF-UI sizing. Fix is `Style="{x:Null}"` on template-inner `ToggleButton` / `Button` / `RepeatButton` / `TextBox`. `WpfUiTemplateContractTests` fails a bare child.
- **Guardrails to preserve:** SHA256 pins, archive limits, cache quarantine, SSRF / private-network guards, redacted support bundles, asInvoker WPF, `schemas/elevation-boundary.json`, receipt-backed undo. Snapshot probes resolve Windows PowerShell through `PowerShellHostPath` (System32). Persistence goes through `AtomicFile` + `LibreSpotPaths`.

## Architecture Assessment

- **Release truth:** last published tag `v4.0.0-preview.27` = `81c3c8c`. HEAD version strings are `4.0.0-preview.28` across three csproj files, README badge, `schemas/parity-manifest.json`, and CHANGELOG. Untracked `gf.json` / `gf2.json` / `gf3.json` / `gfrules.html` — do not commit.
- **Screenshot gates agree.** `Build-Scripts.ps1` and `ReadmeScreenshotTests` both require 1800×1280, theme `dark`, culture `en`.
- **Jump List taxonomy:** `ShellIntegrationService` localizes `NavHome` / `NavSettings`. `--shell-action=recommended|custom|maintenance` is unchanged.
- **Catalog decision drift (RD-81 remainder):** Bloom checklist vs manifest is aligned (`defer` / lastPush 2025-05-20). The unpublished gh-pages `catalog.json` truth gate is still open.
- **Shell:** Home / Maintenance / Settings rail (`NavHome` / `NavSettings`); language picker restored; `ShellWorkspaceHost` still `Visibility="Collapsed"` (~1,600 lines). Provenance card and global search remain operator-blocked. `ShellDisplayVersion` is bound in the rail.
- **RD-76 is done:** `RetrySystemCheckButton` on Home, and Maintenance now shares the unavailable copy plus Retry. The snapshot-error hero uses `Info24` instead of the missing `Info48` glyph.
- **PowerShell hosts:** `LibreSpot.ps1` ~10,850 lines, backend ~6,577, 121 shared functions, composition + parity gate. Zero TODO/FIXME/HACK/XXX in `src/` and `tests/`.
- **Test stack:** xunit.v3 4.0.0 + MTP v2 via `global.json`; Core 31, Desktop non-WPF 927+, WPF 135, Pester 203. Stryker.NET 4.16.0 MTP pilot 24.32% / 24% break. `coverlet.collector` 10.0.1 remains MTP-incompatible. WPF QA capture flake is RD-97.
- **i18n:** 1,278 keys × five reviewed satellites; DE/FR blocked. Fleet CLI stays English by contract.
- **Observability:** local NDJSON + `LibreSpot-Operations` EventSource. No upload path.
- **Mobile / macOS / Linux:** out of scope. SpotX-Bash covers Unix SpotX.

## Consciously Excluded Categories

- **Multi-user.** Per-user isolation is already tested. No multi-tenant surface.
- **Plugin ecosystem.** Marketplace is the plugin surface; LibreSpot curates, it does not host one.
- **Mobile.** Out of contract.
- **Distribution beyond GitHub / signing / ARM64 WPF / DE+FR locales.** Blocked on operator decisions — not omitted.
- **Publishing preview.28.** Code is on `main`; cutting the GitHub release is the blocked operator pass, not a coding-agent item.
- **Upgrade / migration.** Config/answer-file schema exists; the live gap is Spicetify v2→v3 coexistence, already guarded.

## Rejected Ideas

- **Adopt Spicetify v3 as the pin now.** Nine betas in ten days, allowlist tops out at 1.2.94, coexistence bricks v2. Source: `spicetify/cli` releases + `supported-versions.json` @ `v3-beta`.
- **Advance the SpotX pin on its own.** `-defender_exclusions_off` removes the Defender objection; Spicetify v2.44.0 still caps 1.2.93. Source: SpotX `f4cf592b`, Spicetify v2.44.0 notes.
- **File "Retry copy without a button" as a defect.** Refuted on HEAD: `RetrySystemCheckButton` exists. RD-76 deleted because it landed.
- **Migrate to Pester 6.1.0.** Breaking assertion rewrite; 5.9.1 is the supported 5.x line.
- **Restore GitHub Actions / Dependabot.** Deliberate local-builds-only policy. Attestations already come from immutable releases at publish time.
- **Sign the CycloneDX document to satisfy CISA 2026 without SignPath.** Conflicts with unsigned-by-design.
- **An EZBlocker-style mute engine.** Different mechanism, dormant upstream.
- **Unify the CLI into the WPF executable.** Breaks `schemas/fleet-cli-contract.json`.
- **Mirror or rehost upstream assets.** Conflicts with no-redistribution posture.
- **winget / Scoop / Chocolatey / unsigned Velopack now.** Identity + script ban + Defender-quarantine-on-swap.
- **Name impersonator repositories.** Use the documented ClickFix/Vidar campaign.
- **Localize the legacy `LibreSpot.ps1` GUI.** Duplicating 1,278 keys into a 10k-line script whose public release is still 3.7.2.
- **Global hosts/DNS ad blocker, cloud profile sync, uncurated marketplace, macOS/Linux port.** Unchanged.

## Sources

### Upstream
- https://github.com/spicetify/cli/releases
- https://github.com/spicetify/cli/releases/tag/v2.44.0
- https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.9
- https://raw.githubusercontent.com/spicetify/cli/v3-beta/supported-versions.json
- https://github.com/spicetify/cli/issues/3038
- https://github.com/spicetify/marketplace/releases/tag/v1.0.9
- https://github.com/spicetify/marketplace/issues/1212
- https://github.com/SpotX-Official/SpotX/commits/main
- https://github.com/SpotX-Official/SpotX-Bash
- https://en.wikipedia.org/wiki/Template:Latest_stable_software_release/Spotify
- https://github.com/rxri/spicetify-extensions/tree/main/adblock
- https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security

### Competitors and adjacent
- https://github.com/ohitstom/spicetify-easyinstall
- https://github.com/Israleche/SpicetifyManager
- https://github.com/Nuzair46/BlockTheSpot-Installer
- https://github.com/thomas-quant/BlockTheSpot-Resilient
- https://github.com/mrpond/blockthespot
- https://github.com/ReVanced/revanced-manager/releases/tag/v2.6.0
- https://github.com/Vencord/Installer
- https://github.com/BetterDiscord/Installer
- https://github.com/ebkr/r2modmanPlus/releases/tag/v3.2.18
- https://github.com/KRTirtho/spotube/releases/tag/v5.1.2
- https://github.com/hrkfdn/ncspot/releases/tag/v1.3.4
- https://github.com/jpochyla/psst
- https://github.com/microsoft/winget-pkgs/blob/master/doc/Policies.md
- https://newsroom.spotify.com/2026-01-15/premium-pricing-update/
- https://newsroom.spotify.com/2026-05-21/alex-norstrom-gustav-soderstrom-co-ceos-investor-day-remarks/

### Community and threat
- https://www.helpnetsecurity.com/2026/06/11/vidar-infostealer-tiktok-instagram-reels-malware-campaigns/
- https://rhisac.org/threat-intelligence/current-clickfix-threat-landscape-developments/
- https://www.recordedfuture.com/research/clickfix-campaigns-targeting-windows-and-macos
- https://github.com/SysAdminDoc/LibreSpot/discussions/20
- https://github.com/SysAdminDoc/LibreSpot/discussions/21

### Security and platform
- https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/
- https://github.com/dotnet/wpf/security/advisories/GHSA-gg8c-3338-xw2f
- https://nvd.nist.gov/vuln/detail/CVE-2026-50523
- https://learn.microsoft.com/en-us/powershell/scripting/install/powershell-support-lifecycle
- https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro
- https://github.blog/changelog/2025-10-28-immutable-releases-are-now-generally-available/
- https://cli.github.com/manual/gh_release_verify-asset
- https://www.cisa.gov/resources-tools/resources/2026-minimum-elements-software-bill-of-materials-sbom
- https://signpath.org/
- https://docs.velopack.io/getting-started/csharp

### Dependencies and tooling
- https://www.nuget.org/packages/FsCheck.Xunit.v3
- https://github.com/fscheck/FsCheck/releases/tag/3.4.0
- https://www.nuget.org/packages/wpf-ui/
- https://xunit.net/releases/v3/4.0.0
- https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/
- https://github.com/pester/Pester/releases/tag/5.9.1
- https://github.com/pester/Pester/releases/tag/6.1.0
- https://github.com/PowerShell/PSScriptAnalyzer/releases/tag/1.25.0
- https://github.com/CycloneDX/cyclonedx-dotnet/releases/tag/v6.2.0
- https://github.com/stryker-mutator/stryker-net/releases

### In-repo evidence
- `src/LibreSpot.Desktop/App.xaml:10`
- `src/LibreSpot.Desktop/Themes/Controls.xaml:875` and `:1296`
- `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml:414-426`
- `src/LibreSpot.Desktop/MainWindow.xaml:18`, `:1248-1251`, `:1405-1406`
- `src/LibreSpot.Desktop/Services/ShellIntegrationService.cs:75-97`
- `tests/LibreSpot.Desktop.Tests/WindowsShellIntegrationTests.cs:95-96`
- `tests/LibreSpot.Desktop.Tests/ReadmeScreenshotTests.cs:83-85`
- `Build-Scripts.ps1:908-945`
- `schemas/community-assets.json` Bloom `catalogReview.decision`
- `schemas/catalog-refresh-checklist.json` Bloom `decision`
- `schemas/compatibility-baseline.json`
- `.config/dotnet-tools.json`

## Open Questions

- **When Spicetify v3 stable ships, and whether schema v2 / `spicetify support` exit codes freeze.** Fixtures already negotiate schema version; only upstream can answer.
- **Whether the Chromium LevelDB under the Spotify profile is a faithful Marketplace backup** once #1212 lands. Needs a live patched client.
- **Which lane `/releases/latest` should represent** after v4 stable. Positioning, not code.
- **Whether the pinned tuple should ever split** so SpotX can follow 1.2.97 while Spicetify stays on 1.2.93. That needs a per-component compatibility story the UI does not tell today.
