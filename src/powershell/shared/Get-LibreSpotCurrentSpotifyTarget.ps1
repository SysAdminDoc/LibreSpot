function Get-LibreSpotCurrentSpotifyTarget {
    $entry = $global:SpotifyVersionManifest | Where-Object { $_.Id -ne 'auto' } | Select-Object -First 1
    if (-not $entry) {
        return [pscustomobject]@{ Id = 'unknown'; Version = '' }
    }
    return [pscustomobject]@{
        Id      = [string]$entry.Id
        Version = [string]$entry.Version
    }
}
