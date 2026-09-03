$global:BundledThemes = [ordered]@{
    # Themes LibreSpot writes itself. They ship inside the package instead of
    # being downloaded, so the install works with no network and there is no
    # release asset to drift away from an already-published pin. Folder is the
    # directory name under the bundled asset root; Files pins every file that
    # gets copied, so a truncated or edited copy is rejected instead of
    # producing a half-installed theme. Module-InstallThemes looks for
    # <root>\themes\<Folder> under $env:LIBRESPOT_BUNDLED_ASSETS (set by the
    # desktop and CLI hosts), beside the script, and in a source checkout.
    'Prism' = @{
        Folder      = 'Prism'
        DisplayName = 'Prism'
        Description = 'The LibreSpot house theme. Scheduled light and dark, an accent taken from the album art, and effects that step down on slow machines.'
        Files       = [ordered]@{
            'color.ini' = 'bacd6b54c170600488b79f310dd4f41a349db81c3cfdccd43c38be2d898b17bc'
            'theme.js'  = '1e2e2c84c402db0e5cbbedefd98ca47e6d96bd6512bd8e079fc022fbed507870'
            'user.css'  = '05dce4408a12742388d9a20c3d8c1b7b36629c584a9e96774d3f6ded16700025'
        }
    }
}
