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
            'theme.js'  = '9355ada53465cbcaed8894a35bef95db19b759330527e8d909ea7394ce154293'
            'user.css'  = '47c71a0b49401077938e86272579d90ab200a7a15b6991939c5ee8ed43f263e5'
        }
    }
}
