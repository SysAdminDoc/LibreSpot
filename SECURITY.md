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
