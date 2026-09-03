<#
.SYNOPSIS
    Composes and validates the executable PowerShell hosts from canonical
    shared, data-block, and lane-specific sources.

.DESCRIPTION
    Uses src/powershell/composition.json to own shared functions, critical data
    blocks, and the two lane wrapper sets. Generated hosts are byte-compared
    with the checked-in scripts and import/parse-smoked on Windows PowerShell
    5.1 and PowerShell 7.6 before validation or release-manifest generation.

    Run this as part of CI to catch shared-function drift before release.

.EXAMPLE
    pwsh -File Build-Scripts.ps1 -Validate
    pwsh -File Build-Scripts.ps1 -ComposeHosts
    pwsh -File Build-Scripts.ps1 -CompositionSmoke
    pwsh -File Build-Scripts.ps1 -Inventory
    pwsh -File Build-Scripts.ps1 -Lint
    pwsh -File Build-Scripts.ps1 -CatalogTruth

.NOTES
    Part of the "Extract shared PowerShell core logic" roadmap item (Cycle 11).
    The validation pass runs without modifying any files.
#>
[CmdletBinding()]
param(
    [switch]$Validate,
    [switch]$Inventory,
    [switch]$Lint,
    [switch]$ComposeHosts,
    [switch]$CompositionSmoke,
    [string]$CompositionContractPath,
    [string]$CompositionOutputRoot,
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
    [string]$DependencyHealthAllowlistPath,
    [switch]$SpotXSecurityPolicy,
    [string]$SpotXScriptPath,
    [string]$SpotXCandidateCommit,
    [switch]$SpotXCandidatePostDefenderPolicy,
    [switch]$SpotXCandidateDefenderMutations,
    [AllowEmptyString()][string]$SpotXCandidateDefenderOptOut = '',
    [AllowEmptyString()][string]$SpotXCandidateArguments = '',
    [switch]$CheckSpotifyVersionDrift,
    [switch]$PublishRelease,
    [switch]$CompileStableExe,
    [string]$StableExeOutputPath,
    [switch]$GenerateSbom,
    [string]$SbomOutputPath,
    [switch]$SkipStableExeIdentity,
    [switch]$ReleaseTruth,
    [switch]$CatalogTruth,
    [switch]$WatcherIntegration
)

$ErrorActionPreference = 'Stop'

$mainScript = Join-Path $PSScriptRoot 'LibreSpot.ps1'
$backendScript = Join-Path $PSScriptRoot 'src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1'
if ([string]::IsNullOrWhiteSpace($CompositionContractPath)) {
    $CompositionContractPath = Join-Path $PSScriptRoot 'src/powershell/composition.json'
}
$releaseContractPath = Join-Path $PSScriptRoot 'schemas/release-artifact-contract.json'
$pinAdvancePolicyPath = Join-Path $PSScriptRoot 'src/powershell/shared/Test-SpotXPinAdvanceSecurityPolicy.ps1'
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
if (-not (Test-Path -LiteralPath $pinAdvancePolicyPath -PathType Leaf)) {
    throw "Cannot find SpotX pin-advance policy at $pinAdvancePolicyPath"
}
. $pinAdvancePolicyPath

function Get-ScriptFunctionDefinitions {
    param([Parameter(Mandatory)][string]$ScriptContent)

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput(
        $ScriptContent,
        [ref]$tokens,
        [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) {
        $details = @($parseErrors | ForEach-Object {
            "line $($_.Extent.StartLineNumber): $($_.Message)"
        }) -join '; '
        throw "PowerShell parse failed: $details"
    }

    return @($ast.FindAll(
        {
            param($node)
            $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Extent.StartColumnNumber -eq 1
        },
        $true) | Sort-Object { $_.Extent.StartOffset } | ForEach-Object {
        [pscustomobject]@{
            Name        = $_.Name
            Body        = $_.Extent.Text
            StartOffset = $_.Extent.StartOffset
            EndOffset   = $_.Extent.EndOffset
        }
    })
}

function Get-FunctionNames {
    param([string]$ScriptPath)
    $content = [System.IO.File]::ReadAllText($ScriptPath, [System.Text.Encoding]::UTF8)
    return @(Get-ScriptFunctionDefinitions -ScriptContent $content |
        ForEach-Object { $_.Name } |
        Sort-Object -Unique)
}

function Get-FunctionBody {
    param(
        [string]$ScriptContent,
        [string]$FunctionName
    )
    $definition = @(Get-ScriptFunctionDefinitions -ScriptContent $ScriptContent |
        Where-Object { $_.Name -ceq $FunctionName })
    if ($definition.Count -eq 1) { return $definition[0].Body }
    if ($definition.Count -gt 1) { throw "Duplicate function export '$FunctionName'." }
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

function Resolve-LibreSpotCompositionPath {
    param([Parameter(Mandatory)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }
    return Join-Path $PSScriptRoot $Path
}

function Get-LibreSpotCompositionCatalog {
    if (-not (Test-Path -LiteralPath $CompositionContractPath -PathType Leaf)) {
        throw "PowerShell composition contract not found: $CompositionContractPath"
    }

    $contract = Get-Content -Raw -LiteralPath $CompositionContractPath | ConvertFrom-Json
    if ([int]$contract.schemaVersion -ne 1) {
        throw "Unsupported PowerShell composition schema version: $($contract.schemaVersion)"
    }

    $expectedComponentOrder = @('dataBlocks', 'sharedFunctions', 'laneFunctions')
    $componentOrder = @($contract.componentOrder | ForEach-Object { [string]$_ })
    if (($componentOrder -join '|') -cne ($expectedComponentOrder -join '|')) {
        throw "Invalid composition order. Expected: $($expectedComponentOrder -join ', ')."
    }

    $sharedDirectory = Resolve-LibreSpotCompositionPath -Path ([string]$contract.sharedFunctions.directory)
    $sharedFiles = @(Get-ChildItem -LiteralPath $sharedDirectory -Filter ([string]$contract.sharedFunctions.pattern) -File | Sort-Object Name)
    if ($sharedFiles.Count -ne [int]$contract.sharedFunctions.expectedCount) {
        throw "Composition expected $($contract.sharedFunctions.expectedCount) shared modules but found $($sharedFiles.Count). Update the contract with the source change."
    }

    $sharedDefinitions = @{}
    foreach ($file in $sharedFiles) {
        $source = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        $definitions = @(Get-ScriptFunctionDefinitions -ScriptContent $source)
        if ($definitions.Count -ne 1 -or $definitions[0].Name -cne $file.BaseName) {
            throw "Shared module $($file.FullName) must export exactly one top-level function named $($file.BaseName)."
        }
        if ($sharedDefinitions.ContainsKey($definitions[0].Name)) {
            throw "Duplicate shared function export '$($definitions[0].Name)'."
        }
        $sharedDefinitions[$definitions[0].Name] = $definitions[0].Body
    }

    $laneFunctionNames = @($contract.laneFunctions | ForEach-Object { [string]$_ })
    $laneDuplicates = @($laneFunctionNames | Group-Object | Where-Object { $_.Count -gt 1 })
    if ($laneDuplicates.Count -gt 0) {
        throw "Duplicate lane function export(s): $($laneDuplicates.Name -join ', ')"
    }
    foreach ($laneName in $laneFunctionNames) {
        if ($sharedDefinitions.ContainsKey($laneName)) {
            throw "Function '$laneName' is exported by both shared and lane sources."
        }
    }

    $dataBlocks = @()
    foreach ($block in @($contract.dataBlocks)) {
        $sourcePath = Resolve-LibreSpotCompositionPath -Path ([string]$block.source)
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Composition data source not found: $sourcePath"
        }
        $sourceContent = [System.IO.File]::ReadAllText($sourcePath, [System.Text.Encoding]::UTF8)
        $matches = [regex]::Matches($sourceContent, [string]$block.pattern)
        if ($matches.Count -ne 1 -or $sourceContent.Trim() -cne $matches[0].Value.Trim()) {
            throw "Data source $sourcePath must contain only one $($block.name) block."
        }
        $dataBlocks += [pscustomobject]@{
            Name          = [string]$block.name
            Pattern       = [string]$block.pattern
            SourcePath    = $sourcePath
            SourceContent = $matches[0].Value
        }
    }

    $hosts = @()
    foreach ($hostContract in @($contract.hosts)) {
        $targetPath = Resolve-LibreSpotCompositionPath -Path ([string]$hostContract.target)
        $laneSourcePath = Resolve-LibreSpotCompositionPath -Path ([string]$hostContract.laneSource)
        if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) {
            throw "Composition target not found: $targetPath"
        }
        if (-not (Test-Path -LiteralPath $laneSourcePath -PathType Leaf)) {
            throw "Lane source not found: $laneSourcePath"
        }

        $laneSourceContent = [System.IO.File]::ReadAllText($laneSourcePath, [System.Text.Encoding]::UTF8)
        $laneDefinitions = @(Get-ScriptFunctionDefinitions -ScriptContent $laneSourceContent)
        $laneNames = @($laneDefinitions | ForEach-Object { $_.Name })
        $missingLaneNames = @($laneFunctionNames | Where-Object { $_ -cnotin $laneNames })
        $unexpectedLaneNames = @($laneNames | Where-Object { $_ -cnotin $laneFunctionNames })
        $duplicateLaneNames = @($laneNames | Group-Object | Where-Object { $_.Count -gt 1 })
        if ($missingLaneNames.Count -gt 0 -or $unexpectedLaneNames.Count -gt 0 -or $duplicateLaneNames.Count -gt 0) {
            throw "Lane source $laneSourcePath has invalid exports. Missing: $($missingLaneNames -join ', '); unexpected: $($unexpectedLaneNames -join ', '); duplicates: $($duplicateLaneNames.Name -join ', ')."
        }

        $laneDefinitionMap = @{}
        foreach ($definition in $laneDefinitions) {
            $laneDefinitionMap[$definition.Name] = $definition.Body
        }
        $hosts += [pscustomobject]@{
            Id                      = [string]$hostContract.id
            TargetRelativePath      = [string]$hostContract.target
            TargetPath              = $targetPath
            LaneSourcePath          = $laneSourcePath
            LaneSourceContent       = $laneSourceContent
            LaneDefinitions         = $laneDefinitions
            LaneDefinitionMap       = $laneDefinitionMap
            ExcludedSharedFunctions = @($hostContract.excludedSharedFunctions | ForEach-Object { [string]$_ })
        }
    }

    $hostIds = @($hosts | ForEach-Object { $_.Id })
    if (((@($hostIds | Sort-Object)) -join '|') -cne 'backend|main' -or $hostIds.Count -ne 2) {
        throw "Composition contract must declare exactly the main and backend hosts."
    }

    foreach ($hostContract in $hosts) {
        foreach ($excluded in $hostContract.ExcludedSharedFunctions) {
            if (-not $sharedDefinitions.ContainsKey($excluded)) {
                throw "Host '$($hostContract.Id)' excludes unknown shared function '$excluded'."
            }
        }
    }

    # If a top-level function exists in both executable hosts it must be owned
    # by either the shared source set or the explicit lane wrapper set.
    $hostFunctionSets = @{}
    foreach ($hostContract in $hosts) {
        $content = [System.IO.File]::ReadAllText($hostContract.TargetPath, [System.Text.Encoding]::UTF8)
        $definitions = @(Get-ScriptFunctionDefinitions -ScriptContent $content)
        $duplicates = @($definitions | Group-Object Name | Where-Object { $_.Count -gt 1 })
        if ($duplicates.Count -gt 0) {
            throw "Host '$($hostContract.Id)' has duplicate top-level function(s): $($duplicates.Name -join ', ')."
        }
        $hostFunctionSets[$hostContract.Id] = @($definitions | ForEach-Object { $_.Name })
    }
    $unownedCommon = @($hostFunctionSets['main'] | Where-Object {
        $_ -cin $hostFunctionSets['backend'] -and
        -not $sharedDefinitions.ContainsKey($_) -and
        $_ -cnotin $laneFunctionNames
    })
    if ($unownedCommon.Count -gt 0) {
        throw "Functions shared by both hosts lack composition sources: $($unownedCommon -join ', ')."
    }

    return [pscustomobject]@{
        Contract          = $contract
        SharedFiles       = $sharedFiles
        SharedDefinitions = $sharedDefinitions
        LaneFunctionNames = $laneFunctionNames
        DataBlocks        = $dataBlocks
        Hosts             = $hosts
    }
}

function ConvertTo-CompositionLineEndings {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Existing
    )

    $newline = if ($Existing.Contains("`r`n")) { "`r`n" } else { "`n" }
    return (($Source -replace "`r`n", "`n" -replace "`r", "`n") -split "`n" -join $newline).TrimEnd()
}

function Get-LibreSpotComposedHostContent {
    param(
        [Parameter(Mandatory)]$Catalog,
        [Parameter(Mandatory)]$HostContract
    )

    $content = [System.IO.File]::ReadAllText($HostContract.TargetPath, [System.Text.Encoding]::UTF8)
    $targetDefinitions = @(Get-ScriptFunctionDefinitions -ScriptContent $content)
    $targetNames = @($targetDefinitions | ForEach-Object { $_.Name })
    $targetDefinitionMap = @{}
    foreach ($definition in $targetDefinitions) {
        $targetDefinitionMap[$definition.Name] = $definition
    }

    foreach ($excluded in $HostContract.ExcludedSharedFunctions) {
        if ($excluded -cin $targetNames) {
            throw "Host '$($HostContract.Id)' excludes '$excluded' but still exports it."
        }
    }

    $applicableShared = @($Catalog.SharedDefinitions.Keys |
        Where-Object { $_ -cnotin $HostContract.ExcludedSharedFunctions } |
        Sort-Object)
    foreach ($functionName in $applicableShared) {
        if ($functionName -cnotin $targetNames) {
            throw "Host '$($HostContract.Id)' is missing shared function '$functionName'."
        }
    }

    $targetLaneOrder = @($targetDefinitions |
        Where-Object { $_.Name -cin $Catalog.LaneFunctionNames } |
        ForEach-Object { $_.Name })
    $sourceLaneOrder = @($HostContract.LaneDefinitions | ForEach-Object { $_.Name })
    if (($targetLaneOrder -join '|') -cne ($sourceLaneOrder -join '|')) {
        throw "Lane function order for '$($HostContract.Id)' differs from $($HostContract.LaneSourcePath)."
    }

    $replacements = @()
    foreach ($block in $Catalog.DataBlocks) {
        $matches = [regex]::Matches($content, $block.Pattern)
        if ($matches.Count -ne 1) {
            throw "Host '$($HostContract.Id)' must contain exactly one $($block.Name) block; found $($matches.Count)."
        }
        if ((ConvertTo-NormalizedFunctionBody -Body $matches[0].Value) -cne
            (ConvertTo-NormalizedFunctionBody -Body $block.SourceContent)) {
            $replacement = ConvertTo-CompositionLineEndings -Source $block.SourceContent -Existing $matches[0].Value
            $replacements += [pscustomobject]@{
                Start = $matches[0].Index
                End   = $matches[0].Index + $matches[0].Length
                Text  = $replacement
            }
        }
    }

    foreach ($functionName in $applicableShared) {
        $existing = $targetDefinitionMap[$functionName]
        $sourceBody = $Catalog.SharedDefinitions[$functionName]
        if ((ConvertTo-NormalizedFunctionBody -Body $existing.Body) -cne
            (ConvertTo-NormalizedFunctionBody -Body $sourceBody)) {
            $replacements += [pscustomobject]@{
                Start = $existing.StartOffset
                End   = $existing.EndOffset
                Text  = ConvertTo-CompositionLineEndings -Source $sourceBody -Existing $existing.Body
            }
        }
    }
    foreach ($functionName in $sourceLaneOrder) {
        $existing = $targetDefinitionMap[$functionName]
        $sourceBody = $HostContract.LaneDefinitionMap[$functionName]
        if ((ConvertTo-NormalizedFunctionBody -Body $existing.Body) -cne
            (ConvertTo-NormalizedFunctionBody -Body $sourceBody)) {
            $replacements += [pscustomobject]@{
                Start = $existing.StartOffset
                End   = $existing.EndOffset
                Text  = ConvertTo-CompositionLineEndings -Source $sourceBody -Existing $existing.Body
            }
        }
    }

    foreach ($replacement in @($replacements | Sort-Object Start -Descending)) {
        $content = $content.Substring(0, $replacement.Start) +
            $replacement.Text +
            $content.Substring($replacement.End)
    }

    $null = Get-ScriptFunctionDefinitions -ScriptContent $content
    return $content
}

function Test-LibreSpotHostComposition {
    param([switch]$Smoke)

    $catalog = Get-LibreSpotCompositionCatalog
    $staleHosts = @()
    foreach ($hostContract in $catalog.Hosts) {
        $composed = Get-LibreSpotComposedHostContent -Catalog $catalog -HostContract $hostContract
        $actualBytes = [System.IO.File]::ReadAllBytes($hostContract.TargetPath)
        [byte[]]$expectedBytes = @($utf8Bom.GetPreamble()) + @($utf8Bom.GetBytes($composed))
        if ([System.Convert]::ToBase64String($actualBytes) -cne
            [System.Convert]::ToBase64String($expectedBytes)) {
            $staleHosts += $hostContract.TargetRelativePath
        }
    }
    if ($staleHosts.Count -gt 0) {
        throw "Executable PowerShell host(s) are stale: $($staleHosts -join ', '). Run Build-Scripts.ps1 -ComposeHosts."
    }

    Write-Host "PowerShell composition byte-check passed for main and backend hosts." -ForegroundColor Green
    if ($Smoke) {
        Invoke-LibreSpotCompositionSmoke -Catalog $catalog
    }
    return $catalog
}

function Write-LibreSpotComposedHosts {
    param([string]$OutputRoot)

    $catalog = Get-LibreSpotCompositionCatalog
    foreach ($hostContract in $catalog.Hosts) {
        $composed = Get-LibreSpotComposedHostContent -Catalog $catalog -HostContract $hostContract
        $destination = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
            $hostContract.TargetPath
        } else {
            Join-Path ([System.IO.Path]::GetFullPath($OutputRoot)) $hostContract.TargetRelativePath
        }
        $directory = Split-Path -Path $destination -Parent
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            New-Item -Path $directory -ItemType Directory -Force | Out-Null
        }
        [System.IO.File]::WriteAllText($destination, $composed, $utf8Bom)
        Write-Host "Composed $($hostContract.Id) host: $destination" -ForegroundColor Green
    }
}

function Invoke-LibreSpotCompositionSmoke {
    param([Parameter(Mandatory)]$Catalog)

    $smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LibreSpot.Composition.{0}" -f [Guid]::NewGuid().ToString('N'))
    New-Item -Path $smokeRoot -ItemType Directory -Force | Out-Null
    try {
        $driverPath = Join-Path $smokeRoot 'smoke.ps1'
        $driver = @'
param(
    [Parameter(Mandatory)][string]$HostPath,
    [Parameter(Mandatory)][string]$ModulePath,
    [Parameter(Mandatory)][string]$ExpectedFunctionsPath,
    [Parameter(Mandatory)][string]$MinimumVersion
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion -lt [Version]$MinimumVersion) {
    throw "PowerShell $MinimumVersion or newer is required; found $($PSVersionTable.PSVersion)."
}
$tokens = $null
$errors = $null
$null = [System.Management.Automation.Language.Parser]::ParseFile($HostPath, [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) {
    throw "Host parse failed: $($errors.Message -join '; ')"
}
. $ModulePath
$expected = @(Get-Content -LiteralPath $ExpectedFunctionsPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$missing = @($expected | Where-Object { -not (Get-Command -Name $_ -CommandType Function -ErrorAction SilentlyContinue) })
if ($missing.Count -gt 0) {
    throw "Module import missed function(s): $($missing -join ', ')"
}
'ok'
'@
        [System.IO.File]::WriteAllText($driverPath, $driver, $utf8Bom)

        foreach ($hostContract in $Catalog.Hosts) {
            $hostPath = Join-Path $smokeRoot ($hostContract.Id + '.ps1')
            [System.IO.File]::WriteAllText(
                $hostPath,
                (Get-LibreSpotComposedHostContent -Catalog $Catalog -HostContract $hostContract),
                $utf8Bom)

            $moduleParts = @($Catalog.DataBlocks | ForEach-Object { $_.SourceContent.TrimEnd() })
            $moduleParts += @($Catalog.SharedFiles | ForEach-Object {
                [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8).TrimEnd()
            })
            $moduleParts += $hostContract.LaneSourceContent.TrimEnd()
            $modulePath = Join-Path $smokeRoot ($hostContract.Id + '.module.ps1')
            [System.IO.File]::WriteAllText($modulePath, (($moduleParts -join "`n`n") + "`n"), $utf8Bom)

            $expectedPath = Join-Path $smokeRoot ($hostContract.Id + '.functions.txt')
            $expectedFunctions = @($Catalog.SharedDefinitions.Keys | Sort-Object) +
                @($Catalog.LaneFunctionNames | Sort-Object)
            [System.IO.File]::WriteAllLines($expectedPath, $expectedFunctions, $utf8NoBom)

            foreach ($engineContract in @($Catalog.Contract.smokeEngines)) {
                $engine = Get-Command ([string]$engineContract.command) -ErrorAction SilentlyContinue | Select-Object -First 1
                if (-not $engine) {
                    throw "Required composition smoke engine is unavailable: $($engineContract.command)"
                }
                $output = & $engine.Source -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass `
                    -File $driverPath `
                    -HostPath $hostPath `
                    -ModulePath $modulePath `
                    -ExpectedFunctionsPath $expectedPath `
                    -MinimumVersion ([string]$engineContract.minimumVersion) 2>&1
                if ($LASTEXITCODE -ne 0 -or $output -notcontains 'ok') {
                    throw "$($engineContract.command) composition smoke failed for $($hostContract.Id): $($output -join [Environment]::NewLine)"
                }
                Write-Host "  $($engineContract.command) import/parse smoke passed for $($hostContract.Id)." -ForegroundColor Green
            }
        }
    } finally {
        if (Test-Path -LiteralPath $smokeRoot) {
            Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
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

function Get-LibreSpotCliProjectVersion {
    $projectPath = Join-Path $PSScriptRoot 'src/LibreSpot.Cli/LibreSpot.Cli.csproj'
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Cannot infer CLI version; project file not found at $projectPath"
    }

    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $version = $project.Project.PropertyGroup |
        ForEach-Object { $_.Version } |
        Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace([string]$version)) {
        throw "Cannot infer CLI version; <Version> is missing from $projectPath"
    }

    return [string]$version
}

function Get-LibreSpotScriptVersion {
    param([Parameter(Mandatory)][string]$Path)

    $content = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $match = [regex]::Match($content, "(?m)^\`$global:VERSION\s*=\s*'(?<version>[^']+)'\s*$")
    if (-not $match.Success) {
        throw "Cannot infer script version; `$global:VERSION is missing from $Path"
    }

    return [string]$match.Groups['version'].Value
}

function Get-LibreSpotShellDisplayVersion {
    $version = Get-LibreSpotProjectInformationalVersion
    if ($version.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $version
    }

    return "v$version"
}

function Test-PinnedCompatibilityBaseline {
    $baselinePath = Join-Path $PSScriptRoot 'schemas/compatibility-baseline.json'
    $pinnedReleasesPath = Join-Path $PSScriptRoot 'src/powershell/data/PinnedReleases.ps1'
    $catalogPath = Join-Path $PSScriptRoot 'src/LibreSpot.Core/AppCatalog.cs'

    foreach ($path in @($baselinePath, $pinnedReleasesPath, $catalogPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Pinned compatibility baseline input is missing: $path"
        }
    }

    try {
        $baseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json
    } catch {
        throw "Pinned compatibility baseline is not valid JSON: $($_.Exception.Message)"
    }

    $failures = @()
    if ([int]$baseline.schemaVersion -ne 1) {
        $failures += "Unsupported compatibility baseline schema version '$($baseline.schemaVersion)'."
    }

    try {
        $verifiedAt = [DateTimeOffset]::Parse([string]$baseline.lastVerifiedAtUtc)
        if ($verifiedAt -gt [DateTimeOffset]::UtcNow) {
            $failures += "Compatibility baseline verification date '$($baseline.lastVerifiedAtUtc)' is in the future."
        }
    } catch {
        $failures += "Compatibility baseline lastVerifiedAtUtc is not an ISO-8601 timestamp."
    }

    $pinnedSource = [System.IO.File]::ReadAllText($pinnedReleasesPath, [System.Text.Encoding]::UTF8)
    $catalogSource = [System.IO.File]::ReadAllText($catalogPath, [System.Text.Encoding]::UTF8)
    $v3Baseline = $baseline.spicetifyV3Support
    if ([int]$v3Baseline.schemaVersion -ne 2 -or
        [string]$v3Baseline.policy -cne 'allowlist' -or
        [string]$v3Baseline.defaultMapStatus -cne 'classic' -or
        [int]$v3Baseline.featureDetectionMajor -ne 3) {
        $failures += 'Spicetify v3 compatibility baseline must declare schema 2, allowlist policy, classic default maps, and major 3 feature detection.'
    }
    $v3FixturePath = Join-Path $PSScriptRoot ([string]$v3Baseline.fixture)
    if (-not (Test-Path -LiteralPath $v3FixturePath -PathType Leaf)) {
        $failures += "Spicetify v3 support fixture is missing: $v3FixturePath"
    } else {
        try {
            $v3Fixture = Get-Content -Raw -LiteralPath $v3FixturePath | ConvertFrom-Json
            if ([int]$v3Fixture.schema_version -ne 2 -or [string]$v3Fixture.policy -cne 'allowlist') {
                $failures += 'Spicetify v3 support fixture must use schema_version 2 and allowlist policy.'
            }
        } catch {
            $failures += "Spicetify v3 support fixture is not valid JSON: $($_.Exception.Message)"
        }
    }
    $sourceChecks = @(
        @{ Label = 'SpotX version'; Source = $pinnedSource; Pattern = '(?ms)SpotX\s*=\s*@\{.*?^\s*Version\s*=\s*''([^'']+)'; Expected = [string]$baseline.spotx.version },
        @{ Label = 'SpotX commit'; Source = $pinnedSource; Pattern = '(?ms)SpotX\s*=\s*@\{.*?^\s*Commit\s*=\s*''([^'']+)'; Expected = [string]$baseline.spotx.commit },
        @{ Label = 'SpotX SHA256'; Source = $pinnedSource; Pattern = '(?ms)SpotX\s*=\s*@\{.*?^\s*SHA256\s*=\s*''([^'']+)'; Expected = [string]$baseline.spotx.sha256 },
        @{ Label = 'SpotX Defender mutation policy'; Source = $pinnedSource; Pattern = '(?ms)SpotX\s*=\s*@\{.*?^\s*DefenderMutations\s*=\s*\$(true|false)'; Expected = ([bool]$baseline.spotx.defenderMutations).ToString().ToLowerInvariant() },
        @{ Label = 'SpotX Defender opt-out'; Source = $pinnedSource; Pattern = '(?ms)SpotX\s*=\s*@\{.*?^\s*DefenderOptOut\s*=\s*''([^'']*)'; Expected = [string]$baseline.spotx.defenderOptOut },
        @{ Label = 'SpotX Defender policy commit'; Source = $pinnedSource; Pattern = '(?ms)SpotX\s*=\s*@\{.*?^\s*DefenderPolicyCommit\s*=\s*''([^'']+)'; Expected = [string]$baseline.spotx.defenderPolicyCommit },
        @{ Label = 'SpotX Defender policy opt-out'; Source = $pinnedSource; Pattern = '(?ms)SpotX\s*=\s*@\{.*?^\s*DefenderPolicyOptOut\s*=\s*''([^'']*)'; Expected = [string]$baseline.spotx.defenderPolicyOptOut },
        @{ Label = 'SpotX Defender policy active'; Source = $pinnedSource; Pattern = '(?ms)SpotX\s*=\s*@\{.*?^\s*DefenderPolicyActive\s*=\s*\$(true|false)'; Expected = ([bool]$baseline.spotx.defenderPolicyActive).ToString().ToLowerInvariant() },
        @{ Label = 'Spicetify CLI version'; Source = $pinnedSource; Pattern = '(?ms)SpicetifyCLI\s*=\s*@\{.*?^\s*Version\s*=\s*''([^'']+)'; Expected = [string]$baseline.spicetifyCli.version },
        @{ Label = 'Spicetify Windows minimum'; Source = $pinnedSource; Pattern = '(?ms)SpicetifyCLI\s*=\s*@\{.*?^\s*WindowsMinSpotify\s*=\s*''([^'']+)'; Expected = [string]$baseline.spicetifyCli.windowsMinSpotify },
        @{ Label = 'Spicetify Windows declared maximum'; Source = $pinnedSource; Pattern = '(?ms)SpicetifyCLI\s*=\s*@\{.*?^\s*WindowsDeclaredMaxSpotify\s*=\s*''([^'']+)'; Expected = [string]$baseline.spicetifyCli.windowsDeclaredMaxSpotify },
        @{ Label = 'LibreSpot verified maximum'; Source = $pinnedSource; Pattern = '(?ms)SpicetifyCLI\s*=\s*@\{.*?^\s*LibreSpotVerifiedMaxSpotify\s*=\s*''([^'']+)'; Expected = [string]$baseline.spicetifyCli.libreSpotVerifiedMaxSpotify },
        @{ Label = 'Spicetify x64 SHA256'; Source = $pinnedSource; Pattern = '(?ms)SpicetifyCLI\s*=\s*@\{.*?x64\s*=\s*''([^'']+)'; Expected = [string]$baseline.spicetifyCli.sha256.x64 },
        @{ Label = 'Spicetify arm64 SHA256'; Source = $pinnedSource; Pattern = '(?ms)SpicetifyCLI\s*=\s*@\{.*?arm64\s*=\s*''([^'']+)'; Expected = [string]$baseline.spicetifyCli.sha256.arm64 },
        @{ Label = 'Marketplace version'; Source = $pinnedSource; Pattern = '(?ms)Marketplace\s*=\s*@\{.*?^\s*Version\s*=\s*''([^'']+)'; Expected = [string]$baseline.marketplace.version },
        @{ Label = 'Marketplace SHA256'; Source = $pinnedSource; Pattern = '(?ms)Marketplace\s*=\s*@\{.*?^\s*SHA256\s*=\s*''([^'']+)'; Expected = [string]$baseline.marketplace.sha256 },
        @{ Label = 'Themes commit'; Source = $pinnedSource; Pattern = '(?ms)Themes\s*=\s*@\{.*?^\s*Commit\s*=\s*''([^'']+)'; Expected = [string]$baseline.themes.commit },
        @{ Label = 'Themes SHA256'; Source = $pinnedSource; Pattern = '(?ms)Themes\s*=\s*@\{.*?^\s*SHA256\s*=\s*''([^'']+)'; Expected = [string]$baseline.themes.sha256 },
        @{ Label = 'AppCatalog SpotX version'; Source = $catalogSource; Pattern = 'public const string PinnedSpotXVersion\s*=\s*"([^"]+)"'; Expected = [string]$baseline.spotx.version },
        @{ Label = 'AppCatalog SpotX commit'; Source = $catalogSource; Pattern = 'public const string PinnedSpotXCommit\s*=\s*"([^"]+)"'; Expected = [string]$baseline.spotx.commit },
        @{ Label = 'AppCatalog Spotify target ID'; Source = $catalogSource; Pattern = 'public const string PinnedSpotXSpotifyVersionId\s*=\s*"([^"]+)"'; Expected = [string]$baseline.spotify.version },
        @{ Label = 'AppCatalog Spotify target'; Source = $catalogSource; Pattern = 'public const string PinnedSpotXSpotifyVersion\s*=\s*"([^"]+)"'; Expected = [string]$baseline.spotify.version },
        @{ Label = 'AppCatalog Spicetify CLI version'; Source = $catalogSource; Pattern = 'public const string PinnedSpicetifyCliVersion\s*=\s*"([^"]+)"'; Expected = [string]$baseline.spicetifyCli.version },
        @{ Label = 'AppCatalog Spicetify Windows minimum'; Source = $catalogSource; Pattern = 'public const string SpicetifyWindowsDeclaredMinSpotify\s*=\s*"([^"]+)"'; Expected = [string]$baseline.spicetifyCli.windowsMinSpotify },
        @{ Label = 'AppCatalog Spicetify Windows declared maximum'; Source = $catalogSource; Pattern = 'public const string SpicetifyWindowsDeclaredMaxSpotify\s*=\s*"([^"]+)"'; Expected = [string]$baseline.spicetifyCli.windowsDeclaredMaxSpotify },
        @{ Label = 'AppCatalog LibreSpot verified maximum'; Source = $catalogSource; Pattern = 'public const string LibreSpotVerifiedMaxSpotify\s*=\s*"([^"]+)"'; Expected = [string]$baseline.spicetifyCli.libreSpotVerifiedMaxSpotify },
        @{ Label = 'AppCatalog Marketplace version'; Source = $catalogSource; Pattern = 'public const string PinnedMarketplaceVersion\s*=\s*"([^"]+)"'; Expected = [string]$baseline.marketplace.version },
        @{ Label = 'AppCatalog Themes commit'; Source = $catalogSource; Pattern = 'public const string PinnedThemesCommit\s*=\s*"([^"]+)"'; Expected = [string]$baseline.themes.commit }
    )

    foreach ($check in $sourceChecks) {
        $match = [regex]::Match($check.Source, $check.Pattern)
        if (-not $match.Success) {
            $failures += "$($check.Label) is missing from its source."
            continue
        }

        $actual = $match.Groups[1].Value
        if ($actual -ne [string]$check.Expected) {
            $failures += "$($check.Label) '$actual' does not match baseline '$($check.Expected)'."
        }
    }

    try {
        $spotifyVersion = [Version]::Parse([string]$baseline.spotify.version)
        $minimumVersion = [Version]::Parse([string]$baseline.spicetifyCli.windowsMinSpotify)
        $declaredMaximum = [Version]::Parse([string]$baseline.spicetifyCli.windowsDeclaredMaxSpotify)
        $verifiedMaximum = [Version]::Parse([string]$baseline.spicetifyCli.libreSpotVerifiedMaxSpotify)
        if ($spotifyVersion -lt $minimumVersion -or $spotifyVersion -gt $declaredMaximum) {
            $failures += "Pinned Spotify '$spotifyVersion' is outside the range Spicetify declares '$minimumVersion'-'$declaredMaximum'."
        }
        if ($verifiedMaximum -lt $minimumVersion -or $verifiedMaximum -gt $declaredMaximum) {
            $failures += "LibreSpot verified Spotify '$verifiedMaximum' is outside the range Spicetify declares '$minimumVersion'-'$declaredMaximum'."
        }
        if ($spotifyVersion -gt $verifiedMaximum) {
            $failures += "Pinned Spotify '$spotifyVersion' is newer than the build LibreSpot has verified '$verifiedMaximum'."
        }
    } catch {
        $failures += 'Compatibility baseline contains an invalid Spotify or Spicetify version range.'
    }

    if ($failures.Count -gt 0) {
        Write-Host '=== PINNED COMPATIBILITY BASELINE DRIFT ===' -ForegroundColor Red
        foreach ($failure in $failures) { Write-Host "  $failure" -ForegroundColor Red }
        throw 'Pinned SpotX, Spotify, Spicetify, v3 support, Marketplace, and theme metadata must match schemas/compatibility-baseline.json.'
    }

    Write-Host "Pinned compatibility baseline matches the fixture verified $($baseline.lastVerifiedAtUtc)." -ForegroundColor Green
}

function Test-CommunityAssetVerificationFreshness {
    # An asset reviewed against an older Spotify build proves nothing about the one
    # LibreSpot ships. Every active entry must have been re-checked no earlier than
    # the pinned Spotify release, so a pin advance drags the catalog with it.
    $baselinePath = Join-Path $PSScriptRoot 'schemas/compatibility-baseline.json'
    $manifestPath = Join-Path $PSScriptRoot 'schemas/community-assets.json'

    foreach ($path in @($baselinePath, $manifestPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Community asset freshness input is missing: $path"
        }
    }

    $baseline = Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

    $releasedRaw = [string]$baseline.spotify.releasedDate
    if ([string]::IsNullOrWhiteSpace($releasedRaw)) {
        throw 'schemas/compatibility-baseline.json is missing spotify.releasedDate; the community catalog cannot be checked against the pinned client.'
    }

    $released = [datetime]::MinValue
    if (-not [datetime]::TryParseExact($releasedRaw, 'yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$released)) {
        throw "schemas/compatibility-baseline.json spotify.releasedDate is not a yyyy-MM-dd date: $releasedRaw"
    }

    # Discover the sections instead of listing them: a renamed or newly added
    # section used to make every asset inside it invisible to this gate.
    $entries = @()
    foreach ($property in $manifest.PSObject.Properties) {
        if ($property.Name -in @('$comment', 'manifestVersion', 'policy')) { continue }
        $value = $property.Value
        if ($null -eq $value) { continue }

        $assets = if ($value -is [System.Collections.IEnumerable] -and $value -isnot [string]) { @($value) } else { @($value) }
        foreach ($asset in $assets) {
            if ($null -eq $asset -or $asset -isnot [psobject]) { continue }
            if (-not $asset.PSObject.Properties['lastVerifiedDate']) { continue }
            $id = if ($asset.filename) { [string]$asset.filename }
                elseif ($asset.themeId) { [string]$asset.themeId }
                elseif ($asset.appId) { [string]$asset.appId }
                else { $property.Name }
            $entries += [pscustomobject]@{ Section = $property.Name; Id = $id; Asset = $asset }
        }
    }

    if ($entries.Count -eq 0) {
        throw 'schemas/community-assets.json produced no verifiable assets; the freshness gate would pass vacuously.'
    }

    $stale = @()
    foreach ($entry in $entries) {
        $supportState = [string]$entry.Asset.supportState
        if ([string]::IsNullOrWhiteSpace($supportState)) {
            # An absent supportState used to skip the asset entirely.
            $stale += "$($entry.Section)/$($entry.Id): supportState is missing, so its verification cannot be judged."
            continue
        }
        if ($supportState -ne 'active') {
            # A non-active asset is exempt from the date, but it must say why and
            # point somewhere, or 'degraded' becomes a silent way to dodge this gate.
            if ([string]::IsNullOrWhiteSpace([string]$entry.Asset.supportDetail)) {
                $stale += "$($entry.Section)/$($entry.Id): supportState is '$supportState' with no supportDetail explaining it."
            }
            continue
        }

        $verifiedRaw = [string]$entry.Asset.lastVerifiedDate
        $verified = [datetime]::MinValue
        if ([string]::IsNullOrWhiteSpace($verifiedRaw) -or
            -not [datetime]::TryParseExact($verifiedRaw, 'yyyy-MM-dd', [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$verified)) {
            $stale += "$($entry.Section)/$($entry.Id): lastVerifiedDate is missing or not a yyyy-MM-dd date ('$verifiedRaw')."
            continue
        }

        if ($verified -lt $released) {
            $stale += "$($entry.Section)/$($entry.Id): last verified $verifiedRaw, before pinned Spotify $($baseline.spotify.version) was released on $releasedRaw."
        }
    }

    if ($stale.Count -gt 0) {
        Write-Host '=== COMMUNITY ASSET VERIFICATION IS STALE ===' -ForegroundColor Red
        foreach ($item in $stale) { Write-Host "  $item" -ForegroundColor Red }
        Write-Host '  Re-check each asset against the pinned client and update lastVerifiedDate, or move it to a non-active supportState.' -ForegroundColor Red
        throw 'Active community assets must be verified against the pinned Spotify build.'
    }

    Write-Host "Community catalog verification is current for Spotify $($baseline.spotify.version) (released $releasedRaw); $($entries.Count) entries checked." -ForegroundColor Green
}

function Test-LocalReleaseTruth {
    $readmePath = Join-Path $PSScriptRoot 'README.md'
    if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
        throw 'Cannot validate release truth; README.md not found.'
    }

    $desktopVersion = Get-LibreSpotProjectVersion
    $desktopInformationalVersion = Get-LibreSpotProjectInformationalVersion
    $cliVersion = Get-LibreSpotCliProjectVersion
    $shellVersion = Get-LibreSpotShellDisplayVersion
    $mainVersion = Get-LibreSpotScriptVersion -Path $mainScript
    $backendVersion = Get-LibreSpotScriptVersion -Path $backendScript
    $readme = [System.IO.File]::ReadAllText($readmePath, [System.Text.Encoding]::UTF8)
    $badgeVersion = $desktopVersion.Replace('-', '--')
    $failures = @()

    if ($cliVersion -ne $desktopVersion) {
        $failures += "CLI version '$cliVersion' does not match Desktop version '$desktopVersion'."
    }
    if ($desktopInformationalVersion -ne $desktopVersion) {
        $failures += "Desktop InformationalVersion '$desktopInformationalVersion' does not match Version '$desktopVersion'."
    }
    if ($shellVersion -ne "v$desktopVersion") {
        $failures += "WPF display version '$shellVersion' does not match project version 'v$desktopVersion'."
    }
    if ($backendVersion -ne $mainVersion) {
        $failures += "Backend script version '$backendVersion' does not match standalone script version '$mainVersion'."
    }
    if (-not $readme.Contains("Version-$badgeVersion-brightgreen.svg")) {
        $failures += "README preview badge does not name '$desktopVersion'."
    }
    if (-not $readme.Contains("## What's New in v$desktopVersion")) {
        $failures += "README What's New heading does not name 'v$desktopVersion'."
    }
    if (-not $readme.Contains("Current source script version: **v$mainVersion**")) {
        $failures += "README does not distinguish current source script version 'v$mainVersion'."
    }

    if ($failures.Count -gt 0) {
        Write-Host '=== LOCAL RELEASE TRUTH DRIFT ===' -ForegroundColor Red
        foreach ($failure in $failures) { Write-Host "  $failure" -ForegroundColor Red }
        throw 'README and executable version claims must agree.'
    }

    Write-Host "Local release truth matches script v$mainVersion and preview v$desktopVersion." -ForegroundColor Green
}

function Test-PublicReleaseTruth {
    Test-LocalReleaseTruth
    $headers = @{ 'User-Agent' = 'LibreSpot-ReleaseTruth-Validator' }
    $uri = 'https://api.github.com/repos/SysAdminDoc/LibreSpot/releases/latest'
    try {
        $release = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 20 -ErrorAction Stop
    } catch {
        throw "Could not query the public GitHub latest-release channel: $($_.Exception.Message)"
    }

    if ($release.draft -or $release.prerelease -or [string]::IsNullOrWhiteSpace([string]$release.tag_name)) {
        throw "GitHub latest-release response is not a published stable release."
    }

    $tag = [string]$release.tag_name
    $stableVersion = $tag.TrimStart('v')
    $assetNames = @($release.assets | ForEach-Object { [string]$_.name })
    $requiredAssets = @('LibreSpot.ps1', 'LibreSpot.exe', 'checksums.txt')
    $missingAssets = @($requiredAssets | Where-Object { $_ -notin $assetNames })
    if ($missingAssets.Count -gt 0) {
        throw "Public stable $tag is missing documented assets: $($missingAssets -join ', ')."
    }

    $readme = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'README.md'), [System.Text.Encoding]::UTF8)
    if (-not $readme.Contains("Stable-$stableVersion-blue.svg")) {
        throw "README stable badge does not match public latest release $tag."
    }
    if (-not $readme.Contains("public latest stable release, $tag")) {
        throw "README release guidance does not identify the public latest stable release as $tag."
    }

    Write-Host "Public release truth matches $tag ($($assetNames.Count) assets)." -ForegroundColor Green
}

function Invoke-GitCommand {
    param(
        [Parameter(Mandatory)][string]$Arguments,
        [int]$TimeoutSeconds = 120
    )

    # git writes UTF-8 regardless of the console codepage, so redirect through
    # ProcessStartInfo with an explicit decoder instead of the pipeline.
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = 'git'
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $PSScriptRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    # Redirect stdin and disable the terminal prompt so a credential-less remote
    # fails instead of blocking this script on an invisible prompt.
    $startInfo.RedirectStandardInput = $true
    $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $startInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    $startInfo.EnvironmentVariables['GIT_TERMINAL_PROMPT'] = '0'
    $startInfo.EnvironmentVariables['GCM_INTERACTIVE'] = 'never'

    try {
        $process = [System.Diagnostics.Process]::Start($startInfo)
    } catch {
        return [pscustomobject]@{
            ExitCode = -1
            StandardOutput = ''
            StandardError = $_.Exception.Message
        }
    }

    $process.StandardInput.Close()

    # Start both reads before waiting: draining one pipe to the end first
    # deadlocks as soon as the other fills.
    $stdoutRead = $process.StandardOutput.ReadToEndAsync()
    $stderrRead = $process.StandardError.ReadToEndAsync()

    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        try { $process.Kill() } catch { }
        try { $process.WaitForExit(2000) } catch { }
        return [pscustomobject]@{
            ExitCode = -1
            StandardOutput = ''
            StandardError = "git did not finish within $TimeoutSeconds seconds: git $Arguments"
        }
    }

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        StandardOutput = $stdoutRead.GetAwaiter().GetResult()
        StandardError = $stderrRead.GetAwaiter().GetResult()
    }
}

function ConvertTo-CatalogComparable {
    param(
        [Parameter(Mandatory)][string]$Json,
        [switch]$Pretty
    )

    $catalog = $Json | ConvertFrom-Json
    if ($Pretty) {
        return ($catalog | ConvertTo-Json -Depth 16)
    }

    return ($catalog | ConvertTo-Json -Depth 16 -Compress)
}

function ConvertTo-ComparableText {
    param([AllowNull()][string]$Text)

    if ($null -eq $Text) {
        return ''
    }

    # Line endings and a trailing newline are not trust drift; everything else
    # in a generated file is compared verbatim.
    return ($Text -replace "`r`n", "`n").TrimEnd("`n")
}

function Test-CommunityCatalogTruth {
    param([switch]$FetchRemote)

    $catalogTool = Join-Path $PSScriptRoot 'tools/Build-CommunityCatalog.ps1'
    if (-not (Test-Path -LiteralPath $catalogTool -PathType Leaf)) {
        throw "Cannot find the community catalog generator at $catalogTool"
    }

    $localTruthRef = 'refs/librespot/catalog-truth'
    $publishedRef = 'origin/gh-pages'
    if ($FetchRemote) {
        # A shallow or single-branch clone has no origin/gh-pages in its fetch
        # refspec, so a plain "git fetch origin gh-pages" exits 0 and writes
        # nothing but FETCH_HEAD. Fetch into a ref this script owns so a
        # successful fetch always produces something to read.
        $publishedRef = $localTruthRef
        $fetch = Invoke-GitCommand -Arguments "fetch --quiet --force origin refs/heads/gh-pages:$publishedRef"
        if ($fetch.ExitCode -ne 0) {
            Write-Host "Community catalog truth is unverified: could not reach origin/gh-pages. $($fetch.StandardError.Trim())" -ForegroundColor Yellow
            return
        }
    }

    $published = Invoke-GitCommand -Arguments "show ${publishedRef}:catalog.json"
    if (-not $FetchRemote -and ($published.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($published.StandardOutput))) {
        # A --single-branch clone never creates origin/gh-pages, so fall back to
        # whatever the last -CatalogTruth fetched. Otherwise this check would
        # warn and pass on that clone shape no matter what the manifest says.
        $fallback = Invoke-GitCommand -Arguments "show ${localTruthRef}:catalog.json"
        if ($fallback.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($fallback.StandardOutput)) {
            $publishedRef = $localTruthRef
            $published = $fallback
        }
    }

    if ($published.ExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($published.StandardOutput)) {
        if ($FetchRemote) {
            # The fetch worked, so the remote is reachable. Not being able to
            # read the catalog now is a real failure, not an offline machine.
            Write-Host "Fetched origin/gh-pages but could not read ${publishedRef}:catalog.json." -ForegroundColor Red
            Write-Host "  $($published.StandardError.Trim())" -ForegroundColor Red
            throw 'The published community catalog could not be read, so catalog truth is unverified.'
        }

        Write-Host 'Community catalog truth is unverified: origin/gh-pages:catalog.json is not available locally.' -ForegroundColor Yellow
        Write-Host '  Run "Build-Scripts.ps1 -CatalogTruth" while online to fetch the published page and compare against it.' -ForegroundColor Yellow
        return
    }

    # Regenerate with the published build stamp so every generated file - the
    # HTML pages too, not just catalog.json - can be compared verbatim. Without
    # this, a change to the page generator leaves catalog.json byte-identical
    # and the live page goes stale unnoticed.
    $generatedDate = $null
    try {
        $generatedDate = ($published.StandardOutput | ConvertFrom-Json).generatedDate
    } catch {
        $generatedDate = $null
    }
    if ([string]::IsNullOrWhiteSpace([string]$generatedDate)) {
        $generatedDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
    }

    $stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('librespot-catalog-truth-' + [Guid]::NewGuid().ToString('N'))
    $localFiles = [ordered]@{}
    try {
        & $catalogTool -OutputDirectory $stagingRoot -RepoRoot $PSScriptRoot -GeneratedDate $generatedDate | Out-Null
        foreach ($file in @(Get-ChildItem -LiteralPath $stagingRoot -File | Sort-Object Name)) {
            $localFiles[$file.Name] = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        }
    } finally {
        if (Test-Path -LiteralPath $stagingRoot) {
            Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if (-not $localFiles.Contains('catalog.json')) {
        throw 'The community catalog generator did not write catalog.json.'
    }

    $driftReport = New-Object System.Collections.Generic.List[string]
    foreach ($name in @($localFiles.Keys)) {
        $publishedFile = if ($name -eq 'catalog.json') {
            $published
        } else {
            Invoke-GitCommand -Arguments "show ${publishedRef}:$name"
        }

        if ($publishedFile.ExitCode -ne 0) {
            $driftReport.Add("  [$name] generated locally but not published")
            continue
        }

        # catalog.json is compared as data: ConvertTo-Json indents differently
        # on Windows PowerShell 5.1 and PowerShell 7, so the bytes are
        # host-dependent even when the content is identical. Everything else is
        # compared verbatim.
        if ($name -eq 'catalog.json') {
            $localText = ConvertTo-CatalogComparable -Json $localFiles[$name]
            $publishedText = ConvertTo-CatalogComparable -Json $publishedFile.StandardOutput
            if ($localText -eq $publishedText) {
                continue
            }

            $localText = ConvertTo-CatalogComparable -Json $localFiles[$name] -Pretty
            $publishedText = ConvertTo-CatalogComparable -Json $publishedFile.StandardOutput -Pretty
        } else {
            $localText = ConvertTo-ComparableText -Text $localFiles[$name]
            $publishedText = ConvertTo-ComparableText -Text $publishedFile.StandardOutput
            if ($localText -eq $publishedText) {
                continue
            }
        }

        $differences = @(Compare-Object `
                -ReferenceObject ($publishedText -split "`n") `
                -DifferenceObject ($localText -split "`n") `
                -SyncWindow 50)
        foreach ($difference in ($differences | Select-Object -First 8)) {
            $lane = if ($difference.SideIndicator -eq '=>') { 'reviewed' } else { 'published' }
            $driftReport.Add("  [$name] [$lane] $($difference.InputObject.Trim())")
        }
        if ($differences.Count -gt 8) {
            $driftReport.Add("  [$name] ... $($differences.Count - 8) further differing lines")
        }
    }

    # Walk the published side too. Comparing only what the generator emits means
    # dropping an emission, or committing an extra page to gh-pages by hand,
    # leaves a file served from the site that nothing here has ever checked.
    $publishedListing = Invoke-GitCommand -Arguments "ls-tree --name-only -r $publishedRef"
    if ($publishedListing.ExitCode -ne 0) {
        $driftReport.Add("  [$publishedRef] could not be listed: $($publishedListing.StandardError.Trim())")
    } else {
        foreach ($name in @($publishedListing.StandardOutput -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            if (-not $localFiles.Contains($name.Trim())) {
                $driftReport.Add("  [$($name.Trim())] served from gh-pages but the generator does not produce it")
            }
        }
    }

    if ($driftReport.Count -eq 0) {
        Write-Host "Published community catalog matches the reviewed asset manifest ($($localFiles.Count) files)." -ForegroundColor Green
        return
    }

    Write-Host '=== PUBLISHED COMMUNITY CATALOG DRIFT ===' -ForegroundColor Red
    foreach ($line in $driftReport) {
        Write-Host $line -ForegroundColor Red
    }
    Write-Host ''
    Write-Host 'The catalog page at https://sysadmindoc.github.io/LibreSpot/ is advertising trust evidence that no longer matches schemas/community-assets.json.' -ForegroundColor Red
    if (-not $FetchRemote) {
        # This mode compares against whatever origin/gh-pages the clone holds,
        # which is stale if the catalog was published from another machine.
        Write-Host 'This run did not fetch. If the catalog was published elsewhere, run "Build-Scripts.ps1 -CatalogTruth" first and re-check before regenerating.' -ForegroundColor Red
    }
    Write-Host 'Regenerate and republish:' -ForegroundColor Red
    Write-Host '  .\tools\Build-CommunityCatalog.ps1 -OutputDirectory <staging>' -ForegroundColor Red
    Write-Host '  git worktree add <worktree> gh-pages; copy the staging output over it; commit and push gh-pages' -ForegroundColor Red
    throw 'The published community catalog has drifted from the reviewed asset manifest.'
}

function Test-CustomizationCatalogTruth {
    $catalogTool = Join-Path $PSScriptRoot 'src/LibreSpot.App/scripts/catalog-tool.mjs'
    if (-not (Test-Path -LiteralPath $catalogTool -PathType Leaf)) {
        throw "Cannot find the customization catalog verifier at $catalogTool"
    }

    $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
    if ($null -eq $nodeCommand) {
        throw 'Node.js is required to verify the Spotify customization catalog.'
    }

    & $nodeCommand.Source $catalogTool truth
    if ($LASTEXITCODE -ne 0) {
        throw 'The Spotify customization catalog has drifted from the pinned xpui and SpotX sources.'
    }
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

function Get-PngPixelSize {
    param([Parameter(Mandatory)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    # 8-byte signature, then the IHDR chunk: 4-byte length, 4-byte type, then
    # width and height as big-endian 32-bit integers.
    if ($bytes.Length -lt 24) { return $null }
    if ([System.Text.Encoding]::ASCII.GetString($bytes, 12, 4) -ne 'IHDR') { return $null }

    $width = (
        ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor
        ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
    )
    $height = (
        ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor
        ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
    )
    return [pscustomobject]@{ Width = $width; Height = $height }
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
    # README captures are taken at a 1440x1024 logical viewport, which renders at
    # 1800x1280 on the 125% display the release is built from. Passing the pixel
    # size to --uia-size instead of the logical size silently produces 2250x1600.
    $expectedCaptureWidth = 1800
    $expectedCaptureHeight = 1280
    $expectedCaptureTheme = 'dark'
    $expectedCaptureCulture = 'en'
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
        $theme = Get-PngTextMetadataValue -Path $fullPath -Key 'LibreSpotCaptureTheme'
        $culture = Get-PngTextMetadataValue -Path $fullPath -Key 'LibreSpotCaptureCulture'
        $pixelSize = Get-PngPixelSize -Path $fullPath

        if ($null -eq $pixelSize) {
            $failures += "${relativePath}: PNG header could not be read."
        } elseif ($pixelSize.Width -ne $expectedCaptureWidth -or $pixelSize.Height -ne $expectedCaptureHeight) {
            $failures += "${relativePath}: captured at $($pixelSize.Width)x$($pixelSize.Height); expected ${expectedCaptureWidth}x${expectedCaptureHeight} (use --uia-size=1440x1024)."
        }
        if ($theme -ne $expectedCaptureTheme) {
            $failures += "${relativePath}: LibreSpotCaptureTheme '$theme' does not match '$expectedCaptureTheme'."
        }
        if ($culture -ne $expectedCaptureCulture) {
            $failures += "${relativePath}: LibreSpotCaptureCulture '$culture' does not match '$expectedCaptureCulture'."
        }

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

function Get-PinnedSpotXSecurityMetadata {
    $content = [System.IO.File]::ReadAllText($mainScript, [System.Text.Encoding]::UTF8)
    $match = [regex]::Match($content, '(?ms)^\s{4}SpotX\s*=\s*@\{(?<body>.+?)^\s{4}\}')
    if (-not $match.Success) {
        throw 'PinnedReleases.SpotX block was not found in LibreSpot.ps1.'
    }

    $body = $match.Groups['body'].Value
    $fields = @{}
    foreach ($name in @('Commit', 'Url', 'SHA256', 'DefenderOptOut', 'DefenderPolicyCommit', 'DefenderPolicyOptOut')) {
        $field = [regex]::Match($body, "(?m)^\s*$name\s*=\s*'(?<value>[^']*)'\s*$")
        if (-not $field.Success) { throw "PinnedReleases.SpotX.$name is missing." }
        $fields[$name] = [string]$field.Groups['value'].Value
    }
    $mutationField = [regex]::Match($body, '(?mi)^\s*DefenderMutations\s*=\s*\$(?<value>true|false)\s*$')
    if (-not $mutationField.Success) { throw 'PinnedReleases.SpotX.DefenderMutations is missing.' }
    $policyActiveField = [regex]::Match($body, '(?mi)^\s*DefenderPolicyActive\s*=\s*\$(?<value>true|false)\s*$')
    if (-not $policyActiveField.Success) { throw 'PinnedReleases.SpotX.DefenderPolicyActive is missing.' }

    return [pscustomobject][ordered]@{
        commit            = $fields.Commit
        url               = $fields.Url
        sha256            = $fields.SHA256.ToLowerInvariant()
        defenderMutations = [string]$mutationField.Groups['value'].Value -eq 'true'
        defenderOptOut    = $fields.DefenderOptOut
        defenderPolicyCommit = $fields.DefenderPolicyCommit
        defenderPolicyOptOut = $fields.DefenderPolicyOptOut
        defenderPolicyActive = [string]$policyActiveField.Groups['value'].Value -eq 'true'
    }
}

function Test-SpotXInstallerSecurityPolicy {
    param(
        [Parameter(Mandatory)][string]$ScriptPath,
        [Parameter(Mandatory)][string]$ExpectedHash,
        [Parameter(Mandatory)][bool]$DeclaredDefenderMutations,
        [AllowEmptyString()][string]$DeclaredDefenderOptOut
    )

    if (-not (Test-Path -LiteralPath $ScriptPath -PathType Leaf)) {
        throw "SpotX entrypoint not found: $ScriptPath"
    }
    $info = Get-Item -LiteralPath $ScriptPath
    if ($info.Length -le 0 -or $info.Length -gt 1048576) {
        throw "SpotX entrypoint has an invalid size: $($info.Length) bytes."
    }
    $actualHash = Get-FileSha256Lower -Path $ScriptPath
    if ($actualHash -ne $ExpectedHash.ToLowerInvariant()) {
        throw "SpotX entrypoint hash mismatch. Expected $ExpectedHash, got $actualHash."
    }

    $content = [System.IO.File]::ReadAllText($ScriptPath, [System.Text.Encoding]::UTF8)
    $indicators = @()
    foreach ($indicator in @(
        @{ Name = 'Add-MpPreference'; Pattern = '(?i)\bAdd-MpPreference\b' },
        @{ Name = 'Set-MpPreference'; Pattern = '(?i)\bSet-MpPreference\b' },
        @{ Name = 'ExclusionPath'; Pattern = '(?i)-ExclusionPath\b' },
        @{ Name = 'ExclusionProcess'; Pattern = '(?i)-ExclusionProcess\b' }
    )) {
        if ([regex]::IsMatch($content, [string]$indicator.Pattern)) { $indicators += [string]$indicator.Name }
    }
    $containsMutations = $indicators.Count -gt 0
    $declaresUpstreamOptOut = [regex]::IsMatch($content, '(?i)\bdefender_exclusions_off\b')

    if ($containsMutations -ne $DeclaredDefenderMutations) {
        throw "SpotX Defender-mutation metadata does not match the pinned entrypoint (detected: $containsMutations; declared: $DeclaredDefenderMutations)."
    }
    if ($containsMutations) {
        if (-not $declaresUpstreamOptOut -or $DeclaredDefenderOptOut -cne '-defender_exclusions_off') {
            throw 'SpotX contains Defender mutations but its pinned adapter does not prove the exact upstream -defender_exclusions_off switch.'
        }
    } elseif (-not [string]::IsNullOrWhiteSpace($DeclaredDefenderOptOut)) {
        throw 'The safe SpotX pin must not receive an unsupported Defender opt-out argument.'
    }

    return [pscustomobject][ordered]@{
        status                     = 'ok'
        sha256                     = $actualHash
        containsDefenderMutations  = $containsMutations
        defenderMutationIndicators = @($indicators)
        declaresUpstreamOptOut     = $declaresUpstreamOptOut
        adapterOptOut              = $DeclaredDefenderOptOut
    }
}

function Get-PinnedSpotXSecurityPolicy {
    param(
        [string]$ScriptPath,
        [string]$CandidateCommit,
        [switch]$CandidatePostDefenderPolicy,
        [bool]$CandidateDefenderMutations = $false,
        [AllowEmptyString()][string]$CandidateDefenderOptOut = '',
        [AllowEmptyString()][string]$CandidateArguments = ''
    )

    $metadata = Get-PinnedSpotXSecurityMetadata
    if (-not [string]::IsNullOrWhiteSpace($CandidateCommit)) {
        if ([string]::IsNullOrWhiteSpace($ScriptPath)) {
            throw 'A local SpotX candidate script is required for pin-advance policy review.'
        }
        $policy = Test-SpotXPinAdvanceSecurityPolicy `
            -ScriptPath $ScriptPath `
            -CurrentCommit $metadata.commit `
            -CandidateCommit $CandidateCommit `
            -PolicyCommit $metadata.defenderPolicyCommit `
            -RequiredOptOut $metadata.defenderPolicyOptOut `
            -DeclaredDefenderMutations $CandidateDefenderMutations `
            -DeclaredDefenderOptOut $CandidateDefenderOptOut `
            -InvocationArguments $CandidateArguments `
            -PostDefenderPolicy:$CandidatePostDefenderPolicy
        return [pscustomobject][ordered]@{
            mode = 'candidate'
            commit = $CandidateCommit
            currentCommit = $metadata.commit
            policyBoundaryCommit = $metadata.defenderPolicyCommit
            policyRequiredOptOut = $metadata.defenderPolicyOptOut
            policy = $policy
        }
    }

    $downloadedPath = $null
    try {
        if ([string]::IsNullOrWhiteSpace($ScriptPath)) {
            $downloadedPath = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-spotx-policy-{0}.ps1" -f [Guid]::NewGuid().ToString('N'))
            Invoke-WebRequest -UseBasicParsing -Uri $metadata.url -OutFile $downloadedPath
            $ScriptPath = $downloadedPath
        }

        $policy = Test-SpotXInstallerSecurityPolicy `
            -ScriptPath $ScriptPath `
            -ExpectedHash $metadata.sha256 `
            -DeclaredDefenderMutations $metadata.defenderMutations `
            -DeclaredDefenderOptOut $metadata.defenderOptOut
        return [pscustomobject][ordered]@{
            mode = 'pinned'
            commit = $metadata.commit
            url = $metadata.url
            policyBoundaryCommit = $metadata.defenderPolicyCommit
            policyRequiredOptOut = $metadata.defenderPolicyOptOut
            policyActive = $metadata.defenderPolicyActive
            policy = $policy
        }
    } finally {
        if ($downloadedPath) { Remove-Item -LiteralPath $downloadedPath -Force -ErrorAction SilentlyContinue }
    }
}

function Test-PinnedSpotXSecurityAdapter {
    $metadata = Get-PinnedSpotXSecurityMetadata
    $mainContent = [System.IO.File]::ReadAllText($mainScript, [System.Text.Encoding]::UTF8)
    $backendContent = [System.IO.File]::ReadAllText($backendScript, [System.Text.Encoding]::UTF8)
    foreach ($lane in @(
        @{ Name = 'main'; Content = $mainContent },
        @{ Name = 'backend'; Content = $backendContent }
    )) {
        if (-not $lane.Content.Contains('Assert-LibreSpotExternalScriptDefenderPolicy -Stream $stream -Arguments $Arguments -Label $Label')) {
            throw "The $($lane.Name) execution gate does not enforce the Defender policy."
        }
        if (-not $lane.Content.Contains('Open-VerifiedScriptForExecution -FilePath $FilePath -ExpectedHash $ExpectedHash -Label $Label -Arguments $Arguments')) {
            throw "The $($lane.Name) external-script adapter does not pass arguments into the Defender policy."
        }
        if (-not $lane.Content.Contains('$global:PinnedReleases.SpotX.DefenderMutations') -or
            -not $lane.Content.Contains('$global:PinnedReleases.SpotX.DefenderOptOut') -or
            -not $lane.Content.Contains('$global:PinnedReleases.SpotX.DefenderPolicyActive') -or
            -not $lane.Content.Contains('$global:PinnedReleases.SpotX.DefenderPolicyOptOut')) {
            throw "The $($lane.Name) SpotX adapter does not consume Defender policy metadata."
        }
    }
    if (-not $mainContent.Contains("-Label 'SpotX run.ps1 (watcher)' -Arguments `$spotxArgs")) {
        throw 'The stable watcher does not pass SpotX arguments into the Defender policy.'
    }
    if ((-not $metadata.defenderMutations) -and -not [string]::IsNullOrWhiteSpace($metadata.defenderOptOut)) {
        throw 'The current safe SpotX pin declares an unsupported Defender opt-out argument.'
    }
    if ($metadata.defenderMutations -and $metadata.defenderOptOut -cne '-defender_exclusions_off') {
        throw 'A Defender-mutating SpotX pin must declare the exact upstream opt-out.'
    }
    if ($metadata.defenderPolicyCommit -notmatch '^[0-9a-f]{8,40}$') {
        throw 'The SpotX Defender policy boundary must be a hexadecimal commit identifier.'
    }
    if ($metadata.defenderPolicyOptOut -cne '-defender_exclusions_off') {
        throw 'The SpotX Defender policy must require the exact upstream opt-out.'
    }
    if ($metadata.defenderPolicyActive) {
        throw 'The current pinned SpotX commit cannot activate the post-Defender policy.'
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

function Get-LibreSpotStableExeFileVersion {
    param([Parameter(Mandatory)][string]$Path)

    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    return [string]$info.FileVersion
}

function Test-LibreSpotStableExeIdentity {
    param([Parameter(Mandatory)][string]$Path)

    $scriptVersion = Get-LibreSpotScriptVersion -Path $mainScript
    $fileVersion = Get-LibreSpotStableExeFileVersion -Path $Path
    if ([string]::IsNullOrWhiteSpace($fileVersion)) {
        throw "LibreSpot.exe has no file version resource; rebuild it with Build-Scripts.ps1 -CompileStableExe."
    }

    # PS2EXE writes a four-part file version; the script declares three parts.
    $expectedPrefix = "$scriptVersion."
    if ($fileVersion -ne $scriptVersion -and -not $fileVersion.StartsWith($expectedPrefix, [System.StringComparison]::Ordinal)) {
        throw "LibreSpot.exe reports file version '$fileVersion' but LibreSpot.ps1 is version '$scriptVersion'; rebuild it with Build-Scripts.ps1 -CompileStableExe."
    }

    Write-Host "  Stable script executable identity matches LibreSpot.ps1 v$scriptVersion (file version $fileVersion)." -ForegroundColor Green
}

function Get-LibreSpotReleaseBuildProperties {
    # The exact property set the release build pins. Recorded in the manifest so a
    # second party can rebuild with the same inputs and compare.
    [ordered]@{
        Configuration               = 'Release'
        RuntimeIdentifier           = 'win-x64'
        SelfContained               = 'true'
        PublishSingleFile           = 'true'
        EnableCompressionInSingleFile = 'true'
        Deterministic               = 'true'
        ContinuousIntegrationBuild  = 'true'
        EmbedUntrackedSources       = 'true'
        PublishRepositoryUrl        = 'true'
    }
}

function Invoke-LibreSpotReleasePublish {
    param([string]$Root)

    if ([string]::IsNullOrWhiteSpace($Root)) { $Root = Join-Path $PSScriptRoot 'publish' }
    $Root = [System.IO.Path]::GetFullPath($Root)

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) { throw 'Cannot publish the release; dotnet was not found on PATH.' }

    # This deletes the folder it is given, so refuse anything that is not a
    # disposable release root. A mistyped -ReleaseRoot must not take a source
    # tree, a profile folder, or a drive root with it.
    $repoRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
    $rootParent = [System.IO.Path]::GetDirectoryName($Root)
    if ([string]::IsNullOrEmpty($rootParent)) {
        throw "Refusing to publish into a drive root: $Root"
    }
    if ($Root.TrimEnd('\') -ieq $repoRoot.TrimEnd('\')) {
        throw "Refusing to publish into the repository root: $Root"
    }
    foreach ($protectedName in @('.git', 'src', 'tests', 'schemas', 'tools', 'resources', 'docs')) {
        if (Test-Path -LiteralPath (Join-Path $Root $protectedName)) {
            throw "Refusing to delete $Root because it contains '$protectedName'. Point -ReleaseRoot at a disposable folder."
        }
    }

    if (Test-Path -LiteralPath $Root) {
        $unexpected = @(Get-ChildItem -LiteralPath $Root -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch '^(LibreSpot[-.].*|librespot-.*|checksums\.txt|dependency-health\.json|stage-.*)$' })
        if ($unexpected.Count -gt 0) {
            throw ("Refusing to delete $Root because it holds files a release build did not produce: " +
                (($unexpected | Select-Object -First 5 | ForEach-Object { $_.Name }) -join ', ') +
                ". Point -ReleaseRoot at a disposable folder or empty it yourself.")
        }
        Write-Host "Cleaning $Root..." -ForegroundColor Cyan
        Remove-Item -LiteralPath $Root -Recurse -Force
    }
    New-Item -Path $Root -ItemType Directory -Force | Out-Null

    $properties = Get-LibreSpotReleaseBuildProperties
    $projects = @(
        @{ Path = 'src/LibreSpot.Desktop/LibreSpot.Desktop.csproj'; Produces = 'LibreSpot.dll'; Asset = 'LibreSpot-Desktop.exe'; Built = 'LibreSpot.exe' }
        @{ Path = 'src/LibreSpot.Cli/LibreSpot.Cli.csproj';         Produces = 'LibreSpot.Cli.dll'; Asset = 'LibreSpot.Cli.exe'; Built = 'LibreSpot.Cli.exe' }
    )

    foreach ($project in $projects) {
        $projectPath = Join-Path $PSScriptRoot $project.Path
        if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
            throw "Cannot publish the release; project not found: $projectPath"
        }

        $stage = Join-Path $Root ('stage-' + [System.IO.Path]::GetFileNameWithoutExtension($project.Path))
        $arguments = @('publish', $projectPath, '-c', $properties.Configuration, '-r', $properties.RuntimeIdentifier, '--self-contained', $properties.SelfContained, '-o', $stage, '--nologo')
        foreach ($name in @('PublishSingleFile', 'EnableCompressionInSingleFile', 'Deterministic', 'ContinuousIntegrationBuild', 'EmbedUntrackedSources', 'PublishRepositoryUrl')) {
            $arguments += "-p:$name=$($properties[$name])"
        }
        # ContinuousIntegrationBuild is gated on this flag in Directory.Build.props so
        # a developer build keeps its local paths for debugging.
        $arguments += '-p:LibreSpotReleaseBuild=true'

        Write-Host "Publishing $($project.Path)..." -ForegroundColor Cyan
        & dotnet @arguments | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($project.Path) with exit code $LASTEXITCODE." }

        $built = Join-Path $stage $project.Built
        if (-not (Test-Path -LiteralPath $built -PathType Leaf)) {
            throw "Publish did not produce $built."
        }
        Move-Item -LiteralPath $built -Destination (Join-Path $Root $project.Asset) -Force
        Remove-Item -LiteralPath $stage -Recurse -Force
    }

    # The custom-app archive is installed at run time from the release that ships
    # it (RD-142), so it travels with the other assets.
    $engineArchive = Join-Path $PSScriptRoot 'resources/custom-apps/librespot-engine.zip'
    if (-not (Test-Path -LiteralPath $engineArchive -PathType Leaf)) {
        throw "Cannot publish the release; $engineArchive not found."
    }
    Copy-Item -LiteralPath $engineArchive -Destination (Join-Path $Root 'librespot-engine.zip') -Force
    Copy-Item -LiteralPath $mainScript -Destination (Join-Path $Root 'LibreSpot.ps1') -Force

    foreach ($asset in @('LibreSpot-Desktop.exe', 'LibreSpot.Cli.exe', 'librespot-engine.zip', 'LibreSpot.ps1')) {
        $path = Join-Path $Root $asset
        Write-Host ("  {0,-24} {1,12:N0} bytes  {2}" -f $asset, (Get-Item -LiteralPath $path).Length, (Get-FileSha256Lower -Path $path)) -ForegroundColor Gray
    }

    Write-Host "Release publish complete: $Root" -ForegroundColor Green
    Write-Host 'Run -CompileStableExe, -GenerateSbom, then -GenerateReleaseManifest to finish the release root.' -ForegroundColor Gray
}

function Invoke-LibreSpotStableExeCompile {
    param([string]$OutputPath)

    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $OutputPath = Join-Path $PSScriptRoot 'publish/LibreSpot.exe'
    }
    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)

    $iconPath = Join-Path $PSScriptRoot 'LibreSpot.ico'
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
        throw "Cannot compile the stable executable; LibreSpot.ico not found."
    }

    $scriptVersion = Get-LibreSpotScriptVersion -Path $mainScript
    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null
    }

    # ps2exe targets PowerShell 7 on this toolchain, so the compile runs through
    # pwsh even though the rest of Build-Scripts.ps1 runs under Windows PowerShell.
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -eq $pwsh) {
        throw "Cannot compile the stable executable; pwsh was not found on PATH."
    }

    $command = @(
        "Import-Module ps2exe -ErrorAction Stop;",
        "Invoke-ps2exe",
        "-inputFile '$mainScript'",
        "-outputFile '$OutputPath'",
        "-iconFile '$iconPath'",
        "-title 'LibreSpot'",
        "-product 'LibreSpot'",
        "-version '$scriptVersion.0'",
        "-requireAdmin",
        "-noConsole"
    ) -join ' '

    Write-Host "Compiling LibreSpot.ps1 v$scriptVersion with PS2EXE..." -ForegroundColor Cyan
    & $pwsh.Source -NoProfile -Command $command
    if ($LASTEXITCODE -ne 0) {
        throw "PS2EXE compilation failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "PS2EXE reported success but produced no file at $OutputPath."
    }

    Test-LibreSpotStableExeIdentity -Path $OutputPath
    Write-Host "Stable script executable written: $OutputPath" -ForegroundColor Green
}

function Get-LibreSpotCycloneDxToolVersion {
    $manifestPath = Join-Path $PSScriptRoot '.config/dotnet-tools.json'
    $manifest = Get-JsonFile -Path $manifestPath
    $tool = $manifest.tools.CycloneDX
    if ($null -eq $tool) {
        throw "CycloneDX is not pinned in .config/dotnet-tools.json."
    }
    return [string]$tool.version
}

function Test-LibreSpotSbom {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "CycloneDX SBOM not found at $Path. Run Build-Scripts.ps1 -GenerateSbom."
    }

    $document = Get-JsonFile -Path $Path
    if ([string]$document.bomFormat -ne 'CycloneDX') {
        throw "SBOM bomFormat is '$($document.bomFormat)'; expected CycloneDX."
    }
    if ([string]$document.specVersion -ne '1.7') {
        throw "SBOM specVersion is '$($document.specVersion)'; expected 1.7."
    }

    $pinnedVersion = Get-LibreSpotCycloneDxToolVersion
    $toolVersions = @()
    if ($document.metadata.tools.components) {
        foreach ($tool in @($document.metadata.tools.components)) {
            $toolVersions += [string]$tool.version
        }
    }
    if ($document.metadata.tools) {
        foreach ($tool in @($document.metadata.tools)) {
            if ($tool.PSObject.Properties['version']) {
                $toolVersions += [string]$tool.version
            }
        }
    }

    $matchesPin = $false
    foreach ($version in $toolVersions) {
        if ($version -eq $pinnedVersion -or $version.StartsWith("$pinnedVersion.", [System.StringComparison]::Ordinal)) {
            $matchesPin = $true
            break
        }
    }
    if (-not $matchesPin) {
        throw "SBOM tool version is '$(($toolVersions | Where-Object { $_ }) -join ', ')'; expected CycloneDX $pinnedVersion."
    }

    $components = @($document.components)
    if ($components.Count -lt 1) {
        throw "SBOM has no components."
    }

    foreach ($component in $components) {
        $name = [string]$component.name
        $hashes = @($component.hashes)
        $licenses = @($component.licenses)
        if ($hashes.Count -lt 1) {
            throw "SBOM component '$name' is missing hashes."
        }
        if ($licenses.Count -lt 1) {
            throw "SBOM component '$name' is missing licenses."
        }
    }
}

function Invoke-LibreSpotSbomGenerate {
    param([string]$OutputPath)

    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $OutputPath = Join-Path $PSScriptRoot 'publish/LibreSpot.sbom.cdx.json'
    }
    $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    $outputDirectory = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null
    }

    Push-Location $PSScriptRoot
    try {
        & dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet tool restore failed with exit code $LASTEXITCODE."
        }

        $fileName = Split-Path -Leaf $OutputPath
        & dotnet tool run dotnet-CycloneDX -- `
            (Join-Path $PSScriptRoot 'src/LibreSpot.Desktop/LibreSpot.Desktop.csproj') `
            --json `
            --exclude-dev `
            -o $outputDirectory `
            -fn $fileName
        if ($LASTEXITCODE -ne 0) {
            throw "CycloneDX generation failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    Test-LibreSpotSbom -Path $OutputPath
    Write-Host "CycloneDX SBOM written: $OutputPath" -ForegroundColor Green
}

function Test-LibreSpotPublishFootprint {
    param([Parameter(Mandatory)][string]$Root)

    # schemas/publish-footprint-budget.json recorded a budget nothing measured,
    # because the mechanism it named (release CI) does not exist in this repo.
    # The local release build is the only build, so it enforces the budget.
    $budgetPath = Join-Path $PSScriptRoot 'schemas/publish-footprint-budget.json'
    $budgetDocument = Get-JsonFile -Path $budgetPath
    $budget = $budgetDocument.budget
    $artifactName = [string]$budget.artifact
    $artifactPath = Join-Path $Root $artifactName
    if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw "Publish footprint budget cannot be checked; $artifactName not found in $Root."
    }

    $sizeBytes = (Get-Item -LiteralPath $artifactPath).Length
    $sizeMiB = [math]::Round($sizeBytes / 1MB, 2)
    $maxSizeMiB = [double]$budget.maxSizeMiB
    $warnSizeMiB = [double]$budget.warnSizeMiB

    if ($sizeMiB -gt $maxSizeMiB) {
        throw "$artifactName is $sizeMiB MiB, over the $maxSizeMiB MiB publish budget in schemas/publish-footprint-budget.json."
    }

    if ($sizeMiB -gt $warnSizeMiB) {
        Write-Host "  WARNING: $artifactName is $sizeMiB MiB, past the $warnSizeMiB MiB warning threshold (budget $maxSizeMiB MiB)." -ForegroundColor Yellow
    } else {
        Write-Host "  Publish footprint: $artifactName is $sizeMiB MiB (warn $warnSizeMiB MiB, max $maxSizeMiB MiB)." -ForegroundColor Green
    }

    return [ordered]@{
        artifact    = $artifactName
        sizeBytes   = $sizeBytes
        sizeMiB     = $sizeMiB
        warnSizeMiB = $warnSizeMiB
        maxSizeMiB  = $maxSizeMiB
        status      = if ($sizeMiB -gt $warnSizeMiB) { 'warn' } else { 'ok' }
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
    $footprint = Test-LibreSpotPublishFootprint -Root $Root
    if (-not $SkipStableExeIdentity) {
        Test-LibreSpotStableExeIdentity -Path (Join-Path $Root 'LibreSpot.exe')
    }
    Test-LibreSpotSbom -Path (Join-Path $Root 'LibreSpot.sbom.cdx.json')
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
        publishFootprint = $footprint
        # What the release was built with, so anyone can rebuild and compare
        # (RD-146). Recorded, not asserted: a rebuild that differs tells you the
        # inputs differed, and these are the inputs.
        buildInputs      = [ordered]@{
            sdkVersion            = (& dotnet --version 2>$null | Select-Object -First 1)
            commit                = (& git -C $PSScriptRoot rev-parse HEAD 2>$null | Select-Object -First 1)
            # A commit only identifies the build when the tree matched it. Say so
            # rather than let the manifest imply a rebuild will reproduce this.
            treeClean             = (@(& git -C $PSScriptRoot status --porcelain 2>$null).Count -eq 0)
            properties            = Get-LibreSpotReleaseBuildProperties
            # Measured on 2026-09-03 by publishing the same commit into two roots
            # and comparing SHA256: the .NET assets matched, LibreSpot.exe did not.
            reproducibleAssets    = @('LibreSpot-Desktop.exe', 'LibreSpot.Cli.exe', 'librespot-engine.zip', 'LibreSpot.ps1')
            nonDeterministicNotes = @(
                'LibreSpot.exe is produced by ps2exe, which does not build reproducibly: two compiles of the same script produce different bytes. Verify it against checksums.txt from its own release rather than by rebuilding.'
            )
        }
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

function Get-LibreSpotJavaScriptAudit {
    param([Parameter(Mandatory)][string]$WorkspacePath)

    # The live customization engine is a real dependency of the shipped product,
    # but -DependencyHealth only ever looked at NuGet, so an advisory in its
    # JavaScript tree was invisible to every local gate.
    $result = [ordered]@{
        workspace   = ConvertTo-RepoRelativePath -Path $WorkspacePath
        ran         = $false
        reason      = ''
        advisories  = @()
        failures    = @()
    }

    if (-not (Test-Path -LiteralPath (Join-Path $WorkspacePath 'package.json') -PathType Leaf)) {
        $result.reason = 'No package.json; nothing to audit.'
        return $result
    }

    $pnpm = Get-Command pnpm -ErrorAction SilentlyContinue
    if (-not $pnpm) {
        $candidate = Join-Path $env:APPDATA 'npm\pnpm.cmd'
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $pnpm = $candidate } 
    } else {
        $pnpm = $pnpm.Source
    }
    if (-not $pnpm) {
        $result.reason = 'pnpm was not found on PATH; the JavaScript audit was skipped.'
        $result.failures += 'pnpm is required to audit the live customization engine.'
        return $result
    }

    foreach ($scope in @('prod', 'all')) {
        $arguments = @('audit', '--json')
        if ($scope -eq 'prod') { $arguments += '--prod' }

        # pnpm audits the lockfile of the directory it is invoked in. The gate is
        # documented as being run from the repository root, which has no lockfile,
        # so without this the command failed with ERR_PNPM_AUDIT_NO_LOCKFILE and
        # the gate passed having examined nothing.
        $stderrPath = [System.IO.Path]::GetTempFileName()
        $previousErrorAction = $ErrorActionPreference
        $raw = ''
        $exitCode = $null
        $stderrText = ''
        $invocationError = $null
        Push-Location -LiteralPath $WorkspacePath
        try {
            # stderr goes to its own file: pnpm writes registry and deprecation
            # notices there, and folding them into stdout breaks the JSON parse.
            #
            # The preference is lowered for the call itself. This script runs with
            # ErrorActionPreference = Stop, and pnpm has two entirely normal ways
            # to upset that: it writes update and deprecation notices to stderr,
            # and `pnpm audit` exits non-zero precisely when it FINDS something.
            # Either can become a terminating error depending on the host and on
            # $PSNativeCommandUseErrorActionPreference, which would abort the gate
            # at the moment it had news to report. Both are inspected below.
            $ErrorActionPreference = 'Continue'
            $raw = & $pnpm @arguments 2>$stderrPath | Out-String
            $exitCode = $LASTEXITCODE
        } catch {
            # Still a failure to record, never a reason to abandon the gate.
            $invocationError = $_.Exception.Message
        } finally {
            $ErrorActionPreference = $previousErrorAction
            Pop-Location

            # Casting an EMPTY pipeline to [string] yields $null, not '', so the
            # Trim has to come after a null check rather than off the cast.
            $stderrRaw = Get-Content -LiteralPath $stderrPath -Raw -ErrorAction SilentlyContinue
            $stderrText = if ($null -eq $stderrRaw) { '' } else { ([string]$stderrRaw).Trim() }
            Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
        }

        $result.ran = $true

        if ($null -ne $invocationError) {
            $result.failures += "pnpm audit --$scope could not be run: $invocationError $stderrText".Trim()
            continue
        }

        # Every path out of here records a failure. A silent skip is what let the
        # broken invocation above look like a clean audit. Note that pnpm exits
        # non-zero when it FINDS advisories, so the exit code is diagnostic only.
        if ([string]::IsNullOrWhiteSpace($raw)) {
            $result.failures += "pnpm audit --$scope produced no output (exit $exitCode). $stderrText".Trim()
            continue
        }

        $parsed = $null
        try { $parsed = $raw | ConvertFrom-Json } catch {
            $result.failures += "pnpm audit --$scope output could not be parsed (exit $exitCode): $($_.Exception.Message)"
            continue
        }

        if ($parsed.PSObject.Properties['error']) {
            $result.failures += "pnpm audit --$scope failed: $($parsed.error.code) $($parsed.error.message)"
            continue
        }

        if (-not $parsed.PSObject.Properties['advisories']) {
            $result.failures += "pnpm audit --$scope returned no advisories section (exit $exitCode); the audit cannot be trusted."
            continue
        }

        foreach ($property in $parsed.advisories.PSObject.Properties) {
            $advisory = $property.Value
            $result.advisories += [ordered]@{
                scope       = $scope
                id          = [string]$advisory.id
                moduleName  = [string]$advisory.module_name
                severity    = [string]$advisory.severity
                title       = [string]$advisory.title
                url         = [string]$advisory.url
                vulnerable  = [string]$advisory.vulnerable_versions
            }
        }
    }

    return $result
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

        [void][DateTime]::Parse([string]$entry.recheckDate, [System.Globalization.CultureInfo]::InvariantCulture)
        if ([string]$entry.scope -ne 'test-transitive') {
            throw "Dependency health allowlist only accepts test-transitive lag: $($entry.packageId)."
        }
    }

    # Accepted JavaScript advisories carry the same obligations as lagging
    # packages. They are validated here, where the file is already open, so a
    # malformed entry stops the gate instead of silently accepting an advisory.
    # @($null) is a ONE-element array holding $null, not an empty array, so an
    # allowlist written before this section existed would run the body once with
    # a null entry and die on "Cannot index into a null array" instead of being
    # skipped.
    foreach ($advisory in @($doc.acceptedJavaScriptAdvisories | Where-Object { $null -ne $_ })) {
        foreach ($field in @('id', 'owner', 'reason', 'recheckDate')) {
            if (-not $advisory.PSObject.Properties[$field] -or [string]::IsNullOrWhiteSpace([string]$advisory.$field)) {
                throw "Dependency health allowlist JavaScript advisory entry is missing '$field'."
            }
        }

        [void][DateTime]::Parse([string]$advisory.recheckDate, [System.Globalization.CultureInfo]::InvariantCulture)
    }

    return $entries
}

function Get-DependencyHealthJavaScriptAllowlist {
    param([Parameter(Mandatory)][string]$Path)

    # Get-DependencyHealthAllowlist returns the transitive-lag entries alone, so
    # reading acceptedJavaScriptAdvisories off its result always found nothing and
    # no advisory could ever be accepted. Validation still lives there.
    $doc = Get-JsonFile -Path $Path
    return @($doc.acceptedJavaScriptAdvisories | Where-Object { $null -ne $_ })
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

function Get-LibreSpotDotnetRuntimeStatus {
    <#
        Self-contained artifacts embed the .NET runtime, so shipped CVE fixes
        depend on the build host resolving a patched runtime pack. This reports
        the highest installed 10.x Microsoft.NETCore.App / WindowsDesktop.App
        packs and compares them to the documented CVE-patched floor (RD-32).
    #>
    param([Parameter(Mandatory)][string]$AllowlistPath)

    $doc = Get-JsonFile -Path $AllowlistPath
    $floorNode = $doc.PSObject.Properties['dotnetRuntimeFloor']
    if (-not $floorNode -or [string]::IsNullOrWhiteSpace([string]$doc.dotnetRuntimeFloor.version)) {
        throw "Dependency health allowlist is missing dotnetRuntimeFloor.version."
    }
    $floor = [version]([string]$doc.dotnetRuntimeFloor.version)

    $listed = & dotnet --list-runtimes 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet --list-runtimes failed: $($listed -join [Environment]::NewLine)"
    }

    $highest = @{}
    foreach ($line in @($listed)) {
        $match = [regex]::Match([string]$line, '^(?<name>\S+)\s+(?<ver>\d+\.\d+\.\d+)\s')
        if (-not $match.Success) { continue }
        $name = $match.Groups['name'].Value
        if ($name -ne 'Microsoft.NETCore.App' -and $name -ne 'Microsoft.WindowsDesktop.App') { continue }
        $ver = [version]$match.Groups['ver'].Value
        if ($ver.Major -ne $floor.Major) { continue }
        if (-not $highest.ContainsKey($name) -or $ver -gt $highest[$name]) {
            $highest[$name] = $ver
        }
    }

    $packs = @()
    $failures = @()
    foreach ($name in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        $resolved = if ($highest.ContainsKey($name)) { $highest[$name].ToString() } else { $null }
        $belowFloor = $true
        if ($resolved) { $belowFloor = ($highest[$name] -lt $floor) }
        $packs += [pscustomobject][ordered]@{
            pack           = $name
            resolved       = $resolved
            belowFloor     = $belowFloor
        }
        if (-not $resolved) {
            $failures += "No installed $name $($floor.Major).x runtime pack; self-contained publish cannot embed the CVE-patched floor $floor."
        } elseif ($belowFloor) {
            $failures += "$name $resolved is below the CVE-patched .NET runtime floor $floor; rebuild on a patched SDK/runtime."
        }
    }

    return [pscustomobject][ordered]@{
        floorVersion = $floor.ToString()
        floorReason  = [string]$doc.dotnetRuntimeFloor.reason
        recheckDate  = [string]$doc.dotnetRuntimeFloor.recheckDate
        sdkVersion   = (& dotnet --version 2>$null | Select-Object -First 1)
        packs        = $packs
        failures     = $failures
    }
}

function New-LibreSpotDependencyHealthReport {
    param(
        [Parameter(Mandatory)][string]$ReportPath,
        [Parameter(Mandatory)][string]$AllowlistPath,
        [string]$SpotXScriptPath
    )

    $allowlist = Get-DependencyHealthAllowlist -Path $AllowlistPath
    $dotnetRuntime = Get-LibreSpotDotnetRuntimeStatus -AllowlistPath $AllowlistPath
    $spotXSecurityPolicy = Get-PinnedSpotXSecurityPolicy -ScriptPath $SpotXScriptPath
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
    foreach ($runtimeFailure in @($dotnetRuntime.failures)) {
        $failures += $runtimeFailure
    }

    $javaScriptAudit = Get-LibreSpotJavaScriptAudit -WorkspacePath (Join-Path $PSScriptRoot 'src/LibreSpot.App')
    $allowedAdvisories = @(Get-DependencyHealthJavaScriptAllowlist -Path $AllowlistPath)
    foreach ($advisory in @($javaScriptAudit.advisories)) {
        $accepted = $allowedAdvisories | Where-Object { [string]$_.id -eq [string]$advisory.id } | Select-Object -First 1
        if ($accepted) {
            if ([DateTime]::Parse([string]$accepted.recheckDate, [System.Globalization.CultureInfo]::InvariantCulture) -lt [DateTime]::UtcNow.Date) {
                $failures += "Expired JavaScript advisory acceptance: $($advisory.id) $($advisory.moduleName) recheckDate $($accepted.recheckDate)."
            }
            continue
        }
        $failures += "JavaScript advisory: $($advisory.moduleName) $($advisory.severity) $($advisory.id) $($advisory.url) (scope $($advisory.scope))."
    }
    foreach ($auditFailure in @($javaScriptAudit.failures)) {
        $failures += $auditFailure
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
        dotnetRuntime                = $dotnetRuntime
        javaScriptAudit              = $javaScriptAudit
        spotXSecurityPolicy           = $spotXSecurityPolicy
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

$mainDefinitions = @(Get-ScriptFunctionDefinitions -ScriptContent $mainContent)
$backendDefinitions = @(Get-ScriptFunctionDefinitions -ScriptContent $backendContent)
$mainFunctions = @($mainDefinitions | ForEach-Object { $_.Name } | Sort-Object -Unique)
$backendFunctions = @($backendDefinitions | ForEach-Object { $_.Name } | Sort-Object -Unique)
$mainFunctionBodyMap = @{}
foreach ($definition in $mainDefinitions) { $mainFunctionBodyMap[$definition.Name] = $definition.Body }
$backendFunctionBodyMap = @{}
foreach ($definition in $backendDefinitions) { $backendFunctionBodyMap[$definition.Name] = $definition.Body }

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

function Get-SpotifyVersionCore {
    # Reduce a Spotify build string to its major.minor.patch core so a pinned
    # target ('1.2.93') can be compared to SpotX-Bash's fuller buildVer
    # ('1.2.93.667.g7b5cc0ce').
    param([string]$Version)
    if ([string]::IsNullOrWhiteSpace($Version)) { return '' }
    $m = [regex]::Match($Version.Trim(), '^(\d+\.\d+\.\d+)')
    if ($m.Success) { return $m.Groups[1].Value }
    return $Version.Trim()
}

function Test-SpotifyVersionDrift {
    # Report-only drift check: compares LibreSpot's pinned Spotify target (the
    # "current pinned" entry in $global:SpotifyVersionManifest) against the
    # community-canonical SpotX-Bash spotx.sh buildVer. Never auto-bumps.
    # Exit 1 only on a confirmed drift; network/parse failures are indeterminate
    # (exit 0 + warning) so the check is not flaky.
    param(
        [string]$SpotxBashUrl = 'https://raw.githubusercontent.com/SpotX-Official/SpotX-Bash/main/spotx.sh'
    )

    Write-Host "Checking pinned Spotify target against SpotX-Bash buildVer..." -ForegroundColor Cyan

    $pinnedLine = Get-Content -LiteralPath $mainScript | Where-Object {
        $_ -match 'current pinned' -and $_ -match "Version='"
    } | Select-Object -First 1
    if (-not $pinnedLine -or $pinnedLine -notmatch "Version='([^']+)'") {
        Write-Host "  Could not find the 'current pinned' Spotify entry in $mainScript." -ForegroundColor Red
        Write-Host "  (Expected a `$global:SpotifyVersionManifest row labelled 'current pinned'.)" -ForegroundColor Red
        exit 1
    }
    $pinned = $Matches[1]
    $pinnedCore = Get-SpotifyVersionCore -Version $pinned
    Write-Host "  Pinned target:  $pinned (core $pinnedCore)"

    $spotxScript = $null
    try {
        $savedPP = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
        try {
            $spotxScript = (Invoke-WebRequest -Uri $SpotxBashUrl -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop).Content
        } finally { $ProgressPreference = $savedPP }
    } catch {
        Write-Host "  Could not fetch SpotX-Bash spotx.sh: $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "  Drift is indeterminate (network unavailable); leaving the pin unchanged." -ForegroundColor Yellow
        exit 0
    }

    $buildMatch = [regex]::Match($spotxScript, 'buildVer\s*=\s*["'']?(?<v>\d+\.\d+\.\d+[^"''\s]*)')
    if (-not $buildMatch.Success) {
        Write-Host "  Fetched spotx.sh but could not locate a buildVer value." -ForegroundColor Yellow
        Write-Host "  Drift is indeterminate; leaving the pin unchanged." -ForegroundColor Yellow
        exit 0
    }
    $upstream = $buildMatch.Groups['v'].Value
    $upstreamCore = Get-SpotifyVersionCore -Version $upstream
    Write-Host "  SpotX-Bash buildVer: $upstream (core $upstreamCore)"
    Write-Host ""

    if ($pinnedCore -eq $upstreamCore) {
        Write-Host "Pinned Spotify target is current with SpotX-Bash ($upstreamCore)." -ForegroundColor Green
        exit 0
    }

    Write-Host "=== SPOTIFY TARGET DRIFT ===" -ForegroundColor Red
    Write-Host "  Pinned:   $pinnedCore" -ForegroundColor Red
    Write-Host "  Upstream: $upstreamCore (SpotX-Bash buildVer $upstream)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Update the 'current pinned' entry in `$global:SpotifyVersionManifest (both" -ForegroundColor Red
    Write-Host "LibreSpot.ps1 and LibreSpot.Backend.ps1) after confirming SpotX + Spicetify" -ForegroundColor Red
    Write-Host "support the new build. Report-only: no pin was changed." -ForegroundColor Red
    exit 1
}

if ($PublishRelease) {
    Invoke-LibreSpotReleasePublish -Root $ReleaseRoot
    exit 0
}

if ($CompileStableExe) {
    Invoke-LibreSpotStableExeCompile -OutputPath $StableExeOutputPath
    exit 0
}

if ($GenerateSbom) {
    Invoke-LibreSpotSbomGenerate -OutputPath $SbomOutputPath
    exit 0
}

if ($ReleaseTruth) {
    Test-PublicReleaseTruth
    exit 0
}

if ($CatalogTruth) {
    Test-CommunityCatalogTruth -FetchRemote
    Test-CustomizationCatalogTruth
    exit 0
}

if ($WatcherIntegration) {
    $watcherIntegrationPath = Join-Path $PSScriptRoot 'tests/powershell/Invoke-WatcherIntegration.ps1'
    if (-not (Test-Path -LiteralPath $watcherIntegrationPath -PathType Leaf)) {
        throw "Watcher integration harness not found at $watcherIntegrationPath"
    }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $watcherIntegrationPath
    exit $LASTEXITCODE
}

if ($CheckSpotifyVersionDrift) {
    Test-SpotifyVersionDrift
    exit 0
}

if ($SpotXSecurityPolicy) {
    Get-PinnedSpotXSecurityPolicy `
        -ScriptPath $SpotXScriptPath `
        -CandidateCommit $SpotXCandidateCommit `
        -CandidatePostDefenderPolicy:$SpotXCandidatePostDefenderPolicy `
        -CandidateDefenderMutations:$SpotXCandidateDefenderMutations `
        -CandidateDefenderOptOut $SpotXCandidateDefenderOptOut `
        -CandidateArguments $SpotXCandidateArguments | ConvertTo-Json -Depth 8
    exit 0
}

if ($DependencyHealth) {
    New-LibreSpotDependencyHealthReport `
        -ReportPath $DependencyHealthReportPath `
        -AllowlistPath $DependencyHealthAllowlistPath `
        -SpotXScriptPath $SpotXScriptPath
    exit 0
}

if ($GenerateReleaseManifest) {
    $null = Test-LibreSpotHostComposition -Smoke
    New-LibreSpotReleaseManifest `
        -Root $ReleaseRoot `
        -ManifestPath $ReleaseManifestPath `
        -Version $ReleaseVersion `
        -Channel $ReleaseChannel
    exit 0
}

if ($CompositionSmoke) {
    $null = Test-LibreSpotHostComposition -Smoke
    exit 0
}

if ($ComposeHosts) {
    Write-LibreSpotComposedHosts -OutputRoot $CompositionOutputRoot
    if ([string]::IsNullOrWhiteSpace($CompositionOutputRoot)) {
        $null = Test-LibreSpotHostComposition -Smoke
    } else {
        Invoke-LibreSpotCompositionSmoke -Catalog (Get-LibreSpotCompositionCatalog)
    }
    exit 0
}

if ($SyncSharedToBackend -or $SyncSharedToMain) {
    throw "The separate sync commands are retired. Run Build-Scripts.ps1 -ComposeHosts so shared functions, data blocks, and both lane sources are updated atomically."
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
    $null = Test-LibreSpotHostComposition -Smoke
    Write-Host ""
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
        $mainBody = $mainFunctionBodyMap[$fn]
        $backendBody = $backendFunctionBodyMap[$fn]

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
                $laneBody = if ($lane.Name -eq 'main') { $mainFunctionBodyMap[$fnName] } else { $backendFunctionBodyMap[$fnName] }
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
            Write-Host "Run -ComposeHosts after updating the canonical shared source." -ForegroundColor Red
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
    Test-PinnedSpotXSecurityAdapter
    Write-Host "Pinned SpotX Defender policy metadata and execution adapters are consistent." -ForegroundColor Green
    Write-Host ""

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tools/Sync-Localization.ps1') -Validate -ScanRawStrings
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Test-ReadmeWpfScreenshotMetadata
    Test-PinnedCompatibilityBaseline
    Test-CommunityAssetVerificationFreshness
    Test-LocalReleaseTruth
    # Offline-safe: compares against whatever origin/gh-pages the clone already
    # has and warns instead of failing when that ref is missing. -CatalogTruth
    # fetches first.
    Test-CommunityCatalogTruth
    Test-CustomizationCatalogTruth
    exit 0
}

if ($Lint) {
    $moduleName = 'PSScriptAnalyzer'
    $requiredPssaVersion = [Version]'1.25.0'
    $availablePssa = @(Get-Module -ListAvailable -Name $moduleName | Where-Object { $_.Version -eq $requiredPssaVersion })
    if ($availablePssa.Count -eq 0) {
        Write-Host "Installing PSScriptAnalyzer $requiredPssaVersion..." -ForegroundColor Cyan
        Install-Module -Name $moduleName -RequiredVersion $requiredPssaVersion -Force -Scope CurrentUser -SkipPublisherCheck
    }
    Import-Module $moduleName -RequiredVersion $requiredPssaVersion -Force -ErrorAction Stop
    $loadedPssaVersion = (Get-Module -Name $moduleName).Version
    if ($loadedPssaVersion -ne $requiredPssaVersion) {
        throw "PSScriptAnalyzer $requiredPssaVersion is required for the lint contract; loaded $loadedPssaVersion."
    }

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
Write-Host "  pwsh -File Build-Scripts.ps1 -ComposeHosts         # Deterministically assemble both executable hosts"
Write-Host "  pwsh -File Build-Scripts.ps1 -CompositionSmoke     # Byte-check plus PS 5.1/7.6 parse/import smoke"
Write-Host "  pwsh -File Build-Scripts.ps1 -Validate             # Check shared functions for drift"
Write-Host "  pwsh -File Build-Scripts.ps1 -Inventory             # List all functions and their locations"
Write-Host "  pwsh -File Build-Scripts.ps1 -Lint                   # Run PSScriptAnalyzer on both scripts"
Write-Host "  pwsh -File Build-Scripts.ps1 -DependencyHealth       # Emit dependency-health JSON and fail unapproved drift"
Write-Host "  pwsh -File Build-Scripts.ps1 -SpotXSecurityPolicy    # Hash and inspect the pinned SpotX entrypoint for Defender mutations"
Write-Host "  pwsh -File Build-Scripts.ps1 -CheckSpotifyVersionDrift # Compare pinned Spotify target vs SpotX-Bash buildVer (report-only)"
Write-Host "  pwsh -File Build-Scripts.ps1 -ReleaseTruth          # Compare README claims with projects, scripts, and GitHub latest stable"
Write-Host "  pwsh -File Build-Scripts.ps1 -CatalogTruth          # Check published assets plus pinned Spotify and SpotX customization data"
Write-Host "  pwsh -File Build-Scripts.ps1 -WatcherIntegration    # Exercise the watcher through a disposable Task Scheduler task"
