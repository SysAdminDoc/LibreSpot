function Write-Log {
    param(
        [string]$Message,
        [string]$Level = 'INFO'
    )
    $timestamped = "[{0}] [{1}] {2}" -f (Get-Date -Format 'HH:mm:ss'), $Level, $Message
    try {
        Ensure-LogDirectory
        [System.IO.File]::AppendAllText($global:LOG_PATH, $timestamped + [Environment]::NewLine)
    } catch {}
    Write-EventLine -Kind 'log' -Level $Level -Payload $Message
}

function Load-LibreSpotConfig {
    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        return (Normalize-LibreSpotConfig -Config @{})
    }
    try {
        $json = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        return (Normalize-LibreSpotConfig -Config (ConvertTo-PlainHashtable -InputObject $json))
    } catch {
        Write-Log "Saved config was unreadable, so LibreSpot is falling back to recommended defaults." -Level 'WARN'
        Move-ConfigFileToQuarantine -Reason $_.Exception.Message
        return (Normalize-LibreSpotConfig -Config @{})
    }
}

function Get-WatcherState {
    if (-not (Test-Path -LiteralPath $global:WATCHER_STATE_PATH)) {
        return @{
            LastKnownVersion = $null
            LastRunAt = $null
            LastOutcome = $null
            LastAppliedSpotifyVersion = $null
            LastAttemptedSpotifyVersion = $null
            LastSuccessfulApplyAt = $null
            LastApplyAt = $null
            LastApplyOutcome = $null
            LastApplyError = $null
        }
    }

    try {
        $raw = Get-Content -LiteralPath $global:WATCHER_STATE_PATH -Raw -ErrorAction Stop | ConvertFrom-Json
        return @{
            LastKnownVersion = [string]$raw.LastKnownVersion
            LastRunAt = [string]$raw.LastRunAt
            LastOutcome = [string]$raw.LastOutcome
            LastAppliedSpotifyVersion = [string]$raw.LastAppliedSpotifyVersion
            LastAttemptedSpotifyVersion = [string]$raw.LastAttemptedSpotifyVersion
            LastSuccessfulApplyAt = [string]$raw.LastSuccessfulApplyAt
            LastApplyAt = [string]$raw.LastApplyAt
            LastApplyOutcome = [string]$raw.LastApplyOutcome
            LastApplyError = [string]$raw.LastApplyError
        }
    } catch {
        return @{
            LastKnownVersion = $null
            LastRunAt = $null
            LastOutcome = $null
            LastAppliedSpotifyVersion = $null
            LastAttemptedSpotifyVersion = $null
            LastSuccessfulApplyAt = $null
            LastApplyAt = $null
            LastApplyOutcome = $null
            LastApplyError = $null
        }
    }
}

function Set-WatcherState {
    param([hashtable]$State)
    $tempPath = $null
    $backupPath = $null
    try {
        Ensure-LogDirectory
        $merged = Get-WatcherState
        foreach ($key in @($State.Keys)) {
            $merged[$key] = $State[$key]
        }
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
    $entry = [string]$PSCommandPath
    if ([string]::IsNullOrWhiteSpace($entry)) {
        try { $entry = [string]$MyInvocation.MyCommand.Path } catch {}
    }
    # This process normally runs from an ephemeral execution copy
    # (LibreSpot.Backend.<guid>.run.ps1) that the shell deletes right after
    # the run. The scheduled task must target the canonical sibling that
    # EnsureBackendScriptAsync maintains, or every watcher tick launches a
    # file that no longer exists.
    if (-not [string]::IsNullOrWhiteSpace($entry) -and (Split-Path -Path $entry -Leaf) -ne 'LibreSpot.Backend.ps1') {
        $canonical = Join-Path (Split-Path -Path $entry -Parent) 'LibreSpot.Backend.ps1'
        if (Test-Path -LiteralPath $canonical -PathType Leaf) {
            $entry = $canonical
        }
    }
    if ([string]::IsNullOrWhiteSpace($entry) -or -not (Test-Path -LiteralPath $entry -PathType Leaf)) {
        return $null
    }

    $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $ps -PathType Leaf)) { $ps = 'powershell.exe' }

    return @{
        Command   = $ps
        Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$entry`" -Action WatchAutoReapply -ConfigPath `"$global:CONFIG_PATH`""
        Entry     = $entry
    }
}

function Register-AutoReapplyTask {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    $launch = Get-WatcherLaunchCommand
    if (-not $launch) {
        Write-WatcherLog 'Register: no usable backend script path. Watcher not registered.' -Level 'ERROR'
        return $false
    }

    try { Unregister-AutoReapplyTask | Out-Null } catch {}

    $escapedCommand = [System.Security.SecurityElement]::Escape($launch.Command)
    $escapedArguments = [System.Security.SecurityElement]::Escape($launch.Arguments)
    $userId = $null
    try {
        $currentIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
        $userId = $currentIdentity.User.Value
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
    <Description>LibreSpot reapplies SpotX automatically when Spotify updates itself.</Description>
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
    try {
        Ensure-LogDirectory
        [System.IO.File]::WriteAllText($xmlPath, $xml, [System.Text.Encoding]::Unicode)
        Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Unregister the scheduled task to undo.'
        if ($PSCmdlet.ShouldProcess($global:WATCHER_TASK_NAME, 'Register scheduled task')) {
            $output = & schtasks.exe /Create /TN $global:WATCHER_TASK_NAME /XML $xmlPath /F 2>&1
            $ok = ($LASTEXITCODE -eq 0)
            if ($ok) {
                Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Registered' -WouldChange $true -Reversible $true -RollbackHint 'Unregister the scheduled task to undo.'
                Write-WatcherLog "Register: scheduled task created for $($launch.Entry)"
            } else {
                Write-WatcherLog "Register failed (exit $LASTEXITCODE): $($output -join ' ')" -Level 'ERROR'
            }
            return $ok
        }
        return $false
    } catch {
        Write-WatcherLog "Register exception: $($_.Exception.Message)" -Level 'ERROR'
        return $false
    } finally {
        try { if (Test-Path -LiteralPath $xmlPath) { Remove-Item -LiteralPath $xmlPath -Force -ErrorAction SilentlyContinue } } catch {}
    }
}

function Save-LibreSpotConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([hashtable]$Config)

    Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
    if ($PSCmdlet.ShouldProcess($global:CONFIG_PATH, 'Save configuration')) {
        $tempPath = $null
        $backupPath = $null
        try {
            Ensure-LogDirectory
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
            Write-Log "Config save failed: $($_.Exception.Message)" -Level 'WARN'
            if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
            if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
            return $false
        }
    }
    return $false
}

function Invoke-HeadlessReapply {
    param([hashtable]$Config)
    if (-not $Config) { throw 'Invoke-HeadlessReapply: missing config.' }

    $destination = New-LibreSpotTempFile -Name 'spotx_watcher.ps1'
    $customPatchesPath = ''
    $watcher = Start-SpotifyWindowWatcher
    try {
        Write-WatcherLog 'Downloading pinned SpotX for watcher reapply'
        $spotxHash = $global:PinnedReleases.SpotX.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $destination -Label 'SpotX run.ps1')) {
            try {
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $destination
            } catch {
                if (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $destination -Label 'SpotX run.ps1') {
                    Write-WatcherLog 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $destination -ExpectedHash $spotxHash -Label 'SpotX run.ps1'
            Save-ToAssetCache -SourcePath $destination -SHA256Hash $spotxHash -Label 'SpotX run.ps1' -SourceUrl $global:URL_SPOTX
        }
        $params = Build-SpotXParams -Config $Config
        $customPatchesPath = New-SpotXCustomPatchesFile -Config $Config
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            $params = "$params -CustomPatchesPath `"$customPatchesPath`""
            Write-WatcherLog "Custom SpotX patches staged at $customPatchesPath"
        }
        Write-WatcherLog "Invoking SpotX with: $params"
        Invoke-ExternalScriptIsolated -FilePath $destination -Arguments $params -ExpectedHash $spotxHash -Label 'SpotX run.ps1'
        Reapply-SavedSpicetifySetup -Config $Config
        Write-WatcherLog 'Auto-reapply completed successfully.' -Level 'SUCCESS'
    } finally {
        Stop-SpotifyWindowWatcher -Watcher $watcher
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            Remove-Item -LiteralPath $customPatchesPath -Force -ErrorAction SilentlyContinue
        }
        Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-AutoReapplyWatcher {
    Write-WatcherLog '--- Watcher tick ---'

    $saved = $null
    try { $saved = Load-LibreSpotConfig } catch { Write-WatcherLog "Config load failed: $($_.Exception.Message)" -Level 'ERROR' }
    if (-not $saved -or -not (ConvertTo-ConfigBoolean -Value $saved['AutoReapply_Enabled'] -Default $false)) {
        Write-WatcherLog 'Auto-reapply preference is off; skipping.'
        return 0
    }

    $currentVersion = Get-InstalledSpotifyVersion
    if (-not $currentVersion) {
        Write-WatcherLog 'Spotify not installed; skipping.'
        return 0
    }

    $state = Get-WatcherState
    if (-not $state.LastKnownVersion) {
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Initialized' }
        Write-WatcherLog "Initialized last-known version to $currentVersion; no reapply on first tick."
        return 0
    }

    if ($currentVersion -eq $state.LastKnownVersion) {
        Write-WatcherLog "Spotify still at $currentVersion; nothing to do."
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'UpToDate' }
        return 0
    }

    Write-WatcherLog "Spotify version bump: $($state.LastKnownVersion) -> $currentVersion" -Level 'STEP'
    if (Test-SpotifyRunning) {
        Write-WatcherLog 'Spotify is running; deferring reapply to the next tick.'
        Set-WatcherState -State @{
            LastKnownVersion = $state.LastKnownVersion
            LastRunAt = (Get-Date -Format 'o')
            LastOutcome = 'DeferredSpotifyRunning'
            LastAttemptedSpotifyVersion = $currentVersion
        }
        return 0
    }

    try {
        Invoke-HeadlessReapply -Config $saved
        $now = Get-Date -Format 'o'
        Set-WatcherState -State @{
            LastKnownVersion = $currentVersion
            LastRunAt = $now
            LastOutcome = 'Reapplied'
            LastAppliedSpotifyVersion = $currentVersion
            LastAttemptedSpotifyVersion = $currentVersion
            LastSuccessfulApplyAt = $now
            LastApplyAt = $now
            LastApplyOutcome = 'WatcherReapplied'
            LastApplyError = $null
        }
        return 0
    } catch {
        Write-WatcherLog "Reapply failed: $($_.Exception.Message)" -Level 'ERROR'
        $now = Get-Date -Format 'o'
        $message = [string]$_.Exception.Message
        Set-WatcherState -State @{
            LastKnownVersion = $state.LastKnownVersion
            LastRunAt = $now
            LastOutcome = "Error: $message"
            LastAttemptedSpotifyVersion = $currentVersion
            LastApplyAt = $now
            LastApplyOutcome = 'WatcherFailed'
            LastApplyError = $message
        }
        return 1
    }
}

function Hide-SpotifyWindows {
    # In the WPF backend, the Start-SpotifyWindowWatcher runspace already polls
    # every 250ms and hides Spotify/SpotifyInstaller/SpotifySetup windows via
    # its own [LibreSpotWin32]::ShowWindowAsync. This stub satisfies call sites
    # shared with the monolith (which defines its own [Win32] P/Invoke type).
}

function Update-SpicetifyCliProgress {
    param([string]$Line)

    $plain = Remove-ConsoleEscapeSequences -Text $Line
    if ($plain -match 'Patching files\s*\[\s*(\d+)\s*/\s*(\d+)\s*\]') {
        $done = [int]$matches[1]
        $total = [Math]::Max(1, [int]$matches[2])
        $percent = [int][Math]::Min(99, [Math]::Floor(($done / $total) * 100))
        $progressValue = [int][Math]::Min(99, 86 + [Math]::Floor(($done / $total) * 12))
        Update-BackendState -Progress $progressValue -Status "Spicetify is patching Spotify files ($percent%)" -Step "Patching file $done of $total"
    } elseif ($plain -match 'Extracting backup|Preprocessing|Fetching remote CSS map|Patching files') {
        Update-BackendState -Progress 86 -Status 'Spicetify is preparing Spotify files' -Step 'Applying Spicetify setup'
    }
}

function Module-NukeSpotify {
    Write-Log 'Running the full Spotify cleanup path...' -Level 'STEP'
    $removedCount = 0

    Update-BackendState -Progress 5 -Status 'Preparing cleanup' -Step 'Closing Spotify processes'
    Stop-SpotifyProcesses

    Update-BackendState -Progress 10 -Status 'Removing Microsoft Store edition if present' -Step 'Checking installed packages'
    try {
        if ($PSVersionTable.PSVersion.Major -ge 7) { Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue }
        $storeApp = Get-AppxPackage -Name 'SpotifyAB.SpotifyMusic' -ErrorAction SilentlyContinue
        if ($storeApp) {
            Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedProgress = $ProgressPreference
            $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxPackage -Package $storeApp.PackageFullName -ErrorAction Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            } catch {
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry removal or reinstall from the Microsoft Store.'
                throw
            } finally { $ProgressPreference = $savedProgress }
            Write-Log 'Removed the Microsoft Store Spotify package.'
            $removedCount++
        } else {
            Write-Log 'No Microsoft Store Spotify package was detected.'
            Write-Log 'Continuing with desktop Spotify cleanup.'
        }
    } catch {
        Write-Log "Store package removal failed: $($_.Exception.Message)" -Level 'WARN'
    }

    # Remove the provisioned package so new user profiles don't get Spotify pre-installed
    try {
        $provisioned = Get-AppxProvisionedPackage -Online -EA SilentlyContinue | Where-Object { $_.DisplayName -eq 'SpotifyAB.SpotifyMusic' }
        if ($provisioned) {
            Write-OperationJournalEntry -Phase 'appx' -Target $provisioned.PackageName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedProgress = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxProvisionedPackage -Online -PackageName $provisioned.PackageName -EA Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $provisioned.PackageName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
                Write-Log 'Removed provisioned Spotify package.'
                $removedCount++
            } finally { $ProgressPreference = $savedProgress }
        }
    } catch { Write-Log "Provisioned package removal skipped: $($_.Exception.Message)" -Level 'WARN' }

    Update-BackendState -Progress 30 -Status 'Cleaning files, shortcuts, and leftovers' -Step 'Removing desktop state'
    $desktopPath = Get-DesktopPath
    $targets = @(
        @{ Path = (Join-Path $env:APPDATA 'Spotify'); Label = 'Spotify roaming data' }
        @{ Path = (Join-Path $env:LOCALAPPDATA 'Spotify'); Label = 'Spotify local data' }
        @{ Path = (Join-Path $env:APPDATA 'spicetify'); Label = 'Spicetify roaming data' }
        @{ Path = (Join-Path $env:LOCALAPPDATA 'spicetify'); Label = 'Spicetify CLI data' }
        @{ Path = (Join-Path $desktopPath 'Spotify.lnk'); Label = 'Desktop shortcut' }
        @{ Path = (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Spotify.lnk'); Label = 'Start menu shortcut' }
    )
    foreach ($target in $targets) {
        $removedCount += Remove-PathSafely -Path $target.Path -Label $target.Label
    }

    Update-BackendState -Progress 65 -Status 'Cleaning registry and scheduled tasks' -Step 'Removing shell traces'
    foreach ($key in @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify',
        'HKCU:\Software\Spotify',
        'HKCU:\Software\Classes\spotify',
        'HKCU:\Software\Classes\spotify-client',
        'HKCU:\Software\Microsoft\Internet Explorer\Low Rights\ElevationPolicy\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}',
        'HKCU:\Software\Microsoft\Internet Explorer\Low Rights\DragDrop\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\Spotify.exe'
    )) {
        if (Test-Path $key) {
            Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
            try {
                Remove-Item -Path $key -Recurse -Force -ErrorAction Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
                Write-Log "Removed registry key: $key"
                $removedCount++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
                Write-Log "Failed to remove registry key $key" -Level 'WARN'
            }
        }
    }

    foreach ($rv in @(
        @{ Path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'; Name = 'Spotify' },
        @{ Path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'; Name = 'Spotify Web Helper' }
    )) {
        if (Get-ItemProperty -Path $rv.Path -Name $rv.Name -EA SilentlyContinue) {
            $regTarget = "$($rv.Path)\$($rv.Name)"
            Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
            try {
                Remove-ItemProperty -Path $rv.Path -Name $rv.Name -Force -EA Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
                Write-Log "Removed startup entry: $($rv.Name)"
                $removedCount++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
            }
        }
    }

    try {
        # Match only tasks that Spotify itself registers. The previous `-match 'Spotify'`
        # would also remove user-authored tasks that merely mention Spotify (e.g. a
        # "MySpotifyBackup" job), which is surprising and destructive.
        $spotifyTaskNames = @('SpotifyMigrator', 'SpotifyUpdateTask', 'Spotify')
        Get-ScheduledTask -ErrorAction SilentlyContinue |
            Where-Object { $_.TaskName -in $spotifyTaskNames -or $_.TaskName -like 'Spotify-*' } |
            ForEach-Object {
                Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                try {
                    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -ErrorAction Stop
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                    Write-Log "Removed scheduled task: $($_.TaskName)"
                } catch {
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry scheduled task removal manually.'
                }
            }
    } catch {
        Write-Log 'Scheduled task cleanup was skipped.' -Level 'WARN'
    }

    Update-BackendState -Progress 85 -Status 'Performing final verification sweep' -Step 'Confirming removal'
    $verifyPaths = @(
        (Join-Path $env:APPDATA 'Spotify')
        (Join-Path $env:LOCALAPPDATA 'Spotify')
        (Join-Path $env:APPDATA 'spicetify')
        (Join-Path $env:LOCALAPPDATA 'spicetify')
    )
    $maxRetries = 5
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        $remaining = @($verifyPaths | Where-Object { Test-Path $_ })
        if ($remaining.Count -eq 0) { break }
        if ($attempt -gt 1) { Write-Log "Verification retry $attempt/$maxRetries ($($remaining.Count) path(s) still locked)..." }
        Start-Sleep -Milliseconds 1500
        foreach ($path in $remaining) {
            $removedCount += Remove-PathSafely -Path $path -Label "Cleanup retry: $(Split-Path $path -Leaf)"
        }
    }
    $survivors = @($verifyPaths | Where-Object { Test-Path $_ })
    if ($survivors.Count -gt 0) {
        Write-Log "Could not fully remove $($survivors.Count) path(s) (may need reboot):" -Level 'WARN'
        $survivors | ForEach-Object { Write-Log "  - $_" -Level 'WARN' }
    }

    Write-Log "Cleanup complete. $removedCount item(s) were removed." -Level 'SUCCESS'
}

function Module-ApplySpicetify {
    param(
        $Config,
        [string]$EvidenceSource = 'Module-ApplySpicetify'
    )
    Write-Log 'Applying Spicetify changes...' -Level 'STEP'

    # Marketplace-only mode: disable theme injection before apply so the apply step
    # does not try to inject any theme CSS/JS.
    if ($Config.Spicetify_Theme -eq '(None - Marketplace Only)') {
        try {
            Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '0', 'replace_colors', '0', 'overwrite_assets', '0', 'inject_theme_js', '0', '--bypass-admin') -FailureMessage 'Could not disable theme injection.'
        } catch {
            Write-Log "Pre-apply config tweak failed: $($_.Exception.Message)" -Level 'WARN'
        }
    }

    # Diagnostic snapshot. When apply fails silently (known SpotX+Spicetify
    # interop edge case) this is the only way to tell whether spotify_path is
    # correct and whether xpui.spa is actually present on disk.
    $diag = Get-SpicetifyDiagnosticSnapshot
    foreach ($key in $diag.Keys) {
        Write-Log "  diag: $key = $($diag[$key])"
    }

    # Make sure Spotify isn't holding any xpui.spa handles before Spicetify tries to
    # back it up. The earlier brief launch to generate configs should have been killed
    # already, but one final sweep is cheap insurance against "Spotify client is in
    # stock state" errors caused by a stale process still running.
    Stop-SpotifyProcesses -MaxAttempts 3

    # Run `spicetify backup apply` as a single combined command. Splitting it into two
    # invocations breaks on Spicetify CLI 2.43.1: the standalone `backup` subcommand
    # exits non-zero on fresh installs (reporting "Spotify version and backup version
    # are mismatched" with no prior backup present), leaving `apply` with nothing to
    # work from. The combined form matches the legacy LibreSpot.ps1 behavior and the
    # CLI's own "Please run 'spicetify backup apply'" hint.
    $applyError = $null
    $applyStage = 'backup apply'
    try {
        Invoke-SpicetifyCli -Arguments @('backup', 'apply', '--bypass-admin') -FailureMessage 'Could not backup and apply Spicetify changes.'
        Write-Log 'Spicetify applied successfully.' -Level 'SUCCESS'
        Update-ApplyState -Outcome 'SpicetifyApplySucceeded' -Successful $true
        $message = 'Spicetify backup apply succeeded.'
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $true -ApplyMessage $message | Out-Null
        return [pscustomobject]@{
            Stage     = $applyStage
            Succeeded = $true
            Message   = $message
        }
    } catch {
        $applyError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown Spicetify apply error.' }
        Write-Log "Spicetify apply failed: $applyError" -Level 'WARN'
    }

    # Apply failed — attempt rollback without nested try/catch so the reported
    # rollback status is accurate (the old nested form lied: a successful restore
    # threw a 'apply failed but restored' message that the inner catch caught and
    # re-reported as 'rollback also failed').
    Write-Log 'Attempting rollback to keep Spotify usable...' -Level 'WARN'
    $restoreError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage 'Could not restore Spotify after the failed apply.'
    } catch {
        $restoreError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown restore error.' }
    }

    if ([string]::IsNullOrWhiteSpace($restoreError)) {
        Update-ApplyState -Outcome 'SpicetifyApplyRolledBack' -Successful $false -ErrorMessage $applyError
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $false -ApplyMessage $applyError | Out-Null
        throw "Spicetify apply failed but LibreSpot restored Spotify to a usable state. Apply error: $applyError"
    } else {
        Update-ApplyState -Outcome 'SpicetifyApplyRollbackFailed' -Successful $false -ErrorMessage "Apply error: $applyError | Rollback error: $restoreError"
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $false -ApplyMessage "$applyError | Rollback error: $restoreError" | Out-Null
        throw "Spicetify apply failed and rollback also failed. Apply error: $applyError | Rollback error: $restoreError"
    }
}
