function Invoke-ExternalScriptIsolated { param([string]$FilePath,[string]$Arguments,[int]$TimeoutSeconds=600,[string]$ExpectedHash='',[string]$Label='external script')
    Write-Log "Spawning: $FilePath"
    Write-PowerShellSecurityContext
    $stdoutPath = Join-Path $global:TEMP_DIR ("LibreSpot-stdout-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stderrPath = Join-Path $global:TEMP_DIR ("LibreSpot-stderr-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stdoutState = @{ Offset = 0L; Remainder = '' }
    $stderrState = @{ Offset = 0L; Remainder = '' }
    $maxCaptureBytes = 1024 * 1024
    $diskTruncationMarker = [System.Text.Encoding]::UTF8.GetBytes("[output truncated: disk capture bounded]`r`n")
    $trimOutputFile = {
        param([string]$Path)

        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
        $stream = $null
        try {
            $stream = [System.IO.File]::Open(
                $Path,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::ReadWrite)
            if ($stream.Length -le $maxCaptureBytes) { return $false }

            $keepBytes = [Math]::Max(0, $maxCaptureBytes - $diskTruncationMarker.Length)
            $start = [Math]::Max(0, $stream.Length - $keepBytes)
            $null = $stream.Seek($start, [System.IO.SeekOrigin]::Begin)
            $tail = New-Object byte[] $keepBytes
            $total = 0
            while ($total -lt $tail.Length) {
                $read = $stream.Read($tail, $total, $tail.Length - $total)
                if ($read -le 0) { break }
                $total += $read
            }

            $stream.SetLength(0)
            $stream.Position = 0
            $stream.Write($diskTruncationMarker, 0, $diskTruncationMarker.Length)
            if ($total -gt 0) { $stream.Write($tail, 0, $total) }
            try { $stream.Flush($true) } catch { $stream.Flush() }
            return $true
        } catch {
            return $false
        } finally {
            if ($stream) { try { $stream.Dispose() } catch {} }
        }
    }
    # The spawned powershell.exe can be forced into ConstrainedLanguage by WDAC /
    # AppLocker even when this host is FullLanguage; classify that from stderr.
    $appControlHintShown = $false
    # SpotX child-download outages (timeouts, Cloudflare worker failures,
    # phishing-flagged mirrors) otherwise surface as a bare exit code.
    $childFailure = $null
    $scriptGuard = $null
    $p = $null
    $ownedProcess = $null
    try {
        $scriptGuard = Open-VerifiedScriptForExecution -FilePath $FilePath -ExpectedHash $ExpectedHash -Label $Label -Arguments $Arguments
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHash)) {
            Write-Log "  Execution copy verified and locked for $Label"
        }
        $argString = "-NoProfile -ExecutionPolicy Bypass -File `"$FilePath`" $Arguments"
        $ownedProcess = Start-LibreSpotOwnedProcess -FilePath 'powershell.exe' -ArgumentList $argString -NoNewWindow -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        $p = $ownedProcess.Process
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while (-not $p.HasExited) {
            if ((Get-Date) -gt $deadline) {
                Write-Log "Process exceeded ${TimeoutSeconds}s timeout - terminating." -Level 'WARN'
                try { $ownedProcess.Job.Terminate() } catch {}
                try { $p.WaitForExit(5000) } catch {}
                try { $null = & $trimOutputFile -Path $stdoutPath } catch {}
                try { $null = & $trimOutputFile -Path $stderrPath } catch {}
                throw "External process timed out after ${TimeoutSeconds} seconds. It may have hung or entered an interactive prompt."
            }
            if (& $trimOutputFile -Path $stdoutPath) { $stdoutState = @{ Offset = 0L; Remainder = '' } }
            $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
            $stdoutState = @{ Offset = $stdoutRead.Offset; Remainder = $stdoutRead.Remainder }
            foreach ($line in $stdoutRead.Lines) {
                Write-Log $line -Level 'OUT'
                if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
            }

            if (& $trimOutputFile -Path $stderrPath) { $stderrState = @{ Offset = 0L; Remainder = '' } }
            $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
            $stderrState = @{ Offset = $stderrRead.Offset; Remainder = $stderrRead.Remainder }
            foreach ($line in $stderrRead.Lines) {
                Write-Log "[STDERR] $line" -Level 'WARN'
                if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
                if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                    $appControlHintShown = $true
                    Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker, Windows Defender Application Control, or Smart App Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these controls. Do not disable or bypass application control for LibreSpot. On managed devices, ask your administrator whether an approved LibreSpot artifact is allowed. On personal devices, leave Smart App Control enabled and follow official Windows Security guidance." -Level 'WARN'
                }
            }
            Start-Sleep -Milliseconds 200
        }
        $p.WaitForExit()

        if (& $trimOutputFile -Path $stdoutPath) { $stdoutState = @{ Offset = 0L; Remainder = '' } }
        $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
        foreach ($line in $stdoutRead.Lines + @($stdoutRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log $line -Level 'OUT'
            if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
        }
        if (& $trimOutputFile -Path $stderrPath) { $stderrState = @{ Offset = 0L; Remainder = '' } }
        $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
        foreach ($line in $stderrRead.Lines + @($stderrRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log "[STDERR] $line" -Level 'WARN'
            if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
            if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                $appControlHintShown = $true
                Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker, Windows Defender Application Control, or Smart App Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these controls. Do not disable or bypass application control for LibreSpot. On managed devices, ask your administrator whether an approved LibreSpot artifact is allowed. On personal devices, leave Smart App Control enabled and follow official Windows Security guidance." -Level 'WARN'
            }
        }

        # Capture ExitCode defensively. Windows PowerShell can occasionally lose
        # the Process handle when Start-Process is combined with redirected output.
        $exitCode = $null
        try { $exitCode = $p.ExitCode } catch { $exitCode = $null }

        if ($null -eq $exitCode) {
            # Windows PowerShell can drop the ExitCode when Start-Process is paired
            # with redirected output. Don't blindly assume success: if the child's
            # own output already classified a failure (download outage, phishing
            # mirror, patch abort), surface it instead of masking it.
            if ($childFailure) {
                Write-Log $childFailure.Guidance -Level 'WARN'
                try {
                    Write-OperationJournalEntry -Phase 'external' -Target $FilePath -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint $childFailure.Guidance -Data @{ failureCategory = $childFailure.Category; exitCode = 'unavailable' }
                } catch {}
                throw "Process reported a failure and its exit code was unavailable [$($childFailure.Category)]"
            }
            Write-Log 'External process finished but ExitCode was unavailable and no failure signal was found in its output; treating as success. The caller verifies the result independently.' -Level 'WARN'
        } elseif ($exitCode -ne 0) {
            if ($childFailure) {
                Write-Log $childFailure.Guidance -Level 'WARN'
                try {
                    Write-OperationJournalEntry -Phase 'external' -Target $FilePath -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint $childFailure.Guidance -Data @{ failureCategory = $childFailure.Category; exitCode = $exitCode }
                } catch {}
                throw "Process exited with code $exitCode [$($childFailure.Category)]"
            }
            throw "Process exited with code $exitCode"
        }
    } finally {
        if ($ownedProcess) { try { $ownedProcess.Job.Dispose() } catch {} }
        if ($p) { try { $p.Dispose() } catch {} }
        if ($scriptGuard) { try { $scriptGuard.Dispose() } catch {} }
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}
