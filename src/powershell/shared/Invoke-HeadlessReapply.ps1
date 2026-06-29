function Invoke-HeadlessReapply {
    # Minimal reapply pipeline — runs SpotX synchronously with the saved config
    # and reapplies Spicetify if the CLI is present. Intentionally does NOT use
    # any UI / runspace plumbing. Caller runs on the main thread from -Watch.
    param([hashtable]$Config)
    if (-not $Config) { throw 'Invoke-HeadlessReapply: missing config' }

    $tempDir = Join-Path $global:TEMP_DIR ("LibreSpot_Watcher_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    try {
        $spotxRun = Join-Path $tempDir 'spotx_run.ps1'

        # Download + hash-verify SpotX. We DON'T fall back to BITS here because
        # the watcher runs unattended and we'd rather silently skip than use a
        # different download backend than the user-triggered install path.
        $expectedHash = [string]$global:PinnedReleases.SpotX.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $spotxRun -Label 'SpotX run.ps1 (watcher)')) {
            $downloadFailed = $false
            try {
                Write-WatcherLog "Downloading SpotX run.ps1"
                Invoke-WebRequest -Uri $global:URL_SPOTX -OutFile $spotxRun -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
            } catch {
                $downloadFailed = $true
                if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $spotxRun -Label 'SpotX run.ps1 (watcher)') {
                    Write-WatcherLog 'Network download failed; using verified cached copy.' -Level 'WARN'
                    $downloadFailed = $false
                } else { throw }
            }
            if (-not $downloadFailed) {
                $actualHash = (Get-FileHash -LiteralPath $spotxRun -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($actualHash -ne $expectedHash.ToLowerInvariant()) {
                    throw "SpotX hash mismatch. Expected $expectedHash, got $actualHash. Refusing to run."
                }
                Save-ToAssetCache -SourcePath $spotxRun -SHA256Hash $expectedHash
            }
        }

        $spotxArgs = Build-SpotXParams -Config $Config
        Write-WatcherLog "Invoking SpotX with: $spotxArgs"

        # Use powershell.exe isolation so SpotX can't leak runtime state into our
        # own script scope. Exit code is the only signal we care about.
        $psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if (-not (Test-Path -LiteralPath $psExe)) { $psExe = 'powershell.exe' }
        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = $psExe
        $pinfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$spotxRun`" $spotxArgs"
        $pinfo.RedirectStandardOutput = $true
        $pinfo.RedirectStandardError  = $true
        $pinfo.UseShellExecute = $false
        $pinfo.CreateNoWindow = $true
        $proc = [System.Diagnostics.Process]::Start($pinfo)
        # Drain stdout/stderr asynchronously to prevent buffer deadlock.
        # If SpotX writes more than the OS pipe buffer (~4KB) the process
        # hangs forever waiting for the buffer to be read.
        $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
        $stderrTask = $proc.StandardError.ReadToEndAsync()
        if (-not $proc.WaitForExit(20 * 60 * 1000)) {
            try { $proc.Kill() } catch {}
            throw "SpotX timed out after 20 minutes."
        }
        $proc.WaitForExit()  # Ensure async streams are fully flushed
        if ($proc.ExitCode -ne 0) {
            $stderrText = if ($stderrTask.IsCompleted) { $stderrTask.Result } else { '(not available)' }
            throw "SpotX exited with code $($proc.ExitCode). Stderr: $stderrText"
        }
        Write-WatcherLog "SpotX completed successfully" -Level 'SUCCESS'

        # Reapply Spicetify when it's installed. Missing CLI is fine — it just
        # means the user only patches with SpotX and that part is already done.
        if (Test-SpicetifyCliInstalled) {
            try {
                Invoke-SpicetifyCli -Arguments @('backup','apply','--bypass-admin') -FailureMessage 'Watcher Spicetify apply failed.'
                Write-WatcherLog "Spicetify reapplied" -Level 'SUCCESS'
            } catch {
                Write-WatcherLog "Spicetify apply failed: $($_.Exception.Message)" -Level 'WARN'
            }
        }
    } finally {
        try { Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue } catch {}
    }
}
