param(
    [ValidateSet('Install', 'CheckUpdates', 'Reapply', 'RepairMarketplace', 'OpenMarketplace', 'SafeMode', 'CreateBackup', 'RestoreBackup', 'RestoreVanilla', 'UninstallSpicetify', 'FullReset', 'RemoveSelfData', 'EnableAutoReapply', 'DisableAutoReapply', 'WatchAutoReapply')]
    [string]$Action = 'Install',
    [string]$ConfigPath = "$env:APPDATA\LibreSpot\config.json"
)

$ErrorActionPreference = 'Stop'

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch {}

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
$global:VERSION = '3.7.2'
$global:CONFIG_SCHEMA_VERSION = 1
$global:PinnedReleases = @{
    SpotX = @{
        Version = '2.0'
        Commit  = '3284673df69e276c5c0ee90bb1cc9185cecb9ad4'
        Url     = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/3284673df69e276c5c0ee90bb1cc9185cecb9ad4/run.ps1'
        SHA256  = '18684432f8b9ec1c6d7d2481192afc0bcad670aa769a306480948a3e690cc823'
    }
    SpicetifyCLI = @{
        Version = '2.43.2'
        WindowsMinSpotify = '1.2.14'
        WindowsMaxTestedSpotify = '1.2.88'
        CompatibilityUrl = 'https://github.com/spicetify/cli/releases/tag/v2.43.2'
        SHA256  = @{
            x64   = 'fc6ed7b67f15a8e49e6f676ca0511b63ef74736c05593966abf20a90e06aa80d'
            arm64 = 'ed90e11d82affdcf7ae2968a886c8b9500c08f521c271598f13d6d9414110473'
        }
    }
    Marketplace = @{
        Version = '1.0.8'
        Url     = 'https://github.com/spicetify/marketplace/releases/download/v1.0.8/marketplace.zip'
        SHA256  = 'ba20cd30896605ec60c272905004673b995162d2c8ca085351971e409cf80ec7'
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
$resolvedConfigDirectory = $null
try { $resolvedConfigDirectory = Split-Path -Path $ConfigPath -Parent } catch {}
if ([string]::IsNullOrWhiteSpace($resolvedConfigDirectory)) {
    $resolvedConfigDirectory = "$env:APPDATA\LibreSpot"
}
$global:CONFIG_DIR           = $resolvedConfigDirectory
$global:CONFIG_PATH          = $ConfigPath
$global:LOG_PATH             = Join-Path $global:CONFIG_DIR 'install.log'
$global:OPERATION_JOURNAL_PATH = Join-Path $global:CONFIG_DIR 'operation-journal.jsonl'
$global:OPERATION_JOURNAL_MAX_BYTES = 1048576
$global:OPERATION_JOURNAL_RETAIN_BYTES = 786432
$global:CURRENT_OPERATION_ID = $null
$global:CURRENT_OPERATION_ACTION = $null
$global:CACHE_DIR            = Join-Path $global:CONFIG_DIR 'cache'
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

$global:CommunityThemeRepos = @{
    'Catppuccin' = @{ Owner = 'catppuccin'; Repo = 'spicetify';       CommitSha = '1ec645c4cf7f42f9792b9eeb1bb7930f94593277'; SHA256 = '59432d5dfba871f288331e72ca5eb9ae48783e94d96cc3835a2992b3df71ed65'; ThemeFolder = '.' }
    'Comfy'      = @{ Owner = 'Comfy-Themes'; Repo = 'Spicetify';    CommitSha = '32ff101e27cfd33d85b7cc587f7f95db6b2df8b0'; SHA256 = 'd82afe89be0a58c7c2d83a85a0dfa24b473d48d4f63241178e37c94c1fd1e7c6'; ThemeFolder = '.' }
    'Bloom'      = @{ Owner = 'nimsandu'; Repo = 'spicetify-bloom';   CommitSha = '654cfed682b94613b0029997ffafc1eadccc5bef'; SHA256 = '12cb8678f7226b2a014a10fdef8ea462e0ac0a866f84b2de48050004fcd50a70'; ThemeFolder = '.' }
    'Lucid'      = @{ Owner = 'sanoojes'; Repo = 'Spicetify-Lucid';   CommitSha = '5c28e9f955d5ca84a82d06084cc6652e5655ea2d'; SHA256 = 'af3f1ed718b3deda7c52ebf7e0ca4bf7c07f03f212a88dd0534c2ebe81803bf8'; ThemeFolder = '.' }
    'Hazy'       = @{ Owner = 'Astromations'; Repo = 'Hazy';          CommitSha = '1926d9db3e0313b68ca6e2193c2b278e733ac3c4'; SHA256 = '372938c3fea3cbac7850afeb6b66b15673236e248436a7afaacb2ab1d814c4bf'; ThemeFolder = '.' }
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
    SpotX_Language = ''
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
        [hashtable]$Data = $null
    )
    try {
        if ([string]::IsNullOrWhiteSpace($OperationId)) { $OperationId = [Guid]::NewGuid().ToString('N') }
        if ([string]::IsNullOrWhiteSpace($Action)) { $Action = 'Unknown' }
        Ensure-LogDirectory
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
        [string]$RollbackHint = ''
    )
    $global:CURRENT_OPERATION_ID = [Guid]::NewGuid().ToString('N')
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

function Get-LibreSpotConfigSchemaVersion {
    param([hashtable]$Config)
    if (-not $Config -or -not $Config.ContainsKey('ConfigSchemaVersion')) { return 0 }
    return (ConvertTo-ConfigInt -Value $Config.ConfigSchemaVersion -Default 0 -Minimum 0 -Maximum [int]::MaxValue)
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

    $booleanKeys = @(
        'CleanInstall', 'LaunchAfter',
        'SpotX_NewTheme', 'SpotX_PodcastsOff', 'SpotX_BlockUpdate', 'SpotX_AdSectionsOff',
        'SpotX_Premium', 'SpotX_LyricsEnabled', 'SpotX_TopSearch', 'SpotX_RightSidebarOff',
        'SpotX_RightSidebarClr', 'SpotX_CanvasHomeOff', 'SpotX_HomeSubOff', 'SpotX_DisableStartup',
        'SpotX_NoShortcut', 'SpotX_OldLyrics', 'SpotX_HideColIconOff', 'SpotX_Plus',
        'SpotX_NewFullscreen', 'SpotX_FunnyProgress', 'SpotX_ExpSpotify', 'SpotX_LyricsBlock',
        'SpotX_SendVersionOff', 'SpotX_StartSpoti', 'SpotX_DevTools', 'SpotX_Mirror', 'SpotX_ConfirmUninstall',
        'Spicetify_Marketplace', 'AutoReapply_Enabled', 'RiskAcknowledged'
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
        if ($global:CommunityExtensionAliases.ContainsKey($name)) { $name = [string]$global:CommunityExtensionAliases[$name] }
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
                Remove-Item -LiteralPath $global:WATCHER_STATE_PATH -Force -ErrorAction Stop
                [System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)
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
            Save-ToAssetCache -SourcePath $destination -SHA256Hash $spotxHash
        }
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
    if ($PSVersionTable.PSEdition -and $PSVersionTable.PSEdition -ne 'Desktop') {
        $result.Reason = 'PowerShell 7+ (Core) is in use; CVE-2025-54100 affects Windows PowerShell 5.1 only.'
        return [pscustomobject]$result
    }

    try {
        $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop
        if ($cv.CurrentBuild) { $result.OSBuild = "$($cv.CurrentBuild).$($cv.UBR)" }
    } catch {}

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
    return 'NetworkFailure'
}

function Get-NetworkPreflightStatus {
    param(
        [string]$Uri = 'https://raw.githubusercontent.com',
        [string]$Purpose = 'download sources',
        [int]$TimeoutMilliseconds = 5000
    )
    $response = $null
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
        $response = $request.GetResponse()
        $statusCode = $null
        try { $statusCode = [int]$response.StatusCode } catch {}
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
    finally { if ($response) { try { $response.Close() } catch {} } }
    return [pscustomobject]$result
}

function Download-FileSafe {
    param(
        [string]$Uri,
        [string]$OutFile
    )
    Write-DownloaderCveWarningIfNeeded
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
            $webHint = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'Web request'
            Write-Log "$webHint Trying BITS fallback." -Level 'WARN'
            try {
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
                    $bitsDetail = "BITS state: $jobState"
                    try { if ($bitsJob.ErrorDescription) { $bitsDetail = "$bitsDetail - $($bitsJob.ErrorDescription)" } } catch {}
                    Remove-BitsTransfer $bitsJob -ErrorAction SilentlyContinue
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

function Save-ToAssetCache {
    param(
        [string]$SourcePath,
        [string]$SHA256Hash
    )
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return }
    try {
        if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
            New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
        }
        $cachePath = Join-Path $global:CACHE_DIR $hash
        Copy-Item -LiteralPath $SourcePath -Destination $cachePath -Force
        Write-Log "Cached verified asset (SHA256: $hash)"
    } catch {
        Write-Log "Asset cache save failed: $($_.Exception.Message)" -Level 'WARN'
    }
}

function Get-FromAssetCache {
    param(
        [string]$SHA256Hash,
        [string]$DestinationPath,
        [string]$Label
    )
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return $false }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return $false }
    $cachePath = Join-Path $global:CACHE_DIR $hash
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        Write-Log "Cache miss for $Label (SHA256: $hash)"
        return $false
    }
    try {
        $actual = (Get-FileHash -LiteralPath $cachePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $hash) {
            Write-Log "Cached asset for $Label failed re-verification (expected $hash, got $actual). Removing stale entry." -Level 'WARN'
            Remove-Item -LiteralPath $cachePath -Force -ErrorAction SilentlyContinue
            return $false
        }
        $outDir = Split-Path -Path $DestinationPath -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -LiteralPath $cachePath -Destination $DestinationPath -Force
        Write-Log "Using verified cached copy for $Label (SHA256: $hash)"
        return $true
    } catch {
        Write-Log "Cache retrieval failed for ${Label}: $($_.Exception.Message)" -Level 'WARN'
        return $false
    }
}

function Clear-LibreSpotCache {
    if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
        Write-Log 'Asset cache directory does not exist. Nothing to clear.'
        return
    }
    try {
        Remove-Item -LiteralPath $global:CACHE_DIR -Recurse -Force -ErrorAction Stop
        Write-Log 'Asset cache cleared.'
    } catch {
        Write-Log "Failed to clear asset cache: $($_.Exception.Message)" -Level 'WARN'
    }
}

function Expand-ArchiveSafely {
    param(
        [string]$ZipPath,
        [string]$DestinationPath,
        [string]$Label = 'archive',
        [int]$MaxEntries = 10000,
        [long]$MaxExpandedBytes = 500MB
    )
    Add-Type -AssemblyName System.IO.Compression
    $zip = $null
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
        if ($zip.Entries.Count -gt $MaxEntries) {
            throw "Archive '$Label' contains $($zip.Entries.Count) entries (limit $MaxEntries)."
        }
        $totalDeclaredBytes = 0L
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $normalized = $name.Replace('/', '\')
            if ([System.IO.Path]::IsPathRooted($normalized)) {
                throw "Archive '$Label' contains an absolute path entry: $name"
            }
            if ($normalized.Contains('..\') -or $normalized.StartsWith('..') -or $normalized.EndsWith('..')) {
                throw "Archive '$Label' contains a path traversal entry: $name"
            }
            $fullTarget = [System.IO.Path]::GetFullPath((Join-Path $DestinationPath $normalized))
            $fullDest = [System.IO.Path]::GetFullPath($DestinationPath).TrimEnd('\') + '\'
            if (-not $fullTarget.StartsWith($fullDest, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Archive '$Label' entry escapes destination: $name"
            }
            $totalDeclaredBytes += $entry.Length
            if ($totalDeclaredBytes -gt $MaxExpandedBytes) {
                throw "Archive '$Label' declared expanded size exceeds limit ($([math]::Round($MaxExpandedBytes / 1MB))MB)."
            }
        }
    } finally {
        if ($zip) { $zip.Dispose() }
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $DestinationPath)
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
            Write-Log "This host enforces ConstrainedLanguage mode (AppLocker or Windows Defender Application Control). LibreSpot's scripts may be blocked -- this is an enterprise control, not a LibreSpot error, and -ExecutionPolicy Bypass does not bypass it. Ask your administrator to allow LibreSpot/SpotX; do not disable application control to work around it." -Level 'WARN'
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

function Invoke-ExternalScriptIsolated {
    param(
        [string]$FilePath,
        [string]$Arguments,
        [int]$TimeoutSeconds = 600
    )
    Write-Log "Spawning: $FilePath"
    Write-PowerShellSecurityContext
    $stdoutPath = Join-Path $global:TEMP_DIR ("LibreSpot-stdout-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stderrPath = Join-Path $global:TEMP_DIR ("LibreSpot-stderr-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stdoutState = @{ Offset = 0L; Remainder = '' }
    $stderrState = @{ Offset = 0L; Remainder = '' }
    # The spawned powershell.exe can be forced into ConstrainedLanguage by WDAC /
    # AppLocker even when this host is FullLanguage; classify that from stderr.
    $appControlHintShown = $false
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
            foreach ($line in $stderrRead.Lines) {
                Write-Log "[STDERR] $line" -Level 'WARN'
                if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                    $appControlHintShown = $true
                    Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker or Windows Defender Application Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these enterprise controls -- ask your administrator to allow LibreSpot/SpotX, or use a host without WDAC/AppLocker enforcement." -Level 'WARN'
                }
            }
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
            if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                $appControlHintShown = $true
                Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker or Windows Defender Application Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these enterprise controls -- ask your administrator to allow LibreSpot/SpotX, or use a host without WDAC/AppLocker enforcement." -Level 'WARN'
            }
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

function Compare-LibreSpotVersions {
    param([string]$Latest, [string]$Current)
    if ([string]::IsNullOrWhiteSpace($Latest)) { return $false }
    if ([string]::IsNullOrWhiteSpace($Current)) { return $true }
    $stripLatest  = ($Latest  -replace '-preview.*','' -replace '-rc.*','')
    $stripCurrent = ($Current -replace '-preview.*','' -replace '-rc.*','')
    try {
        $latestVersion = [Version]$stripLatest
        $currentVersion = [Version]$stripCurrent
        if ($latestVersion -gt $currentVersion) { return $true }
        if ($latestVersion -lt $currentVersion) { return $false }
        $latestIsStable = ($Latest -eq $stripLatest)
        $currentIsStable = ($Current -eq $stripCurrent)
        if ($latestIsStable -and -not $currentIsStable) { return $true }
        if (-not $latestIsStable -and $currentIsStable) { return $false }
        if ($Latest -eq $Current) { return $false }
        return ([string]::CompareOrdinal($Latest, $Current) -gt 0)
    } catch {
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

    Write-Log 'Compatibility matrix:'
    Write-Log "  SpotX: commit $($global:PinnedReleases.SpotX.Commit.Substring(0,10)) targets Spotify $spotxLabel"
    Write-Log "  Spicetify CLI: v$($spicetify.Version) max-tested Windows/Microsoft Store Spotify $($spicetify.WindowsMinSpotify) -> $($spicetify.WindowsMaxTestedSpotify)"
    Write-Log "  Marketplace: v$($global:PinnedReleases.Marketplace.Version) checked as a custom app package independent of Spotify CSS-map coverage"
    Write-Log "  Themes: commit $($global:PinnedReleases.Themes.Commit.Substring(0,10)) checked as a theme archive independent of Spotify CSS-map coverage"

    $warnings = @(Get-LibreSpotCompatibilityWarnings)
    foreach ($warning in $warnings) {
        Write-Log "  Compatibility warning: $warning" -Level 'WARN'
    }
    return $warnings
}

function Invoke-GitHubApiSafe {
    param(
        [string]$Uri,
        [hashtable]$Headers,
        [int]$TimeoutSec = 15,
        [string]$Label = 'GitHub API'
    )
    try {
        $response = Invoke-WebRequest -Uri $Uri -Headers $Headers -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
        $remaining = $response.Headers['x-ratelimit-remaining']
        if ($remaining -and [int]$remaining -le 5) {
            $resetEpoch = $response.Headers['x-ratelimit-reset']
            $resetTime = if ($resetEpoch) {
                ([DateTimeOffset]::FromUnixTimeSeconds([long]$resetEpoch)).LocalDateTime.ToString('HH:mm:ss')
            } else { 'unknown' }
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
    Write-Log 'Checking pinned dependencies against upstream releases...' -Level 'STEP'
    $headers = @{ 'User-Agent' = "LibreSpot/$global:VERSION" }
    $updates = @()
    $compatWarnings = @()

    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/SpotX-Official/SpotX/commits/main' -Headers $headers -Label 'SpotX'
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
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/cli/releases/latest' -Headers $headers -Label 'Spicetify CLI'
        $latest = $rel.tag_name -replace '^v', ''
        if (Compare-LibreSpotVersions -Latest $latest -Current $global:PinnedReleases.SpicetifyCLI.Version) {
            $updates += 'Spicetify CLI'
            Write-Log "Spicetify CLI update available: $($global:PinnedReleases.SpicetifyCLI.Version) -> $latest" -Level 'WARN'
        } else {
            Write-Log 'Spicetify CLI is up to date.'
        }
    } catch {
        Write-Log "Spicetify CLI update check failed: $($_.Exception.Message)" -Level 'WARN'
    }

    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/marketplace/releases/latest' -Headers $headers -Label 'Marketplace'
        $latest = $rel.tag_name -replace '^v', ''
        if (Compare-LibreSpotVersions -Latest $latest -Current $global:PinnedReleases.Marketplace.Version) {
            $updates += 'Marketplace'
            Write-Log "Marketplace update available: $($global:PinnedReleases.Marketplace.Version) -> $latest" -Level 'WARN'
        } else {
            Write-Log 'Marketplace is up to date.'
        }
    } catch {
        Write-Log "Marketplace update check failed: $($_.Exception.Message)" -Level 'WARN'
    }

    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/spicetify-themes/commits/master' -Headers $headers -Label 'Themes'
        if ($rel.sha -ne $global:PinnedReleases.Themes.Commit) {
            $updates += 'Themes'
            Write-Log "Theme archive has a newer commit available: $($rel.sha.Substring(0, 10))" -Level 'WARN'
        } else {
            Write-Log 'Pinned theme archive is up to date.'
        }
    } catch {
        Write-Log "Themes update check failed: $($_.Exception.Message)" -Level 'WARN'
    }

    $compatWarnings = @(Write-LibreSpotCompatibilityMatrix)

    if ($updates.Count -eq 0 -and $compatWarnings.Count -eq 0) {
        Write-Log 'All pinned dependencies and compatibility baselines are current.' -Level 'SUCCESS'
    } else {
        if ($updates.Count -eq 0) {
            Write-Log 'All pinned dependency versions are current.' -Level 'SUCCESS'
        }
        if ($updates.Count -gt 0) {
            Write-Log "$($updates.Count) dependency update(s) are available." -Level 'WARN'
        }
        if ($compatWarnings.Count -gt 0) {
            Write-Log "$($compatWarnings.Count) compatibility warning(s) detected; review the matrix above before repatching newer Spotify builds." -Level 'WARN'
        }
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
    $displayLabel = if ($Label) { $Label } else { $Path }
    $journalData = @{ label = $displayLabel }
    if ([string]::IsNullOrWhiteSpace($Path)) { return 0 }
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'SkippedMissingTarget' -Result 'Skipped' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target did not exist.' -Data $journalData
        return 0
    }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'RefusedUnsafeTarget' -Result 'Refused' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target failed LibreSpot safe-removal checks.' -Data $journalData
        Write-Log "Refusing to remove unsafe target: $Path" -Level 'WARN'
        return 0
    }
    Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
    try {
        $null = & icacls.exe "$Path" /reset /T /C /Q 2>$null
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
        Write-Log "Removed: $displayLabel"
        return 1
    } catch {
        $journalData['error'] = [string]$_.Exception.Message
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'The target may be partially unchanged; review the error before retrying.' -Data $journalData
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

function Get-MarketplaceHealth {
    $configDir = Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps\marketplace'
    $legacyDir = Join-Path $global:SPICETIFY_DIR 'CustomApps\marketplace'
    $activeDir = if (Test-Path -LiteralPath $configDir -PathType Container) { $configDir } elseif (Test-Path -LiteralPath $legacyDir -PathType Container) { $legacyDir } else { $configDir }
    $hasConfigDir = Test-Path -LiteralPath $configDir -PathType Container
    $hasLegacyDir = Test-Path -LiteralPath $legacyDir -PathType Container
    $hasExtension = Test-Path -LiteralPath (Join-Path $activeDir 'extension.js') -PathType Leaf
    $hasManifest = Test-Path -LiteralPath (Join-Path $activeDir 'manifest.json') -PathType Leaf
    $isEnabled = @(Get-SpicetifyConfigListValue -Key 'custom_apps') -contains 'marketplace'
    $hasFiles = $hasExtension -and $hasManifest

    $status = if ($hasConfigDir -and $hasFiles -and $isEnabled) {
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
        Status       = $status
        Path         = $activeDir
        HasConfigDir = $hasConfigDir
        HasLegacyDir = $hasLegacyDir
        HasFiles     = $hasFiles
        IsEnabled    = $isEnabled
        IsReady      = ($status -eq 'Ready')
        NeedsRepair  = ($status -in @('Hidden','FilesMissing','LegacyPath','Missing'))
    }
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
    $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
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
        $startInfo.WorkingDirectory = Split-Path -Path $spicetifyExe -Parent
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        $collector = New-Object LibreSpotNativeOutputCollector
        $collector.Attach($process)

        $null = $process.Start()
        Write-Log "  Spicetify command: spicetify $displayArguments"
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
                try { $process.Kill() } catch {}
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

# SECURITY: see SECURITY.md "External process execution contract". $Config MUST
# be a Normalize-LibreSpotConfig output: the only interpolated values here are
# SpotX_LyricsTheme (allowlist), SpotX_DownloadMethod (allowlist),
# SpotX_Language (allowlist), SpotX_CacheLimit (integer), and a manifest-supplied
# version. Do NOT interpolate any new free-form/user value into this string
# without normalizing it first.
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
    if (-not [string]::IsNullOrWhiteSpace([string]$Config.SpotX_Language)) {
        $params += "-language $($Config.SpotX_Language)"
    }
    return ($params -join ' ')
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

    $spotifyDir = Split-Path -LiteralPath $SpotifyExePath -Parent
    $appsDir    = Join-Path $spotifyDir 'Apps'
    $signals    = New-Object System.Collections.Generic.List[string]

    $hasBackup = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa.bak')
    $hasBundle = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa')

    if ($hasBackup) { $signals.Add('xpui.spa.bak (SpotX backed up the original bundle before patching)') }
    if ($hasBundle) { $signals.Add('xpui.spa (Spotify app bundle present)') }
    $result.Signals = @($signals)

    if ($hasBackup -and $hasBundle) {
        $result.Verified = $true
        $result.Status   = 'Verified'
        $result.Reason   = 'SpotX left a patched xpui.spa and a backup of the original, so the patch was applied.'
    }
    elseif ($hasBundle) {
        $result.Status = 'Unverified'
        $result.Reason = 'Spotify is present but no SpotX backup (xpui.spa.bak) was found, so the patch may not have been applied. Signature protection on newer Spotify builds can let SpotX exit cleanly without patching.'
    }
    else {
        $result.Status = 'Unverified'
        $result.Reason = 'The Spotify app bundle (Apps\xpui.spa) is missing, so SpotX patching could not be confirmed.'
    }

    return [pscustomobject]$result
}

function Module-InstallSpotX {
    param($Config)
    Write-Log "Installing SpotX v$($global:PinnedReleases.SpotX.Version)..." -Level 'STEP'
    $destination = New-LibreSpotTempFile -Name 'spotx_run.ps1'
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
            Save-ToAssetCache -SourcePath $destination -SHA256Hash $spotxHash
        }

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

        $verify = Get-SpotXPatchVerification -SpotifyExePath $global:SPOTIFY_EXE_PATH
        if ($verify.Verified) {
            Write-Log "SpotX patching completed and verified ($($verify.Signals -join '; '))." -Level 'SUCCESS'
        } else {
            Write-Log "SpotX ran but the patch could not be verified. $($verify.Reason)" -Level 'WARN'
            Write-Log 'If ads still play or the UI is blank, this Spotify build may resist SpotX patching (SpotX issue #760). Try Reapply, or Full Reset to start clean. As a fallback, enable the Spicetify ad-block extension to keep ad-blocking working at the Spicetify layer.' -Level 'WARN'
        }
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
        $spicetifyHash = $global:PinnedReleases.SpicetifyCLI.SHA256[$arch]
        if (-not (Get-FromAssetCache -SHA256Hash $spicetifyHash -DestinationPath $zipPath -Label "Spicetify CLI ($arch)")) {
            try {
                Download-FileSafe -Uri $zipUri -OutFile $zipPath
            } catch {
                if (Get-FromAssetCache -SHA256Hash $spicetifyHash -DestinationPath $zipPath -Label "Spicetify CLI ($arch)") {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $zipPath -ExpectedHash $spicetifyHash -Label "Spicetify CLI ($arch)"
            Save-ToAssetCache -SourcePath $zipPath -SHA256Hash $spicetifyHash
        }

        if (Test-Path -LiteralPath $global:SPICETIFY_DIR) {
            Get-ChildItem -LiteralPath $global:SPICETIFY_DIR -Force -ErrorAction SilentlyContinue | ForEach-Object {
                $null = Remove-PathSafely -Path $_.FullName -Label "Spicetify CLI: $($_.Name)"
            }
        }

        Expand-ArchiveSafely -ZipPath $zipPath -DestinationPath $global:SPICETIFY_DIR -Label "Spicetify CLI ($arch)"

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
        $archiveUrl = "https://github.com/$($repo.Owner)/$($repo.Repo)/archive/$($repo.CommitSha).zip"
        $safeName = ($themeName -replace '[^a-zA-Z0-9_-]','_')
        $zipPath = New-LibreSpotTempFile -Name "community-theme-$safeName.zip"
        $unpackPath = New-LibreSpotTempDirectory -Name "community-theme-$safeName-unpack"
        try {
            Write-Log "Downloading community theme from $($repo.Owner)/$($repo.Repo) @ $($repo.CommitSha.Substring(0,10))..."
            $themeHash = $repo.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themeHash -DestinationPath $zipPath -Label "Community theme '$themeName'")) {
                try {
                    Download-FileSafe -Uri $archiveUrl -OutFile $zipPath
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themeHash -DestinationPath $zipPath -Label "Community theme '$themeName'") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $zipPath -ExpectedHash $themeHash -Label "Community theme '$themeName'"
                Save-ToAssetCache -SourcePath $zipPath -SHA256Hash $themeHash
            }
            Expand-ArchiveSafely -ZipPath $zipPath -DestinationPath $unpackPath -Label "Community theme '$themeName'"
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
            $themesHash = $global:PinnedReleases.Themes.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $zipPath -Label 'Themes archive')) {
                try {
                    Download-FileSafe -Uri $global:URL_THEMES_REPO -OutFile $zipPath
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $zipPath -Label 'Themes archive') {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $zipPath -ExpectedHash $themesHash -Label 'Themes archive'
                Save-ToAssetCache -SourcePath $zipPath -SHA256Hash $themesHash
            }
            Expand-ArchiveSafely -ZipPath $zipPath -DestinationPath $unpackPath -Label 'Themes archive'
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

# Guidance shown when a file LibreSpot verified moments ago has vanished, or a
# known extension / custom-app file is missing. The usual cause is a security
# product (Microsoft Defender or another endpoint suite) quarantining it.
# LibreSpot only DETECTS and GUIDES -- it never disables AV, adds exclusions, or
# auto-restores quarantined files. Pure and side-effect free for unit testing.
function Get-QuarantineGuidance {
    param([string]$What = 'A verified file')
    return "$What is missing right after LibreSpot verified it. A security product (for example Microsoft Defender) may have quarantined it. Open Windows Security > Virus & threat protection > Protection history; if the file is listed, restore it and re-run LibreSpot. LibreSpot will not disable your antivirus, add exclusions, or restore quarantined files for you."
}

function Download-CommunityExtensions {
    param($Config)
    $exts = @($Config.Spicetify_Extensions)
    $extDir = Join-Path $global:SPICETIFY_CONFIG_DIR 'Extensions'
    if (-not (Test-Path -LiteralPath $extDir)) { New-Item -Path $extDir -ItemType Directory -Force | Out-Null }
    $verifiedPaths = @()
    foreach ($ext in $exts) {
        if (-not $global:CommunityExtensions.ContainsKey($ext)) { continue }
        $info = $global:CommunityExtensions[$ext]
        $destFile = Join-Path $extDir $ext
        try {
            Write-Log "Downloading community extension: $ext from $($info.Source)..."
            $extHash = $info.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $destFile -Label "Community extension $ext")) {
                try {
                    Download-FileSafe -Uri $info.Url -OutFile $destFile
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $destFile -Label "Community extension $ext") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                $head = Get-Content -LiteralPath $destFile -TotalCount 5 -ErrorAction SilentlyContinue
                $headStr = ($head -join "`n").TrimStart()
                if ($headStr -match '^<(!DOCTYPE|html)' -or $headStr -match '^404:') {
                    Remove-Item -LiteralPath $destFile -Force -ErrorAction SilentlyContinue
                    Write-Log "Community extension '$ext' appears to be an HTML error page. Skipping." -Level 'WARN'
                    continue
                }
                Confirm-FileHash -Path $destFile -ExpectedHash $extHash -Label "Community extension $ext"
                Save-ToAssetCache -SourcePath $destFile -SHA256Hash $extHash
            }
            Write-Log "Community extension '$ext' saved to $destFile"
            $verifiedPaths += $destFile
        } catch {
            Write-Log "Could not download community extension '$ext': $($_.Exception.Message). Skipping." -Level 'WARN'
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
    New-Item -Path $customAppsDir -ItemType Directory -Force | Out-Null

    $marketplaceDir = Join-Path $customAppsDir 'marketplace'
    $zipPath = New-LibreSpotTempFile -Name 'marketplace.zip'
    $unpackPath = New-LibreSpotTempDirectory -Name 'marketplace-unpack'

    foreach ($dir in $marketplaceDirs) {
        $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
    }
    New-Item -Path $marketplaceDir -ItemType Directory -Force | Out-Null

    try {
        $marketplaceHash = $global:PinnedReleases.Marketplace.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $marketplaceHash -DestinationPath $zipPath -Label 'Marketplace archive')) {
            try {
                Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $zipPath
            } catch {
                if (Get-FromAssetCache -SHA256Hash $marketplaceHash -DestinationPath $zipPath -Label 'Marketplace archive') {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $zipPath -ExpectedHash $marketplaceHash -Label 'Marketplace archive'
            Save-ToAssetCache -SourcePath $zipPath -SHA256Hash $marketplaceHash
        }

        Expand-ArchiveSafely -ZipPath $zipPath -DestinationPath $unpackPath -Label 'Marketplace'
        $source = if (Test-Path -LiteralPath (Join-Path $unpackPath 'marketplace-dist')) { Join-Path $unpackPath 'marketplace-dist\*' } else { Join-Path $unpackPath '*' }
        Copy-Item -Path $source -Destination $marketplaceDir -Recurse -Force

        $health = Get-MarketplaceHealth
        if (-not $health.HasFiles) {
            throw 'Marketplace archive did not produce expected Spicetify custom app files.'
        }

        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @('marketplace') -ManagedItems $managedApps
        $health = Get-MarketplaceHealth
        if ($health.IsReady) {
            Write-Log 'Marketplace enabled successfully. If Spotify hides the sidebar icon, open spotify:app:marketplace directly.' -Level 'SUCCESS'
        } else {
            Write-Log "Marketplace files were installed but status is '$($health.Status)'. Use Maintenance > Repair and open Marketplace if the sidebar icon is hidden." -Level 'WARN'
        }
    } finally {
        Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $unpackPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Open-SpicetifyMarketplace {
    try {
        Start-Process -FilePath 'explorer.exe' -ArgumentList 'spotify:app:marketplace'
        Write-Log 'Requested Spotify Marketplace via spotify:app:marketplace.'
    } catch {
        Write-Log "Could not open spotify:app:marketplace automatically: $($_.Exception.Message)" -Level 'WARN'
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

    Write-Log 'Repairing Marketplace files and custom_apps registration...' -Level 'STEP'
    Module-InstallMarketplace -Config $Config
    Write-Log 'Applying Spicetify so Marketplace is discoverable in Spotify...' -Level 'STEP'
    Module-ApplySpicetify -Config $Config

    $health = Get-MarketplaceHealth
    if ($health.IsReady) {
        Write-Log "Marketplace repair verified at $($health.Path)." -Level 'SUCCESS'
    } else {
        Write-Log "Marketplace repair finished, but status is '$($health.Status)'. Open spotify:app:marketplace directly if the sidebar icon remains hidden." -Level 'WARN'
    }
    Open-SpicetifyMarketplace
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
        Update-ApplyState -Outcome 'SpicetifyApplySucceeded' -Successful $true
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
        Update-ApplyState -Outcome 'SpicetifyApplyRolledBack' -Successful $false -ErrorMessage $applyError
        throw "Spicetify apply failed but LibreSpot restored Spotify to a usable state. Apply error: $applyError"
    } else {
        Update-ApplyState -Outcome 'SpicetifyApplyRollbackFailed' -Successful $false -ErrorMessage "Apply error: $applyError | Rollback error: $restoreError"
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
                    Save-ToAssetCache -SourcePath $destination -SHA256Hash $spotxHash
                }
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
            Open-SpicetifyMarketplace
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
            if (-not (Test-Path -LiteralPath (Join-Path $global:SPICETIFY_DIR 'spicetify.exe'))) {
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
                @{ Path = $global:CONFIG_DIR; Label = 'Config directory'; RemovesActiveProfile = $true }
            )
            $step = 20
            foreach ($entry in $selfPaths) {
                Update-BackendState -Progress $step -Status "Removing $($entry.Label)" -Step $entry.Label
                if (Test-Path -LiteralPath $entry.Path) {
                    if ($entry.RemovesActiveProfile) {
                        if (-not (Test-SafeRemovalTarget -Path $entry.Path)) {
                            Write-OperationJournalEntry -Phase 'remove' -Target $entry.Path -SafetyDecision 'RefusedUnsafeTarget' -Result 'Refused' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target failed LibreSpot safe-removal checks.' -Data @{ label = $entry.Label }
                            Write-Log "Refusing to remove unsafe target: $($entry.Path)" -Level 'WARN'
                        } else {
                            Write-OperationJournalEntry -Phase 'remove' -Target $entry.Path -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'This removes LibreSpot profile data by user request.' -Data @{ label = $entry.Label }
                            Remove-Item -LiteralPath $entry.Path -Recurse -Force -ErrorAction Stop
                            Write-EventLine -Kind 'log' -Level 'INFO' -Payload "Removed: $($entry.Label) ($($entry.Path))"
                        }
                    } else {
                        $null = Remove-PathSafely -Path $entry.Path -Label $entry.Label
                        Write-Log "Removed: $($entry.Label) ($($entry.Path))"
                    }
                } else {
                    if ($entry.RemovesActiveProfile) {
                        Write-EventLine -Kind 'log' -Level 'INFO' -Payload "Not found: $($entry.Label) ($($entry.Path))"
                    } else {
                        Write-Log "Not found: $($entry.Label) ($($entry.Path))"
                    }
                }
                $step += 25
            }
            Write-EventLine -Kind 'log' -Level 'SUCCESS' -Payload 'LibreSpot self-cleanup complete. Spotify and Spicetify were not affected.'
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

    Write-EventLine -Kind 'action' -Payload $Action
    $journalWouldChange = ($Action -notin @('CheckUpdates', 'OpenMarketplace', 'WatchAutoReapply'))
    Start-OperationJournalRun -Action $Action -Target "Backend action: $Action" -WouldChange $journalWouldChange -Reversible $false -RollbackHint 'Review individual journal entries for action-specific rollback hints.' | Out-Null
    if ($Action -notin @('CheckUpdates', 'EnableAutoReapply', 'DisableAutoReapply')) {
        Ensure-Admin
    }

    # Gate patching actions behind risk acknowledgment. The WPF shell handles
    # the dialog; the backend enforces the invariant as a safety net.
    if ($Action -notin @('CheckUpdates', 'EnableAutoReapply', 'DisableAutoReapply', 'WatchAutoReapply')) {
        $riskConfig = Load-LibreSpotConfig
        if (-not (ConvertTo-ConfigBoolean -Value $riskConfig['RiskAcknowledged'] -Default $false)) {
            Write-Log 'RiskAcknowledged is false. The desktop shell must present the acknowledgment dialog before running this action.' -Level 'ERROR'
            Write-EventLine -Kind 'result' -Level 'ERROR' -Payload 'Risk acknowledgment required before this action can proceed.'
            Complete-OperationJournalRun -Result 'Refused' -Message 'Risk acknowledgment required before this action can proceed.'
            exit 1
        }
    }

    if ($Action -eq 'Install') {
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
