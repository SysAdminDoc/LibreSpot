# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P2: RD-153: Cover the desktop executable in the Smart App Control and SmartScreen guidance
  Why: the README FAQ entry says "Smart App Control blocks the script from running" and answers for the script, but the desktop executable is now the first install path and Smart App Control blocks unsigned executables outright with no per-app bypass; SmartScreen reputation restarts from zero for each unsigned release.
  Evidence: `README.md:476-480`; `SECURITY.md`; https://support.microsoft.com/en-us/windows/security/threat-malware-protection/smart-app-control-frequently-asked-questions; https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation.
  Touches: `README.md` FAQ and Trust sections, `SECURITY.md`, `src/LibreSpot.Desktop/Properties/Strings.resx` and the five satellites if the in-app application-control copy names only the script, `tests/LibreSpot.Desktop.Tests/ReleaseTruthTests.cs`.
  Acceptance: WHEN a reader reaches the Smart App Control entry, it SHALL state that `LibreSpot-Desktop.exe`, `LibreSpot.Cli.exe`, `LibreSpot.exe`, and the script are all blocked while Smart App Control is on, that no per-app bypass exists, and that the supported answer is a device where it is off or in evaluation mode; the SmartScreen entry SHALL explain that the "Unknown publisher" warning recurs for every release; the release-truth test SHALL fail if the FAQ names only the script.
  Complexity: S

- [ ] P3: RD-154: Correct the release-notice rate-limit assumption and prove one request per day
  Why: the code comment says an unchanged release "costs no rate limit" because of `If-None-Match`, but GitHub only exempts conditional 304s on authenticated requests; the anonymous budget is 60 per hour and the 24-hour cache is the real protection.
  Evidence: `src/LibreSpot.Core/ReleaseNoticeService.cs:50,345`; https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api; https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api.
  Touches: `src/LibreSpot.Core/ReleaseNoticeService.cs`, `tests/LibreSpot.Desktop.Tests/ReleaseNoticeServiceTests.cs`.
  Acceptance: WHEN the comment is read, it SHALL state the anonymous budget and that the cache, not the ETag, bounds requests; a test SHALL prove that two `GetNoticeAsync` calls inside the cache lifetime make exactly one HTTP request even when the server would answer 304, and that a 403 or 429 extends the cache before retrying.
  Complexity: S

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

- [ ] P3: RD-157: Name the star-farmed decoy repositories pattern in the download verification section
  Why: two repositories with nonsense READMEs and off-GitHub download links ("spicetify-elite", "spotify-adblock-studio") outrank LibreSpot in stars and appear in the same searches; the verification section already teaches hash and attestation checks and can add one sentence on what a decoy looks like.
  Evidence: https://github.com/NeedChandlerMonitor/spicetify-elite; https://github.com/SecretBarber/spotify-adblock-studio; `README.md` "Check the file" section.
  Touches: `README.md` verification and FAQ sections, `SECURITY.md`.
  Acceptance: WHEN a reader reaches the verification section, it SHALL state that LibreSpot is only published at `github.com/SysAdminDoc/LibreSpot/releases`, that any page offering it elsewhere or asking for a "template" or "activation" download is not LibreSpot, and how to check the release attestation; the release-truth test SHALL assert the canonical URL sentence exists.
  Complexity: S

- [ ] P3: RD-158: Audit the reviewed extensions and custom apps for Spotify Web API client IDs under the 2026 developer-access rules
  Why: since 2026-02-06 Spotify's Development Mode requires Premium and caps each client ID at five authorised users, so any bundled or catalog extension that calls the Web API with its own client ID will fail for the sixth user; extensions that use the client's internal Platform APIs are unaffected.
  Evidence: https://developer.spotify.com/blog/2026-02-06-update-on-developer-access-and-platform-security; https://developer.spotify.com/blog/2026-07-23-web-api-quota-updates; `schemas/community-assets.json`; `src/powershell/data/CommunityCustomApps.ps1`.
  Touches: `schemas/community-assets.json`, `tools/Build-CommunityCatalog.ps1`, `README.md` catalog copy.
  Acceptance: WHEN the audit completes, every catalog asset SHALL carry a recorded `webApiUse` value (`none`, `platform-api`, `client-id`) with the file and line that proves it; any `client-id` asset SHALL show a plain-language note in the catalog page and the Extensions panel, or be moved to `degraded`.
  Complexity: S

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

- [ ] P1: RD-161: Reconcile the Spicetify Windows ceiling with what upstream actually declares
  Why: `SpicetifyWindowsMaxTestedSpotify` is `1.2.93` and is presented to users as Spicetify CLI's max-tested version, but the pinned v2.44.0 release declares Windows and Microsoft Store support through Spotify `1.2.96`. The constant drives `CompatibilityVerdict`, so anyone on Spotify 1.2.94, 1.2.95, or 1.2.96 is told their client is Unsupported for a reason that is not true, and the health warning names Spicetify as the source of a limit that is LibreSpot's own.
  Evidence: `src/LibreSpot.Core/AppCatalog.cs:835`, `:1179-1185`; `src/LibreSpot.Core/CompatibilityVerdict.cs:88-106`; `src/powershell/shared/Get-LibreSpotCompatibilityWarnings.ps1:8`; `src/powershell/shared/Write-LibreSpotCompatibilityMatrix.ps1:12`; `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs:953,973`; https://github.com/spicetify/cli/releases/tag/v2.44.0 (Compatibility: Windows/Microsoft Store `1.2.14` -> `1.2.96`).
  Touches: `src/LibreSpot.Core/AppCatalog.cs`, `src/LibreSpot.Core/CompatibilityVerdict.cs`, `src/powershell/data/PinnedReleases.ps1`, `src/powershell/shared/Get-LibreSpotCompatibilityWarnings.ps1`, `src/powershell/shared/Write-LibreSpotCompatibilityMatrix.ps1`, both composed hosts, `schemas/compatibility-baseline.json`, `schemas/spicetify-supported-versions-v2.json`, six resx files if the copy is localized, `tests/LibreSpot.Desktop.Tests/PowerShellRegressionTests.cs`, `README.md`.
  Acceptance: WHEN a user runs a Spotify build between the pinned target and Spicetify's declared ceiling, the compatibility verdict SHALL separate the two limits: Spicetify's declared range (`1.2.14` to `1.2.96`, sourced from the pinned release) and the build LibreSpot has verified (`1.2.93`), and no message SHALL attribute LibreSpot's own ceiling to Spicetify's testing; a verdict above the verified build but inside the declared range SHALL read Degraded with a next step rather than Unsupported; a test SHALL assert both values come from separate fields and that the declared ceiling matches `schemas/spicetify-supported-versions-v2.json`.
  Complexity: M

- [ ] P1: RD-162: Report a failed community asset install as a failed step, not a successful run
  Why: when a community theme cannot be installed, `Module-InstallThemes` logs a warning, returns, and the run continues to report success. A user who picks Catppuccin gets no theme and is told the run completed. That is how three of the five reviewed themes stayed broken in the catalog without anyone noticing: the archive downloaded, its SHA256 verified, and only the copy step failed. The same swallow exists for custom apps in `Module-InstallCustomApps`.
  Evidence: `src/powershell/shared/Module-InstallThemes.ps1:55-57` (`catch { Write-Log ... -Level 'WARN'; return }`); `src/powershell/shared/Module-InstallCustomApps.ps1` (`Could not install custom app ... Skipping.`); live run `work/rd145-theme-Catppuccin.log` on 2026-09-03 ended `Maintenance action 'Reapply' completed successfully` after the theme failed to install. The bundled-theme branch added on 2026-09-03 is a third site with the same shape, and it matters more there because a bundled theme has no download to fall back to: a Prism copy that fails its pin check leaves the user with no theme and a successful run.
  Touches: `src/powershell/shared/Module-InstallThemes.ps1`, `src/powershell/shared/Module-InstallCustomApps.ps1`, `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`, both composed hosts, `src/LibreSpot.Core/BackendScriptService.cs` or the result parsing that decides success, six resx files for the user-facing wording, `tests/powershell/LibreSpot.Tests.ps1`.
  Acceptance: WHEN a selected theme, extension, or custom app fails to install while the rest of the run succeeds, the run SHALL finish with a state that names the asset and says it was not installed, the desktop and CLI SHALL surface that as a warning rather than a plain success, and the fleet CLI SHALL return a distinct non-zero exit code documented in `schemas/fleet-exit-codes.json`; a Pester test SHALL fail if a forced theme-copy failure still produces a success result.
  Complexity: M

- [ ] P3: RD-167: Keep decorative icon glyphs out of the automation tree
  Why: the Axe.Windows scan added in RD-152 reports `NameExcludesPrivateUnicodeCharacters` ten times across the three scanned states, every one of them a `TextBlock` whose text is a private-use character from the symbol font. A screen reader meets an unnamed text element with no useful content beside the control it decorates. The owning control carries the real name, so the fix is to hide the glyph rather than name it.
  Evidence: `schemas/axe-windows-baseline.json` records 7 on `recommended`, 1 on `custom`, 2 on `maintenance`, observed 2026-09-03; each element reports `ClassName=TextBlock`, `ControlType=Text(50020)`, `Name=` (empty), `FrameworkId=WPF`.
  Touches: the XAML that renders icon glyphs (`src/LibreSpot.Desktop/Views/*.xaml`, `src/LibreSpot.Desktop/MainWindow.xaml`), `schemas/axe-windows-baseline.json`.
  Acceptance: WHEN the Axe.Windows scan runs on the three scanned states, no `NameExcludesPrivateUnicodeCharacters` violation SHALL be reported, the corresponding baseline entries SHALL be deleted rather than reduced, and the glyphs SHALL still render unchanged in the captured screenshots.
  Complexity: S

- [ ] P3: RD-168: Give the Home workspace list an accessible name
  Why: the Axe.Windows scan reports `NameNotNull` on a `List` control in the Home workspace. The items inside it are named and reachable, but a screen reader announces the container itself as an unnamed list, so there is nothing to say what the list holds when a user lands on it.
  Evidence: `schemas/axe-windows-baseline.json`, `recommended` state, key `NameNotNull|List(50008)|(none)`, count 1, observed 2026-09-03.
  Touches: the Home workspace list in `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`, six resx files for the name, `schemas/axe-windows-baseline.json`.
  Acceptance: WHEN the Axe.Windows scan runs on the recommended state, no `NameNotNull` violation SHALL be reported, its baseline entry SHALL be deleted, and the name SHALL come from the localized resources rather than a literal so every shipped culture announces it.
  Complexity: S
