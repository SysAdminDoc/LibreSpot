[CmdletBinding()]
param(
    [string]$ComputerName = 'PC-42',
    [string]$LibreSpotCliPath = 'C:\ProgramData\LibreSpot\LibreSpot.Cli.exe',
    [string]$AnswerFile = 'C:\ProgramData\LibreSpot\librespot-answer.json',
    [string]$ProfileName = 'standard'
)

$ErrorActionPreference = 'Stop'

# CLI: LibreSpot.Cli.exe reapply --answer-file C:\ProgramData\LibreSpot\librespot-answer.json --profile standard --silent --yes --no-restart --ndjson
$remoteCommand = @(
    "'$LibreSpotCliPath'",
    'reapply',
    '--answer-file',
    "'$AnswerFile'",
    '--profile',
    $ProfileName,
    '--silent',
    '--yes',
    '--no-restart',
    '--ndjson'
) -join ' '

Invoke-Command -ComputerName $ComputerName -ScriptBlock ([scriptblock]::Create($remoteCommand))
