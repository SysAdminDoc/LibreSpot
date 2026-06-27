function Write-PowerShellSecurityContext {
    if ($global:PsSecurityContextLogged) { return }
    $global:PsSecurityContextLogged = $true
    try {
        $ctx = Get-PowerShellSecurityContext
        Write-Log "PowerShell context: $($ctx.Edition) $($ctx.Version); language mode $($ctx.LanguageMode); execution policy [$($ctx.ExecutionPolicies)]."
        if ($ctx.AppControlEnforced) {
            Write-Log "This host enforces ConstrainedLanguage mode (AppLocker, Windows Defender Application Control, or Smart App Control). LibreSpot's scripts may be blocked. This is a platform-level control, not a LibreSpot error, and -ExecutionPolicy Bypass does not bypass it. On managed devices, ask your administrator to allow LibreSpot/SpotX. On personal devices with Smart App Control (Windows 11), open Settings > Privacy & security > Windows Security > App & browser control > Smart App Control settings to adjust. Alternatively, use the pre-compiled LibreSpot.exe from the Releases page." -Level 'WARN'
        }
    } catch {}
}
