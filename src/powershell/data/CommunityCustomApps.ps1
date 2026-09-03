$global:CommunityCustomApps = [ordered]@{
    'stats' = @{
        DisplayName = 'Stats'
        Description = 'Detailed listening statistics with top tracks, artists, genres, library charts, and optional Last.fm-backed views.'
        Url         = 'https://github.com/harbassan/spicetify-apps/releases/download/stats-v1.1.3/spicetify-stats.release.zip'
        Source      = 'harbassan/spicetify-apps'
        Version     = '1.1.3'
        ReleaseTag  = 'stats-v1.1.3'
        AssetPath   = 'stats'
        RequiredFiles = @('manifest.json', 'extension.js', 'index.js')
        SHA256      = 'c5611ff8caafe9c673ed43de07fbae77296d42fbd14fab868e9cbeac5d2b6cb7'
    }
    'librespot' = @{
        # Bundled apps ship beside the host that runs this script, so the install
        # never needs the network. BundledFileName is looked for in
        # $env:LIBRESPOT_BUNDLED_ASSETS (set by the desktop and CLI hosts) and beside
        # the script itself. The Url is the immutable release asset for this exact
        # version and is only used when no local copy matches the pinned hash; a
        # branch URL would break every already-published release the moment the
        # archive is rebuilt.
        Bundled     = $true
        BundledFileName = 'librespot-engine.zip'
        DisplayName = 'LibreSpot'
        Description = 'Live themes, snippets, feature flags, presets, and health checks inside Spotify.'
        Url         = 'https://github.com/SysAdminDoc/LibreSpot/releases/download/v4.2.0/librespot-engine.zip'
        Source      = 'SysAdminDoc/LibreSpot'
        Version     = '4.2.0'
        ReleaseTag  = 'v4.2.0'
        AssetPath   = 'librespot'
        RequiredFiles = @('manifest.json', 'index.js', 'style.css', 'librespot-engine.js', 'LICENSE', 'THIRD_PARTY_NOTICES.md')
        CompanionExtension = 'librespot-engine.js'
        SHA256      = 'e280fbdf04193b8f98d2b23a3de37e6592af1bc6f5df206d1cd30133201d6c35'
    }
}
