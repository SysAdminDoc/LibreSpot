function Get-ThirdPartyPatcherReport {
    param(
        [string]$SpotifyExePath = $global:SPOTIFY_EXE_PATH,
        [string]$ConfigDirectory = $global:CONFIG_DIR,
        [string]$SpicetifyPath = '',
        [string]$SpicetifyConfigPath = ''
    )

    if ([string]::IsNullOrWhiteSpace($SpicetifyPath) -or [string]::IsNullOrWhiteSpace($SpicetifyConfigPath)) {
        $integration = Get-SpicetifyIntegrationContext
        if ([string]::IsNullOrWhiteSpace($SpicetifyPath)) { $SpicetifyPath = [string]$integration.CliPath }
        if ([string]::IsNullOrWhiteSpace($SpicetifyConfigPath)) { $SpicetifyConfigPath = [string]$integration.ConfigPath }
    }

    $spotifyDirectory = if ([string]::IsNullOrWhiteSpace($SpotifyExePath)) { '' } else { [System.IO.Path]::GetDirectoryName($SpotifyExePath) }
    $appsDirectory = if ([string]::IsNullOrWhiteSpace($spotifyDirectory)) { '' } else { Join-Path $spotifyDirectory 'Apps' }
    $existingPaths = {
        param([string[]]$Candidates)
        @($Candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) })
    }
    $injectorCandidates = if ([string]::IsNullOrWhiteSpace($spotifyDirectory)) { @() } else { @(
        (Join-Path $spotifyDirectory 'dpapi.dll')
        (Join-Path $spotifyDirectory 'config.ini')
        (Join-Path $spotifyDirectory 'version.dll')
        (Join-Path $spotifyDirectory 'winmm.dll')
    ) }
    $spotXCandidates = if ([string]::IsNullOrWhiteSpace($spotifyDirectory)) { @() } else { @(
        (Join-Path $appsDirectory 'xpui.bak')
        (Join-Path $appsDirectory 'xpui.spa.bak')
        (Join-Path $spotifyDirectory 'Spotify.bak')
        (Join-Path $spotifyDirectory 'chrome_elf.dll.bak')
    ) }
    $libreSpotCandidates = if ([string]::IsNullOrWhiteSpace($ConfigDirectory)) { @() } else { @(
        (Join-Path $ConfigDirectory 'operation-journal.jsonl')
        (Join-Path $ConfigDirectory 'install.log')
        (Join-Path $ConfigDirectory 'spicetify-preservation-latest.json')
    ) }

    $injectorEvidence = @(& $existingPaths $injectorCandidates)
    $spotXEvidence = @(& $existingPaths $spotXCandidates)
    $libreSpotEvidence = @(& $existingPaths $libreSpotCandidates)
    $activeBundlePresent = -not [string]::IsNullOrWhiteSpace($appsDirectory) -and (
        (Test-Path -LiteralPath (Join-Path $appsDirectory 'xpui.spa') -PathType Leaf) -or
        (Test-Path -LiteralPath (Join-Path $appsDirectory 'xpui') -PathType Container))
    $libreSpotOwned = $libreSpotEvidence.Count -gt 0
    $footprints = @()

    if ($injectorEvidence.Count -gt 0) {
        $footprints += [pscustomobject]@{
            Id = 'likely-blockthespot'; Name = 'Likely BlockTheSpot-family injector'; Confidence = 'likely'; Ownership = 'foreign'
            EvidencePaths = @($injectorEvidence)
            Recommendation = 'Create a Spicetify backup if applicable, then use Full Reset for a clean migration. LibreSpot will not remove these files outside an explicitly confirmed cleanup.'
        }
    }
    if ($activeBundlePresent -and $spotXEvidence.Count -gt 0) {
        $footprints += [pscustomobject]@{
            Id = if ($libreSpotOwned) { 'librespot-spotx' } else { 'raw-spotx' }
            Name = if ($libreSpotOwned) { 'LibreSpot-managed SpotX' } else { 'Raw SpotX' }
            Confidence = 'verified'; Ownership = if ($libreSpotOwned) { 'librespot' } else { 'foreign' }
            EvidencePaths = @($spotXEvidence)
            Recommendation = if ($libreSpotOwned) { 'Continue with LibreSpot maintenance actions.' } else { 'Keep the existing SpotX backups and use setup without Clean Install to adopt this state; choose Full Reset only when you intend to remove it.' }
        }
    }
    if ((Test-Path -LiteralPath $SpicetifyPath -PathType Leaf) -or (Test-Path -LiteralPath $SpicetifyConfigPath -PathType Leaf)) {
        $spicetifyEvidence = @(& $existingPaths @($SpicetifyPath, $SpicetifyConfigPath))
        $footprints += [pscustomobject]@{
            Id = if ($libreSpotOwned) { 'librespot-spicetify' } else { 'standalone-spicetify' }
            Name = if ($libreSpotOwned) { 'LibreSpot-managed Spicetify' } else { 'Standalone Spicetify' }
            Confidence = 'verified'; Ownership = if ($libreSpotOwned) { 'librespot' } else { 'foreign' }
            EvidencePaths = @($spicetifyEvidence)
            Recommendation = if ($libreSpotOwned) { 'Continue with LibreSpot maintenance actions.' } else { 'Create a backup before setup. LibreSpot preserves the existing config and CustomApps state during migration.' }
        }
    }

    $foreign = @($footprints | Where-Object { $_.Ownership -eq 'foreign' })
    $owned = @($footprints | Where-Object { $_.Ownership -eq 'librespot' })
    $ownership = if ($foreign.Count -gt 0 -and $owned.Count -gt 0) { 'mixed' } elseif ($foreign.Count -gt 0) { 'foreign' } elseif ($owned.Count -gt 0) { 'librespot' } else { 'unmodified' }
    $summary = if ($foreign.Count -gt 0) { "Detected foreign customization state: $(@($foreign.Name) -join ', ')." } elseif ($owned.Count -gt 0) { 'Detected only LibreSpot-managed customization state.' } else { 'No customization footprint was detected.' }
    $recommendation = if ($foreign.Count -gt 0) { @($foreign.Recommendation | Select-Object -Unique) -join ' ' } elseif ($owned.Count -gt 0) { 'Continue with LibreSpot maintenance actions.' } else { 'No migration action is needed.' }
    return [pscustomobject]@{
        Ownership = $ownership
        HasForeignState = $foreign.Count -gt 0
        Summary = $summary
        Recommendation = $recommendation
        Footprints = @($footprints)
    }
}
