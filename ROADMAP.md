# LibreSpot Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Research-Driven Additions

- [ ] P3: RD-155: Show the asset digest and the verify command beside the Update LibreSpot link
  Why: the latest-release payload already carries per-asset SHA256 digests, and the README teaches `gh release verify-asset`; the Home notice could show the desktop asset's digest and the one command so a user verifies before running, matching the release-trust story.
  Evidence: https://github.blog/changelog/2025-06-03-releases-now-expose-digests-for-release-assets/; https://cli.github.com/manual/gh_attestation_verify; `src/LibreSpot.Core/ReleaseNoticeService.cs`; `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml` update-notice row.
  Touches: `src/LibreSpot.Core/ReleaseNoticeService.cs`, `src/LibreSpot.Desktop/ViewModels/MainViewModel.cs`, `src/LibreSpot.Desktop/Views/RecommendedWorkspaceView.xaml`, six resx files, `tests/LibreSpot.Desktop.Tests/MainViewModelReleaseNoticeTests.cs`, QA matrix row `home-update`.
  Acceptance: WHEN a newer stable release is cached, the notice SHALL expose the `LibreSpot-Desktop.exe` digest and a copyable `gh release verify-asset` line through a disclosure, with automation names and all five locales; WHEN the payload has no digest, nothing extra SHALL render; the `home-update` capture SHALL stay within its window.
  Complexity: S

- [ ] P1: RD-177: Close the fourteen calls the worker runspace cannot resolve
  Why: the install and maintenance runspaces are built from `$functionNamesForWorker` alone, and fourteen functions
  reachable from exported ones are missing from it. Each is a `CommandNotFoundException` waiting on a live path:
  `Module-ApplySpicetify` calls `Get-SpicetifyApplyPlan`, `Module-InstallCustomApps` calls
  `New-LibreSpotEngineBootstrap` (the easy-mode custom app), `Module-InstallMarketplace` calls both Marketplace
  fallback installers, and `Reapply-SavedSpicetifySetup` calls `Invoke-WithSpicetifyStatePreservation`. Nothing
  catches this class at compose, at lint, or in any test that dot-sources the shared file, which is how RD-162
  shipped a version that turned every healthy install into a FATAL run.
  Evidence: `tests/powershell/LibreSpot.Tests.ps1` `Worker runspace function closure` records all fourteen pairs as
  a baseline and fails on a fifteenth; observed 2026-09-03. Each pair is listed there.
  Touches: `LibreSpot.ps1` `$functionNamesForWorker`, and the baseline list in that test as pairs are resolved.
  Acceptance: WHEN each recorded pair is resolved, the callee SHALL either be exported to the worker or shown by a
  test to be unreachable from it, the pair SHALL be deleted from the baseline rather than left in place, and the
  closure test SHALL pass with an empty baseline once the list is drained.
  Complexity: M

- [ ] P3: RD-178: Make the icon sweep survive a renamed prefix or an Icon attribute
  Why: `NoXamlUsesAnIconThatWouldPutItsGlyphBackInTheAutomationTree` matches the literal string `ui:SymbolIcon`.
  A file that binds the Wpf.Ui namespace to a different prefix evades it, and so does `<ui:Button Icon="Home24"/>`,
  which `IconElementConverter.ConvertFrom` turns into a plain `SymbolIcon` at runtime with no matching source text.
  Neither form is used today, so this is latent. It also walks `bin` and `obj`, and would flag `ui:SymbolIconSource`
  as a false positive.
  Evidence: `tests/LibreSpot.Desktop.Tests/AutomationNameContractTests.cs`; `Wpf.Ui.Controls.IconElementConverter`
  4.3.0 decompiled 2026-09-03. Raised by an adversarial review.
  Touches: `tests/LibreSpot.Desktop.Tests/AutomationNameContractTests.cs`.
  Acceptance: WHEN a XAML file binds the Wpf.Ui namespace under any prefix and uses `SymbolIcon`, or sets an `Icon`
  attribute that resolves to one, the sweep SHALL name it; the sweep SHALL read only tracked source and SHALL NOT
  match a type whose name merely starts with `SymbolIcon`.
  Complexity: S

- [ ] P3: RD-179: Let the UIA tree walk fail without taking the settle loop with it
  Why: `Walk` calls `GetFirstChild` and `GetNextSibling` unguarded, and the settle loop now walks the window on
  every scan. A shell that dies mid-settle throws `ElementNotAvailableException` out of `ScanUntilSettled` instead
  of falling through to the caller's deliberate "scanned no windows, so this proved nothing" assertion, which is
  the message that actually explains what happened.
  Evidence: `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs` `Walk` and `ScanUntilSettled`; only
  `SnapshotByProcessId` is guarded. Raised by an adversarial review on 2026-09-03.
  Touches: `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`.
  Acceptance: WHEN the shell disappears during a settle pass, the scan SHALL return what it has rather than
  throwing, and the caller's window-count assertion SHALL be the failure the test reports.
  Complexity: S

- [ ] P3: RD-180: Reconcile the exit-13 category with what the code does with it
  Why: `schemas/fleet-exit-codes.json` files `AssetsNotInstalled` as `"category": "failure"` with
  `"intuneBehavior": "failure"`, while the desktop and CLI both treat it as a completed run that carries a warning.
  The taxonomy already has `success`, `blocked`, `retry` and `reboot` to choose from. The README endpoint table
  also still stops at 12.
  Evidence: `schemas/fleet-exit-codes.json`; `src/LibreSpot.Core/BackendScriptService.cs` maps 13 to a successful
  `BackendRunResult`; `README.md` return-code table. Raised by an adversarial review on 2026-09-03.
  Touches: `schemas/fleet-exit-codes.json`, `README.md`.
  Acceptance: WHEN an admin reads the taxonomy, the category and Intune behaviour recorded for 13 SHALL match how
  the desktop and CLI actually treat it, and the README table SHALL list it.
  Complexity: S
