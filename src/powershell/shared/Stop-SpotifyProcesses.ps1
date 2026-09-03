function Stop-SpotifyProcesses { param([int]$MaxAttempts=5,[int]$RetryDelay=500,[int]$CloseWaitMs=5000,[int]$PollIntervalMs=100)
    # Ask every windowed Spotify process to close normally, wait a bounded
    # interval, then force only what survived plus the windowless helpers.
    # Every decision is logged with the process name, PID, elapsed time, and
    # reason so the run log explains the fallback without user data.
    $names = @('Spotify','SpotifyWebHelper','SpotifyMigrator','SpotifyCrashService')
    $procs = @(Get-Process -Name $names -EA SilentlyContinue)
    if (-not $procs) { return }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $closeRequested = @{}
    foreach ($p in $procs) {
        try {
            if ($p.HasExited) { Write-Log "$($p.ProcessName) (PID $($p.Id)): already exited before the close request after $($sw.ElapsedMilliseconds) ms."; continue }
            if ($p.MainWindowHandle -eq [IntPtr]::Zero) { Write-Log "$($p.ProcessName) (PID $($p.Id)): has no main window to receive a close request after $($sw.ElapsedMilliseconds) ms."; continue }
            if ($p.CloseMainWindow()) {
                $closeRequested[[int]$p.Id] = $true
                Write-Log "$($p.ProcessName) (PID $($p.Id)): was asked to close normally after $($sw.ElapsedMilliseconds) ms."
            } else {
                Write-Log "$($p.ProcessName) (PID $($p.Id)): refused the close request after $($sw.ElapsedMilliseconds) ms."
            }
        } catch {
            Write-Log "$($p.ProcessName) (PID $($p.Id)): exited while the close request was sent after $($sw.ElapsedMilliseconds) ms."
        }
    }
    if ($closeRequested.Count -gt 0) {
        $closePhase = [System.Diagnostics.Stopwatch]::StartNew()
        while ($closePhase.ElapsedMilliseconds -lt $CloseWaitMs) {
            $waiting = @($procs | Where-Object { $closeRequested.ContainsKey([int]$_.Id) -and -not $_.HasExited })
            if (-not $waiting) { break }
            Start-Sleep -Milliseconds $PollIntervalMs
        }
    }
    for ($a=1; $a -le $MaxAttempts; $a++) {
        $survivors = @(Get-Process -Name $names -EA SilentlyContinue | Where-Object { -not $_.HasExited })
        if (-not $survivors) { Write-Log "Spotify closed after $($sw.ElapsedMilliseconds) ms."; return }
        foreach ($s in $survivors) {
            $reason = if ($closeRequested.ContainsKey([int]$s.Id)) { "did not exit within $CloseWaitMs ms of the close request, forcing it" }
                      elseif ($s.MainWindowHandle -ne [IntPtr]::Zero) { 'refused the close request, forcing it' }
                      else { 'has no main window to receive a close request, forcing it' }
            Write-Log "$($s.ProcessName) (PID $($s.Id)): $reason after $($sw.ElapsedMilliseconds) ms (attempt $a/$MaxAttempts)." -Level 'WARN'
            try { Stop-Process -Id $s.Id -Force -EA Stop } catch {}
        }
        Start-Sleep -Milliseconds $RetryDelay
    }
    $still = Get-Process -Name "Spotify" -EA SilentlyContinue
    if ($still) { Write-Log "Some Spotify processes survived kill attempts." -Level 'WARN' }
}
