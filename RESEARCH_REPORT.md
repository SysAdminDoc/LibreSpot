# LibreSpot Research Report

Research summary for planning. The full April 2026 research corpus is archived
at [docs/archive/research/RESEARCH.md](docs/archive/research/RESEARCH.md).

Last consolidated: 2026-06-01.

## Executive Summary

LibreSpot is positioned as an actively maintained Windows GUI that combines
SpotX patching and Spicetify customization. The archived research found no
direct competitor with the same combination of GUI depth, theme/extension
selection, auto-reapply support, config backup/restore, and release hardening.

The strongest near-term opportunities are not basic install flow work. They are
trust, distribution, diagnostics, and admin deployment:

- Signing and package-manager distribution remove the largest adoption friction.
- Status/repair tooling addresses the most common user failure mode: Spotify or
  Spicetify updating underneath a patched install.
- Fleet deployment would differentiate LibreSpot from consumer-only tools.
- Catalog reconciliation prevents drift between README claims, installer data,
  and the research backlog.

## Ecosystem Findings

| Area | Point-in-time finding | Planning implication |
|---|---|---|
| SpotX | Active single-maintainer project with frequent compatibility work. | Keep pins explicit, hash-verified, and easy to refresh. |
| Spicetify CLI | Large ecosystem but fragile after Spotify client updates. | Auto-reapply and diagnostics are core product features, not polish. |
| Competing GUI tools | Most direct GUI competitors were dead, tiny, or CLI-only. | LibreSpot can own the GUI niche if distribution and trust improve. |
| Themes/extensions | High-value community themes and extensions change over time. | Treat catalog refresh as an ongoing maintenance loop. |
| Mobile modding | Higher enforcement and DMCA risk than desktop customization. | Keep LibreSpot focused on desktop Windows unless a separate strategy is chosen. |
| Spotify updates | Updates can break SpotX, Spicetify CSS maps, or both. | Preserve version pinning, update blocking, watcher logs, and known-bad warnings. |

## High-Value Backlog From Research

- Complete code signing and package-manager release channels.
- Add `COMPARISON.md` for public positioning against SpotX, Spicetify Manager,
  BlockTheSpot-style installers, and universal CLI scripts.
- Refresh the theme and extension catalog against current upstream state before
  shipping more catalog work.
- Add status-at-a-glance and repair flows before adding more advanced features.
- Add account-safety and terms-of-service language in a factual, non-alarming
  way.
- Decide whether the LibreSpot name should change before distribution expands.
- Monitor Spicetify v3 planning because it could change extension/theme/module
  assumptions.
- Maintain a contingency plan if SpotX availability or distribution changes.

## Risk Ledger

| Risk | Why it matters | Mitigation |
|---|---|---|
| SmartScreen and unsigned binaries | Casual users may abandon the install before first run. | Finish SignPath and signed release automation. |
| Spotify client churn | Patches and CSS maps can break after upstream updates. | Keep update blocking, version picker, auto-reapply, and known-bad version warnings. |
| Single-maintainer upstreams | SpotX turnaround time can vary after Spotify changes. | Pin known-good versions and expose fallback/diagnostic states clearly. |
| Catalog drift | README, installer data, and roadmap can disagree. | Reconcile catalog docs against source data before each feature release. |
| Naming collision | `librespot` is also a Rust Spotify Connect library. | Make the rebrand decision before package-manager submissions. |
| Legal/DMCA pressure | Ad-blocking and modding projects have a higher takedown profile. | Avoid redistributing upstream code, document sources, and maintain Spicetify-only fallback options. |

## Source Notes

The archived research includes detailed source lists for SpotX, Spicetify,
themes, extensions, competitor projects, and legal/trend references. Those
claims are point-in-time April 2026 observations and should be refreshed before
being used as current upstream facts.
