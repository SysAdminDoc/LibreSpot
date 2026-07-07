function Open-VerifiedScriptForExecution {
    param(
        [string]$FilePath,
        [string]$ExpectedHash = '',
        [string]$Label = 'script'
    )

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        throw "No script path was provided for $Label."
    }

    $fullPath = [System.IO.Path]::GetFullPath($FilePath)
    $stream = $null
    try {
        $stream = [System.IO.File]::Open($fullPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHash)) {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $actualHash = -join ($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') })
            } finally {
                if ($sha) { $sha.Dispose() }
            }

            if ($actualHash -ne $ExpectedHash.ToLowerInvariant()) {
                throw "$Label hash mismatch immediately before execution. Expected $ExpectedHash, got $actualHash. Refusing to run."
            }

            if ($stream.CanSeek) {
                $stream.Position = 0
            }
        }

        return $stream
    } catch {
        if ($stream) { $stream.Dispose() }
        throw
    }
}
