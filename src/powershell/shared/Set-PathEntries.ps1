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
            # SetEnvironmentVariable writes a REG_SZ value and therefore
            # destroys expandable PATH tokens. Keep the user PATH explicitly
            # typed as REG_EXPAND_SZ, then notify already-running shells.
            $environmentKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Environment')
            try {
                if ($null -eq $environmentKey) { throw 'Unable to open the current user environment registry key.' }
                $environmentKey.SetValue('Path', $pathValue, [Microsoft.Win32.RegistryValueKind]::ExpandString)
            } finally {
                if ($null -ne $environmentKey) { $environmentKey.Dispose() }
            }

            if (-not ('LibreSpot.EnvironmentChangeNativeMethods' -as [type])) {
                Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace LibreSpot
{
    public static class EnvironmentChangeNativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint message,
            UIntPtr wParam,
            string lParam,
            uint flags,
            uint timeout,
            out UIntPtr result);
    }
 }
'@
            }

            $broadcastResult = [UIntPtr]::Zero
            $null = [LibreSpot.EnvironmentChangeNativeMethods]::SendMessageTimeout(
                [IntPtr]0xffff,
                0x001A,
                [UIntPtr]::Zero,
                'Environment',
                0x0002,
                5000,
                [ref]$broadcastResult)
        }
        Write-OperationJournalEntry -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Updated' -WouldChange $true -Reversible $true -RollbackHint 'Restore the previous PATH value.'
    }
}
