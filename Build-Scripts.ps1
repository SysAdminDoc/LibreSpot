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
    [switch]$Lint
)

$ErrorActionPreference = 'Stop'

$mainScript = Join-Path $PSScriptRoot 'LibreSpot.ps1'
$backendScript = Join-Path $PSScriptRoot 'src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1'

if (-not (Test-Path -LiteralPath $mainScript)) {
    throw "Cannot find LibreSpot.ps1 at $mainScript"
}
if (-not (Test-Path -LiteralPath $backendScript)) {
    throw "Cannot find LibreSpot.Backend.ps1 at $backendScript"
}

function Get-FunctionNames {
    param([string]$ScriptPath)
    $content = Get-Content -Path $ScriptPath -Raw
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

$mainContent = Get-Content -Path $mainScript -Raw
$backendContent = Get-Content -Path $backendScript -Raw

$mainFunctions = Get-FunctionNames -ScriptPath $mainScript
$backendFunctions = Get-FunctionNames -ScriptPath $backendScript

$sharedNames = $mainFunctions | Where-Object { $backendFunctions -contains $_ } | Sort-Object
$mainOnly = $mainFunctions | Where-Object { $backendFunctions -notcontains $_ } | Sort-Object
$backendOnly = $backendFunctions | Where-Object { $mainFunctions -notcontains $_ } | Sort-Object

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
    Write-Host ""

    $drifted = @()
    $missing = @()

    foreach ($fn in $sharedNames) {
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

    Write-Host "All $($sharedNames.Count) shared functions are in sync." -ForegroundColor Green
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

# Default: show usage
Write-Host "Usage:"
Write-Host "  pwsh -File Build-Scripts.ps1 -Validate    # Check shared functions for drift"
Write-Host "  pwsh -File Build-Scripts.ps1 -Inventory   # List all functions and their locations"
Write-Host "  pwsh -File Build-Scripts.ps1 -Lint         # Run PSScriptAnalyzer on both scripts"
