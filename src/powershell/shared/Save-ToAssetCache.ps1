function Save-ToAssetCache { param([string]$SourcePath, [string]$SHA256Hash, [string]$Label = '', [string]$SourceUrl = '')
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return }
    $cacheLease = $null
    try {
        $cacheLease = Enter-LibreSpotAssetCacheLease -CacheDirectory $global:CACHE_DIR -Label "asset-cache save: $Label"
        Recover-LibreSpotAssetCacheTransaction -CacheDirectory $global:CACHE_DIR
        if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
            New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
        }
        $cachePath = Join-Path $global:CACHE_DIR $hash
        Write-LibreSpotAssetCacheFileAtomically -DestinationPath $cachePath -Writer {
            param($stream)
            $source = [System.IO.File]::Open(
                $SourcePath,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                [System.IO.FileShare]::Read)
            try {
                $source.CopyTo($stream)
            } finally {
                $source.Dispose()
            }
        } -Validator {
            param($stagedPath)
            $observedHash = Get-FileSha256Lower -Path $stagedPath
            if ($observedHash -cne $hash) {
                throw "Cached asset $hash failed SHA256 verification before publication. Observed $observedHash."
            }
        }
        $byteSize = (Get-Item -LiteralPath $cachePath).Length
        Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -SourceUrl $SourceUrl -ByteSize $byteSize -Status 'present' -MarkVerified -MarkUsed -CacheLease $cacheLease
        Write-Log "  Cached verified asset (SHA256: $hash)"
    } catch {
        Write-Log "  Asset cache save failed: $($_.Exception.Message)" -Level 'WARN'
    } finally {
        if ($cacheLease) {
            Exit-LibreSpotAssetCacheLease -Lease $cacheLease
        }
    }
}
