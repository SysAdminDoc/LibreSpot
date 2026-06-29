function Get-SpicetifyConfigEntries {
    $configPath = (Get-SpicetifyIntegrationContext).ConfigPath
    $entries = @{}
    if (-not (Test-Path -LiteralPath $configPath)) { return $entries }
    try {
        foreach ($line in Get-Content -LiteralPath $configPath -ErrorAction Stop) {
            if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$') {
                $entries[$Matches[1].Trim()] = $Matches[2].Trim()
            }
        }
    } catch {
        if (Get-Command Write-Log -ErrorAction SilentlyContinue) {
            Write-Log "Could not read Spicetify config: $($_.Exception.Message)" -Level 'WARN'
        }
    }
    return $entries
}
