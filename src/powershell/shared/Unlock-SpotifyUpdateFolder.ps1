function Unlock-SpotifyUpdateFolder {
    $updateDir = Join-Path $env:LOCALAPPDATA "Spotify\Update"
    if (-not (Test-Path $updateDir -PathType Container)) { return }
    try {
        $acl = Get-Acl $updateDir
        $changed = $false
        foreach ($rule in $acl.Access) {
            if ($rule.AccessControlType -eq 'Deny') {
                $null = $acl.RemoveAccessRule($rule); $changed = $true
            }
        }
        if ($changed) { Set-Acl $updateDir $acl; Write-Log "Unlocked Update folder ACLs." }
    } catch { Write-Log "Could not unlock Update folder: $($_.Exception.Message)" -Level 'WARN' }
}
