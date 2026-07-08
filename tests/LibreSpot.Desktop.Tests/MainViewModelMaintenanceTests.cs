using System.IO;
using System.IO.Compression;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using LibreSpot.Desktop.ViewModels;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class MainViewModelMaintenanceTests
{
    [Fact]
    public Task InitializeAsync_ShowsForwardSchemaRecoveryNotice() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteConfig("{\"ConfigSchemaVersion\":999}");

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.True(viewModel.HasConfigurationRecoveryNotice);
            Assert.Equal("Profile needs a newer LibreSpot", viewModel.ConfigurationRecoveryTitle);
            Assert.Contains("newer LibreSpot build", viewModel.ConfigurationRecoveryDetail);
            Assert.Contains("Backup kept as", viewModel.ConfigurationRecoveryDetail);
        });

    [Fact]
    public async Task MaintenanceActionCommand_RoutesAsyncExceptionsToHandler()
    {
        var observed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = new MaintenanceActionDefinition(
            "Explode",
            "Explode",
            "Throws during execution",
            "Run");
        var card = new MaintenanceActionCardViewModel(
            definition,
            _ => Task.FromException(new InvalidOperationException("command exploded")),
            () => true,
            ex => observed.TrySetResult(ex.Message));

        card.Command.Execute(null);

        var message = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("command exploded", message);
    }

    [Fact]
    public Task MaintenanceActions_ShowOpenMarketplaceWhenMarketplaceFilesAreInstalled() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
            fixture.WriteMarketplaceFiles();
            fixture.WriteBackup();

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.Equal("4 of 5 ready", viewModel.MaintenanceReadinessValue);
            Assert.Equal("Files installed", viewModel.MaintenanceMarketplaceValue);
            Assert.Contains("Latest:", viewModel.MaintenanceBackupDetail);
            Assert.True(Card(viewModel, "OpenMarketplace").IsRelevant);
            Assert.False(Card(viewModel, "RepairMarketplace").IsRelevant);
            Assert.True(Card(viewModel, "RestoreBackup").IsRelevant);
        });

    [Fact]
    public Task StatusDashboard_ShowsLaunchSnapshotFields() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
            fixture.WriteMarketplaceFiles();
            fixture.WriteBackup();

            using var viewModel = await fixture.CreateInitializedViewModelAsync(
                spotifyVersion: "1.2.93.647",
                spicetifyVersion: "2.44.0");

            Assert.Equal(6, viewModel.StatusDashboardItems.Count);
            Assert.Contains(viewModel.StatusDashboardItems, item => item.Label == "Spotify version" && item.Value == "1.2.93.647");
            Assert.Contains(viewModel.StatusDashboardItems, item => item.Label == "Spicetify version" && item.Value == "2.44.0");
            Assert.Contains(viewModel.StatusDashboardItems, item => item.Label == "SpotX state" && item.Value == "Verified");
            Assert.Contains(viewModel.StatusDashboardItems, item => item.Label == "Last patch" && item.Value != "No patch record");
            Assert.Contains(viewModel.StatusDashboardItems, item => item.Label == "Watcher" && item.Value == "Disabled");
            Assert.Contains(viewModel.StatusDashboardItems, item => item.Label == "Backups" && item.Value == "1 backup");
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
    public Task HealthIssues_ExposeMappedRepairButtons() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            var marketplaceIssue = viewModel.WarningHealthIssues.Single(issue => issue.Id == "marketplace");
            var action = Assert.Single(marketplaceIssue.Actions, item => item.Action == "RepairMarketplace");

            Assert.Equal("Repair Marketplace", action.ButtonText);
            Assert.False(action.IsDestructive);
            Assert.True(action.Command.CanExecute(null));
            Assert.False(marketplaceIssue.ShowRecommendedActionText);
        });

    [Fact]
    public Task HealthIssues_MapLogRecommendationsToDiagnosticFolder()
    {
        return RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
            fixture.WriteMarketplaceFiles();
            fixture.WriteWatcherState("{\"LastKnownVersion\":\"1.2.88\",\"LastOutcome\":\"Error: SpotX failed\",\"LastRunAt\":\"2026-06-20T12:00:00\",\"LastAttemptedSpotifyVersion\":\"1.2.92\"}");

            using var viewModel = await fixture.CreateInitializedViewModelAsync(spotifyVersion: "1.2.92");

            var issues = viewModel.CriticalHealthIssues
                .Concat(viewModel.WarningHealthIssues)
                .Concat(viewModel.InfoHealthIssues)
                .ToArray();
            Assert.True(
                issues.Any(issue => issue.Id == "post-spotify-update"),
                $"Issues: {string.Join(", ", issues.Select(issue => $"{issue.Id}:{issue.Status}"))}");

            var triageIssue = issues.Single(issue => issue.Id == "post-spotify-update");
            Assert.Contains(triageIssue.Actions, item => item.Action == "OpenLogs" && item.ButtonText == "Open LibreSpot folder");
        });
    }

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

    [Fact]
    public Task SupportBundlePreview_ShowsSelectableDiagnosticCategories() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
            fixture.WriteMarketplaceFiles();
            fixture.WriteInstallLog("backend log");
            fixture.WriteRollingLog("desktop log");
            fixture.WriteCrashReport("crash log");

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.Contains(viewModel.SupportBundleItems, item => item.Id == "health" && item.IsRequired && item.IsSelected);
            Assert.Contains(viewModel.SupportBundleItems, item => item.Id == "logs" && item.IsOptional && item.IsSelected);
            Assert.Contains(viewModel.SupportBundleItems, item => item.Id == "crashes" && item.FileCountText == "1 file");
            Assert.Contains(viewModel.SupportBundleRedactionRules, rule => rule.Contains("tokens", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("diagnostic file", viewModel.SupportBundlePreviewTitle);
            Assert.Contains("Estimated local zip size", viewModel.SupportBundlePreviewDetail);
        });

    [Fact]
    public Task ActivityFailureBundleCommand_ExportsCurrentRunDiagnosticsOnlyForFailedRuns() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            fixture.WriteSpicetifyConfig("custom_apps = marketplace\r\ncurrent_theme = SpicetifyDefault");
            fixture.WriteMarketplaceFiles();
            fixture.WriteInstallLog("backend log");
            fixture.WriteOperationJournal("{\"operationId\":\"op-1\",\"result\":\"Failed\"}");

            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.False(viewModel.CanExportFailureBundle);
            Assert.False(viewModel.ExportFailureBundleCommand.CanExecute(null));

            viewModel.ApplyUiAutomationSmokeState("activity");
            Assert.False(viewModel.CanExportFailureBundle);

            viewModel.ApplyUiAutomationSmokeState("activity-error");
            Assert.True(viewModel.CanExportFailureBundle);
            Assert.True(viewModel.ExportFailureBundleCommand.CanExecute(null));

            await viewModel.ExportFailureBundleCommand.ExecuteAsync(null);

            var bundlePath = Assert.Single(Directory.GetFiles(fixture.ConfigDirectory, "LibreSpot-failure-*.zip"));
            var entries = ReadZipText(bundlePath);

            Assert.Contains("current-run/activity-log.txt", entries.Keys);
            Assert.Contains("current-run/backend-result.json", entries.Keys);
            Assert.Contains("operation/latest-journal.txt", entries.Keys);
            Assert.Contains("SmokeFailure", entries["current-run/backend-result.json"]);
            Assert.Contains("UI automation smoke failure", entries["current-run/activity-log.txt"]);
            Assert.Contains("Failure bundle exported locally", viewModel.LogEntries.Last().Message);
        });

    [Fact]
    public Task CustomSmokeState_DoesNotApplyHiddenSettingsSearchFilter() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            viewModel.ApplyUiAutomationSmokeState("custom");

            Assert.Equal(1, viewModel.SelectedWorkspaceIndex);
            Assert.Equal(string.Empty, viewModel.SettingsSearchText);
            Assert.False(viewModel.HasSettingsSearchText);
            Assert.Equal("Search titles and descriptions across Custom.", viewModel.CustomSearchSummary);
        });

    [Fact]
    public Task ThemeGallery_SearchAndSelectionUpdateThemeConfigurationFields() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.Contains(viewModel.ThemeGalleryItems, item => item.Name == "(None - Marketplace Only)" && item.IsMarketplaceOnly);
            Assert.Contains(viewModel.ThemeGalleryItems, item => item.Name == "Catppuccin" && item.IsCommunity && item.RequiresThemeJs);

            viewModel.ThemeSearchText = "mocha";

            Assert.Contains(viewModel.FilteredThemeGalleryItems, item => item.Name == "Catppuccin");
            Assert.Contains(viewModel.FilteredThemeGalleryItems, item => item.Name == "Dribbblish");
            Assert.False(viewModel.ShowThemeGalleryEmptyState);

            viewModel.ThemeSearchText = "theme.js";

            Assert.Contains(viewModel.FilteredThemeGalleryItems, item => item.Name == "Catppuccin");

            viewModel.SelectedThemeGalleryItem = viewModel.ThemeGalleryItems.Single(item => item.Name == "Catppuccin");

            Assert.Equal("Catppuccin", viewModel.SelectedTheme);
            Assert.Contains("mocha", viewModel.SchemeOptions);

            viewModel.SelectedScheme = "macchiato";
            var configuration = BuildConfiguration(viewModel, "Custom");

            Assert.Equal("Catppuccin", configuration.Spicetify_Theme);
            Assert.Equal("macchiato", configuration.Spicetify_Scheme);

            viewModel.ThemeSearchText = "no-match-value";

            Assert.Empty(viewModel.FilteredThemeGalleryItems);
            Assert.True(viewModel.ShowThemeGalleryEmptyState);
            Assert.Equal("No themes match this search.", viewModel.ThemeGalleryEmptyText);
        });

    [Fact]
    public Task LocalProfiles_LoadPreviewCreateRenameAndDeleteProfiles() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            Assert.Contains(viewModel.LocalProfiles, profile => profile.IsBuiltIn && profile.Name == "Recommended");
            Assert.Contains(viewModel.LocalProfiles, profile => profile.IsBuiltIn && profile.Name == "Visual Theme");
            Assert.Contains(viewModel.LocalProfiles, profile => profile.Id == "recommended" && profile.IsActive);
            Assert.True(viewModel.LocalProfiles[0].IsActive);
            Assert.Contains("profile choices ready", viewModel.ProfileOperationStatus, StringComparison.OrdinalIgnoreCase);

            viewModel.SelectedLocalProfile = viewModel.LocalProfiles.Single(profile => profile.Id == "visual-theme");
            Assert.Contains("read-only", viewModel.ProfileSelectionHint, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Duplicate", viewModel.ProfileEditorHint, StringComparison.Ordinal);
            await InvokePrivateTask(viewModel, "PreviewSelectedProfileAsync");

            var previewed = BuildConfiguration(viewModel, "Custom");
            Assert.Equal("Dribbblish", previewed.Spicetify_Theme);

            viewModel.SelectedTheme = "Catppuccin";
            viewModel.SelectedScheme = "mocha";
            viewModel.ProfileNameText = "Desk preset";
            viewModel.ProfileDescriptionText = "Saved from test";
            await InvokePrivateTask(viewModel, "CreateLocalProfileAsync");

            var created = Assert.Single(viewModel.LocalProfiles, profile => profile.Name == "Desk preset");
            Assert.True(created.IsEditable);
            Assert.Equal("Editable", created.CapabilityText);
            Assert.Contains("Edit the name", viewModel.ProfileEditorHint, StringComparison.Ordinal);
            Assert.True(viewModel.RenameProfileCommand.CanExecute(null));

            viewModel.ProfileNameText = "Desk preset renamed";
            await InvokePrivateTask(viewModel, "RenameLocalProfileAsync");

            var renamed = Assert.Single(viewModel.LocalProfiles, profile => profile.Name == "Desk preset renamed");
            Assert.DoesNotContain(viewModel.LocalProfiles, profile => profile.Name == "Desk preset");

            await InvokePrivateTask(viewModel, "DeleteLocalProfileConfirmedAsync", renamed.Id, renamed.Name);

            Assert.DoesNotContain(viewModel.LocalProfiles, profile => profile.Name == "Desk preset renamed");
            Assert.Contains(viewModel.LocalProfiles, profile => profile.Id == "recommended" && profile.IsActive);
        });

    [Fact]
    public Task LocalProfiles_SetActiveProfileWritesConfigAndKeepsPreviousPointer() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            await InvokePrivateTask(viewModel, "SetActiveProfileAsync", "lyrics-focus");

            var active = BuildConfiguration(viewModel, "Custom");
            Assert.Equal("lavender", active.SpotX_LyricsTheme);
            Assert.Contains(viewModel.LocalProfiles, profile => profile.Id == "lyrics-focus" && profile.IsActive);
            Assert.Contains("previous active profile", viewModel.ProfileOperationStatus, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public Task SharedProfileUriPreview_CancelIsInertAndConfirmImports() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            using var viewModel = await fixture.CreateInitializedViewModelAsync();
            var shareUri = await fixture.CreateExternalShareUriAsync("Shared Desk");
            var beforeCancel = BuildConfiguration(viewModel, "Custom");

            await viewModel.PreviewSharedProfileUriAsync(shareUri);

            Assert.True(viewModel.IsPromptVisible);
            Assert.Equal("Import shared profile: Shared Desk", viewModel.PromptTitle);
            Assert.Contains("preview only", viewModel.PromptBody, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Shared settings", viewModel.PromptSummaryTitle);
            Assert.Contains("Catppuccin", viewModel.PromptSummaryBody, StringComparison.Ordinal);
            Assert.DoesNotContain(viewModel.LocalProfiles, profile => profile.Name == "Shared Desk");

            viewModel.CancelPromptCommand.Execute(null);

            Assert.False(viewModel.IsPromptVisible);
            Assert.DoesNotContain(viewModel.LocalProfiles, profile => profile.Name == "Shared Desk");
            Assert.False(viewModel.IsRunning);
            Assert.Equal(beforeCancel.Spicetify_Theme, BuildConfiguration(viewModel, "Custom").Spicetify_Theme);

            await viewModel.PreviewSharedProfileUriAsync(shareUri);
            await InvokePrivateTask(viewModel, "ConfirmPromptAsync");

            Assert.False(viewModel.IsPromptVisible);
            Assert.False(viewModel.IsRunning);
            Assert.Contains(viewModel.LocalProfiles, profile => profile.Name == "Shared Desk" && profile.IsEditable);
            Assert.Contains("Shared Desk was imported", viewModel.ProfileOperationStatus, StringComparison.Ordinal);
            Assert.Equal(beforeCancel.Spicetify_Theme, BuildConfiguration(viewModel, "Custom").Spicetify_Theme);
        });

    [Fact]
    public Task PromptDefaultConfirm_OnlyAppliesToNonDestructivePrompts() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            using var viewModel = await fixture.CreateInitializedViewModelAsync();

            viewModel.ApplyUiAutomationSmokeState("prompt");

            Assert.True(viewModel.IsPromptVisible);
            Assert.False(viewModel.IsPromptDestructive);
            Assert.True(viewModel.IsPromptConfirmDefault);

            viewModel.ApplyUiAutomationSmokeState("prompt-destructive");

            Assert.True(viewModel.IsPromptVisible);
            Assert.True(viewModel.IsPromptDestructive);
            Assert.False(viewModel.IsPromptConfirmDefault);
        });

    [Fact]
    public Task SuccessfulInstallRun_RestartsSpotifyWhenLaunchAfterIsEnabled() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            fixture.WriteSpotify(withSpotXMarkers: true);
            var spotifyProcessService = new RecordingSpotifyProcessService();
            using var viewModel = await fixture.CreateInitializedViewModelAsync(
                spotifyProcessService: spotifyProcessService,
                noBackendMode: true);
            var configuration = AppCatalog.CreateRecommendedConfiguration();
            configuration.LaunchAfter = true;

            await InvokePrivateTask(
                viewModel,
                "StartBackendRunAsync",
                "Install",
                configuration,
                "Install",
                "Installing",
                0,
                false);

            Assert.Equal(1, spotifyProcessService.RestartCalls);
            Assert.Equal(TimeSpan.FromSeconds(3), spotifyProcessService.LastReopenDelay);
            Assert.Equal("Spotify reopened", viewModel.ActivityStep);
            Assert.Contains(viewModel.LogEntries, entry => entry.Message.Contains("fresh client session", StringComparison.OrdinalIgnoreCase));
        });

    [Fact]
    public Task SuccessfulInstallRun_RespectsLaunchAfterDisabled() =>
        RunStaAsync(async () =>
        {
            using var fixture = new SnapshotFixture();
            var spotifyProcessService = new RecordingSpotifyProcessService();
            using var viewModel = await fixture.CreateInitializedViewModelAsync(
                spotifyProcessService: spotifyProcessService,
                noBackendMode: true);
            var configuration = AppCatalog.CreateRecommendedConfiguration();
            configuration.LaunchAfter = false;

            await InvokePrivateTask(
                viewModel,
                "StartBackendRunAsync",
                "Install",
                configuration,
                "Install",
                "Installing",
                0,
                false);

            Assert.Equal(0, spotifyProcessService.RestartCalls);
        });

    private static MaintenanceActionCardViewModel Card(MainViewModel viewModel, string action) =>
        viewModel.SafeMaintenanceActions
            .Concat(viewModel.DestructiveMaintenanceActions)
            .Single(card => card.Action == action);

    private static InstallConfiguration BuildConfiguration(MainViewModel viewModel, string mode)
    {
        var method = typeof(MainViewModel).GetMethod(
            "BuildConfiguration",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<InstallConfiguration>(method.Invoke(viewModel, new object[] { mode }));
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

    private static async Task InvokePrivateTask(MainViewModel viewModel, string methodName, params object[] args)
    {
        var method = typeof(MainViewModel).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(viewModel, args);
        var task = Assert.IsAssignableFrom<Task>(result);
        await task;
    }

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
        public string ConfigDirectory { get; }
        private string ConfigPath { get; }
        private string SpotifyPath { get; }
        private string SpicetifyPath { get; }
        private string SpicetifyConfigDirectory { get; }
        private string BackupDirectory { get; }
        private string RuntimeDirectory { get; }
        private string RollingLogDirectory { get; }
        private string CrashDirectory { get; }

        public async Task<MainViewModel> CreateInitializedViewModelAsync(
            string? spotifyVersion = null,
            string? spicetifyVersion = null,
            ISpotifyProcessService? spotifyProcessService = null,
            bool noBackendMode = false)
        {
            var viewModel = new MainViewModel(
                new ConfigurationService(ConfigDirectory),
                new BackendScriptService(RuntimeDirectory, noBackendMode),
                new EnvironmentSnapshotService(
                    autoReapplyTaskProbe: () => false,
                    spotifyPath: SpotifyPath,
                    spicetifyPath: SpicetifyPath,
                    spicetifyConfigDirectory: SpicetifyConfigDirectory,
                    backupDirectory: BackupDirectory,
                    rollingLogDirectory: RollingLogDirectory,
                    crashDirectory: CrashDirectory,
                    spotifyVersionProbe: () => spotifyVersion,
                    spicetifyVersionProbe: () => spicetifyVersion),
                new SupportBundleService(ConfigDirectory, RollingLogDirectory, CrashDirectory),
                spotifyProcessService: spotifyProcessService);

            await viewModel.InitializeAsync();
            return viewModel;
        }

        public async Task<string> CreateExternalShareUriAsync(string name)
        {
            var sourceDirectory = Path.Combine(Root, "source-profile-store");
            var service = new LocalProfileService(new ConfigurationService(sourceDirectory));
            var config = AppCatalog.CreateRecommendedConfiguration();
            config.Spicetify_Theme = "Catppuccin";
            config.Spicetify_Scheme = "mocha";
            var profile = await service.CreateFromConfigurationAsync(name, "Shared from test", config);
            var card = await service.CreateShareCardAsync(profile.Summary.Id);
            return card.ShareUri;
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

        public void WriteWatcherState(string content) =>
            WriteFile(Path.Combine(ConfigDirectory, "watcher-state.json"), content);

        public void WriteInstallLog(string content) =>
            WriteFile(Path.Combine(ConfigDirectory, "install.log"), content);

        public void WriteOperationJournal(string content) =>
            WriteFile(Path.Combine(ConfigDirectory, "operation-journal.jsonl"), content);

        public void WriteRollingLog(string content) =>
            WriteFile(Path.Combine(RollingLogDirectory, "librespot-20260616.log"), content);

        public void WriteCrashReport(string content) =>
            WriteFile(Path.Combine(CrashDirectory, "crash-20260616-test.log"), content);

        public void WriteConfig(string content) =>
            WriteFile(ConfigPath, content);

        private static void WriteFile(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            // Background work kicked off by the viewmodel (share-card refresh,
            // QR generation) can briefly hold a profile file open when the
            // test body finishes; retry instead of flaking the test, and never
            // fail it over %TEMP% cleanup.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(Root))
                    {
                        Directory.Delete(Root, recursive: true);
                    }
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }

    private sealed class RecordingSpotifyProcessService : ISpotifyProcessService
    {
        public int RestartCalls { get; private set; }
        public TimeSpan? LastReopenDelay { get; private set; }

        public Task<SpotifyRestartResult> RestartAsync(
            string? preferredSpotifyPath,
            TimeSpan reopenDelay,
            CancellationToken cancellationToken)
        {
            RestartCalls++;
            LastReopenDelay = reopenDelay;
            return Task.FromResult(new SpotifyRestartResult(true, "Spotify was closed and reopened after the run completed."));
        }
    }
}
