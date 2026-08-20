using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class SpicetifyV3ConflictDetectorTests
{
    [Fact]
    public void DetectsXpuiBackupMarkerWithoutAWorkingCliProbe()
    {
        using var fixture = new TemporaryDirectory();
        var spotifyPath = Path.Combine(fixture.Path, "Spotify", "Spotify.exe");
        var markerPath = Path.Combine(fixture.Path, "Spotify", "Apps", "xpui.spa.backup");
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
        File.WriteAllText(markerPath, "v3 marker");

        var report = SpicetifyV3ConflictDetector.Detect(
            spotifyPath,
            Path.Combine(fixture.Path, "spicetify"),
            Path.Combine(fixture.Path, "spicetify-config"));

        Assert.True(report.IsConflict);
        Assert.Contains("Apps\\xpui.spa.backup", report.Markers);
        Assert.Equal("spicetify restore", report.RecommendedAction);
    }

    [Fact]
    public void DetectsV3LayoutDirectoriesAndNewerCliMajor()
    {
        using var fixture = new TemporaryDirectory();
        var installDirectory = Path.Combine(fixture.Path, "spicetify");
        var configDirectory = Path.Combine(fixture.Path, "spicetify-config");
        Directory.CreateDirectory(Path.Combine(installDirectory, "modules"));
        Directory.CreateDirectory(Path.Combine(configDirectory, "hooks"));

        var report = SpicetifyV3ConflictDetector.Detect(
            Path.Combine(fixture.Path, "Spotify", "Spotify.exe"),
            installDirectory,
            configDirectory,
            "v3.0.0-beta.1");

        Assert.True(report.IsConflict);
        Assert.Contains("spicetify install\\modules", report.Markers);
        Assert.Contains("spicetify config\\hooks", report.Markers);
        Assert.Contains("Spicetify CLI major 3", report.Markers);
    }

    [Fact]
    public void LeavesPinnedV2LayoutReady()
    {
        using var fixture = new TemporaryDirectory();

        var report = SpicetifyV3ConflictDetector.Detect(
            Path.Combine(fixture.Path, "Spotify", "Spotify.exe"),
            Path.Combine(fixture.Path, "spicetify"),
            Path.Combine(fixture.Path, "spicetify-config"),
            "2.44.0");

        Assert.False(report.IsConflict);
        Assert.Empty(report.Markers);
    }

    [Fact]
    public void MissingV3SupportContractRefusesMutationWithRestorePath()
    {
        var report = SpicetifyV3ConflictDetector.EvaluateSupportContract(
            "3.0.0-beta.1",
            "1.2.95",
            null);

        Assert.True(report.IsFeatureActive);
        Assert.False(report.Result.ListAvailable);
        Assert.True(report.Result.ContractUnavailable);
        Assert.False(report.Result.CanApply);
        Assert.False(report.Result.CanAutoApply);
        Assert.Equal(1, report.Result.SupportCommandExitCode);
        Assert.Contains("spicetify restore", report.Result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "LibreSpot.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
