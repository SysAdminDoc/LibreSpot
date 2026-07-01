function Save-ToAssetCache { param([string]$SourcePath, [string]$SHA256Hash, [string]$Label = '', [string]$SourceUrl = '')
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return }
    try {
        if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
            New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
        }
        $cachePath = Join-Path $global:CACHE_DIR $hash
        Copy-Item -LiteralPath $SourcePath -Destination $cachePath -Force
        $byteSize = (Get-Item -LiteralPath $cachePath).Length
        Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -SourceUrl $SourceUrl -ByteSize $byteSize -Status 'present' -MarkVerified -MarkUsed
        Write-Log "  Cached verified asset (SHA256: $hash)"
    } catch {
        Write-Log "  Asset cache save failed: $($_.Exception.Message)" -Level 'WARN'
    }
}
