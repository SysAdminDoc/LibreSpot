param(
    [ValidateSet('Install', 'CheckUpdates', 'Reapply', 'RepairMarketplace', 'OpenMarketplace', 'SafeMode', 'CreateBackup', 'RestoreBackup', 'RestoreVanilla', 'UninstallSpicetify', 'FullReset', 'RemoveSelfData', 'ClearCache', 'EnableAutoReapply', 'DisableAutoReapply', 'WatchAutoReapply', 'Plan')]
    [string]$Action = 'Install',
    [string]$ConfigPath = "$env:APPDATA\LibreSpot\config.json",
    [string]$OperationId = ''
)

$ErrorActionPreference = 'Stop'

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch {}

# The WPF shell decodes this process's stdout as UTF-8; PS 5.1 defaults to the
# OEM codepage, which garbles any non-ASCII character in event payloads.
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$script:BackendTestRoot = $null
$testRootValue = [Environment]::GetEnvironmentVariable('LIBRESPOT_TEST_ROOT')
if (-not [string]::IsNullOrWhiteSpace($testRootValue)) {
    $script:BackendTestRoot = [System.IO.Path]::GetFullPath($testRootValue)
    $env:APPDATA = Join-Path $script:BackendTestRoot 'AppData\Roaming'
    $env:LOCALAPPDATA = Join-Path $script:BackendTestRoot 'AppData\Local'
    $env:ProgramData = Join-Path $script:BackendTestRoot 'ProgramData'
    $env:TEMP = Join-Path $script:BackendTestRoot 'Temp'
    $env:USERPROFILE = Join-Path $script:BackendTestRoot 'UserProfile'
    foreach ($directory in @($env:APPDATA, $env:LOCALAPPDATA, $env:ProgramData, $env:TEMP, $env:USERPROFILE)) {
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
            New-Item -Path $directory -ItemType Directory -Force | Out-Null
        }
    }
}

if (-not ('LibreSpotNativeOutputCollector' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

public sealed class LibreSpotNativeOutputCollector {
    private readonly ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
    private readonly DataReceivedEventHandler handler;

    public LibreSpotNativeOutputCollector() {
        handler = OnDataReceived;
    }

    public void Attach(Process process) {
        process.OutputDataReceived += handler;
        process.ErrorDataReceived += handler;
    }

    public void Detach(Process process) {
        process.OutputDataReceived -= handler;
        process.ErrorDataReceived -= handler;
    }

    public bool TryDequeue(out string line) {
        return lines.TryDequeue(out line);
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs eventArgs) {
        if (eventArgs != null && eventArgs.Data != null) {
            lines.Enqueue(eventArgs.Data);
        }
    }
}
'@
}

# Keep this aligned with LibreSpot.ps1:$global:VERSION and the WPF shell's
# csproj <Version>. The release workflow fails the build if these drift.
$global:VERSION = '3.7.4'
$global:CONFIG_SCHEMA_VERSION = 1
$global:PinnedReleases = @{
    SpotX = @{
        Version = '2.0'
        Commit  = '550bc72cd15f6e2a172a6ecc0873d0991eb1c83c'
        Url     = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/550bc72cd15f6e2a172a6ecc0873d0991eb1c83c/run.ps1'
        SHA256  = '863cd19429160c911ce7439426d9e2127064028ccabbaf3007b233a393607606'
        DefenderMutations = $false
        DefenderOptOut = ''
    }
    SpicetifyCLI = @{
        Version = '2.44.0'
        WindowsMinSpotify = '1.2.14'
        WindowsMaxTestedSpotify = '1.2.93'
        CompatibilityUrl = 'https://github.com/spicetify/cli/releases/tag/v2.44.0'
        SHA256  = @{
            x64   = '215435095420e3804001a650c072f51befde897b414b0dac054edc2ea258ebea'
            arm64 = 'a6f827ae6387203bb87ff4af1f5ab21e4671a542ce1a0e3cb82ddc77d2ac7444'
        }
    }
    Marketplace = @{
        Version = '1.0.9'
        Url     = 'https://github.com/spicetify/marketplace/releases/download/v1.0.9/marketplace.zip'
        SHA256  = '2713054703c2365e391658a58c782dd2ebdd8d573f2015b5a2bab58b7eee8685'
    }
    Themes = @{
        Commit  = 'df033493a7dae30ca6e371de9cec1897871dbb0c'
        SHA256  = 'c837828c71d7a938898f87965b1fe9e5812cec831bd9cb1619bd8feb6020fdc3'
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
$global:SPICETIFY_INTEGRATION_VERSION = 'v2'
$resolvedConfigDirectory = $null
try { $resolvedConfigDirectory = Split-Path -Path $ConfigPath -Parent } catch {}
if ([string]::IsNullOrWhiteSpace($resolvedConfigDirectory)) {
    $resolvedConfigDirectory = "$env:APPDATA\LibreSpot"
}
$global:CONFIG_DIR           = $resolvedConfigDirectory
$global:CONFIG_PATH          = $ConfigPath
$global:LOG_PATH             = Join-Path $global:CONFIG_DIR 'install.log'
$global:OPERATION_JOURNAL_PATH = Join-Path $global:CONFIG_DIR 'operation-journal.jsonl'
$global:RUN_RECEIPT_PATH       = Join-Path $global:CONFIG_DIR 'run-receipt.latest.json'
$global:OPERATION_JOURNAL_MAX_BYTES = 1048576
$global:OPERATION_JOURNAL_RETAIN_BYTES = 786432
$global:CURRENT_OPERATION_ID = $null
$global:CURRENT_OPERATION_ACTION = $null
$global:CACHE_DIR            = Join-Path $global:CONFIG_DIR 'cache'
$global:BACKUP_ROOT          = "$env:USERPROFILE\LibreSpot_Backups"
$global:WATCHER_STATE_PATH   = Join-Path $global:CONFIG_DIR 'watcher-state.json'
$global:WATCHER_LOG_PATH     = Join-Path $global:CONFIG_DIR 'watcher.log'
$global:WATCHER_TASK_NAME    = if ($script:BackendTestRoot) { 'LibreSpot\TestReapplyWatcher' } else { 'LibreSpot\ReapplyWatcher' }

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
    'hidePodcasts.js'       = @{ Url = 'https://raw.githubusercontent.com/theRealPadster/spicetify-hide-podcasts/b89365dd86fba24d610fae65d882d7e14a69f2fa/hidePodcasts.js';                         Source = 'theRealPadster/spicetify-hide-podcasts'; SHA256 = '727e5a2f9137f4be77eac83d234a0ce858c5d618e7ff56116a6def01793fc3f8' }
    'beautiful-lyrics.mjs'  = @{ Url = 'https://raw.githubusercontent.com/surfbryce/beautiful-lyrics/61ac582da092311e893423269ca7f09003108705/Extension/Builds/Release/beautiful-lyrics.mjs';      Source = 'surfbryce/beautiful-lyrics'; SHA256 = '93c9ecfcb0a83c832c5ee7ca8fe826bcfaeec7cdd129c0bf05bab84b8ba6ba72' }
    'playlist-icons.js'     = @{ Url = 'https://raw.githubusercontent.com/jeroentvb/spicetify-playlist-icons/8f401f923a5c25f530935faaceb39089a25b701a/playlist-icons.js';                         Source = 'jeroentvb/spicetify-playlist-icons'; SHA256 = '79bbe2bd6a52a521a382a73ef1c8c7ff0b0b9bd7674c48bb0ed44c5d2c944c8d' }
    'volumePercentage.js'   = @{ Url = 'https://raw.githubusercontent.com/daksh2k/spicetify-stuff/89e609d933946a888cdff9cc3d7c4f1e9b88cfde/Extensions/volumePercentage.js';                       Source = 'daksh2k/spicetify-stuff'; SHA256 = 'b88dcde894f4998abc4473773333015c09f0450ec563d256ed5af45db7129aca' }
    'adblock.js'            = @{ Url = 'https://raw.githubusercontent.com/rxri/spicetify-extensions/60554c512739c6f2084879efe9d8a88f1dd16646/adblock/adblock.js';                                    Source = 'rxri/spicetify-extensions'; SHA256 = 'fb6dc4dfc09ee369638ffaf47a9f36202bb99c1555edc79772d7fbb235114623' }
}
$global:CommunityExtensionAliases = @{
    'beautifulLyrics.js' = 'beautiful-lyrics.mjs'
    'playlistIcons.js' = 'playlist-icons.js'
}
$global:CommunityExtensionNames = @($global:CommunityExtensions.Keys)
$global:DeprecatedCommunityExtensionNames = @('beautifulLyrics.js', 'playlistIcons.js', 'songStats.js')
$global:AllManagedExtensionNames = $global:BuiltInExtensionNames + $global:CommunityExtensionNames + $global:DeprecatedCommunityExtensionNames
$global:CommunityCustomApps = @{
    'stats' = @{
        DisplayName = 'Stats'
        Description = 'Detailed listening statistics with top tracks, artists, genres, library charts, and optional Last.fm-backed views.'
        Url         = 'https://github.com/harbassan/spicetify-apps/releases/download/stats-v1.1.3/spicetify-stats.release.zip'
        Source      = 'harbassan/spicetify-apps'
        Version     = '1.1.3'
        ReleaseTag  = 'stats-v1.1.3'
        AssetPath   = 'stats'
        SHA256      = 'c5611ff8caafe9c673ed43de07fbae77296d42fbd14fab868e9cbeac5d2b6cb7'
    }
}

$global:CommunityThemeRepos = @{
    'Catppuccin' = @{ Owner = 'catppuccin'; Repo = 'spicetify';       CommitSha = '1ec645c4cf7f42f9792b9eeb1bb7930f94593277'; SHA256 = '59432d5dfba871f288331e72ca5eb9ae48783e94d96cc3835a2992b3df71ed65'; ThemeFolder = '.' }
    'Comfy'      = @{ Owner = 'Comfy-Themes'; Repo = 'Spicetify';    CommitSha = '32ff101e27cfd33d85b7cc587f7f95db6b2df8b0'; SHA256 = 'd82afe89be0a58c7c2d83a85a0dfa24b473d48d4f63241178e37c94c1fd1e7c6'; ThemeFolder = '.' }
    'Bloom'      = @{ Owner = 'nimsandu'; Repo = 'spicetify-bloom';   CommitSha = '654cfed682b94613b0029997ffafc1eadccc5bef'; SHA256 = '12cb8678f7226b2a014a10fdef8ea462e0ac0a866f84b2de48050004fcd50a70'; ThemeFolder = '.' }
    'Lucid'      = @{ Owner = 'sanoojes'; Repo = 'Spicetify-Lucid';   CommitSha = '5c28e9f955d5ca84a82d06084cc6652e5655ea2d'; SHA256 = 'af3f1ed718b3deda7c52ebf7e0ca4bf7c07f03f212a88dd0534c2ebe81803bf8'; ThemeFolder = '.' }
    'Hazy'       = @{ Owner = 'Astromations'; Repo = 'Hazy';          CommitSha = '1926d9db3e0313b68ca6e2193c2b278e733ac3c4'; SHA256 = '372938c3fea3cbac7850afeb6b66b15673236e248436a7afaacb2ab1d814c4bf'; ThemeFolder = '.' }
}

$global:ThemesNeedingJS = @('Dribbblish', 'StarryNight', 'Turntable', 'Catppuccin', 'Comfy', 'Bloom', 'Lucid', 'Hazy')

# ThemeData and BuiltInExtensions are the hashtable forms that
# Normalize-LibreSpotConfig uses for .Contains() validation.
# ThemeSchemes is a flat ordered hashtable; ThemeData wraps each entry
# so the normalization code can call $global:ThemeData.Contains($name)
# and $global:ThemeData[$name].Schemes consistently.
$global:ThemeData = [ordered]@{}
foreach ($themeName in $global:ThemeSchemes.Keys) {
    $global:ThemeData[$themeName] = @{ Schemes = @($global:ThemeSchemes[$themeName]) }
}
$global:BuiltInExtensions = [ordered]@{}
foreach ($extName in $global:BuiltInExtensionNames) {
    $global:BuiltInExtensions[$extName] = $extName
}

$global:EasyDefaults = @{
    UiCulture = 'en'
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
    SpotX_Language = ''
    SpotX_CustomPatchesEnabled = $false
    SpotX_CustomPatchesJson = ''
    Spicetify_Theme = '(None - Marketplace Only)'
    Spicetify_Scheme = 'Default'
    Spicetify_Marketplace = $true
    Spicetify_Extensions = @('fullAppDisplay.js', 'shuffle+.js', 'trashbin.js')
    Spicetify_CustomApps = @()
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
    @{ Id = '1.2.93';          Version = '1.2.93' }
    @{ Id = '1.2.92';          Version = '1.2.92' }
    @{ Id = '1.2.90.451';      Version = '1.2.90.451.gb094aab0' }
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
    $eventOperationId = if ([string]::IsNullOrWhiteSpace([string]$global:CURRENT_OPERATION_ID)) { '' } else { [string]$global:CURRENT_OPERATION_ID }
    [Console]::Out.WriteLine("@@LS@@|$eventOperationId|$Kind|$Level|$cleanPayload")
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
    $operationLabel = if ([string]::IsNullOrWhiteSpace([string]$global:CURRENT_OPERATION_ID)) { 'none' } else { [string]$global:CURRENT_OPERATION_ID }
    $timestamped = "[{0}] [{1}] [op:{2}] {3}" -f (Get-Date -Format 'HH:mm:ss'), $Level, $operationLabel, $Message
    try {
        Ensure-LogDirectory
        [System.IO.File]::AppendAllText($global:LOG_PATH, $timestamped + [Environment]::NewLine)
    } catch {}
    Write-EventLine -Kind 'log' -Level $Level -Payload $Message
}

function Optimize-OperationJournalRetention {
    try {
        $maxBytes = [int64]$global:OPERATION_JOURNAL_MAX_BYTES
        $retainBytes = [int64]$global:OPERATION_JOURNAL_RETAIN_BYTES
        if ($maxBytes -le 0 -or $retainBytes -le 0 -or $retainBytes -ge $maxBytes) { return }
        if (-not (Test-Path -LiteralPath $global:OPERATION_JOURNAL_PATH -PathType Leaf)) { return }

        $file = Get-Item -LiteralPath $global:OPERATION_JOURNAL_PATH -ErrorAction Stop
        if ($file.Length -le $maxBytes) { return }

        $bytesToRead = [int][Math]::Min($retainBytes, $file.Length)
        $buffer = New-Object 'System.Byte[]' $bytesToRead
        $stream = [System.IO.File]::Open($global:OPERATION_JOURNAL_PATH, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $null = $stream.Seek(-1 * $bytesToRead, [System.IO.SeekOrigin]::End)
            $read = $stream.Read($buffer, 0, $buffer.Length)
        } finally {
            try { $stream.Dispose() } catch {}
        }

        $tail = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
        $firstNewline = $tail.IndexOf("`n")
        if ($firstNewline -ge 0 -and $firstNewline -lt ($tail.Length - 1)) {
            $tail = $tail.Substring($firstNewline + 1)
        }

        $entry = [ordered]@{
            schemaVersion  = 1
            timestamp      = (Get-Date).ToUniversalTime().ToString('o')
            operationId    = 'journal-retention'
            action         = 'OperationJournal'
            phase          = 'retention'
            target         = $global:OPERATION_JOURNAL_PATH
            safetyDecision = 'Allowed'
            result         = 'Trimmed'
            wouldChange    = $true
            reversible     = $false
            rollbackHint   = 'Older operation journal entries were trimmed to keep local diagnostics bounded.'
            data           = @{
                previousBytes = $file.Length
                retainedBytes = [System.Text.Encoding]::UTF8.GetByteCount($tail)
                maxBytes      = $maxBytes
            }
        }
        $json = $entry | ConvertTo-Json -Compress -Depth 6
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($global:OPERATION_JOURNAL_PATH, $json + [Environment]::NewLine + $tail, $utf8NoBom)
    } catch {
        try { Write-Log "Operation journal retention failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}

function Write-OperationJournalEntry {
    param(
        [string]$OperationId = $global:CURRENT_OPERATION_ID,
        [string]$Action = $global:CURRENT_OPERATION_ACTION,
        [string]$Phase = 'event',
        [string]$Target = '',
        [string]$SafetyDecision = 'NotEvaluated',
        [string]$Result = 'Info',
        [bool]$WouldChange = $false,
        [bool]$Reversible = $false,
        [string]$RollbackHint = '',
        [string]$TokenKind = '',
        [string]$PreviousStateRef = '',
        [string]$NewState = '',
        [string]$UndoAction = '',
        [string]$Risk = '',
        [hashtable]$Data = $null
    )
    try {
        if ([string]::IsNullOrWhiteSpace($OperationId)) { $OperationId = [Guid]::NewGuid().ToString('N') }
        if ([string]::IsNullOrWhiteSpace($Action)) { $Action = 'Unknown' }
        if ([string]::IsNullOrWhiteSpace($TokenKind)) {
            switch ($Phase) {
                'config' { $TokenKind = 'configWrite'; break }
                'path' { $TokenKind = if ($Result -eq 'Removed') { 'pathEntryRemove' } else { 'pathEntryAdd' }; break }
                'task' { $TokenKind = if ($Result -eq 'Removed') { 'watcherTaskRemove' } else { 'watcherTaskRegister' }; break }
                'cache' { $TokenKind = 'cacheCleared'; break }
                'appx' { $TokenKind = 'spotifyUninstall'; break }
                'remove' {
                    $TokenKind = if ($Target -match 'Spicetify') { 'spicetifyUninstall' } elseif ($Target -match 'LibreSpot|Config') { 'selfDataRemoved' } else { 'fullReset' }
                    break
                }
            }
        }
        if ([string]::IsNullOrWhiteSpace($UndoAction)) { $UndoAction = $RollbackHint }
        if ([string]::IsNullOrWhiteSpace($Risk)) {
            $Risk = switch ($TokenKind) {
                'fullReset' { 'destructive' }
                'spotifyUninstall' { 'destructive' }
                'spicetifyUninstall' { 'destructive' }
                'selfDataRemoved' { 'high' }
                'watcherTaskRemove' { 'medium' }
                'spotxPatch' { 'medium' }
                'spicetifyApply' { 'medium' }
                default { 'low' }
            }
        }
        if ([string]::IsNullOrWhiteSpace($NewState)) { $NewState = $Result }
        if ($Reversible -and [string]::IsNullOrWhiteSpace($PreviousStateRef)) {
            $PreviousStateRef = if ([string]::IsNullOrWhiteSpace($Target)) { "operation:$OperationId" } else { "target:$Target" }
        }
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        Optimize-OperationJournalRetention
        $entry = [ordered]@{
            schemaVersion  = 1
            timestamp      = (Get-Date).ToUniversalTime().ToString('o')
            operationId    = $OperationId
            action         = $Action
            phase          = $Phase
            target         = $Target
            safetyDecision = $SafetyDecision
            result         = $Result
            wouldChange    = $WouldChange
            reversible     = $Reversible
            rollbackHint   = $RollbackHint
            tokenKind      = $TokenKind
            previousStateRef = $PreviousStateRef
            newState       = $NewState
            undoAction     = $UndoAction
            risk           = $Risk
        }
        if ($Data) { $entry.data = $Data }
        $json = $entry | ConvertTo-Json -Compress -Depth 6
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::AppendAllText($global:OPERATION_JOURNAL_PATH, $json + [Environment]::NewLine, $utf8NoBom)
    } catch {
        try { Write-Log "Operation journal write failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}

function Start-OperationJournalRun {
    param(
        [string]$Action,
        [string]$Target = '',
        [bool]$WouldChange = $true,
        [bool]$Reversible = $false,
        [string]$RollbackHint = '',
        [string]$OperationId = ''
    )
    if ([string]::IsNullOrWhiteSpace($OperationId)) {
        $global:CURRENT_OPERATION_ID = [Guid]::NewGuid().ToString()
    } else {
        $parsedOperationId = [Guid]::Empty
        if (-not [Guid]::TryParse($OperationId, [ref]$parsedOperationId)) {
            throw "OperationId must be a GUID. Received '$OperationId'."
        }
        $global:CURRENT_OPERATION_ID = $parsedOperationId.ToString()
    }
    $global:CURRENT_OPERATION_ACTION = $Action
    Write-OperationJournalEntry -OperationId $global:CURRENT_OPERATION_ID -Action $Action -Phase 'planned' -Target $Target -SafetyDecision 'Pending' -Result 'Started' -WouldChange $WouldChange -Reversible $Reversible -RollbackHint $RollbackHint
    Write-Log "Operation id: $global:CURRENT_OPERATION_ID"
    return $global:CURRENT_OPERATION_ID
}

function Complete-OperationJournalRun {
    param(
        [string]$Result = 'Succeeded',
        [string]$Message = ''
    )
    Write-OperationJournalEntry -Phase 'complete' -Target $Message -SafetyDecision 'NotEvaluated' -Result $Result -WouldChange $false -Reversible $false
    try {
        if ([string]::IsNullOrWhiteSpace($global:RUN_RECEIPT_PATH) -or [string]::IsNullOrWhiteSpace($global:CURRENT_OPERATION_ID)) { return }
        if (-not (Test-Path -LiteralPath $global:OPERATION_JOURNAL_PATH -PathType Leaf)) { return }

        $entries = @()
        foreach ($line in (Get-Content -LiteralPath $global:OPERATION_JOURNAL_PATH -Tail 500 -ErrorAction SilentlyContinue)) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $entry = $line | ConvertFrom-Json -ErrorAction Stop
                if ($entry.operationId -eq $global:CURRENT_OPERATION_ID) { $entries += $entry }
            } catch {}
        }

        $operationEntries = @($entries |
            Where-Object { $_.tokenKind -and $_.phase -ne 'planned' -and $_.phase -ne 'complete' } |
            ForEach-Object {
                [ordered]@{
                    tokenKind        = [string]$_.tokenKind
                    target           = [string]$_.target
                    previousStateRef = [string]$_.previousStateRef
                    newState         = [string]$_.newState
                    result           = if ($_.result -eq 'Failed') { 'failed' } elseif ($_.result -eq 'Skipped') { 'skipped' } else { 'applied' }
                    reversible       = [bool]$_.reversible
                    undoAction       = [string]$_.undoAction
                    risk             = [string]$_.risk
                }
            })

        $status = switch ($Result) {
            'Succeeded' { 'success' }
            'Canceled' { 'canceled' }
            'Cancelled' { 'canceled' }
            'DryRun' { 'dryRun' }
            'PartialSuccess' { 'partialSuccess' }
            default { 'failed' }
        }

        $firstEntry = @($entries | Select-Object -First 1)
        $startedAt = if ($firstEntry.Count -gt 0 -and $firstEntry[0].timestamp) { [string]$firstEntry[0].timestamp } else { (Get-Date).ToUniversalTime().ToString('o') }
        $undoAvailable = @($operationEntries | Where-Object { $_.reversible -and -not [string]::IsNullOrWhiteSpace($_.previousStateRef) }).Count -gt 0
        $receipt = [ordered]@{
            schemaVersion = 1
            receiptId     = [Guid]::NewGuid().ToString()
            runId         = $global:CURRENT_OPERATION_ID
            operationId   = $global:CURRENT_OPERATION_ID
            startedAt     = $startedAt
            completedAt   = (Get-Date).ToUniversalTime().ToString('o')
            action        = $global:CURRENT_OPERATION_ACTION
            status        = $status
            errorSummary  = if ($status -eq 'failed') { $Message } else { $null }
            undoAvailable = $undoAvailable
            logRef        = $global:LOG_PATH
            operations    = $operationEntries
        }

        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($global:RUN_RECEIPT_PATH, ($receipt | ConvertTo-Json -Depth 6), $utf8NoBom)
    } catch {
        try { Write-Log "Run receipt write failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
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
    param([object]$Value, [bool]$Default = $false)
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

function Get-LibreSpotConfigSchemaVersion {
    param([hashtable]$Config)
    if (-not $Config -or -not $Config.ContainsKey('ConfigSchemaVersion')) { return 0 }
    return (ConvertTo-ConfigInt -Value $Config.ConfigSchemaVersion -Default 0 -Minimum 0 -Maximum ([int]::MaxValue))
}

function Assert-LibreSpotConfigSchemaSupported {
    param([hashtable]$Config)
    $schemaVersion = Get-LibreSpotConfigSchemaVersion -Config $Config
    if ($schemaVersion -gt $global:CONFIG_SCHEMA_VERSION) {
        throw "Saved config schema version $schemaVersion is newer than this LibreSpot build supports ($global:CONFIG_SCHEMA_VERSION)."
    }
    return $schemaVersion
}

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

function Move-ConfigFileToQuarantine {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Reason)
    $reasonSuffix = if ([string]::IsNullOrWhiteSpace($Reason)) { '' } else { " Reason: $Reason" }
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $global:CONFIG_PATH) {
            $stamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
            $quarantinePath = $null
            for ($attempt = 0; $attempt -lt 10; $attempt++) {
                $suffix = if ($attempt -eq 0) { '' } else { "-$attempt" }
                $candidateName = "config.corrupt.$stamp$suffix.json"
                $candidatePath = Join-Path $global:CONFIG_DIR $candidateName
                if (-not (Test-Path -LiteralPath $candidatePath)) {
                    $quarantinePath = $candidatePath
                    break
                }
            }
            if (-not $quarantinePath) {
                $quarantinePath = Join-Path $global:CONFIG_DIR ("config.corrupt.{0}.json" -f [Guid]::NewGuid().ToString('N'))
            }

            if ($PSCmdlet.ShouldProcess($global:CONFIG_PATH, 'Quarantine corrupted config')) {
                # Journal writes are best-effort here: this runs during startup
                # config load, BEFORE Write-OperationJournalEntry is defined.
                # A CommandNotFound must not abort the quarantine move, or the
                # corrupt file stays put and every launch repeats the reset.
                try { Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore the quarantined file manually.' } catch {}
                Move-Item -LiteralPath $global:CONFIG_PATH -Destination $quarantinePath -ErrorAction Stop
                try { Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Quarantined' -WouldChange $true -Reversible $true -RollbackHint 'Restore the quarantined file manually.' } catch {}
                $quarantineName = Split-Path -Path $quarantinePath -Leaf
                $script:ConfigLoadWarning = "LibreSpot reset the saved settings because the config file could not be read safely.$reasonSuffix The previous file was moved to $quarantineName."
            }
        } else {
            $script:ConfigLoadWarning = "LibreSpot reset the saved settings because the config file could not be read safely.$reasonSuffix"
        }
    } catch {
        $script:ConfigLoadWarning = 'LibreSpot reset the saved settings because the config file could not be read safely, but it could not move the original file aside automatically.'
    }
    try {
        if ($Reason) { Write-Log "Config reset: $Reason" -Level 'WARN' }
    } catch {}
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

function Write-WatcherLog {
    param([string]$Message, [string]$Level = 'INFO')
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        $line = "[{0}] [{1}] {2}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::AppendAllText($global:WATCHER_LOG_PATH, $line + [Environment]::NewLine, $utf8NoBom)
        # Trim the watcher log when it exceeds ~1 MB so an unattended machine
        # can't fill the disk with 15-minute polling entries.
        if ((Get-Item -LiteralPath $global:WATCHER_LOG_PATH).Length -gt 1048576) {
            $keep = Get-Content -LiteralPath $global:WATCHER_LOG_PATH -Tail 500
            [System.IO.File]::WriteAllLines($global:WATCHER_LOG_PATH, $keep, $utf8NoBom)
        }
    } catch {}
}

function Write-RemoveSelfDataReceipt {
    param([object[]]$Targets)

    try {
        $receiptDirectory = Join-Path $global:TEMP_DIR 'LibreSpot'
        if (-not (Test-Path -LiteralPath $receiptDirectory -PathType Container)) {
            New-Item -Path $receiptDirectory -ItemType Directory -Force | Out-Null
        }

        $receiptPath = Join-Path $receiptDirectory 'remove-self-data-receipt.latest.json'
        $receipt = [ordered]@{
            schemaVersion  = 1
            action         = 'RemoveSelfData'
            result         = 'Succeeded'
            reversible     = $false
            generatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
            spotifyTouched = $false
            spicetifyTouched = $false
            targets        = $Targets
        }
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($receiptPath, ($receipt | ConvertTo-Json -Depth 8), $utf8)
        Write-EventLine -Kind 'log' -Level 'INFO' -Payload "RemoveSelfData receipt written to $receiptPath"
    } catch {
        Write-EventLine -Kind 'log' -Level 'WARN' -Payload "RemoveSelfData receipt could not be written: $($_.Exception.Message)"
    }
}

function Get-WatcherState {
    if (-not (Test-Path -LiteralPath $global:WATCHER_STATE_PATH)) {
        return @{
            LastKnownVersion = $null
            LastRunAt = $null
            LastOutcome = $null
            LastAppliedSpotifyVersion = $null
            LastAttemptedSpotifyVersion = $null
            LastSuccessfulApplyAt = $null
            LastApplyAt = $null
            LastApplyOutcome = $null
            LastApplyError = $null
        }
    }

    try {
        $raw = Get-Content -LiteralPath $global:WATCHER_STATE_PATH -Raw -ErrorAction Stop | ConvertFrom-Json
        return @{
            LastKnownVersion = [string]$raw.LastKnownVersion
            LastRunAt = [string]$raw.LastRunAt
            LastOutcome = [string]$raw.LastOutcome
            LastAppliedSpotifyVersion = [string]$raw.LastAppliedSpotifyVersion
            LastAttemptedSpotifyVersion = [string]$raw.LastAttemptedSpotifyVersion
            LastSuccessfulApplyAt = [string]$raw.LastSuccessfulApplyAt
            LastApplyAt = [string]$raw.LastApplyAt
            LastApplyOutcome = [string]$raw.LastApplyOutcome
            LastApplyError = [string]$raw.LastApplyError
        }
    } catch {
        return @{
            LastKnownVersion = $null
            LastRunAt = $null
            LastOutcome = $null
            LastAppliedSpotifyVersion = $null
            LastAttemptedSpotifyVersion = $null
            LastSuccessfulApplyAt = $null
            LastApplyAt = $null
            LastApplyOutcome = $null
            LastApplyError = $null
        }
    }
}

function Set-WatcherState {
    param([hashtable]$State)
    $tempPath = $null
    $backupPath = $null
    try {
        Ensure-LogDirectory
        $merged = Get-WatcherState
        foreach ($key in @($State.Keys)) {
            $merged[$key] = $State[$key]
        }
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $json = $merged | ConvertTo-Json -Compress
        $tempPath = Join-Path $global:CONFIG_DIR ("watcher-state.{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
        $backupPath = Join-Path $global:CONFIG_DIR ("watcher-state.{0}.bak" -f [Guid]::NewGuid().ToString('N'))
        [System.IO.File]::WriteAllText($tempPath, $json, $utf8NoBom)
        if (Test-Path -LiteralPath $global:WATCHER_STATE_PATH) {
            try {
                [System.IO.File]::Replace($tempPath, $global:WATCHER_STATE_PATH, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                $rescuePath = "$($global:WATCHER_STATE_PATH).rescue"
                Move-Item -LiteralPath $global:WATCHER_STATE_PATH -Destination $rescuePath -Force -ErrorAction Stop
                try {
                    [System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)
                    Remove-Item -LiteralPath $rescuePath -Force -ErrorAction SilentlyContinue
                } catch {
                    Move-Item -LiteralPath $rescuePath -Destination $global:WATCHER_STATE_PATH -Force -ErrorAction SilentlyContinue
                    throw
                }
            }
        } else {
            [System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)
        }
    } catch {
        if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
        if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
        Write-WatcherLog "State save failed: $($_.Exception.Message)" -Level 'WARN'
    }
}

function Update-ApplyState {
    param(
        [string]$Outcome,
        [bool]$Successful,
        [string]$ErrorMessage = ''
    )

    try {
        $now = Get-Date -Format 'o'
        $currentVersion = Get-InstalledSpotifyVersion
        $state = Get-WatcherState
        $state['LastAttemptedSpotifyVersion'] = $currentVersion
        $state['LastApplyAt'] = $now
        $state['LastApplyOutcome'] = $Outcome
        $state['LastApplyError'] = if ([string]::IsNullOrWhiteSpace($ErrorMessage)) { $null } else { $ErrorMessage }
        if ($Successful) {
            $state['LastAppliedSpotifyVersion'] = $currentVersion
            $state['LastSuccessfulApplyAt'] = $now
            if (-not [string]::IsNullOrWhiteSpace($currentVersion)) {
                $state['LastKnownVersion'] = $currentVersion
            }
        }
        Set-WatcherState -State $state
    } catch {
        Write-WatcherLog "Apply state update failed: $($_.Exception.Message)" -Level 'WARN'
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

function Test-SpotifySessionStability {
    param([int]$WaitSeconds = 20)
    if (-not (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) { return $true }
    try {
        $procs = @(Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)
        if ($procs.Count -eq 0) { return $true }
        $initialPid = $procs[0].Id
        Start-Sleep -Seconds $WaitSeconds
        $afterProcs = @(Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)
        if ($afterProcs.Count -eq 0) {
            Write-Log "Spotify exited within ${WaitSeconds}s of patched launch. This may indicate server-side enforcement. If Spotify keeps closing after patching, use Maintenance > Restore vanilla or Full reset before retrying." -Level 'WARN'
            return $false
        }
        $afterPids = @($afterProcs | ForEach-Object { $_.Id })
        if ($afterPids -notcontains $initialPid) {
            Write-Log "Spotify restarted within ${WaitSeconds}s of patched launch (initial PID $initialPid was replaced). This may indicate server-side enforcement or a self-repair restart. If Spotify keeps restarting after patching, use Maintenance > Restore vanilla or Full reset before retrying." -Level 'WARN'
            return $false
        }
        return $true
    } catch { return $true }
}

function Get-WatcherLaunchCommand {
    $entry = [string]$PSCommandPath
    if ([string]::IsNullOrWhiteSpace($entry)) {
        try { $entry = [string]$MyInvocation.MyCommand.Path } catch {}
    }
    # This process normally runs from an ephemeral execution copy
    # (LibreSpot.Backend.<guid>.run.ps1) that the shell deletes right after
    # the run. The scheduled task must target the canonical sibling that
    # EnsureBackendScriptAsync maintains, or every watcher tick launches a
    # file that no longer exists.
    if (-not [string]::IsNullOrWhiteSpace($entry) -and (Split-Path -Path $entry -Leaf) -ne 'LibreSpot.Backend.ps1') {
        $canonical = Join-Path (Split-Path -Path $entry -Parent) 'LibreSpot.Backend.ps1'
        if (Test-Path -LiteralPath $canonical -PathType Leaf) {
            $entry = $canonical
        }
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
    [CmdletBinding(SupportsShouldProcess)]
    param()
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
        Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Unregister the scheduled task to undo.'
        if ($PSCmdlet.ShouldProcess($global:WATCHER_TASK_NAME, 'Register scheduled task')) {
            $output = & schtasks.exe /Create /TN $global:WATCHER_TASK_NAME /XML $xmlPath /F 2>&1
            $ok = ($LASTEXITCODE -eq 0)
            if ($ok) {
                Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Registered' -WouldChange $true -Reversible $true -RollbackHint 'Unregister the scheduled task to undo.'
                Write-WatcherLog "Register: scheduled task created for $($launch.Entry)"
            } else {
                Write-WatcherLog "Register failed (exit $LASTEXITCODE): $($output -join ' ')" -Level 'ERROR'
            }
            return $ok
        }
        return $false
    } catch {
        Write-WatcherLog "Register exception: $($_.Exception.Message)" -Level 'ERROR'
        return $false
    } finally {
        try { if (Test-Path -LiteralPath $xmlPath) { Remove-Item -LiteralPath $xmlPath -Force -ErrorAction SilentlyContinue } } catch {}
    }
}

function Unregister-AutoReapplyTask {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if ($PSCmdlet.ShouldProcess($global:WATCHER_TASK_NAME, 'Remove scheduled task')) {
        Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Re-register the scheduled task to undo.'
        try {
            $null = & schtasks.exe /Delete /TN $global:WATCHER_TASK_NAME /F 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $true -RollbackHint 'Re-register the scheduled task to undo.'
                Write-WatcherLog "Unregister: scheduled task removed"
                return $true
            }
            return $false
        } catch { return $false }
    }
    return $false
}

function Save-LibreSpotConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([hashtable]$Config)

    Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
    if ($PSCmdlet.ShouldProcess($global:CONFIG_PATH, 'Save configuration')) {
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
                    $rescuePath = "$($global:CONFIG_PATH).rescue"
                    Move-Item -LiteralPath $global:CONFIG_PATH -Destination $rescuePath -Force
                    try {
                        [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
                        Remove-Item -LiteralPath $rescuePath -Force -ErrorAction SilentlyContinue
                    } catch {
                        Move-Item -LiteralPath $rescuePath -Destination $global:CONFIG_PATH -Force -ErrorAction SilentlyContinue
                        throw
                    }
                }
            } else {
                [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
            }
            Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Saved' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
            return $true
        } catch {
            Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
            Write-Log "Config save failed: $($_.Exception.Message)" -Level 'WARN'
            if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
            if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
            return $false
        }
    }
    return $false
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
    $customPatchesPath = ''
    $watcher = Start-SpotifyWindowWatcher
    try {
        Write-WatcherLog 'Downloading pinned SpotX for watcher reapply'
        $spotxHash = $global:PinnedReleases.SpotX.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $destination -Label 'SpotX run.ps1')) {
            try {
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $destination
            } catch {
                if (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $destination -Label 'SpotX run.ps1') {
                    Write-WatcherLog 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $destination -ExpectedHash $spotxHash -Label 'SpotX run.ps1'
            Save-ToAssetCache -SourcePath $destination -SHA256Hash $spotxHash -Label 'SpotX run.ps1' -SourceUrl $global:URL_SPOTX
        }
        $params = Build-SpotXParams -Config $Config
        $customPatchesPath = New-SpotXCustomPatchesFile -Config $Config
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            $params = "$params -CustomPatchesPath `"$customPatchesPath`""
            Write-WatcherLog "Custom SpotX patches staged at $customPatchesPath"
        }
        Write-WatcherLog "Invoking SpotX with: $params"
        Invoke-ExternalScriptIsolated -FilePath $destination -Arguments $params -ExpectedHash $spotxHash -Label 'SpotX run.ps1'
        Reapply-SavedSpicetifySetup -Config $Config
        Write-WatcherLog 'Auto-reapply completed successfully.' -Level 'SUCCESS'
    } finally {
        Stop-SpotifyWindowWatcher -Watcher $watcher
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            Remove-Item -LiteralPath $customPatchesPath -Force -ErrorAction SilentlyContinue
        }
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
        Set-WatcherState -State @{
            LastKnownVersion = $state.LastKnownVersion
            LastRunAt = (Get-Date -Format 'o')
            LastOutcome = 'DeferredSpotifyRunning'
            LastAttemptedSpotifyVersion = $currentVersion
        }
        return 0
    }

    try {
        Invoke-HeadlessReapply -Config $saved
        $now = Get-Date -Format 'o'
        Set-WatcherState -State @{
            LastKnownVersion = $currentVersion
            LastRunAt = $now
            LastOutcome = 'Reapplied'
            LastAppliedSpotifyVersion = $currentVersion
            LastAttemptedSpotifyVersion = $currentVersion
            LastSuccessfulApplyAt = $now
            LastApplyAt = $now
            LastApplyOutcome = 'WatcherReapplied'
            LastApplyError = $null
        }
        return 0
    } catch {
        Write-WatcherLog "Reapply failed: $($_.Exception.Message)" -Level 'ERROR'
        $now = Get-Date -Format 'o'
        $message = [string]$_.Exception.Message
        Set-WatcherState -State @{
            LastKnownVersion = $state.LastKnownVersion
            LastRunAt = $now
            LastOutcome = "Error: $message"
            LastAttemptedSpotifyVersion = $currentVersion
            LastApplyAt = $now
            LastApplyOutcome = 'WatcherFailed'
            LastApplyError = $message
        }
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

function New-SpotXCustomPatchesFile {
    param([hashtable]$Config)

    if (-not $Config -or -not $Config.ContainsKey('SpotX_CustomPatchesEnabled')) { return '' }
    if (-not [bool]$Config.SpotX_CustomPatchesEnabled) { return '' }

    $patchJson = if ($Config.ContainsKey('SpotX_CustomPatchesJson')) { [string]$Config.SpotX_CustomPatchesJson } else { '' }
    if ([string]::IsNullOrWhiteSpace($patchJson)) {
        throw 'Custom SpotX patches are enabled, but SpotX_CustomPatchesJson is empty.'
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $byteCount = $utf8.GetByteCount($patchJson)
    if ($byteCount -gt 65536) {
        throw "Custom SpotX patches are $byteCount bytes; the maximum is 65536 bytes."
    }

    try {
        $null = $patchJson | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "Custom SpotX patches JSON is invalid: $($_.Exception.Message)"
    }

    $patchPath = New-LibreSpotTempFile -Name 'spotx-custom-patches.json'
    $patchDir = Split-Path -Path $patchPath -Parent
    if (-not (Test-Path -LiteralPath $patchDir)) {
        New-Item -ItemType Directory -Path $patchDir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($patchPath, $patchJson, $utf8)
    return $patchPath
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
        $parts = $text -split "\r\n|\n|\r"
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

# CVE-2025-54100: Windows PowerShell 5.1 web-content RCE (CVSS 7.8, fixed in the
# December 2025 Windows cumulative updates). Invoke-WebRequest can execute page
# content at parse time on an unpatched host. SHA256 pinning protects payload
# integrity but not the parse-time vector, so warn (non-blocking) when the host
# looks unpatched. PowerShell 7+ (Core) is unaffected. Pure for unit tests.
function Get-DownloaderCveExposure {
    $result = [ordered]@{
        Exposed = $false
        Status  = 'NotAffected'   # NotAffected | Patched | PossiblyExposed | Unknown
        Reason  = ''
        Edition = [string]$PSVersionTable.PSEdition
        OSBuild = ''
    }
    # Only Windows PowerShell 5.1 (Desktop edition) is in scope for this CVE.
    if ($PSVersionTable.PSEdition -and $PSVersionTable.PSEdition -ne 'Desktop') {
        $result.Reason = 'PowerShell 7+ (Core) is in use; CVE-2025-54100 affects Windows PowerShell 5.1 only.'
        return [pscustomobject]$result
    }

    try {
        $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop
        if ($cv.CurrentBuild) { $result.OSBuild = "$($cv.CurrentBuild).$($cv.UBR)" }
    } catch {}

    # Heuristic: the newest installed update vs the December 2025 patch wave.
    # We never claim certainty -- this only flags a host that is plainly behind.
    $patchWave = [datetime]'2025-12-09'
    $latest = $null
    try {
        $latest = Get-HotFix -ErrorAction Stop |
            Where-Object { $_.InstalledOn } |
            Sort-Object InstalledOn -Descending |
            Select-Object -First 1
    } catch {}

    if ($null -eq $latest -or $null -eq $latest.InstalledOn) {
        $result.Status = 'Unknown'
        $result.Reason = 'Could not read the host update history to confirm the December 2025 PowerShell fix (CVE-2025-54100). Keep Windows fully updated.'
        return [pscustomobject]$result
    }
    if ($latest.InstalledOn -ge $patchWave) {
        $result.Status = 'Patched'
        $result.Reason = "Latest Windows update ($($latest.HotFixID), $($latest.InstalledOn.ToString('yyyy-MM-dd'))) is at or past the December 2025 fix for CVE-2025-54100."
        return [pscustomobject]$result
    }

    $result.Exposed = $true
    $result.Status  = 'PossiblyExposed'
    $result.Reason  = "The newest Windows update on this host is from $($latest.InstalledOn.ToString('yyyy-MM-dd')), before the December 2025 cumulative update that fixes CVE-2025-54100 (a Windows PowerShell 5.1 web-content RCE). LibreSpot still hash-verifies every download, but install pending Windows updates to close the parse-time vector."
    return [pscustomobject]$result
}

function Write-DownloaderCveWarningIfNeeded {
    if ($global:CveDownloaderWarned) { return }
    $global:CveDownloaderWarned = $true
    try {
        $exposure = Get-DownloaderCveExposure
        if ($exposure.Exposed) {
            Write-Log "Security: $($exposure.Reason)" -Level 'WARN'
        }
    } catch {}
}

function Get-DownloadFailureHint {
    param(
        [string]$Uri,
        [object]$ErrorRecord,
        [string]$Stage = 'Download'
    )
    $message = ''
    try { $message = [string]$ErrorRecord.Exception.Message } catch { $message = [string]$ErrorRecord }
    $statusCode = $null
    try {
        if ($ErrorRecord.Exception.Response -and $ErrorRecord.Exception.Response.StatusCode) {
            $statusCode = [int]$ErrorRecord.Exception.Response.StatusCode
        }
    } catch {}
    $target = $Uri
    try { $target = ([uri]$Uri).Host } catch {}
    $lowerMessage = $message.ToLowerInvariant()
    if ($statusCode -eq 407 -or $lowerMessage -match 'proxy.*auth|407|proxy authentication') {
        return "$Stage failed: proxy authentication is required for $target. Configure the system or WinHTTP proxy before retrying."
    }
    if ($statusCode -eq 429 -or (($statusCode -eq 403) -and ($target -match 'github'))) {
        return "$Stage failed: GitHub rate limit or access block for $target. Wait for the rate-limit reset or retry from a network with GitHub access."
    }
    if ($lowerMessage -match 'could not be resolved|name resolution|no such host|\bdns\b') {
        return "$Stage failed: DNS could not resolve $target. Check DNS, VPN, firewall, or content-filtering rules."
    }
    if ($lowerMessage -match 'ssl|tls|certificate|trust relationship') {
        return "$Stage failed: TLS or certificate validation failed for $target. Check system time, enterprise TLS inspection, and root certificates."
    }
    if ($lowerMessage -match 'timed out|timeout') {
        return "$Stage failed: the connection to $target timed out. Check connectivity or retry after the network is stable."
    }
    if ($lowerMessage -match 'sha256 mismatch|hash mismatch|checksum') {
        return "$Stage hash verification failed for $target. The downloaded file does not match the expected SHA256 checksum. Try clearing the asset cache and re-downloading."
    }
    if ([string]::IsNullOrWhiteSpace($message)) {
        return "$Stage failed for $target."
    }
    return "$Stage failed for ${target}: $message"
}

function Get-NetworkDiagnosticCode {
    param(
        [string]$Uri,
        [object]$ErrorRecord
    )
    $message = ''
    try { $message = [string]$ErrorRecord.Exception.Message } catch { $message = [string]$ErrorRecord }
    $statusCode = $null
    try {
        if ($ErrorRecord.Exception.Response -and $ErrorRecord.Exception.Response.StatusCode) {
            $statusCode = [int]$ErrorRecord.Exception.Response.StatusCode
        }
    } catch {}
    $target = $Uri
    try { $target = ([uri]$Uri).Host } catch {}
    $lowerMessage = $message.ToLowerInvariant()

    if ($statusCode -eq 407 -or $lowerMessage -match 'proxy.*auth|407|proxy authentication') { return 'ProxyAuthRequired' }
    if ($statusCode -eq 429 -or (($statusCode -eq 403) -and ($target -match 'github'))) { return 'GitHubRateLimitOrBlock' }
    if ($lowerMessage -match 'could not be resolved|name resolution|no such host|\bdns\b') { return 'DnsFailure' }
    if ($lowerMessage -match 'ssl|tls|certificate|trust relationship') { return 'TlsFailure' }
    if ($lowerMessage -match 'timed out|timeout') { return 'Timeout' }
    if ($lowerMessage -match 'sha256 mismatch|hash mismatch|checksum') { return 'HashMismatch' }
    return 'NetworkFailure'
}

function Get-NetworkPreflightStatus {
    param(
        [string]$Uri = 'https://raw.githubusercontent.com',
        [string]$Purpose = 'download sources',
        [int]$TimeoutMilliseconds = 5000
    )
    $resp = $null
    $target = $Uri
    try { $target = ([uri]$Uri).Host } catch {}
    $result = [ordered]@{
        Ready   = $false
        Code    = 'Unknown'
        Target  = $target
        Message = ''
        Detail  = ''
    }
    try {
        $request = [System.Net.WebRequest]::Create($Uri)
        $request.Timeout = $TimeoutMilliseconds
        $request.Method = 'HEAD'
        try { $request.UserAgent = "LibreSpot/$global:VERSION" } catch {}
        $resp = $request.GetResponse()
        $statusCode = $null
        try { $statusCode = [int]$resp.StatusCode } catch {}
        if ($null -eq $statusCode -or ($statusCode -ge 200 -and $statusCode -lt 400)) {
            $result.Ready = $true
            $result.Code = 'Ready'
            $result.Message = "LibreSpot can reach $target for $Purpose."
            $result.Detail = if ($null -eq $statusCode) { 'HTTP status unavailable' } else { "HTTP $statusCode" }
        } elseif ($statusCode -eq 407) {
            $result.Code = 'ProxyAuthRequired'
            $result.Message = "Network preflight failed: proxy authentication is required for $target. Configure the system or WinHTTP proxy before retrying."
            $result.Detail = "HTTP $statusCode"
        } elseif (($statusCode -eq 403 -or $statusCode -eq 429) -and ($target -match 'github')) {
            $result.Code = 'GitHubRateLimitOrBlock'
            $result.Message = "Network preflight failed: GitHub rate limit or access block for $target. Wait for the rate-limit reset or retry from a network with GitHub access."
            $result.Detail = "HTTP $statusCode"
        } else {
            $result.Code = "Http$statusCode"
            $result.Message = "Network preflight failed: $target returned HTTP $statusCode while checking $Purpose."
            $result.Detail = "HTTP $statusCode"
        }
    } catch {
        $result.Code = Get-NetworkDiagnosticCode -Uri $Uri -ErrorRecord $_
        $result.Message = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'Network preflight'
        try { $result.Detail = [string]$_.Exception.Message } catch {}
    }
    finally { if ($resp) { try { $resp.Close() } catch {} } }
    return [pscustomobject]$result
}

function Download-FileSafe { param([string]$Uri,[string]$OutFile)
    Write-DownloaderCveWarningIfNeeded
    Write-Log "Downloading: $Uri"
    $headers = @{'User-Agent'="LibreSpot/$global:VERSION"}
    try {
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        } catch {}
        $outDir = Split-Path -Path $OutFile -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $OutFile) {
            Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
        }
        try {
            Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing -Headers $headers -TimeoutSec 120 -ErrorAction Stop
        }
        catch {
            $webHint = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'Web request'
            Write-Log "$webHint Trying BITS fallback." -Level 'WARN'
            try {
                Import-Module BitsTransfer -EA SilentlyContinue
                $bitsJob = Start-BitsTransfer -Source $Uri -Destination $OutFile -Asynchronous -EA Stop
                $deadline = (Get-Date).AddSeconds(120)
                while ($bitsJob.JobState -in @('Transferring','Connecting','Queued','TransientError')) {
                    if ((Get-Date) -gt $deadline) { Remove-BitsTransfer $bitsJob -EA SilentlyContinue; throw "BITS transfer timed out (120s)" }
                    Start-Sleep -Milliseconds 500
                }
                if ($bitsJob.JobState -ne 'Transferred') {
                    $js=$bitsJob.JobState
                    $bitsDetail = "BITS state: $js"
                    try { if ($bitsJob.ErrorDescription) { $bitsDetail = "$bitsDetail - $($bitsJob.ErrorDescription)" } } catch {}
                    Remove-BitsTransfer $bitsJob -EA SilentlyContinue
                    throw $bitsDetail
                }
                Complete-BitsTransfer $bitsJob
            } catch {
                $bitsHint = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'BITS'
                throw "Download failed after WebRequest and BITS fallback. $webHint $bitsHint"
            }
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

function Assert-LibreSpotExternalScriptDefenderPolicy {
    param(
        [Parameter(Mandatory)][System.IO.Stream]$Stream,
        [string]$Arguments = '',
        [string]$Label = 'script'
    )

    if (-not $Stream.CanSeek) {
        throw "$Label cannot be inspected for Microsoft Defender mutations. Refusing to run."
    }

    $Stream.Position = 0
    $reader = New-Object System.IO.StreamReader($Stream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
    try {
        $content = $reader.ReadToEnd()
    } finally {
        $reader.Dispose()
        $Stream.Position = 0
    }

    $containsDefenderMutation = $content -match '(?i)\b(?:Add|Set)-MpPreference\b|-(?:ExclusionPath|ExclusionProcess)\b'
    if (-not $containsDefenderMutation) { return }

    $isSpotX = $Label -like 'SpotX*'
    $declaresOptOut = $content -match '(?i)\bdefender_exclusions_off\b'
    $passesOptOut = $Arguments -match '(?i)(?:^|\s)-defender_exclusions_off(?:\s|$)'
    if (-not $isSpotX -or -not $declaresOptOut -or -not $passesOptOut) {
        throw "$Label contains Microsoft Defender preference or exclusion commands without a proven, passed -defender_exclusions_off adapter. Refusing to run."
    }
}

function Open-VerifiedScriptForExecution {
    param(
        [string]$FilePath,
        [string]$ExpectedHash = '',
        [string]$Label = 'script',
        [string]$Arguments = ''
    )

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        throw "No script path was provided for $Label."
    }

    $fullPath = [System.IO.Path]::GetFullPath($FilePath)
    $stream = $null
    try {
        $stream = [System.IO.File]::Open($fullPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHash)) {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $actualHash = -join ($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') })
            } finally {
                if ($sha) { $sha.Dispose() }
            }

            if ($actualHash -ne $ExpectedHash.ToLowerInvariant()) {
                throw "$Label hash mismatch immediately before execution. Expected $ExpectedHash, got $actualHash. Refusing to run."
            }

            if ($stream.CanSeek) {
                $stream.Position = 0
            }
        }

        Assert-LibreSpotExternalScriptDefenderPolicy -Stream $stream -Arguments $Arguments -Label $Label

        return $stream
    } catch {
        if ($stream) { $stream.Dispose() }
        throw
    }
}

function Get-FileSha256Lower {
    param([Parameter(Mandatory)][string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return (($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) -join '')
    } finally {
        $stream.Dispose()
        $sha.Dispose()
    }
}

function Confirm-FileHash { param([string]$Path, [string]$ExpectedHash, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($ExpectedHash)) {
        Write-Log "  Hash verification skipped for $Label (no hash pinned)" -Level 'WARN'
        return
    }
    $actual = Get-FileSha256Lower -Path $Path
    $expected = $ExpectedHash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "SHA256 hash mismatch for ${Label}`n  Expected: $expected`n  Actual:   $actual`n  File may be corrupted or tampered with. Update pinned hash if this is a legitimate new version."
    }
    Write-Log "  SHA256 verified: $Label"
}

function Update-AssetCacheIndexEntry {
    param(
        [string]$SHA256Hash,
        [string]$Label = '',
        [string]$SourceUrl = '',
        [object]$ByteSize = $null,
        [string]$Status = 'present',
        [switch]$MarkUsed,
        [switch]$MarkVerified,
        [string]$QuarantinedPath = ''
    )

    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return }

    try {
        if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
            New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
        }

        $indexPath = Join-Path $global:CACHE_DIR 'asset-cache-index.json'
        $now = (Get-Date).ToUniversalTime().ToString('o')
        $entries = @()
        if (Test-Path -LiteralPath $indexPath -PathType Leaf) {
            try {
                $existingDoc = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
                if ($existingDoc.entries) {
                    $entries = @($existingDoc.entries)
                }
            } catch {
                $entries = @()
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
        [System.IO.File]::WriteAllText($indexPath, ($doc | ConvertTo-Json -Depth 8), $utf8)
    } catch {
        try { Write-Log "  Asset cache index update failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}

function Save-ToAssetCache { param([string]$SourcePath, [string]$SHA256Hash, [string]$Label = '', [string]$SourceUrl = '')
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return }
    try {
        if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
            New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
        }
        $cachePath = Join-Path $global:CACHE_DIR $hash
        Copy-Item -LiteralPath $SourcePath -Destination $cachePath -Force
        $byteSize = (Get-Item -LiteralPath $cachePath).Length
        Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -SourceUrl $SourceUrl -ByteSize $byteSize -Status 'present' -MarkVerified -MarkUsed
        Write-Log "  Cached verified asset (SHA256: $hash)"
    } catch {
        Write-Log "  Asset cache save failed: $($_.Exception.Message)" -Level 'WARN'
    }
}

function Get-FromAssetCache { param([string]$SHA256Hash, [string]$DestinationPath, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return $false }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return $false }
    $cachePath = Join-Path $global:CACHE_DIR $hash
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        Write-Log "  Cache miss for $Label (SHA256: $hash)"
        return $false
    }
    try {
        $actual = Get-FileSha256Lower -Path $cachePath
        if ($actual -ne $hash) {
            Write-Log "  Cached asset for $Label failed re-verification (expected $hash, got $actual). Quarantining stale entry." -Level 'WARN'
            $byteSize = (Get-Item -LiteralPath $cachePath).Length
            $corruptDirectory = Join-Path $global:CACHE_DIR 'corrupt'
            if (-not (Test-Path -LiteralPath $corruptDirectory -PathType Container)) {
                New-Item -Path $corruptDirectory -ItemType Directory -Force | Out-Null
            }
            $quarantinePath = Join-Path $corruptDirectory ("$hash-" + (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss') + '.bad')
            Move-Item -LiteralPath $cachePath -Destination $quarantinePath -Force -ErrorAction SilentlyContinue
            Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -ByteSize $byteSize -Status 'corrupt' -MarkVerified -QuarantinedPath $quarantinePath
            Write-OperationJournalEntry -Phase 'cache' -Target $cachePath -SafetyDecision 'Allowed' -Result 'Quarantined' -WouldChange $true -Reversible $false -RollbackHint 'The corrupt cached asset was moved aside and will be downloaded again on demand.' -Data @{
                label = $Label
                expectedSha256 = $hash
                observedSha256 = $actual
                quarantinePath = $quarantinePath
            }
            return $false
        }
        $outDir = Split-Path -Path $DestinationPath -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -LiteralPath $cachePath -Destination $DestinationPath -Force
        $byteSize = (Get-Item -LiteralPath $cachePath).Length
        Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -ByteSize $byteSize -Status 'present' -MarkVerified -MarkUsed
        Write-Log "  Using verified cached copy for $Label (SHA256: $hash)"
        return $true
    } catch {
        Write-Log "  Cache retrieval failed for ${Label}: $($_.Exception.Message)" -Level 'WARN'
        return $false
    }
}

function Clear-LibreSpotCache {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
        Write-Log 'Asset cache directory does not exist. Nothing to clear.'
        return
    }
    if ($PSCmdlet.ShouldProcess($global:CACHE_DIR, 'Clear asset cache')) {
        $cacheFiles = @(Get-ChildItem -LiteralPath $global:CACHE_DIR -File -Recurse -ErrorAction SilentlyContinue)
        $byteMeasure = $cacheFiles | Measure-Object -Property Length -Sum
        $totalBytes = if ($null -eq $byteMeasure.Sum) { [int64]0 } else { [int64]$byteMeasure.Sum }
        Write-OperationJournalEntry -Phase 'cache' -Target $global:CACHE_DIR -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Cache will be rebuilt automatically on next download.' -Data @{
            fileCount = $cacheFiles.Count
            totalBytes = $totalBytes
        }
        try {
            Remove-Item -LiteralPath $global:CACHE_DIR -Recurse -Force -ErrorAction Stop
            Write-OperationJournalEntry -Phase 'cache' -Target $global:CACHE_DIR -SafetyDecision 'Allowed' -Result 'Cleared' -WouldChange $true -Reversible $false -RollbackHint 'Cache will be rebuilt automatically on next download.' -Data @{
                fileCount = $cacheFiles.Count
                totalBytes = $totalBytes
            }
            Write-Log "Asset cache cleared ($($cacheFiles.Count) file(s), $totalBytes bytes)."
        } catch {
            Write-Log "Failed to clear asset cache: $($_.Exception.Message)" -Level 'WARN'
        }
    }
}

function Expand-ArchiveSafely { param([string]$ZipPath,[string]$DestinationPath,[string]$Label='archive',[int]$MaxEntries=10000,[long]$MaxExpandedBytes=500MB)
    # ZipFile/ZipFileExtensions live in System.IO.Compression.FileSystem on .NET
    # Framework (PS 5.1); loading only System.IO.Compression leaves them
    # unresolvable in a clean powershell.exe process.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $zip = $null
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
        if ($zip.Entries.Count -gt $MaxEntries) {
            throw "Archive '$Label' contains $($zip.Entries.Count) entries (limit $MaxEntries)."
        }
        $fullDest = [System.IO.Path]::GetFullPath($DestinationPath).TrimEnd('\') + '\'
        $totalDeclaredBytes = 0L
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $normalized = $name.Replace('/', '\')
            if ([System.IO.Path]::IsPathRooted($normalized)) {
                throw "Archive '$Label' contains an absolute path entry: $name"
            }
            # Reject only genuine '..' path segments, not legitimate names that
            # merely begin or end with two dots (e.g. '..gitkeep', 'changelog..').
            # The resolved-prefix check below is the authoritative escape guard.
            if ($normalized -split '\\' | Where-Object { $_ -eq '..' }) {
                throw "Archive '$Label' contains a path traversal entry: $name"
            }
            $fullTarget = [System.IO.Path]::GetFullPath((Join-Path $DestinationPath $normalized))
            if (-not $fullTarget.StartsWith($fullDest, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Archive '$Label' entry escapes destination: $name"
            }
            $totalDeclaredBytes += $entry.Length
            if ($totalDeclaredBytes -gt $MaxExpandedBytes) {
                throw "Archive '$Label' declared expanded size exceeds limit ($([math]::Round($MaxExpandedBytes / 1MB))MB)."
            }
        }
        $totalActualBytes = 0L
        $copyBuffer = New-Object byte[] 81920
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $targetPath = [System.IO.Path]::GetFullPath((Join-Path $DestinationPath ($name.Replace('/', '\'))))
            if ($name.EndsWith('/') -or $name.EndsWith('\')) {
                [System.IO.Directory]::CreateDirectory($targetPath) | Out-Null
                continue
            }
            $parentDir = [System.IO.Path]::GetDirectoryName($targetPath)
            if (-not [string]::IsNullOrWhiteSpace($parentDir)) {
                [System.IO.Directory]::CreateDirectory($parentDir) | Out-Null
            }
            $tempTargetPath = "$targetPath.librespot-extract-$([guid]::NewGuid().ToString('N')).tmp"
            $entryStream = $null
            $targetStream = $null
            $entrySucceeded = $false
            try {
                $entryStream = $entry.Open()
                $targetStream = [System.IO.File]::Open($tempTargetPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
                while (($bytesRead = $entryStream.Read($copyBuffer, 0, $copyBuffer.Length)) -gt 0) {
                    $totalActualBytes += $bytesRead
                    if ($totalActualBytes -gt $MaxExpandedBytes) {
                        throw "Archive '$Label' actual expanded size exceeds limit ($([math]::Round($MaxExpandedBytes / 1MB))MB)."
                    }
                    $targetStream.Write($copyBuffer, 0, $bytesRead)
                }
                $entrySucceeded = $true
            } finally {
                if ($targetStream) { $targetStream.Dispose() }
                if ($entryStream) { $entryStream.Dispose() }
                if (-not $entrySucceeded -and (Test-Path -LiteralPath $tempTargetPath -PathType Leaf)) {
                    Remove-Item -LiteralPath $tempTargetPath -Force -ErrorAction SilentlyContinue
                }
            }
            try {
                if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
                    Remove-Item -LiteralPath $targetPath -Force
                }
                [System.IO.File]::Move($tempTargetPath, $targetPath)
            } catch {
                if (Test-Path -LiteralPath $tempTargetPath -PathType Leaf) {
                    Remove-Item -LiteralPath $tempTargetPath -Force -ErrorAction SilentlyContinue
                }
                throw
            }
        }
    } finally {
        if ($zip) { $zip.Dispose() }
    }
}

# Records the PowerShell security context for support diagnostics. Execution
# policy is a SAFETY feature, not a security boundary (Microsoft docs): running
# with -ExecutionPolicy Bypass does NOT defeat AppLocker or Windows Defender
# Application Control (WDAC), which force ConstrainedLanguage mode. Surfacing the
# language mode + execution-policy scopes lets CLM/WDAC blocks be told apart from
# ordinary script errors. Pure and side-effect free for unit testing.
function Get-PowerShellSecurityContext {
    $ctx = [ordered]@{
        Edition             = [string]$PSVersionTable.PSEdition
        Version             = [string]$PSVersionTable.PSVersion
        LanguageMode        = ''
        ExecutionPolicies   = ''
        ConstrainedLanguage = $false
        AppControlEnforced  = $false
    }
    try { $ctx.LanguageMode = [string]$ExecutionContext.SessionState.LanguageMode } catch {}
    if ($ctx.LanguageMode -eq 'ConstrainedLanguage') {
        $ctx.ConstrainedLanguage = $true
        # CLM is forced by AppLocker, WDAC, or Smart App Control (SAC on Win11).
        $ctx.AppControlEnforced = $true
    }
    try {
        $scopes = Get-ExecutionPolicy -List -ErrorAction Stop |
            ForEach-Object { "$($_.Scope)=$($_.ExecutionPolicy)" }
        $ctx.ExecutionPolicies = ($scopes -join '; ')
    } catch {}
    return [pscustomobject]$ctx
}

function Write-PowerShellSecurityContext {
    if ($global:PsSecurityContextLogged) { return }
    $global:PsSecurityContextLogged = $true
    try {
        $ctx = Get-PowerShellSecurityContext
        Write-Log "PowerShell context: $($ctx.Edition) $($ctx.Version); language mode $($ctx.LanguageMode); execution policy [$($ctx.ExecutionPolicies)]."
        if ($ctx.AppControlEnforced) {
            Write-Log "This host enforces ConstrainedLanguage mode (AppLocker, Windows Defender Application Control, or Smart App Control). LibreSpot's scripts may be blocked. This is a platform-level control, not a LibreSpot error, and -ExecutionPolicy Bypass does not bypass it. On managed devices, ask your administrator to allow LibreSpot/SpotX. On personal devices with Smart App Control (Windows 11), open Settings > Privacy & security > Windows Security > App & browser control > Smart App Control settings to adjust. Alternatively, use the pre-compiled LibreSpot.exe from the Releases page." -Level 'WARN'
        }
    } catch {}
}

function Test-IsLanguageModeOrAppControlError {
    param([string]$Message)
    if ([string]::IsNullOrWhiteSpace($Message)) {
        try { return ([string]$ExecutionContext.SessionState.LanguageMode -eq 'ConstrainedLanguage') } catch { return $false }
    }
    return ($Message -match 'ConstrainedLanguage|language mode|AppLocker|Application Control|\bWDAC\b')
}

function Get-SpotXChildFailureClassification {
    # SpotX can fail inside its OWN downloader after LibreSpot has already
    # hash-verified run.ps1 (SpotX issues #870, #836). Without classification
    # those runs surface as a generic "Process exited with code N". Returns
    # $null when no known signature matches, otherwise a stable category id
    # plus sanitized guidance (never echoes raw child output, which can
    # contain attacker-influenced mirror HTML).
    param([string]$Line)
    if ([string]::IsNullOrWhiteSpace($Line)) { return $null }

    if ($Line -match 'curl exit code 28|ERR_CONNECTION_TIMED_OUT|Operation timed out after') {
        return [pscustomobject]@{
            Category = 'SpotXChildDownloadTimeout'
            Guidance = "SpotX's own downloader timed out while fetching Spotify components. LibreSpot already verified the SpotX script itself, so this is an upstream network or CDN outage - retry in a few minutes, or choose a different download method under Custom Install > Advanced adjustments."
        }
    }

    if ($Line -match 'loadspot\.amd64fox1\.workers\.dev') {
        return [pscustomobject]@{
            Category = 'SpotXWorkerEndpointFailure'
            Guidance = "SpotX's Cloudflare worker download endpoint failed. This is an upstream SpotX outage (see SpotX issues #870/#836), not a problem on this machine - retry later, or choose a different download method under Custom Install > Advanced adjustments."
        }
    }

    if ($Line -match 'suspected phishing|reported for potential phishing|This website has been blocked') {
        return [pscustomobject]@{
            Category = 'SpotXMirrorBlockedPhishing'
            Guidance = 'A SpotX download mirror is currently flagged by Cloudflare as suspected phishing, so the download was blocked upstream. Turn off the mirror option (or retry without it) and run the setup again.'
        }
    }

    return $null
}

function Get-SpotXDownloadRetryPlan {
    # Maps a classified SpotX child-download failure (from
    # Get-SpotXChildFailureClassification) to a single automatic-retry plan.
    # Timeouts and Cloudflare-worker outages retry once through the SpotX
    # mirror; a mirror flagged as phishing retries once WITHOUT the mirror.
    # Returns $null when the failure is not download-retryable, or when the
    # useful mirror toggle was already the state of the failed attempt - this
    # guarantees at most one automatic retry and that the retry changes the
    # download path (a same-path retry would just fail the same way).
    param(
        [string]$Category,
        [bool]$MirrorAlreadyUsed
    )

    switch ($Category) {
        'SpotXChildDownloadTimeout' {
            if ($MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $true
                Reason    = "SpotX's download timed out; retrying once through the SpotX mirror."
            }
        }
        'SpotXWorkerEndpointFailure' {
            if ($MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $true
                Reason    = "SpotX's primary download endpoint failed; retrying once through the SpotX mirror."
            }
        }
        'SpotXMirrorBlockedPhishing' {
            if (-not $MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $false
                Reason    = 'The SpotX mirror was blocked upstream; retrying once without the mirror.'
            }
        }
        default { return $null }
    }
}

function Invoke-ExternalScriptIsolated { param([string]$FilePath,[string]$Arguments,[int]$TimeoutSeconds=600,[string]$ExpectedHash='',[string]$Label='external script')
    Write-Log "Spawning: $FilePath"
    Write-PowerShellSecurityContext
    $stdoutPath = Join-Path $global:TEMP_DIR ("LibreSpot-stdout-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stderrPath = Join-Path $global:TEMP_DIR ("LibreSpot-stderr-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stdoutState = @{ Offset = 0L; Remainder = '' }
    $stderrState = @{ Offset = 0L; Remainder = '' }
    # The spawned powershell.exe can be forced into ConstrainedLanguage by WDAC /
    # AppLocker even when this host is FullLanguage; classify that from stderr.
    $appControlHintShown = $false
    # SpotX child-download outages (timeouts, Cloudflare worker failures,
    # phishing-flagged mirrors) otherwise surface as a bare exit code.
    $childFailure = $null
    $scriptGuard = $null
    $p = $null
    try {
        $scriptGuard = Open-VerifiedScriptForExecution -FilePath $FilePath -ExpectedHash $ExpectedHash -Label $Label -Arguments $Arguments
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHash)) {
            Write-Log "  Execution copy verified and locked for $Label"
        }
        $argString = "-NoProfile -ExecutionPolicy Bypass -File `"$FilePath`" $Arguments"
        $p = Start-Process -FilePath 'powershell.exe' -ArgumentList $argString -NoNewWindow -PassThru -Wait:$false -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -ErrorAction Stop
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while (-not $p.HasExited) {
            if ((Get-Date) -gt $deadline) {
                Write-Log "Process exceeded ${TimeoutSeconds}s timeout - terminating." -Level 'WARN'
                try { $p.Kill() } catch {}
                try { $p.WaitForExit(5000) } catch {}
                throw "External process timed out after ${TimeoutSeconds} seconds. It may have hung or entered an interactive prompt."
            }
            $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
            $stdoutState = @{ Offset = $stdoutRead.Offset; Remainder = $stdoutRead.Remainder }
            foreach ($line in $stdoutRead.Lines) {
                Write-Log $line -Level 'OUT'
                if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
            }

            $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
            $stderrState = @{ Offset = $stderrRead.Offset; Remainder = $stderrRead.Remainder }
            foreach ($line in $stderrRead.Lines) {
                Write-Log "[STDERR] $line" -Level 'WARN'
                if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
                if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                    $appControlHintShown = $true
                    Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker, Windows Defender Application Control, or Smart App Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these controls. On managed devices, ask your administrator. On personal devices with Smart App Control (Windows 11), adjust it in Settings > Privacy & security > Windows Security. Alternatively, use LibreSpot.exe from the Releases page." -Level 'WARN'
                }
            }
            Start-Sleep -Milliseconds 200
        }
        $p.WaitForExit()

        $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
        foreach ($line in $stdoutRead.Lines + @($stdoutRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log $line -Level 'OUT'
            if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
        }
        $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
        foreach ($line in $stderrRead.Lines + @($stderrRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log "[STDERR] $line" -Level 'WARN'
            if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
            if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                $appControlHintShown = $true
                Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker, Windows Defender Application Control, or Smart App Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these controls. On managed devices, ask your administrator. On personal devices with Smart App Control (Windows 11), adjust it in Settings > Privacy & security > Windows Security. Alternatively, use LibreSpot.exe from the Releases page." -Level 'WARN'
            }
        }

        # Capture ExitCode defensively. Windows PowerShell can occasionally lose
        # the Process handle when Start-Process is combined with redirected output.
        $exitCode = $null
        try { $exitCode = $p.ExitCode } catch { $exitCode = $null }

        if ($null -eq $exitCode) {
            # Windows PowerShell can drop the ExitCode when Start-Process is paired
            # with redirected output. Don't blindly assume success: if the child's
            # own output already classified a failure (download outage, phishing
            # mirror, patch abort), surface it instead of masking it.
            if ($childFailure) {
                Write-Log $childFailure.Guidance -Level 'WARN'
                try {
                    Write-OperationJournalEntry -Phase 'external' -Target $FilePath -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint $childFailure.Guidance -Data @{ failureCategory = $childFailure.Category; exitCode = 'unavailable' }
                } catch {}
                throw "Process reported a failure and its exit code was unavailable [$($childFailure.Category)]"
            }
            Write-Log 'External process finished but ExitCode was unavailable and no failure signal was found in its output; treating as success. The caller verifies the result independently.' -Level 'WARN'
        } elseif ($exitCode -ne 0) {
            if ($childFailure) {
                Write-Log $childFailure.Guidance -Level 'WARN'
                try {
                    Write-OperationJournalEntry -Phase 'external' -Target $FilePath -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint $childFailure.Guidance -Data @{ failureCategory = $childFailure.Category; exitCode = $exitCode }
                } catch {}
                throw "Process exited with code $exitCode [$($childFailure.Category)]"
            }
            throw "Process exited with code $exitCode"
        }
    } finally {
        if ($p) { try { $p.Dispose() } catch {} }
        if ($scriptGuard) { try { $scriptGuard.Dispose() } catch {} }
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Compare-LibreSpotVersions {
    # Semver-ish compare that tolerates `-preview.N` / `-rc.N` suffixes and
    # string-compare them as a tie-breaker when the numeric prefixes match.
    # Returns $true iff $Latest is strictly newer than $Current.
    param([string]$Latest, [string]$Current)
    if ([string]::IsNullOrWhiteSpace($Latest)) { return $false }
    if ([string]::IsNullOrWhiteSpace($Current)) { return $true }
    $stripLatest  = ($Latest  -replace '-preview.*','' -replace '-rc.*','')
    $stripCurrent = ($Current -replace '-preview.*','' -replace '-rc.*','')
    try {
        $l = [Version]$stripLatest
        $c = [Version]$stripCurrent
        if ($l -gt $c) { return $true }
        if ($l -lt $c) { return $false }
        # Numeric prefixes equal: the one WITHOUT a pre-release suffix is newer.
        $latestIsStable  = ($Latest  -eq $stripLatest)
        $currentIsStable = ($Current -eq $stripCurrent)
        if ($latestIsStable -and -not $currentIsStable) { return $true }
        if (-not $latestIsStable -and $currentIsStable) { return $false }
        # Both stable or both pre-release with same numeric prefix: extract the
        # trailing number from the suffix (e.g. `-preview.10` -> 10) and compare
        # numerically so `-preview.10` > `-preview.9` instead of the wrong lexical
        # ordering where "1" < "9".
        if ($Latest -eq $Current) { return $false }
        $latestSuffixNum = 0; $currentSuffixNum = 0
        if ($Latest -match '\.(\d+)$') { [int]::TryParse($Matches[1], [ref]$latestSuffixNum) | Out-Null }
        if ($Current -match '\.(\d+)$') { [int]::TryParse($Matches[1], [ref]$currentSuffixNum) | Out-Null }
        if ($latestSuffixNum -ne $currentSuffixNum) { return ($latestSuffixNum -gt $currentSuffixNum) }
        return ([string]::CompareOrdinal($Latest, $Current) -gt 0)
    } catch {
        # Non-parseable versions: lexical compare is better than claiming all
        # non-equal versions are "newer".
        if ($Latest -eq $Current) { return $false }
        return ([string]::CompareOrdinal($Latest, $Current) -gt 0)
    }
}

function Get-LibreSpotCurrentSpotifyTarget {
    $entry = $global:SpotifyVersionManifest | Where-Object { $_.Id -ne 'auto' } | Select-Object -First 1
    if (-not $entry) {
        return [pscustomobject]@{ Id = 'unknown'; Version = '' }
    }
    return [pscustomobject]@{
        Id      = [string]$entry.Id
        Version = [string]$entry.Version
    }
}

function Get-LibreSpotCompatibilityWarnings {
    $warnings = @()
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spicetifyMax = [string]$global:PinnedReleases.SpicetifyCLI.WindowsMaxTestedSpotify
    if (-not [string]::IsNullOrWhiteSpace($spotxTarget.Id) -and
        -not [string]::IsNullOrWhiteSpace($spicetifyMax) -and
        (Compare-LibreSpotVersions -Latest $spotxTarget.Id -Current $spicetifyMax)) {
        $warnings += "SpotX target Spotify $($spotxTarget.Id) is newer than Spicetify CLI v$($global:PinnedReleases.SpicetifyCLI.Version) max-tested Windows/Microsoft Store Spotify $spicetifyMax; Spicetify CSS maps may need validation after patching."
    }
    return $warnings
}

function Write-LibreSpotCompatibilityMatrix {
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spotxLabel = if ([string]::IsNullOrWhiteSpace($spotxTarget.Version)) {
        $spotxTarget.Id
    } else {
        "$($spotxTarget.Id) ($($spotxTarget.Version))"
    }
    $spicetify = $global:PinnedReleases.SpicetifyCLI

    Write-Log '  Compatibility matrix:'
    Write-Log "    SpotX: commit $($global:PinnedReleases.SpotX.Commit.Substring(0,10)) targets Spotify $spotxLabel"
    Write-Log "    Spicetify CLI: v$($spicetify.Version) max-tested Windows/Microsoft Store Spotify $($spicetify.WindowsMinSpotify) -> $($spicetify.WindowsMaxTestedSpotify)"
    Write-Log "    Marketplace: v$($global:PinnedReleases.Marketplace.Version) checked as a custom app package independent of Spotify CSS-map coverage"
    Write-Log "    Themes: commit $($global:PinnedReleases.Themes.Commit.Substring(0,10)) checked as a theme archive independent of Spotify CSS-map coverage"

    $warnings = @(Get-LibreSpotCompatibilityWarnings)
    foreach ($warning in $warnings) {
        Write-Log "    Compatibility warning: $warning" -Level 'WARN'
    }
    return $warnings
}

function Invoke-GitHubApiSafe { param([string]$Uri,[hashtable]$Headers,[int]$TimeoutSec=15,[string]$Label='GitHub API')
    try {
        $response = Invoke-WebRequest -Uri $Uri -Headers $Headers -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
        $remaining = $response.Headers['x-ratelimit-remaining']
        if ($remaining -and [int]$remaining -le 5) {
            $resetEpoch = $response.Headers['x-ratelimit-reset']
            $resetTime = if ($resetEpoch) { ([DateTimeOffset]::FromUnixTimeSeconds([long]$resetEpoch)).LocalDateTime.ToString('HH:mm:ss') } else { 'unknown' }
            Write-Log "GitHub API rate limit nearly exhausted ($remaining remaining, resets at $resetTime). Subsequent checks may fail." -Level 'WARN'
        }
        return ($response.Content | ConvertFrom-Json)
    } catch {
        $statusCode = $null
        if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        if ($statusCode -eq 403 -or $statusCode -eq 429) {
            $resetEpoch = $null
            try { $resetEpoch = $_.Exception.Response.Headers['x-ratelimit-reset'] } catch {}
            $resetMsg = ''
            if ($resetEpoch) {
                $resetTime = ([DateTimeOffset]::FromUnixTimeSeconds([long]$resetEpoch)).LocalDateTime.ToString('HH:mm:ss')
                $resetMsg = " Rate limit resets at $resetTime."
            }
            throw "GitHub API rate limit reached for $Label (HTTP $statusCode).$resetMsg Try again later or use an authenticated request."
        }
        throw (Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage $Label)
    }
}

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

function Hide-SpotifyWindows {
    # In the WPF backend, the Start-SpotifyWindowWatcher runspace already polls
    # every 250ms and hides Spotify/SpotifyInstaller/SpotifySetup windows via
    # its own [LibreSpotWin32]::ShowWindowAsync. This stub satisfies call sites
    # shared with the monolith (which defines its own [Win32] P/Invoke type).
}

function Clear-DirectoryContentsSafely {
    param([string]$Path, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Container)) { return 0 }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-Log "  Refusing to clear unsafe directory target: $Path" -Level 'WARN'
        return 0
    }
    $removedCount = 0
    Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | ForEach-Object {
        $itemLabel = if ($Label) { "${Label}: $($_.Name)" } else { $_.FullName }
        $removedCount += Remove-PathSafely -Path $_.FullName -Label $itemLabel
    }
    return $removedCount
}

function Stop-SpotifyProcesses { param([int]$MaxAttempts=5,[int]$RetryDelay=500)
    for ($a=1; $a -le $MaxAttempts; $a++) {
        $procs = Get-Process -Name "Spotify","SpotifyWebHelper","SpotifyMigrator","SpotifyCrashService" -EA SilentlyContinue
        if (-not $procs) { return }
        Write-Log "Killing Spotify processes (attempt $a/$MaxAttempts)..."
        $procs | ForEach-Object { try { Stop-Process -Id $_.Id -Force -EA Stop } catch {} }
        Start-Sleep -Milliseconds $RetryDelay
    }
    $still = Get-Process -Name "Spotify" -EA SilentlyContinue
    if ($still) { Write-Log "Some Spotify processes survived kill attempts." -Level 'WARN' }
}

function Unlock-SpotifyUpdateFolder {
    $updateDir = Join-Path $env:LOCALAPPDATA "Spotify\Update"
    if (-not (Test-Path $updateDir -PathType Container)) { return }
    try {
        $acl = Get-Acl $updateDir
        $changed = $false
        # Snapshot the Deny rules before mutating: RemoveAccessRule mutates the
        # underlying collection, so iterating $acl.Access directly throws
        # "collection modified" on the second Deny ACE -- the exact multi-ACE
        # case this function exists to clear.
        $denyRules = @($acl.Access | Where-Object { $_.AccessControlType -eq 'Deny' })
        foreach ($rule in $denyRules) {
            $null = $acl.RemoveAccessRule($rule); $changed = $true
        }
        if ($changed) { Set-Acl $updateDir $acl; Write-Log "Unlocked Update folder ACLs." }
    } catch { Write-Log "Could not unlock Update folder: $($_.Exception.Message)" -Level 'WARN' }
}

function Get-DesktopPath {
    try {
        $shell = (Get-ItemProperty "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" -EA Stop).Desktop
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
        [Environment]::GetFolderPath('Personal'),
        [Environment]::GetFolderPath('CommonDesktopDirectory'),
        [Environment]::GetFolderPath('CommonStartMenu')
    )
    $blockedTargets = @($blockedRaw | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.TrimEnd('\') } | Sort-Object -Unique)

    return ($normalized -notin $blockedTargets)
}

function Remove-PathSafely {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Path,[string]$Label)
    $displayLabel = if ($Label) { $Label } else { $Path }
    $journalData = @{ label = $displayLabel }
    if ([string]::IsNullOrWhiteSpace($Path)) { return 0 }
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'SkippedMissingTarget' -Result 'Skipped' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target did not exist.' -Data $journalData
        return 0
    }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'RefusedUnsafeTarget' -Result 'Refused' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target failed LibreSpot safe-removal checks.' -Data $journalData
        Write-Log "  Refusing to remove unsafe target: $Path" -Level 'WARN'
        return 0
    }
    Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
    if ($PSCmdlet.ShouldProcess($Path, 'Remove file or directory')) {
        try {
            # Never use a recursive filesystem or ACL operation here. A nested
            # junction can redirect both Remove-Item -Recurse and icacls /T
            # outside the approved root on Windows PowerShell 5.1. Enumerate
            # ordinary directories ourselves, unlink every reparse point without
            # traversing it, delete files, then remove directories bottom-up.
            $item = Get-Item -LiteralPath $Path -Force -EA Stop
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                $item.Delete()
                Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
                Write-Log "  Removed link (target untouched): $displayLabel"
                return 1
            }

            if ($item -is [System.IO.DirectoryInfo]) {
                $pendingDirectories = [System.Collections.Generic.Stack[System.IO.DirectoryInfo]]::new()
                $visitedDirectories = [System.Collections.Generic.List[System.IO.DirectoryInfo]]::new()
                $pendingDirectories.Push($item)

                while ($pendingDirectories.Count -gt 0) {
                    $directory = $pendingDirectories.Pop()
                    $visitedDirectories.Add($directory)
                    $children = @($directory.EnumerateFileSystemInfos())
                    foreach ($child in $children) {
                        if ($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                            $child.Delete()
                            continue
                        }
                        if ($child -is [System.IO.DirectoryInfo]) {
                            $pendingDirectories.Push($child)
                            continue
                        }

                        try {
                            $child.Attributes = [System.IO.FileAttributes]::Normal
                            $child.Delete()
                        } catch {
                            $null = & icacls.exe "$($child.FullName)" /reset /C /Q 2>$null
                            $child.Refresh()
                            $child.Attributes = [System.IO.FileAttributes]::Normal
                            $child.Delete()
                        }
                    }
                }

                $directoriesDeepestFirst = @($visitedDirectories | Sort-Object { $_.FullName.Length } -Descending)
                foreach ($directory in $directoriesDeepestFirst) {
                    try {
                        $directory.Attributes = [System.IO.FileAttributes]::Directory
                        $directory.Delete($false)
                    } catch {
                        $null = & icacls.exe "$($directory.FullName)" /reset /C /Q 2>$null
                        $directory.Refresh()
                        $directory.Attributes = [System.IO.FileAttributes]::Directory
                        $directory.Delete($false)
                    }
                }
            } else {
                try {
                    $item.Attributes = [System.IO.FileAttributes]::Normal
                    $item.Delete()
                } catch {
                    $null = & icacls.exe "$Path" /reset /C /Q 2>$null
                    $item.Refresh()
                    $item.Attributes = [System.IO.FileAttributes]::Normal
                    $item.Delete()
                }
            }
            Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
            Write-Log "  Removed: $displayLabel"
            return 1
        } catch {
            $journalData['error'] = [string]$_.Exception.Message
            Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'The approved root may be partially removed, but reparse-point targets were not traversed; review the error before retrying.' -Data $journalData
            Write-Log "  Failed to remove: $Path ($($_.Exception.Message))" -Level 'WARN'
            return 0
        }
    }
    return 0
}

function Get-NormalizedPathString {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim())
    try { return ([System.IO.Path]::GetFullPath($expanded)).TrimEnd('\') }
    catch { return $expanded.TrimEnd('\') }
}

function Get-PathEntries {
    param([ValidateSet('User','Process')] [string]$Scope = 'User')
    if ($Scope -eq 'Process') {
        $rawPath = $env:PATH
    } else {
        # Environment.GetEnvironmentVariable expands REG_EXPAND_SZ values.
        # Read the registry value directly so a PATH edit preserves tokens
        # such as %USERPROFILE% and %JAVA_HOME% byte-for-byte.
        $environmentKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $false)
        try {
            $rawPath = if ($null -eq $environmentKey) {
                $null
            } else {
                $environmentKey.GetValue(
                    'Path',
                    $null,
                    [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            }
        } finally {
            if ($null -ne $environmentKey) { $environmentKey.Dispose() }
        }
    }
    if ([string]::IsNullOrWhiteSpace($rawPath)) { return @() }
    return @($rawPath -split ';' | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Set-PathEntries {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [ValidateSet('User','Process')] [string]$Scope = 'User',
        [string[]]$Entries,
        [ValidateSet('pathEntryAdd','pathEntryRemove')] [string]$TokenKind = 'pathEntryAdd',
        [string]$ChangedEntry = ''
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
    if ($PSCmdlet.ShouldProcess("$Scope PATH", 'Update PATH entries')) {
        $operationId = if ([string]::IsNullOrWhiteSpace([string]$global:CURRENT_OPERATION_ID)) { [Guid]::NewGuid().ToString('N') } else { [string]$global:CURRENT_OPERATION_ID }
        $previousStateRef = ''
        $expectedHash = ''
        $undoReady = $false
        $tempStatePath = ''
        if ($Scope -eq 'User' -and $TokenKind -eq 'pathEntryAdd' -and -not [string]::IsNullOrWhiteSpace($ChangedEntry)) {
            try {
                $environmentKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $false)
                try {
                    $previousPathExists = $null -ne $environmentKey -and @($environmentKey.GetValueNames()) -contains 'Path'
                    $previousPath = if (-not $previousPathExists) { '' } else {
                        [string]$environmentKey.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
                    }
                    $previousPathKind = if (-not $previousPathExists) { 'String' } else { [string]$environmentKey.GetValueKind('Path') }
                    if ($previousPathExists -and $previousPathKind -notin @('String', 'ExpandString')) {
                        throw "User PATH has unsupported registry type '$previousPathKind'."
                    }
                } finally {
                    if ($null -ne $environmentKey) { $environmentKey.Dispose() }
                }

                $hashText = {
                    param([string]$Value)
                    $sha = [System.Security.Cryptography.SHA256]::Create()
                    try {
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($(if ($null -eq $Value) { '' } else { $Value }))
                        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
                    } finally { $sha.Dispose() }
                }
                $previousHash = & $hashText $previousPath
                $expectedHash = & $hashText $pathValue
                $undoRoot = Join-Path $global:CONFIG_DIR 'undo-states'
                if (Test-Path -LiteralPath $undoRoot) {
                    $attributes = [System.IO.File]::GetAttributes($undoRoot)
                    if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Undo state directory is a reparse point.' }
                } else {
                    New-Item -Path $undoRoot -ItemType Directory -Force -ErrorAction Stop | Out-Null
                }
                $safeOperationId = $operationId -replace '[^A-Za-z0-9_-]', '_'
                $previousStateRef = Join-Path $undoRoot ("$safeOperationId-path-entry-add-" + [Guid]::NewGuid().ToString('N') + '.json')
                $tempStatePath = "$previousStateRef.tmp"
                $state = [ordered]@{
                    schemaVersion = 2
                    operationId = $operationId
                    tokenKind = 'pathEntryAdd'
                    scope = 'User'
                    target = 'User PATH'
                    entry = $ChangedEntry
                    previousValueExists = $previousPathExists
                    previousValue = $previousPath
                    previousValueKind = $previousPathKind
                    expectedValueExists = $true
                    expectedValue = $pathValue
                    expectedValueKind = 'ExpandString'
                    previousSha256 = $previousHash
                    expectedSha256 = $expectedHash
                    createdAtUtc = (Get-Date).ToUniversalTime().ToString('o')
                }
                $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
                [System.IO.File]::WriteAllText($tempStatePath, ($state | ConvertTo-Json -Depth 4), $utf8NoBom)
                [System.IO.File]::Move($tempStatePath, $previousStateRef)
                $undoReady = $true
                Get-ChildItem -LiteralPath $undoRoot -Filter '*.json' -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.LastWriteTimeUtc -lt (Get-Date).ToUniversalTime().AddDays(-30) } |
                    Remove-Item -Force -ErrorAction SilentlyContinue
            } catch {
                if ($tempStatePath) { Remove-Item -LiteralPath $tempStatePath -Force -ErrorAction SilentlyContinue }
                $previousStateRef = ''
                $expectedHash = ''
                try { Write-Log "PATH undo-state capture failed; the PATH update will continue without executable undo: $($_.Exception.Message)" -Level 'WARN' } catch {}
            }
        }
        $newState = if ([string]::IsNullOrWhiteSpace($expectedHash)) { 'Updated' } else { "sha256:$expectedHash" }
        Write-OperationJournalEntry -OperationId $operationId -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $undoReady -RollbackHint 'Restore the exact previous PATH value after validating its fingerprint.' -TokenKind $TokenKind -PreviousStateRef $previousStateRef -NewState $newState -UndoAction 'Restore the exact previous user PATH snapshot.' -Risk $(if ($TokenKind -eq 'pathEntryAdd') { 'low' } else { 'medium' })
        if ($Scope -eq 'Process') {
            $env:PATH = $pathValue
        } else {
            # SetEnvironmentVariable writes a REG_SZ value and therefore
            # destroys expandable PATH tokens. Keep the user PATH explicitly
            # typed as REG_EXPAND_SZ, then notify already-running shells.
            $environmentKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Environment')
            try {
                if ($null -eq $environmentKey) { throw 'Unable to open the current user environment registry key.' }
                $environmentKey.SetValue('Path', $pathValue, [Microsoft.Win32.RegistryValueKind]::ExpandString)
            } finally {
                if ($null -ne $environmentKey) { $environmentKey.Dispose() }
            }

            if (-not ('LibreSpot.EnvironmentChangeNativeMethods' -as [type])) {
                Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace LibreSpot
{
    public static class EnvironmentChangeNativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint message,
            UIntPtr wParam,
            string lParam,
            uint flags,
            uint timeout,
            out UIntPtr result);
    }
 }
'@
            }

            $broadcastResult = [UIntPtr]::Zero
            $null = [LibreSpot.EnvironmentChangeNativeMethods]::SendMessageTimeout(
                [IntPtr]0xffff,
                0x001A,
                [UIntPtr]::Zero,
                'Environment',
                0x0002,
                5000,
                [ref]$broadcastResult)
        }
        Write-OperationJournalEntry -OperationId $operationId -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Updated' -WouldChange $true -Reversible $undoReady -RollbackHint 'Restore the exact previous PATH value after validating its fingerprint.' -TokenKind $TokenKind -PreviousStateRef $previousStateRef -NewState $newState -UndoAction 'Restore the exact previous user PATH snapshot.' -Risk $(if ($TokenKind -eq 'pathEntryAdd') { 'low' } else { 'medium' })
    }
}

function Add-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User','Process')] [string]$Scope = 'User'
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
    Set-PathEntries -Scope $Scope -Entries (@($entries) + @($Entry)) -TokenKind 'pathEntryAdd' -ChangedEntry $Entry
    return $true
}

function Remove-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User','Process')] [string]$Scope = 'User'
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
        Set-PathEntries -Scope $Scope -Entries $remaining -TokenKind 'pathEntryRemove' -ChangedEntry $Entry
    }
    return $removed
}

function Get-SpicetifyIntegrationContext {
    $version = if ($global:SPICETIFY_INTEGRATION_VERSION) { [string]$global:SPICETIFY_INTEGRATION_VERSION } else { 'v2' }
    if ($version -notin @('v2','v3-preview')) {
        throw "Unsupported Spicetify integration version '$version'."
    }

    $installDir = [string]$global:SPICETIFY_DIR
    $configDir = [string]$global:SPICETIFY_CONFIG_DIR
    return [pscustomobject]@{
        Version                    = $version
        InstallDirectory           = $installDir
        ConfigDirectory            = $configDir
        CliPath                    = Join-Path $installDir 'spicetify.exe'
        ConfigPath                 = Join-Path $configDir 'config-xpui.ini'
        ThemesDirectory            = Join-Path $configDir 'Themes'
        ExtensionsDirectory        = Join-Path $configDir 'Extensions'
        CustomAppsDirectory        = Join-Path $configDir 'CustomApps'
        MarketplaceDirectory       = Join-Path $configDir 'CustomApps\marketplace'
        LegacyMarketplaceDirectory = Join-Path $installDir 'CustomApps\marketplace'
    }
}

function Get-SpicetifyConfigEntries {
    $configPath = (Get-SpicetifyIntegrationContext).ConfigPath
    $entries = @{}
    if (-not (Test-Path -LiteralPath $configPath)) { return $entries }
    try {
        foreach ($line in Get-Content -LiteralPath $configPath -ErrorAction Stop) {
            if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$') {
                $entries[$Matches[1].Trim()] = $Matches[2].Trim()
            }
        }
    } catch {
        if (Get-Command Write-Log -ErrorAction SilentlyContinue) {
            Write-Log "Could not read Spicetify config: $($_.Exception.Message)" -Level 'WARN'
        }
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

function Get-MarketplaceHealth {
    $integration = Get-SpicetifyIntegrationContext
    $configDir = $integration.MarketplaceDirectory
    $legacyDir = $integration.LegacyMarketplaceDirectory
    $activeDir = if (Test-Path -LiteralPath $configDir -PathType Container) { $configDir } elseif (Test-Path -LiteralPath $legacyDir -PathType Container) { $legacyDir } else { $configDir }
    $hasConfigDir = Test-Path -LiteralPath $configDir -PathType Container
    $hasLegacyDir = Test-Path -LiteralPath $legacyDir -PathType Container
    $hasExtension = Test-Path -LiteralPath (Join-Path $activeDir 'extension.js') -PathType Leaf
    $hasManifest = Test-Path -LiteralPath (Join-Path $activeDir 'manifest.json') -PathType Leaf
    $isEnabled = @(Get-SpicetifyConfigListValue -Key 'custom_apps') -contains 'marketplace'
    $hasFiles = $hasExtension -and $hasManifest

    # Marketplace can only install themes/snippets into an ACTIVE theme with CSS
    # injection on (the official installer activates a placeholder theme). With
    # an empty current_theme the CLI forces all injection off, so the store
    # loads but every theme/snippet install is a silent no-op.
    $configEntries = Get-SpicetifyConfigEntries
    $currentTheme = [string]$configEntries['current_theme']
    $injectCss = [string]$configEntries['inject_css']
    $themeContractReady = (-not [string]::IsNullOrWhiteSpace($currentTheme)) -and ($injectCss -eq '1')

    $status = if ($hasConfigDir -and $hasFiles -and $isEnabled -and -not $themeContractReady) {
        'ThemeInactive'
    } elseif ($hasConfigDir -and $hasFiles -and $isEnabled) {
        'Ready'
    } elseif ($hasConfigDir -and $hasFiles -and -not $isEnabled) {
        'Hidden'
    } elseif ($isEnabled -and -not $hasFiles) {
        'FilesMissing'
    } elseif ($hasLegacyDir -and -not $hasConfigDir) {
        'LegacyPath'
    } else {
        'Missing'
    }

    return [pscustomobject]@{
        Status             = $status
        Path               = $activeDir
        HasConfigDir       = $hasConfigDir
        HasLegacyDir       = $hasLegacyDir
        HasFiles           = $hasFiles
        IsEnabled          = $isEnabled
        CurrentTheme       = $currentTheme
        ThemeContractReady = $themeContractReady
        IsReady            = ($status -eq 'Ready')
        NeedsRepair        = ($status -in @('ThemeInactive','Hidden','FilesMissing','LegacyPath','Missing'))
    }
}

function Copy-DirectorySnapshotSafely {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [long]$MaxBytes = 268435456,
        [hashtable]$State
    )

    if (-not $State) {
        $State = @{ FileCount = 0; Bytes = [long]0; SkippedReparsePoints = 0 }
    }
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        return [pscustomobject]$State
    }

    $source = Get-Item -LiteralPath $SourcePath -Force -ErrorAction Stop
    if (($source.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to snapshot reparse-point root: $SourcePath"
    }

    New-Item -Path $DestinationPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
    foreach ($item in @(Get-ChildItem -LiteralPath $SourcePath -Force -ErrorAction Stop)) {
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            $State.SkippedReparsePoints = [int]$State.SkippedReparsePoints + 1
            continue
        }

        $destination = Join-Path $DestinationPath $item.Name
        if ($item.PSIsContainer) {
            $null = Copy-DirectorySnapshotSafely -SourcePath $item.FullName -DestinationPath $destination -MaxBytes $MaxBytes -State $State
            continue
        }

        $nextBytes = [long]$State.Bytes + [long]$item.Length
        if ($nextBytes -gt $MaxBytes) {
            throw "Spicetify state exceeds the $MaxBytes-byte preservation limit. No repair changes were made."
        }
        Copy-Item -LiteralPath $item.FullName -Destination $destination -Force -ErrorAction Stop
        $State.FileCount = [int]$State.FileCount + 1
        $State.Bytes = $nextBytes
    }

    return [pscustomobject]$State
}

function Merge-DirectorySnapshotMissingFiles {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [hashtable]$State
    )

    if (-not $State) {
        $State = @{ RestoredFileCount = 0; SkippedExistingFiles = 0; SkippedReparsePoints = 0 }
    }
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        return [pscustomobject]$State
    }

    $source = Get-Item -LiteralPath $SourcePath -Force -ErrorAction Stop
    if (($source.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to restore from reparse-point root: $SourcePath"
    }

    if (Test-Path -LiteralPath $DestinationPath) {
        $destinationRoot = Get-Item -LiteralPath $DestinationPath -Force -ErrorAction Stop
        if (($destinationRoot.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            $State.SkippedReparsePoints = [int]$State.SkippedReparsePoints + 1
            return [pscustomobject]$State
        }
    } else {
        New-Item -Path $DestinationPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }

    foreach ($item in @(Get-ChildItem -LiteralPath $SourcePath -Force -ErrorAction Stop)) {
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            $State.SkippedReparsePoints = [int]$State.SkippedReparsePoints + 1
            continue
        }

        $destination = Join-Path $DestinationPath $item.Name
        if ($item.PSIsContainer) {
            $null = Merge-DirectorySnapshotMissingFiles -SourcePath $item.FullName -DestinationPath $destination -State $State
            continue
        }

        if (Test-Path -LiteralPath $destination) {
            $State.SkippedExistingFiles = [int]$State.SkippedExistingFiles + 1
            continue
        }
        Copy-Item -LiteralPath $item.FullName -Destination $destination -ErrorAction Stop
        $State.RestoredFileCount = [int]$State.RestoredFileCount + 1
    }

    return [pscustomobject]$State
}

function New-SpicetifyStatePreservationSnapshot {
    param([Parameter(Mandatory)][string]$Action)

    $integration = Get-SpicetifyIntegrationContext
    $operationId = if ($global:CURRENT_OPERATION_ID) { [string]$global:CURRENT_OPERATION_ID } else { [Guid]::NewGuid().ToString('N') }
    $safeAction = ($Action -replace '[^A-Za-z0-9_-]', '_')
    $stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmssfff')
    $snapshotRoot = Join-Path $global:BACKUP_ROOT 'SpicetifyState'
    $snapshotPath = Join-Path $snapshotRoot ("$stamp-$safeAction-" + $operationId.Substring(0, [Math]::Min(8, $operationId.Length)))
    $configBackupPath = Join-Path $snapshotPath 'config-xpui.ini'
    $customAppsBackupPath = Join-Path $snapshotPath 'CustomApps'
    $manifestPath = Join-Path $snapshotPath 'preservation-manifest.json'
    $evidencePath = Join-Path $global:CONFIG_DIR 'spicetify-preservation-latest.json'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    try {
        New-Item -Path $snapshotPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
        $configBackedUp = $false
        if (Test-Path -LiteralPath $integration.ConfigPath -PathType Leaf) {
            Copy-Item -LiteralPath $integration.ConfigPath -Destination $configBackupPath -Force -ErrorAction Stop
            $configBackedUp = $true
        }

        $copyResult = Copy-DirectorySnapshotSafely -SourcePath $integration.CustomAppsDirectory -DestinationPath $customAppsBackupPath
        $health = Get-MarketplaceHealth
        $document = [ordered]@{
            schemaVersion        = 1
            action               = $Action
            operationId          = $operationId
            createdAtUtc         = (Get-Date).ToUniversalTime().ToString('o')
            status               = 'SnapshotCreated'
            snapshotPath         = $snapshotPath
            configPath           = $integration.ConfigPath
            customAppsPath       = $integration.CustomAppsDirectory
            configBackedUp       = $configBackedUp
            fileCount            = [int]$copyResult.FileCount
            bytes                = [long]$copyResult.Bytes
            skippedReparsePoints = [int]$copyResult.SkippedReparsePoints
            enabledCustomApps    = @(Get-SpicetifyConfigListValue -Key 'custom_apps')
            marketplaceStatus    = [string]$health.Status
            marketplaceReady     = [bool]$health.IsReady
        }
        $json = $document | ConvertTo-Json -Depth 6
        [System.IO.File]::WriteAllText($manifestPath, $json, $utf8)
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        [System.IO.File]::WriteAllText($evidencePath, $json, $utf8)

        Write-OperationJournalEntry -Phase 'preservation' -Target $snapshotPath -SafetyDecision 'Allowed' -Result 'Preserved' -WouldChange $true -Reversible $true -RollbackHint 'Restore the retained Spicetify state snapshot manually if refreshed package files must be rolled back.' -Data @{
            action = $Action
            fileCount = [int]$copyResult.FileCount
            bytes = [long]$copyResult.Bytes
            skippedReparsePoints = [int]$copyResult.SkippedReparsePoints
            configBackedUp = $configBackedUp
        }
        Write-Log "Preserved Spicetify config and CustomApps state at $snapshotPath" -Level 'STEP'

        foreach ($oldSnapshot in @(Get-ChildItem -LiteralPath $snapshotRoot -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -Skip 5)) {
            $null = Remove-PathSafely -Path $oldSnapshot.FullName -Label 'expired Spicetify state snapshot'
        }
        return [pscustomobject]$document
    } catch {
        $message = $_.Exception.Message
        try {
            Write-OperationJournalEntry -Phase 'preservation' -Target $snapshotPath -SafetyDecision 'BlockedBeforeMutation' -Result 'Failed' -WouldChange $false -Reversible $false -RollbackHint 'Free space or remove unsafe reparse points, then retry before changing Marketplace or custom apps.' -Data @{ action = $Action; error = $message }
        } catch {}
        try { $null = Remove-PathSafely -Path $snapshotPath -Label 'incomplete Spicetify state snapshot' } catch {}
        throw "LibreSpot could not preserve Spicetify state before $Action. No repair changes were made. $message"
    }
}

function Restore-SpicetifyStatePreservationSnapshot {
    param(
        [Parameter(Mandatory)]$Snapshot,
        [bool]$OperationSucceeded
    )

    $integration = Get-SpicetifyIntegrationContext
    $snapshotPath = [string]$Snapshot.snapshotPath
    $configBackupPath = Join-Path $snapshotPath 'config-xpui.ini'
    $customAppsBackupPath = Join-Path $snapshotPath 'CustomApps'
    $manifestPath = Join-Path $snapshotPath 'preservation-manifest.json'
    $evidencePath = Join-Path $global:CONFIG_DIR 'spicetify-preservation-latest.json'
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    try {
        $configRestored = $false
        if ((Test-Path -LiteralPath $configBackupPath -PathType Leaf) -and -not (Test-Path -LiteralPath $integration.ConfigPath -PathType Leaf)) {
            $configDirectory = Split-Path -Path $integration.ConfigPath -Parent
            New-Item -Path $configDirectory -ItemType Directory -Force -ErrorAction Stop | Out-Null
            Copy-Item -LiteralPath $configBackupPath -Destination $integration.ConfigPath -ErrorAction Stop
            $configRestored = $true
        }

        $mergeResult = Merge-DirectorySnapshotMissingFiles -SourcePath $customAppsBackupPath -DestinationPath $integration.CustomAppsDirectory
        $status = if ($OperationSucceeded) { 'PreservedAfterSuccess' } else { 'RecoveredAfterFailure' }
        $document = [ordered]@{
            schemaVersion         = 1
            action                = [string]$Snapshot.action
            operationId           = [string]$Snapshot.operationId
            createdAtUtc          = [string]$Snapshot.createdAtUtc
            completedAtUtc        = (Get-Date).ToUniversalTime().ToString('o')
            status                = $status
            operationSucceeded    = $OperationSucceeded
            recoverySucceeded     = $true
            snapshotPath          = $snapshotPath
            backupRetained        = $true
            configRestored        = $configRestored
            restoredFileCount     = [int]$mergeResult.RestoredFileCount
            skippedExistingFiles  = [int]$mergeResult.SkippedExistingFiles
            skippedReparsePoints  = [int]$mergeResult.SkippedReparsePoints
            preservationFileCount = [int]$Snapshot.fileCount
            preservationBytes     = [long]$Snapshot.bytes
        }
        $json = $document | ConvertTo-Json -Depth 6
        [System.IO.File]::WriteAllText($manifestPath, $json, $utf8)
        [System.IO.File]::WriteAllText($evidencePath, $json, $utf8)
        Write-OperationJournalEntry -Phase 'preservation' -Target $snapshotPath -SafetyDecision 'Allowed' -Result $status -WouldChange ($configRestored -or $mergeResult.RestoredFileCount -gt 0) -Reversible $true -RollbackHint 'The retained snapshot can be used for manual rollback; refreshed package files were not overwritten.' -Data @{
            action = [string]$Snapshot.action
            operationSucceeded = $OperationSucceeded
            configRestored = $configRestored
            restoredFileCount = [int]$mergeResult.RestoredFileCount
            skippedExistingFiles = [int]$mergeResult.SkippedExistingFiles
            skippedReparsePoints = [int]$mergeResult.SkippedReparsePoints
        }
        Write-Log "Spicetify preservation completed; backup retained at $snapshotPath" -Level 'SUCCESS'
        return [pscustomobject]@{ Succeeded = $true; Message = ''; Evidence = [pscustomobject]$document }
    } catch {
        $message = $_.Exception.Message
        try {
            $failure = [ordered]@{
                schemaVersion = 1
                action = [string]$Snapshot.action
                operationId = [string]$Snapshot.operationId
                completedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
                status = 'RecoveryFailed'
                operationSucceeded = $OperationSucceeded
                recoverySucceeded = $false
                snapshotPath = $snapshotPath
                backupRetained = $true
                error = $message
            }
            [System.IO.File]::WriteAllText($evidencePath, ($failure | ConvertTo-Json -Depth 5), $utf8)
            Write-OperationJournalEntry -Phase 'preservation' -Target $snapshotPath -SafetyDecision 'NeedsReview' -Result 'RecoveryFailed' -WouldChange $false -Reversible $true -RollbackHint 'The snapshot is retained; restore it manually before retrying.' -Data @{ action = [string]$Snapshot.action; error = $message }
        } catch {}
        return [pscustomobject]@{ Succeeded = $false; Message = $message; Evidence = $null }
    }
}

function Invoke-WithSpicetifyStatePreservation {
    param(
        [Parameter(Mandatory)][string]$Action,
        [Parameter(Mandatory)][scriptblock]$Operation
    )

    $snapshot = New-SpicetifyStatePreservationSnapshot -Action $Action
    $operationError = $null
    $result = $null
    try {
        $result = & $Operation
    } catch {
        $operationError = $_
    }

    $recovery = Restore-SpicetifyStatePreservationSnapshot -Snapshot $snapshot -OperationSucceeded ($null -eq $operationError)
    if (-not $recovery.Succeeded) {
        $operationMessage = if ($operationError) { "$($operationError.Exception.Message) " } else { '' }
        throw "${operationMessage}Spicetify state recovery failed, but the backup remains at $($snapshot.snapshotPath). $($recovery.Message)"
    }
    if ($operationError) {
        throw $operationError
    }

    return $result
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

function Write-SpicetifyCliOutputLine {
    param(
        [string]$Line,
        [hashtable]$ProgressState
    )

    $plain = Remove-ConsoleEscapeSequences -Text $Line
    $plain = [regex]::Replace($plain, '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]', '')
    $plain = [regex]::Replace($plain, '\s+', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($plain)) { return $null }

    if ($plain -match 'Patching files\s*\[\s*0*(\d+)\s*/\s*0*(\d+)\s*\]') {
        $done = [int]$matches[1]
        $total = [Math]::Max(1, [int]$matches[2])
        $percent = [int][Math]::Min(100, [Math]::Floor(($done / $total) * 100))
        $bucket = if ($percent -ge 100) { 100 } else { [int]([Math]::Floor($percent / 10) * 10) }

        if ($ProgressState -and (-not $ProgressState.ContainsKey('LastUiPatchPercent') -or [int]$ProgressState['LastUiPatchPercent'] -ne $percent)) {
            Update-SpicetifyCliProgress -Line $plain
            $ProgressState['LastUiPatchPercent'] = $percent
        }

        $shouldLog = (-not $ProgressState) -or (-not $ProgressState.ContainsKey('LastPatchBucket')) -or ([int]$ProgressState['LastPatchBucket'] -ne $bucket)
        if ($shouldLog) {
            if ($ProgressState) { $ProgressState['LastPatchBucket'] = $bucket }
            $message = "Patching files: $done/$total ($percent%)"
            Write-Log "  $message"
            return $message
        }
        return $null
    }

    if ($plain -match '^(?:[-\\|/]\s*)?(Backing up app files|Extracting backup|Fetching remote CSS map|Copying raw assets|Updating theme''s styles|Applying additional modifications|Refreshing extensions|Refreshing custom apps)$') {
        $stage = $matches[1]
        Update-SpicetifyCliProgress -Line $stage
        if ((-not $ProgressState) -or (-not $ProgressState.ContainsKey('LastStage')) -or ([string]$ProgressState['LastStage'] -ne $stage)) {
            if ($ProgressState) { $ProgressState['LastStage'] = $stage }
            Write-Log "  $stage"
            return $stage
        }
        return $null
    }

    Update-SpicetifyCliProgress -Line $plain
    Write-Log "  $plain"
    return $plain
}

function Invoke-SpicetifyCli {
    param(
        [string[]]$Arguments,
        [string]$FailureMessage = 'Spicetify command failed.',
        [int]$TimeoutSeconds = 900,
        [int]$IdleTimeoutSeconds = 90
    )
    $integration = Get-SpicetifyIntegrationContext
    $spicetifyExe = $integration.CliPath
    if (-not (Test-Path -LiteralPath $spicetifyExe)) {
        throw 'Spicetify CLI is not installed.'
    }

    $progressState = @{ LastPatchBucket = -1; LastUiPatchPercent = -1; LastStage = '' }
    $outputLines = [System.Collections.Generic.List[string]]::new()
    $process = $null
    $collector = $null

    # Keep PowerShell from turning redirected native stderr into its own
    # terminating error. The .NET process object avoids PowerShell handle
    # bugs seen with redirected files while a C# collector drains both streams
    # without running PowerShell scriptblocks on process output threads.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $argumentString = ConvertTo-NativeArgumentString -Arguments $Arguments
        $displayArguments = ($Arguments | ForEach-Object { [string]$_ }) -join ' '
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $spicetifyExe
        $startInfo.Arguments = $argumentString
        $startInfo.WorkingDirectory = $integration.InstallDirectory
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        $collector = New-Object LibreSpotNativeOutputCollector
        $collector.Attach($process)

        $null = $process.Start()
        Write-Log "  Spicetify ($($integration.Version)) command: spicetify $displayArguments"
        Write-Log "  Spicetify PID: $($process.Id)"
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        $startedAt = Get-Date
        $lastOutputAt = $startedAt
        $lastHeartbeatAt = $startedAt
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $statusIntervalSeconds = if ($IdleTimeoutSeconds -gt 0) { [Math]::Min([Math]::Max($IdleTimeoutSeconds, 5), 15) } else { 15 }
        $heartbeatSeconds = [Math]::Min($statusIntervalSeconds, 10)

        $drainOutput = {
            $count = 0
            [string]$queuedLine = $null
            while ($collector.TryDequeue([ref]$queuedLine)) {
                if (-not [string]::IsNullOrWhiteSpace($queuedLine)) {
                    $processed = Write-SpicetifyCliOutputLine -Line $queuedLine -ProgressState $progressState
                    if ($processed) { [void]$outputLines.Add($processed) }
                    $count++
                }
                $queuedLine = $null
            }
            return $count
        }

        $getTail = {
            if ($outputLines.Count -le 0) { return '' }
            $start = [Math]::Max(0, $outputLines.Count - 4)
            $slice = for ($i = $start; $i -lt $outputLines.Count; $i++) { $outputLines[$i] }
            return ' Output: ' + ((($slice | ForEach-Object { Remove-ConsoleEscapeSequences -Text $_ }) -replace '\s+', ' ') -join ' | ')
        }

        while (-not $process.WaitForExit(250)) {
            $drained = & $drainOutput
            if ($drained -gt 0) { $lastOutputAt = Get-Date }

            $now = Get-Date
            if ($now -gt $deadline) {
                Write-Log "Spicetify command exceeded ${TimeoutSeconds}s timeout and will be terminated." -Level 'WARN'
                try { $process.Kill(); $process.WaitForExit(5000) } catch {}
                $tail = & $getTail
                throw "$FailureMessage Timed out after $TimeoutSeconds seconds.$tail"
            }

            if ($IdleTimeoutSeconds -gt 0 -and $now -ge $lastOutputAt.AddSeconds($IdleTimeoutSeconds)) {
                $idleSeconds = [int]($now - $lastOutputAt).TotalSeconds
                Write-Log "  Spicetify has not emitted a new line for ${idleSeconds}s; still waiting until the ${TimeoutSeconds}s hard timeout." -Level 'WARN'
                $lastOutputAt = $now
            }

            if ($now -ge $lastHeartbeatAt.AddSeconds($heartbeatSeconds)) {
                $elapsedSeconds = [int]($now - $startedAt).TotalSeconds
                $idleSeconds = [int]($now - $lastOutputAt).TotalSeconds
                Write-Log "  Spicetify still running (${elapsedSeconds}s elapsed, ${idleSeconds}s since last output)."
                Update-SpicetifyCliProgress -Line 'Patching files'
                $lastHeartbeatAt = $now
            }
        }

        Start-Sleep -Milliseconds 200
        $null = & $drainOutput

        $exitCode = $null
        try { $exitCode = $process.ExitCode } catch { $exitCode = $null }
        if ($null -eq $exitCode) {
            Write-Log 'Spicetify process finished but ExitCode was unavailable; treating as success.' -Level 'WARN'
        } elseif ($exitCode -ne 0) {
            $tail = & $getTail
            throw "$FailureMessage Exit code: $exitCode.$tail"
        } else {
            Write-Log "  Spicetify exited with code 0."
        }
    } finally {
        $ErrorActionPreference = $previousPreference
        if ($process) {
            if ($collector) { try { $collector.Detach($process) } catch {} }
            try { $process.CancelOutputRead() } catch {}
            try { $process.CancelErrorRead() } catch {}
            try { $process.Dispose() } catch {}
        }
    }
}

function Test-SpicetifyCliInstalled {
    return (Test-Path -LiteralPath (Get-SpicetifyIntegrationContext).CliPath)
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
            Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedProgress = $ProgressPreference
            $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxPackage -Package $storeApp.PackageFullName -ErrorAction Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            } catch {
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry removal or reinstall from the Microsoft Store.'
                throw
            } finally { $ProgressPreference = $savedProgress }
            Write-Log 'Removed the Microsoft Store Spotify package.'
            $removedCount++
        } else {
            Write-Log 'No Microsoft Store Spotify package was detected.'
            Write-Log 'Continuing with desktop Spotify cleanup.'
        }
    } catch {
        Write-Log "Store package removal failed: $($_.Exception.Message)" -Level 'WARN'
    }

    # Remove the provisioned package so new user profiles don't get Spotify pre-installed
    try {
        $provisioned = Get-AppxProvisionedPackage -Online -EA SilentlyContinue | Where-Object { $_.DisplayName -eq 'SpotifyAB.SpotifyMusic' }
        if ($provisioned) {
            Write-OperationJournalEntry -Phase 'appx' -Target $provisioned.PackageName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedProgress = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxProvisionedPackage -Online -PackageName $provisioned.PackageName -EA Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $provisioned.PackageName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
                Write-Log 'Removed provisioned Spotify package.'
                $removedCount++
            } finally { $ProgressPreference = $savedProgress }
        }
    } catch { Write-Log "Provisioned package removal skipped: $($_.Exception.Message)" -Level 'WARN' }

    Update-BackendState -Progress 30 -Status 'Cleaning files, shortcuts, and leftovers' -Step 'Removing desktop state'
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
        'HKCU:\Software\Microsoft\Internet Explorer\Low Rights\ElevationPolicy\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}',
        'HKCU:\Software\Microsoft\Internet Explorer\Low Rights\DragDrop\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\Spotify.exe'
    )) {
        if (Test-Path $key) {
            Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
            try {
                Remove-Item -Path $key -Recurse -Force -ErrorAction Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
                Write-Log "Removed registry key: $key"
                $removedCount++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
                Write-Log "Failed to remove registry key $key" -Level 'WARN'
            }
        }
    }

    foreach ($rv in @(
        @{ Path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'; Name = 'Spotify' },
        @{ Path = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'; Name = 'Spotify Web Helper' }
    )) {
        if (Get-ItemProperty -Path $rv.Path -Name $rv.Name -EA SilentlyContinue) {
            $regTarget = "$($rv.Path)\$($rv.Name)"
            Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
            try {
                Remove-ItemProperty -Path $rv.Path -Name $rv.Name -Force -EA Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
                Write-Log "Removed startup entry: $($rv.Name)"
                $removedCount++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
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
                Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                try {
                    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -ErrorAction Stop
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                    Write-Log "Removed scheduled task: $($_.TaskName)"
                } catch {
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry scheduled task removal manually.'
                }
            }
    } catch {
        Write-Log 'Scheduled task cleanup was skipped.' -Level 'WARN'
    }

    Update-BackendState -Progress 85 -Status 'Performing final verification sweep' -Step 'Confirming removal'
    $verifyPaths = @(
        (Join-Path $env:APPDATA 'Spotify')
        (Join-Path $env:LOCALAPPDATA 'Spotify')
        (Join-Path $env:APPDATA 'spicetify')
        (Join-Path $env:LOCALAPPDATA 'spicetify')
    )
    $maxRetries = 5
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        $remaining = @($verifyPaths | Where-Object { Test-Path $_ })
        if ($remaining.Count -eq 0) { break }
        if ($attempt -gt 1) { Write-Log "Verification retry $attempt/$maxRetries ($($remaining.Count) path(s) still locked)..." }
        Start-Sleep -Milliseconds 1500
        foreach ($path in $remaining) {
            $removedCount += Remove-PathSafely -Path $path -Label "Cleanup retry: $(Split-Path $path -Leaf)"
        }
    }
    $survivors = @($verifyPaths | Where-Object { Test-Path $_ })
    if ($survivors.Count -gt 0) {
        Write-Log "Could not fully remove $($survivors.Count) path(s) (may need reboot):" -Level 'WARN'
        $survivors | ForEach-Object { Write-Log "  - $_" -Level 'WARN' }
    }

    Write-Log "Cleanup complete. $removedCount item(s) were removed." -Level 'SUCCESS'
}

# SECURITY: see SECURITY.md "External process execution contract". $Config MUST
# be a Normalize-LibreSpotConfig output: the only interpolated values here are
# SpotX_LyricsTheme (allowlist), SpotX_DownloadMethod (allowlist),
# SpotX_Language (allowlist), SpotX_CacheLimit (integer), and a manifest-supplied
# version. Do NOT interpolate any new free-form/user value into this string
# without normalizing it first.
function Build-SpotXParams { param($Config)
    $p = @()
    # Always auto-remove MS Store Spotify without prompt (prevents stdin hang)
    $p += "-confirm_uninstall_ms_spoti"
    # Let SpotX manage Spotify version compatibility (auto-overwrite unsupported versions)
    $p += "-confirm_spoti_recomended_over"
    if ([bool]$global:PinnedReleases.SpotX.DefenderMutations) {
        $defenderOptOut = [string]$global:PinnedReleases.SpotX.DefenderOptOut
        if ($defenderOptOut -cne '-defender_exclusions_off') {
            throw 'The pinned SpotX adapter does not declare the required Microsoft Defender opt-out.'
        }
        $p += $defenderOptOut
    }
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

# Post-patch effectiveness check. A clean SpotX exit code does NOT prove the
# patch landed: Spotify's signature protection on newer builds (>=1.2.70) can
# let SpotX run to completion without actually patching xpui (SpotX issue #760).
# SpotX backs up the original bundle to Apps\xpui.spa.bak *before* it patches,
# so a successfully patched install leaves BOTH the patched xpui.spa AND the
# .bak alongside Spotify.exe. We assert those on-disk markers and return a
# structured verdict so callers can surface "patched & verified" vs "ran but
# unverified" with a recovery hint instead of trusting exit code 0 alone.
# Pure and side-effect free so it can be unit-tested against a synthetic dir.
function Get-SpotXPatchVerification {
    param([string]$SpotifyExePath = $global:SPOTIFY_EXE_PATH)

    $result = [ordered]@{
        Verified = $false
        Status   = 'Missing'   # Missing | Unverified | Verified
        Reason   = ''
        Signals  = @()
    }

    if ([string]::IsNullOrWhiteSpace($SpotifyExePath) -or -not (Test-Path -LiteralPath $SpotifyExePath)) {
        $result.Reason = 'Spotify.exe was not found, so SpotX could not have patched anything.'
        return [pscustomobject]$result
    }

    $spotifyDir = [System.IO.Path]::GetDirectoryName($SpotifyExePath)
    $appsDir    = Join-Path $spotifyDir 'Apps'
    $signals    = New-Object System.Collections.Generic.List[string]

    # SpotX backs up the original app bundle before patching. Current SpotX names
    # that backup Apps\xpui.bak; older SpotX builds used Apps\xpui.spa.bak. Either
    # one proves SpotX rewrote the bundle. (Checking only xpui.spa.bak produced a
    # false "patch could not be verified" warning on every successful install.)
    $hasXpuiBak    = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.bak')
    $hasXpuiSpaBak = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa.bak')
    $hasBackup     = $hasXpuiBak -or $hasXpuiSpaBak

    # The bundle is a packed xpui.spa, or an extracted Apps\xpui directory once
    # Spicetify has applied on top of the SpotX-patched client.
    $hasSpaBundle = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa') -PathType Leaf
    $hasDirBundle = Test-Path -LiteralPath (Join-Path $appsDir 'xpui') -PathType Container
    $hasBundle    = $hasSpaBundle -or $hasDirBundle

    # SpotX also patches the native binaries and leaves durable .bak copies next to
    # Spotify.exe. Spicetify's later apply consumes/renames the xpui backup, but the
    # binary backups persist, so they corroborate a SpotX run after the fact.
    $hasBinBackup = (Test-Path -LiteralPath (Join-Path $spotifyDir 'Spotify.bak')) -or `
                    (Test-Path -LiteralPath (Join-Path $spotifyDir 'chrome_elf.dll.bak'))

    if ($hasXpuiBak)    { $signals.Add('xpui.bak (SpotX backed up the original bundle before patching)') }
    if ($hasXpuiSpaBak) { $signals.Add('xpui.spa.bak (legacy SpotX bundle backup)') }
    if ($hasBinBackup)  { $signals.Add('Spotify.bak/chrome_elf.dll.bak (SpotX patched the native binaries)') }
    if ($hasSpaBundle)  { $signals.Add('xpui.spa (Spotify app bundle present)') }
    elseif ($hasDirBundle) { $signals.Add('Apps\xpui (bundle extracted by Spicetify)') }
    $result.Signals = @($signals)

    if (($hasBackup -or $hasBinBackup) -and $hasBundle) {
        $result.Verified = $true
        $result.Status   = 'Verified'
        $result.Reason   = 'SpotX left a patched app bundle and a backup of the original, so the patch was applied.'
    }
    elseif ($hasBundle) {
        $result.Status = 'Unverified'
        $result.Reason = 'Spotify is present but no SpotX backup (Apps\xpui.bak or a patched-binary backup) was found, so the patch may not have been applied. Signature protection on newer Spotify builds can let SpotX exit cleanly without patching.'
    }
    else {
        $result.Status = 'Unverified'
        $result.Reason = 'The Spotify app bundle (Apps\xpui.spa) is missing, so SpotX patching could not be confirmed.'
    }

    return [pscustomobject]$result
}

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

function Module-InstallSpotX { param($Config,$SyncHash)
    Write-Log "Installing SpotX v$($global:PinnedReleases.SpotX.Version)..." -Level 'STEP'
    $dest = New-LibreSpotTempFile -Name 'spotx_run.ps1'
    $customPatchesPath = ''
    try {
        $spotxHash = $global:PinnedReleases.SpotX.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $dest -Label 'SpotX run.ps1')) {
            try {
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $dest
            } catch {
                if (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $dest -Label 'SpotX run.ps1') {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $dest -ExpectedHash $spotxHash -Label "SpotX run.ps1"
            Save-ToAssetCache -SourcePath $dest -SHA256Hash $spotxHash -Label 'SpotX run.ps1' -SourceUrl $global:URL_SPOTX
        }
        $baseParams = Build-SpotXParams -Config $Config
        $customPatchesPath = New-SpotXCustomPatchesFile -Config $Config
        $patchSuffix = ''
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            $patchSuffix = " -CustomPatchesPath `"$customPatchesPath`""
            Write-Log "Custom SpotX patches staged at $customPatchesPath"
        }
        if (Test-Path $global:SPOTIFY_EXE_PATH) {
            $ver = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            Write-Log "Spotify $ver detected - SpotX will verify version compatibility"
        } else {
            Write-Log "Spotify not installed - SpotX will download recommended version"
        }
        Write-Log "Params: $($baseParams + $patchSuffix)"
        if ($SyncHash) { $SyncHash.AllowSpotify = $true }
        try {
            # SpotX can fail inside its own downloader after LibreSpot already
            # hash-verified run.ps1 (timeout, Cloudflare-worker outage, or a
            # mirror flagged as phishing). Invoke-ExternalScriptIsolated tags
            # those with a [SpotX...] category. On a classified download
            # failure, retry exactly once through the SpotX mirror (or, for a
            # phishing-blocked mirror, without it) before surfacing the error.
            $spotxMirrorInUse = [bool]$Config.SpotX_Mirror
            $spotxAttempt = 0
            while ($true) {
                $spotxAttempt++
                try {
                    Invoke-ExternalScriptIsolated -FilePath $dest -Arguments ($baseParams + $patchSuffix) -ExpectedHash $spotxHash -Label 'SpotX run.ps1'
                    break
                } catch {
                    $spotxCategory = if ($_.Exception.Message -match '\[(SpotX\w+)\]') { $Matches[1] } else { $null }
                    $spotxRetry = if ($spotxCategory -and $spotxAttempt -eq 1) {
                        Get-SpotXDownloadRetryPlan -Category $spotxCategory -MirrorAlreadyUsed $spotxMirrorInUse
                    } else { $null }
                    if (-not $spotxRetry) { throw }
                    Write-Log $spotxRetry.Reason -Level 'WARN'
                    $hasMirror = $baseParams -match '(^|\s)-mirror(\s|$)'
                    if ($spotxRetry.UseMirror -and -not $hasMirror) {
                        $baseParams = ($baseParams.Trim() + ' -mirror').Trim()
                    } elseif ((-not $spotxRetry.UseMirror) -and $hasMirror) {
                        $baseParams = ($baseParams -replace '(^|\s)-mirror(\s|$)', ' ').Trim()
                    }
                    $spotxMirrorInUse = $spotxRetry.UseMirror
                }
            }
            # Verify SpotX patching succeeded
            if (-not (Test-Path $global:SPOTIFY_EXE_PATH)) {
                throw "SpotX failed - Spotify.exe not found at $global:SPOTIFY_EXE_PATH. Check the log above for errors."
            }
            $elfDll = Join-Path (Split-Path $global:SPOTIFY_EXE_PATH) "chrome_elf.dll"
            if (-not (Test-Path $elfDll)) {
                throw "Spotify installation is incomplete - chrome_elf.dll is missing. This usually means the Spotify download failed or was corrupted."
            }
            $patchedVer = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            $verify = Get-SpotXPatchVerification -SpotifyExePath $global:SPOTIFY_EXE_PATH
            if ($verify.Verified) {
                Write-Log "Spotify $patchedVer patched and verified ($($verify.Signals -join '; '))." -Level 'SUCCESS'
            } else {
                Write-Log "Spotify ${patchedVer}: SpotX ran but the patch could not be verified. $($verify.Reason)" -Level 'WARN'
                Write-Log "If ads still play or the UI is blank, this Spotify build may resist SpotX patching (SpotX issue #760). Try Maintenance > Reapply, or Maintenance > Full Reset to start clean. As a fallback, enable 'Ad-block (Spicetify fallback)' in Custom Install to keep ad-blocking working at the Spicetify layer." -Level 'WARN'
            }
            Write-Log "Launching Spotify (hidden) to generate config files..."
            if (Test-Path $global:SPOTIFY_EXE_PATH) {
                # Force-close any Spotify the user (or SpotX) left running so this
                # first launch starts from a clean, freshly patched process, then
                # reopen it to generate the config files.
                Write-Log "Force-closing any running Spotify before the first launch..."
                Stop-SpotifyProcesses -maxAttempts 5
                Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$global:SPOTIFY_EXE_PATH`""
                Start-Sleep -Milliseconds 800
                Hide-SpotifyWindows
            }
            $prefsPath = Join-Path $env:APPDATA "Spotify\prefs"
            $waited = 0; $maxWait = 45
            while ($waited -lt $maxWait) {
                if ((Test-Path $prefsPath) -and ((Get-Item $prefsPath).Length -gt 10)) {
                    Write-Log "Config files detected after ${waited}s."; break
                }
                Hide-SpotifyWindows
                Start-Sleep -Seconds 2; $waited += 2
            }
            if ($waited -ge $maxWait) { Write-Log "Timed out waiting for config (${maxWait}s). Continuing..." -Level 'WARN' }
            Start-Sleep -Seconds 3; Stop-SpotifyProcesses -maxAttempts 3
        } finally {
            if ($SyncHash) { $SyncHash.AllowSpotify = $false }
        }
    } finally {
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            Remove-Item -LiteralPath $customPatchesPath -Force -ErrorAction SilentlyContinue
        }
        Remove-Item -LiteralPath $dest -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallSpicetifyCLI {
    $integration = Get-SpicetifyIntegrationContext
    $ver = $global:PinnedReleases.SpicetifyCLI.Version
    Write-Log "Installing Spicetify CLI v$ver..." -Level 'STEP'
    New-Item -Path $integration.InstallDirectory -ItemType Directory -Force | Out-Null
    $arch = switch ($env:PROCESSOR_ARCHITECTURE) { 'ARM64' {'arm64'} default {'x64'} }
    $zip = $global:URL_SPICETIFY_FMT -f $ver, $arch
    $zp = New-LibreSpotTempFile -Name 'spicetify.zip'
    try {
        $expectedHash = $global:PinnedReleases.SpicetifyCLI.SHA256[$arch]
        if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zp -Label "Spicetify CLI ($arch)")) {
            try {
                Download-FileSafe -Uri $zip -OutFile $zp
            } catch {
                if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zp -Label "Spicetify CLI ($arch)") {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $zp -ExpectedHash $expectedHash -Label "Spicetify CLI ($arch)"
            Save-ToAssetCache -SourcePath $zp -SHA256Hash $expectedHash -Label "Spicetify CLI ($arch)" -SourceUrl $zip
        }
        if (Test-Path -LiteralPath $integration.InstallDirectory) {
            $null = Clear-DirectoryContentsSafely -Path $integration.InstallDirectory -Label 'Spicetify CLI'
        }
        Expand-ArchiveSafely -ZipPath $zp -DestinationPath $integration.InstallDirectory -Label 'Spicetify CLI'
        $sExe = $integration.CliPath
        if (-not (Test-Path $sExe)) { throw "spicetify.exe not found after extraction - ZIP may be corrupted" }
        $null = Add-PathEntry -Entry $integration.InstallDirectory -Scope 'Process'
        if (Add-PathEntry -Entry $integration.InstallDirectory -Scope 'User') {
            Write-Log "Added Spicetify to user PATH."
        }
        Write-Log "Generating config..."
        Invoke-SpicetifyCli -Arguments @('config', '--bypass-admin') -FailureMessage 'Could not generate the initial Spicetify config.'
        Write-Log "Spicetify CLI v$ver installed."
    } finally {
        Remove-Item -LiteralPath $zp -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallThemes { param($Config)
    $tn = $Config.Spicetify_Theme; if ($tn -eq '(None - Marketplace Only)') { Write-Log "No theme selected."; return }
    Write-Log "Installing theme: $tn..." -Level 'STEP'
    $td = (Get-SpicetifyIntegrationContext).ThemesDirectory
    if (-not (Test-Path $td)) { New-Item -Path $td -ItemType Directory -Force | Out-Null }

    $isCommunity = $global:CommunityThemeRepos.ContainsKey($tn)

    if ($isCommunity) {
        # Community theme — download commit-pinned archive and verify hash
        $repo = $global:CommunityThemeRepos[$tn]
        $archiveUrl = "https://github.com/$($repo.Owner)/$($repo.Repo)/archive/$($repo.CommitSha).zip"
        $safeName = ($tn -replace '[^a-zA-Z0-9_-]','_')
        $tz = New-LibreSpotTempFile -Name "community-theme-$safeName.zip"
        $tu = New-LibreSpotTempDirectory -Name "community-theme-$safeName-unpack"
        try {
            Write-Log "Downloading community theme from $($repo.Owner)/$($repo.Repo) @ $($repo.CommitSha.Substring(0,10))..."
            $themeHash = $repo.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themeHash -DestinationPath $tz -Label "Community theme '$tn'")) {
                try {
                    Download-FileSafe -Uri $archiveUrl -OutFile $tz
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themeHash -DestinationPath $tz -Label "Community theme '$tn'") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $tz -ExpectedHash $themeHash -Label "Community theme '$tn'"
                Save-ToAssetCache -SourcePath $tz -SHA256Hash $themeHash -Label "Community theme '$tn'" -SourceUrl $archiveUrl
            }
            Expand-ArchiveSafely -ZipPath $tz -DestinationPath $tu -Label "Community theme '$tn'"
            $root = Get-ChildItem -LiteralPath $tu -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw "Community theme archive for '$tn' did not contain a root folder." }
            $src = if ($repo.ThemeFolder -eq '.') { $root.FullName } else { Join-Path $root.FullName $repo.ThemeFolder }
            if (-not (Test-Path -LiteralPath $src -PathType Container)) {
                throw "Theme folder '$($repo.ThemeFolder)' was not found in the $($repo.Owner)/$($repo.Repo) archive."
            }
            # Verify the archive actually contains Spicetify theme files
            $hasColorIni = Test-Path -LiteralPath (Join-Path $src 'color.ini')
            $hasUserCss  = Test-Path -LiteralPath (Join-Path $src 'user.css')
            if (-not ($hasColorIni -or $hasUserCss)) {
                throw "Community theme '$tn' archive does not contain color.ini or user.css - not a valid Spicetify theme."
            }
            $dst = Join-Path $td $tn
            if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
            # Copy only theme-relevant files, not repo metadata (.git, .github, etc.)
            New-Item -Path $dst -ItemType Directory -Force | Out-Null
            $themeFiles = @('color.ini','user.css','theme.js','theme.script.js','assets','README.md')
            foreach ($tf in $themeFiles) {
                $tfSrc = Join-Path $src $tf
                if (Test-Path -LiteralPath $tfSrc) {
                    Copy-Item $tfSrc -Destination (Join-Path $dst $tf) -Recurse -Force
                }
            }
            Write-Log "Community theme '$tn' copied to $dst"
        } catch {
            Write-Log "Community theme '$tn' failed to install: $($_.Exception.Message). The install will continue without this theme." -Level 'WARN'
            return
        } finally {
            Remove-Item -LiteralPath $tz -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $tu -Recurse -Force -ErrorAction SilentlyContinue
        }
    } else {
        # Official theme — extract from the pinned spicetify-themes archive
        $tz = New-LibreSpotTempFile -Name 'themes.zip'
        $tu = New-LibreSpotTempDirectory -Name 'themes-unpack'
        try {
            $themesHash = $global:PinnedReleases.Themes.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $tz -Label 'Themes archive')) {
                try {
                    Download-FileSafe -Uri $global:URL_THEMES_REPO -OutFile $tz
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $tz -Label 'Themes archive') {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $tz -ExpectedHash $themesHash -Label "Themes archive"
                Save-ToAssetCache -SourcePath $tz -SHA256Hash $themesHash -Label 'Themes archive' -SourceUrl $global:URL_THEMES_REPO
            }
            Expand-ArchiveSafely -ZipPath $tz -DestinationPath $tu -Label 'Themes archive'
            $root = Get-ChildItem -LiteralPath $tu -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw "Theme archive did not contain an unpacked root folder." }
            $src = Join-Path $root.FullName $tn
            if (-not (Test-Path -LiteralPath $src -PathType Container)) {
                throw "Theme '$tn' was not found in the pinned theme archive."
            }
            $dst = Join-Path $td $tn
            if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
            Copy-Item $src -Destination $dst -Recurse -Force
            Write-Log "Theme copied to $dst"
        } finally {
            Remove-Item -LiteralPath $tz -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $tu -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if (-not (Test-Path (Join-Path $td $tn))) { return }
    $sc = $Config.Spicetify_Scheme; Write-Log "Setting theme=$tn, scheme=$sc"
    Invoke-SpicetifyCli -Arguments @('config', 'current_theme', $tn, '--bypass-admin') -FailureMessage "Could not set Spicetify theme '$tn'."
    if (-not [string]::IsNullOrWhiteSpace($sc)) {
        Invoke-SpicetifyCli -Arguments @('config', 'color_scheme', $sc, '--bypass-admin') -FailureMessage "Could not set color scheme '$sc'."
    }
    $needsThemeJs = $global:ThemesNeedingJS -contains $tn
    $jsVal = if ($needsThemeJs) { "1" } else { "0" }
    Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '1', 'replace_colors', '1', 'overwrite_assets', '1', 'inject_theme_js', $jsVal, '--bypass-admin') -FailureMessage 'Could not enable the selected theme assets.'
}

# Guidance shown when a file LibreSpot verified moments ago has vanished, or a
# known extension / custom-app file is missing. The usual cause is a security
# product (Microsoft Defender or another endpoint suite) quarantining it.
# LibreSpot only DETECTS and GUIDES -- it never disables AV, adds exclusions, or
# auto-restores quarantined files. Pure and side-effect free for unit testing.
function Get-QuarantineGuidance {
    param([string]$What = 'A verified file')
    return "$What is missing right after LibreSpot verified it. A security product (for example Microsoft Defender) may have quarantined it. Open Windows Security > Virus & threat protection > Protection history; if the file is listed, restore it and re-run LibreSpot. LibreSpot will not disable your antivirus, add exclusions, or restore quarantined files for you."
}

function Download-CommunityExtensions { param($Config)
    $exts = @($Config.Spicetify_Extensions)
    $extDir = (Get-SpicetifyIntegrationContext).ExtensionsDirectory
    if (-not (Test-Path $extDir)) { New-Item -Path $extDir -ItemType Directory -Force | Out-Null }
    $verifiedPaths = @()
    foreach ($ext in $exts) {
        if (-not $global:CommunityExtensions.Contains($ext)) { continue }
        $info = $global:CommunityExtensions[$ext]
        $destFile = Join-Path $extDir $ext
        $tempFile = Join-Path $extDir (".librespot-$ext.$PID.$([Guid]::NewGuid().ToString('N')).tmp")
        try {
            Write-Log "Downloading community extension: $ext from $($info.Source)..."
            $extHash = $info.SHA256
            $fromCache = Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $tempFile -Label "Community extension $ext"
            if (-not $fromCache) {
                try {
                    Download-FileSafe -Uri $info.Url -OutFile $tempFile
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $tempFile -Label "Community extension $ext") {
                        $fromCache = $true
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
            }
            # Sanity check: make sure we got JavaScript, not a 404 HTML page.
            # Read just the first 512 bytes to avoid loading a huge file.
            $head = Get-Content -LiteralPath $tempFile -TotalCount 5 -ErrorAction SilentlyContinue
            $headStr = ($head -join "`n").TrimStart()
            if ($headStr -match '^<(!DOCTYPE|html)' -or $headStr -match '^404:') {
                Write-Log "Community extension '$ext' downloaded but appears to be an HTML error page, not JavaScript. The URL may have changed. Skipping." -Level 'WARN'
                continue
            }
            Confirm-FileHash -Path $tempFile -ExpectedHash $extHash -Label "Community extension $ext"
            if (-not $fromCache) {
                Save-ToAssetCache -SourcePath $tempFile -SHA256Hash $extHash -Label "Community extension $ext" -SourceUrl $info.Url
            }
            Move-Item -LiteralPath $tempFile -Destination $destFile -Force
            Write-Log "Community extension '$ext' saved to $destFile"
            $verifiedPaths += $destFile
        } catch {
            Write-Log "Could not download community extension '$ext': $($_.Exception.Message). Skipping." -Level 'WARN'
        } finally {
            Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
        }
    }
    # A file LibreSpot just verified that has since vanished is the classic
    # antivirus-quarantine signal. Detect and guide; never auto-restore.
    $quarantineWarned = $false
    foreach ($vp in $verifiedPaths) {
        if (-not $quarantineWarned -and -not (Test-Path -LiteralPath $vp)) {
            $quarantineWarned = $true
            Write-Log (Get-QuarantineGuidance -What "The verified extension file '$(Split-Path -Leaf $vp)'") -Level 'WARN'
        }
    }
}

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

function Install-MarketplacePlaceholderTheme {
    # The official Marketplace installer creates a placeholder theme at
    # Themes\marketplace\color.ini and points current_theme at it so the store
    # can write theme colors and CSS snippets into an active theme. Without an
    # active theme, spicetify leaves inject_css/replace_colors off and every
    # Marketplace theme or snippet install silently does nothing. Content is the
    # upstream placeholder verbatim (a single [Marketplace] section header).
    # Idempotent; returns the placeholder theme directory path.
    $integration = Get-SpicetifyIntegrationContext
    $themeDirectory = Join-Path $integration.ThemesDirectory 'marketplace'
    $colorIniPath = Join-Path $themeDirectory 'color.ini'
    $expectedContent = "[Marketplace]`n"

    New-Item -Path $themeDirectory -ItemType Directory -Force | Out-Null
    $needsWrite = $true
    if (Test-Path -LiteralPath $colorIniPath -PathType Leaf) {
        try {
            $existing = [System.IO.File]::ReadAllText($colorIniPath)
            if ($existing.Trim() -eq $expectedContent.Trim()) { $needsWrite = $false }
        } catch {}
    }
    if ($needsWrite) {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($colorIniPath, $expectedContent, $utf8NoBom)
        Write-Log "Created Marketplace placeholder theme at $themeDirectory"
    }
    return $themeDirectory
}

function Install-MarketplaceNavFallbackExtension {
    # Spotify's global-nav redesigns periodically break the Spicetify CLI's
    # injected Marketplace nav link SILENTLY (regex mismatch in insertNavLink,
    # or the patched component variant never mounts - spicetify/marketplace
    # #1133/#1185/#1194). The store route still works, but users have no button.
    # This LibreSpot-managed extension waits for the app to settle and registers
    # a Spicetify.Topbar button ONLY when no Marketplace entry rendered, so the
    # store always stays reachable from the UI. Local-only; no network calls.
    # Idempotent; returns the extension file name.
    $integration = Get-SpicetifyIntegrationContext
    $extensionsDirectory = $integration.ExtensionsDirectory
    $fileName = 'librespot-marketplace-button.js'
    $filePath = Join-Path $extensionsDirectory $fileName

    $lines = @(
        '// LibreSpot Marketplace access button (fallback).',
        '// Registers a Topbar button only when the Spicetify-injected Marketplace',
        '// nav link failed to render (a recurring silent break across Spotify',
        '// global-nav redesigns). Managed by LibreSpot; removed when Marketplace',
        '// is disabled.',
        '(function libreSpotMarketplaceButton() {',
        '    if (window.__libreSpotMarketplaceButton) { return; }',
        '    window.__libreSpotMarketplaceButton = true;',
        '    var ICON = ''<svg role="img" height="16" width="16" viewBox="0 0 76.465 68.262" fill="currentColor"><path d="M151.909 72.923v6.5h10.097l8.663 44.567h48.968v-6.5h-43.61l-1.2-6.172h42.974l10.35-33.91h-59.915l-.872-4.485H151.91zm17.59 10.984h49.867l-6.393 20.91h-39.409l-4.064-20.91zm5.626 44.11a6.5 6.5 0 0 0-6.5 6.5 6.5 6.5 0 0 0 6.5 6.501 6.5 6.5 0 0 0 6.5-6.5 6.5 6.5 0 0 0-6.5-6.5zm38.274 0a6.5 6.5 0 0 0-6.5 6.5 6.5 6.5 0 0 0 6.5 6.501 6.5 6.5 0 0 0 6.5-6.5 6.5 6.5 0 0 0-6.5-6.5z" transform="translate(-151.909 -72.923)"/></svg>'';',
        '    function apiReady() {',
        '        return !!(window.Spicetify && Spicetify.Platform && Spicetify.Platform.History &&',
        '            Spicetify.Topbar && Spicetify.Topbar.Button);',
        '    }',
        '    function navEntryPresent() {',
        '        // The wrapper-rendered nav link, a sidebar item, or an earlier instance',
        '        // of this button. Covers the localized names Marketplace ships.',
        '        return !!document.querySelector(',
        '            ''a[href="/marketplace"], [aria-label="Marketplace"], [title="Marketplace"],'' +',
        '            '' [aria-label="Маркетплейс"], [title="Маркетплейс"]'');',
        '    }',
        '    function registerFallback() {',
        '        if (navEntryPresent()) { return; }',
        '        try {',
        '            new Spicetify.Topbar.Button("Marketplace", ICON, function () {',
        '                Spicetify.Platform.History.push("/marketplace");',
        '            });',
        '        } catch (error) {',
        '            // Topbar API drift: leave the UI untouched rather than break startup.',
        '        }',
        '    }',
        '    (function waitForApi(attempts) {',
        '        if (apiReady()) {',
        '            // Grace period so the native nav link (when it works) wins.',
        '            setTimeout(registerFallback, 4000);',
        '            return;',
        '        }',
        '        if (attempts > 0) { setTimeout(function () { waitForApi(attempts - 1); }, 300); }',
        '    })(200);',
        '})();'
    )
    $content = ($lines -join "`n") + "`n"

    New-Item -Path $extensionsDirectory -ItemType Directory -Force | Out-Null
    $needsWrite = $true
    if (Test-Path -LiteralPath $filePath -PathType Leaf) {
        try {
            if ([System.IO.File]::ReadAllText($filePath) -eq $content) { $needsWrite = $false }
        } catch {}
    }
    if ($needsWrite) {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($filePath, $content, $utf8NoBom)
        Write-Log "Installed the Marketplace access-button fallback extension at $filePath"
    }
    return $fileName
}

function Module-InstallMarketplace { param($Config)
    $integration = Get-SpicetifyIntegrationContext
    # 'spicetify-marketplace' is the pre-1.0 app name; the official installer
    # removes it, so keep it managed here to clean up legacy installs.
    $managedApps = @('marketplace', 'spicetify-marketplace')
    $managedExtensions = @('librespot-marketplace-button.js')
    $marketplaceDirs = @(
        $integration.MarketplaceDirectory,
        $integration.LegacyMarketplaceDirectory
    )
    if (-not $Config.Spicetify_Marketplace) {
        Write-Log "Marketplace: disabled. Removing LibreSpot-managed Marketplace state if present..." -Level 'STEP'
        # Clear the placeholder-theme reference BEFORE deleting its directory so
        # an interrupted removal never leaves current_theme pointing at a theme
        # that no longer exists (which would fail every later spicetify apply).
        $configuredTheme = [string](Get-SpicetifyConfigEntries)['current_theme']
        if ($configuredTheme -eq 'marketplace') {
            Invoke-SpicetifyCli -Arguments @('config', 'current_theme', '', 'inject_css', '0', 'replace_colors', '0', '--bypass-admin') -FailureMessage 'Could not clear the Marketplace placeholder theme.'
            Write-Log 'Cleared the Marketplace placeholder theme from Spicetify config.'
        }
        foreach ($dir in $marketplaceDirs) {
            $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
        }
        $placeholderDir = Join-Path $integration.ThemesDirectory 'marketplace'
        $null = Remove-PathSafely -Path $placeholderDir -Label 'Marketplace placeholder theme'
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems @() -ManagedItems $managedExtensions
        $fallbackPath = Join-Path $integration.ExtensionsDirectory 'librespot-marketplace-button.js'
        if (Test-Path -LiteralPath $fallbackPath -PathType Leaf) {
            Remove-Item -LiteralPath $fallbackPath -Force -ErrorAction SilentlyContinue
            Write-Log 'Removed the Marketplace access-button fallback extension.'
        }
        return
    }

    Write-Log "Installing Marketplace..." -Level 'STEP'
    $ca = $integration.CustomAppsDirectory
    New-Item -Path $ca -ItemType Directory -Force | Out-Null
    $md=Join-Path $ca "marketplace"
    $mz = New-LibreSpotTempFile -Name 'marketplace.zip'
    $mu = New-LibreSpotTempDirectory -Name 'marketplace-unpack'
    foreach ($dir in $marketplaceDirs) {
        $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
    }
    New-Item -Path $md -ItemType Directory -Force | Out-Null
    try {
        $marketplaceHash = $global:PinnedReleases.Marketplace.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $marketplaceHash -DestinationPath $mz -Label 'Marketplace archive')) {
            try {
                Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $mz
            } catch {
                if (Get-FromAssetCache -SHA256Hash $marketplaceHash -DestinationPath $mz -Label 'Marketplace archive') {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $mz -ExpectedHash $marketplaceHash -Label "Marketplace"
            Save-ToAssetCache -SourcePath $mz -SHA256Hash $marketplaceHash -Label 'Marketplace archive' -SourceUrl $global:URL_MARKETPLACE
        }
        Expand-ArchiveSafely -ZipPath $mz -DestinationPath $mu -Label 'Marketplace'
        $sp = if (Test-Path (Join-Path $mu "marketplace-dist")) { Join-Path $mu "marketplace-dist\*" } else { Join-Path $mu "*" }
        Copy-Item -Path $sp -Destination $md -Recurse -Force
        $health = Get-MarketplaceHealth
        if (-not $health.HasFiles) {
            throw 'Marketplace archive did not produce expected Spicetify custom app files.'
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @('marketplace') -ManagedItems $managedApps
        # Official Marketplace install contract: the store can only install
        # themes and CSS snippets into an ACTIVE theme with CSS injection on.
        # Themes install before Marketplace, so an empty current_theme here
        # means no (or a failed) theme selection - point Spicetify at the
        # upstream placeholder theme, created before the config references it.
        $configuredTheme = [string](Get-SpicetifyConfigEntries)['current_theme']
        if ([string]::IsNullOrWhiteSpace($configuredTheme)) {
            $null = Install-MarketplacePlaceholderTheme
            Invoke-SpicetifyCli -Arguments @('config', 'current_theme', 'marketplace', '--bypass-admin') -FailureMessage 'Could not activate the Marketplace placeholder theme.'
            Write-Log 'Activated the Marketplace placeholder theme so store themes and snippets can render.'
        } elseif ($configuredTheme -eq 'marketplace') {
            # Re-assert the placeholder files in case a previous run was interrupted.
            $null = Install-MarketplacePlaceholderTheme
        }
        Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '1', 'replace_colors', '1', '--bypass-admin') -FailureMessage 'Could not enable CSS injection for Marketplace.'
        # Spotify's global-nav changes can silently break the injected nav link
        # (spicetify/marketplace#1133/#1185); this managed extension adds a
        # Topbar access button only when no Marketplace entry rendered.
        $fallbackName = Install-MarketplaceNavFallbackExtension
        Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems @($fallbackName) -ManagedItems $managedExtensions
        $health = Get-MarketplaceHealth
        if ($health.IsReady) {
            Write-Log "Marketplace enabled. The store appears as a Marketplace item in Spotify; if it is hidden, open spotify:app:marketplace directly."
            Write-Log "If the store page loads empty, GitHub may be rate-limiting the catalog fetch - wait about a minute and reopen Marketplace."
        } else {
            Write-Log "Marketplace files were installed but status is '$($health.Status)'. Use Maintenance > Repair and open Marketplace if the sidebar icon is hidden." -Level 'WARN'
        }
    } finally {
        Remove-Item -LiteralPath $mz -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $mu -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Open-SpicetifyMarketplace {
    $requestedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    try {
        Start-Process -FilePath 'explorer.exe' -ArgumentList 'spotify:app:marketplace'
        Write-Log 'Requested Spotify Marketplace via spotify:app:marketplace.'
        Start-Sleep -Milliseconds 500
        $spotifyRunning = try { @((Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)).Count -gt 0 } catch { $false }
        $result = [pscustomobject]@{
            Succeeded               = $true
            Message                 = 'spotify:app:marketplace was handed to Windows.'
            RequestedAtUtc          = $requestedAtUtc
            SpotifyRunningAfterOpen = $spotifyRunning
        }
    } catch {
        $message = "Could not open spotify:app:marketplace automatically: $($_.Exception.Message)"
        Write-Log $message -Level 'WARN'
        $result = [pscustomobject]@{
            Succeeded               = $false
            Message                 = $message
            RequestedAtUtc          = $requestedAtUtc
            SpotifyRunningAfterOpen = $null
        }
    }
    Write-MarketplaceVisibilityEvidence -Source 'OpenMarketplace' -OpenUriSucceeded $result.Succeeded -OpenUriMessage $result.Message -OpenUriRequestedAtUtc $result.RequestedAtUtc -SpotifyRunningAfterOpen $result.SpotifyRunningAfterOpen | Out-Null
    return $result
}

function Module-InstallCustomApps { param($Config)
    $requestedApps = @($Config.Spicetify_CustomApps | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $managedApps = @($global:CommunityCustomApps.Keys)
    $integration = Get-SpicetifyIntegrationContext
    $customAppsDirectory = $integration.CustomAppsDirectory

    if ($requestedApps.Count -eq 0) {
        Write-Log 'Custom apps: none selected. Removing LibreSpot-managed custom apps if present...' -Level 'STEP'
        foreach ($appId in $managedApps) {
            $null = Remove-PathSafely -Path (Join-Path $customAppsDirectory $appId) -Label "Custom app $appId"
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        return
    }

    Write-Log "Custom apps: $($requestedApps -join ', ')..." -Level 'STEP'
    New-Item -Path $customAppsDirectory -ItemType Directory -Force | Out-Null
    $installedApps = [System.Collections.Generic.List[string]]::new()

    foreach ($appId in $requestedApps) {
        if (-not $global:CommunityCustomApps.Contains($appId)) {
            Write-Log "Unknown custom app '$appId'. Skipping." -Level 'WARN'
            continue
        }

        $info = $global:CommunityCustomApps[$appId]
        $safeName = ($appId -replace '[^a-zA-Z0-9_-]', '_')
        $zipPath = New-LibreSpotTempFile -Name "custom-app-$safeName.zip"
        $unpackPath = New-LibreSpotTempDirectory -Name "custom-app-$safeName-unpack"
        $destinationPath = Join-Path $customAppsDirectory $appId

        try {
            Write-Log "Downloading custom app '$($info.DisplayName)' from $($info.Source)..."
            $expectedHash = [string]$info.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zipPath -Label "Custom app $appId archive")) {
                try {
                    Download-FileSafe -Uri $info.Url -OutFile $zipPath
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zipPath -Label "Custom app $appId archive") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $zipPath -ExpectedHash $expectedHash -Label "Custom app $appId"
                Save-ToAssetCache -SourcePath $zipPath -SHA256Hash $expectedHash -Label "Custom app $appId archive" -SourceUrl $info.Url
            }

            Expand-ArchiveSafely -ZipPath $zipPath -DestinationPath $unpackPath -Label "Custom app $appId" -MaxExpandedBytes 250MB
            $sourcePath = Join-Path $unpackPath ([string]$info.AssetPath)
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
                $candidate = Get-ChildItem -LiteralPath $unpackPath -Directory -ErrorAction SilentlyContinue |
                    Where-Object {
                        (Test-Path -LiteralPath (Join-Path $_.FullName 'manifest.json') -PathType Leaf) -and
                        (Test-Path -LiteralPath (Join-Path $_.FullName 'extension.js') -PathType Leaf)
                    } |
                    Select-Object -First 1
                if ($candidate) { $sourcePath = $candidate.FullName }
            }

            if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
                throw "Custom app archive did not contain expected folder '$($info.AssetPath)'."
            }

            foreach ($requiredFile in @('manifest.json', 'extension.js')) {
                if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $requiredFile) -PathType Leaf)) {
                    throw "Custom app '$appId' is missing required file '$requiredFile'."
                }
            }

            $null = Remove-PathSafely -Path $destinationPath -Label "Custom app $appId"
            New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
            Copy-Item -Path (Join-Path $sourcePath '*') -Destination $destinationPath -Recurse -Force
            $installedApps.Add($appId)
            Write-Log "Custom app '$($info.DisplayName)' installed to $destinationPath"
        } catch {
            Write-Log "Could not install custom app '$appId': $($_.Exception.Message). Skipping." -Level 'WARN'
        } finally {
            Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $unpackPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @($installedApps) -ManagedItems $managedApps
}

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

function Repair-Marketplace {
    param($Config)
    if (-not (Test-SpicetifyCliInstalled)) {
        throw 'Spicetify CLI is not installed, so LibreSpot cannot repair Marketplace yet. Run Recommended setup or Reapply first.'
    }
    if (-not $Config) {
        $Config = Normalize-LibreSpotConfig -Config @{}
    }
    $Config.Spicetify_Marketplace = $true

    Invoke-WithSpicetifyStatePreservation -Action 'RepairMarketplace' -Operation {
        Write-Log 'Repairing Marketplace files and custom_apps registration...' -Level 'STEP'
        Module-InstallMarketplace -Config $Config
        Write-Log 'Applying Spicetify so Marketplace is discoverable in Spotify...' -Level 'STEP'
        $applyResult = Module-ApplySpicetify -Config $Config -EvidenceSource 'RepairMarketplace'

        $health = Get-MarketplaceHealth
        if ($health.IsReady) {
            Write-Log "Marketplace repair verified at $($health.Path)." -Level 'SUCCESS'
        } else {
            Write-Log "Marketplace repair finished, but status is '$($health.Status)'. Open spotify:app:marketplace directly if the sidebar icon remains hidden." -Level 'WARN'
        }
        $openResult = Open-SpicetifyMarketplace
        Write-MarketplaceVisibilityEvidence -Source 'RepairMarketplace' -ApplyStage $applyResult.Stage -ApplySucceeded $applyResult.Succeeded -ApplyMessage $applyResult.Message -OpenUriSucceeded $openResult.Succeeded -OpenUriMessage $openResult.Message -OpenUriRequestedAtUtc $openResult.RequestedAtUtc -SpotifyRunningAfterOpen $openResult.SpotifyRunningAfterOpen | Out-Null
    } | Out-Null
}

function Get-SpicetifyCliMajorVersion {
    # Pure: parse the leading integer major from a Spicetify version string such
    # as '2.44.0', 'v3.0.0', or '3.1.2-dev'. Returns $null for empty or
    # non-numeric input (e.g. 'Dev'), where the version is treated as unknown.
    param([string]$Version)
    if ([string]::IsNullOrWhiteSpace($Version)) { return $null }
    $trimmed = $Version.Trim().TrimStart('v', 'V')
    $match = [regex]::Match($trimmed, '^\d+')
    if (-not $match.Success) { return $null }
    return [int]$match.Value
}

function Test-SpicetifyCliVersionSupported {
    # LibreSpot targets Spicetify 2.x. A future Spicetify v3 (spicetify/cli#3038)
    # changes the on-disk contract (symlink + hooks + modules) that LibreSpot's
    # patch detection does not understand. Unknown/unparseable versions are
    # treated as supported so a missing probe never raises a false warning; only
    # a parsed major greater than 2 is unsupported.
    param([string]$Version)
    $major = Get-SpicetifyCliMajorVersion -Version $Version
    if ($null -eq $major) { return $true }
    return ($major -le 2)
}

function Get-InstalledSpicetifyCliVersion {
    # Best-effort read of the installed Spicetify CLI version via `spicetify -v`.
    # Returns $null when the CLI is absent or the probe fails for any reason;
    # callers must treat $null as 'unknown', never as an error. Never throws.
    try {
        $cliPath = (Get-SpicetifyIntegrationContext).CliPath
    } catch {
        return $null
    }
    if (-not (Test-Path -LiteralPath $cliPath)) { return $null }
    try {
        $output = & $cliPath '-v' 2>$null
        foreach ($line in @($output)) {
            $match = [regex]::Match([string]$line, '\d+(?:\.\d+)+')
            if ($match.Success) { return $match.Value }
        }
    } catch {}
    return $null
}

function Get-SpicetifyDiagnosticSnapshot {
    $snapshot = [ordered]@{}
    $configPath = (Get-SpicetifyIntegrationContext).ConfigPath
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
    # A future Spicetify v3 changes the on-disk contract (spicetify/cli#3038); flag
    # an unsupported CLI major so diagnostics do not read as a broken 2.x patch.
    $cliVersion = Get-InstalledSpicetifyCliVersion
    $snapshot['spicetify_cli_version'] = $cliVersion
    $snapshot['spicetify_cli_supported'] = Test-SpicetifyCliVersionSupported -Version $cliVersion
    return $snapshot
}

function Module-ApplySpicetify {
    param(
        $Config,
        [string]$EvidenceSource = 'Module-ApplySpicetify'
    )
    Write-Log 'Applying Spicetify changes...' -Level 'STEP'

    # Marketplace-only mode intentionally does NOT disable theme injection here.
    # The official Marketplace contract needs inject_css/replace_colors on with
    # the placeholder theme active (Module-InstallMarketplace asserts this), or
    # every store theme/snippet install is a silent no-op. When no theme at all
    # is configured, the Spicetify CLI already forces injection off on its own
    # (InitSetting in src/cmd/cmd.go), so zeroing the ini here was redundant for
    # safety and actively broke the Marketplace theme contract.

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
    $applyStage = 'backup apply'
    try {
        Invoke-SpicetifyCli -Arguments @('backup', 'apply', '--bypass-admin') -FailureMessage 'Could not backup and apply Spicetify changes.'
        Write-Log 'Spicetify applied successfully.' -Level 'SUCCESS'
        Update-ApplyState -Outcome 'SpicetifyApplySucceeded' -Successful $true
        $message = 'Spicetify backup apply succeeded.'
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $true -ApplyMessage $message | Out-Null
        return [pscustomobject]@{
            Stage     = $applyStage
            Succeeded = $true
            Message   = $message
        }
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
        Update-ApplyState -Outcome 'SpicetifyApplyRolledBack' -Successful $false -ErrorMessage $applyError
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $false -ApplyMessage $applyError | Out-Null
        throw "Spicetify apply failed but LibreSpot restored Spotify to a usable state. Apply error: $applyError"
    } else {
        Update-ApplyState -Outcome 'SpicetifyApplyRollbackFailed' -Successful $false -ErrorMessage "Apply error: $applyError | Rollback error: $restoreError"
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $false -ApplyMessage "$applyError | Rollback error: $restoreError" | Out-Null
        throw "Spicetify apply failed and rollback also failed. Apply error: $applyError | Rollback error: $restoreError"
    }
}

function Reapply-SavedSpicetifySetup { param($Config)
    if (-not (Test-SpicetifyCliInstalled)) {
        Write-Log "Spicetify CLI is missing, so LibreSpot will reinstall it before restoring your saved setup." -Level 'WARN'
        Module-InstallSpicetifyCLI
    }

    Invoke-WithSpicetifyStatePreservation -Action 'Reapply' -Operation {
        Module-InstallThemes -Config $Config
        Module-InstallExtensions -Config $Config
        Module-InstallMarketplace -Config $Config
        Module-InstallCustomApps -Config $Config
        Module-ApplySpicetify -Config $Config -EvidenceSource 'Reapply' | Out-Null
    } | Out-Null
}

function Write-PlanEntry {
    param(
        [string]$Category,
        [string]$Target,
        [bool]$WouldChange = $true,
        [string]$SafetyDecision = 'Allowed',
        [bool]$RequiresElevation = $false,
        [bool]$Reversible = $false,
        [string]$Source = '',
        [string]$Description = ''
    )
    $entry = @{
        category         = $Category
        target           = $Target
        wouldChange      = $WouldChange
        safetyDecision   = $SafetyDecision
        requiresElevation = $RequiresElevation
        reversible       = $Reversible
        source           = $Source
        description      = $Description
    }
    Write-EventLine -Kind 'plan' -Level 'INFO' -Payload ($entry | ConvertTo-Json -Compress -Depth 4)
}

function Invoke-LibreSpotPlan {
    $config = Load-LibreSpotConfig
    Write-EventLine -Kind 'plan-start' -Level 'INFO' -Payload "Generating plan for $($config.Mode) install"

    $patcherReport = Get-ThirdPartyPatcherReport
    foreach ($footprint in @($patcherReport.Footprints | Where-Object { $_.Ownership -eq 'foreign' })) {
        Write-PlanEntry -Category 'migration' -Target $footprint.Name `
            -Description "Migration review: $($footprint.Name) detected. $($footprint.Recommendation)" `
            -WouldChange $true -Reversible ($footprint.Id -eq 'standalone-spicetify') `
            -Source 'Get-ThirdPartyPatcherReport'
    }

    if ($config.CleanInstall) {
        Write-PlanEntry -Category 'spotify' -Target $global:SPOTIFY_EXE_PATH `
            -Description 'Remove existing Spotify installation (8-phase cleanup)' `
            -Reversible $false -Source 'Module-NukeSpotify'
        Write-PlanEntry -Category 'file' -Target "$env:APPDATA\Spotify" `
            -Description 'Remove Spotify roaming data directory' `
            -Reversible $false -Source 'Module-NukeSpotify'
        Write-PlanEntry -Category 'file' -Target "$env:LOCALAPPDATA\Spotify" `
            -Description 'Remove Spotify local data directory' `
            -Reversible $false -Source 'Module-NukeSpotify'
        Write-PlanEntry -Category 'scheduled-task' -Target '\LibreSpot\ReapplyWatcher' `
            -WouldChange (Test-AutoReapplyTaskRegistered) `
            -Description 'Remove watcher task during cleanup' `
            -Reversible $true -Source 'Module-NukeSpotify'
    }

    Write-PlanEntry -Category 'download' -Target $global:URL_SPOTX `
        -Description "Download and verify SpotX from pinned commit $($global:PinnedReleases.SpotX.Commit)" `
        -WouldChange $true -Reversible $false -Source 'Module-InstallSpotX'
    Write-PlanEntry -Category 'spotify' -Target $global:SPOTIFY_EXE_PATH `
        -Description 'Apply SpotX patches to Spotify' `
        -WouldChange $true -Reversible $true -RequiresElevation $false `
        -Source 'Module-InstallSpotX'
    if ($config.SpotX_CustomPatchesEnabled -and -not [string]::IsNullOrWhiteSpace([string]$config.SpotX_CustomPatchesJson)) {
        Write-PlanEntry -Category 'config' -Target 'SpotX custom patches.json' `
            -Description 'Stage reviewed custom SpotX patches from the saved profile' `
            -WouldChange $true -Reversible $true -RequiresElevation $false `
            -Source 'New-SpotXCustomPatchesFile'
    }

    $spicetifyIntegration = Get-SpicetifyIntegrationContext
    Write-PlanEntry -Category 'download' -Target "Spicetify CLI v$($global:PinnedReleases.SpicetifyCLI.Version)" `
        -Description 'Download and install Spicetify CLI' `
        -WouldChange (-not (Test-Path -LiteralPath $spicetifyIntegration.CliPath)) `
        -Reversible $true -Source 'Module-InstallSpicetifyCLI'
    Write-PlanEntry -Category 'path' -Target $spicetifyIntegration.InstallDirectory `
        -Description 'Add Spicetify to user PATH' `
        -WouldChange $true -Reversible $true -Source 'Module-InstallSpicetifyCLI'

    if ($config.Spicetify_Theme -and $config.Spicetify_Theme -ne 'Default') {
        Write-PlanEntry -Category 'download' -Target "Theme: $($config.Spicetify_Theme)" `
            -Description "Download and install theme $($config.Spicetify_Theme)" `
            -WouldChange $true -Reversible $true -Source 'Module-InstallThemes'
    }

    $extensionList = @()
    if ($config.Spicetify_Extensions) { $extensionList = @($config.Spicetify_Extensions) }
    if ($extensionList.Count -gt 0) {
        Write-PlanEntry -Category 'config' -Target 'Spicetify extensions' `
            -Description "Sync $($extensionList.Count) extension(s): $($extensionList -join ', ')" `
            -WouldChange $true -Reversible $true -Source 'Module-InstallExtensions'
    }

    if ($config.Spicetify_Marketplace) {
        Write-PlanEntry -Category 'download' -Target "Marketplace v$($global:PinnedReleases.Marketplace.Version)" `
            -Description 'Download and install Spicetify Marketplace custom app' `
            -WouldChange $true -Reversible $true -Source 'Module-InstallMarketplace'
    }

    $customAppList = @()
    if ($config.Spicetify_CustomApps) { $customAppList = @($config.Spicetify_CustomApps) }
    if ($customAppList.Count -gt 0) {
        Write-PlanEntry -Category 'download' -Target "Custom apps: $($customAppList -join ', ')" `
            -Description "Download and install $($customAppList.Count) verified Spicetify custom app(s)" `
            -WouldChange $true -Reversible $true -Source 'Module-InstallCustomApps'
    }

    Write-PlanEntry -Category 'spicetify' -Target 'backup apply' `
        -Description 'Apply all Spicetify customizations to Spotify' `
        -WouldChange $true -Reversible $true -Source 'Module-ApplySpicetify'

    if ($config.LaunchAfter -and (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) {
        Write-PlanEntry -Category 'process' -Target $global:SPOTIFY_EXE_PATH `
            -Description 'Launch Spotify after install' `
            -WouldChange $false -Reversible $false -Source 'Invoke-LibreSpotInstall'
    }

    Write-PlanEntry -Category 'config' -Target $ConfigPath `
        -Description 'Save configuration to disk' `
        -WouldChange $true -Reversible $true -Source 'Save-LibreSpotConfig'

    if ($config.AutoReapply_Enabled) {
        Write-PlanEntry -Category 'scheduled-task' -Target '\LibreSpot\ReapplyWatcher' `
            -Description 'Register auto-reapply watcher scheduled task' `
            -WouldChange (-not (Test-AutoReapplyTaskRegistered)) `
            -Reversible $true -Source 'Register-AutoReapplyTask'
    }

    Write-EventLine -Kind 'plan-end' -Level 'INFO' -Payload 'Plan generation complete — no mutations performed'
}

function Invoke-LibreSpotInstall {
    $config = Load-LibreSpotConfig
    Write-Log "--- LibreSpot installation started ($($config.Mode)) ---" -Level 'HEADER'
    $patcherReport = Get-ThirdPartyPatcherReport
    if ($patcherReport.HasForeignState) {
        Write-Log "$($patcherReport.Summary) $($patcherReport.Recommendation)" -Level 'WARN'
        Write-OperationJournalEntry -Phase 'foreign-patcher-detection' -Target 'Spotify and Spicetify state' -SafetyDecision 'NeedsReview' -Result 'Detected' -WouldChange $false -Reversible $true -RollbackHint $patcherReport.Recommendation -Data @{
            ownership = $patcherReport.Ownership
            footprintIds = @($patcherReport.Footprints | Where-Object { $_.Ownership -eq 'foreign' } | ForEach-Object { $_.Id })
        }
    }
    $steps = @('SpotX', 'SpicetifyCLI', 'Themes', 'Extensions', 'Marketplace', 'CustomApps', 'Apply')
    if ($config.CleanInstall) { $steps = @('Cleanup') + $steps }

    $labels = @{
        Cleanup = 'Removing the old setup'
        SpotX = 'Applying SpotX'
        SpicetifyCLI = 'Installing Spicetify CLI'
        Themes = 'Adding bundled themes'
        Extensions = 'Preparing extensions'
        Marketplace = 'Installing Marketplace'
        CustomApps = 'Adding custom apps'
        Apply = 'Applying your setup'
    }

    # Hide any Spotify windows that SpotX/Spicetify briefly surface during patching
    # so the desktop shell stays in focus and Spotify never flashes over this window.
    $installSteps = {
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
                    'CustomApps' { Module-InstallCustomApps -Config $config }
                    'Apply' { Module-ApplySpicetify -Config $config | Out-Null }
                }
            }
        } finally {
            Stop-SpotifyWindowWatcher -Watcher $watcher
        }
    }
    $standaloneSpicetify = @($patcherReport.Footprints | Where-Object { $_.Id -eq 'standalone-spicetify' }).Count -gt 0
    if ($standaloneSpicetify) {
        Invoke-WithSpicetifyStatePreservation -Action 'InstallMigration' -Operation $installSteps | Out-Null
    } else {
        & $installSteps
    }

    if ($config.LaunchAfter -and (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) {
        Write-Log 'Launching Spotify...'
        Update-BackendState -Progress 98 -Status 'Launching Spotify' -Step 'Checking patched session stability'
        # Spotify is single-instance: if any Spotify (or helper) process is still
        # alive, this launch just focuses the existing un-patched window instead of
        # starting the freshly patched session, so Marketplace/extensions stay
        # hidden until a manual restart. Force a clean slate first so the patched
        # result is visible from the get-go.
        Stop-SpotifyProcesses -MaxAttempts 5
        # Launch via explorer.exe so Spotify starts in the desktop user context instead of
        # inheriting our elevated token. A directly-started Spotify would run as Administrator,
        # which Spotify explicitly warns against and which breaks drag-and-drop from Explorer
        # and some web-auth flows.
        Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$global:SPOTIFY_EXE_PATH`""
        if (-not (Test-SpotifySessionStability -WaitSeconds 20)) {
            Update-BackendState -Progress 99 -Status 'Restoring Spotify' -Step 'Undoing active Spicetify customizations after an unstable launch'
            Write-Log 'Spotify did not stay open after patching; attempting Spicetify restore before stopping the install.' -Level 'WARN'
            Stop-SpotifyProcesses -MaxAttempts 3
            $restoreError = $null
            $restored = $false
            try {
                $restored = Restore-SpotifyIfSpicetifyPresent `
                    -FailureMessage 'Could not restore Spotify after the unstable patched launch.' `
                    -MissingMessage 'Spicetify CLI was not found, so LibreSpot could not automatically restore active customizations.'
            } catch {
                $restoreError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown restore error.' }
                Write-Log "Automatic restore after unstable launch failed: $restoreError" -Level 'ERROR'
            }

            if ([string]::IsNullOrWhiteSpace($restoreError) -and $restored) {
                Write-Log 'Spicetify restore completed after the unstable patched launch.' -Level 'SUCCESS'
                throw 'Spotify did not stay open after patching. LibreSpot restored active Spicetify customizations before stopping; rerun with fewer Spicetify extensions or use Full reset if Spotify still will not open.'
            }

            if ([string]::IsNullOrWhiteSpace($restoreError)) {
                throw 'Spotify did not stay open after patching, and automatic Spicetify restore was unavailable. Use Maintenance > Restore vanilla or Full reset before retrying.'
            }

            throw "Spotify did not stay open after patching, and automatic Spicetify restore failed: $restoreError"
        }
    }

    Update-BackendState -Progress 100 -Status 'Setup complete' -Step 'Spotify is ready'
    Write-Log '--- Installation complete ---' -Level 'SUCCESS'
}

function Invoke-LibreSpotMaintenance {
    $patcherReport = Get-ThirdPartyPatcherReport
    if ($patcherReport.HasForeignState -and $Action -in @('Reapply', 'RepairMarketplace', 'SafeMode', 'CreateBackup', 'RestoreBackup', 'RestoreVanilla', 'UninstallSpicetify', 'FullReset')) {
        Write-Log "$($patcherReport.Summary) Requested action: $Action. $($patcherReport.Recommendation)" -Level 'WARN'
        Write-OperationJournalEntry -Phase 'foreign-patcher-detection' -Target 'Spotify and Spicetify state' -SafetyDecision 'NeedsReview' -Result 'Detected' -WouldChange $false -Reversible $true -RollbackHint $patcherReport.Recommendation -Data @{
            action = $Action
            ownership = $patcherReport.Ownership
            footprintIds = @($patcherReport.Footprints | Where-Object { $_.Ownership -eq 'foreign' } | ForEach-Object { $_.Id })
        }
    }
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
                $spotxHash = $global:PinnedReleases.SpotX.SHA256
                if (-not (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $destination -Label 'SpotX run.ps1')) {
                    try {
                        Download-FileSafe -Uri $global:URL_SPOTX -OutFile $destination
                    } catch {
                        if (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $destination -Label 'SpotX run.ps1') {
                            Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                        } else { throw }
                    }
                    Confirm-FileHash -Path $destination -ExpectedHash $spotxHash -Label 'SpotX run.ps1'
                    Save-ToAssetCache -SourcePath $destination -SHA256Hash $spotxHash -Label 'SpotX run.ps1' -SourceUrl $global:URL_SPOTX
                }
                $params = Build-SpotXParams -Config $savedConfig
                Invoke-ExternalScriptIsolated -FilePath $destination -Arguments $params -ExpectedHash $spotxHash -Label 'SpotX run.ps1'

                Update-BackendState -Progress 60 -Status 'Restoring saved Spicetify state' -Step 'Rebuilding CLI, themes, extensions, and Marketplace'
                Reapply-SavedSpicetifySetup -Config $savedConfig
                Write-Log 'Saved Spicetify setup restored successfully.' -Level 'SUCCESS'
            } finally {
                Stop-SpotifyWindowWatcher -Watcher $watcher
                Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
            }
        }
        'RepairMarketplace' {
            Update-BackendState -Progress 20 -Status 'Repairing Marketplace' -Step 'Reinstalling the custom app'
            $savedConfig = Load-LibreSpotConfig
            Repair-Marketplace -Config $savedConfig
        }
        'OpenMarketplace' {
            Update-BackendState -Progress 35 -Status 'Opening Marketplace' -Step 'Launching spotify:app:marketplace'
            $health = Get-MarketplaceHealth
            if (-not $health.IsReady) {
                Write-Log "Marketplace status is '$($health.Status)', so open-only launch may fail. Use Repair Marketplace first if Spotify does not show it." -Level 'WARN'
            }
            Open-SpicetifyMarketplace | Out-Null
            Write-Log 'Marketplace launch requested.' -Level 'SUCCESS'
        }
        'SafeMode' {
            Update-BackendState -Progress 10 -Status 'Entering safe mode' -Step 'Disabling all themes and extensions'
            Restore-SpotifyIfSpicetifyPresent `
                -FailureMessage 'Spicetify restore failed — try Reapply or Restore Vanilla.' `
                -MissingMessage 'Spicetify CLI not found — no customizations to disable.'
            Write-Log 'Safe mode active — all customizations disabled. Use Reapply to restore your setup.' -Level 'SUCCESS'
        }
        'CreateBackup' {
            Update-BackendState -Progress 10 -Status 'Creating backup' -Step 'Backing up Spicetify configuration'
            if (-not (Test-SpicetifyCliInstalled)) {
                Write-Log 'Spicetify CLI is not installed — nothing to back up.' -Level 'WARN'
            } else {
                Invoke-SpicetifyCli -Arguments @('backup', '--bypass-admin') -FailureMessage 'Spicetify backup failed.'
                Write-Log 'Spicetify configuration backed up.' -Level 'SUCCESS'
            }
        }
        'RestoreBackup' {
            Update-BackendState -Progress 10 -Status 'Restoring backup' -Step 'Restoring Spicetify configuration from backup'
            Restore-SpotifyIfSpicetifyPresent `
                -FailureMessage 'Spicetify restore failed. The backup may be missing or corrupt.' `
                -MissingMessage 'Spicetify CLI not found — cannot restore.'
            Update-BackendState -Progress 60 -Status 'Reapplying' -Step 'Reapplying Spicetify customizations after restore'
            Invoke-SpicetifyCli -Arguments @('backup', 'apply', '--bypass-admin') -FailureMessage 'Spicetify apply after restore failed.'
            Write-Log 'Backup restored and customizations reapplied.' -Level 'SUCCESS'
        }
        'RestoreVanilla' {
            Update-BackendState -Progress 35 -Status 'Restoring vanilla Spotify' -Step 'Removing active Spicetify customizations'
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Could not restore vanilla Spotify.' -MissingMessage 'Spicetify CLI was not found, so LibreSpot cannot run a restore. Spotify may already be vanilla.') {
                Write-Log 'Vanilla Spotify restored successfully.' -Level 'SUCCESS'
            }
        }
        'UninstallSpicetify' {
            Update-BackendState -Progress 15 -Status 'Restoring Spotify first' -Step 'Removing active customizations'
            $spicetifyIntegration = Get-SpicetifyIntegrationContext
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Could not restore Spotify before uninstalling Spicetify.' -MissingMessage 'Spicetify CLI was already missing, so LibreSpot will remove any leftover files and PATH entries directly.') {
                Write-Log 'Spotify restored successfully before removing Spicetify.' -Level 'SUCCESS'
            }
            Update-BackendState -Progress 45 -Status 'Removing Spicetify files' -Step 'Cleaning local tools and config'
            $null = Remove-PathSafely -Path $spicetifyIntegration.ConfigDirectory -Label 'Spicetify config directory'
            $null = Remove-PathSafely -Path $spicetifyIntegration.InstallDirectory -Label 'Spicetify CLI directory'
            $null = Remove-PathEntry -Entry $spicetifyIntegration.InstallDirectory -Scope 'Process'
            if (Remove-PathEntry -Entry $spicetifyIntegration.InstallDirectory -Scope 'User') {
                Write-Log 'Removed Spicetify from the user PATH.'
            }
        }
        'FullReset' {
            Update-BackendState -Progress 10 -Status 'Restoring vanilla Spotify' -Step 'Preparing deep cleanup'
            $spicetifyIntegration = Get-SpicetifyIntegrationContext
            try {
                Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage 'Could not restore Spotify before the full reset.'
            } catch {
                Write-Log "$($_.Exception.Message) Continuing because Spotify will be removed next." -Level 'WARN'
            }
            Update-BackendState -Progress 30 -Status 'Removing Spicetify tools' -Step 'Cleaning customization layer'
            $null = Remove-PathSafely -Path $spicetifyIntegration.ConfigDirectory -Label 'Spicetify config directory'
            $null = Remove-PathSafely -Path $spicetifyIntegration.InstallDirectory -Label 'Spicetify CLI directory'
            Update-BackendState -Progress 45 -Status 'Removing watcher task' -Step 'Unregistering scheduled task'
            if (Unregister-AutoReapplyTask) {
                Write-Log 'Auto-reapply scheduled task removed.'
                Set-AutoReapplyConfigPreference -Enabled $false
            }
            Update-BackendState -Progress 50 -Status 'Removing Spotify itself' -Step 'Running full cleanup'
            Module-NukeSpotify
            $null = Remove-PathEntry -Entry $spicetifyIntegration.InstallDirectory -Scope 'Process'
            $null = Remove-PathEntry -Entry $spicetifyIntegration.InstallDirectory -Scope 'User'
        }
        'RemoveSelfData' {
            Update-BackendState -Progress 10 -Status 'Removing watcher task' -Step 'Unregistering scheduled task'
            if (Unregister-AutoReapplyTask) {
                Write-Log 'Auto-reapply scheduled task removed.'
            } else {
                Write-Log 'Auto-reapply scheduled task was not registered.'
            }
            $selfPaths = @(
                @{ Path = $global:BACKUP_ROOT; Label = 'Backup directory' }
                @{ Path = (Join-Path $env:LOCALAPPDATA 'LibreSpot'); Label = 'Log/crash directory' }
                @{ Path = (Join-Path $env:ProgramData 'LibreSpot'); Label = 'Machine data and fleet logs' }
                @{ Path = $global:CONFIG_DIR; Label = 'Config directory'; RemovesActiveProfile = $true }
            )
            $receiptTargets = @()
            $step = 20
            foreach ($entry in $selfPaths) {
                Update-BackendState -Progress $step -Status "Removing $($entry.Label)" -Step $entry.Label
                if (Test-Path -LiteralPath $entry.Path) {
                    if ($entry.RemovesActiveProfile) {
                        if (-not (Test-SafeRemovalTarget -Path $entry.Path)) {
                            Write-OperationJournalEntry -Phase 'remove' -Target $entry.Path -SafetyDecision 'RefusedUnsafeTarget' -Result 'Refused' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target failed LibreSpot safe-removal checks.' -Data @{ label = $entry.Label }
                            Write-Log "Refusing to remove unsafe target: $($entry.Path)" -Level 'WARN'
                            $receiptTargets += [pscustomobject]@{ label = $entry.Label; result = 'RefusedUnsafeTarget' }
                        } else {
                            Write-OperationJournalEntry -Phase 'remove' -Target $entry.Path -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'This removes LibreSpot profile data by user request.' -Data @{ label = $entry.Label }
                            Remove-Item -LiteralPath $entry.Path -Recurse -Force -ErrorAction Stop
                            Write-EventLine -Kind 'log' -Level 'INFO' -Payload "Removed: $($entry.Label) ($($entry.Path))"
                            $receiptTargets += [pscustomobject]@{ label = $entry.Label; result = 'Removed' }
                        }
                    } else {
                        $removed = Remove-PathSafely -Path $entry.Path -Label $entry.Label
                        Write-Log "Removed: $($entry.Label) ($($entry.Path))"
                        $receiptTargets += [pscustomobject]@{ label = $entry.Label; result = if ($removed) { 'Removed' } else { 'Skipped' } }
                    }
                } else {
                    $receiptTargets += [pscustomobject]@{ label = $entry.Label; result = 'NotFound' }
                    if ($entry.RemovesActiveProfile) {
                        Write-EventLine -Kind 'log' -Level 'INFO' -Payload "Not found: $($entry.Label) ($($entry.Path))"
                    } else {
                        Write-Log "Not found: $($entry.Label) ($($entry.Path))"
                    }
                }
                $step += 25
            }
            Write-RemoveSelfDataReceipt -Targets $receiptTargets
            Write-EventLine -Kind 'log' -Level 'SUCCESS' -Payload 'LibreSpot self-cleanup complete. Spotify and Spicetify were not affected.'
        }
        'ClearCache' {
            Update-BackendState -Progress 35 -Status 'Clearing asset cache' -Step 'Removing cached downloads'
            Clear-LibreSpotCache
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
        default {
            throw "Unhandled maintenance action: $Action"
        }
    }

    Update-BackendState -Progress 100 -Status 'Maintenance complete' -Step 'LibreSpot is ready'
    if ($Action -ne 'RemoveSelfData') {
        Write-Log "--- Maintenance action '$Action' completed successfully ---" -Level 'SUCCESS'
    }
}

try {
    Ensure-LogDirectory
    if ($Action -eq 'WatchAutoReapply') {
        $scriptDir = Split-Path -Path $PSCommandPath -Parent
        $sidecarPath = Join-Path $scriptDir 'LibreSpot.Backend.ps1.sha256'
        if (Test-Path -LiteralPath $sidecarPath -PathType Leaf) {
            $expectedHash = ([System.IO.File]::ReadAllText($sidecarPath)).Trim()
            if ($expectedHash.Length -ge 64) {
                $selfBytes = [System.IO.File]::ReadAllBytes($PSCommandPath)
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try {
                    $actualHash = [System.BitConverter]::ToString($sha.ComputeHash($selfBytes)).Replace('-', '')
                } finally { $sha.Dispose() }
                if ($actualHash -ne $expectedHash) {
                    Write-WatcherLog "Integrity check failed: expected $expectedHash, got $actualHash" -Level 'ERROR'
                    exit 1
                }
            }
        }
        $code = Invoke-AutoReapplyWatcher
        exit $code
    }

    $journalWouldChange = ($Action -notin @('CheckUpdates', 'OpenMarketplace', 'WatchAutoReapply', 'Plan'))
    Start-OperationJournalRun -Action $Action -Target "Backend action: $Action" -WouldChange $journalWouldChange -Reversible $false -RollbackHint 'Review individual journal entries for action-specific rollback hints.' -OperationId $OperationId | Out-Null
    Write-EventLine -Kind 'action' -Payload $Action
    # Admin is not required: SpotX patches per-user %APPDATA%\Spotify and
    # Spicetify runs --bypass-admin. Operations that genuinely need elevation
    # (provisioned AppX removal, scheduled tasks, firewall rules) handle
    # permission failures in their own try/catch blocks.

    # Gate patching actions behind risk acknowledgment. The WPF shell handles
    # the dialog; the backend enforces the invariant as a safety net. Plan is
    # read-only (it mutates nothing), and it runs before the shell shows the
    # combined plan+risk prompt, so it must not require prior acknowledgment.
    if ($Action -notin @('CheckUpdates', 'RemoveSelfData', 'ClearCache', 'EnableAutoReapply', 'DisableAutoReapply', 'WatchAutoReapply', 'Plan')) {
        $riskConfig = Load-LibreSpotConfig
        if (-not (ConvertTo-ConfigBoolean -Value $riskConfig['RiskAcknowledged'] -Default $false)) {
            Write-Log 'RiskAcknowledged is false. The desktop shell must present the acknowledgment dialog before running this action.' -Level 'ERROR'
            Write-EventLine -Kind 'result' -Level 'ERROR' -Payload 'Risk acknowledgment required before this action can proceed.'
            Complete-OperationJournalRun -Result 'Refused' -Message 'Risk acknowledgment required before this action can proceed.'
            exit 1
        }
    }

    if ($Action -eq 'Plan') {
        Invoke-LibreSpotPlan
    } elseif ($Action -eq 'Install') {
        Invoke-LibreSpotInstall
    } else {
        Invoke-LibreSpotMaintenance
    }
    if ($Action -ne 'RemoveSelfData') {
        Complete-OperationJournalRun -Result 'Succeeded' -Message "Backend action $Action completed."
    }
    Write-EventLine -Kind 'result' -Level 'SUCCESS' -Payload 'LibreSpot backend completed successfully.'
    exit 0
} catch {
    $message = $_.Exception.Message
    if ($Action -ne 'RemoveSelfData') {
        try { Complete-OperationJournalRun -Result 'Failed' -Message $message } catch {}
        Write-Log $message -Level 'ERROR'
    }
    Write-EventLine -Kind 'result' -Level 'ERROR' -Payload $message
    exit 1
}
