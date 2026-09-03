# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P3: RD-155: Show the asset digest and the verify command beside the Update LibreSpot link
  Why: the latest-release payload already carries per-asset SHA256 digests, and the README teaches `gh release verify-asset`; the Home notice could show the desktop asset's digest and the one command so a user verifies before running, matching the release-trust story.
  Evidence: https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/; https://cli.github.com/manual/gh_attestation_verify; `src/LibreSpot.Core/ReleaseNoticeService.cs`; `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml` update-notice row.
  Touches: `src/LibreSpot.Core/ReleaseNoticeService.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`, `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`, six resx files, `tests/LibreSpot.Desktop.Tests/MainViewModelReleaseNoticeTests.cs`, QA matrix row `home-update`.
  Acceptance: WHEN a newer stable release is cached, the notice SHALL expose the `LibreSpot-Desktop.exe` digest and a copyable `gh release verify-asset` line through a disclosure, with automation names and all five locales; WHEN the payload has no digest, nothing extra SHALL render; the `home-update` capture SHALL stay within its window.
  Complexity: S

- [ ] P3: RD-156: Offer an opt-in local minidump toggle for crash diagnosis
  Why: crash reporting is log-based; the runtime can write a Triage minidump (paths and secrets stripped) with three environment variables and no elevation, which gives a support bundle real crash evidence without any telemetry.
  Evidence: `src/LibreSpot.Desktop/Services/CrashReporter.cs`; https://learn.microsoft.com/en-us/dotnet/core/diagnostics/collect-dumps-crash; `schemas/data-inventory.json`.
  Touches: `src/LibreSpot.Desktop/Services/CrashReporter.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`, `src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`, `src/LibreSpot.Core/SupportBundleService.cs`, `schemas/data-inventory.json`, six resx files.
  Acceptance: WHEN the user turns the toggle on under Maintenance diagnostics, the next launch SHALL set `DOTNET_DbgEnableMiniDump=1`, `DOTNET_DbgMiniDumpType=3`, and a `%e-%p-%t` name under the crashes folder; the support bundle SHALL include the newest dump only when the toggle is on and SHALL list it in the redaction report; the data inventory SHALL document the path and retention (keep 2).
  Complexity: M

- [ ] P3: RD-159: Add a safe-mode launch that starts Spotify once with LibreSpot-managed extensions and apps disabled
  Why: when an extension breaks Spotify's startup, the only recovery paths are Repair (reinstalls the same set) or Restore vanilla (removes everything); BetterDiscord's most-requested recovery feature is a crash screen that lets users disable addons, and a one-session safe mode gives the same result without a UI inside a broken client.
  Evidence: https://github.com/BetterDiscord/BetterDiscord/issues/1920; https://github.com/BetterDiscord/BetterDiscord/issues/2237; `src/powershell/shared/Get-SpicetifyApplyPlan.ps1`; `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`.
  Touches: `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`, `src/powershell/backend/lane-functions.ps1`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`, `src/LibreSpot.Desktop/Views/MaintenanceWorkspaceView.xaml`, `src/LibreSpot.Cli/Program.cs` (`repair --safe-mode`), `schemas/fleet-cli-contract.json`, six resx files.
  Acceptance: WHEN the user chooses Start Spotify in safe mode, the system SHALL snapshot `config-xpui.ini` and `CustomApps`, apply with extensions and custom apps disabled, launch Spotify, and offer one action that restores the snapshot and re-applies; the operation journal SHALL record it as reversible; a Pester test SHALL prove the config round trip and the CLI verb SHALL report it in JSON.
  Complexity: M

- [ ] P3: RD-160: Export and import a verified asset-cache bundle for offline and mirrored fleet installs
  Why: SpotX's download chain is blocked by Cloudflare "Suspected Phishing" flags, some ISPs, and Spotify's token-gated installer links, and Chocolatey and Scoop users expect an internalized package that installs with no network; LibreSpot already keeps a verified asset cache with source, hash, and size metadata.
  Evidence: https://github.com/SpotX-Official/SpotX/issues/836; https://github.com/SpotX-Official/SpotX/issues/844; https://github.com/SpotX-Official/SpotX/issues/829; https://docs.chocolatey.org/en-us/features/package-internalizer/; `README.md` "Asset-cache inventory"; `src/powershell/shared/*AssetCache*.ps1`.
  Touches: `src/LibreSpot.Cli/Program.cs` (`cache export`, `cache import`), `schemas/fleet-cli-contract.json`, `src/powershell/shared` asset-cache functions, both composed hosts, `README.md` Managed section.
  Acceptance: WHEN `cache export --output <zip>` runs on a machine with a complete cache, it SHALL write every verified entry with its metadata and a manifest; WHEN `cache import <zip>` runs on a machine with no network, a following `install --answer-file` SHALL complete for every LibreSpot-fetched asset without a download, and Spotify itself SHALL be reported as the one asset that still needs SpotX's chain; hashes SHALL be re-verified on import.
  Complexity: M

- [ ] P1: RD-162: Report a failed community asset install as a failed step, not a successful run
  Why: when a community theme cannot be installed, `Module-InstallThemes` logs a warning, returns, and the run continues to report success. A user who picks Catppuccin gets no theme and is told the run completed. That is how three of the five reviewed themes stayed broken in the catalog without anyone noticing: the archive downloaded, its SHA256 verified, and only the copy step failed. The same swallow exists for custom apps in `Module-InstallCustomApps`.
  Evidence: `src/powershell/shared/Module-InstallThemes.ps1:55-57` (`catch { Write-Log ... -Level 'WARN'; return }`); `src/powershell/shared/Module-InstallCustomApps.ps1` (`Could not install custom app ... Skipping.`); live run `work/rd145-theme-Catppuccin.log` on 2026-09-03 ended `Maintenance action 'Reapply' completed successfully` after the theme failed to install. The bundled-theme branch added on 2026-09-03 is a third site with the same shape, and it matters more there because a bundled theme has no download to fall back to: a Prism copy that fails its pin check leaves the user with no theme and a successful run.
  Touches: `src/powershell/shared/Module-InstallThemes.ps1`, `src/powershell/shared/Module-InstallCustomApps.ps1`, `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`, both composed hosts, `src/LibreSpot.Core/BackendScriptService.cs` or the result parsing that decides success, six resx files for the user-facing wording, `tests/powershell/LibreSpot.Tests.ps1`.
  Acceptance: WHEN a selected theme, extension, or custom app fails to install while the rest of the run succeeds, the run SHALL finish with a state that names the asset and says it was not installed, the desktop and CLI SHALL surface that as a warning rather than a plain success, and the fleet CLI SHALL return a distinct non-zero exit code documented in `schemas/fleet-exit-codes.json`; a Pester test SHALL fail if a forced theme-copy failure still produces a success result.
  Complexity: M

- [ ] P3: RD-168: Give the Home workspace list an accessible name
  Why: the Axe.Windows scan reports `NameNotNull` on a `List` control in the Home workspace. The items inside it are named and reachable, but a screen reader announces the container itself as an unnamed list, so there is nothing to say what the list holds when a user lands on it.
  Evidence: `schemas/axe-windows-baseline.json`, `recommended` state, key `NameNotNull|List(50008)|(none)`, count 1, observed 2026-09-03.
  Touches: the Home workspace list in `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`, six resx files for the name, `schemas/axe-windows-baseline.json`.
  Acceptance: WHEN the Axe.Windows scan runs on the recommended state, no `NameNotNull` violation SHALL be reported, its baseline entry SHALL be deleted, and the name SHALL come from the localized resources rather than a literal so every shipped culture announces it.
  Complexity: S

- [ ] P2: RD-169: Decide whether a remote loader belongs in Recommended Setup at all
  Why: the RD-158 audit established that `beautiful-lyrics.mjs` is a 4.6 KB loader whose entire body is fetched from a third-party host at load time, and it is an easy-mode default, so Recommended Setup installs code that no pin covers on every machine. The eligibility rule now recognises `remote-loader` and requires disclosure, which is the honest description of what ships, but it is not an answer to whether it should ship by default. The same question applies to any future asset in that category. This is a product and risk decision, not a defect, and it is deliberately not being taken silently inside an audit commit.
  Evidence: `schemas/community-assets.json` (`beautiful-lyrics.mjs`, `networkBehavior: remote-loader`, `easyModeDefault: true`); the pinned file fetches `https://extensions.socalifornian.live/version/beautiful-lyrics` at line 154 and dynamically imports `https://extensions-storage.socalifornian.live/beautiful-lyrics@<version>.mjs` at line 91, both verified against the pinned commit on 2026-09-03; `src/LibreSpot.Cli/Program.cs:59` and `src/LibreSpot.Core/AppCatalog.cs:1068` carry the easy-mode extension set; `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1:192` and the monolith carry the pin.
  Touches: `schemas/community-assets.json`, `src/LibreSpot.Cli/Program.cs`, `src/LibreSpot.Core/AppCatalog.cs`, both composed hosts, `README.md` extension copy, `tests/LibreSpot.Desktop.Tests/CommunityAssetsManifestTests.cs`.
  Acceptance: WHEN the decision is recorded, either `beautiful-lyrics.mjs` SHALL stop being an easy-mode default and be removed from every hard-coded recommended set so the catalog and the installed set agree, or the catalog SHALL carry a written rationale for shipping unpinned third-party code by default; a test SHALL fail if the easy-mode set in `Program.cs` and the `easyModeDefault` flags in the manifest ever disagree, whichever way the decision goes.
  Complexity: M

- [ ] P3: RD-173: Identify the non-WPF suite failure that appeared once and did not come back
  Why: one run of `--filter-not-class "*Wpf*"` on 2026-09-03 reported `failed: 1` out of 1154 while the four runs
  around it reported `failed: 0`. The failing test's name was not captured before the output scrolled, so there is
  nothing to reproduce from. A suite that can fail one test in five runs cannot tell a real regression from noise,
  and the next person to see it will have the same nothing to work with.
  Evidence: four consecutive clean runs of the same command in the same working tree on 2026-09-03 (1153 passed,
  1 skipped) either side of a single `failed: 1`; the run happened immediately after two git worktrees were removed
  from under `%TEMP%`, which is the only external event in the window.
  Touches: `tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj` (a TRX or diagnostic logger so a failure
  survives the console), whichever test the log then names.
  Acceptance: WHEN the non-WPF suite runs, a machine-readable result file SHALL be written so a failure can be
  identified after the fact; the suite SHALL then be run enough times to either name the flaky test and fix its
  root cause, or record that it did not recur across the runs that were made.
  Complexity: S
