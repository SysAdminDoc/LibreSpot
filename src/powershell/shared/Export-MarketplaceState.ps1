function Export-MarketplaceState {
    [CmdletBinding()]
    param([string]$OutputPath)

    $integration = Get-SpicetifyIntegrationContext
    $health = Get-MarketplaceHealth
    $operationId = if ($global:CURRENT_OPERATION_ID) { [string]$global:CURRENT_OPERATION_ID } else { [Guid]::NewGuid().ToString('N') }
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmssfff')
    $exportRoot = Join-Path $global:BACKUP_ROOT 'MarketplaceState'
    $createdOutput = $false
    $stagePath = $null

    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $OutputPath = Join-Path $exportRoot ("marketplace-state-$stamp-" + $operationId.Substring(0, [Math]::Min(8, $operationId.Length)) + '.zip')
    } else {
        $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    }

    try {
        if (Test-Path -LiteralPath $OutputPath) {
            throw "Marketplace state export already exists at $OutputPath. Choose a new destination instead of overwriting an archive."
        }

        $outputDirectory = Split-Path -Path $OutputPath -Parent
        if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
            throw 'Marketplace state export destination must include a directory.'
        }
        New-Item -Path $outputDirectory -ItemType Directory -Force -ErrorAction Stop | Out-Null

        $stagePath = Join-Path (Get-LibreSpotTempRoot) ('marketplace-state-export-' + [Guid]::NewGuid().ToString('N'))
        New-Item -Path $stagePath -ItemType Directory -Force -ErrorAction Stop | Out-Null
        $marketplaceStagePath = Join-Path $stagePath 'CustomApps\marketplace'
        $configStagePath = Join-Path $stagePath 'config-xpui.ini'
        $manifestPath = Join-Path $stagePath 'marketplace-state-manifest.json'

        $marketplaceSource = [string]$health.Path
        if ([string]::IsNullOrWhiteSpace($marketplaceSource)) {
            $marketplaceSource = [string]$integration.MarketplaceDirectory
            if (-not (Test-Path -LiteralPath $marketplaceSource -PathType Container)) {
                $marketplaceSource = [string]$integration.LegacyMarketplaceDirectory
            }
        }

        $configIncluded = $false
        $configBytes = [long]0
        if (Test-Path -LiteralPath $integration.ConfigPath -PathType Leaf) {
            Copy-Item -LiteralPath $integration.ConfigPath -Destination $configStagePath -Force -ErrorAction Stop
            $configIncluded = $true
            $configBytes = [long](Get-Item -LiteralPath $configStagePath -Force -ErrorAction Stop).Length
        }

        $copyResult = Copy-DirectorySnapshotSafely -SourcePath $marketplaceSource -DestinationPath $marketplaceStagePath
        $manifest = [ordered]@{
            schemaVersion = 1
            format = 'LibreSpot.MarketplaceState'
            createdAtUtc = (Get-Date).ToUniversalTime().ToString('o')
            operationId = $operationId
            source = [ordered]@{
                configPath = [string]$integration.ConfigPath
                marketplacePath = $marketplaceSource
                marketplaceStatus = [string]$health.Status
                enabledCustomApps = @(Get-SpicetifyConfigListValue -Key 'custom_apps')
            }
            files = [ordered]@{
                configXpuiIni = $configIncluded
                configXpuiIniBytes = $configBytes
                marketplaceFileCount = [int]$copyResult.FileCount
                marketplaceBytes = [long]$copyResult.Bytes
                skippedReparsePoints = [int]$copyResult.SkippedReparsePoints
            }
            archiveEntries = @(
                'marketplace-state-manifest.json',
                $(if ($configIncluded) { 'config-xpui.ini' }),
                $(if ([int]$copyResult.FileCount -gt 0) { 'CustomApps/marketplace/**' })
            ) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }
            browserStorage = [ordered]@{
                exported = $false
                status = 'not-portable'
                reason = 'Marketplace browser storage belongs to Spotify embedded browser state and is not included in this file archive.'
                recovery = 'Use Marketplace built-in export/import when available; LibreSpot never claims this archive restores browser storage.'
            }
            restoration = [ordered]@{
                mode = 'validated-file-manifest'
                behavior = 'missing-files-only'
                requiresReapply = $true
                overwritesExistingMarketplaceFiles = $false
                browserStorageRestored = $false
            }
        }
        $json = $manifest | ConvertTo-Json -Depth 8
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($manifestPath, $json, $utf8)

        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $stagePath,
            $OutputPath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false)
        $createdOutput = $true

        $zip = $null
        try {
            $zip = [System.IO.Compression.ZipFile]::OpenRead($OutputPath)
            if ($null -eq $zip.GetEntry('marketplace-state-manifest.json')) {
                throw 'The generated Marketplace state archive did not contain its manifest.'
            }
        } finally {
            if ($zip) { $zip.Dispose() }
        }

        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR -PathType Container)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        [System.IO.File]::WriteAllText((Join-Path $global:CONFIG_DIR 'marketplace-state-export-latest.json'), $json, $utf8)
        $archiveBytes = [long](Get-Item -LiteralPath $OutputPath -Force -ErrorAction Stop).Length
        Write-OperationJournalEntry -Phase 'marketplace-state' -Target $OutputPath -SafetyDecision 'Allowed' -Result 'Exported' -WouldChange $true -Reversible $true -RollbackHint 'Use RestoreMarketplaceState or the retained archive to restore missing Marketplace files. Browser storage is not included.' -Data @{
            archivePath = $OutputPath
            configIncluded = $configIncluded
            marketplaceFileCount = [int]$copyResult.FileCount
            marketplaceBytes = [long]$copyResult.Bytes
            browserStorageExported = $false
        }
        Write-Log "Marketplace state export created at $OutputPath. Browser storage was not exported and may reset." -Level 'WARN'

        foreach ($oldExport in @(Get-ChildItem -LiteralPath $exportRoot -Filter 'marketplace-state-*.zip' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -Skip 5)) {
            $null = Remove-PathSafely -Path $oldExport.FullName -Label 'expired Marketplace state export'
        }

        return [pscustomobject]@{
            Succeeded = $true
            Path = $OutputPath
            ArchiveBytes = $archiveBytes
            ConfigIncluded = $configIncluded
            MarketplaceFileCount = [int]$copyResult.FileCount
            MarketplaceBytes = [long]$copyResult.Bytes
            BrowserStorageExported = $false
            BrowserStorageStatus = 'not-portable'
            Manifest = [pscustomobject]$manifest
        }
    } catch {
        $message = [string]$_.Exception.Message
        if ($createdOutput -and (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
            Remove-Item -LiteralPath $OutputPath -Force -ErrorAction SilentlyContinue
        }
        try {
            Write-OperationJournalEntry -Phase 'marketplace-state' -Target $OutputPath -SafetyDecision 'BlockedBeforeMutation' -Result 'ExportFailed' -WouldChange $false -Reversible $false -RollbackHint 'No Marketplace repair changes were made; fix the export destination or storage issue and retry.' -Data @{ error = $message }
        } catch {}
        throw "LibreSpot could not export Marketplace state. No Marketplace repair changes were made. $message"
    } finally {
        if ($stagePath -and (Test-Path -LiteralPath $stagePath)) {
            try { $null = Remove-PathSafely -Path $stagePath -Label 'temporary Marketplace state export' } catch {}
        }
    }
}
