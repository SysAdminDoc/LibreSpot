function Get-MarketplaceHealth {
    $configDir = Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps\marketplace'
    $legacyDir = Join-Path $global:SPICETIFY_DIR 'CustomApps\marketplace'
    $activeDir = if (Test-Path -LiteralPath $configDir -PathType Container) { $configDir } elseif (Test-Path -LiteralPath $legacyDir -PathType Container) { $legacyDir } else { $configDir }
    $hasConfigDir = Test-Path -LiteralPath $configDir -PathType Container
    $hasLegacyDir = Test-Path -LiteralPath $legacyDir -PathType Container
    $hasExtension = Test-Path -LiteralPath (Join-Path $activeDir 'extension.js') -PathType Leaf
    $hasManifest = Test-Path -LiteralPath (Join-Path $activeDir 'manifest.json') -PathType Leaf
    $isEnabled = @(Get-SpicetifyConfigListValue -Key 'custom_apps') -contains 'marketplace'
    $hasFiles = $hasExtension -and $hasManifest

    $status = if ($hasConfigDir -and $hasFiles -and $isEnabled) {
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
        Status       = $status
        Path         = $activeDir
        HasConfigDir = $hasConfigDir
        HasLegacyDir = $hasLegacyDir
        HasFiles     = $hasFiles
        IsEnabled    = $isEnabled
        IsReady      = ($status -eq 'Ready')
        NeedsRepair  = ($status -in @('Hidden','FilesMissing','LegacyPath','Missing'))
    }
}
