using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class BackendScriptServiceTests
{
    [Fact]
    public void RunAsync_HoldsExecutionCopyWithReadOnlySharing()
    {
        var source = ReadRepoFile("src", "LibreSpot.Core", "BackendScriptService.cs");

        Assert.Contains("executionCopyGuard", source);
        Assert.Contains("FileMode.CreateNew", source);
        Assert.Contains("FileShare.Read", source);
        Assert.DoesNotContain("File.Copy(canonicalPath, executionCopy", source);
    }

    [Fact]
    public void AppStartup_CleansStaleExecutionCopies()
    {
        var appSource = ReadRepoFile("src", "LibreSpot.Desktop", "App.xaml.cs");
        var mainWindowSource = ReadRepoFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");

        Assert.Contains("BackendScriptService.CleanStaleExecutionCopies()", appSource);
        Assert.DoesNotContain("ShellIntegrationService.RegisterCurrentUserShellHooksIfPossible()", appSource);
        Assert.Contains("await _viewModel.InitializeAsync();", mainWindowSource);
        Assert.Contains("ShellIntegrationService.RegisterCurrentUserShellHooksIfPossible()", mainWindowSource);
        Assert.Contains("ShellIntegrationService.ConfigureJumpListIfPossible()", mainWindowSource);
    }

    [Fact]
    public void TryEnsureBundledAssets_ExtractsTheEngineArchiveThatEveryPinExpects()
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new BackendScriptService(runtimeDirectory);

            var assetsDirectory = service.TryEnsureBundledAssets();

            Assert.Equal(Path.Combine(runtimeDirectory, "assets"), assetsDirectory);
            var extracted = Path.Combine(assetsDirectory!, BackendScriptService.BundledEngineFileName);
            Assert.True(File.Exists(extracted), $"The bundled engine archive was not written to {extracted}.");

            // The extracted copy is what the backend installs, so its bytes must be
            // the archive every pinned SHA256 in the catalog was taken from.
            var expected = Convert.ToHexString(SHA256.HashData(
                ReadRepoBytes("resources", "custom-apps", "librespot-engine.zip"))).ToLowerInvariant();
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extracted))).ToLowerInvariant();
            Assert.Equal(expected, actual);

            // A second call must reuse the extracted copy rather than rewrite it.
            var writtenAt = File.GetLastWriteTimeUtc(extracted);
            Assert.Equal(assetsDirectory, service.TryEnsureBundledAssets());
            Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(extracted));
        }
        finally
        {
            try { Directory.Delete(runtimeDirectory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void RunAsync_PointsTheBackendAtTheBundledAssetsFolder()
    {
        var source = ReadRepoFile("src", "LibreSpot.Core", "BackendScriptService.cs");

        Assert.Contains("process.StartInfo.Environment[\"LIBRESPOT_BUNDLED_ASSETS\"] = bundledAssetsDirectory;", source);
        Assert.Contains("LIBRESPOT_BUNDLED_ASSETS", ReadRepoFile("src", "powershell", "shared", "Module-InstallCustomApps.ps1"));
    }

    [Fact]
    public async Task RunAsync_RejectsUnknownActionsBeforePreparingRuntime()
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var service = new BackendScriptService(runtimeDirectory);

        var result = await service.RunAsync("DefinitelyNotAnAction", "config.json", _ => { });

        Assert.False(result.Success);
        Assert.Contains("Unknown backend action", result.ErrorMessage);
        Assert.False(Directory.Exists(runtimeDirectory));
    }

    [Fact]
    public async Task RunAsync_RejectsBlankConfigPathBeforePreparingRuntime()
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var service = new BackendScriptService(runtimeDirectory);

        var result = await service.RunAsync("Install", " ", _ => { });

        Assert.False(result.Success);
        Assert.Contains("configuration path", result.ErrorMessage);
        Assert.False(Directory.Exists(runtimeDirectory));
    }

    [Theory]
    [InlineData("RepairMarketplace")]
    [InlineData("OpenMarketplace")]
    [InlineData("ExportMarketplaceState")]
    [InlineData("RestoreMarketplaceState")]
    [InlineData("ClearCache")]
    [InlineData("EnableAutoReapply")]
    [InlineData("DisableAutoReapply")]
    [InlineData("WatchAutoReapply")]
    public async Task RunAsync_AcceptsMaintenanceActionsBeforeConfigPathValidation(string action)
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var service = new BackendScriptService(runtimeDirectory);

        var result = await service.RunAsync(action, " ", _ => { });

        Assert.False(result.Success);
        Assert.Contains("configuration path", result.ErrorMessage);
        Assert.False(Directory.Exists(runtimeDirectory));
    }

    [Fact]
    public async Task RunAsync_HonorsPreCanceledTokenBeforePreparingRuntime()
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var service = new BackendScriptService(runtimeDirectory);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await service.RunAsync("Install", "config.json", _ => { }, cts.Token);

        Assert.False(result.Success);
        Assert.Contains("canceled", result.ErrorMessage);
        Assert.False(Directory.Exists(runtimeDirectory));
    }

    [Fact]
    public async Task RunAsync_WarnsAndStopsBackendAfterNoOutputStall()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var runtimeDirectory = Path.Combine(tempRoot, "Runtime");
        var scriptPath = Path.Combine(tempRoot, "silent-backend.ps1");
        var messages = new List<BackendMessage>();
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(scriptPath, "Start-Sleep -Milliseconds 1000\r\nexit 0\r\n");

        try
        {
            var service = new BackendScriptService(
                runtimeDirectory,
                noBackendMode: false,
                new BackendWatchdogOptions(
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(150),
                    TimeSpan.FromMilliseconds(10)),
                scriptPath);

            var result = await service.RunAsync("Install", Path.Combine(tempRoot, "config.json"), messages.Add);

            Assert.False(result.Success);
            Assert.Equal("BackendHostStalled", result.ErrorCode);
            Assert.Contains("watchdog", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(messages, message =>
                message.Kind == "status" &&
                message.Level == "WARN" &&
                message.Payload.Contains("Still waiting", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(messages, message =>
                message.Kind == "log" &&
                message.Level == "WARN" &&
                message.Payload.Contains("No backend output", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(messages, message =>
                message.Kind == "log" &&
                message.Level == "ERROR" &&
                message.Payload.Contains("watchdog", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// How long this machine takes, right now, to get a PowerShell host up and its
    /// first line back through the service. Runs with a watchdog that cannot fire
    /// so the measurement itself is never the thing that times out.
    /// </summary>
    private static async Task<TimeSpan> MeasureBackendColdStartAsync(string tempRoot)
    {
        var probeDirectory = Path.Combine(tempRoot, "ColdStartProbe");
        var probeScript = Path.Combine(tempRoot, "cold-start-probe.ps1");
        Directory.CreateDirectory(probeDirectory);
        await File.WriteAllTextAsync(
            probeScript,
            """
            Write-Output "@@LS@@|status|INFO|up"
            exit 0
            """);

        var probe = new BackendScriptService(
            probeDirectory,
            noBackendMode: false,
            // Generous enough that the probe is never the thing that times out,
            // tight enough that a host which cannot start fails this test in half
            // a minute instead of five.
            new BackendWatchdogOptions(
                TimeSpan.FromSeconds(20),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(1)),
            probeScript);

        var stopwatch = Stopwatch.StartNew();
        var result = await probe.RunAsync("Install", Path.Combine(tempRoot, "probe-config.json"), _ => { });
        stopwatch.Stop();

        Assert.True(result.Success, "The cold-start probe could not start a backend host at all.");
        return stopwatch.Elapsed;
    }

    [Fact]
    public async Task RunAsync_ResetsWatchdogWhenBackendKeepsEmittingOutput()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var runtimeDirectory = Path.Combine(tempRoot, "Runtime");
        var messages = new List<BackendMessage>();
        Directory.CreateDirectory(tempRoot);
        // Two different gaps have to fit inside the stall budget, and only one of
        // them is under this test's control. The inter-tick gaps are small and
        // predictable. The first gap is the PowerShell host's cold start, because
        // the idle clock begins at Process.Start and nothing is written until the
        // host is up, and on a saturated machine that has been measured at over
        // ten seconds. Every fixed budget picked so far has eventually been too
        // small, so the budget is measured on this machine at this moment instead
        // of guessed: start a host that prints one line and exits, see how long
        // the line took, and give the real run several times that.
        // Clamped at both ends. The floor is what a quiet machine gets, where the
        // measurement is a few hundred milliseconds and multiplying it would give
        // a budget thinner than the one that already proved too small. The ceiling
        // matters more: the script has to outlast the budget, so an unbounded
        // measurement would size a script to match and a single pathological
        // reading could leave this test running for half an hour.
        var coldStart = await MeasureBackendColdStartAsync(tempRoot);
        var stallTimeout = TimeSpan.FromMilliseconds(
            Math.Clamp(coldStart.TotalMilliseconds * 4, 5000, 15000));

        // The script then has to keep talking for longer than that budget, or the
        // run finishes before the watchdog could ever fire and the assertions hold
        // whether or not output resets the timer.
        var tickGap = TimeSpan.FromMilliseconds(300);
        var tickCount = (int)Math.Ceiling(stallTimeout.TotalMilliseconds * 1.2 / tickGap.TotalMilliseconds);
        var scriptPath = Path.Combine(tempRoot, "chatty-backend.ps1");
        await File.WriteAllTextAsync(
            scriptPath,
            $$"""
            for ($i = 0; $i -lt {{tickCount}}; $i++) {
                Write-Output "@@LS@@|status|INFO|tick $i"
                Start-Sleep -Milliseconds {{(int)tickGap.TotalMilliseconds}}
            }
            exit 0
            """);

        try
        {
            var service = new BackendScriptService(
                runtimeDirectory,
                noBackendMode: false,
                new BackendWatchdogOptions(
                    TimeSpan.FromMilliseconds(stallTimeout.TotalMilliseconds / 2),
                    stallTimeout,
                    TimeSpan.FromMilliseconds(50)),
                scriptPath);

            var startedAt = Stopwatch.StartNew();
            var result = await service.RunAsync("Install", Path.Combine(tempRoot, "config.json"), messages.Add);
            startedAt.Stop();

            Assert.True(result.Success);
            Assert.Null(result.ErrorCode);
            Assert.Contains(messages, message => message.Kind == "status" && message.Payload == $"tick {tickCount - 1}");
            Assert.DoesNotContain(messages, message =>
                message.Kind == "log" &&
                message.Payload.Contains("watchdog", StringComparison.OrdinalIgnoreCase));

            // Guards the assertions above against going vacuous: if the script is ever
            // shortened below the stall budget, the watchdog cannot fire and the rest
            // of this test stops meaning anything.
            Assert.True(
                startedAt.Elapsed > stallTimeout,
                $"The backend ran for {startedAt.Elapsed}, which is inside the {stallTimeout} stall budget, so this test would pass even with the watchdog reset removed.");
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_UsesCallerOperationIdForBackendArgumentsAndMessages()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var runtimeDirectory = Path.Combine(tempRoot, "Runtime");
        var scriptPath = Path.Combine(tempRoot, "correlated-backend.ps1");
        var operationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var messages = new List<BackendMessage>();
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(
            scriptPath,
            "param([string]$Action,[string]$ConfigPath,[string]$OperationId)\r\n" +
            "Write-Output \"@@LS@@|$OperationId|status|INFO|$Action correlated\"\r\nexit 0\r\n");

        try
        {
            var service = new BackendScriptService(
                runtimeDirectory,
                noBackendMode: false,
                BackendWatchdogOptions.Default,
                backendScriptPathOverride: scriptPath);

            var result = await service.RunAsync(
                "Install",
                Path.Combine(tempRoot, "config.json"),
                messages.Add,
                operationId);

            Assert.True(result.Success, result.ErrorMessage);
            var message = Assert.Single(messages);
            Assert.Equal(operationId, message.OperationId);
            Assert.Equal("Install correlated", message.Payload);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_RejectsBackendMessageWithDifferentOperationId()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var runtimeDirectory = Path.Combine(tempRoot, "Runtime");
        var scriptPath = Path.Combine(tempRoot, "mismatched-backend.ps1");
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(
            scriptPath,
            "param([string]$Action,[string]$ConfigPath,[string]$OperationId)\r\n" +
            "Write-Output '@@LS@@|bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb|status|INFO|wrong run'\r\nexit 0\r\n");

        try
        {
            var service = new BackendScriptService(
                runtimeDirectory,
                noBackendMode: false,
                BackendWatchdogOptions.Default,
                backendScriptPathOverride: scriptPath);
            var result = await service.RunAsync(
                "Install",
                Path.Combine(tempRoot, "config.json"),
                _ => { },
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            Assert.False(result.Success);
            Assert.Contains("correlation mismatch", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task RunAsync_ReturnsFailureWhenRuntimeDirectoryCannotBeCreated()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var runtimeDirectory = Path.Combine(tempRoot, "Runtime");
            await File.WriteAllTextAsync(runtimeDirectory, "not-a-directory");
            var service = new BackendScriptService(runtimeDirectory);

            var result = await service.RunAsync("Install", "config.json", _ => { });

            Assert.False(result.Success);
            Assert.Contains("backend runtime folder", result.ErrorMessage);
            Assert.True(File.Exists(runtimeDirectory));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_HardensRuntimeDirectoryAcls()
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"), "Runtime");
        try
        {
            var service = new BackendScriptService(runtimeDirectory);
            await service.RunAsync("Install", "config.json", _ => { });

            Assert.True(Directory.Exists(runtimeDirectory));

            var dirInfo = new DirectoryInfo(runtimeDirectory);
            var security = dirInfo.GetAccessControl();
            Assert.True(security.AreAccessRulesProtected, "Runtime directory should have inheritance disabled.");

            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, targetType: typeof(SecurityIdentifier));
            var currentUser = WindowsIdentity.GetCurrent().User!;
            var hasOwnerRule = false;
            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference.Value == currentUser.Value && rule.AccessControlType == AccessControlType.Allow)
                {
                    hasOwnerRule = true;
                }
            }
            Assert.True(hasOwnerRule, "Runtime directory should grant the current user explicit access.");
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(runtimeDirectory)!, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CleanStaleExecutionCopies_RemovesLeftoverRunFiles()
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtimeDirectory);
        try
        {
            var staleFile = Path.Combine(runtimeDirectory, "LibreSpot.Backend.deadbeef.run.ps1");
            File.WriteAllText(staleFile, "stale");
            Assert.True(File.Exists(staleFile));

            BackendScriptService.CleanStaleExecutionCopies(runtimeDirectory);

            Assert.False(File.Exists(staleFile));
        }
        finally
        {
            try { Directory.Delete(runtimeDirectory, recursive: true); } catch { }
        }
    }

    private static byte[] ReadRepoBytes(params string[] relativeParts) =>
        File.ReadAllBytes(ResolveRepoPath(relativeParts));

    private static string ReadRepoFile(params string[] relativeParts) =>
        File.ReadAllText(ResolveRepoPath(relativeParts));

    private static string ResolveRepoPath(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }

        var root = dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
        return Path.Combine(new[] { root }.Concat(relativeParts).ToArray());
    }
}
