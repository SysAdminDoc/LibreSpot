function Get-SpicetifyDiagnosticSnapshot {
    $snapshot = [ordered]@{}
    $configPath = (Get-SpicetifyIntegrationContext).ConfigPath
    if (Test-Path -LiteralPath $configPath) {
        try {
            foreach ($line in Get-Content -LiteralPath $configPath -ErrorAction Stop) {
                if ($line -match '^\s*(spotify_path|prefs_path)\s*=\s*(.+?)\s*$') {
                    $snapshot[$Matches[1]] = $Matches[2].Trim()
                }
            }
        } catch {}
    }
    $snapshot['xpui_spa_exists'] = Test-Path -LiteralPath (Join-Path (Split-Path $global:SPOTIFY_EXE_PATH -Parent) 'Apps\xpui.spa')
    $snapshot['spotify_exe_exists'] = Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH
    # A future Spicetify v3 changes the on-disk contract (spicetify/cli#3038); flag
    # an unsupported CLI major so diagnostics do not read as a broken 2.x patch.
    $cliVersion = Get-InstalledSpicetifyCliVersion
    $snapshot['spicetify_cli_version'] = $cliVersion
    $snapshot['spicetify_cli_supported'] = Test-SpicetifyCliVersionSupported -Version $cliVersion
    $v3Conflict = Get-SpicetifyV3Conflict -CliVersion $cliVersion
    $snapshot['spicetify_v3_conflict'] = [bool]$v3Conflict.IsConflict
    $snapshot['spicetify_v3_markers'] = @($v3Conflict.Markers)
    $snapshot['spicetify_v3_safe_action'] = $v3Conflict.SafeAction
    $marketplaceHealth = Get-MarketplaceHealth
    $snapshot['marketplace_storage_model'] = [string]$marketplaceHealth.BrowserStorage.storageModel
    $snapshot['marketplace_storage_database'] = [string]$marketplaceHealth.BrowserStorage.databaseName
    $snapshot['marketplace_storage_object_store'] = [string]$marketplaceHealth.BrowserStorage.objectStore
    $snapshot['marketplace_storage_status'] = [string]$marketplaceHealth.BrowserStorage.status
    $snapshot['marketplace_storage_detection_only'] = [bool]$marketplaceHealth.BrowserStorage.detectionOnly
    $snapshot['marketplace_storage_recovery'] = [string]$marketplaceHealth.BrowserStorage.recovery
    $integration = Get-SpicetifyIntegrationContext
    $supportPaths = @(
        (Join-Path (Split-Path -Parent $integration.CliPath) 'supported-versions.json'),
        (Join-Path $integration.ConfigDirectory 'supported-versions.json'))
    $support = Get-SpicetifyV3SupportContract `
        -CliVersion $cliVersion `
        -SpotifyVersion (Get-InstalledSpotifyVersion) `
        -SupportListPath $supportPaths
    if ($support.FeatureActive) {
        $snapshot['spicetify_v3_support_verdict'] = $support.Verdict
        $snapshot['spicetify_v3_support_can_apply'] = [bool]$support.CanApply
        $snapshot['spicetify_v3_support_can_auto_apply'] = [bool]$support.CanAutoApply
        $snapshot['spicetify_v3_support_list_available'] = [bool]$support.ListAvailable
        $snapshot['spicetify_v3_support_version'] = $support.NormalizedVersion
        $snapshot['spicetify_v3_support_fallback'] = $support.FallbackVersion
        $snapshot['spicetify_v3_support_path'] = $support.SupportListPath
        $snapshot['spicetify_v3_support_reason'] = $support.Reason
    }
    return $snapshot
}
