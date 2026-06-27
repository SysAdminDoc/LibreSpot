function Check-ForUpdates {
    Write-Log '=== Checking for dependency updates ===' -Level 'STEP'
    $headers = @{'User-Agent'="LibreSpot/$global:VERSION"}
    $updates = @()
    $compatWarnings = @()

    # SpotX (pinned to a specific commit on main, check for newer commits)
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/SpotX-Official/SpotX/commits/main' -Headers $headers -Label 'SpotX'
        $latestSha = $rel.sha
        $pinnedSha = $global:PinnedReleases.SpotX.Commit
        if ($latestSha -ne $pinnedSha) {
            $short = $latestSha.Substring(0,10)
            $msg = ($rel.commit.message -split "`n")[0]
            $updates += "SpotX: new commit $short"
            Write-Log "  SpotX: new commit $short ($msg)" -Level 'WARN'
        } else { Write-Log "  SpotX: $($pinnedSha.Substring(0,10)) (up to date)" }
    } catch { Write-Log "  SpotX: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Spicetify CLI
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/cli/releases/latest' -Headers $headers -Label 'Spicetify CLI'
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.SpicetifyCLI.Version
        if (Compare-LibreSpotVersions -Latest $latest -Current $pinned) { $updates += "CLI: $pinned -> $latest"; Write-Log "  Spicetify CLI: $pinned -> $latest available" -Level 'WARN' }
        else { Write-Log "  Spicetify CLI: v$pinned (up to date)" }
    } catch { Write-Log "  Spicetify CLI: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Marketplace
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/marketplace/releases/latest' -Headers $headers -Label 'Marketplace'
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.Marketplace.Version
        if (Compare-LibreSpotVersions -Latest $latest -Current $pinned) { $updates += "Marketplace: $pinned -> $latest"; Write-Log "  Marketplace: $pinned -> $latest available" -Level 'WARN' }
        else { Write-Log "  Marketplace: v$pinned (up to date)" }
    } catch { Write-Log "  Marketplace: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Themes
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/spicetify-themes/commits/master' -Headers $headers -Label 'Themes'
        $latest = $rel.sha
        $pinned = $global:PinnedReleases.Themes.Commit
        if ($latest -ne $pinned) {
            $short = $latest.Substring(0,10)
            $msg = ($rel.commit.message -split "`n")[0]
            $updates += "Themes: new commit $short"
            Write-Log "  Themes: new commit $short ($msg)" -Level 'WARN'
        } else { Write-Log "  Themes: $($pinned.Substring(0,10)) (up to date)" }
    } catch { Write-Log "  Themes: check failed ($($_.Exception.Message))" -Level 'WARN' }

    $compatWarnings = @(Write-LibreSpotCompatibilityMatrix)

    # LibreSpot itself
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/SysAdminDoc/LibreSpot/releases/latest' -Headers $headers -Label 'LibreSpot'
        $latest = $rel.tag_name -replace '^v',''
        if (Compare-LibreSpotVersions -Latest $latest -Current $global:VERSION) {
            $updates += "LibreSpot: $($global:VERSION) -> $latest"
            Write-Log "  LibreSpot: $($global:VERSION) -> $latest available" -Level 'WARN'
        } else {
            Write-Log "  LibreSpot: v$($global:VERSION) (up to date)"
        }
    } catch { Write-Log "  LibreSpot: check failed ($($_.Exception.Message))" -Level 'WARN' }

    if ($updates.Count -eq 0 -and $compatWarnings.Count -eq 0) {
        Write-Log "All dependencies and compatibility baselines are up to date." -Level 'SUCCESS'
    } else {
        if ($updates.Count -eq 0) {
            Write-Log "All pinned dependency versions are current." -Level 'SUCCESS'
        }
        if ($updates.Count -gt 0) {
            Write-Log "$($updates.Count) update(s) available. Update the PinnedReleases block in the script to upgrade." -Level 'WARN'
        }
        if ($compatWarnings.Count -gt 0) {
            Write-Log "$($compatWarnings.Count) compatibility warning(s) detected; review the matrix above before repatching newer Spotify builds." -Level 'WARN'
        }
        if ($updates.Count -gt 0) {
            Write-Log "After updating versions, re-download each component and update its SHA256 hash." -Level 'WARN'
        }
    }
    Write-Log '=== Update check complete ===' -Level 'STEP'
}
