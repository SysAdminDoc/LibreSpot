function Set-PathEntries {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [ValidateSet('User','Process')] [string]$Scope = 'User',
        [string[]]$Entries,
        [ValidateSet('pathEntryAdd','pathEntryRemove')] [string]$TokenKind = 'pathEntryAdd',
        [string]$ChangedEntry = ''
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
        $operationId = if ([string]::IsNullOrWhiteSpace([string]$global:CURRENT_OPERATION_ID)) { [Guid]::NewGuid().ToString('N') } else { [string]$global:CURRENT_OPERATION_ID }
        $previousStateRef = ''
        $expectedHash = ''
        $undoReady = $false
        $tempStatePath = ''
        if ($Scope -eq 'User' -and $TokenKind -eq 'pathEntryAdd' -and -not [string]::IsNullOrWhiteSpace($ChangedEntry)) {
            try {
                $environmentKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $false)
                try {
                    $previousPathExists = $null -ne $environmentKey -and @($environmentKey.GetValueNames()) -contains 'Path'
                    $previousPath = if (-not $previousPathExists) { '' } else {
                        [string]$environmentKey.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                    }
                    $previousPathKind = if (-not $previousPathExists) { 'String' } else { [string]$environmentKey.GetValueKind('Path') }
                    if ($previousPathExists -and $previousPathKind -notin @('String', 'ExpandString')) {
                        throw "User PATH has unsupported registry type '$previousPathKind'."
                    }
                } finally {
                    if ($null -ne $environmentKey) { $environmentKey.Dispose() }
                }

                $hashText = {
                    param([string]$Value)
                    $sha = [System.Security.Cryptography.SHA256]::Create()
                    try {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($(if ($null -eq $Value) { '' } else { $Value }))
                        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
                    } finally { $sha.Dispose() }
                }
                $previousHash = & $hashText $previousPath
                $expectedHash = & $hashText $pathValue
                $undoRoot = Join-Path $global:CONFIG_DIR 'undo-states'
                if (Test-Path -LiteralPath $undoRoot) {
                    $attributes = [System.IO.File]::GetAttributes($undoRoot)
                    if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Undo state directory is a reparse point.' }
                } else {
                    New-Item -Path $undoRoot -ItemType Directory -Force -ErrorAction Stop | Out-Null
                }
                $safeOperationId = $operationId -replace '[^A-Za-z0-9_-]', '_'
                $previousStateRef = Join-Path $undoRoot ("$safeOperationId-path-entry-add-" + [Guid]::NewGuid().ToString('N') + '.json')
                $tempStatePath = "$previousStateRef.tmp"
                $state = [ordered]@{
                    schemaVersion = 2
                    operationId = $operationId
                    tokenKind = 'pathEntryAdd'
                    scope = 'User'
                    target = 'User PATH'
                    entry = $ChangedEntry
                    previousValueExists = $previousPathExists
                    previousValue = $previousPath
                    previousValueKind = $previousPathKind
                    expectedValueExists = $true
                    expectedValue = $pathValue
                    expectedValueKind = 'ExpandString'
                    previousSha256 = $previousHash
                    expectedSha256 = $expectedHash
                    createdAtUtc = (Get-Date).ToUniversalTime().ToString('o')
                }
                $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
                [System.IO.File]::WriteAllText($tempStatePath, ($state | ConvertTo-Json -Depth 4), $utf8NoBom)
                [System.IO.File]::Move($tempStatePath, $previousStateRef)
                $undoReady = $true
                Get-ChildItem -LiteralPath $undoRoot -Filter '*.json' -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.LastWriteTimeUtc -lt (Get-Date).ToUniversalTime().AddDays(-30) } |
                    Remove-Item -Force -ErrorAction SilentlyContinue
            } catch {
                if ($tempStatePath) { Remove-Item -LiteralPath $tempStatePath -Force -ErrorAction SilentlyContinue }
                $previousStateRef = ''
                $expectedHash = ''
                try { Write-Log "PATH undo-state capture failed; the PATH update will continue without executable undo: $($_.Exception.Message)" -Level 'WARN' } catch {}
            }
        }
        $newState = if ([string]::IsNullOrWhiteSpace($expectedHash)) { 'Updated' } else { "sha256:$expectedHash" }
        Write-OperationJournalEntry -OperationId $operationId -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $undoReady -RollbackHint 'Restore the exact previous PATH value after validating its fingerprint.' -TokenKind $TokenKind -PreviousStateRef $previousStateRef -NewState $newState -UndoAction 'Restore the exact previous user PATH snapshot.' -Risk $(if ($TokenKind -eq 'pathEntryAdd') { 'low' } else { 'medium' })
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
        Write-OperationJournalEntry -OperationId $operationId -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Updated' -WouldChange $true -Reversible $undoReady -RollbackHint 'Restore the exact previous PATH value after validating its fingerprint.' -TokenKind $TokenKind -PreviousStateRef $previousStateRef -NewState $newState -UndoAction 'Restore the exact previous user PATH snapshot.' -Risk $(if ($TokenKind -eq 'pathEntryAdd') { 'low' } else { 'medium' })
    }
}
