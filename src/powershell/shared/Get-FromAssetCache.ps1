function Get-FromAssetCache { param([string]$SHA256Hash, [string]$DestinationPath, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return $false }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return $false }
    $cachePath = Join-Path $global:CACHE_DIR $hash
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        Write-Log "  Cache miss for $Label (SHA256: $hash)"
        return $false
    }
    try {
        $actual = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $hash) {
            Write-Log "  Cached asset for $Label failed re-verification (expected $hash, got $actual). Removing stale entry." -Level 'WARN'
            Remove-Item -LiteralPath $cachePath -Force -ErrorAction SilentlyContinue
            return $false
        }
        $outDir = Split-Path -Path $DestinationPath -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -LiteralPath $cachePath -Destination $DestinationPath -Force
        Write-Log "  Using verified cached copy for $Label (SHA256: $hash)"
        return $true
    } catch {
        Write-Log "  Cache retrieval failed for ${Label}: $($_.Exception.Message)" -Level 'WARN'
        return $false
    }
}
