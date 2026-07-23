function Module-InstallMarketplace { param($Config)
    $integration = Get-SpicetifyIntegrationContext
    # 'spicetify-marketplace' is the pre-1.0 app name; the official installer
    # removes it, so keep it managed here to clean up legacy installs.
    $managedApps = @('marketplace', 'spicetify-marketplace')
    $managedExtensions = @('librespot-marketplace-button.js')
    $marketplaceDirs = @(
        $integration.MarketplaceDirectory,
        $integration.LegacyMarketplaceDirectory
    )
    if (-not $Config.Spicetify_Marketplace) {
        Write-Log "Marketplace: disabled. Removing LibreSpot-managed Marketplace state if present..." -Level 'STEP'
        # Clear the placeholder-theme reference BEFORE deleting its directory so
        # an interrupted removal never leaves current_theme pointing at a theme
        # that no longer exists (which would fail every later spicetify apply).
        $configuredTheme = [string](Get-SpicetifyConfigEntries)['current_theme']
        if ($configuredTheme -eq 'marketplace') {
            Invoke-SpicetifyCli -Arguments @('config', 'current_theme', '', 'inject_css', '0', 'replace_colors', '0', '--bypass-admin') -FailureMessage 'Could not clear the Marketplace placeholder theme.'
            Write-Log 'Cleared the Marketplace placeholder theme from Spicetify config.'
        }
        foreach ($dir in $marketplaceDirs) {
            $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
        }
        $placeholderDir = Join-Path $integration.ThemesDirectory 'marketplace'
        $null = Remove-PathSafely -Path $placeholderDir -Label 'Marketplace placeholder theme'
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems @() -ManagedItems $managedExtensions
        $fallbackPath = Join-Path $integration.ExtensionsDirectory 'librespot-marketplace-button.js'
        if (Test-Path -LiteralPath $fallbackPath -PathType Leaf) {
            Remove-Item -LiteralPath $fallbackPath -Force -ErrorAction SilentlyContinue
            Write-Log 'Removed the Marketplace access-button fallback extension.'
        }
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
        # Official Marketplace install contract: the store can only install
        # themes and CSS snippets into an ACTIVE theme with CSS injection on.
        # Themes install before Marketplace, so an empty current_theme here
        # means no (or a failed) theme selection - point Spicetify at the
        # upstream placeholder theme, created before the config references it.
        $configuredTheme = [string](Get-SpicetifyConfigEntries)['current_theme']
        if ([string]::IsNullOrWhiteSpace($configuredTheme)) {
            $null = Install-MarketplacePlaceholderTheme
            Invoke-SpicetifyCli -Arguments @('config', 'current_theme', 'marketplace', '--bypass-admin') -FailureMessage 'Could not activate the Marketplace placeholder theme.'
            Write-Log 'Activated the Marketplace placeholder theme so store themes and snippets can render.'
        } elseif ($configuredTheme -eq 'marketplace') {
            # Re-assert the placeholder files in case a previous run was interrupted.
            $null = Install-MarketplacePlaceholderTheme
        }
        Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '1', 'replace_colors', '1', '--bypass-admin') -FailureMessage 'Could not enable CSS injection for Marketplace.'
        # Spotify's global-nav changes can silently break the injected nav link
        # (spicetify/marketplace#1133/#1185); this managed extension adds a
        # Topbar access button only when no Marketplace entry rendered.
        $fallbackName = Install-MarketplaceNavFallbackExtension
        Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems @($fallbackName) -ManagedItems $managedExtensions
        $health = Get-MarketplaceHealth
        if ($health.IsReady) {
            Write-Log "Marketplace enabled. The store appears as a Marketplace item in Spotify; if it is hidden, open spotify:app:marketplace directly."
            Write-Log "If the store page loads empty, GitHub may be rate-limiting the catalog fetch - wait about a minute and reopen Marketplace."
        } else {
            Write-Log "Marketplace files were installed but status is '$($health.Status)'. Use Maintenance > Repair and open Marketplace if the sidebar icon is hidden." -Level 'WARN'
        }
    } finally {
        Remove-Item -LiteralPath $mz -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $mu -Recurse -Force -ErrorAction SilentlyContinue
    }
}
