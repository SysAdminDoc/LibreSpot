using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class SupportBundleServiceTests
{
    [Fact]
    public async Task ExportAsync_RedactsSensitiveContentAcrossLogsAndCrashes()
    {
        using var fixture = new SupportBundleFixture();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var slashProfile = profile.Replace('\\', '/');
        var machine = Environment.MachineName;
        fixture.WriteStackReadyState();
        fixture.WriteInstallLog(
            $"""
            Authorization: Bearer ghp_abcdefghijklmnopqrstuvwxyz123456
            x-github-request-id: raw-request-id
            HTTP_PROXY=http://proxyUser:proxyPass@example.test:8080
            command.exe --token topsecret --password othersecret
            log path: {profile}\repos\LibreSpot\src\Program.cs on {machine}
            slash path: {slashProfile}/repos/LibreSpot/src/Program.cs
            """);
        fixture.WriteRollingLog("GITHUB_TOKEN=ghp_abcdefghijklmnopqrstuvwxyz123456");
        fixture.WriteCrashReport(
            $"""
            System.InvalidOperationException: fail
               at LibreSpot.Program.Main() in {profile}\repos\LibreSpot\src\Program.cs:line 42
            Secret=plain-text-secret
            """);

        var result = await fixture.ExportAsync(new SupportBundleOptions(true, true, true));
        var text = string.Join("\n", ReadZipText(result.Path).Values);

        Assert.DoesNotContain(profile, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(slashProfile, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(machine, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ghp_abcdefghijklmnopqrstuvwxyz123456", text);
        Assert.DoesNotContain("raw-request-id", text);
        Assert.DoesNotContain("proxyUser:proxyPass", text);
        Assert.DoesNotContain("topsecret", text);
        Assert.DoesNotContain("plain-text-secret", text);
        Assert.Contains("<USERPROFILE>", text);
        Assert.Contains("<MACHINE>", text);
        Assert.Contains("<redacted", text);
    }

    [Fact]
    public async Task ExportAsync_RespectsOptionalSelection()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        fixture.WriteInstallLog("install journal");
        var journalTarget = Path.Combine(fixture.ConfigDirectory, "secret.txt");
        fixture.WriteOperationJournal($"{{\"operationId\":\"op-1\",\"target\":\"{journalTarget.Replace("\\", "\\\\")}\",\"result\":\"Removed\"}}");
        fixture.WriteRollingLog("desktop log");
        fixture.WriteCrashReport("crash log");

        var result = await fixture.ExportAsync(new SupportBundleOptions(
            IncludeOperationJournal: true,
            IncludeLogs: false,
            IncludeCrashReports: false));
        var entries = ReadZipText(result.Path);
        var escapedJournalTarget = journalTarget.Replace("\\", "\\\\");

        Assert.Contains("manifest.json", entries.Keys);
        Assert.Contains("health/health-report.json", entries.Keys);
        Assert.Contains("health/provenance.json", entries.Keys);
        Assert.Contains("health/runtime.json", entries.Keys);
        Assert.Contains("operation/latest-journal.txt", entries.Keys);
        Assert.Contains("Operation journal JSONL", entries["operation/latest-journal.txt"]);
        Assert.Contains("<LIBRESPOT_CONFIG>", entries["operation/latest-journal.txt"]);
        Assert.DoesNotContain(journalTarget, entries["operation/latest-journal.txt"]);
        Assert.DoesNotContain(escapedJournalTarget, entries["operation/latest-journal.txt"]);
        Assert.DoesNotContain(entries.Keys, name => name.StartsWith("logs/", StringComparison.Ordinal));
        Assert.DoesNotContain(entries.Keys, name => name.StartsWith("crashes/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportAsync_IncludesCurrentRunLogAndBackendMetadata()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        fixture.WriteOperationJournal("{\"operationId\":\"op-1\",\"result\":\"Failed\"}");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var currentRun = new SupportBundleRunContext(
            "Recommended setup",
            "Needs review",
            "Installing SpotX",
            "Error",
            "Install",
            "BackendHostStalled",
            $"Timed out while reading {profile}\\secret.txt --token super-secret",
            DateTimeOffset.Parse("2026-07-07T15:00:00Z"),
            DateTimeOffset.Parse("2026-07-07T15:01:00Z"),
            DateTimeOffset.Parse("2026-07-07T15:02:00Z"),
            new[]
            {
                $"[15:00:01] [ERROR] failed beside {profile}\\secret.txt",
                "[15:00:02] [WARN] retry unavailable"
            });

        var result = await fixture.ExportAsync(new SupportBundleOptions(
            IncludeOperationJournal: true,
            IncludeLogs: false,
            IncludeCrashReports: false,
            CurrentRun: currentRun));
        var entries = ReadZipText(result.Path);

        Assert.Contains("current-run/activity-log.txt", entries.Keys);
        Assert.Contains("current-run/backend-result.json", entries.Keys);
        Assert.Contains("operation/latest-journal.txt", entries.Keys);
        Assert.Contains("BackendHostStalled", entries["current-run/backend-result.json"]);
        Assert.Contains("\"backendAction\": \"Install\"", entries["current-run/backend-result.json"]);
        Assert.Contains("\"currentRun\"", entries["manifest.json"]);
        Assert.Contains("retry unavailable", entries["current-run/activity-log.txt"]);
        Assert.Contains("<USERPROFILE>", entries["current-run/activity-log.txt"]);
        Assert.DoesNotContain(profile, string.Join("\n", entries.Values), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret", string.Join("\n", entries.Values), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_OmitsInvalidUtf8DiagnosticWindows()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        fixture.WriteCrashReportBytes([0x48, 0x69, 0x20, 0xC3, 0x28]);

        var result = await fixture.ExportAsync(new SupportBundleOptions(
            IncludeOperationJournal: false,
            IncludeLogs: false,
            IncludeCrashReports: true));
        var entries = ReadZipText(result.Path);
        var crashWindow = Assert.Single(entries, entry => entry.Key.StartsWith("crashes/", StringComparison.Ordinal));

        Assert.Contains("<omitted: file is not UTF-8 text>", crashWindow.Value);
    }

    [Fact]
    public async Task ExportAsync_IncludesRuntimePinsAndNoNetworkUploadManifest()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();

        var result = await fixture.ExportAsync(new SupportBundleOptions());
        var entries = ReadZipText(result.Path);

        Assert.Contains("\"networkUpload\": \"none\"", entries["manifest.json"]);
        Assert.Contains(AppCatalog.PinnedSpotXVersion, entries["health/runtime.json"]);
        Assert.Contains(AppCatalog.PinnedSpotXCommit, entries["health/runtime.json"]);
        Assert.Contains(AppCatalog.PinnedSpicetifyCliVersion, entries["health/runtime.json"]);
        Assert.Contains(AppCatalog.PinnedMarketplaceVersion, entries["health/runtime.json"]);
    }

    [Fact]
    public async Task ExportAsync_DoesNotReuseFixedDestinationTempFile()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        var staleTemp = Path.Combine(fixture.Root, "support.zip.tmp");
        File.WriteAllText(staleTemp, "existing export temp from another process");

        var result = await fixture.ExportAsync(new SupportBundleOptions());

        Assert.True(File.Exists(result.Path));
        Assert.Equal("existing export temp from another process", File.ReadAllText(staleTemp));
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "support.zip.*.tmp"));
    }

    [Fact]
    public async Task ExportAsync_IncludesCustomPatchImportProvenanceWithoutRawSourceSecrets()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        fixture.WriteConfig(
            """
            {
              "ConfigSchemaVersion": 1,
              "SpotX_CustomPatchesEnabled": true,
              "SpotX_CustomPatchesJson": "{ \"xpui\": { \"match\": \"one\", \"replace\": \"two\" } }",
              "SpotX_CustomPatchesSourceUrl": "https://example.test/patches.json?token=topsecret",
              "SpotX_CustomPatchesFetchedAtUtc": "2026-06-30T12:34:56Z",
              "SpotX_CustomPatchesSourceByteCount": 54,
              "SpotX_CustomPatchesSourceSha256": "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"
            }
            """);

        var result = await fixture.ExportAsync(new SupportBundleOptions());
        var entries = ReadZipText(result.Path);
        var health = entries["health/health-report.json"];

        Assert.Contains("\"customPatchImport\"", health);
        Assert.Contains("\"sourceSha256\": \"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789\"", health);
        Assert.Contains("\"sourceByteCount\": 54", health);
        Assert.DoesNotContain("topsecret", health, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("redacted", health, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_IncludesCommunityAssetHealth()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        var checkedAt = DateTimeOffset.Parse("2026-06-30T12:00:00Z");
        var communityReport = new CommunityAssetDriftReport(
            new[]
            {
                new CommunityAssetState(
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
                    checkedAt,
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
            checkedAt);
        var upstreamReport = new UpstreamDriftReport(
            new[]
            {
                new UpstreamDependencyState(
                    "spotx",
                    "SpotX",
                    AppCatalog.PinnedSpotXCommit,
                    AppCatalog.PinnedSpotXCommit,
                    AppCatalog.PinnedSpotXCommit,
                    "current",
                    "git ls-remote",
                    checkedAt,
                    null,
                    false,
                    "Pinned SpotX metadata is current.")
                {
                    SourceUrl = "https://github.com/SpotX-Official/SpotX",
                    ReleaseNotesUrl = $"https://github.com/SpotX-Official/SpotX/compare/{AppCatalog.PinnedSpotXCommit}...main",
                    LastVerifiedAtUtc = DateTimeOffset.Parse("2026-07-08T00:00:00Z")
                }
            },
            checkedAt);

        var result = await fixture.ExportAsync(
            new SupportBundleOptions(),
            communityReport,
            upstreamReport);
        var entries = ReadZipText(result.Path);
        var health = entries["health/health-report.json"];
        var provenance = entries["health/provenance.json"];

        Assert.Contains("\"communityAssets\"", health);
        Assert.Contains("\"id\": \"extension:beautiful-lyrics.mjs\"", health);
        Assert.Contains("\"license\": \"NOASSERTION\"", health);
        Assert.Contains("\"networkBehavior\": \"third-party-service\"", health);
        Assert.Contains("\"requiresTrustReview\": true", health);
        Assert.Contains("\"upstreamDependencies\"", provenance);
        Assert.Contains("\"freshnessStatus\": \"current\"", provenance);
        Assert.Contains("\"lastVerifiedAtUtc\": \"2026-07-08T00:00:00+00:00\"", provenance);
        Assert.Contains("\"releaseNotesUrl\": \"https://github.com/SpotX-Official/SpotX/compare/", provenance);
    }

    [Fact]
    public async Task ExportAsync_IncludesMarketplaceVisibilityEvidence()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        fixture.WriteMarketplaceEvidence();

        var result = await fixture.ExportAsync(new SupportBundleOptions());
        var entries = ReadZipText(result.Path);
        var health = entries["health/health-report.json"];

        Assert.Contains("\"marketplaceVisibility\"", health);
        Assert.Contains("\"marketplaceLikelyVisible\": true", health);
        Assert.Contains("\"source\": \"RepairMarketplace\"", health);
        Assert.Contains("\"manifestVersion\": \"1.0.9\"", health);
        Assert.Contains("\"applySucceeded\": true", health);
        Assert.Contains("\"openUriSucceeded\": true", health);
        Assert.DoesNotContain(fixture.Root, health, StringComparison.OrdinalIgnoreCase);
        using var healthDocument = JsonDocument.Parse(health);
        var marketplacePath = healthDocument.RootElement
            .GetProperty("marketplaceVisibility")
            .GetProperty("marketplacePath")
            .GetString();
        Assert.Contains("<LIBRESPOT_CONFIG>", marketplacePath);
    }

    [Fact]
    public async Task ExportAsync_IncludesSpicetifyPreservationEvidence()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        fixture.WriteSpicetifyPreservationEvidence();

        var result = await fixture.ExportAsync(new SupportBundleOptions(
            IncludeOperationJournal: true,
            IncludeLogs: false,
            IncludeCrashReports: false));
        var entries = ReadZipText(result.Path);
        var operation = entries["operation/latest-journal.txt"];

        Assert.Contains("Spicetify preservation evidence", operation);
        Assert.Contains("PreservedAfterSuccess", operation);
        Assert.Contains("custom-app.txt", operation);
        Assert.DoesNotContain(fixture.Root, operation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_IncludesAssetCacheInventory()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        fixture.WriteAssetCacheEntry(
            "SpotX installer",
            "https://example.test/spotx-run.ps1?token=secret",
            new byte[] { 0x4c, 0x69, 0x62, 0x72, 0x65 });

        var result = await fixture.ExportAsync(new SupportBundleOptions());
        var entries = ReadZipText(result.Path);
        var health = entries["health/health-report.json"];

        Assert.Contains("\"assetCache\"", health);
        Assert.Contains("\"entryCount\": 1", health);
        Assert.Contains("\"presentCount\": 1", health);
        Assert.Contains("\"label\": \"SpotX installer\"", health);
        Assert.Contains("redacted", health, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", health, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.ConfigDirectory, health, StringComparison.OrdinalIgnoreCase);
        using var healthDocument = JsonDocument.Parse(health);
        var assetCache = healthDocument.RootElement.GetProperty("assetCache");
        Assert.Contains("<LIBRESPOT_CONFIG>", assetCache.GetProperty("cacheDirectory").GetString());
        Assert.Contains("<LIBRESPOT_CONFIG>", assetCache.GetProperty("entries")[0].GetProperty("path").GetString());
    }

    [Fact]
    public void CreatePreview_ReportsSelectionsEstimateAndRedactionRules()
    {
        using var fixture = new SupportBundleFixture();
        fixture.WriteStackReadyState();
        fixture.WriteInstallLog("install journal");
        fixture.WriteRollingLog("desktop log");
        fixture.WriteCrashReport("crash log");

        var preview = fixture.Service.CreatePreview(
            fixture.GetSnapshot(),
            new SupportBundleOptions(IncludeOperationJournal: true, IncludeLogs: false, IncludeCrashReports: true));

        Assert.True(preview.EstimatedBytes > 0);
        Assert.Contains(preview.Entries, entry => entry.Id == "health" && entry.IsRequired && entry.IsSelected);
        Assert.Contains(preview.Entries, entry => entry.Id == "logs" && !entry.IsSelected && entry.FileCount > 0);
        Assert.Contains(preview.Entries, entry => entry.Id == "crashes" && entry.IsSelected && entry.FileCount > 0);
        Assert.Contains(preview.RedactionRules, rule => rule.Contains("tokens", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, string> ReadZipText(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.ToDictionary(
            entry => entry.FullName,
            entry =>
            {
                using var reader = new StreamReader(entry.Open());
                return reader.ReadToEnd();
            },
            StringComparer.Ordinal);
    }

    private sealed class SupportBundleFixture : IDisposable
    {
        public SupportBundleFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
            ConfigDirectory = Path.Combine(Root, "LibreSpot");
            ConfigPath = Path.Combine(ConfigDirectory, "config.json");
            SpotifyPath = Path.Combine(Root, "Spotify", "Spotify.exe");
            SpicetifyPath = Path.Combine(Root, "spicetify", "spicetify.exe");
            SpicetifyConfigDirectory = Path.Combine(Root, "spicetify-config");
            BackupDirectory = Path.Combine(Root, "backups");
            RollingLogDirectory = Path.Combine(Root, "logs");
            CrashDirectory = Path.Combine(Root, "crashes");
            Directory.CreateDirectory(ConfigDirectory);
            Service = new SupportBundleService(ConfigDirectory, RollingLogDirectory, CrashDirectory);
        }

        public string Root { get; }
        public string ConfigDirectory { get; }
        public string ConfigPath { get; }
        public string SpotifyPath { get; }
        public string SpicetifyPath { get; }
        public string SpicetifyConfigDirectory { get; }
        public string BackupDirectory { get; }
        public string RollingLogDirectory { get; }
        public string CrashDirectory { get; }
        public SupportBundleService Service { get; }

        public Task<SupportBundleResult> ExportAsync(
            SupportBundleOptions options,
            CommunityAssetDriftReport? communityAssetDriftReport = null,
            UpstreamDriftReport? upstreamDriftReport = null) =>
            Service.ExportAsync(
                Path.Combine(Root, "support.zip"),
                GetSnapshot(communityAssetDriftReport, upstreamDriftReport),
                options);

        public EnvironmentSnapshot GetSnapshot(
            CommunityAssetDriftReport? communityAssetDriftReport = null,
            UpstreamDriftReport? upstreamDriftReport = null)
        {
            var service = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => true,
                spotifyPath: SpotifyPath,
                spicetifyPath: SpicetifyPath,
                spicetifyConfigDirectory: SpicetifyConfigDirectory,
                backupDirectory: BackupDirectory,
                rollingLogDirectory: RollingLogDirectory,
                crashDirectory: CrashDirectory,
                upstreamDriftProbe: () => upstreamDriftReport ?? UpstreamDriftReport.Empty,
                communityAssetDriftProbe: () => communityAssetDriftReport ?? CommunityAssetDriftReport.Empty);

            return service.GetSnapshot(ConfigPath);
        }

        public void WriteStackReadyState()
        {
            WriteFile(ConfigPath, "{}");
            WriteFile(SpotifyPath, "spotify");
            var appsDirectory = Path.Combine(Path.GetDirectoryName(SpotifyPath)!, "Apps");
            WriteFile(Path.Combine(appsDirectory, "xpui.spa"), "bundle");
            WriteFile(Path.Combine(appsDirectory, "xpui.spa.bak"), "backup");
            WriteFile(SpicetifyPath, "spicetify");
            WriteFile(Path.Combine(SpicetifyConfigDirectory, "config-xpui.ini"), "custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
            WriteFile(Path.Combine(SpicetifyConfigDirectory, "CustomApps", "marketplace", "extension.js"), "");
            WriteFile(Path.Combine(SpicetifyConfigDirectory, "CustomApps", "marketplace", "manifest.json"), "{\"version\":\"1.0.9\"}");
            WriteFile(Path.Combine(BackupDirectory, "20260616-100000", "config-xpui.ini"), "current_theme = SpicetifyDefault");
        }

        public void WriteMarketplaceEvidence() =>
            WriteFile(
                Path.Combine(ConfigDirectory, "marketplace-evidence.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        schemaVersion = 1,
                        generatedAtUtc = DateTimeOffset.Parse("2026-06-30T12:00:00Z"),
                        source = "RepairMarketplace",
                        filesPresent = true,
                        registered = true,
                        likelyVisible = true,
                        marketplaceStatus = "Ready",
                        marketplacePath = Path.Combine(ConfigDirectory, "spicetify", "CustomApps", "marketplace"),
                        manifestVersion = "1.0.9",
                        applyStage = "backup apply",
                        applySucceeded = true,
                        applyMessage = "Spicetify backup apply succeeded.",
                        applyCompletedAtUtc = DateTimeOffset.Parse("2026-06-30T12:01:00Z"),
                        openUriSucceeded = true,
                        openUriMessage = "spotify:app:marketplace was handed to Windows.",
                        openUriRequestedAtUtc = DateTimeOffset.Parse("2026-06-30T12:02:00Z"),
                        spotifyRunningAfterOpen = true,
                        lastObservedSpotifySession = "spotify-process-running",
                        lastObservedAtUtc = DateTimeOffset.Parse("2026-06-30T12:03:00Z")
                    }));

        public void WriteSpicetifyPreservationEvidence() =>
            WriteFile(
                Path.Combine(ConfigDirectory, "spicetify-preservation-latest.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        schemaVersion = 1,
                        operationId = "op-preserve",
                        action = "RepairMarketplace",
                        status = "PreservedAfterSuccess",
                        snapshotPath = Path.Combine(Root, "LibreSpot_Backups", "SpicetifyState", "op-preserve"),
                        restoredFiles = new[] { "CustomApps\\foreign-app\\custom-app.txt" },
                        skippedExistingFiles = new[] { "CustomApps\\marketplace\\extension.js" }
                    }));

        public string WriteAssetCacheEntry(string label, string sourceUrl, byte[] content)
        {
            var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            var cacheDirectory = Path.Combine(ConfigDirectory, "cache");
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllBytes(Path.Combine(cacheDirectory, hash), content);
            WriteFile(
                Path.Combine(cacheDirectory, "asset-cache-index.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        schemaVersion = 1,
                        generatedAtUtc = DateTimeOffset.Parse("2026-06-30T12:00:00Z"),
                        entries = new[]
                        {
                            new
                            {
                                sha256 = hash,
                                label,
                                sourceUrl,
                                byteSize = content.LongLength,
                                firstSeenAtUtc = DateTimeOffset.Parse("2026-06-30T11:00:00Z"),
                                lastUsedAtUtc = DateTimeOffset.Parse("2026-06-30T11:30:00Z"),
                                lastVerifiedAtUtc = DateTimeOffset.Parse("2026-06-30T12:00:00Z"),
                                status = "present"
                            }
                        }
                    }));

            return hash;
        }

        public void WriteConfig(string content) =>
            WriteFile(ConfigPath, content);

        public void WriteInstallLog(string content) =>
            WriteFile(Path.Combine(ConfigDirectory, "install.log"), content);

        public void WriteOperationJournal(string content) =>
            WriteFile(Path.Combine(ConfigDirectory, "operation-journal.jsonl"), content);

        public void WriteRollingLog(string content) =>
            WriteFile(Path.Combine(RollingLogDirectory, "librespot-20260616.log"), content);

        public void WriteCrashReport(string content) =>
            WriteFile(Path.Combine(CrashDirectory, "crash-20260616-test.log"), content);

        public void WriteCrashReportBytes(byte[] content)
        {
            var path = Path.Combine(CrashDirectory, "crash-20260616-test.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        private static void WriteFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
