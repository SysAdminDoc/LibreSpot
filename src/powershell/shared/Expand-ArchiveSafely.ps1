function Expand-ArchiveSafely { param([string]$ZipPath,[string]$DestinationPath,[string]$Label='archive',[int]$MaxEntries=10000,[long]$MaxExpandedBytes=500MB)
    # ZipFile/ZipFileExtensions live in System.IO.Compression.FileSystem on .NET
    # Framework (PS 5.1); loading only System.IO.Compression leaves them
    # unresolvable in a clean powershell.exe process.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $zip = $null
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
        if ($zip.Entries.Count -gt $MaxEntries) {
            throw "Archive '$Label' contains $($zip.Entries.Count) entries (limit $MaxEntries)."
        }
        $fullDest = [System.IO.Path]::GetFullPath($DestinationPath).TrimEnd('\') + '\'
        $totalDeclaredBytes = 0L
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $normalized = $name.Replace('/', '\')
            if ([System.IO.Path]::IsPathRooted($normalized)) {
                throw "Archive '$Label' contains an absolute path entry: $name"
            }
            # Reject only genuine '..' path segments, not legitimate names that
            # merely begin or end with two dots (e.g. '..gitkeep', 'changelog..').
            # The resolved-prefix check below is the authoritative escape guard.
            if ($normalized -split '\\' | Where-Object { $_ -eq '..' }) {
                throw "Archive '$Label' contains a path traversal entry: $name"
            }
            $fullTarget = [System.IO.Path]::GetFullPath((Join-Path $DestinationPath $normalized))
            if (-not $fullTarget.StartsWith($fullDest, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Archive '$Label' entry escapes destination: $name"
            }
            $totalDeclaredBytes += $entry.Length
            if ($totalDeclaredBytes -gt $MaxExpandedBytes) {
                throw "Archive '$Label' declared expanded size exceeds limit ($([math]::Round($MaxExpandedBytes / 1MB))MB)."
            }
        }
        $totalActualBytes = 0L
        $copyBuffer = New-Object byte[] 81920
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $targetPath = [System.IO.Path]::GetFullPath((Join-Path $DestinationPath ($name.Replace('/', '\'))))
            if ($name.EndsWith('/') -or $name.EndsWith('\')) {
                [System.IO.Directory]::CreateDirectory($targetPath) | Out-Null
                continue
            }
            $parentDir = [System.IO.Path]::GetDirectoryName($targetPath)
            if (-not [string]::IsNullOrWhiteSpace($parentDir)) {
                [System.IO.Directory]::CreateDirectory($parentDir) | Out-Null
            }
            $tempTargetPath = "$targetPath.librespot-extract-$([guid]::NewGuid().ToString('N')).tmp"
            $entryStream = $null
            $targetStream = $null
            $entrySucceeded = $false
            try {
                $entryStream = $entry.Open()
                $targetStream = [System.IO.File]::Open($tempTargetPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
                while (($bytesRead = $entryStream.Read($copyBuffer, 0, $copyBuffer.Length)) -gt 0) {
                    $totalActualBytes += $bytesRead
                    if ($totalActualBytes -gt $MaxExpandedBytes) {
                        throw "Archive '$Label' actual expanded size exceeds limit ($([math]::Round($MaxExpandedBytes / 1MB))MB)."
                    }
                    $targetStream.Write($copyBuffer, 0, $bytesRead)
                }
                $entrySucceeded = $true
            } finally {
                if ($targetStream) { $targetStream.Dispose() }
                if ($entryStream) { $entryStream.Dispose() }
                if (-not $entrySucceeded -and (Test-Path -LiteralPath $tempTargetPath -PathType Leaf)) {
                    Remove-Item -LiteralPath $tempTargetPath -Force -ErrorAction SilentlyContinue
                }
            }
            try {
                if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
                    Remove-Item -LiteralPath $targetPath -Force
                }
                [System.IO.File]::Move($tempTargetPath, $targetPath)
            } catch {
                if (Test-Path -LiteralPath $tempTargetPath -PathType Leaf) {
                    Remove-Item -LiteralPath $tempTargetPath -Force -ErrorAction SilentlyContinue
                }
                throw
            }
        }
    } finally {
        if ($zip) { $zip.Dispose() }
    }
}
