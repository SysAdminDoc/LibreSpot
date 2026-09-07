# Research: LibreSpot

Date: 2026-09-07. Replaces all prior research.

## Executive Summary

LibreSpot is a Windows Spotify customization manager with a WPF desktop, fleet CLI, PowerShell host, and an in-client Store and live engine. The strongest direction is dependable recovery around its curated, pinned integrations. Source v4.5.0 is prepared; the immutable public release is v4.4.0. The previous compatibility, update-status and WPF accessibility findings have been addressed. This plan instead concentrates on reproduced failures in profile persistence and support-export privacy, followed by interruptions and concurrent operations. Sources: `README.md`, `CHANGELOG.md`, `schemas/release-artifact-contract.json`, [published release](https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.4.0).

Priority order:

1. Preserve the only unreadable profile when quarantine storage still refuses writes (RD-222).
2. Validate imported state and live edits before persistence, including ordinary incomplete schedule edits (RD-223, RD-224).
3. Close reproduced redaction gaps and stop treating a structurally valid dump as proof of privacy filtering (RD-225, RD-226).
4. Make backup restoration and Marketplace reset preserve recoverable data (RD-228, RD-229).
5. Serialize mutations across hosts, own installer descendants, and replace installed assets through staging and rollback (RD-230 through RD-232).
6. Repair Marketplace storage lifecycle and migration handling, then make cache writes survive concurrent work and process death (RD-234 through RD-237).
7. Recovery and accessibility inside Spotify are implemented through RD-242; verifier follow-ups are tracked as RD-247 through RD-252.
8. Make diagnostics observable and test the Windows crash artifact itself (RD-233, RD-243 through RD-245); correct contradictory public documentation (RD-246).

The remaining entries are recommendations unless an implemented item is called out below. Findings marked **Verified** were traced in source; exercised findings identify their synthetic reproduction. **Likely** describes a failure consequence not reproduced on a real installation. **Needs live validation** means a fixture or static check cannot establish the installed-client result.

Short in-client paths are relative to `src/LibreSpot.App/src/`; shared PowerShell helper names refer to `src/powershell/shared/`.

## Product Map

- **Setup and customization:** Home selects an appropriate action; Custom configures Spotify, SpotX and Spicetify. Recommended setup uses bundled Prism and LibreSpot; separate Marketplace installation is optional. Sources: `src/LibreSpot.Core/AppCatalog.cs`, `src/powershell/shared/Module-InstallThemes.ps1`, `README.md`.
- **In-client work:** Store, Look, Tweaks, Features, Presets and Health cover catalog discovery, temporary previews and live changes. Sources: `src/LibreSpot.App/src/surface/navigation.ts`, `src/LibreSpot.App/src/app.ts`.
- **Recovery:** maintenance repairs, authenticated safe-mode restoration, operation undo, profile exchange and Marketplace backups already exist. Sources: `src/LibreSpot.Desktop/ViewModels/MainViewModel.Maintenance.cs`, `src/LibreSpot.App/src/core/backup.ts`, `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`.
- **Managed endpoints:** CLI answer files, per-user update watching and portable verified asset caches support repeatable deployment. Sources: `src/LibreSpot.Cli/Program.cs`, `samples/deployment/`, `src/LibreSpot.Core/AssetCacheBundleService.cs`.

The primary users are Windows listeners who want configuration and repair without learning the upstream CLIs, and administrators who need predictable per-user deployment. Executables target win-x64; ARM64 Spicetify support does not imply a native ARM64 LibreSpot executable. The desktop is `asInvoker`. These distinctions are already documented in `README.md`, the three `src/LibreSpot.*/LibreSpot.*.csproj` files and `schemas/elevation-boundary.json`.

Data crosses four boundaries: GitHub/vendor downloads into pinned local assets; PowerShell workers into Spotify/Spicetify files; the companion into Spotify APIs; and profile data into localStorage/Marketplace IndexedDB. Support export is local and explicit. The root license is MIT; the in-client component has AGPL-3.0-only licensing and bundled notices. Sources: `LICENSE`, `src/LibreSpot.App/package.json`, `src/LibreSpot.App/THIRD_PARTY_NOTICES.md`, `schemas/data-inventory.json`, `schemas/community-assets.json`.

## Competitive Landscape

| Project or class | Useful evidence and lesson | Avoid |
|---|---|---|
| [Spicetify CLI](https://github.com/spicetify/cli) | Stable 2.44.0 remains the pinned integration. [Beta.14](https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.14), published 2026-09-05, adds reversible Windows update-staging protection and explicitly leaves a complete future update transaction unverified. Preserve that distinction between supported code and exercised workflow. | Adopting a prerelease or copying its Windows ACL changes without tuple verification. |
| [Marketplace](https://github.com/spicetify/marketplace) | Its v1.0.11 storage migration and localStorage fallback directly affect LibreSpot backup/reset. Own recovery must follow both backends, not assume IndexedDB is the complete state. | Claiming an IndexedDB-only reset removes every Marketplace setting. |
| [SpotX](https://github.com/SpotX-Official/SpotX) | [#891](https://github.com/SpotX-Official/SpotX/issues/891) supplies concrete mirror/manifest failure evidence. Download fallback and post-patch verification remain core integration work. | Advancing pins from version numbers alone or treating a service outage as patch corruption. RD-183 already covers the pin decision. |
| [BlockTheSpot](https://github.com/mrpond/BlockTheSpot) | Archived on 2026-02-14. Its DLL/config ownership is useful migration evidence. | Presenting it as a maintained replacement backend. |
| [EasyInstall](https://github.com/ohitstom/spicetify-easyinstall), [Israleche's manager](https://github.com/Israleche/SpicetifyManager), [AdotBdot's manager](https://github.com/AdotBdot/SpicetifyManager), [Protonos installer](https://github.com/Protonosgit/Spicetify_Installer) | Existing GUI/TUI installers provide path repair, local asset discovery and reapply workflows. EasyInstall has 2026 URL repairs despite its older release; two of the other projects are archived. LibreSpot's verification and recovery should remain visible advantages. | The previous claim that GUI competition has no useful signal, or treating README feature claims as runtime proof. |
| [SpotX-Bash](https://github.com/SpotX-Official/SpotX-Bash), [spotify-adblock](https://github.com/abba23/spotify-adblock) | Useful platform-specific rollback and request-filtering implementations. | Transferring Linux/macOS support ceilings to Windows or adding another binary patcher without ownership rules. |
| [Scoop-Spotify](https://github.com/TheRandomLabs/Scoop-Spotify) | Per-user packaging and patch-preserving wrappers illustrate explicit component ownership. | Its suggested hash-check bypass for regional download mismatches. Preserve LibreSpot's pinned digest requirement. |
| [Syncify](https://github.com/wSoltani/syncify) | Retained backups, empty-backup refusal and explicit coverage limits support a durable reset-recovery record. Its README limits coverage to Marketplace keys and discloses plaintext cloud storage. | Claiming arbitrary extension settings are covered, or adding cloud accounts to solve a local recovery defect. |
| [WindowBlinds](https://www.stardock.com/products/windowblinds/) | Editable saved presets make preset identity versus edited contents a meaningful distinction. LibreSpot already has the appearance controls; RD-241 fixes its misleading Applied state. | A general Windows skinning subsystem. |
| [Vortex](https://github.com/Nexus-Mods/Vortex/wiki/MODDINGWIKI-Users-General-Setting-up-Profiles/e13e66a7f519fd780de406717b531ce0d6b33974), [Windhawk](https://github.com/ramensoftware/windhawk/releases), [Vencord](https://vencord.dev/) | Scoped profiles, lifecycle controls and curated plugins support LibreSpot's existing product boundaries. Vortex documents that profile coverage varies by target. | The unsupported claim that these products prove a universal rollback or upstream-outage solution. |
| [Soundiiz](https://soundiiz.com/pricing) | Paid export and its [format documentation](https://support.soundiiz.com/hc/en-us/articles/38063068900754-How-to-Export-Your-Playlists-and-Favorites-to-a-File) show why backup coverage must be explicit. | Playlist transfer, Spotify account management or a paid-service dependency. |
| [librespot](https://github.com/librespot-org/librespot), [ncspot](https://github.com/hrkfdn/ncspot) | These are Premium-dependent playback/Connect tools, not installers for the official client. Their role explains the existing branding concern. | Duplicating authentication and playback in this manager; branding remains an existing blocked decision. |

## Reported Issues

**Verified on 2026-09-06:** the repository is not a fork and has no open issues or pull requests. All six closed threads were reviewed. [#22](https://github.com/SysAdminDoc/LibreSpot/issues/22) records Store delivery. [#4](https://github.com/SysAdminDoc/LibreSpot/issues/4) and [#5](https://github.com/SysAdminDoc/LibreSpot/issues/5) carry specific historical fixes. The earlier setup, header-gap and download threads [#1](https://github.com/SysAdminDoc/LibreSpot/issues/1), [#2](https://github.com/SysAdminDoc/LibreSpot/issues/2) and [#3](https://github.com/SysAdminDoc/LibreSpot/issues/3) do not establish a reproducible v4.5.0 defect. Closure alone does not prove that every original cause was fixed.

Discussions [#20](https://github.com/SysAdminDoc/LibreSpot/discussions/20) and [#21](https://github.com/SysAdminDoc/LibreSpot/discussions/21) exist and have no replies. The former describes an obsolete preview direction. The repository already has the `spotx` topic. Neither “empty discussions” nor “missing spotx topic” should survive from the prior research.

Upstream [Marketplace #1231](https://github.com/spicetify/marketplace/issues/1231#issuecomment-5512903863) provides the strongest actionable report: the reporter required both legacy localStorage cleanup and IndexedDB deletion. Its mechanism is present in the pinned storage source and supports RD-235. [Marketplace #1236](https://github.com/spicetify/marketplace/issues/1236) remains unresolved without a reliable reproducer. [Spicetify #3918](https://github.com/spicetify/cli/issues/3918) and [merged #3917](https://github.com/spicetify/cli/pull/3917) concern light-surface/popup styling, including Spotify 1.2.98 behavior. They need a pinned-client reproduction before being called LibreSpot bugs.

Community [lost-setup](https://www.reddit.com/r/spicetify/comments/1uci5ou/spicetify_literally_deleted_the_hours_i_spent_on/) and [backup-discovery](https://www.reddit.com/r/spicetify/comments/1uh68ad/save_a_backup_file/) reports are anecdotes about Spicetify, not LibreSpot incident counts. They reinforce recovery value. [HN](https://news.ycombinator.com/item?id=39775011) and [awesome-ricing](https://github.com/fosslife/awesome-ricing) mostly suggest controls already shipped.

## Security, Privacy, and Reliability

### Profile and backup safety

**Verified, exercised:** `EngineStore.save` in `src/LibreSpot.App/src/core/store.ts` overwrites the active key even when its second quarantine attempt fails. Refusing quarantine writes while allowing a smaller replacement loses the original after constructing a new store. The existing test frees storage before saving and misses continued refusal (RD-222).

**Verified, exercised:** `parseProfile` in `core/profile.ts` accepts null snippet collections, object-valued presets and invalid appearance values through a cast. Clearing an enabled schedule's time in `panels/look.ts` persists an invalid clock because `core/engine.ts` saves before applying. Reload accepts that invalid value again. A harmless VM canary also demonstrated unsafe string interpolation in `exportThemeRuntime`; that export currently has test callers only, so this is a core API flaw, not a demonstrated panel exploit (RD-223, RD-224).

**Verified source gap:** `core/backup.ts:parseBackup` accepts negative/fractional schema values and converts a present malformed Marketplace section into empty state. Validate the full envelope while preserving the existing absent-section compatibility case (RD-223).

**Verified source paths:** `extensions/librespot-engine.ts` discards update failures through callers that void the promise and can announce success after flag application returns unavailable. Before RD-228, restore wrote Marketplace before the engine with no compensation for a later engine failure. RD-228 now snapshots both stores, preserves the prior engine bytes, keeps Marketplace's merge semantics explicit, compensates exact affected keys, and retains a bounded recovery record when compensation cannot finish. RD-229 now writes a Marketplace reset record in LibreSpot storage before clipboard access or deletion, and Health restores, exports, or dismisses it after a clipboard replacement or fresh runtime, recreating the known Marketplace settings store when needed. Recovered raw-state export also advertises Restore, whose parser currently requires a backup envelope (RD-223, RD-224).

### Support-export privacy

**Verified, exercised with synthetic input:** `src/LibreSpot.Core/SupportBundleService.cs:RedactText` misses JSON secret values and timestamp-prefixed bearer headers; a quoted password retains words after the first space. Its minidump validator accepts identical structurally valid fixtures carrying no privacy flags, full-memory flags, or Triage flags. It never examines the header flags at byte 24. Structural validity cannot substantiate the “privacy-filtered” description (RD-225, RD-226). No real credential or user memory dump was used.

**Needs live validation:** existing crash tests fake launch/environment and synthesize dump headers. Test the Windows single-file capture model through an isolated crash fixture (RD-245). Do not infer a Windows failure from Microsoft's generic single-file warning: the .NET 10.0.11 [Windows implementation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/coreclr/debug/createdump/createdumpwindows.cpp) uses MiniDumpWriteDump; the special single-file path is conditional on Unix in `createdumpmain.cpp`.

### Install and cache lifecycle

**Implemented and exercised:** RD-232 stages the pinned CLI, theme, custom-app, and companion packages on the target volume, verifies complete staged contents, and commits package plus configuration changes through a durable swap marker. Failed requested apps retain their existing config entries. Extraction, copy, bootstrap, rename, and post-rename configuration failures restore the original fingerprints. Recovery validates marker ownership, direct target parents, same-volume staging, reparse boundaries, and the current configuration fingerprint before it touches an abandoned transaction. Marketplace-only theme selection still runs pending transaction recovery. Sources: those shared modules and `tests/powershell/LibreSpot.Tests.ps1`.

RD-230 gives the desktop backend, standalone CLI workers, and both watcher hosts one reentrant per-user lease. Its identity uses the current Windows user plus canonical Spotify and Spicetify targets, so a configurable LibreSpot data root cannot split ownership and separate installations do not contend. The lease is acquired before operation journals, snapshots, Spotify shutdown, and watcher reapply work. A second host gets a `LIBRESPOT_MUTATION_BUSY` result without entering its mutation body, and a terminated owner can be recovered. RD-231 wraps standalone external scripts in a PowerShell 5.1-compatible kill-on-close Job Object and desktop backend hosts in `OwnedProcessTree`. Timeout, cancellation, watchdog shutdown, and launcher exit terminate child and grandchild fixtures before cleanup; a backend cancellation fixture preserved an unrelated process, and containment setup failures return an explicit error.

`Save-ToAssetCache.ps1` and `Update-AssetCacheIndexEntry.ps1` now stage and flush cache files, re-read indexes under the shared `.asset-cache.lock` lease used by Core and PowerShell, and publish both object and JSON replacements atomically. Corrupt indexes stay in place and are reported, and concurrent inserts retain both entries. RD-237 adds a bounded, durable transaction marker before each cache directory rename. Core and PowerShell validate its owned sibling paths and reparse boundaries under the same lease, restore the prior tree when the replacement is incomplete, and keep a verified replacement when the second rename completed. Process-termination fixtures cover all swap boundaries while preserving unindexed files and unrelated siblings.

**Preserve existing safeguards:** pinned consumers independently select cache digests; a forged bundle manifest cannot substitute bytes for a repository pin. Safe-mode recovery authenticates its snapshot with DPAPI. Removal already has a junction-safe implementation; extend its use rather than redesigning it. Sources: `src/LibreSpot.Core/AssetCacheBundleService.cs`, `src/powershell/shared/Get-FromAssetCache.ps1`, `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`, `src/powershell/shared/Remove-PathSafely.ps1`.

### Marketplace storage

**Implemented and exercised (RD-234):** `core/backup.ts` closes a database connection arriving after its open attempt settles. Delete requests expose pending, watchdog, success, and terminal-error states, keep the request alive after a watchdog, and reuse the in-flight operation for duplicate calls. Health disables the reset action while the request is pending and announces that a watchdog did not cancel it. Focused fixtures cover blocked-then-success, timeout-then-success, terminal error, late-open cleanup, and unrelated storage canaries. [IndexedDB deletion](https://www.w3.org/TR/IndexedDB-3/#delete-a-database) waits for existing connections to close before continuing.

Pinned [Marketplace Storage.ts](https://raw.githubusercontent.com/spicetify/marketplace/v1.0.11/src/logic/Storage.ts) migrates surviving `marketplace:` keys when the new database lacks its migration marker, and uses localStorage when IndexedDB is unavailable. Successful migration normally removes legacy keys. This conditional storage path was the RD-235 recovery gap.

**Implemented and exercised (RD-235):** the Marketplace adapter now reads only owned `marketplace:` keys from IndexedDB and localStorage, applies the pinned database and migration-marker precedence, and records backend availability separately from an empty store. Backup files retain both views, reset clears the owned keys in both backends, and restore replaces the captured state while preserving unrelated storage. Prior backup envelopes remain readable, and migration fixtures prove a removed theme does not return after reset and reload.

## Architecture Assessment

**Prioritization:** P1 is Now: data preservation, privacy and ownership before mutation. P2 is Next: precise recovery, observability and accessibility, with small root-cause fixes first. Larger product additions remain Later or Under Consideration where `Roadmap_Blocked.md` already records their prerequisites. The recommendations are reliability parity within the existing architecture; no framework rewrite is justified. Dependency order is explicit in `ROADMAP.md`.

**Implemented and exercised (RD-238, RD-239):** the companion API wait now has a 30-second deadline and publishes a persistent loading, ready or error status. The in-client surface listens for that status, binds a runtime only after the loaded marker is true, removes a failed pre-start runtime, and offers an accessible retry action. Frame probes have a 1.5-second deadline, defer while the document is hidden or frames never arrive, and run after engine initialization without blocking listener setup. A deferred sample leaves the current effects tier unchanged. Each panel now sits inside a React error boundary that preserves navigation and Health access, redacts exception details from the view, and retries without changing saved state.

**Implemented and exercised (RD-240):** `core/engine.ts:refreshAccent` now captures the state, scheme and dynamic-accent inputs for each request and discards late results when those inputs or the request generation changed. A derived Material palette is retained through `apply`, preview cancellation, navigation and timer reapplication. Scheduled light and dark changes invalidate the old derivation and trigger a fresh request. Reversed artwork completions and a scheduled boundary fixture cover both paths.

**Implemented and exercised (RD-241):** the Presets panel now derives the Applied state from each built-in profile's owned fields, including optional scale and accessibility settings. It ignores the display name and unrelated profile values, so editing a preset-owned control re-enables Apply while a same-name custom profile remains distinct. Surface fixtures cover both false-match cases and unrelated state changes.

**Implemented and exercised (RD-242):** shared Toggle, Select, Slider, Input and Color rows assign deterministic description IDs and connect them with `aria-describedby`, so feature explanations and live or desktop application limits are exposed with each control. Store filtering publishes one atomic polite status message for the current result count or empty state. Structural DOM fixtures verify every shared row, and the Store source contract checks the single status region. An isolated screen-reader run is still required before claiming live assistive-technology validation.

**Verified source UI gaps:** existing six-panel and four WPF screenshots were inspected. Live interaction, screen-reader output and newly changed visual states require isolated validation; existing pictures do not establish those results.

**Implemented and exercised (RD-233):** both lane functions now reset `LibreSpotReapplyStep` at tick entry, retain it through cleanup, write the originating download, parameter, patch, or Spicetify application stage into the failure diagnostics, and clear it after the watcher records the result. The actual watcher call chains pass controlled failures for all four stages while preserving the retry and hold policy.

**Implemented and exercised (RD-243):** `CrashReporter` probes the per-user log directory, keeps a writable temporary fallback in Serilog's fallback chain when the primary directory is unavailable, and observes SelfLog failures without writing through the failed sink. One bounded in-memory status reaches the activity view, while support bundles include a redacted logging-status report, list unreadable files and omit their payloads. No network sink is enabled.

**Implemented and exercised (RD-244):** `Read-ProcessOutputDelta.ps1` reads fixed byte chunks with a bounded remainder and emits markers for oversized lines or a capped batch. Both host preambles drain stdout and stderr through asynchronous readers with bounded queues, retain the newest diagnostics when the queue fills, and mark discarded output. `Invoke-SpicetifyCli.ps1` keeps a 32-line failure tail, and isolated redirected worker logs are trimmed to a 1 MiB tail with a disk-capture marker. PowerShell fixtures cover many short lines, an oversized unterminated failure line, queue limits, marker emission, and bounded redirected files while preserving exit and failure classification.

**Fresh verification findings (RD-247 through RD-252):** a malformed PowerShell cache index can still be treated as an empty inventory; a cache-root junction can redirect PowerShell writes; copied pre-existing cache objects are not durably flushed; Core process-death tests do not terminate a helper process; companion readiness accepts truthy objects whose bootstrap methods are missing; and lifecycle coverage relies on source assertions instead of a staged companion integration fixture. These are recorded in `ROADMAP.md` with reproductions, affected files and acceptance tests.

**Dependency assessment on 2026-09-06:** pnpm's complete installed lockfile audit and the desktop NuGet direct/transitive vulnerability query both returned no advisories. The .NET 10.0.11 and PowerShell 7.6.5 floors already exist. Primary changelogs were checked for the .NET UI/logging packages and the TypeScript toolchain. New TypeScript 7 and Vitest 5 releases do not alone justify an upgrade: TypeScript 7 lacks the compiler API used by existing tooling, while [Vitest 5](https://vitest.dev/blog/vitest-5.html) offers browser tracing that can be evaluated when a browser fixture needs it. Keep React aligned with the host ABI. Sources: `src/LibreSpot.App/package.json`, `eslint.config.js`, `schemas/dependency-health-allowlist.json`, [TypeScript release](https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/).

**Testing strategy:** retain local builds and fixture isolation. Use real parser/store/worker code with synthetic malformed values, delayed callbacks and process termination at commit boundaries. The [Pathfinder research](https://arxiv.org/abs/2503.01390) supports selecting representative interruption states; its POSIX/MMIO implementation is not a Windows dependency recommendation. Avoid tests that only look for source strings or model a killed process by throwing an exception.

**Documentation:** `README.md` and `SECURITY.md` still deny redistributing upstream code despite bundled licensed components. `.github/CONTRIBUTING.md` gives a blanket MIT statement; the PR template runs one Pester file rather than the configured suite. `CHANGELOG.md` retains contradictory Prism API and smoke-containment statements. `src/LibreSpot.App/README.md` still names the old Extensions navigation. Correct current claims while preserving clearly historical signing material (RD-246).

## Rejected Ideas

- **Unreviewed pin advances:** RD-183 remains the SpotX decision. RD-208 includes upstream Blackout removal, not one harmless theme fix. Preserve those existing entries; do not duplicate them. Sources: `Roadmap_Blocked.md`, [themes #1283](https://github.com/spicetify/spicetify-themes/pull/1283).
- **Spicetify v3 adoption and a new update-blocking mechanism:** beta.14 is still a prerelease and its own notes leave an update cycle unverified. Sources: beta.14 release, `Get-SpicetifyV3Conflict.ps1`.
- **Cloud sync, arbitrary catalog URLs and complete extension-settings backup:** unnecessary account/trust boundaries; Marketplace does not own every extension's data. Sources: Syncify README, `schemas/community-assets.json`.
- **Mobile/browser/Linux port, playback/Connect and playlist transfer:** these require different platform or authentication ownership, and existing blocked items already describe the product choices. Sources: `Roadmap_Blocked.md`, librespot, ncspot, Soundiiz.
- **Signing enrollment, package identity or another distribution channel:** signing is settled; package/update ownership and outward submissions already have decision records. Sources: `SIGNPATH.md`, `Roadmap_Blocked.md`. No new winget or CI work.
- **Broad localization or another WPF redesign:** the five shell locales and English-only in-client decision are documented; expanded WPF accessibility checks already shipped. Apply the targeted in-client descriptions/status fix instead. Sources: `README.md`, `.crowdin.yml`, `tests/LibreSpot.Desktop.Tests/WpfUiAutomationSmokeTests.cs`.
- **Upgrade every dependency, replace local storage with a database framework, or add CRDTs:** no measured benefit justifies the compatibility/maintenance cost. Repair existing commit boundaries. Sources: TypeScript release, `core/store.ts`, [local-first research](https://www.inkandswitch.com/essay/local-first/).
- **Claim account bans or confirmed light-theme regressions from anecdotes:** no verified LibreSpot account incident or pinned-client reproduction was established. Sources: upstream #3918/#3917 and the community threads above.

## Sources

### Product, trackers and comparison

https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.4.0
https://github.com/SysAdminDoc/LibreSpot/issues
https://github.com/SysAdminDoc/LibreSpot/discussions/21
https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.14
https://github.com/spicetify/cli/releases/tag/v3.0.0-beta.13
https://github.com/spicetify/cli/releases/tag/v2.44.0
https://github.com/spicetify/marketplace/releases/tag/v1.0.11
https://github.com/spicetify/marketplace/issues/1231
https://github.com/spicetify/marketplace/pull/1232
https://raw.githubusercontent.com/spicetify/marketplace/v1.0.11/src/logic/Storage.ts
https://github.com/SpotX-Official/SpotX/issues/891
https://github.com/spicetify/spicetify-themes/pull/1283
https://github.com/spicetify/cli/pull/3917
https://github.com/mrpond/BlockTheSpot
https://github.com/ohitstom/spicetify-easyinstall
https://github.com/Israleche/SpicetifyManager
https://github.com/AdotBdot/SpicetifyManager
https://github.com/Protonosgit/Spicetify_Installer
https://github.com/SpotX-Official/SpotX-Bash
https://github.com/abba23/spotify-adblock
https://github.com/TheRandomLabs/Scoop-Spotify
https://github.com/wSoltani/syncify
https://www.stardock.com/products/windowblinds/
https://soundiiz.com/pricing
https://github.com/ramensoftware/windhawk/releases
https://vencord.dev/
https://github.com/librespot-org/librespot
https://github.com/hrkfdn/ncspot

### Standards, platform, dependencies and engineering

https://html.spec.whatwg.org/multipage/webstorage.html
https://www.w3.org/TR/IndexedDB-3/#delete-a-database
https://json-schema.org/draft/2020-12/json-schema-validation
https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html
https://www.w3.org/TR/wcag2ict/
https://react.dev/reference/react/Component
https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects
https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ns-minidumpapiset-minidump_header
https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ne-minidumpapiset-minidump_type
https://learn.microsoft.com/en-us/dotnet/core/diagnostics/collect-dumps-crash
https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/coreclr/debug/createdump/createdumpwindows.cpp
https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/coreclr/debug/createdump/createdumpmain.cpp
https://github.com/dotnet/core/blob/main/release-notes/10.0/cve.md
https://github.com/PowerShell/PowerShell/releases/tag/v7.6.5
https://github.com/serilog/serilog/wiki/Reliability
https://github.com/serilog/serilog-sinks-file/pull/342
https://github.com/lepoco/wpfui/releases/tag/4.3.0
https://github.com/CommunityToolkit/dotnet/releases/tag/v8.4.2
https://github.com/icsharpcode/AvalonEdit/releases/tag/v6.3.1
https://github.com/Shane32/QRCoder/releases/tag/v1.8.0
https://github.com/serilog/serilog/releases/tag/v4.4.0
https://github.com/evanw/esbuild/releases/tag/v0.28.2
https://github.com/typescript-eslint/typescript-eslint/releases/tag/v8.69.0
https://vitest.dev/blog/vitest-5.html
https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/
https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/secure-your-dependencies/verify-release-integrity
https://arxiv.org/abs/2503.01390
https://www.inkandswitch.com/essay/local-first/

### Community and discovery

https://www.reddit.com/r/spicetify/comments/1uci5ou/spicetify_literally_deleted_the_hours_i_spent_on/
https://www.reddit.com/r/spicetify/comments/1uh68ad/save_a_backup_file/
https://www.reddit.com/r/spicetify/comments/1ts6vyn/i_made_a_spicetify_extension_that_backs_up_and/
https://news.ycombinator.com/item?id=39775011
https://github.com/fosslife/awesome-ricing

## Open Questions

No operator answer blocks RD-222 through RD-246. Existing pin, packaging, branding and support-policy decisions remain in `Roadmap_Blocked.md`. Installed-client and Windows crash behavior identified as Needs live validation must be proved in isolated fixtures before implementation is declared complete.
