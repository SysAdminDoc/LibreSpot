#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }
<#
    .SYNOPSIS
        Pester 5.x tests for isolated PowerShell behavior in LibreSpot.ps1.

    .DESCRIPTION
        Since LibreSpot.ps1 is a monolith that immediately bootstraps WPF when
        dot-sourced, we cannot source the file directly.  Instead we parse the
        script text and extract individual function definitions via regex, then
        load them into the test session with Invoke-Expression.

    .NOTES
        Compatible with Windows PowerShell 5.1 and PowerShell 7+.
#>

BeforeAll {
    # ---- helpers ----
    function Extract-FunctionBlock {
        param(
            [string]$Script,
            [string]$FunctionName
        )
        # Match a top-level function definition whose closing brace sits at
        # column 0.  The (?ms) flags enable multiline (^ matches line start)
        # and single-line (. matches newline) modes.
        $pattern = "(?ms)^function\s+${FunctionName}\s*\{.+?^\}"
        if ($Script -match $pattern) { return $Matches[0] }
        throw "Function '$FunctionName' not found in script text"
    }

    # ---- locate scripts ----
    $mainScript = Join-Path $PSScriptRoot '..\..\LibreSpot.ps1'
    if (-not (Test-Path -LiteralPath $mainScript)) {
        throw "Cannot find LibreSpot.ps1 at $mainScript"
    }
    $scriptContent = Get-Content -Path $mainScript -Raw

    # ---- extract the CONFIG_SCHEMA_VERSION constant ----
    if ($scriptContent -match '\$global:CONFIG_SCHEMA_VERSION\s*=\s*(\d+)') {
        $global:CONFIG_SCHEMA_VERSION = [int]$Matches[1]
    } else {
        throw 'Could not extract $global:CONFIG_SCHEMA_VERSION from script'
    }

    # ---- extract and load pure functions ----
    # PowerShell treats bare `[int]::MaxValue` in argument position as a string
    # rather than evaluating the expression (it only works in default-value
    # position inside param blocks). Replace these tokens with their numeric
    # literals so the extracted functions work correctly when invoked.
    $functionsToLoad = @(
        'Get-NormalizedPathString'
        'Get-PathEntries'
        'Set-PathEntries'
        'Add-PathEntry'
        'Remove-PathEntry'
        'ConvertTo-ConfigBoolean'
        'ConvertTo-ConfigInt'
        'Get-LibreSpotConfigSchemaVersion'
        'Assert-LibreSpotConfigSchemaSupported'
        'Normalize-LibreSpotConfig'
        'New-LibreSpotEngineBootstrap'
        'Get-SpicetifyApplyPlan'
        'Repair-LibreSpotManagedCustomAppRoutes'
        'Compare-LibreSpotVersions'
        'Get-SpotXChildFailureClassification'
        'Get-ThirdPartyPatcherReport'
        'Copy-DirectorySnapshotSafely'
        'Merge-DirectorySnapshotMissingFiles'
        'Get-LibreSpotTempRoot'
        'Expand-ArchiveSafely'
        'Export-MarketplaceState'
        'Restore-MarketplaceState'
        'New-SpicetifyStatePreservationSnapshot'
        'Restore-SpicetifyStatePreservationSnapshot'
        'Invoke-WithSpicetifyStatePreservation'
        'Get-WatcherLaunchCommand'
        'Get-WatcherState'
        'Set-WatcherState'
        'Invoke-AutoReapplyWatcher'
        'Invoke-HeadlessReapply'
        'Register-AutoReapplyTask'
        'Get-PowerShell7SecurityFloorStatus'
        'Write-PowerShell7SecurityFloorWarningIfNeeded'
        'Get-QuarantineGuidance'
    )
    $blocks = foreach ($fn in $functionsToLoad) {
        $block = Extract-FunctionBlock $scriptContent $fn
        $block = $block -replace '\[int\]::MaxValue', [string][int]::MaxValue
        $block = $block -replace '\[int\]::MinValue', [string][int]::MinValue
        $block
    }
    $combined = $blocks -join "`n`n"
    Invoke-Expression $combined

    function Write-Log {
        param([string]$Message, [string]$Level = 'INFO')
    }

    function Write-OperationJournalEntry {
        param(
            [string]$OperationId,
            [string]$Phase,
            [string]$Target,
            [string]$SafetyDecision,
            [string]$Result,
            [bool]$WouldChange,
            [bool]$Reversible,
            [string]$RollbackHint,
            [string]$TokenKind,
            [string]$PreviousStateRef,
            [string]$NewState,
            [string]$UndoAction,
            [string]$Risk,
            [hashtable]$Data
        )
    }

    # ---- set up the minimal global state that Normalize-LibreSpotConfig needs ----
    $global:EasyDefaults = @{
        UiCulture='en'
        SpotX_NewTheme=$true; SpotX_PodcastsOff=$true; SpotX_BlockUpdate=$true; SpotX_AdSectionsOff=$true
        SpotX_Premium=$false; SpotX_LyricsEnabled=$true; SpotX_LyricsTheme="spotify"
        SpotX_TopSearch=$false; SpotX_RightSidebarOff=$false; SpotX_RightSidebarClr=$false
        SpotX_CanvasHomeOff=$false; SpotX_HomeSubOff=$false; SpotX_DisableStartup=$true; SpotX_NoShortcut=$false; SpotX_CacheLimit=0
        SpotX_Plus=$false; SpotX_NewFullscreen=$false; SpotX_FunnyProgress=$false; SpotX_ExpSpotify=$false; SpotX_LyricsBlock=$false
        SpotX_SendVersionOff=$true; SpotX_StartSpoti=$false
        SpotX_DevTools=$false; SpotX_Mirror=$false; SpotX_DownloadMethod=""; SpotX_ConfirmUninstall=$false
        SpotX_SpotifyVersionId="auto"
        SpotX_Language=""
        SpotX_CustomPatchesEnabled=$false; SpotX_CustomPatchesJson=""
        Spicetify_Theme="(None - Marketplace Only)"; Spicetify_Scheme="Default"; Spicetify_Marketplace=$true
        Spicetify_Extensions=@("fullAppDisplay.js","shuffle+.js","trashbin.js")
        Spicetify_CustomApps=@('librespot')
        LibreSpot_EngineProfileJson=''
        LibreSpot_EnabledSnippets=@()
        LibreSpot_FeatureOverridesJson='{}'
        CleanInstall=$true; LaunchAfter=$true
        AutoReapply_Enabled=$false
    }

    $global:SpotXLyricsThemes = @(
        'spotify','blueberry','blue','discord','forest','fresh','github','lavender',
        'orange','pumpkin','purple','red','strawberry','turquoise','yellow','oceano',
        'royal','krux','pinkle','zing','radium','sandbar','postlight','relish',
        'drot','default','spotify#2'
    )

    $global:ThemeData = [ordered]@{
        "(None - Marketplace Only)" = @{ Schemes = @("Default"); Preview = @{} }
        "Sleek" = @{ Schemes = @("Wealthy","Cherry","Coral","Deep","Greener","Deeper","Psycho","UltraBlack","Nord","Futura","Elementary","BladeRunner","Dracula","VantaBlack","RosePine","Eldritch","Catppuccin","AyuDark","TokyoNight"); Preview = @{} }
    }

    $global:BuiltInExtensions = [ordered]@{
        "fullAppDisplay.js"   = "Full-screen album art display"
        "shuffle+.js"         = "True shuffle"
        "trashbin.js"         = "Skip unwanted songs"
        "keyboardShortcut.js" = "Vim-style keyboard navigation"
        "bookmark.js"         = "Save and recall pages"
        "loopyLoop.js"        = "A-B loop"
        "popupLyrics.js"      = "Synchronized lyrics popup"
        "autoSkipVideo.js"    = "Skip canvas videos"
        "autoSkipExplicit.js" = "Skip explicit tracks"
        "webnowplaying.js"    = "Now-playing for Rainmeter"
    }

    $global:CommunityExtensions = [ordered]@{
        "hidePodcasts.js"      = @{ Description = "Hide podcasts" }
        "beautiful-lyrics.mjs" = @{ Description = "Beautiful lyrics" }
        "playlist-icons.js"    = @{ Description = "Playlist icons" }
        "volumePercentage.js"  = @{ Description = "Volume percentage" }
        "adblock.js"           = @{ Description = "Adblock" }
    }

    $global:CommunityExtensionAliases = @{
        "beautifulLyrics.js" = "beautiful-lyrics.mjs"
        "playlistIcons.js"   = "playlist-icons.js"
    }

    $global:CommunityCustomApps = [ordered]@{
        librespot = @{ DisplayName = 'LibreSpot' }
        stats = @{ DisplayName = 'Stats' }
    }

    $global:SpotifyVersionManifest = @(
        @{ Id='auto'; Label='Auto'; Version=''; Notes='Recommended.' }
        @{ Id='1.2.93'; Label='1.2.93'; Version='1.2.93'; Notes='Pinned.' }
        @{ Id='1.2.92'; Label='1.2.92'; Version='1.2.92'; Notes='Previous fallback.' }
    )
    $global:SpotifyVersionIds = @($global:SpotifyVersionManifest | ForEach-Object { $_.Id })
}

Describe 'Get-SpicetifyApplyPlan' {
    It 'reuses a complete backup that matches the installed Spotify version' {
        $root = Join-Path $TestDrive 'apply-plan-current'
        $backup = Join-Path $root 'Backup'
        $extracted = Join-Path $root 'Extracted'
        New-Item -Path $backup, (Join-Path $extracted 'Raw'), (Join-Path $extracted 'Themed') -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $backup 'xpui.spa') -Value 'xpui' -Encoding Ascii
        Set-Content -LiteralPath (Join-Path $backup 'login.spa') -Value 'login' -Encoding Ascii
        $configPath = Join-Path $root 'config-xpui.ini'
        Set-Content -LiteralPath $configPath -Value "[Backup]`r`nversion = 1.2.93.667.g7b5cc0ce" -Encoding Ascii

        $plan = Get-SpicetifyApplyPlan -ConfigPath $configPath -BackupDirectory $backup -ExtractedDirectory $extracted -SpotifyVersion '1.2.93.667.g7b5cc0ce'

        $plan.Stage | Should -Be 'apply --no-restart'
        @($plan.Arguments) | Should -Be @('apply', '--no-restart', '--bypass-admin')
    }

    It 'creates a fresh backup when the saved backup is incomplete or stale' {
        $root = Join-Path $TestDrive 'apply-plan-stale'
        $backup = Join-Path $root 'Backup'
        $extracted = Join-Path $root 'Extracted'
        New-Item -Path $backup, (Join-Path $extracted 'Raw'), (Join-Path $extracted 'Themed') -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $backup 'xpui.spa') -Value 'xpui' -Encoding Ascii
        $configPath = Join-Path $root 'config-xpui.ini'
        Set-Content -LiteralPath $configPath -Value "[Backup]`r`nversion = 1.2.92.1" -Encoding Ascii

        $plan = Get-SpicetifyApplyPlan -ConfigPath $configPath -BackupDirectory $backup -ExtractedDirectory $extracted -SpotifyVersion '1.2.93.667.g7b5cc0ce'

        $plan.Stage | Should -Be 'backup apply'
        @($plan.Arguments) | Should -Be @('backup', 'apply', '--bypass-admin')
    }

    It 'matches Spotify file versions to Spicetify versions with a git hash suffix' {
        $root = Join-Path $TestDrive 'apply-plan-spotify-hash'
        $backup = Join-Path $root 'Backup'
        $extracted = Join-Path $root 'Extracted'
        New-Item -Path $backup, (Join-Path $extracted 'Raw'), (Join-Path $extracted 'Themed') -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $backup 'xpui.spa') -Value 'xpui' -Encoding Ascii
        Set-Content -LiteralPath (Join-Path $backup 'login.spa') -Value 'login' -Encoding Ascii
        $configPath = Join-Path $root 'config-xpui.ini'
        Set-Content -LiteralPath $configPath -Value "[Backup]`r`nversion = 1.2.93.667.g7b5cc0ce" -Encoding Ascii

        $plan = Get-SpicetifyApplyPlan -ConfigPath $configPath -BackupDirectory $backup -ExtractedDirectory $extracted -SpotifyVersion '1.2.93.667'

        $plan.Stage | Should -Be 'apply --no-restart'
        @($plan.Arguments) | Should -Be @('apply', '--no-restart', '--bypass-admin')
    }
}

Describe 'Get-QuarantineGuidance' {
    It 'requires source and hash verification before restore' {
        $guidance = Get-QuarantineGuidance -What 'The verified test file'

        $guidance | Should -Match 'Protection history'
        $guidance | Should -Match 'official source'
        $guidance | Should -Match 'SHA256'
        $guidance | Should -Match 'leave the file blocked'
        $guidance | Should -Not -Match 'add an exclusion'
    }
}

Describe 'Build-CommunityCatalog' {
    BeforeAll {
        $script:catalogRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-catalog-{0}" -f ([guid]::NewGuid().ToString('N')))
        $script:catalogGenerator = Join-Path $PSScriptRoot '..\..\tools\Build-CommunityCatalog.ps1'
        $script:communityManifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '..\..\schemas\community-assets.json') | ConvertFrom-Json
        & $script:catalogGenerator -OutputDirectory $script:catalogRoot -GeneratedDate '2026-08-20' | Out-Null
        $script:catalog = Get-Content -Raw -LiteralPath (Join-Path $script:catalogRoot 'catalog.json') | ConvertFrom-Json
        $script:catalogHtml = Get-Content -Raw -LiteralPath (Join-Path $script:catalogRoot 'index.html')
    }

    AfterAll {
        Remove-Item -LiteralPath $script:catalogRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'renders every community manifest asset with required trust metadata' {
        $expectedCount = @($script:communityManifest.extensions).Count +
            @($script:communityManifest.themes).Count +
            @($script:communityManifest.customApps).Count
        $script:catalog.items.Count | Should -Be $expectedCount

        foreach ($item in @($script:catalog.items)) {
            $item.provenance.sourceUrl | Should -Not -BeNullOrEmpty
            $item.license.spdx | Should -Not -BeNullOrEmpty
            $item.verification.badge | Should -Be 'Pinned SHA256 verified'
            $item.verification.digest | Should -Not -BeNullOrEmpty
            $item.review.evaluatedDate | Should -Not -BeNullOrEmpty
            $item.review.evidenceUrls.Count | Should -BeGreaterThan 0
        }
    }

    It 'joins every community theme to the shared preview manifest' {
        @($script:catalog.items | Where-Object kind -eq 'theme').Count | Should -Be @($script:communityManifest.themes).Count
        @($script:catalog.items | Where-Object { $_.kind -eq 'theme' -and $null -ne $_.preview }).Count |
            Should -Be @($script:communityManifest.themes).Count
    }

    It 'shows provenance, license, verification, review, and evidence in the page' {
        foreach ($token in @('Provenance', 'License', 'Pinned SHA256 verified', 'Reviewed', 'Evidence and release links')) {
            $script:catalogHtml | Should -Match ([regex]::Escape($token))
        }
    }
}

Describe 'Per-user PATH registry isolation' {
    BeforeEach {
        $script:pathRegistryRoot = "Software\LibreSpot\Tests\PathIsolation\$([Guid]::NewGuid().ToString('N'))"
        $script:userAPathKey = "$script:pathRegistryRoot\UserA"
        $script:userBPathKey = "$script:pathRegistryRoot\UserB"
        $script:pathFixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LibreSpot-PathIsolation-" + [Guid]::NewGuid().ToString('N'))
        $script:userAConfigRoot = Join-Path $script:pathFixtureRoot 'UserA\LibreSpot'
        $script:userBConfigRoot = Join-Path $script:pathFixtureRoot 'UserB\LibreSpot'

        $keyA = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($script:userAPathKey)
        $keyB = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($script:userBPathKey)
        try {
            $keyA.SetValue('Path', '%USERPROFILE%\UserA\Bin;C:\Shared\Tools', [Microsoft.Win32.RegistryValueKind]::ExpandString)
            $keyB.SetValue('Path', '%USERPROFILE%\UserB\Bin;C:\Shared\Tools', [Microsoft.Win32.RegistryValueKind]::ExpandString)
        } finally {
            $keyA.Dispose()
            $keyB.Dispose()
        }
        New-Item -Path $script:userAConfigRoot, $script:userBConfigRoot -ItemType Directory -Force | Out-Null
    }

    AfterEach {
        try { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($script:pathRegistryRoot) } catch {}
        Remove-Item -LiteralPath $script:pathFixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'keeps two user PATH values, expandable tokens, and undo state independent' {
        $aBefore = @(Get-PathEntries -Scope User -EnvironmentKeyPath $script:userAPathKey)
        $bBefore = @(Get-PathEntries -Scope User -EnvironmentKeyPath $script:userBPathKey)
        $aBefore | Should -Contain '%USERPROFILE%\UserA\Bin'
        $bBefore | Should -Contain '%USERPROFILE%\UserB\Bin'
        $aBefore | Should -Not -Contain '%USERPROFILE%\UserB\Bin'
        $bBefore | Should -Not -Contain '%USERPROFILE%\UserA\Bin'

        $global:CONFIG_DIR = $script:userAConfigRoot
        $global:CURRENT_OPERATION_ID = 'path-isolation-user-a'
        Set-PathEntries -Scope User -EnvironmentKeyPath $script:userAPathKey -Entries @(
            '%USERPROFILE%\UserA\Bin'
            'C:\Shared\Tools'
            'C:\Users\LibreSpot\UserA'
        ) -TokenKind 'pathEntryAdd' -ChangedEntry 'C:\Users\LibreSpot\UserA' -SkipEnvironmentBroadcast

        $global:CONFIG_DIR = $script:userBConfigRoot
        $global:CURRENT_OPERATION_ID = 'path-isolation-user-b'
        Set-PathEntries -Scope User -EnvironmentKeyPath $script:userBPathKey -Entries @(
            '%USERPROFILE%\UserB\Bin'
            'C:\Shared\Tools'
            'C:\Users\LibreSpot\UserB'
        ) -TokenKind 'pathEntryAdd' -ChangedEntry 'C:\Users\LibreSpot\UserB' -SkipEnvironmentBroadcast

        $aAfter = @(Get-PathEntries -Scope User -EnvironmentKeyPath $script:userAPathKey)
        $bAfter = @(Get-PathEntries -Scope User -EnvironmentKeyPath $script:userBPathKey)
        $aAfter | Should -Contain '%USERPROFILE%\UserA\Bin'
        $aAfter | Should -Contain 'C:\Users\LibreSpot\UserA'
        $aAfter | Should -Not -Contain 'C:\Users\LibreSpot\UserB'
        $bAfter | Should -Contain '%USERPROFILE%\UserB\Bin'
        $bAfter | Should -Contain 'C:\Users\LibreSpot\UserB'
        $bAfter | Should -Not -Contain 'C:\Users\LibreSpot\UserA'

        $keyA = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($script:userAPathKey, $false)
        $keyB = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($script:userBPathKey, $false)
        try {
            $keyA.GetValueKind('Path') | Should -Be ([Microsoft.Win32.RegistryValueKind]::ExpandString)
            $keyB.GetValueKind('Path') | Should -Be ([Microsoft.Win32.RegistryValueKind]::ExpandString)
        } finally {
            $keyA.Dispose()
            $keyB.Dispose()
        }

        (Get-ChildItem -LiteralPath (Join-Path $script:userAConfigRoot 'undo-states') -Filter '*.json' -File).Count | Should -Be 1
        (Get-ChildItem -LiteralPath (Join-Path $script:userBConfigRoot 'undo-states') -Filter '*.json' -File).Count | Should -Be 1
        { Get-PathEntries -Scope User -EnvironmentKeyPath '..\Environment' } | Should -Throw
    }
}

Describe 'Get-ThirdPartyPatcherReport' {
    BeforeEach {
        $script:patcherRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LibreSpot-PatcherReport-" + [Guid]::NewGuid().ToString('N'))
        $script:spotifyPath = Join-Path $script:patcherRoot 'Spotify\Spotify.exe'
        $script:configDirectory = Join-Path $script:patcherRoot 'LibreSpot'
        $script:spicetifyPath = Join-Path $script:patcherRoot 'spicetify\spicetify.exe'
        $script:spicetifyConfigPath = Join-Path $script:patcherRoot 'spicetify-config\config-xpui.ini'
        New-Item -Path (Split-Path $script:spotifyPath -Parent) -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath $script:spotifyPath -Value 'spotify' -Encoding UTF8
    }

    AfterEach {
        Remove-Item -LiteralPath $script:patcherRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'distinguishes raw SpotX and standalone Spicetify from LibreSpot-owned state' {
        New-Item -Path (Join-Path (Split-Path $script:spotifyPath -Parent) 'Apps') -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path (Split-Path $script:spotifyPath -Parent) 'Apps\xpui.spa') -Value 'bundle' -Encoding UTF8
        Set-Content -LiteralPath (Join-Path (Split-Path $script:spotifyPath -Parent) 'Apps\xpui.bak') -Value 'backup' -Encoding UTF8
        New-Item -Path (Split-Path $script:spicetifyPath -Parent) -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath $script:spicetifyPath -Value 'cli' -Encoding UTF8

        $foreign = Get-ThirdPartyPatcherReport -SpotifyExePath $script:spotifyPath -ConfigDirectory $script:configDirectory -SpicetifyPath $script:spicetifyPath -SpicetifyConfigPath $script:spicetifyConfigPath
        $foreign.Ownership | Should -Be 'foreign'
        $foreign.Footprints.Id | Should -Contain 'raw-spotx'
        $foreign.Footprints.Id | Should -Contain 'standalone-spicetify'

        New-Item -Path $script:configDirectory -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $script:configDirectory 'operation-journal.jsonl') -Value '{}' -Encoding UTF8
        $owned = Get-ThirdPartyPatcherReport -SpotifyExePath $script:spotifyPath -ConfigDirectory $script:configDirectory -SpicetifyPath $script:spicetifyPath -SpicetifyConfigPath $script:spicetifyConfigPath
        $owned.Ownership | Should -Be 'librespot'
        $owned.Footprints.Id | Should -Contain 'librespot-spotx'
        $owned.Footprints.Id | Should -Contain 'librespot-spicetify'
    }

    It 'keeps BlockTheSpot-family injector residue foreign even beside LibreSpot evidence' {
        New-Item -Path $script:configDirectory -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $script:configDirectory 'install.log') -Value 'ok' -Encoding UTF8
        Set-Content -LiteralPath (Join-Path (Split-Path $script:spotifyPath -Parent) 'dpapi.dll') -Value 'injector' -Encoding UTF8

        $report = Get-ThirdPartyPatcherReport -SpotifyExePath $script:spotifyPath -ConfigDirectory $script:configDirectory -SpicetifyPath $script:spicetifyPath -SpicetifyConfigPath $script:spicetifyConfigPath
        $report.HasForeignState | Should -BeTrue
        $report.Footprints.Id | Should -Contain 'likely-blockthespot'
        $report.Recommendation | Should -Match 'explicitly confirmed cleanup'
    }
}

# =============================================================================
# Get-NormalizedPathString
# =============================================================================
Describe 'Get-NormalizedPathString' {

    Context 'Null, empty, and whitespace inputs' {
        It 'Returns $null for $null input' {
            Get-NormalizedPathString -Path $null | Should -BeNullOrEmpty
        }

        It 'Returns $null for empty string' {
            Get-NormalizedPathString -Path '' | Should -BeNullOrEmpty
        }

        It 'Returns $null for whitespace-only string' {
            Get-NormalizedPathString -Path '   ' | Should -BeNullOrEmpty
        }
    }

    Context 'Environment variable expansion' {
        It 'Expands %TEMP% environment variable' {
            $expected = [Environment]::ExpandEnvironmentVariables('%TEMP%').TrimEnd('\')
            $result = Get-NormalizedPathString -Path '%TEMP%'
            # GetFullPath will resolve it; just verify the env var was expanded
            $result | Should -Not -BeLike '*%TEMP%*'
        }

        It 'Expands %USERPROFILE% environment variable' {
            $result = Get-NormalizedPathString -Path '%USERPROFILE%\Documents'
            $result | Should -Not -BeLike '*%USERPROFILE%*'
            $result | Should -BeLike '*Documents'
        }
    }

    Context 'Trailing backslash normalization' {
        It 'Strips single trailing backslash' {
            $result = Get-NormalizedPathString -Path 'C:\Windows\'
            $result | Should -Not -Match '\\$'
        }

        It 'Strips multiple trailing backslashes' {
            $result = Get-NormalizedPathString -Path 'C:\Windows\\'
            $result | Should -Not -Match '\\$'
        }
    }

    Context 'Full path resolution' {
        It 'Returns an absolute path for a relative input' {
            $result = Get-NormalizedPathString -Path 'somefolder\subfolder'
            # GetFullPath resolves relative to cwd, so result should be rooted
            [System.IO.Path]::IsPathRooted($result) | Should -BeTrue
        }

        It 'Preserves already-absolute paths' {
            $result = Get-NormalizedPathString -Path 'C:\Windows\System32'
            $result | Should -BeExactly 'C:\Windows\System32'
        }
    }

    Context 'Whitespace trimming' {
        It 'Trims leading and trailing whitespace before processing' {
            $result = Get-NormalizedPathString -Path '  C:\Windows  '
            $result | Should -BeExactly 'C:\Windows'
        }
    }
}

# =============================================================================
# Remove-PathSafely
# =============================================================================
Describe 'Remove-PathSafely' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Remove-PathSafely.ps1')

        function Test-SafeRemovalTarget { param([string]$Path) return $true }
        function Write-OperationJournalEntry {
            param(
                [string]$Phase,
                [string]$Target,
                [string]$SafetyDecision,
                [string]$Result,
                [bool]$WouldChange,
                [bool]$Reversible,
                [string]$RollbackHint,
                [hashtable]$Data
            )
        }
        function Write-Log { param([string]$Message, [string]$Level) }
    }

    It 'Unlinks a nested junction without touching its external target' {
        $testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('LibreSpot.RemoveSafe.' + [guid]::NewGuid().ToString('N'))
        $approvedRoot = Join-Path $testRoot 'approved'
        $nestedRoot = Join-Path $approvedRoot 'nested'
        $externalRoot = Join-Path $testRoot 'external'
        $junctionPath = Join-Path $nestedRoot 'escape'
        $sentinelPath = Join-Path $externalRoot 'must-survive.txt'

        try {
            [System.IO.Directory]::CreateDirectory($nestedRoot) | Out-Null
            [System.IO.Directory]::CreateDirectory($externalRoot) | Out-Null
            [System.IO.File]::WriteAllText($sentinelPath, 'outside approved root')
            $null = & cmd.exe /d /c "mklink /J `"$junctionPath`" `"$externalRoot`""
            $LASTEXITCODE | Should -Be 0

            Remove-PathSafely -Path $approvedRoot -Label 'test root' -Confirm:$false | Should -Be 1

            Test-Path -LiteralPath $approvedRoot | Should -BeFalse
            Test-Path -LiteralPath $sentinelPath -PathType Leaf | Should -BeTrue
            [System.IO.File]::ReadAllText($sentinelPath) | Should -BeExactly 'outside approved root'
        } finally {
            if (Test-Path -LiteralPath $junctionPath) {
                $junction = Get-Item -LiteralPath $junctionPath -Force
                if ($junction.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                    $junction.Delete()
                }
            }
            if (Test-Path -LiteralPath $approvedRoot) {
                Remove-Item -LiteralPath $approvedRoot -Recurse -Force
            }
            if (Test-Path -LiteralPath $externalRoot) {
                Remove-Item -LiteralPath $externalRoot -Recurse -Force
            }
            if (Test-Path -LiteralPath $testRoot) {
                Remove-Item -LiteralPath $testRoot -Force
            }
        }
    }
}

# =============================================================================
# ConvertTo-ConfigInt
# =============================================================================
Describe 'ConvertTo-ConfigInt' {

    Context 'Basic integer parsing' {
        It 'Parses a valid integer string' {
            ConvertTo-ConfigInt -Value '42' | Should -Be 42
        }

        It 'Parses zero' {
            ConvertTo-ConfigInt -Value '0' | Should -Be 0
        }

        It 'Parses negative integers' {
            ConvertTo-ConfigInt -Value '-7' | Should -Be -7
        }

        It 'Passes through an actual [int] value' {
            ConvertTo-ConfigInt -Value 100 | Should -Be 100
        }
    }

    Context 'Default value fallback' {
        It 'Returns default for $null' {
            ConvertTo-ConfigInt -Value $null -Default 5 | Should -Be 5
        }

        It 'Returns default for non-numeric string' {
            ConvertTo-ConfigInt -Value 'abc' -Default 10 | Should -Be 10
        }

        It 'Returns default for empty string' {
            ConvertTo-ConfigInt -Value '' -Default 3 | Should -Be 3
        }

        It 'Returns 0 when no Default is specified and value is $null' {
            ConvertTo-ConfigInt -Value $null | Should -Be 0
        }
    }

    Context 'Minimum and maximum clamping' {
        It 'Clamps below Minimum to Minimum' {
            ConvertTo-ConfigInt -Value '-5' -Minimum 0 | Should -Be 0
        }

        It 'Clamps above Maximum to Maximum' {
            ConvertTo-ConfigInt -Value '999' -Maximum 100 | Should -Be 100
        }

        It 'Does not clamp a value within range' {
            ConvertTo-ConfigInt -Value '50' -Minimum 0 -Maximum 100 | Should -Be 50
        }

        It 'Clamps default value when default itself is below Minimum' {
            ConvertTo-ConfigInt -Value $null -Default -10 -Minimum 0 | Should -Be 0
        }

        It 'Clamps default value when default itself is above Maximum' {
            ConvertTo-ConfigInt -Value 'bad' -Default 200 -Maximum 100 | Should -Be 100
        }
    }
}

# =============================================================================
# ConvertTo-ConfigBoolean (helper used by Normalize-LibreSpotConfig)
# =============================================================================
Describe 'ConvertTo-ConfigBoolean' {

    Context 'Truthy string values' {
        It 'Converts "true" to $true' {
            ConvertTo-ConfigBoolean -Value 'true' | Should -BeTrue
        }

        It 'Converts "True" (mixed case) to $true' {
            ConvertTo-ConfigBoolean -Value 'True' | Should -BeTrue
        }

        It 'Converts "1" to $true' {
            ConvertTo-ConfigBoolean -Value '1' | Should -BeTrue
        }

        It 'Converts "yes" to $true' {
            ConvertTo-ConfigBoolean -Value 'yes' | Should -BeTrue
        }

        It 'Converts "on" to $true' {
            ConvertTo-ConfigBoolean -Value 'on' | Should -BeTrue
        }
    }

    Context 'Falsy string values' {
        It 'Converts "false" to $false' {
            ConvertTo-ConfigBoolean -Value 'false' | Should -BeFalse
        }

        It 'Converts "0" to $false' {
            ConvertTo-ConfigBoolean -Value '0' | Should -BeFalse
        }

        It 'Converts "no" to $false' {
            ConvertTo-ConfigBoolean -Value 'no' | Should -BeFalse
        }

        It 'Converts "off" to $false' {
            ConvertTo-ConfigBoolean -Value 'off' | Should -BeFalse
        }
    }

    Context 'Non-string types' {
        It 'Passes through $true' {
            ConvertTo-ConfigBoolean -Value $true | Should -BeTrue
        }

        It 'Passes through $false' {
            ConvertTo-ConfigBoolean -Value $false | Should -BeFalse
        }

        It 'Treats non-zero integer as $true' {
            ConvertTo-ConfigBoolean -Value 42 | Should -BeTrue
        }

        It 'Treats zero integer as $false' {
            ConvertTo-ConfigBoolean -Value 0 | Should -BeFalse
        }
    }

    Context 'Default fallback' {
        It 'Returns default for $null' {
            ConvertTo-ConfigBoolean -Value $null -Default $true | Should -BeTrue
        }

        It 'Returns default for empty string' {
            ConvertTo-ConfigBoolean -Value '' -Default $true | Should -BeTrue
        }

        It 'Returns default for unrecognized string' {
            ConvertTo-ConfigBoolean -Value 'maybe' -Default $false | Should -BeFalse
        }

        It 'Returns $false by default when Default is not specified and value is $null' {
            ConvertTo-ConfigBoolean -Value $null | Should -BeFalse
        }
    }
}

# =============================================================================
# Get-LibreSpotConfigSchemaVersion
# =============================================================================
Describe 'Get-LibreSpotConfigSchemaVersion' {

    Context 'Missing or absent key' {
        It 'Returns 0 for $null config' {
            Get-LibreSpotConfigSchemaVersion -Config $null | Should -Be 0
        }

        It 'Returns 0 for empty hashtable' {
            Get-LibreSpotConfigSchemaVersion -Config @{} | Should -Be 0
        }

        It 'Returns 0 when ConfigSchemaVersion key is absent' {
            Get-LibreSpotConfigSchemaVersion -Config @{ Mode = 'Easy' } | Should -Be 0
        }
    }

    Context 'Valid schema versions' {
        It 'Returns 1 for ConfigSchemaVersion = 1' {
            Get-LibreSpotConfigSchemaVersion -Config @{ ConfigSchemaVersion = 1 } | Should -Be 1
        }

        It 'Parses string "1" as integer 1' {
            Get-LibreSpotConfigSchemaVersion -Config @{ ConfigSchemaVersion = '1' } | Should -Be 1
        }

        It 'Returns 0 for non-numeric ConfigSchemaVersion' {
            Get-LibreSpotConfigSchemaVersion -Config @{ ConfigSchemaVersion = 'abc' } | Should -Be 0
        }
    }
}

# =============================================================================
# Assert-LibreSpotConfigSchemaSupported
# =============================================================================
Describe 'Assert-LibreSpotConfigSchemaSupported' {

    Context 'Supported schema versions' {
        It 'Returns 0 for empty config (schema version 0)' {
            Assert-LibreSpotConfigSchemaSupported -Config @{} | Should -Be 0
        }

        It 'Returns the schema version when it equals CONFIG_SCHEMA_VERSION' {
            $result = Assert-LibreSpotConfigSchemaSupported -Config @{ ConfigSchemaVersion = $global:CONFIG_SCHEMA_VERSION }
            $result | Should -Be $global:CONFIG_SCHEMA_VERSION
        }

        It 'Accepts schema version less than CONFIG_SCHEMA_VERSION' {
            # Only meaningful when CONFIG_SCHEMA_VERSION > 0
            if ($global:CONFIG_SCHEMA_VERSION -gt 0) {
                $result = Assert-LibreSpotConfigSchemaSupported -Config @{ ConfigSchemaVersion = 0 }
                $result | Should -Be 0
            } else {
                Set-ItResult -Skipped -Because 'CONFIG_SCHEMA_VERSION is 0; no lower version to test'
            }
        }
    }

    Context 'Unsupported schema versions' {
        It 'Throws when schema version exceeds CONFIG_SCHEMA_VERSION' {
            $futureVersion = $global:CONFIG_SCHEMA_VERSION + 1
            { Assert-LibreSpotConfigSchemaSupported -Config @{ ConfigSchemaVersion = $futureVersion } } |
                Should -Throw "*newer than this LibreSpot build supports*"
        }

        It 'Throws with a message that includes both version numbers' {
            $futureVersion = $global:CONFIG_SCHEMA_VERSION + 5
            { Assert-LibreSpotConfigSchemaSupported -Config @{ ConfigSchemaVersion = $futureVersion } } |
                Should -Throw "*$futureVersion*$($global:CONFIG_SCHEMA_VERSION)*"
        }
    }
}

# =============================================================================
# Normalize-LibreSpotConfig
# =============================================================================
Describe 'Normalize-LibreSpotConfig' {

    Context 'Empty or minimal config' {
        It 'Returns all defaults for empty hashtable' {
            $result = Normalize-LibreSpotConfig -Config @{}
            $result | Should -BeOfType [hashtable]
            $result.ConfigSchemaVersion | Should -Be $global:CONFIG_SCHEMA_VERSION
            # Mode auto-detects; with an empty config the extensions array
            # becomes empty (it is re-validated against known extensions), so
            # the auto-detection logic sees a divergence from EasyDefaults and
            # sets Mode = 'Custom'.  The key assertion here is that the scalar
            # defaults are stamped correctly.
            $result.CleanInstall | Should -BeTrue
            $result.LaunchAfter | Should -BeTrue
            $result.SpotX_NewTheme | Should -BeTrue
            $result.SpotX_PodcastsOff | Should -BeTrue
        }

        It 'Returns all defaults for $null config' {
            # $null config is passed through; Assert-LibreSpotConfigSchemaSupported
            # handles $null gracefully (returns 0).  Mode is not asserted here
            # because the auto-detection sees the cleared extensions array as a
            # divergence from EasyDefaults.
            $result = Normalize-LibreSpotConfig -Config $null
            $result.ConfigSchemaVersion | Should -Be $global:CONFIG_SCHEMA_VERSION
        }
    }

    Context 'ConfigSchemaVersion stamping' {
        It 'Stamps ConfigSchemaVersion to the current global value' {
            $result = Normalize-LibreSpotConfig -Config @{ ConfigSchemaVersion = 0 }
            $result.ConfigSchemaVersion | Should -Be $global:CONFIG_SCHEMA_VERSION
        }

        It 'Overwrites a valid-but-older schema version in the output' {
            $result = Normalize-LibreSpotConfig -Config @{ ConfigSchemaVersion = 0; Mode = 'Easy' }
            $result.ConfigSchemaVersion | Should -Be $global:CONFIG_SCHEMA_VERSION
        }
    }

    Context 'Unknown keys are stripped' {
        It 'Does not carry over unknown keys from input' {
            $result = Normalize-LibreSpotConfig -Config @{ SomeBogusKey = 'hello'; AnotherFake = 123 }
            $result.ContainsKey('SomeBogusKey') | Should -BeFalse
            $result.ContainsKey('AnotherFake') | Should -BeFalse
        }

        It 'Still contains all expected default keys' {
            $result = Normalize-LibreSpotConfig -Config @{ SomeBogusKey = 'hello' }
            $result.ContainsKey('CleanInstall') | Should -BeTrue
            $result.ContainsKey('LaunchAfter') | Should -BeTrue
            $result.ContainsKey('SpotX_NewTheme') | Should -BeTrue
            $result.ContainsKey('Spicetify_Theme') | Should -BeTrue
        }
    }

    Context 'Boolean coercion' {
        It 'Coerces string "true" to $true for a boolean key' {
            $result = Normalize-LibreSpotConfig -Config @{ CleanInstall = 'true' }
            $result.CleanInstall | Should -BeTrue
        }

        It 'Coerces string "false" to $false for a boolean key' {
            $result = Normalize-LibreSpotConfig -Config @{ CleanInstall = 'false' }
            $result.CleanInstall | Should -BeFalse
        }

        It 'Coerces string "1" to $true for a boolean key' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_Premium = '1' }
            $result.SpotX_Premium | Should -BeTrue
        }

        It 'Coerces string "0" to $false for a boolean key' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_NewTheme = '0' }
            $result.SpotX_NewTheme | Should -BeFalse
        }

        It 'Uses the default when boolean value is unrecognized' {
            $result = Normalize-LibreSpotConfig -Config @{ CleanInstall = 'maybe' }
            # Default for CleanInstall is $true
            $result.CleanInstall | Should -BeTrue
        }
    }

    Context 'Mode handling' {
        It 'Accepts Mode = Easy' {
            $result = Normalize-LibreSpotConfig -Config @{ Mode = 'Easy' }
            $result.Mode | Should -Be 'Easy'
        }

        It 'Accepts Mode = Custom' {
            $result = Normalize-LibreSpotConfig -Config @{ Mode = 'Custom' }
            $result.Mode | Should -Be 'Custom'
        }

        It 'Falls back to Easy for invalid Mode values' {
            $result = Normalize-LibreSpotConfig -Config @{ Mode = 'Advanced' }
            $result.Mode | Should -Be 'Easy'
        }

        It 'Auto-detects Custom mode when values differ from EasyDefaults and Mode is absent' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_Premium = $true }
            # SpotX_Premium default is $false, so setting to $true triggers Custom
            $result.Mode | Should -Be 'Custom'
        }

        It 'Keeps Easy mode when all values match EasyDefaults and Mode is absent' {
            # Build a config that explicitly matches every EasyDefault so the
            # auto-detection loop finds no divergences. The extensions list
            # must be provided because the normalization always re-validates
            # it (an absent key produces an empty array, which differs from
            # the default).
            $matchingConfig = @{}
            foreach ($key in $global:EasyDefaults.Keys) {
                $matchingConfig[$key] = $global:EasyDefaults[$key]
            }
            $matchingConfig.Remove('Mode')  # ensure Mode is absent for auto-detection
            $result = Normalize-LibreSpotConfig -Config $matchingConfig
            $result.Mode | Should -Be 'Easy'
        }
    }

    Context 'Integer fields' {
        It 'Clamps SpotX_CacheLimit to valid range' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_CacheLimit = 99999 }
            $result.SpotX_CacheLimit | Should -BeLessOrEqual 50000
        }

        It 'Accepts valid SpotX_CacheLimit value' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_CacheLimit = 500 }
            $result.SpotX_CacheLimit | Should -Be 500
        }

        It 'Clamps negative SpotX_CacheLimit to 0' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_CacheLimit = -10 }
            $result.SpotX_CacheLimit | Should -Be 0
        }
    }

    Context 'SpotX_DownloadMethod validation' {
        It 'Accepts empty string' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_DownloadMethod = '' }
            $result.SpotX_DownloadMethod | Should -Be ''
        }

        It 'Accepts curl' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_DownloadMethod = 'curl' }
            $result.SpotX_DownloadMethod | Should -Be 'curl'
        }

        It 'Accepts webclient' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_DownloadMethod = 'webclient' }
            $result.SpotX_DownloadMethod | Should -Be 'webclient'
        }

        It 'Normalizes to lowercase' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_DownloadMethod = 'CURL' }
            $result.SpotX_DownloadMethod | Should -Be 'curl'
        }

        It 'Resets invalid values to empty string' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_DownloadMethod = 'wget' }
            $result.SpotX_DownloadMethod | Should -Be ''
        }
    }

    Context 'SpotX_Language validation' {
        It 'Accepts a valid language code' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_Language = 'en' }
            $result.SpotX_Language | Should -Be 'en'
        }

        It 'Accepts pt-BR' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_Language = 'pt-BR' }
            $result.SpotX_Language | Should -Be 'pt-BR'
        }

        It 'Resets invalid language to empty string' {
            $result = Normalize-LibreSpotConfig -Config @{ SpotX_Language = 'xx-FAKE' }
            $result.SpotX_Language | Should -Be ''
        }
    }

    Context 'UiCulture validation' {
        It 'Accepts supported desktop cultures' {
            foreach ($culture in @('en','ru','zh-Hans','pt-BR','es')) {
                $result = Normalize-LibreSpotConfig -Config @{ UiCulture = $culture }
                $result.UiCulture | Should -Be $culture
            }
        }

        It 'Falls back to English for unsupported cultures' {
            $result = Normalize-LibreSpotConfig -Config @{ UiCulture = 'xx-FAKE' }
            $result.UiCulture | Should -Be 'en'
        }

        It 'Does not mark an otherwise default config as Custom' {
            $config = @{}
            foreach ($key in $global:EasyDefaults.Keys) {
                $config[$key] = $global:EasyDefaults[$key]
            }
            $config.UiCulture = 'es'

            $result = Normalize-LibreSpotConfig -Config $config
            $result.Mode | Should -Be 'Easy'
        }
    }

    Context 'SpotX custom patches validation' {
        It 'Preserves enabled bounded custom patch JSON' {
            $json = '{ "xpui": { "match": "one", "replace": "two" } }'
            $result = Normalize-LibreSpotConfig -Config @{
                SpotX_CustomPatchesEnabled = $true
                SpotX_CustomPatchesJson = "  $json  "
            }

            $result.SpotX_CustomPatchesEnabled | Should -BeTrue
            $result.SpotX_CustomPatchesJson | Should -Be $json
        }
    }

    Context 'LibreSpot live engine configuration' {
        It 'keeps a bounded profile, safe snippet IDs, and feature override JSON' {
            $result = Normalize-LibreSpotConfig -Config @{
                Spicetify_CustomApps = @('librespot', 'stats', 'librespot', 'unknown')
                LibreSpot_EngineProfileJson = '{"schemaVersion":1,"name":"Desktop","theme":"Prism","scheme":"Dark","schemes":{"Dark":{"text":"ffffff"}}}'
                LibreSpot_EnabledSnippets = @('compact-sidebar', 'compact-sidebar', '../unsafe', '')
                LibreSpot_FeatureOverridesJson = '{"enableFoo":true,"limit":7}'
            }

            @($result.Spicetify_CustomApps) | Should -Be @('librespot', 'stats')
            @($result.LibreSpot_EnabledSnippets) | Should -Be @('compact-sidebar')
            $result.LibreSpot_EngineProfileJson | Should -Match '"theme":"Prism"'
            $overrides = $result.LibreSpot_FeatureOverridesJson | ConvertFrom-Json
            $overrides.enableFoo | Should -BeTrue
            $overrides.limit | Should -Be 7
        }

        It 'drops malformed engine JSON and restores an empty override object' {
            $result = Normalize-LibreSpotConfig -Config @{
                LibreSpot_EngineProfileJson = '{not-json'
                LibreSpot_FeatureOverridesJson = '[1,2,3]'
            }

            $result.LibreSpot_EngineProfileJson | Should -Be ''
            $result.LibreSpot_FeatureOverridesJson | Should -Be '{}'
        }

        It 'keeps the LibreSpot custom app in the recommended defaults' {
            $result = Normalize-LibreSpotConfig -Config @{}

            @($result.Spicetify_CustomApps) | Should -Contain 'librespot'
        }
    }

    Context 'RiskAcknowledged flag' {
        It 'Defaults RiskAcknowledged to $false' {
            $result = Normalize-LibreSpotConfig -Config @{}
            $result.RiskAcknowledged | Should -BeFalse
        }

        It 'Preserves RiskAcknowledged = $true from config' {
            $result = Normalize-LibreSpotConfig -Config @{ RiskAcknowledged = $true }
            $result.RiskAcknowledged | Should -BeTrue
        }
    }

    Context 'Sidebar/lyrics business rules' {
        It 'Forces RightSidebarClr off when RightSidebarOff is true' {
            $result = Normalize-LibreSpotConfig -Config @{
                SpotX_RightSidebarOff = $true
                SpotX_RightSidebarClr = $true
            }
            $result.SpotX_RightSidebarClr | Should -BeFalse
        }

        It 'Forces OldLyrics and LyricsBlock off when LyricsEnabled is false' {
            $result = Normalize-LibreSpotConfig -Config @{
                SpotX_LyricsEnabled = $false
                SpotX_OldLyrics     = $true
                SpotX_LyricsBlock   = $true
            }
            $result.SpotX_OldLyrics | Should -BeFalse
            $result.SpotX_LyricsBlock | Should -BeFalse
        }

        It 'Forces OldLyrics off when LyricsBlock is true (even with lyrics enabled)' {
            $result = Normalize-LibreSpotConfig -Config @{
                SpotX_LyricsEnabled = $true
                SpotX_LyricsBlock   = $true
                SpotX_OldLyrics     = $true
            }
            $result.SpotX_OldLyrics | Should -BeFalse
            $result.SpotX_LyricsBlock | Should -BeTrue
        }
    }

    Context 'Extension alias resolution' {
        It 'Resolves deprecated alias beautifulLyrics.js to beautiful-lyrics.mjs' {
            $result = Normalize-LibreSpotConfig -Config @{
                Spicetify_Extensions = @('beautifulLyrics.js')
            }
            $result.Spicetify_Extensions | Should -Contain 'beautiful-lyrics.mjs'
            $result.Spicetify_Extensions | Should -Not -Contain 'beautifulLyrics.js'
        }

        It 'Strips unknown extensions' {
            $result = Normalize-LibreSpotConfig -Config @{
                Spicetify_Extensions = @('nonexistent-ext.js', 'fullAppDisplay.js')
            }
            $result.Spicetify_Extensions | Should -Contain 'fullAppDisplay.js'
            $result.Spicetify_Extensions | Should -Not -Contain 'nonexistent-ext.js'
        }

        It 'Deduplicates extensions' {
            $result = Normalize-LibreSpotConfig -Config @{
                Spicetify_Extensions = @('fullAppDisplay.js', 'fullAppDisplay.js', 'trashbin.js')
            }
            $count = @($result.Spicetify_Extensions | Where-Object { $_ -eq 'fullAppDisplay.js' }).Count
            $count | Should -Be 1
        }
    }

    Context 'Schema version rejection' {
        It 'Throws for a future schema version' {
            $futureVersion = $global:CONFIG_SCHEMA_VERSION + 1
            { Normalize-LibreSpotConfig -Config @{ ConfigSchemaVersion = $futureVersion } } |
                Should -Throw "*newer than this LibreSpot build supports*"
        }
    }
}

# =============================================================================
# Compare-LibreSpotVersions
# =============================================================================
Describe 'Compare-LibreSpotVersions' {

    # NOTE: Compare-LibreSpotVersions returns $true when $Latest is strictly
    # newer than $Current. It returns $false otherwise (including equal).

    Context 'Equal versions' {
        It 'Returns $false for identical versions' {
            Compare-LibreSpotVersions -Latest '1.2.3' -Current '1.2.3' | Should -BeFalse
        }

        It 'Returns $false for identical four-part versions' {
            Compare-LibreSpotVersions -Latest '1.2.3.400' -Current '1.2.3.400' | Should -BeFalse
        }
    }

    Context 'Latest is greater' {
        It 'Returns $true when major is greater' {
            Compare-LibreSpotVersions -Latest '2.0.0' -Current '1.0.0' | Should -BeTrue
        }

        It 'Returns $true when minor is greater' {
            Compare-LibreSpotVersions -Latest '1.3.0' -Current '1.2.0' | Should -BeTrue
        }

        It 'Returns $true when patch is greater' {
            Compare-LibreSpotVersions -Latest '1.2.4' -Current '1.2.3' | Should -BeTrue
        }

        It 'Returns $true for four-part version comparison' {
            Compare-LibreSpotVersions -Latest '1.2.92.500' -Current '1.2.90.451' | Should -BeTrue
        }
    }

    Context 'Latest is less' {
        It 'Returns $false when major is less' {
            Compare-LibreSpotVersions -Latest '1.0.0' -Current '2.0.0' | Should -BeFalse
        }

        It 'Returns $false when minor is less' {
            Compare-LibreSpotVersions -Latest '1.1.0' -Current '1.2.0' | Should -BeFalse
        }

        It 'Returns $false when patch is less' {
            Compare-LibreSpotVersions -Latest '1.2.2' -Current '1.2.3' | Should -BeFalse
        }
    }

    Context 'Preview / pre-release suffix handling' {
        It 'Stable is newer than preview with same numeric version' {
            Compare-LibreSpotVersions -Latest '1.2.3' -Current '1.2.3-preview.1' | Should -BeTrue
        }

        It 'Preview is not newer than stable with same numeric version' {
            Compare-LibreSpotVersions -Latest '1.2.3-preview.1' -Current '1.2.3' | Should -BeFalse
        }

        It 'Higher preview number is newer' {
            Compare-LibreSpotVersions -Latest '1.2.3-preview.5' -Current '1.2.3-preview.4' | Should -BeTrue
        }

        It 'Lower preview number is not newer' {
            Compare-LibreSpotVersions -Latest '1.2.3-preview.3' -Current '1.2.3-preview.4' | Should -BeFalse
        }

        It 'Identical preview versions are not newer' {
            Compare-LibreSpotVersions -Latest '1.2.3-preview.1' -Current '1.2.3-preview.1' | Should -BeFalse
        }

        It 'Handles -rc suffix: stable beats rc' {
            Compare-LibreSpotVersions -Latest '2.0.0' -Current '2.0.0-rc.1' | Should -BeTrue
        }

        It 'Handles -rc suffix: rc does not beat stable' {
            Compare-LibreSpotVersions -Latest '2.0.0-rc.1' -Current '2.0.0' | Should -BeFalse
        }
    }

    Context 'Null and empty inputs' {
        It 'Returns $false when Latest is null/empty' {
            Compare-LibreSpotVersions -Latest '' -Current '1.0.0' | Should -BeFalse
        }

        It 'Returns $true when Current is null/empty (any latest is newer)' {
            Compare-LibreSpotVersions -Latest '1.0.0' -Current '' | Should -BeTrue
        }

        It 'Returns $false when both are null/empty' {
            Compare-LibreSpotVersions -Latest '' -Current '' | Should -BeFalse
        }

        It 'Returns $false when Latest is whitespace' {
            Compare-LibreSpotVersions -Latest '   ' -Current '1.0.0' | Should -BeFalse
        }

        It 'Returns $true when Current is whitespace' {
            Compare-LibreSpotVersions -Latest '1.0.0' -Current '   ' | Should -BeTrue
        }
    }
}

Describe 'Get-SpotXChildFailureClassification' {
    Context 'Known SpotX child-download outage signatures' {
        It 'Classifies curl exit code 28 as a child download timeout' {
            $r = Get-SpotXChildFailureClassification -Line 'Download failed: curl exit code 28 while fetching SpotifyFullSetup.exe'
            $r | Should -Not -BeNullOrEmpty
            $r.Category | Should -Be 'SpotXChildDownloadTimeout'
            $r.Guidance | Should -Match 'timed out'
        }

        It 'Classifies ERR_CONNECTION_TIMED_OUT as a child download timeout' {
            $r = Get-SpotXChildFailureClassification -Line 'GET https://download.scdn.co failed: ERR_CONNECTION_TIMED_OUT'
            $r.Category | Should -Be 'SpotXChildDownloadTimeout'
        }

        It 'Classifies the Cloudflare worker endpoint host as a worker failure' {
            $r = Get-SpotXChildFailureClassification -Line 'Error from https://loadspot.amd64fox1.workers.dev/spotify: 522'
            $r.Category | Should -Be 'SpotXWorkerEndpointFailure'
            $r.Guidance | Should -Match 'upstream'
        }

        It 'Classifies Cloudflare suspected-phishing block text' {
            $r = Get-SpotXChildFailureClassification -Line 'Warning: This website has been reported for potential phishing.'
            $r.Category | Should -Be 'SpotXMirrorBlockedPhishing'
            $r.Guidance | Should -Match 'mirror'
        }

        It 'Does not echo the raw child output in the guidance' {
            $raw = 'Error from https://loadspot.amd64fox1.workers.dev/spotify?token=secret123: 522'
            $r = Get-SpotXChildFailureClassification -Line $raw
            $r.Guidance | Should -Not -Match 'secret123'
        }
    }

    Context 'Non-matching input' {
        It 'Returns $null for unrelated output' {
            Get-SpotXChildFailureClassification -Line 'Patching xpui.js ... done' | Should -BeNullOrEmpty
        }

        It 'Returns $null for null/empty/whitespace' {
            Get-SpotXChildFailureClassification -Line $null | Should -BeNullOrEmpty
            Get-SpotXChildFailureClassification -Line '' | Should -BeNullOrEmpty
            Get-SpotXChildFailureClassification -Line '   ' | Should -BeNullOrEmpty
        }
    }
}

# =============================================================================
# Build-SpotXParams (dot-sourced from shared module)
# =============================================================================
Describe 'Build-SpotXParams' {
    BeforeAll {
        $sharedDir = Join-Path $PSScriptRoot '..\..\src\powershell\shared'
        $block = Extract-FunctionBlock (Get-Content -Path (Join-Path $sharedDir 'Build-SpotXParams.ps1') -Raw) 'Build-SpotXParams'
        Invoke-Expression $block
    }

    It 'Always includes confirm_uninstall_ms_spoti and confirm_spoti_recomended_over' {
        $config = [pscustomobject]@{}
        $result = Build-SpotXParams -Config $config
        $result | Should -Match '-confirm_uninstall_ms_spoti'
        $result | Should -Match '-confirm_spoti_recomended_over'
    }

    It 'Includes podcasts_off when config flag is set' {
        $config = [pscustomobject]@{ SpotX_PodcastsOff = $true }
        $result = Build-SpotXParams -Config $config
        $result | Should -Match '-podcasts_off'
        $result | Should -Not -Match '-podcasts_on'
    }

    It 'Includes podcasts_on when config flag is not set' {
        $config = [pscustomobject]@{ SpotX_PodcastsOff = $false }
        $result = Build-SpotXParams -Config $config
        $result | Should -Match '-podcasts_on'
        $result | Should -Not -Match '-podcasts_off'
    }

    It 'Includes lyrics flags when lyrics enabled with block' {
        $config = [pscustomobject]@{
            SpotX_LyricsEnabled = $true
            SpotX_LyricsTheme = 'spotify'
            SpotX_LyricsBlock = $true
            SpotX_OldLyrics = $false
        }
        $result = Build-SpotXParams -Config $config
        $result | Should -Match '-lyrics_stat spotify'
        $result | Should -Match '-lyrics_block'
    }

    It 'Includes version flag for non-auto version' {
        $config = [pscustomobject]@{ SpotX_SpotifyVersionId = '1.2.93' }
        $result = Build-SpotXParams -Config $config
        $result | Should -Match '-version 1\.2\.93'
    }

    It 'Excludes version flag for auto' {
        $config = [pscustomobject]@{ SpotX_SpotifyVersionId = 'auto' }
        $result = Build-SpotXParams -Config $config
        $result | Should -Not -Match '-version'
    }

    It 'Includes cache_limit when >= 500' {
        $config = [pscustomobject]@{ SpotX_CacheLimit = 1000 }
        $result = Build-SpotXParams -Config $config
        $result | Should -Match '-cache_limit 1000'
    }

    It 'Excludes cache_limit when < 500' {
        $config = [pscustomobject]@{ SpotX_CacheLimit = 100 }
        $result = Build-SpotXParams -Config $config
        $result | Should -Not -Match '-cache_limit'
    }
}

# =============================================================================
# ConvertTo-NativeArgumentString (dot-sourced from shared module)
# =============================================================================
Describe 'ConvertTo-NativeArgumentString' {
    BeforeAll {
        $sharedDir = Join-Path $PSScriptRoot '..\..\src\powershell\shared'
        . (Join-Path $sharedDir 'ConvertTo-NativeArgumentString.ps1')
    }

    It 'Passes through simple arguments unquoted' {
        ConvertTo-NativeArgumentString -Arguments @('hello', 'world') | Should -Be 'hello world'
    }

    It 'Quotes arguments with spaces' {
        ConvertTo-NativeArgumentString -Arguments @('hello world') | Should -Be '"hello world"'
    }

    It 'Escapes embedded double quotes' {
        ConvertTo-NativeArgumentString -Arguments @('say "hi"') | Should -Be '"say \"hi\""'
    }

    It 'Handles empty string argument' {
        ConvertTo-NativeArgumentString -Arguments @('') | Should -Be '""'
    }

    It 'Handles backslashes before quotes' {
        ConvertTo-NativeArgumentString -Arguments @('C:\path\"end') | Should -Be '"C:\path\\\"end"'
    }

    It 'Handles single argument' {
        ConvertTo-NativeArgumentString -Arguments @('simple') | Should -Be 'simple'
    }
}

# =============================================================================
# Confirm-FileHash (dot-sourced from shared module)
# =============================================================================
Describe 'Confirm-FileHash' {
    BeforeAll {
        $sharedDir = Join-Path $PSScriptRoot '..\..\src\powershell\shared'
        function Write-Log { param([string]$Message, [string]$Level) }
        . (Join-Path $sharedDir 'Get-FileSha256Lower.ps1')
        . (Join-Path $sharedDir 'Confirm-FileHash.ps1')
    }

    It 'Succeeds when hash matches' {
        $tempFile = [System.IO.Path]::GetTempFileName()
        try {
            [System.IO.File]::WriteAllText($tempFile, 'test content')
            $expectedHash = Get-FileSha256Lower -Path $tempFile
            { Confirm-FileHash -Path $tempFile -ExpectedHash $expectedHash -Label 'test' } | Should -Not -Throw
        } finally {
            Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
        }
    }

    It 'Throws on hash mismatch' {
        $tempFile = [System.IO.Path]::GetTempFileName()
        try {
            [System.IO.File]::WriteAllText($tempFile, 'test content')
            { Confirm-FileHash -Path $tempFile -ExpectedHash 'aaaa' -Label 'test' } | Should -Throw '*hash mismatch*'
        } finally {
            Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
        }
    }

    It 'Skips verification when no hash is provided' {
        { Confirm-FileHash -Path 'nonexistent' -ExpectedHash '' -Label 'test' } | Should -Not -Throw
        { Confirm-FileHash -Path 'nonexistent' -ExpectedHash $null -Label 'test' } | Should -Not -Throw
    }
}

# =============================================================================
# Get-SpotXDownloadRetryPlan (dot-sourced from shared module)
# =============================================================================
Describe 'Get-SpotXDownloadRetryPlan' {
    BeforeAll {
        $sharedDir = Join-Path $PSScriptRoot '..\..\src\powershell\shared'
        . (Join-Path $sharedDir 'Get-SpotXDownloadRetryPlan.ps1')
    }

    It 'Retries a timeout through the mirror when the mirror was not used' {
        $plan = Get-SpotXDownloadRetryPlan -Category 'SpotXChildDownloadTimeout' -MirrorAlreadyUsed $false
        $plan | Should -Not -BeNullOrEmpty
        $plan.UseMirror | Should -BeTrue
    }

    It 'Retries a worker-endpoint failure through the mirror when the mirror was not used' {
        $plan = Get-SpotXDownloadRetryPlan -Category 'SpotXWorkerEndpointFailure' -MirrorAlreadyUsed $false
        $plan | Should -Not -BeNullOrEmpty
        $plan.UseMirror | Should -BeTrue
    }

    It 'Does not retry a timeout when the mirror was already used (no useful toggle)' {
        Get-SpotXDownloadRetryPlan -Category 'SpotXChildDownloadTimeout' -MirrorAlreadyUsed $true | Should -BeNullOrEmpty
    }

    It 'Does not retry a worker-endpoint failure when the mirror was already used' {
        Get-SpotXDownloadRetryPlan -Category 'SpotXWorkerEndpointFailure' -MirrorAlreadyUsed $true | Should -BeNullOrEmpty
    }

    It 'Retries a phishing-blocked mirror without the mirror when the mirror was used' {
        $plan = Get-SpotXDownloadRetryPlan -Category 'SpotXMirrorBlockedPhishing' -MirrorAlreadyUsed $true
        $plan | Should -Not -BeNullOrEmpty
        $plan.UseMirror | Should -BeFalse
    }

    It 'Does not retry a phishing block when the mirror was not used (nothing to disable)' {
        Get-SpotXDownloadRetryPlan -Category 'SpotXMirrorBlockedPhishing' -MirrorAlreadyUsed $false | Should -BeNullOrEmpty
    }

    It 'Returns null for an unknown or non-download category' {
        Get-SpotXDownloadRetryPlan -Category 'SomethingElse' -MirrorAlreadyUsed $false | Should -BeNullOrEmpty
        Get-SpotXDownloadRetryPlan -Category '' -MirrorAlreadyUsed $false | Should -BeNullOrEmpty
    }
}

# =============================================================================
# Spicetify state preservation
# =============================================================================
Describe 'Spicetify state preservation' {
    BeforeEach {
        $script:preservationRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LibreSpot.Preservation.Tests\" + [Guid]::NewGuid().ToString('N'))
        $global:BACKUP_ROOT = Join-Path $script:preservationRoot 'backups'
        $global:CONFIG_DIR = Join-Path $script:preservationRoot 'librespot'
        $global:TEMP_DIR = Join-Path $script:preservationRoot 'temp'
        $global:CURRENT_OPERATION_ID = '11111111222233334444555555555555'
        $script:spicetifyConfigDirectory = Join-Path $script:preservationRoot 'spicetify'
        $script:spicetifyConfigPath = Join-Path $script:spicetifyConfigDirectory 'config-xpui.ini'
        $script:customAppsDirectory = Join-Path $script:spicetifyConfigDirectory 'CustomApps'
        $script:journalEntries = @()

        function Get-SpicetifyIntegrationContext {
            return [pscustomobject]@{
                ConfigPath = $script:spicetifyConfigPath
                CustomAppsDirectory = $script:customAppsDirectory
                MarketplaceDirectory = Join-Path $script:customAppsDirectory 'marketplace'
                LegacyMarketplaceDirectory = Join-Path $script:preservationRoot 'legacy-marketplace'
            }
        }
        function Get-SpicetifyConfigListValue { return @('marketplace', 'foreign-app') }
        function Get-MarketplaceHealth {
            return [pscustomobject]@{
                Status = 'Ready'
                IsReady = $true
                Path = Join-Path $script:customAppsDirectory 'marketplace'
                BrowserStorage = [pscustomobject]@{
                    storageModel = 'indexeddb'
                    databaseName = 'spicetify-marketplace'
                    objectStore = 'settings'
                    status = 'detected-not-backed-up'
                    detectionOnly = $true
                    fileLevelBackup = 'not-validated'
                    exported = $false
                    restored = $false
                    recovery = "Use Marketplace's own export/import controls before a repair or reset."
                }
            }
        }
        function Write-OperationJournalEntry {
            param($Phase, $Target, $SafetyDecision, $Result, $WouldChange, $Reversible, $RollbackHint, $Data)
            $script:journalEntries += [pscustomobject]@{ Phase = $Phase; Target = $Target; Result = $Result; Data = $Data }
        }
        function Write-Log { param($Message, $Level) }
        function Remove-PathSafely {
            param($Path, $Label)
            if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
            return $true
        }

        New-Item -Path (Join-Path $script:customAppsDirectory 'marketplace') -ItemType Directory -Force | Out-Null
        New-Item -Path (Join-Path $script:customAppsDirectory 'foreign-app') -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath $script:spicetifyConfigPath -Value 'custom_apps = marketplace|foreign-app' -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\extension.js') -Value 'old-runtime' -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\user-state.json') -Value '{"kept":true}' -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $script:customAppsDirectory 'foreign-app\settings.json') -Value '{"foreign":true}' -Encoding UTF8
    }

    AfterEach {
        Remove-Item -LiteralPath $script:preservationRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Keeps refreshed package files and restores only missing state' {
        $snapshot = New-SpicetifyStatePreservationSnapshot -Action 'RepairMarketplace'

        Test-Path -LiteralPath (Join-Path $snapshot.snapshotPath 'config-xpui.ini') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $snapshot.snapshotPath 'CustomApps\marketplace\user-state.json') | Should -BeTrue
        $snapshot.enabledCustomApps | Should -Contain 'foreign-app'

        Set-Content -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\extension.js') -Value 'fresh-runtime' -Encoding UTF8
        Remove-Item -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\user-state.json') -Force
        Remove-Item -LiteralPath (Join-Path $script:customAppsDirectory 'foreign-app') -Recurse -Force

        $recovery = Restore-SpicetifyStatePreservationSnapshot -Snapshot $snapshot -OperationSucceeded $true

        $recovery.Succeeded | Should -BeTrue
        (Get-Content -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\extension.js') -Raw).Trim() | Should -Be 'fresh-runtime'
        Test-Path -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\user-state.json') | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $script:customAppsDirectory 'foreign-app\settings.json') | Should -BeTrue
        $evidence = Get-Content -LiteralPath (Join-Path $global:CONFIG_DIR 'spicetify-preservation-latest.json') -Raw | ConvertFrom-Json
        $evidence.status | Should -Be 'PreservedAfterSuccess'
        $script:journalEntries.Result | Should -Contain 'Preserved'
        $script:journalEntries.Result | Should -Contain 'PreservedAfterSuccess'
    }

    It 'Exports a validated file manifest without claiming IndexedDB portability' {
        $export = Export-MarketplaceState

        $export.Succeeded | Should -BeTrue
        Test-Path -LiteralPath $export.Path -PathType Leaf | Should -BeTrue
        $export.BrowserStorageExported | Should -BeFalse
        $export.BrowserStorageStatus | Should -Be 'detected-not-backed-up'

        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($export.Path)
        try {
            $manifestEntry = $zip.GetEntry('marketplace-state-manifest.json')
            $manifestEntry | Should -Not -BeNullOrEmpty
            $manifest = [System.IO.StreamReader]::new($manifestEntry.Open()).ReadToEnd() | ConvertFrom-Json
            $manifest.format | Should -Be 'LibreSpot.MarketplaceState'
            $manifest.browserStorage.exported | Should -BeFalse
            $manifest.browserStorage.storageModel | Should -Be 'indexeddb'
            $manifest.browserStorage.databaseName | Should -Be 'spicetify-marketplace'
            $manifest.browserStorage.status | Should -Be 'detected-not-backed-up'
            $manifest.browserStorage.detectionOnly | Should -BeTrue
            $manifest.restoration.behavior | Should -Be 'missing-files-only'
            ($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }) | Should -Contain 'CustomApps/marketplace/user-state.json'
        } finally {
            $zip.Dispose()
        }
    }

    It 'Restores missing Marketplace files from the latest archive without overwriting refreshed files' {
        $export = Export-MarketplaceState
        Set-Content -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\extension.js') -Value 'fresh-runtime' -Encoding UTF8
        Remove-Item -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\user-state.json') -Force

        $restore = Restore-MarketplaceState

        $restore.Succeeded | Should -BeTrue
        $restore.BrowserStorageRestored | Should -BeFalse
        $restore.RestoredFileCount | Should -BeGreaterThan 0
        (Get-Content -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\extension.js') -Raw).Trim() | Should -Be 'fresh-runtime'
        Test-Path -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\user-state.json') | Should -BeTrue
        $evidence = Get-Content -LiteralPath (Join-Path $global:CONFIG_DIR 'marketplace-state-recovery-latest.json') -Raw | ConvertFrom-Json
        $evidence.status | Should -Be 'RestoredMissingFiles'
        $evidence.browserStorage.restored | Should -BeFalse
        $evidence.browserStorage.status | Should -Be 'detected-not-backed-up'
        $evidence.browserStorage.recovery | Should -Match 'export/import'
        $export.Path | Should -Be $restore.Path
    }

    It 'Recovers missing state when the wrapped operation fails' {
        {
            Invoke-WithSpicetifyStatePreservation -Action 'Reapply' -Operation {
                Remove-Item -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\user-state.json') -Force
                throw 'simulated reapply failure'
            }
        } | Should -Throw '*simulated reapply failure*'

        Test-Path -LiteralPath (Join-Path $script:customAppsDirectory 'marketplace\user-state.json') | Should -BeTrue
        $evidence = Get-Content -LiteralPath (Join-Path $global:CONFIG_DIR 'spicetify-preservation-latest.json') -Raw | ConvertFrom-Json
        $evidence.status | Should -Be 'RecoveredAfterFailure'
        $evidence.operationSucceeded | Should -BeFalse
    }

    It 'Blocks oversized snapshots before a caller can mutate state' {
        $destination = Join-Path $script:preservationRoot 'too-small'
        {
            Copy-DirectorySnapshotSafely -SourcePath $script:customAppsDirectory -DestinationPath $destination -MaxBytes 4
        } | Should -Throw '*preservation limit*'
    }
}

# =============================================================================
# Lane-specific auto-reapply watcher
# =============================================================================
Describe 'Lane-specific auto-reapply watcher' {
    BeforeEach {
        $script:watcherRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LibreSpot.Watcher.Tests\" + [Guid]::NewGuid().ToString('N'))
        $global:CONFIG_DIR = Join-Path $script:watcherRoot 'config'
        $global:WATCHER_STATE_PATH = Join-Path $global:CONFIG_DIR 'watcher-state.json'
        $global:WATCHER_TASK_NAME = 'LibreSpot\PesterWatcher'
        $script:watcherWrites = @()
        New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null

        function Write-WatcherLog { param($Message, $Level) }
        function Write-OperationJournalEntry { param($Phase, $Target, $SafetyDecision, $Result, $WouldChange, $Reversible, $RollbackHint) }
        function Unregister-AutoReapplyTask { return $true }
    }

    AfterEach {
        Remove-Item -LiteralPath $script:watcherRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Builds a hidden PowerShell launch command for a durable script entry point' {
        $entry = Join-Path $script:watcherRoot 'LibreSpot.ps1'
        Set-Content -LiteralPath $entry -Value '# test entry' -Encoding UTF8
        $script:EntryCommandPath = $entry

        $launch = Get-WatcherLaunchCommand

        $launch.Entry | Should -Be $entry
        $launch.Arguments | Should -Match '-NoProfile'
        $launch.Arguments | Should -Match '-WindowStyle Hidden'
        $launch.Arguments | Should -Match '-Watch'
    }

    It 'Builds least-privilege task XML without mutating under WhatIf' {
        $entry = Join-Path $script:watcherRoot 'LibreSpot.ps1'
        Set-Content -LiteralPath $entry -Value '# test entry' -Encoding UTF8
        $script:EntryCommandPath = $entry

        Register-AutoReapplyTask -WhatIf | Should -BeFalse
    }

    It 'Initializes a first watcher tick without reapplying' {
        function Get-InstalledSpotifyVersion { return '2.0.0.0' }
        function Get-WatcherState { return @{ LastKnownVersion = $null } }
        function Set-WatcherState { param($State) $script:watcherWrites += $State }
        function Test-SpotifyRunning { return $false }

        Invoke-AutoReapplyWatcher | Should -Be 0
        $script:watcherWrites[-1].LastOutcome | Should -Be 'Initialized'
    }

    It 'Honors the disabled config gate after a version change' {
        function Get-InstalledSpotifyVersion { return '2.0.0.0' }
        function Get-WatcherState { return @{ LastKnownVersion = '1.0.0.0' } }
        function Set-WatcherState { param($State) $script:watcherWrites += $State }
        function Test-SpotifyRunning { return $false }
        function Load-LibreSpotConfig { return @{ AutoReapply_Enabled = $false } }
        function Normalize-LibreSpotConfig { param($Config) return $Config }

        Invoke-AutoReapplyWatcher | Should -Be 0
        $script:watcherWrites[-1].LastOutcome | Should -Be 'PreferenceOff'
    }

    It 'Defers while Spotify is active and retains the old version' {
        function Get-InstalledSpotifyVersion { return '2.0.0.0' }
        function Get-WatcherState { return @{ LastKnownVersion = '1.0.0.0' } }
        function Set-WatcherState { param($State) $script:watcherWrites += $State }
        function Test-SpotifyRunning { return $true }

        Invoke-AutoReapplyWatcher | Should -Be 0
        $script:watcherWrites[-1].LastOutcome | Should -Be 'DeferredSpotifyRunning'
        $script:watcherWrites[-1].LastKnownVersion | Should -Be '1.0.0.0'
    }

    It 'Retains the old version when the reapply boundary fails' {
        function Get-InstalledSpotifyVersion { return '2.0.0.0' }
        function Get-WatcherState { return @{ LastKnownVersion = '1.0.0.0' } }
        function Set-WatcherState { param($State) $script:watcherWrites += $State }
        function Test-SpotifyRunning { return $false }
        function Load-LibreSpotConfig { return @{ AutoReapply_Enabled = $true } }
        function Normalize-LibreSpotConfig { param($Config) return $Config }
        function Invoke-HeadlessReapply { throw 'Synthetic network failure.' }

        Invoke-AutoReapplyWatcher | Should -Be 1
        $script:watcherWrites[-1].LastKnownVersion | Should -Be '1.0.0.0'
        $script:watcherWrites[-1].LastOutcome | Should -Match '^Error:'
    }

    It 'Rejects a headless reapply without config before touching temp state' {
        { Invoke-HeadlessReapply -Config $null } | Should -Throw '*missing config*'
    }

    It 'Leaves the prior state intact when its atomic replacement is interrupted' {
        $original = '{"LastKnownVersion":"1.0.0.0","LastOutcome":"Seeded"}'
        [System.IO.File]::WriteAllText($global:WATCHER_STATE_PATH, $original)
        $lock = [System.IO.File]::Open(
            $global:WATCHER_STATE_PATH,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        try {
            Set-WatcherState -State @{ LastKnownVersion = '2.0.0.0'; LastOutcome = 'Reapplied' }
        } finally {
            $lock.Dispose()
        }

        [System.IO.File]::ReadAllText($global:WATCHER_STATE_PATH) | Should -Be $original
        @(Get-ChildItem -LiteralPath $global:CONFIG_DIR -Force | Where-Object Name -Match '^watcher-state\..+\.(tmp|bak|rescue)$').Count | Should -Be 0
    }
}

Describe 'Get-SpicetifyCliMajorVersion' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyCliMajorVersion.ps1')
    }

    It 'Parses the leading major from a 2.x version' {
        Get-SpicetifyCliMajorVersion -Version '2.44.0' | Should -Be 2
    }

    It 'Parses a v-prefixed and pre-release v3 version' {
        Get-SpicetifyCliMajorVersion -Version 'v3.1.2-dev' | Should -Be 3
    }

    It 'Returns $null for empty input' {
        Get-SpicetifyCliMajorVersion -Version '' | Should -Be $null
    }

    It 'Returns $null for non-numeric input' {
        Get-SpicetifyCliMajorVersion -Version 'Dev' | Should -Be $null
    }
}

Describe 'Test-SpicetifyCliVersionSupported' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyCliMajorVersion.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Test-SpicetifyCliVersionSupported.ps1')
    }

    It 'Supports the pinned 2.x line' {
        Test-SpicetifyCliVersionSupported -Version '2.44.0' | Should -BeTrue
    }

    It 'Rejects a future v3 major' {
        Test-SpicetifyCliVersionSupported -Version '3.0.0' | Should -BeFalse
    }

    It 'Rejects any newer major' {
        Test-SpicetifyCliVersionSupported -Version '4.1.0' | Should -BeFalse
    }

    It 'Treats an unknown version as supported (never a false warning)' {
        Test-SpicetifyCliVersionSupported -Version $null | Should -BeTrue
        Test-SpicetifyCliVersionSupported -Version 'Dev' | Should -BeTrue
    }
}

Describe 'Get-SpicetifyV3Conflict' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyIntegrationContext.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyCliMajorVersion.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyV3Conflict.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Invoke-SpicetifyCli.ps1')
    }

    BeforeEach {
        $script:v3ConflictRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-v3-" + [Guid]::NewGuid().ToString('N'))
        $global:SPOTIFY_EXE_PATH = Join-Path $script:v3ConflictRoot 'Spotify\Spotify.exe'
        $global:SPICETIFY_DIR = Join-Path $script:v3ConflictRoot 'spicetify'
        $global:SPICETIFY_CONFIG_DIR = Join-Path $script:v3ConflictRoot 'spicetify-config'
        New-Item -Path (Join-Path $script:v3ConflictRoot 'Spotify\Apps') -ItemType Directory -Force | Out-Null
        New-Item -Path $global:SPICETIFY_DIR -ItemType Directory -Force | Out-Null
        New-Item -Path $global:SPICETIFY_CONFIG_DIR -ItemType Directory -Force | Out-Null
    }

    AfterEach {
        Remove-Item -LiteralPath $script:v3ConflictRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'detects the v3 xpui backup marker and gives the safe restore command' {
        New-Item -Path (Join-Path $script:v3ConflictRoot 'Spotify\Apps\xpui.spa.backup') -ItemType File -Force | Out-Null

        $report = Get-SpicetifyV3Conflict -CliVersion '2.44.0'

        $report.IsConflict | Should -BeTrue
        $report.Markers | Should -Contain 'Apps\xpui.spa.backup'
        $report.Message | Should -Match "spicetify restore"
    }

    It 'detects v3 layout directories and a newer CLI major' {
        New-Item -Path (Join-Path $global:SPICETIFY_DIR 'modules') -ItemType Directory -Force | Out-Null
        New-Item -Path (Join-Path $global:SPICETIFY_CONFIG_DIR 'hooks') -ItemType Directory -Force | Out-Null

        $report = Get-SpicetifyV3Conflict -CliVersion '3.0.0-beta.1'

        $report.IsConflict | Should -BeTrue
        $report.Markers | Should -Contain 'spicetify install\modules'
        $report.Markers | Should -Contain 'spicetify config\hooks'
        $report.Markers | Should -Contain 'Spicetify CLI major 3'
    }

    It 'leaves the pinned v2 layout ready' {
        $report = Get-SpicetifyV3Conflict -CliVersion '2.44.0'

        $report.IsConflict | Should -BeFalse
        @($report.Markers).Count | Should -Be 0
    }

    It 'refuses a mutating CLI command before a native process starts' {
        New-Item -Path (Join-Path $script:v3ConflictRoot 'Spotify\Apps\xpui.spa.backup') -ItemType File -Force | Out-Null

        { Invoke-SpicetifyCli -Arguments @('backup', 'apply') } | Should -Throw '*spicetify restore*'
    }

    It 'allows the recovery command through the conflict gate' {
        New-Item -Path (Join-Path $script:v3ConflictRoot 'Spotify\Apps\xpui.spa.backup') -ItemType File -Force | Out-Null

        { Invoke-SpicetifyCli -Arguments @('restore') } | Should -Throw '*Spicetify CLI is not installed*'
    }
}

Describe 'Get-SpicetifyV3SupportContract' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyCliMajorVersion.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyV3SupportContract.ps1')
        $script:v3SupportFixture = Join-Path $PSScriptRoot '..\..\schemas\spicetify-supported-versions-v2.json'
    }

    It 'does not activate the v3 contract for the pinned v2 CLI' {
        $report = Get-SpicetifyV3SupportContract `
            -CliVersion '2.44.0' `
            -SpotifyVersion '1.2.95' `
            -SupportListPath (Join-Path $TestDrive 'missing.json')

        $report.FeatureActive | Should -BeFalse
        $report.Verdict | Should -Be 'not-applicable'
        $report.CanApply | Should -BeTrue
    }

    It 'classifies a v3 candidate with a lower modular fallback as degraded' {
        $report = Get-SpicetifyV3SupportContract `
            -CliVersion '3.0.0-beta.1' `
            -SpotifyVersion '1.2.95' `
            -SupportListPath $script:v3SupportFixture

        $report.FeatureActive | Should -BeTrue
        $report.Verdict | Should -Be 'degraded'
        $report.FallbackVersion | Should -Be '1.2.94'
        $report.CanApply | Should -BeTrue
        $report.CanAutoApply | Should -BeTrue
    }

    It 'refuses an unsupported v3 version without a same-minor fallback' {
        $report = Get-SpicetifyV3SupportContract `
            -CliVersion '3.0.0-beta.1' `
            -SpotifyVersion '1.2.69' `
            -SupportListPath $script:v3SupportFixture

        $report.Verdict | Should -Be 'refused'
        $report.CanApply | Should -BeFalse
        $report.CanAutoApply | Should -BeFalse
    }

    It 'fails closed when a detected v3 CLI has no support document' {
        $report = Get-SpicetifyV3SupportContract `
            -CliVersion '3.0.0-beta.1' `
            -SpotifyVersion '1.2.95' `
            -SupportListPath (Join-Path $TestDrive 'missing.json')

        $report.FeatureActive | Should -BeTrue
        $report.ListAvailable | Should -BeFalse
        $report.Verdict | Should -Be 'unknown'
        $report.CanApply | Should -BeFalse
        $report.CanAutoApply | Should -BeFalse
        $report.Reason | Should -Match 'spicetify restore'
    }

    It 'fails closed when a detected v3 CLI has malformed support data' {
        $malformedPath = Join-Path $TestDrive 'malformed-supported-versions.json'
        Set-Content -LiteralPath $malformedPath -Value '{"schema_version":2' -NoNewline

        $report = Get-SpicetifyV3SupportContract `
            -CliVersion '3.0.0-beta.1' `
            -SpotifyVersion '1.2.95' `
            -SupportListPath $malformedPath

        $report.FeatureActive | Should -BeTrue
        $report.ListAvailable | Should -BeFalse
        $report.Verdict | Should -Be 'unknown'
        $report.CanApply | Should -BeFalse
        $report.CanAutoApply | Should -BeFalse
        $report.Reason | Should -Match 'spicetify restore'
    }

    It 'allows an allowlisted Spotify version from the support document' {
        $report = Get-SpicetifyV3SupportContract `
            -CliVersion '3.0.0-beta.1' `
            -SpotifyVersion '1.2.94' `
            -SupportListPath $script:v3SupportFixture

        $report.FeatureActive | Should -BeTrue
        $report.ListAvailable | Should -BeTrue
        $report.Verdict | Should -Be 'allowlisted'
        $report.CanApply | Should -BeTrue
        $report.CanAutoApply | Should -BeTrue
    }
}

Describe 'Marketplace theme contract' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyIntegrationContext.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyConfigEntries.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyConfigListValue.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-MarketplaceHealth.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Install-MarketplacePlaceholderTheme.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Install-MarketplaceNavFallbackExtension.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Test-SpicetifyCustomAppRouteWiring.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Repair-SpicetifyCustomAppWiring.ps1')
        if (-not (Get-Command Write-Log -ErrorAction SilentlyContinue)) {
            function global:Write-Log { param([string]$Message, [string]$Level = 'INFO') }
        }
    }

    BeforeEach {
        $script:testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-mp-" + [Guid]::NewGuid().ToString('N'))
        $global:SPICETIFY_DIR = Join-Path $script:testRoot 'cli'
        $global:SPICETIFY_CONFIG_DIR = Join-Path $script:testRoot 'config'
        # Keep the route-wiring probe hermetic (never touch a real Spotify install).
        $global:SPOTIFY_EXE_PATH = $null
        New-Item -Path $global:SPICETIFY_CONFIG_DIR -ItemType Directory -Force | Out-Null
    }

    AfterEach {
        Remove-Item -LiteralPath $script:testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'Install-MarketplacePlaceholderTheme writes the upstream placeholder color.ini' {
        $themeDir = Install-MarketplacePlaceholderTheme
        $colorIni = Join-Path $themeDir 'color.ini'
        Test-Path $colorIni | Should -BeTrue
        ([System.IO.File]::ReadAllText($colorIni)).Trim() | Should -Be '[Marketplace]'
    }

    It 'Install-MarketplacePlaceholderTheme is idempotent' {
        $themeDir = Install-MarketplacePlaceholderTheme
        $colorIni = Join-Path $themeDir 'color.ini'
        $firstWrite = (Get-Item $colorIni).LastWriteTimeUtc
        Start-Sleep -Milliseconds 50
        Install-MarketplacePlaceholderTheme | Out-Null
        (Get-Item $colorIni).LastWriteTimeUtc | Should -Be $firstWrite
    }

    It 'Install-MarketplaceNavFallbackExtension writes a guarded Topbar fallback' {
        $name = Install-MarketplaceNavFallbackExtension
        $name | Should -Be 'librespot-marketplace-button.js'
        $path = Join-Path (Get-SpicetifyIntegrationContext).ExtensionsDirectory $name
        Test-Path $path | Should -BeTrue
        $js = [System.IO.File]::ReadAllText($path)
        $js | Should -Match 'Spicetify\.Topbar\.Button'
        $js | Should -Match 'History\.push\("/marketplace"\)'
        $js | Should -Match 'navEntryPresent'
        # Never registers when a native entry already rendered.
        $js | Should -Match 'if \(navEntryPresent\(\)\) \{ return; \}'
    }

    It 'Get-MarketplaceHealth reports ThemeInactive when marketplace is on but no theme is active' {
        $marketplaceDir = Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps\marketplace'
        New-Item -Path $marketplaceDir -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $marketplaceDir 'extension.js') -Value 'x'
        Set-Content -LiteralPath (Join-Path $marketplaceDir 'manifest.json') -Value '{}'
        Set-Content -LiteralPath (Join-Path $global:SPICETIFY_CONFIG_DIR 'config-xpui.ini') -Value "custom_apps = marketplace`ncurrent_theme = `ninject_css = 0"

        $health = Get-MarketplaceHealth
        $health.Status | Should -Be 'ThemeInactive'
        $health.ThemeContractReady | Should -BeFalse
        $health.IsReady | Should -BeFalse
        $health.NeedsRepair | Should -BeTrue
    }

    It 'Get-MarketplaceHealth reports Ready when the placeholder theme contract is satisfied' {
        $marketplaceDir = Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps\marketplace'
        New-Item -Path $marketplaceDir -ItemType Directory -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $marketplaceDir 'extension.js') -Value 'x'
        Set-Content -LiteralPath (Join-Path $marketplaceDir 'manifest.json') -Value '{}'
        Set-Content -LiteralPath (Join-Path $global:SPICETIFY_CONFIG_DIR 'config-xpui.ini') -Value "custom_apps = marketplace`ncurrent_theme = marketplace`ninject_css = 1"

        $health = Get-MarketplaceHealth
        $health.Status | Should -Be 'Ready'
        $health.ThemeContractReady | Should -BeTrue
        $health.CurrentTheme | Should -Be 'marketplace'
        $health.IsReady | Should -BeTrue
        $health.NeedsRepair | Should -BeFalse
    }

    It 'Get-MarketplaceHealth reports the IndexedDB boundary as detected only' {
        $health = Get-MarketplaceHealth

        $health.BrowserStorage.storageModel | Should -Be 'indexeddb'
        $health.BrowserStorage.databaseName | Should -Be 'spicetify-marketplace'
        $health.BrowserStorage.objectStore | Should -Be 'settings'
        $health.BrowserStorage.status | Should -Be 'detected-not-backed-up'
        $health.BrowserStorage.detectionOnly | Should -BeTrue
        $health.BrowserStorage.exported | Should -BeFalse
        $health.BrowserStorage.recovery | Should -Match 'export/import'
    }
}

Describe 'Marketplace route wiring (SpotX xpui.js layout)' {
    BeforeAll {
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Test-SpicetifyCustomAppRouteWiring.ps1')
        . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Repair-SpicetifyCustomAppWiring.ps1')
        if (-not (Get-Command Write-Log -ErrorAction SilentlyContinue)) {
            function global:Write-Log { param([string]$Message, [string]$Level = 'INFO') }
        }

        function script:New-WiringFixture {
            param(
                [string]$IndexHtml,
                [string]$BundleJs,
                [switch]$SkipRouteBundle,
                [string[]]$RouteApps = @('marketplace')
            )
            $root = Join-Path ([System.IO.Path]::GetTempPath()) ("librespot-wiring-" + [Guid]::NewGuid().ToString('N'))
            $xpui = Join-Path $root 'xpui'
            New-Item -Path $xpui -ItemType Directory -Force | Out-Null
            [System.IO.File]::WriteAllText((Join-Path $xpui 'index.html'), $IndexHtml)
            if ($null -ne $BundleJs) {
                [System.IO.File]::WriteAllText((Join-Path $xpui 'xpui.js'), $BundleJs)
            }
            if (-not $SkipRouteBundle) {
                foreach ($routeApp in $RouteApps) {
                    [System.IO.File]::WriteAllText((Join-Path $xpui "spicetify-routes-$routeApp.js"), '/* chunk */')
                }
            }
            return $root
        }

        # Minimal synthetic bundle carrying every anchor the shim ports from
        # the Spicetify CLI (react lazy, settings route, chunk-name maps, css gate).
        $script:AnchoredBundle = 'var rK=b.lazy((()=>i.e(4961).then(i.bind(i,10418))));var Z=[(0,m.jsx)(eg.qh,{path:"/settings",element:(0,m.jsx)(f7,{})})];l.u=e=>""+(({123:"xpui-routes-a"})[e]||e)+".js",l.miniCssF=e=>""+(({123:"xpui-routes-a"})[e]||e)+".css";l.f.miniCss=function(e,t){if(h[e])t.push(h[e]);else 0!==h[e]&&({123:1,456:1})[e]&&t.push(h[e]=x(e))};'
        $script:LiveIndexHtml = '<html><body><script defer="defer" src="/xpui.js"></script></body></html>'
    }

    AfterEach {
        if ($script:fixtureRoot) {
            Remove-Item -LiteralPath $script:fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
            $script:fixtureRoot = $null
        }
    }

    It 'detects NotWired when the live xpui.js never references the store chunk' {
        $script:fixtureRoot = New-WiringFixture -IndexHtml $script:LiveIndexHtml -BundleJs $script:AnchoredBundle
        $state = Test-SpicetifyCustomAppRouteWiring -AppsDirectory $script:fixtureRoot
        $state.State | Should -Be 'NotWired'
        $state.RouteBundlePresent | Should -BeTrue
    }

    It 'reports NotApplicable for the CLI-supported xpui-snapshot layout' {
        $snapshotIndex = '<html><body><script defer="defer" src="/xpui-modules.js"></script><script defer="defer" src="/xpui-snapshot.js"></script></body></html>'
        $script:fixtureRoot = New-WiringFixture -IndexHtml $snapshotIndex -BundleJs $script:AnchoredBundle
        (Test-SpicetifyCustomAppRouteWiring -AppsDirectory $script:fixtureRoot).State | Should -Be 'NotApplicable'
        (Repair-SpicetifyCustomAppWiring -AppsDirectory $script:fixtureRoot).Status | Should -Be 'NotApplicable'
    }

    It 'patches the live bundle with the route, lazy loader, and chunk maps' {
        $script:fixtureRoot = New-WiringFixture -IndexHtml $script:LiveIndexHtml -BundleJs $script:AnchoredBundle
        $result = Repair-SpicetifyCustomAppWiring -AppsDirectory $script:fixtureRoot
        $result.Status | Should -Be 'Patched'

        $patched = [System.IO.File]::ReadAllText((Join-Path $script:fixtureRoot 'xpui\xpui.js'))
        # Lazy component appended after the anchor expression with the same symbols.
        $patched | Should -Match ([regex]::Escape(',spicetifyApp0=b.lazy((()=>i.e("spicetify-routes-marketplace").then(i.bind(i,"spicetify-routes-marketplace"))))'))
        # Route mounted before the settings route with wildcard paths.
        $patched | Should -Match ([regex]::Escape('(0,m.jsx)(eg.qh,{path:"/marketplace/*",pathV6:"/marketplace/*",element:(0,m.jsx)(spicetifyApp0,{})}),'))
        # Chunk-name maps and the css gate learned the new chunk.
        $patched | Should -Match ([regex]::Escape('l.u=e=>""+(({"spicetify-routes-marketplace":"spicetify-routes-marketplace",123:"xpui-routes-a"})'))
        $patched | Should -Match ([regex]::Escape('l.miniCssF=e=>""+(({"spicetify-routes-marketplace":"spicetify-routes-marketplace",123:"xpui-routes-a"})'))
        $patched | Should -Match ([regex]::Escape('({123:1,456:1,"spicetify-routes-marketplace":1})[e]'))
        # Pre-patch backup kept alongside.
        Test-Path (Join-Path $script:fixtureRoot 'xpui\xpui.js.librespot.bak') | Should -BeTrue
    }

    It 'is idempotent: a second repair reports Wired without rewriting' {
        $script:fixtureRoot = New-WiringFixture -IndexHtml $script:LiveIndexHtml -BundleJs $script:AnchoredBundle
        (Repair-SpicetifyCustomAppWiring -AppsDirectory $script:fixtureRoot).Status | Should -Be 'Patched'
        $bundlePath = Join-Path $script:fixtureRoot 'xpui\xpui.js'
        $firstWrite = (Get-Item $bundlePath).LastWriteTimeUtc
        Start-Sleep -Milliseconds 50
        (Repair-SpicetifyCustomAppWiring -AppsDirectory $script:fixtureRoot).Status | Should -Be 'Wired'
        (Get-Item $bundlePath).LastWriteTimeUtc | Should -Be $firstWrite
    }

    It 'assigns a distinct lazy component to every managed custom-app route' {
        $script:fixtureRoot = New-WiringFixture -IndexHtml $script:LiveIndexHtml -BundleJs $script:AnchoredBundle -RouteApps @('marketplace', 'librespot')
        (Repair-SpicetifyCustomAppWiring -AppsDirectory $script:fixtureRoot -AppName 'marketplace').Status | Should -Be 'Patched'
        (Repair-SpicetifyCustomAppWiring -AppsDirectory $script:fixtureRoot -AppName 'librespot').Status | Should -Be 'Patched'

        $patched = [System.IO.File]::ReadAllText((Join-Path $script:fixtureRoot 'xpui\xpui.js'))
        $patched | Should -Match ([regex]::Escape('spicetifyApp0=b.lazy((()=>i.e("spicetify-routes-marketplace")'))
        $patched | Should -Match ([regex]::Escape('spicetifyApp1=b.lazy((()=>i.e("spicetify-routes-librespot")'))
        $patched | Should -Match ([regex]::Escape('path:"/marketplace/*",pathV6:"/marketplace/*",element:(0,m.jsx)(spicetifyApp0,{})'))
        $patched | Should -Match ([regex]::Escape('path:"/librespot/*",pathV6:"/librespot/*",element:(0,m.jsx)(spicetifyApp1,{})'))
        $patched | Should -Match ([regex]::Escape('({123:1,456:1,"spicetify-routes-marketplace":1,"spicetify-routes-librespot":1})[e]'))
        ([regex]::Matches($patched, '\bspicetifyApp0=')).Count | Should -Be 1
        ([regex]::Matches($patched, '\bspicetifyApp1=')).Count | Should -Be 1
    }

    It 'leaves the bundle untouched when the injection anchors are missing' {
        $script:fixtureRoot = New-WiringFixture -IndexHtml $script:LiveIndexHtml -BundleJs 'var noAnchorsHere=1;'
        $result = Repair-SpicetifyCustomAppWiring -AppsDirectory $script:fixtureRoot
        $result.Status | Should -Be 'AnchorsMissing'
        [System.IO.File]::ReadAllText((Join-Path $script:fixtureRoot 'xpui\xpui.js')) | Should -Be 'var noAnchorsHere=1;'
    }

    It 'requires the route chunk file before patching' {
        $script:fixtureRoot = New-WiringFixture -IndexHtml $script:LiveIndexHtml -BundleJs $script:AnchoredBundle -SkipRouteBundle
        (Repair-SpicetifyCustomAppWiring -AppsDirectory $script:fixtureRoot).Status | Should -Be 'RouteBundleMissing'
    }
}

Describe 'Get-SpicetifyAttestationVerdict' {
BeforeAll {
    . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyAttestationVerdict.ps1')
}

    It 'treats a zero exit code as Verified regardless of output' {
        Get-SpicetifyAttestationVerdict -ExitCode 0 -Output '' | Should -Be 'Verified'
        Get-SpicetifyAttestationVerdict -ExitCode 0 -Output 'anything at all' | Should -Be 'Verified'
    }

    It 'maps clear verification-failure output to Mismatch' {
        Get-SpicetifyAttestationVerdict -ExitCode 1 -Output 'X verification failed' | Should -Be 'Mismatch'
        Get-SpicetifyAttestationVerdict -ExitCode 1 -Output 'failed to verify signature' | Should -Be 'Mismatch'
        Get-SpicetifyAttestationVerdict -ExitCode 1 -Output 'no attestations found for subject' | Should -Be 'Mismatch'
        Get-SpicetifyAttestationVerdict -ExitCode 1 -Output 'no matching attestations' | Should -Be 'Mismatch'
        Get-SpicetifyAttestationVerdict -ExitCode 1 -Output 'the digest does not match' | Should -Be 'Mismatch'
    }

    It 'treats tooling, network and auth failures as Unavailable (never fails closed)' {
        Get-SpicetifyAttestationVerdict -ExitCode 1 -Output 'could not connect to api.github.com' | Should -Be 'Unavailable'
        Get-SpicetifyAttestationVerdict -ExitCode 4 -Output 'gh auth login required' | Should -Be 'Unavailable'
        Get-SpicetifyAttestationVerdict -ExitCode 1 -Output 'API rate limit exceeded' | Should -Be 'Unavailable'
        Get-SpicetifyAttestationVerdict -ExitCode 1 -Output '' | Should -Be 'Unavailable'
    }
}

Describe 'Test-SpicetifyCliAttestation' {
BeforeAll {
    . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Get-SpicetifyAttestationVerdict.ps1')
    . (Join-Path $PSScriptRoot '..\..\src\powershell\shared\Test-SpicetifyCliAttestation.ps1')
    $script:realFile = Join-Path ([System.IO.Path]::GetTempPath()) ("ls-attn-{0}.zip" -f ([guid]::NewGuid().ToString('N')))
    Set-Content -LiteralPath $script:realFile -Value 'payload' -Encoding ascii
    $script:goodAttestation = @{ Repo = 'spicetify/cli'; CertIdentityRegex = '^https://github\.com/spicetify/cli/'; OidcIssuer = 'https://token.actions.githubusercontent.com' }
}

Describe 'PowerShell 7 security floor' {
    It 'warns for PowerShell 7.6.0 through 7.6.4 and names the fixed version' {
        $status = Get-PowerShell7SecurityFloorStatus -VersionString '7.6.4' -Edition 'Core'
        $status.NeedsUpdate | Should -BeTrue
        $status.Status | Should -Be 'UpdateRecommended'
        $status.MinimumVersion | Should -Be '7.6.5'
        $status.Reason | Should -Match 'CVE-2026-50523'
        $status.Reason | Should -Match '7\.6\.5'
    }

    It 'stays silent at the fixed PowerShell 7 floor' {
        foreach ($version in @('7.6.5', '7.6.6')) {
            $status = Get-PowerShell7SecurityFloorStatus -VersionString $version -Edition 'Core'
            $status.NeedsUpdate | Should -BeFalse
            $status.Status | Should -Be 'Supported'
        }
    }

    It 'does not apply the PowerShell 7 floor to Windows PowerShell 5.1' {
        $status = Get-PowerShell7SecurityFloorStatus -VersionString '5.1.26100.1' -Edition 'Desktop'
        $status.NeedsUpdate | Should -BeFalse
        $status.Status | Should -Be 'NotApplicable'
    }
}
AfterAll {
    Remove-Item -LiteralPath $script:realFile -Force -ErrorAction SilentlyContinue
}

    It 'degrades to Unavailable when no attestation metadata is supplied' {
        Test-SpicetifyCliAttestation -Path $script:realFile -Attestation $null | Should -Be 'Unavailable'
    }

    It 'degrades to Unavailable when the repo is missing from the metadata' {
        Test-SpicetifyCliAttestation -Path $script:realFile -Attestation @{ Repo = '' } | Should -Be 'Unavailable'
    }

    It 'degrades to Unavailable when the artifact file does not exist' {
        $missing = Join-Path ([System.IO.Path]::GetTempPath()) ("ls-missing-{0}.zip" -f ([guid]::NewGuid().ToString('N')))
        Test-SpicetifyCliAttestation -Path $missing -Attestation $script:goodAttestation | Should -Be 'Unavailable'
    }
}

Describe 'Lane orchestration modules and primary GUI dispatch' {
    BeforeAll {
        $script:orchestrationGlobalNames = @(
            'TEMP_DIR', 'CONFIG_DIR', 'CACHE_DIR', 'SPOTIFY_EXE_PATH', 'PinnedReleases',
            'URL_SPOTX', 'URL_SPICETIFY_FMT', 'URL_THEMES_REPO', 'URL_MARKETPLACE',
            'CommunityThemeRepos', 'ThemesNeedingJS', 'DeprecatedCommunityExtensionNames', 'CommunityCustomApps'
        )
        $script:orchestrationOriginalGlobals = @{}
        foreach ($name in $script:orchestrationGlobalNames) {
            $existing = Get-Variable -Name $name -Scope Global -ErrorAction SilentlyContinue
            $script:orchestrationOriginalGlobals[$name] = [pscustomobject]@{
                Exists = $null -ne $existing
                Value = if ($existing) { $existing.Value } else { $null }
            }
        }

        $tokens = $null
        $parseErrors = $null
        $script:orchestrationAst = [System.Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $PSScriptRoot '..\..\LibreSpot.ps1'),
            [ref]$tokens,
            [ref]$parseErrors
        )
        @($parseErrors).Count | Should -Be 0

        $moduleNames = @(
            'Module-NukeSpotify',
            'Module-InstallSpotX',
            'Module-InstallSpicetifyCLI',
            'Module-InstallThemes',
            'Module-InstallExtensions',
            'Module-InstallMarketplace',
            'Module-InstallCustomApps',
            'Module-ApplySpicetify'
        )
        foreach ($moduleName in $moduleNames) {
            $moduleAst = $script:orchestrationAst.Find({
                param($node)
                $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -eq $moduleName
            }, $true)
            if (-not $moduleAst) { throw "Could not find orchestration function '$moduleName'." }
            Invoke-Expression $moduleAst.Extent.Text
        }

        function Get-GuiClickHandler {
            param([string]$ControlName)

            $expectedExpression = '$ui[' + [char]39 + $ControlName + [char]39 + ']'
            $handlerAst = $script:orchestrationAst.Find({
                param($node)
                $node -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and
                    $node.Member.Value -eq 'Add_Click' -and
                    $node.Expression.Extent.Text -eq $expectedExpression
            }, $true)
            if (-not $handlerAst) { throw "Could not find the $ControlName click handler." }
            return $handlerAst.Arguments[0].ScriptBlock.GetScriptBlock()
        }

        $script:installClickHandler = Get-GuiClickHandler -ControlName 'BtnInstall'
        $script:reapplyClickHandler = Get-GuiClickHandler -ControlName 'BtnReapply'
        $script:fullResetClickHandler = Get-GuiClickHandler -ControlName 'BtnFullReset'

        function Test-Path {
            [CmdletBinding(DefaultParameterSetName = 'Path')]
            param(
                [Parameter(Position = 0, ParameterSetName = 'Path')]
                [string[]]$Path,
                [Parameter(Mandatory, ParameterSetName = 'LiteralPath')]
                [string[]]$LiteralPath,
                [Microsoft.PowerShell.Commands.TestPathType]$PathType = [Microsoft.PowerShell.Commands.TestPathType]::Any
            )

            $targets = if ($PSCmdlet.ParameterSetName -eq 'LiteralPath') { $LiteralPath } else { $Path }
            foreach ($target in $targets) {
                if ([string]$target -match '^HK(CC|CR|CU|LM|U):') {
                    $false
                    continue
                }
                if ($PSCmdlet.ParameterSetName -eq 'LiteralPath') {
                    Microsoft.PowerShell.Management\Test-Path -LiteralPath $target -PathType $PathType
                } else {
                    Microsoft.PowerShell.Management\Test-Path -Path $target -PathType $PathType
                }
            }
        }

        function Write-Log {
            param([string]$Message, [string]$Level = 'INFO')
            $script:orchestrationCalls.Log += "$Level|$Message"
        }
        function Write-OperationJournalEntry {
            param(
                [string]$OperationId,
                [string]$Phase,
                [string]$Target,
                [string]$SafetyDecision,
                [string]$Result,
                [bool]$WouldChange,
                [bool]$Reversible,
                [string]$RollbackHint,
                [string]$TokenKind,
                [string]$PreviousStateRef,
                [string]$NewState,
                [string]$UndoAction,
                [string]$Risk,
                [hashtable]$Data
            )
            $script:orchestrationCalls.Journal += "$Phase|$Target|$Result"
        }
        function Get-SpicetifyV3Conflict { [pscustomobject]@{ IsConflict = $false; Message = '' } }
        function New-LibreSpotTempFile {
            param([string]$Name)
            New-Item -Path $global:TEMP_DIR -ItemType Directory -Force | Out-Null
            Join-Path $global:TEMP_DIR ("{0}-{1}" -f ([guid]::NewGuid().ToString('N')), $Name)
        }
        function New-LibreSpotTempDirectory {
            param([string]$Name)
            $path = Join-Path $global:TEMP_DIR ("{0}-{1}" -f ([guid]::NewGuid().ToString('N')), $Name)
            New-Item -Path $path -ItemType Directory -Force | Out-Null
            $path
        }
        function Get-FromAssetCache { param([string]$SHA256Hash, [string]$DestinationPath, [string]$Label) $false }
        function Download-FileSafe {
            param([string]$Uri, [string]$OutFile)
            $script:orchestrationCalls.Downloads += $Uri
            $parent = Split-Path -Path $OutFile -Parent
            if (-not (Test-Path -LiteralPath $parent)) { New-Item -Path $parent -ItemType Directory -Force | Out-Null }
            Set-Content -LiteralPath $OutFile -Value 'fixture payload' -Encoding Ascii
        }
        function Confirm-FileHash { param([string]$Path, [string]$ExpectedHash, [string]$Label) $true }
        function Save-ToAssetCache { param([string]$SourcePath, [string]$SHA256Hash, [string]$Label, [string]$SourceUrl) }
        function Build-SpotXParams { param($Config) '-confirm_uninstall_ms_spoti -block_update_on' }
        function New-SpotXCustomPatchesFile { param($Config) '' }
        function Invoke-ExternalScriptIsolated {
            param([string]$FilePath, [string]$Arguments, [string]$ExpectedHash, [string]$Label)
            if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) { throw 'SpotX fixture script was not staged.' }
            $script:orchestrationCalls.ExternalScripts += $FilePath
        }
        function Get-SpotXDownloadRetryPlan { param([string]$Category, [bool]$MirrorAlreadyUsed) $null }
        function Get-SpotXPatchVerification { param([string]$SpotifyExePath) [pscustomobject]@{ Verified = $true; Signals = @('fixture'); Reason = '' } }
        function Hide-SpotifyWindows { $script:orchestrationCalls.HideSpotify++ }
        function Stop-SpotifyProcesses { param([int]$MaxAttempts = 3) $script:orchestrationCalls.StopSpotify++ }
        function Start-Process { param([string]$FilePath, [string]$ArgumentList) $script:orchestrationCalls.Processes += "$FilePath|$ArgumentList" }
        function Start-Sleep { param([int]$Seconds, [int]$Milliseconds) }
        function Get-SpicetifyIntegrationContext { $script:orchestrationIntegration }
        function Clear-DirectoryContentsSafely {
            param([string]$Path, [string]$Label)
            if (Test-Path -LiteralPath $Path) {
                Get-ChildItem -LiteralPath $Path -Force | Microsoft.PowerShell.Management\Remove-Item -Recurse -Force
            }
            $true
        }
        function Add-PathEntry {
            param([string]$Entry, [string]$Scope)
            $script:orchestrationCalls.PathEntries += "$Scope|$Entry"
            $true
        }
        function Test-SpicetifyCliAttestation { param([string]$Path, [hashtable]$Attestation) 'Unavailable' }
        function Invoke-SpicetifyCli {
            param([string[]]$Arguments, [string]$FailureMessage)
            $script:orchestrationCalls.Cli += ($Arguments -join ' ')
        }
        function Expand-ArchiveSafely {
            param([string]$ZipPath, [string]$DestinationPath, [string]$Label, [long]$MaxExpandedBytes = 0)
            New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
            switch -Wildcard ($Label) {
                'Spicetify CLI*' {
                    Set-Content -LiteralPath (Join-Path $DestinationPath 'spicetify.exe') -Value 'fixture cli' -Encoding Ascii
                }
                'Themes archive' {
                    $theme = Join-Path $DestinationPath 'spicetify-themes-fixture\Dribbblish'
                    New-Item -Path $theme -ItemType Directory -Force | Out-Null
                    Set-Content -LiteralPath (Join-Path $theme 'color.ini') -Value '[Base]' -Encoding Ascii
                    Set-Content -LiteralPath (Join-Path $theme 'user.css') -Value 'body {}' -Encoding Ascii
                }
                'Marketplace*' {
                    $marketplace = Join-Path $DestinationPath 'marketplace-dist'
                    New-Item -Path $marketplace -ItemType Directory -Force | Out-Null
                    Set-Content -LiteralPath (Join-Path $marketplace 'manifest.json') -Value '{}' -Encoding Ascii
                    Set-Content -LiteralPath (Join-Path $marketplace 'extension.js') -Value 'fixture' -Encoding Ascii
                }
                'Custom app stats*' {
                    $stats = Join-Path $DestinationPath 'stats'
                    New-Item -Path $stats -ItemType Directory -Force | Out-Null
                    Set-Content -LiteralPath (Join-Path $stats 'manifest.json') -Value '{}' -Encoding Ascii
                    Set-Content -LiteralPath (Join-Path $stats 'extension.js') -Value 'fixture' -Encoding Ascii
                }
                'Custom app librespot*' {
                    $librespot = Join-Path $DestinationPath 'librespot'
                    New-Item -Path $librespot -ItemType Directory -Force | Out-Null
                    Set-Content -LiteralPath (Join-Path $librespot 'manifest.json') -Value '{"version":"4.1.1"}' -Encoding Ascii
                    Set-Content -LiteralPath (Join-Path $librespot 'index.js') -Value 'fixture app' -Encoding Ascii
                    Set-Content -LiteralPath (Join-Path $librespot 'style.css') -Value ':root{}' -Encoding Ascii
                    Set-Content -LiteralPath (Join-Path $librespot 'librespot-engine.js') -Value 'window.LibreSpotEngine={};' -Encoding Ascii
                    Set-Content -LiteralPath (Join-Path $librespot 'LICENSE') -Value 'MIT' -Encoding Ascii
                    Set-Content -LiteralPath (Join-Path $librespot 'THIRD_PARTY_NOTICES.md') -Value 'notices' -Encoding Ascii
                }
            }
        }
        function Download-CommunityExtensions { param($Config) $script:orchestrationCalls.ExtensionDownloads++ }
        function Sync-SpicetifyListSetting {
            param([string]$Key, [string[]]$DesiredItems, [string[]]$ManagedItems)
            $script:orchestrationCalls.Sync += "$Key=$($DesiredItems -join ',')"
        }
        function Get-SpicetifyConfigEntries { $script:spicetifyConfigEntries }
        function Install-MarketplacePlaceholderTheme {
            $theme = Join-Path $script:orchestrationIntegration.ThemesDirectory 'marketplace'
            New-Item -Path $theme -ItemType Directory -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $theme 'color.ini') -Value '[Marketplace]' -Encoding Ascii
            $theme
        }
        function Install-MarketplaceNavFallbackExtension {
            New-Item -Path $script:orchestrationIntegration.ExtensionsDirectory -ItemType Directory -Force | Out-Null
            $name = 'librespot-marketplace-button.js'
            Set-Content -LiteralPath (Join-Path $script:orchestrationIntegration.ExtensionsDirectory $name) -Value 'fixture' -Encoding Ascii
            $name
        }
        function Get-MarketplaceHealth {
            $manifest = Join-Path $script:orchestrationIntegration.MarketplaceDirectory 'manifest.json'
            $hasFiles = Test-Path -LiteralPath $manifest -PathType Leaf
            [pscustomobject]@{ HasFiles = $hasFiles; IsReady = $hasFiles; Status = if ($hasFiles) { 'Ready' } else { 'Missing' } }
        }
        function Remove-PathSafely {
            param([string]$Path, [string]$Label)
            $fixtureRoot = [System.IO.Path]::GetFullPath($script:orchestrationRoot).TrimEnd('\') + '\'
            $fullPath = [System.IO.Path]::GetFullPath($Path)
            if (-not $fullPath.StartsWith($fixtureRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Orchestration fixture refused path outside its root: $Path"
            }
            $script:orchestrationCalls.RemovedPaths += $fullPath
            $exists = Test-Path -LiteralPath $fullPath
            if ($exists) { Microsoft.PowerShell.Management\Remove-Item -LiteralPath $fullPath -Recurse -Force }
            [int]$exists
        }
        function Get-SpicetifyDiagnosticSnapshot { [ordered]@{ Spotify = $global:SPOTIFY_EXE_PATH; Config = $script:orchestrationIntegration.ConfigDirectory } }
        function Get-SpicetifyApplyPlan { $script:orchestrationApplyPlan }
        function Repair-SpicetifyCustomAppWiring {
            param([string]$AppName)
            $script:orchestrationCalls.Wiring++
            $script:orchestrationCalls.WiringApps += $AppName
            $bundlePath = Join-Path $script:orchestrationRoot "$AppName-route.fixture"
            Set-Content -LiteralPath $bundlePath -Value 'wired' -Encoding Ascii
            [pscustomobject]@{ Status = 'Wired'; BundlePath = $bundlePath; Detail = 'Fixture route is wired.' }
        }
        function Write-MarketplaceVisibilityEvidence {
            param([string]$Source, [string]$ApplyStage, [bool]$ApplySucceeded, [string]$ApplyMessage)
            $script:orchestrationCalls.Evidence += "$Source|$ApplyStage|$ApplySucceeded"
            Set-Content -LiteralPath (Join-Path $script:orchestrationRoot 'apply-evidence.fixture') -Value $ApplyMessage -Encoding Ascii
        }

        function Get-DesktopPath { $script:orchestrationDesktop }
        function Get-AppxPackage { param([string]$Name) $script:orchestrationCalls.SystemQueries += "Appx|$Name" }
        function Get-AppxProvisionedPackage { param([switch]$Online) $script:orchestrationCalls.SystemQueries += 'ProvisionedAppx' }
        function Get-ItemProperty { param([string]$Path, [string]$Name) $script:orchestrationCalls.SystemQueries += "RegistryValue|$Path|$Name" }
        function Get-ScheduledTask { $script:orchestrationCalls.SystemQueries += 'ScheduledTasks' }
        function Get-NetFirewallRule { $script:orchestrationCalls.SystemQueries += 'FirewallRules' }
        function Remove-AppxPackage { throw 'The fixture must not remove AppX packages.' }
        function Remove-AppxProvisionedPackage { throw 'The fixture must not remove provisioned AppX packages.' }
        function Remove-ItemProperty { throw 'The fixture must not remove registry values.' }
        function Unregister-ScheduledTask { throw 'The fixture must not remove scheduled tasks.' }
        function Remove-NetFirewallRule { throw 'The fixture must not remove firewall rules.' }

        function Confirm-NetworkReadyForAction { param([string]$Message, [string]$Purpose) $true }
        function Assert-RiskAcknowledged { $true }
        function Test-CompatibilityGate { $true }
        function Show-ThemedDialog {
            param(
                [string]$Message,
                [string]$Title,
                [string]$Buttons,
                [string]$Icon,
                [string]$PrimaryText,
                [string]$SecondaryText,
                [switch]$PrimaryIsDestructive
            )
            $script:orchestrationCalls.Dialogs += $Title
            'Yes'
        }
        function Switch-ToInstallPage {
            param(
                [string]$Title,
                [string]$Context,
                [string]$PrepareLabel,
                [string]$RunLabel,
                [string]$VerifyLabel,
                [string]$CompleteLabel
            )
            $script:orchestrationCalls.Pages += $Title
        }
        function Start-MaintenanceJob { param([string]$Action) $script:orchestrationCalls.Maintenance += $Action }
        function Get-InstallConfig { param([bool]$EasyMode) [pscustomobject]@{ Mode = if ($EasyMode) { 'Easy' } else { 'Custom' }; CleanInstall = $true } }
        function Normalize-LibreSpotConfig { param($Config) $Config }
        function Save-LibreSpotConfig { param($Config) $true }
        function Capture-CustomConfigBaseline { $script:orchestrationCalls.BaselineCaptures++ }
        function Update-ModePresentation { }
        function Start-InstallJob { param($Config) $script:orchestrationCalls.InstallJobs += [string]$Config.Mode }
        function Reset-UiAfterLaunchFailure { param([string]$Title, [string]$Message) throw "$Title`: $Message" }
    }

    BeforeEach {
        $script:previousAppData = $env:APPDATA
        $script:previousLocalAppData = $env:LOCALAPPDATA
        $script:previousTemp = $env:TEMP
        $script:previousProcessorArchitecture = $env:PROCESSOR_ARCHITECTURE
        $script:orchestrationRoot = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $env:APPDATA = Join-Path $script:orchestrationRoot 'AppData\Roaming'
        $env:LOCALAPPDATA = Join-Path $script:orchestrationRoot 'AppData\Local'
        $env:TEMP = Join-Path $script:orchestrationRoot 'Temp'
        $script:orchestrationDesktop = Join-Path $script:orchestrationRoot 'Desktop'
        $global:TEMP_DIR = Join-Path $script:orchestrationRoot 'LibreSpotTemp'
        $global:CONFIG_DIR = Join-Path $script:orchestrationRoot 'LibreSpotConfig'
        $global:CACHE_DIR = Join-Path $global:CONFIG_DIR 'cache'
        $global:SPOTIFY_EXE_PATH = Join-Path $env:APPDATA 'Spotify\Spotify.exe'
        $script:orchestrationIntegration = [pscustomobject]@{
            InstallDirectory = Join-Path $env:LOCALAPPDATA 'spicetify'
            CliPath = Join-Path $env:LOCALAPPDATA 'spicetify\spicetify.exe'
            ConfigDirectory = Join-Path $env:APPDATA 'spicetify'
            ConfigPath = Join-Path $env:APPDATA 'spicetify\config-xpui.ini'
            ThemesDirectory = Join-Path $env:APPDATA 'spicetify\Themes'
            CustomAppsDirectory = Join-Path $env:APPDATA 'spicetify\CustomApps'
            MarketplaceDirectory = Join-Path $env:APPDATA 'spicetify\CustomApps\marketplace'
            LegacyMarketplaceDirectory = Join-Path $env:APPDATA 'spicetify\Apps\marketplace'
            ExtensionsDirectory = Join-Path $env:APPDATA 'spicetify\Extensions'
        }
        foreach ($path in @(
            $env:APPDATA,
            $env:LOCALAPPDATA,
            $env:TEMP,
            $script:orchestrationDesktop,
            $global:TEMP_DIR,
            $global:CONFIG_DIR,
            (Split-Path $global:SPOTIFY_EXE_PATH -Parent),
            $script:orchestrationIntegration.ConfigDirectory
        )) {
            New-Item -Path $path -ItemType Directory -Force | Out-Null
        }
        Set-Content -LiteralPath $global:SPOTIFY_EXE_PATH -Value 'fixture spotify' -Encoding Ascii
        Set-Content -LiteralPath (Join-Path (Split-Path $global:SPOTIFY_EXE_PATH -Parent) 'chrome_elf.dll') -Value 'fixture elf' -Encoding Ascii
        Set-Content -LiteralPath (Join-Path (Split-Path $global:SPOTIFY_EXE_PATH -Parent) 'prefs') -Value 'fixture preferences are ready' -Encoding Ascii

        $global:PinnedReleases = @{
            SpotX = @{ Version = 'fixture'; SHA256 = 'spotx-fixture-hash' }
            SpicetifyCLI = @{ Version = 'fixture'; SHA256 = @{ x64 = 'cli-fixture-hash'; arm64 = 'cli-fixture-hash' }; Attestation = @{ Repo = 'fixture/cli' } }
            Themes = @{ SHA256 = 'themes-fixture-hash' }
            Marketplace = @{ SHA256 = 'marketplace-fixture-hash' }
        }
        $global:URL_SPOTX = 'https://example.invalid/spotx.ps1'
        $global:URL_SPICETIFY_FMT = 'https://example.invalid/spicetify-{0}-{1}.zip'
        $global:URL_THEMES_REPO = 'https://example.invalid/themes.zip'
        $global:URL_MARKETPLACE = 'https://example.invalid/marketplace.zip'
        $global:CommunityThemeRepos = @{}
        $global:ThemesNeedingJS = @()
        $global:DeprecatedCommunityExtensionNames = @()
        $global:CommunityCustomApps = [ordered]@{
            stats = @{ DisplayName = 'Stats'; Source = 'fixture/stats'; Url = 'https://example.invalid/stats.zip'; AssetPath = 'stats'; SHA256 = 'stats-fixture-hash' }
            librespot = @{
                DisplayName = 'LibreSpot'
                Source = 'fixture/librespot'
                Url = 'https://example.invalid/librespot.zip'
                AssetPath = 'librespot'
                RequiredFiles = @('manifest.json', 'index.js', 'style.css', 'librespot-engine.js', 'LICENSE', 'THIRD_PARTY_NOTICES.md')
                CompanionExtension = 'librespot-engine.js'
                SHA256 = 'librespot-fixture-hash'
            }
        }
        $script:spicetifyConfigEntries = @{}
        $script:orchestrationApplyPlan = [pscustomobject]@{
            Stage = 'backup apply'
            Arguments = @('backup', 'apply', '--bypass-admin')
            FailureMessage = 'fixture failure'
            SuccessMessage = 'Spicetify backup apply succeeded.'
            Reason = 'fixture fresh apply'
        }
        $script:orchestrationCalls = @{
            Log = @(); Journal = @(); Downloads = @(); ExternalScripts = @(); Processes = @(); Cli = @(); PathEntries = @()
            Sync = @(); RemovedPaths = @(); Evidence = @(); SystemQueries = @(); Dialogs = @(); Pages = @()
            Maintenance = @(); InstallJobs = @(); StopSpotify = 0; HideSpotify = 0; ExtensionDownloads = 0
            Wiring = 0; WiringApps = @(); BaselineCaptures = 0
        }
        $env:PROCESSOR_ARCHITECTURE = 'AMD64'
    }

    AfterEach {
        $env:APPDATA = $script:previousAppData
        $env:LOCALAPPDATA = $script:previousLocalAppData
        $env:TEMP = $script:previousTemp
        $env:PROCESSOR_ARCHITECTURE = $script:previousProcessorArchitecture
        if ($script:orchestrationRoot -and (Test-Path -LiteralPath $script:orchestrationRoot)) {
            Microsoft.PowerShell.Management\Remove-Item -LiteralPath $script:orchestrationRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    AfterAll {
        foreach ($name in $script:orchestrationGlobalNames) {
            $original = $script:orchestrationOriginalGlobals[$name]
            if ($original.Exists) {
                Set-Variable -Name $name -Scope Global -Value $original.Value
            } else {
                Remove-Variable -Name $name -Scope Global -ErrorAction SilentlyContinue
            }
        }
    }

    It 'Module-NukeSpotify removes only the fake Spotify tree' {
        Module-NukeSpotify

        Test-Path -LiteralPath (Join-Path $env:APPDATA 'Spotify') | Should -BeFalse
        Test-Path -LiteralPath (Join-Path $env:APPDATA 'spicetify') | Should -BeFalse
        $script:orchestrationCalls.StopSpotify | Should -BeGreaterThan 0
        @($script:orchestrationCalls.RemovedPaths).Count | Should -BeGreaterThan 0
        @($script:orchestrationCalls.RemovedPaths | Where-Object { -not $_.StartsWith($script:orchestrationRoot, [System.StringComparison]::OrdinalIgnoreCase) }).Count | Should -Be 0
    }

    It 'Module-InstallSpotX stages and invokes the pinned script against fake Spotify' {
        Module-InstallSpotX -Config ([pscustomobject]@{ SpotX_Mirror = $false }) -SyncHash $null

        $script:orchestrationCalls.Downloads | Should -Contain $global:URL_SPOTX
        @($script:orchestrationCalls.ExternalScripts).Count | Should -Be 1
        @($script:orchestrationCalls.Processes | Where-Object { $_ -like 'explorer.exe*' }).Count | Should -Be 1
        $script:orchestrationCalls.StopSpotify | Should -BeGreaterThan 0
    }

    It 'Module-InstallSpicetifyCLI expands the CLI into the fake integration root' {
        Module-InstallSpicetifyCLI

        Test-Path -LiteralPath $script:orchestrationIntegration.CliPath -PathType Leaf | Should -BeTrue
        $script:orchestrationCalls.Cli | Should -Contain 'config --bypass-admin'
        @($script:orchestrationCalls.PathEntries | Where-Object { $_ -like 'User|*' }).Count | Should -Be 1
    }

    It 'Module-InstallThemes copies and configures a theme in the fake root' {
        $config = [pscustomobject]@{ Spicetify_Theme = 'Dribbblish'; Spicetify_Scheme = 'Base' }
        Module-InstallThemes -Config $config

        Test-Path -LiteralPath (Join-Path $script:orchestrationIntegration.ThemesDirectory 'Dribbblish\color.ini') -PathType Leaf | Should -BeTrue
        $script:orchestrationCalls.Cli | Should -Contain 'config current_theme Dribbblish --bypass-admin'
    }

    It 'Module-InstallExtensions downloads and synchronizes the selected extensions' {
        $config = [pscustomobject]@{ Spicetify_Extensions = @('shuffle+.js', 'beautiful-lyrics.mjs') }
        Module-InstallExtensions -Config $config

        $script:orchestrationCalls.ExtensionDownloads | Should -Be 1
        $script:orchestrationCalls.Sync | Should -Contain 'extensions=shuffle+.js,beautiful-lyrics.mjs'
    }

    It 'Module-InstallMarketplace installs the app and placeholder theme in the fake root' {
        Module-InstallMarketplace -Config ([pscustomobject]@{ Spicetify_Marketplace = $true })

        Test-Path -LiteralPath (Join-Path $script:orchestrationIntegration.MarketplaceDirectory 'manifest.json') -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $script:orchestrationIntegration.ThemesDirectory 'marketplace\color.ini') -PathType Leaf | Should -BeTrue
        $script:orchestrationCalls.Sync | Should -Contain 'custom_apps=marketplace'
        $script:orchestrationCalls.Sync | Should -Contain 'extensions=librespot-marketplace-button.js'
    }

    It 'Module-InstallCustomApps installs and synchronizes a selected app in the fake root' {
        Module-InstallCustomApps -Config ([pscustomobject]@{ Spicetify_CustomApps = @('stats') })

        Test-Path -LiteralPath (Join-Path $script:orchestrationIntegration.CustomAppsDirectory 'stats\manifest.json') -PathType Leaf | Should -BeTrue
        $script:orchestrationCalls.Sync | Should -Contain 'custom_apps=stats'
    }

    It 'Module-InstallCustomApps installs the LibreSpot app and bootstrapped companion' {
        $config = @{
            Spicetify_CustomApps = @('librespot')
            LibreSpot_EngineProfileJson = '{"schemaVersion":1,"name":"Desktop","theme":"Prism","scheme":"Dark","schemes":{"Dark":{"text":"ffffff"}}}'
            LibreSpot_EnabledSnippets = @('compact-sidebar')
            LibreSpot_FeatureOverridesJson = '{"enableFoo":true}'
            SpotX_Premium = $true
        }

        Module-InstallCustomApps -Config $config

        Test-Path -LiteralPath (Join-Path $script:orchestrationIntegration.CustomAppsDirectory 'librespot\manifest.json') -PathType Leaf | Should -BeTrue
        $companionPath = Join-Path $script:orchestrationIntegration.ExtensionsDirectory 'librespot-engine.js'
        Test-Path -LiteralPath $companionPath -PathType Leaf | Should -BeTrue
        $companion = Get-Content -Raw -LiteralPath $companionPath
        $companion | Should -Match '^window\.__libreSpotDesktopBootstrap='
        $bootstrapMatch = [regex]::Match($companion, "payloadBase64:'([^']+)'")
        $bootstrapMatch.Success | Should -BeTrue
        $payloadJson = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($bootstrapMatch.Groups[1].Value))
        $payload = $payloadJson | ConvertFrom-Json
        @($payload.enabledSnippets) | Should -Be @('compact-sidebar')
        $payload.featureOverrides.enableFoo | Should -BeTrue
        $payload.spotxSwitches.SpotX_Premium | Should -BeTrue
        $script:orchestrationCalls.Sync | Should -Contain 'custom_apps=librespot'
        $script:orchestrationCalls.Sync | Should -Contain 'extensions=librespot-engine.js'
    }

    It 'repairs both managed custom app routes after apply' {
        $results = Repair-LibreSpotManagedCustomAppRoutes -Config ([pscustomobject]@{
            Spicetify_Marketplace = $true
            Spicetify_CustomApps = @('librespot')
        })

        @($results.AppName) | Should -Be @('marketplace', 'librespot')
        @($script:orchestrationCalls.WiringApps) | Should -Be @('marketplace', 'librespot')
    }

    It 'Module-ApplySpicetify applies and records evidence against the fake root' {
        $result = Module-ApplySpicetify -Config ([pscustomobject]@{ Spicetify_Marketplace = $true }) -EvidenceSource 'PesterFixture'

        $result.Succeeded | Should -BeTrue
        $script:orchestrationCalls.Cli | Should -Contain 'backup apply --bypass-admin'
        $script:orchestrationCalls.Evidence | Should -Contain 'PesterFixture|backup apply|True'
        Test-Path -LiteralPath (Join-Path $script:orchestrationRoot 'marketplace-route.fixture') -PathType Leaf | Should -BeTrue
        Test-Path -LiteralPath (Join-Path $script:orchestrationRoot 'apply-evidence.fixture') -PathType Leaf | Should -BeTrue
    }

    It 'Module-ApplySpicetify reuses a current backup without restarting Spotify' {
        $script:orchestrationApplyPlan = [pscustomobject]@{
            Stage = 'apply --no-restart'
            Arguments = @('apply', '--no-restart', '--bypass-admin')
            FailureMessage = 'fixture failure'
            SuccessMessage = 'Spicetify apply succeeded using the current verified backup.'
            Reason = 'fixture reapply'
        }

        $result = Module-ApplySpicetify -Config ([pscustomobject]@{ Spicetify_Marketplace = $true }) -EvidenceSource 'PesterReapplyFixture'

        $result.Succeeded | Should -BeTrue
        $script:orchestrationCalls.Cli | Should -Contain 'apply --no-restart --bypass-admin'
        $script:orchestrationCalls.Evidence | Should -Contain 'PesterReapplyFixture|apply --no-restart|True'
    }

    It 'routes Reapply and Full Reset clicks to their maintenance jobs' {
        $ui = @{}
        & $script:reapplyClickHandler
        & $script:fullResetClickHandler

        @($script:orchestrationCalls.Maintenance) | Should -Be @('Reapply', 'FullReset')
        $script:orchestrationCalls.Pages | Should -Contain 'Reapplying your setup'
        $script:orchestrationCalls.Pages | Should -Contain 'Preparing full reset'
    }

    It 'routes the recommended setup click through save and async job launch' {
        $ui = @{
            BtnInstall = [pscustomobject]@{ IsEnabled = $true }
            ModeEasy = [pscustomobject]@{ IsChecked = $true }
        }
        & $script:installClickHandler

        $ui.BtnInstall.IsEnabled | Should -BeFalse
        $script:orchestrationCalls.InstallJobs | Should -Contain 'Easy'
        $script:orchestrationCalls.Pages | Should -Contain 'Preparing recommended setup'
        $script:orchestrationCalls.BaselineCaptures | Should -Be 1
    }
}
