function Restore-SpotifyIfSpicetifyPresent {
    param(
        [string]$FailureMessage,
        [string]$MissingMessage
    )

    if (-not (Test-SpicetifyCliInstalled)) {
        if ($MissingMessage) {
            Write-Log $MissingMessage -Level 'WARN'
        }
        return $false
    }

    Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage $FailureMessage
    return $true
}
