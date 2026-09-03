function Get-LibreSpotCompatibilityWarnings {
    $warnings = @()
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spicetifyDeclaredMax = [string]$global:PinnedReleases.SpicetifyCLI.WindowsDeclaredMaxSpotify
    $verifiedMax = [string]$global:PinnedReleases.SpicetifyCLI.LibreSpotVerifiedMaxSpotify
    if (-not [string]::IsNullOrWhiteSpace($spotxTarget.Id) -and
        -not [string]::IsNullOrWhiteSpace($spicetifyDeclaredMax) -and
        (Compare-LibreSpotVersions -Latest $spotxTarget.Id -Current $spicetifyDeclaredMax)) {
        $warnings += "SpotX target Spotify $($spotxTarget.Id) is newer than Spicetify CLI v$($global:PinnedReleases.SpicetifyCLI.Version) declares for Windows/Microsoft Store ($spicetifyDeclaredMax); validate Spicetify CSS maps after patching, and if the pinned Spicetify is advanced past 2.44.0, confirm the newer build does not hard-refuse 'backup apply' on this Spotify version (spicetify/cli main gate merged after 2.44.0)."
    } elseif (-not [string]::IsNullOrWhiteSpace($spotxTarget.Id) -and
        -not [string]::IsNullOrWhiteSpace($verifiedMax) -and
        (Compare-LibreSpotVersions -Latest $spotxTarget.Id -Current $verifiedMax)) {
        $warnings += "SpotX target Spotify $($spotxTarget.Id) is newer than the build LibreSpot has verified ($verifiedMax), though Spicetify CLI v$($global:PinnedReleases.SpicetifyCLI.Version) declares Windows/Microsoft Store support through $spicetifyDeclaredMax; this ceiling is LibreSpot's own, so apply as usual and confirm themes and extensions loaded."
    }
    return $warnings
}
