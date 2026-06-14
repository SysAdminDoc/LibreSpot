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
