function Module-ApplySpicetify { param($Config)
    Write-Log "Applying Spicetify changes..." -Level 'STEP'
    if ($Config.Spicetify_Theme -eq '(None - Marketplace Only)') {
        try {
            Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '0', 'replace_colors', '0', 'overwrite_assets', '0', 'inject_theme_js', '0', '--bypass-admin') -FailureMessage 'Could not disable theme asset injection for the Marketplace-only setup.'
        } catch {
            Write-Log "Pre-apply config tweak failed: $($_.Exception.Message)" -Level 'WARN'
        }
    }

    $diag = Get-SpicetifyDiagnosticSnapshot
    foreach ($key in $diag.Keys) {
        Write-Log "  diag: $key = $($diag[$key])"
    }

    Write-Log "Ensuring Spotify is fully closed before patching files..."
    Stop-SpotifyProcesses -MaxAttempts 3

    # Spicetify expects `backup apply` as a combined invocation — especially after
    # SpotX has patched the client (version mismatch between Spotify and any prior
    # backup). Running them separately causes "version mismatch" failures.
    $applyError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('backup', 'apply', '--bypass-admin') -FailureMessage 'Could not apply the selected Spicetify setup.'
        Write-Log "Spicetify applied successfully."
        return
    } catch {
        $applyError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown Spicetify apply error.' }
        Write-Log "Spicetify backup apply failed: $applyError" -Level 'WARN'
    }

    Write-Log "Attempting rollback to keep Spotify usable..." -Level 'WARN'
    $restoreError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage 'Could not restore Spotify after the failed apply.'
    } catch {
        $restoreError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown restore error.' }
    }

    if ([string]::IsNullOrWhiteSpace($restoreError)) {
        throw "Spicetify apply failed but LibreSpot restored Spotify to a usable state. Apply error: $applyError"
    }

    throw "Spicetify apply failed and rollback also failed. Apply error: $applyError | Rollback error: $restoreError"
}
