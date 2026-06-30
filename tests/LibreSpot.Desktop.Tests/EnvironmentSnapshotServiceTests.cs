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

    [Fact]
    public void AutoReapplyTaskProbe_DrainsRedirectedPipesBeforeWaitForExit()
    {
        var source = File.ReadAllText(Path.Combine(
            ResolveRepoRoot(), "src", "LibreSpot.Desktop", "Services", "EnvironmentSnapshotService.cs"));

        var stdoutIndex = source.IndexOf("StandardOutput.ReadToEndAsync", StringComparison.Ordinal);
        var stderrIndex = source.IndexOf("StandardError.ReadToEndAsync", StringComparison.Ordinal);
        var waitIndex = source.IndexOf("WaitForExit(1500)", StringComparison.Ordinal);

        Assert.True(stdoutIndex > 0, "schtasks stdout must be drained asynchronously.");
        Assert.True(stderrIndex > 0, "schtasks stderr must be drained asynchronously.");
        Assert.True(waitIndex > 0, "schtasks probe must keep the bounded wait.");
        Assert.True(stdoutIndex < waitIndex, "stdout drain must start before WaitForExit.");
        Assert.True(stderrIndex < waitIndex, "stderr drain must start before WaitForExit.");
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
    public void GetSnapshot_HealthReport_IncludesUpstreamDriftComponents()
    {
        using var fixture = new SnapshotFixture();
        var checkedAt = DateTimeOffset.Parse("2026-06-29T12:00:00Z");
        var report = new UpstreamDriftReport(
            new[]
            {
                new UpstreamDependencyState(
                    "spotx",
                    "SpotX",
                    AppCatalog.PinnedSpotXCommit,
                    AppCatalog.PinnedSpotXCommit,
                    "ba77e8cc0b8e79806b3bcc7c767b9b4bc20f9680",
                    "behind",
                    "git ls-remote",
                    checkedAt,
                    TimeSpan.FromHours(2),
                    false,
                    "Pinned/current metadata; latest from git ls-remote; cache age 2 hours.")
            },
            checkedAt);

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false, upstreamDriftReport: report);

        var component = Assert.Single(snapshot.HealthReport.Components, item => item.Id == "upstream-spotx");
        Assert.Equal("SpotX upstream", component.Name);
        Assert.Equal("Upstream changed", component.Status);
        Assert.Equal(HealthSeverity.Info, component.Severity);
        Assert.StartsWith("ba77e8cc", component.DetectedVersion);
        Assert.Contains("git ls-remote", component.Evidence);
        Assert.Contains("cache age 2 hours", component.Evidence);
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
        Assert.Contains(snapshot.HealthReport.InfoIssues, component => component.Id == "post-spotify-update" && component.Status == "Not applicable");
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
    public void GetSnapshot_ExtensionIntegrity_AllFilesPresent()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpicetifyConfig("extensions = fullAppDisplay.js|shuffle+.js\r\ncustom_apps = marketplace");
        fixture.WriteExtensionFiles("fullAppDisplay.js", "shuffle+.js");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var ext = Assert.Single(snapshot.HealthReport.Components, c => c.Id == "extension-integrity");
        Assert.Equal("All present", ext.Status);
        Assert.Equal(HealthSeverity.Ready, ext.Severity);
    }

    [Fact]
    public void GetSnapshot_ExtensionIntegrity_MissingFileTriggersQuarantineWarning()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpicetifyConfig("extensions = fullAppDisplay.js|beautiful-lyrics.mjs\r\ncustom_apps = marketplace");
        fixture.WriteExtensionFiles("fullAppDisplay.js");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var ext = Assert.Single(snapshot.HealthReport.WarningIssues, c => c.Id == "extension-integrity");
        Assert.Contains("missing", ext.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("beautiful-lyrics.mjs", ext.Evidence);
        Assert.Contains("security product", ext.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reapply", ext.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_ExtensionIntegrity_RejectsPathLikeExtensionEntries()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpicetifyConfig(@"extensions = fullAppDisplay.js|..\outside.js|C:\Temp\bad.js");
        fixture.WriteExtensionFiles("fullAppDisplay.js");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var ext = Assert.Single(snapshot.HealthReport.WarningIssues, c => c.Id == "extension-integrity");
        Assert.Equal("2 invalid entries", ext.Status);
        Assert.Equal(HealthSeverity.Warning, ext.Severity);
        Assert.Contains("plain file names", ext.Evidence);
        Assert.DoesNotContain("outside.js", ext.Evidence);
        Assert.DoesNotContain("C:\\Temp", ext.Evidence);
        Assert.Contains("Reapply", ext.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_ExtensionIntegrity_NoExtensionsRegistered()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpicetifyConfig("custom_apps = marketplace");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var ext = Assert.Single(snapshot.HealthReport.Components, c => c.Id == "extension-integrity");
        Assert.Equal("None registered", ext.Status);
        Assert.Equal(HealthSeverity.Ready, ext.Severity);
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

    [Fact]
    public void GetSnapshot_SpotifyDirectoryWithoutExe_ReportsNotInstalled()
    {
        using var fixture = new SnapshotFixture();
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.SpotifyPath)!);

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        Assert.False(snapshot.SpotifyInstalled);
        var spotify = Assert.Single(snapshot.HealthReport.Components, c => c.Id == "spotify");
        Assert.Equal("Not installed", spotify.Status);
    }

    [Fact]
    public void GetSnapshot_SpotXMarkersWithoutSpotifyExe_ReportsNotChecked()
    {
        using var fixture = new SnapshotFixture();
        var appsDir = Path.Combine(Path.GetDirectoryName(fixture.SpotifyPath)!, "Apps");
        Directory.CreateDirectory(appsDir);
        File.WriteAllText(Path.Combine(appsDir, "xpui.spa"), "bundle");
        File.WriteAllText(Path.Combine(appsDir, "xpui.spa.bak"), "backup");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        Assert.False(snapshot.SpotifyInstalled);
        var spotx = Assert.Single(snapshot.HealthReport.Components, c => c.Id == "spotx");
        Assert.Equal("Not checked", spotx.Status);
    }

    [Fact]
    public void GetSnapshot_SpotifyWithOnlyBundleNoBak_ReportsUnverified()
    {
        using var fixture = new SnapshotFixture();
        fixture.WriteSpotify(withSpotXMarkers: false);
        var appsDir = Path.Combine(Path.GetDirectoryName(fixture.SpotifyPath)!, "Apps");
        Directory.CreateDirectory(appsDir);
        File.WriteAllText(Path.Combine(appsDir, "xpui.spa"), "bundle");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        Assert.True(snapshot.SpotifyInstalled);
        var spotx = Assert.Single(snapshot.HealthReport.Components, c => c.Id == "spotx");
        Assert.Equal("Unverified", spotx.Status);
        Assert.Equal(HealthSeverity.Warning, spotx.Severity);
        Assert.Contains("Reapply", spotx.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_SpotifyInstalledWithoutWatcherHistory_DoesNotClaimNoDrift()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.95.100" };
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
        fixture.WriteMarketplaceFiles();

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var triage = Assert.Single(snapshot.HealthReport.InfoIssues, c => c.Id == "post-spotify-update");
        Assert.Equal("Watcher not enabled", triage.Status);
        Assert.Equal(HealthSeverity.Info, triage.Severity);
        Assert.Contains("no recorded patched Spotify version", triage.Evidence);
        Assert.Contains("Reapply", triage.RecommendedActionIds);
    }

    [Fact]
    public void GetSnapshot_SpotifyVersionProbeReturnsKnownVersion()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.92.456" };
        fixture.WriteSpotify(withSpotXMarkers: true);

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: false);

        var spotify = Assert.Single(snapshot.HealthReport.Components, c => c.Id == "spotify");
        Assert.Equal("1.2.92.456", spotify.DetectedVersion);
        Assert.Equal(HealthSeverity.Ready, spotify.Severity);
    }

    [Fact]
    public void GetSnapshot_SpotifyVersionMismatchTriggersReapplyNeeded()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.95.100" };
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
        fixture.WriteMarketplaceFiles();
        fixture.WriteWatcherState(DateTime.Now.AddHours(-1), "UpToDate", lastKnownVersion: "1.2.92.456", lastAppliedSpotifyVersion: "1.2.92.456");

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        var triage = Assert.Single(snapshot.HealthReport.Components, c => c.Id == "post-spotify-update");
        Assert.Equal("Reapply needed", triage.Status);
        Assert.Equal(HealthSeverity.Warning, triage.Severity);
    }

    [Fact]
    public void GetSnapshot_FullStackWithArchitectureRecordsBothFields()
    {
        using var fixture = new SnapshotFixture { SpotifyVersion = "1.2.92.456", SpicetifyVersion = "2.43.2" };
        fixture.WriteSavedConfig();
        fixture.WriteSpotify(withSpotXMarkers: true);
        fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = Catppuccin\r\ninject_css = 1\r\nreplace_colors = 1");
        fixture.WriteMarketplaceFiles();
        fixture.WriteBackup();
        fixture.WriteWatcherState(DateTime.Now.AddHours(-1), "UpToDate",
            lastKnownVersion: "1.2.92.456",
            lastAppliedSpotifyVersion: "1.2.92.456",
            lastSuccessfulApplyAt: DateTime.Now.AddHours(-1));
        fixture.WriteInstallLog();

        var snapshot = fixture.GetSnapshot(autoReapplyRegistered: true);

        Assert.NotEqual("Unknown", snapshot.HostArchitecture);
        Assert.NotEqual("Unknown", snapshot.ProcessArchitecture);
        Assert.Equal("Stack ready", snapshot.HealthReport.StatusTitle);
        Assert.True(snapshot.SpotifyInstalled);
        Assert.True(snapshot.SpicetifyInstalled);
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

        public EnvironmentSnapshot GetSnapshot(bool autoReapplyRegistered, UpstreamDriftReport? upstreamDriftReport = null)
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
                spotifyRunningProbe: () => SpotifyRunning,
                upstreamDriftProbe: () => upstreamDriftReport ?? UpstreamDriftReport.Empty);

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

        public void WriteExtensionFiles(params string[] extensionNames)
        {
            var extensionsDir = Path.Combine(SpicetifyConfigDirectory, "Extensions");
            foreach (var name in extensionNames)
            {
                WriteFile(Path.Combine(extensionsDir, name), "// extension content");
            }
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
