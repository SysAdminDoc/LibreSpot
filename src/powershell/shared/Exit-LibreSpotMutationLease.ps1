function Exit-LibreSpotMutationLease {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Lease)

    if ($null -eq $global:LibreSpotMutationLeases -or $null -eq $Lease -or [string]::IsNullOrWhiteSpace([string]$Lease.Key)) {
        return
    }
    $state = $global:LibreSpotMutationLeases[[string]$Lease.Key]
    if ($null -eq $state) {
        return
    }
    $state.Depth = [int]$state.Depth - 1
    if ($state.Depth -gt 0) {
        return
    }
    $global:LibreSpotMutationLeases.Remove([string]$Lease.Key)
    try { $state.Stream.Dispose() } catch {}
}
