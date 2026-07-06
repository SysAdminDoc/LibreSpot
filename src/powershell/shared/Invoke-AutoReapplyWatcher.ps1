function Invoke-AutoReapplyWatcher {
    # -Watch entry point. Returns an exit code to satisfy schtasks reporting.
    Write-WatcherLog "--- Watcher tick ---"

    $currentVersion = Get-InstalledSpotifyVersion
    if (-not $currentVersion) {
        Write-WatcherLog "Spotify not installed - skipping."
        return 0
    }

    $state = Get-WatcherState

    # First-ever run: record the version and do nothing. Reapplying on the
    # first tick would clobber a freshly-installed unconfigured Spotify.
    if (-not $state.LastKnownVersion) {
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Initialized' }
        Write-WatcherLog "Initialized last-known version to $currentVersion (no reapply this tick)"
        return 0
    }

    if ($currentVersion -eq $state.LastKnownVersion) {
        Write-WatcherLog "Spotify still at $currentVersion - nothing to do"
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'UpToDate' }
        return 0
    }

    Write-WatcherLog "Spotify version bump: $($state.LastKnownVersion) -> $currentVersion" -Level 'STEP'

    if (Test-SpotifyRunning) {
        Write-WatcherLog "Spotify is running - deferring reapply to next tick"
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'DeferredSpotifyRunning' }
        return 0
    }

    $saved = $null
    try { $saved = Load-LibreSpotConfig } catch { Write-WatcherLog "Config load failed: $($_.Exception.Message)" -Level 'ERROR' }
    if (-not $saved) {
        Write-WatcherLog "No saved LibreSpot config - cannot reapply automatically" -Level 'WARN'
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'NoConfig' }
        return 0
    }
    $saved = Normalize-LibreSpotConfig -Config $saved

    if (-not (ConvertTo-ConfigBoolean -Value $saved['AutoReapply_Enabled'] -Default $false)) {
        Write-WatcherLog 'Auto-reapply preference is off; skipping.'
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'PreferenceOff' }
        return 0
    }

    try {
        Invoke-HeadlessReapply -Config $saved
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Reapplied' }
        return 0
    } catch {
        Write-WatcherLog "Reapply failed: $($_.Exception.Message)" -Level 'ERROR'
        # Keep LastKnownVersion unchanged so we'll retry next tick.
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = "Error: $($_.Exception.Message)" }
        return 1
    }
}
