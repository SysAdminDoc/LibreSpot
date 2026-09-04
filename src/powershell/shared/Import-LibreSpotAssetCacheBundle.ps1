function Import-LibreSpotAssetCacheBundle {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BundlePath,

        [Parameter(DontShow = $true)]
        [scriptblock]$AfterBackupMove
    )

    $maxManifestBytes = 4MB
    $maxIndexBytes = 4MB
    $maxEntryCount = 2048
    $maxAssetBytes = 1GB
    $maxBundleBytes = 4GB
    $requirement = "Spotify itself is not stored in LibreSpot's asset cache. SpotX's Spotify installer chain still needs access to Spotify's vendor download."
    $resolvedBundle = [System.IO.Path]::GetFullPath($BundlePath)
    $resolvedCache = [System.IO.Path]::GetFullPath($global:CACHE_DIR).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if ($resolvedBundle.Equals($resolvedCache, [System.StringComparison]::OrdinalIgnoreCase) -or
        $resolvedBundle.StartsWith(($resolvedCache + [System.IO.Path]::DirectorySeparatorChar), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The imported bundle must be stored outside the target asset-cache directory.'
    }
    if (-not (Test-Path -LiteralPath $resolvedBundle -PathType Leaf)) {
        throw "Asset-cache bundle not found: $resolvedBundle"
    }

    if (-not (Test-Path -LiteralPath $global:CONFIG_DIR -PathType Container)) {
        New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
    }
    $stagingRoot = Join-Path $global:CONFIG_DIR ('.asset-cache-import-' + [guid]::NewGuid().ToString('N'))
    $replacementRoot = Join-Path $global:CONFIG_DIR ('.asset-cache-ready-' + [guid]::NewGuid().ToString('N'))
    $rollbackRoot = Join-Path $global:CONFIG_DIR ('.asset-cache-rollback-' + [guid]::NewGuid().ToString('N'))
    New-Item -Path $stagingRoot -ItemType Directory -Force | Out-Null

    $archive = $null
    $file = $null
    try {
        Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
        $file = [System.IO.File]::Open($resolvedBundle, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        $archive = [System.IO.Compression.ZipArchive]::new($file, [System.IO.Compression.ZipArchiveMode]::Read, $false, [System.Text.Encoding]::UTF8)
        if ($archive.Entries.Count -gt ($maxEntryCount + 1)) {
            throw "The bundle contains too many ZIP entries. Maximum: $($maxEntryCount + 1)."
        }

        $manifestEntries = @($archive.Entries | Where-Object { $_.FullName -ceq 'manifest.json' })
        if ($manifestEntries.Count -ne 1) {
            throw 'The bundle must contain exactly one manifest.json entry.'
        }
        $manifestEntry = $manifestEntries[0]
        if ($manifestEntry.Length -le 0 -or $manifestEntry.Length -gt $maxManifestBytes) {
            throw "The bundle manifest must be between 1 and $maxManifestBytes bytes."
        }
        $manifestStream = $manifestEntry.Open()
        try {
            $reader = [System.IO.StreamReader]::new($manifestStream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
            try {
                $manifestJson = $reader.ReadToEnd()
            } finally {
                $reader.Dispose()
            }
        } finally {
            $manifestStream.Dispose()
        }
        try {
            $manifest = $manifestJson | ConvertFrom-Json -ErrorAction Stop
        } catch {
            throw "The bundle manifest is malformed: $($_.Exception.Message)"
        }

        if ([int]$manifest.schemaVersion -ne 1 -or [string]$manifest.bundleType -cne 'librespot-asset-cache') {
            throw 'The bundle manifest type or schema version is not supported.'
        }
        $productVersion = [string]$manifest.productVersion
        if ([string]::IsNullOrWhiteSpace($productVersion) -or $productVersion.Length -gt 64) {
            throw 'The bundle manifest has an invalid product version.'
        }

        $entries = @($manifest.entries)
        if ($entries.Count -eq 0 -or $entries.Count -gt $maxEntryCount -or [int]$manifest.entryCount -ne $entries.Count) {
            throw "The bundle manifest must declare between 1 and $maxEntryCount entries and a matching entryCount."
        }
        if (@($manifest.externalRequirements | Where-Object { [string]$_.id -ceq 'spotify-installer' }).Count -eq 0) {
            throw "The bundle manifest does not disclose Spotify's external installer requirement."
        }

        $expectedNames = @{
            'manifest.json' = $true
        }
        $seenHashes = @{}
        $normalizedEntries = @()
        [int64]$totalBytes = 0
        foreach ($entry in ($entries | Sort-Object sha256)) {
            $hash = [string]$entry.sha256
            if ($hash -cnotmatch '\A[0-9a-f]{64}\z') {
                throw 'The bundle manifest contains an invalid SHA256 value.'
            }
            if ($seenHashes.ContainsKey($hash)) {
                throw "The bundle manifest contains duplicate asset $hash."
            }
            $seenHashes[$hash] = $true

            $label = [string]$entry.label
            $sourceUrl = if ($null -eq $entry.sourceUrl) { $null } else { [string]$entry.sourceUrl }
            if ([string]::IsNullOrWhiteSpace($label) -or $label.Length -gt 256 -or ($null -ne $sourceUrl -and $sourceUrl.Length -gt 2048)) {
                throw "Asset $hash has invalid label or source metadata."
            }
            [datetimeoffset]$verifiedAt = [datetimeoffset]::MinValue
            $lastVerifiedAtUtc = if ($null -eq $entry.lastVerifiedAtUtc) { '' } else { [string]$entry.lastVerifiedAtUtc }
            $hasVerifiedTimestamp = [datetimeoffset]::TryParse(
                $lastVerifiedAtUtc,
                [System.Globalization.CultureInfo]::InvariantCulture,
                [System.Globalization.DateTimeStyles]::RoundtripKind,
                [ref]$verifiedAt)
            if ([string]$entry.status -cne 'present' -or -not $hasVerifiedTimestamp -or
                -not [string]::IsNullOrWhiteSpace([string]$entry.quarantinedPath)) {
                throw "Asset $hash is not a verified present entry."
            }
            [int64]$byteSize = $entry.byteSize
            if ($byteSize -lt 0 -or $byteSize -gt $maxAssetBytes -or $totalBytes -gt ($maxBundleBytes - $byteSize)) {
                throw "Asset $hash exceeds the supported bundle safety limit."
            }
            $totalBytes += $byteSize
            $assetName = "assets/$hash"
            $expectedNames[$assetName] = $true
            $normalizedEntries += [pscustomobject][ordered]@{
                sha256            = $hash
                label             = $label
                sourceUrl         = $sourceUrl
                byteSize          = $byteSize
                firstSeenAtUtc    = if ($null -eq $entry.firstSeenAtUtc) { $null } else { [string]$entry.firstSeenAtUtc }
                lastUsedAtUtc     = if ($null -eq $entry.lastUsedAtUtc) { $null } else { [string]$entry.lastUsedAtUtc }
                lastVerifiedAtUtc = $null
                status            = 'present'
                quarantinedPath   = $null
            }
        }
        if ([int64]$manifest.totalBytes -ne $totalBytes) {
            throw "The bundle manifest totalBytes is $($manifest.totalBytes), expected $totalBytes."
        }

        $seenNames = @{}
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName
            if ($seenNames.ContainsKey($name)) {
                throw "The bundle contains duplicate ZIP entry '$name'."
            }
            $seenNames[$name] = $true
            if (-not $expectedNames.ContainsKey($name)) {
                throw "The bundle contains unexpected ZIP entry '$name'."
            }
            $unixFileType = (($entry.ExternalAttributes -shr 16) -band 0xF000)
            if ($unixFileType -eq 0xA000) {
                throw "ZIP entry '$name' is a symbolic link."
            }
        }
        if ($seenNames.Count -ne $expectedNames.Count) {
            throw 'The bundle does not contain exactly the assets declared by its manifest.'
        }

        foreach ($entry in $normalizedEntries) {
            $name = "assets/$($entry.sha256)"
            $zipEntries = @($archive.Entries | Where-Object { $_.FullName -ceq $name })
            if ($zipEntries.Count -ne 1) {
                throw "The bundle is missing asset $($entry.sha256)."
            }
            $zipEntry = $zipEntries[0]
            if ($zipEntry.Length -ne $entry.byteSize) {
                throw "Asset $($entry.sha256) has size $($zipEntry.Length), expected $($entry.byteSize)."
            }
            $stagedPath = Join-Path $stagingRoot $entry.sha256
            $source = $zipEntry.Open()
            $destination = [System.IO.File]::Open($stagedPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            try {
                $source.CopyTo($destination)
            } finally {
                $destination.Dispose()
                $source.Dispose()
            }
            $actualHash = Get-FileSha256Lower -Path $stagedPath
            if ($actualHash -cne $entry.sha256) {
                throw "Asset $($entry.sha256) failed SHA256 verification. Observed $actualHash."
            }
        }

        $indexPath = Join-Path $global:CACHE_DIR 'asset-cache-index.json'
        $existingEntries = @()
        if (Test-Path -LiteralPath $indexPath -PathType Leaf) {
            $indexInfo = Get-Item -LiteralPath $indexPath -Force
            if ($indexInfo.Length -le 0 -or $indexInfo.Length -gt $maxIndexBytes) {
                throw "The existing asset-cache index must be between 1 and $maxIndexBytes bytes."
            }
            try {
                $existingIndex = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
            } catch {
                throw "The existing asset-cache index is malformed: $($_.Exception.Message)"
            }
            if ([int]$existingIndex.schemaVersion -ne 1) {
                throw 'The existing asset-cache index uses an unsupported schema version.'
            }
            $existingEntries = @($existingIndex.entries)
            if ($existingEntries.Count -gt $maxEntryCount) {
                throw 'The existing asset-cache index contains too many entries.'
            }
        }

        $now = (Get-Date).ToUniversalTime().ToString('o')
        $importedHashes = @{}
        foreach ($entry in $normalizedEntries) {
            $entry.lastVerifiedAtUtc = $now
            $importedHashes[$entry.sha256] = $true
        }
        $mergedEntries = @($existingEntries | Where-Object { -not $importedHashes.ContainsKey(([string]$_.sha256).ToLowerInvariant()) }) + @($normalizedEntries)
        $mergedEntries = @($mergedEntries | Sort-Object sha256)
        if ($mergedEntries.Count -gt $maxEntryCount) {
            throw 'The merged asset-cache index would contain too many entries.'
        }

        if (Test-Path -LiteralPath $resolvedCache -PathType Leaf) {
            throw 'The target asset-cache path is a file, not a directory.'
        }

        New-Item -Path $replacementRoot -ItemType Directory -Force | Out-Null
        if (Test-Path -LiteralPath $resolvedCache -PathType Container) {
            $cacheRootItem = Get-Item -LiteralPath $resolvedCache -Force
            if (($cacheRootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'The target asset-cache directory is a reparse point and cannot be imported safely.'
            }

            $pendingDirectories = [System.Collections.Generic.Queue[object]]::new()
            $pendingDirectories.Enqueue([pscustomobject]@{ Source = $resolvedCache; Destination = $replacementRoot })
            while ($pendingDirectories.Count -gt 0) {
                $directoryPair = $pendingDirectories.Dequeue()
                foreach ($child in @(Get-ChildItem -LiteralPath $directoryPair.Source -Force -ErrorAction Stop)) {
                    if (($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                        throw "The target asset cache contains a reparse point: $($child.FullName)"
                    }
                    $destinationPath = Join-Path $directoryPair.Destination $child.Name
                    if ($child.PSIsContainer) {
                        New-Item -Path $destinationPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
                        $pendingDirectories.Enqueue([pscustomobject]@{ Source = $child.FullName; Destination = $destinationPath })
                    } else {
                        [System.IO.File]::Copy($child.FullName, $destinationPath, $false)
                    }
                }
            }
        }

        foreach ($entry in $normalizedEntries) {
            [System.IO.File]::Copy((Join-Path $stagingRoot $entry.sha256), (Join-Path $replacementRoot $entry.sha256), $true)
        }

        $indexDocument = [ordered]@{
            schemaVersion  = 1
            generatedAtUtc = $now
            entries        = @($mergedEntries)
        }
        $replacementIndex = Join-Path $replacementRoot 'asset-cache-index.json'
        [System.IO.File]::WriteAllText($replacementIndex, ($indexDocument | ConvertTo-Json -Depth 8), [System.Text.UTF8Encoding]::new($false))

        $archive.Dispose()
        $archive = $null
        $file.Dispose()
        $file = $null

        $originalMoved = $false
        try {
            if (Test-Path -LiteralPath $resolvedCache -PathType Container) {
                [System.IO.Directory]::Move($resolvedCache, $rollbackRoot)
                $originalMoved = $true
                if ($null -ne $AfterBackupMove) {
                    & $AfterBackupMove
                }
            }
            [System.IO.Directory]::Move($replacementRoot, $resolvedCache)
        } catch {
            $commitError = $_
            if ($originalMoved) {
                try {
                    if (Test-Path -LiteralPath $resolvedCache) {
                        throw 'The failed import left the target cache path occupied.'
                    }
                    [System.IO.Directory]::Move($rollbackRoot, $resolvedCache)
                } catch {
                    throw "Asset-cache commit failed and automatic rollback also failed. The original cache is retained at $rollbackRoot. Commit error: $($commitError.Exception.Message) Rollback error: $($_.Exception.Message)"
                }
            }
            throw $commitError
        }

        if ($originalMoved -and (Test-Path -LiteralPath $rollbackRoot -PathType Container)) {
            Remove-Item -LiteralPath $rollbackRoot -Recurse -Force -ErrorAction SilentlyContinue
        }

        return [pscustomobject][ordered]@{
            Path                  = $resolvedBundle
            EntryCount            = $normalizedEntries.Count
            TotalBytes            = $totalBytes
            ProductVersion        = $productVersion
            ExternalRequirementId = 'spotify-installer'
            ExternalRequirement   = $requirement
        }
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        if ($null -ne $file) { $file.Dispose() }
        if (Test-Path -LiteralPath $stagingRoot -PathType Container) {
            Remove-Item -LiteralPath $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path -LiteralPath $replacementRoot -PathType Container) {
            Remove-Item -LiteralPath $replacementRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}
