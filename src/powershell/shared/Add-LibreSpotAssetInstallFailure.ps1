function Add-LibreSpotAssetInstallFailure {
    # Records one selected asset that could not be installed while the rest of
    # the run carried on. Every caller used to log a warning and return, which
    # left the run reporting success to a user who got no theme.
    param(
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$Reason = ''
    )

    if ($null -eq $global:LibreSpotAssetInstallFailures) {
        $global:LibreSpotAssetInstallFailures = [System.Collections.Generic.List[object]]::new()
    }

    $trimmedReason = if ($null -eq $Reason) { '' } else { ([string]$Reason).Trim() }
    $global:LibreSpotAssetInstallFailures.Add([pscustomobject]@{
        Kind   = [string]$Kind
        Name   = [string]$Name
        Reason = $trimmedReason
    })

    $suffix = if ([string]::IsNullOrWhiteSpace($trimmedReason)) { '' } else { " $trimmedReason" }
    Write-Log "$Kind '$Name' was not installed.$suffix" -Level 'WARN'
}
