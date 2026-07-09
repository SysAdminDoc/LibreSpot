[CmdletBinding()]
param(
    [string]$ComputerName = 'PC-42',
    [string]$LibreSpotCliPath = 'C:\ProgramData\LibreSpot\LibreSpot.Cli.exe',
    [string]$AnswerFile = 'C:\ProgramData\LibreSpot\librespot-answer.json',
    [string]$ProfileName = 'standard'
)

$ErrorActionPreference = 'Stop'

# CLI: LibreSpot.Cli.exe reapply --answer-file C:\ProgramData\LibreSpot\librespot-answer.json --profile standard --silent --yes --no-restart --ndjson
$exitCode = Invoke-Command -ComputerName $ComputerName -ScriptBlock {
    param($CliPath, $Answer, $Profile)
    & $CliPath reapply --answer-file $Answer --profile $Profile --silent --yes --no-restart --ndjson
    $LASTEXITCODE
} -ArgumentList $LibreSpotCliPath, $AnswerFile, $ProfileName

exit [int]$exitCode
