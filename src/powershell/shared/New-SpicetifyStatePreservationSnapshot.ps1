function New-SpicetifyStatePreservationSnapshot {
    param([Parameter(Mandatory)][string]$Action)

    $integration = Get-SpicetifyIntegrationContext
    $operationId = if ($global:CURRENT_OPERATION_ID) { [string]$global:CURRENT_OPERATION_ID } else { [Guid]::NewGuid().ToString('N') }
    $safeAction = ($Action -replace '[^A-Za-z0-9_-]', '_')
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmssfff')
    $snapshotRoot = Join-Path $global:BACKUP_ROOT 'SpicetifyState'
    $snapshotPath = Join-Path $snapshotRoot ("$stamp-$safeAction-" + $operationId.Substring(0, [Math]::Min(8, $operationId.Length)))
    $configBackupPath = Join-Path $snapshotPath 'config-xpui.ini'
    $customAppsBackupPath = Join-Path $snapshotPath 'CustomApps'
    $manifestPath = Join-Path $snapshotPath 'preservation-manifest.json'
    $evidencePath = Join-Path $global:CONFIG_DIR 'spicetify-preservation-latest.json'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    try {
        New-Item -Path $snapshotPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
        $configBackedUp = $false
        if (Test-Path -LiteralPath $integration.ConfigPath -PathType Leaf) {
            Copy-Item -LiteralPath $integration.ConfigPath -Destination $configBackupPath -Force -ErrorAction Stop
            $configBackedUp = $true
        }

        $copyResult = Copy-DirectorySnapshotSafely -SourcePath $integration.CustomAppsDirectory -DestinationPath $customAppsBackupPath
        $health = Get-MarketplaceHealth
        $document = [ordered]@{
            schemaVersion        = 1
            action               = $Action
            operationId          = $operationId
            createdAtUtc         = (Get-Date).ToUniversalTime().ToString('o')
            status               = 'SnapshotCreated'
            snapshotPath         = $snapshotPath
            configPath           = $integration.ConfigPath
            customAppsPath       = $integration.CustomAppsDirectory
            configBackedUp       = $configBackedUp
            fileCount            = [int]$copyResult.FileCount
            bytes                = [long]$copyResult.Bytes
            skippedReparsePoints = [int]$copyResult.SkippedReparsePoints
            enabledCustomApps    = @(Get-SpicetifyConfigListValue -Key 'custom_apps')
            marketplaceStatus    = [string]$health.Status
            marketplaceReady     = [bool]$health.IsReady
        }
        $json = $document | ConvertTo-Json -Depth 6
        [System.IO.File]::WriteAllText($manifestPath, $json, $utf8)
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        [System.IO.File]::WriteAllText($evidencePath, $json, $utf8)

        Write-OperationJournalEntry -Phase 'preservation' -Target $snapshotPath -SafetyDecision 'Allowed' -Result 'Preserved' -WouldChange $true -Reversible $true -RollbackHint 'Restore the retained Spicetify state snapshot manually if refreshed package files must be rolled back.' -Data @{
            action = $Action
            fileCount = [int]$copyResult.FileCount
            bytes = [long]$copyResult.Bytes
            skippedReparsePoints = [int]$copyResult.SkippedReparsePoints
            configBackedUp = $configBackedUp
        }
        Write-Log "Preserved Spicetify config and CustomApps state at $snapshotPath" -Level 'STEP'

        foreach ($oldSnapshot in @(Get-ChildItem -LiteralPath $snapshotRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -Skip 5)) {
            $null = Remove-PathSafely -Path $oldSnapshot.FullName -Label 'expired Spicetify state snapshot'
        }
        return [pscustomobject]$document
    } catch {
        $message = $_.Exception.Message
        try {
            Write-OperationJournalEntry -Phase 'preservation' -Target $snapshotPath -SafetyDecision 'BlockedBeforeMutation' -Result 'Failed' -WouldChange $false -Reversible $false -RollbackHint 'Free space or remove unsafe reparse points, then retry before changing Marketplace or custom apps.' -Data @{ action = $Action; error = $message }
        } catch {}
        try { $null = Remove-PathSafely -Path $snapshotPath -Label 'incomplete Spicetify state snapshot' } catch {}
        throw "LibreSpot could not preserve Spicetify state before $Action. No repair changes were made. $message"
    }
}
