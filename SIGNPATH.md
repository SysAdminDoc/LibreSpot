# SignPath Foundation setup — LibreSpot

This file tracks LibreSpot's enrollment in the [SignPath Foundation free OSS code-signing program](https://signpath.org/). The goal is to have every `LibreSpot.exe` / `LibreSpot-Desktop.exe` in a GitHub release signed by a cert whose publisher Windows SmartScreen recognizes, instead of users hitting the "Unknown publisher" warning on every install.

Approval is usually **1–2 weeks async**. This doc exists so we can prepare the repo side while that queue moves.

---

## Part 1 — Application answers (draft these into signpath.org/apply)

Copy-paste ready. Update the maintainer email on submit if you want that field to differ from public git history.

### Project identity
- **Project name**: `LibreSpot`
- **Project URL**: `https://github.com/SysAdminDoc/LibreSpot`
- **License**: MIT (see `LICENSE` in the repo)
- **Primary maintainer**: Matthew Parker — `matt@mavenimaging.com` (or whatever contact address you want SignPath notifications to land at)

### Short description (1–2 sentences)
> LibreSpot is a single-window Windows installer that combines [SpotX](https://github.com/SpotX-Official/SpotX) ad-blocking and [Spicetify](https://github.com/spicetify) theming/extension management into one GUI, so a non-technical Spotify user can reach a patched, themed client in one click rather than running multiple PowerShell scripts in sequence.

### Long description
> LibreSpot wraps two open-source Spotify-customization projects (SpotX and Spicetify) in a Windows-native installer with three modes: Easy (one-click defaults), Custom (full SpotX flag surface + Spicetify theme picker + extension toggles), and Maintenance (backups, reapply after Spotify auto-updates, full uninstall). Distributed as a single-file PowerShell monolith compiled via PS2EXE, with a .NET 8 WPF desktop shell in preview that wraps the same backend. Pinned SpotX / Spicetify CLI / Marketplace / Themes versions are SHA256-verified on every download. Ships a scheduled-task-based auto-reapply watcher that notices Spotify's silent auto-updates and re-patches unattended.
>
> Target audience is ~50k Windows users per year who currently either manually run SpotX + Spicetify scripts (error-prone, especially when Spotify auto-updates) or fall back to ad-filled official Spotify. LibreSpot raises the floor for that audience and is strictly a wrapper — it downloads both dependencies from their official upstream repos and does not host or redistribute any SpotX or Spicetify code.

### Usage / what you want signed
> Every tagged release (`v*.*.*` for the PowerShell track and `v*.*.*-preview.*` for the WPF shell) produces two Windows PE artifacts: `LibreSpot.exe` (PS2EXE-compiled PowerShell monolith, ~70 MB) and `LibreSpot-Desktop.exe` (.NET 8 WPF self-contained single-file executable, ~120 MB). Both are self-elevating and install on Windows 10 22H2 or newer. Release cadence today is 1–4 tags per month during active development. Both need to be signed by an EV-quality publisher so Windows SmartScreen stops blocking first-run launches.

### Why signing helps your users
> SmartScreen's "Unknown publisher" dialog is the single biggest friction point for new LibreSpot users: the app is an admin-elevating installer, so the default UAC prompt plus an opaque publisher name reads as sketchy even though every download the app performs is SHA256-verified against pinned hashes and every release already ships SBOM + SLSA L3 in-toto attestations via GitHub's [attest-build-provenance](https://github.com/actions/attest-build-provenance). A recognizable signed publisher closes that credibility gap.

### Build system disclosure
> All releases build on GitHub-hosted runners (`windows-latest` + `ubuntu-latest`) via `.github/workflows/release.yml` on tag push. The workflow runs a preflight job that asserts version coherence between the three version sources, then runs `dotnet test`, then builds PS2EXE + `dotnet publish`, then assembles the release. No self-hosted runners, no pre-built artifacts injected.

### Public-trust evidence to link
- GitHub repo: https://github.com/SysAdminDoc/LibreSpot
- Release history: https://github.com/SysAdminDoc/LibreSpot/releases
- CHANGELOG: https://github.com/SysAdminDoc/LibreSpot/blob/main/CHANGELOG.md
- Roadmap: https://github.com/SysAdminDoc/LibreSpot/blob/main/ROADMAP.md

---

## Part 2 — Repo-side prep (already landed, no action until approved)

When SignPath approves the project they'll send three identifiers:
- An **Organization ID** (GUID)
- A **Project slug** (typically `librespot`)
- One or more **signing-policy slugs** (we'll use `release-signing` for tag builds)

Plus they'll issue one secret:
- A **SignPath API token** for the submitter user

### Repository settings to configure once

**GitHub repo → Settings → Secrets and variables → Actions:**

| Kind     | Name                            | Value                                                   |
|----------|---------------------------------|---------------------------------------------------------|
| Secret   | `SIGNPATH_API_TOKEN`            | (issued by SignPath after approval)                     |
| Variable | `SIGNPATH_ORGANIZATION_ID`      | GUID from the SignPath dashboard                        |
| Variable | `SIGNPATH_PROJECT_SLUG`         | Project slug (likely `librespot`)                       |
| Variable | `SIGNPATH_RELEASE_POLICY_SLUG`  | `release-signing`                                       |

Set these via `gh`:

```bash
gh secret   set SIGNPATH_API_TOKEN
gh variable set SIGNPATH_ORGANIZATION_ID     --body '<guid>'
gh variable set SIGNPATH_PROJECT_SLUG        --body 'librespot'
gh variable set SIGNPATH_RELEASE_POLICY_SLUG --body 'release-signing'
```

### Workflow changes

`.github/workflows/release.yml` already contains conditional signing steps that fire **only** when `SIGNPATH_API_TOKEN` is present. Until you set the secret, every existing tag release works exactly as it does today (unsigned artifacts, SBOM + SLSA attestations). After you set the secret, signing inserts itself between the build jobs and the release-assembly job automatically.

The action we use is [`signpath/github-action-submit-signing-request@v2`](https://github.com/signpath/github-action-submit-signing-request). `wait-for-completion: true` blocks the job until SignPath returns the signed artifact, so the release-assembly step sees signed `.exe`s when SignPath is enabled.

### SignPath dashboard configuration

Once logged into the SignPath web UI after approval:

1. Link **`SysAdminDoc/LibreSpot`** as a Trusted Build System (GitHub is a built-in integration).
2. Create an **Artifact Configuration** that accepts the two `.exe` files. A minimal configuration just whitelists the filenames `LibreSpot.exe` and `LibreSpot-Desktop.exe`.
3. Create a **Signing Policy** named `release-signing` with:
   - `disallow_reruns: true` — stops an attacker with stale push access from re-running an old workflow and signing historical code.
   - `require_github_hosted: true` — mandatory for the free OSS tier anyway.
   - Approval: **Automatic** on the signing policy for `release-signing` (no human-in-the-loop needed since the workflow is triggered only by tag push which requires write access to the repo). You can tighten to manual approval later if the threat model demands it.

---

## Part 3 — Verifying a signed release

After the first signed tag ships:

```powershell
# Check digital signature
Get-AuthenticodeSignature .\LibreSpot.exe          | Format-List
Get-AuthenticodeSignature .\LibreSpot-Desktop.exe  | Format-List

# Verify SignPath Foundation is the signer
# (Subject should be "CN=SignPath Foundation ...")
(Get-AuthenticodeSignature .\LibreSpot.exe).SignerCertificate.Subject

# Confirm the provenance attestation still passes (independent of signing)
gh attestation verify .\LibreSpot.exe          -R SysAdminDoc/LibreSpot
gh attestation verify .\LibreSpot-Desktop.exe  -R SysAdminDoc/LibreSpot
```

The signer identity will read **"SignPath Foundation"**, not "SysAdminDoc" / "Matthew Parker" — that's how the free OSS tier works. The cert's Enhanced Key Usage + the associated SignPath project record jointly prove the binary was built from `SysAdminDoc/LibreSpot`. Users who care can open the certificate details from the Properties dialog and see the project-specific metadata; SmartScreen just cares that it's a known good publisher.

---

## Troubleshooting

**SmartScreen still warns after signing.** SignPath Foundation's cert has an established reputation, but individual binary reputation still builds per-file-hash. Give it ~24–48 hours after the first signed release drops; SmartScreen silently accumulates install events and promotes a new hash once it crosses an internal threshold. EV certs bypass that reputation period but require a hardware token + verified business identity that the free tier does not provide.

**Signing request stuck as "Waiting for approval."** The `release-signing` policy's Approval Setting defaults to **Manual** in the SignPath web UI; change it to **Automatic** for tag-triggered workflows.

**SignPath returns "origin verification failed."** The `SIGNPATH_ORGANIZATION_ID` or `SIGNPATH_PROJECT_SLUG` variable doesn't match the dashboard, OR the repo isn't linked as a trusted build system, OR the workflow is running on a fork/PR branch. None of these should affect tag-triggered builds on the main repo.

**Release workflow fails on `signpath/github-action-submit-signing-request`.** Confirm `SIGNPATH_API_TOKEN` has **Submitter** permissions in SignPath (not just Viewer). The token must belong to a user account with submit rights on the signing policy you named.
