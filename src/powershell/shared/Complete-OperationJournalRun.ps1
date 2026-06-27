function Complete-OperationJournalRun {
    param(
        [string]$Result = 'Succeeded',
        [string]$Message = ''
    )
    Write-OperationJournalEntry -Phase 'complete' -Target $Message -SafetyDecision 'NotEvaluated' -Result $Result -WouldChange $false -Reversible $false
}
