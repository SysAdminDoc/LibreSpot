function Save-LibreSpotConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([hashtable]$Config)
    if (-not $PSCmdlet.ShouldProcess($global:CONFIG_PATH, 'Save configuration')) {
        return $true
    }
    Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
    $tempPath = $null
    $backupPath = $null
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }
        $tempPath = Join-Path $global:CONFIG_DIR ("config.{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
        $backupPath = Join-Path $global:CONFIG_DIR ("config.{0}.bak" -f [Guid]::NewGuid().ToString('N'))
        $normalizedConfig = Normalize-LibreSpotConfig -Config $Config
        $json = [ordered]@{}
        foreach ($key in $normalizedConfig.Keys) { $json[$key] = $normalizedConfig[$key] }
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($tempPath, ($json | ConvertTo-Json -Depth 4), $utf8)
        if (Test-Path -LiteralPath $global:CONFIG_PATH) {
            try {
                [System.IO.File]::Replace($tempPath, $global:CONFIG_PATH, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                # Replace() can fail on some filesystems; fall back to atomic Move
                Remove-Item -LiteralPath $global:CONFIG_PATH -Force -ErrorAction Stop
                [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
            }
        } else {
            [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
        }
        Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Saved' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
        return $true
    } catch {
        Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
        try { Write-Log "Config save failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
        if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
        if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
        return $false
    }
}
