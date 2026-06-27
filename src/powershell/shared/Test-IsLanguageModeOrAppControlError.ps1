function Test-IsLanguageModeOrAppControlError {
    param([string]$Message)
    if ([string]::IsNullOrWhiteSpace($Message)) {
        try { return ([string]$ExecutionContext.SessionState.LanguageMode -eq 'ConstrainedLanguage') } catch { return $false }
    }
    return ($Message -match 'ConstrainedLanguage|language mode|AppLocker|Application Control|\bWDAC\b')
}
