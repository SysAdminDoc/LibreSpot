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
    return $snapshot
}
