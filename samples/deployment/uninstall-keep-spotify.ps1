[CmdletBinding()]
param(
    [string]$LibreSpotCliPath = 'C:\ProgramData\LibreSpot\LibreSpot.Cli.exe',
    [string]$LogDir = 'C:\ProgramData\LibreSpot\logs'
)

$ErrorActionPreference = 'Stop'

# CLI: LibreSpot.Cli.exe uninstall --silent --yes --keep-spotify --ndjson --log-dir C:\ProgramData\LibreSpot\logs
& $LibreSpotCliPath uninstall --silent --yes --keep-spotify --ndjson --log-dir $LogDir
exit $LASTEXITCODE
