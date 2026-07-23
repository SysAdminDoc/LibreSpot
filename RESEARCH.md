# Research — LibreSpot

Date: 2026-07-14 — replaces all prior research.

## Executive Summary

LibreSpot is a Windows-first, local-only orchestrator for a verified SpotX + Spicetify setup, with a mature PowerShell lane, a .NET 10 WPF shell, a fleet CLI, pinned/hash-verified dependencies, compatibility diagnostics, profiles, offline cache reuse, operation receipts, and redacted support export. Its strongest direction is not a broader feature catalog; it is making the existing trusted orchestration boundary crash-safe and resistant to unsafe upstream behavior. The highest-value opportunities, in order, are:

1. **[Verified] Make profile activation transactional.** `LocalProfileService.ApplyProfileAsync` (`src/LibreSpot.Desktop/Services/LocalProfileService.cs:158-170`) and `Apply-LibreSpotProfile` (`LibreSpot.ps1:1667-1678`) update previous pointer, config, and active pointer as three independent commits.
2. **[Verified] Block upstream security-product mutations.** The current SpotX pin (`src/LibreSpot.Desktop/Models/AppCatalog.cs:588-595`) predates a 2026-07-11 upstream change that adds Microsoft Defender exclusions by default; pin refreshes need a deny-by-default policy and verified opt-out.
3. **[Verified] Enforce the local-data inventory.** `schemas/data-inventory.json` claims complete coverage but omits profiles, active/previous pointers, operation journal, run receipt, preservation evidence, and fleet logs that current code writes.
4. **[Verified] Finish safe undo execution.** `schemas/operation-token-types.json` and `OperationJournalUndoService.cs` model reversible state, but `MainWindow.xaml:4745-4805` exposes manual notes only; execute an allowlisted low-risk subset and refuse binary/destructive tokens.
5. **[Verified] Complete stable operation correlation (existing RD-20).** One visible ID should join WPF, backend, CLI, receipts, logs, crashes, and support bundles.
6. **[Verified] Detect foreign patcher state before mutation (existing RD-19).** Archived BlockTheSpot and standalone SpotX/Spicetify footprints can change repair semantics.
7. **[Verified] Generate both PowerShell hosts from one contract (existing RD-25).** Copied host bodies remain a recurring parity boundary despite extracted shared functions.
8. **[Verified] Finish advertised locales and broad in-app search (existing RD-26/RD-18).** The framework exists; translation depth and intent-based discovery remain incomplete.

## Product Map

- **Core workflows:** [Verified] inspect compatibility/readiness; install or reapply Recommended/Custom setups; repair, restore, back up, or remove managed state; create/import/apply local profiles; export diagnostics and automate the same lifecycle through the fleet CLI.
- **Users:** [Verified] individual Windows users wanting guided customization; advanced users choosing exact SpotX/Spicetify options; support contributors diagnosing failed patch state; endpoint administrators using answer files, NDJSON, receipts, and noninteractive verbs.
- **Platforms/distribution:** [Verified] Windows 10/11; Windows PowerShell 5.1 and PowerShell 7; .NET 10 WPF and CLI currently published for `win-x64`; portable GitHub release assets. Signing, package identity, ARM64 native artifacts, and installed-app updating remain policy-blocked in `Roadmap_Blocked.md`.
- **Integrations/data flow:** [Verified] Spotify desktop state -> environment snapshot -> compatibility/preflight plan -> pinned SpotX/Spicetify/Marketplace downloads -> SHA256 cache -> local mutation -> JSONL events/journal/receipt -> optional redacted support ZIP. No credentials or telemetry service are required.
- **Product philosophy:** [Verified] local-only, least privilege, explicit consent, reproducible pins, observable mutation, and recovery before novelty (`README.md`, `CLAUDE.md`, `schemas/elevation-boundary.json`).

## Competitive Landscape

- **SpotX / SpotX-Bash:** [Verified] excel at rapid Spotify-version support, update blocking, rollback flags, and expert parameters. Learn explicit compatibility windows and retain older known-good targets. Avoid remote `curl|iex` execution and default security-product exclusions; LibreSpot should remain the stricter trust wrapper.
- **Spicetify CLI:** [Verified] publishes platform attestations, exact Spotify compatibility ranges, restore workflows, and fast fixes. Learn provenance verification and hard compatibility ceilings. Avoid making users reason about command order, backup resets, or Store-vs-desktop path differences.
- **Spicetify Marketplace/themes:** [Verified] provide strong discovery, uninstall, localization, blacklisting, and a large theme/extension ecosystem. Learn clear trust metadata and removal state. Avoid treating community code or IndexedDB state as validated LibreSpot profile data.
- **BlockTheSpot Installer:** [Verified] makes recommended/newer Spotify selection and backup-based restore obvious. Learn a visible escape hatch and post-action launch verification. Avoid archived patch engines and unverified in-place binary restore; LibreSpot's stock restore remains correctly blocked pending a real patched-client rig.
- **spicetify-easyinstall and similar one-click GUIs:** [Verified] reduce terminal friction and expose themes/extensions/config in one flow. Learn concise guided defaults. Avoid opaque cleanup: its issue history includes deleted configs and incomplete downloads, while LibreSpot should preview scope and preserve foreign state.
- **ReVanced Manager:** [Verified] demonstrates patch selection, compatibility-aware customization, settings discovery, and update management at ecosystem scale. Learn computed preflight plans and per-patch compatibility. Avoid importing its mobile, signing, and general plugin-manager scope into a Windows Spotify orchestrator.
- **WinGet / Ninite / PDQ Deploy:** [Verified] make audit output, download caches, offline behavior, configuration history, and detailed deployment results first-class. Learn deterministic receipts and cache health. Avoid centralized inventory, licensing, and multi-admin control that duplicate endpoint-management products.
- **Intune Enterprise App Management:** [Verified] shows the value of supersedence, assignment state, monitoring, and update rings. LibreSpot should integrate through its CLI/exit contracts, not become a cloud control plane or silently auto-update volatile patchers.

## Security, Privacy, and Reliability

- **[Verified] Unsafe upstream drift:** LibreSpot pins SpotX commit `550bc72...`; SpotX `main` was four commits ahead on 2026-07-14. Commit `afb4c3f` added default-on Defender path/process exclusions plus `-defender_exclusions_off`. The shipped pin is unaffected, but `Build-Scripts.ps1` and the pin-refresh path do not encode a policy that rejects or disables security-product mutations.
- **[Verified] Crash-consistency gap:** profile activation writes `active-profile.previous.json`, `config.json`, and `active-profile.json` separately in both WPF and stable PowerShell. A failure or concurrent host between writes can leave a valid active pointer paired with another profile's config; current pointer recovery handles malformed/missing/dangling pointers, not this split-brain state.
- **[Verified] Incomplete privacy contract:** `schemas/data-inventory.json` omits current write sites including `profiles/*.json`, both profile pointers, `operation-journal.jsonl`, `run-receipt.latest.json`, `spicetify-preservation-latest.json`, and `%ProgramData%\LibreSpot\logs`. This leaves sensitivity, retention, deletion, and support-export behavior unenforced for real artifacts.
- **[Verified] Manual-only rollback:** receipt/token metadata records previous-state references and risk, but the WPF surface only renders an undo note. Recovery should execute only a small allowlist, require preview/confirmation, journal the rollback, and refuse `spotxPatch`, destructive, missing-state, unknown, or stale tokens.
- **[Verified] Current positives:** NuGet vulnerability scans for the WPF and CLI projects returned no vulnerable packages on 2026-07-14; direct desktop dependencies were current; installed .NET 10.0.9 is supported through 2028-11-14. Download hashes, path/reparse defenses, bounded parsers, atomic single-file writes, support redaction, and least-privilege WPF startup are already implemented.
- **[Likely] Recovery priority:** executable low-risk undo should follow transactional profile activation so restoring `config.json` cannot recreate profile/config divergence. Binary SpotX rollback remains outside this item and under the live-validation blocker in `Roadmap_Blocked.md`.

## Architecture Assessment

- **[Verified] Boundary pressure:** `MainWindow.xaml` (~5,200 lines), `MainViewModel.cs` (~4,500), and `EnvironmentSnapshotService.cs` (~2,300) are feature aggregators. Extract future changes behind existing state-domain/services first; do not perform a speculative UI rewrite.
- **[Verified] Host duplication:** `LibreSpot.ps1` and `src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1` still embed copied composition and lane wrappers. RD-25 is the correct root fix; new security and transaction helpers must live in `src/powershell/shared/` and be generated into both hosts.
- **[Verified] Persistent-state ownership is scattered:** config schema versioning, profile-store versions, pointers, receipts, journal, watcher state, cache indexes, and drift caches each implement local parsing/recovery rules. Make `schemas/data-inventory.json` the registry for ownership/retention/version policy before adding a generic migration framework.
- **[Verified] Test strengths:** the suite covers schemas, property-based configuration behavior, backend regression, support redaction, environment state, UI automation, theme/high-contrast/localization fixtures, CLI contracts, release artifacts, and bounded reads.
- **[Verified] Test gaps:** no fault injection spans every profile-activation write boundary; no guard prevents a SpotX pin refresh from enabling Defender exclusions; no test proves every owned storage path appears in the data inventory; no executor/idempotency tests exist for operation tokens.
- **[Verified] Documentation gaps:** RD-27 already covers stale issue intake, and RD-26 covers translation depth. Packaging, signing, release channels, ARM64, Windows lifecycle, stock restore, alternative clients, Marketplace backup, and package identity remain explicitly blocked; do not duplicate them in the actionable roadmap.
- **[Verified] Category disposition:** accessibility has keyboard/UIA/high-contrast contracts with no net-new gap; i18n, observability, docs, and search remain active as RD-26/RD-20/RD-27/RD-18; distribution/packaging and installed-app upgrades are blocked; plugin hosting, mobile, multi-user control, redistributable offline kits, and speculative migrations are rejected below. Testing and offline/resilience acceptance are embedded in RD-28 through RD-31.

## Rejected Ideas

- **Cross-platform or mobile core app:** [Rejected] SpotX-Windows, WPF, Task Scheduler, registry, PATH, and Windows package detection are foundational; SpotX-Bash/Spotube/ReVanced prove demand but imply a separate product and support contract.
- **Alternative-client install cards:** [Rejected] Spotube/ncspot/other clients have different playback, account, Premium, legal, and support models; the required compliance decision is already in `Roadmap_Blocked.md`.
- **Hosted profile/plugin marketplace:** [Rejected] Spicetify Marketplace already owns discovery, while its wiki explicitly warns that community code is unvalidated. LibreSpot should curate pins and share only its managed settings, not become another executable-code registry.
- **Full Marketplace/IndexedDB backup in `.librespot`:** [Rejected] Marketplace 1.0.9 moved storage to IndexedDB; mixing browser state with a credential-free settings profile violates the existing data boundary. The broader policy decision is already blocked.
- **Automatic desktop updater/package-manager publication now:** [Rejected] TUF/SLSA and USENIX updater research support stronger update trust, but LibreSpot lacks the approved identity, signing enrollment, installed-app ownership, and feed policy; all are already blocked.
- **Central multi-user/cloud management:** [Rejected] PDQ/Intune cover that product class. LibreSpot's fleet CLI, JSON contracts, stable exit codes, and local receipts are the correct integration boundary.
- **Redistributable offline kit:** [Rejected] Ninite demonstrates user value, but bundling Spotify and third-party patch payloads adds redistribution/licensing and staleness risk. LibreSpot already reuses a verified local cache for offline installs.
- **Generic v2 migration engine before a v2 artifact exists:** [Rejected] every owned schema is currently version 1. First make ownership/version policy complete and executable; add N-1 migrations with the first concrete schema change rather than inventing synthetic transforms.
- **Allow Defender exclusions for fewer false positives:** [Rejected] SpotX's upstream option weakens endpoint protection for Spotify and the PowerShell host. LibreSpot's trust posture requires an explicit hard opt-out, not a convenience toggle.

## Sources

### Direct OSS ecosystem

- https://github.com/SpotX-Official/SpotX
- https://github.com/SpotX-Official/SpotX/compare/550bc72cd15f6e2a172a6ecc0873d0991eb1c83c...main
- https://github.com/SpotX-Official/SpotX/commit/afb4c3fcd13807679fc3ffdb9fbe963edc552d15
- https://github.com/SpotX-Official/SpotX/issues/878
- https://github.com/SpotX-Official/SpotX-Bash
- https://github.com/spicetify/cli
- https://github.com/spicetify/cli/releases/tag/v2.44.0
- https://github.com/spicetify/marketplace
- https://github.com/spicetify/marketplace/releases/tag/v1.0.9
- https://github.com/spicetify/marketplace/wiki
- https://github.com/spicetify/spicetify-themes
- https://github.com/Nuzair46/BlockTheSpot-Installer
- https://github.com/ohitstom/spicetify-easyinstall
- https://github.com/ReVanced/revanced-manager

### Community signal

- https://www.reddit.com/r/spicetify/comments/1umtw10/spicetify_not_working_after_update/

### Adjacent and commercial products

- https://github.com/microsoft/winget-cli/releases
- https://ninite.com/help/features/
- https://www.pdq.com/pdq-deploy/
- https://learn.microsoft.com/en-us/intune/app-management/

### Standards and research

- https://theupdateframework.github.io/specification/v1.0.19/
- https://slsa.dev/spec/v1.0/levels
- https://opentelemetry.io/docs/languages/dotnet/logs/correlation/
- https://www.usenix.org/conference/usenixsecurity26/technical-sessions
- https://www.usenix.org/system/files/conference/osdi14/osdi14-paper-pillai.pdf

### Dependencies and advisories

- https://github.com/serilog/serilog/releases/tag/v4.4.0
- https://github.com/CommunityToolkit/dotnet/releases/tag/v8.4.2
- https://github.com/lepoco/wpfui
- https://github.com/codebude/QRCoder/releases
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
- https://github.com/advisories

## Open Questions

None.
