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

    private static string RemoveFunctionBody(string script, string functionName) =>
        Regex.Replace(
            script,
            $@"function\s+{Regex.Escape(functionName)}\s*\{{(?<body>.+?)^\}}",
            string.Empty,
            RegexOptions.Singleline | RegexOptions.Multiline);

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

    [Fact]
    public void ForeignPatchSignature_UsesPowerShell51SafeParentDirectoryResolution()
    {
        var script = ReadFile("LibreSpot.ps1");
        var match = Regex.Match(
            script,
            @"function\s+Get-ExistingSpotifyPatchSignature\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(match.Success, "Get-ExistingSpotifyPatchSignature function block not found in LibreSpot.ps1.");
        var body = match.Groups["body"].Value;

        Assert.Contains("[System.IO.Path]::GetDirectoryName($global:SPOTIFY_EXE_PATH)", body);
        Assert.DoesNotContain("Split-Path -LiteralPath", body);
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
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SetWatcherState_WritesThroughAtomicTempFile(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("function Set-WatcherState", script);
        Assert.Contains("watcher-state.{0}.tmp", script);
        Assert.Contains("[System.IO.File]::Replace($tempPath, $global:WATCHER_STATE_PATH", script);
        Assert.Contains("[System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)", script);
        Assert.DoesNotContain("[System.IO.File]::WriteAllText($global:WATCHER_STATE_PATH", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/powershell/lane-specific/Set-WatcherState.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SetWatcherState_MergesOverExistingState_SoOtherLaneFieldsSurvive(string relativePath)
    {
        // Both the monolith watcher and the WPF backend write the same
        // watcher-state.json. A save that serializes only its own fields
        // destroys the other lane's extended fields (LastAppliedSpotifyVersion,
        // LastSuccessfulApplyAt, ...). Every lane must serialize a merged view.
        var script = ReadFile(relativePath.Split('/'));

        var fnBody = Regex.Match(
            script,
            @"function\s+Set-WatcherState\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Set-WatcherState function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("$json = $merged | ConvertTo-Json", body);
        Assert.DoesNotContain("$json = $State | ConvertTo-Json", body);
    }

    [Fact]
    public void FullReset_UnregistersAutoReapplyWatcher_InMonolith()
    {
        // A full reset removes Spotify entirely; leaving the reapply watcher
        // scheduled task behind means it fires at logon forever against a
        // machine with no Spotify.
        var script = ReadFile("LibreSpot.ps1");

        var start = script.IndexOf("--- Full Reset ---", StringComparison.Ordinal);
        var end = script.IndexOf("--- Full Reset Complete ---", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Full Reset block not found in LibreSpot.ps1.");

        var block = script.Substring(start, end - start);
        Assert.Contains("Unregister-AutoReapplyTask", block);
    }

    [Fact]
    public void FullReset_UnregistersAutoReapplyWatcher_InBackend()
    {
        var script = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");

        var start = script.IndexOf("'FullReset' {", StringComparison.Ordinal);
        var end = script.IndexOf("'RemoveSelfData' {", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "FullReset action block not found in backend script.");

        var block = script.Substring(start, end - start);
        Assert.Contains("Unregister-AutoReapplyTask", block);
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
        Assert.Contains("Get-SpicetifyIntegrationContext", listMatch.Value);
        Assert.Contains("Get-FileSha256Lower", listMatch.Value);

        // Module-ApplySpicetify records marketplace visibility evidence on its
        // success path; Write-OperationJournalEntry calls the retention helper
        // inside its try block. Either one missing from the allow-list makes a
        // successful apply look like a failure (and roll itself back) or makes
        // the operation journal silently dead inside workers.
        Assert.Contains("Write-MarketplaceVisibilityEvidence", listMatch.Value);
        Assert.Contains("Optimize-OperationJournalRetention", listMatch.Value);
    }

    [Fact]
    public void RequiredSpicetifyGlobals_AreExportedIntoWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var listMatch = Regex.Match(
            script,
            @"\$varNamesForWorker\s*=\s*@\((?<body>.+?)^\)",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(listMatch.Success, "Worker variable export list not found.");
        Assert.Contains("SPICETIFY_DIR", listMatch.Value);
        Assert.Contains("SPICETIFY_CONFIG_DIR", listMatch.Value);
        Assert.Contains("SPICETIFY_INTEGRATION_VERSION", listMatch.Value);

        // Build-SpotXParams and Normalize-LibreSpotConfig read the Spotify
        // version manifest inside workers; without these exports a pinned
        // SpotX_SpotifyVersionId silently degrades to 'auto'. The journal
        // retention limits keep Optimize-OperationJournalRetention live.
        Assert.Contains("SpotifyVersionManifest", listMatch.Value);
        Assert.Contains("SpotifyVersionIds", listMatch.Value);
        Assert.Contains("OPERATION_JOURNAL_MAX_BYTES", listMatch.Value);
        Assert.Contains("OPERATION_JOURNAL_RETAIN_BYTES", listMatch.Value);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyIntegrationContext_DefinesVersionedPathContract(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var body = Regex.Match(
            script,
            @"function\s+Get-SpicetifyIntegrationContext\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(body.Success, "Get-SpicetifyIntegrationContext function block not found.");
        Assert.Contains("SPICETIFY_INTEGRATION_VERSION", body.Value);
        Assert.Contains("'v2'", body.Value);
        Assert.Contains("'v3-preview'", body.Value);
        Assert.Contains("InstallDirectory", body.Value);
        Assert.Contains("ConfigDirectory", body.Value);
        Assert.Contains("CliPath", body.Value);
        Assert.Contains("ConfigPath", body.Value);
        Assert.Contains("ThemesDirectory", body.Value);
        Assert.Contains("ExtensionsDirectory", body.Value);
        Assert.Contains("CustomAppsDirectory", body.Value);
        Assert.Contains("MarketplaceDirectory", body.Value);
        Assert.Contains("LegacyMarketplaceDirectory", body.Value);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyCallSites_DoNotRebuildIntegrationPaths(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        script = RemoveFunctionBody(script, "Get-SpicetifyIntegrationContext");
        script = Regex.Replace(script, @"^\$global:SPICETIFY_(DIR|CONFIG_DIR|INTEGRATION_VERSION)\s*=.*$", string.Empty, RegexOptions.Multiline);

        Assert.DoesNotContain("Join-Path $global:SPICETIFY_DIR", script);
        Assert.DoesNotContain("Join-Path $global:SPICETIFY_CONFIG_DIR", script);
        Assert.DoesNotContain("Remove-PathSafely -Path $global:SPICETIFY", script);
        Assert.DoesNotContain("Remove-PathEntry -Entry $global:SPICETIFY", script);
    }

    [Theory]
    [InlineData("src/powershell/shared/Download-CommunityExtensions.ps1")]
    [InlineData("src/powershell/shared/Get-MarketplaceHealth.ps1")]
    [InlineData("src/powershell/shared/Get-SpicetifyConfigEntries.ps1")]
    [InlineData("src/powershell/shared/Get-SpicetifyDiagnosticSnapshot.ps1")]
    [InlineData("src/powershell/shared/Invoke-SpicetifyCli.ps1")]
    [InlineData("src/powershell/shared/Module-InstallMarketplace.ps1")]
    [InlineData("src/powershell/shared/Module-InstallSpicetifyCLI.ps1")]
    [InlineData("src/powershell/shared/Module-InstallThemes.ps1")]
    [InlineData("src/powershell/shared/Test-SpicetifyCliInstalled.ps1")]
    public void SharedSpicetifyHelpers_UseIntegrationContext(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("Get-SpicetifyIntegrationContext", script);
        Assert.DoesNotContain("Join-Path $global:SPICETIFY_DIR", script);
        Assert.DoesNotContain("Join-Path $global:SPICETIFY_CONFIG_DIR", script);
        Assert.DoesNotContain("Remove-PathSafely -Path $global:SPICETIFY", script);
        Assert.DoesNotContain("Remove-PathEntry -Entry $global:SPICETIFY", script);
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
        Assert.Contains("$script:CliInstallWatcher", body);
        Assert.Contains("'-installwatcher'", body);
        Assert.Contains("$script:CliUninstallWatcher", body);
        Assert.Contains("'-uninstallwatcher'", body);
        Assert.Contains("ArgumentList", body);

        // -watch must NOT be forwarded through elevation: the watcher task
        // runs LeastPrivilege and bypasses the gate entirely (a RunAs forward
        // would pop UAC on every unattended tick).
        Assert.DoesNotContain("'-watch'", body);
    }

    [Fact]
    public void SelfElevationInlineFallback_VerifiesPayloadHashFromEncodedCommand()
    {
        var script = ReadFile("LibreSpot.ps1");

        Assert.Contains("Kind = 'InlinePayload'", script);
        Assert.Contains("[System.Security.Cryptography.SHA256]::Create()", script);
        Assert.Contains("LibreSpot elevation payload hash mismatch", script);
        Assert.Contains("'-EncodedCommand', $encodedBootstrap", script);
        Assert.Contains("& ([scriptblock]::Create(`$payload)) @forwardedArgs", script);
        Assert.DoesNotContain("LibreSpot-elevated.ps1", script);
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

    [Fact]
    public void UpstreamStalenessRefresh_DoesNotRunCmdletsInsideThreadPoolDelegate()
    {
        var script = ReadFile("LibreSpot.ps1");

        Assert.Contains("Start-UpstreamStalenessNoticeRefresh", script);
        Assert.Contains("Invoke-UpstreamStalenessHttp", script);
        Assert.Contains("Read-UpstreamStalenessCache", script);
        Assert.Contains("Save-UpstreamStalenessCache", script);

        var fnBody = Regex.Match(
            script,
            @"function\s+Start-UpstreamStalenessNoticeRefresh\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success, "Start-UpstreamStalenessNoticeRefresh function block not found.");
        var body = fnBody.Groups["body"].Value;

        Assert.Contains("QueueUserWorkItem", body);
        Assert.Contains("Invoke-UpstreamStalenessHttp", body);
        Assert.DoesNotContain("Get-UpstreamStalenessNotice", body);
        Assert.Contains("Dispatcher.BeginInvoke", body);

        var httpBody = Regex.Match(
            script,
            @"function\s+Invoke-UpstreamStalenessHttp\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(httpBody.Success, "Invoke-UpstreamStalenessHttp function block not found.");
        Assert.DoesNotContain("Invoke-WebRequest", httpBody.Groups["body"].Value);
        Assert.DoesNotContain("Invoke-GitHubApiSafe", httpBody.Groups["body"].Value);
        Assert.DoesNotContain("ConvertFrom-Json", httpBody.Groups["body"].Value);
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

        // The reapply pipeline also needs the asset-cache and Spicetify CLI
        // helpers, which are defined in much later sections. The exit block
        // must come after ALL of them or a real reapply tick dies with
        // CommandNotFound while no-op ticks keep passing.
        foreach (var dependency in new[]
                 {
                     "function Get-FromAssetCache",
                     "function Save-ToAssetCache",
                     "function Invoke-SpicetifyCli",
                     "function Test-SpicetifyCliInstalled"
                 })
        {
            var dependencyIndex = script.IndexOf(dependency, StringComparison.Ordinal);
            Assert.True(dependencyIndex > 0, $"{dependency} definition not found.");
            Assert.True(
                cliWatchIndex > dependencyIndex,
                $"-Watch exit branch must be placed AFTER {dependency}.");
        }

        // And the watcher must never pass through the self-elevation gate —
        // an unattended LeastPrivilege task forwarding -watch via RunAs pops
        // a UAC prompt every 30 minutes.
        Assert.Contains("if (-not $script:CliWatch -and -not ([Security.Principal.WindowsPrincipal]", script);
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
    public void SpotifySessionStability_UsesInitialPidToDetectRestarts()
    {
        var script = ReadFile("LibreSpot.ps1");
        var fnBody = Regex.Match(
            script,
            @"function\s+Test-SpotifySessionStability\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, "Test-SpotifySessionStability function block not found.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("$initialPid", body);
        Assert.Contains("$afterPids", body);
        Assert.Contains("-notcontains $initialPid", body);
        Assert.Contains("Spotify restarted within", body);
    }

    [Fact]
    public void BackendInstall_VerifiesSpotifySessionBeforeReportingComplete()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        var fnBody = Regex.Match(
            backend,
            @"function\s+Invoke-LibreSpotInstall\s*\{(?<body>.+?)^function\s+Invoke-LibreSpotMaintenance",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, "Invoke-LibreSpotInstall function block not found.");
        var body = fnBody.Groups["body"].Value;
        var launchIndex = body.IndexOf("Start-Process -FilePath 'explorer.exe'", StringComparison.Ordinal);
        var stabilityIndex = body.IndexOf("Test-SpotifySessionStability -WaitSeconds 20", StringComparison.Ordinal);
        var completeIndex = body.IndexOf("Update-BackendState -Progress 100 -Status 'Setup complete'", StringComparison.Ordinal);

        Assert.True(launchIndex >= 0, "LaunchAfter should still hand Spotify to explorer.exe.");
        Assert.True(stabilityIndex > launchIndex, "The WPF backend must validate Spotify after launching it.");
        Assert.True(completeIndex > stabilityIndex, "Setup complete must only be reported after the stability check.");
        Assert.Contains("Checking patched session stability", body);
        Assert.Contains("Spotify did not stay open after patching", body);
        Assert.Contains("Restore-SpotifyIfSpicetifyPresent", body);
        Assert.Contains("Undoing active Spicetify customizations after an unstable launch", body);
    }

    [Fact]
    public void BackendSpotifySessionStability_UsesInitialPidToDetectRestarts()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        var fnBody = Regex.Match(
            backend,
            @"function\s+Test-SpotifySessionStability\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, "Backend Test-SpotifySessionStability function block not found.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("$initialPid", body);
        Assert.Contains("$afterPids", body);
        Assert.Contains("-notcontains $initialPid", body);
        Assert.Contains("Spotify restarted within", body);
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
        Assert.Contains("<Arguments>$escapedArguments</Arguments>", body);
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
        Assert.Contains("Get-FileSha256Lower -Path $spotxRun", body);
        Assert.Contains("Download-FileSafe -Uri $global:URL_SPOTX -OutFile $spotxRun", body);
        Assert.DoesNotContain("Invoke-WebRequest -Uri $global:URL_SPOTX", body);
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
    public void DesktopBackend_WatcherStateTracksApplyAttemptsAndPreservesFields()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");

        Assert.Contains("LastAppliedSpotifyVersion", backend);
        Assert.Contains("LastAttemptedSpotifyVersion", backend);
        Assert.Contains("LastSuccessfulApplyAt", backend);
        Assert.Contains("LastApplyOutcome", backend);
        Assert.Contains("function Update-ApplyState", backend);
        Assert.Contains("$merged = Get-WatcherState", backend);
        Assert.Contains("SpicetifyApplyRolledBack", backend);
        Assert.Contains("WatcherReapplied", backend);
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
    public void PowerShellNormalizer_PreservesBoundedCustomPatchJson(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Normalize-LibreSpotConfig\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Normalize-LibreSpotConfig function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("SpotX_CustomPatchesEnabled", body);
        Assert.Contains("SpotX_CustomPatchesJson", body);
        Assert.Contains("GetByteCount($patchJson) -le 65536", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpotXCustomPatches_AreStagedThroughValidatedTempFile(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var helper = Regex.Match(
            script,
            @"function\s+New-SpotXCustomPatchesFile\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        var installer = Regex.Match(
            script,
            @"function\s+Module-InstallSpotX\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(helper.Success, $"New-SpotXCustomPatchesFile function block not found in {relativePath}.");
        Assert.True(installer.Success, $"Module-InstallSpotX function block not found in {relativePath}.");
        Assert.Contains("ConvertFrom-Json -ErrorAction Stop", helper.Groups["body"].Value);
        Assert.Contains("New-LibreSpotTempFile -Name 'spotx-custom-patches.json'", helper.Groups["body"].Value);
        Assert.Contains("[System.IO.File]::WriteAllText($patchPath, $patchJson, $utf8)", helper.Groups["body"].Value);
        Assert.Contains("New-SpotXCustomPatchesFile -Config $Config", installer.Groups["body"].Value);
        Assert.Contains("-CustomPatchesPath", installer.Groups["body"].Value);
        Assert.Contains("Remove-Item -LiteralPath $customPatchesPath", installer.Groups["body"].Value);
    }

    [Fact]
    public void SpotXCustomPatches_HelperIsExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);

        Assert.True(exportBlock.Success, "Could not locate functionNamesForWorker block.");
        Assert.Contains("'New-SpotXCustomPatchesFile'", exportBlock.Groups["list"].Value);
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
    [InlineData("src/powershell/shared/Invoke-ExternalScriptIsolated.ps1")]
    public void ExternalScriptRunner_HoldsVerifiedReadLockBeforeSpawn(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Invoke-ExternalScriptIsolated\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Invoke-ExternalScriptIsolated function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("ExpectedHash", body);
        Assert.Contains("Open-VerifiedScriptForExecution", body);
        Assert.Contains("$scriptGuard.Dispose()", body);
        Assert.True(
            body.IndexOf("Open-VerifiedScriptForExecution", StringComparison.Ordinal) <
            body.IndexOf("Start-Process", StringComparison.Ordinal),
            "The script file must be verified and locked before Start-Process receives its path.");
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    [InlineData("src/powershell/shared/Open-VerifiedScriptForExecution.ps1")]
    public void VerifiedScriptExecutionHelper_UsesReadOnlySharingAndSha256(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Open-VerifiedScriptForExecution\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Open-VerifiedScriptForExecution function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;
        Assert.Contains("[System.IO.FileShare]::Read", body);
        Assert.Contains("[System.Security.Cryptography.SHA256]::Create()", body);
        Assert.Contains("hash mismatch immediately before execution", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    [InlineData("src/powershell/shared/Module-InstallSpotX.ps1")]
    public void SpotXLaunches_PassPinnedHashIntoExecutionGuard(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("-ExpectedHash $spotxHash", script);
        Assert.Contains("-Label 'SpotX run.ps1'", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpicetifyCliPin_UsesCurrentTestedRelease(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("Version = '2.44.0'", script);
        Assert.Contains("WindowsMinSpotify = '1.2.14'", script);
        Assert.Contains("WindowsMaxTestedSpotify = '1.2.93'", script);
        Assert.Contains("CompatibilityUrl = 'https://github.com/spicetify/cli/releases/tag/v2.44.0'", script);
        Assert.Contains("215435095420e3804001a650c072f51befde897b414b0dac054edc2ea258ebea", script);
        Assert.Contains("a6f827ae6387203bb87ff4af1f5ab21e4671a542ce1a0e3cb82ddc77d2ac7444", script);
        Assert.DoesNotContain("Version = '2.43.2'", script);
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

        Assert.Contains("550bc72cd15f6e2a172a6ecc0873d0991eb1c83c", script);
        Assert.Contains("863cd19429160c911ce7439426d9e2127064028ccabbaf3007b233a393607606", script);
        Assert.Contains("1.2.93", script);
        Assert.DoesNotContain("3284673df69e276c5c0ee90bb1cc9185cecb9ad4", script);
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
        Assert.Contains("Confirm-FileHash -Path $tempFile -ExpectedHash $extHash", script);
        Assert.Contains("Move-Item -LiteralPath $tempFile -Destination $destFile -Force", script);
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
        Assert.Contains("Get-SpicetifyIntegrationContext", body);
        Assert.Contains("$integration.CustomAppsDirectory", body);
        Assert.Contains("$integration.MarketplaceDirectory", body);
        Assert.Contains("$integration.LegacyMarketplaceDirectory", body);
        Assert.DoesNotContain("Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps'", body);
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
        Assert.Contains("Spicetify ($($integration.Version)) command: spicetify", body);
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

    [Fact]
    public void PowerShellCustomMode_UsesSharedLocalProfileStore()
    {
        var script = ReadFile("LibreSpot.ps1");

        Assert.Contains("$global:PROFILE_DIR", script);
        Assert.Contains("$global:ACTIVE_PROFILE_PATH", script);
        Assert.Contains("$global:PREVIOUS_PROFILE_PATH", script);
        Assert.Contains("function Get-LibreSpotBuiltInProfiles", script);
        Assert.Contains("function Initialize-LibreSpotProfileStore", script);
        Assert.Contains("function Apply-LibreSpotProfile", script);
        Assert.Contains("CmbLocalProfiles", script);
        Assert.Contains("BtnProfilePreview", script);
        Assert.Contains("BtnProfileApply", script);
        Assert.Contains("BtnProfileSaveCurrent", script);
        Assert.Contains("ProfileStatusText", script);
        Assert.Contains("SecondaryActionButton", script);
        Assert.Contains("Get-LocalProfileStatusText", script);
        Assert.Contains("if ($_.IsActive) { 0 } else { 1 }", script);
        Assert.Contains("Previewing $($selectedProfile.Name) in Custom. config.json was not changed.", script);
        Assert.Contains("Saved $($savedProfile.Name) as a local profile. Preview or set it active when ready.", script);
        Assert.Contains("No profiles are available yet. Save the current Custom selections to create one.", script);
        Assert.Contains("Set active profile", script);
        Assert.Contains("previous active profile pointer", script);
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
    public void NoBackendActionRequiresAdminElevation()
    {
        var backend = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        var viewModel = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");

        Assert.DoesNotContain("Ensure-Admin", backend.Split("function Ensure-Admin")[0]);
        Assert.Contains("RequiresAdministrator(string action) => false", viewModel);
    }

    // ---------------------------------------------------------------------
    // Safe archive extraction — path traversal, absolute path, and size
    // limit protection via Expand-ArchiveSafely.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    [InlineData("src/powershell/shared/Expand-ArchiveSafely.ps1")]
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
        // Traversal guard rejects genuine ".." path segments; the resolved
        // destination-prefix StartsWith check below is the authoritative escape guard.
        Assert.Contains("path traversal entry", body);
        Assert.Contains("StartsWith", body);
        Assert.Contains("MaxEntries", body);
        Assert.Contains("MaxExpandedBytes", body);
        Assert.Contains("escapes destination", body);
        Assert.Contains("totalActualBytes", body);
        Assert.Contains("actual expanded size exceeds limit", body);
        Assert.Contains("entryStream.Read", body);
        Assert.Contains("[System.IO.File]::Move", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    [InlineData("src/powershell/shared/Expand-ArchiveSafely.ps1")]
    public void AllArchiveExtractionUsesExpandArchiveSafely(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        var rawDirectoryCalls = Regex.Matches(script, @"\[System\.IO\.Compression\.ZipFile\]::ExtractToDirectory");
        Assert.Empty(rawDirectoryCalls);

        var fnBody = Regex.Match(
            script,
            @"function\s+Expand-ArchiveSafely\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success);
        Assert.DoesNotContain("ExtractToFile", fnBody.Groups["body"].Value);
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
        Assert.Contains("Get-DownloadFailureHint", body);
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
    // Download failure diagnostics — WebRequest/BITS fallback should explain
    // common DNS, TLS, proxy, GitHub block/rate-limit, and timeout causes.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void DownloadFileSafe_UsesNetworkFailureClassifier(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var classifier = Regex.Match(
            script,
            @"function\s+Get-DownloadFailureHint\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        var downloader = Regex.Match(
            script,
            @"function\s+Download-FileSafe\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(classifier.Success, $"Get-DownloadFailureHint function block not found in {relativePath}.");
        Assert.True(downloader.Success, $"Download-FileSafe function block not found in {relativePath}.");

        var classifierBody = classifier.Groups["body"].Value;
        Assert.Contains("proxy authentication", classifierBody);
        Assert.Contains("GitHub rate limit or access block", classifierBody);
        Assert.Contains("DNS could not resolve", classifierBody);
        Assert.Contains("TLS or certificate validation failed", classifierBody);
        Assert.Contains("timed out", classifierBody);

        var downloaderBody = downloader.Groups["body"].Value;
        Assert.Contains("Get-DownloadFailureHint", downloaderBody);
        Assert.Contains("Trying BITS fallback", downloaderBody);
        Assert.Contains("Download failed after WebRequest and BITS fallback", downloaderBody);
    }

    [Fact]
    public void GetDownloadFailureHint_IsExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        Assert.Contains("'Get-DownloadFailureHint'", exportBlock.Groups["list"].Value);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void NetworkPreflightStatus_ClassifiesCommonFailureModes(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var diagnostic = Regex.Match(
            script,
            @"function\s+Get-NetworkDiagnosticCode\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        var preflight = Regex.Match(
            script,
            @"function\s+Get-NetworkPreflightStatus\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(diagnostic.Success, $"Get-NetworkDiagnosticCode function block not found in {relativePath}.");
        Assert.True(preflight.Success, $"Get-NetworkPreflightStatus function block not found in {relativePath}.");

        var combined = diagnostic.Groups["body"].Value + preflight.Groups["body"].Value;
        Assert.Contains("ProxyAuthRequired", combined);
        Assert.Contains("GitHubRateLimitOrBlock", combined);
        Assert.Contains("DnsFailure", combined);
        Assert.Contains("TlsFailure", combined);
        Assert.Contains("Timeout", combined);
        Assert.Contains("Get-DownloadFailureHint", combined);
        Assert.Contains("Network preflight", combined);
    }

    [Fact]
    public void MonolithNetworkActionsUseClassifiedPreflightDialog()
    {
        var script = ReadFile("LibreSpot.ps1");
        var helper = Regex.Match(
            script,
            @"function\s+Confirm-NetworkReadyForAction\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(helper.Success, "Confirm-NetworkReadyForAction function block not found.");

        var body = helper.Groups["body"].Value;
        Assert.Contains("Get-NetworkPreflightStatus", body);
        Assert.Contains("Network Check Failed", body);
        Assert.Contains("Write-Log", body);
        Assert.Contains("$status.Message", body);
        Assert.Contains("Confirm-NetworkReadyForAction -Message", script);
    }

    [Fact]
    public void NetworkPreflightHelpers_AreExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        var exports = exportBlock.Groups["list"].Value;
        Assert.Contains("'Get-NetworkDiagnosticCode'", exports);
        Assert.Contains("'Get-NetworkPreflightStatus'", exports);
        Assert.Contains("'Test-NetworkReady'", exports);
    }

    // ---------------------------------------------------------------------
    // Operation journal — destructive actions should leave structured JSONL
    // breadcrumbs with operation ids, safety decisions, and rollback hints.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void OperationJournalWriter_EmitsStructuredJsonlContract(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var writer = Regex.Match(
            script,
            @"function\s+Write-OperationJournalEntry\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        var starter = Regex.Match(
            script,
            @"function\s+Start-OperationJournalRun\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        var completer = Regex.Match(
            script,
            @"function\s+Complete-OperationJournalRun\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(writer.Success, $"Write-OperationJournalEntry function block not found in {relativePath}.");
        Assert.True(starter.Success, $"Start-OperationJournalRun function block not found in {relativePath}.");
        Assert.True(completer.Success, $"Complete-OperationJournalRun function block not found in {relativePath}.");

        var body = writer.Groups["body"].Value;
        Assert.Contains("operation-journal.jsonl", script);
        Assert.Contains("ConvertTo-Json -Compress", body);
        Assert.Contains("schemaVersion", body);
        Assert.Contains("operationId", body);
        Assert.Contains("safetyDecision", body);
        Assert.Contains("wouldChange", body);
        Assert.Contains("rollbackHint", body);
        Assert.Contains("tokenKind", body);
        Assert.Contains("previousStateRef", body);
        Assert.Contains("newState", body);
        Assert.Contains("undoAction", body);
        Assert.Contains("risk", body);
        Assert.Contains("Optimize-OperationJournalRetention", body);
        Assert.Contains("OPERATION_JOURNAL_MAX_BYTES", script);
        Assert.Contains("OPERATION_JOURNAL_RETAIN_BYTES", script);
        Assert.Contains("RUN_RECEIPT_PATH", script);
        Assert.Contains("run-receipt.latest.json", script);
        Assert.Contains("ConvertFrom-Json", completer.Groups["body"].Value);
        Assert.Contains("journal-retention", script);
        Assert.Contains("result         = 'Trimmed'", script);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void RemovePathSafely_JournalsSafetyDecisionsAndResults(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var remover = Regex.Match(
            script,
            @"function\s+Remove-PathSafely\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(remover.Success, $"Remove-PathSafely function block not found in {relativePath}.");
        var body = remover.Groups["body"].Value;

        Assert.Contains("Write-OperationJournalEntry", body);
        Assert.Contains("SkippedMissingTarget", body);
        Assert.Contains("RefusedUnsafeTarget", body);
        Assert.Contains("Allowed", body);
        Assert.Contains("Planned", body);
        Assert.Contains("Removed", body);
        Assert.Contains("Failed", body);
        Assert.Contains("Restore from a backup", body);
    }

    [Fact]
    public void OperationJournalHelpers_AreExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        var exports = exportBlock.Groups["list"].Value;
        Assert.Contains("'Write-OperationJournalEntry'", exports);
        Assert.Contains("'Start-OperationJournalRun'", exports);
        Assert.Contains("'Complete-OperationJournalRun'", exports);
        Assert.Contains("'OPERATION_JOURNAL_PATH'", script);
        Assert.Contains("'RUN_RECEIPT_PATH'", script);
    }

    [Fact]
    public void BuildScripts_SharedSyncUsesUtf8ReadsAndHostSpecificExclusions()
    {
        var script = ReadFile("Build-Scripts.ps1");

        Assert.Contains("[System.IO.File]::ReadAllText($backendScript, [System.Text.Encoding]::UTF8)", script);
        Assert.Contains("[System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)", script);
        Assert.DoesNotContain("$backendContent = Get-Content -Path $backendScript -Raw", script);
        Assert.Contains("$validatedNames = $sharedNames | Where-Object { $laneSpecificFunctions -notcontains $_ }", script);
        Assert.Contains("'Module-ApplySpicetify'", script);
        Assert.Contains("'Module-NukeSpotify'", script);
        Assert.Contains("'Update-SpicetifyCliProgress'", script);
    }

    [Fact]
    public void BackendCleanup_NukesDirectlyWithoutNativeUninstaller()
    {
        var script = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        var fnBody = Regex.Match(
            script,
            @"function\s+Module-NukeSpotify\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, "Module-NukeSpotify function block not found in backend script.");
        var body = fnBody.Groups["body"].Value;

        Assert.DoesNotContain("/UNINSTALL", body);
        Assert.DoesNotContain("Native Spotify uninstaller", body);
        Assert.Contains("Remove-AppxProvisionedPackage", body);
        Assert.Contains("Update-BackendState", body);
        Assert.Contains("Verification retry", body);
        Assert.Contains("Remove-PathSafely", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void PowerShellUserMessages_DelimitVariablesBeforeLiteralColons(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.DoesNotContain("$target:", script);
        Assert.DoesNotContain("$Label:", script);
        Assert.Contains("${target}:", script);
        Assert.Contains("${Label}:", script);
    }

    [Fact]
    public void BackendRemoveSelfData_DoesNotRecreateProfileAfterDeletingIt()
    {
        var script = ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        var removeSelfData = Regex.Match(
            script,
            @"'RemoveSelfData'\s*\{(?<body>.+?)^\s*\}\s*'EnableAutoReapply'",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(removeSelfData.Success, "RemoveSelfData action block not found in backend script.");
        var body = removeSelfData.Groups["body"].Value;

        var backupIndex = body.IndexOf("Backup directory", StringComparison.Ordinal);
        var localLogIndex = body.IndexOf("Log/crash directory", StringComparison.Ordinal);
        var configIndex = body.IndexOf("Config directory", StringComparison.Ordinal);
        Assert.True(backupIndex >= 0 && localLogIndex > backupIndex && configIndex > localLogIndex,
            "RemoveSelfData must remove the active LibreSpot config profile last.");
        Assert.Contains("RemovesActiveProfile", body);
        Assert.Contains("Write-RemoveSelfDataReceipt", body);
        Assert.Contains("Write-EventLine -Kind 'log' -Level 'SUCCESS'", body);
        Assert.DoesNotContain("Write-Log 'LibreSpot self-cleanup complete", body);
        Assert.Contains("if ($Action -ne 'RemoveSelfData')", script);
        Assert.Contains("'RemoveSelfData', 'ClearCache'", script);
        Assert.Contains("Write-Log \"--- Maintenance action '$Action' completed successfully ---\"", script);
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

        // The discriminating signal is SpotX's pre-patch backup of the original
        // bundle. Current SpotX writes Apps\xpui.bak; older builds used xpui.spa.bak.
        Assert.Contains("xpui.bak", body);
        Assert.Contains("xpui.spa.bak", body);
        Assert.Contains("xpui.spa", body);
        // Durable patched-binary backups corroborate a SpotX run after Spicetify
        // has consumed the xpui backup.
        Assert.Contains("Spotify.bak", body);
        // All three verdict states must be reachable from the function body.
        Assert.Contains("'Verified'", body);
        Assert.Contains("'Unverified'", body);
        Assert.Contains("'Missing'", body);
        // Verdict requires the patched bundle plus a backup or patched-binary marker.
        Assert.Contains("($hasBackup -or $hasBinBackup) -and $hasBundle", body);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void SpotXPatchVerification_UsesPowerShell51SafeParentDirectoryResolution(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Get-SpotXPatchVerification\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"Get-SpotXPatchVerification function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        Assert.Contains("[System.IO.Path]::GetDirectoryName($SpotifyExePath)", body);
        Assert.DoesNotContain("Split-Path -LiteralPath", body);
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

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    [InlineData("src/powershell/shared/Module-InstallSpotX.ps1")]
    public void InstallSpotX_RetriesClassifiedDownloadFailureViaMirror(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Module-InstallSpotX\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success, $"Module-InstallSpotX function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        // A classified SpotX download failure must drive a single mirror-toggle
        // retry, not surface immediately as a hard failure.
        Assert.Contains("Get-SpotXDownloadRetryPlan", body);
        Assert.Contains("-mirror", body);
        Assert.Contains("$spotxAttempt", body);
    }

    [Fact]
    public void SpotXDownloadRetryPlan_IsExportedToWorkerRunspace()
    {
        var script = ReadFile("LibreSpot.ps1");
        var exportBlock = Regex.Match(script, @"\$functionNamesForWorker\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(exportBlock.Success, "Worker function export list not found.");
        Assert.Contains("'Get-SpotXDownloadRetryPlan'", exportBlock.Groups["list"].Value);
    }

    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    [InlineData("src/powershell/shared/Module-InstallSpotX.ps1")]
    public void InstallSpotX_ForceClosesSpotifyBeforeTheFirstLaunch(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            @"function\s+Module-InstallSpotX\s*\{(?<body>.+?)^\}",
            RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(fnBody.Success, $"Module-InstallSpotX function block not found in {relativePath}.");
        var body = fnBody.Groups["body"].Value;

        // The first config-generation launch must force-close any running
        // Spotify before reopening, so it starts from a clean patched process.
        var killIndex = body.IndexOf("Force-closing any running Spotify", StringComparison.Ordinal);
        var launchIndex = body.IndexOf("Start-Process -FilePath 'explorer.exe'", StringComparison.Ordinal);
        Assert.True(killIndex >= 0, "Force-close step missing before the first Spotify launch.");
        Assert.True(launchIndex > killIndex, "Force-close must run before the Spotify launch.");
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
        // Language: allowlist membership against known BCP-47 codes.
        Assert.Matches(@"\$allowedLanguages\s*-contains\s*\$lang", script);
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
            "$Config.SpotX_Language",
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
        Assert.Contains("'Open-VerifiedScriptForExecution'", exportBlock.Groups["list"].Value);
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

    // ---------------------------------------------------------------------
    // ShouldProcess coverage — mutating helpers must declare
    // SupportsShouldProcess so -WhatIf and -Confirm propagate correctly.
    // PSScriptAnalyzer will enforce the reverse (ShouldProcess declared but
    // never called), but this test locks in the forward contract.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1", "Remove-PathSafely")]
    [InlineData("LibreSpot.ps1", "Save-LibreSpotConfig")]
    [InlineData("LibreSpot.ps1", "Set-PathEntries")]
    [InlineData("LibreSpot.ps1", "Register-AutoReapplyTask")]
    [InlineData("LibreSpot.ps1", "Unregister-AutoReapplyTask")]
    [InlineData("LibreSpot.ps1", "Clear-LibreSpotCache")]
    [InlineData("LibreSpot.ps1", "Move-ConfigFileToQuarantine")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1", "Remove-PathSafely")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1", "Save-LibreSpotConfig")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1", "Set-PathEntries")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1", "Register-AutoReapplyTask")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1", "Unregister-AutoReapplyTask")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1", "Clear-LibreSpotCache")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1", "Move-ConfigFileToQuarantine")]
    public void MutatingHelper_DeclaresAndCallsShouldProcess(string relativePath, string functionName)
    {
        var script = ReadFile(relativePath.Split('/'));
        var fnBody = Regex.Match(
            script,
            $@"function\s+{Regex.Escape(functionName)}\s*\{{(?<body>.+?)^\}}",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(fnBody.Success, $"{functionName} function block not found in {relativePath}.");

        var fullBlock = fnBody.Value;
        Assert.Contains("SupportsShouldProcess", fullBlock);
        Assert.Contains("$PSCmdlet.ShouldProcess(", fnBody.Groups["body"].Value);
    }

    // ---------------------------------------------------------------------
    // Accessibility — icon-only and StackPanel-content controls must have
    // AutomationProperties.Name so screen readers can identify them.
    // ---------------------------------------------------------------------
    [Theory]
    [InlineData("LibreSpot.ps1")]
    [InlineData("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1")]
    public void AssetCacheHelpers_UpdateIndexQuarantineCorruptionAndWriteClearReceipt(string relativePath)
    {
        var script = ReadFile(relativePath.Split('/'));

        Assert.Contains("function Update-AssetCacheIndexEntry", script);
        Assert.Contains("asset-cache-index.json", script);
        Assert.Contains("Update-AssetCacheIndexEntry -SHA256Hash", script);
        Assert.Contains("-MarkVerified -MarkUsed", script);
        Assert.Contains("-Status 'corrupt'", script);
        Assert.Contains("Move-Item", script);
        Assert.Contains("-Result 'Quarantined'", script);
        Assert.Contains("fileCount", script);
        Assert.Contains("totalBytes", script);
        Assert.Contains("Asset cache cleared", script);
    }

    [Theory]
    [InlineData("MinimizeBtn", "Minimize window")]
    [InlineData("CloseTitleBtn", "Close window")]
    [InlineData("ModeEasy", "Recommended setup")]
    [InlineData("ModeCustom", "Custom Install")]
    [InlineData("ModeMaint", "Maintenance")]
    [InlineData("BtnBackupConfig", "Create configuration backup")]
    [InlineData("BtnRestoreConfig", "Restore the newest backup")]
    [InlineData("BtnCheckUpdates", "Check pinned versions")]
    [InlineData("BtnRepairMarketplace", "Repair and open Marketplace")]
    [InlineData("BtnReapply", "Reapply after a Spotify update")]
    [InlineData("BtnSafeMode", "Safe mode")]
    [InlineData("BtnSpicetifyRestore", "Restore vanilla Spotify")]
    [InlineData("BtnUninstallSpicetify", "Uninstall Spicetify")]
    [InlineData("BtnFullReset", "Full Reset")]
    public void PowerShellXaml_InteractiveControlHasAutomationName(string controlName, string expectedFragment)
    {
        var script = ReadFile("LibreSpot.ps1");

        var pattern = $@"Name=""{controlName}""[^>]*AutomationProperties\.Name=""(?<name>[^""]+)""";
        var match = Regex.Match(script, pattern);

        Assert.True(match.Success, $"Control '{controlName}' is missing AutomationProperties.Name in LibreSpot.ps1 XAML.");
        Assert.Contains(expectedFragment, match.Groups["name"].Value);
    }

    [Fact]
    public void PowerShellAndReadme_UseWpfFeatureNamesForRecommendedDefaults()
    {
        var script = ReadFile("LibreSpot.ps1");
        var readme = ReadFile("README.md");

        foreach (var text in new[] { script, readme })
        {
            Assert.DoesNotContain("Easy Install", text);
            Assert.DoesNotContain("Shuffle+", text);
        }

        Assert.Contains("Recommended setup", script);
        Assert.Contains("Recommended setup", readme);
        Assert.Contains("True Shuffle", script);
        Assert.Contains("True Shuffle", readme);
    }
}
