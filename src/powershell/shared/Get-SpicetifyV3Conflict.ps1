function Get-SpicetifyV3Conflict {
    [CmdletBinding()]
    param(
        [string]$SpotifyPath,
        [string]$SpicetifyInstallDirectory,
        [string]$SpicetifyConfigDirectory,
        [string]$CliVersion
    )

    if ([string]::IsNullOrWhiteSpace($SpotifyPath)) {
        $SpotifyPath = [string]$global:SPOTIFY_EXE_PATH
    }
    if ([string]::IsNullOrWhiteSpace($SpicetifyInstallDirectory)) {
        $SpicetifyInstallDirectory = [string]$global:SPICETIFY_DIR
    }
    if ([string]::IsNullOrWhiteSpace($SpicetifyConfigDirectory)) {
        $SpicetifyConfigDirectory = [string]$global:SPICETIFY_CONFIG_DIR
    }

    if ([string]::IsNullOrWhiteSpace($CliVersion) -and
        (Get-Command Get-InstalledSpicetifyCliVersion -CommandType Function -ErrorAction SilentlyContinue)) {
        try { $CliVersion = [string](Get-InstalledSpicetifyCliVersion) } catch {}
    }

    $markers = [System.Collections.Generic.List[string]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $addMarker = {
        param([string]$Marker)
        if (-not [string]::IsNullOrWhiteSpace($Marker) -and $seen.Add($Marker)) {
            $markers.Add($Marker) | Out-Null
        }
    }

    $spotifyDirectory = $null
    if (-not [string]::IsNullOrWhiteSpace($SpotifyPath)) {
        try { $spotifyDirectory = Split-Path -Path $SpotifyPath -Parent } catch {}
    }
    if (-not [string]::IsNullOrWhiteSpace($spotifyDirectory) -and
        (Test-Path -LiteralPath (Join-Path $spotifyDirectory 'Apps\xpui.spa.backup') -PathType Leaf)) {
        & $addMarker 'Apps\xpui.spa.backup'
    }

    foreach ($layout in @(
        [pscustomobject]@{ Path = $SpicetifyInstallDirectory; Label = 'spicetify install' }
        [pscustomobject]@{ Path = $SpicetifyConfigDirectory; Label = 'spicetify config' }
    )) {
        if ([string]::IsNullOrWhiteSpace([string]$layout.Path)) { continue }
        foreach ($directoryName in @('modules', 'hooks')) {
            $candidate = Join-Path ([string]$layout.Path) $directoryName
            if (Test-Path -LiteralPath $candidate -PathType Container) {
                & $addMarker ("{0}\{1}" -f $layout.Label, $directoryName)
            }
        }
    }

    if (Get-Command Get-SpicetifyCliMajorVersion -CommandType Function -ErrorAction SilentlyContinue) {
        $major = Get-SpicetifyCliMajorVersion -Version $CliVersion
        if ($null -ne $major -and $major -gt 2) {
            & $addMarker ("Spicetify CLI major {0}" -f $major)
        }
    }

    $isConflict = $markers.Count -gt 0
    $message = if ($isConflict) {
        "Spicetify v3 or newer artifacts were detected ($($markers -join ', ')). LibreSpot stopped before changing Spotify. Run 'spicetify restore' first, then reinstall the pinned Spicetify 2.x CLI."
    } else {
        $null
    }

    return [pscustomobject][ordered]@{
        IsConflict = $isConflict
        Markers = $markers.ToArray()
        SafeAction = 'spicetify restore'
        Message = $message
    }
}
