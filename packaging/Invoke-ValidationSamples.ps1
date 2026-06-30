[CmdletBinding()]
param(
    [ValidateSet('all', 'winget', 'scoop', 'chocolatey')]
    [string]$Tool = 'all',

    [string]$PublishRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'publish'),

    [string]$ManifestPath,

    [switch]$RunInstallChecks
)

$ErrorActionPreference = 'Stop'

function Test-ToolAvailable {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name is not available on PATH. Install it before running this validation sample."
    }
}

function Get-FileSha256Lower {
    param([Parameter(Mandatory)][string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return (($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) -join '')
    } finally {
        $stream.Dispose()
        $sha.Dispose()
    }
}

function Test-ReleaseManifestForPackageValidation {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Root
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Release manifest not found at $Path. Generate it with Build-Scripts.ps1 -GenerateReleaseManifest before package validation."
    }

    $manifest = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    if ([int]$manifest.schemaVersion -ne 1) {
        throw "Release manifest schemaVersion must be 1."
    }

    $artifacts = @($manifest.artifacts)
    if ($artifacts.Count -eq 0) {
        throw "Release manifest does not contain any artifacts."
    }

    foreach ($role in @('stable-script-exe', 'desktop-shell', 'fleet-cli', 'checksums', 'sbom')) {
        if (-not ($artifacts | Where-Object { [string]$_.packageRole -eq $role })) {
            throw "Release manifest is missing package role '$role'."
        }
    }

    foreach ($artifact in $artifacts) {
        foreach ($field in @('name', 'version', 'packageRole', 'runtimeIdentifier', 'buildMode', 'path')) {
            if (-not $artifact.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$artifact.$field)) {
                throw "Release manifest artifact '$($artifact.name)' is missing '$field'."
            }
        }

        if ([bool]$artifact.selfReferential) {
            if ((Split-Path -Leaf $Path) -ne [string]$artifact.name) {
                throw "Only the release manifest artifact may be self-referential."
            }
            continue
        }

        if ([string]$artifact.sha256 -match 'PLACEHOLDER' -or [string]::IsNullOrWhiteSpace([string]$artifact.sha256)) {
            throw "Release manifest artifact '$($artifact.name)' has a placeholder or empty SHA256."
        }

        $artifactPath = Join-Path $Root ([string]$artifact.path)
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "Release manifest references missing artifact '$artifactPath'."
        }

        if ((Get-FileSha256Lower -Path $artifactPath) -ne [string]$artifact.sha256) {
            throw "Release manifest SHA256 drift detected for '$($artifact.name)'."
        }

        if ((Get-Item -LiteralPath $artifactPath).Length -ne [int64]$artifact.sizeBytes) {
            throw "Release manifest byte-size drift detected for '$($artifact.name)'."
        }
    }

    $packageFiles = Get-ChildItem -LiteralPath $Root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in '.yaml', '.json', '.ps1', '.nuspec' -and $_.FullName -ne $Path }

    foreach ($file in $packageFiles) {
        $content = Get-Content -Raw -LiteralPath $file.FullName
        if ($content -match 'PLACEHOLDER_SHA256') {
            throw "Package validation cannot run with placeholder hashes in $($file.FullName). Regenerate package files from the release manifest first."
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $PublishRoot 'librespot-release-manifest.json'
}

$selected = if ($Tool -eq 'all') { @('winget', 'scoop', 'chocolatey') } else { @($Tool) }

Test-ReleaseManifestForPackageValidation -Path $ManifestPath -Root $PublishRoot

foreach ($name in $selected) {
    switch ($name) {
        'winget' {
            Test-ToolAvailable winget
            $wingetRoot = Join-Path $PublishRoot 'winget'
            # PACKAGE-CHECK: winget validate .\publish\winget\
            & winget validate $wingetRoot
        }
        'scoop' {
            Test-ToolAvailable scoop
            $scoopManifest = Join-Path $PublishRoot 'scoop\librespot.json'
            # PACKAGE-CHECK: scoop install .\publish\scoop\librespot.json
            # PACKAGE-CHECK: .\bin\checkver.ps1 librespot -u -dir .\publish\scoop\
            if ($RunInstallChecks) {
                & scoop install $scoopManifest
            } else {
                Write-Information "Skipping Scoop install check; pass -RunInstallChecks in a disposable VM to execute it." -InformationAction Continue
            }
        }
        'chocolatey' {
            Test-ToolAvailable choco
            $chocolateyRoot = Join-Path $PublishRoot 'chocolatey'
            Push-Location $chocolateyRoot
            try {
                # PACKAGE-CHECK: choco pack
                & choco pack
                if ($RunInstallChecks) {
                    # PACKAGE-CHECK: choco install librespot --source .
                    & choco install librespot --source . --yes
                } else {
                    Write-Information "Skipping Chocolatey install check; pass -RunInstallChecks in a disposable VM to execute it." -InformationAction Continue
                }
            } finally {
                Pop-Location
            }
        }
    }
}
