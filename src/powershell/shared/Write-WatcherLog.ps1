function Write-WatcherLog {
    param([string]$Message, [string]$Level = 'INFO')
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        $line = "[{0}] [{1}] {2}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::AppendAllText($global:WATCHER_LOG_PATH, $line + [Environment]::NewLine, $utf8NoBom)
        # Trim the watcher log when it exceeds ~1 MB so an unattended machine
        # can't fill the disk with 15-minute polling entries.
        if ((Get-Item -LiteralPath $global:WATCHER_LOG_PATH).Length -gt 1048576) {
            $keep = Get-Content -LiteralPath $global:WATCHER_LOG_PATH -Tail 500
            [System.IO.File]::WriteAllLines($global:WATCHER_LOG_PATH, $keep, $utf8NoBom)
        }
    } catch {}
}
