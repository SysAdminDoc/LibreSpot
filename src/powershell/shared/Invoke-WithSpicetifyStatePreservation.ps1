function Invoke-WithSpicetifyStatePreservation {
    param(
        [Parameter(Mandatory)][string]$Action,
        [Parameter(Mandatory)][scriptblock]$Operation
    )

    $snapshot = New-SpicetifyStatePreservationSnapshot -Action $Action
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
