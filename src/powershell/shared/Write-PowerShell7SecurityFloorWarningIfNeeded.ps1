function Write-PowerShell7SecurityFloorWarningIfNeeded {
    if ($global:PowerShell7SecurityFloorWarned) { return }
    $global:PowerShell7SecurityFloorWarned = $true
    try {
        $floor = Get-PowerShell7SecurityFloorStatus
        if ($floor.NeedsUpdate) {
            Write-Log "Security: $($floor.Reason)" -Level 'WARN'
        }
    } catch {}
}
