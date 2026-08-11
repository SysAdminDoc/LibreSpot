function Invoke-WithSpicetifyStatePreservation {
    param(
        [Parameter(Mandatory)][string]$Action,
        [Parameter(Mandatory)][scriptblock]$Operation
    )

    $marketplaceExport = $null
    if ($Action -in @('Reapply', 'RepairMarketplace')) {
        $marketplaceExport = Export-MarketplaceState
    }

    $snapshot = New-SpicetifyStatePreservationSnapshot -Action $Action
    if ($marketplaceExport) {
        Add-Member -InputObject $snapshot -MemberType NoteProperty -Name marketplaceExportPath -Value ([string]$marketplaceExport.Path) -Force
    }
    $operationError = $null
    $result = $null
    try {
        $result = & $Operation
    } catch {
        $operationError = $_
    }

    $recovery = Restore-SpicetifyStatePreservationSnapshot -Snapshot $snapshot -OperationSucceeded ($null -eq $operationError)
    if (-not $recovery.Succeeded) {
        $operationMessage = if ($operationError) { "$($operationError.Exception.Message) " } else { '' }
        throw "${operationMessage}Spicetify state recovery failed, but the backup remains at $($snapshot.snapshotPath). $($recovery.Message)"
    }
    if ($operationError) {
        throw $operationError
    }

    return $result
}
