function Get-LibreSpotTempRoot {
    $root = Join-Path $global:TEMP_DIR 'LibreSpot'
    if (Test-Path -LiteralPath $root -PathType Leaf) {
        $root = Join-Path $global:TEMP_DIR ("LibreSpot-{0}" -f [System.Diagnostics.Process]::GetCurrentProcess().Id)
    }

    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        New-Item -Path $root -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    return $root
}
