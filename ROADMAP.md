# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

- [ ] Publish winget manifests for portable assets.

- [ ] Add Velopack packaging for the WPF shell.

- [ ] Create a Scoop bucket with `checkver` and `autoupdate`.

- [ ] Submit Chocolatey only after signing and checksum automation have settled.

- [ ] Whether the April 2026 SpotX and Spicetify pin guidance is still current.

- [ ] Spotify Connect regression test harness.

- [ ] Spicetify v3 readiness and migration risk.

- [ ] P3 — RD-36: Decompose `MainWindow.xaml` into per-screen UserControls
  Why: `MainWindow.xaml` is 5,509 lines holding all six nav screens (Home/Setup/Unblock/Tools/Settings/About) + the inspector in one file, slowing edits and raising merge/regression risk on the shipping shell.
  Evidence: `src/LibreSpot.Desktop/MainWindow.xaml`.
  Touches: `MainWindow.xaml`, new `Views/*.xaml` UserControls, `Themes/Controls.xaml`, FlaUI smoke + rendered-QA tests (AutomationIds/x:Names must be preserved).
  Acceptance: each nav screen becomes a UserControl under `Views/`; `MainWindow` composes them; every `AutomationId`/`x:Name` referenced by tests is preserved byte-for-byte; the rendered-WPF QA capture and FlaUI suite pass unchanged across dark/high-contrast and English/Spanish.
  Complexity: L

- [ ] P3 — RD-37: Add German and French WPF locales
  Why: the localization framework, runtime language selector, and strict validation gate (`tools/Sync-Localization.ps1`) already support five locales (en/es/pt-BR/ru/zh-Hans); de/fr are large Spotify-modding audiences and low-risk given the gate.
  Evidence: `src/LibreSpot.Desktop/Properties/Strings.*.resx` (five locales, no de/fr); `tools/Sync-Localization.ps1`.
  Touches: new `Strings.de.resx` / `Strings.fr.resx`, language-selector list, localization validation allowlist, health-component/scrollbar automation-name coverage.
  Acceptance: de and fr resource sets pass `Sync-Localization` (placeholder parity, no English carry-over, protected product/file tokens, no truncation); the language selector lists both; hidden long-text prompt rendering covers them.
  Complexity: L
