using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class OfflineAssetCacheRegressionTests
{
    [Fact]
    public async Task SharedInstallModules_FallBackToVerifiedCachedAssetsWhenNetworkFails()
    {
        using var result = await RunHarnessAsync();

        var fallback = result.RootElement.GetProperty("fallback");
        Assert.True(fallback.GetProperty("succeeded").GetBoolean(), result.RawOutput);
        Assert.Equal(5, fallback.GetProperty("networkAttemptCount").GetInt32());
        Assert.Equal(5, fallback.GetProperty("warningFallbackCount").GetInt32());
        Assert.Equal(5, fallback.GetProperty("verifiedCacheUseCount").GetInt32());
        AssertJsonArrayContains(fallback.GetProperty("networkAttempts"), "https://example.invalid/spotx-run.ps1");
        AssertJsonArrayContains(fallback.GetProperty("networkAttempts"), "https://example.invalid/spicetify-test-x64.zip");
        AssertJsonArrayContains(fallback.GetProperty("networkAttempts"), "https://example.invalid/themes.zip");
        AssertJsonArrayContains(fallback.GetProperty("networkAttempts"), "https://example.invalid/marketplace.zip");
        AssertJsonArrayContains(fallback.GetProperty("networkAttempts"), "https://example.invalid/stats.zip");
        AssertJsonArrayContains(fallback.GetProperty("expandedLabels"), "Spicetify CLI");
        AssertJsonArrayContains(fallback.GetProperty("expandedLabels"), "Themes archive");
        AssertJsonArrayContains(fallback.GetProperty("expandedLabels"), "Marketplace");
        AssertJsonArrayContains(fallback.GetProperty("expandedLabels"), "Custom app stats");
        Assert.True(fallback.GetProperty("spotxInvoked").GetBoolean());
        Assert.True(fallback.GetProperty("spicetifyCliInvoked").GetBoolean());
        Assert.True(fallback.GetProperty("themeInstalled").GetBoolean());
        Assert.True(fallback.GetProperty("marketplaceInstalled").GetBoolean());
        Assert.True(fallback.GetProperty("statsInstalled").GetBoolean());
    }

    [Fact]
    public async Task SharedInstallModules_StopBeforeInstallWhenCacheIsMissingOrCorrupt()
    {
        using var result = await RunHarnessAsync();

        var missing = result.RootElement.GetProperty("missing");
        Assert.True(missing.GetProperty("failed").GetBoolean(), result.RawOutput);
        Assert.Equal(1, missing.GetProperty("networkAttemptCount").GetInt32());
        Assert.Equal(0, missing.GetProperty("expandedCount").GetInt32());
        Assert.False(missing.GetProperty("marketplaceInstalled").GetBoolean());
        Assert.Contains("Simulated offline network failure", missing.GetProperty("message").GetString());

        var corrupt = result.RootElement.GetProperty("corrupt");
        Assert.True(corrupt.GetProperty("failed").GetBoolean(), result.RawOutput);
        Assert.Equal(1, corrupt.GetProperty("networkAttemptCount").GetInt32());
        Assert.Equal(0, corrupt.GetProperty("expandedCount").GetInt32());
        Assert.False(corrupt.GetProperty("marketplaceInstalled").GetBoolean());
        Assert.True(corrupt.GetProperty("corruptWarning").GetBoolean());
        Assert.True(corrupt.GetProperty("quarantined").GetBoolean());
    }

    private static async Task<HarnessResult> RunHarnessAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.OfflineCache.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var harnessPath = Path.Combine(root, "offline-cache-harness.ps1");
            await File.WriteAllTextAsync(harnessPath, HarnessScript);

            var start = new ProcessStartInfo("powershell")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = ResolveRepoRoot()
            };

            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(harnessPath);
            start.ArgumentList.Add("-RepoRoot");
            start.ArgumentList.Add(ResolveRepoRoot());
            start.ArgumentList.Add("-Root");
            start.ArgumentList.Add(root);

            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start powershell.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            Assert.True(process.ExitCode == 0, $"Harness exited {process.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
            var document = JsonDocument.Parse(stdout);
            return new HarnessResult(root, document, stdout);
        }
        catch
        {
            try { Directory.Delete(root, recursive: true); } catch { }
            throw;
        }
    }

    private static void AssertJsonArrayContains(JsonElement array, string expected)
    {
        Assert.Contains(array.EnumerateArray(), item => string.Equals(item.GetString(), expected, StringComparison.Ordinal));
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private sealed class HarnessResult : IDisposable
    {
        private readonly string root;
        private readonly JsonDocument document;

        public HarnessResult(string root, JsonDocument document, string rawOutput)
        {
            this.root = root;
            this.document = document;
            RawOutput = rawOutput;
        }

        public JsonElement RootElement => document.RootElement;

        public string RawOutput { get; }

        public void Dispose()
        {
            document.Dispose();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private const string HarnessScript = """
param(
    [Parameter(Mandatory = $true)][string]$RepoRoot,
    [Parameter(Mandatory = $true)][string]$Root
)

$ErrorActionPreference = 'Stop'

function Get-FileHash {
    param(
        [string]$LiteralPath,
        [string]$Algorithm = 'SHA256'
    )

    if ($Algorithm -ne 'SHA256') {
        throw "Unsupported test hash algorithm '$Algorithm'."
    }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($LiteralPath)
        try {
            $bytes = $sha.ComputeHash($stream)
        } finally {
            $stream.Dispose()
        }
    } finally {
        $sha.Dispose()
    }

    return [pscustomobject]@{
        Algorithm = 'SHA256'
        Hash = (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
        Path = $LiteralPath
    }
}

$sharedRoot = Join-Path $RepoRoot 'src\powershell\shared'
foreach ($name in @(
    'Confirm-FileHash',
    'Update-AssetCacheIndexEntry',
    'Get-FromAssetCache',
    'Save-ToAssetCache',
    'Get-LibreSpotTempRoot',
    'New-LibreSpotTempFile',
    'New-LibreSpotTempDirectory',
    'Module-InstallSpotX',
    'Module-InstallSpicetifyCLI',
    'Module-InstallThemes',
    'Module-InstallMarketplace',
    'Module-InstallCustomApps'
)) {
    . (Join-Path $sharedRoot "$name.ps1")
}

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $script:LogEntries += [pscustomobject]@{
        level = $Level
        message = $Message
    }
}

function Write-OperationJournalEntry {
    param(
        [string]$Phase,
        [string]$Target,
        [string]$SafetyDecision,
        [string]$Result,
        [bool]$WouldChange,
        [bool]$Reversible,
        [string]$RollbackHint,
        [object]$Data
    )

    $script:JournalEntries += [pscustomobject]@{
        phase = $Phase
        target = $Target
        result = $Result
        data = $Data
    }
}

function Download-FileSafe {
    param([string]$Uri, [string]$OutFile)
    $script:NetworkAttempts += $Uri
    if ($script:DeferredCache.ContainsKey($Uri)) {
        Write-AssetToCache -Asset $script:DeferredCache[$Uri]
    }

    throw "Simulated offline network failure for $Uri"
}

function Expand-ArchiveSafely {
    param(
        [string]$ZipPath,
        [string]$DestinationPath,
        [string]$Label,
        [long]$MaxExpandedBytes = 0
    )

    $script:ExpandedLabels += $Label
    New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
    switch -Wildcard ($Label) {
        'Spicetify CLI*' {
            Set-TextFile -Path (Join-Path $DestinationPath 'spicetify.exe') -Value 'stub spicetify'
        }
        'Themes archive' {
            $themeRoot = Join-Path $DestinationPath 'spicetify-themes-test'
            $theme = Join-Path $themeRoot 'Dribbblish'
            New-Item -Path $theme -ItemType Directory -Force | Out-Null
            Set-TextFile -Path (Join-Path $theme 'color.ini') -Value '[Base]'
            Set-TextFile -Path (Join-Path $theme 'user.css') -Value 'body {}'
        }
        'Marketplace*' {
            $marketplace = Join-Path $DestinationPath 'marketplace-dist'
            New-Item -Path $marketplace -ItemType Directory -Force | Out-Null
            Set-TextFile -Path (Join-Path $marketplace 'manifest.json') -Value '{"name":"marketplace"}'
            Set-TextFile -Path (Join-Path $marketplace 'extension.js') -Value 'console.log("marketplace");'
        }
        'Custom app stats*' {
            $stats = Join-Path $DestinationPath 'stats'
            New-Item -Path $stats -ItemType Directory -Force | Out-Null
            Set-TextFile -Path (Join-Path $stats 'manifest.json') -Value '{"name":"stats"}'
            Set-TextFile -Path (Join-Path $stats 'extension.js') -Value 'console.log("stats");'
        }
    }
}

function Get-SpicetifyIntegrationContext { return $script:Integration }
function Invoke-SpicetifyCli { param([string[]]$Arguments, [string]$FailureMessage) $script:SpicetifyCliCalls += ($Arguments -join ' ') }
function Add-PathEntry { param([string]$Entry, [string]$Scope) return $true }
function Clear-DirectoryContentsSafely { param([string]$Path, [string]$Label) return $true }
function Sync-SpicetifyListSetting { param([string]$Key, [string[]]$DesiredItems, [string[]]$ManagedItems) $script:SyncedSettings += "$Key=$($DesiredItems -join ',')" }
function Remove-PathSafely { param([string]$Path, [string]$Label) if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }; return $true }
function Build-SpotXParams { param($Config) return '' }
function New-SpotXCustomPatchesFile { param($Config) return '' }
function Invoke-ExternalScriptIsolated { param([string]$FilePath, [string]$Arguments) $script:ExternalScripts += $FilePath }
function Get-SpotXPatchVerification { param([string]$SpotifyExePath) return [pscustomobject]@{ Verified = $true; Signals = @('test'); Reason = '' } }
function Hide-SpotifyWindows {}
function Stop-SpotifyProcesses { param([int]$maxAttempts = 3) }
function Start-Process {}
function Start-Sleep {}
function Get-MarketplaceHealth {
    $marketplace = Join-Path $script:Integration.CustomAppsDirectory 'marketplace'
    $hasFiles = Test-Path -LiteralPath (Join-Path $marketplace 'manifest.json') -PathType Leaf
    return [pscustomobject]@{
        HasFiles = $hasFiles
        IsReady = $hasFiles
        Status = if ($hasFiles) { 'Ready' } else { 'Missing' }
    }
}

function Set-TextFile {
    param([string]$Path, [string]$Value)
    $directory = Split-Path -Path $Path -Parent
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -Path $directory -ItemType Directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Value, [System.Text.UTF8Encoding]::new($false))
}

function New-AssetSpec {
    param([string]$Name, [string]$Uri)
    $seedPath = Join-Path $script:ScenarioRoot "$Name.seed"
    Set-TextFile -Path $seedPath -Value "offline-cache-$Name"
    $hash = (Get-FileHash -LiteralPath $seedPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $bytes = [System.IO.File]::ReadAllBytes($seedPath)
    Remove-Item -LiteralPath $seedPath -Force
    return [pscustomobject]@{
        name = $Name
        uri = $Uri
        hash = $hash
        bytes = $bytes
    }
}

function Write-AssetToCache {
    param([object]$Asset)
    if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
        New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
    }

    [System.IO.File]::WriteAllBytes((Join-Path $global:CACHE_DIR $Asset.hash), [byte[]]$Asset.bytes)
}

function Reset-Scenario {
    param([string]$Name)

    $script:ScenarioRoot = Join-Path $Root $Name
    $global:CONFIG_DIR = Join-Path $script:ScenarioRoot 'config'
    $global:CACHE_DIR = Join-Path $global:CONFIG_DIR 'cache'
    $global:TEMP_DIR = Join-Path $script:ScenarioRoot 'temp'
    $spotifyDirectory = Join-Path $script:ScenarioRoot 'AppData\Roaming\Spotify'
    $global:SPOTIFY_EXE_PATH = Join-Path $spotifyDirectory 'Spotify.exe'
    $script:Integration = [pscustomobject]@{
        InstallDirectory = Join-Path $script:ScenarioRoot 'AppData\Local\spicetify'
        CliPath = Join-Path $script:ScenarioRoot 'AppData\Local\spicetify\spicetify.exe'
        ThemesDirectory = Join-Path $script:ScenarioRoot 'AppData\Roaming\spicetify\Themes'
        CustomAppsDirectory = Join-Path $script:ScenarioRoot 'AppData\Roaming\spicetify\CustomApps'
        MarketplaceDirectory = Join-Path $script:ScenarioRoot 'AppData\Roaming\spicetify\CustomApps\marketplace'
        LegacyMarketplaceDirectory = Join-Path $script:ScenarioRoot 'AppData\Roaming\spicetify\Apps\marketplace'
        ConfigDirectory = Join-Path $script:ScenarioRoot 'AppData\Roaming\spicetify'
    }

    foreach ($path in @($global:CONFIG_DIR, $global:CACHE_DIR, $global:TEMP_DIR, $spotifyDirectory, $script:Integration.ConfigDirectory)) {
        New-Item -Path $path -ItemType Directory -Force | Out-Null
    }

    Set-TextFile -Path $global:SPOTIFY_EXE_PATH -Value 'spotify'
    Set-TextFile -Path (Join-Path $spotifyDirectory 'chrome_elf.dll') -Value 'chrome_elf'
    $env:PROCESSOR_ARCHITECTURE = 'AMD64'
    $script:LogEntries = @()
    $script:JournalEntries = @()
    $script:NetworkAttempts = @()
    $script:ExpandedLabels = @()
    $script:SpicetifyCliCalls = @()
    $script:SyncedSettings = @()
    $script:ExternalScripts = @()
    $script:DeferredCache = @{}
}

function New-AssetSet {
    return @{
        spotx = New-AssetSpec -Name 'spotx' -Uri 'https://example.invalid/spotx-run.ps1'
        spicetify = New-AssetSpec -Name 'spicetify' -Uri 'https://example.invalid/spicetify-test-x64.zip'
        themes = New-AssetSpec -Name 'themes' -Uri 'https://example.invalid/themes.zip'
        marketplace = New-AssetSpec -Name 'marketplace' -Uri 'https://example.invalid/marketplace.zip'
        stats = New-AssetSpec -Name 'stats' -Uri 'https://example.invalid/stats.zip'
    }
}

function Configure-Pins {
    param([hashtable]$Assets)

    $global:PinnedReleases = @{
        SpotX = @{
            Version = 'test'
            SHA256 = $Assets.spotx.hash
        }
        SpicetifyCLI = @{
            Version = 'test'
            SHA256 = @{
                x64 = $Assets.spicetify.hash
                arm64 = $Assets.spicetify.hash
            }
        }
        Marketplace = @{
            SHA256 = $Assets.marketplace.hash
        }
        Themes = @{
            SHA256 = $Assets.themes.hash
        }
    }

    $global:URL_SPOTX = $Assets.spotx.uri
    $global:URL_SPICETIFY_FMT = 'https://example.invalid/spicetify-{0}-{1}.zip'
    $global:URL_MARKETPLACE = $Assets.marketplace.uri
    $global:URL_THEMES_REPO = $Assets.themes.uri
    $global:CommunityThemeRepos = @{}
    $global:ThemesNeedingJS = @()
    $global:CommunityCustomApps = [ordered]@{
        stats = @{
            DisplayName = 'Stats'
            Source = 'test/stats'
            Url = $Assets.stats.uri
            AssetPath = 'stats'
            SHA256 = $Assets.stats.hash
        }
    }
}

function New-TestConfig {
    return [pscustomobject]@{
        Spicetify_Theme = 'Dribbblish'
        Spicetify_Scheme = 'Base'
        Spicetify_Marketplace = $true
        Spicetify_CustomApps = @('stats')
    }
}

function Invoke-InstallSet {
    $config = New-TestConfig
    Module-InstallSpotX -Config $config -SyncHash $null
    Module-InstallSpicetifyCLI
    Module-InstallThemes -Config $config
    Module-InstallMarketplace -Config $config
    Module-InstallCustomApps -Config $config
}

function Run-FallbackScenario {
    Reset-Scenario -Name 'fallback'
    $assets = New-AssetSet
    Configure-Pins -Assets $assets
    foreach ($asset in $assets.Values) {
        $script:DeferredCache[$asset.uri] = $asset
    }

    Invoke-InstallSet

    return [pscustomobject]@{
        succeeded = $true
        networkAttemptCount = @($script:NetworkAttempts).Count
        networkAttempts = @($script:NetworkAttempts)
        warningFallbackCount = @($script:LogEntries | Where-Object { $_.level -eq 'WARN' -and $_.message -eq 'Network download failed; using verified cached copy.' }).Count
        verifiedCacheUseCount = @($script:LogEntries | Where-Object { $_.level -ne 'WARN' -and $_.message -like '  Using verified cached copy*' }).Count
        expandedLabels = @($script:ExpandedLabels)
        spotxInvoked = @($script:ExternalScripts).Count -eq 1
        spicetifyCliInvoked = @($script:SpicetifyCliCalls | Where-Object { $_ -like 'config --bypass-admin*' }).Count -gt 0
        themeInstalled = Test-Path -LiteralPath (Join-Path $script:Integration.ThemesDirectory 'Dribbblish\color.ini') -PathType Leaf
        marketplaceInstalled = Test-Path -LiteralPath (Join-Path $script:Integration.CustomAppsDirectory 'marketplace\manifest.json') -PathType Leaf
        statsInstalled = Test-Path -LiteralPath (Join-Path $script:Integration.CustomAppsDirectory 'stats\manifest.json') -PathType Leaf
    }
}

function Run-MissingScenario {
    Reset-Scenario -Name 'missing'
    $assets = New-AssetSet
    Configure-Pins -Assets $assets
    try {
        Module-InstallMarketplace -Config (New-TestConfig)
        $failed = $false
        $message = ''
    } catch {
        $failed = $true
        $message = $_.Exception.Message
    }

    return [pscustomobject]@{
        failed = $failed
        message = $message
        networkAttemptCount = @($script:NetworkAttempts).Count
        expandedCount = @($script:ExpandedLabels).Count
        marketplaceInstalled = Test-Path -LiteralPath (Join-Path $script:Integration.CustomAppsDirectory 'marketplace\manifest.json') -PathType Leaf
    }
}

function Run-CorruptScenario {
    Reset-Scenario -Name 'corrupt'
    $assets = New-AssetSet
    Configure-Pins -Assets $assets
    Set-TextFile -Path (Join-Path $global:CACHE_DIR $assets.marketplace.hash) -Value 'wrong cached bytes'
    try {
        Module-InstallMarketplace -Config (New-TestConfig)
        $failed = $false
    } catch {
        $failed = $true
    }

    return [pscustomobject]@{
        failed = $failed
        networkAttemptCount = @($script:NetworkAttempts).Count
        expandedCount = @($script:ExpandedLabels).Count
        marketplaceInstalled = Test-Path -LiteralPath (Join-Path $script:Integration.CustomAppsDirectory 'marketplace\manifest.json') -PathType Leaf
        corruptWarning = @($script:LogEntries | Where-Object { $_.message -like '*failed re-verification*' -and $_.level -eq 'WARN' }).Count -eq 1
        quarantined = (Test-Path -LiteralPath (Join-Path $global:CACHE_DIR 'corrupt') -PathType Container) -and @((Get-ChildItem -LiteralPath (Join-Path $global:CACHE_DIR 'corrupt') -File)).Count -eq 1
    }
}

$report = [ordered]@{
    fallback = Run-FallbackScenario
    missing = Run-MissingScenario
    corrupt = Run-CorruptScenario
}

$report | ConvertTo-Json -Depth 12
""";
}
