# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

Added 2026-09-04 from RESEARCH.md. IDs continue the RD scheme; RD-180 was the last used.

- [ ] P1: RD-182: Hold the auto-reapply watcher after repeated failures on one Spotify build
  Why: after "Reapply failed" the watcher keeps `LastKnownVersion` so it retries on the next tick, every 30 minutes, forever, with no failure count, no hold state, and nothing in Maintenance naming the build or the step that failed. A Spotify build the tuple does not support turns into an endless stop-and-apply loop plus a growing watcher log.
  Evidence: `LibreSpot.ps1:1025-1030` ("Keep LastKnownVersion unchanged so we'll retry next tick"); `Get-WatcherState` at `LibreSpot.ps1:538` stores only a `LastOutcome` string; SpiceManager's "compatibility hold mode" (https://github.com/EliasOnsihuay/SpiceManager); Spicetify v3's `update_policy` that remembers a block (https://github.com/spicetify/cli/tree/v3-beta); https://reddit.com/r/spicetify/comments/1vw95y7/.
  Touches: `Invoke-AutoReapplyWatcher`, `Get-WatcherState`, `Set-WatcherState`, `Update-AutoReapplyStatusLabel` (`LibreSpot.ps1:7254`), the desktop health component that reads watcher state, `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`, `tests/powershell/LibreSpot.Tests.ps1`.
  Acceptance: WHEN reapply fails three consecutive times for the same Spotify version, the watcher SHALL record a hold with that version, the failing step and the timestamp and SHALL skip further attempts for that version; Maintenance SHALL show the hold with a manual Reapply action; a new Spotify version or a successful manual reapply SHALL clear it; Pester SHALL cover the count, the hold, the skip and both clears without Task Scheduler.
  Complexity: M

- [ ] P2: RD-184: Document all fifteen fleet exit codes in the README and gate the table against the schema
  Why: README's endpoint table lists eight codes; `schemas/fleet-exit-codes.json` defines fifteen, and the seven missing ones (30, 40, 50, 60, 1618, 3010, 1641) are exactly the retry and reboot classes an Intune detection or remediation script must treat differently from failure.
  Evidence: `README.md:383-392`; `schemas/fleet-exit-codes.json` codes 0, 1, 2, 10, 11, 12, 13, 20, 30, 40, 50, 60, 1618, 3010, 1641; https://learn.microsoft.com/en-us/intune/intune-service/apps/apps-win32-add.
  Touches: `README.md` fleet section, `tests/LibreSpot.Desktop.Tests/FleetSchemaTests.cs` or `ReadmeContract` test.
  Acceptance: WHEN the schema defines a code, the README table SHALL have a row with the same meaning and `intuneBehavior`, and a test SHALL fail when any schema code has no README row or a row's behaviour word differs from the schema.
  Complexity: S

- [ ] P2: RD-183: Advance the SpotX pin to a reviewed post-`9d344658` commit for the manifest-aware mirror and download fallbacks, holding Spotify at 1.2.93
  Why: users in regions where Cloudflare classifies SpotX's worker or `raw.githubusercontent.com` is blocked cannot install at all, and the pinned `550bc72c` predates the fixes: `-mirror` now also rewrites the LoaderSpot manifest URL, `-download_method curl|webclient` adds a second transport with a 429 fallback, and the full-build `-v 1.2.93.667.g7b5cc0ce` form skips the manifest fetch. `patches.json` changes since the pin only cap entries at 1.2.93 or 1.2.94 or add `fr >= 1.2.94` rules, so patch behaviour on 1.2.93 is unchanged.
  Evidence: https://github.com/SpotX-Official/SpotX/issues/891; https://github.com/SpotX-Official/SpotX/issues/836 (403 report on 2026-09-04 while the same URL served HTTP 206 from another region); https://github.com/SpotX-Official/SpotX/compare/550bc72c...main; `src/powershell/shared/Module-InstallSpotX.ps1:40-60` already retries through `-mirror`; `src/powershell/shared/Test-SpotXPinAdvanceSecurityPolicy.ps1`.
  Touches: `src/powershell/data/PinnedReleases.ps1`, `src/powershell/shared/Build-SpotXParams.ps1`, `src/powershell/shared/Get-SpotXDownloadRetryPlan.ps1`, `Test-SpotXPinAdvanceSecurityPolicy.ps1`, `schemas/compatibility-baseline.json`, `docs/how-spotx-and-spicetify-alter-spotify.md`, Pester and Desktop pin tests, both composed hosts.
  Acceptance: `Build-Scripts.ps1 -SpotXSecurityPolicy` SHALL pass for the candidate commit with the `-defender_exclusions_off` boundary intact; the retry plan SHALL use the manifest-aware mirror on the second attempt and the full-build `-v` form on a manifest fetch failure; a live install against Spotify 1.2.93.667 SHALL complete with `Get-SpotXPatchVerification` green; the pinned run.ps1 hash, baseline and docs SHALL move together.
  Complexity: L

- [ ] P2: RD-185: Show the pinned Spotify build's embedded Chromium version and age in the compatibility card and the trust disclosure
  Why: pinning Spotify at 1.2.93 keeps every user on `libcef.dll` 146.0.10 (Chromium 146.0.7680.179) while Chrome 152.0.7977.82 (2026-09-04) fixes a V8 bug with an exploit in the wild, and CEF does not backport. Nothing in the health model, the compatibility card or the README says which browser engine a pinned build carries, so the trade-off the pin makes is invisible.
  Evidence: `%APPDATA%\Spotify\libcef.dll` product version read 2026-09-04 on the installed 1.2.93.667; https://thehackernews.com/2026/09/google-releases-chrome-update-to-patch.html (CVE-2026-85046); `grep -ri libcef src/` matches nothing; `README.md` "Trust & risk disclosure".
  Touches: `src/LibreSpot.Core/EnvironmentSnapshotService.cs` (read `libcef.dll` FileVersion beside `Spotify.exe`), `src/LibreSpot.Core/AppCatalog.cs` compatibility card, `schemas/compatibility-baseline.json` (record the pinned build's Chromium major and its release date), all six `Strings*.resx`, `src/LibreSpot.Core/SupportBundleService.cs`, `README.md` trust section.
  Acceptance: WHEN Spotify is installed, the snapshot SHALL record the `libcef.dll` product version and the compatibility card SHALL show "Chromium 146, pinned build" with the baseline's release date; the support bundle SHALL include it; the README trust disclosure SHALL state that the pinned build carries an older browser engine than current Chrome and that the tuple advance is how it moves; a test SHALL fail when the baseline's Chromium major disagrees with the pinned installer's known value.
  Complexity: M

- [ ] P2: RD-186: Give Settings one scrollbar by letting the theme gallery grow to its content
  Why: the theme gallery is capped at 340 pixels with its own vertical scrollbar inside the page scroll, the shipped screenshot shows the Prism card cut through its scheme list, and README says the page has a single scrollbar. Two nested scroll regions were flagged in the 2026-08-21 research and survived the Settings recomposition.
  Evidence: `src/LibreSpot.Desktop/Views/CustomAppearanceSection.xaml:64-67` (`MaxHeight="340"`, `VerticalScrollBarVisibility="Auto"`); `assets/screenshots/wpf-custom.png`; `README.md:141`; eight fixed-width `TextBlock`s without `TextTrimming` at `MaintenanceWorkspaceView.xaml:240,524,603`, `RecommendedWorkspaceView.xaml:370`, `CustomWorkspaceView.xaml:54`, `MainWindow.xaml:2012,3398,3716`.
  Touches: `CustomAppearanceSection.xaml`, the `WpfQaMatrixTests` row for the Settings state, `assets/screenshots/wpf-custom.png`, the five fixed-width `TextBlock` sites in live views.
  Acceptance: WHEN Settings opens, the theme gallery SHALL render every card without an inner scrollbar and the page SHALL have one scrollbar; an offscreen capture at the 1080x720 minimum window SHALL reach the last card through page scroll alone; the fixed-width `TextBlock`s in live views SHALL either trim with a tooltip or wrap; the README screenshot SHALL be recaptured.
  Complexity: M

- [ ] P2: RD-187: Pin the PS2EXE module version in the release build and record it in the manifest
  Why: `-CompileStableExe` imports `ps2exe` with no version, so the bytes of the `LibreSpot.exe` release asset depend on whichever module the build machine holds; 1.0.18 (2026-06-07) changed emitted behaviour by adding `$ScriptRoot`, and 1.0.16 and 1.0.17 changed host and embedding options. The reproducible-build work in RD-146 stops at the .NET assets.
  Evidence: `Build-Scripts.ps1:1971` (`Import-Module ps2exe -ErrorAction Stop`); https://www.powershellgallery.com/packages/ps2exe; https://github.com/MScholtes/PS2EXE; `schemas/release-artifact-contract.json`.
  Touches: `Build-Scripts.ps1` (`Invoke-LibreSpotStableExeCompile`), `schemas/release-artifact-contract.json`, the release manifest generator, `README.md` local release procedure, `tests/LibreSpot.Desktop.Tests/ReleaseArtifactContractTests.cs`.
  Acceptance: the compile step SHALL import `ps2exe -RequiredVersion <pinned>` and fail with a message naming the install command when it is absent; the release manifest SHALL record the compiler version; a test SHALL fail when the pinned version in the script and the contract disagree.
  Complexity: S

- [ ] P2: RD-188: Validate production outputs against the four schemas no test reads
  Why: `asset-cache-bundle.json`, `ndjson-log-format.json`, `operation-token-types.json` and `run-receipt-format.json` are user-data and fleet contracts; the last two are embedded into Core and consumed by the undo path, and none has a test that loads the schema file and validates a real output against it.
  Evidence: no test file under `tests/` references those four filenames; `src/LibreSpot.Core/LibreSpot.Core.csproj:30-31` embeds two of them; `src/powershell/shared/Complete-OperationJournalRun.ps1:55-64` writes the receipt; `README.md:337` names the bundle contract.
  Touches: `tests/LibreSpot.Core.Tests/` (new schema-validation tests), `tests/powershell/AssetCacheBundle.Tests.ps1`. No JSON-schema library is in any lockfile; validate the way `tests/LibreSpot.Desktop.Tests/FleetSchemaTests.cs` and `ProfileSchemaTests.cs` already do (read the schema with `System.Text.Json`, check required properties, types and enums by hand) rather than adding a dependency.
  Acceptance: WHEN production code writes a receipt, an NDJSON log line, an undo token and a bundle manifest, a test SHALL validate each against its schema file; planting an extra required field in the schema or removing a field from the writer SHALL turn the test red.
  Complexity: M

- [ ] P2: RD-189: Compute the next reviewable Spotify build from SpotX, the classmaps and Spicetify's declared range in the drift check
  Why: three upstream sources bound the next pin (SpotX `patches.json` reaches 1.2.99, the newest classmap is 1020097 by inheritance from 1020096, Spicetify 2.44.0 declares 1.2.96) and public Spotify is 1.2.98.301 with 1.2.99 staged, but `-CheckSpotifyVersionDrift` reports drift without computing the highest build all three cover, so every tuple decision starts from headlines.
  Evidence: https://raw.githubusercontent.com/SpotX-Official/SpotX/main/run.ps1; https://github.com/spicetify/classmaps/commits/main; `schemas/spicetify-supported-versions-v2.json` `cli_declared_windows_range`; https://github.com/spicetify/spicetify-themes/issues/1290 (1.2.98 breaks Text); https://spotify.en.uptodown.com/windows/versions.
  Touches: `Build-Scripts.ps1` (`-CheckSpotifyVersionDrift`), `src/LibreSpot.Core/UpstreamDriftService.cs`, `schemas/compatibility-baseline.json`, `%LOCALAPPDATA%\LibreSpot\upstream-drift-cache.json` shape, Pester.
  Acceptance: the drift report SHALL name the highest Spotify build covered by all three sources with the URL each bound came from, SHALL warn when public Spotify exceeds it, and SHALL say "no reviewable build above the pin" when the bound equals the pin; Pester SHALL cover a fixture where the classmap bound is lowest.
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

- [ ] P3: RD-197: Gate the pnpm build-script allowlist so install scripts cannot come back
  Why: the 2026-08-04 CHAINDROP wave compromised `keyv`, `flat-cache` and `file-entry-cache` with `preinstall` payloads; LibreSpot's lockfile pins older versions and pnpm 11's allowlist names only esbuild and Parcel's watcher, but no test asserts the allowlist stays exact, so one edit re-enables every dependency's install scripts.
  Evidence: https://www.elastic.co/security-labs/shai-hulud-chaindrop-npm-supply-chain; `src/LibreSpot.App/pnpm-workspace.yaml:4-6` (`allowBuilds` lists only `@parcel/watcher` and `esbuild`); `CLAUDE.md` "pnpm's dependency build allowlist now names only esbuild and Parcel's file watcher".
  Touches: `tests/powershell/DependencyHealth.Tests.ps1` or a vitest under `src/LibreSpot.App/tests`, `Build-Scripts.ps1 -DependencyHealth`.
  Acceptance: a test SHALL read the allowlist and fail when it contains anything beyond `esbuild` and `@parcel/watcher`; `-DependencyHealth` SHALL fail when any package under `node_modules` declares a `preinstall` script outside the allowlist.
  Complexity: S

- [ ] P1: RD-198: Rebuild the live-engine archive and re-pin it once the in-flight release lands
  Why: RD-181 changed `src/LibreSpot.App/src/core/store.ts`, `src/panels/health.ts`, `src/spicetify-globals.d.ts` and `src/extensions/librespot-engine.ts`, but `resources/custom-apps/librespot-engine.zip` was not rebuilt because a parallel session holds an uncommitted rebuilt archive and its three SHA256 pins for the v4.5.0 release. Nothing gates archive-against-source drift (the tests only check archive against pins), so the shipped custom app will not carry the quarantine recovery until the archive is rebuilt.
  Evidence: `git status` on 2026-09-04 showed `resources/custom-apps/librespot-engine.zip`, `src/powershell/data/CommunityCustomApps.ps1`, `LibreSpot.ps1` and `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1` modified by another session; the Desktop non-WPF suite passed 1218 tests with the app source changed and the archive stale; `CLAUDE.md` "Rebuilding the live-engine ZIP changes its installer pin".
  Touches: `resources/custom-apps/librespot-engine.zip`, `src/powershell/data/CommunityCustomApps.ps1`, `LibreSpot.ps1`, `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1`, `schemas/parity-manifest.json`.
  Acceptance: WHEN the archive is rebuilt from current app source, its SHA256 SHALL match all three installer pins and `BundledLibreSpotArchive_MatchesEveryPinAndShipsItsPackageVersion` SHALL pass; a test SHALL fail when the archive's embedded engine bundle does not contain the current `QUARANTINE_POINTER_KEY` string, so source-to-archive drift is caught rather than assumed.
  Complexity: S
