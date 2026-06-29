[CmdletBinding()]
param(
    [string]$LibreSpotCliPath = 'C:\ProgramData\LibreSpot\LibreSpot.Cli.exe',
    [string]$AnswerFile = 'C:\ProgramData\LibreSpot\librespot-answer.json',
    [string]$LogDir = 'C:\ProgramData\LibreSpot\logs'
)

$ErrorActionPreference = 'Stop'

# CLI: LibreSpot.Cli.exe install --answer-file C:\ProgramData\LibreSpot\librespot-answer.json --profile standard --silent --yes --no-restart --ndjson --log-dir C:\ProgramData\LibreSpot\logs
& $LibreSpotCliPath install --answer-file $AnswerFile --profile standard --silent --yes --no-restart --ndjson --log-dir $LogDir
exit $LASTEXITCODE
