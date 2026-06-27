function Load-LibreSpotConfig {
    $script:ConfigLoadWarning = $null
    if (-not (Test-Path -LiteralPath $global:CONFIG_PATH)) { return $null }
    try {
        $json = Get-Content -LiteralPath $global:CONFIG_PATH -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        $cfg = ConvertTo-PlainHashtable -InputObject $json
        return (Normalize-LibreSpotConfig -Config $cfg)
    } catch {
        Move-ConfigFileToQuarantine -Reason $_.Exception.Message
    }
    return $null
}
