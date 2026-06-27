function Get-PowerShellSecurityContext {
    $ctx = [ordered]@{
        Edition             = [string]$PSVersionTable.PSEdition
        Version             = [string]$PSVersionTable.PSVersion
        LanguageMode        = ''
        ExecutionPolicies   = ''
        ConstrainedLanguage = $false
        AppControlEnforced  = $false
    }
    try { $ctx.LanguageMode = [string]$ExecutionContext.SessionState.LanguageMode } catch {}
    if ($ctx.LanguageMode -eq 'ConstrainedLanguage') {
        $ctx.ConstrainedLanguage = $true
        # CLM is forced by AppLocker, WDAC, or Smart App Control (SAC on Win11).
        $ctx.AppControlEnforced = $true
    }
    try {
        $scopes = Get-ExecutionPolicy -List -ErrorAction Stop |
            ForEach-Object { "$($_.Scope)=$($_.ExecutionPolicy)" }
        $ctx.ExecutionPolicies = ($scopes -join '; ')
    } catch {}
    return [pscustomobject]$ctx
}
