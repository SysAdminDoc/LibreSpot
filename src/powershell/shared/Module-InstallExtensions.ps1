function Module-InstallExtensions { param($Config)
    $exts = @($Config.Spicetify_Extensions)
    if ($exts.Count -eq 0) {
        Write-Log "Extensions: none selected. Removing LibreSpot-managed extensions if they are still enabled..." -Level 'STEP'
    } else {
        Write-Log "Extensions: $($exts -join ', ')..." -Level 'STEP'
    }
    # Download any selected community extensions to the Extensions folder first
    Download-CommunityExtensions -Config $Config
    $allManaged = @($global:BuiltInExtensions.Keys) + @($global:CommunityExtensions.Keys) + @($global:DeprecatedCommunityExtensionNames)
    Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems $exts -ManagedItems $allManaged
}
