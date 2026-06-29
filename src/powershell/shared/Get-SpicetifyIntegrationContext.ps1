function Get-SpicetifyIntegrationContext {
    $version = if ($global:SPICETIFY_INTEGRATION_VERSION) { [string]$global:SPICETIFY_INTEGRATION_VERSION } else { 'v2' }
    if ($version -notin @('v2','v3-preview')) {
        throw "Unsupported Spicetify integration version '$version'."
    }

    $installDir = [string]$global:SPICETIFY_DIR
    $configDir = [string]$global:SPICETIFY_CONFIG_DIR
    return [pscustomobject]@{
        Version                    = $version
        InstallDirectory           = $installDir
        ConfigDirectory            = $configDir
        CliPath                    = Join-Path $installDir 'spicetify.exe'
        ConfigPath                 = Join-Path $configDir 'config-xpui.ini'
        ThemesDirectory            = Join-Path $configDir 'Themes'
        ExtensionsDirectory        = Join-Path $configDir 'Extensions'
        CustomAppsDirectory        = Join-Path $configDir 'CustomApps'
        MarketplaceDirectory       = Join-Path $configDir 'CustomApps\marketplace'
        LegacyMarketplaceDirectory = Join-Path $installDir 'CustomApps\marketplace'
    }
}
