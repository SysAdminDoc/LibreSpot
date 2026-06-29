[CmdletBinding()]
param(
    [switch]$Validate,
    [switch]$PrefillMissing,
    [switch]$ScanRawStrings
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot 'src/LibreSpot.Desktop/Properties/Strings.resx'
$propertiesDir = Split-Path -Parent $sourcePath
$cultures = @('en', 'ru', 'zh-Hans', 'pt-BR', 'es')
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Source resource file not found at $sourcePath"
}

function Get-ResxEntries {
    param([string]$Path)

    [xml]$doc = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $entries = [ordered]@{}
    foreach ($data in $doc.root.data) {
        $entries[[string]$data.name] = [pscustomobject]@{
            Value = [string]$data.value
            Comment = [string]$data.comment
        }
    }
    return $entries
}

function Save-PrefilledResx {
    param(
        [string]$Culture,
        [string]$TargetPath
    )

    $content = [System.IO.File]::ReadAllText($sourcePath, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($TargetPath, $content, $utf8NoBom)
    Write-Host "Prefilled missing $Culture resource file from source strings." -ForegroundColor Yellow
}

function Test-RawXamlStrings {
    $xamlPath = Join-Path $repoRoot 'src/LibreSpot.Desktop/MainWindow.xaml'
    $content = [System.IO.File]::ReadAllText($xamlPath, [System.Text.Encoding]::UTF8)
    $pattern = '\b(?:Text|Content|Header|ToolTip|Description|Title|AutomationProperties\.Name|AutomationProperties\.HelpText)="(?<value>[^\{][^"]*)"'
    $violations = [regex]::Matches($content, $pattern) |
        Where-Object {
            $value = $_.Groups['value'].Value
            -not [string]::IsNullOrWhiteSpace($value) -and
            -not $value.StartsWith('pack://', [System.StringComparison]::OrdinalIgnoreCase)
        } |
        ForEach-Object { $_.Value }

    if ($violations.Count -gt 0) {
        Write-Host "Raw user-facing XAML strings must use resource bindings:" -ForegroundColor Red
        foreach ($violation in $violations) {
            Write-Host "  $violation" -ForegroundColor Red
        }
        exit 1
    }
}

$sourceEntries = Get-ResxEntries -Path $sourcePath
$sourceKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($key in $sourceEntries.Keys) {
    $null = $sourceKeys.Add($key)
}

$failures = New-Object System.Collections.Generic.List[string]

foreach ($culture in $cultures) {
    $targetPath = Join-Path $propertiesDir "Strings.$culture.resx"
    if (-not (Test-Path -LiteralPath $targetPath)) {
        if ($PrefillMissing) {
            Save-PrefilledResx -Culture $culture -TargetPath $targetPath
        } else {
            $failures.Add("Missing resource file: Strings.$culture.resx")
            continue
        }
    }

    $targetEntries = Get-ResxEntries -Path $targetPath
    $targetKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($key in $targetEntries.Keys) {
        $null = $targetKeys.Add($key)
    }

    foreach ($key in $sourceKeys) {
        if (-not $targetKeys.Contains($key)) {
            $failures.Add("Strings.$culture.resx is missing key '$key'")
            continue
        }

        if ([string]::IsNullOrWhiteSpace($targetEntries[$key].Value)) {
            $failures.Add("Strings.$culture.resx key '$key' has an empty value")
        }
    }

    foreach ($key in $targetKeys) {
        if (-not $sourceKeys.Contains($key)) {
            $failures.Add("Strings.$culture.resx has stale key '$key'")
        }
    }
}

if ($ScanRawStrings) {
    Test-RawXamlStrings
}

if ($Validate -or $ScanRawStrings) {
    if ($failures.Count -gt 0) {
        Write-Host "Localization validation failed:" -ForegroundColor Red
        foreach ($failure in $failures) {
            Write-Host "  $failure" -ForegroundColor Red
        }
        exit 1
    }

    Write-Host "Localization resources are complete for $($cultures -join ', ')." -ForegroundColor Green
    if ($ScanRawStrings) {
        Write-Host "No raw user-facing XAML strings found." -ForegroundColor Green
    }
}
