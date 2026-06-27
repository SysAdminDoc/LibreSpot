function Stop-SpotifyProcesses { param([int]$MaxAttempts=5,[int]$RetryDelay=500)
    for ($a=1; $a -le $MaxAttempts; $a++) {
        $procs = Get-Process -Name "Spotify","SpotifyWebHelper","SpotifyMigrator","SpotifyCrashService" -EA SilentlyContinue
        if (-not $procs) { return }
        Write-Log "Killing Spotify processes (attempt $a/$MaxAttempts)..."
        $procs | ForEach-Object { try { Stop-Process -Id $_.Id -Force -EA Stop } catch {} }
        Start-Sleep -Milliseconds $RetryDelay
    }
    $still = Get-Process -Name "Spotify" -EA SilentlyContinue
    if ($still) { Write-Log "Some Spotify processes survived kill attempts." -Level 'WARN' }
}
