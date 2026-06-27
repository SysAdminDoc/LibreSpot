function Write-DownloaderCveWarningIfNeeded {
    if ($global:CveDownloaderWarned) { return }
    $global:CveDownloaderWarned = $true
    try {
        $exposure = Get-DownloaderCveExposure
        if ($exposure.Exposed) {
            Write-Log "Security: $($exposure.Reason)" -Level 'WARN'
        }
    } catch {}
}
