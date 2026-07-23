function Test-SpicetifyCliVersionSupported {
    # LibreSpot targets Spicetify 2.x. A future Spicetify v3 (spicetify/cli#3038)
    # changes the on-disk contract (symlink + hooks + modules) that LibreSpot's
    # patch detection does not understand. Unknown/unparseable versions are
    # treated as supported so a missing probe never raises a false warning; only
    # a parsed major greater than 2 is unsupported.
    param([string]$Version)
    $major = Get-SpicetifyCliMajorVersion -Version $Version
    if ($null -eq $major) { return $true }
    return ($major -le 2)
}
