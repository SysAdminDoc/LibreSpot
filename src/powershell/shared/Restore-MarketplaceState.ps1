function Restore-MarketplaceState {
    [CmdletBinding()]
    param([string]$InputPath)

    $integration = Get-SpicetifyIntegrationContext
    $exportRoot = Join-Path $global:BACKUP_ROOT 'MarketplaceState'
    $operationId = if ($global:CURRENT_OPERATION_ID) { [string]$global:CURRENT_OPERATION_ID } else { [Guid]::NewGuid().ToString('N') }
    $stagePath = Join-Path (Get-LibreSpotTempRoot) ('marketplace-state-restore-' + [Guid]::NewGuid().ToString('N'))

    if ([string]::IsNullOrWhiteSpace($InputPath)) {
        $latest = Get-ChildItem -LiteralPath $exportRoot -Filter 'marketplace-state-*.zip' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
        if (-not $latest) {
            throw "No Marketplace state export was found under $exportRoot. Run ExportMarketplaceState first."
        }
        $InputPath = $latest.FullName
    } else {
        $InputPath = [System.IO.Path]::GetFullPath($InputPath)
    }

    if (-not (Test-Path -LiteralPath $InputPath -PathType Leaf)) {
        throw "Marketplace state export was not found at $InputPath."
    }

    try {
        New-Item -Path $stagePath -ItemType Directory -Force -ErrorAction Stop | Out-Null
        Expand-ArchiveSafely -ZipPath $InputPath -DestinationPath $stagePath -Label 'Marketplace state export' -MaxEntries 10000 -MaxExpandedBytes 256MB

        $manifestPath = Join-Path $stagePath 'marketplace-state-manifest.json'
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            throw 'The Marketplace state archive has no manifest and cannot be restored.'
        }
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -ErrorAction Stop | ConvertFrom-Json
        if ([int]$manifest.schemaVersion -ne 1 -or [string]$manifest.format -ne 'LibreSpot.MarketplaceState') {
            throw 'The Marketplace state archive uses an unsupported manifest format.'
        }
        if ($manifest.browserStorage.exported -eq $true) {
            throw 'The Marketplace state archive claims to contain browser storage, which LibreSpot cannot validate or restore safely.'
        }

        $sourceConfigPath = Join-Path $stagePath 'config-xpui.ini'
        $sourceMarketplacePath = Join-Path $stagePath 'CustomApps\marketplace'
        $actualMarketplaceFiles = @()
        if (Test-Path -LiteralPath $sourceMarketplacePath -PathType Container) {
            $actualMarketplaceFiles = @(Get-ChildItem -LiteralPath $sourceMarketplacePath -File -Recurse -Force -ErrorAction Stop)
        }
        $actualMarketplaceBytes = [long]($actualMarketplaceFiles | Measure-Object -Property Length -Sum).Sum
        if ([int]$manifest.files.marketplaceFileCount -ne $actualMarketplaceFiles.Count -or
            [long]$manifest.files.marketplaceBytes -ne $actualMarketplaceBytes) {
            throw 'The Marketplace state archive manifest does not match its extracted Marketplace files.'
        }
        if ([bool]$manifest.files.configXpuiIni -and -not (Test-Path -LiteralPath $sourceConfigPath -PathType Leaf)) {
            throw 'The Marketplace state archive manifest requires config-xpui.ini, but the file is missing.'
        }
        if (-not [bool]$manifest.files.configXpuiIni -and (Test-Path -LiteralPath $sourceConfigPath -PathType Leaf)) {
            throw 'The Marketplace state archive contains config-xpui.ini but its manifest does not declare it.'
        }
        $configRestored = $false
        if ((Test-Path -LiteralPath $sourceConfigPath -PathType Leaf) -and -not (Test-Path -LiteralPath $integration.ConfigPath -PathType Leaf)) {
            $configDirectory = Split-Path -Path $integration.ConfigPath -Parent
            New-Item -Path $configDirectory -ItemType Directory -Force -ErrorAction Stop | Out-Null
            Copy-Item -LiteralPath $sourceConfigPath -Destination $integration.ConfigPath -Force -ErrorAction Stop
            $configRestored = $true
        }

        $mergeResult = Merge-DirectorySnapshotMissingFiles -SourcePath $sourceMarketplacePath -DestinationPath (Join-Path $integration.CustomAppsDirectory 'marketplace')
        $health = Get-MarketplaceHealth
        $document = [ordered]@{
            schemaVersion = 1
            format = 'LibreSpot.MarketplaceStateRecovery'
            operationId = $operationId
            completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
            status = 'RestoredMissingFiles'
            sourceArchive = $InputPath
            configRestored = $configRestored
            restoredFileCount = [int]$mergeResult.RestoredFileCount
            skippedExistingFiles = [int]$mergeResult.SkippedExistingFiles
            skippedReparsePoints = [int]$mergeResult.SkippedReparsePoints
            marketplaceStatus = [string]$health.Status
            browserStorage = [ordered]@{
                restored = $false
                status = 'not-portable'
                message = 'Marketplace browser storage was not present in the archive and was not restored.'
            }
            restoration = [ordered]@{
                mode = 'validated-file-manifest'
                behavior = 'missing-files-only'
                overwroteExistingMarketplaceFiles = $false
            }
        }
        $json = $document | ConvertTo-Json -Depth 8
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR -PathType Container)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        [System.IO.File]::WriteAllText((Join-Path $global:CONFIG_DIR 'marketplace-state-recovery-latest.json'), $json, $utf8)
        Write-OperationJournalEntry -Phase 'marketplace-state' -Target $InputPath -SafetyDecision 'Allowed' -Result 'RestoredMissingFiles' -WouldChange ($configRestored -or $mergeResult.RestoredFileCount -gt 0) -Reversible $true -RollbackHint 'The preceding preservation snapshot remains available; browser storage was not restored.' -Data @{
            sourceArchive = $InputPath
            configRestored = $configRestored
            restoredFileCount = [int]$mergeResult.RestoredFileCount
            skippedExistingFiles = [int]$mergeResult.SkippedExistingFiles
            browserStorageRestored = $false
        }
        Write-Log "Restored missing Marketplace files from $InputPath. Browser storage was not restored and may reset." -Level 'WARN'
        return [pscustomobject]@{
            Succeeded = $true
            Path = $InputPath
            ConfigRestored = $configRestored
            RestoredFileCount = [int]$mergeResult.RestoredFileCount
            SkippedExistingFiles = [int]$mergeResult.SkippedExistingFiles
            BrowserStorageRestored = $false
            BrowserStorageStatus = 'not-portable'
            Evidence = [pscustomobject]$document
        }
    } catch {
        $message = [string]$_.Exception.Message
        try {
            Write-OperationJournalEntry -Phase 'marketplace-state' -Target $InputPath -SafetyDecision 'NeedsReview' -Result 'RestoreFailed' -WouldChange $false -Reversible $true -RollbackHint 'The surrounding preservation snapshot is retained; review it before retrying.' -Data @{ sourceArchive = $InputPath; error = $message; browserStorageRestored = $false }
        } catch {}
        throw "LibreSpot could not restore Marketplace state from $InputPath. Existing files were not overwritten by the manifest restore. $message"
    } finally {
        if (Test-Path -LiteralPath $stagePath) {
            try { $null = Remove-PathSafely -Path $stagePath -Label 'temporary Marketplace state restore' } catch {}
        }
    }
}
