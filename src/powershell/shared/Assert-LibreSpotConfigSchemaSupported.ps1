function Assert-LibreSpotConfigSchemaSupported {
    param([hashtable]$Config)
    $schemaVersion = Get-LibreSpotConfigSchemaVersion -Config $Config
    if ($schemaVersion -gt $global:CONFIG_SCHEMA_VERSION) {
        throw "Saved config schema version $schemaVersion is newer than this LibreSpot build supports ($global:CONFIG_SCHEMA_VERSION)."
    }
    return $schemaVersion
}
