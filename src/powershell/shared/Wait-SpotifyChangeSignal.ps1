function Wait-SpotifyChangeSignal { param([int]$TimeoutSeconds = 1680, [int]$QuietSeconds = 20, [int]$SliceMilliseconds = 1000)
    # Blocks until Spotify's own files change, or until the timeout. The scheduled
    # task repeats every 30 minutes; without this a client that updates a minute
    # after a tick stays unpatched for the rest of that half hour. The default
    # timeout sits just under the repeat interval so the task's next run takes over
    # rather than two ticks overlapping.
    #
    # FileSystemWatcher.WaitForChanged is used rather than an event handler on
    # purpose: a PowerShell script block attached to add_Changed runs on the
    # watcher's own thread, which has no runspace, so it never executes and the
    # wait never returns. WaitForChanged blocks inside .NET and returns a result.
    #
    # Returns $true when a change was seen, $false on timeout. Any failure to watch
    # returns $false so the caller simply falls back to the poll.
    $spotifyRoot = Split-Path -Parent $global:SPOTIFY_EXE_PATH
    $updateRoot = Join-Path $env:LOCALAPPDATA 'Spotify\Update'

    $watchers = New-Object System.Collections.Generic.List[System.IO.FileSystemWatcher]
    try {
        foreach ($target in @($spotifyRoot, $updateRoot)) {
            if ([string]::IsNullOrWhiteSpace($target) -or -not (Test-Path -LiteralPath $target -PathType Container)) { continue }
            try {
                $watcher = New-Object System.IO.FileSystemWatcher $target
                $watcher.IncludeSubdirectories = $false
                $watcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor [System.IO.NotifyFilters]::LastWrite -bor [System.IO.NotifyFilters]::Size
                $watchers.Add($watcher)
            } catch {
                Write-WatcherLog "Could not watch '$target': $($_.Exception.Message). The poll still covers it." -Level 'WARN'
            }
        }

        if ($watchers.Count -eq 0) {
            Write-WatcherLog 'No Spotify folder to watch; relying on the scheduled repeat.'
            return $false
        }

        # Each watcher is polled for a short slice in turn, so two folders are
        # covered with about one slice of granularity.
        $slice = [Math]::Max(50, $SliceMilliseconds)
        $changeTypes = [System.IO.WatcherChangeTypes]::Created -bor [System.IO.WatcherChangeTypes]::Changed -bor
            [System.IO.WatcherChangeTypes]::Deleted -bor [System.IO.WatcherChangeTypes]::Renamed

        Write-WatcherLog "Watching $($watchers.Count) Spotify folder(s) for up to $TimeoutSeconds seconds."
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $seen = $false
        while (-not $seen -and (Get-Date) -lt $deadline) {
            foreach ($watcher in $watchers) {
                if ((Get-Date) -ge $deadline) { break }
                if (-not $watcher.WaitForChanged($changeTypes, $slice).TimedOut) { $seen = $true; break }
            }
        }

        if (-not $seen) { return $false }

        # An update writes many files. Wait for the writes to stop before acting so
        # the reapply runs once against a settled install, not mid-copy.
        $quiet = [Math]::Max(1, $QuietSeconds) * 1000
        do {
            $settled = $true
            foreach ($watcher in $watchers) {
                if (-not $watcher.WaitForChanged($changeTypes, $quiet).TimedOut) { $settled = $false; break }
            }
        } while (-not $settled)

        Write-WatcherLog 'Spotify files changed and settled.' -Level 'STEP'
        return $true
    } finally {
        foreach ($watcher in $watchers) {
            try { $watcher.Dispose() } catch { }
        }
    }
}
