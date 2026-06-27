function Test-SpotifyRunning {
    try { return [bool](Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue) }
    catch { return $false }
}
