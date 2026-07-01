function Module-InstallMarketplace { param($Config)
    $integration = Get-SpicetifyIntegrationContext
    $managedApps = @('marketplace')
    $marketplaceDirs = @(
        $integration.MarketplaceDirectory,
        $integration.LegacyMarketplaceDirectory
    )
    if (-not $Config.Spicetify_Marketplace) {
        Write-Log "Marketplace: disabled. Removing LibreSpot-managed Marketplace state if present..." -Level 'STEP'
        foreach ($dir in $marketplaceDirs) {
            $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        return
    }

    Write-Log "Installing Marketplace..." -Level 'STEP'
    $ca = $integration.CustomAppsDirectory
    New-Item -Path $ca -ItemType Directory -Force | Out-Null
    $md=Join-Path $ca "marketplace"
    $mz = New-LibreSpotTempFile -Name 'marketplace.zip'
    $mu = New-LibreSpotTempDirectory -Name 'marketplace-unpack'
    foreach ($dir in $marketplaceDirs) {
        $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
    }
    New-Item -Path $md -ItemType Directory -Force | Out-Null
    try {
        $marketplaceHash = $global:PinnedReleases.Marketplace.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $marketplaceHash -DestinationPath $mz -Label 'Marketplace archive')) {
            try {
                Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $mz
            } catch {
                if (Get-FromAssetCache -SHA256Hash $marketplaceHash -DestinationPath $mz -Label 'Marketplace archive') {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $mz -ExpectedHash $marketplaceHash -Label "Marketplace"
            Save-ToAssetCache -SourcePath $mz -SHA256Hash $marketplaceHash -Label 'Marketplace archive' -SourceUrl $global:URL_MARKETPLACE
        }
        Expand-ArchiveSafely -ZipPath $mz -DestinationPath $mu -Label 'Marketplace'
        $sp = if (Test-Path (Join-Path $mu "marketplace-dist")) { Join-Path $mu "marketplace-dist\*" } else { Join-Path $mu "*" }
        Copy-Item -Path $sp -Destination $md -Recurse -Force
        $health = Get-MarketplaceHealth
        if (-not $health.HasFiles) {
            throw 'Marketplace archive did not produce expected Spicetify custom app files.'
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @('marketplace') -ManagedItems $managedApps
        $health = Get-MarketplaceHealth
        if ($health.IsReady) {
            Write-Log "Marketplace enabled. If Spotify hides the sidebar icon, open spotify:app:marketplace directly."
        } else {
            Write-Log "Marketplace files were installed but status is '$($health.Status)'. Use Maintenance > Repair and open Marketplace if the sidebar icon is hidden." -Level 'WARN'
        }
    } finally {
        Remove-Item -LiteralPath $mz -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $mu -Recurse -Force -ErrorAction SilentlyContinue
    }
}
