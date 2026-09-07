function Write-LibreSpotAssetCacheFileAtomically {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Writer,

        [scriptblock]$Validator
    )

    if ([string]::IsNullOrWhiteSpace($DestinationPath)) {
        throw 'The asset-cache destination path is required.'
    }

    $resolvedDestination = [System.IO.Path]::GetFullPath($DestinationPath)
    $parent = [System.IO.Path]::GetDirectoryName($resolvedDestination)
    if ([string]::IsNullOrWhiteSpace($parent)) {
        throw 'The asset-cache destination path has no parent directory.'
    }
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -Path $parent -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }

    $existingDestination = Get-Item -LiteralPath $resolvedDestination -Force -ErrorAction SilentlyContinue
    if ($existingDestination -and (($existingDestination.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw "The asset-cache destination is a reparse point: $resolvedDestination"
    }

    $temporaryPath = Join-Path $parent ('.asset-cache-write-' + [guid]::NewGuid().ToString('N') + '.tmp')
    $stream = $null
    try {
        $stream = [System.IO.File]::Open(
            $temporaryPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
        try {
            & $Writer $stream
            $stream.Flush($true)
        } finally {
            $stream.Dispose()
            $stream = $null
        }

        if ($null -ne $Validator) {
            & $Validator $temporaryPath
        }

        if ([System.IO.File]::Exists($resolvedDestination)) {
            $backupPath = $temporaryPath + '.bak'
            try {
                [System.IO.File]::Replace($temporaryPath, $resolvedDestination, $backupPath, $true)
            } finally {
                if ([System.IO.File]::Exists($backupPath)) {
                    try { [System.IO.File]::Delete($backupPath) } catch {}
                }
            }
        } else {
            [System.IO.File]::Move($temporaryPath, $resolvedDestination)
        }
        $temporaryPath = $null
    } finally {
        if ($null -ne $stream) {
            try { $stream.Dispose() } catch {}
        }
        if ($temporaryPath -and [System.IO.File]::Exists($temporaryPath)) {
            try { [System.IO.File]::Delete($temporaryPath) } catch {}
        }
    }
}
