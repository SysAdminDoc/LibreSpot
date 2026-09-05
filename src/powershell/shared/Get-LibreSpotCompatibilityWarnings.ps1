function Get-LibreSpotCompatibilityWarnings {
    # InstalledSpotifyVersion is a parameter so a caller can pass a build it
    # already read and tests can inject one. Left unset it reads the live
    # client. The pinned-tuple warnings below say whether the PINS agree with
    # each other; the installed-build warning says whether the user's own
    # Spotify is inside what LibreSpot verified, which is a different question
    # and was the one no script lane ever answered.
    param([string]$InstalledSpotifyVersion)

    $warnings = @()
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spicetifyDeclaredMax = [string]$global:PinnedReleases.SpicetifyCLI.WindowsDeclaredMaxSpotify
    $verifiedMax = [string]$global:PinnedReleases.SpicetifyCLI.LibreSpotVerifiedMaxSpotify
    $cliVersion = [string]$global:PinnedReleases.SpicetifyCLI.Version

    if (-not $PSBoundParameters.ContainsKey('InstalledSpotifyVersion')) {
        $InstalledSpotifyVersion = [string](Get-InstalledSpotifyVersion)
    }

    # Past what Spicetify itself claims to handle is a different situation from
    # past what LibreSpot has checked, and only the first is upstream's. Wording
    # matches AppCatalog.CheckInstalledSpotifyCompatibility so every lane says
    # the same thing about the same build.
    if (-not [string]::IsNullOrWhiteSpace($InstalledSpotifyVersion)) {
        if (-not [string]::IsNullOrWhiteSpace($spicetifyDeclaredMax) -and
            (Compare-LibreSpotVersions -Latest $InstalledSpotifyVersion -Current $spicetifyDeclaredMax)) {
            $warnings += "Installed Spotify $InstalledSpotifyVersion is newer than Spicetify CLI $cliVersion declares support for ($spicetifyDeclaredMax). Themes and extensions may not apply correctly."
        } elseif (-not [string]::IsNullOrWhiteSpace($verifiedMax) -and
            (Compare-LibreSpotVersions -Latest $InstalledSpotifyVersion -Current $verifiedMax)) {
            $warnings += "Installed Spotify $InstalledSpotifyVersion is newer than the build LibreSpot has verified ($verifiedMax), though Spicetify CLI $cliVersion declares support through $spicetifyDeclaredMax. Apply as usual, then check that themes and extensions loaded before relying on them."
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($spotxTarget.Id) -and
        -not [string]::IsNullOrWhiteSpace($spicetifyDeclaredMax) -and
        (Compare-LibreSpotVersions -Latest $spotxTarget.Id -Current $spicetifyDeclaredMax)) {
        $warnings += "SpotX target Spotify $($spotxTarget.Id) is newer than Spicetify CLI v$cliVersion declares for Windows/Microsoft Store ($spicetifyDeclaredMax); validate Spicetify CSS maps after patching, and if the pinned Spicetify is advanced past 2.44.0, confirm the newer build does not hard-refuse 'backup apply' on this Spotify version (spicetify/cli main gate merged after 2.44.0)."
    } elseif (-not [string]::IsNullOrWhiteSpace($spotxTarget.Id) -and
        -not [string]::IsNullOrWhiteSpace($verifiedMax) -and
        (Compare-LibreSpotVersions -Latest $spotxTarget.Id -Current $verifiedMax)) {
        $warnings += "SpotX target Spotify $($spotxTarget.Id) is newer than the build LibreSpot has verified ($verifiedMax), though Spicetify CLI v$cliVersion declares Windows/Microsoft Store support through $spicetifyDeclaredMax; this ceiling is LibreSpot's own, so apply as usual and confirm themes and extensions loaded."
    }
    return $warnings
}
