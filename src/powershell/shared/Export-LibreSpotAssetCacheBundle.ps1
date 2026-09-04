function Export-LibreSpotAssetCacheBundle {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,

        [string]$ProductVersion = 'unknown'
    )

    $maxIndexBytes = 4MB
    $maxEntryCount = 2048
    $maxAssetBytes = 1GB
    $maxBundleBytes = 4GB
    $requirement = "Spotify itself is not stored in LibreSpot's asset cache. SpotX's Spotify installer chain still needs access to Spotify's vendor download."
    $indexPath = Join-Path $global:CACHE_DIR 'asset-cache-index.json'
    if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
        throw 'The asset cache has no index. Run an online LibreSpot install before exporting it.'
    }

    $indexInfo = Get-Item -LiteralPath $indexPath -Force
    if ($indexInfo.Length -le 0 -or $indexInfo.Length -gt $maxIndexBytes) {
        throw "The asset-cache index must be between 1 and $maxIndexBytes bytes."
    }

    try {
        $index = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "The asset-cache index is malformed: $($_.Exception.Message)"
    }

    if ([int]$index.schemaVersion -ne 1) {
        throw 'The asset-cache index uses an unsupported schema version.'
    }

    $indexedEntries = @($index.entries)
    if ($indexedEntries.Count -eq 0 -or $indexedEntries.Count -gt $maxEntryCount) {
        throw "The asset-cache index must contain between 1 and $maxEntryCount entries."
    }

    $seen = @{}
    $verifiedEntries = @()
    [int64]$totalBytes = 0
    foreach ($entry in ($indexedEntries | Sort-Object sha256)) {
        $hash = ([string]$entry.sha256).ToLowerInvariant()
        if ($hash -notmatch '\A[0-9a-f]{64}\z') {
            throw 'The asset-cache index contains an invalid SHA256 value.'
        }
        if ($seen.ContainsKey($hash)) {
            throw "The asset-cache index contains duplicate asset $hash."
        }
        $seen[$hash] = $true

        $label = [string]$entry.label
        if ([string]::IsNullOrWhiteSpace($label) -or $label.Length -gt 256) {
            throw "Asset $hash has an invalid label."
        }
        $sourceUrl = if ($null -eq $entry.sourceUrl) { $null } else { [string]$entry.sourceUrl }
        if ($null -ne $sourceUrl -and $sourceUrl.Length -gt 2048) {
            throw "Asset $hash has invalid source metadata."
        }
        if ([string]$entry.status -ne 'present' -or [string]::IsNullOrWhiteSpace([string]$entry.lastVerifiedAtUtc)) {
            throw "The asset cache is incomplete. $hash ($label) is not a verified present entry."
        }

        [int64]$expectedBytes = $entry.byteSize
        if ($expectedBytes -lt 0 -or $expectedBytes -gt $maxAssetBytes -or $totalBytes -gt ($maxBundleBytes - $expectedBytes)) {
            throw 'The asset cache exceeds the supported bundle safety limit.'
        }
        $assetPath = Join-Path $global:CACHE_DIR $hash
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            throw "The asset cache is incomplete. Missing $hash ($label)."
        }
        $assetInfo = Get-Item -LiteralPath $assetPath -Force
        if (($assetInfo.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Cached asset $hash is a reparse point and cannot be exported."
        }
        if ($assetInfo.Length -ne $expectedBytes) {
            throw "Cached asset $hash has size $($assetInfo.Length), expected $expectedBytes."
        }
        $actualHash = Get-FileSha256Lower -Path $assetPath
        if ($actualHash -ne $hash) {
            throw "Cached asset $hash failed SHA256 verification. Observed $actualHash."
        }

        $totalBytes += $expectedBytes
        $verifiedEntries += [pscustomobject][ordered]@{
            sha256            = $hash
            label             = $label
            sourceUrl         = $sourceUrl
            byteSize          = $expectedBytes
            firstSeenAtUtc    = if ($null -eq $entry.firstSeenAtUtc) { $null } else { [string]$entry.firstSeenAtUtc }
            lastUsedAtUtc     = if ($null -eq $entry.lastUsedAtUtc) { $null } else { [string]$entry.lastUsedAtUtc }
            lastVerifiedAtUtc = [string]$entry.lastVerifiedAtUtc
            status            = 'present'
            quarantinedPath   = $null
        }
    }

    $manifest = [ordered]@{
        schemaVersion        = 1
        bundleType           = 'librespot-asset-cache'
        productVersion       = $ProductVersion
        generatedAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
        entryCount           = $verifiedEntries.Count
        totalBytes           = $totalBytes
        entries              = @($verifiedEntries)
        externalRequirements = @(
            [ordered]@{
                id     = 'spotify-installer'
                reason = $requirement
            }
        )
    }

    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
    $resolvedCache = [System.IO.Path]::GetFullPath($global:CACHE_DIR).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if ($resolvedOutput.Equals($resolvedCache, [System.StringComparison]::OrdinalIgnoreCase) -or
        $resolvedOutput.StartsWith(($resolvedCache + [System.IO.Path]::DirectorySeparatorChar), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The exported bundle must be written outside the asset-cache directory.'
    }
    $outputDirectory = Split-Path -Path $resolvedOutput -Parent
    if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
        throw 'The bundle output path has no parent directory.'
    }
    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
        New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null
    }

    $temporaryPath = Join-Path $outputDirectory ('.' + [System.IO.Path]::GetFileName($resolvedOutput) + '.' + [guid]::NewGuid().ToString('N') + '.tmp')
    $backupPath = "$temporaryPath.bak"
    $archive = $null
    $file = $null
    try {
        Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
        $file = [System.IO.File]::Open($temporaryPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $archive = [System.IO.Compression.ZipArchive]::new($file, [System.IO.Compression.ZipArchiveMode]::Create, $false, [System.Text.Encoding]::UTF8)

        $manifestEntry = $archive.CreateEntry('manifest.json', [System.IO.Compression.CompressionLevel]::Optimal)
        $manifestStream = $manifestEntry.Open()
        try {
            $manifestBytes = [System.Text.UTF8Encoding]::new($false).GetBytes(($manifest | ConvertTo-Json -Depth 8))
            $manifestStream.Write($manifestBytes, 0, $manifestBytes.Length)
        } finally {
            $manifestStream.Dispose()
        }

        foreach ($entry in $verifiedEntries) {
            $zipEntry = $archive.CreateEntry("assets/$($entry.sha256)", [System.IO.Compression.CompressionLevel]::Optimal)
            $source = [System.IO.File]::OpenRead((Join-Path $global:CACHE_DIR $entry.sha256))
            $destination = $zipEntry.Open()
            try {
                $source.CopyTo($destination)
            } finally {
                $destination.Dispose()
                $source.Dispose()
            }
        }

        $archive.Dispose()
        $archive = $null
        $file = $null
        if (Test-Path -LiteralPath $resolvedOutput -PathType Leaf) {
            [System.IO.File]::Replace($temporaryPath, $resolvedOutput, $backupPath, $true)
        } else {
            [System.IO.File]::Move($temporaryPath, $resolvedOutput)
        }

        return [pscustomobject][ordered]@{
            Path                  = $resolvedOutput
            EntryCount            = $verifiedEntries.Count
            TotalBytes            = $totalBytes
            ProductVersion        = $ProductVersion
            ExternalRequirementId = 'spotify-installer'
            ExternalRequirement   = $requirement
        }
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        if ($null -ne $file) { $file.Dispose() }
        foreach ($path in @($temporaryPath, $backupPath)) {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
        }
    }
}
