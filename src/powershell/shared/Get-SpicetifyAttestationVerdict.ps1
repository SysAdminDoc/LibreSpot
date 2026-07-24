function Get-SpicetifyAttestationVerdict {
    # Pure: turn a `gh attestation verify` exit code + combined output into a
    # trust verdict, layered on top of the mandatory SHA256 gate. Never throws.
    #   'Verified'    - gh confirmed provenance against the pinned signer identity.
    #   'Mismatch'    - gh ran and provenance verification FAILED (trust warning).
    #   'Unavailable' - tooling/network/auth problem; fall back to SHA256-only.
    # Only a clear verification-failure signal in the output maps to 'Mismatch';
    # every other non-zero result (network, auth, rate-limit, missing tooling) is
    # 'Unavailable' so a best-effort provenance check never fails the install closed.
    param(
        [int]$ExitCode,
        [string]$Output
    )
    if ($ExitCode -eq 0) { return 'Verified' }
    $text = [string]$Output
    if ($text -match '(?i)(verification failed|failed to verify|no attestations (were )?found|does not match|no matching attestations|failed to verify signature)') {
        return 'Mismatch'
    }
    return 'Unavailable'
}
