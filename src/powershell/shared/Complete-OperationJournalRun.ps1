function Complete-OperationJournalRun {
    param(
        [string]$Result = 'Succeeded',
        [string]$Message = ''
    )
    Write-OperationJournalEntry -Phase 'complete' -Target $Message -SafetyDecision 'NotEvaluated' -Result $Result -WouldChange $false -Reversible $false
    try {
        if ([string]::IsNullOrWhiteSpace($global:RUN_RECEIPT_PATH) -or [string]::IsNullOrWhiteSpace($global:CURRENT_OPERATION_ID)) { return }
        if (-not (Test-Path -LiteralPath $global:OPERATION_JOURNAL_PATH -PathType Leaf)) { return }

        $entries = @()
        foreach ($line in (Get-Content -LiteralPath $global:OPERATION_JOURNAL_PATH -Tail 500 -ErrorAction SilentlyContinue)) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $entry = $line | ConvertFrom-Json -ErrorAction Stop
                if ($entry.operationId -eq $global:CURRENT_OPERATION_ID) { $entries += $entry }
            } catch {}
        }

        $operationEntries = @($entries |
            Where-Object { $_.tokenKind -and $_.phase -ne 'planned' -and $_.phase -ne 'complete' } |
            ForEach-Object {
                [ordered]@{
                    tokenKind        = [string]$_.tokenKind
                    target           = [string]$_.target
                    previousStateRef = [string]$_.previousStateRef
                    newState         = [string]$_.newState
                    result           = if ($_.result -eq 'Failed') { 'failed' } elseif ($_.result -eq 'Skipped') { 'skipped' } else { 'applied' }
                    reversible       = [bool]$_.reversible
                    undoAction       = [string]$_.undoAction
                    risk             = [string]$_.risk
                }
            })

        $status = switch ($Result) {
            'Succeeded' { 'success' }
            'Canceled' { 'canceled' }
            'Cancelled' { 'canceled' }
            'DryRun' { 'dryRun' }
            'PartialSuccess' { 'partialSuccess' }
            default { 'failed' }
        }

        $firstEntry = @($entries | Select-Object -First 1)
        $startedAt = if ($firstEntry.Count -gt 0 -and $firstEntry[0].timestamp) { [string]$firstEntry[0].timestamp } else { (Get-Date).ToUniversalTime().ToString('o') }
        $undoAvailable = @($operationEntries | Where-Object { $_.reversible -and -not [string]::IsNullOrWhiteSpace($_.previousStateRef) }).Count -gt 0
        $receipt = [ordered]@{
            schemaVersion = 1
            receiptId     = [Guid]::NewGuid().ToString()
            runId         = $global:CURRENT_OPERATION_ID
            operationId   = $global:CURRENT_OPERATION_ID
            startedAt     = $startedAt
            completedAt   = (Get-Date).ToUniversalTime().ToString('o')
            action        = $global:CURRENT_OPERATION_ACTION
            status        = $status
            errorSummary  = if ($status -eq 'failed') { $Message } else { $null }
            undoAvailable = $undoAvailable
            logRef        = $global:LOG_PATH
            operations    = $operationEntries
        }

        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($global:RUN_RECEIPT_PATH, ($receipt | ConvertTo-Json -Depth 6), $utf8NoBom)
    } catch {
        try { Write-Log "Run receipt write failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}
