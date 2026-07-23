function Get-InstalledSpicetifyCliVersion {
    # Best-effort read of the installed Spicetify CLI version via `spicetify -v`.
    # Returns $null when the CLI is absent or the probe fails for any reason;
    # callers must treat $null as 'unknown', never as an error. Never throws.
    try {
        $cliPath = (Get-SpicetifyIntegrationContext).CliPath
    } catch {
        return $null
    }
    if (-not (Test-Path -LiteralPath $cliPath)) { return $null }
    try {
        $output = & $cliPath '-v' 2>$null
        foreach ($line in @($output)) {
            $match = [regex]::Match([string]$line, '\d+(?:\.\d+)+')
            if ($match.Success) { return $match.Value }
        }
    } catch {}
    return $null
}
