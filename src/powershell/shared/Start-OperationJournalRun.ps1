function Start-OperationJournalRun {
    param(
        [string]$Action,
        [string]$Target = '',
        [bool]$WouldChange = $true,
        [bool]$Reversible = $false,
        [string]$RollbackHint = ''
    )
    $global:CURRENT_OPERATION_ID = [Guid]::NewGuid().ToString('N')
    $global:CURRENT_OPERATION_ACTION = $Action
    Write-OperationJournalEntry -OperationId $global:CURRENT_OPERATION_ID -Action $Action -Phase 'planned' -Target $Target -SafetyDecision 'Pending' -Result 'Started' -WouldChange $WouldChange -Reversible $Reversible -RollbackHint $RollbackHint
    Write-Log "Operation id: $global:CURRENT_OPERATION_ID"
    return $global:CURRENT_OPERATION_ID
}
