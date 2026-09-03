# SignPath Foundation setup for LibreSpot

This file records LibreSpot's **evaluation** of the [SignPath Foundation free OSS code-signing program](https://signpath.org/). LibreSpot ships **unsigned by design** and does not pursue code signing. SignPath enrollment was considered as a way to have the Windows executable release assets (`LibreSpot.exe`, `LibreSpot-Desktop.exe`, and `LibreSpot.Cli.exe`) recognized by Windows SmartScreen, then deliberately set aside, there is no pending certificate and no "Unknown publisher" fix is expected from signing. Integrity is verified instead via the SHA256 `checksums.txt` published with each release.

This document is retained for historical context, not as an active enrollment plan. The inputs below are the answers that *would* be submitted if the unsigned-by-design decision were ever reversed.

---

## Part 1: Application answers

Copy-paste ready. Update the maintainer email on submit if you want that field to differ from public git history.

### Project identity

- **Project name**: `LibreSpot`
- **Project URL**: `https://github.com/SysAdminDoc/LibreSpot`
- **License**: MIT (see `LICENSE` in the repo)
- **Primary maintainer**: Matthew Parker, `matt@mavenimaging.com` (or whatever contact address you want SignPath notifications to land at)

### Short description

> LibreSpot is a single-window Windows installer that combines [SpotX](https://github.com/SpotX-Official/SpotX) ad-blocking and [Spicetify](https://github.com/spicetify) theming/extension management into one GUI, so a non-technical Spotify user can reach a patched, themed client in one click rather than running multiple PowerShell scripts in sequence.

### Long description

> LibreSpot wraps two open-source Spotify-customization projects (SpotX and Spicetify) in a Windows-native installer with three modes: Easy (one-click defaults), Custom (full SpotX flag surface + Spicetify theme picker + extension toggles), and Maintenance (backups, reapply after Spotify auto-updates, full uninstall). Distributed as a single-file PowerShell monolith compiled via PS2EXE, with a .NET 10 WPF desktop shell and a .NET 10 fleet CLI preview that wrap the same backend contracts. Pinned SpotX / Spicetify CLI / Marketplace / Themes versions are SHA256-verified on every download. Ships a scheduled-task-based auto-reapply watcher that notices Spotify's silent auto-updates and re-patches unattended.
>
> Target audience is Windows users who currently either manually run SpotX + Spicetify scripts or fall back to ad-filled official Spotify. LibreSpot raises the floor for that audience and is strictly a wrapper: it downloads dependencies from their official upstream repos and does not host or redistribute SpotX or Spicetify code.

### Usage / what you want signed

> Tagged releases produce up to three Windows PE artifacts: `LibreSpot.exe` (PS2EXE-compiled PowerShell monolith), `LibreSpot-Desktop.exe` (.NET 10 WPF self-contained single-file executable), and `LibreSpot.Cli.exe` (.NET 10 console-capable fleet executable). The GUI artifacts are self-elevating; the CLI is intended for endpoint tooling and local validation flows. All three need to be signed by a trusted publisher so Windows SmartScreen stops blocking first-run launches.

### Why signing helps your users

> SmartScreen's "Unknown publisher" dialog is the single biggest friction point for new LibreSpot users: the app is an admin-elevating installer, so the default UAC prompt plus an opaque publisher name reads as sketchy even though every download the app performs is SHA256-verified against pinned hashes and local releases ship checksums, a release manifest, and CycloneDX SBOM output. A recognizable signed publisher closes that credibility gap.

### Build system disclosure

> Releases are built locally by the maintainer because the repository intentionally does not track build or release workflows. The local release process runs version coherence checks, `dotnet test`, PS2EXE packaging, `dotnet publish`, checksum generation, SBOM generation, and a post-upload release audit against `schemas/release-artifact-contract.json`. GitHub Actions build-provenance attestations are not produced by the current process. New releases are published as immutable GitHub releases, which generate a Sigstore-verifiable release attestation. Verify the release with `gh release verify <tag>` and each downloaded asset with `gh release verify-asset <tag> <local-asset-path>`.

### Public-trust evidence to link

- GitHub repo: https://github.com/SysAdminDoc/LibreSpot
- Release history: https://github.com/SysAdminDoc/LibreSpot/releases
- CHANGELOG: https://github.com/SysAdminDoc/LibreSpot/blob/main/CHANGELOG.md
- Roadmap: https://github.com/SysAdminDoc/LibreSpot/blob/main/ROADMAP.md

---

## Part 2: Operator-side prep

When SignPath approves the project they'll send three identifiers:

- An **Organization ID** (GUID)
- A **Project slug** (typically `librespot`)
- One or more **signing-policy slugs** (use `release-signing` for production releases)

Plus they'll issue one secret:

- A **SignPath API token** for the submitter user

### Local signing inputs

Keep these values in the maintainer's local release environment or secret store, not in the repository:

| Name | Value |
|------|-------|
| `SIGNPATH_API_TOKEN` | Issued by SignPath after approval |
| `SIGNPATH_ORGANIZATION_ID` | GUID from the SignPath dashboard |
| `SIGNPATH_PROJECT_SLUG` | Project slug, likely `librespot` |
| `SIGNPATH_RELEASE_POLICY_SLUG` | Signing policy slug, likely `release-signing` |

### Local release changes signing would require (not planned)

The local release contract records signing as `unsigned-by-design` with provider `none` in `schemas/release-artifact-contract.json`; nothing is pending. Only if the unsigned-by-design decision were reversed would a local signing step be added, submitting `LibreSpot.exe`, `LibreSpot-Desktop.exe`, and `LibreSpot.Cli.exe` to SignPath after build output is produced and before checksums, SBOM subjects, and `librespot-release-manifest.json` are finalized, with the release audit then verifying Authenticode signatures. This is not on the roadmap.

### SignPath dashboard configuration

Once logged into the SignPath web UI after approval:

1. Link the local release build identity required by SignPath's current OSS program instructions. Do not assume a repository workflow integration exists until the build process changes.
2. Create an **Artifact Configuration** that accepts the three `.exe` files: `LibreSpot.exe`, `LibreSpot-Desktop.exe`, and `LibreSpot.Cli.exe`.
3. Create a **Signing Policy** named `release-signing` with manual approval until the local signing submission flow is proven and auditable.

---

## Part 3: Verifying a signed release

After the first signed tag ships:

```powershell
# Check digital signatures
Get-AuthenticodeSignature .\LibreSpot.exe          | Format-List
Get-AuthenticodeSignature .\LibreSpot-Desktop.exe  | Format-List
Get-AuthenticodeSignature .\LibreSpot.Cli.exe      | Format-List

# Verify SignPath Foundation is the signer
# (Subject should be "CN=SignPath Foundation ...")
(Get-AuthenticodeSignature .\LibreSpot.exe).SignerCertificate.Subject
```

The signer identity will read **"SignPath Foundation"**, not "SysAdminDoc" / "Matthew Parker". That's how the free OSS tier works. The cert's Enhanced Key Usage and associated SignPath project record jointly prove the binary was signed through the LibreSpot project. Users who care can open the certificate details from the Properties dialog and see the project-specific metadata; SmartScreen just cares that it's a known good publisher.

---

## Troubleshooting

**SmartScreen still warns after signing.** SignPath Foundation's cert has an established reputation, but individual binary reputation still builds per-file hash. Give it 24-48 hours after the first signed release drops; SmartScreen silently accumulates install events and promotes a new hash once it crosses an internal threshold. EV certs bypass that reputation period but require a hardware token and verified business identity that the free tier does not provide.

**Signing request stuck as "Waiting for approval."** The initial policy should stay manual until the local submission flow is verified. Approve the request in the SignPath dashboard, then document the release evidence in the local release notes.

**SignPath returns "origin verification failed."** The `SIGNPATH_ORGANIZATION_ID` or `SIGNPATH_PROJECT_SLUG` value does not match the dashboard, or the configured build identity does not match the SignPath project policy.

**Signing submission fails.** Confirm `SIGNPATH_API_TOKEN` has **Submitter** permissions in SignPath (not just Viewer). The token must belong to a user account with submit rights on the signing policy you named.
