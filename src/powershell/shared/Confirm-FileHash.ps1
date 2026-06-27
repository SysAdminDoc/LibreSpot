function Confirm-FileHash { param([string]$Path, [string]$ExpectedHash, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($ExpectedHash)) {
        Write-Log "  Hash verification skipped for $Label (no hash pinned)" -Level 'WARN'
        return
    }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLower()
    $expected = $ExpectedHash.ToLower()
    if ($actual -ne $expected) {
        throw "SHA256 hash mismatch for ${Label}`n  Expected: $expected`n  Actual:   $actual`n  File may be corrupted or tampered with. Update pinned hash if this is a legitimate new version."
    }
    Write-Log "  SHA256 verified: $Label"
}
