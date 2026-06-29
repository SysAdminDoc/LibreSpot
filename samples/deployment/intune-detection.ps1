[CmdletBinding()]
param(
    [string]$LibreSpotCliPath = 'C:\ProgramData\LibreSpot\LibreSpot.Cli.exe'
)

$ErrorActionPreference = 'Stop'

# CLI: LibreSpot.Cli.exe detect --intune
& $LibreSpotCliPath detect --intune
exit $LASTEXITCODE
