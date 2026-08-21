using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class AtomicFileTests
{
    [Fact]
    public async Task WriteAllTextAsync_ReplacesTheDestinationAndCleansUpTheTempFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LibreSpot.AtomicFile.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        File.WriteAllText(path, "old");

        try
        {
            await AtomicFile.WriteAllTextAsync(path, "new");

            Assert.Equal("new", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteAsync_LeavesTheDestinationAloneWhenTheWriterThrows()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LibreSpot.AtomicFile.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "config.json");
        File.WriteAllText(path, "keep-me");

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AtomicFile.WriteAsync(path, (_, _) => throw new InvalidOperationException("boom")));

            Assert.Equal("keep-me", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentWrites_UseUniqueTempNamesAndLeaveOneCompleteDestination()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LibreSpot.AtomicFile.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "profile.json");

        try
        {
            var first = AtomicFile.WriteAllTextAsync(path, new string('a', 32_768));
            var second = AtomicFile.WriteAllTextAsync(path, new string('b', 32_768));
            await Task.WhenAll(first, second);

            var content = File.ReadAllText(path);
            Assert.True(content.All(ch => ch == 'a') || content.All(ch => ch == 'b'));
            Assert.Equal(32_768, content.Length);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CreateTempPath_NeverReusesAProcessIdOnlyName()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LibreSpot.AtomicFile.Tests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(directory, "desk.json");

        var first = AtomicFile.CreateTempPath(destination);
        var second = AtomicFile.CreateTempPath(destination);

        Assert.NotEqual(first, second);
        Assert.DoesNotContain(Environment.ProcessId.ToString(), Path.GetFileName(first));
        Assert.StartsWith("desk.json.", Path.GetFileName(first), StringComparison.Ordinal);
    }

    [Fact]
    public void CreateTempPath_ResolvesARelativeDestinationAgainstTheCurrentDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LibreSpot.AtomicFile.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = directory;
            var tempPath = AtomicFile.CreateTempPath("plan.json");

            Assert.Equal(directory, Path.GetDirectoryName(tempPath), StringComparer.OrdinalIgnoreCase);
            AtomicFile.WriteAllText("plan.json", "ok");
            Assert.Equal("ok", File.ReadAllText(Path.Combine(directory, "plan.json")));
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Environment.CurrentDirectory = previous;
            Directory.Delete(directory, recursive: true);
        }
    }
}
