using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class BackendScriptServiceTests
{
    [Fact]
    public void RunAsync_HoldsExecutionCopyWithReadOnlySharing()
    {
        var source = ReadRepoFile("src", "LibreSpot.Desktop", "Services", "BackendScriptService.cs");

        Assert.Contains("executionCopyGuard", source);
        Assert.Contains("FileMode.CreateNew", source);
        Assert.Contains("FileShare.Read", source);
        Assert.DoesNotContain("File.Copy(canonicalPath, executionCopy", source);
    }

    [Fact]
    public void AppStartup_CleansStaleExecutionCopies()
    {
        var source = ReadRepoFile("src", "LibreSpot.Desktop", "App.xaml.cs");

        Assert.Contains("BackendScriptService.CleanStaleExecutionCopies()", source);
        Assert.Contains("ShellIntegrationService.RegisterCurrentUserShellHooksIfPossible()", source);
        Assert.Contains("ShellIntegrationService.ConfigureJumpListIfPossible()", source);
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

    [Fact]
    public async Task RunAsync_ResetsWatchdogWhenBackendKeepsEmittingOutput()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var runtimeDirectory = Path.Combine(tempRoot, "Runtime");
        var scriptPath = Path.Combine(tempRoot, "chatty-backend.ps1");
        var messages = new List<BackendMessage>();
        Directory.CreateDirectory(tempRoot);
        await File.WriteAllTextAsync(
            scriptPath,
            """
            for ($i = 0; $i -lt 6; $i++) {
                Write-Output "@@LS@@|status|INFO|tick $i"
                Start-Sleep -Milliseconds 35
            }
            exit 0
            """);

        try
        {
            var service = new BackendScriptService(
                runtimeDirectory,
                noBackendMode: false,
                new BackendWatchdogOptions(
                    TimeSpan.FromMilliseconds(70),
                    TimeSpan.FromMilliseconds(140),
                    TimeSpan.FromMilliseconds(10)),
                scriptPath);

            var result = await service.RunAsync("Install", Path.Combine(tempRoot, "config.json"), messages.Add);

            Assert.True(result.Success);
            Assert.Null(result.ErrorCode);
            Assert.Contains(messages, message => message.Kind == "status" && message.Payload == "tick 5");
            Assert.DoesNotContain(messages, message =>
                message.Kind == "log" &&
                message.Payload.Contains("watchdog", StringComparison.OrdinalIgnoreCase));
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

    private static string ReadRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }

        var root = dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
        return File.ReadAllText(Path.Combine(new[] { root }.Concat(relativeParts).ToArray()));
    }
}
