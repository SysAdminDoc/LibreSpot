# LibreSpot Roadmap

Active roadmap for forward-looking work only. Completed release work lives in
[COMPLETED.md](COMPLETED.md), and research synthesis lives in
[RESEARCH_REPORT.md](RESEARCH_REPORT.md). The full April 2026 research archive is
kept at [docs/archive/research/RESEARCH.md](docs/archive/research/RESEARCH.md).

Last consolidated: 2026-06-01.

## Current State

- Stable script release: v3.7.2.
- Native WPF shell line: v4.0.0-preview.6.
- Release pipeline now builds PS2EXE and WPF artifacts with checksums, SBOMs, and
  build provenance attestations.
- Auto-reapply watcher, self-update checks, pre-patched Spotify detection,
  Spotify version selection, and the v3.7 UI refresh have shipped.
- Point-in-time dependency pins from the latest repo docs:
  - SpotX `0abf98a3` for Spotify `1.2.86.502`
  - Spicetify CLI v2.43.1
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
