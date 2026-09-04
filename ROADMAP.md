# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

Added 2026-09-04 from RESEARCH.md. IDs continue the RD scheme; RD-180 was the last used.

- [ ] P2: RD-183: Advance the SpotX pin to a reviewed post-`9d344658` commit for the manifest-aware mirror and download fallbacks, holding Spotify at 1.2.93
  Why: users in regions where Cloudflare classifies SpotX's worker or `raw.githubusercontent.com` is blocked cannot install at all, and the pinned `550bc72c` predates the fixes: `-mirror` now also rewrites the LoaderSpot manifest URL, `-download_method curl|webclient` adds a second transport with a 429 fallback, and the full-build `-v 1.2.93.667.g7b5cc0ce` form skips the manifest fetch. `patches.json` changes since the pin only cap entries at 1.2.93 or 1.2.94 or add `fr >= 1.2.94` rules, so patch behaviour on 1.2.93 is unchanged.
  Evidence: https://github.com/SpotX-Official/SpotX/issues/891; https://github.com/SpotX-Official/SpotX/issues/836 (403 report on 2026-09-04 while the same URL served HTTP 206 from another region); https://github.com/SpotX-Official/SpotX/compare/550bc72c...main; `src/powershell/shared/Module-InstallSpotX.ps1:40-60` already retries through `-mirror`; `src/powershell/shared/Test-SpotXPinAdvanceSecurityPolicy.ps1`.
  Touches: `src/powershell/data/PinnedReleases.ps1`, `src/powershell/shared/Build-SpotXParams.ps1`, `src/powershell/shared/Get-SpotXDownloadRetryPlan.ps1`, `Test-SpotXPinAdvanceSecurityPolicy.ps1`, `schemas/compatibility-baseline.json`, `docs/how-spotx-and-spicetify-alter-spotify.md`, Pester and Desktop pin tests, both composed hosts.
  Acceptance: `Build-Scripts.ps1 -SpotXSecurityPolicy` SHALL pass for the candidate commit with the `-defender_exclusions_off` boundary intact; the retry plan SHALL use the manifest-aware mirror on the second attempt and the full-build `-v` form on a manifest fetch failure; a live install against Spotify 1.2.93.667 SHALL complete with `Get-SpotXPatchVerification` green; the pinned run.ps1 hash, baseline and docs SHALL move together.
  Complexity: L

- [ ] P2: RD-186: Give Settings one scrollbar by letting the theme gallery grow to its content
  Why: the theme gallery is capped at 340 pixels with its own vertical scrollbar inside the page scroll, the shipped screenshot shows the Prism card cut through its scheme list, and README says the page has a single scrollbar. Two nested scroll regions were flagged in the 2026-08-21 research and survived the Settings recomposition.
  Evidence: `src/LibreSpot.Desktop/Views/CustomAppearanceSection.xaml:64-67` (`MaxHeight="340"`, `VerticalScrollBarVisibility="Auto"`); `assets/screenshots/wpf-custom.png`; `README.md:141`; eight fixed-width `TextBlock`s without `TextTrimming` at `MaintenanceWorkspaceView.xaml:240,524,603`, `RecommendedWorkspaceView.xaml:370`, `CustomWorkspaceView.xaml:54`, `MainWindow.xaml:2012,3398,3716`.
  Touches: `CustomAppearanceSection.xaml`, the `WpfQaMatrixTests` row for the Settings state, `assets/screenshots/wpf-custom.png`, the five fixed-width `TextBlock` sites in live views.
  Acceptance: WHEN Settings opens, the theme gallery SHALL render every card without an inner scrollbar and the page SHALL have one scrollbar; an offscreen capture at the 1080x720 minimum window SHALL reach the last card through page scroll alone; the fixed-width `TextBlock`s in live views SHALL either trim with a tooltip or wrap; the README screenshot SHALL be recaptured.
  Complexity: M

- [ ] P3: RD-196: Reset Marketplace storage from the engine's Health panel after exporting a Marketplace backup
  Why: Marketplace 1.0.11 migrated keys but stale IndexedDB and localStorage from older installs still restore themes after a full Spicetify reinstall and can block uninstalling a theme; upstream closed the report as not planned. LibreSpot already names the `spicetify-marketplace` database and exports Marketplace's own backup JSON.
  Evidence: https://github.com/spicetify/marketplace/issues/1231 (opened 2026-09-02, closed not planned); https://github.com/spicetify/marketplace/releases/tag/v1.0.11; `src/LibreSpot.App/src/core/backup.ts:11`; `src/LibreSpot.App/src/extensions/librespot-engine.ts:522`; `CLAUDE.md` note on the Marketplace `settings` store's in-line key.
  Touches: `src/LibreSpot.App/src/panels/health.ts`, `src/LibreSpot.App/src/core/backup.ts`, `src/LibreSpot.App/tests/backup.test.ts`; the live proof uses the hidden-CDP recipe recorded in `CLAUDE.md` (2026-09-03, `--remote-debugging-port=9223 --minimized` with every window hidden).
  Acceptance: the action SHALL export the Marketplace backup JSON first and refuse to continue if the export fails, SHALL delete the `spicetify-marketplace` IndexedDB database and Marketplace's localStorage keys, SHALL reload, and a hidden-CDP run SHALL prove Marketplace comes back empty and re-imports the backup; every IndexedDB call SHALL be wrapped so a failure rejects instead of hanging.
  Complexity: M

- [ ] P1: RD-198: Rebuild the live-engine archive and re-pin it once the in-flight release lands
  Why: RD-181 changed `src/LibreSpot.App/src/core/store.ts`, `src/panels/health.ts`, `src/spicetify-globals.d.ts` and `src/extensions/librespot-engine.ts`, but `resources/custom-apps/librespot-engine.zip` was not rebuilt because a parallel session holds an uncommitted rebuilt archive and its three SHA256 pins for the v4.5.0 release. Nothing gates archive-against-source drift (the tests only check archive against pins), so the shipped custom app will not carry the quarantine recovery until the archive is rebuilt.
  Evidence: `git status` on 2026-09-04 showed `resources/custom-apps/librespot-engine.zip`, `src/powershell/data/CommunityCustomApps.ps1`, `LibreSpot.ps1` and `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1` modified by another session; the Desktop non-WPF suite passed 1218 tests with the app source changed and the archive stale; `CLAUDE.md` "Rebuilding the live-engine ZIP changes its installer pin".
  Touches: `resources/custom-apps/librespot-engine.zip`, `src/powershell/data/CommunityCustomApps.ps1`, `LibreSpot.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`, `schemas/parity-manifest.json`.
  Acceptance: WHEN the archive is rebuilt from current app source, its SHA256 SHALL match all three installer pins and `BundledLibreSpotArchive_MatchesEveryPinAndShipsItsPackageVersion` SHALL pass; a test SHALL fail when the archive's embedded engine bundle does not contain the current `QUARANTINE_POINTER_KEY` string, so source-to-archive drift is caught rather than assumed.
  Complexity: S
