using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class BackendScriptServiceTests
{
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
}
