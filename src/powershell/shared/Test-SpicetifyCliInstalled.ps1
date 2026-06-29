function Test-SpicetifyCliInstalled {
    return (Test-Path -LiteralPath (Get-SpicetifyIntegrationContext).CliPath)
}
