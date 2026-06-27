<#
.SYNOPSIS
    Validates that shared PowerShell functions between LibreSpot.ps1 and the
    WPF backend script remain in sync. Future: generates both scripts from
    shared source fragments.

.DESCRIPTION
    Extracts function bodies from both scripts and compares the 86+ shared
    functions for content drift. Any mismatch is reported as an error,
    preventing silent one-lane-only changes that cause feature or security
    regressions.

    Run this as part of CI to catch shared-function drift before release.

.EXAMPLE
    pwsh -File Build-Scripts.ps1 -Validate
    pwsh -File Build-Scripts.ps1 -Inventory
    pwsh -File Build-Scripts.ps1 -Lint

.NOTES
    Part of the "Extract shared PowerShell core logic" roadmap item (Cycle 11).
    The validation pass runs without modifying any files.
#>
[CmdletBinding()]
param(
    [switch]$Validate,
    [switch]$Inventory,
    [switch]$Lint,
    [switch]$SyncSharedToBackend
)

$ErrorActionPreference = 'Stop'

$mainScript = Join-Path $PSScriptRoot 'LibreSpot.ps1'
$backendScript = Join-Path $PSScriptRoot 'src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if (-not (Test-Path -LiteralPath $mainScript)) {
    throw "Cannot find LibreSpot.ps1 at $mainScript"
}
if (-not (Test-Path -LiteralPath $backendScript)) {
    throw "Cannot find LibreSpot.Backend.ps1 at $backendScript"
}

function Get-FunctionNames {
    param([string]$ScriptPath)
    $content = [System.IO.File]::ReadAllText($ScriptPath, [System.Text.Encoding]::UTF8)
    $names = [regex]::Matches($content, '(?m)^\s*function\s+([A-Za-z0-9_-]+)') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
    return $names
}

function Get-FunctionBody {
    param(
        [string]$ScriptContent,
        [string]$FunctionName
    )
    # Match a top-level function definition whose closing brace sits at column 0.
    # Escape the function name so hyphens and other regex-significant characters
    # are treated literally.
    $escapedName = [regex]::Escape($FunctionName)
    $pattern = "(?ms)^function\s+${escapedName}\s*\{.+?^\}"
    $match = [regex]::Match($ScriptContent, $pattern)
    if ($match.Success) {
        return $match.Value
    }
    return $null
}

function ConvertTo-NormalizedFunctionBody {
    param([string]$Body)
    if (-not $Body) { return '' }
    # Normalize whitespace for comparison:
    # - Trim each line
    # - Remove blank lines
    # - Collapse multiple spaces
    $lines = $Body -split "`r?`n" |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne '' }
    return ($lines -join "`n")
}

$mainContent = [System.IO.File]::ReadAllText($mainScript, [System.Text.Encoding]::UTF8)
$backendContent = [System.IO.File]::ReadAllText($backendScript, [System.Text.Encoding]::UTF8)

$mainFunctions = Get-FunctionNames -ScriptPath $mainScript
$backendFunctions = Get-FunctionNames -ScriptPath $backendScript

$sharedNames = $mainFunctions | Where-Object { $backendFunctions -contains $_ } | Sort-Object
$mainOnly = $mainFunctions | Where-Object { $backendFunctions -notcontains $_ } | Sort-Object
$backendOnly = $backendFunctions | Where-Object { $mainFunctions -notcontains $_ } | Sort-Object

# Functions where the backend has intentionally different implementations
# (different entry paths, arguments, or event protocols). These are shared
# by name but not by body; each lane owns its host-specific wrapper.
$laneSpecificFunctions = @(
    'Register-AutoReapplyTask'       # Main: -Watch flag; Backend: -Action WatchAutoReapply
    'Get-WatcherState'               # Backend extends with LastApplied/AttemptedSpotifyVersion
    'Get-WatcherLaunchCommand'       # Backend builds -Action args; Main builds -Watch args
    'Invoke-AutoReapplyWatcher'      # Backend uses Update-ApplyState; Main uses direct state writes
    'Invoke-HeadlessReapply'         # Backend delegates to Module-* with Update-BackendState
    'Set-WatcherState'               # Backend preserves extra state fields
    'Write-Log'                      # Main: Update-UI; Backend: Write-EventLine
    'Save-LibreSpotConfig'           # Backend: Update-BackendState progress; Main: GUI state
    'Load-LibreSpotConfig'           # Backend: different logging path
    'Update-SpicetifyCliProgress'    # Backend streams progress events; Main updates WPF controls directly
    'Module-NukeSpotify'             # Backend streams phase progress; Main owns GUI phase logging
    'Module-ApplySpicetify'          # Backend records watcher apply outcomes
)

if ($Inventory) {
    Write-Host "`n=== SHARED FUNCTION INVENTORY ===" -ForegroundColor Cyan
    Write-Host "Main script functions: $($mainFunctions.Count)"
    Write-Host "Backend script functions: $($backendFunctions.Count)"
    Write-Host "Shared functions: $($sharedNames.Count)"
    Write-Host "Main-only functions: $($mainOnly.Count)"
    Write-Host "Backend-only functions: $($backendOnly.Count)"

    Write-Host "`n--- Shared ($($sharedNames.Count)) ---" -ForegroundColor Green
    foreach ($fn in $sharedNames) { Write-Host "  $fn" }

    Write-Host "`n--- Main-only ($($mainOnly.Count)) ---" -ForegroundColor Yellow
    foreach ($fn in $mainOnly) { Write-Host "  $fn" }

    Write-Host "`n--- Backend-only ($($backendOnly.Count)) ---" -ForegroundColor Yellow
    foreach ($fn in $backendOnly) { Write-Host "  $fn" }

    Write-Host ""
    exit 0
}

if ($Validate) {
    Write-Host "Validating shared function sync between scripts..." -ForegroundColor Cyan
    Write-Host "  Main:    $mainScript ($($mainFunctions.Count) functions)"
    Write-Host "  Backend: $backendScript ($($backendFunctions.Count) functions)"
    Write-Host "  Shared:  $($sharedNames.Count) functions"
    Write-Host "  Excluded lane-specific: $($laneSpecificFunctions.Count) functions"
    Write-Host ""

    $drifted = @()
    $missing = @()
    $validatedNames = $sharedNames | Where-Object { $laneSpecificFunctions -notcontains $_ }

    foreach ($fn in $validatedNames) {
        $mainBody = Get-FunctionBody -ScriptContent $mainContent -FunctionName $fn
        $backendBody = Get-FunctionBody -ScriptContent $backendContent -FunctionName $fn

        if (-not $mainBody) {
            $missing += "${fn}: could not extract from main script"
            continue
        }
        if (-not $backendBody) {
            $missing += "${fn}: could not extract from backend script"
            continue
        }

        $mainNorm = ConvertTo-NormalizedFunctionBody -Body $mainBody
        $backendNorm = ConvertTo-NormalizedFunctionBody -Body $backendBody

        if ($mainNorm -ne $backendNorm) {
            $drifted += $fn
        }
    }

    if ($missing.Count -gt 0) {
        Write-Host "=== EXTRACTION FAILURES ===" -ForegroundColor Red
        foreach ($m in $missing) { Write-Host "  $m" -ForegroundColor Red }
        Write-Host ""
    }

    if ($drifted.Count -gt 0) {
        Write-Host "=== DRIFTED FUNCTIONS ($($drifted.Count)) ===" -ForegroundColor Red
        foreach ($fn in $drifted) {
            Write-Host "  $fn" -ForegroundColor Red
        }
        Write-Host ""
        Write-Host "These functions exist in both scripts but have different implementations." -ForegroundColor Red
        Write-Host "Update both scripts in the same commit to keep them in sync." -ForegroundColor Red
        Write-Host ""
        exit 1
    }

    Write-Host "All $($validatedNames.Count) generated shared functions are in sync." -ForegroundColor Green
    Write-Host "$($laneSpecificFunctions.Count) host-specific wrappers are excluded from body comparison." -ForegroundColor Green
    exit 0
}

if ($Lint) {
    $moduleName = 'PSScriptAnalyzer'
    if (-not (Get-Module -ListAvailable -Name $moduleName)) {
        Write-Host "Installing PSScriptAnalyzer..." -ForegroundColor Cyan
        Install-Module -Name $moduleName -Force -Scope CurrentUser -SkipPublisherCheck
    }
    Import-Module $moduleName -ErrorAction Stop

    $settingsPath = Join-Path $PSScriptRoot '.psscriptanalyzerrc.psd1'
    if (-not (Test-Path -LiteralPath $settingsPath)) {
        throw "PSScriptAnalyzer settings file not found at $settingsPath"
    }

    $scripts = @($mainScript, $backendScript)
    $totalIssues = 0

    foreach ($script in $scripts) {
        $name = Split-Path $script -Leaf
        Write-Host "Analyzing $name..." -ForegroundColor Cyan
        $results = Invoke-ScriptAnalyzer -Path $script -Settings $settingsPath -Recurse
        if ($results.Count -gt 0) {
            $totalIssues += $results.Count
            foreach ($r in $results) {
                $severity = $r.Severity.ToString().ToUpper()
                Write-Host "  [$severity] $($r.RuleName) at line $($r.Line): $($r.Message)" -ForegroundColor $(
                    switch ($r.Severity) { 'Error' { 'Red' } 'Warning' { 'Yellow' } default { 'Gray' } }
                )
            }
        } else {
            Write-Host "  No issues." -ForegroundColor Green
        }
    }

    if ($totalIssues -gt 0) {
        Write-Host "`n$totalIssues issue(s) found." -ForegroundColor Red
        exit 1
    }
    Write-Host "`nAll scripts pass PSScriptAnalyzer." -ForegroundColor Green
    exit 0
}

if ($SyncSharedToBackend) {
    $sharedDir = Join-Path $PSScriptRoot 'src/powershell/shared'
    if (-not (Test-Path -LiteralPath $sharedDir)) {
        throw "Shared source directory not found at $sharedDir"
    }

    $sharedFiles = Get-ChildItem -Path $sharedDir -Filter '*.ps1' -File | Sort-Object Name
    if ($sharedFiles.Count -eq 0) {
        throw "No .ps1 files found in $sharedDir"
    }

    Write-Host "Syncing shared functions to backend script..." -ForegroundColor Cyan
    Write-Host "  Source:     $sharedDir ($($sharedFiles.Count) files)" -ForegroundColor Gray
    Write-Host "  Exclusions: $($laneSpecificFunctions.Count) lane-specific functions" -ForegroundColor Gray
    Write-Host ""

    $backendContent = [System.IO.File]::ReadAllText($backendScript, [System.Text.Encoding]::UTF8)
    $updatedCount = 0
    $skippedCount = 0
    $excludedCount = 0

    foreach ($file in $sharedFiles) {
        $fnName = $file.BaseName

        if ($laneSpecificFunctions -contains $fnName) {
            Write-Host "  EXCL $fnName (lane-specific)" -ForegroundColor DarkGray
            $excludedCount++
            continue
        }

        $sharedBody = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)

        $existingBody = Get-FunctionBody -ScriptContent $backendContent -FunctionName $fnName
        if (-not $existingBody) {
            Write-Host "  SKIP $fnName (not found in backend)" -ForegroundColor Yellow
            $skippedCount++
            continue
        }

        $sharedNorm = ConvertTo-NormalizedFunctionBody -Body $sharedBody
        $existingNorm = ConvertTo-NormalizedFunctionBody -Body $existingBody

        if ($sharedNorm -ne $existingNorm) {
            $backendContent = $backendContent.Replace($existingBody, $sharedBody.TrimEnd())
            Write-Host "  UPDATED $fnName" -ForegroundColor Green
            $updatedCount++
        }
    }

    if ($updatedCount -gt 0) {
        [System.IO.File]::WriteAllText($backendScript, $backendContent, $utf8NoBom)
    }
    Write-Host "`n$updatedCount synced, $excludedCount excluded (lane-specific), $skippedCount skipped (not in backend)." -ForegroundColor Green
    exit 0
}

# Default: show usage
Write-Host "Usage:"
Write-Host "  pwsh -File Build-Scripts.ps1 -Validate             # Check shared functions for drift"
Write-Host "  pwsh -File Build-Scripts.ps1 -Inventory             # List all functions and their locations"
Write-Host "  pwsh -File Build-Scripts.ps1 -Lint                   # Run PSScriptAnalyzer on both scripts"
Write-Host "  pwsh -File Build-Scripts.ps1 -SyncSharedToBackend   # Copy shared function sources into backend"
