function Restore-SpicetifyStatePreservationSnapshot {
    param(
        [Parameter(Mandatory)]$Snapshot,
        [bool]$OperationSucceeded
    )

    $integration = Get-SpicetifyIntegrationContext
    $snapshotPath = [string]$Snapshot.snapshotPath
    $configBackupPath = Join-Path $snapshotPath 'config-xpui.ini'
    $customAppsBackupPath = Join-Path $snapshotPath 'CustomApps'
    $manifestPath = Join-Path $snapshotPath 'preservation-manifest.json'
    $evidencePath = Join-Path $global:CONFIG_DIR 'spicetify-preservation-latest.json'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    try {
        $configRestored = $false
        if ((Test-Path -LiteralPath $configBackupPath -PathType Leaf) -and -not (Test-Path -LiteralPath $integration.ConfigPath -PathType Leaf)) {
            $configDirectory = Split-Path -Path $integration.ConfigPath -Parent
            New-Item -Path $configDirectory -ItemType Directory -Force -ErrorAction Stop | Out-Null
            Copy-Item -LiteralPath $configBackupPath -Destination $integration.ConfigPath -ErrorAction Stop
            $configRestored = $true
        }

        $mergeResult = Merge-DirectorySnapshotMissingFiles -SourcePath $customAppsBackupPath -DestinationPath $integration.CustomAppsDirectory
        $status = if ($OperationSucceeded) { 'PreservedAfterSuccess' } else { 'RecoveredAfterFailure' }
        $document = [ordered]@{
            schemaVersion         = 1
            action                = [string]$Snapshot.action
            operationId           = [string]$Snapshot.operationId
            createdAtUtc          = [string]$Snapshot.createdAtUtc
            completedAtUtc        = (Get-Date).ToUniversalTime().ToString('o')
            status                = $status
            operationSucceeded    = $OperationSucceeded
            recoverySucceeded     = $true
            snapshotPath          = $snapshotPath
            backupRetained        = $true
            configRestored        = $configRestored
            restoredFileCount     = [int]$mergeResult.RestoredFileCount
            skippedExistingFiles  = [int]$mergeResult.SkippedExistingFiles
            skippedReparsePoints  = [int]$mergeResult.SkippedReparsePoints
            preservationFileCount = [int]$Snapshot.fileCount
            preservationBytes     = [long]$Snapshot.bytes
        }
        $json = $document | ConvertTo-Json -Depth 6
        [System.IO.File]::WriteAllText($manifestPath, $json, $utf8)
        [System.IO.File]::WriteAllText($evidencePath, $json, $utf8)
        Write-OperationJournalEntry -Phase 'preservation' -Target $snapshotPath -SafetyDecision 'Allowed' -Result $status -WouldChange ($configRestored -or $mergeResult.RestoredFileCount -gt 0) -Reversible $true -RollbackHint 'The retained snapshot can be used for manual rollback; refreshed package files were not overwritten.' -Data @{
            action = [string]$Snapshot.action
            operationSucceeded = $OperationSucceeded
            configRestored = $configRestored
            restoredFileCount = [int]$mergeResult.RestoredFileCount
            skippedExistingFiles = [int]$mergeResult.SkippedExistingFiles
            skippedReparsePoints = [int]$mergeResult.SkippedReparsePoints
        }
        Write-Log "Spicetify preservation completed; backup retained at $snapshotPath" -Level 'SUCCESS'
        return [pscustomobject]@{ Succeeded = $true; Message = ''; Evidence = [pscustomobject]$document }
    } catch {
        $message = $_.Exception.Message
        try {
            $failure = [ordered]@{
                schemaVersion = 1
                action = [string]$Snapshot.action
                operationId = [string]$Snapshot.operationId
                completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
                status = 'RecoveryFailed'
                operationSucceeded = $OperationSucceeded
                recoverySucceeded = $false
                snapshotPath = $snapshotPath
                backupRetained = $true
                error = $message
            }
            [System.IO.File]::WriteAllText($evidencePath, ($failure | ConvertTo-Json -Depth 5), $utf8)
            Write-OperationJournalEntry -Phase 'preservation' -Target $snapshotPath -SafetyDecision 'NeedsReview' -Result 'RecoveryFailed' -WouldChange $false -Reversible $true -RollbackHint 'The snapshot is retained; restore it manually before retrying.' -Data @{ action = [string]$Snapshot.action; error = $message }
        } catch {}
        return [pscustomobject]@{ Succeeded = $false; Message = $message; Evidence = $null }
    }
}
