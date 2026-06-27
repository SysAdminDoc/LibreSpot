function Get-InstalledSpotifyVersion {
    if (-not (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) { return $null }
    try { return (Get-Item -LiteralPath $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion }
    catch { return $null }
}
