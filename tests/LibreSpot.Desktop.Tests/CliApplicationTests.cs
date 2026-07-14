extern alias Cli;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Xunit;
using CliApp = Cli::LibreSpot.Cli.CliApplication;
using CliAssetCacheEntryState = Cli::LibreSpot.Desktop.Models.AssetCacheEntryState;
using CliAssetCacheInventoryReport = Cli::LibreSpot.Desktop.Models.AssetCacheInventoryReport;
using CliBackendMessage = Cli::LibreSpot.Desktop.Services.BackendMessage;
using CliBackendRunResult = Cli::LibreSpot.Desktop.Services.BackendRunResult;
using CliCommunityAssetDriftReport = Cli::LibreSpot.Desktop.Models.CommunityAssetDriftReport;
using CliCommunityAssetState = Cli::LibreSpot.Desktop.Models.CommunityAssetState;
using CliEnvironmentSnapshot = Cli::LibreSpot.Desktop.Models.EnvironmentSnapshot;
using CliHealthSeverity = Cli::LibreSpot.Desktop.Models.HealthSeverity;
using CliMarketplaceVisibilityEvidence = Cli::LibreSpot.Desktop.Models.MarketplaceVisibilityEvidence;
using CliStackHealthComponent = Cli::LibreSpot.Desktop.Models.StackHealthComponent;
using CliStackHealthReport = Cli::LibreSpot.Desktop.Models.StackHealthReport;
using CliUpstreamDependencyState = Cli::LibreSpot.Desktop.Models.UpstreamDependencyState;
using CliUpstreamDriftReport = Cli::LibreSpot.Desktop.Models.UpstreamDriftReport;

namespace LibreSpot.Desktop.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public void VersionCommand_WritesConsoleArtifactVersion()
    {
        var result = Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("LibreSpot.Cli 4.0.0-preview.17", result.Stdout.Trim());
        Assert.Equal(string.Empty, result.Stderr);
    }

    [Fact]
    public void VersionJson_EmitsDependencyPins()
    {
        var result = Run("version", "--json");

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("4.0.0-preview.17", doc.RootElement.GetProperty("productVersion").GetString());
        Assert.Equal("2.44.0", doc.RootElement.GetProperty("dependencies").GetProperty("spicetifyCli").GetProperty("version").GetString());
        Assert.Equal("1.0.9", doc.RootElement.GetProperty("dependencies").GetProperty("marketplaceVersion").GetString());
        Assert.StartsWith("550bc72", doc.RootElement.GetProperty("dependencies").GetProperty("spotX").GetProperty("commit").GetString());
    }

    [Fact]
    public void StatusJson_UsesHealthReportComponents()
    {
        var upstreamReport = new CliUpstreamDriftReport(
            new[]
            {
                new CliUpstreamDependencyState(
                    "spotx",
                    "SpotX",
                    "550bc72cd15f6e2a172a6ecc0873d0991eb1c83c",
                    "550bc72cd15f6e2a172a6ecc0873d0991eb1c83c",
                    "9fbbf88e0b8e79806b3bcc7c767b9b4bc20f9680",
                    "behind",
                    "git ls-remote",
                    DateTimeOffset.Parse("2026-06-27T12:30:00Z"),
                    TimeSpan.FromHours(2),
                    false,
                    "Pinned/current 550bc72c; latest 9fbbf88e; drift behind; source git ls-remote; cache age 2 hours.")
                {
                    SourceUrl = "https://github.com/SpotX-Official/SpotX",
                    ReleaseNotesUrl = "https://github.com/SpotX-Official/SpotX/compare/550bc72cd15f6e2a172a6ecc0873d0991eb1c83c...main",
                    LastVerifiedAtUtc = DateTimeOffset.Parse("2026-07-08T00:00:00Z")
                }
            },
            DateTimeOffset.Parse("2026-06-27T12:30:00Z"));
        var communityReport = new CliCommunityAssetDriftReport(
            new[]
            {
                new CliCommunityAssetState(
                    "extension:beautiful-lyrics.mjs",
                    "extension",
                    "Beautiful Lyrics",
                    "https://raw.githubusercontent.com/surfbryce/beautiful-lyrics/61ac582da092311e893423269ca7f09003108705/Extension/Builds/Release/beautiful-lyrics.mjs",
                    "https://github.com/surfbryce/beautiful-lyrics.git",
                    "refs/heads/main",
                    "61ac582da092311e893423269ca7f09003108705",
                    "93c9ecfcb0a83c832c5ee7ca8fe826bcfaeec7cdd129c0bf05bab84b8ba6ba72",
                    "61ac582da092311e893423269ca7f09003108705",
                    "current",
                    "git ls-remote",
                    DateTimeOffset.Parse("2026-06-27T12:31:00Z"),
                    null,
                    false,
                    "NOASSERTION",
                    "active",
                    "skip-with-warning",
                    "third-party-service",
                    "Fetches lyrics from a third-party lyrics backend.",
                    true,
                    "Pinned/current community metadata; network third-party-service; trust review required.")
                {
                    ReleaseNotesUrl = "https://github.com/surfbryce/beautiful-lyrics/compare/61ac582da092311e893423269ca7f09003108705...main",
                    LastVerifiedAtUtc = DateTimeOffset.Parse("2026-06-15T00:00:00Z")
                }
            },
            DateTimeOffset.Parse("2026-06-27T12:31:00Z"));
        var assetCacheReport = new CliAssetCacheInventoryReport(
            new[]
            {
                new CliAssetCacheEntryState(
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    "SpotX installer",
                    "https://example.test/spotx-run.ps1",
                    4096,
                    DateTimeOffset.Parse("2026-06-27T11:00:00Z"),
                    DateTimeOffset.Parse("2026-06-27T11:30:00Z"),
                    DateTimeOffset.Parse("2026-06-27T12:00:00Z"),
                    "present",
                    "C:\\Users\\Test\\AppData\\Roaming\\LibreSpot\\cache\\0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    true,
                    "Hash-named cache file exists and matches the expected SHA256.")
            },
            "C:\\Users\\Test\\AppData\\Roaming\\LibreSpot\\cache",
            "C:\\Users\\Test\\AppData\\Roaming\\LibreSpot\\cache\\asset-cache-index.json",
            DateTimeOffset.Parse("2026-06-27T12:00:00Z"));
        var snapshot = SnapshotWithReports(
            true,
            true,
            upstreamReport,
            communityReport,
            new CliMarketplaceVisibilityEvidence(
                1,
                DateTimeOffset.Parse("2026-06-27T12:32:00Z"),
                "RepairMarketplace",
                true,
                true,
                true,
                "Ready",
                "C:\\Users\\Test\\AppData\\Roaming\\spicetify\\CustomApps\\marketplace",
                "1.0.9",
                "backup apply",
                true,
                "Spicetify backup apply succeeded.",
                DateTimeOffset.Parse("2026-06-27T12:33:00Z"),
                true,
                "spotify:app:marketplace was handed to Windows.",
                DateTimeOffset.Parse("2026-06-27T12:34:00Z"),
                true,
                "spotify-process-running",
                DateTimeOffset.Parse("2026-06-27T12:35:00Z")),
            assetCacheReport,
            Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready, version: "1.2.93"),
            Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready, version: "2.44.0"),
            Component("backups", "Backups", "2 backups", CliHealthSeverity.Ready),
            Component("auto-reapply-watcher", "Auto-reapply watcher", "UpToDate", CliHealthSeverity.Ready),
            Component("post-spotify-update", "After Spotify update", "No drift", CliHealthSeverity.Ready, changed: DateTime.Parse("2026-06-27T12:00:00Z").ToUniversalTime()),
            Component("upstream-spotx", "SpotX upstream", "Upstream changed", CliHealthSeverity.Info, version: "9fbbf88e0b8e79806b3bcc7c767b9b4bc20f9680"));

        var result = Run(
            new[] { "status", "--json", "--config-path", "C:\\LibreSpot\\config.json" },
            _ => snapshot);

        Assert.Equal(0, result.ExitCode);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal(3, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("Stack ready", doc.RootElement.GetProperty("statusTitle").GetString());
        Assert.Equal("C:\\LibreSpot\\config.json", doc.RootElement.GetProperty("configPath").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("backupCount").GetInt32());
        Assert.Equal("UpToDate", doc.RootElement.GetProperty("lastWatcherOutcome").GetString());
        Assert.Equal("2026-06-27T12:00:00+00:00", doc.RootElement.GetProperty("lastPatchTimeUtc").GetString());
        Assert.Equal("spotify", doc.RootElement.GetProperty("components")[0].GetProperty("id").GetString());
        Assert.Equal("1.2.93", doc.RootElement.GetProperty("components")[0].GetProperty("detectedVersion").GetString());
        Assert.Equal("unmodified", doc.RootElement.GetProperty("patcherOwnership").GetProperty("ownership").GetString());
        Assert.False(doc.RootElement.GetProperty("patcherOwnership").GetProperty("hasForeignState").GetBoolean());
        var upstream = doc.RootElement.GetProperty("components")
            .EnumerateArray()
            .Single(component => component.GetProperty("id").GetString() == "upstream-spotx");
        Assert.Equal("Upstream changed", upstream.GetProperty("status").GetString());
        Assert.Equal("9fbbf88e0b8e79806b3bcc7c767b9b4bc20f9680", upstream.GetProperty("detectedVersion").GetString());
        var upstreamDependency = doc.RootElement.GetProperty("upstreamDependencies")[0];
        Assert.Equal("spotx", upstreamDependency.GetProperty("id").GetString());
        Assert.Equal("550bc72cd15f6e2a172a6ecc0873d0991eb1c83c", upstreamDependency.GetProperty("pinnedValue").GetString());
        Assert.Equal("9fbbf88e0b8e79806b3bcc7c767b9b4bc20f9680", upstreamDependency.GetProperty("latestValue").GetString());
        Assert.Equal("behind", upstreamDependency.GetProperty("driftState").GetString());
        Assert.Equal("git ls-remote", upstreamDependency.GetProperty("metadataSource").GetString());
        Assert.Equal(7200, upstreamDependency.GetProperty("cacheAgeSeconds").GetDouble());
        Assert.Equal("https://github.com/SpotX-Official/SpotX", upstreamDependency.GetProperty("sourceUrl").GetString());
        Assert.Contains("/compare/", upstreamDependency.GetProperty("releaseNotesUrl").GetString());
        Assert.Equal("2026-07-08T00:00:00+00:00", upstreamDependency.GetProperty("lastVerifiedAtUtc").GetString());
        Assert.Equal("stale", upstreamDependency.GetProperty("freshnessStatus").GetString());
        var communityAsset = doc.RootElement.GetProperty("communityAssets")[0];
        Assert.Equal("extension:beautiful-lyrics.mjs", communityAsset.GetProperty("id").GetString());
        Assert.Equal("NOASSERTION", communityAsset.GetProperty("license").GetString());
        Assert.Equal("third-party-service", communityAsset.GetProperty("networkBehavior").GetString());
        Assert.True(communityAsset.GetProperty("requiresTrustReview").GetBoolean());
        Assert.Contains("/compare/", communityAsset.GetProperty("releaseNotesUrl").GetString());
        Assert.Equal("2026-06-15T00:00:00+00:00", communityAsset.GetProperty("lastVerifiedAtUtc").GetString());
        Assert.Equal("current", communityAsset.GetProperty("freshnessStatus").GetString());
        Assert.True(doc.RootElement.GetProperty("marketplaceLikelyVisible").GetBoolean());
        var marketplaceVisibility = doc.RootElement.GetProperty("marketplaceVisibility");
        Assert.Equal("RepairMarketplace", marketplaceVisibility.GetProperty("source").GetString());
        Assert.Equal("1.0.9", marketplaceVisibility.GetProperty("manifestVersion").GetString());
        Assert.True(marketplaceVisibility.GetProperty("applySucceeded").GetBoolean());
        Assert.True(marketplaceVisibility.GetProperty("openUriSucceeded").GetBoolean());
        var assetCache = doc.RootElement.GetProperty("assetCache");
        Assert.Equal(1, assetCache.GetProperty("entryCount").GetInt32());
        Assert.Equal(1, assetCache.GetProperty("presentCount").GetInt32());
        Assert.Equal(4096, assetCache.GetProperty("totalBytes").GetInt64());
        Assert.Equal("SpotX installer", assetCache.GetProperty("entries")[0].GetProperty("label").GetString());
        Assert.Equal("present", assetCache.GetProperty("entries")[0].GetProperty("status").GetString());
    }

    [Fact]
    public void StatusScopeMachine_UsesProgramDataConfigPath()
    {
        string? observedConfigPath = null;
        var result = Run(
            new[] { "status", "--json", "--scope", "machine" },
            path =>
            {
                observedConfigPath = path;
                return Snapshot(
                    spotifyInstalled: true,
                    spicetifyInstalled: true,
                    Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready));
            });

        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LibreSpot",
            "config.json");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expectedPath, observedConfigPath);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal(expectedPath, doc.RootElement.GetProperty("configPath").GetString());
    }

    [Fact]
    public void StatusInvalidScope_IsRejectedBeforeSnapshot()
    {
        var snapshotRead = false;
        var result = Run(
            new[] { "status", "--scope", "tenant" },
            _ =>
            {
                snapshotRead = true;
                return Snapshot(spotifyInstalled: true, spicetifyInstalled: true);
            });

        Assert.Equal(2, result.ExitCode);
        Assert.False(snapshotRead);
        Assert.Contains("--scope must be user or machine", result.Stderr);
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
    public void DetectIntuneJson_EmitsOnlyJsonWithIntuneExitCode()
    {
        var snapshot = Snapshot(
            spotifyInstalled: true,
            spicetifyInstalled: true,
            Component("marketplace", "Marketplace", "Files missing", CliHealthSeverity.Warning, action: "RepairMarketplace"));

        var result = Run(new[] { "detect", "--intune", "--json" }, _ => snapshot);

        Assert.Equal(11, result.ExitCode);
        Assert.Equal(string.Empty, result.Stderr);
        using var doc = JsonDocument.Parse(result.Stdout);
        Assert.Equal("drifted", doc.RootElement.GetProperty("state").GetString());
        Assert.Equal(11, doc.RootElement.GetProperty("exitCode").GetInt32());
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
    public void ValidateAnswerFile_RejectsOversizedInputBeforeParsing()
    {
        var answerFile = Path.Combine(Path.GetTempPath(), "librespot-answer-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllBytes(answerFile, new byte[(1024 * 1024) + 1]);
        try
        {
            var result = Run("validate", "--answer-file", answerFile, "--json");

            Assert.Equal(2, result.ExitCode);
            using var doc = JsonDocument.Parse(result.Stdout);
            var error = Assert.Single(doc.RootElement.GetProperty("errors").EnumerateArray());
            Assert.Contains("maximum is 1048576 bytes", error.GetProperty("message").GetString());
        }
        finally
        {
            File.Delete(answerFile);
        }
    }

    [Fact]
    public void ValidateAnswerFile_RejectsSchemaValuesTheCliWouldOtherwiseNormalize()
    {
        var answerFile = Path.Combine(Path.GetTempPath(), "librespot-answer-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(
            answerFile,
            """
            {
              "schemaVersion": 1,
              "installMode": "recommended",
              "uiCulture": "de",
              "spotx": {
                "cacheLimit": 90000,
                "lyricsTheme": "not-a-theme"
              },
              "spicetify": {
                "extensions": ["fullAppDisplay.js", "unknown.js", "fullAppDisplay.js"]
              },
              "profiles": {
                "strict": {
                  "spotx": {
                    "downloadMethod": "bits"
                  }
                }
              },
              "eulaAccepted": true,
              "riskAcknowledged": true
            }
            """);

        try
        {
            var result = Run("validate", "--answer-file", answerFile, "--json");

            Assert.Equal(2, result.ExitCode);
            using var doc = JsonDocument.Parse(result.Stdout);
            Assert.False(doc.RootElement.GetProperty("valid").GetBoolean());
            var errors = doc.RootElement.GetProperty("errors").EnumerateArray()
                .Select(error => $"{error.GetProperty("path").GetString()} {error.GetProperty("message").GetString()}")
                .ToArray();
            Assert.Contains(errors, error => error.Contains("$.uiCulture", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("$.spotx.cacheLimit", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("$.spotx.lyricsTheme", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("$.spicetify.extensions[1]", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("$.spicetify.extensions[2]", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("$.profiles.strict.spotx.downloadMethod", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(answerFile);
        }
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
        Assert.True(Guid.TryParse(doc.RootElement.GetProperty("operationId").GetString(), out _));
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
                Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready, version: "1.2.93"),
                Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready, version: "2.44.0"));

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

    [Fact]
    public void Undo_RequiresExplicitConfirmationAfterMachineReadablePreview()
    {
        using var fixture = new CliUndoFixture();

        var preview = Run(
            "undo", "--operation-id", fixture.OperationId, "--token-kind", "pathEntryAdd",
            "--dry-run", "--json", "--config-path", fixture.ConfigPath);

        Assert.Equal(0, preview.ExitCode);
        Assert.Equal(string.Empty, preview.Stderr);
        using (var document = JsonDocument.Parse(preview.Stdout))
        {
            Assert.True(document.RootElement.GetProperty("allowed").GetBoolean());
            Assert.True(document.RootElement.GetProperty("alreadyUndone").GetBoolean());
            Assert.Equal("ready", document.RootElement.GetProperty("status").GetString());
        }

        var unconfirmed = Run(
            "undo", "--operation-id", fixture.OperationId, "--token-kind", "pathEntryAdd",
            "--config-path", fixture.ConfigPath);

        Assert.Equal(2, unconfirmed.ExitCode);
        Assert.Contains("requires --yes", unconfirmed.Stderr);
        Assert.Equal(string.Empty, unconfirmed.Stdout);
    }

    [Fact]
    public void Undo_AlreadyRestoredPathIsIdempotentAcrossSeparateCliRuns()
    {
        using var fixture = new CliUndoFixture();
        var before = CliUndoFixture.ReadUserPath();

        var first = Run(
            "undo", "--operation-id", fixture.OperationId, "--token-kind", "pathEntryAdd",
            "--yes", "--json", "--config-path", fixture.ConfigPath);
        var second = Run(
            "undo", "--operation-id", fixture.OperationId, "--token-kind", "pathEntryAdd",
            "--yes", "--json", "--config-path", fixture.ConfigPath);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(string.Empty, first.Stderr);
        Assert.Equal(string.Empty, second.Stderr);
        using var firstDocument = JsonDocument.Parse(first.Stdout);
        using var secondDocument = JsonDocument.Parse(second.Stdout);
        Assert.Equal("alreadyUndone", firstDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("alreadyUndone", secondDocument.RootElement.GetProperty("status").GetString());
        Assert.NotEqual(
            firstDocument.RootElement.GetProperty("undoOperationId").GetString(),
            secondDocument.RootElement.GetProperty("undoOperationId").GetString());
        Assert.Equal(before, CliUndoFixture.ReadUserPath());
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
              "uiCulture": "pt-BR",
              "spotifyTarget": { "version": "1.2.90.451" },
              "spotx": {
                "premium": true,
                "podcastsOff": false,
                "cacheLimit": 2048,
                "lyricsTheme": "github",
                "customPatchesEnabled": true,
                "customPatchesJson": "{ \"xpui\": { \"match\": \"one\", \"replace\": \"two\" } }"
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
            Assert.Equal("pt-BR", config.RootElement.GetProperty("UiCulture").GetString());
            Assert.False(config.RootElement.GetProperty("LaunchAfter").GetBoolean());
            Assert.True(config.RootElement.GetProperty("RiskAcknowledged").GetBoolean());
            Assert.True(config.RootElement.GetProperty("SpotX_Premium").GetBoolean());
            Assert.False(config.RootElement.GetProperty("SpotX_PodcastsOff").GetBoolean());
            Assert.Equal(2048, config.RootElement.GetProperty("SpotX_CacheLimit").GetInt32());
            Assert.Equal("github", config.RootElement.GetProperty("SpotX_LyricsTheme").GetString());
            Assert.True(config.RootElement.GetProperty("SpotX_CustomPatchesEnabled").GetBoolean());
            Assert.Equal("{ \"xpui\": { \"match\": \"one\", \"replace\": \"two\" } }", config.RootElement.GetProperty("SpotX_CustomPatchesJson").GetString());
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
    public void InstallInvalidCustomPatchJson_IsRejectedBeforeBackendRuns()
    {
        var answerFile = Path.Combine(Path.GetTempPath(), "librespot-answer-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(
            answerFile,
            """
            {
              "schemaVersion": 1,
              "installMode": "custom",
              "spotx": {
                "customPatchesEnabled": true,
                "customPatchesJson": "{ not valid json"
              },
              "eulaAccepted": true,
              "riskAcknowledged": true
            }
            """);

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
            Assert.Contains("customPatchesJson", result.Stdout);
            Assert.Contains("not valid JSON", result.Stdout);
        }
        finally
        {
            File.Delete(answerFile);
        }
    }

    [Fact]
    public void InstallBackendFailure_PreservesDocumentedBackendExitCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
        var sample = Path.Combine(ResolveRepoRoot(), "samples", "minimal.json");
        var configPath = Path.Combine(root, "config.json");
        var logDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(root);
        try
        {
            var result = Run(
                new[] { "install", "--answer-file", sample, "--config-path", configPath, "--log-dir", logDir, "--ndjson" },
                _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
                (_, _, _, _) => Task.FromResult(new CliBackendRunResult(false, "installer busy", ExitCode: 1618)));

            Assert.Equal(1618, result.ExitCode);
            Assert.Contains("installer busy", result.Stderr);
            var lines = result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            using var failed = JsonDocument.Parse(lines[^1]);
            Assert.Equal("LS1003", failed.RootElement.GetProperty("eventId").GetString());
            Assert.Equal(1618, failed.RootElement.GetProperty("exitCode").GetInt32());
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
    public void InstallBackendSoftReboot_ReturnsSoftRebootExitCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
        var sample = Path.Combine(ResolveRepoRoot(), "samples", "minimal.json");
        var configPath = Path.Combine(root, "config.json");
        var logDir = Path.Combine(root, "logs");
        Directory.CreateDirectory(root);
        try
        {
            var result = Run(
                new[] { "install", "--answer-file", sample, "--config-path", configPath, "--log-dir", logDir, "--ndjson" },
                _ => Snapshot(spotifyInstalled: true, spicetifyInstalled: true),
                (_, _, _, _) => Task.FromResult(new CliBackendRunResult(true, ExitCode: 3010)));

            Assert.Equal(3010, result.ExitCode);
            Assert.Equal(string.Empty, result.Stderr);
            var lines = result.Stdout.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            using var completed = JsonDocument.Parse(lines[^1]);
            Assert.Equal("LS1002", completed.RootElement.GetProperty("eventId").GetString());
            Assert.Equal(3010, completed.RootElement.GetProperty("exitCode").GetInt32());
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
                else if (!string.IsNullOrWhiteSpace(cleanupPath) && Directory.Exists(cleanupPath))
                {
                    Directory.Delete(cleanupPath, recursive: true);
                }
            }
        }
    }

    [Fact]
    public void Readme_FleetDeploymentExamplesUseImplementedCommands()
    {
        var readme = File.ReadAllText(Path.Combine(ResolveRepoRoot(), "README.md"));

        foreach (var command in new[]
                 {
                     "LibreSpot.Cli.exe detect --intune",
                     "LibreSpot.Cli.exe install --answer-file .\\librespot-answer.json --profile standard --silent --yes --no-restart --ndjson",
                     "LibreSpot.Cli.exe repair --repair-id RepairMarketplace --silent --yes --ndjson",
                     "LibreSpot.Cli.exe reapply --answer-file C:\\ProgramData\\LibreSpot\\librespot-answer.json --profile standard --silent --yes --no-restart --ndjson",
                     "LibreSpot.Cli.exe detect --json",
                     "LibreSpot.Cli.exe uninstall --silent --yes --keep-spotify --ndjson"
                 })
        {
            Assert.Contains(command, readme);
        }

        Assert.Contains("| `0` | Success or compliant |", readme);
        Assert.Contains("| `12` | Repair needed |", readme);
        Assert.Contains("%ProgramData%\\LibreSpot\\logs", readme);
    }

    [Fact]
    public void DeploymentSampleScriptsUseImplementedCliCommands()
    {
        var repoRoot = ResolveRepoRoot();
        var answerFile = Path.Combine(repoRoot, "samples", "deployment", "librespot-answer.json");
        var tempRoot = Path.Combine(Path.GetTempPath(), "LibreSpot.DeploymentSamples", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var examples = ReadDeploymentCliExamples(repoRoot).ToArray();

            Assert.Contains(examples, example => example.Contains("detect --intune", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(examples, example => example.Contains("install --answer-file", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(examples, example => example.Contains("repair --repair-id RepairMarketplace", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(examples, example => example.Contains("uninstall --silent --yes --keep-spotify", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(examples, example => example.Contains("reapply --answer-file", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(examples, example => example.Contains("detect --json", StringComparison.OrdinalIgnoreCase));
            var winrmSample = File.ReadAllText(Path.Combine(repoRoot, "samples", "deployment", "winrm-reapply-standard.ps1"));
            Assert.Contains("$LASTEXITCODE", winrmSample);
            Assert.Contains("exit [int]$exitCode", winrmSample);

            foreach (var example in examples)
            {
                var tokens = TokenizeCommand(example);
                Assert.NotEmpty(tokens);
                Assert.Equal("LibreSpot.Cli.exe", tokens[0]);

                var safeArgs = ToSafeDeploymentSmokeArgs(tokens.Skip(1).ToArray(), answerFile, tempRoot);
                var result = Run(
                    safeArgs,
                    _ => Snapshot(
                        spotifyInstalled: true,
                        spicetifyInstalled: true,
                        Component("spotify", "Spotify", "Detected", CliHealthSeverity.Ready),
                        Component("spicetify-cli", "Spicetify CLI", "Detected", CliHealthSeverity.Ready)),
                    (_, _, _, _) => Task.FromResult(new CliBackendRunResult(true)));

                Assert.True(
                    result.ExitCode == 0,
                    $"{example} failed with exit {result.ExitCode}. stdout: {result.Stdout} stderr: {result.Stderr}");
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
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
            "undo" => UndoSmokeArgs(),
            "watcher install" => (new[] { "watcher", "install", "--silent" }, null),
            "watcher remove" => (new[] { "watcher", "remove", "--silent" }, null),
            _ => throw new InvalidOperationException($"No parser smoke args are defined for implemented verb '{verb}'.")
        };
    }

    private static (string[] Args, string? CleanupPath) UndoSmokeArgs()
    {
        var fixture = new CliUndoFixture();
        return (
            new[]
            {
                "undo", "--operation-id", fixture.OperationId, "--token-kind", "pathEntryAdd",
                "--dry-run", "--json", "--config-path", fixture.ConfigPath
            },
            fixture.Root);
    }

    private static void AssertNdjsonRequiredFields(JsonElement line)
    {
        foreach (var field in new[] { "schemaVersion", "eventId", "timestamp", "level", "component", "message" })
        {
            Assert.True(line.TryGetProperty(field, out _), $"NDJSON line is missing '{field}'.");
        }
    }

    private static IEnumerable<string> ReadDeploymentCliExamples(string repoRoot)
    {
        var sampleRoot = Path.Combine(repoRoot, "samples", "deployment");
        foreach (var file in Directory.EnumerateFiles(sampleRoot, "*.ps1").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("# CLI:", StringComparison.OrdinalIgnoreCase))
                {
                    yield return trimmed["# CLI:".Length..].Trim();
                }
            }
        }
    }

    private static string[] TokenizeCommand(string command) =>
        Regex.Matches(command, @"(?:""(?<quoted>[^""]*)""|'(?<single>[^']*)'|(?<bare>\S+))")
            .Select(match =>
                match.Groups["quoted"].Success ? match.Groups["quoted"].Value :
                match.Groups["single"].Success ? match.Groups["single"].Value :
                match.Groups["bare"].Value)
            .ToArray();

    private static string[] ToSafeDeploymentSmokeArgs(string[] args, string answerFile, string tempRoot)
    {
        var safe = args
            .Select(arg => arg.EndsWith("librespot-answer.json", StringComparison.OrdinalIgnoreCase)
                ? answerFile
                : arg)
            .ToList();

        for (var i = 0; i < safe.Count - 1; i++)
        {
            if (string.Equals(safe[i], "--log-dir", StringComparison.OrdinalIgnoreCase))
            {
                safe[i + 1] = Path.Combine(tempRoot, "logs");
            }
            else if (string.Equals(safe[i], "--output", StringComparison.OrdinalIgnoreCase))
            {
                safe[i + 1] = Path.Combine(tempRoot, "support.zip");
            }
        }

        if (safe.Count > 0 &&
            safe[0] is "install" or "reapply" or "repair" or "uninstall" &&
            !safe.Any(arg => string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase)))
        {
            safe.Insert(1, "--dry-run");
        }

        return safe.ToArray();
    }

    private static CliEnvironmentSnapshot Snapshot(
        bool spotifyInstalled,
        bool spicetifyInstalled,
        params CliStackHealthComponent[] components) =>
        SnapshotWithUpstream(spotifyInstalled, spicetifyInstalled, CliUpstreamDriftReport.Empty, components);

    private sealed class CliUndoFixture : IDisposable
    {
        private readonly string _root;

        public CliUndoFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "LibreSpot.Cli.Tests", Guid.NewGuid().ToString("N"));
            var stateRoot = Path.Combine(_root, "undo-states");
            Directory.CreateDirectory(stateRoot);
            ConfigPath = Path.Combine(_root, "config.json");
            OperationId = Guid.NewGuid().ToString();
            var current = ReadUserPath();
            Assert.True(current.Kind is RegistryValueKind.String or RegistryValueKind.ExpandString);
            var hash = Hash(current.Value);
            var statePath = Path.Combine(stateRoot, $"{OperationId}-path-entry-add.json");
            File.WriteAllText(
                statePath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 2,
                    operationId = OperationId,
                    tokenKind = "pathEntryAdd",
                    scope = "User",
                    target = "User PATH",
                    entry = "C:\\LibreSpot\\bin",
                    previousValueExists = current.Exists,
                    previousValue = current.Value,
                    previousValueKind = current.Kind.ToString(),
                    expectedValueExists = true,
                    expectedValue = current.Value,
                    expectedValueKind = RegistryValueKind.ExpandString.ToString(),
                    previousSha256 = hash,
                    expectedSha256 = hash,
                    createdAtUtc = DateTimeOffset.UtcNow
                }));
            File.WriteAllText(
                Path.Combine(_root, "run-receipt.latest.json"),
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    receiptId = Guid.NewGuid(),
                    runId = Guid.Parse(OperationId),
                    operationId = Guid.Parse(OperationId),
                    startedAt = DateTimeOffset.UtcNow,
                    completedAt = DateTimeOffset.UtcNow,
                    action = "Install",
                    status = "success",
                    undoAvailable = true,
                    operations = new[]
                    {
                        new
                        {
                            tokenKind = "pathEntryAdd",
                            target = "User PATH",
                            previousStateRef = statePath,
                            newState = $"sha256:{hash}",
                            result = "applied",
                            reversible = true,
                            undoAction = "Restore the exact previous user PATH snapshot.",
                            risk = "low"
                        }
                    }
                }));
        }

        public string ConfigPath { get; }
        public string OperationId { get; }
        public string Root => _root;

        public static (bool Exists, string Value, RegistryValueKind Kind) ReadUserPath()
        {
            using var key = Registry.CurrentUser.OpenSubKey("Environment", writable: false);
            var exists = key?.GetValueNames().Any(name => string.Equals(name, "Path", StringComparison.OrdinalIgnoreCase)) == true;
            return !exists || key is null
                ? (false, string.Empty, RegistryValueKind.String)
                : (true, key.GetValue("Path", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty, key.GetValueKind("Path"));
        }

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    private static CliEnvironmentSnapshot SnapshotWithUpstream(
        bool spotifyInstalled,
        bool spicetifyInstalled,
        CliUpstreamDriftReport upstreamDriftReport,
        params CliStackHealthComponent[] components) =>
        SnapshotWithReports(spotifyInstalled, spicetifyInstalled, upstreamDriftReport, CliCommunityAssetDriftReport.Empty, null, components);

    private static CliEnvironmentSnapshot SnapshotWithReports(
        bool spotifyInstalled,
        bool spicetifyInstalled,
        CliUpstreamDriftReport upstreamDriftReport,
        CliCommunityAssetDriftReport communityAssetDriftReport,
        CliMarketplaceVisibilityEvidence? marketplaceVisibilityEvidence,
        params CliStackHealthComponent[] components) =>
        SnapshotWithReports(
            spotifyInstalled,
            spicetifyInstalled,
            upstreamDriftReport,
            communityAssetDriftReport,
            marketplaceVisibilityEvidence,
            CliAssetCacheInventoryReport.Empty,
            components);

    private static CliEnvironmentSnapshot SnapshotWithReports(
        bool spotifyInstalled,
        bool spicetifyInstalled,
        CliUpstreamDriftReport upstreamDriftReport,
        CliCommunityAssetDriftReport communityAssetDriftReport,
        CliMarketplaceVisibilityEvidence? marketplaceVisibilityEvidence,
        CliAssetCacheInventoryReport assetCacheInventory,
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
            HealthReport = new CliStackHealthReport(components),
            UpstreamDriftReport = upstreamDriftReport,
            CommunityAssetDriftReport = communityAssetDriftReport,
            MarketplaceVisibilityEvidence = marketplaceVisibilityEvidence,
            AssetCacheInventory = assetCacheInventory
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
