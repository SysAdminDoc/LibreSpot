# LibreSpot Roadmap

Active roadmap for forward-looking work only. Completed release work lives in
[COMPLETED.md](COMPLETED.md), and research synthesis lives in
[RESEARCH_REPORT.md](RESEARCH_REPORT.md). The full April 2026 research archive is
kept at [docs/archive/research/RESEARCH.md](docs/archive/research/RESEARCH.md).

Last consolidated: 2026-06-01.
Last researched: 2026-06-04, Cycle 13.

## Implementer Instructions (for the build machine)

This roadmap is fed continuously by the research machine. On every pass, the
build machine should:

1. `git pull --rebase` to get the latest researched items before starting.
2. Work the open implementer-actionable items top-down by priority (P0 -> P3).
   Build them properly: multi-file structure, real error handling, no runtime
   auto-install hacks, version strings synced, docs/CHANGELOG updated in the
   same commit.
3. In addition to building items, run a full UX audit each pass. Walk every
   screen, page, dialog, form, table, and empty/loading/error/disabled state
   across light, dark, and high-contrast themes. Check onboarding, navigation
   clarity, spacing, contrast, alignment, clipping, overflow, hierarchy,
   microcopy, destructive-action guards, keyboard and screen-reader
   accessibility, and trust signals. Fix what you find, or file it back as a
   new implementer-actionable roadmap item if it is larger than a pass.
4. Check off each item you complete, leave it in place, commit per logical
   change with a "why" message, and push.
5. Never edit this Implementer Instructions block or the Researcher Queue
   headings. Never force-push.

## Current State

- Stable script release: v3.7.2.
- Native WPF shell line: v4.0.0-preview.6.
- Release pipeline now builds PS2EXE and WPF artifacts with checksums, SBOMs, and
  build provenance attestations.
- Auto-reapply watcher, self-update checks, pre-patched Spotify detection,
  Spotify version selection, and the v3.7 UI refresh have shipped.
- Point-in-time dependency pins from the latest repo docs:
  - SpotX `0abf98a3` for Spotify `1.2.86.502`
  - Spicetify CLI v2.43.2 in code, with some docs still stale at v2.43.1
  - Marketplace v1.0.8
  - Spicetify themes `9af41cf`

## Next Release Queue

| Priority | Track | Work | Exit criteria |
|---|---|---|---|
| P0 | Rebrand decision | Decide whether to keep LibreSpot, rename before package-manager distribution, or keep the repo name but rename the app. | Decision recorded before winget/scoop/choco work begins. |
| P0 | v4.0 stable | Finish WPF shell polish: Wpf.Ui controls, visual QA, completion toasts, undo-selected-actions pane, status dashboard, and repair/diagnostic flow. | Stable WPF build has parity with the script shell and passes release preflight. |
| P0 | Signing | Complete SignPath Foundation enrollment and wire Authenticode signing into tagged releases. | Release assets are signed and verification docs are current. |
| P1 | Distribution | Add winget, Scoop bucket automation, and Velopack auto-update. Consider Chocolatey after signing is complete. | Users can install/update from package-manager channels without manual downloads. |
| P1 | Fleet CLI | Add silent install, JSON presets, detect/status JSON, NDJSON logs, validate, uninstall, dry-run, and deployment docs. | Admins can deploy LibreSpot idempotently through scripts or endpoint tools. |
| P1 | Diagnostics | Add status-at-a-glance and repair flows for Spotify, SpotX, Spicetify, backups, scheduled task state, and last patch time. | Common broken states are detected and expose one-click fixes. |
| P2 | Ecosystem catalog | Reconcile shipped theme/extension catalog against research; add remaining high-value themes/extensions and custom apps. | Catalog data and README agree with the actual installer behavior. |
| P2 | Windows shell integration | Add jump list, taskbar thumbnail buttons, tray minimize, `librespot://` protocol, `.librespot` import association, and actionable persistent toasts. | Shell affordances work for installed and portable scenarios. |
| P2 | Community sharing | Add local preset profiles, shareable URIs, bundled preset gallery, secure import preview, QR cards, changelog viewer, community links, and `COMPARISON.md`. | Users can save, import, share, and compare presets without a hosted service. |
| P3 | Custom patches editor | Add AvalonEdit JSON authoring for SpotX `patches.json`, schema linting, regex safety checks, dry-run matching, and import-from-URL review. | Power users can validate and stage custom patch sets safely. |
| P3 | Localization | Introduce resource-based UI strings, runtime culture switching, CI checks for raw strings, machine-translation prefill, and Crowdin sync. | EN/RU/ZH-Hans/PT-BR/ES can ship without hardcoded UI text. |
| P3 | Alternative clients | Add opt-in cards for Spotube, Psst, and Ncspot with a capability matrix. | Users can choose alternatives while the main patched-Spotify flow remains primary. |

## v4.0 Stable Scope

The v4.0 stable cut should stay focused on the native shell and release trust:

- Adopt Wpf.Ui `TitleBar`, `Snackbar`/`InfoBar`, `NumberBox`, and `SplitButton`
  where they remove custom control code.
- Add completion toasts once COM activation/notification registration is
  reliable for the unpackaged app.
- Add the undo-selected-actions pane after a successful run for reversible
  operations such as update blocking, shortcuts, scheduled tasks, and config
  changes.
- Add a launch dashboard showing Spotify version, Spicetify version, SpotX
  state, last patch timestamp, watcher status, and backup count.
- Add repair/diagnostic buttons with per-issue fixes instead of a single broad
  reset path.
- Keep the release workflow's parse, version-sync, regression-test, checksum,
  SBOM, and attestation gates mandatory.

## Distribution And Trust

Distribution work should happen in this order:

1. Finish the rebrand decision.
2. Complete SignPath enrollment and signing automation.
3. Publish winget manifests for portable assets.
4. Add Velopack packaging for the WPF shell.
5. Create a Scoop bucket with `checkver` and `autoupdate`.
6. Submit Chocolatey only after signing and checksum automation have settled.

Microsoft Store/MSIX remains a poor fit because the app needs to patch files in
the user's Spotify installation and interact with classic desktop locations.

## Fleet Deployment

Fleet work targets sysadmin use cases that competing Spotify customization
tools do not cover:

- `--silent`, `--quiet`, `--no-restart`, `--accept-eula`, and `/S` aliases.
- JSON answer files with schema validation.
- `--detect --json` with explicit compliant/not-installed/drift exit codes.
- NDJSON logs under `%ProgramData%\LibreSpot\logs\` with rotation.
- `uninstall --silent --purge --yes --keep-spotify`.
- `validate` with typo suggestions.
- `--dry-run` and PowerShell `-WhatIf` parity.
- Deployment examples for WinRM, PSRemoting over SSH, PDQ Deploy, Intune Win32
  apps, and SCCM-style return codes.

## Research Backlog

These require fresh research before implementation:

- Whether the April 2026 SpotX and Spicetify pin guidance is still current.
- Spotify Linux/macOS patching and whether it belongs in this repo or a sister
  project.
- Anti-forensics/tamper checks for antivirus-quarantined extension files.
- Spotify Connect regression test harness.
- Spicetify v3 readiness and migration risk.
- DMCA/availability contingency if SpotX distribution changes.

## 🔬 Researcher Queue (Cycle 1 - 2026-06-04)

These items are net-new or sharpened from the June 4 refresh. Ownership tags:
🔬 = researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where credentials or policy decisions block completion.

- [ ] 🔬 🤖 P0 - Refresh the SpotX and
  Spotify compatibility pin set.
  - Why: LibreSpot still pins SpotX commit `0abf98a3` and labels Spotify
    `1.2.86.502` as current, while upstream SpotX main has May 2026 work for
    Spotify `1.2.90`, including version bump and patch-target mismatch guards.
  - Evidence: `LibreSpot.ps1:137`, `LibreSpot.ps1:809`,
    https://github.com/SpotX-Official/SpotX/commit/13ef73f820afad845637bc81a56052ce390f615c,
    https://github.com/SpotX-Official/SpotX/commit/b53956f71d2ee0ce585e475b8a4d6fa8d814b579,
    https://github.com/SpotX-Official/SpotX/commit/95882aa5b308832102ac8a206d300bf6f5436bfb
  - Touches: `LibreSpot.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs`, tests, README, CHANGELOG.
  - Acceptance: the default SpotX pin, SHA256, Spotify version dropdown labels,
    watcher reapply path, and README compatibility table all agree with a
    locally tested upstream SpotX commit and recommended Spotify build.
  - Verify: run `dotnet test tests\LibreSpot.Desktop.Tests\LibreSpot.Desktop.Tests.csproj -c Release --nologo`;
    parse both PowerShell files; run Maintenance > Check pinned versions.

- [ ] 🔬 🤖 P0 - Make the dependency
  state single-sourced across code and docs.
  - Why: source and tests already expect Spicetify CLI `2.43.2`, but
    `CLAUDE.md`, `ROADMAP.md` historical text, and `RESEARCH_REPORT.md` still
    mention `2.43.1`; this creates avoidable handoff drift before the next
    release.
  - Evidence: `LibreSpot.ps1:143`, `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:430`,
    `README.md`, https://github.com/spicetify/cli/releases/tag/v2.43.2
  - Touches: README, CLAUDE notes, ROADMAP current-state text,
    `RESEARCH_REPORT.md`, and any version badges/tables.
  - Acceptance: one dependency table is treated as authoritative and all public
    docs match the code pins: SpotX commit, Spicetify CLI, Marketplace, themes,
    and Spotify build.
  - Verify: `rg -n "2\\.43\\.1|2\\.43\\.2|1\\.2\\.86\\.502|0abf98a3" README.md ROADMAP.md RESEARCH_REPORT.md CLAUDE.md LibreSpot.ps1 src tests`.

- [ ] 🔬 🤖 P1 - Add an automated
  upstream compatibility matrix check.
  - Why: Spicetify v2.43.2 declares Windows compatibility through Spotify
    `1.2.88`, while SpotX and SpotX-Bash have later 1.2.90 activity; users need
    the app to distinguish "SpotX can patch this" from "Spicetify CSS maps are
    known-good" instead of treating updates as a single green/red state.
  - Evidence: https://github.com/spicetify/cli/releases/tag/v2.43.2,
    https://github.com/SpotX-Official/SpotX-Bash/commit/fa8730d16e7acfb70744be677ac9b7aa3e3eaf3c,
    `LibreSpot.ps1:4785`
  - Touches: update-check code, maintenance dashboard, config model,
    `AppCatalog.SpotifyVersionManifest`, README compatibility section.
  - Acceptance: Check Updates reports separate SpotX, Spicetify CLI, Marketplace,
    and themes compatibility statuses, including "newer SpotX available but
    Spicetify max-tested Spotify is older" warning copy.
  - Verify: mock GitHub responses in tests; run Maintenance > Check pinned
    versions with network enabled and disabled.

- [ ] 🔬 🤖 P1 - Add a preflight for
  Marketplace visibility and recovery.
  - Why: current community threads repeatedly report Marketplace missing,
    only showing themes, or needing `spotify:app:marketplace` after Spotify
    updates; LibreSpot installs Marketplace but does not yet expose a targeted
    "open/fix Marketplace" repair path.
  - Evidence: https://www.reddit.com/r/spicetify/comments/1sleiz4/spicetify_marketplace_icon_not_showing_up/,
    https://www.reddit.com/r/spicetify/comments/1th8vhv/marketplace_only_shows_themes_no_extensions/,
    https://www.reddit.com/r/spicetify/comments/1spyvxz/marketplace_fix/,
    `LibreSpot.ps1:5301`
  - Touches: maintenance dashboard diagnostics, Marketplace install/verify step,
    repair flow, docs FAQ.
  - Acceptance: after install/reapply, LibreSpot verifies Marketplace files,
    shows a specific status when the custom app exists but sidebar entry is not
    discoverable, and offers a repair/open action with clear fallback guidance.
  - Verify: simulate missing CustomApps Marketplace files and an installed-but-hidden
    state; run WPF and PowerShell maintenance flows.

- [ ] 🔬 🤖 P1 - Turn distribution work
  into package-specific manifests and dry-run checks.
  - Why: winget, Scoop, and Chocolatey each need different metadata, hash, and
    silent/portable behavior; current roadmap names the channels but does not
    yet spell out package files, validation commands, or update automation.
  - Evidence: https://learn.microsoft.com/en-us/windows/package-manager/package/manifest,
    https://learn.microsoft.com/windows/package-manager/winget/,
    https://github.com/ScoopInstaller/Scoop/wiki/App-Manifests,
    https://github.com/ScoopInstaller/Scoop/wiki/App-Manifest-Autoupdate,
    https://docs.chocolatey.org/en-us/create/create-packages/
  - Touches: `publish/`, release workflow, package manifest templates, docs.
  - Acceptance: repo has draft winget YAML, Scoop JSON with `checkver` and
    `autoupdate`, and Chocolatey nuspec/tools scripts gated behind signing
    readiness; each has a documented local validation command.
  - Verify: `winget validate`, `scoop install .\bucket\librespot.json`,
    `checkver.ps1 librespot -u`, and `choco pack` in a clean temp folder.

- [ ] 🔬 🤖 P1 - Add a trust and legal
  disclosure page before broader distribution.
  - Why: Spotify's published user guidelines prohibit modifying or reverse
    engineering the service except where law prevents that restriction, and the
    existing README safety section focuses on hashes/signing rather than terms,
    account-risk boundaries, and what LibreSpot does not redistribute.
  - Evidence: https://www.spotify.com/us/legal/user-guidelines//,
    https://developer.spotify.com/terms/,
    `README.md`
  - Touches: README, `docs/trust-and-risk.md` or equivalent, installer "Is this
    safe?" copy, release notes.
  - Acceptance: users get factual, non-alarming disclosures covering account
    terms risk, no credential collection, direct-from-upstream downloads,
    hash/provenance verification, no bundled Spotify binaries, and how to return
    to stock Spotify.
  - Verify: documentation review only; no code required unless installer text is
    updated in the same pass.

- [ ] 🔬 🤖 P2 - Add NuGet dependency
  refresh automation and policy.
  - Why: `dotnet list package --outdated` shows newer Serilog, Serilog.Sinks.File,
    test SDK, xUnit runner, and coverlet packages while vulnerability checks are
    clean; this is a controlled maintenance opportunity, not an emergency.
  - Evidence: local `dotnet list package --outdated` on 2026-06-04,
    local `dotnet list package --vulnerable --include-transitive` on 2026-06-04,
    https://www.nuget.org/packages/Serilog,
    https://www.nuget.org/packages/Serilog.Sinks.File
  - Touches: `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`,
    `tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj`,
    release workflow.
  - Acceptance: Dependabot or a documented monthly dependency-refresh workflow
    covers NuGet packages, runs tests, and records when an update is held for
    behavior risk.
  - Verify: `dotnet list package --outdated`; `dotnet test ... -c Release --nologo`;
    release workflow dry-run where practical.

- [ ] 🔬 🤖 P2 - Expand catalog refresh
  into a measured Marketplace inventory process.
  - Why: GitHub topic searches show about 298 `spicetify-extensions`, 101
    `spicetify-themes`, and 11 `spicetify-custom-apps` repositories, while
    LibreSpot curates 15 extensions and 21 themes; future catalog additions need
    popularity, maintenance, license, install-path, and breakage checks.
  - Evidence: https://github.com/topics/spicetify-extensions,
    https://github.com/topics/spicetify-themes,
    https://github.com/spicetify/marketplace,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs:216`
  - Touches: catalog model, README catalog tables, install verifier, tests.
  - Acceptance: catalog-refresh checklist ranks candidate themes/extensions by
    stars, latest push, license, install method, known Spotify-version issues,
    and whether Marketplace already provides a safer path.
  - Verify: run the checklist against Beautiful Lyrics, rxri extensions,
    Catppuccin, Comfy, Bloom, Lucid, Hazy, and at least five rejected candidates.

- [ ] 🔬 🤖 P2 - Add OpenSSF Scorecard
  and supply-chain hygiene reporting.
  - Why: releases already have checksums, SBOM, and GitHub artifact
    attestations, but public trust would improve with a repeatable security
    hygiene signal and remediation tracking before package-manager submission.
  - Evidence: `.github/workflows/release.yml:355`,
    https://github.com/ossf/scorecard-action,
    https://openssf.org/scorecard/,
    https://github.com/actions/attest
  - Touches: `.github/workflows/scorecard.yml`, README badges, security docs.
  - Acceptance: Scorecard runs on schedule and pull request, publishes results
    where supported, and any low scores become roadmap items rather than silent
    warnings.
  - Verify: run workflow manually; confirm no write permissions beyond what the
    action needs.

- [ ] 🔬 🔧 P3 - Decide whether macOS/Linux
  belongs in core, docs-only, or a sibling project.
  - Why: SpotX-Bash is active and supports 1.2.90, while LibreSpot's product
    architecture is Windows PowerShell/WPF with Windows-specific scheduled task,
    registry, and AppData assumptions.
  - Evidence: https://github.com/SpotX-Official/SpotX-Bash,
    https://github.com/SpotX-Official/SpotX-Bash/commit/fa8730d16e7acfb70744be677ac9b7aa3e3eaf3c,
    `LibreSpot.ps1:334`, `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj`
  - Touches: product strategy docs; no feature code until the decision is made.
  - Acceptance: decision record chooses one: Windows-only with links, sibling
    repo, or staged cross-platform CLI; it names unsupported assumptions and
    distribution consequences.
  - Verify: review decision against install flow, watcher, package, and support
    burden.

## 🔬 Researcher Queue (Cycle 2 - 2026-06-04)

These items extend Cycle 1 without replacing it. Ownership tags stay the same:
🔬 = researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where credentials, naming, or release policy decisions block
completion.

- [ ] 🔬 🤖 P0 - Add a runtime and
  build-tool lifecycle gate.
  - Why: the WPF shell targets `net8.0-windows` and the release workflow uses
    `actions/setup-dotnet@v4` with `8.0.x`, while .NET 8 is already in
    maintenance and reaches end of support on 2026-11-10. .NET 10 is the active
    LTS line through 2028-11-14, and PowerShell 7.6 LTS is aligned to .NET 10.
    Release build tools are also behind current upstreams: PS2EXE is pinned to
    1.0.15 while PSGallery reports 1.0.17, and CycloneDX is pinned to 3.0.8
    while upstream `cyclonedx-dotnet` is 6.2.0.
  - Evidence: `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:4`,
    `.github/workflows/release.yml:235`, `.github/workflows/release.yml:259`,
    https://dotnet.microsoft.com/en-us/platform/support/policy,
    https://devblogs.microsoft.com/powershell/announcing-powershell-7-6/,
    https://github.com/MScholtes/PS2EXE,
    https://github.com/CycloneDX/cyclonedx-dotnet/releases/tag/v6.2.0
  - Touches: release workflow, WPF csproj, dependency policy docs, roadmap.
  - Acceptance: CI has a failing gate or release checklist item that names the
    current .NET target support phase, latest SDK patch, PS2EXE version,
    CycloneDX version, and the decision to hold or migrate. No tagged WPF
    release should ship after .NET 8 EOL without either a .NET 10 migration or
    a documented exception.
  - Verify: `dotnet --info`; query the .NET releases index; `Find-Module ps2exe`;
    `dotnet tool search CycloneDX`; `dotnet test ... -c Release --nologo`.

- [ ] 🔬 🤖 P1 - Harden GitHub Actions
  supply-chain pinning separately from Scorecard.
  - Why: Cycle 1 already asks for Scorecard reporting, but the release workflow
    still consumes tag-based action refs such as `actions/checkout@v4`,
    `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`,
    `actions/download-artifact@v4`, `actions/attest-build-provenance@v2`, and
    `actions/attest-sbom@v2`. GitHub's hardening guidance recommends pinning
    third-party actions to full-length commit SHAs, and current action major
    streams have moved beyond the pinned tags.
  - Evidence: `.github/workflows/release.yml:36`,
    `.github/workflows/release.yml:235`, `.github/workflows/release.yml:355`,
    https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions,
    https://github.com/actions/checkout/releases/tag/v6.0.3,
    https://github.com/actions/upload-artifact/releases/tag/v7.0.1,
    https://github.com/actions/download-artifact/releases/tag/v8.0.1,
    https://github.com/actions/attest-build-provenance/releases/tag/v4.1.0
  - Touches: `.github/workflows/release.yml`, Dependabot or Renovate config,
    release docs.
  - Acceptance: every action ref is either pinned to a full commit SHA with a
    nearby comment naming the human-readable version, or is explicitly exempted
    in a policy file. Updates are batched through a documented dependency bot
    workflow and release dry-run.
  - Verify: add a workflow static check that fails on `uses: .*@v[0-9]`;
    run release workflow manually on a preview tag in a test branch.

- [ ] 🔬 🤖 P1 - De-risk Wpf.Ui
  adoption with the correct package identity.
  - Why: the v4.0 stable scope says to adopt Wpf.Ui `TitleBar`, `Snackbar` /
    `InfoBar`, `NumberBox`, and `SplitButton`, but the WPF project currently
    has no Wpf.Ui package reference. The active package line is `WPF-UI` 4.3.0;
    the similarly named `Wpf.Ui` NuGet ID is on an older 3.4.2.7 line. Adding
    the wrong ID would create avoidable API and docs drift.
  - Evidence: `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:23`,
    https://github.com/lepoco/wpfui/releases/tag/4.3.0,
    https://www.nuget.org/packages/WPF-UI,
    https://www.nuget.org/packages/Wpf.Ui
  - Touches: WPF csproj, `MainWindow.xaml`, theme dictionaries, WPF tests,
    roadmap v4 scope docs.
  - Acceptance: a short ADR or implementation note chooses `WPF-UI` 4.3.0 (or
    deliberately rejects it), lists which local custom controls it replaces,
    and proves the selected package can render the required controls without
    breaking the existing Mica, focus, and theme resources.
  - Verify: `dotnet add package WPF-UI --version 4.3.0` in a spike branch;
    `dotnet build`; parse XAML; manual light/dark/high-contrast smoke pass.

- [ ] 🔬 🤖 P1 - Add a WPF UI
  automation and accessibility regression harness.
  - Why: the WPF shell already has many `AutomationProperties.Name` /
    `HelpText` bindings, polite live regions, and custom focus restoration, but
    current tests are unit/regression tests only. Microsoft recommends Windows
    accessibility testing with tools such as Accessibility Insights and UIA
    Verify, and FlaUI 5.0.0 is current for UI Automation-based WPF tests.
  - Evidence: `src/LibreSpot.Desktop/MainWindow.xaml:54`,
    `src/LibreSpot.Desktop/MainWindow.xaml:1598`,
    `src/LibreSpot.Desktop/MainWindow.xaml.cs:142`,
    `tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj`,
    https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing,
    https://github.com/microsoft/accessibility-insights-windows,
    https://github.com/FlaUI/FlaUI/releases/tag/v5.0.0
  - Touches: WPF test project, CI workflow, app launch/test hooks, UX audit
    checklist.
  - Acceptance: an automated smoke test launches the WPF shell in a no-backend
    test mode, snapshots the UI Automation tree for Recommended, Custom,
    Maintenance, prompt, and activity states, and fails on unlabeled actionable
    controls, focus traps, missing live-region announcements, or obvious
    keyboard navigation regressions.
  - Verify: `dotnet test` runs the UIA tests on Windows runners or a documented
    local-only lane; Accessibility Insights manual fast pass produces no
    critical failures before v4 stable.

- [ ] 🔬 🤖 P1 - Define the Velopack
  app identity and update feed before packaging.
  - Why: distribution planning names Velopack, but the repo currently has no
    Velopack package, app ID, update channel, or `RELEASES` feed. Velopack
    1.2.0 is current, and its docs make the release feed the discovery point
    for updates; identity and install location decisions must be settled before
    the WPF shell moves from portable release asset to installed app.
  - Evidence: `ROADMAP.md:89`, `.github/workflows/release.yml:242`,
    `src/LibreSpot.Desktop/app.manifest:3`,
    https://docs.velopack.io/distributing/overview,
    https://github.com/velopack/velopack/releases/tag/1.2.0,
    https://www.nuget.org/packages/Velopack/1.2.0
  - Touches: packaging docs, release workflow, WPF csproj, app manifest, update
    check UX, installer/uninstaller docs.
  - Acceptance: a packaging design note chooses package ID, display name,
    update channel names, GitHub Releases vs external feed hosting, install
    root, Start Menu shortcut behavior, state migration from portable builds,
    and the rule for preserving Authenticode signatures across updates.
  - Verify: after implementation, run `vpk pack` / `vpk upload` dry-runs in a
    temp release folder and verify update discovery against a local feed.

- [ ] 🔬 🤖 P1 - Add a Windows
  PowerShell 5.1 and PowerShell 7 compatibility lane.
  - Why: README promises PowerShell 5.1+, the script and backend intentionally
    shell out to Windows PowerShell for SpotX isolation, and some code paths
    handle PowerShell 7 with `Import-Module Appx -UseWindowsPowerShell`.
    Release preflight currently parses through `pwsh`, so it does not prove the
    raw script still runs under the built-in Windows PowerShell host users get
    by default.
  - Evidence: `README.md:26`, `LibreSpot.ps1:283`, `LibreSpot.ps1:443`,
    `LibreSpot.ps1:4965`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:556`,
    `.github/workflows/release.yml:108`,
    https://learn.microsoft.com/en-us/powershell/scripting/install/install-powershell-on-windows
  - Touches: release workflow, PowerShell regression tests, README requirements
    copy, backend host-selection notes.
  - Acceptance: CI or a documented local release checklist runs syntax parse and
    non-mutating smoke commands under both `powershell.exe` 5.1 and current
    `pwsh` 7.6, with explicit unsupported-host messages for anything else.
  - Verify: `powershell.exe -NoProfile -File .\LibreSpot.ps1 -?` or equivalent
    no-op path; `pwsh -NoProfile` parse/import checks; full .NET tests.

- [ ] 🔬 🤖 P2 - Design a
  privacy-safe diagnostic export bundle.
  - Why: the WPF shell writes rolling Serilog logs and crash reports under
    `%LOCALAPPDATA%\LibreSpot`, then lets users copy the report path or open the
    crash folder. That is useful for support, but before fleet/admin docs ask
    users to share diagnostics, LibreSpot needs a redaction policy for local
    usernames, paths, environment details, command output, and future NDJSON
    logs.
  - Evidence: `src/LibreSpot.Desktop/Services/CrashReporter.cs:17`,
    `src/LibreSpot.Desktop/Services/CrashReporter.cs:53`,
    `src/LibreSpot.Desktop/Services/CrashReporter.cs:110`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1703`,
    `ROADMAP.md:104`
  - Touches: diagnostics design docs, crash/support UX, logging tests, fleet
    deployment docs.
  - Acceptance: a support-bundle spec names included files, excluded files,
    redaction patterns, retention, preview-before-share behavior, and a "no
    credentials/no Spotify tokens" invariant. Implementation can follow later.
  - Verify: once implemented, unit-test redaction patterns with sample logs and
    confirm exported bundles contain no raw user profile path or access token
    shaped strings.

- [ ] 🔬 🔧 P0 - Finalize package
  identity before any public distribution manifest.
  - Why: `winget search LibreSpot --source winget` found no existing Windows
    package on 2026-06-04, but the broader `librespot` name is already an
    established open-source Spotify client/library with distro and crates.io
    package identity. The existing roadmap has a rebrand decision, but package
    IDs, display names, executable names, protocol names, and support burden
    need one concrete decision before winget/Scoop/Chocolatey/Velopack files
    exist.
  - Evidence: local `winget search LibreSpot --source winget` on 2026-06-04,
    https://github.com/librespot-org/librespot,
    https://crates.io/crates/librespot,
    https://github.com/microsoft/winget-pkgs,
    `src/LibreSpot.Desktop/app.manifest:3`,
    `SIGNPATH.md:3`
  - Touches: product decision record, package manifests, SignPath docs, README,
    shell integration docs, future protocol/file associations.
  - Acceptance: operator records one canonical identity set: display name,
    package IDs, executable names, publisher string, config folder names,
    protocol URI, and whether old `%APPDATA%\LibreSpot` paths stay forever or
    migrate.
  - Verify: repeat winget search; search Chocolatey and Scoop; check GitHub and
    crates.io name collision notes; review SignPath and package manifests before
    first submission.

## 🔬 Researcher Queue (Cycle 3 - 2026-06-04)

Cycle 3 shifts from upstream/package tooling to reliability architecture. Tags:
🔬 = researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed if policy or environment access is required.

- [ ] 🔬 🤖 P0 - Add a versioned
  config schema and migration contract.
  - Why: PowerShell and WPF both normalize `config.json`, quarantine corrupt
    files, and silently coerce invalid values back to defaults, but no
    `SchemaVersion` / `ConfigVersion` key or external JSON schema exists. As
    fleet presets, shareable profiles, and package migration arrive, implicit
    best-effort normalization will not be enough to prove old configs upgrade
    safely.
  - Evidence: `LibreSpot.ps1:872`, `LibreSpot.ps1:1034`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs:4`,
    `src/LibreSpot.Desktop/Services/ConfigurationService.cs:41`,
    `tests/LibreSpot.Desktop.Tests/ConfigurationServiceTests.cs:31`,
    https://json-schema.org/draft/2020-12
  - Touches: config model, PowerShell normalizer, WPF `InstallConfiguration`,
    docs for presets/fleet, tests.
  - Acceptance: `config.json` includes a schema version; repo has a
    `schemas/librespot-config.schema.json`; old known config samples migrate
    through explicit version steps; unknown future schema versions fail with a
    clear recovery message instead of being silently rewritten.
  - Verify: JSON schema validation against valid/invalid samples; tests for
    v3.5, v3.6, v3.7, and malformed configs in both PowerShell and WPF lanes.

- [ ] 🔬 🤖 P1 - Add network,
  proxy, and GitHub rate-limit diagnostics.
  - Why: the script currently treats network readiness as a 5-second HEAD
    request to `https://raw.githubusercontent.com`, while downloads use
    `Invoke-WebRequest` then BITS fallback. BITS supports proxy configuration,
    and GitHub documents primary/secondary rate limits plus
    `x-ratelimit-remaining` / `x-ratelimit-reset` headers. LibreSpot should
    distinguish "offline", "GitHub blocked", "proxy auth required", "rate
    limited", and "hash mismatch" before telling users only that the internet is
    unavailable.
  - Evidence: `LibreSpot.ps1:4461`, `LibreSpot.ps1:4666`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:931`,
    https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api,
    https://learn.microsoft.com/en-us/powershell/module/bitstransfer/start-bitstransfer
  - Touches: network preflight, download helper, maintenance diagnostics, FAQ,
    fleet CLI status JSON.
  - Acceptance: Check Updates and install preflight report separate statuses for
    DNS/TLS failure, raw GitHub blocked, GitHub API rate limit, proxy-required
    BITS state, download timeout, and hash mismatch. Logs include actionable
    next steps without printing credentials or proxy secrets.
  - Verify: mock failing endpoints; force proxy/no-proxy paths; simulate GitHub
    `403` / `429` headers; confirm UI and headless status JSON differ by cause.

- [ ] 🔬 🤖 P1 - Add an
  operation journal for destructive actions.
  - Why: full reset, uninstall, restore vanilla, Marketplace replacement, and
    Spicetify cleanup already use guard helpers such as `Test-SafeRemovalTarget`
    and rollback exists for Spicetify apply failures. There is still no durable
    per-run journal that records what was about to be removed, what was removed,
    what backup was available, and which operations are reversible. That journal
    is the missing foundation for the roadmap's undo-selected-actions pane and
    fleet `--dry-run`.
  - Evidence: `LibreSpot.ps1:4894`, `LibreSpot.ps1:4936`,
    `LibreSpot.ps1:4954`, `LibreSpot.ps1:5393`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1174`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2128`,
    `ROADMAP.md:72`, `ROADMAP.md:102`
  - Touches: backend action model, log format, maintenance UI, dry-run/fleet
    design, tests.
  - Acceptance: every mutating action writes an operation id and JSONL journal
    under the LibreSpot profile with planned action, target, safety decision,
    result, and rollback hint. Dry-run emits the same journal with
    `wouldChange=true`; undo UI consumes only journal entries marked reversible.
  - Verify: tests prove unsafe paths are refused and journaled; full reset dry
    run produces no filesystem mutation; successful/failed Spicetify apply
    journals rollback status accurately.

- [ ] 🔬 🤖 P1 - Build a
  Spotify install-source compatibility fixture set.
  - Why: `winget show Spotify.Spotify --source winget` now reports Spotify
    `1.2.90.451.gb094aab0` as an EXE installer from
    `SpotifyFullSetupX64.exe`, while LibreSpot also handles Microsoft Store
    package `SpotifyAB.SpotifyMusic`, per-user AppData installs, legacy x86 /
    Windows 7 builds, cached installers, shortcuts, registry keys, and
    scheduled tasks. Those branches are too important to rely on live machines
    alone.
  - Evidence: local `winget show Spotify.Spotify --source winget` on
    2026-06-04, `LibreSpot.ps1:4963`, `LibreSpot.ps1:4994`,
    `LibreSpot.ps1:5028`, `LibreSpot.ps1:5054`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1664`,
    `README.md:86`
  - Touches: environment snapshot service, reset/uninstall tests, SpotX version
    picker, support docs.
  - Acceptance: repo has fixture descriptions or fake filesystem/registry
    tests for Store/MSIX Spotify, winget EXE Spotify, per-user installer,
    legacy x86, missing uninstaller, stale shortcuts, and partially removed
    app state. Diagnostics name the detected source and explain support level.
  - Verify: run reset/uninstall tests against fixture roots without touching the
    host; run one manual clean install on the current winget Spotify package
    before changing default compatibility pins.

- [ ] 🔬 🤖 P2 - Add Defender
  quarantine and antivirus-interference diagnostics.
  - Why: the research backlog already calls out antivirus-quarantined extension
    files. LibreSpot verifies hashes and Marketplace manifests, but it does not
    yet classify "file disappeared after download/apply" as possible Defender or
    endpoint-security interference. Microsoft documents Protection History and
    `MpCmdRun` restoration flows, but LibreSpot should only detect and guide,
    not auto-restore quarantined files.
  - Evidence: `ROADMAP.md:118`, `LibreSpot.ps1:5333`,
    `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:452`,
    https://learn.microsoft.com/en-us/defender-endpoint/restore-quarantined-files-microsoft-defender-antivirus
  - Touches: install verifier, Marketplace/custom app verifier, diagnostics
    copy, FAQ/trust docs.
  - Acceptance: when a verified download later vanishes or a known extension /
    custom app manifest is missing, diagnostics show a "possible security
    product quarantine" state with links to Windows Security Protection History
    and manual restore guidance. LibreSpot never disables AV, creates
    exclusions, or restores quarantined files automatically.
  - Verify: simulate missing verified files after download; test wording avoids
    encouraging users to bypass security products blindly.

- [ ] 🔬 🤖 P2 - Add PowerShell
  static analysis and Pester coverage for the script lane.
  - Why: .NET tests parse PowerShell files and enforce critical invariants, but
    they do not run PSScriptAnalyzer or Pester. PSScriptAnalyzer 1.25.0 is
    current, Pester 5.7.1 is the latest release, and the monolith still contains
    high-risk logic for downloads, scheduled tasks, config migration, path
    removal, and external process execution that would benefit from native
    PowerShell test semantics.
  - Evidence: `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs`,
    `LibreSpot.ps1:4666`, `LibreSpot.ps1:4894`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:999`,
    https://github.com/PowerShell/PSScriptAnalyzer/releases/tag/1.25.0,
    https://github.com/pester/Pester/releases/tag/5.7.1
  - Touches: test project or `tests/powershell`, release workflow, developer
    docs.
  - Acceptance: CI runs PSScriptAnalyzer with a curated rule set and Pester
    tests for pure/non-mutating PowerShell functions. Mutating functions are
    covered through mocked filesystem/registry/process wrappers before any real
    host mutation is allowed.
  - Verify: `Invoke-ScriptAnalyzer` passes with known justified suppressions;
    `Invoke-Pester` passes on Windows PowerShell 5.1 and PowerShell 7.6 lanes.

## 🔬 Researcher Queue (Cycle 4 - 2026-06-04)

Cycle 4 focuses on distribution-readiness boundaries that must be explicit
before package-manager, updater, shell-integration, or public support work
ships. Tags: 🔬 = researcher-added this cycle; 🤖 =
implementer-actionable now; 🔧 = operator-needed if policy or environment
access is required.

- [ ] 🔬 🤖 🔧 P0 - Add a
  community asset supply manifest and disable broken catalog entries.
  - Why: both the PowerShell and WPF catalogs download community extensions
    from branch-based raw GitHub URLs and community themes from branch archive
    URLs. A live HEAD check on 2026-06-04 returned `404` for all five current
    community extension URLs: `hidePodcasts.js`, `beautifulLyrics.js`,
    `playlistIcons.js`, `songStats.js`, and `volumePercentage.js`. Theme repos
    are also branch-pinned with no per-asset hash, license, or last-verified
    marker. That is too fragile for a signed installer or package-manager
    submission.
  - Evidence: `LibreSpot.ps1:729`, `LibreSpot.ps1:757`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:138`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:148`,
    live raw URL HEAD checks on 2026-06-04,
    https://api.github.com/repos/theRealPadster/spicetify-hide-podcasts,
    https://api.github.com/repos/surfbryce/beautiful-lyrics,
    https://api.github.com/repos/jeroentvb/spicetify-playlist-icons,
    https://api.github.com/repos/daksh2k/spicetify-stuff
  - Touches: catalog model, PowerShell and WPF download helpers, CI release
    preflight, README feature table, support diagnostics.
  - Acceptance: every community extension and theme has a tracked manifest row
    with display name, owner/repo, source URL, commit SHA, asset path, SHA256,
    SPDX license, support state, last verified date, and fallback behavior.
    Branch-only raw URLs are not used for install without a matching commit
    pin and hash. Entries that are `404`, repository-missing, unknown-license,
    or operator-blocked are hidden or clearly disabled before release.
  - Verify: CI downloads each enabled community asset from its pinned commit,
    verifies SHA256, validates SPDX/license policy, and fails if a URL returns
    non-2xx. Unit tests prove broken entries are not offered in Easy mode or
    Custom mode.

- [ ] 🔬 🤖 🔧 P0 - Add a
  third-party notices and license policy gate.
  - Why: the repo currently tracks only its MIT `LICENSE` plus archived
    research docs; there is no tracked `NOTICE`, `THIRD_PARTY_NOTICES.md`,
    `SECURITY.md`, `COPYING`, or license manifest. Current community asset
    metadata includes GPL-3.0, AGPL-3.0, WTFPL, MIT, and blank/unknown
    licenses. The earlier trust/legal disclosure item flags the user-facing
    posture; this item adds the concrete gate required before packaging or
    redistributing curated third-party assets.
  - Evidence: local `git ls-files '*LICENSE*' '*NOTICE*' '*SECURITY*'
    '*COPYING*' '*THIRD*'` on 2026-06-04, `LICENSE:1`,
    `LibreSpot.ps1:729`, `LibreSpot.ps1:757`,
    https://reuse.software/spec-3.3/,
    https://spdx.org/licenses/,
    https://opensource.guide/legal/
  - Touches: legal docs, catalog manifest, release checklist, package manager
    manifests, About dialog / diagnostics export.
  - Acceptance: repo has a third-party notices document or generated notices
    artifact covering NuGet packages, bundled/retrieved PowerShell tooling,
    SpotX/Spicetify dependencies, themes, extensions, icons, and generated
    assets. Each shipped or curated item has an SPDX expression, source URL,
    copyright holder where available, redistribution posture, and operator
    decision for copyleft/unknown licenses.
  - Verify: release preflight fails when a catalog item lacks SPDX/license
    data, when license text is missing for a shipped license, or when an
    operator-blocked license is enabled. Package artifacts include or link the
    notices output.

- [ ] 🔬 🤖 P1 - Add
  `SECURITY.md` and public intake templates before broader distribution.
  - Why: LibreSpot downloads executable/script content, patches local Spotify
    files, handles elevated actions, and will likely receive package-manager
    users who need a clear way to report security-sensitive failures. The repo
    currently has no tracked `SECURITY.md` or `.github/ISSUE_TEMPLATE/*`.
  - Evidence: local `git ls-files '.github/ISSUE_TEMPLATE/*' '*SECURITY*'`
    on 2026-06-04,
    https://docs.github.com/en/code-security/getting-started/adding-a-security-policy-to-your-repository,
    https://docs.github.com/articles/creating-an-issue-template-for-your-repository
  - Touches: `SECURITY.md`, issue templates, support docs, release checklist.
  - Acceptance: `SECURITY.md` defines supported versions, vulnerability report
    channel, expected response window, non-goals, and policy for Spotify /
    SpotX / Spicetify upstream issues. Issue forms separate bug reports,
    compatibility breakage, feature requests, and non-public security reports
    without asking users to paste secrets or private logs.
  - Verify: GitHub community health recognizes the security policy and issue
    templates; maintainer dry-run opens each template and confirms required
    fields capture OS, Spotify source/version, LibreSpot version, and sanitized
    diagnostics.

- [ ] 🔬 🤖 🔧 P1 - Decide the
  Windows support lifecycle after Windows 10 Home/Pro end of support.
  - Why: README requirements still say Windows 10/11, the WPF pitch promises
    Windows 10 fallback for Windows 11 Mica, and the app manifest lists legacy
    supportedOS GUIDs from Vista through Windows 10. Microsoft lifecycle data
    says Windows 10 Home/Pro reached end of support on 2025-10-14. LibreSpot
    also exposes legacy Spotify installer choices, so OS support, Spotify
    target version support, and best-effort compatibility need separate labels.
  - Evidence: `README.md:26`, `README.md:40`,
    `src/LibreSpot.Desktop/app.manifest:3`,
    `src/LibreSpot.Desktop/app.manifest:7`,
    https://learn.microsoft.com/en-us/lifecycle/products/windows-10-home-and-pro
  - Touches: README requirements, compatibility matrix, installer docs,
    diagnostics, app manifest support notes.
  - Acceptance: operator records one support policy for Windows 11, Windows 10
    Home/Pro after 2025-10-14, LTSC/ESU environments, Windows 7/8.1 Spotify
    target versions, ARM64, and PowerShell 5.1/7 lanes. Docs distinguish
    "supported host OS", "best-effort host OS", and "Spotify target version".
  - Verify: compatibility matrix and diagnostics report the same labels; WPF
    and PowerShell startup warnings do not contradict README/package metadata;
    release checklist requires one manual smoke test on each supported host OS.

- [ ] 🔬 🤖 P1 - Define a
  least-privilege elevation and notification boundary.
  - Why: the PowerShell script self-elevates immediately and the PS2EXE release
    is built with admin requirements, while the WPF app manifest is
    `asInvoker` and backend actions throw when admin is required. Microsoft
    documents that app notifications are not supported for elevated apps. The
    future WPF shell, protocol activation, repair diagnostics, and package
    update flow need a single boundary for what runs elevated and what remains
    usable without elevation.
  - Evidence: `LibreSpot.ps1:634`, `LibreSpot.ps1:659`,
    `.github/workflows/release.yml`, `src/LibreSpot.Desktop/app.manifest:7`,
    `src/LibreSpot.Desktop/Services/BackendScriptService.cs`,
    https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/send-local-toast,
    https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests,
    https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/how-it-works
  - Touches: WPF launcher, backend action contract, PS2EXE settings, updater,
    diagnostics, notification registration docs.
  - Acceptance: repo has an action matrix classifying each workflow as
    no-admin, prompts-for-admin, or admin-only. Non-mutating workflows such as
    browsing config, checking updates, exporting diagnostics, viewing logs, and
    receiving notifications run without elevation. Mutating Spotify/Spicetify
    actions request elevation only when executed and preserve clear cancel
    recovery.
  - Verify: automated tests assert non-mutating WPF commands do not require
    admin; manual run as a standard user can check updates and export
    diagnostics; elevated sessions do not claim toast support unless the
    notification path is proven.

- [ ] 🔬 🤖 P2 - Write the
  shell-integration registration design before implementing protocol, toasts,
  jump lists, or file associations.
  - Why: the roadmap already calls for `librespot://`, `.librespot` import
    association, jump lists, taskbar thumbnail buttons, tray minimize, and
    persistent toasts. Current planning does not yet define AppUserModelID,
    Start Menu shortcut ownership, protocol registry keys, package vs portable
    behavior, toast activation arguments, or uninstall cleanup. Those decisions
    need to align with the package identity and elevation boundary.
  - Evidence: `ROADMAP.md:58`, `ROADMAP.md:70`,
    https://learn.microsoft.com/en-us/windows/win32/shell/appids,
    https://learn.microsoft.com/en-us/windows/win32/shell/links,
    https://learn.microsoft.com/en-us/windows/win32/shell/fa-intro,
    https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/toast-desktop-apps
  - Touches: design doc, installer/updater, uninstall cleanup, app activation,
    notification service, diagnostics.
  - Acceptance: design specifies canonical AppUserModelID, shortcut path,
    protocol URI, `.librespot` ProgID, toast activation payloads, jump-list
    categories, portable-mode behavior, and uninstall rollback. It states
    which registrations are per-user vs machine-wide and how package-manager /
    Velopack installs differ from portable ZIP usage.
  - Verify: implementation checklist has registry/shortcut before-and-after
    captures; uninstall removes only LibreSpot-owned registrations; portable
    mode does not write shell registrations unless the user opts in.

## 🔬 Researcher Queue (Cycle 5 - 2026-06-04)

Cycle 5 audits release operations after the supply-chain and distribution
research from earlier cycles. It focuses on channel correctness, artifact
contracts, repeatable restores, release notes, and bad-release response. Tags:
🔬 = researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed if policy or environment access is required.

- [ ] 🔬 🤖 P0 - Add a release
  channel and tag verification gate.
  - Why: the release preflight already detects preview/RC tags, but the release
    creation step calls `gh release create "$TAG" --title "$TAG"
    --generate-notes` without `--prerelease`, `--latest=false`,
    `--verify-tag`, or `--fail-on-no-commits`. GitHub CLI docs state that
    `gh release create` can create a missing tag from the default branch unless
    `--verify-tag` is used; GitHub REST docs say releases default to
    `prerelease=false` and newly published releases default to latest. That can
    turn a WPF preview tag into a normal/latest release.
  - Evidence: `.github/workflows/release.yml:65`,
    `.github/workflows/release.yml:100`,
    `.github/workflows/release.yml:378`,
    https://cli.github.com/manual/gh_release_create,
    https://docs.github.com/v3/repos/releases/
  - Touches: release workflow, release checklist, README release channel docs.
  - Acceptance: release job derives channel from the validated tag. Stable
    tags create normal latest-eligible releases; `-preview.N` and `-rc.N` tags
    pass `--prerelease --latest=false`; every release uses `--verify-tag` and
    a no-duplicate/no-empty-release guard. Manual dispatch aborts if the input
    tag does not exist on the remote or does not point to the intended commit.
  - Verify: static workflow test covers stable, preview, RC, malformed, and
    missing-tag inputs; dry-run or disposable-repo run proves preview releases
    are prerelease and not latest.

- [ ] 🔬 🤖 🔧 P0 - Add a
  release artifact contract and post-upload audit.
  - Why: README and planning docs now say releases ship checksums, WPF
    artifacts, CycloneDX SBOM, and attestations, but the current public latest
    release `v3.7.2` exposes only `checksums.txt`, `LibreSpot.exe`, and
    `LibreSpot.ps1`; `v4.0.0-preview.1` exposes only
    `LibreSpot-v4.0.0-preview.1.exe`. The workflow uploads with `--clobber`,
    and GitHub release API output reports existing releases as
    `immutable=false`. The release contract needs to be machine-checked after
    upload instead of implied by workflow comments.
  - Evidence: local `gh release view v3.7.2 --json assets,isImmutable` on
    2026-06-04, local `gh release view v4.0.0-preview.1 --json
    assets,isImmutable` on 2026-06-04, `.github/workflows/release.yml:341`,
    `.github/workflows/release.yml:354`,
    `.github/workflows/release.yml:362`,
    `.github/workflows/release.yml:380`, `README.md:184`,
    https://docs.github.com/v3/repos/releases/
  - Touches: release workflow, README signing/verification docs, release
    checklist, support docs for historical releases.
  - Acceptance: repo defines an artifact matrix per release channel and
    produces a `release-manifest.json` containing expected asset names, sizes,
    SHA256, GitHub asset digest where available, attestation state, signer
    state, and channel. After upload, CI reads the GitHub release back and
    fails if any required asset, checksum entry, SBOM, attestation, or release
    channel flag is missing. Docs distinguish historical releases from the new
    contract.
  - Verify: release workflow post-upload step compares `gh release view
    --json assets,isPrerelease,isImmutable` to the manifest; checksum file is
    parsed and every listed asset exists exactly once; no non-draft release is
    overwritten without an explicit operator override.

- [ ] 🔬 🤖 P1 - Add NuGet
  lock-file restore and audit enforcement.
  - Why: Cycle 1 already asks for dependency refresh automation; this item is
    narrower: repeatable restore and advisory enforcement. The repo has no
    tracked `packages.lock.json`, `Directory.Packages.props`, `global.json`,
    or `.config/dotnet-tools.json`. Current `dotnet list package --vulnerable
    --include-transitive` is clean, but NuGet docs note advisory data changes
    over time and recommend checking restore output in CI. NuGet lock files and
    `--locked-mode` are designed for CI restore repeatability.
  - Evidence: local `git ls-files 'packages.lock.json'
    '**/packages.lock.json' 'Directory.Packages.props' 'global.json'
    '.config/dotnet-tools.json'` on 2026-06-04, local `dotnet list package
    --vulnerable --include-transitive` on 2026-06-04,
    https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files,
    https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages,
    https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-restore
  - Touches: project files, lock files or central package management, release
    workflow, developer docs.
  - Acceptance: CI restores NuGet dependencies in locked mode, runs NuGet Audit
    with an explicit severity threshold, and has a documented suppression path
    for accepted advisories. Dependency updates intentionally refresh lock
    files in the same PR/commit as the package version changes.
  - Verify: `dotnet restore --locked-mode` passes on a clean checkout; changing
    a PackageReference without updating lock files fails CI; vulnerability
    checks fail at the chosen threshold and show actionable package paths.

- [ ] 🔬 🤖 P1 - Add structured
  release notes and changelog gating.
  - Why: the workflow relies on generated notes, but there is no tracked
    `.github/release.yml` to categorize dependency, security, UX, breaking, or
    preview changes. `CHANGELOG.md` has an `[Unreleased]` section, but release
    creation does not verify that user-facing notes, known issues, verification
    commands, rollback notes, and channel-specific warnings are present before
    publishing.
  - Evidence: `.github/workflows/release.yml:378`, `CHANGELOG.md:5`,
    local `git ls-files '.github/release.yml'` on 2026-06-04,
    https://docs.github.com/en/repositories/releasing-projects-on-github/automatically-generated-release-notes
  - Touches: `.github/release.yml`, release checklist, changelog process,
    release workflow.
  - Acceptance: generated release notes are configured into stable categories,
    Dependabot/dependency changes are separated, and the release job prepends
    operator-authored notes for install impact, known issues, verification
    commands, signing status, and rollback guidance. Stable releases cannot
    publish while `[Unreleased]` is empty or still contains placeholder copy.
  - Verify: generate notes for a test tag and inspect categories; release
    preflight fails when required release-note sections are missing; published
    release body includes checksum/attestation verification commands.

- [ ] 🔬 🤖 🔧 P2 - Write a
  bad-release, signing, and rollback runbook.
  - Why: SignPath enrollment is pending, existing releases are mutable, and the
    workflow currently uses `--clobber` for asset upload. There is no documented
    operator path for a bad checksum, missing asset, compromised token,
    unsigned fallback, SmartScreen false positive, SignPath failure, or release
    that must be marked unsafe after publication.
  - Evidence: `SIGNPATH.md:76`, `SIGNPATH.md:106`,
    `.github/workflows/release.yml:181`,
    `.github/workflows/release.yml:286`,
    `.github/workflows/release.yml:386`,
    local `gh api repos/SysAdminDoc/LibreSpot/releases` on 2026-06-04,
    https://cli.github.com/manual/gh_release_create
  - Touches: release docs, `SECURITY.md`, SignPath docs, release workflow
    policy comments, support templates.
  - Acceptance: runbook defines when to yank, mark prerelease, edit release
    notes, delete assets, revoke/reissue signing credentials, rotate GitHub
    secrets, publish a superseding hotfix, and notify users. It distinguishes
    historical mutable releases from future immutable releases and says whether
    `--clobber` is allowed only for draft releases.
  - Verify: tabletop exercise against one hypothetical missing-SBOM release and
    one compromised-signing-token scenario; checklist includes exact `gh` and
    SignPath dashboard commands without requiring destructive execution.

## 🔬 Researcher Queue (Cycle 6 - 2026-06-04)

Cycle 6 inspects security boundaries created by elevated execution, PowerShell
script dispatch, archives, scheduled tasks, and local diagnostics. This is a
planning pass, not a completed formal Codex Security report. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed if policy or environment access is required.

- [ ] 🔬 🤖 P0 - Harden elevated
  backend runtime extraction and watcher entrypoint integrity.
  - Why: WPF extracts the embedded backend script to
    `%LOCALAPPDATA%\LibreSpot\Runtime\LibreSpot.Backend.ps1`, verifies the file
    hash before reuse, then executes it with Windows PowerShell. The watcher
    task registers a persistent `powershell.exe -ExecutionPolicy Bypass -File
    <entry>` command where `<entry>` is the backend script path. The hash check
    is a strong mitigation, but an elevated process executing a user-writable
    script path still deserves a race-resistant runtime directory and a watcher
    launcher that validates integrity at execution time.
  - Evidence: `src/LibreSpot.Desktop/Services/BackendScriptService.cs:27`,
    `src/LibreSpot.Desktop/Services/BackendScriptService.cs:84`,
    `src/LibreSpot.Desktop/Services/BackendScriptService.cs:223`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:547`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:561`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:573`,
    https://learn.microsoft.com/en-us/windows/security/identity-protection/user-account-control/how-user-account-control-works,
    https://learn.microsoft.com/en-us/dotnet/api/system.security.accesscontrol.filesystemrights
  - Touches: backend runtime extraction, watcher registration, release
    packaging, tests.
  - Acceptance: runtime script directory is created with explicit ACLs and
    inheritance policy suitable for elevated execution; backend execution uses
    either a per-process immutable temp copy or a post-open verified handle so
    the file cannot be swapped between validation and execution. Scheduled task
    action points to a signed installed executable or to a launcher that
    verifies the backend script hash before every run.
  - Verify: unit tests reject world-writable runtime directories; watcher XML
    tests prove task actions do not point at unverified mutable script content;
    manual standard-user/elevated-user race test cannot replace the script
    between hash validation and process start.

- [ ] 🔬 🤖 P1 - Add a safe
  archive extraction helper for every downloaded ZIP.
  - Why: both the PowerShell monolith and WPF backend call
    `[System.IO.Compression.ZipFile]::ExtractToDirectory` directly for
    Spicetify CLI, community themes, official themes, and Marketplace. Core
    pinned archives are hash-verified, but community theme archives are
    branch-based, and the code does not have one shared pre-extraction validator
    for absolute paths, `..` traversal, duplicate entries, oversized entries,
    symlink/reparse-point surprises, or unexpected top-level structure.
  - Evidence: `LibreSpot.ps1:5161`, `LibreSpot.ps1:5194`,
    `LibreSpot.ps1:5233`, `LibreSpot.ps1:5329`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1869`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1911`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1945`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2045`,
    https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.zipfile.extracttodirectory,
    https://cwe.mitre.org/data/definitions/23.html,
    https://owasp.org/www-community/attacks/Path_Traversal
  - Touches: archive helper, theme/Marketplace/Spicetify installers, tests,
    catalog verifier.
  - Acceptance: all archive extraction flows go through one safe helper that
    enumerates entries before extraction, rejects paths escaping the target,
    rejects absolute/rooted paths, enforces max file count and expanded byte
    limits, extracts into an empty unique directory, and verifies required
    manifest files before copying to final locations.
  - Verify: malicious ZIP fixtures with `../`, absolute Windows paths, duplicate
    names, huge declared sizes, and missing required files are rejected; normal
    Spicetify CLI, Marketplace, and theme fixtures still extract successfully.

- [ ] 🔬 🤖 P1 - Add PowerShell
  execution-policy and WDAC/ConstrainedLanguage compatibility gates.
  - Why: LibreSpot intentionally uses `-ExecutionPolicy Bypass` for its own
    generated scripts, SpotX, and watcher task. Microsoft documents execution
    policy as a safety feature rather than a security boundary, and PowerShell
    can run in ConstrainedLanguage mode under AppLocker or Windows Defender
    Application Control. LibreSpot should not promise that bypassing execution
    policy defeats enterprise application control, and it should diagnose CLM /
    WDAC failures separately from normal script errors.
  - Evidence: `LibreSpot.ps1:285`, `LibreSpot.ps1:449`,
    `LibreSpot.ps1:4735`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:561`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1016`,
    https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_execution_policies,
    https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_language_modes,
    https://learn.microsoft.com/en-us/powershell/scripting/security/app-control/application-control
  - Touches: preflight diagnostics, backend error classifier, FAQ/support docs,
    enterprise/fleet docs.
  - Acceptance: startup/preflight records PowerShell edition, version,
    execution policy scopes, language mode, and App Control/WDAC/AppLocker
    indicators where available. CLM or policy-blocked script execution produces
    a clear support message and does not tell users to weaken enterprise
    controls blindly.
  - Verify: run in normal FullLanguage, simulated ConstrainedLanguage, and
    restricted execution-policy scenarios; diagnostics classify each state and
    tests prove the copy never claims execution policy is a security boundary.

- [ ] 🔬 🤖 P1 - Add an external
  process execution contract for SpotX and Spicetify.
  - Why: WPF safely allow-lists backend actions and uses
    `ProcessStartInfo.ArgumentList`, but the PowerShell backend still launches
    SpotX through a single-string `Start-Process -ArgumentList` due Windows
    PowerShell 5.1 redirected-output quirks. The code comments state that this
    is safe because the file path is generated and arguments come from
    `Build-SpotXParams`; that guarantee should be locked into tests and a small
    command-contract table before more dynamic flags, custom patch paths, or
    fleet CLI inputs are added.
  - Evidence: `src/LibreSpot.Desktop/Services/BackendScriptService.cs:17`,
    `src/LibreSpot.Desktop/Services/BackendScriptService.cs:111`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1008`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1016`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1800`,
    `tests/LibreSpot.Desktop.Tests/BackendScriptServiceTests.cs:9`
  - Touches: SpotX parameter builder, process helper, tests, fleet CLI design.
  - Acceptance: repo documents each external executable/script, allowed
    argument sources, quoting strategy, timeout, output capture, exit-code
    handling, and security rationale. Tests prove every SpotX argument is from
    a fixed flag or normalized enum/integer and that future free-form arguments
    must use tokenized execution or explicit escaping.
  - Verify: add regression tests for `Build-SpotXParams` covering every
    user-controlled config field; fuzz path/config values containing quotes,
    semicolons, pipes, ampersands, and newlines; confirm no payload becomes an
    extra PowerShell command.

- [ ] 🔬 🤖 P2 - Add a local
  data security and retention inventory.
  - Why: Cycle 2 already asks for a privacy-safe diagnostic export bundle; this
    item covers the data at rest before export. WPF logs roll for 14 days,
    crash reports persist for 30 days, PowerShell writes `install.log`,
    `watcher.log`, `config.json`, and `watcher-state.json`, and crash reports
    include full exception strings that may contain local paths. There is no
    single inventory naming file locations, sensitivity, ACL expectations,
    retention, and delete/export behavior.
  - Evidence: `src/LibreSpot.Desktop/Services/CrashReporter.cs:14`,
    `src/LibreSpot.Desktop/Services/CrashReporter.cs:30`,
    `src/LibreSpot.Desktop/Services/CrashReporter.cs:110`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:94`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:500`,
    `LibreSpot.ps1:230`
  - Touches: diagnostics docs, support export, settings UI, delete/reset flow,
    tests.
  - Acceptance: repo has a data inventory for config, logs, crash reports,
    watcher state, temp files, backups, release artifacts, and future support
    bundles. Each row states location, owner, sensitivity, retention, user
    delete path, whether it is included in exports, and redaction rules.
  - Verify: tests or script checks confirm retention limits are enforced; Reset
    and uninstall docs state which LibreSpot data remains; sample support bundle
    cannot include raw username, home path, tokens, proxy credentials, or full
    command output without preview.

## 🔬 Researcher Queue (Cycle 7 - 2026-06-04)

Cycle 7 inspects WPF desktop quality gates that should land before a stable
native shell: contrast themes, reduced motion, localization extraction, release
publish footprint, and keyboard/focus regression coverage. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed if policy or release-channel decisions are required.

- [ ] 🔬 🤖 P0 - Add a high-contrast
  and reduced-motion theme contract for the WPF shell.
  - Why: the current desktop theme is custom and dark-first, with palette
    resources in `Themes/Palette.xaml`, many `Storyboard` / `DoubleAnimation`
    blocks in `MainWindow.xaml` and `Themes/Controls.xaml`, and custom controls
    that set `FocusVisualStyle` to null before drawing their own focus rings.
    There is no high-contrast resource dictionary, no `SystemColors` mapping,
    and no reduced-motion switch for hover, press, overlay, shimmer, or
    entrance animations.
  - Evidence: `src/LibreSpot.Desktop/Themes/Palette.xaml`,
    `src/LibreSpot.Desktop/Themes/Controls.xaml:175`,
    `src/LibreSpot.Desktop/Themes/Controls.xaml:944`,
    `src/LibreSpot.Desktop/Themes/Controls.xaml:1073`,
    `src/LibreSpot.Desktop/MainWindow.xaml:383`,
    `src/LibreSpot.Desktop/MainWindow.xaml:1556`,
    https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes,
    https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/motion,
    https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility
  - Touches: WPF resource dictionaries, shell integration, custom control
    styles, theme tests, accessibility docs.
  - Acceptance: the shell detects Windows high-contrast state, maps semantic UI
    resources to system foreground/background/highlight/button colors, avoids
    color-only state changes, and disables or replaces nonessential animations
    when system animation/reduced-motion settings call for it. Mica, shadows,
    gradients, shimmer, hover scale, overlay enter animations, and selected
    states have explicit high-contrast and no-motion behavior.
  - Verify: Accessibility Insights fast pass plus manual checks in all four
    built-in contrast themes; keyboard focus remains visible on every command;
    screenshots compare normal/high-contrast/no-motion states; a test or design
    token check proves every new storyboard is gated by the motion policy.

- [ ] 🔬 🤖 P1 - Add a desktop
  publish footprint and cold-start performance budget.
  - Why: the release workflow publishes the WPF shell as a self-contained
    win-x64 single-file executable, but it does not record artifact size,
    startup time, working-set budget, or a ReadyToRun/trimming decision. A
    local release-equivalent publish on 2026-06-04 produced
    `LibreSpot.exe` at 162,327,508 bytes (154.81 MiB), so package-manager,
    Velopack, and portable-download work need a deliberate footprint budget
    before the asset is treated as stable.
  - Evidence: `.github/workflows/release.yml:249`,
    `.github/workflows/release.yml:252`,
    `.github/workflows/release.yml:253`,
    `src/LibreSpot.Desktop/LibreSpot.Desktop.csproj:1`,
    local `dotnet publish src/LibreSpot.Desktop/LibreSpot.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true` on 2026-06-04,
    https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview,
    https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities,
    https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run
  - Touches: release workflow, WPF publish profile/project settings, release
    docs, package-manager manifests, startup smoke tests.
  - Acceptance: release CI records WPF exe size, compressed artifact size,
    cold-start-to-main-window time, first-refresh time, and baseline working set
    for win-x64. The repo documents why WPF trimming is disabled unless .NET
    support changes, evaluates ReadyToRun as a measured startup/size tradeoff,
    and sets fail/warn thresholds before winget, Scoop, Chocolatey, or Velopack
    publish the desktop asset.
  - Verify: CI publish job emits a machine-readable metrics file and fails only
    on agreed thresholds; local smoke starts the published exe and exits cleanly;
    any ReadyToRun experiment includes before/after size and startup numbers;
    release notes state the runtime/size tradeoff for users choosing portable
    downloads.

- [ ] 🔬 🤖 P1 - Convert the
  existing localization roadmap row into an extraction-first implementation
  plan.
  - Why: the Next Release Queue already calls for localization, but the current
    WPF shell has no `.resx` or resource files, many hard-coded English XAML
    strings, hard-coded automation help text/tooltips, and user-facing time or
    number formatting that sometimes uses `InvariantCulture`. The first
    implementation slice should extract and gate strings before adding
    translation vendors or runtime culture switching.
  - Evidence: `rg --files src/LibreSpot.Desktop | rg "\\.(resx|resources)$"`
    returned no resource files on 2026-06-04,
    `src/LibreSpot.Desktop/MainWindow.xaml:455`,
    `src/LibreSpot.Desktop/MainWindow.xaml:575`,
    `src/LibreSpot.Desktop/MainWindow.xaml:715`,
    `src/LibreSpot.Desktop/MainWindow.xaml:1478`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:992`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1967`,
    https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview
  - Touches: WPF resources, view models, converters, UI tests, docs,
    localization contribution workflow.
  - Acceptance: choose a WPF-compatible resource strategy for .NET WPF
    satellite assemblies, move visible strings, tooltips, automation names,
    prompts, status messages, and release-facing copy behind resources, and add
    a CI check that blocks new hard-coded user-visible strings outside approved
    exceptions. Culture-sensitive display uses current culture, while file
    names, machine logs, hashes, and command contracts keep invariant culture.
  - Verify: pseudo-localized build covers main window, prompts, activity panel,
    settings search, and maintenance flows without clipping; unit tests cover
    culture-sensitive formatting; raw-string scanner reports zero unexpected
    UI literals; translators can update one resource file without touching
    view-model logic.

- [ ] 🔬 🤖 P2 - Add keyboard,
  focus, and overlay regression coverage for custom WPF controls.
  - Why: the shell already has useful accessibility work, including
    `AutomationProperties` names/help text, Escape handling, focus restoration,
    and tab cycling for modal overlays. That reduces risk, but custom styles,
    custom focus visuals, a search box, tabbed pages, command cards, activity
    panel, and confirmation prompts still need an explicit keyboard contract
    before the broader UI automation harness from Cycle 2 can catch real
    regressions.
  - Evidence: `src/LibreSpot.Desktop/MainWindow.xaml:20`,
    `src/LibreSpot.Desktop/MainWindow.xaml:1546`,
    `src/LibreSpot.Desktop/MainWindow.xaml:1919`,
    `src/LibreSpot.Desktop/MainWindow.xaml.cs:151`,
    `src/LibreSpot.Desktop/MainWindow.xaml.cs:178`,
    `src/LibreSpot.Desktop/Themes/Controls.xaml:837`,
    https://learn.microsoft.com/en-us/accessibility-tools-docs/items/wpf/control_automationproperties,
    https://learn.microsoft.com/en-us/accessibility-tools-docs/items/wpf/control_iskeyboardfocusable,
    https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing
  - Touches: WPF tests, control styles, accessibility documentation, UI
    automation harness.
  - Acceptance: define tab order, default/cancel buttons, Escape behavior,
    focus trap/restoration, disabled-state focusability, and screen-reader
    names for the main pages, search results, activity overlay, and decision
    prompt. Every custom control either preserves a visible system focus cue or
    documents and tests its custom replacement.
  - Verify: automated or scripted keyboard walks cover Recommended, Custom,
    Maintenance, activity, and prompt surfaces; Accessibility Insights event
    monitoring sees focus move predictably; tests fail if a modal opens without
    initial focus or closes without restoring prior focus.

## 🔬 Researcher Queue (Cycle 8 - 2026-06-04)

Cycle 8 inspects delivery surfaces that decide whether the legacy PowerShell
script, embedded WPF backend, and native shell can keep moving together:
parity manifests, backend event contracts, non-release CI visibility, and
contributor intake. Tags: 🔬 = researcher-added this cycle; 🤖 =
implementer-actionable now; 🔧 = operator-needed where branch or community
policy decisions are required.

- [ ] 🔬 🤖 P0 - Generate a
  script/WPF/backend parity manifest before v4 stable.
  - Why: v4.0 stable is defined as having parity with the script shell, but
    parity is not yet a generated artifact. The WPF shell has 37 persisted
    `InstallConfiguration` properties, while the PowerShell `EasyDefaults`
    table has 34 explicit keys and leaves `Mode`, `SpotX_HideColIconOff`, and
    `SpotX_OldLyrics` implicit. The WPF backend service has a nine-action
    allow-list, while the visible maintenance catalog exposes five maintenance
    actions. Existing tests assert important hand-picked invariants, but they
    do not emit a single table showing every script option, backend action,
    config key, default value, UI control, and test owner.
  - Evidence: `ROADMAP.md:52`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs:5`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs:275`,
    `src/LibreSpot.Desktop/Services/BackendScriptService.cs:20`,
    `LibreSpot.ps1:786`,
    `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:7`,
    local config/action comparison on 2026-06-04,
    https://pester.dev/docs/commands/Invoke-Pester
  - Touches: generated docs or manifest, AppCatalog, PowerShell defaults,
    backend action registry, regression tests, v4 release checklist.
  - Acceptance: repo has a generated parity manifest that lists every
    user-facing option, default value, JSON key, SpotX flag, Spicetify action,
    maintenance action, watcher action, script availability, WPF availability,
    and automated test coverage. The manifest fails CI when a key or action is
    added in one lane without an owner, migration rule, and explicit parity
    decision.
  - Verify: generator output is deterministic; `dotnet test` compares the
    checked-in manifest to live source; Pester or text-parsing tests compare
    PowerShell defaults and `Build-SpotXParams`; a v4 stable release checklist
    links to the manifest and has no `unknown` or `unowned` rows.

- [ ] 🔬 🤖 P1 - Version the
  backend event protocol and add replay fixtures.
  - Why: the WPF backend currently streams pipe-delimited messages with the
    `@@LS@@|` prefix; the desktop service splits each line into kind, level,
    and payload, while the PowerShell backend emits `progress`, `status`,
    `step`, `action`, `result`, and fallback `log` messages. This works, but it
    has no schema, version field, escaping contract beyond newline replacement,
    replay corpus, or compatibility rule for new message kinds. A stable shell
    needs backend progress and error text to remain parseable across updates,
    watcher runs, and future fleet/JSON modes.
  - Evidence: `src/LibreSpot.Desktop/Services/BackendScriptService.cs:15`,
    `src/LibreSpot.Desktop/Services/BackendScriptService.cs:187`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:216`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:245`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2301`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:2311`,
    https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation-modes,
    https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts
  - Touches: backend message writer/parser, event fixtures, tests, release
    notes, future fleet/diagnostic docs.
  - Acceptance: backend messages have a documented versioned contract, either
    by moving to compact JSON lines or by formally specifying the current
    delimiter format, escaping rules, allowed kinds, severity values, progress
    range, terminal result semantics, and unknown-kind behavior. CI stores
    replay fixtures for successful install, canceled run, failed download,
    watcher deferred, watcher reapplied, and backend exception paths.
  - Verify: parser tests replay fixtures into `MainViewModel` without starting
    PowerShell; malformed, unknown, multiline, high-volume, and partial-line
    messages are handled deterministically; release CI uploads protocol
    fixtures as artifacts for failed runs.

- [ ] 🔬 🤖 P1 - Add a
  non-release CI workflow with visible contract summaries.
  - Why: the only tracked workflow is `Release`, triggered by version tags or
    manual dispatch. Its WPF job runs `dotnet test`, but ordinary pushes and
    pull requests do not get a separate CI workflow, job summary, test result
    artifact, analyzer output, or parity-manifest diff before release time.
    That pushes routine failures into the tagged release path and makes
    implementers inspect logs instead of a concise contract summary.
  - Evidence: `.github/workflows/release.yml:9`,
    `.github/workflows/release.yml:13`,
    `.github/workflows/release.yml:245`,
    `.github/workflows/release.yml:279`,
    local `.github` tree on 2026-06-04 only contained
    `.github/workflows/release.yml`,
    https://docs.github.com/en/actions/reference/workflow-commands-for-github-actions,
    https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts
  - Touches: `.github/workflows/ci.yml`, test output configuration, parity
    manifest generator, artifact retention policy, developer docs.
  - Acceptance: PR and main-branch pushes run a fast CI workflow separate from
    release. It reports version sync, parse checks, `dotnet test`, parity
    manifest status, PowerShell analyzer/Pester lane when that Cycle 3 item is
    implemented, docs-link checks, and docs-only skip decisions in
    `GITHUB_STEP_SUMMARY`. Failing jobs upload structured test output,
    generated manifests, and logs with a documented retention period.
  - Verify: open a test PR that changes only docs and one that changes a config
    key; CI summaries show what ran and why; artifacts include test and parity
    output; release workflow can depend on the same scripts without duplicating
    logic.

- [ ] 🔬 🤖 🔧 P2 - Add repository
  community-health and contributor intake files.
  - Why: GitHub's community-profile API reported 42% health for the repository
    on 2026-06-04. README and MIT license are present, but code of conduct,
    contributing guide, issue template, and pull request template are null, and
    there is no tracked CODEOWNERS file. Cycle 4 already covers security
    intake; this item covers ordinary bugs, compatibility reports, feature
    requests, roadmap-only contributions, and ownership routing for sensitive
    areas such as release, signing, backend scripts, and package manifests.
  - Evidence: local `.github` tree on 2026-06-04,
    `gh api repos/SysAdminDoc/LibreSpot/community/profile` on 2026-06-04,
    https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/creating-a-default-community-health-file,
    https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners
  - Touches: `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SUPPORT.md`,
    `.github/ISSUE_TEMPLATE/*`, `.github/PULL_REQUEST_TEMPLATE.md`,
    `.github/CODEOWNERS`, roadmap contribution docs.
  - Acceptance: contributors can file bug, compatibility, release, packaging,
    feature, and documentation reports with fields for Windows version,
    Spotify install source/version, Spicetify version, LibreSpot version,
    selected profile, logs, and reproduction steps. PR template requires scope,
    risk, tests, screenshots for UI work, release-note impact, and whether
    roadmap/research docs changed. CODEOWNERS routes release/signing, scripts,
    WPF shell, docs, and package manifests to explicit maintainers or a
    documented placeholder until teams exist.
  - Verify: GitHub community-profile API health rises after files land; issue
    forms render without schema errors; CODEOWNERS syntax validates; a sample
    bug report contains enough data to reproduce a Spotify version mismatch
    without asking the reporter for basic environment details.

## 🔬 Researcher Queue (Cycle 9 - 2026-06-04)

Cycle 9 narrows the existing Alternative clients row into compliance-first
ecosystem work. It does not reopen the broader macOS/Linux support decision
from Cycle 1; it focuses on how LibreSpot should present third-party clients
without implying official support, unsafe downloads, or policy certainty. Tags:
🔬 = researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where legal/support policy decisions are required.

- [ ] 🔬 🤖 🔧 P1 - Build an
  alternative-client capability and compliance matrix before adding cards.
  - Why: Spotube, Psst, and Ncspot are not interchangeable alternatives to the
    patched Windows Spotify flow. Live GitHub checks on 2026-06-04 showed
    Spotube as active with v5.1.1 published 2026-02-24 and 46k+ stars; Psst as
    recently pushed but still describing itself as early and requiring Premium;
    Ncspot as active with v1.3.4 published 2026-05-22 and Premium-only terminal
    UX. Spotify's February 2026 developer-platform update added Premium and
    user-count limits for Development Mode, and the Developer Policy restricts
    streaming, replacement clients, branding, data use, and integrations with
    content from another service. LibreSpot needs a factual matrix and legal
    disclaimer before any UI suggests these are safe drop-in replacements.
  - Evidence: `ROADMAP.md:62`,
    live GitHub API checks for `KRTirtho/spotube`, `jpochyla/psst`, and
    `hrkfdn/ncspot` on 2026-06-04,
    `KRTirtho/spotube` README on 2026-06-04,
    `jpochyla/psst` README on 2026-06-04,
    `hrkfdn/ncspot` README on 2026-06-04,
    https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security,
    https://developer.spotify.com/policy,
    https://developer.spotify.com/terms/
  - Touches: roadmap docs, README comparison table, future WPF cards, support
    docs, legal/trust copy.
  - Acceptance: matrix lists each client name, upstream URL, latest release,
    last push, license/SPDX status, platform support, package-manager channels,
    Premium requirement, playback source, account/auth model, Spotify Connect
    support, offline/download claims, lyrics support, telemetry claims, package
    signatures/checksums, and known policy/support caveats. UI cards link out
    only after maintainers approve the disclaimer and support boundary.
  - Verify: regenerate the matrix from GitHub API plus checked README snippets;
    cards cannot show install buttons until every row has a support state,
    verified source URL, and policy note; docs state that LibreSpot does not
    endorse, bundle, modify, or support third-party clients.

- [ ] 🔬 🤖 P2 - Add an
  ecosystem freshness monitor for alternative-client recommendations.
  - Why: candidate projects have different release models: Spotube publishes
    normal releases, Psst's latest release is a rolling tag, and Ncspot has
    frequent tagged releases. A static README table will go stale quickly,
    especially while Spotify is changing API access and platform policy. The
    same monitor can catch archived repos, disabled repos, missing releases,
    license changes, package channel breakage, and major README claims such as
    "Premium required" or "downloadable tracks" that should affect LibreSpot's
    wording.
  - Evidence: `KRTirtho/spotube` latest release `v5.1.1` on 2026-02-24,
    `jpochyla/psst` latest release `rolling` on 2025-06-28,
    `hrkfdn/ncspot` latest release `v1.3.4` on 2026-05-22,
    `librespot-org/librespot` latest release `v0.8.0` on 2025-11-10,
    https://docs.github.com/en/actions/reference/workflow-commands-for-github-actions,
    https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts
  - Touches: research scripts, CI summaries, README/roadmap data tables,
    support docs.
  - Acceptance: scheduled or manual research workflow queries GitHub metadata
    for approved alternatives and writes a summary with latest release, push
    date, archive status, star count, license, package channels, and warning
    flags. Warnings are generated when a repo is archived/disabled, latest
    release is older than a chosen threshold, license detection changes, README
    claims shift into higher-risk territory, or Spotify policy pages change.
  - Verify: dry-run monitor produces deterministic Markdown/JSON output; a
    fixture with an archived repo and stale release triggers warnings; workflow
    summary explains whether the public cards remain current.

- [ ] 🔬 🤖 🔧 P2 - Define a
  safe handoff policy for alternative-client install links.
  - Why: the current product is a Windows Spotify patcher, while alternative
    clients may be cross-platform, terminal-only, Premium-only, use different
    playback sources, or have their own update channels. Automatically
    installing or deep-linking to binaries would expand LibreSpot's support and
    legal surface beyond its signed artifacts. A safer first implementation is
    an informational card with user-controlled external links, source/status
    metadata, and a clear "not managed by LibreSpot" boundary.
  - Evidence: `ROADMAP.md:62`, `README.md:7`,
    `README.md:51`,
    `KRTirtho/spotube` README installation table on 2026-06-04,
    `jpochyla/psst` README download table on 2026-06-04,
    `hrkfdn/ncspot` README installation notes on 2026-06-04,
    https://developer.spotify.com/policy
  - Touches: WPF card UI, README, support docs, trust/legal disclosure,
    telemetry-free external-link handling.
  - Acceptance: first release of alternative-client cards is docs/link-only:
    no automatic download, no bundled installer, no package-manager invocation,
    no account-token handling, and no support promise beyond showing current
    upstream metadata. Cards open verified upstream project/release/package
    pages in the browser, include a support boundary, and distinguish GUI,
    terminal, mobile, desktop, Premium-only, and non-Spotify-audio-source
    behaviors.
  - Verify: UI tests prove cards cannot execute installers; external-link
    allow-list contains only approved upstream URLs; support docs include a
    sample response for users asking LibreSpot to troubleshoot a third-party
    client.

## 🔬 Researcher Queue (Cycle 10 - 2026-06-04)

Cycle 10 inspects the user-facing documentation and source-tree hygiene around
release channels. Cycle 5 already covers the release artifact contract itself;
this pass focuses on making README badges, screenshots, tracked files, and
trust claims consume that contract instead of drifting independently. Tags:
🔬 = researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where release/support policy decisions are required.

- [ ] 🔬 🤖 P1 - Separate source
  assets from generated release artifacts.
  - Why: the repository currently tracks `LibreSpot.exe`, `LibreSpot.ps1`, and
    `checksums.txt` at the root. `LibreSpot.ps1` is source, but `LibreSpot.exe`
    and `checksums.txt` look like generated release outputs that can drift from
    GitHub Releases and confuse users or automated checks about which binary
    and checksum are authoritative. GitHub's large-file guidance recommends
    using Releases to distribute binary files rather than tracking generated
    binaries in the repository.
  - Evidence: local `git ls-files checksums.txt LibreSpot.exe LibreSpot.ps1`
    on 2026-06-04,
    `gh release view v3.7.2 --json assets` on 2026-06-04,
    https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github,
    https://docs.github.com/articles/about-releases
  - Touches: `.gitignore`, release workflow, README download instructions,
    support docs, release checklist.
  - Acceptance: repo defines which root-level binaries or checksum files are
    source artifacts versus generated release outputs. Generated `.exe`,
    checksum, SBOM, attestation, and signed-artifact files are ignored or stored
    only as release assets; if a bootstrap binary is intentionally tracked, it
    has a documented provenance, version, checksum, and update rule.
  - Verify: `git status --ignored` after a local release build shows generated
    artifacts ignored; README download links point to GitHub Releases, not
    root-tree binaries; a release dry-run proves checksums are generated from
    fresh build outputs.

- [ ] 🔬 🤖 P1 - Generate README
  badges, screenshots, and trust claims from release metadata.
  - Why: README badges advertise `Version 4.0.0-preview.6` and `Stable 3.7.2`,
    but live GitHub release metadata on 2026-06-04 shows latest stable
    `v3.7.2` and latest preview release `v4.0.0-preview.1`. README screenshots
    use February 2026 attachment names, while the v3.7 UI and WPF preview have
    changed substantially. The signing/verification section also lists
    `LibreSpot-Desktop.exe`, SBOM, and attestation expectations that depend on
    the Cycle 5 release-manifest contract rather than the actual latest stable
    release assets.
  - Evidence: `README.md:11`, `README.md:12`, `README.md:29`,
    `README.md:31`, `README.md:181`, `README.md:184`,
    `README.md:191`, `README.md:192`,
    `gh release list --limit 10 --json tagName,isPrerelease,isImmutable`
    on 2026-06-04,
    `gh release view v3.7.2 --json assets,isPrerelease,isImmutable`
    on 2026-06-04,
    `gh release view v4.0.0-preview.1 --json assets,isPrerelease,isImmutable`
    on 2026-06-04,
    https://docs.github.com/en/repositories/releasing-projects-on-github/automatically-generated-release-notes,
    https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations/using-artifact-attestations-to-establish-provenance-for-builds,
    https://cli.github.com/manual/gh_attestation_verify
  - Touches: README, screenshot capture workflow, release-manifest consumer,
    docs tests, support/trust docs.
  - Acceptance: README badges and verification tables are generated from the
    release manifest or live release metadata, not hand-maintained strings.
    Stable and preview channels are shown separately with exact asset names,
    signing state, checksum state, SBOM state, and attestation state. Screenshots
    come from a dated release-candidate capture run and include stable
    PowerShell UI, WPF preview, high-contrast, and common error/maintenance
    states where available.
  - Verify: docs-generation dry-run updates no files when metadata is current;
    a fixture representing `v3.7.2` without WPF assets produces historical
    release wording instead of generic "every release" claims; screenshot
    capture artifacts include viewport, OS theme, app version, and commit SHA.

## 🔬 Researcher Queue (Cycle 11 - 2026-06-04)

Cycle 11 inspects maintainability risks in the PowerShell and WPF code shape.
It does not implement refactors; it turns measured duplication and file size
into implementation-ready extraction and quality-gate work. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where release sequencing decisions are required.

- [ ] 🔬 🤖 P1 - Extract shared
  PowerShell core logic into a generated script-module source.
  - Why: `LibreSpot.ps1` is 5,769 lines, the embedded WPF backend is 2,318
    lines, and a function inventory on 2026-06-04 found 58 function names shared
    between them. Shared functions include config normalization, SpotX parameter
    building, downloads, path management, Spicetify runners, watcher state,
    archive/install modules, and safety helpers. Hand-copying those functions
    across two scripts makes every future SpotX flag, download hardening, safe
    extraction, config migration, and process contract change vulnerable to
    one-lane drift.
  - Evidence: local function inventory on 2026-06-04,
    `LibreSpot.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`,
    `CHANGELOG.md:148`,
    `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:7`,
    https://learn.microsoft.com/en-us/powershell/scripting/developer/module/how-to-write-a-powershell-script-module,
    https://learn.microsoft.com/en-us/powershell/scripting/developer/module/how-to-write-a-powershell-module-manifest
  - Touches: PowerShell source layout, backend embedding step, release build,
    regression tests, developer docs.
  - Acceptance: repo has a shared PowerShell source module or source-fragment
    layout that generates both `LibreSpot.ps1` and the embedded backend script.
    Shared functions have one source of truth; lane-specific wrappers own only
    UI, event protocol, elevation, and host dispatch. Generated outputs are
    deterministic and include a banner naming their source commit.
  - Verify: build step regenerates both scripts and fails on dirty generated
    output; parser tests run against generated scripts; function inventory
    proves shared functions are not manually duplicated; release workflow uses
    generated artifacts only after tests pass.

- [ ] 🔬 🤖 P2 - Add source
  shape metrics and maintainability budgets to CI.
  - Why: the codebase now has several large coordination files:
    `LibreSpot.ps1` at 5,769 lines, `MainViewModel.cs` at 2,202 lines,
    `LibreSpot.Backend.ps1` at 2,318 lines, and `AppCatalog.cs` at 372 lines.
    Some of that is deliberate because the project ships single-file
    PowerShell and a self-contained WPF shell, but growth should be visible
    before it becomes another late-release refactor. The Cycle 8 parity
    manifest can share the same inventory data.
  - Evidence: local line/byte measurement on 2026-06-04,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs`,
    https://learn.microsoft.com/en-us/powershell/utility-modules/psscriptanalyzer/overview,
    https://docs.github.com/en/actions/reference/workflow-commands-for-github-actions
  - Touches: CI workflow, test utilities, roadmap/research reports, developer
    docs.
  - Acceptance: CI emits a source-shape summary with line count, function
    count, public option count, backend action count, shared PowerShell function
    count, largest XAML dictionaries, and largest C# types. Budgets can start
    as warnings, but new files crossing agreed thresholds must include a split
    plan or explicit exception.
  - Verify: metrics are deterministic on Windows runners; docs-only changes do
    not trigger misleading code-shape regressions; a fixture or synthetic
    oversized file proves warning/fail thresholds work.

- [ ] 🔬 🤖 P2 - Split WPF view-model
  state domains before adding more v4 stable UI.
  - Why: `MainViewModel.cs` currently coordinates environment snapshots,
    install/custom option editing, settings search, maintenance commands,
    activity streaming, prompt state, cancellation, log display, and external
    folder launching. Upcoming roadmap items add diagnostics, undo actions,
    toasts, preset sharing, localization, accessibility tests, parity manifests,
    and shell integration. Without state-domain boundaries, each UI feature
    increases the risk of broad property-change churn and hard-to-test command
    interactions.
  - Evidence: `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:106`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1365`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1436`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1562`,
    `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs:1945`,
    https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/
  - Touches: WPF view models, command services, tests, UI automation harness,
    localization work.
  - Acceptance: define sub-view-model or service ownership for environment
    summary, option editor, maintenance actions, activity/log stream, prompts,
    settings search, and release/update status. `MainViewModel` becomes a
    coordinator rather than the owner of every state transition. Public
    properties stay stable for XAML or are migrated with focused tests.
  - Verify: existing 72 desktop tests still pass; new unit tests cover each
    state domain without launching the full window; UI automation snapshots
    prove data context and command bindings still populate all three main tabs,
    activity overlay, and prompt overlay.

## 🔬 Researcher Queue (Cycle 12 - 2026-06-04)

Cycle 12 inspects the stable PowerShell GUI lane separately from the WPF
preview. The public `releases/latest/download/LibreSpot.ps1` path still opens
the PowerShell-hosted XAML shell, so it needs a minimum quality and support
boundary even while v4 moves toward the native WPF shell. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where release-channel policy decisions are required.

- [ ] 🔬 🤖 P1 - Add a legacy
  PowerShell GUI accessibility and contrast gate.
  - Why: Cycle 7 covers the native WPF shell, but the current stable script UI
    is still a WPF/XAML surface embedded in `LibreSpot.ps1`. A 2026-06-04 scan
    found zero `AutomationProperties`, zero `SystemColors`, 44 tooltips, 44
    checkbox controls, six `BeginStoryboard` blocks, three `DoubleAnimation`
    blocks, and many hard-coded dark-theme hex colors. The v3.7 shell has focus
    trigger styling and useful microcopy, but it does not have the explicit
    screen-reader names, high-contrast mapping, or reduced-motion policy being
    planned for the native WPF shell.
  - Evidence: `LibreSpot.ps1:1119`, `LibreSpot.ps1:1249`,
    `LibreSpot.ps1:1341`, `LibreSpot.ps1:1542`,
    `LibreSpot.ps1:2011`, `LibreSpot.ps1:2052`,
    `LibreSpot.ps1:4309`,
    local `rg`/count scan of `LibreSpot.ps1` on 2026-06-04,
    https://learn.microsoft.com/en-us/accessibility-tools-docs/items/wpf/control_automationproperties,
    https://learn.microsoft.com/en-us/windows/apps/design/accessibility/high-contrast-themes,
    https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing
  - Touches: `LibreSpot.ps1` XAML resources, themed dialog XAML, manual QA
    checklist, README support notes.
  - Acceptance: before another stable script release, the PowerShell GUI has a
    minimum accessibility checklist covering keyboard traversal, visible focus,
    named icon-only/titlebar buttons, meaningful checkbox/combo names,
    high-contrast fallback, non-color-only destructive/warning states, and a
    reduced-motion/no-shimmer mode or documented exception. If maintainers
    choose not to backport these fixes, README and release notes explicitly mark
    the PowerShell shell as legacy and point accessibility-sensitive users to
    the native WPF track once stable.
  - Verify: manual keyboard and screen-reader smoke pass covers Easy, Custom,
    Maintenance, progress, and confirmation dialog; high-contrast screenshots
    prove text and focus remain visible; static scan fails if new unlabeled
    icon-only controls are added.

- [ ] 🔬 🤖 🔧 P1 - Define the
  stable script support and retirement boundary.
  - Why: README describes LibreSpot as a single-script PowerShell GUI, and the
    latest stable release is still `v3.7.2` with `LibreSpot.ps1` and
    `LibreSpot.exe` assets. At the same time, the roadmap makes v4 stable the
    native WPF shell. Without a channel policy, users will not know whether the
    PowerShell GUI continues receiving accessibility fixes, security fixes,
    dependency pin updates, watcher fixes, or only critical hotfixes after WPF
    stabilizes.
  - Evidence: `README.md:7`, `README.md:18`,
    `README.md:38`, `README.md:181`,
    `gh release view v3.7.2 --json assets,isPrerelease` on 2026-06-04,
    `gh release view v4.0.0-preview.1 --json assets,isPrerelease` on
    2026-06-04,
    https://docs.github.com/articles/about-releases
  - Touches: README channel table, release notes, roadmap v4 stable scope,
    support docs, self-update messaging.
  - Acceptance: repo documents whether the PowerShell GUI is active stable,
    maintenance-only LTS, or deprecated after WPF stable. The policy names what
    still lands in the script lane, how long critical fixes are backported, how
    users migrate saved `config.json`, whether PS2EXE continues shipping, and
    which release channel `/latest` should point at during and after the v4
    transition.
  - Verify: README and release notes show one stable recommendation; release
    workflow enforces the channel policy; self-update/check-update messaging
    does not suggest preview WPF builds as stable unless maintainers have made
    that decision.

## 🔬 Researcher Queue (Cycle 13 - 2026-06-04)

Cycle 13 inspects destructive-operation safety and backup parity beyond the
broader operation-journal item already in Cycle 3. Tags: 🔬 =
researcher-added this cycle; 🤖 = implementer-actionable now; 🔧 =
operator-needed where user-data or release-channel policy decisions are
required.

- [ ] 🔬 🤖 P0 - Backport the
  narrowed scheduled-task cleanup guard to the stable script.
  - Why: the WPF backend cleanup path already avoids deleting arbitrary
    scheduled tasks that merely contain "Spotify" in their task name; its
    comment explicitly calls out a user-authored `MySpotifyBackup` task as the
    risk. The stable `LibreSpot.ps1` full reset still uses
    `Where-Object { $_.TaskName -match 'Spotify' }`, so the public stable lane
    can remove unrelated user automation during a destructive reset while the
    backend lane is safer.
  - Evidence: `LibreSpot.ps1:5056`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1743`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1744`,
    `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:1746`,
    https://learn.microsoft.com/en-us/powershell/module/scheduledtasks/unregister-scheduledtask
  - Touches: `LibreSpot.ps1`, backend parity tests, destructive reset tests,
    support docs.
  - Acceptance: both script lanes remove only Spotify-owned scheduled tasks
    using an allow-list or an ownership predicate that includes task path,
    action executable/arguments, and known Spotify task names. User-authored
    tasks such as `MySpotifyBackup`, `SpotifyPlaylistExport`, or RMM tasks that
    mention Spotify are not removed.
  - Verify: PowerShell regression tests cover Spotify-owned task fixtures and
    user-authored false positives; full reset dry-run/journal from Cycle 3
    lists only owned tasks; manual review confirms no broad `TaskName -match
    'Spotify'` cleanup remains.

- [ ] 🔬 🤖 P1 - Bring backup and
  restore controls to the native WPF maintenance surface before v4 stable.
  - Why: the stable PowerShell GUI exposes "Create configuration backup" and
    "Restore the newest backup" controls, wires enablement/tooltips, and calls
    backup/restore handlers. The native WPF maintenance catalog currently lists
    Check Updates, Reapply, Restore Vanilla, Uninstall Spicetify, and Full
    Reset, but no explicit backup or restore action. Since Spicetify's documented
    workflow includes backup/apply/restore commands, v4 parity should include
    first-class backup and restore affordances rather than making users fall
    back to the legacy shell for recovery work.
  - Evidence: `LibreSpot.ps1:2011`, `LibreSpot.ps1:2012`,
    `LibreSpot.ps1:3991`, `LibreSpot.ps1:4135`,
    `LibreSpot.ps1:4156`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs:275`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs:277`,
    `src/LibreSpot.Desktop/Models/AppCatalog.cs:281`,
    https://spicetify.app/docs/cli/commands
  - Touches: WPF maintenance catalog, backend action registry, backend script,
    UI prompts, tests, support docs.
  - Acceptance: WPF Maintenance has explicit backup and restore actions with
    enablement based on Spicetify install state and existing backup count.
    Backup names the destination and does not modify Spotify; restore previews
    the selected/latest backup, restores through the backend, and reports
    whether a follow-up apply/reapply happened.
  - Verify: tests cover backup disabled/missing/available states, restore with
    no backup, restore success, restore failure, and parity with the stable
    script's backup location. UI automation snapshots include backup/restore
    controls and destructive reset remains separately grouped.
