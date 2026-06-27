function New-LibreSpotTempDirectory {
    param([string]$Name = 'workspace')

    $directoryName = if ([string]::IsNullOrWhiteSpace($Name)) { 'workspace' } else { $Name }
    $path = Join-Path (Get-LibreSpotTempRoot) ("{0}-{1}" -f [Guid]::NewGuid().ToString('N'), $directoryName)
    New-Item -Path $path -ItemType Directory -Force | Out-Null
    return $path
}
