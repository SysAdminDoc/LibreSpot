function Open-SpicetifyMarketplace {
    try {
        Start-Process -FilePath 'explorer.exe' -ArgumentList 'spotify:app:marketplace'
        Write-Log 'Requested Spotify Marketplace via spotify:app:marketplace.'
    } catch {
        Write-Log "Could not open spotify:app:marketplace automatically: $($_.Exception.Message)" -Level 'WARN'
    }
}
