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
