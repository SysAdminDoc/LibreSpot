function Get-DownloaderCveExposure {
    $result = [ordered]@{
        Exposed = $false
        Status  = 'NotAffected'   # NotAffected | Patched | PossiblyExposed | Unknown
        Reason  = ''
        Edition = [string]$PSVersionTable.PSEdition
        OSBuild = ''
    }
    # Windows PowerShell 5.1 advisories that reach the download primitive this
    # script uses. Every one is delivered by a Windows cumulative update, so
    # only the in-box Desktop edition is in scope; PowerShell 7 is a separate
    # product with its own floor (Get-PowerShell7SecurityFloorStatus).
    # Ordered oldest fix first: the last entry is the patch-wave anchor.
    $advisories = @(
        @{ Id = 'CVE-2025-54100'; Fixed = [datetime]'2025-12-09'; Summary = 'web-content remote code execution, CVSS 7.8' }
        @{ Id = 'CVE-2026-26170'; Fixed = [datetime]'2026-04-14'; Summary = 'improper input validation, local elevation of privilege, CVSS 7.8' }
        @{ Id = 'CVE-2026-40400'; Fixed = [datetime]'2026-07-14'; Summary = 'relative path traversal, code execution over a network, CVSS 8.0' }
    )
    $patchWave = $advisories[$advisories.Count - 1].Fixed
    $tracked = (@($advisories | ForEach-Object {
        "$($_.Id) ($($_.Summary); fixed $($_.Fixed.ToString('yyyy-MM-dd')))"
    })) -join '; '

    if ($PSVersionTable.PSEdition -and $PSVersionTable.PSEdition -ne 'Desktop') {
        $result.Reason = "PowerShell 7+ (Core) is in use; $tracked affect Windows PowerShell 5.1 only."
        return [pscustomobject]$result
    }

    try {
        $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop
        if ($cv.CurrentBuild) { $result.OSBuild = "$($cv.CurrentBuild).$($cv.UBR)" }
    } catch {}

    # Heuristic: the newest installed update vs the July 2026 patch wave, the
    # newest fix among the tracked advisories. We never claim certainty -- this
    # only flags a host that is plainly behind.
    $latest = $null
    try {
        $latest = Get-HotFix -ErrorAction Stop |
            Where-Object { $_.InstalledOn } |
            Sort-Object InstalledOn -Descending |
            Select-Object -First 1
    } catch {}

    if ($null -eq $latest -or $null -eq $latest.InstalledOn) {
        $result.Status = 'Unknown'
        $result.Reason = "Could not read the host update history to confirm the Windows PowerShell 5.1 fixes for $tracked. Keep Windows fully updated."
        return [pscustomobject]$result
    }
    if ($latest.InstalledOn -ge $patchWave) {
        $result.Status = 'Patched'
        $result.Reason = "Latest Windows update ($($latest.HotFixID), $($latest.InstalledOn.ToString('yyyy-MM-dd'))) is at or past the $($patchWave.ToString('yyyy-MM-dd')) cumulative update, the newest fix among the tracked Windows PowerShell 5.1 advisories: $tracked."
        return [pscustomobject]$result
    }

    $pending = (@($advisories | Where-Object { $latest.InstalledOn -lt $_.Fixed } | ForEach-Object { $_.Id })) -join ', '
    $result.Exposed = $true
    $result.Status  = 'PossiblyExposed'
    $result.Reason  = "The newest Windows update on this host is from $($latest.InstalledOn.ToString('yyyy-MM-dd')), before the $($patchWave.ToString('yyyy-MM-dd')) cumulative update. Tracked Windows PowerShell 5.1 advisories: $tracked. Still unfixed at this host's patch level: $pending. LibreSpot still hash-verifies every download, but install pending Windows updates to close the parse-time and path-traversal vectors."
    return [pscustomobject]$result
}
