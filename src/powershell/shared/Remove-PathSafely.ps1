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
            # Never use a recursive filesystem or ACL operation here. A nested
            # junction can redirect both Remove-Item -Recurse and icacls /T
            # outside the approved root on Windows PowerShell 5.1. Enumerate
            # ordinary directories ourselves, unlink every reparse point without
            # traversing it, delete files, then remove directories bottom-up.
            $item = Get-Item -LiteralPath $Path -Force -EA Stop
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                $item.Delete()
                Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
                Write-Log "  Removed link (target untouched): $displayLabel"
                return 1
            }

            if ($item -is [System.IO.DirectoryInfo]) {
                $pendingDirectories = [System.Collections.Generic.Stack[System.IO.DirectoryInfo]]::new()
                $visitedDirectories = [System.Collections.Generic.List[System.IO.DirectoryInfo]]::new()
                $pendingDirectories.Push($item)

                while ($pendingDirectories.Count -gt 0) {
                    $directory = $pendingDirectories.Pop()
                    $visitedDirectories.Add($directory)
                    $children = @($directory.EnumerateFileSystemInfos())
                    foreach ($child in $children) {
                        if ($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                            $child.Delete()
                            continue
                        }
                        if ($child -is [System.IO.DirectoryInfo]) {
                            $pendingDirectories.Push($child)
                            continue
                        }

                        try {
                            $child.Attributes = [System.IO.FileAttributes]::Normal
                            $child.Delete()
                        } catch {
                            $null = & icacls.exe "$($child.FullName)" /reset /C /Q 2>$null
                            $child.Refresh()
                            $child.Attributes = [System.IO.FileAttributes]::Normal
                            $child.Delete()
                        }
                    }
                }

                $directoriesDeepestFirst = @($visitedDirectories | Sort-Object { $_.FullName.Length } -Descending)
                foreach ($directory in $directoriesDeepestFirst) {
                    try {
                        $directory.Attributes = [System.IO.FileAttributes]::Directory
                        $directory.Delete($false)
                    } catch {
                        $null = & icacls.exe "$($directory.FullName)" /reset /C /Q 2>$null
                        $directory.Refresh()
                        $directory.Attributes = [System.IO.FileAttributes]::Directory
                        $directory.Delete($false)
                    }
                }
            } else {
                try {
                    $item.Attributes = [System.IO.FileAttributes]::Normal
                    $item.Delete()
                } catch {
                    $null = & icacls.exe "$Path" /reset /C /Q 2>$null
                    $item.Refresh()
                    $item.Attributes = [System.IO.FileAttributes]::Normal
                    $item.Delete()
                }
            }
            Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
            Write-Log "  Removed: $displayLabel"
            return 1
        } catch {
            $journalData['error'] = [string]$_.Exception.Message
            Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'The approved root may be partially removed, but reparse-point targets were not traversed; review the error before retrying.' -Data $journalData
            Write-Log "  Failed to remove: $Path ($($_.Exception.Message))" -Level 'WARN'
            return 0
        }
    }
    return 0
}
