function Clear-LibreSpotCache {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
        Write-Log 'Asset cache directory does not exist. Nothing to clear.'
        return
    }
    if ($PSCmdlet.ShouldProcess($global:CACHE_DIR, 'Clear asset cache')) {
        $cacheFiles = @(Get-ChildItem -LiteralPath $global:CACHE_DIR -File -Recurse -ErrorAction SilentlyContinue)
        $byteMeasure = $cacheFiles | Measure-Object -Property Length -Sum
        $totalBytes = if ($null -eq $byteMeasure.Sum) { [int64]0 } else { [int64]$byteMeasure.Sum }
        Write-OperationJournalEntry -Phase 'cache' -Target $global:CACHE_DIR -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Cache will be rebuilt automatically on next download.' -Data @{
            fileCount = $cacheFiles.Count
            totalBytes = $totalBytes
        }
        try {
            Remove-Item -LiteralPath $global:CACHE_DIR -Recurse -Force -ErrorAction Stop
            Write-OperationJournalEntry -Phase 'cache' -Target $global:CACHE_DIR -SafetyDecision 'Allowed' -Result 'Cleared' -WouldChange $true -Reversible $false -RollbackHint 'Cache will be rebuilt automatically on next download.' -Data @{
                fileCount = $cacheFiles.Count
                totalBytes = $totalBytes
            }
            Write-Log "Asset cache cleared ($($cacheFiles.Count) file(s), $totalBytes bytes)."
        } catch {
            Write-Log "Failed to clear asset cache: $($_.Exception.Message)" -Level 'WARN'
        }
    }
}
