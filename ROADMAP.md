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
