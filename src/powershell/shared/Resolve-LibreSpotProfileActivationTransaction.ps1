function Resolve-LibreSpotProfileActivationTransaction {
    if (-not (Test-Path -LiteralPath $global:PROFILE_ACTIVATION_TRANSACTION_PATH -PathType Leaf)) {
        Get-ChildItem -LiteralPath $global:CONFIG_DIR -Filter 'profile-activation.*.staged.json' -File -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
        return
    }

    try {
        $transaction = Get-Content -LiteralPath $global:PROFILE_ACTIVATION_TRANSACTION_PATH -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw 'The pending profile activation record is unreadable. Move profile-activation.pending.json aside before retrying.'
    }
    if (-not (Test-LibreSpotProfileActivationTransaction -Transaction $transaction)) {
        throw 'The pending profile activation record contains invalid fields. Move profile-activation.pending.json aside before retrying.'
    }

    $oldStagePath = Join-Path $global:CONFIG_DIR ([string]$transaction.OldConfigStageFile)
    $newStagePath = Join-Path $global:CONFIG_DIR ([string]$transaction.NewConfigStageFile)
    $activeId = Read-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH
    $currentFingerprint = Get-LibreSpotFileFingerprint -Path $global:CONFIG_PATH -MissingAsEmpty
    $currentIsNew = $currentFingerprint -eq [string]$transaction.NewConfigFingerprint

    if ([string]$activeId -eq [string]$transaction.NewProfileId -and $currentIsNew) {
        Write-LibreSpotProfilePointer -Path $global:PREVIOUS_PROFILE_PATH -ProfileId ([string]$transaction.OldProfileId)
        Complete-LibreSpotProfileActivationTransaction -OldStagePath $oldStagePath -NewStagePath $newStagePath
        return
    }

    $configExists = Test-Path -LiteralPath $global:CONFIG_PATH -PathType Leaf
    $currentIsOld = $currentFingerprint -eq [string]$transaction.OldConfigFingerprint -and
        ([bool]$transaction.OldConfigExisted -or -not $configExists)
    $oldStageIsValid = (Test-Path -LiteralPath $oldStagePath -PathType Leaf) -and
        (Get-LibreSpotFileFingerprint -Path $oldStagePath) -eq [string]$transaction.OldConfigFingerprint

    if ($currentIsOld -or $oldStageIsValid) {
        if (-not $currentIsOld) {
            if ([bool]$transaction.OldConfigExisted) {
                Install-LibreSpotStagedConfig -StagePath $oldStagePath -DestinationPath $global:CONFIG_PATH
            } else {
                Remove-Item -LiteralPath $global:CONFIG_PATH -Force -ErrorAction SilentlyContinue
            }
        }
        Write-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH -ProfileId ([string]$transaction.OldProfileId)
        if ([string]::IsNullOrWhiteSpace([string]$transaction.PreviousProfileId)) {
            Remove-Item -LiteralPath $global:PREVIOUS_PROFILE_PATH -Force -ErrorAction SilentlyContinue
        } else {
            Write-LibreSpotProfilePointer -Path $global:PREVIOUS_PROFILE_PATH -ProfileId ([string]$transaction.PreviousProfileId)
        }
        Complete-LibreSpotProfileActivationTransaction -OldStagePath $oldStagePath -NewStagePath $newStagePath
        return
    }

    $newStageIsValid = (Test-Path -LiteralPath $newStagePath -PathType Leaf) -and
        (Get-LibreSpotFileFingerprint -Path $newStagePath) -eq [string]$transaction.NewConfigFingerprint
    if (-not $currentIsNew -and $newStageIsValid) {
        Install-LibreSpotStagedConfig -StagePath $newStagePath -DestinationPath $global:CONFIG_PATH
        $currentIsNew = $true
    }
    if ($currentIsNew) {
        Write-LibreSpotProfilePointer -Path $global:PREVIOUS_PROFILE_PATH -ProfileId ([string]$transaction.OldProfileId)
        Write-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH -ProfileId ([string]$transaction.NewProfileId)
        Complete-LibreSpotProfileActivationTransaction -OldStagePath $oldStagePath -NewStagePath $newStagePath
        return
    }

    throw 'The pending profile activation cannot be recovered because neither staged configuration matches its recorded fingerprint.'
}
