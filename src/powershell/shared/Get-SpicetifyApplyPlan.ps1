function Get-SpicetifyApplyPlan {
    param(
        [string]$ConfigPath = '',
        [string]$BackupDirectory = '',
        [string]$ExtractedDirectory = '',
        [string]$SpotifyVersion = ''
    )

    $integration = $null
    if ([string]::IsNullOrWhiteSpace($ConfigPath) -or
        [string]::IsNullOrWhiteSpace($BackupDirectory) -or
        [string]::IsNullOrWhiteSpace($ExtractedDirectory)) {
        $integration = Get-SpicetifyIntegrationContext
    }
    if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
        $ConfigPath = [string]$integration.ConfigPath
    }
    if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
        $BackupDirectory = Join-Path ([string]$integration.ConfigDirectory) 'Backup'
    }
    if ([string]::IsNullOrWhiteSpace($ExtractedDirectory)) {
        $ExtractedDirectory = Join-Path ([string]$integration.ConfigDirectory) 'Extracted'
    }
    if ([string]::IsNullOrWhiteSpace($SpotifyVersion)) {
        $SpotifyVersion = [string](Get-InstalledSpotifyVersion)
    }

    $backupVersion = ''
    if (Test-Path -LiteralPath $ConfigPath -PathType Leaf) {
        $section = ''
        foreach ($line in Get-Content -LiteralPath $ConfigPath -ErrorAction SilentlyContinue) {
            if ($line -match '^\s*\[([^\]]+)\]\s*$') {
                $section = [string]$Matches[1]
                continue
            }
            if ($section -eq 'Backup' -and $line -match '^\s*version\s*=\s*(.*?)\s*$') {
                $backupVersion = [string]$Matches[1]
                break
            }
        }
    }

    $normalizedSpotifyVersion = ([string]$SpotifyVersion).Trim().TrimStart('v')
    $normalizedBackupVersion = ([string]$backupVersion).Trim().TrimStart('v')
    # Spotify.exe exposes the four-part file version while Spicetify records
    # the same build with Spotify's trailing git hash. Compare the shared build
    # tuple or every real current backup looks stale (1.2.93.667 versus
    # 1.2.93.667.g7b5cc0ce).
    $comparableSpotifyVersion = $normalizedSpotifyVersion -replace '\.g[0-9a-f]+$', ''
    $comparableBackupVersion = $normalizedBackupVersion -replace '\.g[0-9a-f]+$', ''
    $versionsMatch = -not [string]::IsNullOrWhiteSpace($comparableSpotifyVersion) -and
        $comparableSpotifyVersion.Equals($comparableBackupVersion, [StringComparison]::OrdinalIgnoreCase)
    $backupReady = (Test-Path -LiteralPath (Join-Path $BackupDirectory 'xpui.spa') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $BackupDirectory 'login.spa') -PathType Leaf)
    $extractedReady = (Test-Path -LiteralPath (Join-Path $ExtractedDirectory 'Raw') -PathType Container) -and
        (Test-Path -LiteralPath (Join-Path $ExtractedDirectory 'Themed') -PathType Container)

    if ($versionsMatch -and $backupReady -and $extractedReady) {
        return [pscustomobject]@{
            Stage          = 'apply --no-restart'
            Arguments      = @('apply', '--no-restart', '--bypass-admin')
            FailureMessage = 'Could not apply Spicetify changes from the current verified backup.'
            SuccessMessage = 'Spicetify apply succeeded using the current verified backup.'
            Reason         = "Reusing the prepared Spicetify backup for Spotify $normalizedSpotifyVersion."
            BackupVersion  = $normalizedBackupVersion
        }
    }

    return [pscustomobject]@{
        Stage          = 'backup apply'
        Arguments      = @('backup', 'apply', '--bypass-admin')
        FailureMessage = 'Could not backup and apply Spicetify changes.'
        SuccessMessage = 'Spicetify backup apply succeeded.'
        Reason         = 'No complete current-version Spicetify backup was available.'
        BackupVersion  = $normalizedBackupVersion
    }
}
