# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| v3.7.x (PowerShell script) | Yes |
| v4.0.x-preview (WPF desktop shell) | Best-effort |
| < v3.7.0 | No |

## Reporting a Vulnerability

**Do not open a public issue for security vulnerabilities.**

To report a vulnerability, use [GitHub private vulnerability reporting](https://github.com/SysAdminDoc/LibreSpot/security/advisories/new).

Include:

- A description of the vulnerability and its impact
- Steps to reproduce or a proof of concept
- Your LibreSpot version (`$global:VERSION` in the script, or Help > About in the WPF shell)
- Your Windows version and Spotify version
- Whether the issue requires administrator privileges to exploit

### What to expect

- Acknowledgment within 72 hours
- Status update within 7 days
- Fix or mitigation timeline communicated within 14 days
- Credit in the release notes (unless you prefer anonymity)

### Scope

The following are **in scope**:

- Arbitrary code execution via LibreSpot's download, patching, or script execution paths
- Privilege escalation beyond the requested UAC elevation
- Hash verification bypass (SHA256 checks on SpotX, Spicetify CLI, extensions, themes)
- Credential or token exposure in logs, crash dumps, or config files
- Unsafe file operations (symlink attacks, path traversal, TOCTOU races)
- Scheduled task manipulation (the ReapplyWatcher task)

The following are **out of scope**:

- Vulnerabilities in Spotify itself (report to [Spotify](https://hackerone.com/spotify))
- Vulnerabilities in SpotX (report to [SpotX-Official/SpotX](https://github.com/SpotX-Official/SpotX/issues))
- Vulnerabilities in Spicetify CLI (report to [spicetify/cli](https://github.com/spicetify/cli/issues))
- Social engineering attacks requiring the user to run arbitrary PowerShell
- Issues requiring physical access to the machine
- Denial of service against local Spotify installations (LibreSpot modifies local files by design)

### Upstream dependency issues

If you discover a vulnerability in SpotX, Spicetify CLI, or a community extension/theme that LibreSpot retrieves, please report it to the upstream project first. If the upstream is unresponsive after 14 days, you may report it here and we will disable or pin around the affected component.

## Host platform advisories

### CVE-2025-54100 — Windows PowerShell 5.1 web-content RCE

[CVE-2025-54100](https://nvd.nist.gov/vuln/detail/CVE-2025-54100) is a remote-code-execution flaw (CVSS 7.8) in Windows PowerShell 5.1's handling of web content, fixed in the **December 2025 Windows cumulative updates**. Content fetched by `Invoke-WebRequest` can execute at parse time on an unpatched host — the same download primitive LibreSpot uses to retrieve SpotX, Spicetify CLI, extensions, and themes.

**Mitigations in LibreSpot:**

- **SHA256 pinning** — every download is verified against a pinned hash before use. This guarantees the *integrity* of the payload (a tampered or substituted file is rejected) but does **not** by itself remove the parse-time execution vector on an unpatched PowerShell 5.1 host.
- **Patch-level preflight** — the downloader runs a non-blocking check (`Get-DownloaderCveExposure`) the first time it fetches anything. On Windows PowerShell 5.1 (Desktop edition) it inspects the host's most recent Windows update and logs a `WARN` when the host predates the December 2025 patch wave. It never blocks the install — it tells you to update Windows.
- **PowerShell 7+ is unaffected** — PowerShell 7 (Core) is a separate product and is out of scope for this CVE; the preflight skips it.

**Required action for users:** keep Windows fully updated. Hosts on the December 2025 cumulative update or later have the fix; older hosts should install pending updates before running LibreSpot's `irm … | iex` quickstart.

## Supply-chain hygiene

LibreSpot runs the [OpenSSF Scorecard](https://github.com/ossf/scorecard) action weekly and on pushes to `main` ([.github/workflows/scorecard.yml](.github/workflows/scorecard.yml)). It publishes results to the public Scorecard API (the badge in the README) and uploads both SARIF and JSON reports as build artifacts. This complements the existing release-time controls (commit-pinned dependencies, SHA256 checksums, CycloneDX SBOM, and GitHub provenance attestations).

Scorecard findings are treated as work, not noise: a low score on any check should become a `ROADMAP.md` item with a remediation plan rather than a silently ignored warning. Each run compares results against a checked-in baseline ([schemas/scorecard-baseline.json](schemas/scorecard-baseline.json)), writes the triage table to the GitHub Actions job summary, uploads the SARIF/JSON/triage artifacts, and fails on unaccepted floor regressions.

**Accepted single-maintainer limits:** the following Scorecard checks score zero or low and are documented as expected for a single-maintainer project — they are not silently ignored:

| Check | Score | Reason |
|-------|-------|--------|
| Branch-Protection | 0 | Required reviews are not practical without additional contributors. Branch protection is enabled with admin enforcement, force-push and deletion disabled. |
| Code-Review | 0 | All commits are direct pushes. Quality is maintained through CI, static analysis, and research/build machine separation. |
| Contributors | 0 | Single-maintainer project by design. |
| CII-Best-Practices | 0 | Enrollment deferred until v4.0 stable ships and community adoption grows. |
| Fuzzing | 0 | Property-based testing (FsCheck) is planned; OSS-Fuzz enrollment is deferred. |
| Signed-Releases | — | SignPath Foundation enrollment is pending. Releases ship with checksums, SBOM, and provenance attestations but are not yet Authenticode-signed. |

These limits are revisited when their documented trigger conditions are met (e.g., a second maintainer joins, signing completes). The full accepted-risk registry is in [schemas/scorecard-baseline.json](schemas/scorecard-baseline.json).

## Legal contingency

LibreSpot is a wrapper and orchestrator — it does not host, redistribute, or modify the code of SpotX, Spicetify CLI, or Spotify itself. All upstream code is downloaded directly from its official GitHub repository using commit-pinned URLs with SHA256 verification. LibreSpot is MIT-licensed, uses no Spotify API Client IDs, and makes no network requests except to GitHub (for downloads) and Spotify (normal app traffic through the unmodified Spotify client).

### If SpotX is taken down

If the SpotX repository (`SpotX-Official/SpotX`) is removed or DMCA'd:
- LibreSpot's pinned download URLs will fail. The installer will report a download error with the specific hash-verification failure and will not proceed with patching.
- LibreSpot will **not** silently fall back to an alternative source. Users will see a clear error explaining that the upstream SpotX project is unavailable.
- Existing Spotify installations that were already patched will continue to work until Spotify auto-updates override the patches.
- Users can restore stock Spotify at any time using **Maintenance > Full Reset** or manually by reinstalling Spotify from [spotify.com/download](https://www.spotify.com/download/).
- Spicetify theming and extensions will continue to work independently of SpotX.

### If Spicetify is taken down

If the Spicetify CLI repository (`spicetify/cli`) is removed:
- LibreSpot's Spicetify CLI download will fail. The installer will skip Spicetify setup and report the failure clearly.
- SpotX ad-blocking will continue to work independently of Spicetify.
- Users with existing Spicetify installations can run `spicetify restore` to remove theming, or use **Maintenance > Restore vanilla Spotify** in LibreSpot.
- The Spicetify Marketplace, themes archive, and community extensions are hosted in separate repositories — a Spicetify CLI takedown would not necessarily affect those, but LibreSpot would not be able to apply them without the CLI.

### Restoring stock Spotify without LibreSpot

If LibreSpot itself becomes unavailable, users can restore an unmodified Spotify client manually:
1. Run `spicetify restore` in a terminal (if Spicetify CLI is still installed) to undo theme/extension injection.
2. Uninstall Spotify: **Settings > Apps > Spotify** or run `%APPDATA%\Spotify\Spotify.exe /UNINSTALL /SILENT`.
3. Delete residual data: remove `%APPDATA%\Spotify`, `%LOCALAPPDATA%\Spotify`, `%APPDATA%\spicetify`, and `%LOCALAPPDATA%\spicetify`.
4. Reinstall Spotify from [spotify.com/download](https://www.spotify.com/download/).
5. Remove the LibreSpot ReapplyWatcher scheduled task if registered: `schtasks /Delete /TN "LibreSpot\ReapplyWatcher" /F`.

### Spotify's enforcement posture (as of mid-2026)

Spotify has taken enforcement actions against tools that redistribute patched binaries or unlock premium features: 520 GitHub repos were DMCA'd in August 2025, and ReVanced's premium-unlock patch was specifically targeted. Desktop tools that focus on ad-blocking and UI customization (SpotX, Spicetify) have not been targeted and remain live. However, Spotify's January 2026 server-side dual-sync verification killed mobile mod APKs (xManager archived, ReVancedXposed archived), demonstrating that enforcement can escalate. See the README's [Trust & risk disclosure](#trust--risk-disclosure) section for current details. LibreSpot's session-stability canary (20-second post-launch monitor) will warn if desktop enforcement expands.

## External process execution contract

LibreSpot shells out to a few external programs. The boundary that keeps this safe is: **arguments are either fixed literal flags or values that `Normalize-LibreSpotConfig` has already constrained to an allowlist or an integer.** A crafted `config.json` cannot turn a setting into an extra command.

| Executable / script | Argument source | Quoting / execution | Timeout | Output | Exit handling |
|---|---|---|---|---|---|
| SpotX `run.ps1` | `Build-SpotXParams` — fixed flags plus four normalized fields (`SpotX_LyricsTheme` → 27-value allowlist, `SpotX_DownloadMethod` → `{curl,webclient}`, `SpotX_SpotifyVersionId` → manifest-id allowlist that selects a manifest-supplied version, `SpotX_CacheLimit` → integer 0–50000) | WPF backend uses `ProcessStartInfo.ArgumentList`; the PowerShell backend uses a single-string `Start-Process -ArgumentList` (a Windows PowerShell 5.1 redirected-output quirk) over the generated path | `Invoke-ExternalScriptIsolated` (600s default, killed on overrun) | Streamed via `Read-ProcessOutputDelta` | Exit code checked, then post-patch markers verified (`Get-SpotXPatchVerification`) |
| Spicetify CLI | `Invoke-SpicetifyCli` — fixed verbs/flags; list values are allowlisted extension/theme names | `Start-Process -ArgumentList` over the resolved `spicetify.exe` | bounded waits | captured | exit code checked, auto-restore on apply failure |
| `schtasks.exe` | Fixed task name `LibreSpot\ReapplyWatcher` | `ProcessStartInfo.ArgumentList` | 1500ms (off the UI thread) | captured | exit code → registered/not |

**Rule for new arguments:** any new SpotX/Spicetify argument that carries a user-controlled value must be normalized to an allowlist or integer in `Normalize-LibreSpotConfig` **before** it reaches the parameter builder, or it must use tokenized execution (`ArgumentList`) with explicit escaping. The regression tests `Normalize_ConstrainsSpotXInterpolatedFieldsToAllowlistsOrIntegers` and `BuildSpotXParams_OnlyInterpolatesKnownSafeNormalizedFields` (both PowerShell paths) fail if a new free-form interpolation is added without updating this contract.

### Execution policy and application control

LibreSpot runs its own generated scripts, SpotX, and the watcher task with `-ExecutionPolicy Bypass`. PowerShell execution policy is a **safety feature, not a security boundary** ([Microsoft docs](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_execution_policies)) — it stops accidental script execution, it does **not** stop a determined user, and bypassing it does **not** weaken any enterprise control. In particular, `-ExecutionPolicy Bypass` does **not** defeat AppLocker or Windows Defender Application Control (WDAC), which enforce PowerShell [ConstrainedLanguage mode](https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_language_modes) regardless of execution policy.

On a host where application control is enforced, LibreSpot's scripts (or the SpotX child process) may be blocked. LibreSpot diagnoses this rather than presenting it as a generic failure: at run start it logs the PowerShell edition, version, language mode, and execution-policy scopes (`Get-PowerShellSecurityContext`), warns when the host is already in ConstrainedLanguage, and classifies app-control errors in spawned-process output (`Test-IsLanguageModeOrAppControlError`). The guidance is always to ask an administrator to allow LibreSpot/SpotX — never to weaken application control.
