function Clear-LibreSpotCache {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
        Write-Log 'Asset cache directory does not exist. Nothing to clear.'
        return
    }
    if ($PSCmdlet.ShouldProcess($global:CACHE_DIR, 'Clear asset cache')) {
        Write-OperationJournalEntry -Phase 'cache' -Target $global:CACHE_DIR -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Cache will be rebuilt automatically on next download.'
        try {
            Remove-Item -LiteralPath $global:CACHE_DIR -Recurse -Force -ErrorAction Stop
            Write-OperationJournalEntry -Phase 'cache' -Target $global:CACHE_DIR -SafetyDecision 'Allowed' -Result 'Cleared' -WouldChange $true -Reversible $false -RollbackHint 'Cache will be rebuilt automatically on next download.'
            Write-Log 'Asset cache cleared.'
        } catch {
            Write-Log "Failed to clear asset cache: $($_.Exception.Message)" -Level 'WARN'
        }
    }
}
