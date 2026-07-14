function Apply-LibreSpotProfile {
    param([string]$Id)
    $activationLock = Enter-LibreSpotProfileActivationLock
    $oldStagePath = $null
    $newStagePath = $null
    $transactionWritten = $false
    try {
        Resolve-LibreSpotProfileActivationTransaction
        $profileEntry = Get-LibreSpotProfileById -Id $Id -LockHeld
        if (-not $profileEntry) { throw "Profile '$Id' was not found." }
        $previousId = Read-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH
        if ([string]::IsNullOrWhiteSpace($previousId)) { throw 'The active profile pointer is unavailable.' }
        $priorPreviousId = Read-LibreSpotProfilePointer -Path $global:PREVIOUS_PROFILE_PATH
        $transactionId = [Guid]::NewGuid().ToString('N')
        $oldStageFile = "profile-activation.$transactionId.previous.staged.json"
        $newStageFile = "profile-activation.$transactionId.next.staged.json"
        $oldStagePath = Join-Path $global:CONFIG_DIR $oldStageFile
        $newStagePath = Join-Path $global:CONFIG_DIR $newStageFile
        $oldConfigExisted = Test-Path -LiteralPath $global:CONFIG_PATH -PathType Leaf
        if ($oldConfigExisted) {
            Copy-LibreSpotFileDurable -SourcePath $global:CONFIG_PATH -DestinationPath $oldStagePath
        } else {
            Write-LibreSpotFileDurable -Path $oldStagePath -Content ''
        }

        $normalizedConfig = Normalize-LibreSpotConfig -Config $profileEntry.Configuration
        $orderedConfig = [ordered]@{}
        foreach ($key in $normalizedConfig.Keys) { $orderedConfig[$key] = $normalizedConfig[$key] }
        Write-LibreSpotJsonAtomically -Path $newStagePath -Document $orderedConfig -Depth 4
        $transaction = [ordered]@{
            SchemaVersion        = 1
            TransactionId        = $transactionId
            OldProfileId         = [string]$previousId
            NewProfileId         = [string]$profileEntry.Id
            PreviousProfileId    = if ([string]::IsNullOrWhiteSpace($priorPreviousId)) { $null } else { [string]$priorPreviousId }
            OldConfigExisted     = [bool]$oldConfigExisted
            OldConfigFingerprint = Get-LibreSpotFileFingerprint -Path $oldStagePath
            NewConfigFingerprint = Get-LibreSpotFileFingerprint -Path $newStagePath
            OldConfigStageFile   = $oldStageFile
            NewConfigStageFile   = $newStageFile
            StartedAt            = (Get-Date).ToUniversalTime().ToString('o')
        }
        Write-LibreSpotJsonAtomically -Path $global:PROFILE_ACTIVATION_TRANSACTION_PATH -Document $transaction -Depth 8
        $transactionWritten = $true

        Write-LibreSpotProfilePointer -Path $global:PREVIOUS_PROFILE_PATH -ProfileId $previousId
        Install-LibreSpotStagedConfig -StagePath $newStagePath -DestinationPath $global:CONFIG_PATH
        Write-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH -ProfileId $profileEntry.Id
        Remove-Item -LiteralPath $global:PROFILE_ACTIVATION_TRANSACTION_PATH -Force -ErrorAction Stop
        $transactionWritten = $false
        Remove-Item -LiteralPath $oldStagePath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $newStagePath -Force -ErrorAction SilentlyContinue
        return $profileEntry
    } finally {
        if (-not $transactionWritten) {
            if ($oldStagePath) { Remove-Item -LiteralPath $oldStagePath -Force -ErrorAction SilentlyContinue }
            if ($newStagePath) { Remove-Item -LiteralPath $newStagePath -Force -ErrorAction SilentlyContinue }
        }
        $activationLock.Dispose()
    }
}
