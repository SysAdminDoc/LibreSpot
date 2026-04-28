param(
    [ValidateSet('Install', 'CheckUpdates', 'Reapply', 'RestoreVanilla', 'UninstallSpicetify', 'FullReset', 'EnableAutoReapply', 'DisableAutoReapply', 'WatchAutoReapply')]
    [string]$Action = 'Install',
    [string]$ConfigPath = "$env:APPDATA\LibreSpot\config.json"
)

$ErrorActionPreference = 'Stop'

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch {}

# Keep this aligned with LibreSpot.ps1:$global:VERSION and the WPF shell's
# csproj <Version>. The release workflow fails the build if these drift.
$global:VERSION = '3.6.0'
$global:PinnedReleases = @{
    SpotX = @{
        Version = '2.0'
        Commit  = '0abf98a36be501740d774a56d54d5f7fbbafc35c'
        Url     = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/0abf98a36be501740d774a56d54d5f7fbbafc35c/run.ps1'
        SHA256  = '38d4205a2afc2050781bbfe28c6713edd6b0aef2c084304b58d92308b081f569'
    }
    SpicetifyCLI = @{
        Version = '2.43.1'
        SHA256  = @{
            x64   = 'c9b5e677d5b3046d14da09a3f713bd7b864b67b0c4c4b7ea2ab53c261e63b491'
            arm64 = '4cc793a947678ededaa244899c216d60230f535cb8ccaadf683e99c4ae741e13'
        }
    }
    Marketplace = @{
        Version = '1.0.8'
        Url     = 'https://github.com/spicetify/marketplace/releases/download/v1.0.8/marketplace.zip'
        SHA256  = 'ba20cd30896605ec60c272905004673b995162d2c8ca085351971e409cf80ec7'
    }
    Themes = @{
        Commit  = '9af41cf91af6f6093c0e060d57264f08f6bb161c'
        SHA256  = 'fd55e443e88302dfd45e201f35ec67db5f51c4346b58fab5da90faf7b1a66f28'
    }
}

$global:URL_SPOTX         = $global:PinnedReleases.SpotX.Url
$global:URL_MARKETPLACE   = $global:PinnedReleases.Marketplace.Url
$global:URL_THEMES_REPO   = "https://github.com/spicetify/spicetify-themes/archive/$($global:PinnedReleases.Themes.Commit).zip"
$global:URL_SPICETIFY_FMT = 'https://github.com/spicetify/cli/releases/download/v{0}/spicetify-{0}-windows-{1}.zip'

$global:TEMP_DIR             = $env:TEMP
$global:SPOTIFY_EXE_PATH     = "$env:APPDATA\Spotify\Spotify.exe"
$global:SPICETIFY_DIR        = "$env:LOCALAPPDATA\spicetify"
$global:SPICETIFY_CONFIG_DIR = "$env:APPDATA\spicetify"
$resolvedConfigDirectory = $null
try { $resolvedConfigDirectory = Split-Path -Path $ConfigPath -Parent } catch {}
if ([string]::IsNullOrWhiteSpace($resolvedConfigDirectory)) {
    $resolvedConfigDirectory = "$env:APPDATA\LibreSpot"
}
$global:CONFIG_DIR           = $resolvedConfigDirectory
$global:CONFIG_PATH          = $ConfigPath
$global:LOG_PATH             = Join-Path $global:CONFIG_DIR 'install.log'
$global:WATCHER_STATE_PATH   = Join-Path $global:CONFIG_DIR 'watcher-state.json'
$global:WATCHER_LOG_PATH     = Join-Path $global:CONFIG_DIR 'watcher.log'
$global:WATCHER_TASK_NAME    = 'LibreSpot\ReapplyWatcher'

$global:ThemeSchemes = [ordered]@{
    '(None - Marketplace Only)' = @('Default')
    'Sleek'       = @('Wealthy', 'Cherry', 'Coral', 'Deep', 'Greener', 'Deeper', 'Psycho', 'UltraBlack', 'Nord', 'Futura', 'Elementary', 'BladeRunner', 'Dracula', 'VantaBlack', 'RosePine', 'Eldritch', 'Catppuccin', 'AyuDark', 'TokyoNight')
    'Dribbblish'  = @('base', 'white', 'dark', 'dracula', 'nord-light', 'nord-dark', 'purple', 'samurai', 'beach-sunset', 'gruvbox', 'gruvbox-material-dark', 'rosepine', 'lunar', 'catppuccin-latte', 'catppuccin-frappe', 'catppuccin-macchiato', 'catppuccin-mocha', 'tokyo-night', 'kanagawa')
    'Ziro'        = @('blue-dark', 'blue-light', 'gray-dark', 'gray-light', 'green-dark', 'green-light', 'orange-dark', 'orange-light', 'purple-dark', 'purple-light', 'red-dark', 'red-light', 'rose-pine', 'rose-pine-moon', 'rose-pine-dawn', 'tokyo-night')
    'text'        = @('Spotify', 'Spicetify', 'CatppuccinMocha', 'CatppuccinMacchiato', 'CatppuccinLatte', 'Dracula', 'Gruvbox', 'Kanagawa', 'Nord', 'Rigel', 'RosePine', 'RosePineMoon', 'RosePineDawn', 'Solarized', 'TokyoNight', 'TokyoNightStorm', 'ForestGreen', 'EverforestDarkHard', 'EverforestDarkMedium', 'EverforestDarkSoft')
    'StarryNight' = @('Base', 'Cotton-candy', 'Forest', 'Galaxy', 'Orange', 'Sky', 'Sunrise')
    'Turntable'   = @('turntable')
    'Blackout'    = @('def')
    'Blossom'     = @('dark')
    'BurntSienna' = @('Base')
    'Default'     = @('Ocean')
    'Dreary'      = @('Psycho', 'Deeper', 'BIB', 'Mono', 'Golden', 'Graytone-Blue')
    'Flow'        = @('Pink', 'Green', 'Silver', 'Violet', 'Ocean')
    'Matte'       = @('matte', 'periwinkle', 'periwinkle-dark', 'porcelain', 'rose-pine-moon', 'gray-dark1', 'gray-dark2', 'gray-dark3', 'gray', 'gray-light')
    'Nightlight'  = @('Nightlight Colors')
    'Onepunch'    = @('dark', 'light', 'legacy')
    'SharkBlue'   = @('Base')
    # Community themes (downloaded from individual GitHub repos)
    'Catppuccin'  = @('mocha', 'macchiato', 'frappe', 'latte')
    'Comfy'       = @('Comfy', 'Mono', 'Chromatic')
    'Bloom'       = @('dark', 'light', 'darkMono', 'darkGreen', 'coffee', 'comfy', 'violet')
    'Lucid'       = @('dark', 'light', 'dark-green', 'coffee', 'comfy', 'dark-fluent', 'greenland', 'biscuit', 'macos', 'rosepine', 'dracula', 'dracula-pro')
    'Hazy'        = @('dark', 'light')
}

$global:BuiltInExtensionNames = @(
    'fullAppDisplay.js',
    'shuffle+.js',
    'trashbin.js',
    'keyboardShortcut.js',
    'bookmark.js',
    'loopyLoop.js',
    'popupLyrics.js',
    'autoSkipVideo.js',
    'autoSkipExplicit.js',
    'webnowplaying.js'
)

$global:CommunityExtensions = @{
    'hidePodcasts.js'     = @{ Url = 'https://raw.githubusercontent.com/theRealPadster/spicetify-hide-podcasts/main/dist/hidePodcasts.js';   Source = 'theRealPadster/spicetify-hide-podcasts' }
    'beautifulLyrics.js'  = @{ Url = 'https://raw.githubusercontent.com/surfbryce/beautiful-lyrics/main/dist/beautifulLyrics.js';           Source = 'surfbryce/beautiful-lyrics' }
    'playlistIcons.js'    = @{ Url = 'https://raw.githubusercontent.com/jeroentvb/spicetify-playlist-icons/main/dist/playlistIcons.js';     Source = 'jeroentvb/spicetify-playlist-icons' }
    'songStats.js'        = @{ Url = 'https://raw.githubusercontent.com/Shinyhero36/spicetify-song-stats/main/dist/songStats.js';           Source = 'Shinyhero36/spicetify-song-stats' }
    'volumePercentage.js' = @{ Url = 'https://raw.githubusercontent.com/daksh2k/spicetify-stuff/main/Extensions/volumePercentage.js';       Source = 'daksh2k/spicetify-stuff' }
}
$global:CommunityExtensionNames = @($global:CommunityExtensions.Keys)
$global:AllManagedExtensionNames = $global:BuiltInExtensionNames + $global:CommunityExtensionNames

$global:CommunityThemeRepos = @{
    'Catppuccin' = @{ Owner = 'catppuccin'; Repo = 'spicetify';       Branch = 'main'; ThemeFolder = '.' }
    'Comfy'      = @{ Owner = 'Comfy-Themes'; Repo = 'Spicetify';    Branch = 'main'; ThemeFolder = '.' }
    'Bloom'      = @{ Owner = 'nimsandu'; Repo = 'spicetify-bloom';   Branch = 'main'; ThemeFolder = '.' }
    'Lucid'      = @{ Owner = 'sanoojes'; Repo = 'Spicetify-Lucid';   Branch = 'main'; ThemeFolder = '.' }
    'Hazy'       = @{ Owner = 'Astromations'; Repo = 'Hazy';          Branch = 'main'; ThemeFolder = '.' }
}

$global:ThemesNeedingJS = @('Dribbblish', 'StarryNight', 'Turntable', 'Catppuccin', 'Comfy', 'Bloom', 'Lucid', 'Hazy')

$global:EasyDefaults = @{
    SpotX_NewTheme = $true
    SpotX_PodcastsOff = $true
    SpotX_BlockUpdate = $true
    SpotX_AdSectionsOff = $true
    SpotX_Premium = $false
    SpotX_LyricsEnabled = $true
    SpotX_LyricsTheme = 'spotify'
    SpotX_TopSearch = $false
    SpotX_RightSidebarOff = $false
    SpotX_RightSidebarClr = $false
    SpotX_CanvasHomeOff = $false
    SpotX_HomeSubOff = $false
    SpotX_DisableStartup = $true
    SpotX_NoShortcut = $false
    SpotX_CacheLimit = 0
    SpotX_Plus = $false
    SpotX_NewFullscreen = $false
    SpotX_FunnyProgress = $false
    SpotX_ExpSpotify = $false
    SpotX_LyricsBlock = $false
    SpotX_OldLyrics = $false
    SpotX_HideColIconOff = $false
    SpotX_SendVersionOff = $true
    SpotX_StartSpoti = $false
    SpotX_DevTools = $false
    SpotX_Mirror = $false
    SpotX_DownloadMethod = ''
    SpotX_ConfirmUninstall = $false
    SpotX_SpotifyVersionId = 'auto'
    Spicetify_Theme = '(None - Marketplace Only)'
    Spicetify_Scheme = 'Default'
    Spicetify_Marketplace = $true
    Spicetify_Extensions = @('fullAppDisplay.js', 'shuffle+.js', 'trashbin.js')
    CleanInstall = $true
    LaunchAfter = $true
    # Track 4.2 auto-reapply watcher preference. The backend reads this so it
    # knows whether to honor a user's saved "keep the watcher on" choice when
    # re-saving the config from the WPF shell.
    AutoReapply_Enabled = $false
}

$global:SpotXLyricsThemes = @(
    'spotify', 'blueberry', 'blue', 'discord', 'forest', 'fresh', 'github', 'lavender',
    'orange', 'pumpkin', 'purple', 'red', 'strawberry', 'turquoise', 'yellow', 'oceano',
    'royal', 'krux', 'pinkle', 'zing', 'radium', 'sandbar', 'postlight', 'relish',
    'drot', 'default', 'spotify#2'
)

$global:SpotifyVersionManifest = @(
    @{ Id = 'auto';            Version = '' }
    @{ Id = '1.2.86.502';      Version = '1.2.86.502.g8cd7fb22' }
    @{ Id = '1.2.85.519';      Version = '1.2.85.519.g7c42e2e8' }
    @{ Id = '1.2.53.440.x86';  Version = '1.2.53.440.g7b2f582a' }
    @{ Id = '1.2.5.1006.win7'; Version = '1.2.5.1006.g22820f93' }
)
$global:SpotifyVersionIds = @($global:SpotifyVersionManifest | ForEach-Object { $_.Id })

function Write-EventLine {
    param(
        [string]$Kind,
        [string]$Level = 'INFO',
        [string]$Payload = ''
    )
    $cleanPayload = if ($null -eq $Payload) { '' } else { ([string]$Payload -replace "`r?`n", ' ') }
    [Console]::Out.WriteLine("@@LS@@|$Kind|$Level|$cleanPayload")
}

function Ensure-LogDirectory {
    if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
        New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
    }
}

function Write-Log {
    param(
        [string]$Message,
        [string]$Level = 'INFO'
    )
    $timestamped = "[{0}] [{1}] {2}" -f (Get-Date -Format 'HH:mm:ss'), $Level, $Message
    try {
        Ensure-LogDirectory
        [System.IO.File]::AppendAllText($global:LOG_PATH, $timestamped + [Environment]::NewLine)
    } catch {}
    Write-EventLine -Kind 'log' -Level $Level -Payload $Message
}

function Update-BackendState {
    param(
        [int]$Progress,
        [string]$Status,
        [string]$Step
    )
    Write-EventLine -Kind 'progress' -Payload ([string]$Progress)
    if ($Status) { Write-EventLine -Kind 'status' -Payload $Status }
    if ($Step) { Write-EventLine -Kind 'step' -Payload $Step }
}

function ConvertTo-PlainHashtable {
    param([object]$InputObject)
    $result = @{}
    if ($null -eq $InputObject) { return $result }
    if ($InputObject -is [hashtable]) {
        foreach ($key in $InputObject.Keys) { $result[[string]$key] = $InputObject[$key] }
        return $result
    }
    foreach ($property in $InputObject.PSObject.Properties) {
        if ($property.Value -is [System.Collections.IEnumerable] -and $property.Value -isnot [string]) {
            $result[$property.Name] = @($property.Value)
        } else {
            $result[$property.Name] = $property.Value
        }
    }
    return $result
}

function ConvertTo-ConfigBoolean {
    param(
        [object]$Value,
        [bool]$Default = $false
    )
    if ($null -eq $Value) { return $Default }
    if ($Value -is [bool]) { return [bool]$Value }
    if ($Value -is [int] -or $Value -is [long]) { return ([int64]$Value -ne 0) }
    $text = ([string]$Value).Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($text)) { return $Default }
    switch -Regex ($text) {
        '^(1|true|yes|on)$' { return $true }
        '^(0|false|no|off)$' { return $false }
        default { return $Default }
    }
}

function ConvertTo-ConfigInt {
    param(
        [object]$Value,
        [int]$Default = 0,
        [int]$Minimum = [int]::MinValue,
        [int]$Maximum = [int]::MaxValue
    )
    $parsed = 0
    if ($null -eq $Value -or -not [int]::TryParse([string]$Value, [ref]$parsed)) {
        $parsed = $Default
    }
    if ($parsed -lt $Minimum) { $parsed = $Minimum }
    if ($parsed -gt $Maximum) { $parsed = $Maximum }
    return $parsed
}

function Normalize-LibreSpotConfig {
    param([hashtable]$Config)

    $normalized = @{ Mode = 'Easy' }
    foreach ($key in $global:EasyDefaults.Keys) {
        $defaultValue = $global:EasyDefaults[$key]
        if ($defaultValue -is [System.Collections.IEnumerable] -and $defaultValue -isnot [string]) {
            $normalized[$key] = @($defaultValue)
        } else {
            $normalized[$key] = $defaultValue
        }
    }

    if ($Config -and $Config.ContainsKey('Mode')) {
        $mode = [string]$Config.Mode
        if ($mode -in @('Easy', 'Custom')) { $normalized.Mode = $mode }
    }

    $booleanKeys = @(
        'CleanInstall', 'LaunchAfter',
        'SpotX_NewTheme', 'SpotX_PodcastsOff', 'SpotX_BlockUpdate', 'SpotX_AdSectionsOff',
        'SpotX_Premium', 'SpotX_LyricsEnabled', 'SpotX_TopSearch', 'SpotX_RightSidebarOff',
        'SpotX_RightSidebarClr', 'SpotX_CanvasHomeOff', 'SpotX_HomeSubOff', 'SpotX_DisableStartup',
        'SpotX_NoShortcut', 'SpotX_OldLyrics', 'SpotX_HideColIconOff', 'SpotX_Plus',
        'SpotX_NewFullscreen', 'SpotX_FunnyProgress', 'SpotX_ExpSpotify', 'SpotX_LyricsBlock',
        'SpotX_SendVersionOff', 'SpotX_StartSpoti', 'SpotX_DevTools', 'SpotX_Mirror', 'SpotX_ConfirmUninstall',
        'Spicetify_Marketplace', 'AutoReapply_Enabled'
    )
    foreach ($key in $booleanKeys) {
        if ($Config -and $Config.ContainsKey($key)) {
            $normalized[$key] = ConvertTo-ConfigBoolean -Value $Config[$key] -Default ([bool]$normalized[$key])
        }
    }

    if ($Config -and $Config.ContainsKey('SpotX_CacheLimit')) {
        $normalized.SpotX_CacheLimit = ConvertTo-ConfigInt -Value $Config.SpotX_CacheLimit -Default ([int]$normalized.SpotX_CacheLimit) -Minimum 0 -Maximum 50000
    }

    $dm = if ($Config -and $Config.ContainsKey('SpotX_DownloadMethod')) { [string]$Config.SpotX_DownloadMethod } else { [string]$normalized.SpotX_DownloadMethod }
    $dm = $dm.Trim().ToLowerInvariant()
    if ($dm -notin @('','curl','webclient')) { $dm = '' }
    $normalized.SpotX_DownloadMethod = $dm

    $svid = if ($Config -and $Config.ContainsKey('SpotX_SpotifyVersionId')) { [string]$Config.SpotX_SpotifyVersionId } else { [string]$normalized.SpotX_SpotifyVersionId }
    if ([string]::IsNullOrWhiteSpace($svid) -or $svid -notin $global:SpotifyVersionIds) { $svid = 'auto' }
    $normalized.SpotX_SpotifyVersionId = $svid

    $lyricsTheme = if ($Config -and $Config.ContainsKey('SpotX_LyricsTheme')) { [string]$Config.SpotX_LyricsTheme } else { [string]$normalized.SpotX_LyricsTheme }
    if ([string]::IsNullOrWhiteSpace($lyricsTheme) -or $lyricsTheme -notin $global:SpotXLyricsThemes) {
        $lyricsTheme = [string]$global:EasyDefaults.SpotX_LyricsTheme
    }
    $normalized.SpotX_LyricsTheme = $lyricsTheme

    $themeName = if ($Config -and $Config.ContainsKey('Spicetify_Theme')) { [string]$Config.Spicetify_Theme } else { [string]$normalized.Spicetify_Theme }
    if ([string]::IsNullOrWhiteSpace($themeName) -or -not $global:ThemeSchemes.Contains($themeName)) {
        $themeName = [string]$global:EasyDefaults.Spicetify_Theme
    }
    $normalized.Spicetify_Theme = $themeName

    $availableSchemes = if ($global:ThemeSchemes.Contains($themeName)) { @($global:ThemeSchemes[$themeName]) } else { @('Default') }
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
        if ($name -notin $global:AllManagedExtensionNames) { continue }
        if (-not $extensions.Contains($name)) { $extensions.Add($name) }
    }
    $normalized.Spicetify_Extensions = @($extensions)

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

function Move-ConfigFileToQuarantine {
    param([string]$Reason)

    $configDirectory = Split-Path -Path $ConfigPath -Parent
    if ([string]::IsNullOrWhiteSpace($configDirectory)) {
        $configDirectory = $global:CONFIG_DIR
    }

    try {
        if (-not (Test-Path -LiteralPath $configDirectory)) {
            New-Item -Path $configDirectory -ItemType Directory -Force | Out-Null
        }

        if (Test-Path -LiteralPath $ConfigPath) {
            $stamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
            $quarantinePath = $null
            for ($attempt = 0; $attempt -lt 10; $attempt++) {
                $suffix = if ($attempt -eq 0) { '' } else { "-$attempt" }
                $candidatePath = Join-Path $configDirectory "config.corrupt.$stamp$suffix.json"
                if (-not (Test-Path -LiteralPath $candidatePath)) {
                    $quarantinePath = $candidatePath
                    break
                }
            }
            if (-not $quarantinePath) {
                $quarantinePath = Join-Path $configDirectory ("config.corrupt.{0}.json" -f [Guid]::NewGuid().ToString('N'))
            }

            Move-Item -LiteralPath $ConfigPath -Destination $quarantinePath -ErrorAction Stop
            Write-Log "Saved config was moved to $(Split-Path -Path $quarantinePath -Leaf) after a read failure." -Level 'WARN'
        }
    } catch {
        Write-Log 'LibreSpot could not move the unreadable config aside automatically.' -Level 'WARN'
    }

    if ($Reason) {
        Write-Log "Config reset: $Reason" -Level 'WARN'
    }
}

function Load-LibreSpotConfig {
    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        return (Normalize-LibreSpotConfig -Config @{})
    }
    try {
        $json = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        return (Normalize-LibreSpotConfig -Config (ConvertTo-PlainHashtable -InputObject $json))
    } catch {
        Write-Log "Saved config was unreadable, so LibreSpot is falling back to recommended defaults." -Level 'WARN'
        Move-ConfigFileToQuarantine -Reason $_.Exception.Message
        return (Normalize-LibreSpotConfig -Config @{})
    }
}

function Ensure-Admin {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator
    )
    if (-not $isAdmin) {
        throw 'LibreSpot needs administrator permission to modify Spotify. Launch the desktop app as administrator and try again.'
    }
}

function Write-WatcherLog {
    param([string]$Message, [string]$Level = 'INFO')
    try {
        Ensure-LogDirectory
        $line = "[{0}] [{1}] {2}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::AppendAllText($global:WATCHER_LOG_PATH, $line + [Environment]::NewLine, $utf8NoBom)
        if ((Test-Path -LiteralPath $global:WATCHER_LOG_PATH) -and (Get-Item -LiteralPath $global:WATCHER_LOG_PATH).Length -gt 1048576) {
            $keep = Get-Content -LiteralPath $global:WATCHER_LOG_PATH -Tail 500
            [System.IO.File]::WriteAllLines($global:WATCHER_LOG_PATH, $keep, $utf8NoBom)
        }
    } catch {}
}

function Get-WatcherState {
    if (-not (Test-Path -LiteralPath $global:WATCHER_STATE_PATH)) {
        return @{ LastKnownVersion = $null; LastRunAt = $null; LastOutcome = $null }
    }

    try {
        $raw = Get-Content -LiteralPath $global:WATCHER_STATE_PATH -Raw -ErrorAction Stop | ConvertFrom-Json
        return @{
            LastKnownVersion = [string]$raw.LastKnownVersion
            LastRunAt        = [string]$raw.LastRunAt
            LastOutcome      = [string]$raw.LastOutcome
        }
    } catch {
        return @{ LastKnownVersion = $null; LastRunAt = $null; LastOutcome = $null }
    }
}

function Set-WatcherState {
    param([hashtable]$State)
    try {
        Ensure-LogDirectory
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $json = $State | ConvertTo-Json -Compress
        [System.IO.File]::WriteAllText($global:WATCHER_STATE_PATH, $json, $utf8NoBom)
    } catch {
        Write-WatcherLog "State save failed: $($_.Exception.Message)" -Level 'WARN'
    }
}

function Get-InstalledSpotifyVersion {
    if (-not (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) { return $null }
    try { return (Get-Item -LiteralPath $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion }
    catch { return $null }
}

function Test-SpotifyRunning {
    try { return [bool](Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue) }
    catch { return $false }
}

function Get-WatcherLaunchCommand {
    $entry = [string]$PSCommandPath
    if ([string]::IsNullOrWhiteSpace($entry)) {
        try { $entry = [string]$MyInvocation.MyCommand.Path } catch {}
    }
    if ([string]::IsNullOrWhiteSpace($entry) -or -not (Test-Path -LiteralPath $entry -PathType Leaf)) {
        return $null
    }

    $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $ps -PathType Leaf)) { $ps = 'powershell.exe' }

    return @{
        Command   = $ps
        Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$entry`" -Action WatchAutoReapply -ConfigPath `"$global:CONFIG_PATH`""
        Entry     = $entry
    }
}

function Test-AutoReapplyTaskRegistered {
    try {
        $out = & schtasks.exe /Query /TN $global:WATCHER_TASK_NAME 2>$null
        return ($LASTEXITCODE -eq 0) -and ($out -and $out.Length -gt 0)
    } catch { return $false }
}

function Register-AutoReapplyTask {
    $launch = Get-WatcherLaunchCommand
    if (-not $launch) {
        Write-WatcherLog 'Register: no usable backend script path. Watcher not registered.' -Level 'ERROR'
        return $false
    }

    try { Unregister-AutoReapplyTask | Out-Null } catch {}

    $escapedCommand = [System.Security.SecurityElement]::Escape($launch.Command)
    $escapedArguments = [System.Security.SecurityElement]::Escape($launch.Arguments)
    $userId = $null
    try {
        $currentIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
        $userId = $currentIdentity.User.Value
    } catch {}
    if ([string]::IsNullOrWhiteSpace($userId)) {
        $userId = if ($env:USERDOMAIN -and $env:USERDOMAIN -ne $env:COMPUTERNAME) {
            "$env:USERDOMAIN\$env:USERNAME"
        } else { $env:USERNAME }
    }
    $userId = [System.Security.SecurityElement]::Escape($userId)

    $xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>LibreSpot</Author>
    <Description>LibreSpot reapplies SpotX automatically when Spotify updates itself.</Description>
    <URI>\LibreSpot\ReapplyWatcher</URI>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT2M</Delay>
      <Repetition>
        <Interval>PT30M</Interval>
        <Duration>PT0S</Duration>
        <StopAtDurationEnd>false</StopAtDurationEnd>
      </Repetition>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>$userId</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT30M</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$escapedCommand</Command>
      <Arguments>$escapedArguments</Arguments>
    </Exec>
  </Actions>
</Task>
"@

    $xmlPath = Join-Path $global:CONFIG_DIR "watcher-task.xml"
    try {
        Ensure-LogDirectory
        [System.IO.File]::WriteAllText($xmlPath, $xml, [System.Text.Encoding]::Unicode)
        $output = & schtasks.exe /Create /TN $global:WATCHER_TASK_NAME /XML $xmlPath /F 2>&1
        $ok = ($LASTEXITCODE -eq 0)
        if ($ok) {
            Write-WatcherLog "Register: scheduled task created for $($launch.Entry)"
        } else {
            Write-WatcherLog "Register failed (exit $LASTEXITCODE): $($output -join ' ')" -Level 'ERROR'
        }
        return $ok
    } catch {
        Write-WatcherLog "Register exception: $($_.Exception.Message)" -Level 'ERROR'
        return $false
    } finally {
        try { if (Test-Path -LiteralPath $xmlPath) { Remove-Item -LiteralPath $xmlPath -Force -ErrorAction SilentlyContinue } } catch {}
    }
}

function Unregister-AutoReapplyTask {
    try {
        $null = & schtasks.exe /Delete /TN $global:WATCHER_TASK_NAME /F 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-WatcherLog 'Unregister: scheduled task removed'
            return $true
        }
        return $false
    } catch { return $false }
}

function Save-LibreSpotConfig {
    param([hashtable]$Config)

    $tempPath = $null
    $backupPath = $null
    try {
        Ensure-LogDirectory
        $tempPath = Join-Path $global:CONFIG_DIR ("config.{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
        $backupPath = Join-Path $global:CONFIG_DIR ("config.{0}.bak" -f [Guid]::NewGuid().ToString('N'))
        $normalizedConfig = Normalize-LibreSpotConfig -Config $Config
        $json = [ordered]@{}
        foreach ($key in $normalizedConfig.Keys) { $json[$key] = $normalizedConfig[$key] }
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($tempPath, ($json | ConvertTo-Json -Depth 4), $utf8)

        if (Test-Path -LiteralPath $global:CONFIG_PATH) {
            try {
                [System.IO.File]::Replace($tempPath, $global:CONFIG_PATH, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                Remove-Item -LiteralPath $global:CONFIG_PATH -Force -ErrorAction Stop
                [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
            }
        } else {
            [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
        }
        return $true
    } catch {
        Write-Log "Config save failed: $($_.Exception.Message)" -Level 'WARN'
        if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
        if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
        return $false
    }
}

function Set-AutoReapplyConfigPreference {
    param([bool]$Enabled)

    $config = Load-LibreSpotConfig
    if (-not $config) {
        $config = Normalize-LibreSpotConfig -Config @{}
    }
    $config['AutoReapply_Enabled'] = $Enabled
    if (-not (Save-LibreSpotConfig -Config $config)) {
        throw 'LibreSpot could not save the auto-reapply preference.'
    }
}

function Invoke-HeadlessReapply {
    param([hashtable]$Config)
    if (-not $Config) { throw 'Invoke-HeadlessReapply: missing config.' }

    $destination = New-LibreSpotTempFile -Name 'spotx_watcher.ps1'
    $watcher = Start-SpotifyWindowWatcher
    try {
        Write-WatcherLog 'Downloading pinned SpotX for watcher reapply'
        Download-FileSafe -Uri $global:URL_SPOTX -OutFile $destination
        Confirm-FileHash -Path $destination -ExpectedHash $global:PinnedReleases.SpotX.SHA256 -Label 'SpotX run.ps1'
        $params = Build-SpotXParams -Config $Config
        Write-WatcherLog "Invoking SpotX with: $params"
        Invoke-ExternalScriptIsolated -FilePath $destination -Arguments $params
        Reapply-SavedSpicetifySetup -Config $Config
        Write-WatcherLog 'Auto-reapply completed successfully.' -Level 'SUCCESS'
    } finally {
        Stop-SpotifyWindowWatcher -Watcher $watcher
        Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-AutoReapplyWatcher {
    Write-WatcherLog '--- Watcher tick ---'

    $saved = $null
    try { $saved = Load-LibreSpotConfig } catch { Write-WatcherLog "Config load failed: $($_.Exception.Message)" -Level 'ERROR' }
    if (-not $saved -or -not (ConvertTo-ConfigBoolean -Value $saved['AutoReapply_Enabled'] -Default $false)) {
        Write-WatcherLog 'Auto-reapply preference is off; skipping.'
        return 0
    }

    $currentVersion = Get-InstalledSpotifyVersion
    if (-not $currentVersion) {
        Write-WatcherLog 'Spotify not installed; skipping.'
        return 0
    }

    $state = Get-WatcherState
    if (-not $state.LastKnownVersion) {
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Initialized' }
        Write-WatcherLog "Initialized last-known version to $currentVersion; no reapply on first tick."
        return 0
    }

    if ($currentVersion -eq $state.LastKnownVersion) {
        Write-WatcherLog "Spotify still at $currentVersion; nothing to do."
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'UpToDate' }
        return 0
    }

    Write-WatcherLog "Spotify version bump: $($state.LastKnownVersion) -> $currentVersion" -Level 'STEP'
    if (Test-SpotifyRunning) {
        Write-WatcherLog 'Spotify is running; deferring reapply to the next tick.'
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'DeferredSpotifyRunning' }
        return 0
    }

    try {
        Invoke-HeadlessReapply -Config $saved
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Reapplied' }
        return 0
    } catch {
        Write-WatcherLog "Reapply failed: $($_.Exception.Message)" -Level 'ERROR'
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = "Error: $($_.Exception.Message)" }
        return 1
    }
}

# Background watcher that keeps any Spotify / SpotifyInstaller window hidden while
# SpotX and Spicetify run. Several stages briefly launch Spotify (SpotX patching,
# first-run config generation, Spicetify backup) and those windows would otherwise
# pop up over the LibreSpot desktop shell and steal focus. The watcher runs in a
# dedicated runspace and polls every ~250ms for new MainWindowHandles so we catch
# the window the moment Spotify creates it.
function Start-SpotifyWindowWatcher {
    $iss = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault()
    $runspace = [runspacefactory]::CreateRunspace($iss)
    $runspace.ApartmentState = 'STA'
    $runspace.ThreadOptions  = 'ReuseThread'
    $runspace.Open()

    # Shared hashtable is how we signal "stop" from the main thread.
    $control = [hashtable]::Synchronized(@{ Running = $true })
    $runspace.SessionStateProxy.SetVariable('Control', $control)

    $ps = [PowerShell]::Create()
    $ps.Runspace = $runspace
    $null = $ps.AddScript({
        try {
            Add-Type -ErrorAction SilentlyContinue -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class LibreSpotWin32 {
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    public const int SW_HIDE = 0;
}
'@
        } catch {}

        while ($Control.Running) {
            try {
                $procs = Get-Process -Name 'Spotify', 'SpotifyInstaller', 'SpotifySetup' -ErrorAction SilentlyContinue
                foreach ($p in $procs) {
                    $handle = $p.MainWindowHandle
                    if ($handle -ne [IntPtr]::Zero) {
                        try { [LibreSpotWin32]::ShowWindowAsync($handle, [LibreSpotWin32]::SW_HIDE) | Out-Null } catch {}
                    }
                }
            } catch {}
            Start-Sleep -Milliseconds 250
        }
    })

    $handle = $ps.BeginInvoke()
    return [pscustomobject]@{
        Control  = $control
        Runspace = $runspace
        PowerShell = $ps
        AsyncHandle = $handle
    }
}

function Stop-SpotifyWindowWatcher {
    param($Watcher)
    if (-not $Watcher) { return }
    try { $Watcher.Control.Running = $false } catch {}
    try { $null = $Watcher.PowerShell.EndInvoke($Watcher.AsyncHandle) } catch {}
    try { $Watcher.PowerShell.Dispose() } catch {}
    try { $Watcher.Runspace.Close() } catch {}
    try { $Watcher.Runspace.Dispose() } catch {}
}

function Get-LibreSpotTempRoot {
    $root = Join-Path $global:TEMP_DIR 'LibreSpot'
    if (Test-Path -LiteralPath $root -PathType Leaf) {
        $root = Join-Path $global:TEMP_DIR ("LibreSpot-{0}" -f [System.Diagnostics.Process]::GetCurrentProcess().Id)
    }

    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        New-Item -Path $root -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    return $root
}

function New-LibreSpotTempFile {
    param([string]$Name)

    $fileName = if ([string]::IsNullOrWhiteSpace($Name)) { 'artifact.tmp' } else { $Name }
    return (Join-Path (Get-LibreSpotTempRoot) ("{0}-{1}" -f [Guid]::NewGuid().ToString('N'), $fileName))
}

function New-LibreSpotTempDirectory {
    param([string]$Name = 'workspace')

    $directoryName = if ([string]::IsNullOrWhiteSpace($Name)) { 'workspace' } else { $Name }
    $path = Join-Path (Get-LibreSpotTempRoot) ("{0}-{1}" -f [Guid]::NewGuid().ToString('N'), $directoryName)
    New-Item -Path $path -ItemType Directory -Force | Out-Null
    return $path
}

function Read-ProcessOutputDelta {
    param(
        [string]$Path,
        [long]$Offset = 0,
        [string]$Remainder = ''
    )
    $result = @{
        Offset = $Offset
        Remainder = $Remainder
        Lines = @()
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $result }
    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        $reader = $null
        try {
            if ($result.Offset -gt $stream.Length) { $result.Offset = 0; $result.Remainder = '' }
            $null = $stream.Seek($result.Offset, [System.IO.SeekOrigin]::Begin)
            $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
            $chunk = $reader.ReadToEnd()
            $result.Offset = $stream.Position
        } finally {
            if ($reader) { try { $reader.Dispose() } catch {} }
            try { $stream.Dispose() } catch {}
        }
        if ([string]::IsNullOrEmpty($chunk)) { return $result }
        $text = [string]$result.Remainder + $chunk
        $parts = $text -split "`r`n|`n|`r"
        $hasTrailingNewline = $text.EndsWith("`n") -or $text.EndsWith("`r")
        if ($hasTrailingNewline) {
            $result.Remainder = ''
            $result.Lines = @($parts | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        } elseif ($parts.Count -gt 0) {
            $result.Remainder = [string]$parts[-1]
            if ($parts.Count -gt 1) {
                $result.Lines = @($parts[0..($parts.Count - 2)] | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            }
        }
    } catch {}
    return $result
}

function Download-FileSafe {
    param(
        [string]$Uri,
        [string]$OutFile
    )
    Write-Log "Downloading: $Uri"
    $headers = @{ 'User-Agent' = "LibreSpot/$global:VERSION" }
    try {
        $outDir = Split-Path -Path $OutFile -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $OutFile) {
            Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
        }
        try {
            Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing -Headers $headers -TimeoutSec 120 -ErrorAction Stop
        } catch {
            Write-Log 'Web request failed, trying BITS...' -Level 'WARN'
            Import-Module BitsTransfer -ErrorAction SilentlyContinue
            $bitsJob = Start-BitsTransfer -Source $Uri -Destination $OutFile -Asynchronous -ErrorAction Stop
            $deadline = (Get-Date).AddSeconds(120)
            while ($bitsJob.JobState -in @('Transferring', 'Connecting', 'Queued', 'TransientError')) {
                if ((Get-Date) -gt $deadline) {
                    Remove-BitsTransfer $bitsJob -ErrorAction SilentlyContinue
                    throw 'BITS transfer timed out (120s)'
                }
                Start-Sleep -Milliseconds 500
            }
            if ($bitsJob.JobState -ne 'Transferred') {
                $jobState = $bitsJob.JobState
                Remove-BitsTransfer $bitsJob -ErrorAction SilentlyContinue
                throw "BITS state: $jobState"
            }
            Complete-BitsTransfer $bitsJob
        }
        if (-not (Test-Path -LiteralPath $OutFile)) { throw "Download produced no file: $OutFile" }
        if ((Get-Item -LiteralPath $OutFile).Length -eq 0) { throw "Download produced empty file: $OutFile" }
    } catch {
        if (Test-Path -LiteralPath $OutFile) {
            Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
        }
        throw
    }
}

function Confirm-FileHash {
    param(
        [string]$Path,
        [string]$ExpectedHash,
        [string]$Label
    )
    if ([string]::IsNullOrWhiteSpace($ExpectedHash)) {
        Write-Log "Hash verification skipped for $Label (no hash pinned)." -Level 'WARN'
        return
    }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $expected = $ExpectedHash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "SHA256 mismatch for ${Label}. Expected $expected but received $actual."
    }
    Write-Log "SHA256 verified: $Label"
}

function Invoke-ExternalScriptIsolated {
    param(
        [string]$FilePath,
        [string]$Arguments,
        [int]$TimeoutSeconds = 600
    )
    Write-Log "Spawning: $FilePath"
    $stdoutPath = Join-Path $global:TEMP_DIR ("LibreSpot-stdout-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stderrPath = Join-Path $global:TEMP_DIR ("LibreSpot-stderr-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stdoutState = @{ Offset = 0L; Remainder = '' }
    $stderrState = @{ Offset = 0L; Remainder = '' }
    $process = $null
    try {
        # Use the single-string ArgumentList form. The array form is tempting because
        # it auto-quotes each element, but on Windows PowerShell 5.1 combining
        # `-ArgumentList` (array) with `-RedirectStandardOutput` and `-Wait:$false`
        # returns a Process wrapper whose handle is released before ExitCode can be
        # read, so a successful SpotX run surfaces as a spurious "exited with code ."
        # failure. $FilePath is always a LibreSpot-generated temp path (no user input
        # reaches this callsite) and $Arguments comes from Build-SpotXParams which only
        # emits fixed flags, so the single-string form is safe here.
        $argString = "-NoProfile -ExecutionPolicy Bypass -File `"$FilePath`" $Arguments"
        $process = Start-Process -FilePath 'powershell.exe' -ArgumentList $argString -PassThru -Wait:$false -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -WindowStyle Hidden -ErrorAction Stop
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while (-not $process.HasExited) {
            if ((Get-Date) -gt $deadline) {
                Write-Log "Process exceeded ${TimeoutSeconds}s timeout and will be terminated." -Level 'WARN'
                try { $process.Kill() } catch {}
                try { $process.WaitForExit(5000) } catch {}
                throw "External process timed out after ${TimeoutSeconds} seconds."
            }
            $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
            $stdoutState = @{ Offset = $stdoutRead.Offset; Remainder = $stdoutRead.Remainder }
            foreach ($line in $stdoutRead.Lines) { Write-Log $line -Level 'OUT' }

            $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
            $stderrState = @{ Offset = $stderrRead.Offset; Remainder = $stderrRead.Remainder }
            foreach ($line in $stderrRead.Lines) { Write-Log "[STDERR] $line" -Level 'WARN' }
            Start-Sleep -Milliseconds 200
        }
        $process.WaitForExit()

        $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
        foreach ($line in $stdoutRead.Lines + @($stdoutRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log $line -Level 'OUT'
        }
        $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
        foreach ($line in $stderrRead.Lines + @($stderrRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log "[STDERR] $line" -Level 'WARN'
        }

        # Capture ExitCode defensively. If the Process wrapper has lost its handle
        # (a known PowerShell quirk under certain Start-Process parameter combinations)
        # the getter can return $null — and `$null -ne 0` evaluates to $true, which
        # would turn a successful run into a spurious failure. Treat null as success
        # but log a warning so we notice if the environment ever regresses to this.
        $exitCode = $null
        try { $exitCode = $process.ExitCode } catch { $exitCode = $null }

        if ($null -eq $exitCode) {
            Write-Log 'External process finished but ExitCode was unavailable; treating as success.' -Level 'WARN'
        } elseif ($exitCode -ne 0) {
            throw "Process exited with code $exitCode."
        }
    } finally {
        if ($process) { try { $process.Dispose() } catch {} }
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Check-ForUpdates {
    Write-Log 'Checking pinned dependencies against upstream releases...' -Level 'STEP'
    $headers = @{ 'User-Agent' = "LibreSpot/$global:VERSION" }
    $updates = @()

    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/SpotX-Official/SpotX/commits/main' -Headers $headers -TimeoutSec 15
        if ($rel.sha -ne $global:PinnedReleases.SpotX.Commit) {
            $updates += 'SpotX'
            Write-Log "SpotX has a newer commit available: $($rel.sha.Substring(0, 10))" -Level 'WARN'
        } else {
            Write-Log "SpotX is pinned to the latest tested commit."
        }
    } catch {
        Write-Log "SpotX update check failed: $($_.Exception.Message)" -Level 'WARN'
    }

    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/cli/releases/latest' -Headers $headers -TimeoutSec 15
        $latest = $rel.tag_name -replace '^v', ''
        if ($latest -ne $global:PinnedReleases.SpicetifyCLI.Version) {
            $updates += 'Spicetify CLI'
            Write-Log "Spicetify CLI update available: $($global:PinnedReleases.SpicetifyCLI.Version) -> $latest" -Level 'WARN'
        } else {
            Write-Log 'Spicetify CLI is up to date.'
        }
    } catch {
        Write-Log "Spicetify CLI update check failed: $($_.Exception.Message)" -Level 'WARN'
    }

    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/marketplace/releases/latest' -Headers $headers -TimeoutSec 15
        $latest = $rel.tag_name -replace '^v', ''
        if ($latest -ne $global:PinnedReleases.Marketplace.Version) {
            $updates += 'Marketplace'
            Write-Log "Marketplace update available: $($global:PinnedReleases.Marketplace.Version) -> $latest" -Level 'WARN'
        } else {
            Write-Log 'Marketplace is up to date.'
        }
    } catch {
        Write-Log "Marketplace update check failed: $($_.Exception.Message)" -Level 'WARN'
    }

    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/spicetify-themes/commits/master' -Headers $headers -TimeoutSec 15
        if ($rel.sha -ne $global:PinnedReleases.Themes.Commit) {
            $updates += 'Themes'
            Write-Log "Theme archive has a newer commit available: $($rel.sha.Substring(0, 10))" -Level 'WARN'
        } else {
            Write-Log 'Pinned theme archive is up to date.'
        }
    } catch {
        Write-Log "Themes update check failed: $($_.Exception.Message)" -Level 'WARN'
    }

    if ($updates.Count -eq 0) {
        Write-Log 'All pinned dependencies are current.' -Level 'SUCCESS'
    } else {
        Write-Log "$($updates.Count) dependency update(s) are available." -Level 'WARN'
    }
}

function Stop-SpotifyProcesses {
    param(
        [int]$MaxAttempts = 5,
        [int]$RetryDelay = 500
    )
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $processes = Get-Process -Name 'Spotify', 'SpotifyWebHelper', 'SpotifyMigrator', 'SpotifyCrashService' -ErrorAction SilentlyContinue
        if (-not $processes) { return }
        Write-Log "Stopping Spotify processes (attempt $attempt/$MaxAttempts)..."
        $processes | ForEach-Object {
            try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch {}
        }
        Start-Sleep -Milliseconds $RetryDelay
    }
}

function Unlock-SpotifyUpdateFolder {
    $updateDir = Join-Path $env:LOCALAPPDATA 'Spotify\Update'
    if (-not (Test-Path -LiteralPath $updateDir -PathType Container)) { return }
    try {
        $acl = Get-Acl $updateDir
        $changed = $false
        foreach ($rule in $acl.Access) {
            if ($rule.AccessControlType -eq 'Deny') {
                $null = $acl.RemoveAccessRule($rule)
                $changed = $true
            }
        }
        if ($changed) {
            Set-Acl $updateDir $acl
            Write-Log 'Unlocked Spotify update folder ACLs.'
        }
    } catch {
        Write-Log "Could not unlock Spotify update folder: $($_.Exception.Message)" -Level 'WARN'
    }
}

function Get-DesktopPath {
    try {
        $shell = (Get-ItemProperty 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders' -ErrorAction Stop).Desktop
        if ($shell) { $shell = [Environment]::ExpandEnvironmentVariables($shell) }
        if ($shell -and (Test-Path $shell)) { return $shell }
    } catch {}
    return [Environment]::GetFolderPath('Desktop')
}

function Test-SafeRemovalTarget {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    try {
        $resolved = (Get-Item -LiteralPath $Path -Force -ErrorAction Stop).FullName
    } catch {
        return $false
    }
    if ([string]::IsNullOrWhiteSpace($resolved)) { return $false }

    $normalized = $resolved.TrimEnd('\')
    $root = [System.IO.Path]::GetPathRoot($resolved).TrimEnd('\')
    if ($normalized -eq $root) { return $false }

    # Roots that must never be removed. Expanded beyond the obvious profile/system
    # roots to cover OneDrive-redirected Desktop/Documents, the Public Desktop used
    # by Start-Menu shortcuts, and ALLUSERSPROFILE / ProgramData for machine-wide
    # state. Missing these would mean a malformed config could nuke the whole profile.
    $blockedRaw = @(
        $env:USERPROFILE,
        $env:APPDATA,
        $env:LOCALAPPDATA,
        $env:TEMP,
        $env:SystemRoot,
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        $env:ProgramData,
        $env:ALLUSERSPROFILE,
        $env:PUBLIC,
        $env:OneDrive,
        $env:OneDriveConsumer,
        $env:OneDriveCommercial,
        [Environment]::GetFolderPath('Desktop'),
        [Environment]::GetFolderPath('Personal'),   # Documents
        [Environment]::GetFolderPath('CommonDesktopDirectory'),
        [Environment]::GetFolderPath('CommonStartMenu')
    )
    $blockedTargets = @($blockedRaw | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.TrimEnd('\') } | Sort-Object -Unique)

    $normalizedLower = $normalized.ToLowerInvariant()
    foreach ($blocked in $blockedTargets) {
        if ([string]::Equals($normalizedLower, $blocked.ToLowerInvariant(), [System.StringComparison]::OrdinalIgnoreCase)) {
            return $false
        }
    }
    return $true
}

function Remove-PathSafely {
    param(
        [string]$Path,
        [string]$Label
    )
    if ([string]::IsNullOrWhiteSpace($Path)) { return 0 }
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-Log "Refusing to remove unsafe target: $Path" -Level 'WARN'
        return 0
    }
    try {
        $null = & icacls.exe $Path /reset /T /C /Q 2>$null
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        Write-Log "Removed: $(if ($Label) { $Label } else { $Path })"
        return 1
    } catch {
        Write-Log "Failed to remove ${Path}: $($_.Exception.Message)" -Level 'WARN'
        return 0
    }
}

function Get-NormalizedPathString {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim())
    try { return ([System.IO.Path]::GetFullPath($expanded)).TrimEnd('\') }
    catch { return $expanded.TrimEnd('\') }
}

function Get-PathEntries {
    param([ValidateSet('User', 'Process')] [string]$Scope = 'User')
    $rawPath = if ($Scope -eq 'Process') { $env:PATH } else { [Environment]::GetEnvironmentVariable('PATH', $Scope) }
    if ([string]::IsNullOrWhiteSpace($rawPath)) { return @() }
    return @($rawPath -split ';' | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Set-PathEntries {
    param(
        [ValidateSet('User', 'Process')] [string]$Scope = 'User',
        [string[]]$Entries
    )
    $orderedEntries = [System.Collections.Generic.List[string]]::new()
    $seen = @{}
    foreach ($entry in @($Entries)) {
        if ([string]::IsNullOrWhiteSpace($entry)) { continue }
        $normalized = Get-NormalizedPathString -Path $entry
        if ([string]::IsNullOrWhiteSpace($normalized)) { continue }
        $key = $normalized.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        $orderedEntries.Add($entry.Trim())
    }
    $pathValue = ($orderedEntries -join ';')
    if ($Scope -eq 'Process') {
        $env:PATH = $pathValue
    } else {
        [Environment]::SetEnvironmentVariable('PATH', $pathValue, $Scope)
    }
}

function Add-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User', 'Process')] [string]$Scope = 'User'
    )
    $normalized = Get-NormalizedPathString -Path $Entry
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $false }
    $entries = @(Get-PathEntries -Scope $Scope)
    foreach ($existing in $entries) {
        $existingNormalized = Get-NormalizedPathString -Path $existing
        if ($existingNormalized -and $existingNormalized.ToLowerInvariant() -eq $normalized.ToLowerInvariant()) {
            return $false
        }
    }
    Set-PathEntries -Scope $Scope -Entries (@($entries) + @($Entry))
    return $true
}

function Remove-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User', 'Process')] [string]$Scope = 'User'
    )
    $normalized = Get-NormalizedPathString -Path $Entry
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $false }
    $entries = @(Get-PathEntries -Scope $Scope)
    $remaining = @()
    $removed = $false
    foreach ($existing in $entries) {
        $existingNormalized = Get-NormalizedPathString -Path $existing
        if ($existingNormalized -and $existingNormalized.ToLowerInvariant() -eq $normalized.ToLowerInvariant()) {
            $removed = $true
            continue
        }
        $remaining += $existing
    }
    if ($removed) {
        Set-PathEntries -Scope $Scope -Entries $remaining
    }
    return $removed
}

function Get-SpicetifyConfigEntries {
    $configPath = Join-Path $global:SPICETIFY_CONFIG_DIR 'config-xpui.ini'
    $entries = @{}
    if (-not (Test-Path -LiteralPath $configPath)) { return $entries }
    try {
        foreach ($line in Get-Content -LiteralPath $configPath -ErrorAction Stop) {
            if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$') {
                $entries[$Matches[1].Trim()] = $Matches[2].Trim()
            }
        }
    } catch {
        Write-Log "Could not read Spicetify config: $($_.Exception.Message)" -Level 'WARN'
    }
    return $entries
}

function Get-SpicetifyConfigListValue {
    param([string]$Key)
    $entries = Get-SpicetifyConfigEntries
    if (-not $entries.ContainsKey($Key)) { return @() }
    $raw = [string]$entries[$Key]
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }
    return @(
        $raw -split '\|' |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
    )
}

function ConvertTo-NativeArgumentString {
    param([string[]]$Arguments)

    $parts = @()
    foreach ($argument in @($Arguments)) {
        $value = if ($null -eq $argument) { '' } else { [string]$argument }
        if ($value.Length -gt 0 -and $value -notmatch '[\s"]') {
            $parts += $value
            continue
        }

        $builder = New-Object System.Text.StringBuilder
        [void]$builder.Append('"')
        $backslashes = 0
        foreach ($character in $value.ToCharArray()) {
            if ($character -eq [char]92) {
                $backslashes++
                continue
            }
            if ($character -eq [char]34) {
                if ($backslashes -gt 0) {
                    [void]$builder.Append(('\' * ($backslashes * 2)))
                    $backslashes = 0
                }
                [void]$builder.Append('\"')
                continue
            }
            if ($backslashes -gt 0) {
                [void]$builder.Append(('\' * $backslashes))
                $backslashes = 0
            }
            [void]$builder.Append($character)
        }
        if ($backslashes -gt 0) {
            [void]$builder.Append(('\' * ($backslashes * 2)))
        }
        [void]$builder.Append('"')
        $parts += $builder.ToString()
    }

    return ($parts -join ' ')
}

function Remove-ConsoleEscapeSequences {
    param([string]$Text)

    if ($null -eq $Text) { return '' }
    $escapePattern = [regex]::Escape([string][char]27) + '\[[0-?]*[ -/]*[@-~]'
    return [regex]::Replace([string]$Text, $escapePattern, '')
}

function Update-SpicetifyCliProgress {
    param([string]$Line)

    $plain = Remove-ConsoleEscapeSequences -Text $Line
    if ($plain -match 'Patching files\s*\[\s*(\d+)\s*/\s*(\d+)\s*\]') {
        $done = [int]$matches[1]
        $total = [Math]::Max(1, [int]$matches[2])
        $percent = [int][Math]::Min(99, [Math]::Floor(($done / $total) * 100))
        $progressValue = [int][Math]::Min(99, 86 + [Math]::Floor(($done / $total) * 12))
        Update-BackendState -Progress $progressValue -Status "Spicetify is patching Spotify files ($percent%)" -Step "Patching file $done of $total"
    } elseif ($plain -match 'Extracting backup|Preprocessing|Fetching remote CSS map|Patching files') {
        Update-BackendState -Progress 86 -Status 'Spicetify is preparing Spotify files' -Step 'Applying Spicetify setup'
    }
}

function Invoke-SpicetifyCli {
    param(
        [string[]]$Arguments,
        [string]$FailureMessage = 'Spicetify command failed.',
        [int]$TimeoutSeconds = 900
    )
    $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
    if (-not (Test-Path -LiteralPath $spicetifyExe)) {
        throw 'Spicetify CLI is not installed.'
    }

    $stdoutPath = New-LibreSpotTempFile -Name 'spicetify-stdout.log'
    $stderrPath = New-LibreSpotTempFile -Name 'spicetify-stderr.log'
    $stdoutState = @{ Offset = 0L; Remainder = '' }
    $stderrState = @{ Offset = 0L; Remainder = '' }
    $outputLines = @()
    $process = $null

    # Keep PowerShell from turning redirected native stderr into its own
    # terminating error. We stream both files and decide success from ExitCode.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $argumentString = ConvertTo-NativeArgumentString -Arguments $Arguments
        $process = Start-Process -FilePath $spicetifyExe -ArgumentList $argumentString -PassThru -Wait:$false -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -WindowStyle Hidden -ErrorAction Stop
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

        while (-not $process.HasExited) {
            $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
            $stdoutState = @{ Offset = $stdoutRead.Offset; Remainder = $stdoutRead.Remainder }
            foreach ($line in @($stdoutRead.Lines)) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $outputLines += $line
                Write-Log "  $line"
                Update-SpicetifyCliProgress -Line $line
            }

            $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
            $stderrState = @{ Offset = $stderrRead.Offset; Remainder = $stderrRead.Remainder }
            foreach ($line in @($stderrRead.Lines)) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $outputLines += $line
                Write-Log "  $line"
                Update-SpicetifyCliProgress -Line $line
            }

            if ((Get-Date) -gt $deadline) {
                Write-Log "Spicetify command exceeded ${TimeoutSeconds}s timeout and will be terminated." -Level 'WARN'
                try { $process.Kill() } catch {}
                try { $process.WaitForExit(5000) } catch {}
                $tail = if ($outputLines.Count -gt 0) {
                    $slice = if ($outputLines.Count -le 4) { $outputLines } else { $outputLines[-4..-1] }
                    ' Output: ' + ((($slice | ForEach-Object { Remove-ConsoleEscapeSequences -Text $_ }) -replace '\s+', ' ') -join ' | ')
                } else {
                    ''
                }
                throw "$FailureMessage Timed out after $TimeoutSeconds seconds.$tail"
            }

            Start-Sleep -Milliseconds 200
        }
        $process.WaitForExit()

        foreach ($streamState in @(
            @{ Path = $stdoutPath; State = $stdoutState },
            @{ Path = $stderrPath; State = $stderrState }
        )) {
            $read = Read-ProcessOutputDelta -Path $streamState.Path -Offset $streamState.State.Offset -Remainder $streamState.State.Remainder
            foreach ($line in @($read.Lines) + @($read.Remainder)) {
                if ([string]::IsNullOrWhiteSpace($line)) { continue }
                $outputLines += $line
                Write-Log "  $line"
                Update-SpicetifyCliProgress -Line $line
            }
        }

        $exitCode = $null
        try { $exitCode = $process.ExitCode } catch { $exitCode = $null }
        if ($null -eq $exitCode) {
            Write-Log 'Spicetify process finished but ExitCode was unavailable; treating as success.' -Level 'WARN'
        } elseif ($exitCode -ne 0) {
            $tail = if ($outputLines.Count -gt 0) {
                $slice = if ($outputLines.Count -le 4) { $outputLines } else { $outputLines[-4..-1] }
                ' Output: ' + ((($slice | ForEach-Object { Remove-ConsoleEscapeSequences -Text $_ }) -replace '\s+', ' ') -join ' | ')
            } else {
                ''
            }
            throw "$FailureMessage Exit code: $exitCode.$tail"
        }
    } finally {
        $ErrorActionPreference = $previousPreference
        if ($process) { try { $process.Dispose() } catch {} }
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Test-SpicetifyCliInstalled {
    $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
    return (Test-Path -LiteralPath $spicetifyExe)
}

function Restore-SpotifyIfSpicetifyPresent {
    param(
        [string]$FailureMessage,
        [string]$MissingMessage
    )

    if (-not (Test-SpicetifyCliInstalled)) {
        if ($MissingMessage) {
            Write-Log $MissingMessage -Level 'WARN'
        }
        return $false
    }

    Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage $FailureMessage
    return $true
}

function Sync-SpicetifyListSetting {
    param(
        [string]$Key,
        [string[]]$DesiredItems,
        [string[]]$ManagedItems
    )
    $desired = @($DesiredItems | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $managed = @($ManagedItems | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $current = @(Get-SpicetifyConfigListValue -Key $Key)

    $currentLookup = @{}
    foreach ($item in $current) { $currentLookup[$item.ToLowerInvariant()] = $true }

    $desiredLookup = @{}
    foreach ($item in $desired) { $desiredLookup[$item.ToLowerInvariant()] = $true }

    $managedLookup = @{}
    foreach ($item in $managed) { $managedLookup[$item.ToLowerInvariant()] = $true }

    $changed = $false
    foreach ($item in $desired) {
        if ($currentLookup.ContainsKey($item.ToLowerInvariant())) { continue }
        Invoke-SpicetifyCli -Arguments @('config', $Key, $item, '--bypass-admin') -FailureMessage "Could not enable $Key item '$item'."
        Write-Log "Enabled $Key item: $item"
        $changed = $true
    }

    foreach ($item in $current) {
        $itemKey = $item.ToLowerInvariant()
        if (-not $managedLookup.ContainsKey($itemKey)) { continue }
        if ($desiredLookup.ContainsKey($itemKey)) { continue }
        Invoke-SpicetifyCli -Arguments @('config', $Key, "$item-", '--bypass-admin') -FailureMessage "Could not remove $Key item '$item'."
        Write-Log "Removed $Key item: $item"
        $changed = $true
    }

    if (-not $changed) {
        Write-Log "No $Key changes were needed."
    }
}

function Module-NukeSpotify {
    Write-Log 'Running the full Spotify cleanup path...' -Level 'STEP'
    $removedCount = 0

    Update-BackendState -Progress 5 -Status 'Preparing cleanup' -Step 'Closing Spotify processes'
    Stop-SpotifyProcesses

    Update-BackendState -Progress 10 -Status 'Removing Microsoft Store edition if present' -Step 'Checking installed packages'
    try {
        if ($PSVersionTable.PSVersion.Major -ge 7) { Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue }
        $storeApp = Get-AppxPackage -Name 'SpotifyAB.SpotifyMusic' -ErrorAction SilentlyContinue
        if ($storeApp) {
            $savedProgress = $ProgressPreference
            $ProgressPreference = 'SilentlyContinue'
            try { Remove-AppxPackage -Package $storeApp.PackageFullName -ErrorAction Stop } finally { $ProgressPreference = $savedProgress }
            Write-Log 'Removed the Microsoft Store Spotify package.'
            $removedCount++
        } else {
            Write-Log 'No Microsoft Store Spotify package was detected.'
        }
    } catch {
        Write-Log "Store package removal failed: $($_.Exception.Message)" -Level 'WARN'
    }

    Update-BackendState -Progress 20 -Status 'Running the native Spotify uninstaller' -Step 'Removing desktop installation'
    $spotifyExe = Join-Path $env:APPDATA 'Spotify\Spotify.exe'
    if (Test-Path -LiteralPath $spotifyExe) {
        try {
            Unlock-SpotifyUpdateFolder
            # Use Start-Process with an array argument list instead of `cmd /c "quoted string"`.
            # The cmd form breaks if the username or path contains metacharacters that cmd
            # interprets (&, |, ^, quote-pairs). Start-Process escapes each argument cleanly.
            $uninstaller = Start-Process -FilePath $spotifyExe -ArgumentList @('/UNINSTALL', '/SILENT') -PassThru -Wait:$false -WindowStyle Hidden -ErrorAction Stop
            if ($uninstaller) {
                $null = $uninstaller.WaitForExit(60000)
                if (-not $uninstaller.HasExited) {
                    try { $uninstaller.Kill() } catch {}
                    Write-Log 'Native uninstaller did not exit within 60s; it was terminated.' -Level 'WARN'
                }
                try { $uninstaller.Dispose() } catch {}
            }
            Start-Sleep -Seconds 2
            Write-Log 'Native Spotify uninstaller completed.'
            $removedCount++
        } catch {
            Write-Log "Native uninstaller failed: $($_.Exception.Message)" -Level 'WARN'
        }
    }
    Stop-SpotifyProcesses -MaxAttempts 3

    Update-BackendState -Progress 40 -Status 'Cleaning files, shortcuts, and leftovers' -Step 'Removing desktop state'
    $desktopPath = Get-DesktopPath
    $targets = @(
        @{ Path = (Join-Path $env:APPDATA 'Spotify'); Label = 'Spotify roaming data' }
        @{ Path = (Join-Path $env:LOCALAPPDATA 'Spotify'); Label = 'Spotify local data' }
        @{ Path = (Join-Path $env:APPDATA 'spicetify'); Label = 'Spicetify roaming data' }
        @{ Path = (Join-Path $env:LOCALAPPDATA 'spicetify'); Label = 'Spicetify CLI data' }
        @{ Path = (Join-Path $desktopPath 'Spotify.lnk'); Label = 'Desktop shortcut' }
        @{ Path = (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Spotify.lnk'); Label = 'Start menu shortcut' }
    )
    foreach ($target in $targets) {
        $removedCount += Remove-PathSafely -Path $target.Path -Label $target.Label
    }

    Update-BackendState -Progress 65 -Status 'Cleaning registry and scheduled tasks' -Step 'Removing shell traces'
    foreach ($key in @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify',
        'HKCU:\Software\Spotify',
        'HKCU:\Software\Classes\spotify',
        'HKCU:\Software\Classes\spotify-client',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\Spotify.exe'
    )) {
        if (Test-Path $key) {
            try {
                Remove-Item -Path $key -Recurse -Force -ErrorAction Stop
                Write-Log "Removed registry key: $key"
                $removedCount++
            } catch {
                Write-Log "Failed to remove registry key $key" -Level 'WARN'
            }
        }
    }

    try {
        # Match only tasks that Spotify itself registers. The previous `-match 'Spotify'`
        # would also remove user-authored tasks that merely mention Spotify (e.g. a
        # "MySpotifyBackup" job), which is surprising and destructive.
        $spotifyTaskNames = @('SpotifyMigrator', 'SpotifyUpdateTask', 'Spotify')
        Get-ScheduledTask -ErrorAction SilentlyContinue |
            Where-Object { $_.TaskName -in $spotifyTaskNames -or $_.TaskName -like 'Spotify-*' } |
            ForEach-Object {
                try {
                    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -ErrorAction Stop
                    Write-Log "Removed scheduled task: $($_.TaskName)"
                } catch {}
            }
    } catch {
        Write-Log 'Scheduled task cleanup was skipped.' -Level 'WARN'
    }

    Update-BackendState -Progress 85 -Status 'Performing final verification sweep' -Step 'Confirming removal'
    foreach ($leftover in @((Join-Path $env:APPDATA 'Spotify'), (Join-Path $env:LOCALAPPDATA 'Spotify'))) {
        if (Test-Path $leftover) {
            $removedCount += Remove-PathSafely -Path $leftover -Label 'Spotify cleanup retry'
        }
    }

    Write-Log "Cleanup complete. $removedCount item(s) were removed." -Level 'SUCCESS'
}

function Build-SpotXParams {
    param($Config)
    $params = @()
    $params += '-confirm_uninstall_ms_spoti'
    $params += '-confirm_spoti_recomended_over'
    if ($Config.SpotX_NewTheme) { $params += '-new_theme' }
    if ($Config.SpotX_PodcastsOff) { $params += '-podcasts_off' } else { $params += '-podcasts_on' }
    if ($Config.SpotX_AdSectionsOff) { $params += '-adsections_off' }
    if ($Config.SpotX_BlockUpdate) { $params += '-block_update_on' } else { $params += '-block_update_off' }
    if ($Config.SpotX_Premium) { $params += '-premium' }
    if ($Config.SpotX_DisableStartup) { $params += '-DisableStartup' }
    if ($Config.SpotX_NoShortcut) { $params += '-no_shortcut' }
    if ($Config.SpotX_LyricsEnabled) {
        $params += "-lyrics_stat $($Config.SpotX_LyricsTheme)"
        if ($Config.SpotX_LyricsBlock) {
            $params += '-lyrics_block'
        } elseif ($Config.SpotX_OldLyrics) {
            $params += '-old_lyrics'
        }
    }
    if ($Config.SpotX_TopSearch) { $params += '-topsearchbar' }
    if ($Config.SpotX_RightSidebarOff) { $params += '-rightsidebar_off' }
    if ($Config.SpotX_RightSidebarClr) { $params += '-rightsidebarcolor' }
    if ($Config.SpotX_CanvasHomeOff) { $params += '-canvashome_off' }
    if ($Config.SpotX_HomeSubOff) { $params += '-homesub_off' }
    if ($Config.SpotX_HideColIconOff) { $params += '-hide_col_icon_off' }
    if ($Config.SpotX_Plus) { $params += '-plus' }
    if ($Config.SpotX_NewFullscreen) { $params += '-newFullscreenMode' }
    if ($Config.SpotX_FunnyProgress) { $params += '-funnyprogressBar' }
    if ($Config.SpotX_ExpSpotify) { $params += '-exp_spotify' }
    if ($Config.SpotX_SendVersionOff) { $params += '-sendversion_off' }
    if ($Config.SpotX_StartSpoti) { $params += '-start_spoti' }
    if ($Config.SpotX_DevTools) { $params += '-devtools' }
    if ($Config.SpotX_Mirror) { $params += '-mirror' }
    if ($Config.SpotX_ConfirmUninstall) { $params += '-confirm_spoti_recomended_uninstall' }
    if (-not [string]::IsNullOrWhiteSpace([string]$Config.SpotX_DownloadMethod)) {
        $params += "-download_method $($Config.SpotX_DownloadMethod)"
    }
    $versionId = [string]$Config.SpotX_SpotifyVersionId
    if (-not [string]::IsNullOrWhiteSpace($versionId) -and $versionId -ne 'auto') {
        $entry = $global:SpotifyVersionManifest | Where-Object { $_.Id -eq $versionId } | Select-Object -First 1
        if ($entry -and -not [string]::IsNullOrWhiteSpace([string]$entry.Version)) {
            $params += "-version $($entry.Version)"
        }
    }
    if ($Config.SpotX_CacheLimit -ge 500) { $params += "-cache_limit $($Config.SpotX_CacheLimit)" }
    return ($params -join ' ')
}

function Module-InstallSpotX {
    param($Config)
    Write-Log "Installing SpotX v$($global:PinnedReleases.SpotX.Version)..." -Level 'STEP'
    $destination = New-LibreSpotTempFile -Name 'spotx_run.ps1'
    try {
        Download-FileSafe -Uri $global:URL_SPOTX -OutFile $destination
        Confirm-FileHash -Path $destination -ExpectedHash $global:PinnedReleases.SpotX.SHA256 -Label 'SpotX run.ps1'

        $params = Build-SpotXParams -Config $Config
        if (Test-Path $global:SPOTIFY_EXE_PATH) {
            Write-Log "Existing Spotify installation detected: $((Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion)"
        } else {
            Write-Log 'Spotify is not installed yet, so SpotX will download the recommended build.'
        }

        Invoke-ExternalScriptIsolated -FilePath $destination -Arguments $params
        if (-not (Test-Path $global:SPOTIFY_EXE_PATH)) {
            throw "SpotX finished but Spotify.exe was not found at $global:SPOTIFY_EXE_PATH."
        }

        $spotifyDir = Split-Path $global:SPOTIFY_EXE_PATH -Parent
        if (-not (Test-Path (Join-Path $spotifyDir 'chrome_elf.dll'))) {
            throw 'Spotify installation looks incomplete because chrome_elf.dll is missing.'
        }

        Write-Log 'SpotX patching completed successfully.' -Level 'SUCCESS'
        Write-Log 'Launching Spotify once to generate its base config files...'
        Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$global:SPOTIFY_EXE_PATH`""
        Start-Sleep -Seconds 6
        Stop-SpotifyProcesses -MaxAttempts 3
    } finally {
        Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallSpicetifyCLI {
    $version = $global:PinnedReleases.SpicetifyCLI.Version
    Write-Log "Installing Spicetify CLI v$version..." -Level 'STEP'
    New-Item -Path $global:SPICETIFY_DIR -ItemType Directory -Force | Out-Null
    $arch = switch ($env:PROCESSOR_ARCHITECTURE) { 'ARM64' { 'arm64' } default { 'x64' } }
    $zipUri = $global:URL_SPICETIFY_FMT -f $version, $arch
    $zipPath = New-LibreSpotTempFile -Name 'spicetify.zip'
    try {
        Download-FileSafe -Uri $zipUri -OutFile $zipPath
        Confirm-FileHash -Path $zipPath -ExpectedHash $global:PinnedReleases.SpicetifyCLI.SHA256[$arch] -Label "Spicetify CLI ($arch)"

        if (Test-Path -LiteralPath $global:SPICETIFY_DIR) {
            Get-ChildItem -LiteralPath $global:SPICETIFY_DIR -Force -ErrorAction SilentlyContinue | ForEach-Object {
                $null = Remove-PathSafely -Path $_.FullName -Label "Spicetify CLI: $($_.Name)"
            }
        }

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $global:SPICETIFY_DIR)

        if (-not (Test-Path (Join-Path $global:SPICETIFY_DIR 'spicetify.exe'))) {
            throw 'Spicetify CLI archive extracted without spicetify.exe.'
        }

        $null = Add-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'Process'
        if (Add-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'User') {
            Write-Log 'Added Spicetify CLI to the user PATH.'
        }
        Invoke-SpicetifyCli -Arguments @('config', '--bypass-admin') -FailureMessage 'Could not generate the initial Spicetify config.'
        Write-Log 'Spicetify CLI installed successfully.' -Level 'SUCCESS'
    } finally {
        Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallThemes {
    param($Config)
    $themeName = $Config.Spicetify_Theme
    if ($themeName -eq '(None - Marketplace Only)') {
        Write-Log 'No theme selected. Skipping theme installation.'
        return
    }
    Write-Log "Installing theme: $themeName..." -Level 'STEP'
    $themesDir = Join-Path $global:SPICETIFY_CONFIG_DIR 'Themes'
    if (-not (Test-Path -LiteralPath $themesDir)) {
        New-Item -Path $themesDir -ItemType Directory -Force | Out-Null
    }

    $isCommunity = $global:CommunityThemeRepos.ContainsKey($themeName)

    if ($isCommunity) {
        $repo = $global:CommunityThemeRepos[$themeName]
        $archiveUrl = "https://github.com/$($repo.Owner)/$($repo.Repo)/archive/refs/heads/$($repo.Branch).zip"
        $safeName = ($themeName -replace '[^a-zA-Z0-9_-]','_')
        $zipPath = New-LibreSpotTempFile -Name "community-theme-$safeName.zip"
        $unpackPath = New-LibreSpotTempDirectory -Name "community-theme-$safeName-unpack"
        try {
            Write-Log "Downloading community theme from $($repo.Owner)/$($repo.Repo)..."
            Download-FileSafe -Uri $archiveUrl -OutFile $zipPath
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $unpackPath)
            $root = Get-ChildItem -LiteralPath $unpackPath -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw "Community theme archive for '$themeName' did not contain a root folder." }
            $sourcePath = if ($repo.ThemeFolder -eq '.') { $root.FullName } else { Join-Path $root.FullName $repo.ThemeFolder }
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
                throw "Theme folder '$($repo.ThemeFolder)' not found in $($repo.Owner)/$($repo.Repo) archive."
            }
            $hasColorIni = Test-Path (Join-Path $sourcePath 'color.ini')
            $hasUserCss  = Test-Path (Join-Path $sourcePath 'user.css')
            if (-not ($hasColorIni -or $hasUserCss)) {
                throw "Community theme '$themeName' archive does not contain color.ini or user.css."
            }
            $destination = Join-Path $themesDir $themeName
            if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
            New-Item -Path $destination -ItemType Directory -Force | Out-Null
            $themeFiles = @('color.ini','user.css','theme.js','theme.script.js','assets','README.md')
            foreach ($tf in $themeFiles) {
                $tfSrc = Join-Path $sourcePath $tf
                if (Test-Path -LiteralPath $tfSrc) {
                    Copy-Item $tfSrc -Destination (Join-Path $destination $tf) -Recurse -Force
                }
            }
            Write-Log "Community theme '$themeName' copied to $destination"
        } finally {
            Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $unpackPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    } else {
        $zipPath = New-LibreSpotTempFile -Name 'themes.zip'
        $unpackPath = New-LibreSpotTempDirectory -Name 'themes-unpack'
        try {
            Download-FileSafe -Uri $global:URL_THEMES_REPO -OutFile $zipPath
            Confirm-FileHash -Path $zipPath -ExpectedHash $global:PinnedReleases.Themes.SHA256 -Label 'Themes archive'
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $unpackPath)
            $root = Get-ChildItem -LiteralPath $unpackPath -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw 'Pinned themes archive could not be unpacked safely.' }
            $sourcePath = Join-Path $root.FullName $themeName
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
                throw "Theme '$themeName' was not found in the pinned theme archive."
            }
            $destination = Join-Path $themesDir $themeName
            if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
            Copy-Item -Path $sourcePath -Destination $destination -Recurse -Force
            Write-Log "Theme copied to $destination"
        } finally {
            Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $unpackPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Invoke-SpicetifyCli -Arguments @('config', 'current_theme', $themeName, '--bypass-admin') -FailureMessage "Could not set Spicetify theme '$themeName'."
    Invoke-SpicetifyCli -Arguments @('config', 'color_scheme', $Config.Spicetify_Scheme, '--bypass-admin') -FailureMessage "Could not set color scheme '$($Config.Spicetify_Scheme)'."

    $needsThemeJs = $global:ThemesNeedingJS -contains $themeName
    $themeJs = if ($needsThemeJs) { '1' } else { '0' }
    Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '1', 'replace_colors', '1', 'overwrite_assets', '1', 'inject_theme_js', $themeJs, '--bypass-admin') -FailureMessage 'Could not enable theme assets.'

    Write-Log 'Theme assets copied and configured.' -Level 'SUCCESS'
}

function Download-CommunityExtensions {
    param($Config)
    $exts = @($Config.Spicetify_Extensions)
    $extDir = Join-Path $global:SPICETIFY_CONFIG_DIR 'Extensions'
    if (-not (Test-Path -LiteralPath $extDir)) { New-Item -Path $extDir -ItemType Directory -Force | Out-Null }
    foreach ($ext in $exts) {
        if (-not $global:CommunityExtensions.ContainsKey($ext)) { continue }
        $info = $global:CommunityExtensions[$ext]
        $destFile = Join-Path $extDir $ext
        try {
            Write-Log "Downloading community extension: $ext from $($info.Source)..."
            Download-FileSafe -Uri $info.Url -OutFile $destFile
            $head = Get-Content -LiteralPath $destFile -TotalCount 5 -ErrorAction SilentlyContinue
            $headStr = ($head -join "`n").TrimStart()
            if ($headStr -match '^<(!DOCTYPE|html)' -or $headStr -match '^404:') {
                Remove-Item -LiteralPath $destFile -Force -ErrorAction SilentlyContinue
                Write-Log "Community extension '$ext' appears to be an HTML error page. Skipping." -Level 'WARN'
                continue
            }
            Write-Log "Community extension '$ext' saved to $destFile"
        } catch {
            Write-Log "Could not download community extension '$ext': $($_.Exception.Message). Skipping." -Level 'WARN'
        }
    }
}

function Module-InstallExtensions {
    param($Config)
    $extensions = @($Config.Spicetify_Extensions)
    if ($extensions.Count -eq 0) {
        Write-Log 'No extensions selected. LibreSpot will remove previously managed extension toggles.' -Level 'STEP'
    } else {
        Write-Log "Enabling extensions: $($extensions -join ', ')." -Level 'STEP'
    }
    Download-CommunityExtensions -Config $Config
    Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems $extensions -ManagedItems $global:AllManagedExtensionNames
}

function Module-InstallMarketplace {
    param($Config)
    $managedApps = @('marketplace')
    $marketplaceDirs = @(
        (Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps\marketplace'),
        (Join-Path $global:SPICETIFY_DIR 'CustomApps\marketplace')
    )

    if (-not $Config.Spicetify_Marketplace) {
        Write-Log 'Marketplace is disabled, so LibreSpot will remove any managed Marketplace state.' -Level 'STEP'
        foreach ($dir in $marketplaceDirs) {
            $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        return
    }

    Write-Log 'Installing Marketplace...' -Level 'STEP'
    $customAppsDir = Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps'
    if (-not (Test-Path -LiteralPath $customAppsDir)) {
        $customAppsDir = Join-Path $global:SPICETIFY_DIR 'CustomApps'
    }
    New-Item -Path $customAppsDir -ItemType Directory -Force | Out-Null

    $marketplaceDir = Join-Path $customAppsDir 'marketplace'
    $zipPath = New-LibreSpotTempFile -Name 'marketplace.zip'
    $unpackPath = New-LibreSpotTempDirectory -Name 'marketplace-unpack'

    if (Test-Path -LiteralPath $marketplaceDir) {
        $null = Remove-PathSafely -Path $marketplaceDir -Label 'Marketplace app'
    }
    New-Item -Path $marketplaceDir -ItemType Directory -Force | Out-Null

    try {
        Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $zipPath
        Confirm-FileHash -Path $zipPath -ExpectedHash $global:PinnedReleases.Marketplace.SHA256 -Label 'Marketplace archive'

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $unpackPath)
        $source = if (Test-Path -LiteralPath (Join-Path $unpackPath 'marketplace-dist')) { Join-Path $unpackPath 'marketplace-dist\*' } else { Join-Path $unpackPath '*' }
        Copy-Item -Path $source -Destination $marketplaceDir -Recurse -Force

        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @('marketplace') -ManagedItems $managedApps
        Write-Log 'Marketplace enabled successfully.' -Level 'SUCCESS'
    } finally {
        Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $unpackPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-SpicetifyDiagnosticSnapshot {
    $snapshot = [ordered]@{}
    $configPath = Join-Path $global:SPICETIFY_CONFIG_DIR 'config-xpui.ini'
    if (Test-Path -LiteralPath $configPath) {
        try {
            foreach ($line in Get-Content -LiteralPath $configPath -ErrorAction Stop) {
                if ($line -match '^\s*(spotify_path|prefs_path)\s*=\s*(.+?)\s*$') {
                    $snapshot[$Matches[1]] = $Matches[2].Trim()
                }
            }
        } catch {}
    }
    $snapshot['xpui_spa_exists'] = Test-Path -LiteralPath (Join-Path (Split-Path $global:SPOTIFY_EXE_PATH -Parent) 'Apps\xpui.spa')
    $snapshot['spotify_exe_exists'] = Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH
    return $snapshot
}

function Module-ApplySpicetify {
    param($Config)
    Write-Log 'Applying Spicetify changes...' -Level 'STEP'

    # Marketplace-only mode: disable theme injection before apply so the apply step
    # does not try to inject any theme CSS/JS.
    if ($Config.Spicetify_Theme -eq '(None - Marketplace Only)') {
        try {
            Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '0', 'replace_colors', '0', 'overwrite_assets', '0', 'inject_theme_js', '0', '--bypass-admin') -FailureMessage 'Could not disable theme injection.'
        } catch {
            Write-Log "Pre-apply config tweak failed: $($_.Exception.Message)" -Level 'WARN'
        }
    }

    # Diagnostic snapshot. When apply fails silently (known SpotX+Spicetify
    # interop edge case) this is the only way to tell whether spotify_path is
    # correct and whether xpui.spa is actually present on disk.
    $diag = Get-SpicetifyDiagnosticSnapshot
    foreach ($key in $diag.Keys) {
        Write-Log "  diag: $key = $($diag[$key])"
    }

    # Make sure Spotify isn't holding any xpui.spa handles before Spicetify tries to
    # back it up. The earlier brief launch to generate configs should have been killed
    # already, but one final sweep is cheap insurance against "Spotify client is in
    # stock state" errors caused by a stale process still running.
    Stop-SpotifyProcesses -MaxAttempts 3

    # Run `spicetify backup apply` as a single combined command. Splitting it into two
    # invocations breaks on Spicetify CLI 2.43.1: the standalone `backup` subcommand
    # exits non-zero on fresh installs (reporting "Spotify version and backup version
    # are mismatched" with no prior backup present), leaving `apply` with nothing to
    # work from. The combined form matches the legacy LibreSpot.ps1 behavior and the
    # CLI's own "Please run 'spicetify backup apply'" hint.
    $applyError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('backup', 'apply', '--bypass-admin') -FailureMessage 'Could not backup and apply Spicetify changes.'
        Write-Log 'Spicetify applied successfully.' -Level 'SUCCESS'
        return
    } catch {
        $applyError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown Spicetify apply error.' }
        Write-Log "Spicetify apply failed: $applyError" -Level 'WARN'
    }

    # Apply failed — attempt rollback without nested try/catch so the reported
    # rollback status is accurate (the old nested form lied: a successful restore
    # threw a 'apply failed but restored' message that the inner catch caught and
    # re-reported as 'rollback also failed').
    Write-Log 'Attempting rollback to keep Spotify usable...' -Level 'WARN'
    $restoreError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage 'Could not restore Spotify after the failed apply.'
    } catch {
        $restoreError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown restore error.' }
    }

    if ([string]::IsNullOrWhiteSpace($restoreError)) {
        throw "Spicetify apply failed but LibreSpot restored Spotify to a usable state. Apply error: $applyError"
    } else {
        throw "Spicetify apply failed and rollback also failed. Apply error: $applyError | Rollback error: $restoreError"
    }
}

function Reapply-SavedSpicetifySetup {
    param($Config)

    if (-not (Test-SpicetifyCliInstalled)) {
        Write-Log 'Spicetify CLI is missing, so LibreSpot will reinstall it before restoring your saved setup.' -Level 'WARN'
        Module-InstallSpicetifyCLI
    }

    Module-InstallThemes -Config $Config
    Module-InstallExtensions -Config $Config
    Module-InstallMarketplace -Config $Config
    Module-ApplySpicetify -Config $Config
}

function Invoke-LibreSpotInstall {
    $config = Load-LibreSpotConfig
    Write-Log "--- LibreSpot installation started ($($config.Mode)) ---" -Level 'HEADER'
    $steps = @('SpotX', 'SpicetifyCLI', 'Themes', 'Extensions', 'Marketplace', 'Apply')
    if ($config.CleanInstall) { $steps = @('Cleanup') + $steps }

    $labels = @{
        Cleanup = 'Removing the old setup'
        SpotX = 'Applying SpotX'
        SpicetifyCLI = 'Installing Spicetify CLI'
        Themes = 'Adding bundled themes'
        Extensions = 'Preparing extensions'
        Marketplace = 'Installing Marketplace'
        Apply = 'Applying your setup'
    }

    # Hide any Spotify windows that SpotX/Spicetify briefly surface during patching
    # so the desktop shell stays in focus and Spotify never flashes over this window.
    $watcher = Start-SpotifyWindowWatcher
    try {
        $count = $steps.Count
        for ($index = 0; $index -lt $count; $index++) {
            $step = $steps[$index]
            $progress = [int](($index / [double]$count) * 100)
            Update-BackendState -Progress $progress -Status $labels[$step] -Step ("Step {0} of {1}" -f ($index + 1), $count)
            switch ($step) {
                'Cleanup' { Module-NukeSpotify }
                'SpotX' { Module-InstallSpotX -Config $config }
                'SpicetifyCLI' { Module-InstallSpicetifyCLI }
                'Themes' { Module-InstallThemes -Config $config }
                'Extensions' { Module-InstallExtensions -Config $config }
                'Marketplace' { Module-InstallMarketplace -Config $config }
                'Apply' { Module-ApplySpicetify -Config $config }
            }
        }
    } finally {
        Stop-SpotifyWindowWatcher -Watcher $watcher
    }

    if ($config.LaunchAfter -and (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) {
        Write-Log 'Launching Spotify...' -Level 'SUCCESS'
        # Launch via explorer.exe so Spotify starts in the desktop user context instead of
        # inheriting our elevated token. A directly-started Spotify would run as Administrator,
        # which Spotify explicitly warns against and which breaks drag-and-drop from Explorer
        # and some web-auth flows.
        Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$global:SPOTIFY_EXE_PATH`""
    }

    Update-BackendState -Progress 100 -Status 'Setup complete' -Step 'Spotify is ready'
    Write-Log '--- Installation complete ---' -Level 'SUCCESS'
}

function Invoke-LibreSpotMaintenance {
    switch ($Action) {
        'CheckUpdates' {
            Update-BackendState -Progress 15 -Status 'Checking upstream releases' -Step 'Comparing pinned versions'
            Check-ForUpdates
        }
        'Reapply' {
            Update-BackendState -Progress 15 -Status 'Refreshing SpotX' -Step 'Downloading pinned SpotX'
            $savedConfig = Load-LibreSpotConfig
            $destination = New-LibreSpotTempFile -Name 'spotx_run.ps1'
            $watcher = Start-SpotifyWindowWatcher
            try {
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $destination
                Confirm-FileHash -Path $destination -ExpectedHash $global:PinnedReleases.SpotX.SHA256 -Label 'SpotX run.ps1'
                $params = Build-SpotXParams -Config $savedConfig
                Invoke-ExternalScriptIsolated -FilePath $destination -Arguments $params

                Update-BackendState -Progress 60 -Status 'Restoring saved Spicetify state' -Step 'Rebuilding CLI, themes, extensions, and Marketplace'
                Reapply-SavedSpicetifySetup -Config $savedConfig
                Write-Log 'Saved Spicetify setup restored successfully.' -Level 'SUCCESS'
            } finally {
                Stop-SpotifyWindowWatcher -Watcher $watcher
                Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
            }
        }
        'RestoreVanilla' {
            Update-BackendState -Progress 35 -Status 'Restoring vanilla Spotify' -Step 'Removing active Spicetify customizations'
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Could not restore vanilla Spotify.' -MissingMessage 'Spicetify CLI was not found, so LibreSpot cannot run a restore. Spotify may already be vanilla.') {
                Write-Log 'Vanilla Spotify restored successfully.' -Level 'SUCCESS'
            }
        }
        'UninstallSpicetify' {
            Update-BackendState -Progress 15 -Status 'Restoring Spotify first' -Step 'Removing active customizations'
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Could not restore Spotify before uninstalling Spicetify.' -MissingMessage 'Spicetify CLI was already missing, so LibreSpot will remove any leftover files and PATH entries directly.') {
                Write-Log 'Spotify restored successfully before removing Spicetify.' -Level 'SUCCESS'
            }
            Update-BackendState -Progress 45 -Status 'Removing Spicetify files' -Step 'Cleaning local tools and config'
            $null = Remove-PathSafely -Path $global:SPICETIFY_CONFIG_DIR -Label 'Spicetify config directory'
            $null = Remove-PathSafely -Path $global:SPICETIFY_DIR -Label 'Spicetify CLI directory'
            $null = Remove-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'Process'
            if (Remove-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'User') {
                Write-Log 'Removed Spicetify from the user PATH.'
            }
        }
        'FullReset' {
            Update-BackendState -Progress 10 -Status 'Restoring vanilla Spotify' -Step 'Preparing deep cleanup'
            try {
                Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage 'Could not restore Spotify before the full reset.'
            } catch {
                Write-Log "$($_.Exception.Message) Continuing because Spotify will be removed next." -Level 'WARN'
            }
            Update-BackendState -Progress 30 -Status 'Removing Spicetify tools' -Step 'Cleaning customization layer'
            $null = Remove-PathSafely -Path $global:SPICETIFY_CONFIG_DIR -Label 'Spicetify config directory'
            $null = Remove-PathSafely -Path $global:SPICETIFY_DIR -Label 'Spicetify CLI directory'
            Update-BackendState -Progress 50 -Status 'Removing Spotify itself' -Step 'Running full cleanup'
            Module-NukeSpotify
            $null = Remove-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'Process'
            $null = Remove-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'User'
        }
        'EnableAutoReapply' {
            Update-BackendState -Progress 25 -Status 'Registering watcher' -Step 'Creating scheduled task'
            if (-not (Register-AutoReapplyTask)) {
                throw "LibreSpot could not register the auto-reapply watcher. See $global:WATCHER_LOG_PATH."
            }
            Update-BackendState -Progress 70 -Status 'Saving watcher preference' -Step 'Updating config.json'
            Set-AutoReapplyConfigPreference -Enabled $true
            Write-Log 'Auto-reapply watcher enabled.' -Level 'SUCCESS'
        }
        'DisableAutoReapply' {
            Update-BackendState -Progress 25 -Status 'Removing watcher' -Step 'Deleting scheduled task'
            if (Unregister-AutoReapplyTask) {
                Write-Log 'Auto-reapply scheduled task removed.'
            } else {
                Write-Log 'Auto-reapply scheduled task was not registered.'
            }
            Update-BackendState -Progress 70 -Status 'Saving watcher preference' -Step 'Updating config.json'
            Set-AutoReapplyConfigPreference -Enabled $false
            Write-Log 'Auto-reapply watcher disabled.' -Level 'SUCCESS'
        }
    }

    Update-BackendState -Progress 100 -Status 'Maintenance complete' -Step 'LibreSpot is ready'
    Write-Log "--- Maintenance action '$Action' completed successfully ---" -Level 'SUCCESS'
}

try {
    Ensure-LogDirectory
    if ($Action -eq 'WatchAutoReapply') {
        $code = Invoke-AutoReapplyWatcher
        exit $code
    }

    Write-EventLine -Kind 'action' -Payload $Action
    if ($Action -notin @('EnableAutoReapply', 'DisableAutoReapply')) {
        Ensure-Admin
    }

    if ($Action -eq 'Install') {
        Invoke-LibreSpotInstall
    } else {
        Invoke-LibreSpotMaintenance
    }
    Write-EventLine -Kind 'result' -Level 'SUCCESS' -Payload 'LibreSpot backend completed successfully.'
    exit 0
} catch {
    $message = $_.Exception.Message
    Write-Log $message -Level 'ERROR'
    Write-EventLine -Kind 'result' -Level 'ERROR' -Payload $message
    exit 1
}
