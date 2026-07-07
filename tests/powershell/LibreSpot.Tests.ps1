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
        @{ Id='1.2.92'; Label='1.2.92'; Version='1.2.92'; Notes='Pinned.' }
    )
    $global:SpotifyVersionIds = @($global:SpotifyVersionManifest | ForEach-Object { $_.Id })
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
