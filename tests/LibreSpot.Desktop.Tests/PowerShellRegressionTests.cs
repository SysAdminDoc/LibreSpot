using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

/// <summary>
/// Regression guards that parse the PowerShell monolith + backend script as text.
/// These aren't rich PS tests (we'd need Pester for that), but they lock in the
/// invariants most likely to drift and re-break the v3.5.1 hardening pass:
///   - No false-positive files in the foreign-patch detection list.
///   - PowerShell + backend $global:VERSION stay in sync.
///   - Compare-LibreSpotVersions semantics stay semver-aware.
/// If you find yourself wanting to skip one of these, look at the CHANGELOG
/// v3.5.1 entry first — the bug it prevents is documented there.
/// </summary>
public sealed class PowerShellRegressionTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static string ResolveRepoRoot()
    {
        // Walk up from the test assembly until we find LibreSpot.ps1 next to the folder.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate LibreSpot.ps1 from the test runner.");
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    // ---------------------------------------------------------------------
    // Foreign-patch detection — v3.5.1 false-positive guard.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("chrome_elf.dll", "ships with every Spotify install; LibreSpot requires it at install time")]
    [InlineData("xpui.spa.bak", "produced by every successful SpotX run")]
    public void ForeignPatchSignatureList_DoesNotContain_KnownFalsePositive(string forbidden, string reason)
    {
        var script = ReadFile("LibreSpot.ps1");

        var match = Regex.Match(
            script,
            @"function\s+Get-ExistingSpotifyPatchSignature\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(match.Success, "Get-ExistingSpotifyPatchSignature function block not found in LibreSpot.ps1.");

        // Allow mentions inside the leading comment block (intentional historical note),
        // but the executable body must not reference the forbidden file.
        var body = match.Groups["body"].Value;
        var codeLines = string.Join("\n", body
            .Split('\n')
            .Where(line => !Regex.IsMatch(line, @"^\s*#")));

        Assert.DoesNotContain(forbidden, codeLines);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void ForeignPatchWarning_UsesBlockTheSpotMigrationCopy()
    {
        var script = ReadFile("LibreSpot.ps1");

        Assert.Contains("BlockTheSpot-family legacy patcher", script);
        Assert.Contains("Legacy BlockTheSpot config.ini", script);
        Assert.Contains("LibreSpot can cleanly replace BlockTheSpot-family DLL-injection artifacts", script);
        Assert.DoesNotContain("active foreign patch", script, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Version sync — v3.5.1 stale-backend-version guard.
    // ---------------------------------------------------------------------
    [Fact]
    public void PowerShellMonolith_AndBackend_StayOnTheSameVersion()
    {
        var monolith = ReadFile("LibreSpot.ps1");
        var backend  = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");

        var versionRegex = new Regex(@"^\$global:VERSION\s*=\s*'([^']+)'", RegexOptions.Multiline);
        var monolithVersion = versionRegex.Match(monolith).Groups[1].Value;
        var backendVersion  = versionRegex.Match(backend).Groups[1].Value;

        Assert.False(string.IsNullOrWhiteSpace(monolithVersion), "LibreSpot.ps1 must declare $global:VERSION.");
        Assert.False(string.IsNullOrWhiteSpace(backendVersion),  "Backend.ps1 must declare $global:VERSION.");
        Assert.Equal(monolithVersion, backendVersion);
    }

    // ---------------------------------------------------------------------
    // Version compare helper — locks in the semver-aware semantics introduced
    // alongside Check-ForUpdates to kill the 2.43.9 vs 2.43.10 lexical bug.
    // We exercise the helper by grepping its signature and a handful of unit
    // cases expressed as comment annotations; the helper itself is PowerShell
    // so we can't call it directly from xUnit without Pester. Checking that
    // the helper exists, handles [Version] parsing, and strips -preview.* /
    // -rc.* is enough of a smoke test to catch most accidental reversions.
    // ---------------------------------------------------------------------
    [Fact]
    public void CompareLibreSpotVersions_HelperExists()
    {
        var script = ReadFile("LibreSpot.ps1");
        Assert.Contains("function Compare-LibreSpotVersions", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void UpdateChecker_UsesSemverComparisonInBothPowerShellPaths(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("function Compare-LibreSpotVersions", script);
        Assert.Contains("Compare-LibreSpotVersions -Latest", script);
        Assert.DoesNotContain("$latest -ne $global:PinnedReleases.SpicetifyCLI.Version", script);
        Assert.DoesNotContain("$latest -ne $global:PinnedReleases.Marketplace.Version", script);
    }

    [Theory]
    [InlineData("[Version]")]
    [InlineData("-preview")]
    [InlineData("-rc")]
    public void CompareLibreSpotVersions_MentionsSemverCorrectness(string token)
    {
        var script = ReadFile("LibreSpot.ps1");

        var fnBody = Regex.Match(
            script,
            @"function\s+Compare-LibreSpotVersions\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, "Compare-LibreSpotVersions function block not found.");
        Assert.Contains(token, fnBody.Groups["body"].Value);
    }

    [Fact]
    public void RequiredHelpers_AreExportedIntoWorkerRunspace()
    {
        // The maintenance worker runspace runs with an explicit allow-list of function
        // names. Check-ForUpdates calls Compare-LibreSpotVersions at runtime, so the
        // helper has to be on that list or the worker throws "command not found."
        var script = ReadFile("LibreSpot.ps1");
        var listMatch = Regex.Match(
            script,
            @"'Update-UI'\s*,\s*'Write-Log'[^\)]+?\)",
            RegexOptions.Singleline);
        Assert.True(listMatch.Success, "Worker function export list not found.");
        Assert.Contains("Compare-LibreSpotVersions", listMatch.Value);
        Assert.Contains("Get-LibreSpotCurrentSpotifyTarget", listMatch.Value);
        Assert.Contains("Get-LibreSpotCompatibilityWarnings", listMatch.Value);
        Assert.Contains("Write-LibreSpotCompatibilityMatrix", listMatch.Value);
        Assert.Contains("Write-SpicetifyCliOutputLine", listMatch.Value);
    }

    [Fact]
    public void CleanCliFlag_StartsEasyCleanInstall()
    {
        var script = ReadFile("LibreSpot.ps1");
        var blockMatch = Regex.Match(
            script,
            @"if\s*\(\$script:CliClean\)\s*\{(?<body>.+?)# =============================================================================\s*\r?\n# 19\. LAUNCH",
            RegexOptions.Singleline);

        Assert.True(blockMatch.Success, "CliClean startup block not found.");
        var body = blockMatch.Groups["body"].Value;
        Assert.Contains("Add_ContentRendered", body);
        Assert.Contains("Get-InstallConfig -EasyMode $true", body);
        Assert.Contains("$script:InstallConfig.CleanInstall = $true", body);
        Assert.Contains("$window.WindowState = 'Minimized'", body);
        Assert.Contains("Start-InstallJob -Config $script:InstallConfig", body);
    }

    [Fact]
    public void SelfElevation_PreservesCliFlags()
    {
        var script = ReadFile("LibreSpot.ps1");
        var blockMatch = Regex.Match(
            script,
            @"\$elevationArgs\s*=\s*@\(\)(?<body>.+?)Start-Process\s+-FilePath\s+'powershell\.exe'",
            RegexOptions.Singleline);

        Assert.True(blockMatch.Success, "Self-elevation CLI flag preservation block not found.");
        var body = blockMatch.Groups["body"].Value;
        Assert.Contains("$script:CliClean", body);
        Assert.Contains("'-clean'", body);
        Assert.Contains("$script:CliWatch", body);
        Assert.Contains("'-watch'", body);
        Assert.Contains("$script:CliInstallWatcher", body);
        Assert.Contains("'-installwatcher'", body);
        Assert.Contains("$script:CliUninstallWatcher", body);
        Assert.Contains("'-uninstallwatcher'", body);
        Assert.Contains("ArgumentList", body);
    }

    [Fact]
    public void SelfElevationFallback_UsesThemedBootstrapNotice()
    {
        var script = ReadFile("LibreSpot.ps1");

        Assert.Contains("function Show-BootstrapNotice", script);
        Assert.Contains("Show-BootstrapNotice -Title 'LibreSpot' -Message", script);
        Assert.DoesNotContain("System.Windows.MessageBox", script);
    }

    // ---------------------------------------------------------------------
    // Self-update async refactor — locks the UI-freeze fix in place.
    // ---------------------------------------------------------------------
    [Fact]
    public void SelfUpdateBannerRefresh_UsesThreadPool_NotDispatcherOnly()
    {
        var script = ReadFile("LibreSpot.ps1");

        var fnBody = Regex.Match(
            script,
            @"function\s+Start-SelfUpdateBannerRefresh\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, "Start-SelfUpdateBannerRefresh function block not found.");
        // The thread-pool hop is the whole point — without it the HTTP call
        // blocks the UI thread for up to 5 seconds on a slow GitHub response.
        Assert.Contains("ThreadPool", fnBody.Groups["body"].Value);
        Assert.Contains("QueueUserWorkItem", fnBody.Groups["body"].Value);
    }

    // ---------------------------------------------------------------------
    // Auto-reapply watcher — Track 4.2 (v3.6.0).
    // ---------------------------------------------------------------------
    [Fact]
    public void Watcher_WatchEntryExits_AfterBuildSpotXParamsDefined()
    {
        // The -Watch CLI path calls Invoke-AutoReapplyWatcher, which in turn
        // calls Build-SpotXParams / Load-LibreSpotConfig / Normalize-LibreSpotConfig.
        // Those functions are all defined later in the script, so the -Watch
        // exit MUST live after them — otherwise the call fails at runtime.
        var script = ReadFile("LibreSpot.ps1");

        var buildDefIndex = script.IndexOf("function Build-SpotXParams", StringComparison.Ordinal);
        Assert.True(buildDefIndex > 0, "Build-SpotXParams definition not found.");

        var cliWatchIndex = script.IndexOf("if ($script:CliWatch) {", buildDefIndex, StringComparison.Ordinal);
        Assert.True(
            cliWatchIndex > buildDefIndex,
            "-Watch exit branch must be placed AFTER Build-SpotXParams is defined.");
    }

    [Fact]
    public void Watcher_CliEntryPoints_AllExitExplicitly()
    {
        // Each CLI flag handler (-watch, -installwatcher, -uninstallwatcher)
        // needs to `exit` — otherwise it falls through into the WPF loader and
        // the scheduled task stalls waiting for XAML that never renders.
        var script = ReadFile("LibreSpot.ps1");

        foreach (var entry in new (string Flag, string? Log)[]
        {
            ("CliWatch", null),
            ("CliInstallWatcher", "CLI: -installwatcher"),
            ("CliUninstallWatcher", "CLI: -uninstallwatcher")
        })
        {
            var branches = Regex.Matches(
                script,
                @"if\s*\(\s*\$script:" + entry.Flag + @"\s*\)\s*\{(?<body>.+?)^\}",
                RegexOptions.Singleline | RegexOptions.Multiline);

            var foundExitBranch = false;
            foreach (Match branch in branches)
            {
                var body = branch.Groups["body"].Value;
                if (!body.Contains("exit")) { continue; }
                if (entry.Log is not null && !body.Contains(entry.Log)) { continue; }
                foundExitBranch = true;
                break;
            }

            Assert.True(foundExitBranch, $"Could not locate explicit-exit CLI entry branch for $script:{entry.Flag}.");
        }
    }

    [Fact]
    public void Watcher_TaskDefinition_EmitsValidXmlShape()
    {
        // The Register-AutoReapplyTask function builds an XML task definition
        // and hands it to schtasks /Create /XML. A typo in the namespace or a
        // missing required element crashes the scheduler silently, which shows
        // up later as "watcher registered but never fires." These assertions
        // lock in the shape the Windows Task Scheduler actually requires.
        var script = ReadFile("LibreSpot.ps1");

        var fn = Regex.Match(
            script,
            @"function\s+Register-AutoReapplyTask\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fn.Success, "Register-AutoReapplyTask function block not found.");

        var body = fn.Groups["body"].Value;
        Assert.Contains("http://schemas.microsoft.com/windows/2004/02/mit/task", body);
        Assert.Contains("<LogonTrigger>", body);
        Assert.Contains("<Repetition>", body);
        Assert.Contains("<Actions Context=\"Author\">", body);
        Assert.Contains("<RunLevel>LeastPrivilege</RunLevel>", body);
        // schtasks /Create /XML requires UTF-16 LE — a UTF-8 write makes
        // schtasks emit "ERROR: Invalid XML" without explaining why.
        Assert.Contains("System.Text.Encoding]::Unicode", body);
    }

    [Fact]
    public void Watcher_HeadlessReapply_VerifiesHashBeforeRunningSpotX()
    {
        // The watcher runs unattended via a scheduled task — it must never
        // execute SpotX script content it didn't hash-verify, or a
        // man-in-the-middle between github and the user becomes a pre-auth
        // arbitrary-code-execution vector.
        var script = ReadFile("LibreSpot.ps1");

        var fn = Regex.Match(
            script,
            @"function\s+Invoke-HeadlessReapply\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fn.Success, "Invoke-HeadlessReapply function block not found.");

        var body = fn.Groups["body"].Value;
        Assert.Contains("Get-FileHash", body);
        Assert.Contains("PinnedReleases.SpotX.SHA256", body);
        Assert.Contains("hash mismatch", body);
    }

    [Fact]
    public void Watcher_OnFirstRun_InitializesStateWithoutReapplying()
    {
        // If a user enables the watcher for the first time on a fresh Spotify
        // install, the very first tick must only RECORD the current Spotify
        // version — NOT immediately reapply. Otherwise we clobber an untouched
        // install that the user hasn't configured yet.
        var script = ReadFile("LibreSpot.ps1");

        var fn = Regex.Match(
            script,
            @"function\s+Invoke-AutoReapplyWatcher\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fn.Success, "Invoke-AutoReapplyWatcher function block not found.");

        var body = fn.Groups["body"].Value;
        Assert.Contains("if (-not $state.LastKnownVersion)", body);
        Assert.Contains("Initialized", body);
    }

    [Fact]
    public void Watcher_SkipsWhenSpotifyIsRunning()
    {
        // Reapplying while Spotify is actively playing audio would kill the
        // user's session and dump them back at the login screen. The watcher
        // defers to the next tick instead.
        var script = ReadFile("LibreSpot.ps1");

        var fn = Regex.Match(
            script,
            @"function\s+Invoke-AutoReapplyWatcher\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fn.Success);
        Assert.Contains("Test-SpotifyRunning", fn.Groups["body"].Value);
        Assert.Contains("DeferredSpotifyRunning", fn.Groups["body"].Value);
    }

    [Theory]
    [InlineData("EnableAutoReapply")]
    [InlineData("DisableAutoReapply")]
    [InlineData("WatchAutoReapply")]
    public void DesktopBackend_ExposesAutoReapplyActions(string action)
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        Assert.Contains($"'{action}'", backend);
    }

    [Fact]
    public void DesktopBackend_CanRegisterAndRunAutoReapplyWatcher()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");

        Assert.Contains("function Register-AutoReapplyTask", backend);
        Assert.Contains("function Unregister-AutoReapplyTask", backend);
        Assert.Contains("function Invoke-AutoReapplyWatcher", backend);
        Assert.Contains("function Invoke-HeadlessReapply", backend);
        Assert.Contains("-Action WatchAutoReapply", backend);
        Assert.Contains("<Arguments>$escapedArguments</Arguments>", backend);
        Assert.Contains("System.Text.Encoding]::Unicode", backend);
        Assert.Contains("AutoReapply_Enabled", backend);
    }

    [Fact]
    public void DesktopBackend_WatcherVerifiesIntegrityBeforeRunning()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");

        Assert.Contains("LibreSpot.Backend.ps1.sha256", backend);
        Assert.Contains("SHA256]::Create()", backend);
        Assert.Contains("Integrity check failed", backend);
    }

    [Fact]
    public void AutoReapply_IsPartOfBooleanNormalization()
    {
        // Config round-trips through a hand-maintained boolean-keys list. If
        // AutoReapply_Enabled ever drops off that list, the normalized config
        // silently forgets the preference on every load.
        var script = ReadFile("LibreSpot.ps1");
        var booleanBlock = Regex.Match(
            script,
            @"\$booleanKeys\s*=\s*@\((?<body>.+?)\)",
            RegexOptions.Singleline);
        Assert.True(booleanBlock.Success, "Could not locate $booleanKeys block.");
        Assert.Contains("AutoReapply_Enabled", booleanBlock.Groups["body"].Value);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void PowerShellNormalizer_ResolvesLyricsModeConflicts(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Normalize-LibreSpotConfig\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Normalize-LibreSpotConfig function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("-not $normalized.SpotX_LyricsEnabled", body);
        Assert.Contains("$normalized.SpotX_LyricsBlock = $false", body);
        Assert.Contains("$normalized.SpotX_OldLyrics = $false", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void BuildSpotXParams_GatesMutuallyExclusiveLyricsFlags(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Build-SpotXParams\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Build-SpotXParams function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("if ($Config.SpotX_LyricsEnabled)", body);
        Assert.Contains("elseif ($Config.SpotX_OldLyrics)", body);
        Assert.DoesNotMatch(@"(?m)^\s*if\s+\(\$Config\.SpotX_OldLyrics\)", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void ExternalScriptRunner_ToleratesUnavailableExitCode(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Invoke-ExternalScriptIsolated\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Invoke-ExternalScriptIsolated function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("$null -eq $exitCode", body);
        Assert.Contains("ExitCode was unavailable", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyCliPin_UsesCurrentTestedRelease(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("Version = '2.43.2'", script);
        Assert.Contains("WindowsMinSpotify = '1.2.14'", script);
        Assert.Contains("WindowsMaxTestedSpotify = '1.2.88'", script);
        Assert.Contains("CompatibilityUrl = 'https://github.com/spicetify/cli/releases/tag/v2.43.2'", script);
        Assert.Contains("fc6ed7b67f15a8e49e6f676ca0511b63ef74736c05593966abf20a90e06aa80d", script);
        Assert.Contains("ed90e11d82affdcf7ae2968a886c8b9500c08f521c271598f13d6d9414110473", script);
        Assert.DoesNotContain("Version = '2.43.1'", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void UpdateChecker_ReportsSeparateCompatibilityMatrixStatuses(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("function Get-LibreSpotCurrentSpotifyTarget", script);
        Assert.Contains("function Get-LibreSpotCompatibilityWarnings", script);
        Assert.Contains("function Write-LibreSpotCompatibilityMatrix", script);
        Assert.Contains("Write-LibreSpotCompatibilityMatrix", script);
        Assert.Contains("SpotX target Spotify", script);
        Assert.Contains("max-tested Windows/Microsoft Store Spotify", script);
        Assert.Contains("Spicetify CSS maps may need validation after patching", script);
        Assert.Contains("Marketplace:", script);
        Assert.Contains("Themes:", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpotXPin_UsesCurrentTestedCommitAndSpotifyBaseline(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("3284673df69e276c5c0ee90bb1cc9185cecb9ad4", script);
        Assert.Contains("18684432f8b9ec1c6d7d2481192afc0bcad670aa769a306480948a3e690cc823", script);
        Assert.Contains("1.2.92", script);
        Assert.DoesNotContain("95882aa5b308832102ac8a206d300bf6f5436bfb", script);
        Assert.DoesNotContain("67e7ad2ec42531712f33959b1170590d48f7b2940a9a478f956b5770a69b1af3", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void ThemeArchivePin_UsesCurrentTestedCommitAndHash(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("df033493a7dae30ca6e371de9cec1897871dbb0c", script);
        Assert.Contains("c837828c71d7a938898f87965b1fe9e5812cec831bd9cb1619bd8feb6020fdc3", script);
        Assert.DoesNotContain("9af41cf91af6f6093c0e060d57264f08f6bb161c", script);
        Assert.DoesNotContain("fd55e443e88302dfd45e201f35ec67db5f51c4346b58fab5da90faf7b1a66f28", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void CommunityExtensionCatalog_UsesPinnedWorkingAssetsAndHashVerification(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var catalogStart = script.IndexOf("$global:CommunityExtensions", StringComparison.Ordinal);
        var catalogEnd = script.IndexOf("$global:CommunityExtensionAliases", StringComparison.Ordinal);

        Assert.True(catalogStart >= 0, $"Community extension catalog not found in {relativePath}.");
        Assert.True(catalogEnd > catalogStart, $"Community extension catalog terminator not found in {relativePath}.");

        var catalog = script[catalogStart..catalogEnd];

        Assert.Contains("hidePodcasts.js", catalog);
        Assert.Contains("b89365dd86fba24d610fae65d882d7e14a69f2fa/hidePodcasts.js", catalog);
        Assert.Contains("727e5a2f9137f4be77eac83d234a0ce858c5d618e7ff56116a6def01793fc3f8", catalog);

        Assert.Contains("beautiful-lyrics.mjs", catalog);
        Assert.Contains("61ac582da092311e893423269ca7f09003108705/Extension/Builds/Release/beautiful-lyrics.mjs", catalog);
        Assert.Contains("93c9ecfcb0a83c832c5ee7ca8fe826bcfaeec7cdd129c0bf05bab84b8ba6ba72", catalog);

        Assert.Contains("playlist-icons.js", catalog);
        Assert.Contains("8f401f923a5c25f530935faaceb39089a25b701a/playlist-icons.js", catalog);
        Assert.Contains("79bbe2bd6a52a521a382a73ef1c8c7ff0b0b9bd7674c48bb0ed44c5d2c944c8d", catalog);

        Assert.Contains("volumePercentage.js", catalog);
        Assert.Contains("89e609d933946a888cdff9cc3d7c4f1e9b88cfde/Extensions/volumePercentage.js", catalog);
        Assert.Contains("b88dcde894f4998abc4473773333015c09f0450ec563d256ed5af45db7129aca", catalog);

        Assert.DoesNotContain("/main/dist/", catalog);
        Assert.DoesNotContain("Shinyhero36/spicetify-song-stats", catalog);
        Assert.DoesNotContain("'songStats.js'        = @{", catalog);
        Assert.Contains("Confirm-FileHash -Path $destFile -ExpectedHash $info.SHA256", script);
    }

    [Fact]
    public void PowerShellExtensionUi_UsesCurrentCommunityExtensionIds()
    {
        var script = ReadFile("LibreSpot.ps1");

        Assert.DoesNotContain("ChkExt_songStats", script);
        Assert.DoesNotContain("'ChkExt_beautifulLyrics'='beautifulLyrics.js'", script);
        Assert.DoesNotContain("'ChkExt_playlistIcons'='playlistIcons.js'", script);
        Assert.Contains("'ChkExt_beautifulLyrics'='beautiful-lyrics.mjs'", script);
        Assert.Contains("'ChkExt_playlistIcons'='playlist-icons.js'", script);
        Assert.Contains("$global:DeprecatedCommunityExtensionNames", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void MarketplaceInstaller_UsesSpicetifyConfigCustomApps(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Module-InstallMarketplace\s*(?:\{\s*param\(\$Config\)|\{\s*\r?\n\s*param\(\$Config\))(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Module-InstallMarketplace function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps'", body);
        Assert.DoesNotContain("Join-Path $global:SPICETIFY_DIR 'CustomApps'", body);
        Assert.Contains("Get-MarketplaceHealth", body);
        Assert.Contains("Marketplace archive did not produce expected Spicetify custom app files.", body);
        Assert.Contains("spotify:app:marketplace", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void MarketplaceHealthAndRepair_AreWiredInBothPowerShellPaths(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("function Get-MarketplaceHealth", script);
        Assert.Contains("'Ready'", script);
        Assert.Contains("'Hidden'", script);
        Assert.Contains("'FilesMissing'", script);
        Assert.Contains("'LegacyPath'", script);
        Assert.Contains("extension.js", script);
        Assert.Contains("manifest.json", script);
        Assert.Contains("function Repair-Marketplace", script);
        Assert.Contains("custom_apps", script);
        Assert.Contains("spotify:app:marketplace", script);
    }

    [Fact]
    public void PowerShellMaintenanceUi_ExposesMarketplaceRepairAction()
    {
        var script = ReadFile("LibreSpot.ps1");

        Assert.Contains("BtnRepairMarketplace", script);
        Assert.Contains("Repair and open Marketplace", script);
        Assert.Contains("Start-MaintenanceJob -Action 'RepairMarketplace'", script);
        Assert.Contains("'RepairMarketplace' { 'Marketplace repaired' }", script);
        Assert.Contains("Repair-Marketplace", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyCliRunner_StreamsNativeOutputAndTimesOut(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Invoke-SpicetifyCli\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Invoke-SpicetifyCli function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("$previousPreference = $ErrorActionPreference", body);
        Assert.Contains("$ErrorActionPreference = 'Continue'", body);
        Assert.Contains("System.Diagnostics.ProcessStartInfo", body);
        Assert.Contains("LibreSpotNativeOutputCollector", body);
        Assert.Contains("$collector.Attach($process)", body);
        Assert.Contains("$collector.TryDequeue", body);
        Assert.Contains("$collector.Detach($process)", body);
        Assert.Contains("BeginOutputReadLine", body);
        Assert.Contains("BeginErrorReadLine", body);
        Assert.Contains("WaitForExit(250)", body);
        Assert.Contains("RedirectStandardOutput", body);
        Assert.Contains("RedirectStandardError", body);
        Assert.Contains("CreateNoWindow", body);
        Assert.Contains("TimeoutSeconds", body);
        Assert.Contains("statusIntervalSeconds", body);
        Assert.Contains("Spicetify command: spicetify", body);
        Assert.Contains("Spicetify PID", body);
        Assert.Contains("Spicetify still running", body);
        Assert.Contains("hard timeout", body);
        Assert.Contains("Output:", body);
        Assert.Contains("Write-SpicetifyCliOutputLine", body);
        Assert.Contains("Update-SpicetifyCliProgress", body);
        Assert.Contains("LastPatchBucket", body);
        Assert.DoesNotContain("& $spicetifyExe @Arguments", body);
        Assert.DoesNotContain("Start-Process", body);
        Assert.DoesNotContain("$process.HasExited", body);
        Assert.DoesNotContain("System.Diagnostics.DataReceivedEventHandler", body);
        Assert.DoesNotContain("add_OutputDataReceived", body);
        Assert.DoesNotContain("Read-ProcessOutputDelta", body);
        Assert.DoesNotContain("ReadToEndAsync", body);
        Assert.DoesNotContain("Write-Log \"  $line\"", body);
        Assert.DoesNotMatch(@"(?m)^\s*return\s+\$output\b", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyOutputCollector_AvoidsPowerShellCallbacksOnNativeOutputThreads(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("public sealed class LibreSpotNativeOutputCollector", script);
        Assert.Contains("ConcurrentQueue<string>", script);
        Assert.Contains("DataReceivedEventHandler handler", script);
        Assert.DoesNotContain("[System.Diagnostics.DataReceivedEventHandler]{", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyOutputWriter_CompactsNativeProgressFrames(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Write-SpicetifyCliOutputLine\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Write-SpicetifyCliOutputLine function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("Remove-ConsoleEscapeSequences", body);
        Assert.Contains("LastPatchBucket", body);
        Assert.Contains("Patching files: $done/$total", body);
        Assert.Contains("LastStage", body);
        Assert.Contains("Extracting backup", body);
        Assert.Contains("Refreshing custom apps", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyProgressParser_RecognizesPatchCounts(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Update-SpicetifyCliProgress\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Update-SpicetifyCliProgress function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("Patching files", body);
        Assert.Contains("$done", body);
        Assert.Contains("$total", body);
        Assert.Contains("$percent", body);
        Assert.Contains("Extracting backup", body);
        Assert.Contains("Preprocessing", body);
        Assert.Contains("Fetching remote CSS map", body);
    }

    [Fact]
    public void WpfSpicetifyProgress_UsesGuardedDispatcherInvoke()
    {
        var script = ReadFile("LibreSpot.ps1");
        var fnBody = Regex.Match(
            script,
            @"function\s+Update-SpicetifyCliProgress\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, "Update-SpicetifyCliProgress function block not found.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("$sh.Dispatcher.Invoke", body);
        Assert.Contains("try {", body);
        Assert.Contains("if ($statusLabel)", body);
        Assert.DoesNotContain("BeginInvoke", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void ProcessOutputReader_FlushesCarriageReturnProgressLines(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Read-ProcessOutputDelta\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Read-ProcessOutputDelta function block not found in {relativePath}.");
        Assert.Matches(@"-split\s+[""`'](?:\\r\\n\|\\n\|\\r|`r`n\|`n\|`r)[""`']", fnBody.Groups["body"].Value);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyApplyRollback_ReportsApplyAndRollbackErrorsSeparately(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Module-ApplySpicetify\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Module-ApplySpicetify function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("Stop-SpotifyProcesses -MaxAttempts 3", body);
        Assert.Contains("Attempting rollback to keep Spotify usable", body);
        Assert.Contains("Apply error: $applyError", body);
        Assert.Contains("Rollback error: $restoreError", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void LaunchAfter_HandsSpotifyToExplorerInsteadOfElevatedProcess(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        Assert.Contains("Start-Process -FilePath 'explorer.exe'", script);
        Assert.DoesNotContain("Start-Process $global:SPOTIFY_EXE_PATH", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void ConfigQuarantine_UsesCollisionResistantNames(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Move-ConfigFileToQuarantine\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Move-ConfigFileToQuarantine function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("for ($attempt = 0; $attempt -lt 10; $attempt++)", body);
        Assert.Contains("[Guid]::NewGuid()", body);
        Assert.Contains("-ErrorAction Stop", body);
    }

    [Fact]
    public void PowerShellConfigSave_UsesUniqueTempAndBackupPaths()
    {
        var script = ReadFile("LibreSpot.ps1");
        var fnBody = Regex.Match(
            script,
            @"function\s+Save-LibreSpotConfig\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, "Save-LibreSpotConfig function block not found.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("config.{0}.tmp", body);
        Assert.Contains("config.{0}.bak", body);
        Assert.Contains("[Guid]::NewGuid()", body);
        Assert.DoesNotContain("$global:CONFIG_PATH.tmp", body);
        Assert.DoesNotContain("$global:CONFIG_PATH.bak", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void PowerShellNormalizer_StampsAndRejectsConfigSchemaVersions(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Normalize-LibreSpotConfig\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Normalize-LibreSpotConfig function block not found in {relativePath}.");
        Assert.Contains("$global:CONFIG_SCHEMA_VERSION = 1", script);
        Assert.Contains("function Get-LibreSpotConfigSchemaVersion", script);
        Assert.Contains("function Assert-LibreSpotConfigSchemaSupported", script);
        Assert.Contains("Saved config schema version", script);
        Assert.Contains("Assert-LibreSpotConfigSchemaSupported", fnBody.Groups["body"].Value);
        Assert.Contains("ConfigSchemaVersion = $global:CONFIG_SCHEMA_VERSION", fnBody.Groups["body"].Value);
    }

    [Fact]
    public void WorkerRunspace_ExportsConfigSchemaHelpers()
    {
        var script = ReadFile("LibreSpot.ps1");
        var listMatch = Regex.Match(
            script,
            @"\$functionNamesForWorker\s*=\s*@\((?<functions>.+?)\)\s*\r?\n\s*\$issMain",
            RegexOptions.Singleline);
        var variableListMatch = Regex.Match(
            script,
            @"\$varNamesForWorker\s*=\s*@\((?<variables>.+?)\)\s*\r?\nforeach",
            RegexOptions.Singleline);

        Assert.True(listMatch.Success, "Worker function export list not found.");
        Assert.True(variableListMatch.Success, "Worker variable export list not found.");
        Assert.Contains("Get-LibreSpotConfigSchemaVersion", listMatch.Groups["functions"].Value);
        Assert.Contains("Assert-LibreSpotConfigSchemaSupported", listMatch.Groups["functions"].Value);
        Assert.Contains("CONFIG_SCHEMA_VERSION", variableListMatch.Groups["variables"].Value);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void TempRootHelper_HandlesFileCollisionAtDefaultRoot(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Get-LibreSpotTempRoot\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Get-LibreSpotTempRoot function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("-PathType Leaf", body);
        Assert.Contains("GetCurrentProcess().Id", body);
        Assert.Contains("-PathType Container", body);
        Assert.Contains("-ErrorAction Stop", body);
    }

    [Fact]
    public void CheckUpdates_RemainsNonAdminInBackendAndDesktopShell()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        var viewModel = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("@('CheckUpdates', 'EnableAutoReapply', 'DisableAutoReapply')", backend);
        Assert.Contains("definition.Action, \"CheckUpdates\"", viewModel);
        Assert.Contains("StartBackendRunAsync(definition.Action, null, definition.Title, definition.Description, 2, requiresAdministrator)", viewModel);
    }

    // ---------------------------------------------------------------------
    // Safe archive extraction — path traversal, absolute path, and size
    // limit protection via Expand-ArchiveSafely.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void ExpandArchiveSafely_ExistsAndValidatesEntries(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Expand-ArchiveSafely\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Expand-ArchiveSafely function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        Assert.Contains("IsPathRooted", body);
        Assert.Contains(@"'..\'" , body);
        Assert.Contains("StartsWith", body);
        Assert.Contains("MaxEntries", body);
        Assert.Contains("MaxExpandedBytes", body);
        Assert.Contains("escapes destination", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void AllArchiveExtractionUsesExpandArchiveSafely(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        var rawCalls = Regex.Matches(script, @"\[System\.IO\.Compression\.ZipFile\]::ExtractToDirectory");
        Assert.Equal(1, rawCalls.Count);

        var fnBody = Regex.Match(
            script,
            @"function\s+Expand-ArchiveSafely\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success);
        Assert.Contains("ExtractToDirectory", fnBody.Groups["body"].Value);
    }

    [Fact]
    public void ExpandArchiveSafely_IsExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        Assert.Contains("'Expand-ArchiveSafely'", exportBlock.Groups["list"].Value);
    }

    // ---------------------------------------------------------------------
    // GitHub API rate-limit resilience — update checks use
    // Invoke-GitHubApiSafe instead of raw Invoke-RestMethod.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void InvokeGitHubApiSafe_ExistsAndHandlesRateLimits(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Invoke-GitHubApiSafe\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Invoke-GitHubApiSafe function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        Assert.Contains("x-ratelimit-remaining", body);
        Assert.Contains("x-ratelimit-reset", body);
        Assert.Contains("429", body);
        Assert.Contains("403", body);
        Assert.Contains("rate limit", body.ToLowerInvariant());
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void CheckForUpdates_UsesGitHubApiSafeNotRawRestMethod(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Check-ForUpdates\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Check-ForUpdates function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        Assert.Contains("Invoke-GitHubApiSafe", body);
        Assert.DoesNotContain("Invoke-RestMethod", body);
    }

    [Fact]
    public void InvokeGitHubApiSafe_IsExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        Assert.Contains("'Invoke-GitHubApiSafe'", exportBlock.Groups["list"].Value);
    }

    // ---------------------------------------------------------------------
    // SpotX post-patch effectiveness verification.
    // A clean SpotX exit code does NOT prove the patch landed: Spotify's
    // signature protection (>=1.2.70) can let SpotX exit 0 without patching
    // (SpotX issue #760). Get-SpotXPatchVerification asserts the on-disk
    // markers SpotX leaves (xpui.spa + xpui.spa.bak) and the install flow must
    // surface "verified" vs "ran but unverified" instead of always logging
    // success. These guards lock that distinction in both PowerShell paths.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpotXPatchVerification_FunctionExistsInBothPowerShellPaths(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        Assert.Contains("function Get-SpotXPatchVerification", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpotXPatchVerification_DiscriminatesVerifiedFromUnverified(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        var fnBody = Regex.Match(
            script,
            @"function\s+Get-SpotXPatchVerification\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success, $"Get-SpotXPatchVerification function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        // The discriminating signal is SpotX's pre-patch backup of the original bundle.
        Assert.Contains("xpui.spa.bak", body);
        Assert.Contains("xpui.spa", body);
        // All three verdict states must be reachable from the function body.
        Assert.Contains("'Verified'", body);
        Assert.Contains("'Unverified'", body);
        Assert.Contains("'Missing'", body);
        // Verdict requires BOTH the patched bundle and the backup to be present.
        Assert.Contains("$hasBackup -and $hasBundle", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void InstallSpotX_SurfacesVerificationInsteadOfUnconditionalSuccess(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        var fnBody = Regex.Match(
            script,
            @"function\s+Module-InstallSpotX\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success, $"Module-InstallSpotX function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        // The install flow must call the verifier and branch on its verdict,
        // not log success purely on exit code.
        Assert.Contains("Get-SpotXPatchVerification", body);
        Assert.Contains("$verify.Verified", body);
        Assert.Contains("#760", body);
        Assert.DoesNotContain("Spotify $patchedVer patched successfully.", body);
        Assert.DoesNotContain("SpotX patching completed successfully.", body);
    }

    [Fact]
    public void SpotXPatchVerification_IsExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        Assert.Contains("'Get-SpotXPatchVerification'", exportBlock.Groups["list"].Value);
    }

    // ---------------------------------------------------------------------
    // CVE-2025-54100: Windows PowerShell 5.1 web-content RCE (CVSS 7.8, fixed
    // in the December 2025 Windows cumulative updates). SHA256 pinning protects
    // payload integrity but not the parse-time vector on an unpatched host, so
    // the downloader must surface a non-blocking patch-level warning.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void DownloaderCveExposure_FunctionExistsAndDiscriminatesStates(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        Assert.Contains("function Get-DownloaderCveExposure", script);

        var fnBody = Regex.Match(
            script,
            @"function\s+Get-DownloaderCveExposure\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success, $"Get-DownloaderCveExposure function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        // PowerShell 7+ (Core) is out of scope for this CVE and must be skipped.
        Assert.Contains("Desktop", body);
        // All verdict states must be reachable.
        Assert.Contains("'NotAffected'", body);
        Assert.Contains("'Patched'", body);
        Assert.Contains("'PossiblyExposed'", body);
        Assert.Contains("'Unknown'", body);
        // The discriminator is the December 2025 patch wave.
        Assert.Contains("2025-12-09", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void DownloadFileSafe_RunsNonBlockingCveWarning(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Download-FileSafe\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success, $"Download-FileSafe function block not found in {relativePath}.");
        // Every download path must emit the patch-level heads-up.
        Assert.Contains("Write-DownloaderCveWarningIfNeeded", fnBody.Groups["body"].Value);
    }

    [Fact]
    public void DownloaderCveFunctions_AreExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        Assert.Contains("'Get-DownloaderCveExposure'", exportBlock.Groups["list"].Value);
        Assert.Contains("'Write-DownloaderCveWarningIfNeeded'", exportBlock.Groups["list"].Value);
    }

    [Fact]
    public void SecurityPolicy_DocumentsDownloaderCve()
    {
        var security = ReadFile("SECURITY.md");
        Assert.Contains("CVE-2025-54100", security);
        Assert.Contains("December 2025", security);
        // The two named mitigations are hash pinning and patch level.
        Assert.Contains("SHA256", security);
    }

    // ---------------------------------------------------------------------
    // SpotX external-process execution contract.
    // The PowerShell backend assembles the SpotX argument string by string
    // interpolation and runs it via a single-string Start-Process (a Windows
    // PowerShell 5.1 redirected-output quirk). That is only safe because EVERY
    // interpolated value is either a fixed flag or a normalized enum/integer.
    // These guards lock that invariant: Normalize-LibreSpotConfig must constrain
    // each user-controlled field, and Build-SpotXParams must not interpolate
    // anything outside the known-safe set. A new free-form argument fails the
    // guard until it is reviewed here (and normalized or tokenized), instead of
    // silently enabling command injection from a crafted config.json.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void Normalize_ConstrainsSpotXInterpolatedFieldsToAllowlistsOrIntegers(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        // Download method: reset to the {'', curl, webclient} allowlist.
        Assert.Matches(@"SpotX_DownloadMethod[\s\S]{0,200}-notin\s*@\(\s*''\s*,\s*'curl'\s*,\s*'webclient'\s*\)", script);
        // Lyrics theme: allowlist membership against the known theme set.
        Assert.Matches(@"\$lyricsTheme\s*-notin\s*\$global:SpotXLyricsThemes", script);
        // Spotify version id: allowlist membership against the manifest ids.
        Assert.Matches(@"-notin\s*\$global:SpotifyVersionIds", script);
        // Cache limit: integer coercion with an upper bound.
        Assert.Matches(@"SpotX_CacheLimit\s*=\s*ConvertTo-ConfigInt[\s\S]{0,160}-Maximum\s*50000", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void BuildSpotXParams_OnlyInterpolatesKnownSafeNormalizedFields(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fn = Regex.Match(
            script,
            @"function\s+Build-SpotXParams\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fn.Success, $"Build-SpotXParams not found in {relativePath}.");
        var body = fn.Groups["body"].Value;

        // Every $( ... ) subexpression interpolated into an argument string must
        // reference only a normalized/allowlisted field. A new interpolation
        // fails this test until it is reviewed and added here.
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "$Config.SpotX_LyricsTheme",
            "$Config.SpotX_DownloadMethod",
            "$Config.SpotX_CacheLimit",
            "$entry.Version",
        };
        foreach (Match m in Regex.Matches(body, @"\$\((?<expr>[^)]*)\)"))
        {
            var expr = m.Groups["expr"].Value.Trim();
            Assert.True(
                allowed.Contains(expr),
                $"Build-SpotXParams in {relativePath} interpolates '{expr}', which is not in the known-safe set. " +
                "Normalize it in Normalize-LibreSpotConfig (allowlist/integer) and add it here, or use tokenized execution.");
        }
    }

    // ---------------------------------------------------------------------
    // PowerShell execution-policy / language-mode / application-control
    // diagnostics. LibreSpot runs with -ExecutionPolicy Bypass, which is a
    // safety feature, NOT a security boundary, and does not defeat AppLocker /
    // WDAC (they enforce ConstrainedLanguage). The shell records the security
    // context at run start and classifies CLM/WDAC blocks separately from
    // ordinary errors, without telling users to weaken enterprise controls.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void PowerShellSecurityContext_RecordsEditionVersionPolicyAndLanguageMode(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        Assert.Contains("function Get-PowerShellSecurityContext", script);

        var fn = Regex.Match(
            script,
            @"function\s+Get-PowerShellSecurityContext\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fn.Success, $"Get-PowerShellSecurityContext not found in {relativePath}.");
        var body = fn.Groups["body"].Value;

        Assert.Contains("PSVersionTable.PSEdition", body);
        Assert.Contains("PSVersionTable.PSVersion", body);
        Assert.Contains("LanguageMode", body);
        Assert.Contains("Get-ExecutionPolicy -List", body);
        // ConstrainedLanguage implies enforced application control.
        Assert.Contains("ConstrainedLanguage", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void ExternalScriptRunner_LogsSecurityContextAndClassifiesAppControlErrors(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        Assert.Contains("function Test-IsLanguageModeOrAppControlError", script);

        var fn = Regex.Match(
            script,
            @"function\s+Invoke-ExternalScriptIsolated\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fn.Success, $"Invoke-ExternalScriptIsolated not found in {relativePath}.");
        var body = fn.Groups["body"].Value;

        // Proactive: logs the host security context before spawning.
        Assert.Contains("Write-PowerShellSecurityContext", body);
        // Reactive: classifies app-control failures from spawned-process output.
        Assert.Contains("Test-IsLanguageModeOrAppControlError", body);
    }

    [Fact]
    public void DiagnosticsCopy_NeverClaimsExecutionPolicyIsASecurityBoundary()
    {
        // The copy must explicitly frame execution policy as NOT a security
        // boundary, and must never assert that bypassing it defeats app control.
        var security = ReadFile("SECURITY.md");
        Assert.Contains("not a security boundary", security);
        Assert.Contains("ConstrainedLanguage", security);
        Assert.Contains("does **not** defeat AppLocker or Windows Defender Application Control", security);

        // No script should tell users that -ExecutionPolicy Bypass bypasses app
        // control; our copy says the opposite ("does not bypass").
        foreach (var path in new[] { "LibreSpot.ps1", "src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1" })
        {
            var script = ReadFile(path.Split('/'));
            Assert.DoesNotMatch(@"Bypass[^\n]{0,40}(bypasses|defeats|disables)[^\n]{0,40}(AppLocker|application control|WDAC)", script);
        }
    }

    [Fact]
    public void PowerShellSecurityContextFunctions_AreExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        Assert.Contains("'Get-PowerShellSecurityContext'", exportBlock.Groups["list"].Value);
        Assert.Contains("'Write-PowerShellSecurityContext'", exportBlock.Groups["list"].Value);
        Assert.Contains("'Test-IsLanguageModeOrAppControlError'", exportBlock.Groups["list"].Value);
    }

    [Fact]
    public void CommunityThemes_UseCommitPinnedArchiveUrls_NotBranchPinned()
    {
        foreach (var path in new[] { "LibreSpot.ps1", "src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1" })
        {
            var script = ReadFile(path.Split('/'));
            Assert.DoesNotContain("archive/refs/heads/", script);

            var themeBlock = Regex.Match(script, @"\$global:CommunityThemeRepos\s*=\s*@\{(?<body>.+?)\n\}", RegexOptions.Singleline);
            Assert.True(themeBlock.Success, $"CommunityThemeRepos not found in {path}");
            Assert.DoesNotContain("Branch", themeBlock.Groups["body"].Value);
            Assert.Contains("CommitSha", themeBlock.Groups["body"].Value);
            Assert.Contains("SHA256", themeBlock.Groups["body"].Value);
        }
    }

    [Fact]
    public void CommunityThemes_CommitShasMatchBetweenScripts()
    {
        var mainScript = ReadFile("LibreSpot.ps1");
        var backendScript = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");

        var themes = new[] { "Catppuccin", "Comfy", "Bloom", "Lucid", "Hazy" };
        foreach (var theme in themes)
        {
            var mainCommit = Regex.Match(mainScript,
                $@"""{theme}""\s*=\s*@\{{[^}}]*CommitSha\s*=\s*""([a-f0-9]{{40}})""", RegexOptions.Singleline);
            var backendCommit = Regex.Match(backendScript,
                $@"'{theme}'\s*=\s*@\{{[^}}]*CommitSha\s*=\s*'([a-f0-9]{{40}})'", RegexOptions.Singleline);

            Assert.True(mainCommit.Success, $"CommitSha not found for {theme} in LibreSpot.ps1");
            Assert.True(backendCommit.Success, $"CommitSha not found for {theme} in Backend script");
            Assert.Equal(mainCommit.Groups[1].Value, backendCommit.Groups[1].Value);

            var mainHash = Regex.Match(mainScript,
                $@"""{theme}""\s*=\s*@\{{[^}}]*SHA256\s*=\s*""([a-f0-9]{{64}})""", RegexOptions.Singleline);
            var backendHash = Regex.Match(backendScript,
                $@"'{theme}'\s*=\s*@\{{[^}}]*SHA256\s*=\s*'([a-f0-9]{{64}})'", RegexOptions.Singleline);

            Assert.True(mainHash.Success, $"SHA256 not found for {theme} in LibreSpot.ps1");
            Assert.True(backendHash.Success, $"SHA256 not found for {theme} in Backend script");
            Assert.Equal(mainHash.Groups[1].Value, backendHash.Groups[1].Value);
        }
    }

    // ---------------------------------------------------------------------
    // Risk acknowledgment — first-run ToS dialog gate.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void RiskAcknowledged_IsPartOfBooleanNormalization(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var booleanBlock = Regex.Match(
            script,
            @"\$booleanKeys\s*=\s*@\((?<body>.+?)\)",
            RegexOptions.Singleline);
        Assert.True(booleanBlock.Success, $"Could not locate $booleanKeys block in {relativePath}.");
        Assert.Contains("RiskAcknowledged", booleanBlock.Groups["body"].Value);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void RiskAcknowledged_DefaultsToFalseInNormalization(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Normalize-LibreSpotConfig\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success, $"Normalize-LibreSpotConfig not found in {relativePath}.");
        Assert.Contains("RiskAcknowledged", fnBody.Groups["body"].Value);
        // The default must be false (not $true or true).
        Assert.Matches(@"\$normalized\['RiskAcknowledged'\]\s*=\s*\$false", fnBody.Groups["body"].Value);
    }

    [Fact]
    public void PowerShellMonolith_GatesInstallBehindRiskAcknowledgment()
    {
        var script = ReadFile("LibreSpot.ps1");

        // The install button must call Assert-RiskAcknowledged before proceeding.
        Assert.Contains("function Assert-RiskAcknowledged", script);
        Assert.Contains("Assert-RiskAcknowledged", script);

        // The function must load config, check RiskAcknowledged, show a dialog,
        // save on acceptance, and return false on cancellation.
        var fn = Regex.Match(
            script,
            @"function\s+Assert-RiskAcknowledged\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fn.Success, "Assert-RiskAcknowledged function block not found.");
        var body = fn.Groups["body"].Value;
        Assert.Contains("RiskAcknowledged", body);
        Assert.Contains("Show-ThemedDialog", body);
        Assert.Contains("Save-LibreSpotConfig", body);
        Assert.Contains("return $false", body);
        Assert.Contains("return $true", body);
    }

    [Fact]
    public void DesktopBackend_GatesPatchingActionsBehindRiskAcknowledgment()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");

        // The backend entry point must check RiskAcknowledged before Install
        // and maintenance actions, and exit with an error if not acknowledged.
        Assert.Contains("RiskAcknowledged", backend);
        Assert.Contains("Risk acknowledgment required", backend);
    }
}
