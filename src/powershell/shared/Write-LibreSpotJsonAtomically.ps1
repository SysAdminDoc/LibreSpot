function Write-LibreSpotJsonAtomically {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][object]$Document, [int]$Depth = 12)

    $directory = Split-Path -Path $Path -Parent
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -Path $directory -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    $tempPath = Join-Path $directory ("profile-activation.{0}.json.tmp" -f [Guid]::NewGuid().ToString('N'))
    try {
        Write-LibreSpotFileDurable -Path $tempPath -Content ($Document | ConvertTo-Json -Depth $Depth)
        Install-LibreSpotStagedConfig -StagePath $tempPath -DestinationPath $Path
    } finally {
        Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
    }
}
