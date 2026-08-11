using System.Text.Json;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Microsoft.Win32;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class MultiUserIsolationTests
{
    [Fact]
    public async Task PerUserRoots_IsolateConfigurationProfilesBackupsLogsAndCrashes()
    {
        using var userA = new UserFixture("A");
        using var userB = new UserFixture("B");

        var configurationA = AppCatalog.CreateRecommendedConfiguration();
        configurationA.SpotX_LyricsTheme = "github";
        configurationA.SpotX_Premium = true;
        var configurationB = AppCatalog.CreateRecommendedConfiguration();
        configurationB.SpotX_LyricsTheme = "lavender";
        configurationB.SpotX_Premium = false;

        await userA.Configuration.SaveAsync(configurationA);
        await userB.Configuration.SaveAsync(configurationB);

        var profileA = await userA.Profiles.CreateFromConfigurationAsync("Alice", "User A", configurationA);
        var profileB = await userB.Profiles.CreateFromConfigurationAsync("Bob", "User B", configurationB);
        await userA.Profiles.ApplyProfileAsync(profileA.Summary.Id);
        await userB.Profiles.ApplyProfileAsync(profileB.Summary.Id);

        userA.WriteDiagnostics();
        userB.WriteDiagnostics();

        var profilesA = await userA.Profiles.GetProfilesAsync();
        var profilesB = await userB.Profiles.GetProfilesAsync();
        var loadedA = await userA.Configuration.LoadAsync();
        var loadedB = await userB.Configuration.LoadAsync();

        Assert.Contains(profilesA, profile => profile.Name == "Alice");
        Assert.DoesNotContain(profilesA, profile => profile.Name == "Bob");
        Assert.Contains(profilesB, profile => profile.Name == "Bob");
        Assert.DoesNotContain(profilesB, profile => profile.Name == "Alice");
        Assert.Equal("github", loadedA.SpotX_LyricsTheme);
        Assert.Equal("lavender", loadedB.SpotX_LyricsTheme);
        Assert.True(loadedA.SpotX_Premium);
        Assert.False(loadedB.SpotX_Premium);
        Assert.Equal(profileA.Summary.Id, Assert.Single(profilesA, profile => profile.Name == "Alice").Id);
        Assert.Equal(profileB.Summary.Id, Assert.Single(profilesB, profile => profile.Name == "Bob").Id);

        var snapshotA = userA.CreateSnapshot().GetSnapshot(userA.Configuration.ConfigPath);
        var snapshotB = userB.CreateSnapshot().GetSnapshot(userB.Configuration.ConfigPath);
        Assert.Equal(userA.BackupDirectory, GetComponent(snapshotA, "backups").Path);
        Assert.Equal(userB.BackupDirectory, GetComponent(snapshotB, "backups").Path);
        Assert.Equal(userA.Configuration.LogPath, GetComponent(snapshotA, "logs").Path);
        Assert.Equal(userB.Configuration.LogPath, GetComponent(snapshotB, "logs").Path);
        Assert.Equal(userA.CrashDirectory, GetComponent(snapshotA, "crash-reports").Path);
        Assert.Equal(userB.CrashDirectory, GetComponent(snapshotB, "crash-reports").Path);
        Assert.Equal("1 backup", GetComponent(snapshotA, "backups").Status);
        Assert.Equal("1 backup", GetComponent(snapshotB, "backups").Status);
        Assert.DoesNotContain(userB.Root, GetComponent(snapshotA, "backups").Path!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(userA.Root, GetComponent(snapshotB, "backups").Path!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellRegistrationPlans_AreDistinctAndCurrentUserScoped()
    {
        using var userA = new UserFixture("A");
        using var userB = new UserFixture("B");
        var executableA = userA.Path("LibreSpot.exe");
        var executableB = userB.Path("LibreSpot.exe");

        var planA = ShellIntegrationService.BuildRegistrationPlan(executableA);
        var planB = ShellIntegrationService.BuildRegistrationPlan(executableB);

        Assert.All(planA, entry => Assert.StartsWith(@"Software\Classes\", entry.KeyPath, StringComparison.Ordinal));
        Assert.All(planB, entry => Assert.StartsWith(@"Software\Classes\", entry.KeyPath, StringComparison.Ordinal));
        Assert.Contains(planA, entry => entry.Value.Contains(executableA, StringComparison.Ordinal));
        Assert.DoesNotContain(planA, entry => entry.Value.Contains(executableB, StringComparison.Ordinal));
        Assert.Contains(planB, entry => entry.Value.Contains(executableB, StringComparison.Ordinal));
        Assert.DoesNotContain(planB, entry => entry.Value.Contains(executableA, StringComparison.Ordinal));

        var source = File.ReadAllText(Path.Combine(ResolveRepoRoot(), "src", "LibreSpot.Desktop", "Services", "ShellIntegrationService.cs"));
        Assert.Contains("Registry.CurrentUser", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Registry.LocalMachine", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WpfShell_StaysAsInvokerAndDocumentsPerUserNoAdminActions()
    {
        var root = ResolveRepoRoot();
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "schemas", "elevation-boundary.json")));

        Assert.Equal("asInvoker", schema.RootElement.GetProperty("executionLevel").GetProperty("wpfDesktopShell").GetString());
        foreach (var action in schema.RootElement.GetProperty("actions").EnumerateArray())
        {
            Assert.Equal("no-admin", action.GetProperty("elevation").GetString());
        }
    }

    private static StackHealthComponent GetComponent(EnvironmentSnapshot snapshot, string id) =>
        Assert.Single(snapshot.HealthReport.Components, component => component.Id == id);

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class UserFixture : IDisposable
    {
        public UserFixture(string id)
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LibreSpot.MultiUser.Tests", Guid.NewGuid().ToString("N"), $"User{id}");
            ConfigDirectory = System.IO.Path.Combine(Root, "AppData", "Roaming", "LibreSpot");
            BackupDirectory = System.IO.Path.Combine(Root, "UserProfile", "LibreSpot_Backups");
            RollingLogDirectory = System.IO.Path.Combine(Root, "AppData", "Local", "LibreSpot", "logs");
            CrashDirectory = System.IO.Path.Combine(Root, "AppData", "Local", "LibreSpot", "crashes");
            Directory.CreateDirectory(ConfigDirectory);
            Configuration = new ConfigurationService(ConfigDirectory);
            Profiles = new LocalProfileService(Configuration);
        }

        public string Root { get; }
        public string ConfigDirectory { get; }
        public string BackupDirectory { get; }
        public string RollingLogDirectory { get; }
        public string CrashDirectory { get; }
        public ConfigurationService Configuration { get; }
        public LocalProfileService Profiles { get; }

        public string Path(string relativePath) => System.IO.Path.Combine(Root, relativePath);

        public void WriteDiagnostics()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(BackupDirectory, "20260811-010101"));
            Directory.CreateDirectory(RollingLogDirectory);
            Directory.CreateDirectory(CrashDirectory);
            File.WriteAllText(Configuration.LogPath, "user-specific install log");
            File.WriteAllText(System.IO.Path.Combine(RollingLogDirectory, "librespot-user.log"), "user-specific rolling log");
            File.WriteAllText(System.IO.Path.Combine(CrashDirectory, "crash-user.log"), "user-specific crash report");
        }

        public EnvironmentSnapshotService CreateSnapshot() => new(
            autoReapplyTaskProbe: () => false,
            spotifyPath: Path("AppData\\Roaming\\Spotify\\Spotify.exe"),
            spicetifyPath: Path("AppData\\Local\\spicetify\\spicetify.exe"),
            spicetifyConfigDirectory: Path("AppData\\Roaming\\spicetify"),
            backupDirectory: BackupDirectory,
            rollingLogDirectory: RollingLogDirectory,
            crashDirectory: CrashDirectory,
            upstreamDriftProbe: () => UpstreamDriftReport.Empty,
            communityAssetDriftProbe: () => CommunityAssetDriftReport.Empty);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(System.IO.Path.GetDirectoryName(Root)))
                {
                    Directory.Delete(System.IO.Path.GetDirectoryName(Root)!, recursive: true);
                }
            }
            catch
            {
                // Test cleanup is best effort; the unique temp root is not shared with the user profile.
            }
        }
    }
}
