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

- [ ] P3: RD-190: Correct the README numbers that drifted and add them to the lane-truth gate
  Why: "16 extensions" against ten built-in plus five community, "45 tests" for the Spotify surface against 62, a `gh release verify v4.1.2` example two releases old, "eight assets" beside "six contract-covered artifacts", and What's New sections for v4.1.0 and v4.2.0 that were never tagged.
  Evidence: `README.md:304`, `:157`, `:616`, `:594`, `:675`, `:107-160`; `LibreSpot.ps1:1435` and `:1452`; `git tag` shows v4.1.2, v4.3.0, v4.4.0 only after v4.0.0; 62 `it(` blocks under `src/LibreSpot.App/tests`.
  Touches: `README.md`, `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs` (the RD-144 lane-truth gate).
  Acceptance: each count SHALL match its source; the lane-truth test SHALL read the extension count from the data files and the app test count from `src/LibreSpot.App/tests`; the verify example SHALL use the current tag; untagged What's New sections SHALL say which tagged release carried them.
  Complexity: S

- [ ] P3: RD-191: Report a failed run-receipt write and a failed config rollback instead of silencing them
  Why: a run that mutated the machine can leave the undo surface empty because the receipt write failure is only WARN-logged; a config rollback that fails moves the rescue file back with `-ErrorAction SilentlyContinue` and throws the original error, leaving a `.rescue` file and no `config.json` with no message naming it; the undo service's own failure-path journal and receipt writes are each wrapped in an empty catch.
  Evidence: `src/powershell/shared/Complete-OperationJournalRun.ps1:63-66`; `src/powershell/shared/Install-LibreSpotStagedConfig.ps1:23`; `src/LibreSpot.Core/OperationJournalUndoService.cs:345-346` and `:454-455`.
  Touches: those three files, the backend result payload (`Write-EventLine` WARN path), `src/LibreSpot.Desktop/ViewModels/MainViewModel.BackendMessages.cs`, Pester and `OperationJournalUndoServiceTests`.
  Acceptance: WHEN the receipt cannot be written after a mutating run, the run SHALL finish as a warning that names the receipt path and says undo is unavailable for it; WHEN a rollback fails, the error SHALL name the rescue file; WHEN the undo failure-path writes fail, the result message SHALL say the journal was not updated; tests SHALL plant an unwritable path for each.
  Complexity: M

- [ ] P3: RD-192: Format diagnostic timestamps with the invariant culture and UTC
  Why: three sites format dates with no culture, so a non-Gregorian default calendar (th-TH, ar-SA) writes a non-ISO date into health output and support bundles, and crash filenames use local time so they collide across a DST fall-back hour and misorder against the UTC journal.
  Evidence: `src/LibreSpot.Core/AppCatalog.cs:693`; `src/LibreSpot.Core/EnvironmentSnapshotService.cs:2504`; `src/LibreSpot.Desktop/Services/CrashReporter.cs:171`.
  Touches: those three files, the existing crash-reporter tests in `tests/LibreSpot.Desktop.Tests` (grep `CrashReporter` to find them), a culture-scoped test in the Localization collection.
  Acceptance: under th-TH and ar-SA the three outputs SHALL be Gregorian ISO strings; crash filenames SHALL use UTC with a trailing `Z`; tests SHALL run under those cultures inside the serialized Localization collection.
  Complexity: S

- [ ] P3: RD-193: Fail the offscreen UIA scan on invokable elements smaller than 24 by 24 device-independent pixels
  Why: WCAG2ICT (2025-12-11) applies success criterion 2.5.8 Target Size Minimum to desktop software and Axe.Windows 2.4.2 has no rule for it, so the current scan cannot catch a shrunken button or a tiny disclosure chevron.
  Evidence: https://www.w3.org/TR/wcag2ict-22/; https://github.com/microsoft/axe-windows/blob/main/docs/RulesDescription.md (rule set unchanged since 2.4.2); `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs` `Walk`.
  Touches: `WpfUiAutomationSmokeTests.cs`, `tests/LibreSpot.Desktop.Tests/AxeScanShape.cs`, `schemas/keyboard-focus-contract.json` or a new baseline file for intentional exceptions.
  Acceptance: the walk SHALL record every element exposing Invoke, Toggle, SelectionItem or ExpandCollapse whose bounding rectangle is under 24 by 24 DIPs and fail with the automation id; a smoke state with a planted 20 by 20 button SHALL fail; the baseline SHALL start empty.
  Complexity: M

- [ ] P3: RD-194: Untrack `design-qa.md` or move it under `docs/`, and gate the root document set
  Why: the file is tracked while `.gitignore:34` ignores it, and it sits outside the root markdown set `AGENTS.md` allows, so edits show in `git status` while new files of the same kind would not, and the repository's own hygiene rule is contradicted by its tree.
  Evidence: `git ls-files -v design-qa.md` prints `H design-qa.md`; `.gitignore:34`; `AGENTS.md` root-level markdown policy; `tests/LibreSpot.Desktop.Tests/RepositoryIntakeContractTests.cs`.
  Touches: `design-qa.md`, `.gitignore`, `RepositoryIntakeContractTests.cs`.
  Acceptance: the root SHALL contain only the documents `AGENTS.md` lists (plus `LICENSE`), a test SHALL fail when a tracked root markdown file is outside that set or is both tracked and ignored, and the design QA record SHALL live under `docs/` if it is kept.
  Complexity: S

- [ ] P3: RD-195: Update the Smart App Control FAQ for the KB5079391 toggle
  Why: since 2026-03-27 Windows 11 24H2 and 25H2 can turn Smart App Control off without a clean install, which changes the practical answer for a user who wants to run an unsigned executable; the FAQ still describes "a device where Smart App Control is off or still in evaluation mode" as the only path.
  Evidence: `README.md:553-556`; https://www.bleepingcomputer.com/news/microsoft/windows-11-kb5079391-update-rolls-out-smart-app-control-improvements/; https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions.
  Touches: `README.md` FAQ, the localized in-app Smart App Control copy if it repeats the sentence.
  Acceptance: the FAQ SHALL state that Windows 11 24H2 and 25H2 with KB5079391 or later can turn Smart App Control off from Settings without reinstalling, SHALL keep the "LibreSpot does not recommend a bypass" stance, and SHALL still say the file's SHA256 must match `checksums.txt` first.
  Complexity: S

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

- [ ] P2: RD-199: Let the standalone script host clear a watcher hold after a manual reapply
  Why: the WPF backend clears the hold from `Update-ApplyState`, but the standalone `LibreSpot.ps1` host has no equivalent: its manual apply runs in the worker runspace, which resolves only the names in `$functionNamesForWorker`, and neither `Set-WatcherState` nor the hold helpers are in that set. Calling them from `Module-ApplySpicetify` would be a `CommandNotFoundException` on a live install path, the exact class of failure RD-177 fixed. The hold message in that host now says what actually clears it there, so nothing is misleading, but a user who reapplies by hand still waits for the next Spotify update.
  Evidence: `grep -rn "function Update-ApplyState"` matches only `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`; `LibreSpot.ps1` writes watcher state only inside `Invoke-AutoReapplyWatcher`; `tests/powershell/LibreSpot.Tests.ps1` "only promises a manual clear in the host that can do one" pins the current split; `CLAUDE.md` 2026-09-03 note on the worker runspace.
  Touches: `LibreSpot.ps1` `$functionNamesForWorker`, `src/powershell/gui/lane-functions.ps1`, the hold message, `tests/powershell/LibreSpot.Tests.ps1`.
  Acceptance: WHEN a manual apply succeeds in the standalone host, the hold SHALL clear; the worker-runspace closure test SHALL stay green with an empty baseline, proving every function the new path calls is exported; and the host-promise test SHALL be updated to require the same message in both hosts.
  Complexity: M

- [ ] P3: RD-200: Give the Spotify engine evidence one format resource instead of two joined strings
  Why: the compatibility evidence is assembled in code as `L("HealthEvidenceSpotifyDetected") + " " + F("HealthEvidenceSpotifyEngineFormat", engine)`. A translator cannot reorder the two sentences and cannot remove the space, which is wrong for zh-Hans where sentences are not space separated. The engine version also reaches no machine-readable snapshot field, so nothing can compare what the machine actually runs against `embeddedChromiumMajor` in the baseline.
  Evidence: `src/LibreSpot.Core/EnvironmentSnapshotService.cs` `BuildSpotifyComponent`; `schemas/compatibility-baseline.json` `spotify.embeddedChromiumMajor`; raised by an adversarial review on 2026-09-04.
  Touches: `src/LibreSpot.Core/EnvironmentSnapshotService.cs`, all six `Strings*.resx`, the snapshot model, `tests/LibreSpot.Desktop.Tests/EnvironmentSnapshotServiceTests.cs`.
  Acceptance: the evidence SHALL come from one format resource carrying both sentences; the snapshot SHALL expose the engine version as its own field; and a test SHALL fail when the field's major differs from the baseline's recorded major on a machine where Spotify is installed.
  Complexity: S
