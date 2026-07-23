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
    return $snapshot
}
