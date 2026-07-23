function Get-MarketplaceHealth {
    $integration = Get-SpicetifyIntegrationContext
    $configDir = $integration.MarketplaceDirectory
    $legacyDir = $integration.LegacyMarketplaceDirectory
    $activeDir = if (Test-Path -LiteralPath $configDir -PathType Container) { $configDir } elseif (Test-Path -LiteralPath $legacyDir -PathType Container) { $legacyDir } else { $configDir }
    $hasConfigDir = Test-Path -LiteralPath $configDir -PathType Container
    $hasLegacyDir = Test-Path -LiteralPath $legacyDir -PathType Container
    $hasExtension = Test-Path -LiteralPath (Join-Path $activeDir 'extension.js') -PathType Leaf
    $hasManifest = Test-Path -LiteralPath (Join-Path $activeDir 'manifest.json') -PathType Leaf
    $isEnabled = @(Get-SpicetifyConfigListValue -Key 'custom_apps') -contains 'marketplace'
    $hasFiles = $hasExtension -and $hasManifest

    # Marketplace can only install themes/snippets into an ACTIVE theme with CSS
    # injection on (the official installer activates a placeholder theme). With
    # an empty current_theme the CLI forces all injection off, so the store
    # loads but every theme/snippet install is a silent no-op.
    $configEntries = Get-SpicetifyConfigEntries
    $currentTheme = [string]$configEntries['current_theme']
    $injectCss = [string]$configEntries['inject_css']
    $themeContractReady = (-not [string]::IsNullOrWhiteSpace($currentTheme)) -and ($injectCss -eq '1')

    $status = if ($hasConfigDir -and $hasFiles -and $isEnabled -and -not $themeContractReady) {
        'ThemeInactive'
    } elseif ($hasConfigDir -and $hasFiles -and $isEnabled) {
        'Ready'
    } elseif ($hasConfigDir -and $hasFiles -and -not $isEnabled) {
        'Hidden'
    } elseif ($isEnabled -and -not $hasFiles) {
        'FilesMissing'
    } elseif ($hasLegacyDir -and -not $hasConfigDir) {
        'LegacyPath'
    } else {
        'Missing'
    }

    return [pscustomobject]@{
        Status             = $status
        Path               = $activeDir
        HasConfigDir       = $hasConfigDir
        HasLegacyDir       = $hasLegacyDir
        HasFiles           = $hasFiles
        IsEnabled          = $isEnabled
        CurrentTheme       = $currentTheme
        ThemeContractReady = $themeContractReady
        IsReady            = ($status -eq 'Ready')
        NeedsRepair        = ($status -in @('ThemeInactive','Hidden','FilesMissing','LegacyPath','Missing'))
    }
}
