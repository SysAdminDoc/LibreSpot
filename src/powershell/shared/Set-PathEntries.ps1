function Set-PathEntries {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [ValidateSet('User','Process')] [string]$Scope = 'User',
        [string[]]$Entries
    )
    $orderedEntries = [System.Collections.Generic.List[string]]::new()
    $seen = @{}
    foreach ($entry in @($Entries)) {
        if ([string]::IsNullOrWhiteSpace($entry)) { continue }
        $normalized = Get-NormalizedPathString -Path $entry
        if ([string]::IsNullOrWhiteSpace($normalized)) { continue }
        $key = $normalized.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        $orderedEntries.Add($entry.Trim())
    }
    $pathValue = ($orderedEntries -join ';')
    if ($PSCmdlet.ShouldProcess("$Scope PATH", 'Update PATH entries')) {
        Write-OperationJournalEntry -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore the previous PATH value.'
        if ($Scope -eq 'Process') {
            $env:PATH = $pathValue
        } else {
            [Environment]::SetEnvironmentVariable('PATH', $pathValue, $Scope)
        }
        Write-OperationJournalEntry -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Updated' -WouldChange $true -Reversible $true -RollbackHint 'Restore the previous PATH value.'
    }
}
