[CmdletBinding()]
param([ValidateRange(10, 120)][int]$TimeoutSeconds = 30)

$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') {
    throw 'Watcher integration requires Windows Task Scheduler.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$hostPath = Join-Path $PSScriptRoot 'WatcherIntegrationHost.ps1'
if (-not (Test-Path -LiteralPath $hostPath -PathType Leaf)) {
    throw "Watcher integration host not found: $hostPath"
}

$id = [Guid]::NewGuid().ToString('N')
$taskName = "\LibreSpot-WatcherIntegration-$id"
$root = Join-Path ([System.IO.Path]::GetTempPath()) "LibreSpot.WatcherIntegration.$id"
$configDirectory = Join-Path $root 'config'
$statePath = Join-Path $configDirectory 'watcher-state.json'
$scenarioPath = Join-Path $root 'scenario.json'
$xmlPath = Join-Path $root 'task.xml'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$startedAt = Get-Date
$failed = $false

function Assert-WatcherCondition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-IntegrationTask {
    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    return $service.GetFolder('\').GetTask($taskName.TrimStart('\'))
}

function Wait-ForPath {
    param([string]$Path, [int]$Seconds = $TimeoutSeconds)
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) { return $true }
        Start-Sleep -Milliseconds 100
    }
    return $false
}

function Set-Scenario {
    param([string]$Scenario)
    $runId = [Guid]::NewGuid().ToString('N')
    $initialState = [ordered]@{
        LastKnownVersion = '1.0.0.0'
        LastRunAt = '2026-07-14T00:00:00.0000000Z'
        LastOutcome = 'Seeded'
    }
    [System.IO.File]::WriteAllText($statePath, ($initialState | ConvertTo-Json -Compress), $utf8NoBom)
    [System.IO.File]::WriteAllText(
        $scenarioPath,
        ([ordered]@{ schemaVersion = 1; scenario = $Scenario; runId = $runId } | ConvertTo-Json -Compress),
        $utf8NoBom)
    return $runId
}

function Start-Scenario {
    param([string]$Scenario)
    $runId = Set-Scenario -Scenario $Scenario
    $resultPath = Join-Path $root "result-$runId.json"
    $startOutput = & schtasks.exe /Run /TN $taskName 2>&1
    Assert-WatcherCondition ($LASTEXITCODE -eq 0) "Could not start $Scenario watcher task: $($startOutput -join ' ')"
    Assert-WatcherCondition (Wait-ForPath -Path $resultPath) "$Scenario watcher tick did not complete within $TimeoutSeconds seconds."
    return (Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Get-FailureEvidence {
    $task = $null
    try { $task = Get-IntegrationTask } catch {}
    $events = @()
    $eventError = $null
    $operationalLogEnabled = $null
    try {
        $operationalLogEnabled = [bool](Get-WinEvent -ListLog 'Microsoft-Windows-TaskScheduler/Operational' -ErrorAction Stop).IsEnabled
        $events = @(Get-WinEvent -FilterHashtable @{
            LogName = 'Microsoft-Windows-TaskScheduler/Operational'
            StartTime = $startedAt
        } -ErrorAction Stop | Where-Object { $_.Message -like "*$taskName*" } | Select-Object -First 20 |
            ForEach-Object {
                [ordered]@{
                    timeCreated = $_.TimeCreated.ToUniversalTime().ToString('o')
                    id = $_.Id
                    level = $_.LevelDisplayName
                    message = $_.Message
                }
            })
    } catch { $eventError = $_.Exception.Message }

    return [ordered]@{
        taskName = $taskName
        taskState = if ($task) { [int]$task.State } else { $null }
        lastTaskResult = if ($task) { [int]$task.LastTaskResult } else { $null }
        operationalLogEnabled = $operationalLogEnabled
        operationalEvents = $events
        operationalEventError = $eventError
    }
}

try {
    New-Item -Path $configDirectory -ItemType Directory -Force | Out-Null
    $powerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    Assert-WatcherCondition (Test-Path -LiteralPath $powerShell -PathType Leaf) "Windows PowerShell was not found at $powerShell."
    $userSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$hostPath`" -Root `"$root`" -RepositoryRoot `"$repositoryRoot`""
    $escapedPowerShell = [System.Security.SecurityElement]::Escape($powerShell)
    $escapedArguments = [System.Security.SecurityElement]::Escape($arguments)
    $escapedSid = [System.Security.SecurityElement]::Escape($userSid)
    $escapedTaskName = [System.Security.SecurityElement]::Escape($taskName)
    $xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>LibreSpot integration tests</Author>
    <Description>Disposable standard-user watcher process-boundary test.</Description>
    <URI>$escapedTaskName</URI>
  </RegistrationInfo>
  <Principals>
    <Principal id="Author">
      <UserId>$escapedSid</UserId>
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
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT5M</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$escapedPowerShell</Command>
      <Arguments>$escapedArguments</Arguments>
    </Exec>
  </Actions>
</Task>
"@
    [System.IO.File]::WriteAllText($xmlPath, $xml, [System.Text.Encoding]::Unicode)

    $createOutput = & schtasks.exe /Create /TN $taskName /XML $xmlPath /F 2>&1
    Assert-WatcherCondition ($LASTEXITCODE -eq 0) "Task registration failed: $($createOutput -join ' ')"

    [xml]$exported = ((& schtasks.exe /Query /TN $taskName /XML 2>&1) -join [Environment]::NewLine)
    Assert-WatcherCondition ($LASTEXITCODE -eq 0) 'Task XML export failed.'
    Write-Verbose ("Exported task XML: " + $exported.OuterXml)
    $runLevel = [string]$exported.Task.Principals.Principal.RunLevel
    $logonType = [string]$exported.Task.Principals.Principal.LogonType
    $multipleInstances = [string]$exported.Task.Settings.MultipleInstancesPolicy
    $executionTimeLimit = [string]$exported.Task.Settings.ExecutionTimeLimit
    $exportedCommand = [string]$exported.Task.Actions.Exec.Command
    $registeredTask = Get-IntegrationTask
    $registeredRunLevel = [int]$registeredTask.Definition.Principal.RunLevel
    Assert-WatcherCondition ($registeredRunLevel -eq 0 -and $runLevel -ne 'HighestAvailable') "Watcher task is not least privilege (COM level $registeredRunLevel, exported '$runLevel')."
    Assert-WatcherCondition ($logonType -eq 'InteractiveToken') "Watcher task does not use the current interactive token (exported '$logonType')."
    Assert-WatcherCondition ($multipleInstances -eq 'IgnoreNew') "Watcher task can overlap itself (exported '$multipleInstances')."
    Assert-WatcherCondition ($executionTimeLimit -eq 'PT5M') "Watcher task lacks its integration time limit (exported '$executionTimeLimit')."
    Assert-WatcherCondition ($exportedCommand -eq $powerShell) "Watcher task command drifted (exported '$exportedCommand')."

    $expectations = @(
        @{ Scenario = 'Success'; ExitCode = 0; Outcome = 'Reapplied' },
        @{ Scenario = 'Disabled'; ExitCode = 0; Outcome = 'PreferenceOff' },
        @{ Scenario = 'CorruptConfig'; ExitCode = 0; Outcome = 'NoConfig' },
        @{ Scenario = 'NetworkUnavailable'; ExitCode = 1; Outcome = 'Error:' },
        @{ Scenario = 'ActiveSpotify'; ExitCode = 0; Outcome = 'DeferredSpotifyRunning' },
        @{ Scenario = 'InterruptedStateWrite'; ExitCode = 0; Outcome = 'Seeded' }
    )
    foreach ($expectation in $expectations) {
        $result = Start-Scenario -Scenario $expectation.Scenario
        Assert-WatcherCondition ([int]$result.exitCode -eq $expectation.ExitCode) "$($expectation.Scenario) returned $($result.exitCode), expected $($expectation.ExitCode)."
        Assert-WatcherCondition ([string]$result.state.LastOutcome -like "$($expectation.Outcome)*") "$($expectation.Scenario) ended as '$($result.state.LastOutcome)'."
        Assert-WatcherCondition (@($result.tempArtifacts).Count -eq 0) "$($expectation.Scenario) left state temp artifacts behind."
        if ($expectation.Scenario -eq 'InterruptedStateWrite') {
            Assert-WatcherCondition ([bool]$result.stateWritePreserved) 'Interrupted state write did not preserve the previous state file.'
        }
    }

    $cancellationRunId = Set-Scenario -Scenario 'Cancellation'
    $cancelReadyPath = Join-Path $root "cancel-ready-$cancellationRunId.txt"
    $runOutput = & schtasks.exe /Run /TN $taskName 2>&1
    Assert-WatcherCondition ($LASTEXITCODE -eq 0) "Could not start cancellation scenario: $($runOutput -join ' ')"
    Assert-WatcherCondition (Wait-ForPath -Path $cancelReadyPath) 'Cancellation scenario never entered the reapply boundary.'
    $endOutput = & schtasks.exe /End /TN $taskName 2>&1
    Assert-WatcherCondition ($LASTEXITCODE -eq 0) "Could not cancel watcher task: $($endOutput -join ' ')"
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline -and (Get-IntegrationTask).State -eq 4) { Start-Sleep -Milliseconds 100 }
    Assert-WatcherCondition ((Get-IntegrationTask).State -ne 4) 'Cancelled watcher task remained running.'
    $cancelState = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-WatcherCondition ($cancelState.LastOutcome -eq 'Seeded') 'Task cancellation partially advanced watcher state.'

    Write-Host "Watcher integration passed for task $taskName (7 scenarios)." -ForegroundColor Green
} catch {
    $failed = $true
    $evidence = Get-FailureEvidence
    Write-Warning ("Watcher integration evidence: " + ($evidence | ConvertTo-Json -Depth 6 -Compress))
    throw
} finally {
    try { $null = & schtasks.exe /End /TN $taskName 2>&1 } catch {}
    try { $null = & schtasks.exe /Delete /TN $taskName /F 2>&1 } catch {}
    try { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue } catch {}
    if ($failed) { Write-Warning "Removed disposable task $taskName and isolated test data after failure." }
}
