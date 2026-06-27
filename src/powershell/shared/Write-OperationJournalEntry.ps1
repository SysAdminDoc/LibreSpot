function Write-OperationJournalEntry {
    param(
        [string]$OperationId = $global:CURRENT_OPERATION_ID,
        [string]$Action = $global:CURRENT_OPERATION_ACTION,
        [string]$Phase = 'event',
        [string]$Target = '',
        [string]$SafetyDecision = 'NotEvaluated',
        [string]$Result = 'Info',
        [bool]$WouldChange = $false,
        [bool]$Reversible = $false,
        [string]$RollbackHint = '',
        [hashtable]$Data = $null
    )
    try {
        if ([string]::IsNullOrWhiteSpace($OperationId)) { $OperationId = [Guid]::NewGuid().ToString('N') }
        if ([string]::IsNullOrWhiteSpace($Action)) { $Action = 'Unknown' }
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        Optimize-OperationJournalRetention
        $entry = [ordered]@{
            schemaVersion  = 1
            timestamp      = (Get-Date).ToUniversalTime().ToString('o')
            operationId    = $OperationId
            action         = $Action
            phase          = $Phase
            target         = $Target
            safetyDecision = $SafetyDecision
            result         = $Result
            wouldChange    = $WouldChange
            reversible     = $Reversible
            rollbackHint   = $RollbackHint
        }
        if ($Data) { $entry.data = $Data }
        $json = $entry | ConvertTo-Json -Compress -Depth 6
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::AppendAllText($global:OPERATION_JOURNAL_PATH, $json + [Environment]::NewLine, $utf8NoBom)
    } catch {
        try { Write-Log "Operation journal write failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}
