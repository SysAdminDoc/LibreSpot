#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }
<#
    .SYNOPSIS
        Pester 5.x tests for pure/non-mutating PowerShell functions in LibreSpot.ps1.

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
        'ConvertTo-ConfigBoolean'
        'ConvertTo-ConfigInt'
        'Get-LibreSpotConfigSchemaVersion'
        'Assert-LibreSpotConfigSchemaSupported'
        'Normalize-LibreSpotConfig'
        'Compare-LibreSpotVersions'
        'Get-SpotXChildFailureClassification'
        'Get-ThirdPartyPatcherReport'
        'Copy-DirectorySnapshotSafely'
        'Merge-DirectorySnapshotMissingFiles'
        'New-SpicetifyStatePreservationSnapshot'
        'Restore-SpicetifyStatePreservationSnapshot'
        'Invoke-WithSpicetifyStatePreservation'
        'Get-WatcherLaunchCommand'
        'Get-WatcherState'
        'Set-WatcherState'
        'Invoke-AutoReapplyWatcher'
        'Invoke-HeadlessReapply'
        'Register-AutoReapplyTask'
    )
    $blocks = foreach ($fn in $functionsToLoad) {
        $block = Extract-FunctionBlock $scriptContent $fn
        $block = $block -replace '\[int\]::MaxValue', [string][int]::MaxValue
        $block = $block -replace '\[int\]::MinValue', [string][int]::MinValue
        $block
    }
    $combined = $blocks -join "`n`n"
    Invoke-Expression $combined

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

    $global:SpotifyVersionManifest = @(
        @{ Id='auto'; Label='Auto'; Version=''; Notes='Recommended.' }
        @{ Id='1.2.93'; Label='1.2.93'; Version='1.2.93'; Notes='Pinned.' }
        @{ Id='1.2.92'; Label='1.2.92'; Version='1.2.92'; Notes='Previous fallback.' }
    )
    $global:SpotifyVersionIds = @($global:SpotifyVersionManifest | ForEach-Object { $_.Id })
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
        $global:CURRENT_OPERATION_ID = '11111111222233334444555555555555'
        $script:spicetifyConfigDirectory = Join-Path $script:preservationRoot 'spicetify'
        $script:spicetifyConfigPath = Join-Path $script:spicetifyConfigDirectory 'config-xpui.ini'
        $script:customAppsDirectory = Join-Path $script:spicetifyConfigDirectory 'CustomApps'
        $script:journalEntries = @()

        function Get-SpicetifyIntegrationContext {
            return [pscustomobject]@{
                ConfigPath = $script:spicetifyConfigPath
                CustomAppsDirectory = $script:customAppsDirectory
            }
        }
        function Get-SpicetifyConfigListValue { return @('marketplace', 'foreign-app') }
        function Get-MarketplaceHealth {
            return [pscustomobject]@{ Status = 'Ready'; IsReady = $true }
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
