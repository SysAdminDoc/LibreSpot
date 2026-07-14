function Write-LibreSpotFileDurable {
    param([Parameter(Mandatory)][string]$Path, [AllowEmptyString()][string]$Content)

    $directory = Split-Path -Path $Path -Parent
    if ($directory -and -not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -Path $directory -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $stream = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    $writer = New-Object System.IO.StreamWriter($stream, $utf8)
    try {
        $writer.Write($Content)
        $writer.Flush()
        $stream.Flush($true)
    } finally {
        $writer.Dispose()
    }
}
