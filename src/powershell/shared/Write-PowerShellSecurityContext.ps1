function Write-PowerShellSecurityContext {
    if ($global:PsSecurityContextLogged) { return }
    $global:PsSecurityContextLogged = $true
    try {
        $ctx = Get-PowerShellSecurityContext
        Write-Log "PowerShell context: $($ctx.Edition) $($ctx.Version); language mode $($ctx.LanguageMode); execution policy [$($ctx.ExecutionPolicies)]."
        Write-PowerShell7SecurityFloorWarningIfNeeded
        if ($ctx.AppControlEnforced) {
            Write-Log "This host enforces ConstrainedLanguage mode (AppLocker, Windows Defender Application Control, or Smart App Control). LibreSpot's scripts may be blocked. This is a platform-level control, not a LibreSpot error, and -ExecutionPolicy Bypass does not bypass it. Do not disable or bypass application control for LibreSpot. On managed devices, ask your administrator whether an approved LibreSpot artifact is allowed. On personal devices, leave Smart App Control enabled and follow official Windows Security guidance." -Level 'WARN'
        }
    } catch {}
}
