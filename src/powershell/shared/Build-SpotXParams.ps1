function Build-SpotXParams { param($Config)
    $p = @()
    # Always auto-remove MS Store Spotify without prompt (prevents stdin hang)
    $p += "-confirm_uninstall_ms_spoti"
    # Let SpotX manage Spotify version compatibility (auto-overwrite unsupported versions)
    $p += "-confirm_spoti_recomended_over"
    if ($Config.SpotX_NewTheme)        { $p += "-new_theme" }
    if ($Config.SpotX_PodcastsOff)     { $p += "-podcasts_off" } else { $p += "-podcasts_on" }
    if ($Config.SpotX_AdSectionsOff)   { $p += "-adsections_off" }
    if ($Config.SpotX_BlockUpdate)     { $p += "-block_update_on" } else { $p += "-block_update_off" }
    if ($Config.SpotX_Premium)         { $p += "-premium" }
    if ($Config.SpotX_DisableStartup)  { $p += "-DisableStartup" }
    if ($Config.SpotX_NoShortcut)      { $p += "-no_shortcut" }
    if ($Config.SpotX_StartSpoti)      { $p += "-start_spoti" }
    if ($Config.SpotX_LyricsEnabled) {
        $p += "-lyrics_stat $($Config.SpotX_LyricsTheme)"
        if ($Config.SpotX_LyricsBlock) {
            $p += "-lyrics_block"
        } elseif ($Config.SpotX_OldLyrics) {
            $p += "-old_lyrics"
        }
    }
    if ($Config.SpotX_TopSearch)       { $p += "-topsearchbar" }
    if ($Config.SpotX_RightSidebarOff) { $p += "-rightsidebar_off" }
    if ($Config.SpotX_RightSidebarClr) { $p += "-rightsidebarcolor" }
    if ($Config.SpotX_CanvasHomeOff)   { $p += "-canvashome_off" }
    if ($Config.SpotX_HomeSubOff)      { $p += "-homesub_off" }
    if ($Config.SpotX_HideColIconOff)  { $p += "-hide_col_icon_off" }
    if ($Config.SpotX_Plus)             { $p += "-plus" }
    if ($Config.SpotX_NewFullscreen)    { $p += "-newFullscreenMode" }
    if ($Config.SpotX_FunnyProgress)    { $p += "-funnyprogressBar" }
    if ($Config.SpotX_ExpSpotify)       { $p += "-exp_spotify" }
    if ($Config.SpotX_SendVersionOff)   { $p += "-sendversion_off" }
    if ($Config.SpotX_DevTools)         { $p += "-devtools" }
    if ($Config.SpotX_Mirror)           { $p += "-mirror" }
    if ($Config.SpotX_ConfirmUninstall) { $p += "-confirm_spoti_recomended_uninstall" }
    if (-not [string]::IsNullOrWhiteSpace([string]$Config.SpotX_DownloadMethod)) {
        $p += "-download_method $($Config.SpotX_DownloadMethod)"
    }
    $versionId = [string]$Config.SpotX_SpotifyVersionId
    if (-not [string]::IsNullOrWhiteSpace($versionId) -and $versionId -ne 'auto') {
        $entry = $global:SpotifyVersionManifest | Where-Object { $_.Id -eq $versionId } | Select-Object -First 1
        if ($entry -and -not [string]::IsNullOrWhiteSpace([string]$entry.Version)) {
            $p += "-version $($entry.Version)"
        }
    }
    if ($Config.SpotX_CacheLimit -ge 500) { $p += "-cache_limit $($Config.SpotX_CacheLimit)" }
    if (-not [string]::IsNullOrWhiteSpace([string]$Config.SpotX_Language)) {
        $p += "-language $($Config.SpotX_Language)"
    }
    return ($p -join " ")
}
