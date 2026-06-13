# DRAFT — blocked on: code signing, finalized artifact
$ErrorActionPreference = 'Stop'

$packageArgs = @{
    packageName    = 'librespot'
    fileType       = 'exe'
    url64bit       = 'https://github.com/SysAdminDoc/LibreSpot/releases/download/v3.7.2/LibreSpot.exe'
    checksum64     = 'PLACEHOLDER_SHA256'
    checksumType64 = 'sha256'
    softwareName   = 'LibreSpot*'
    silentArgs     = '-Easy'
    validExitCodes = @(0)
}

$toolsDir = "$(Split-Path -Parent $MyInvocation.MyCommand.Definition)"
$exePath  = Join-Path $toolsDir 'LibreSpot.exe'

Get-ChocolateyWebFile @packageArgs -FileFullPath $exePath
