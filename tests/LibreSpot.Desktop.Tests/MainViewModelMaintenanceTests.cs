using System.IO;
using LibreSpot.Desktop.Services;
using LibreSpot.Desktop.ViewModels;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class MainViewModelMaintenanceTests
{
    [Fact]
    public Task MaintenanceActions_ShowOpenMarketplaceWhenMarketplaceIsReady() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
            fixture.WriteMarketplaceFiles();
            fixture.WriteBackup();

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.Equal("5 of 5 ready", viewModel.MaintenanceReadinessValue);
            Assert.Contains("Latest:", viewModel.MaintenanceBackupDetail);
            Assert.True(Card(viewModel, "OpenMarketplace").IsRelevant);
            Assert.False(Card(viewModel, "RepairMarketplace").IsRelevant);
            Assert.True(Card(viewModel, "RestoreBackup").IsRelevant);
        });

    [Fact]
    public Task MaintenanceActions_ShowRepairMarketplaceWhenFilesAreMissing() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.Equal("Files missing", viewModel.MaintenanceMarketplaceValue);
            Assert.True(Card(viewModel, "RepairMarketplace").IsRelevant);
            Assert.False(Card(viewModel, "OpenMarketplace").IsRelevant);
        });

    [Fact]
    public Task MaintenanceActions_DisableRestoreBackupWhenNoBackupExists() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
            fixture.WriteMarketplaceFiles();

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.Equal("None yet", viewModel.MaintenanceBackupValue);
            Assert.True(Card(viewModel, "CreateBackup").IsRelevant);
            Assert.False(Card(viewModel, "RestoreBackup").IsRelevant);
        });

    [Fact]
    public Task MaintenanceActions_ShowSafeModeForDisabledThemeInjection() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = Catppuccin\r\ninject_css = 0\r\nreplace_colors = 1");
            fixture.WriteMarketplaceFiles();

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.Equal("Injection disabled", viewModel.MaintenanceThemeValue);
            Assert.True(Card(viewModel, "SafeMode").IsRelevant);
            Assert.True(Card(viewModel, "Reapply").IsRelevant);
        });

    private static MaintenanceActionCardViewModel Card(MainViewModel viewModel, string action) =>
        viewModel.SafeMaintenanceActions
            .Concat(viewModel.DestructiveMaintenanceActions)
            .Single(card => card.Action == action);

    private static Task RunStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(async () =>
        {
            try
            {
                await action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.ContinueWith(task =>
        {
            thread.Join();
            return task;
        }).Unwrap();
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
            RuntimeDirectory = Path.Combine(Root, "runtime");
            RollingLogDirectory = Path.Combine(Root, "logs");
            CrashDirectory = Path.Combine(Root, "crashes");
            Directory.CreateDirectory(ConfigDirectory);
        }

        private string Root { get; }
        private string ConfigDirectory { get; }
        private string ConfigPath { get; }
        private string SpotifyPath { get; }
        private string SpicetifyPath { get; }
        private string SpicetifyConfigDirectory { get; }
        private string BackupDirectory { get; }
        private string RuntimeDirectory { get; }
        private string RollingLogDirectory { get; }
        private string CrashDirectory { get; }

        public async Task<MainViewModel> CreateInitializedViewModelAsync()
        {
            var viewModel = new MainViewModel(
                new ConfigurationService(ConfigDirectory),
                new BackendScriptService(RuntimeDirectory),
                new EnvironmentSnapshotService(
                    autoReapplyTaskProbe: () => false,
                    spotifyPath: SpotifyPath,
                    spicetifyPath: SpicetifyPath,
                    spicetifyConfigDirectory: SpicetifyConfigDirectory,
                    backupDirectory: BackupDirectory,
                    rollingLogDirectory: RollingLogDirectory,
                    crashDirectory: CrashDirectory));

            await viewModel.InitializeAsync();
            return viewModel;
        }

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
