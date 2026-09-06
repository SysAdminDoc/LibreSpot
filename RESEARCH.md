# Research: LibreSpot

Date: 2026-09-06. Replaces all prior research.

## Executive Summary

LibreSpot is a Windows Spotify customization manager with a WPF desktop, fleet CLI, PowerShell host, and an in-client Store and live engine. The strongest direction is dependable recovery around its curated, pinned integrations. Source v4.5.0 is prepared; the immutable public release is v4.4.0. The previous compatibility, update-status and WPF accessibility findings have been addressed. This plan instead concentrates on reproduced failures in profile persistence and support-export privacy, followed by interruptions and concurrent operations. Sources: `README.md`, `CHANGELOG.md`, `schemas/release-artifact-contract.json`, [published release](https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.4.0).

Priority order:

1. Preserve the only unreadable profile when quarantine storage still refuses writes (RD-222).
2. Validate imported state and live edits before persistence, including ordinary incomplete schedule edits (RD-223, RD-224).
3. Close reproduced redaction gaps and stop treating a structurally valid dump as proof of privacy filtering (RD-225, RD-226).
4. Make backup restoration and Marketplace reset preserve recoverable data (RD-228, RD-229).
5. Serialize mutations across hosts, own installer descendants, and replace installed assets through staging and rollback (RD-230 through RD-232).
6. Repair Marketplace storage lifecycle and migration handling, then make cache writes survive concurrent work and process death (RD-234 through RD-237).
7. Finish recovery and accessibility inside Spotify: bounded startup, render fallback, coherent dynamic colors and truthful preset state (RD-238 through RD-242).
8. Make diagnostics observable and test the Windows crash artifact itself (RD-233, RD-243 through RD-245); correct contradictory public documentation (RD-246).

These are recommendations, not implemented fixes. Findings marked **Verified** were traced in source; exercised findings identify their synthetic reproduction. **Likely** describes a failure consequence not reproduced on a real installation. **Needs live validation** means a fixture or static check cannot establish the installed-client result.

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

**Verified source paths:** `extensions/librespot-engine.ts` discards update failures through callers that void the promise and can announce success after flag application returns unavailable. Restore writes Marketplace before the engine and has no compensation for a later engine failure. Reset deletes the database after copying its recovery data only to the clipboard. A later clipboard replacement leaves no retained reset record. Recovered raw-state export also advertises Restore, whose parser currently requires a backup envelope (RD-223, RD-224, RD-228, RD-229).

### Support-export privacy

**Verified, exercised with synthetic input:** `src/LibreSpot.Core/SupportBundleService.cs:RedactText` misses JSON secret values and timestamp-prefixed bearer headers; a quoted password retains words after the first space. Its minidump validator accepts identical structurally valid fixtures carrying no privacy flags, full-memory flags, or Triage flags. It never examines the header flags at byte 24. Structural validity cannot substantiate the “privacy-filtered” description (RD-225, RD-226). No real credential or user memory dump was used.

**Needs live validation:** existing crash tests fake launch/environment and synthesize dump headers. Test the Windows single-file capture model through an isolated crash fixture (RD-245). Do not infer a Windows failure from Microsoft's generic single-file warning: the .NET 10.0.11 [Windows implementation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.11/src/coreclr/debug/createdump/createdumpwindows.cpp) uses MiniDumpWriteDump; the special single-file path is conditional on Unix in `createdumpmain.cpp`.

### Install and cache lifecycle

**Verified source gaps; failure consequences Likely:** `Invoke-ExternalScriptIsolated.ps1` kills only its direct process on timeout. CLI, theme and custom-app installers remove working files before replacement completes. No shared mutation lease covers desktop, CLI and watcher together; the scheduled task's IgnoreNew policy covers only that task (RD-230 through RD-232). Sources: those shared modules, `src/powershell/backend/lane-functions.ps1`, `src/LibreSpot.Core/BackendScriptService.cs`.

`Save-ToAssetCache.ps1` overwrites final objects and `Update-AssetCacheIndexEntry.ps1` performs an unlocked whole-index rewrite. C# and PowerShell bundle import compensate rename failures with catch blocks, but have no durable recovery record for termination between the two directory renames. Existing “interruption” tests throw exceptions, which still execute compensation. Separate individual-write atomicity from restart recovery (RD-236, RD-237).

**Preserve existing safeguards:** pinned consumers independently select cache digests; a forged bundle manifest cannot substitute bytes for a repository pin. Safe-mode recovery authenticates its snapshot with DPAPI. Removal already has a junction-safe implementation; extend its use rather than redesigning it. Sources: `src/LibreSpot.Core/AssetCacheBundleService.cs`, `src/powershell/shared/Get-FromAssetCache.ps1`, `src/powershell/shared/Reapply-SavedSpicetifySetup.ps1`, `src/powershell/shared/Remove-PathSafely.ps1`.

### Marketplace storage

**Verified, exercised:** `core/backup.ts` ignores a database connection arriving after its open timeout instead of closing it. **Verified source/spec consequence:** rejecting a blocked or timed-out delete promise does not cancel the IndexedDB request; deletion can finish after the UI says it did not reset anything. [IndexedDB deletion](https://www.w3.org/TR/IndexedDB-3/#delete-a-database) waits for existing connections to close before continuing (RD-234).

Pinned [Marketplace Storage.ts](https://raw.githubusercontent.com/spicetify/marketplace/v1.0.11/src/logic/Storage.ts) migrates surviving `marketplace:` keys when the new database lacks its migration marker, and uses localStorage when IndexedDB is unavailable. Successful migration normally removes legacy keys. LibreSpot's database-only backup/reset therefore misses a conditional but real storage mode (RD-235). Preserve unrelated Spotify and LibreSpot keys.

## Architecture Assessment

**Prioritization:** P1 is Now: data preservation, privacy and ownership before mutation. P2 is Next: precise recovery, observability and accessibility, with small root-cause fixes first. Larger product additions remain Later or Under Consideration where `Roadmap_Blocked.md` already records their prerequisites. The recommendations are reliability parity within the existing architecture; no framework rewrite is justified. Dependency order is explicit in `ROADMAP.md`.

**In-client lifecycle:** `app.ts:useRuntime` polls indefinitely after the companion's bounded API wait ends. If it captures the runtime published before `engine.start()` fails, it retains that failed object even after the companion removes the global. `core/performance.ts` waits entirely on animation frames, so background rendering can stall the measurement awaited by startup. There is no panel error boundary. Bound startup, invalidate failed instances and preserve repair access (RD-238, RD-239).

**Verified, exercised color defects:** `core/engine.ts:refreshAccent` accepts older artwork results after newer ones. A controlled promise-order test changed the accent back to the old track. `apply` also overwrites a derived Material palette with the base scheme; the companion calls it on navigation and every minute. These need generation-aware results and consistent reapplication (RD-240).

**Verified source UI gaps:** `panels/presets.ts` disables Apply using the preset name alone, even after edits retain that name. `surface/ui.ts` renders descriptions without associating them with controls; Store result changes lack the announcement already present in Features (RD-241, RD-242). Existing six-panel and four WPF screenshots were inspected. Live interaction, screen-reader output and newly changed visual states require isolated validation; existing pictures do not establish those results.

**Diagnostics:** both lane functions clear `LibreSpotReapplyStep` before the outer catch records it; a no-op dependency fixture reproduced a generic “reapply” stage after a specific patch failure (RD-233). `CrashReporter.Initialize` configures a file sink without SelfLog/failure-listener handling even though its pinned sink supports it (RD-243). `Read-ProcessOutputDelta.ps1` retains an unbounded partial line, and `Invoke-SpicetifyCli.ps1` stores every line when it only needs an error tail. Both host preambles also define an unbounded `LibreSpotNativeOutputCollector` queue; the runner's `BeginOutputReadLine` can buffer an oversized unterminated line before its first callback. Bounds must include that reader and collector (RD-244).

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
