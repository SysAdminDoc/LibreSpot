function Start-OperationJournalRun {
    param(
        [string]$Action,
        [string]$Target = '',
        [bool]$WouldChange = $true,
        [bool]$Reversible = $false,
        [string]$RollbackHint = '',
        [string]$OperationId = ''
    )
    if ([string]::IsNullOrWhiteSpace($OperationId)) {
        $global:CURRENT_OPERATION_ID = [Guid]::NewGuid().ToString()
    } else {
        $parsedOperationId = [Guid]::Empty
        if (-not [Guid]::TryParse($OperationId, [ref]$parsedOperationId)) {
            throw "OperationId must be a GUID. Received '$OperationId'."
        }
        $global:CURRENT_OPERATION_ID = $parsedOperationId.ToString()
    }
    $global:CURRENT_OPERATION_ACTION = $Action
    Write-OperationJournalEntry -OperationId $global:CURRENT_OPERATION_ID -Action $Action -Phase 'planned' -Target $Target -SafetyDecision 'Pending' -Result 'Started' -WouldChange $WouldChange -Reversible $Reversible -RollbackHint $RollbackHint
    Write-Log "Operation id: $global:CURRENT_OPERATION_ID"
    return $global:CURRENT_OPERATION_ID
}
