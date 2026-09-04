using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using LibreSpot.Desktop.ViewModels;
using Xunit;

namespace LibreSpot.Desktop.Tests;

// Applies each supported culture to LocalizationService.Current, which writes
// Strings.Culture and CultureInfo.DefaultThreadCurrentUICulture for the whole
// process. Anything asserting English UI text while that loop is mid-flight
// reads Spanish, Russian or Chinese instead, so this cannot run in parallel.
[Collection("Localization")]
public sealed class MainViewModelReleaseNoticeTests
{
    private static readonly string DesktopDigest = $"sha256:{new string('a', 64)}";

    [Fact]
    public Task Home_ShowsTheQuietLinkOnlyWhenANewerStableReleaseExists() => RunStaAsync(async () =>
    {
        using var fixture = new ShellFixture();
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ReleaseNotice>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var viewModel = fixture.Create(async (version, token) =>
        {
            Assert.Matches(@"^\d+\.\d+\.\d+", version);
            probeStarted.SetResult();
            return await release.Task.WaitAsync(token);
        });

        var initialize = viewModel.InitializeAsync();
        await probeStarted.Task;
        await initialize;

        // Snapshot load finished while the release probe was still pending.
        Assert.False(viewModel.IsSnapshotLoading);
        Assert.False(viewModel.HasLibreSpotUpdateNotice);
        Assert.Equal(string.Empty, viewModel.LibreSpotUpdateNoticeText);
        Assert.False(viewModel.OpenLibreSpotUpdateCommand.CanExecute(null));
        var actionBefore = viewModel.HomeAction.ActionId;

        release.SetResult(new ReleaseNotice(
            true,
            "9.9.9",
            "https://github.com/SysAdminDoc/LibreSpot/releases/tag/v9.9.9",
            "live",
            "test",
            DesktopDigest));
        await viewModel.LibreSpotUpdateCheck;

        Assert.True(viewModel.HasLibreSpotUpdateNotice);
        Assert.Contains("9.9.9", viewModel.LibreSpotUpdateNoticeText, StringComparison.Ordinal);
        Assert.Equal("Update LibreSpot", viewModel.LibreSpotUpdateNoticeLinkLabel);
        Assert.Contains("9.9.9", viewModel.LibreSpotUpdateNoticeAutomationName, StringComparison.Ordinal);
        Assert.True(viewModel.HasLibreSpotUpdateVerification);
        Assert.Equal(DesktopDigest, viewModel.LibreSpotUpdateDigest);
        Assert.Equal(
            "gh release verify-asset -R SysAdminDoc/LibreSpot v9.9.9 .\\LibreSpot-Desktop.exe",
            viewModel.LibreSpotUpdateVerificationCommandText);
        Assert.True(viewModel.CopyLibreSpotUpdateVerificationCommand.CanExecute(null));
        Assert.True(viewModel.OpenLibreSpotUpdateCommand.CanExecute(null));
        Assert.Equal(actionBefore, viewModel.HomeAction.ActionId);
        Assert.False(viewModel.IsPromptVisible);
    });

    [Fact]
    public Task Home_StaysSilentWhenTheProbeIsSilentOrThrows() => RunStaAsync(async () =>
    {
        using var fixture = new ShellFixture();
        using var silent = fixture.Create((_, _) => Task.FromResult(ReleaseNotice.Silent("offline", "test")));
        await silent.InitializeAsync();
        await silent.LibreSpotUpdateCheck;
        Assert.False(silent.HasLibreSpotUpdateNotice);
        Assert.False(silent.HasLibreSpotUpdateVerification);

        using var throwing = fixture.Create((_, _) => throw new InvalidOperationException("boom"));
        await throwing.InitializeAsync();
        await throwing.LibreSpotUpdateCheck;
        Assert.False(throwing.HasLibreSpotUpdateNotice);

        using var absent = fixture.Create(null);
        await absent.InitializeAsync();
        await absent.LibreSpotUpdateCheck;
        Assert.False(absent.HasLibreSpotUpdateNotice);
        Assert.False(absent.CopyLibreSpotUpdateVerificationCommand.CanExecute(null));
    });

    [Fact]
    public Task Home_NoticeFollowsTheSelectedLanguage() => RunStaAsync(async () =>
    {
        using var fixture = new ShellFixture();
        using var viewModel = fixture.Create((_, _) => Task.FromResult(new ReleaseNotice(true, "9.9.9", null, "live", "test")));
        await viewModel.InitializeAsync();
        await viewModel.LibreSpotUpdateCheck;
        Assert.False(viewModel.HasLibreSpotUpdateVerification);
        Assert.Equal(string.Empty, viewModel.LibreSpotUpdateDigest);
        Assert.Equal(string.Empty, viewModel.LibreSpotUpdateVerificationCommandText);

        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var option in LocalizationService.SupportedCultures)
            {
                LocalizationService.Current.ApplyCulture(option.CultureName);
                var text = viewModel.LibreSpotUpdateNoticeText;
                Assert.Contains("9.9.9", text, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(viewModel.LibreSpotUpdateNoticeLinkLabel));
                seen[option.CultureName] = text + "|" + viewModel.LibreSpotUpdateNoticeLinkLabel;
            }
        }
        finally
        {
            LocalizationService.Current.ApplyCulture(LocalizationService.DefaultCultureName);
        }

        Assert.Equal(LocalizationService.SupportedCultures.Count, seen.Values.Distinct(StringComparer.Ordinal).Count());
    });

    [Fact]
    public void EveryLocaleCarriesTheNoticeStrings()
    {
        var propertiesDirectory = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties");
        foreach (var file in Directory.EnumerateFiles(propertiesDirectory, "Strings*.resx"))
        {
            var document = XDocument.Load(file);
            foreach (var key in new[]
                     {
                         "Vm_LibreSpotUpdateNoticeFormat",
                         "Vm_LibreSpotUpdateNoticeLink",
                         "Vm_LibreSpotUpdateVerificationDisclosure",
                         "Vm_LibreSpotUpdateVerificationDisclosureHint",
                         "Vm_LibreSpotUpdateDigestLabel",
                         "Vm_LibreSpotUpdateDigestAutomationName",
                         "Vm_LibreSpotUpdateVerifyCommandLabel",
                         "Vm_LibreSpotUpdateVerifyCommandAutomationName",
                         "Vm_LibreSpotUpdateVerifyCommandHint",
                         "Vm_LibreSpotUpdateCopyCommand",
                         "Vm_LibreSpotUpdateCopyCommandName",
                         "Vm_LibreSpotUpdateCopyCommandHint",
                         "Vm_LibreSpotUpdateVerifyCopied",
                         "Vm_LibreSpotUpdateVerifyClipboardUnavailable"
                     })
            {
                var value = document.Root!.Elements("data")
                    .Single(element => (string?)element.Attribute("name") == key)
                    .Element("value")!.Value;
                Assert.False(string.IsNullOrWhiteSpace(value), $"{Path.GetFileName(file)} has an empty {key}.");
                if (key.EndsWith("Format", StringComparison.Ordinal))
                {
                    Assert.Contains("{0}", value, StringComparison.Ordinal);
                }
            }
        }
    }

    [Fact]
    public void HomeView_KeepsTheNoticeSeparateFromThePrimaryAction()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Views", "RecommendedWorkspaceView.xaml"));

        var primary = xaml.IndexOf("HomePrimaryActionButton", StringComparison.Ordinal);
        var notice = xaml.IndexOf("HomeUpdateNotice", StringComparison.Ordinal);
        Assert.True(primary >= 0 && notice > primary, "The update notice must sit below the primary action, never replace it.");
        Assert.Contains("Visibility=\"{Binding HasLibreSpotUpdateNotice", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenLibreSpotUpdateCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"HomeUpdateLibreSpotLink""[\s\S]{0,400}AutomationProperties\.Name=""\{Binding LibreSpotUpdateNoticeLinkLabel\}"""), xaml);
        Assert.Contains("AutomationProperties.Name=\"{Binding LibreSpotUpdateNoticeAutomationName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding HasLibreSpotUpdateVerification", xaml, StringComparison.Ordinal);
        Assert.Contains("IsExpanded=\"{Binding IsLibreSpotUpdateVerificationExpanded, Mode=TwoWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"HomeUpdateVerificationDisclosure\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"HomeUpdateVerifyCommandText\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding LibreSpotUpdateVerificationCommandText, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"HomeUpdateCopyVerifyCommandButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CopyLibreSpotUpdateVerificationCommand}\"", xaml, StringComparison.Ordinal);
    }

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
            _root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", "release-notice-vm", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_root, "LibreSpot"));
        }

        public MainViewModel Create(Func<string, CancellationToken, Task<ReleaseNotice>>? releaseNoticeProbe)
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
                new SupportBundleService(configDirectory, Path.Combine(_root, "logs"), Path.Combine(_root, "crashes")),
                releaseNoticeProbe: releaseNoticeProbe);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }
}
