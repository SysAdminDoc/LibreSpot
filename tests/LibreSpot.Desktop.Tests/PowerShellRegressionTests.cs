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
    public void CompareLibreSpotVersions_IsExportedIntoWorkerRunspace()
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

        foreach (var flag in new[] { "CliWatch", "CliInstallWatcher", "CliUninstallWatcher" })
        {
            var branch = Regex.Match(
                script,
                @"if\s*\(\s*\$script:" + flag + @"\s*\)\s*\{(?<body>.+?)^\}",
                RegexOptions.Singleline | RegexOptions.Multiline);
            Assert.True(branch.Success, $"Could not locate if-branch for $script:{flag}.");
            Assert.Contains("exit", branch.Groups["body"].Value);
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
}
