function Normalize-LibreSpotConfig {
    param([hashtable]$Config)

    $null = Assert-LibreSpotConfigSchemaSupported -Config $Config

    $normalized = @{
        ConfigSchemaVersion = $global:CONFIG_SCHEMA_VERSION
        Mode = 'Easy'
    }
    foreach ($key in $global:EasyDefaults.Keys) {
        $defaultValue = $global:EasyDefaults[$key]
        if ($defaultValue -is [System.Collections.IEnumerable] -and $defaultValue -isnot [string]) {
            $normalized[$key] = @($defaultValue)
        } else {
            $normalized[$key] = $defaultValue
        }
    }

    # RiskAcknowledged is a UX-only flag not part of EasyDefaults; default false.
    if (-not $normalized.ContainsKey('RiskAcknowledged')) { $normalized['RiskAcknowledged'] = $false }

    if ($Config -and $Config.ContainsKey('Mode')) {
        $mode = [string]$Config.Mode
        if ($mode -in @('Easy', 'Custom')) { $normalized.Mode = $mode }
    }

    $uiCulture = if ($Config -and $Config.ContainsKey('UiCulture')) { [string]$Config.UiCulture } else { [string]$normalized.UiCulture }
    $allowedUiCultures = @('en','ru','zh-Hans','pt-BR','es')
    $normalized.UiCulture = if ($allowedUiCultures -contains $uiCulture) { $uiCulture } else { 'en' }

    $booleanKeys = @(
        'CleanInstall','LaunchAfter',
        'SpotX_NewTheme','SpotX_PodcastsOff','SpotX_BlockUpdate','SpotX_AdSectionsOff',
        'SpotX_Premium','SpotX_LyricsEnabled','SpotX_TopSearch','SpotX_RightSidebarOff',
        'SpotX_RightSidebarClr','SpotX_CanvasHomeOff','SpotX_HomeSubOff',
        'SpotX_DisableStartup','SpotX_NoShortcut','SpotX_OldLyrics','SpotX_HideColIconOff',
        'SpotX_Plus','SpotX_NewFullscreen','SpotX_FunnyProgress','SpotX_ExpSpotify','SpotX_LyricsBlock',
        'SpotX_SendVersionOff','SpotX_StartSpoti','SpotX_DevTools','SpotX_Mirror','SpotX_ConfirmUninstall',
        'SpotX_CustomPatchesEnabled','Spicetify_Marketplace','AutoReapply_Enabled','RiskAcknowledged'
    )
    foreach ($key in $booleanKeys) {
        if ($Config -and $Config.ContainsKey($key)) {
            $normalized[$key] = ConvertTo-ConfigBoolean -Value $Config[$key] -Default ([bool]$normalized[$key])
        }
    }

    if ($Config -and $Config.ContainsKey('SpotX_CacheLimit')) {
        $normalized.SpotX_CacheLimit = ConvertTo-ConfigInt -Value $Config.SpotX_CacheLimit -Default ([int]$normalized.SpotX_CacheLimit) -Minimum 0 -Maximum 50000
    }

    if ($Config -and $Config.ContainsKey('SpotX_CustomPatchesJson')) {
        $patchJson = [string]$Config.SpotX_CustomPatchesJson
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        if ($utf8.GetByteCount($patchJson) -le 65536) {
            $normalized.SpotX_CustomPatchesJson = $patchJson.Trim()
        }
    }

    $dm = if ($Config -and $Config.ContainsKey('SpotX_DownloadMethod')) { [string]$Config.SpotX_DownloadMethod } else { [string]$normalized.SpotX_DownloadMethod }
    $dm = $dm.Trim().ToLowerInvariant()
    if ($dm -notin @('','curl','webclient')) { $dm = '' }
    $normalized.SpotX_DownloadMethod = $dm

    $lang = if ($Config -and $Config.ContainsKey('SpotX_Language')) { [string]$Config.SpotX_Language } else { [string]$normalized.SpotX_Language }
    $allowedLanguages = @('en','ru','de','fr','es','pt','pt-BR','it','nl','pl','sv','no','da','fi','ja','ko','zh-CN','zh-TW','ar','tr','cs','hu','ro','uk','id','th','vi')
    $normalized.SpotX_Language = if ($allowedLanguages -contains $lang) { $lang } else { '' }

    $svid = if ($Config -and $Config.ContainsKey('SpotX_SpotifyVersionId')) { [string]$Config.SpotX_SpotifyVersionId } else { [string]$normalized.SpotX_SpotifyVersionId }
    if ([string]::IsNullOrWhiteSpace($svid) -or $svid -notin $global:SpotifyVersionIds) { $svid = 'auto' }
    $normalized.SpotX_SpotifyVersionId = $svid

    $lyricsTheme = if ($Config -and $Config.ContainsKey('SpotX_LyricsTheme')) { [string]$Config.SpotX_LyricsTheme } else { [string]$normalized.SpotX_LyricsTheme }
    if ([string]::IsNullOrWhiteSpace($lyricsTheme) -or $lyricsTheme -notin $global:SpotXLyricsThemes) {
        $lyricsTheme = [string]$global:EasyDefaults.SpotX_LyricsTheme
    }
    $normalized.SpotX_LyricsTheme = $lyricsTheme

    $themeName = if ($Config -and $Config.ContainsKey('Spicetify_Theme')) { [string]$Config.Spicetify_Theme } else { [string]$normalized.Spicetify_Theme }
    if ([string]::IsNullOrWhiteSpace($themeName) -or -not $global:ThemeData.Contains($themeName)) {
        $themeName = [string]$global:EasyDefaults.Spicetify_Theme
    }
    $normalized.Spicetify_Theme = $themeName

    $availableSchemes = @()
    if ($global:ThemeData.Contains($themeName)) {
        $availableSchemes = @($global:ThemeData[$themeName].Schemes)
    }
    $defaultScheme = if ($availableSchemes -contains [string]$global:EasyDefaults.Spicetify_Scheme) {
        [string]$global:EasyDefaults.Spicetify_Scheme
    } elseif ($availableSchemes.Count -gt 0) {
        [string]$availableSchemes[0]
    } else {
        'Default'
    }
    $schemeName = if ($Config -and $Config.ContainsKey('Spicetify_Scheme')) { [string]$Config.Spicetify_Scheme } else { $defaultScheme }
    if ([string]::IsNullOrWhiteSpace($schemeName) -or $schemeName -notin $availableSchemes) {
        $schemeName = $defaultScheme
    }
    $normalized.Spicetify_Scheme = $schemeName

    $extensions = [System.Collections.Generic.List[string]]::new()
    $rawExtensions = @()
    if ($Config -and $Config.ContainsKey('Spicetify_Extensions')) {
        if ($Config.Spicetify_Extensions -is [string]) {
            $rawExtensions = @([string]$Config.Spicetify_Extensions)
        } elseif ($Config.Spicetify_Extensions -is [System.Collections.IEnumerable]) {
            $rawExtensions = @($Config.Spicetify_Extensions)
        }
    }
    foreach ($extension in $rawExtensions) {
        $name = [string]$extension
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if ($global:CommunityExtensionAliases.ContainsKey($name)) { $name = [string]$global:CommunityExtensionAliases[$name] }
        if (-not $global:BuiltInExtensions.Contains($name) -and -not $global:CommunityExtensions.Contains($name)) { continue }
        if (-not $extensions.Contains($name)) { $extensions.Add($name) }
    }
    $normalized.Spicetify_Extensions = @($extensions)

    $customApps = [System.Collections.Generic.List[string]]::new()
    $rawCustomApps = @()
    if ($Config -and $Config.ContainsKey('Spicetify_CustomApps')) {
        if ($Config.Spicetify_CustomApps -is [string]) {
            $rawCustomApps = @([string]$Config.Spicetify_CustomApps)
        } elseif ($Config.Spicetify_CustomApps -is [System.Collections.IEnumerable]) {
            $rawCustomApps = @($Config.Spicetify_CustomApps)
        }
    }
    foreach ($customApp in $rawCustomApps) {
        $name = [string]$customApp
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if (-not $global:CommunityCustomApps.Contains($name)) { continue }
        if (-not $customApps.Contains($name)) { $customApps.Add($name) }
    }
    $normalized.Spicetify_CustomApps = @($customApps)

    if ($normalized.SpotX_RightSidebarOff) {
        $normalized.SpotX_RightSidebarClr = $false
    }

    if (-not $normalized.SpotX_LyricsEnabled) {
        $normalized.SpotX_OldLyrics = $false
        $normalized.SpotX_LyricsBlock = $false
    } elseif ($normalized.SpotX_LyricsBlock) {
        $normalized.SpotX_OldLyrics = $false
    }

    if ($Config -and -not $Config.ContainsKey('Mode')) {
        foreach ($key in $global:EasyDefaults.Keys) {
            if ($key -eq 'UiCulture') { continue }
            $defaultValue = $global:EasyDefaults[$key]
            $currentValue = $normalized[$key]
            $isEnumerableDefault = ($defaultValue -is [System.Collections.IEnumerable] -and $defaultValue -isnot [string])
            if ($isEnumerableDefault) {
                if ((@($currentValue) -join '|') -ne (@($defaultValue) -join '|')) {
                    $normalized.Mode = 'Custom'
                    break
                }
                continue
            }
            if ([string]$currentValue -ne [string]$defaultValue) {
                $normalized.Mode = 'Custom'
                break
            }
        }
    }

    return $normalized
}
