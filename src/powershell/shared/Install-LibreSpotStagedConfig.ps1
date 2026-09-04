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
                    $moveError = $_
                    try {
                        Move-Item -LiteralPath $rescuePath -Destination $DestinationPath -Force -ErrorAction Stop
                    } catch {
                        # Silencing this left no config file and no clue where the
                        # old one went. Name the rescue copy so it can be put back
                        # by hand.
                        throw ("Could not install the staged config, and restoring the previous one failed. " +
                            "Your previous configuration is at $rescuePath and has to be moved back to " +
                            "$DestinationPath by hand. Install error: $($moveError.Exception.Message) " +
                            "Restore error: $($_.Exception.Message)")
                    }
                    throw $moveError
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
