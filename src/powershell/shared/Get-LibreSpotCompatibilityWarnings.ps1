function Get-LibreSpotCompatibilityWarnings {
    $warnings = @()
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spicetifyMax = [string]$global:PinnedReleases.SpicetifyCLI.WindowsMaxTestedSpotify
    if (-not [string]::IsNullOrWhiteSpace($spotxTarget.Id) -and
        -not [string]::IsNullOrWhiteSpace($spicetifyMax) -and
        (Compare-LibreSpotVersions -Latest $spotxTarget.Id -Current $spicetifyMax)) {
        $warnings += "SpotX target Spotify $($spotxTarget.Id) is newer than Spicetify CLI v$($global:PinnedReleases.SpicetifyCLI.Version) max-tested Windows/Microsoft Store Spotify $spicetifyMax; Spicetify CSS maps may need validation after patching."
    }
    return $warnings
}
