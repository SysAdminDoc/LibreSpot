function Unlock-SpotifyUpdateFolder {
    $updateDir = Join-Path $env:LOCALAPPDATA "Spotify\Update"
    if (-not (Test-Path $updateDir -PathType Container)) { return }
    try {
        $acl = Get-Acl $updateDir
        $changed = $false
        # Snapshot the Deny rules before mutating: RemoveAccessRule mutates the
        # underlying collection, so iterating $acl.Access directly throws
        # "collection modified" on the second Deny ACE -- the exact multi-ACE
        # case this function exists to clear.
        $denyRules = @($acl.Access | Where-Object { $_.AccessControlType -eq 'Deny' })
        foreach ($rule in $denyRules) {
            $null = $acl.RemoveAccessRule($rule); $changed = $true
        }
        if ($changed) { Set-Acl $updateDir $acl; Write-Log "Unlocked Update folder ACLs." }
    } catch { Write-Log "Could not unlock Update folder: $($_.Exception.Message)" -Level 'WARN' }
}
