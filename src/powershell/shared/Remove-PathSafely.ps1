function Remove-PathSafely {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Path,[string]$Label)
    $displayLabel = if ($Label) { $Label } else { $Path }
    $journalData = @{ label = $displayLabel }
    if ([string]::IsNullOrWhiteSpace($Path)) { return 0 }
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'SkippedMissingTarget' -Result 'Skipped' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target did not exist.' -Data $journalData
        return 0
    }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'RefusedUnsafeTarget' -Result 'Refused' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target failed LibreSpot safe-removal checks.' -Data $journalData
        Write-Log "  Refusing to remove unsafe target: $Path" -Level 'WARN'
        return 0
    }
    Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
    if ($PSCmdlet.ShouldProcess($Path, 'Remove file or directory')) {
        try {
            $null = & icacls.exe "$Path" /reset /T /C /Q 2>$null
            Remove-Item -LiteralPath $Path -Recurse -Force -EA Stop
            Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
            Write-Log "  Removed: $displayLabel"
            return 1
        } catch {
            $journalData['error'] = [string]$_.Exception.Message
            Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'The target may be partially unchanged; review the error before retrying.' -Data $journalData
            Write-Log "  Failed to remove: $Path ($($_.Exception.Message))" -Level 'WARN'
            return 0
        }
    }
    return 0
}
