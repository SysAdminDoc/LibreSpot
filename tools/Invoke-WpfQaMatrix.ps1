[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$OutputPath,

    [switch]$Quick
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $repoRoot 'tests\LibreSpot.Desktop.Tests\LibreSpot.Desktop.Tests.csproj'
$temporaryOutput = [string]::IsNullOrWhiteSpace($OutputPath)
if ($temporaryOutput) {
    $OutputPath = Join-Path ([System.IO.Path]::GetTempPath()) ('LibreSpot.WpfQa.' + [guid]::NewGuid().ToString('N'))
}
else {
    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
}

[System.IO.Directory]::CreateDirectory($OutputPath) | Out-Null
$previousCaptureRoot = [Environment]::GetEnvironmentVariable('LIBRESPOT_QA_CAPTURE_ROOT', 'Process')
$previousQuick = [Environment]::GetEnvironmentVariable('LIBRESPOT_QA_QUICK', 'Process')

try {
    $env:LIBRESPOT_QA_CAPTURE_ROOT = $OutputPath
    $env:LIBRESPOT_QA_QUICK = if ($Quick) { '1' } else { '0' }

    & dotnet test $testProject `
        --configuration $Configuration `
        --filter 'FullyQualifiedName~WpfQaMatrixTests' `
        --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) {
        throw "WPF QA matrix failed with exit code $LASTEXITCODE. Captures remain at $OutputPath."
    }

    $captures = @(Get-ChildItem -LiteralPath $OutputPath -Filter '*.png' -File)
    $minimumCount = if ($Quick) { 15 } else { 56 }
    if ($captures.Count -lt $minimumCount) {
        throw "WPF QA matrix produced $($captures.Count) captures; expected at least $minimumCount."
    }

    if ($temporaryOutput) {
        Remove-Item -LiteralPath $OutputPath -Recurse -Force
        Write-Output "WPF QA matrix passed ($($captures.Count) captures verified; temporary captures removed)."
    }
    else {
        Write-Output "WPF QA matrix passed ($($captures.Count) captures): $OutputPath"
    }
}
finally {
    [Environment]::SetEnvironmentVariable('LIBRESPOT_QA_CAPTURE_ROOT', $previousCaptureRoot, 'Process')
    [Environment]::SetEnvironmentVariable('LIBRESPOT_QA_QUICK', $previousQuick, 'Process')
}
