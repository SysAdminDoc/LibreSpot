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
    public void VersionJson_EmitsDependencyPins()
    {
        var result = Run("version", "--json");

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("4.0.0-preview.6", doc.RootElement.GetProperty("productVersion").GetString());
        Assert.Equal("2.43.2", doc.RootElement.GetProperty("dependencies").GetProperty("spicetifyCli").GetProperty("version").GetString());
        Assert.Equal("1.0.8", doc.RootElement.GetProperty("dependencies").GetProperty("marketplaceVersion").GetString());
        Assert.StartsWith("3284673", doc.RootElement.GetProperty("dependencies").GetProperty("spotX").GetProperty("commit").GetString());
    }

    [Fact]
    public void StatusJson_UsesHealthReportComponents()
    {
        var snapshot = Snapshot(
            spotifyInstalled: true,
            spicetifyInstalled: true,
            Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready, version: "1.2.92"),
            Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready, version: "2.43.2"),
            Component("backups", "Backups", "2 backups", CliHealthSeverity.Ready),
            Component("auto-reapply-watcher", "Auto-reapply watcher", "UpToDate", CliHealthSeverity.Ready),
            Component("post-spotify-update", "After Spotify update", "No drift", CliHealthSeverity.Ready, changed: DateTime.Parse("2026-06-27T12:00:00Z").ToUniversalTime()));

        var result = Run(
            new[] { "status", "--json", "--config-path", "C:\\LibreSpot\\config.json" },
            _ => snapshot);

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Stack ready", doc.RootElement.GetProperty("statusTitle").GetString());
        Assert.Equal("C:\\LibreSpot\\config.json", doc.RootElement.GetProperty("configPath").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("backupCount").GetInt32());
        Assert.Equal("UpToDate", doc.RootElement.GetProperty("lastWatcherOutcome").GetString());
        Assert.Equal("2026-06-27T12:00:00+00:00", doc.RootElement.GetProperty("lastPatchTimeUtc").GetString());
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
    public void DetectJson_MapsSpotifyRunningPostUpdateToBlockedExitCode()
    {
        var snapshot = Snapshot(
            spotifyInstalled: true,
            spicetifyInstalled: true,
            Component("post-spotify-update", "After Spotify update", "Close Spotify first", CliHealthSeverity.Warning, action: "Reapply"));

        var result = Run(new[] { "detect", "--json" }, _ => snapshot);

        Assert.Equal(20, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal("blocked", doc.RootElement.GetProperty("state").GetString());
        Assert.Equal(20, doc.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public void DetectJson_MapsSingleComponentInstallToPartial()
    {
        var snapshot = Snapshot(
            spotifyInstalled: true,
            spicetifyInstalled: false,
            Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready));

        var result = Run(new[] { "detect", "--json" }, _ => snapshot);

        Assert.Equal(11, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal("partial", doc.RootElement.GetProperty("state").GetString());
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

    [Fact]
    public void InstallDryRun_EmitsNdjsonPlanWithoutMutating()
    {
        var sample = Path.Combine(ResolveRepoRoot(), "samples", "minimal.json");

        var result = Run("install", "--dry-run", "--answer-file", sample, "--ndjson");

        Assert.Equal(0, result.ExitCode);
        var lines = result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 3);
        using var started = JsonDocument.Parse(lines[0]);
        using var step = JsonDocument.Parse(lines[1]);
        using var completed = JsonDocument.Parse(lines[^1]);
        AssertNdjsonRequiredFields(started.RootElement);
        AssertNdjsonRequiredFields(step.RootElement);
        AssertNdjsonRequiredFields(completed.RootElement);
        Assert.Equal("LS1001", started.RootElement.GetProperty("eventId").GetString());
        Assert.Equal("info", started.RootElement.GetProperty("level").GetString());
        Assert.Equal("lifecycle", started.RootElement.GetProperty("component").GetString());
        Assert.Equal("install", started.RootElement.GetProperty("verb").GetString());
        Assert.True(Guid.TryParse(started.RootElement.GetProperty("operationId").GetString(), out var operationId));
        Assert.Equal(operationId, Guid.Parse(step.RootElement.GetProperty("operationId").GetString()!));
        Assert.Equal("LS8001", step.RootElement.GetProperty("eventId").GetString());
        Assert.Equal("journal", step.RootElement.GetProperty("component").GetString());
        Assert.Equal("validate-answer-file", step.RootElement.GetProperty("payload").GetProperty("id").GetString());
        Assert.Equal("LS1002", completed.RootElement.GetProperty("eventId").GetString());
        Assert.Equal("success", completed.RootElement.GetProperty("level").GetString());
        Assert.Equal(0, completed.RootElement.GetProperty("exitCode").GetInt32());
        Assert.True(completed.RootElement.GetProperty("payload").GetProperty("stepCount").GetInt32() >= 3);
    }

    [Fact]
    public void SilentSlashAlias_IsAcceptedForDryRun()
    {
        var sample = Path.Combine(ResolveRepoRoot(), "samples", "minimal.json");

        var result = Run("install", "/S", "--dry-run", "--answer-file", sample, "--ndjson");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"eventId\":\"LS1001\"", result.Stdout);
    }

    [Fact]
    public void JsonAndNdjsonConflict_IsRejectedBeforeVerbExecution()
    {
        var sample = Path.Combine(ResolveRepoRoot(), "samples", "minimal.json");

        var result = Run("install", "--dry-run", "--answer-file", sample, "--json", "--ndjson");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--json and --ndjson cannot be used together", result.Stderr);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public void PlanJson_EmitsSingleDryRunDocument()
    {
        var sample = Path.Combine(ResolveRepoRoot(), "samples", "minimal.json");

        var result = Run("plan", "--answer-file", sample, "--json");

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal("install", doc.RootElement.GetProperty("operation").GetString());
        Assert.True(doc.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("mutates").GetBoolean());
        Assert.Contains(
            doc.RootElement.GetProperty("steps").EnumerateArray(),
            step => step.GetProperty("id").GetString() == "run-backend-plan" &&
                    step.GetProperty("requiresAdmin").GetBoolean());
    }

    [Fact]
    public void InstallWithoutDryRun_IsRejected()
    {
        var sample = Path.Combine(ResolveRepoRoot(), "samples", "minimal.json");

        var result = Run("install", "--answer-file", sample, "--ndjson");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("only with --dry-run", result.Stderr);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Fact]
    public void FleetContract_ImplementedVerbsAreAcceptedByParser()
    {
        var repoRoot = ResolveRepoRoot();
        var sample = Path.Combine(repoRoot, "samples", "minimal.json");
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Combine(repoRoot, "schemas", "fleet-cli-contract.json")));

        foreach (var verb in contract.RootElement.GetProperty("verbs").EnumerateArray())
        {
            var status = verb.GetProperty("implementationStatus").GetString();
            if (status is not ("implemented" or "dry-run-only"))
            {
                continue;
            }

            var name = verb.GetProperty("verb").GetString()!;
            var result = Run(
                ArgsForImplementedVerb(name, sample),
                _ => Snapshot(
                    spotifyInstalled: true,
                    spicetifyInstalled: true,
                    Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready),
                    Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready)));

            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Unknown LibreSpot CLI verb", result.Stderr);
        }
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

    private static string[] ArgsForImplementedVerb(string verb, string sample) =>
        verb switch
        {
            "status" => new[] { "status", "--json" },
            "detect" => new[] { "detect", "--json" },
            "validate" => new[] { "validate", "--answer-file", sample, "--json" },
            "plan" => new[] { "plan", "--answer-file", sample, "--json" },
            "version" => new[] { "version", "--json" },
            "install" => new[] { "install", "--dry-run", "--answer-file", sample, "--ndjson" },
            "reapply" => new[] { "reapply", "--dry-run", "--answer-file", sample, "--ndjson" },
            "uninstall" => new[] { "uninstall", "--dry-run", "--ndjson" },
            _ => throw new InvalidOperationException($"No parser smoke args are defined for implemented verb '{verb}'.")
        };

    private static void AssertNdjsonRequiredFields(JsonElement line)
    {
        foreach (var field in new[] { "schemaVersion", "eventId", "timestamp", "level", "component", "message" })
        {
            Assert.True(line.TryGetProperty(field, out _), $"NDJSON line is missing '{field}'.");
        }
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
        string? action = null,
        DateTime? changed = null) =>
        new(
            id,
            name,
            status,
            severity,
            version,
            null,
            changed,
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
