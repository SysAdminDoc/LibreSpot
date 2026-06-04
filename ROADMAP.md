# LibreSpot Roadmap

Active roadmap for forward-looking work only. Completed release work lives in
[COMPLETED.md](COMPLETED.md), and research synthesis lives in
[RESEARCH_REPORT.md](RESEARCH_REPORT.md). The full April 2026 research archive is
kept at [docs/archive/research/RESEARCH.md](docs/archive/research/RESEARCH.md).

Last consolidated: 2026-06-01.
Last researched: 2026-06-04, Cycle 1.

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
