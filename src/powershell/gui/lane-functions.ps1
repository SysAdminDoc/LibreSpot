function Get-WatcherState {
    if (-not (Test-Path -LiteralPath $global:WATCHER_STATE_PATH)) {
        return @{ LastKnownVersion = $null; LastRunAt = $null; LastOutcome = $null }
    }
    try {
        $raw = Get-Content -LiteralPath $global:WATCHER_STATE_PATH -Raw -ErrorAction Stop | ConvertFrom-Json
        return @{
            LastKnownVersion = [string]$raw.LastKnownVersion
            LastRunAt        = [string]$raw.LastRunAt
            LastOutcome      = [string]$raw.LastOutcome
        }
    } catch {
        return @{ LastKnownVersion = $null; LastRunAt = $null; LastOutcome = $null }
    }
}

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
                Move-Item -LiteralPath $global:WATCHER_STATE_PATH -Destination $rescuePath -Force -ErrorAction Stop
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

function Get-WatcherLaunchCommand {
    # Returns a [string[]]{ FileName, ArgumentList... } suitable for schtasks.exe's
    # /TR value. Prefers the compiled LibreSpot.exe when the user launched from it;
    # falls back to powershell.exe + -File when launched from the raw .ps1. Returns
    # $null when neither path is usable (e.g. `irm | iex`) so the caller can surface
    # a helpful error instead of registering a broken task.
    $entry = [string]$script:EntryCommandPath
    if ([string]::IsNullOrWhiteSpace($entry)) { return $null }
    if (-not (Test-Path -LiteralPath $entry)) { return $null }

    $ext = [System.IO.Path]::GetExtension($entry).ToLowerInvariant()
    if ($ext -eq '.exe') {
        return @{ Command = $entry; Arguments = '-Watch'; Entry = $entry }
    }
    if ($ext -eq '.ps1') {
        $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if (-not (Test-Path -LiteralPath $ps)) { $ps = 'powershell.exe' }
        return @{ Command = $ps; Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$entry`" -Watch"; Entry = $entry }
    }
    return $null
}

function Register-AutoReapplyTask {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    # Creates a per-user scheduled task that fires at logon, then again every
    # 30 minutes, invoking LibreSpot in -Watch mode. Returns $true on success.
    $launch = Get-WatcherLaunchCommand
    if (-not $launch) {
        Write-WatcherLog 'Register: no usable LibreSpot entry path (iex launch?). Watcher not registered.' -Level 'ERROR'
        return $false
    }

    # Unregister first so we don't get "task already exists" failures when the
    # user toggles the setting. schtasks /Create /F also overwrites, but the
    # explicit delete keeps the semantics obvious.
    try { Unregister-AutoReapplyTask | Out-Null } catch {}

    # Build an inline XML task definition. schtasks.exe's flag syntax can't
    # express "logon trigger + repetition every 30 minutes for 1 day" cleanly,
    # but the XML schema can. Repetition Duration=PT0S means "forever" per
    # MS-TSCH 2.3.5.2; Interval=PT30M is every 30 minutes.
    $escapedCommand = [System.Security.SecurityElement]::Escape($launch.Command)
    $escapedArguments = [System.Security.SecurityElement]::Escape($launch.Arguments)
    # Use the current user's SID for domain-joined machines where bare USERNAME
    # may not resolve.  Fall back to USERDOMAIN\USERNAME, then bare USERNAME.
    $userId = $null
    try {
        $currentIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
        $userId = $currentIdentity.User.Value   # SID string, e.g. S-1-5-21-...
    } catch {}
    if ([string]::IsNullOrWhiteSpace($userId)) {
        $userId = if ($env:USERDOMAIN -and $env:USERDOMAIN -ne $env:COMPUTERNAME) {
            "$env:USERDOMAIN\$env:USERNAME"
        } else { $env:USERNAME }
    }
    $userId = [System.Security.SecurityElement]::Escape($userId)
    $xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>LibreSpot</Author>
    <Description>LibreSpot reapplies SpotX automatically when Spotify updates itself. Toggle from Maintenance inside the app.</Description>
    <URI>\LibreSpot\ReapplyWatcher</URI>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT2M</Delay>
      <Repetition>
        <Interval>PT30M</Interval>
        <Duration>PT0S</Duration>
        <StopAtDurationEnd>false</StopAtDurationEnd>
      </Repetition>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>$userId</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT30M</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$escapedCommand</Command>
      <Arguments>$escapedArguments</Arguments>
    </Exec>
  </Actions>
</Task>
"@

    $xmlPath = Join-Path $global:CONFIG_DIR "watcher-task.xml"
    if ($PSCmdlet.ShouldProcess($global:WATCHER_TASK_NAME, 'Register scheduled task')) {
        Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Unregister the scheduled task to undo.'
        try {
            if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
                New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
            }
            # schtasks /Create /XML requires UTF-16 LE with BOM to match the XML header.
            [System.IO.File]::WriteAllText($xmlPath, $xml, [System.Text.Encoding]::Unicode)

            $output = & schtasks.exe /Create /TN $global:WATCHER_TASK_NAME /XML $xmlPath /F 2>&1
            $ok = ($LASTEXITCODE -eq 0)
            if ($ok) {
                Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Registered' -WouldChange $true -Reversible $true -RollbackHint 'Unregister the scheduled task to undo.'
                Write-WatcherLog "Register: scheduled task created for $($launch.Entry)"
            } else {
                Write-WatcherLog "Register failed (exit $LASTEXITCODE): $($output -join ' ')" -Level 'ERROR'
            }
            return $ok
        } catch {
            Write-WatcherLog "Register exception: $($_.Exception.Message)" -Level 'ERROR'
            return $false
        } finally {
            try { if (Test-Path -LiteralPath $xmlPath) { Remove-Item -LiteralPath $xmlPath -Force -ErrorAction SilentlyContinue } } catch {}
        }
    }
    return $false
}

function Invoke-HeadlessReapply {
    # Minimal reapply pipeline — runs SpotX synchronously with the saved config
    # and reapplies Spicetify if the CLI is present. Intentionally does NOT use
    # any UI / runspace plumbing. Caller runs on the main thread from -Watch.
    param([hashtable]$Config)
    if (-not $Config) { throw 'Invoke-HeadlessReapply: missing config' }

    $tempDir = Join-Path $global:TEMP_DIR ("LibreSpot_Watcher_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    $customPatchesPath = ''
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    try {
        $spotxRun = Join-Path $tempDir 'spotx_run.ps1'

        # Download + hash-verify SpotX through the same guarded downloader as
        # user-triggered install/reapply so CVE and network diagnostics stay consistent.
        $expectedHash = [string]$global:PinnedReleases.SpotX.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $spotxRun -Label 'SpotX run.ps1 (watcher)')) {
            $downloadFailed = $false
            try {
                Write-WatcherLog "Downloading SpotX run.ps1"
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $spotxRun
            } catch {
                $downloadFailed = $true
                if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $spotxRun -Label 'SpotX run.ps1 (watcher)') {
                    Write-WatcherLog 'Network download failed; using verified cached copy.' -Level 'WARN'
                    $downloadFailed = $false
                } else { throw }
            }
            if (-not $downloadFailed) {
                $actualHash = Get-FileSha256Lower -Path $spotxRun
                if ($actualHash -ne $expectedHash.ToLowerInvariant()) {
                    throw "SpotX hash mismatch. Expected $expectedHash, got $actualHash. Refusing to run."
                }
                Save-ToAssetCache -SourcePath $spotxRun -SHA256Hash $expectedHash -Label 'SpotX run.ps1 (watcher)' -SourceUrl $global:URL_SPOTX
            }
        }

        $spotxArgs = Build-SpotXParams -Config $Config
        $customPatchesPath = New-SpotXCustomPatchesFile -Config $Config
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            $spotxArgs = "$spotxArgs -CustomPatchesPath `"$customPatchesPath`""
            Write-WatcherLog "Custom SpotX patches staged at $customPatchesPath"
        }
        Write-WatcherLog "Invoking SpotX with: $spotxArgs"

        # Use powershell.exe isolation so SpotX can't leak runtime state into our
        # own script scope. Exit code is the only signal we care about.
        $psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if (-not (Test-Path -LiteralPath $psExe)) { $psExe = 'powershell.exe' }
        $spotxGuard = $null
        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = $psExe
        $pinfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$spotxRun`" $spotxArgs"
        $pinfo.RedirectStandardOutput = $true
        $pinfo.RedirectStandardError  = $true
        $pinfo.UseShellExecute = $false
        $pinfo.CreateNoWindow = $true
        try {
            $spotxGuard = Open-VerifiedScriptForExecution -FilePath $spotxRun -ExpectedHash $expectedHash -Label 'SpotX run.ps1 (watcher)' -Arguments $spotxArgs
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
        } finally {
            if ($spotxGuard) { try { $spotxGuard.Dispose() } catch {} }
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
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            Remove-Item -LiteralPath $customPatchesPath -Force -ErrorAction SilentlyContinue
        }
        try { Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Invoke-AutoReapplyWatcher {
    # -Watch entry point. Returns an exit code to satisfy schtasks reporting.
    Write-WatcherLog "--- Watcher tick ---"

    $currentVersion = Get-InstalledSpotifyVersion
    if (-not $currentVersion) {
        Write-WatcherLog "Spotify not installed - skipping."
        return 0
    }

    $state = Get-WatcherState

    # First-ever run: record the version and do nothing. Reapplying on the
    # first tick would clobber a freshly-installed unconfigured Spotify.
    if (-not $state.LastKnownVersion) {
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Initialized' }
        Write-WatcherLog "Initialized last-known version to $currentVersion (no reapply this tick)"
        return 0
    }

    if ($currentVersion -eq $state.LastKnownVersion) {
        Write-WatcherLog "Spotify still at $currentVersion - nothing to do"
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'UpToDate' }
        return 0
    }

    Write-WatcherLog "Spotify version bump: $($state.LastKnownVersion) -> $currentVersion" -Level 'STEP'

    if (Test-SpotifyRunning) {
        Write-WatcherLog "Spotify is running - deferring reapply to next tick"
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'DeferredSpotifyRunning' }
        return 0
    }

    $saved = $null
    try { $saved = Load-LibreSpotConfig } catch { Write-WatcherLog "Config load failed: $($_.Exception.Message)" -Level 'ERROR' }
    if (-not $saved) {
        Write-WatcherLog "No saved LibreSpot config - cannot reapply automatically" -Level 'WARN'
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'NoConfig' }
        return 0
    }
    $saved = Normalize-LibreSpotConfig -Config $saved

    if (-not (ConvertTo-ConfigBoolean -Value $saved['AutoReapply_Enabled'] -Default $false)) {
        Write-WatcherLog 'Auto-reapply preference is off; skipping.'
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'PreferenceOff' }
        return 0
    }

    try {
        Invoke-HeadlessReapply -Config $saved
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Reapplied' }
        return 0
    } catch {
        Write-WatcherLog "Reapply failed: $($_.Exception.Message)" -Level 'ERROR'
        # Keep LastKnownVersion unchanged so we'll retry next tick.
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = "Error: $($_.Exception.Message)" }
        return 1
    }
}

function Save-LibreSpotConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([hashtable]$Config)
    if (-not $PSCmdlet.ShouldProcess($global:CONFIG_PATH, 'Save configuration')) {
        return $true
    }
    Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
    $tempPath = $null
    $backupPath = $null
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }
        $tempPath = Join-Path $global:CONFIG_DIR ("config.{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
        $backupPath = Join-Path $global:CONFIG_DIR ("config.{0}.bak" -f [Guid]::NewGuid().ToString('N'))
        $normalizedConfig = Normalize-LibreSpotConfig -Config $Config
        $json = [ordered]@{}
        foreach ($key in $normalizedConfig.Keys) { $json[$key] = $normalizedConfig[$key] }
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($tempPath, ($json | ConvertTo-Json -Depth 4), $utf8)
        if (Test-Path -LiteralPath $global:CONFIG_PATH) {
            try {
                [System.IO.File]::Replace($tempPath, $global:CONFIG_PATH, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                $rescuePath = "$($global:CONFIG_PATH).rescue"
                Move-Item -LiteralPath $global:CONFIG_PATH -Destination $rescuePath -Force
                try {
                    [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
                    Remove-Item -LiteralPath $rescuePath -Force -ErrorAction SilentlyContinue
                } catch {
                    Move-Item -LiteralPath $rescuePath -Destination $global:CONFIG_PATH -Force -ErrorAction SilentlyContinue
                    throw
                }
            }
        } else {
            [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
        }
        Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Saved' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
        return $true
    } catch {
        Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
        try { Write-Log "Config save failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
        if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
        if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
        return $false
    }
}

function Load-LibreSpotConfig {
    $script:ConfigLoadWarning = $null
    if (-not (Test-Path -LiteralPath $global:CONFIG_PATH)) { return $null }
    try {
        $json = Get-Content -LiteralPath $global:CONFIG_PATH -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        $cfg = ConvertTo-PlainHashtable -InputObject $json
        return (Normalize-LibreSpotConfig -Config $cfg)
    } catch {
        Move-ConfigFileToQuarantine -Reason $_.Exception.Message
    }
    return $null
}

function Update-SpicetifyCliProgress {
    param([string]$Line)

    $plain = Remove-ConsoleEscapeSequences -Text $Line
    $sh = $script:syncHash
    if (-not $sh -or -not $sh.Dispatcher) { return }
    if ($plain -match 'Patching files\s*\[\s*(\d+)\s*/\s*(\d+)\s*\]') {
        $done = [int]$matches[1]
        $total = [Math]::Max(1, [int]$matches[2])
        $percent = [int][Math]::Min(99, [Math]::Floor(($done / $total) * 100))
        $progressValue = [int][Math]::Min(99, 86 + [Math]::Floor(($done / $total) * 12))
        $progressBar = $sh.ProgressBar
        $statusLabel = $sh.StatusLabel
        $stepLabel = $sh.StepLabel
        $installContext = $sh.InstallContext
        try {
            $sh.Dispatcher.Invoke([Action]{
                try {
                    if ($progressBar -and $progressBar.Value -lt $progressValue) { $progressBar.Value = $progressValue }
                    if ($statusLabel) { $statusLabel.Text = "Spicetify is patching Spotify files ($percent%)" }
                    if ($stepLabel) { $stepLabel.Text = "Applying setup: patching file $done of $total" }
                    if ($installContext) { $installContext.Text = "Spicetify is rebuilding Spotify's UI package. This can take several minutes on slower disks." }
                } catch {}
            }) | Out-Null
        } catch {}
    } elseif ($plain -match 'Extracting backup|Preprocessing|Fetching remote CSS map|Patching files') {
        $statusLabel = $sh.StatusLabel
        $installContext = $sh.InstallContext
        try {
            $sh.Dispatcher.Invoke([Action]{
                try {
                    if ($statusLabel) { $statusLabel.Text = 'Spicetify is preparing Spotify files' }
                    if ($installContext) { $installContext.Text = "Spicetify is rebuilding Spotify's UI package. This can take several minutes on slower disks." }
                } catch {}
            }) | Out-Null
        } catch {}
    }
}

function Write-Log {
    param([string]$Message,[string]$Level='INFO')
    $displayMessage = if ([string]::IsNullOrWhiteSpace([string]$global:CURRENT_OPERATION_ID)) {
        $Message
    } else {
        "[op:$global:CURRENT_OPERATION_ID] $Message"
    }
    Update-UI -Message $displayMessage -Level $Level -IsHeader ($Level -eq 'STEP' -or $Level -eq 'HEADER')
}

function Hide-SpotifyWindows {
    Get-Process -Name Spotify -EA SilentlyContinue | ForEach-Object {
        if ($_.MainWindowHandle -ne [IntPtr]::Zero) {
            [Win32]::ShowWindowAsync($_.MainWindowHandle, [Win32]::SW_HIDE) | Out-Null
        }
    }
}

function Module-NukeSpotify {
    Write-Log "=== LibreSpot Comprehensive Spotify Uninstaller ===" -Level 'STEP'
    $rc = 0

    # --- Phase 1: Kill all Spotify processes ---
    Write-Log "[Phase 1/7] Terminating Spotify processes..."
    Stop-SpotifyProcesses

    # --- Phase 2: Remove Spotify Store (UWP/AppX) ---
    Write-Log "[Phase 2/7] Checking for Microsoft Store Spotify..."
    try {
        if ($PSVersionTable.PSVersion.Major -ge 7) { Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue }
        $storeApp = Get-AppxPackage -Name "SpotifyAB.SpotifyMusic" -EA SilentlyContinue
        if ($storeApp) {
            Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedPP = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxPackage -Package $storeApp.PackageFullName -EA Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            } catch {
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry removal or reinstall from the Microsoft Store.'
                throw
            } finally { $ProgressPreference = $savedPP }
            Write-Log "  Removed Spotify Store app."; $rc++
        } else { Write-Log "  No Store version found." }
    } catch { Write-Log "  Store removal failed: $($_.Exception.Message)" -Level 'WARN' }

    # Remove the provisioned package so new user profiles don't get Spotify pre-installed
    try {
        $provisioned = Get-AppxProvisionedPackage -Online -EA SilentlyContinue | Where-Object { $_.DisplayName -eq 'SpotifyAB.SpotifyMusic' }
        if ($provisioned) {
            Write-OperationJournalEntry -Phase 'appx' -Target $provisioned.PackageName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedPP = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxProvisionedPackage -Online -PackageName $provisioned.PackageName -EA Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $provisioned.PackageName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
                Write-Log "  Removed provisioned Spotify package."; $rc++
            } finally { $ProgressPreference = $savedPP }
        }
    } catch { Write-Log "  Provisioned package removal skipped: $($_.Exception.Message)" -Level 'WARN' }

    # --- Phase 3: Nuke file system ---
    Write-Log "[Phase 3/7] Removing Spotify files and folders..."
    $desktopPath = Get-DesktopPath
    $filesToNuke = @(
        @{ Path = (Join-Path $env:APPDATA "Spotify");        Label = "Spotify Roaming (%APPDATA%)" }
        @{ Path = (Join-Path $env:LOCALAPPDATA "Spotify");   Label = "Spotify Local (%LOCALAPPDATA%)" }
        @{ Path = (Join-Path $env:APPDATA "spicetify");      Label = "Spicetify Config (%APPDATA%)" }
        @{ Path = (Join-Path $env:LOCALAPPDATA "spicetify"); Label = "Spicetify CLI (%LOCALAPPDATA%)" }
        @{ Path = (Join-Path $env:TEMP "SpotifyUninstall.exe"); Label = "Spotify uninstaller (TEMP)" }
        @{ Path = (Join-Path $desktopPath "Spotify.lnk");    Label = "Desktop shortcut" }
        @{ Path = (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Spotify.lnk"); Label = "Start Menu shortcut" }
    )
    foreach ($f in $filesToNuke) { $rc += Remove-PathSafely -Path $f.Path -Label $f.Label }

    # Glob targets: SpotX temp folders, Spotify installers, spicetify temp
    @(
        @{ Pattern = (Join-Path $env:TEMP "SpotX_Temp*");  Label = "SpotX temp" }
        @{ Pattern = (Join-Path $env:TEMP "Spotify_*");    Label = "Spotify temp installer" }
        @{ Pattern = (Join-Path $env:TEMP "spicetify*");   Label = "Spicetify temp" }
    ) | ForEach-Object {
        $lbl = $_.Label
        Get-ChildItem -Path $_.Pattern -EA SilentlyContinue | ForEach-Object {
            $rc += Remove-PathSafely -Path $_.FullName -Label "${lbl}: $($_.Name)"
        }
    }

    # IE/Edge cached Spotify installers
    $ieCache = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\INetCache"
    if (Test-Path $ieCache) {
        Get-ChildItem -Path $ieCache -Recurse -Force -Filter "SpotifyFullSetup*" -EA SilentlyContinue | ForEach-Object {
            $rc += Remove-PathSafely -Path $_.FullName -Label "Cached installer: $($_.Name)"
        }
    }

    # --- Phase 4: Registry cleanup ---
    Write-Log "[Phase 4/7] Cleaning registry..."
    $regKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify"
        "HKCU:\Software\Spotify"
        "HKCU:\Software\Classes\spotify"
        "HKCU:\Software\Classes\spotify-client"
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\ElevationPolicy\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\DragDrop\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\Spotify.exe"
    )
    foreach ($key in $regKeys) {
        if (Test-Path $key) {
            Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
            try {
                Remove-Item -Path $key -Recurse -Force -EA Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
                Write-Log "  Removed: $key"; $rc++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
                Write-Log "  Failed: $key" -Level 'WARN'
            }
        }
    }
    $regValues = @(
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify" }
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify Web Helper" }
    )
    foreach ($rv in $regValues) {
        if (Get-ItemProperty -Path $rv.Path -Name $rv.Name -EA SilentlyContinue) {
            $regTarget = "$($rv.Path)\$($rv.Name)"
            Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
            try {
                Remove-ItemProperty -Path $rv.Path -Name $rv.Name -Force -EA Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
                Write-Log "  Removed startup: $($rv.Name)"; $rc++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
            }
        }
    }

    # --- Phase 5: Scheduled tasks ---
    Write-Log "[Phase 5/7] Removing scheduled tasks..."
    try {
        $spotifyTaskNames = @('SpotifyMigrator', 'SpotifyUpdateTask', 'Spotify')
        Get-ScheduledTask -EA SilentlyContinue |
            Where-Object { $_.TaskName -in $spotifyTaskNames -or $_.TaskName -like 'Spotify-*' } |
            ForEach-Object {
                Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                try {
                    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -EA Stop
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                    Write-Log "  Removed task: $($_.TaskName)"; $rc++
                } catch {
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry scheduled task removal manually.'
                }
            }
    } catch { Write-Log "  Task cleanup skipped." }

    # --- Phase 6: Firewall rules ---
    Write-Log "[Phase 6/7] Removing firewall rules..."
    try {
        Get-NetFirewallRule -EA SilentlyContinue | Where-Object { $_.DisplayName -match 'Spotify' } | ForEach-Object {
            try { Remove-NetFirewallRule -Name $_.Name -EA Stop; Write-Log "  Removed firewall: $($_.DisplayName)"; $rc++ }
            catch {}
        }
    } catch { Write-Log "  Firewall cleanup skipped." }

    # --- Phase 7: Verification sweep (amd64fox/Uninstall-Spotify retry pattern) ---
    Write-Log "[Phase 7/7] Verification sweep..."
    $verifyPaths = @(
        (Join-Path $env:APPDATA "Spotify")
        (Join-Path $env:LOCALAPPDATA "Spotify")
        (Join-Path $env:APPDATA "spicetify")
        (Join-Path $env:LOCALAPPDATA "spicetify")
    )
    $maxRetries = 5
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        $remaining = @($verifyPaths | Where-Object { Test-Path $_ })
        if ($remaining.Count -eq 0) { break }
        if ($attempt -gt 1) { Write-Log "  Retry $attempt/$maxRetries ($($remaining.Count) path(s) still locked)..." }
        Start-Sleep -Milliseconds 1500
        foreach ($path in $remaining) {
            if (Remove-PathSafely -Path $path -Label "Cleanup retry: $(Split-Path $path -Leaf)") { $rc++ }
        }
    }
    $survivors = @($verifyPaths | Where-Object { Test-Path $_ })
    if ($survivors.Count -gt 0) {
        Write-Log "  Could not fully remove $($survivors.Count) path(s) (may need reboot):" -Level 'WARN'
        $survivors | ForEach-Object { Write-Log "    - $_" -Level 'WARN' }
    }

    Write-Log "=== Nuke complete: $rc items removed ===" -Level 'STEP'
}

function Module-ApplySpicetify {
    param(
        $Config,
        [string]$EvidenceSource = 'Module-ApplySpicetify'
    )
    Write-Log "Applying Spicetify changes..." -Level 'STEP'
    # Marketplace-only mode intentionally does NOT disable theme injection here.
    # The official Marketplace contract needs inject_css/replace_colors on with
    # the placeholder theme active (Module-InstallMarketplace asserts this), or
    # every store theme/snippet install is a silent no-op. When no theme at all
    # is configured, the Spicetify CLI already forces injection off on its own
    # (InitSetting in src/cmd/cmd.go), so zeroing the ini here was redundant for
    # safety and actively broke the Marketplace theme contract.

    $diag = Get-SpicetifyDiagnosticSnapshot
    foreach ($key in $diag.Keys) {
        Write-Log "  diag: $key = $($diag[$key])"
    }

    Write-Log "Ensuring Spotify is fully closed before patching files..."
    Stop-SpotifyProcesses -MaxAttempts 3

    # Spicetify expects `backup apply` as a combined invocation — especially after
    # SpotX has patched the client (version mismatch between Spotify and any prior
    # backup). Running them separately causes "version mismatch" failures.
    $applyError = $null
    $applyStage = 'backup apply'
    try {
        Invoke-SpicetifyCli -Arguments @('backup', 'apply', '--bypass-admin') -FailureMessage 'Could not apply the selected Spicetify setup.'
        Write-Log "Spicetify applied successfully."
        # SpotX serves the combined /xpui.js bundle, but the Spicetify CLI only
        # wires custom-app routes into xpui-modules.js/xpui-snapshot.js. Port
        # the injection to the live bundle or the store page renders blank.
        if ($Config -and $Config.Spicetify_Marketplace) {
            try {
                $wiring = Repair-SpicetifyCustomAppWiring
                Write-Log "Marketplace route wiring: $($wiring.Status). $($wiring.Detail)"
                if ($wiring.Status -eq 'AnchorsMissing') {
                    Write-Log 'Spotify changed its bundle shape; the store page may stay blank until Spicetify supports this Spotify build.' -Level 'WARN'
                }
            } catch {
                Write-Log "Marketplace route wiring failed: $($_.Exception.Message)" -Level 'WARN'
            }
        }
        $message = 'Spicetify backup apply succeeded.'
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $true -ApplyMessage $message | Out-Null
        return [pscustomobject]@{
            Stage     = $applyStage
            Succeeded = $true
            Message   = $message
        }
    } catch {
        $applyError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown Spicetify apply error.' }
        Write-Log "Spicetify backup apply failed: $applyError" -Level 'WARN'
    }

    Write-Log "Attempting rollback to keep Spotify usable..." -Level 'WARN'
    $restoreError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage 'Could not restore Spotify after the failed apply.'
    } catch {
        $restoreError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown restore error.' }
    }

    if ([string]::IsNullOrWhiteSpace($restoreError)) {
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $false -ApplyMessage $applyError | Out-Null
        throw "Spicetify apply failed but LibreSpot restored Spotify to a usable state. Apply error: $applyError"
    }

    Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $false -ApplyMessage "$applyError | Rollback error: $restoreError" | Out-Null
    throw "Spicetify apply failed and rollback also failed. Apply error: $applyError | Rollback error: $restoreError"
}
