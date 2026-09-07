function Update-AssetCacheIndexEntry {
    param(
        [string]$SHA256Hash,
        [string]$Label = '',
        [string]$SourceUrl = '',
        [object]$ByteSize = $null,
        [string]$Status = 'present',
        [switch]$MarkUsed,
        [switch]$MarkVerified,
        [string]$QuarantinedPath = '',
        [object]$CacheLease = $null
    )

    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return }

    $ownsLease = $false
    try {
        if ($null -eq $CacheLease) {
            $CacheLease = Enter-LibreSpotAssetCacheLease -CacheDirectory $global:CACHE_DIR -Label 'asset-cache index update'
            $ownsLease = $true
        }

        if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
            New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
        }

        $indexPath = Join-Path $global:CACHE_DIR 'asset-cache-index.json'
        $now = (Get-Date).ToUniversalTime().ToString('o')
        $entries = @()
        if (Test-Path -LiteralPath $indexPath -PathType Leaf) {
            try {
                $existingDoc = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
                if ($null -eq $existingDoc -or [int]$existingDoc.schemaVersion -ne 1) {
                    throw 'The asset-cache index uses an unsupported schema version.'
                }
                $entriesProperty = $existingDoc.PSObject.Properties['entries']
                if ($null -eq $entriesProperty -or $null -eq $entriesProperty.Value) {
                    throw 'The asset-cache index has no entries array.'
                }
                $entries = @($entriesProperty.Value)
                if ($entries.Count -gt 2048) {
                    throw 'The asset-cache index contains too many entries.'
                }
                $knownHashes = @{}
                foreach ($entry in $entries) {
                    $entryHash = ([string]$entry.sha256).ToLowerInvariant()
                    if ($entryHash -notmatch '\A[0-9a-f]{64}\z' -or $knownHashes.ContainsKey($entryHash)) {
                        throw 'The asset-cache index contains an invalid or duplicate SHA256 value.'
                    }
                    if ([string]::IsNullOrWhiteSpace([string]$entry.label) -or ([string]$entry.label).Length -gt 256) {
                        throw "The asset-cache index entry $entryHash has an invalid label."
                    }
                    $knownHashes[$entryHash] = $true
                }
            } catch {
                try { Write-Log "  Asset cache index is corrupt and was retained: $($_.Exception.Message)" -Level 'WARN' } catch {}
                return
            }
        }

        $existing = $entries | Where-Object { $_.sha256 -eq $hash } | Select-Object -First 1
        $remaining = @($entries | Where-Object { $_.sha256 -ne $hash })
        $cachePath = Join-Path $global:CACHE_DIR $hash
        $resolvedByteSize = $ByteSize
        if ($null -eq $resolvedByteSize -and (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
            $resolvedByteSize = (Get-Item -LiteralPath $cachePath).Length
        }
        if ($null -eq $resolvedByteSize -and $existing) {
            $resolvedByteSize = $existing.byteSize
        }
        if ($null -eq $resolvedByteSize) {
            $resolvedByteSize = 0
        }

        $entry = [ordered]@{
            sha256            = $hash
            label             = if (-not [string]::IsNullOrWhiteSpace($Label)) { $Label } elseif ($existing -and $existing.label) { [string]$existing.label } else { 'Cached asset' }
            sourceUrl         = if (-not [string]::IsNullOrWhiteSpace($SourceUrl)) { $SourceUrl } elseif ($existing -and $existing.sourceUrl) { [string]$existing.sourceUrl } else { $null }
            byteSize          = [int64]$resolvedByteSize
            firstSeenAtUtc    = if ($existing -and $existing.firstSeenAtUtc) { [string]$existing.firstSeenAtUtc } else { $now }
            lastUsedAtUtc     = if ($MarkUsed) { $now } elseif ($existing -and $existing.lastUsedAtUtc) { [string]$existing.lastUsedAtUtc } else { $null }
            lastVerifiedAtUtc = if ($MarkVerified) { $now } elseif ($existing -and $existing.lastVerifiedAtUtc) { [string]$existing.lastVerifiedAtUtc } else { $null }
            status            = if ([string]::IsNullOrWhiteSpace($Status)) { 'present' } else { $Status }
            quarantinedPath   = if ([string]::IsNullOrWhiteSpace($QuarantinedPath)) { $null } else { $QuarantinedPath }
        }

        $doc = [ordered]@{
            schemaVersion  = 1
            generatedAtUtc = $now
            entries        = @($remaining + [pscustomobject]$entry | Sort-Object sha256)
        }

        $utf8 = New-Object System.Text.UTF8Encoding($false)
        $json = $doc | ConvertTo-Json -Depth 8
        Write-LibreSpotAssetCacheFileAtomically -DestinationPath $indexPath -Writer {
            param($stream)
            $bytes = $utf8.GetBytes($json)
            $stream.Write($bytes, 0, $bytes.Length)
        }
    } catch {
        try { Write-Log "  Asset cache index update failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    } finally {
        if ($ownsLease -and $null -ne $CacheLease) {
            Exit-LibreSpotAssetCacheLease -Lease $CacheLease
        }
    }
}
