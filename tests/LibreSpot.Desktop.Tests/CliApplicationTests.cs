extern alias Cli;

using System.IO.Compression;
using System.Text.Json;
using Xunit;
using CliApp = Cli::LibreSpot.Cli.CliApplication;
using CliBackendMessage = Cli::LibreSpot.Desktop.Services.BackendMessage;
using CliBackendRunResult = Cli::LibreSpot.Desktop.Services.BackendRunResult;
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
    public void ValidateAnswerFile_RejectsMissingRequestedProfile()
    {
        var sample = Path.Combine(ResolveRepoRoot(), "samples", "minimal.json");

        var result = Run("validate", "--answer-file", sample, "--profile", "missing", "--json");

        Assert.Equal(2, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
        Assert.Contains("Profile 'missing'", doc.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
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
    public void ExportSupport_WritesLocalRedactedZip()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "support.zip");
        try
        {
            var snapshot = Snapshot(
                spotifyInstalled: true,
                spicetifyInstalled: true,
                Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready, version: "1.2.92"),
                Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready, version: "2.43.2"));

            var result = Run(new[] { "export-support", "--output", output }, _ => snapshot);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.Stderr);
            Assert.Contains(output, result.Stdout);
            Assert.True(File.Exists(output));

            using var archive = ZipFile.OpenRead(output);
            var entries = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
            Assert.Contains("manifest.json", entries);
            Assert.Contains("health/health-report.json", entries);
            Assert.Contains("health/runtime.json", entries);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportSupport_RejectsJsonOutputMode()
    {
        var result = Run("export-support", "--json");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("unsupported flag", result.Stderr);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Theory]
    [InlineData("install", "EnableAutoReapply", "Auto-reapply watcher installed.")]
    [InlineData("remove", "DisableAutoReapply", "Auto-reapply watcher removed.")]
    public void WatcherVerbs_RunMappedBackendActions(string subverb, string expectedAction, string expectedMessage)
    {
        var actions = new List<string>();
        var result = Run(
            new[] { "watcher", subverb },
            _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
            (action, _, onMessage, _) =>
            {
                actions.Add(action);
                onMessage(new CliBackendMessage("status", "INFO", "backend status"));
                return Task.FromResult(new CliBackendRunResult(true));
            });

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { expectedAction }, actions);
        Assert.Contains("backend status", result.Stdout);
        Assert.Contains(expectedMessage, result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void WatcherSilent_SuppressesSuccessOutput()
    {
        var result = Run(
            new[] { "watcher", "install", "--silent" },
            _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
            (_, _, onMessage, _) =>
            {
                onMessage(new CliBackendMessage("status", "INFO", "hidden status"));
                return Task.FromResult(new CliBackendRunResult(true));
            });

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void WatcherUnsupportedJson_IsRejectedBeforeBackendRuns()
    {
        var backendRan = false;
        var result = Run(
            new[] { "watcher", "install", "--json" },
            _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
            (_, _, _, _) =>
            {
                backendRan = true;
                return Task.FromResult(new CliBackendRunResult(true));
            });

        Assert.Equal(2, result.ExitCode);
        Assert.False(backendRan);
        Assert.Contains("unsupported flag", result.Stderr);
        Assert.Equal(string.Empty, result.Stdout);
    }

    [Theory]
    [InlineData("install", "Install")]
    [InlineData("reapply", "Reapply")]
    public void MutatingInstallAndReapply_RunBackendAfterPersistingAnswerFile(string verb, string expectedAction)
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
        var answerFile = Path.Combine(root, "answer.json");
        var configPath = Path.Combine(root, "config.json");
        var logDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(root);
        File.WriteAllText(
            answerFile,
            """
            {
              "schemaVersion": 1,
              "installMode": "custom",
              "spotifyTarget": { "version": "1.2.90.451" },
              "spotx": {
                "premium": true,
                "podcastsOff": false,
                "cacheLimit": 2048,
                "lyricsTheme": "github"
              },
              "spicetify": {
                "theme": "Dribbblish",
                "scheme": "nord-dark",
                "extensions": ["fullAppDisplay.js", "shuffle+.js"],
                "marketplace": true
              },
              "watcher": { "enabled": true },
              "eulaAccepted": true,
              "riskAcknowledged": true
            }
            """);

        try
        {
            var actions = new List<string>();
            var configPaths = new List<string>();
            var result = Run(
                new[] { verb, "--answer-file", answerFile, "--config-path", configPath, "--log-dir", logDir, "--no-restart", "--ndjson" },
                _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
                (action, path, onMessage, _) =>
                {
                    actions.Add(action);
                    configPaths.Add(path);
                    onMessage(new CliBackendMessage("step", "INFO", "backend step"));
                    return Task.FromResult(new CliBackendRunResult(true));
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(new[] { expectedAction }, actions);
            Assert.Equal(new[] { configPath }, configPaths);
            Assert.Equal(string.Empty, result.Stderr);
            Assert.Contains("\"eventId\":\"LS1001\"", result.Stdout);
            Assert.Contains("\"eventId\":\"LS9001\"", result.Stdout);
            Assert.Contains("\"eventId\":\"LS1002\"", result.Stdout);
            var logFile = Assert.Single(Directory.EnumerateFiles(logDir, "librespot-*.ndjson"));
            Assert.Contains("\"eventId\":\"LS9001\"", File.ReadAllText(logFile));

            using var config = JsonDocument.Parse(File.ReadAllText(configPath));
            Assert.Equal("Custom", config.RootElement.GetProperty("Mode").GetString());
            Assert.False(config.RootElement.GetProperty("LaunchAfter").GetBoolean());
            Assert.True(config.RootElement.GetProperty("RiskAcknowledged").GetBoolean());
            Assert.True(config.RootElement.GetProperty("SpotX_Premium").GetBoolean());
            Assert.False(config.RootElement.GetProperty("SpotX_PodcastsOff").GetBoolean());
            Assert.Equal(2048, config.RootElement.GetProperty("SpotX_CacheLimit").GetInt32());
            Assert.Equal("github", config.RootElement.GetProperty("SpotX_LyricsTheme").GetString());
            Assert.Equal("1.2.90.451", config.RootElement.GetProperty("SpotX_SpotifyVersionId").GetString());
            Assert.Equal("Dribbblish", config.RootElement.GetProperty("Spicetify_Theme").GetString());
            Assert.Equal("nord-dark", config.RootElement.GetProperty("Spicetify_Scheme").GetString());
            Assert.True(config.RootElement.GetProperty("AutoReapply_Enabled").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void MutatingInstall_UsesSelectedAnswerFileProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
        var answerFile = Path.Combine(root, "answer.json");
        var configPath = Path.Combine(root, "config.json");
        var logDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(root);
        File.WriteAllText(
            answerFile,
            """
            {
              "schemaVersion": 1,
              "installMode": "recommended",
              "spotx": { "premium": false },
              "eulaAccepted": true,
              "riskAcknowledged": true,
              "profiles": {
                "visual": {
                  "installMode": "custom",
                  "spotx": {
                    "premium": true,
                    "lyricsTheme": "lavender"
                  },
                  "spicetify": {
                    "theme": "Dribbblish",
                    "scheme": "catppuccin-mocha",
                    "extensions": ["fullAppDisplay.js"]
                  }
                }
              }
            }
            """);

        try
        {
            var result = Run(
                new[] { "install", "--answer-file", answerFile, "--profile", "visual", "--config-path", configPath, "--log-dir", logDir, "--ndjson" },
                _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
                (action, _, _, _) => Task.FromResult(new CliBackendRunResult(action == "Install")));

            Assert.Equal(0, result.ExitCode);
            using var config = JsonDocument.Parse(File.ReadAllText(configPath));
            Assert.Equal("Custom", config.RootElement.GetProperty("Mode").GetString());
            Assert.True(config.RootElement.GetProperty("SpotX_Premium").GetBoolean());
            Assert.Equal("lavender", config.RootElement.GetProperty("SpotX_LyricsTheme").GetString());
            Assert.Equal("Dribbblish", config.RootElement.GetProperty("Spicetify_Theme").GetString());
            Assert.Equal("catppuccin-mocha", config.RootElement.GetProperty("Spicetify_Scheme").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void InstallInvalidAnswerFile_IsRejectedBeforeBackendRuns()
    {
        var answerFile = Path.Combine(Path.GetTempPath(), "librespot-answer-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(answerFile, "{\"schemaVersion\":1,\"eulaAccepted\":true}");
        try
        {
            var backendRan = false;
            var result = Run(
                new[] { "install", "--answer-file", answerFile, "--ndjson" },
                _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
                (_, _, _, _) =>
                {
                    backendRan = true;
                    return Task.FromResult(new CliBackendRunResult(true));
                });

            Assert.Equal(2, result.ExitCode);
            Assert.False(backendRan);
            Assert.Contains("riskAcknowledged", result.Stdout);
        }
        finally
        {
            File.Delete(answerFile);
        }
    }

    [Fact]
    public void UninstallSilentPurge_RunsSpicetifyCleanupAndSelfDataRemoval()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(root, "config.json");
        var logDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(root);
        try
        {
            var actions = new List<string>();
            var result = Run(
                new[] { "uninstall", "--silent", "--yes", "--purge", "--keep-spotify", "--config-path", configPath, "--log-dir", logDir, "--ndjson" },
                _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
                (action, _, onMessage, _) =>
                {
                    actions.Add(action);
                    onMessage(new CliBackendMessage("status", "INFO", $"{action} status"));
                    return Task.FromResult(new CliBackendRunResult(true));
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(new[] { "UninstallSpicetify", "RemoveSelfData" }, actions);
            Assert.DoesNotContain("FullReset", actions);
            Assert.Contains("\"eventId\":\"LS1002\"", result.Stdout);
            Assert.Equal(string.Empty, result.Stderr);
            Assert.Single(Directory.EnumerateFiles(logDir, "librespot-*.ndjson"));

            using var config = JsonDocument.Parse(File.ReadAllText(configPath));
            Assert.True(config.RootElement.GetProperty("RiskAcknowledged").GetBoolean());
            Assert.False(config.RootElement.GetProperty("LaunchAfter").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void UninstallWithoutConsent_IsRejectedBeforeBackendRuns()
    {
        var backendRan = false;
        var result = Run(
            new[] { "uninstall" },
            _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
            (_, _, _, _) =>
            {
                backendRan = true;
                return Task.FromResult(new CliBackendRunResult(true));
            });

        Assert.Equal(2, result.ExitCode);
        Assert.False(backendRan);
        Assert.Contains("--yes or --silent", result.Stderr);
    }

    [Fact]
    public void RepairSilent_RunsMappedBackendActionAndPersistsConsent()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(root, "config.json");
        var logDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(root);
        try
        {
            var actions = new List<string>();
            var result = Run(
                new[] { "repair", "--repair-id", "RepairMarketplace", "--silent", "--yes", "--config-path", configPath, "--log-dir", logDir, "--ndjson" },
                _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
                (action, _, onMessage, _) =>
                {
                    actions.Add(action);
                    onMessage(new CliBackendMessage("step", "INFO", "repair step"));
                    return Task.FromResult(new CliBackendRunResult(true));
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(new[] { "RepairMarketplace" }, actions);
            Assert.Contains("\"eventId\":\"LS1002\"", result.Stdout);
            Assert.Equal(string.Empty, result.Stderr);
            Assert.Single(Directory.EnumerateFiles(logDir, "librespot-*.ndjson"));

            using var config = JsonDocument.Parse(File.ReadAllText(configPath));
            Assert.True(config.RootElement.GetProperty("RiskAcknowledged").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void RepairWatcherId_MapsToEnableAutoReapply()
    {
        var actions = new List<string>();
        var result = Run(
            new[] { "repair", "--repair-id", "WatchAutoReapply", "--silent", "--yes" },
            _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
            (action, _, _, _) =>
            {
                actions.Add(action);
                return Task.FromResult(new CliBackendRunResult(true));
            });

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "EnableAutoReapply" }, actions);
    }

    [Fact]
    public void RepairOpenLogs_IsRejectedBeforeBackendRuns()
    {
        var backendRan = false;
        var result = Run(
            new[] { "repair", "--repair-id", "OpenLogs", "--silent", "--yes" },
            _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
            (_, _, _, _) =>
            {
                backendRan = true;
                return Task.FromResult(new CliBackendRunResult(true));
            });

        Assert.Equal(2, result.ExitCode);
        Assert.False(backendRan);
        Assert.Contains("informational only", result.Stderr);
    }

    [Fact]
    public void RepairInstall_RequiresAnswerFileBeforeBackendRuns()
    {
        var backendRan = false;
        var result = Run(
            new[] { "repair", "--repair-id", "Install", "--silent", "--yes" },
            _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
            (_, _, _, _) =>
            {
                backendRan = true;
                return Task.FromResult(new CliBackendRunResult(true));
            });

        Assert.Equal(2, result.ExitCode);
        Assert.False(backendRan);
        Assert.Contains("--answer-file", result.Stderr);
    }

    [Fact]
    public void NdjsonLogRotation_PrunesOldFleetLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
        var logDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(logDir);
        try
        {
            for (var i = 0; i < 25; i++)
            {
                var path = Path.Combine(logDir, $"librespot-old-{i:D2}.ndjson");
                File.WriteAllText(path, "{}");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-100 - i));
            }

            var result = Run(
                new[] { "repair", "--repair-id", "RepairMarketplace", "--dry-run", "--ndjson", "--log-dir", logDir },
                _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true));

            Assert.Equal(0, result.ExitCode);
            var logs = Directory.EnumerateFiles(logDir, "librespot-*.ndjson").ToArray();
            Assert.Equal(20, logs.Length);
            Assert.Contains(logs, path => File.ReadAllText(path).Contains("\"eventId\":\"LS1002\""));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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
            var (args, cleanupPath) = ArgsForImplementedVerb(name, sample);
            try
            {
                var result = Run(
                    args,
                    _ => Snapshot(
                        spotifyInstalled: true,
                        spicetifyInstalled: true,
                        Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready),
                        Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready)),
                    (_, _, _, _) => Task.FromResult(new CliBackendRunResult(true)));

                Assert.Equal(0, result.ExitCode);
                Assert.DoesNotContain("Unknown LibreSpot CLI verb", result.Stderr);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(cleanupPath) && File.Exists(cleanupPath))
                {
                    File.Delete(cleanupPath);
                }
            }
        }
    }

    private static CliRunResult Run(params string[] args) =>
        Run(args, _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true));

    private static CliRunResult Run(
        string[] args,
        Func<string, CliEnvironmentSnapshot> snapshotFactory,
        Func<string, string, Action<CliBackendMessage>, CancellationToken, Task<CliBackendRunResult>>? backendRunner = null)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliApp.Run(args, stdout, stderr, snapshotFactory, backendRunner);
        return new CliRunResult(exitCode, stdout.ToString(), stderr.ToString());
    }

    private static (string[] Args, string? CleanupPath) ArgsForImplementedVerb(string verb, string sample)
    {
        var supportBundlePath = Path.Combine(
            Path.GetTempPath(),
            "LibreSpot.Cli.Tests",
            $"support-{Guid.NewGuid():N}.zip");

        return verb switch
        {
            "status" => (new[] { "status", "--json" }, null),
            "detect" => (new[] { "detect", "--json" }, null),
            "validate" => (new[] { "validate", "--answer-file", sample, "--json" }, null),
            "plan" => (new[] { "plan", "--answer-file", sample, "--json" }, null),
            "version" => (new[] { "version", "--json" }, null),
            "install" => (new[] { "install", "--dry-run", "--answer-file", sample, "--ndjson" }, null),
            "reapply" => (new[] { "reapply", "--dry-run", "--answer-file", sample, "--ndjson" }, null),
            "uninstall" => (new[] { "uninstall", "--dry-run", "--ndjson" }, null),
            "repair" => (new[] { "repair", "--repair-id", "RepairMarketplace", "--dry-run", "--ndjson" }, null),
            "export-support" => (new[] { "export-support", "--output", supportBundlePath }, supportBundlePath),
            "watcher install" => (new[] { "watcher", "install", "--silent" }, null),
            "watcher remove" => (new[] { "watcher", "remove", "--silent" }, null),
            _ => throw new InvalidOperationException($"No parser smoke args are defined for implemented verb '{verb}'.")
        };
    }

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
