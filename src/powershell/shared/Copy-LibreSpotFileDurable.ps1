function Copy-LibreSpotFileDurable {
    param([Parameter(Mandatory)][string]$SourcePath, [Parameter(Mandatory)][string]$DestinationPath)

    $source = New-Object System.IO.FileStream($SourcePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    $destination = New-Object System.IO.FileStream($DestinationPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $source.CopyTo($destination)
        $destination.Flush($true)
    } finally {
        $destination.Dispose()
        $source.Dispose()
    }
}
