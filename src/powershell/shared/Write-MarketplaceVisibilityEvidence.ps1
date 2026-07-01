function Write-MarketplaceVisibilityEvidence {
    param(
        [string]$Source = 'Unknown',
        [string]$ApplyStage = '',
        [object]$ApplySucceeded = $null,
        [string]$ApplyMessage = '',
        [object]$OpenUriSucceeded = $null,
        [string]$OpenUriMessage = '',
        [object]$OpenUriRequestedAtUtc = $null,
        [object]$SpotifyRunningAfterOpen = $null
    )

    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }

        $health = Get-MarketplaceHealth
        $manifestPath = Join-Path $health.Path 'manifest.json'
        $manifestVersion = $null
        if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
            try {
                $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
                foreach ($property in @('version','Version','marketplaceVersion')) {
                    if ($manifest.PSObject.Properties.Name -contains $property) {
                        $value = [string]$manifest.$property
                        if (-not [string]::IsNullOrWhiteSpace($value)) {
                            $manifestVersion = $value
                            break
                        }
                    }
                }
            } catch {
                $manifestVersion = $null
            }
        }

        $applySucceededValue = if ($null -ne $ApplySucceeded) { [bool]$ApplySucceeded } else { $null }
        $openSucceededValue = if ($null -ne $OpenUriSucceeded) { [bool]$OpenUriSucceeded } else { $null }
        $spotifyRunningValue = if ($null -ne $SpotifyRunningAfterOpen) {
            [bool]$SpotifyRunningAfterOpen
        } else {
            try { @((Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)).Count -gt 0 } catch { $null }
        }
        $openRequestedAt = if ($OpenUriRequestedAtUtc) { [string]$OpenUriRequestedAtUtc } else { $null }
        $applyCompletedAt = if ($null -ne $applySucceededValue) { (Get-Date).ToUniversalTime().ToString('o') } else { $null }
        $lastObservedAt = if ($null -ne $spotifyRunningValue) { (Get-Date).ToUniversalTime().ToString('o') } else { $null }
        $lastObservedSession = if ($null -eq $spotifyRunningValue) {
            'not observed'
        } elseif ($spotifyRunningValue) {
            'spotify-process-running'
        } else {
            'spotify-process-not-running'
        }
        $likelyVisible = [bool]($health.HasFiles -and $health.IsEnabled -and ($applySucceededValue -eq $true) -and ($openSucceededValue -eq $true))

        $doc = [ordered]@{
            schemaVersion              = 1
            generatedAtUtc             = (Get-Date).ToUniversalTime().ToString('o')
            source                     = $Source
            filesPresent               = [bool]$health.HasFiles
            registered                 = [bool]$health.IsEnabled
            likelyVisible              = $likelyVisible
            marketplaceStatus          = [string]$health.Status
            marketplacePath            = [string]$health.Path
            manifestVersion            = $manifestVersion
            applyStage                 = $ApplyStage
            applySucceeded             = $applySucceededValue
            applyMessage               = $ApplyMessage
            applyCompletedAtUtc        = $applyCompletedAt
            openUriSucceeded           = $openSucceededValue
            openUriMessage             = $OpenUriMessage
            openUriRequestedAtUtc      = $openRequestedAt
            spotifyRunningAfterOpen    = $spotifyRunningValue
            lastObservedSpotifySession = $lastObservedSession
            lastObservedAtUtc          = $lastObservedAt
        }

        $path = Join-Path $global:CONFIG_DIR 'marketplace-evidence.json'
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($path, ($doc | ConvertTo-Json -Depth 5), $utf8)
        Write-OperationJournalEntry -Phase 'marketplace' -Target $path -SafetyDecision 'Allowed' -Result 'Recorded' -WouldChange $true -Reversible $false -RollbackHint 'Re-run Repair Marketplace or Reapply to refresh Marketplace visibility evidence.' -Data @{
            source = $Source
            marketplaceStatus = $health.Status
            likelyVisible = $likelyVisible
            applySucceeded = $applySucceededValue
            openUriSucceeded = $openSucceededValue
        }
        return [pscustomobject]$doc
    } catch {
        try { Write-Log "Marketplace visibility evidence could not be recorded: $($_.Exception.Message)" -Level 'WARN' } catch {}
        return $null
    }
}
