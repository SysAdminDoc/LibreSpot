function Exit-LibreSpotAssetCacheLease {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Lease)

    if ($null -eq $global:LibreSpotAssetCacheLeases -or
        $null -eq $Lease -or
        [string]::IsNullOrWhiteSpace([string]$Lease.Key)) {
        return
    }

    $state = $global:LibreSpotAssetCacheLeases[[string]$Lease.Key]
    if ($null -eq $state) {
        return
    }

    $state.Depth = [int]$state.Depth - 1
    if ($state.Depth -gt 0) {
        return
    }

    $global:LibreSpotAssetCacheLeases.Remove([string]$Lease.Key)
    try { $state.Stream.Dispose() } catch {}
}
