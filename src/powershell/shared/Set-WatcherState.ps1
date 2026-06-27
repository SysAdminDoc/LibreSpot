function Set-WatcherState {
    param([hashtable]$State)
    $tempPath = $null
    $backupPath = $null
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        # Use [UTF8Encoding]($false) to avoid the BOM that PS 5.1's
        # `-Encoding UTF8` produces, which can trip up ConvertFrom-Json.
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $json = $State | ConvertTo-Json -Compress
        $tempPath = Join-Path $global:CONFIG_DIR ("watcher-state.{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
        $backupPath = Join-Path $global:CONFIG_DIR ("watcher-state.{0}.bak" -f [Guid]::NewGuid().ToString('N'))
        [System.IO.File]::WriteAllText($tempPath, $json, $utf8NoBom)
        if (Test-Path -LiteralPath $global:WATCHER_STATE_PATH) {
            try {
                [System.IO.File]::Replace($tempPath, $global:WATCHER_STATE_PATH, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                Remove-Item -LiteralPath $global:WATCHER_STATE_PATH -Force -ErrorAction Stop
                [System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)
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
