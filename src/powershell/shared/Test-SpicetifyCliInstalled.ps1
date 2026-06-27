function Test-SpicetifyCliInstalled {
    $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
    return (Test-Path -LiteralPath $spicetifyExe)
}
