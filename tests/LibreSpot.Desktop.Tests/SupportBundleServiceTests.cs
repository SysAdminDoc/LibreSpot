using System.IO;
using System.IO.Compression;
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
        var machine = Environment.MachineName;
        fixture.WriteStackReadyState();
        fixture.WriteInstallLog(
            $"""
            Authorization: Bearer ghp_abcdefghijklmnopqrstuvwxyz123456
            x-github-request-id: raw-request-id
            HTTP_PROXY=http://proxyUser:proxyPass@example.test:8080
            command.exe --token topsecret --password othersecret
            log path: {profile}\repos\LibreSpot\src\Program.cs on {machine}
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
        fixture.WriteRollingLog("desktop log");
        fixture.WriteCrashReport("crash log");

        var result = await fixture.ExportAsync(new SupportBundleOptions(
            IncludeOperationJournal: true,
            IncludeLogs: false,
            IncludeCrashReports: false));
        var entries = ReadZipText(result.Path);

        Assert.Contains("manifest.json", entries.Keys);
        Assert.Contains("health/health-report.json", entries.Keys);
        Assert.Contains("health/runtime.json", entries.Keys);
        Assert.Contains("operation/latest-journal.txt", entries.Keys);
        Assert.DoesNotContain(entries.Keys, name => name.StartsWith("logs/", StringComparison.Ordinal));
        Assert.DoesNotContain(entries.Keys, name => name.StartsWith("crashes/", StringComparison.Ordinal));
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

        public Task<SupportBundleResult> ExportAsync(SupportBundleOptions options) =>
            Service.ExportAsync(Path.Combine(Root, "support.zip"), GetSnapshot(), options);

        public EnvironmentSnapshot GetSnapshot()
        {
            var service = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => true,
                spotifyPath: SpotifyPath,
                spicetifyPath: SpicetifyPath,
                spicetifyConfigDirectory: SpicetifyConfigDirectory,
                backupDirectory: BackupDirectory,
                rollingLogDirectory: RollingLogDirectory,
                crashDirectory: CrashDirectory);

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
            WriteFile(Path.Combine(SpicetifyConfigDirectory, "CustomApps", "marketplace", "manifest.json"), "{}");
            WriteFile(Path.Combine(BackupDirectory, "20260616-100000", "config-xpui.ini"), "current_theme = SpicetifyDefault");
        }

        public void WriteInstallLog(string content) =>
            WriteFile(Path.Combine(ConfigDirectory, "install.log"), content);

        public void WriteRollingLog(string content) =>
            WriteFile(Path.Combine(RollingLogDirectory, "librespot-20260616.log"), content);

        public void WriteCrashReport(string content) =>
            WriteFile(Path.Combine(CrashDirectory, "crash-20260616-test.log"), content);

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
