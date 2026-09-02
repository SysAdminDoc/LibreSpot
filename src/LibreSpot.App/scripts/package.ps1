[CmdletBinding()]
param(
    [string]$InputDirectory = (Join-Path $PSScriptRoot '..\dist'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\..\..\resources\custom-apps\librespot-engine.zip')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

$requiredFiles = @(
    @{ Source = (Join-Path $InputDirectory 'index.js'); Entry = 'librespot/index.js' },
    @{ Source = (Join-Path $InputDirectory 'style.css'); Entry = 'librespot/style.css' },
    @{ Source = (Join-Path $InputDirectory 'manifest.json'); Entry = 'librespot/manifest.json' },
    @{ Source = (Join-Path $InputDirectory 'librespot-engine.js'); Entry = 'librespot/librespot-engine.js' },
    @{ Source = (Join-Path $PSScriptRoot '..\LICENSE'); Entry = 'librespot/LICENSE' },
    @{ Source = (Join-Path $PSScriptRoot '..\THIRD_PARTY_NOTICES.md'); Entry = 'librespot/THIRD_PARTY_NOTICES.md' }
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $file.Source -PathType Leaf)) {
        throw "Cannot package LibreSpot because '$($file.Source)' is missing."
    }
}

$packageMetadata = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\package.json') -Raw | ConvertFrom-Json
$manifestMetadata = Get-Content -LiteralPath (Join-Path $InputDirectory 'manifest.json') -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$manifestMetadata.version) -or
    [string]$manifestMetadata.version -ne [string]$packageMetadata.version) {
    throw "Cannot package LibreSpot because manifest version '$($manifestMetadata.version)' does not match package version '$($packageMetadata.version)'."
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutput)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$temporaryPath = "$resolvedOutput.tmp"
Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue

$stream = $null
$archive = $null
try {
    $stream = [System.IO.File]::Open($temporaryPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $archive = [System.IO.Compression.ZipArchive]::new($stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    $fixedTimestamp = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    foreach ($file in $requiredFiles | Sort-Object Entry) {
        $entry = $archive.CreateEntry($file.Entry, [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $fixedTimestamp
        $entryStream = $entry.Open()
        try {
            $sourceStream = [System.IO.File]::OpenRead($file.Source)
            try {
                $sourceStream.CopyTo($entryStream)
            } finally {
                $sourceStream.Dispose()
            }
        } finally {
            $entryStream.Dispose()
        }
    }
} finally {
    if ($archive) { $archive.Dispose() }
    if ($stream) { $stream.Dispose() }
}

[System.IO.File]::Move($temporaryPath, $resolvedOutput, $true)
$hash = (Get-FileHash -LiteralPath $resolvedOutput -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "LibreSpot custom-app archive: $resolvedOutput"
Write-Host "SHA256: $hash"
