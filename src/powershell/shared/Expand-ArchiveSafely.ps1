function Expand-ArchiveSafely { param([string]$ZipPath,[string]$DestinationPath,[string]$Label='archive',[int]$MaxEntries=10000,[long]$MaxExpandedBytes=500MB)
    Add-Type -AssemblyName System.IO.Compression
    $zip = $null
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
        if ($zip.Entries.Count -gt $MaxEntries) {
            throw "Archive '$Label' contains $($zip.Entries.Count) entries (limit $MaxEntries)."
        }
        $totalDeclaredBytes = 0L
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $normalized = $name.Replace('/', '\')
            if ([System.IO.Path]::IsPathRooted($normalized)) {
                throw "Archive '$Label' contains an absolute path entry: $name"
            }
            if ($normalized.Contains('..\') -or $normalized.StartsWith('..') -or $normalized.EndsWith('..')) {
                throw "Archive '$Label' contains a path traversal entry: $name"
            }
            $fullTarget = [System.IO.Path]::GetFullPath((Join-Path $DestinationPath $normalized))
            $fullDest = [System.IO.Path]::GetFullPath($DestinationPath).TrimEnd('\') + '\'
            if (-not $fullTarget.StartsWith($fullDest, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Archive '$Label' entry escapes destination: $name"
            }
            $totalDeclaredBytes += $entry.Length
            if ($totalDeclaredBytes -gt $MaxExpandedBytes) {
                throw "Archive '$Label' declared expanded size exceeds limit ($([math]::Round($MaxExpandedBytes / 1MB))MB)."
            }
        }
    } finally {
        if ($zip) { $zip.Dispose() }
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $DestinationPath)
}
