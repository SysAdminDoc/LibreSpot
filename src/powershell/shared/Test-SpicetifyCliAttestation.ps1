function Test-SpicetifyCliAttestation {
    # Best-effort GitHub build-provenance check for the pinned Spicetify CLI
    # download, layered on top of the mandatory SHA256 gate (Confirm-FileHash).
    # Delegates the cryptography to `gh attestation verify` when the GitHub CLI is
    # present, checking the artifact against the cached signer identity in
    # $global:PinnedReleases.SpicetifyCLI.Attestation (repo + cert-identity regex +
    # OIDC issuer). Never throws and never fails the install closed: returns
    # 'Verified', 'Mismatch' (a real provenance failure worth warning about), or
    # 'Unavailable' (no gh / no network / no attestation tooling -> SHA256-only).
    param(
        [Parameter(Mandatory)][string]$Path,
        $Attestation
    )
    if (-not $Attestation) { return 'Unavailable' }
    $repo = [string]$Attestation.Repo
    if ([string]::IsNullOrWhiteSpace($repo)) { return 'Unavailable' }
    if (-not (Test-Path -LiteralPath $Path)) { return 'Unavailable' }

    $gh = Get-Command -Name 'gh' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $gh) { return 'Unavailable' }

    $ghArgs = @('attestation', 'verify', $Path, '--repo', $repo)
    if (-not [string]::IsNullOrWhiteSpace([string]$Attestation.CertIdentityRegex)) {
        $ghArgs += @('--cert-identity-regex', [string]$Attestation.CertIdentityRegex)
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$Attestation.OidcIssuer)) {
        $ghArgs += @('--cert-oidc-issuer', [string]$Attestation.OidcIssuer)
    }

    try {
        $output = & $gh.Source @ghArgs 2>&1
        $code = $LASTEXITCODE
    } catch {
        return 'Unavailable'
    }
    return Get-SpicetifyAttestationVerdict -ExitCode $code -Output ($output | Out-String)
}
