extern alias Cli;

using System.Text.Json;
using Xunit;
using CliApp = Cli::LibreSpot.Cli.CliApplication;
using CliEnvironmentSnapshot = Cli::LibreSpot.Desktop.Models.EnvironmentSnapshot;
using CliHealthSeverity = Cli::LibreSpot.Desktop.Models.HealthSeverity;
using CliStackHealthComponent = Cli::LibreSpot.Desktop.Models.StackHealthComponent;
using CliStackHealthReport = Cli::LibreSpot.Desktop.Models.StackHealthReport;

namespace LibreSpot.Desktop.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public void VersionCommand_WritesConsoleArtifactVersion()
    {
        var result = Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("LibreSpot.Cli 4.0.0-preview.6", result.Stdout.Trim());
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void StatusJson_UsesHealthReportComponents()
    {
        var snapshot = Snapshot(
            spotifyInstalled: true,
            spicetifyInstalled: true,
            Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready, version: "1.2.92"),
            Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready, version: "2.43.2"));

        var result = Run(
            new[] { "status", "--json", "--config-path", "C:\\LibreSpot\\config.json" },
            _ => snapshot);

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Stack ready", doc.RootElement.GetProperty("statusTitle").GetString());
        Assert.Equal("C:\\LibreSpot\\config.json", doc.RootElement.GetProperty("configPath").GetString());
        Assert.Equal("spotify", doc.RootElement.GetProperty("components")[0].GetProperty("id").GetString());
        Assert.Equal("1.2.92", doc.RootElement.GetProperty("components")[0].GetProperty("detectedVersion").GetString());
    }

    [Fact]
    public void DetectJson_MapsCleanSlateToNotInstalledExitCode()
    {
        var snapshot = Snapshot(
            spotifyInstalled: false,
            spicetifyInstalled: false,
            Component("spotify", "Spotify", "Not installed", CliHealthSeverity.Info, action: "Install"),
            Component("spicetify-cli", "Spicetify CLI", "Not installed", CliHealthSeverity.Info, action: "Install"));

        var result = Run(new[] { "detect", "--json" }, _ => snapshot);

        Assert.Equal(10, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal("notInstalled", doc.RootElement.GetProperty("state").GetString());
        Assert.Equal(10, doc.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Contains("Install", doc.RootElement.GetProperty("recommendedRepairIds").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public void DetectIntune_OnlyCompliantReturnsSuccessStdout()
    {
        var snapshot = Snapshot(
            spotifyInstalled: true,
            spicetifyInstalled: true,
            Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready),
            Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready));

        var result = Run(new[] { "detect", "--intune" }, _ => snapshot);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("LibreSpot compliant", result.Stdout.Trim());
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void DetectIntune_NoncompliantUsesNonZeroExitAndStderr()
    {
        var snapshot = Snapshot(
            spotifyInstalled: true,
            spicetifyInstalled: true,
            Component("marketplace", "Marketplace", "Files missing", CliHealthSeverity.Warning, action: "RepairMarketplace"));

        var result = Run(new[] { "detect", "--intune" }, _ => snapshot);

        Assert.Equal(11, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("LibreSpot drifted", result.Stderr);
    }

    [Fact]
    public void ValidateAnswerFile_RequiresConsentFields()
    {
        var answerFile = Path.Combine(Path.GetTempPath(), "librespot-answer-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(answerFile, "{\"schemaVersion\":1,\"eulaAccepted\":true}");
        try
        {
            var result = Run("validate", "--answer-file", answerFile, "--json");

            Assert.Equal(2, result.ExitCode);
            using var doc = JsonDocument.Parse(result.Stdout);
            Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
            Assert.Contains("riskAcknowledged", doc.RootElement.GetProperty("errors")[0].GetProperty("path").GetString());
        }
        finally
        {
            File.Delete(answerFile);
        }
    }

    [Fact]
    public void ValidateAnswerFile_AcceptsMinimalSample()
    {
        var repoRoot = ResolveRepoRoot();
        var sample = Path.Combine(repoRoot, "samples", "minimal.json");

        var result = Run("validate", "--answer-file", sample, "--json");

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.True(doc.RootElement.GetProperty("valid").GetBoolean());
    }

    private static CliRunResult Run(params string[] args) =>
        Run(args, _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true));

    private static CliRunResult Run(string[] args, Func<string, CliEnvironmentSnapshot> snapshotFactory)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliApp.Run(args, stdout, stderr, snapshotFactory);
        return new CliRunResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    private static CliEnvironmentSnapshot Snapshot(
        bool spotifyInstalled,
        bool spicetifyInstalled,
        params CliStackHealthComponent[] components) =>
        new()
        {
            SpotifyInstalled = spotifyInstalled,
            SpicetifyInstalled = spicetifyInstalled,
            MarketplaceFilesPresent = spotifyInstalled && spicetifyInstalled,
            MarketplaceRegistered = spotifyInstalled && spicetifyInstalled,
            SavedConfigExists = true,
            ConfigFolderExists = true,
            AutoReapplyTaskRegistered = false,
            HostArchitecture = "x64",
            ProcessArchitecture = "x64",
            HealthReport = new CliStackHealthReport(components)
        };

    private static CliStackHealthComponent Component(
        string id,
        string name,
        string status,
        string severity,
        string? version = null,
        string? action = null) =>
        new(
            id,
            name,
            status,
            severity,
            version,
            null,
            null,
            $"{name} evidence",
            string.IsNullOrWhiteSpace(action) ? Array.Empty<string>() : new[] { action });

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "README.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not resolve repo root.");
    }

    private sealed record CliRunResult(int ExitCode, string Stdout, string Stderr);
}
