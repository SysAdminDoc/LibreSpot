function Set-WatcherState {
    param([hashtable]$State)
    $tempPath = $null
    $backupPath = $null
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        # Merge over the existing file so fields written by the WPF backend
        # lane (LastAppliedSpotifyVersion, LastSuccessfulApplyAt, ...) survive
        # a save from this lane. Both lanes share the same watcher-state.json.
        $merged = @{}
        if (Test-Path -LiteralPath $global:WATCHER_STATE_PATH) {
            try {
                $existing = Get-Content -LiteralPath $global:WATCHER_STATE_PATH -Raw -ErrorAction Stop | ConvertFrom-Json
                foreach ($prop in $existing.PSObject.Properties) { $merged[$prop.Name] = $prop.Value }
            } catch {}
        }
        foreach ($key in @($State.Keys)) { $merged[$key] = $State[$key] }
        # Use [UTF8Encoding]($false) to avoid the BOM that PS 5.1's
        # `-Encoding UTF8` produces, which can trip up ConvertFrom-Json.
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $json = $merged | ConvertTo-Json -Compress
        $tempPath = Join-Path $global:CONFIG_DIR ("watcher-state.{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
        $backupPath = Join-Path $global:CONFIG_DIR ("watcher-state.{0}.bak" -f [Guid]::NewGuid().ToString('N'))
        [System.IO.File]::WriteAllText($tempPath, $json, $utf8NoBom)
        if (Test-Path -LiteralPath $global:WATCHER_STATE_PATH) {
            try {
                [System.IO.File]::Replace($tempPath, $global:WATCHER_STATE_PATH, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                $rescuePath = "$($global:WATCHER_STATE_PATH).rescue"
                Move-Item -LiteralPath $global:WATCHER_STATE_PATH -Destination $rescuePath -Force
                try {
                    [System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)
                    Remove-Item -LiteralPath $rescuePath -Force -ErrorAction SilentlyContinue
                } catch {
                    Move-Item -LiteralPath $rescuePath -Destination $global:WATCHER_STATE_PATH -Force -ErrorAction SilentlyContinue
                    throw
                }
            }
        } else {
            [System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)
        }
    } catch {
        if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
        if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
        Write-WatcherLog "State save failed: $($_.Exception.Message)" -Level 'WARN'
    }
}
