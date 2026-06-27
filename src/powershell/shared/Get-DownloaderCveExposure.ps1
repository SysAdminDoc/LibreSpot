function Get-DownloaderCveExposure {
    $result = [ordered]@{
        Exposed = $false
        Status  = 'NotAffected'   # NotAffected | Patched | PossiblyExposed | Unknown
        Reason  = ''
        Edition = [string]$PSVersionTable.PSEdition
        OSBuild = ''
    }
    # Only Windows PowerShell 5.1 (Desktop edition) is in scope for this CVE.
    if ($PSVersionTable.PSEdition -and $PSVersionTable.PSEdition -ne 'Desktop') {
        $result.Reason = 'PowerShell 7+ (Core) is in use; CVE-2025-54100 affects Windows PowerShell 5.1 only.'
        return [pscustomobject]$result
    }

    try {
        $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop
        if ($cv.CurrentBuild) { $result.OSBuild = "$($cv.CurrentBuild).$($cv.UBR)" }
    } catch {}

    # Heuristic: the newest installed update vs the December 2025 patch wave.
    # We never claim certainty -- this only flags a host that is plainly behind.
    $patchWave = [datetime]'2025-12-09'
    $latest = $null
    try {
        $latest = Get-HotFix -ErrorAction Stop |
            Where-Object { $_.InstalledOn } |
            Sort-Object InstalledOn -Descending |
            Select-Object -First 1
    } catch {}

    if ($null -eq $latest -or $null -eq $latest.InstalledOn) {
        $result.Status = 'Unknown'
        $result.Reason = 'Could not read the host update history to confirm the December 2025 PowerShell fix (CVE-2025-54100). Keep Windows fully updated.'
        return [pscustomobject]$result
    }
    if ($latest.InstalledOn -ge $patchWave) {
        $result.Status = 'Patched'
        $result.Reason = "Latest Windows update ($($latest.HotFixID), $($latest.InstalledOn.ToString('yyyy-MM-dd'))) is at or past the December 2025 fix for CVE-2025-54100."
        return [pscustomobject]$result
    }

    $result.Exposed = $true
    $result.Status  = 'PossiblyExposed'
    $result.Reason  = "The newest Windows update on this host is from $($latest.InstalledOn.ToString('yyyy-MM-dd')), before the December 2025 cumulative update that fixes CVE-2025-54100 (a Windows PowerShell 5.1 web-content RCE). LibreSpot still hash-verifies every download, but install pending Windows updates to close the parse-time vector."
    return [pscustomobject]$result
}
