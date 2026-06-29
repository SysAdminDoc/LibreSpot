[CmdletBinding()]
param(
    [string]$HostName = 'admin@PC-42',
    [string]$LibreSpotCliPath = 'C:\ProgramData\LibreSpot\LibreSpot.Cli.exe'
)

$ErrorActionPreference = 'Stop'

# CLI: LibreSpot.Cli.exe detect --json
ssh $HostName "$LibreSpotCliPath detect --json"
exit $LASTEXITCODE
