function Get-LibreSpotFileFingerprint {
    param([Parameter(Mandatory)][string]$Path, [switch]$MissingAsEmpty)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        if (-not $MissingAsEmpty) { throw "File not found: $Path" }
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            return (($sha.ComputeHash([byte[]]@()) | ForEach-Object { $_.ToString('x2') }) -join '')
        } finally {
            $sha.Dispose()
        }
    }
    return (Get-FileSha256Lower -Path $Path)
}
