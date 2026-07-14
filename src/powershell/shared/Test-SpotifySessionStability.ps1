function Test-SpotifySessionStability {
    param([int]$WaitSeconds = 20)
    if (-not (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) { return $true }
    try {
        $procs = @(Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)
        if ($procs.Count -eq 0) { return $true }
        $initialPid = $procs[0].Id
        Start-Sleep -Seconds $WaitSeconds
        $afterProcs = @(Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)
        if ($afterProcs.Count -eq 0) {
            Write-Log "Spotify exited within ${WaitSeconds}s of patched launch. This may indicate server-side enforcement. If Spotify keeps closing after patching, use Maintenance > Restore vanilla or Full reset before retrying." -Level 'WARN'
            return $false
        }
        $afterPids = @($afterProcs | ForEach-Object { $_.Id })
        if ($afterPids -notcontains $initialPid) {
            Write-Log "Spotify restarted within ${WaitSeconds}s of patched launch (initial PID $initialPid was replaced). This may indicate server-side enforcement or a self-repair restart. If Spotify keeps restarting after patching, use Maintenance > Restore vanilla or Full reset before retrying." -Level 'WARN'
            return $false
        }
        return $true
    } catch { return $true }
}
