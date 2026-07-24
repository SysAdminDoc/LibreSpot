function Get-LibreSpotCompatibilityWarnings {
    $warnings = @()
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spicetifyMax = [string]$global:PinnedReleases.SpicetifyCLI.WindowsMaxTestedSpotify
    if (-not [string]::IsNullOrWhiteSpace($spotxTarget.Id) -and
        -not [string]::IsNullOrWhiteSpace($spicetifyMax) -and
        (Compare-LibreSpotVersions -Latest $spotxTarget.Id -Current $spicetifyMax)) {
        $warnings += "SpotX target Spotify $($spotxTarget.Id) is newer than Spicetify CLI v$($global:PinnedReleases.SpicetifyCLI.Version) max-tested Windows/Microsoft Store Spotify $spicetifyMax; validate Spicetify CSS maps after patching, and if the pinned Spicetify is advanced past 2.44.0, confirm the newer build does not hard-refuse 'backup apply' on this Spotify version (spicetify/cli main gate merged after 2.44.0)."
    }
    return $warnings
}
