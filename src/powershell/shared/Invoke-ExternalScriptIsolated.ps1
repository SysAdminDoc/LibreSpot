function Invoke-ExternalScriptIsolated { param([string]$FilePath,[string]$Arguments,[int]$TimeoutSeconds=600,[string]$ExpectedHash='',[string]$Label='external script')
    Write-Log "Spawning: $FilePath"
    Write-PowerShellSecurityContext
    $stdoutPath = Join-Path $global:TEMP_DIR ("LibreSpot-stdout-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stderrPath = Join-Path $global:TEMP_DIR ("LibreSpot-stderr-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stdoutState = @{ Offset = 0L; Remainder = '' }
    $stderrState = @{ Offset = 0L; Remainder = '' }
    # The spawned powershell.exe can be forced into ConstrainedLanguage by WDAC /
    # AppLocker even when this host is FullLanguage; classify that from stderr.
    $appControlHintShown = $false
    # SpotX child-download outages (timeouts, Cloudflare worker failures,
    # phishing-flagged mirrors) otherwise surface as a bare exit code.
    $childFailure = $null
    $scriptGuard = $null
    $p = $null
    try {
        $scriptGuard = Open-VerifiedScriptForExecution -FilePath $FilePath -ExpectedHash $ExpectedHash -Label $Label -Arguments $Arguments
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHash)) {
            Write-Log "  Execution copy verified and locked for $Label"
        }
        $argString = "-NoProfile -ExecutionPolicy Bypass -File `"$FilePath`" $Arguments"
        $p = Start-Process -FilePath 'powershell.exe' -ArgumentList $argString -NoNewWindow -PassThru -Wait:$false -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -ErrorAction Stop
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while (-not $p.HasExited) {
            if ((Get-Date) -gt $deadline) {
                Write-Log "Process exceeded ${TimeoutSeconds}s timeout - terminating." -Level 'WARN'
                try { $p.Kill() } catch {}
                try { $p.WaitForExit(5000) } catch {}
                throw "External process timed out after ${TimeoutSeconds} seconds. It may have hung or entered an interactive prompt."
            }
            $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
            $stdoutState = @{ Offset = $stdoutRead.Offset; Remainder = $stdoutRead.Remainder }
            foreach ($line in $stdoutRead.Lines) {
                Write-Log $line -Level 'OUT'
                if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
            }

            $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
            $stderrState = @{ Offset = $stderrRead.Offset; Remainder = $stderrRead.Remainder }
            foreach ($line in $stderrRead.Lines) {
                Write-Log "[STDERR] $line" -Level 'WARN'
                if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
                if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                    $appControlHintShown = $true
                    Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker, Windows Defender Application Control, or Smart App Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these controls. On managed devices, ask your administrator. On personal devices with Smart App Control (Windows 11), adjust it in Settings > Privacy & security > Windows Security. Alternatively, use LibreSpot.exe from the Releases page." -Level 'WARN'
                }
            }
            Start-Sleep -Milliseconds 200
        }
        $p.WaitForExit()

        $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
        foreach ($line in $stdoutRead.Lines + @($stdoutRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log $line -Level 'OUT'
            if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
        }
        $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
        foreach ($line in $stderrRead.Lines + @($stderrRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log "[STDERR] $line" -Level 'WARN'
            if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
            if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                $appControlHintShown = $true
                Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker, Windows Defender Application Control, or Smart App Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these controls. On managed devices, ask your administrator. On personal devices with Smart App Control (Windows 11), adjust it in Settings > Privacy & security > Windows Security. Alternatively, use LibreSpot.exe from the Releases page." -Level 'WARN'
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
        if ($p) { try { $p.Dispose() } catch {} }
        if ($scriptGuard) { try { $scriptGuard.Dispose() } catch {} }
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}
