using System.IO;
using System.Text.Json;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class EnvironmentSnapshotServiceTests
{
    [Fact]
    public void GetSnapshot_UsesDirectoryFromSuppliedConfigPath()
    {
        var configDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(configDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(configDirectory);
            var service = new EnvironmentSnapshotService();

            var snapshotBeforeSave = service.GetSnapshot(configPath);
            Assert.True(snapshotBeforeSave.ConfigFolderExists);
            Assert.False(snapshotBeforeSave.SavedConfigExists);

            File.WriteAllText(configPath, "{}");

            var snapshotAfterSave = service.GetSnapshot(configPath);
            Assert.True(snapshotAfterSave.ConfigFolderExists);
            Assert.True(snapshotAfterSave.SavedConfigExists);
        }
        finally
        {
            if (Directory.Exists(configDirectory))
            {
                Directory.Delete(configDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GetSnapshot_ReportsAutoReapplyTaskProbeState()
    {
        var configDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(configDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(configDirectory);
            var service = new EnvironmentSnapshotService(autoReapplyTaskProbe: () => true);

            var snapshot = service.GetSnapshot(configPath);

            Assert.True(snapshot.AutoReapplyTaskRegistered);
        }
        finally
        {
            if (Directory.Exists(configDirectory))
            {
                Directory.Delete(configDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetSnapshotAsync_MatchesSyncResult()
    {
        var configDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(configDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(configPath, "{}");

            var service = new EnvironmentSnapshotService(autoReapplyTaskProbe: () => true);

            // GetSnapshotAsync offloads the blocking schtasks probe to the thread
            // pool (so the UI dispatcher is never blocked) and must return the
            // same result as the synchronous path.
            var asyncSnapshot = await service.GetSnapshotAsync(configPath);
            var syncSnapshot = service.GetSnapshot(configPath);

            Assert.Equal(syncSnapshot.AutoReapplyTaskRegistered, asyncSnapshot.AutoReapplyTaskRegistered);
            Assert.Equal(syncSnapshot.SavedConfigExists, asyncSnapshot.SavedConfigExists);
            Assert.Equal(syncSnapshot.ConfigFolderExists, asyncSnapshot.ConfigFolderExists);
            Assert.True(asyncSnapshot.AutoReapplyTaskRegistered);
            Assert.True(asyncSnapshot.SavedConfigExists);
        }
        finally
        {
            if (Directory.Exists(configDirectory))
            {
                Directory.Delete(configDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GetSnapshotAsync_OffloadsToThreadPoolViaTaskRun()
    {
        // Lock in the structural guarantee: the async wrapper must use Task.Run so
        // the 1500ms schtasks probe never executes on the caller's (UI) thread.
        var source = File.ReadAllText(Path.Combine(
            ResolveRepoRoot(), "src", "LibreSpot.Desktop", "Services", "EnvironmentSnapshotService.cs"));
        Assert.Matches(@"GetSnapshotAsync\([^)]*\)\s*=>\s*\r?\n?\s*Task\.Run", source);
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }

    [Fact]
    public void GetSnapshot_ReportsMarketplaceReadyWhenFilesAndConfigRegistrationExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var appConfigDirectory = Path.Combine(root, "LibreSpot");
        var spicetifyConfigDirectory = Path.Combine(root, "spicetify-config");
        var spicetifyExe = Path.Combine(root, "spicetify", "spicetify.exe");
        var marketplaceDirectory = Path.Combine(spicetifyConfigDirectory, "CustomApps", "marketplace");
        var configPath = Path.Combine(appConfigDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(appConfigDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(spicetifyExe)!);
            Directory.CreateDirectory(marketplaceDirectory);
            File.WriteAllText(spicetifyExe, "");
            File.WriteAllText(Path.Combine(marketplaceDirectory, "extension.js"), "");
            File.WriteAllText(Path.Combine(marketplaceDirectory, "manifest.json"), "{}");
            File.WriteAllText(Path.Combine(spicetifyConfigDirectory, "config-xpui.ini"), "custom_apps = history | marketplace");

            var service = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => false,
                spicetifyPath: spicetifyExe,
                spicetifyConfigDirectory: spicetifyConfigDirectory);

            var snapshot = service.GetSnapshot(configPath);

            Assert.True(snapshot.SpicetifyInstalled);
            Assert.True(snapshot.MarketplaceFilesPresent);
            Assert.True(snapshot.MarketplaceRegistered);
            Assert.True(snapshot.MarketplaceReady);
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
    public void GetSnapshot_ReportsHiddenMarketplaceWhenFilesExistWithoutRegistration()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var appConfigDirectory = Path.Combine(root, "LibreSpot");
        var spicetifyConfigDirectory = Path.Combine(root, "spicetify-config");
        var spicetifyExe = Path.Combine(root, "spicetify", "spicetify.exe");
        var marketplaceDirectory = Path.Combine(spicetifyConfigDirectory, "CustomApps", "marketplace");
        var configPath = Path.Combine(appConfigDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(appConfigDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(spicetifyExe)!);
            Directory.CreateDirectory(marketplaceDirectory);
            File.WriteAllText(spicetifyExe, "");
            File.WriteAllText(Path.Combine(marketplaceDirectory, "extension.js"), "");
            File.WriteAllText(Path.Combine(marketplaceDirectory, "manifest.json"), "{}");
            File.WriteAllText(Path.Combine(spicetifyConfigDirectory, "config-xpui.ini"), "custom_apps = history");

            var service = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => false,
                spicetifyPath: spicetifyExe,
                spicetifyConfigDirectory: spicetifyConfigDirectory);

            var snapshot = service.GetSnapshot(configPath);

            Assert.True(snapshot.MarketplaceFilesPresent);
            Assert.False(snapshot.MarketplaceRegistered);
            Assert.False(snapshot.MarketplaceReady);
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
    public void GetSnapshot_HealthReport_CoversAllReadyState()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSavedConfig();
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = Catppuccin\r\ninject_css = 1\r\nreplace_colors = 1");
        fixture.WriteMarketplaceFiles();
        fixture.WriteBackup();
        fixture.WriteWatcherState(DateTime.Now.AddHours(-1), "UpToDate");
        fixture.WriteInstallLog();

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        Assert.Equal("Stack ready", snapshot.HealthReport.StatusTitle);
        Assert.Empty(snapshot.HealthReport.CriticalIssues);
        Assert.Empty(snapshot.HealthReport.WarningIssues);
        Assert.Contains(snapshot.HealthReport.Components, component => component.Id == "spotify" && component.Severity == HealthSeverity.Ready);
        Assert.Contains(snapshot.HealthReport.Components, component => component.Id == "spotx" && component.Status == "Verified");
        Assert.Contains(snapshot.HealthReport.Components, component => component.Id == "marketplace" && component.Status == "Ready");
        Assert.Contains(snapshot.HealthReport.Components, component => component.Id == "active-theme" && component.Status == "Active");
    }

    [Fact]
    public void GetSnapshot_HealthReport_CoversCleanSlate()
    {
        using var fixture = new SnapshotFixture();

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        Assert.Equal("Clean slate", snapshot.HealthReport.StatusTitle);
        Assert.Contains(snapshot.HealthReport.InfoIssues, component => component.Id == "spotify" && component.Status == "Not installed");
        Assert.Contains(snapshot.HealthReport.InfoIssues, component => component.Id == "spicetify-cli" && component.Status == "Not installed");
        Assert.Contains(snapshot.HealthReport.InfoIssues, component => component.Id == "spotx" && component.Status == "Not checked");
    }

    [Fact]
    public void GetSnapshot_HealthReport_CoversPartialInstall()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpotify(withSpotXMarkers: false);

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        Assert.Equal("Needs repair", snapshot.HealthReport.StatusTitle);
        Assert.Contains(snapshot.HealthReport.CriticalIssues, component => component.Id == "spotx" && component.Status == "Bundle missing");
        Assert.Contains(snapshot.HealthReport.InfoIssues, component => component.Id == "spicetify-cli" && component.Status == "Not installed");
    }

    [Fact]
    public void GetSnapshot_HealthReport_CoversMarketplaceFilesMissing()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var marketplace = Assert.Single(snapshot.HealthReport.WarningIssues, component => component.Id == "marketplace");
        Assert.Equal("Files missing", marketplace.Status);
        Assert.Contains("RepairMarketplace", marketplace.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_HealthReport_CoversThemeInjectionMismatch()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = Catppuccin\r\ninject_css = 0\r\nreplace_colors = 1");
        fixture.WriteMarketplaceFiles();

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var theme = Assert.Single(snapshot.HealthReport.WarningIssues, component => component.Id == "active-theme");
        Assert.Equal("Injection disabled", theme.Status);
        Assert.Contains("Reapply", theme.RecommendedActionIds);
        Assert.Contains("SafeMode", theme.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_HealthReport_CoversMissingBackup()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
        fixture.WriteMarketplaceFiles();

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var backups = Assert.Single(snapshot.HealthReport.WarningIssues, component => component.Id == "backups");
        Assert.Equal("None yet", backups.Status);
        Assert.Contains("CreateBackup", backups.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_HealthReport_CoversStaleWatcher()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteWatcherState(DateTime.Now.AddDays(-10), "UpToDate");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        var watcher = Assert.Single(snapshot.HealthReport.WarningIssues, component => component.Id == "auto-reapply-watcher");
        Assert.Equal("Stale", watcher.Status);
        Assert.Contains("WatchAutoReapply", watcher.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_HealthReport_CoversRecentCrashReports()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteRecentCrashReport();

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var crash = Assert.Single(snapshot.HealthReport.WarningIssues, component => component.Id == "crash-reports");
        Assert.Equal("1 recent crash", crash.Status);
    }

    [Fact]
    public void GetSnapshot_PostUpdateTriage_CoversWatcherReappliedCurrentSpotify()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.93", SpicetifyVersion = "2.43.2" };
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
        fixture.WriteMarketplaceFiles();
        fixture.WriteWatcherState(
            DateTime.Now.AddMinutes(-10),
            "Reapplied",
            lastKnownVersion: "1.2.93",
            lastAppliedSpotifyVersion: "1.2.93",
            lastSuccessfulApplyAt: DateTime.Now.AddMinutes(-10),
            lastApplyOutcome: "WatcherReapplied");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        var triage = Assert.Single(snapshot.HealthReport.Components, component => component.Id == "post-spotify-update");
        Assert.Equal("Reapplied", triage.Status);
        Assert.Equal(HealthSeverity.Ready, triage.Severity);
        Assert.DoesNotContain(snapshot.HealthReport.WarningIssues, component => component.Id == "post-spotify-update");
    }

    [Fact]
    public void GetSnapshot_PostUpdateTriage_CoversWatcherSkippedBecauseSpotifyWasRunning()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.93", SpotifyRunning = true };
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
        fixture.WriteMarketplaceFiles();
        fixture.WriteWatcherState(
            DateTime.Now.AddMinutes(-5),
            "DeferredSpotifyRunning",
            lastKnownVersion: "1.2.92",
            lastAppliedSpotifyVersion: "1.2.92");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        var triage = Assert.Single(snapshot.HealthReport.WarningIssues, component => component.Id == "post-spotify-update");
        Assert.Equal("Close Spotify first", triage.Status);
        Assert.Contains("Reapply", triage.RecommendedActionIds);
        Assert.Contains("OpenLogs", triage.RecommendedActionIds);
        Assert.DoesNotContain("FullReset", triage.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_PostUpdateTriage_CoversWatcherFailedDuringSpotX()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.93" };
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
        fixture.WriteMarketplaceFiles();
        fixture.WriteWatcherState(
            DateTime.Now.AddMinutes(-5),
            "Error: SpotX patch failed with code 1",
            lastKnownVersion: "1.2.92",
            lastAppliedSpotifyVersion: "1.2.92",
            lastApplyOutcome: "WatcherFailed",
            lastApplyError: "SpotX patch failed with code 1");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        var triage = Assert.Single(snapshot.HealthReport.CriticalIssues, component => component.Id == "post-spotify-update");
        Assert.Equal("SpotX reapply failed", triage.Status);
        Assert.Contains("Reapply", triage.RecommendedActionIds);
        Assert.Contains("OpenLogs", triage.RecommendedActionIds);
        Assert.DoesNotContain("FullReset", triage.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_PostUpdateTriage_CoversSpicetifyApplyRolledBack()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.93", SpicetifyVersion = "2.43.2" };
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = Catppuccin\r\ninject_css = 1\r\nreplace_colors = 1");
        fixture.WriteMarketplaceFiles();
        fixture.WriteWatcherState(
            DateTime.Now.AddMinutes(-5),
            "UpToDate",
            lastKnownVersion: "1.2.92",
            lastAppliedSpotifyVersion: "1.2.92",
            lastApplyAt: DateTime.Now.AddMinutes(-5),
            lastApplyOutcome: "SpicetifyApplyRolledBack",
            lastApplyError: "Spicetify apply failed but LibreSpot restored Spotify to a usable state.");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        var triage = Assert.Single(snapshot.HealthReport.WarningIssues, component => component.Id == "post-spotify-update");
        Assert.Equal("Spicetify rolled back", triage.Status);
        Assert.Contains("Reapply", triage.RecommendedActionIds);
        Assert.Contains("RestoreVanilla", triage.RecommendedActionIds);
        Assert.Contains("OpenLogs", triage.RecommendedActionIds);
        Assert.DoesNotContain("FullReset", triage.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_PostUpdateTriage_CoversMarketplaceStillMissingAfterReapply()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.93", SpicetifyVersion = "2.43.2" };
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
        fixture.WriteWatcherState(
            DateTime.Now.AddMinutes(-5),
            "Reapplied",
            lastKnownVersion: "1.2.93",
            lastAppliedSpotifyVersion: "1.2.93",
            lastSuccessfulApplyAt: DateTime.Now.AddMinutes(-5),
            lastApplyOutcome: "WatcherReapplied");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        var triage = Assert.Single(snapshot.HealthReport.WarningIssues, component => component.Id == "post-spotify-update");
        Assert.Equal("Marketplace still missing", triage.Status);
        Assert.Contains("RepairMarketplace", triage.RecommendedActionIds);
        Assert.Contains("OpenLogs", triage.RecommendedActionIds);
        Assert.DoesNotContain("FullReset", triage.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_RecordsHostAndProcessArchitecture()
    {
        using var fixture = new SnapshotFixture();

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        Assert.NotEqual("Unknown", snapshot.HostArchitecture);
        Assert.NotEqual("Unknown", snapshot.ProcessArchitecture);
        Assert.Contains(snapshot.HostArchitecture, new[] { "X64", "X86", "Arm64", "Arm" });
        Assert.Contains(snapshot.ProcessArchitecture, new[] { "X64", "X86", "Arm64", "Arm" });
    }

    private sealed class SnapshotFixture : IDisposable
    {
        public SnapshotFixture()
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
        }

        public string? SpotifyVersion { get; init; }
        public string? SpicetifyVersion { get; init; }
        public bool SpotifyRunning { get; init; }

        public string Root { get; }
        public string ConfigDirectory { get; }
        public string ConfigPath { get; }
        public string SpotifyPath { get; }
        public string SpicetifyPath { get; }
        public string SpicetifyConfigDirectory { get; }
        public string BackupDirectory { get; }
        public string RollingLogDirectory { get; }
        public string CrashDirectory { get; }

        public EnvironmentSnapshot GetSnapshot(bool autoReapplyRegistered)
        {
            var service = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => autoReapplyRegistered,
                spotifyPath: SpotifyPath,
                spicetifyPath: SpicetifyPath,
                spicetifyConfigDirectory: SpicetifyConfigDirectory,
                backupDirectory: BackupDirectory,
                rollingLogDirectory: RollingLogDirectory,
                crashDirectory: CrashDirectory,
                spotifyVersionProbe: () => SpotifyVersion,
                spicetifyVersionProbe: () => SpicetifyVersion,
                spotifyRunningProbe: () => SpotifyRunning);

            return service.GetSnapshot(ConfigPath);
        }

        public void WriteSavedConfig() =>
            WriteFile(ConfigPath, "{}");

        public void WriteSpotify(bool withSpotXMarkers)
        {
            WriteFile(SpotifyPath, string.Empty);
            if (withSpotXMarkers)
            {
                var appsDirectory = Path.Combine(Path.GetDirectoryName(SpotifyPath)!, "Apps");
                WriteFile(Path.Combine(appsDirectory, "xpui.spa"), "bundle");
                WriteFile(Path.Combine(appsDirectory, "xpui.spa.bak"), "backup");
            }
        }

        public void WriteSpicetifyConfig(string configBody)
        {
            WriteFile(SpicetifyPath, string.Empty);
            WriteFile(Path.Combine(SpicetifyConfigDirectory, "config-xpui.ini"), configBody);
        }

        public void WriteMarketplaceFiles()
        {
            var marketplaceDirectory = Path.Combine(SpicetifyConfigDirectory, "CustomApps", "marketplace");
            WriteFile(Path.Combine(marketplaceDirectory, "extension.js"), string.Empty);
            WriteFile(Path.Combine(marketplaceDirectory, "manifest.json"), "{}");
        }

        public void WriteBackup()
        {
            var backup = Path.Combine(BackupDirectory, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            WriteFile(Path.Combine(backup, "config-xpui.ini"), "current_theme = Catppuccin");
        }

        public void WriteWatcherState(
            DateTime lastRunAt,
            string outcome,
            string lastKnownVersion = "1.2.92",
            string? lastAppliedSpotifyVersion = null,
            DateTime? lastSuccessfulApplyAt = null,
            DateTime? lastApplyAt = null,
            string? lastApplyOutcome = null,
            string? lastApplyError = null) =>
            WriteFile(
                Path.Combine(ConfigDirectory, "watcher-state.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        LastKnownVersion = lastKnownVersion,
                        LastRunAt = lastRunAt,
                        LastOutcome = outcome,
                        LastAppliedSpotifyVersion = lastAppliedSpotifyVersion,
                        LastAttemptedSpotifyVersion = SpotifyVersion,
                        LastSuccessfulApplyAt = lastSuccessfulApplyAt,
                        LastApplyAt = lastApplyAt,
                        LastApplyOutcome = lastApplyOutcome,
                        LastApplyError = lastApplyError
                    }));

        public void WriteInstallLog() =>
            WriteFile(Path.Combine(ConfigDirectory, "install.log"), "ok");

        public void WriteRecentCrashReport() =>
            WriteFile(Path.Combine(CrashDirectory, "crash-20260616-test.log"), "crash");

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
