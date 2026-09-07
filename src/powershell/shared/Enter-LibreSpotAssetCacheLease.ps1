function Enter-LibreSpotAssetCacheLease {
    [CmdletBinding()]
    param(
        [string]$CacheDirectory = $global:CACHE_DIR,
        [string]$Label = 'asset-cache',
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 30,
        [ValidateRange(10, 5000)][int]$RetryMilliseconds = 50
    )

    if ([string]::IsNullOrWhiteSpace($CacheDirectory)) {
        throw 'LibreSpot could not resolve the asset-cache directory for its shared lease.'
    }

    $resolvedCache = [System.IO.Path]::GetFullPath($CacheDirectory)
    $currentBoundary = $resolvedCache
    while (-not [string]::IsNullOrWhiteSpace($currentBoundary)) {
        $boundaryItem = Get-Item -LiteralPath $currentBoundary -Force -ErrorAction SilentlyContinue
        if ($boundaryItem) {
            if (($boundaryItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "The asset-cache path boundary is a reparse point and cannot be used safely: $currentBoundary"
            }
            if (-not $boundaryItem.PSIsContainer) {
                throw "The asset-cache path boundary is a file, not a directory: $currentBoundary"
            }
        }
        $nextBoundary = [System.IO.Path]::GetDirectoryName($currentBoundary)
        if ([string]::IsNullOrWhiteSpace($nextBoundary) -or $nextBoundary -eq $currentBoundary) {
            break
        }
        $currentBoundary = $nextBoundary
    }
    $cacheItem = Get-Item -LiteralPath $resolvedCache -Force -ErrorAction SilentlyContinue
    if ($cacheItem) {
        if (($cacheItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw 'The asset-cache directory is a reparse point and cannot be used safely.'
        }
        if (-not $cacheItem.PSIsContainer) {
            throw 'The asset-cache path is a file, not a directory.'
        }
    }
    $parent = [System.IO.Path]::GetDirectoryName($resolvedCache)
    if ([string]::IsNullOrWhiteSpace($parent)) {
        throw 'LibreSpot could not resolve the asset-cache parent directory for its shared lease.'
    }
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -Path $parent -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    $parentItem = Get-Item -LiteralPath $parent -Force -ErrorAction Stop
    if (($parentItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "The asset-cache parent is a reparse point and cannot be used safely: $parent"
    }

    $leasePath = Join-Path $parent '.asset-cache.lock'
    if ($null -eq $global:LibreSpotAssetCacheLeases) {
        $global:LibreSpotAssetCacheLeases = @{}
    }
    $existing = $global:LibreSpotAssetCacheLeases[$leasePath]
    if ($null -ne $existing) {
        $existing.Depth = [int]$existing.Depth + 1
        return [pscustomobject]@{ Key = $leasePath; Reentrant = $true; Label = $Label }
    }

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $stream = $null
    while ($null -eq $stream) {
        try {
            $stream = [System.IO.File]::Open(
                $leasePath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
        } catch [System.IO.IOException] {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "LIBRESPOT_ASSET_CACHE_BUSY: Another LibreSpot operation is using the asset cache. '$Label' was deferred without changing cached assets."
            }
            Start-Sleep -Milliseconds $RetryMilliseconds
        } catch {
            throw "LibreSpot could not open its shared asset-cache lease: $($_.Exception.Message)"
        }
    }

    $global:LibreSpotAssetCacheLeases[$leasePath] = [pscustomobject]@{
        Stream = $stream
        Depth = 1
    }
    return [pscustomobject]@{ Key = $leasePath; Reentrant = $false; Label = $Label }
}
