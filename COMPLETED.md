# LibreSpot Completed Work

Consolidated completion log for roadmap items that have shipped. The detailed
release history remains in [CHANGELOG.md](CHANGELOG.md).

Last consolidated: 2026-06-01.

## Current Delivered Baseline

- Stable script release: v3.7.2.
- Native WPF shell line: v4.0.0-preview.6.
- The PowerShell script and WPF backend share the same SpotX/Spicetify install
  behavior, configuration normalization, and watcher state.
- Release workflow builds PS2EXE and WPF artifacts, emits checksums, emits a
  CycloneDX SBOM, and records build provenance attestations.

## Completed Releases

| Release | Completed work |
|---|---|
| v3.3.1 | Fixed the `-newFullscreenMode` SpotX flag wiring, verified the SpotX parameter block, softened the historical `-SpotifyPath` gotcha, and confirmed the theme pin was still current at that time. |
| v3.4.0 | Added SpotX flags for version reporting privacy, Spotify auto-start, devtools, mirror selection, recommended uninstall flow, and download method selection. |
| v4.0.0-preview.4 | Added Mica support, taskbar progress mirroring, Serilog crash logs/dumps, accessibility names/live region behavior, and the release CI workflow with checksums, SBOM, and attestations. |
| v3.5.0 / v4.0.0-preview.5 | Added self-update checks, pre-patched Spotify detection, Spotify version dropdown, and the `-Clean` one-shot rebuild flag. |
| v3.5.1 | Hardened the release pipeline, added PowerShell regression tests, fixed false-positive foreign patch detection, moved self-update checks off the UI thread, and fixed semantic version comparison. |
| v3.6.0 / v4.0.0-preview.6 | Shipped the auto-reapply watcher through UI, CLI, scheduled task XML, headless `-Watch`, saved config replay, hash verification, logging, and regression tests. |
| v3.7.0 | Reworked the PowerShell GUI into a more polished Win11-style shell with sidebar navigation, design tokens, Lucide icons, hover motion, Mica integration, and a shimmering progress bar. |
| v3.7.1 | Completed the density pass and switched brand rendering to `logo.png` where available. |
| v3.7.2 | Fixed the Easy-mode confirmation dialog resource-scope crash and documented the standalone-XAML gotcha. |

## Completed Roadmap Tracks

### Critical Fixes

- Corrected `-new_fullscreen_mode` to SpotX's real `-newFullscreenMode` flag in
  the PowerShell script and WPF backend.
- Re-verified `-SpotifyPath` support against SpotX and updated the repo notes.
- Confirmed the spicetify-themes pin did not need a bump at that checkpoint.
- Rechecked all `Build-SpotXParams` emissions against SpotX `run.ps1`.

### SpotX Flag Expansion

The following flags are now surfaced in the installer or backend config:

- `-sendversion_off`
- `-start_spoti`
- `-devtools`
- `-mirror`
- `-confirm_spoti_recomended_uninstall`
- `-download_method`

The remaining intentionally deferred SpotX surfaces are tracked in
[ROADMAP.md](ROADMAP.md): `-version` evolution, `-CustomPatchesPath`,
`-language`, and niche Goofy/error-log flags.

### Competitor-Parity Items

- Spotify version dropdown.
- Auto-reapply scheduled watcher.
- Self-update check with a local 24-hour cache.
- Pre-patched Spotify warning for injector-style files.
- `-Clean` script flag.
- Local crash reporting and release-provenance hardening.

### WPF Shell And Release Trust

- Mica backdrop fallback handling.
- Taskbar progress state/value mirroring.
- Serilog logs and crash dumps.
- Accessibility pass for icon buttons and activity badge updates.
- GitHub Actions release workflow for stable/pre-release artifacts.
