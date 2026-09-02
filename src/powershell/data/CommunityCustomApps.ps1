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
        Bundled     = $true
        DisplayName = 'LibreSpot'
        Description = 'Live themes, snippets, feature flags, presets, and health checks inside Spotify.'
        Url         = 'https://raw.githubusercontent.com/SysAdminDoc/LibreSpot/main/resources/custom-apps/librespot-engine.zip'
        Source      = 'SysAdminDoc/LibreSpot'
        Version     = '4.1.1'
        ReleaseTag  = 'main'
        AssetPath   = 'librespot'
        RequiredFiles = @('manifest.json', 'index.js', 'style.css', 'librespot-engine.js', 'LICENSE', 'THIRD_PARTY_NOTICES.md')
        CompanionExtension = 'librespot-engine.js'
        SHA256      = 'bfdbac107c32b254a475f53be8a04ca3e56ecc624b37fd93742115d145a7e2eb'
    }
}
