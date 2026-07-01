function Repair-Marketplace {
    param($Config)
    if (-not (Test-SpicetifyCliInstalled)) {
        throw 'Spicetify CLI is not installed, so LibreSpot cannot repair Marketplace yet. Run Recommended setup or Reapply first.'
    }
    if (-not $Config) {
        $Config = Normalize-LibreSpotConfig -Config @{}
    }
    $Config.Spicetify_Marketplace = $true

    Write-Log 'Repairing Marketplace files and custom_apps registration...' -Level 'STEP'
    Module-InstallMarketplace -Config $Config
    Write-Log 'Applying Spicetify so Marketplace is discoverable in Spotify...' -Level 'STEP'
    $applyResult = Module-ApplySpicetify -Config $Config -EvidenceSource 'RepairMarketplace'

    $health = Get-MarketplaceHealth
    if ($health.IsReady) {
        Write-Log "Marketplace repair verified at $($health.Path)." -Level 'SUCCESS'
    } else {
        Write-Log "Marketplace repair finished, but status is '$($health.Status)'. Open spotify:app:marketplace directly if the sidebar icon remains hidden." -Level 'WARN'
    }
    $openResult = Open-SpicetifyMarketplace
    Write-MarketplaceVisibilityEvidence -Source 'RepairMarketplace' -ApplyStage $applyResult.Stage -ApplySucceeded $applyResult.Succeeded -ApplyMessage $applyResult.Message -OpenUriSucceeded $openResult.Succeeded -OpenUriMessage $openResult.Message -OpenUriRequestedAtUtc $openResult.RequestedAtUtc -SpotifyRunningAfterOpen $openResult.SpotifyRunningAfterOpen | Out-Null
}
