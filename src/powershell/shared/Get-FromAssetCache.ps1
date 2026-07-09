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
        $actual = Get-FileSha256Lower -Path $cachePath
        if ($actual -ne $hash) {
            Write-Log "  Cached asset for $Label failed re-verification (expected $hash, got $actual). Quarantining stale entry." -Level 'WARN'
            $byteSize = (Get-Item -LiteralPath $cachePath).Length
            $corruptDirectory = Join-Path $global:CACHE_DIR 'corrupt'
            if (-not (Test-Path -LiteralPath $corruptDirectory -PathType Container)) {
                New-Item -Path $corruptDirectory -ItemType Directory -Force | Out-Null
            }
            $quarantinePath = Join-Path $corruptDirectory ("$hash-" + (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss') + '.bad')
            Move-Item -LiteralPath $cachePath -Destination $quarantinePath -Force -ErrorAction SilentlyContinue
            Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -ByteSize $byteSize -Status 'corrupt' -MarkVerified -QuarantinedPath $quarantinePath
            Write-OperationJournalEntry -Phase 'cache' -Target $cachePath -SafetyDecision 'Allowed' -Result 'Quarantined' -WouldChange $true -Reversible $false -RollbackHint 'The corrupt cached asset was moved aside and will be downloaded again on demand.' -Data @{
                label = $Label
                expectedSha256 = $hash
                observedSha256 = $actual
                quarantinePath = $quarantinePath
            }
            return $false
        }
        $outDir = Split-Path -Path $DestinationPath -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -LiteralPath $cachePath -Destination $DestinationPath -Force
        $byteSize = (Get-Item -LiteralPath $cachePath).Length
        Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -ByteSize $byteSize -Status 'present' -MarkVerified -MarkUsed
        Write-Log "  Using verified cached copy for $Label (SHA256: $hash)"
        return $true
    } catch {
        Write-Log "  Cache retrieval failed for ${Label}: $($_.Exception.Message)" -Level 'WARN'
        return $false
    }
}
