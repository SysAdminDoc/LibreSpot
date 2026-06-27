function Get-LibreSpotConfigSchemaVersion {
    param([hashtable]$Config)
    if (-not $Config -or -not $Config.ContainsKey('ConfigSchemaVersion')) { return 0 }
    return (ConvertTo-ConfigInt -Value $Config.ConfigSchemaVersion -Default 0 -Minimum 0 -Maximum [int]::MaxValue)
}
