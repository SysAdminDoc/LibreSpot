function New-LibreSpotTempFile {
    param([string]$Name)

    $fileName = if ([string]::IsNullOrWhiteSpace($Name)) { 'artifact.tmp' } else { $Name }
    return (Join-Path (Get-LibreSpotTempRoot) ("{0}-{1}" -f [Guid]::NewGuid().ToString('N'), $fileName))
}
