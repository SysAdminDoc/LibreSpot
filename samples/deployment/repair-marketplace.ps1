[CmdletBinding()]
param(
    [string]$LibreSpotCliPath = 'C:\ProgramData\LibreSpot\LibreSpot.Cli.exe',
    [string]$LogDir = 'C:\ProgramData\LibreSpot\logs'
)

$ErrorActionPreference = 'Stop'

# CLI: LibreSpot.Cli.exe repair --repair-id RepairMarketplace --silent --yes --ndjson --log-dir C:\ProgramData\LibreSpot\logs
& $LibreSpotCliPath repair --repair-id RepairMarketplace --silent --yes --ndjson --log-dir $LogDir
exit $LASTEXITCODE
