function Open-SpicetifyMarketplace {
    $requestedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    try {
        Start-Process -FilePath 'explorer.exe' -ArgumentList 'spotify:app:marketplace'
        Write-Log 'Requested Spotify Marketplace via spotify:app:marketplace.'
        Start-Sleep -Milliseconds 500
        $spotifyRunning = try { @((Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)).Count -gt 0 } catch { $false }
        $result = [pscustomobject]@{
            Succeeded               = $true
            Message                 = 'spotify:app:marketplace was handed to Windows.'
            RequestedAtUtc          = $requestedAtUtc
            SpotifyRunningAfterOpen = $spotifyRunning
        }
    } catch {
        $message = "Could not open spotify:app:marketplace automatically: $($_.Exception.Message)"
        Write-Log $message -Level 'WARN'
        $result = [pscustomobject]@{
            Succeeded               = $false
            Message                 = $message
            RequestedAtUtc          = $requestedAtUtc
            SpotifyRunningAfterOpen = $null
        }
    }
    Write-MarketplaceVisibilityEvidence -Source 'OpenMarketplace' -OpenUriSucceeded $result.Succeeded -OpenUriMessage $result.Message -OpenUriRequestedAtUtc $result.RequestedAtUtc -SpotifyRunningAfterOpen $result.SpotifyRunningAfterOpen | Out-Null
    return $result
}
