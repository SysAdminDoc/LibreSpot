using System.IO;
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
}
