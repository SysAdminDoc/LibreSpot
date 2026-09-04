#requires -Version 5.1

BeforeAll {
    $script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Copy-DirectorySnapshotSafely.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Get-FileSha256Lower.ps1')
    . (Join-Path $script:RepoRoot 'src\powershell\shared\Reapply-SavedSpicetifySetup.ps1')
    Add-Type -AssemblyName System.Security -ErrorAction Stop
    $script:SafeModeEntropy = [System.Text.Encoding]::UTF8.GetBytes('LibreSpot.SafeModeRecovery.v3')

    function Read-ProtectedSafeModeMarker {
        param([Parameter(Mandatory = $true)][string]$Path)

        $envelope = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        $protectedBytes = [Convert]::FromBase64String([string]$envelope.protectedState)
        $markerBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
            $protectedBytes,
            $script:SafeModeEntropy,
            [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
        return [System.Text.Encoding]::UTF8.GetString($markerBytes) | ConvertFrom-Json
    }

    function Get-SpicetifyIntegrationContext { return $script:Integration }
    function Get-SpicetifyV3Conflict { return [pscustomobject]@{ IsConflict = $false; Message = '' } }
    function Test-SpicetifyCliInstalled { return $true }
    function Module-InstallSpicetifyCLI { throw 'Spicetify CLI reinstall was not expected.' }
    function Stop-SpotifyProcesses { param($MaxAttempts) $script:StopCount++ }
    function Get-SpicetifyApplyPlan { return [pscustomobject]@{ Arguments = @('apply') } }
    function Invoke-SpicetifyCli { param($Arguments, $FailureMessage) $script:InvokeCount++ }
    function Module-ApplySpicetify {
        param($Config, $EvidenceSource)
        $script:ApplyCount++
        return [pscustomobject]@{ Status = 'Applied' }
    }
    function Get-SpicetifyConfigListValue {
        param([string]$Key)
        $text = [System.IO.File]::ReadAllText($script:Integration.ConfigPath)
        $match = [regex]::Match($text, "(?m)^[ \t]*$([regex]::Escape($Key))[ \t]*=[ \t]*(?<value>[^\r\n]*)")
        if (-not $match.Success -or [string]::IsNullOrWhiteSpace($match.Groups['value'].Value)) { return @() }
        return @($match.Groups['value'].Value.Split('|') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    }
    function Write-OperationJournalEntry {
        param($Phase, $Target, $SafetyDecision, $Result, $WouldChange, $Reversible, $RollbackHint, $TokenKind, $PreviousStateRef, $NewState, $UndoAction, $Risk, $Data)
        $script:Journal.Add([pscustomobject]@{
            Phase = $Phase
            Target = $Target
            Result = $Result
            Reversible = [bool]$Reversible
            TokenKind = $TokenKind
            PreviousStateRef = $PreviousStateRef
            Data = $Data
        })
    }
    function Write-Log { param($Message, $Level) }
    function Clear-DirectoryContentsSafely {
        param([string]$Path, [string]$Label)
        $script:ClearCount++
        if (-not ([System.IO.Path]::GetFullPath($Path)).StartsWith($script:TestRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Test refused to clear a path outside its fixture: $Path"
        }
        foreach ($item in @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue)) {
            Remove-Item -LiteralPath $item.FullName -Recurse -Force -ErrorAction Stop
        }
    }
    function Remove-PathSafely {
        param([string]$Path, [string]$Label)
        if (-not ([System.IO.Path]::GetFullPath($Path)).StartsWith($script:TestRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Test refused to remove a path outside its fixture: $Path"
        }
        if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop }
        return 1
    }
}

Describe 'One-session Spotify safe mode' {
    BeforeEach {
        $script:TestRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('LibreSpot-SafeMode-' + [guid]::NewGuid().ToString('N'))
        $configDirectory = Join-Path $script:TestRoot 'spicetify'
        $customAppsDirectory = Join-Path $configDirectory 'CustomApps'
        $global:BACKUP_ROOT = Join-Path $script:TestRoot 'backups'
        $global:CONFIG_DIR = Join-Path $script:TestRoot 'librespot'
        $global:CURRENT_OPERATION_ID = '11111111-2222-3333-4444-555555555555'
        New-Item -Path $customAppsDirectory -ItemType Directory -Force | Out-Null
        New-Item -Path (Join-Path $customAppsDirectory 'alpha\empty') -ItemType Directory -Force | Out-Null
        New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null

        $configPath = Join-Path $configDirectory 'config-xpui.ini'
        $configText = "[Setting]`r`ncurrent_theme = Sleek`r`nextensions = alpha.js|beta.js`r`ncustom_apps = librespot|stats`r`ncolor_scheme = RosePine`r`n"
        $configBytes = [byte[]](0xEF, 0xBB, 0xBF) + [System.Text.Encoding]::UTF8.GetBytes($configText)
        [System.IO.File]::WriteAllBytes($configPath, $configBytes)
        [System.IO.File]::WriteAllText((Join-Path $customAppsDirectory 'root.txt'), 'root app')
        [System.IO.File]::WriteAllText((Join-Path $customAppsDirectory 'alpha\index.js'), 'console.log("alpha");')

        $script:OriginalConfigBase64 = [Convert]::ToBase64String($configBytes)
        $script:Integration = [pscustomobject]@{
            ConfigPath = $configPath
            CustomAppsDirectory = $customAppsDirectory
        }
        $script:StopCount = 0
        $script:InvokeCount = 0
        $script:ApplyCount = 0
        $script:ClearCount = 0
        $script:Journal = [System.Collections.Generic.List[object]]::new()
    }

    AfterEach {
        if (Test-Path -LiteralPath $script:TestRoot) {
            Remove-Item -LiteralPath $script:TestRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'round-trips config bytes and the complete CustomApps tree through one restore action' {
        $entered = Reapply-SavedSpicetifySetup -Config @{} -SafeMode

        $entered.Status | Should -Be 'Active'
        @(Get-SpicetifyConfigListValue -Key 'extensions').Count | Should -Be 0
        @(Get-SpicetifyConfigListValue -Key 'custom_apps').Count | Should -Be 0
        $script:InvokeCount | Should -Be 1
        $markerPath = Join-Path $global:CONFIG_DIR 'safe-mode-session.json'
        $envelope = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
        $marker = Read-ProtectedSafeModeMarker -Path $markerPath
        $manifest = Get-Content -LiteralPath (Join-Path $marker.snapshotPath 'safe-mode-manifest.json') -Raw | ConvertFrom-Json
        $envelope.schemaVersion | Should -Be 3
        @($envelope.PSObject.Properties.Name) | Should -Be @('schemaVersion', 'protectedState')
        $envelope.protectedState | Should -Match '^[A-Za-z0-9+/]+={0,2}$'
        $marker.schemaVersion | Should -Be 3
        $marker.status | Should -Be 'Active'
        $marker.manifestSha256 | Should -Match '^[0-9a-f]{64}$'
        $manifest.customAppsFileCount | Should -Be 2
        $activeEntry = $script:Journal | Where-Object Result -EQ 'Active' | Select-Object -Last 1
        $activeEntry.Reversible | Should -BeTrue
        $activeEntry.TokenKind | Should -Be 'safeModeSession'

        [System.IO.File]::WriteAllText($script:Integration.ConfigPath, '[Setting]`ncustom_apps = intruder')
        Remove-Item -LiteralPath $script:Integration.CustomAppsDirectory -Recurse -Force
        New-Item -Path $script:Integration.CustomAppsDirectory -ItemType Directory -Force | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $script:Integration.CustomAppsDirectory 'intruder.js'), 'changed')

        $restored = Reapply-SavedSpicetifySetup -Config @{} -RestoreSafeMode

        $restored.Status | Should -Be 'Restored'
        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath)) | Should -Be $script:OriginalConfigBase64
        @(Get-ChildItem -LiteralPath $script:Integration.CustomAppsDirectory -File -Recurse | ForEach-Object {
            $_.FullName.Substring($script:Integration.CustomAppsDirectory.Length + 1)
        } | Sort-Object) | Should -Be @('alpha\index.js', 'root.txt')
        (Test-Path -LiteralPath (Join-Path $script:Integration.CustomAppsDirectory 'alpha\empty') -PathType Container) | Should -BeTrue
        (Test-Path -LiteralPath $markerPath) | Should -BeFalse
        $script:ApplyCount | Should -Be 1
        @($script:Journal | Where-Object Result -EQ 'Restored').Count | Should -Be 1
    }

    It 'refuses a tampered snapshot before clearing or applying the live setup' {
        $null = Reapply-SavedSpicetifySetup -Config @{} -SafeMode
        $markerPath = Join-Path $global:CONFIG_DIR 'safe-mode-session.json'
        $marker = Read-ProtectedSafeModeMarker -Path $markerPath
        [System.IO.File]::AppendAllText((Join-Path $marker.snapshotPath 'config-xpui.ini'), 'tampered')
        $liveBefore = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath))

        { Reapply-SavedSpicetifySetup -Config @{} -RestoreSafeMode } | Should -Throw -ExpectedMessage '*failed SHA256 verification*'

        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath)) | Should -Be $liveBefore
        (Test-Path -LiteralPath $markerPath -PathType Leaf) | Should -BeTrue
        $script:ClearCount | Should -Be 0
        $script:ApplyCount | Should -Be 0
    }

    It 'refuses recovery fields injected into the minimal marker before touching the live setup' {
        $null = Reapply-SavedSpicetifySetup -Config @{} -SafeMode
        $markerPath = Join-Path $global:CONFIG_DIR 'safe-mode-session.json'
        $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
        $marker | Add-Member -NotePropertyName customAppsExisted -NotePropertyValue $false
        $marker | Add-Member -NotePropertyName customAppsFiles -NotePropertyValue @()
        [System.IO.File]::WriteAllText($markerPath, ($marker | ConvertTo-Json -Depth 5))
        $liveBefore = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath))

        { Reapply-SavedSpicetifySetup -Config @{} -RestoreSafeMode } | Should -Throw -ExpectedMessage '*unexpected or invalid fields*'

        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath)) | Should -Be $liveBefore
        $script:ClearCount | Should -Be 0
        $script:ApplyCount | Should -Be 0
    }

    It 'refuses a tampered recovery manifest before touching the live setup' {
        $null = Reapply-SavedSpicetifySetup -Config @{} -SafeMode
        $markerPath = Join-Path $global:CONFIG_DIR 'safe-mode-session.json'
        $marker = Read-ProtectedSafeModeMarker -Path $markerPath
        $manifestPath = Join-Path $marker.snapshotPath 'safe-mode-manifest.json'
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $manifest.customAppsExisted = $false
        $manifest.customAppsFileCount = 0
        $manifest.customAppsFiles = @()
        [System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 7))
        $liveBefore = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath))

        { Reapply-SavedSpicetifySetup -Config @{} -RestoreSafeMode } | Should -Throw -ExpectedMessage '*manifest failed SHA256 verification*'

        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath)) | Should -Be $liveBefore
        $script:ClearCount | Should -Be 0
        $script:ApplyCount | Should -Be 0
    }

    It 'refuses coordinated protected-marker and manifest tampering before touching the live setup' {
        $null = Reapply-SavedSpicetifySetup -Config @{} -SafeMode
        $markerPath = Join-Path $global:CONFIG_DIR 'safe-mode-session.json'
        $marker = Read-ProtectedSafeModeMarker -Path $markerPath
        $manifestPath = Join-Path $marker.snapshotPath 'safe-mode-manifest.json'
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $manifest.customAppsExisted = $false
        $manifest.customAppsFileCount = 0
        $manifest.customAppsFiles = @()
        [System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 7))

        $envelope = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
        $protectedBytes = [Convert]::FromBase64String([string]$envelope.protectedState)
        $protectedBytes[[Math]::Floor($protectedBytes.Length / 2)] = $protectedBytes[[Math]::Floor($protectedBytes.Length / 2)] -bxor 0x01
        $envelope.protectedState = [Convert]::ToBase64String($protectedBytes)
        [System.IO.File]::WriteAllText($markerPath, ($envelope | ConvertTo-Json -Depth 3))
        $liveBefore = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath))

        { Reapply-SavedSpicetifySetup -Config @{} -RestoreSafeMode } | Should -Throw -ExpectedMessage '*could not be authenticated*'

        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath)) | Should -Be $liveBefore
        $script:ClearCount | Should -Be 0
        $script:ApplyCount | Should -Be 0
    }

    It 'refuses a coordinated marker and manifest downgrade to the unauthenticated legacy schema' {
        $null = Reapply-SavedSpicetifySetup -Config @{} -SafeMode
        $markerPath = Join-Path $global:CONFIG_DIR 'safe-mode-session.json'
        $marker = Read-ProtectedSafeModeMarker -Path $markerPath
        $manifestPath = Join-Path $marker.snapshotPath 'safe-mode-manifest.json'
        $legacy = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $legacy.schemaVersion = 1
        $legacy | Add-Member -NotePropertyName status -NotePropertyValue 'Active'
        $legacyJson = $legacy | ConvertTo-Json -Depth 7
        [System.IO.File]::WriteAllText($manifestPath, $legacyJson)
        [System.IO.File]::WriteAllText($markerPath, $legacyJson)
        $liveBefore = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath))

        { Reapply-SavedSpicetifySetup -Config @{} -RestoreSafeMode } | Should -Throw -ExpectedMessage '*Unsupported safe-mode recovery schema version*'

        [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($script:Integration.ConfigPath)) | Should -Be $liveBefore
        (Test-Path -LiteralPath $markerPath -PathType Leaf) | Should -BeTrue
        $script:ClearCount | Should -Be 0
        $script:ApplyCount | Should -Be 0
    }
}
