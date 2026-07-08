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
    [switch]$SyncSharedToBackend,
    [switch]$SyncSharedToMain,
    [switch]$GenerateReleaseManifest,
    [string]$ReleaseRoot,
    [string]$ReleaseVersion,
    [ValidateSet('stable', 'preview', 'rc')]
    [string]$ReleaseChannel,
    [string]$ReleaseManifestPath,
    [switch]$DependencyHealth,
    [string]$DependencyHealthReportPath,
    [string]$DependencyHealthAllowlistPath
)

$ErrorActionPreference = 'Stop'

$mainScript = Join-Path $PSScriptRoot 'LibreSpot.ps1'
$backendScript = Join-Path $PSScriptRoot 'src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1'
$releaseContractPath = Join-Path $PSScriptRoot 'schemas/release-artifact-contract.json'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
# The two runnable scripts MUST keep a UTF-8 BOM: Windows PowerShell 5.1 reads
# BOM-less files in the ANSI codepage, and non-ASCII characters (em-dashes,
# the U+2139 info glyph) then corrupt the token stream — a single character in
# a double-quoted string can hard-fail the whole file parse (14 cascading
# errors observed). JSON/report outputs stay BOM-less.
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    $ReleaseRoot = Join-Path $PSScriptRoot 'publish'
}
if ([string]::IsNullOrWhiteSpace($ReleaseManifestPath)) {
    $ReleaseManifestPath = Join-Path $ReleaseRoot 'librespot-release-manifest.json'
}
if ([string]::IsNullOrWhiteSpace($DependencyHealthReportPath)) {
    $DependencyHealthReportPath = Join-Path $ReleaseRoot 'dependency-health.json'
}
if ([string]::IsNullOrWhiteSpace($DependencyHealthAllowlistPath)) {
    $DependencyHealthAllowlistPath = Join-Path $PSScriptRoot 'schemas/dependency-health-allowlist.json'
}

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

function Get-JsonFile {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "JSON file not found: $Path"
    }

    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-LibreSpotProjectVersion {
    $projectPath = Join-Path $PSScriptRoot 'src/LibreSpot.Desktop/LibreSpot.Desktop.csproj'
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Cannot infer release version; project file not found at $projectPath"
    }

    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $version = $project.Project.PropertyGroup |
        ForEach-Object { $_.Version } |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace([string]$version)) {
        throw "Cannot infer release version; <Version> is missing from $projectPath"
    }

    return [string]$version
}

function Get-LibreSpotProjectInformationalVersion {
    $projectPath = Join-Path $PSScriptRoot 'src/LibreSpot.Desktop/LibreSpot.Desktop.csproj'
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Cannot infer desktop informational version; project file not found at $projectPath"
    }

    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $version = $project.Project.PropertyGroup |
        ForEach-Object { $_.InformationalVersion } |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace([string]$version)) {
        throw "Cannot infer desktop informational version; <InformationalVersion> is missing from $projectPath"
    }

    return [string]$version
}

function Get-LibreSpotShellDisplayVersion {
    $viewModelPath = Join-Path $PSScriptRoot 'src/LibreSpot.Desktop/ViewModels/MainViewModel.cs'
    if (-not (Test-Path -LiteralPath $viewModelPath -PathType Leaf)) {
        throw "Cannot infer shell display version; MainViewModel.cs not found at $viewModelPath"
    }

    $content = [System.IO.File]::ReadAllText($viewModelPath, [System.Text.Encoding]::UTF8)
    $match = [regex]::Match($content, 'ShellDisplayVersion\s*=>\s*"(?<version>v[^"]+)"')
    if (-not $match.Success) {
        throw "Cannot infer shell display version; MainViewModel.ShellDisplayVersion must be a literal v-prefixed version."
    }

    return [string]$match.Groups['version'].Value
}

function Get-PngTextMetadataValue {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Key
    )

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $signature = [byte[]](137, 80, 78, 71, 13, 10, 26, 10)
    if ($bytes.Length -lt $signature.Length) { return $null }
    for ($i = 0; $i -lt $signature.Length; $i++) {
        if ($bytes[$i] -ne $signature[$i]) { return $null }
    }

    $offset = $signature.Length
    while ($offset + 12 -le $bytes.Length) {
        $length = (
            ([int]$bytes[$offset] -shl 24) -bor
            ([int]$bytes[($offset + 1)] -shl 16) -bor
            ([int]$bytes[($offset + 2)] -shl 8) -bor
            [int]$bytes[($offset + 3)]
        )
        if ($length -lt 0 -or $offset + 12 + $length -gt $bytes.Length) { return $null }

        $type = [System.Text.Encoding]::ASCII.GetString($bytes, $offset + 4, 4)
        if ($type -eq 'tEXt') {
            $dataOffset = $offset + 8
            $dataEnd = $dataOffset + $length
            $split = -1
            for ($i = $dataOffset; $i -lt $dataEnd; $i++) {
                if ($bytes[$i] -eq 0) {
                    $split = $i
                    break
                }
            }

            if ($split -gt $dataOffset) {
                $chunkKey = [System.Text.Encoding]::ASCII.GetString($bytes, $dataOffset, $split - $dataOffset)
                if ($chunkKey -eq $Key) {
                    return [System.Text.Encoding]::ASCII.GetString($bytes, $split + 1, $dataEnd - $split - 1)
                }
            }
        }

        if ($type -eq 'IEND') { break }
        $offset += 12 + $length
    }

    return $null
}

function Test-ReadmeWpfScreenshotMetadata {
    $readmePath = Join-Path $PSScriptRoot 'README.md'
    if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
        throw "Cannot validate README screenshots; README.md not found."
    }

    $expectedScreenshots = [ordered]@{
        'assets/screenshots/wpf-recommended.png'    = 'recommended'
        'assets/screenshots/wpf-custom.png'         = 'custom'
        'assets/screenshots/wpf-maintenance.png'    = 'maintenance'
        'assets/screenshots/wpf-activity-undo.png'  = 'activity-undo'
    }
    $expectedShellVersion = Get-LibreSpotShellDisplayVersion
    $expectedAssemblyVersion = Get-LibreSpotProjectInformationalVersion
    $readme = [System.IO.File]::ReadAllText($readmePath, [System.Text.Encoding]::UTF8)
    $referenced = @{}
    foreach ($match in [regex]::Matches($readme, 'assets/screenshots/(?<file>wpf-[^"]+\.png)')) {
        $referenced["assets/screenshots/$($match.Groups['file'].Value)"] = $true
    }

    $failures = @()
    foreach ($relativePath in $expectedScreenshots.Keys) {
        $expectedState = [string]$expectedScreenshots[$relativePath]
        if (-not $referenced.ContainsKey($relativePath)) {
            $failures += "${relativePath}: README does not reference this WPF screenshot."
            continue
        }

        $fullPath = Join-Path $PSScriptRoot ($relativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            $failures += "${relativePath}: screenshot file is missing."
            continue
        }

        $shellVersion = Get-PngTextMetadataValue -Path $fullPath -Key 'LibreSpotShellVersion'
        $assemblyVersion = Get-PngTextMetadataValue -Path $fullPath -Key 'LibreSpotCaptureAssemblyVersion'
        $state = Get-PngTextMetadataValue -Path $fullPath -Key 'LibreSpotCaptureState'
        $capturedAt = Get-PngTextMetadataValue -Path $fullPath -Key 'LibreSpotCaptureUtc'

        if ($shellVersion -ne $expectedShellVersion) {
            $failures += "${relativePath}: LibreSpotShellVersion '$shellVersion' does not match '$expectedShellVersion'."
        }
        if ($assemblyVersion -ne $expectedAssemblyVersion) {
            $failures += "${relativePath}: LibreSpotCaptureAssemblyVersion '$assemblyVersion' does not match '$expectedAssemblyVersion'."
        }
        if ($state -ne $expectedState) {
            $failures += "${relativePath}: LibreSpotCaptureState '$state' does not match '$expectedState'."
        }
        if ([string]::IsNullOrWhiteSpace($capturedAt)) {
            $failures += "${relativePath}: LibreSpotCaptureUtc metadata is missing."
        } else {
            $parsedTimestamp = [datetimeoffset]::MinValue
            if (-not [datetimeoffset]::TryParse($capturedAt, [ref]$parsedTimestamp)) {
                $failures += "${relativePath}: LibreSpotCaptureUtc '$capturedAt' is not a valid timestamp."
            }
        }
    }

    if ($failures.Count -gt 0) {
        Write-Host "=== STALE README WPF SCREENSHOTS ===" -ForegroundColor Red
        foreach ($failure in $failures) {
            Write-Host "  $failure" -ForegroundColor Red
        }
        Write-Host ""
        throw "README WPF screenshots must be recaptured with the current shell version."
    }

    Write-Host "README WPF screenshot metadata matches shell version $expectedShellVersion." -ForegroundColor Green
}

function Resolve-LibreSpotReleaseChannel {
    param(
        [Parameter(Mandatory)][string]$Version,
        [string]$ExplicitChannel
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitChannel)) {
        return $ExplicitChannel
    }

    $normalized = $Version.Trim()
    if ($normalized.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(1)
    }

    if ($normalized -match '^\d+\.\d+\.\d+-preview\.\d+$') { return 'preview' }
    if ($normalized -match '^\d+\.\d+\.\d+-rc\.\d+$') { return 'rc' }
    if ($normalized -match '^\d+\.\d+\.\d+$') { return 'stable' }

    throw "Cannot infer release channel from version '$Version'. Pass -ReleaseChannel stable|preview|rc."
}

function Get-ReleaseChecksumMap {
    param([Parameter(Mandatory)][string]$ChecksumsPath)

    if (-not (Test-Path -LiteralPath $ChecksumsPath -PathType Leaf)) {
        throw "checksums.txt not found at $ChecksumsPath"
    }

    $map = @{}
    foreach ($line in [System.IO.File]::ReadLines($ChecksumsPath)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -match 'PLACEHOLDER') {
            throw "checksums.txt contains a placeholder hash: $line"
        }
        if ($line -notmatch '^(?<hash>[A-Fa-f0-9]{64})\s+\*?(?<name>.+)$') {
            throw "checksums.txt contains an invalid sha256sum line: $line"
        }

        $name = Split-Path -Leaf $Matches.name.Trim()
        if ($map.ContainsKey($name)) {
            throw "checksums.txt contains duplicate entry for $name"
        }

        $map[$name] = $Matches.hash.ToLowerInvariant()
    }

    return $map
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

function Get-AuthenticodeState {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Requirement
    )

    if ($Requirement -eq 'none') {
        return 'not-required'
    }

    if (-not (Get-Command Get-AuthenticodeSignature -ErrorAction SilentlyContinue)) {
        return 'unavailable'
    }

    try {
        return (Get-AuthenticodeSignature -FilePath $Path).Status.ToString()
    } catch {
        return "error: $($_.Exception.Message)"
    }
}

function Assert-ReleaseArtifactMetadata {
    param([Parameter(Mandatory)]$Artifact)

    foreach ($field in @('packageRole', 'runtimeIdentifier', 'buildMode')) {
        if (-not $Artifact.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$Artifact.$field)) {
            throw "Release artifact '$($Artifact.name)' is missing metadata field '$field'."
        }
    }
}

function New-ReleaseArtifactManifestEntry {
    param(
        [Parameter(Mandatory)]$Artifact,
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][hashtable]$ChecksumMap,
        [Parameter(Mandatory)]$SigningContract,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$Channel,
        [Parameter(Mandatory)][string]$ManifestFileName
    )

    Assert-ReleaseArtifactMetadata -Artifact $Artifact

    $name = [string]$Artifact.name
    $isSelfReferential = $Artifact.PSObject.Properties['selfReferential'] -and [bool]$Artifact.selfReferential
    $path = Join-Path $Root $name
    $checksumVerified = $null
    $sha256 = $null
    $sizeBytes = $null

    if ($isSelfReferential -and $name -eq $ManifestFileName) {
        # A manifest cannot contain the final hash of itself without changing
        # its own content. The post-write verifier checks that this entry exists.
    } else {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required release artifact is missing: $path"
        }

        $sha256 = Get-FileSha256Lower -Path $path
        $sizeBytes = (Get-Item -LiteralPath $path).Length

        if ([bool]$Artifact.checksumEntry) {
            if (-not $ChecksumMap.ContainsKey($name)) {
                throw "checksums.txt is missing required entry for $name"
            }
            if ($ChecksumMap[$name] -ne $sha256) {
                throw "checksums.txt hash for $name does not match the artifact."
            }
            $checksumVerified = $true
        }
    }

    $distributionChannels = @()
    if ($Artifact.PSObject.Properties['distributionChannels']) {
        $distributionChannels = @($Artifact.distributionChannels)
    }

    $entry = [ordered]@{
        name                 = $name
        description          = [string]$Artifact.description
        packageRole          = [string]$Artifact.packageRole
        version              = $Version
        channel              = $Channel
        buildMode            = [string]$Artifact.buildMode
        runtimeIdentifier    = [string]$Artifact.runtimeIdentifier
        path                 = $name
        sizeBytes            = $sizeBytes
        sha256               = $sha256
        checksumEntry        = [bool]$Artifact.checksumEntry
        checksumVerified     = $checksumVerified
        signing              = [ordered]@{
            requirement   = [string]$Artifact.signingRequirement
            expectedState = if ([string]$Artifact.signingRequirement -eq 'none') { 'not-required' } else { [string]$SigningContract.status }
            actualState   = if ($sha256) { Get-AuthenticodeState -Path $path -Requirement ([string]$Artifact.signingRequirement) } else { 'self-referential' }
        }
        sbomSubject          = if ($Artifact.PSObject.Properties['sbomSubject']) { [string]$Artifact.sbomSubject } else { $null }
        distributionChannels = $distributionChannels
        selfReferential      = [bool]$isSelfReferential
    }

    return [pscustomobject]$entry
}

function Test-LibreSpotReleaseManifest {
    param(
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)]$Contract
    )

    $manifest = Get-JsonFile -Path $ManifestPath
    if ([int]$manifest.schemaVersion -ne 1) {
        throw "Release manifest schemaVersion must be 1."
    }

    $requiredArtifacts = @($Contract.artifacts | Where-Object { [bool]$_.required })
    $requiredNames = @($requiredArtifacts | ForEach-Object { [string]$_.name })
    $actualNames = @($manifest.artifacts | ForEach-Object { [string]$_.name })

    foreach ($name in $requiredNames) {
        if ($actualNames -notcontains $name) {
            throw "Release manifest is missing required artifact '$name'."
        }
    }

    $duplicates = $actualNames | Group-Object | Where-Object { $_.Count -ne 1 }
    if ($duplicates) {
        throw "Release manifest has duplicate artifact entries: $($duplicates.Name -join ', ')"
    }

    foreach ($entry in @($manifest.artifacts)) {
        foreach ($field in @('name', 'version', 'packageRole', 'runtimeIdentifier', 'buildMode', 'path')) {
            if (-not $entry.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$entry.$field)) {
                throw "Manifest artifact '$($entry.name)' is missing '$field'."
            }
        }

        if ([bool]$entry.selfReferential) {
            if ((Split-Path -Leaf $ManifestPath) -ne [string]$entry.name) {
                throw "Only the manifest artifact may be self-referential."
            }
            continue
        }

        $artifactPath = Join-Path $Root ([string]$entry.path)
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "Manifest references a missing artifact: $artifactPath"
        }

        if ([string]::IsNullOrWhiteSpace([string]$entry.sha256) -or [string]$entry.sha256 -match 'PLACEHOLDER') {
            throw "Manifest artifact '$($entry.name)' has an invalid SHA256 value."
        }

        $actualHash = Get-FileSha256Lower -Path $artifactPath
        if ($actualHash -ne [string]$entry.sha256) {
            throw "Manifest SHA256 for '$($entry.name)' does not match the artifact."
        }

        $actualSize = (Get-Item -LiteralPath $artifactPath).Length
        if ([int64]$entry.sizeBytes -ne $actualSize) {
            throw "Manifest size for '$($entry.name)' does not match the artifact."
        }

        if ([bool]$entry.checksumEntry -and -not [bool]$entry.checksumVerified) {
            throw "Manifest artifact '$($entry.name)' was not verified against checksums.txt."
        }
    }
}

function New-LibreSpotReleaseManifest {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$ManifestPath,
        [string]$Version,
        [string]$Channel
    )

    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw "Release root not found: $Root"
    }

    $contract = Get-JsonFile -Path $releaseContractPath
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = Get-LibreSpotProjectVersion
    }
    $Channel = Resolve-LibreSpotReleaseChannel -Version $Version -ExplicitChannel $Channel

    $checksumsPath = Join-Path $Root 'checksums.txt'
    $checksumMap = Get-ReleaseChecksumMap -ChecksumsPath $checksumsPath
    $manifestFileName = Split-Path -Leaf $ManifestPath

    $entries = @()
    foreach ($artifact in @($contract.artifacts | Where-Object { [bool]$_.required })) {
        $entries += New-ReleaseArtifactManifestEntry `
            -Artifact $artifact `
            -Root $Root `
            -ChecksumMap $checksumMap `
            -SigningContract $contract.signingContract `
            -Version $Version `
            -Channel $Channel `
            -ManifestFileName $manifestFileName
    }

    $manifest = [ordered]@{
        schemaVersion    = 1
        contractVersion  = [int]$contract.schemaVersion
        generatedAtUtc   = [DateTime]::UtcNow.ToString('o')
        generator        = 'Build-Scripts.ps1'
        version          = $Version
        channel          = $Channel
        buildMode        = 'local'
        signingProvider  = [string]$contract.signingContract.provider
        signingStatus    = [string]$contract.signingContract.status
        artifactCount    = $entries.Count
        artifacts        = $entries
    }

    $manifestDirectory = Split-Path -Parent $ManifestPath
    if (-not [string]::IsNullOrWhiteSpace($manifestDirectory)) {
        New-Item -Path $manifestDirectory -ItemType Directory -Force | Out-Null
    }

    $json = $manifest | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($ManifestPath, $json + [Environment]::NewLine, $utf8NoBom)

    Test-LibreSpotReleaseManifest -ManifestPath $ManifestPath -Root $Root -Contract $contract
    Write-Host "Release manifest generated and verified: $ManifestPath" -ForegroundColor Green
}

function ConvertTo-RepoRelativePath {
    param([Parameter(Mandatory)][string]$Path)

    $root = [System.IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\', '/')
    $full = [System.IO.Path]::GetFullPath($Path)
    if ($full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        $full = $full.Substring($root.Length).TrimStart('\', '/')
    }

    return $full.Replace('\', '/')
}

function Get-LibreSpotDotNetProjects {
    @(
        'src/LibreSpot.Desktop/LibreSpot.Desktop.csproj'
        'src/LibreSpot.Cli/LibreSpot.Cli.csproj'
        'tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj'
    ) | ForEach-Object { Join-Path $PSScriptRoot $_ }
}

function Invoke-DotNetListPackageJson {
    param(
        [Parameter(Mandatory)][string]$ProjectPath,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $dotnetArgs = @('list', $ProjectPath, 'package') + $Arguments + @('--format', 'json')
    $output = & dotnet @dotnetArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($dotnetArgs -join ' ') failed: $($output -join [Environment]::NewLine)"
    }

    $json = ($output | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw "dotnet $($dotnetArgs -join ' ') returned no JSON."
    }

    return $json | ConvertFrom-Json
}

function ConvertTo-DependencyPackageRows {
    param(
        [Parameter(Mandatory)]$Document,
        [Parameter(Mandatory)][string]$Kind
    )

    $rows = @()
    foreach ($project in @($Document.projects)) {
        $projectPath = ConvertTo-RepoRelativePath -Path ([string]$project.path)
        $projectKind = if ($projectPath.StartsWith('tests/', [System.StringComparison]::OrdinalIgnoreCase)) { 'test' } else { 'runtime' }

        $frameworks = @()
        if ($project.PSObject.Properties['frameworks']) {
            $frameworks = @($project.frameworks)
        }

        foreach ($framework in $frameworks) {
            if ($null -eq $framework) {
                continue
            }

            foreach ($section in @(
                @{ Name = 'topLevelPackages'; DependencyKind = 'direct' },
                @{ Name = 'transitivePackages'; DependencyKind = 'transitive' }
            )) {
                $sectionName = [string]$section.Name
                if (-not $framework.PSObject.Properties[$sectionName]) {
                    continue
                }

                foreach ($package in @($framework.PSObject.Properties[$sectionName].Value)) {
                    $vulnerabilities = @()
                    if ($package.PSObject.Properties['vulnerabilities']) {
                        foreach ($vulnerability in @($package.vulnerabilities)) {
                            $vulnerabilities += [pscustomobject][ordered]@{
                                severity    = [string]$vulnerability.severity
                                advisoryUrl = [string]$vulnerability.advisoryUrl
                            }
                        }
                    }

                    $rows += [pscustomobject][ordered]@{
                        projectPath      = $projectPath
                        projectKind      = $projectKind
                        framework        = [string]$framework.framework
                        dependencyKind   = [string]$section.DependencyKind
                        scope            = "$projectKind-$($section.DependencyKind)"
                        packageId        = [string]$package.id
                        requestedVersion = if ($package.PSObject.Properties['requestedVersion']) { [string]$package.requestedVersion } else { $null }
                        resolvedVersion  = if ($package.PSObject.Properties['resolvedVersion']) { [string]$package.resolvedVersion } else { $null }
                        latestVersion    = if ($package.PSObject.Properties['latestVersion']) { [string]$package.latestVersion } else { $null }
                        reportKind       = $Kind
                        vulnerabilities  = $vulnerabilities
                    }
                }
            }
        }
    }

    return @($rows)
}

function Get-DependencyHealthAllowlist {
    param([Parameter(Mandatory)][string]$Path)

    $doc = Get-JsonFile -Path $Path
    if ([int]$doc.schemaVersion -ne 1) {
        throw "Dependency health allowlist schemaVersion must be 1."
    }

    $entries = @($doc.acceptedTransitiveLag)
    foreach ($entry in $entries) {
        foreach ($field in @('packageId', 'scope', 'projectPath', 'owner', 'reason', 'recheckDate')) {
            if (-not $entry.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$entry.$field)) {
                throw "Dependency health allowlist entry is missing '$field'."
            }
        }

        [void][DateTime]::Parse([string]$entry.recheckDate)
        if ([string]$entry.scope -ne 'test-transitive') {
            throw "Dependency health allowlist only accepts test-transitive lag: $($entry.packageId)."
        }
    }

    return $entries
}

function Test-TransitiveLagAllowed {
    param(
        [Parameter(Mandatory)]$Row,
        [Parameter(Mandatory)]$Allowlist
    )

    foreach ($entry in @($Allowlist)) {
        if ([string]$entry.scope -ne [string]$Row.scope) { continue }
        if (-not [string]::Equals([string]$entry.packageId, [string]$Row.packageId, [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        if (-not [string]::Equals([string]$entry.projectPath, [string]$Row.projectPath, [System.StringComparison]::OrdinalIgnoreCase)) { continue }
        return $true
    }

    return $false
}

function New-LibreSpotDependencyHealthReport {
    param(
        [Parameter(Mandatory)][string]$ReportPath,
        [Parameter(Mandatory)][string]$AllowlistPath
    )

    $allowlist = Get-DependencyHealthAllowlist -Path $AllowlistPath
    $projects = Get-LibreSpotDotNetProjects
    $outdatedPackages = @()
    $vulnerablePackages = @()

    foreach ($project in $projects) {
        if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
            throw "Project file not found: $project"
        }

        $outdatedDocument = Invoke-DotNetListPackageJson -ProjectPath $project -Arguments @('--outdated', '--include-transitive')
        $vulnerableDocument = Invoke-DotNetListPackageJson -ProjectPath $project -Arguments @('--vulnerable', '--include-transitive')
        $outdatedPackages += ConvertTo-DependencyPackageRows -Document $outdatedDocument -Kind 'outdated'
        $vulnerablePackages += ConvertTo-DependencyPackageRows -Document $vulnerableDocument -Kind 'vulnerable'
    }

    $outdatedDirect = @($outdatedPackages | Where-Object { $_.dependencyKind -eq 'direct' })
    $outdatedTransitive = @($outdatedPackages | Where-Object { $_.dependencyKind -eq 'transitive' })
    $allowedTransitive = @($outdatedTransitive | Where-Object { Test-TransitiveLagAllowed -Row $_ -Allowlist $allowlist })
    $unapprovedTransitive = @($outdatedTransitive | Where-Object { -not (Test-TransitiveLagAllowed -Row $_ -Allowlist $allowlist) })
    $today = [DateTime]::UtcNow.Date
    $expiredAllowlist = @($allowlist | Where-Object { [DateTime]::Parse([string]$_.recheckDate).Date -lt $today })
    $auditPipeline = [string]::Equals([string]$env:AuditPipeline, 'true', [System.StringComparison]::OrdinalIgnoreCase)
    $moderatePlus = @('moderate', 'high', 'critical')
    $auditFailures = @()

    if ($auditPipeline) {
        foreach ($package in $vulnerablePackages) {
            foreach ($vulnerability in @($package.vulnerabilities)) {
                if ($moderatePlus -contains ([string]$vulnerability.severity).ToLowerInvariant()) {
                    $auditFailures += [pscustomobject][ordered]@{
                        packageId       = $package.packageId
                        projectPath     = $package.projectPath
                        severity        = $vulnerability.severity
                        advisoryUrl     = $vulnerability.advisoryUrl
                        resolvedVersion = $package.resolvedVersion
                    }
                }
            }
        }
    }

    $failures = @()
    foreach ($package in $outdatedDirect) {
        $failures += "Direct package drift: $($package.packageId) $($package.resolvedVersion) -> $($package.latestVersion) in $($package.projectPath)."
    }
    foreach ($package in $unapprovedTransitive) {
        $failures += "Unapproved transitive package drift: $($package.packageId) $($package.resolvedVersion) -> $($package.latestVersion) in $($package.projectPath)."
    }
    foreach ($entry in $expiredAllowlist) {
        $failures += "Expired dependency-health allowlist entry: $($entry.packageId) recheckDate $($entry.recheckDate)."
    }
    foreach ($failure in $auditFailures) {
        $failures += "AuditPipeline vulnerability: $($failure.packageId) $($failure.severity) $($failure.advisoryUrl)."
    }

    $report = [ordered]@{
        schemaVersion                = 1
        generatedAtUtc               = [DateTime]::UtcNow.ToString('o')
        generator                    = 'Build-Scripts.ps1 -DependencyHealth'
        auditPipeline                = $auditPipeline
        allowlistPath                = ConvertTo-RepoRelativePath -Path $AllowlistPath
        projectCount                 = $projects.Count
        vulnerablePackageCount       = $vulnerablePackages.Count
        outdatedDirectPackageCount   = $outdatedDirect.Count
        outdatedTransitivePackageCount = $outdatedTransitive.Count
        acceptedTransitiveLagCount   = $allowedTransitive.Count
        failureCount                 = $failures.Count
        status                       = if ($failures.Count -eq 0) { 'ok' } else { 'failed' }
        projects                     = @($projects | ForEach-Object { ConvertTo-RepoRelativePath -Path $_ })
        vulnerablePackages           = $vulnerablePackages
        outdatedDirectPackages       = $outdatedDirect
        outdatedTransitivePackages   = $outdatedTransitive
        acceptedTransitiveLag        = $allowedTransitive
        failures                     = $failures
    }

    $reportDirectory = Split-Path -Parent $ReportPath
    if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
        New-Item -Path $reportDirectory -ItemType Directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($ReportPath, (($report | ConvertTo-Json -Depth 12) + [Environment]::NewLine), $utf8NoBom)
    Write-Host "Dependency health report written: $ReportPath" -ForegroundColor Green

    if ($failures.Count -gt 0) {
        foreach ($failure in $failures) {
            Write-Host "  $failure" -ForegroundColor Red
        }
        exit 1
    }
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
    'Hide-SpotifyWindows'            # Main: [Win32] ShowWindowAsync; Backend: stub (watcher runspace owns hiding)
)

if ($DependencyHealth) {
    New-LibreSpotDependencyHealthReport `
        -ReportPath $DependencyHealthReportPath `
        -AllowlistPath $DependencyHealthAllowlistPath
    exit 0
}

if ($GenerateReleaseManifest) {
    New-LibreSpotReleaseManifest `
        -Root $ReleaseRoot `
        -ManifestPath $ReleaseManifestPath `
        -Version $ReleaseVersion `
        -Channel $ReleaseChannel
    exit 0
}

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
    Write-Host ""

    # --- Shared-module source-of-truth check ---
    $sharedDir = Join-Path $PSScriptRoot 'src/powershell/shared'
    if (Test-Path -LiteralPath $sharedDir) {
        $sharedDrift = @()
        $sharedFiles = Get-ChildItem -Path $sharedDir -Filter '*.ps1' -File | Sort-Object Name
        foreach ($file in $sharedFiles) {
            $fnName = $file.BaseName
            if ($laneSpecificFunctions -contains $fnName) { continue }

            $sharedBody = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
            $sharedNorm = ConvertTo-NormalizedFunctionBody -Body $sharedBody

            foreach ($lane in @(@{ Name = 'main'; Content = $mainContent }, @{ Name = 'backend'; Content = $backendContent })) {
                $laneBody = Get-FunctionBody -ScriptContent $lane.Content -FunctionName $fnName
                if (-not $laneBody) { continue }
                $laneNorm = ConvertTo-NormalizedFunctionBody -Body $laneBody
                if ($sharedNorm -ne $laneNorm) {
                    $sharedDrift += "$fnName ($($lane.Name) differs from shared source)"
                }
            }
        }
        if ($sharedDrift.Count -gt 0) {
            Write-Host "=== SHARED SOURCE DRIFT ($($sharedDrift.Count)) ===" -ForegroundColor Red
            foreach ($d in $sharedDrift) { Write-Host "  $d" -ForegroundColor Red }
            Write-Host ""
            Write-Host "These functions in the scripts differ from src/powershell/shared/." -ForegroundColor Red
            Write-Host "Run -SyncSharedToBackend and manually update LibreSpot.ps1, or edit the shared source." -ForegroundColor Red
            Write-Host ""
            exit 1
        }
        Write-Host "All shared module files match their script counterparts." -ForegroundColor Green
        Write-Host ""
    }

    # --- Critical data-block parity check ---
    $dataBlockPatterns = @(
        @{ Name = 'PinnedReleases'; Pattern = '(?ms)\$global:PinnedReleases\s*=\s*@\{.+?^\}' }
    )
    $dataBlockDrift = @()
    foreach ($block in $dataBlockPatterns) {
        $mainMatch = [regex]::Match($mainContent, $block.Pattern)
        $backendMatch = [regex]::Match($backendContent, $block.Pattern)
        if ($mainMatch.Success -and $backendMatch.Success) {
            $mainNorm = ConvertTo-NormalizedFunctionBody -Body $mainMatch.Value
            $backendNorm = ConvertTo-NormalizedFunctionBody -Body $backendMatch.Value
            if ($mainNorm -ne $backendNorm) {
                $dataBlockDrift += $block.Name
            }
        } elseif ($mainMatch.Success -ne $backendMatch.Success) {
            $dataBlockDrift += "$($block.Name) (present in one script but not the other)"
        }
    }
    if ($dataBlockDrift.Count -gt 0) {
        Write-Host "=== CRITICAL DATA BLOCK DRIFT ===" -ForegroundColor Red
        foreach ($d in $dataBlockDrift) { Write-Host "  $d" -ForegroundColor Red }
        Write-Host ""
        Write-Host "PinnedReleases, SHA256 hashes, or version manifests differ between scripts." -ForegroundColor Red
        Write-Host "Users on different lanes will download different (potentially incompatible) versions." -ForegroundColor Red
        Write-Host ""
        exit 1
    }
    Write-Host "Critical data blocks (PinnedReleases) are in sync." -ForegroundColor Green
    Write-Host ""

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tools/Sync-Localization.ps1') -Validate -ScanRawStrings
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Test-ReadmeWpfScreenshotMetadata
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

        # Guard the PS 5.1 launch path: BOM-less UTF-8 + non-ASCII content is
        # read as ANSI by Windows PowerShell and can hard-fail the file parse.
        $firstBytes = [System.IO.File]::ReadAllBytes($script)[0..2]
        $hasBom = ($firstBytes.Count -ge 3 -and $firstBytes[0] -eq 0xEF -and $firstBytes[1] -eq 0xBB -and $firstBytes[2] -eq 0xBF)
        if (-not $hasBom) {
            Write-Host "  [ERROR] $name has no UTF-8 BOM; Windows PowerShell 5.1 would read it as ANSI." -ForegroundColor Red
            $totalIssues++
        }
        $parseTokens = $null
        $parseErrors = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$parseTokens, [ref]$parseErrors)
        if ($parseErrors.Count -gt 0) {
            foreach ($pe in $parseErrors) {
                Write-Host "  [ERROR] Parse: line $($pe.Extent.StartLineNumber): $($pe.Message)" -ForegroundColor Red
            }
            $totalIssues += $parseErrors.Count
        }

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
        [System.IO.File]::WriteAllText($backendScript, $backendContent, $utf8Bom)
    }
    Write-Host "`n$updatedCount synced, $excludedCount excluded (lane-specific), $skippedCount skipped (not in backend)." -ForegroundColor Green
    exit 0
}

if ($SyncSharedToMain) {
    $sharedDir = Join-Path $PSScriptRoot 'src/powershell/shared'
    if (-not (Test-Path -LiteralPath $sharedDir)) {
        throw "Shared source directory not found at $sharedDir"
    }

    $sharedFiles = Get-ChildItem -Path $sharedDir -Filter '*.ps1' -File | Sort-Object Name
    if ($sharedFiles.Count -eq 0) {
        throw "No .ps1 files found in $sharedDir"
    }

    Write-Host "Syncing shared functions to standalone script..." -ForegroundColor Cyan
    Write-Host "  Source:     $sharedDir ($($sharedFiles.Count) files)" -ForegroundColor Gray
    Write-Host "  Target:     $mainScript" -ForegroundColor Gray
    Write-Host "  Exclusions: $($laneSpecificFunctions.Count) lane-specific functions" -ForegroundColor Gray
    Write-Host ""

    $mainContentForSync = [System.IO.File]::ReadAllText($mainScript, [System.Text.Encoding]::UTF8)
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

        $existingBody = Get-FunctionBody -ScriptContent $mainContentForSync -FunctionName $fnName
        if (-not $existingBody) {
            Write-Host "  SKIP $fnName (not found in main script)" -ForegroundColor Yellow
            $skippedCount++
            continue
        }

        $sharedNorm = ConvertTo-NormalizedFunctionBody -Body $sharedBody
        $existingNorm = ConvertTo-NormalizedFunctionBody -Body $existingBody

        if ($sharedNorm -ne $existingNorm) {
            $mainContentForSync = $mainContentForSync.Replace($existingBody, $sharedBody.TrimEnd())
            Write-Host "  UPDATED $fnName" -ForegroundColor Green
            $updatedCount++
        }
    }

    if ($updatedCount -gt 0) {
        [System.IO.File]::WriteAllText($mainScript, $mainContentForSync, $utf8Bom)
    }
    Write-Host "`n$updatedCount synced, $excludedCount excluded (lane-specific), $skippedCount skipped (not in main)." -ForegroundColor Green
    exit 0
}

# Default: show usage
Write-Host "Usage:"
Write-Host "  pwsh -File Build-Scripts.ps1 -Validate             # Check shared functions for drift"
Write-Host "  pwsh -File Build-Scripts.ps1 -Inventory             # List all functions and their locations"
Write-Host "  pwsh -File Build-Scripts.ps1 -Lint                   # Run PSScriptAnalyzer on both scripts"
Write-Host "  pwsh -File Build-Scripts.ps1 -SyncSharedToBackend   # Copy shared function sources into backend"
Write-Host "  pwsh -File Build-Scripts.ps1 -SyncSharedToMain      # Copy shared function sources into standalone script"
Write-Host "  pwsh -File Build-Scripts.ps1 -DependencyHealth       # Emit dependency-health JSON and fail unapproved drift"
