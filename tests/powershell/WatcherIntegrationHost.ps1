[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Root,
    [Parameter(Mandatory)][string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
$scenarioPath = Join-Path $Root 'scenario.json'
if (-not (Test-Path -LiteralPath $scenarioPath -PathType Leaf)) {
    throw "Watcher integration scenario not found: $scenarioPath"
}

$scenarioDocument = Get-Content -LiteralPath $scenarioPath -Raw -Encoding UTF8 | ConvertFrom-Json
$scenario = [string]$scenarioDocument.scenario
$runId = [string]$scenarioDocument.runId
$resultPath = Join-Path $Root "result-$runId.json"
$startedPath = Join-Path $Root "started-$runId.txt"
$cancellationReadyPath = Join-Path $Root "cancel-ready-$runId.txt"
$global:CONFIG_DIR = Join-Path $Root 'config'
$global:WATCHER_STATE_PATH = Join-Path $global:CONFIG_DIR 'watcher-state.json'
$global:WATCHER_LOG_PATH = Join-Path $global:CONFIG_DIR 'watcher.log'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $Root "host-progress-$runId.txt"), 'loading-functions', $utf8NoBom)

foreach ($path in @(
    'src/powershell/lane-specific/Get-WatcherState.ps1',
    'src/powershell/lane-specific/Set-WatcherState.ps1',
    'src/powershell/lane-specific/Invoke-AutoReapplyWatcher.ps1'
)) {
    . (Join-Path $RepositoryRoot $path)
}
[System.IO.File]::WriteAllText((Join-Path $Root "host-progress-$runId.txt"), 'functions-loaded', $utf8NoBom)

function Write-WatcherLog {
    param([string]$Message, [string]$Level = 'INFO')
    if (-not (Test-Path -LiteralPath $global:CONFIG_DIR -PathType Container)) {
        New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
    }
    $line = '[{0}] [{1}] {2}' -f (Get-Date -Format 'o'), $Level, $Message
    [System.IO.File]::AppendAllText($global:WATCHER_LOG_PATH, $line + [Environment]::NewLine, $utf8NoBom)
}

function Get-InstalledSpotifyVersion { return '2.0.0.0' }
function Test-SpotifyRunning { return ($scenario -eq 'ActiveSpotify') }
function ConvertTo-ConfigBoolean {
    param($Value, [bool]$Default = $false)
    if ($null -eq $Value) { return $Default }
    return [bool]$Value
}
function Normalize-LibreSpotConfig { param($Config) return $Config }
function Load-LibreSpotConfig {
    if ($scenario -eq 'CorruptConfig') { throw 'Synthetic corrupt config.' }
    return @{ AutoReapply_Enabled = ($scenario -ne 'Disabled') }
}
function Invoke-HeadlessReapply {
    param([hashtable]$Config)
    if ($scenario -eq 'NetworkUnavailable') {
        throw [System.Net.WebException]::new('Synthetic network unavailable.')
    }
    if ($scenario -eq 'Cancellation') {
        [System.IO.File]::WriteAllText($cancellationReadyPath, 'ready', $utf8NoBom)
        Start-Sleep -Seconds 300
    }
}

if (-not (Test-Path -LiteralPath $global:CONFIG_DIR -PathType Container)) {
    New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
}
[System.IO.File]::WriteAllText($startedPath, $scenario, $utf8NoBom)

$stateLock = $null
$initialStateBytes = $null
try {
    if ($scenario -eq 'InterruptedStateWrite') {
        $initialStateBytes = [System.IO.File]::ReadAllBytes($global:WATCHER_STATE_PATH)
        $stateLock = [System.IO.File]::Open(
            $global:WATCHER_STATE_PATH,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
    }

    $exitCode = Invoke-AutoReapplyWatcher
} finally {
    if ($stateLock) { $stateLock.Dispose() }
}

$state = Get-WatcherState
$tempArtifacts = @(Get-ChildItem -LiteralPath $global:CONFIG_DIR -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^watcher-state\..+\.(tmp|bak|rescue)$' } |
    ForEach-Object { $_.Name })
$stateWritePreserved = $null
if ($scenario -eq 'InterruptedStateWrite') {
    $currentStateBytes = [System.IO.File]::ReadAllBytes($global:WATCHER_STATE_PATH)
    $stateWritePreserved = ([Convert]::ToBase64String($initialStateBytes) -eq [Convert]::ToBase64String($currentStateBytes))
}

$result = [ordered]@{
    schemaVersion = 1
    runId = $runId
    scenario = $scenario
    exitCode = [int]$exitCode
    state = $state
    stateWritePreserved = $stateWritePreserved
    tempArtifacts = $tempArtifacts
    completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
}
[System.IO.File]::WriteAllText($resultPath, ($result | ConvertTo-Json -Depth 6), $utf8NoBom)
exit $exitCode
