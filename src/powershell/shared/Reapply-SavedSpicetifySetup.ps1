function Reapply-SavedSpicetifySetup { param($Config)
    if (-not (Test-SpicetifyCliInstalled)) {
        Write-Log "Spicetify CLI is missing, so LibreSpot will reinstall it before restoring your saved setup." -Level 'WARN'
        Module-InstallSpicetifyCLI
    }

    Module-InstallThemes -Config $Config
    Module-InstallExtensions -Config $Config
    Module-InstallMarketplace -Config $Config
    Module-ApplySpicetify -Config $Config
}
