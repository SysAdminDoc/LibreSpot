function Install-LibreSpotStagedConfig {
    param([Parameter(Mandatory)][string]$StagePath, [Parameter(Mandatory)][string]$DestinationPath)

    $directory = Split-Path -Path $DestinationPath -Parent
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -Path $directory -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    $tempPath = Join-Path $directory ("profile-activation.{0}.commit.tmp" -f [Guid]::NewGuid().ToString('N'))
    $backupPath = Join-Path $directory ("profile-activation.{0}.commit.bak" -f [Guid]::NewGuid().ToString('N'))
    try {
        Copy-LibreSpotFileDurable -SourcePath $StagePath -DestinationPath $tempPath
        if (Test-Path -LiteralPath $DestinationPath -PathType Leaf) {
            try {
                [System.IO.File]::Replace($tempPath, $DestinationPath, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                $rescuePath = "$DestinationPath.rescue"
                Move-Item -LiteralPath $DestinationPath -Destination $rescuePath -Force -ErrorAction Stop
                try {
                    [System.IO.File]::Move($tempPath, $DestinationPath)
                    Remove-Item -LiteralPath $rescuePath -Force -ErrorAction SilentlyContinue
                } catch {
                    Move-Item -LiteralPath $rescuePath -Destination $DestinationPath -Force -ErrorAction SilentlyContinue
                    throw
                }
            }
        } else {
            [System.IO.File]::Move($tempPath, $DestinationPath)
        }
    } finally {
        Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
    }
}
