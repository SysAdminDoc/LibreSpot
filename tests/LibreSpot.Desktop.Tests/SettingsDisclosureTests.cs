using System.IO;
using System.Text.RegularExpressions;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using LibreSpot.Desktop.ViewModels;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class SettingsDisclosureTests
{
    [Fact]
    public Task Settings_OpensWithTheFourCommonChoicesAndEveryGroupClosed() => RunStaAsync(async () =>
    {
        using var fixture = new ShellFixture();
        using var viewModel = fixture.Create();
        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasVisibleEssentials);
        Assert.Equal(nameof(InstallConfiguration.LaunchAfter), viewModel.LaunchAfterOption.Key);
        Assert.Equal(nameof(InstallConfiguration.Spicetify_Marketplace), viewModel.MarketplaceOption.Key);
        Assert.Same(viewModel.ExperienceOptions.Single(o => o.Key == nameof(InstallConfiguration.Spicetify_Marketplace)), viewModel.MarketplaceOption);
        Assert.Same(viewModel.InstallOptions.Single(o => o.Key == nameof(InstallConfiguration.LaunchAfter)), viewModel.LaunchAfterOption);

        var detailKeys = viewModel.InstallDetailOptions.Cast<OptionToggleViewModel>().Select(o => o.Key).ToArray();
        Assert.Equal(new[] { nameof(InstallConfiguration.CleanInstall) }, detailKeys);

        Assert.All(GroupStates(viewModel), state => Assert.False(state.Value, $"{state.Key} must start closed."));
        Assert.True(viewModel.HasVisibleProfileTools);
    });

    [Fact]
    public Task Settings_SearchOpensOnlyTheGroupsThatHoldAMatch() => RunStaAsync(async () =>
    {
        using var fixture = new ShellFixture();
        using var viewModel = fixture.Create();
        await viewModel.InitializeAsync();

        viewModel.SettingsSearchText = "lyrics";

        Assert.True(viewModel.HasVisibleAppearanceDetails, "Lyrics theme lives in Appearance details.");
        Assert.True(viewModel.IsAppearanceDetailsExpanded);
        Assert.True(viewModel.IsBehaviorExpanded, "Lyrics patches live in Playback and interface patches.");
        Assert.False(viewModel.HasVisibleInstallDetails);
        Assert.False(viewModel.IsInstallDetailsExpanded);
        Assert.False(viewModel.HasVisibleProfileTools, "Profile tools are not searchable and stay out of the way during a search.");
        Assert.True(viewModel.HasAnyCustomSearchMatches);
    });

    [Fact]
    public Task Settings_ClearingSearchRestoresTheUsersDisclosureState() => RunStaAsync(async () =>
    {
        using var fixture = new ShellFixture();
        using var viewModel = fixture.Create();
        await viewModel.InitializeAsync();

        viewModel.IsAdvancedExpanded = true;
        viewModel.IsProfileToolsExpanded = true;

        viewModel.SettingsSearchText = "cache";
        Assert.True(viewModel.IsInstallDetailsExpanded, "Cache limit lives in Installation details.");

        // Collapsing a group while searching is allowed and does not leak into the saved state.
        viewModel.IsInstallDetailsExpanded = false;
        Assert.False(viewModel.IsInstallDetailsExpanded);

        viewModel.SettingsSearchText = string.Empty;

        Assert.True(viewModel.IsAdvancedExpanded);
        Assert.True(viewModel.IsProfileToolsExpanded);
        Assert.False(viewModel.IsInstallDetailsExpanded);
        Assert.False(viewModel.IsAppearanceDetailsExpanded);
        Assert.False(viewModel.IsBehaviorExpanded);
        Assert.True(viewModel.HasVisibleProfileTools);
    });

    [Fact]
    public Task Settings_SearchOpensAGroupAgainAfterTheUserClosedItDuringAnEarlierSearch() => RunStaAsync(async () =>
    {
        using var fixture = new ShellFixture();
        using var viewModel = fixture.Create();
        await viewModel.InitializeAsync();

        viewModel.SettingsSearchText = "cache";
        viewModel.IsInstallDetailsExpanded = false;
        viewModel.SettingsSearchText = "cache limit";

        Assert.True(viewModel.IsInstallDetailsExpanded, "A new search term starts from the matches again.");
    });

    [Fact]
    public void EssentialsSection_ExposesExactlyTheFourCommonChoices()
    {
        var essentials = ReadView("CustomAppearanceSection.xaml");

        foreach (var binding in new[] { "SelectedSpotifyVersionId", "FilteredThemeGalleryItems", "MarketplaceOption", "LaunchAfterOption" })
        {
            Assert.Contains($"{{Binding {binding}", essentials, StringComparison.Ordinal);
        }

        foreach (var hidden in new[] { "CacheLimitText", "SchemeOptions", "LyricsThemes", "SelectedDownloadMethod", "InstallOptions", "CoreOptions", "AdvancedOptions" })
        {
            Assert.DoesNotContain($"{{Binding {hidden}", essentials, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SettingsWorkspace_HasOneScrollbarOneLevelOfDisclosureAndAnUnclippedApplyAction()
    {
        var workspace = ReadView("CustomWorkspaceView.xaml");
        var sectionFiles = Directory.GetFiles(ViewsDirectory, "Custom*Section.xaml");

        var scrollViewers = Regex.Matches(workspace, "<ScrollViewer").Count
            + sectionFiles.Sum(file => Regex.Matches(File.ReadAllText(file), "<ScrollViewer").Count);
        Assert.Equal(1, scrollViewers);

        Assert.All(sectionFiles, file => Assert.DoesNotContain("<Expander", File.ReadAllText(file), StringComparison.Ordinal));

        var expanders = Regex.Matches(workspace, @"<Expander[\s\S]*?>").Select(match => match.Value).ToList();
        Assert.Equal(8, expanders.Count);
        Assert.All(expanders, expander =>
        {
            Assert.Contains("Style=\"{StaticResource SettingsSectionExpanderStyle}\"", expander, StringComparison.Ordinal);
            Assert.Matches(@"IsExpanded=""\{Binding Is\w+Expanded, Mode=TwoWay\}""", expander);
            Assert.Contains("AutomationProperties.AutomationId=\"Settings", expander, StringComparison.Ordinal);
            Assert.Contains("AutomationProperties.Name=", expander, StringComparison.Ordinal);
        });

        var scrollEnd = workspace.IndexOf("</ScrollViewer>", StringComparison.Ordinal);
        var apply = workspace.IndexOf("AutomationProperties.AutomationId=\"ApplyCustomProfileButton\"", StringComparison.Ordinal);
        Assert.True(scrollEnd >= 0 && apply > scrollEnd, "The apply action must sit in the footer outside the scrolling column.");

        var profiles = ReadView("CustomProfileSummarySection.xaml");
        var list = Regex.Match(profiles, @"<ListBox x:Name=""LocalProfilesList""[\s\S]*?>").Value;
        Assert.DoesNotContain("MaxHeight", list, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyCustomProfileButton", profiles, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsSectionExpanderStyle_KeepsFocusVisibleAndTheHeaderTargetLarge()
    {
        var controls = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Themes", "Controls.xaml"));
        var start = controls.IndexOf("x:Key=\"SettingsSectionExpanderStyle\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "SettingsSectionExpanderStyle not found.");
        var style = controls[start..controls.IndexOf("</Style>", start, StringComparison.Ordinal)];

        Assert.Contains("IsKeyboardFocused", style, StringComparison.Ordinal);
        Assert.Contains("AccentRingBrush", style, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{TemplateBinding Header}\"", style, StringComparison.Ordinal);
        var minHeight = int.Parse(Regex.Match(style, @"MinHeight=""(\d+)""").Groups[1].Value);
        Assert.True(minHeight >= 44, "The header must stay a 44 pixel target.");
    }

    private static IEnumerable<KeyValuePair<string, bool>> GroupStates(MainViewModel viewModel) =>
    [
        new("install", viewModel.IsInstallDetailsExpanded),
        new("appearance", viewModel.IsAppearanceDetailsExpanded),
        new("behavior", viewModel.IsBehaviorExpanded),
        new("advanced", viewModel.IsAdvancedExpanded),
        new("live", viewModel.IsLiveCustomizationExpanded),
        new("extensions", viewModel.IsExtensionsExpanded),
        new("apps", viewModel.IsCustomAppsExpanded),
        new("profiles", viewModel.IsProfileToolsExpanded)
    ];

    private static string ViewsDirectory => Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Views");

    private static string ReadView(string name) => File.ReadAllText(Path.Combine(ViewsDirectory, name));

    private static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
        }
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

    private sealed class ShellFixture : IDisposable
    {
        private readonly string _root;

        public ShellFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", "settings-disclosure", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "LibreSpot"));
        }

        public MainViewModel Create()
        {
            var configDirectory = Path.Combine(_root, "LibreSpot");
            return new MainViewModel(
                new ConfigurationService(configDirectory),
                new BackendScriptService(Path.Combine(_root, "runtime"), noBackendMode: true),
                new EnvironmentSnapshotService(
                    autoReapplyTaskProbe: () => false,
                    spotifyPath: Path.Combine(_root, "Spotify", "Spotify.exe"),
                    spicetifyPath: Path.Combine(_root, "spicetify", "spicetify.exe"),
                    spicetifyConfigDirectory: Path.Combine(_root, "spicetify-config"),
                    backupDirectory: Path.Combine(_root, "backups"),
                    rollingLogDirectory: Path.Combine(_root, "logs"),
                    crashDirectory: Path.Combine(_root, "crashes"),
                    spotifyVersionProbe: () => null,
                    spicetifyVersionProbe: () => null,
                    upstreamDriftProbe: () => UpstreamDriftReport.Empty,
                    communityAssetDriftProbe: () => CommunityAssetDriftReport.Empty),
                new SupportBundleService(configDirectory, Path.Combine(_root, "logs"), Path.Combine(_root, "crashes")));
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }
}
