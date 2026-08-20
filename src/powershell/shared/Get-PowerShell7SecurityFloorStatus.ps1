function Get-PowerShell7SecurityFloorStatus {
    [CmdletBinding()]
    param(
        [string]$VersionString = [string]$PSVersionTable.PSVersion,
        [string]$Edition = [string]$PSVersionTable.PSEdition
    )

    $minimumVersion = [Version]'7.6.5'
    $version = $null
    try { $version = [Version]::Parse($VersionString) } catch {}

    $result = [ordered]@{
        NeedsUpdate   = $false
        Status        = 'NotApplicable'
        Edition       = $Edition
        Version       = $VersionString
        MinimumVersion = $minimumVersion.ToString()
        Cve           = 'CVE-2026-50523'
        Reason        = ''
    }

    if (-not [string]::Equals($Edition, 'Core', [StringComparison]::OrdinalIgnoreCase)) {
        $result.Reason = 'PowerShell 7 security floor does not apply to Windows PowerShell 5.1.'
        return [pscustomobject]$result
    }

    if ($null -eq $version) {
        $result.Status = 'Unknown'
        $result.Reason = "Could not parse the PowerShell 7 version '$VersionString'. Keep PowerShell updated to 7.6.5 or newer."
        return [pscustomobject]$result
    }

    if ($version -lt $minimumVersion) {
        $result.NeedsUpdate = $true
        $result.Status = 'UpdateRecommended'
        $result.Reason = "PowerShell 7 $VersionString is below the LibreSpot security floor of 7.6.5. Update PowerShell before continuing. This floor covers $($result.Cve) and related August 2026 servicing fixes."
    } else {
        $result.Status = 'Supported'
        $result.Reason = "PowerShell 7 $VersionString meets the LibreSpot security floor of 7.6.5."
    }

    return [pscustomobject]$result
}
