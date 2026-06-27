function Optimize-OperationJournalRetention {
    try {
        $maxBytes = [int64]$global:OPERATION_JOURNAL_MAX_BYTES
        $retainBytes = [int64]$global:OPERATION_JOURNAL_RETAIN_BYTES
        if ($maxBytes -le 0 -or $retainBytes -le 0 -or $retainBytes -ge $maxBytes) { return }
        if (-not (Test-Path -LiteralPath $global:OPERATION_JOURNAL_PATH -PathType Leaf)) { return }

        $file = Get-Item -LiteralPath $global:OPERATION_JOURNAL_PATH -ErrorAction Stop
        if ($file.Length -le $maxBytes) { return }

        $bytesToRead = [int][Math]::Min($retainBytes, $file.Length)
        $buffer = New-Object 'System.Byte[]' $bytesToRead
        $stream = [System.IO.File]::Open($global:OPERATION_JOURNAL_PATH, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $null = $stream.Seek(-1 * $bytesToRead, [System.IO.SeekOrigin]::End)
            $read = $stream.Read($buffer, 0, $buffer.Length)
        } finally {
            try { $stream.Dispose() } catch {}
        }

        $tail = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
        $firstNewline = $tail.IndexOf("`n")
        if ($firstNewline -ge 0 -and $firstNewline -lt ($tail.Length - 1)) {
            $tail = $tail.Substring($firstNewline + 1)
        }

        $entry = [ordered]@{
            schemaVersion  = 1
            timestamp      = (Get-Date).ToUniversalTime().ToString('o')
            operationId    = 'journal-retention'
            action         = 'OperationJournal'
            phase          = 'retention'
            target         = $global:OPERATION_JOURNAL_PATH
            safetyDecision = 'Allowed'
            result         = 'Trimmed'
            wouldChange    = $true
            reversible     = $false
            rollbackHint   = 'Older operation journal entries were trimmed to keep local diagnostics bounded.'
            data           = @{
                previousBytes = $file.Length
                retainedBytes = [System.Text.Encoding]::UTF8.GetByteCount($tail)
                maxBytes      = $maxBytes
            }
        }
        $json = $entry | ConvertTo-Json -Compress -Depth 6
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($global:OPERATION_JOURNAL_PATH, $json + [Environment]::NewLine + $tail, $utf8NoBom)
    } catch {
        try { Write-Log "Operation journal retention failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}
