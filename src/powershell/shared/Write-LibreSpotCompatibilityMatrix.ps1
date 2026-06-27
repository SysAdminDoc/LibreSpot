function Write-LibreSpotCompatibilityMatrix {
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spotxLabel = if ([string]::IsNullOrWhiteSpace($spotxTarget.Version)) {
        $spotxTarget.Id
    } else {
        "$($spotxTarget.Id) ($($spotxTarget.Version))"
    }
    $spicetify = $global:PinnedReleases.SpicetifyCLI

    Write-Log '  Compatibility matrix:'
    Write-Log "    SpotX: commit $($global:PinnedReleases.SpotX.Commit.Substring(0,10)) targets Spotify $spotxLabel"
    Write-Log "    Spicetify CLI: v$($spicetify.Version) max-tested Windows/Microsoft Store Spotify $($spicetify.WindowsMinSpotify) -> $($spicetify.WindowsMaxTestedSpotify)"
    Write-Log "    Marketplace: v$($global:PinnedReleases.Marketplace.Version) checked as a custom app package independent of Spotify CSS-map coverage"
    Write-Log "    Themes: commit $($global:PinnedReleases.Themes.Commit.Substring(0,10)) checked as a theme archive independent of Spotify CSS-map coverage"

    $warnings = @(Get-LibreSpotCompatibilityWarnings)
    foreach ($warning in $warnings) {
        Write-Log "    Compatibility warning: $warning" -Level 'WARN'
    }
    return $warnings
}
