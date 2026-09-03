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

- [ ] P2: RD-173: Fix the flaky `Settings_SearchOpensOnlyTheGroupsThatHoldAMatch`
  Why: this test fails intermittently in a full `--filter-not-class "*Wpf*"` run and never in isolation, so the
  suite cannot tell a real regression from noise. It was seen twice on 2026-09-03, once as an unidentified
  `failed: 1` and once by name, with clean runs either side both times. Passing 9/9 three times as a class while
  failing inside the full run points at cross-test interference or a shared static, not at the assertion.
  Evidence: `failed: 1` of 1154 on 2026-09-03 with the name lost to the console, then
  `failed LibreSpot.Desktop.Tests.SettingsDisclosureTests.Settings_SearchOpensOnlyTheGroupsThatHoldAMatch (175ms)`
  in a later full run of 1160; `--filter-class "*SettingsDisclosureTests*"` passed 9/9 three consecutive times
  immediately after, and two further full runs passed 1159/1159.
  Touches: `tests/LibreSpot.Desktop.Tests/SettingsDisclosureTests.cs`, whatever state it shares with the tests that
  run beside it, and `tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj` if a result file is needed to
  catch the next occurrence.
  Acceptance: WHEN the non-WPF suite runs repeatedly, this test SHALL pass every time, and the cause SHALL be named
  in the fix rather than worked around with a retry, a sleep, or a collection attribute that only serialises it;
  a machine-readable result file SHALL be written so any future failure is identifiable after the fact.
  Complexity: M

- [ ] P2: RD-175: Guard `DecorativeSymbolIcon` the way the other custom control is guarded
  Why: RD-167 is a per-usage rename in XAML with nothing but a slow app-launching scan behind it. A new
  `ui:SymbolIcon` added anywhere, or a deleted `GetChildrenCore`, leaves all 1159 non-WPF tests green. Worse, an
  icon added to a state that is not one of the three scanned (`activity-undo`, `support-bundle`, `profile` and
  `prompt` are all real smoke states) is caught by nothing at all. `LiveRegionContentControl` has both halves of
  the pattern this control is missing: a source lint that pins its peer and an in-process STA peer test.
  Evidence: `AutomationNameContractTests.LiveRegionContentControl_KeepsPoliteLiveRegionPeer` and
  `WpfUiAutomationSmokeTests.LiveRegionContentControl_AutomationPeerReportsPolite` as the model; raised by an
  adversarial review on 2026-09-03.
  Touches: `tests/LibreSpot.Desktop.Tests/AutomationNameContractTests.cs`,
  `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`.
  Acceptance: WHEN any XAML under `src/LibreSpot.Desktop` uses `ui:SymbolIcon` directly, a source test SHALL fail
  and name the file; WHEN `DecorativeSymbolIcon` creates its peer, an in-process test SHALL assert that peer
  reports no children and is neither a control nor a content element, without launching the app.
  Complexity: S

- [ ] P3: RD-176: Assert the rate-limit message carries the status code it was given
  Why: `TryGetLatestStableAsync` puts its message into `ReleaseNotice.Reason`, which reaches the user, but the new
  mapping tests assert only `Status`. Replacing `RateLimited($"HTTP {(int)response.StatusCode} ...")` with a
  hardcoded `RateLimited("HTTP 429 ...")` passes every test in the repo while reporting a 403 to the user as a 429.
  The same seam would also cover 304, 200 and malformed JSON, none of which touch the real client today.
  Evidence: `src/LibreSpot.Core/ReleaseNoticeService.cs` rate-limit and missing branches;
  `tests/LibreSpot.Desktop.Tests/ReleaseNoticeServiceTests.cs` `TryGetLatestStableAsync_*` theories, which use
  `StubResponse` but assert status only; `GetNoticeAsync_AConditionalRequestSendsTheCachedETag` still asserts the
  header against `FakeClient`, so the real `If-None-Match` write and `ETag` read are untested. Raised by an
  adversarial review on 2026-09-03.
  Touches: `tests/LibreSpot.Desktop.Tests/ReleaseNoticeServiceTests.cs`.
  Acceptance: WHEN the client maps a refused response, a test SHALL assert the returned message names the status
  code it actually received, failing if the code is hardcoded; the existing stub handler SHALL also cover 304, a
  successful body and a malformed body through the real client.
  Complexity: S
