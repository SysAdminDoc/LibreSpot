function Unregister-AutoReapplyTask {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if ($PSCmdlet.ShouldProcess($global:WATCHER_TASK_NAME, 'Remove scheduled task')) {
        Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Re-register the scheduled task to undo.'
        try {
            $null = & schtasks.exe /Delete /TN $global:WATCHER_TASK_NAME /F 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $true -RollbackHint 'Re-register the scheduled task to undo.'
                Write-WatcherLog "Unregister: scheduled task removed"
                return $true
            }
            return $false
        } catch { return $false }
    }
    return $false
}
