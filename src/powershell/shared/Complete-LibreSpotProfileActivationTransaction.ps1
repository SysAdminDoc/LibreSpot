function Complete-LibreSpotProfileActivationTransaction {
    param([string]$OldStagePath, [string]$NewStagePath)

    Remove-Item -LiteralPath $global:PROFILE_ACTIVATION_TRANSACTION_PATH -Force -ErrorAction Stop
    Remove-Item -LiteralPath $OldStagePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $NewStagePath -Force -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath $global:CONFIG_DIR -Filter 'profile-activation.*.staged.json' -File -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}
