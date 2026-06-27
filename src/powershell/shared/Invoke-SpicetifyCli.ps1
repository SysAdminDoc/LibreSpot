function Invoke-SpicetifyCli {
    param(
        [string[]]$Arguments,
        [string]$FailureMessage = 'Spicetify command failed.',
        [int]$TimeoutSeconds = 900,
        [int]$IdleTimeoutSeconds = 90
    )
    $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
    if (-not (Test-Path -LiteralPath $spicetifyExe)) {
        throw 'Spicetify CLI is not installed.'
    }

    $progressState = @{ LastPatchBucket = -1; LastUiPatchPercent = -1; LastStage = '' }
    $outputLines = [System.Collections.Generic.List[string]]::new()
    $process = $null
    $collector = $null

    # Keep PowerShell from turning redirected native stderr into its own
    # terminating error. The .NET process object avoids PowerShell handle
    # bugs seen with redirected files while a C# collector drains both streams
    # without running PowerShell scriptblocks on process output threads.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $argumentString = ConvertTo-NativeArgumentString -Arguments $Arguments
        $displayArguments = ($Arguments | ForEach-Object { [string]$_ }) -join ' '
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $spicetifyExe
        $startInfo.Arguments = $argumentString
        $startInfo.WorkingDirectory = Split-Path -Path $spicetifyExe -Parent
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        $collector = New-Object LibreSpotNativeOutputCollector
        $collector.Attach($process)

        $null = $process.Start()
        Write-Log "  Spicetify command: spicetify $displayArguments"
        Write-Log "  Spicetify PID: $($process.Id)"
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        $startedAt = Get-Date
        $lastOutputAt = $startedAt
        $lastHeartbeatAt = $startedAt
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $statusIntervalSeconds = if ($IdleTimeoutSeconds -gt 0) { [Math]::Min([Math]::Max($IdleTimeoutSeconds, 5), 15) } else { 15 }
        $heartbeatSeconds = [Math]::Min($statusIntervalSeconds, 10)

        $drainOutput = {
            $count = 0
            [string]$queuedLine = $null
            while ($collector.TryDequeue([ref]$queuedLine)) {
                if (-not [string]::IsNullOrWhiteSpace($queuedLine)) {
                    $processed = Write-SpicetifyCliOutputLine -Line $queuedLine -ProgressState $progressState
                    if ($processed) { [void]$outputLines.Add($processed) }
                    $count++
                }
                $queuedLine = $null
            }
            return $count
        }

        $getTail = {
            if ($outputLines.Count -le 0) { return '' }
            $start = [Math]::Max(0, $outputLines.Count - 4)
            $slice = for ($i = $start; $i -lt $outputLines.Count; $i++) { $outputLines[$i] }
            return ' Output: ' + ((($slice | ForEach-Object { Remove-ConsoleEscapeSequences -Text $_ }) -replace '\s+', ' ') -join ' | ')
        }

        while (-not $process.WaitForExit(250)) {
            $drained = & $drainOutput
            if ($drained -gt 0) { $lastOutputAt = Get-Date }

            $now = Get-Date
            if ($now -gt $deadline) {
                Write-Log "Spicetify command exceeded ${TimeoutSeconds}s timeout and will be terminated." -Level 'WARN'
                try { $process.Kill(); $process.WaitForExit(5000) } catch {}
                $tail = & $getTail
                throw "$FailureMessage Timed out after $TimeoutSeconds seconds.$tail"
            }

            if ($IdleTimeoutSeconds -gt 0 -and $now -ge $lastOutputAt.AddSeconds($IdleTimeoutSeconds)) {
                $idleSeconds = [int]($now - $lastOutputAt).TotalSeconds
                Write-Log "  Spicetify has not emitted a new line for ${idleSeconds}s; still waiting until the ${TimeoutSeconds}s hard timeout." -Level 'WARN'
                $lastOutputAt = $now
            }

            if ($now -ge $lastHeartbeatAt.AddSeconds($heartbeatSeconds)) {
                $elapsedSeconds = [int]($now - $startedAt).TotalSeconds
                $idleSeconds = [int]($now - $lastOutputAt).TotalSeconds
                Write-Log "  Spicetify still running (${elapsedSeconds}s elapsed, ${idleSeconds}s since last output)."
                Update-SpicetifyCliProgress -Line 'Patching files'
                $lastHeartbeatAt = $now
            }
        }

        Start-Sleep -Milliseconds 200
        $null = & $drainOutput

        $exitCode = $null
        try { $exitCode = $process.ExitCode } catch { $exitCode = $null }
        if ($null -eq $exitCode) {
            Write-Log 'Spicetify process finished but ExitCode was unavailable; treating as success.' -Level 'WARN'
        } elseif ($exitCode -ne 0) {
            $tail = & $getTail
            throw "$FailureMessage Exit code: $exitCode.$tail"
        } else {
            Write-Log "  Spicetify exited with code 0."
        }
    } finally {
        $ErrorActionPreference = $previousPreference
        if ($process) {
            if ($collector) { try { $collector.Detach($process) } catch {} }
            try { $process.CancelOutputRead() } catch {}
            try { $process.CancelErrorRead() } catch {}
            try { $process.Dispose() } catch {}
        }
    }
}
