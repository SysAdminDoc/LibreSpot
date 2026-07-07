using System.Windows.Media.Imaging;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CommunitySharingExperienceTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void QrCodeImageService_CreatesFrozenBitmapForProfileShareUri()
    {
        RunSta(() =>
        {
            var image = QrCodeImageService.CreateImage("librespot://profile?data=eyJzY2hlbWEiOiJsaWJyZXNwb3QifQ");
            var bitmap = Assert.IsType<BitmapImage>(image);

            Assert.True(bitmap.IsFrozen, "QR images must be frozen before binding across WPF threads.");
            Assert.True(bitmap.PixelWidth > 0, "QR image must have renderable width.");
            Assert.True(bitmap.PixelHeight > 0, "QR image must have renderable height.");
        });
    }

    [Fact]
    public void ChangelogPreviewService_LoadsNewestSectionHighlights()
    {
        var highlights = ChangelogPreviewService.LoadUnreleasedHighlights();

        Assert.NotEmpty(highlights);
        Assert.DoesNotContain(highlights, item => item.Contains("not embedded", StringComparison.OrdinalIgnoreCase));
        // The preview reads the newest changelog section's bullets, so it must
        // never surface the empty-section placeholder and every highlight must
        // be real changelog copy (non-trivial length).
        Assert.DoesNotContain(highlights, item => item.Contains("No changelog entries", StringComparison.OrdinalIgnoreCase));
        Assert.All(highlights, item => Assert.True(item.Length > 8));
    }

    [Fact]
    public void WpfShell_ExposesCommunitySharingSurface()
    {
        var xaml = ReadRepoFile("src", "LibreSpot.Desktop", "MainWindow.xaml");

        Assert.Contains("Ui_ProfileShareCard", xaml);
        Assert.Contains("SelectedProfileQrImage", xaml);
        Assert.Contains("SelectedProfileShareUri", xaml);
        Assert.Contains("CopyProfileShareUriCommand", xaml);
        Assert.Contains("SelectedProfileComparisonText", xaml);
        Assert.Contains("CopyProfileComparisonCommand", xaml);
        Assert.Contains("ChangelogHighlights", xaml);
        Assert.Contains("OpenRepositoryCommand", xaml);
        Assert.Contains("OpenSpicetifyCommunityCommand", xaml);
        Assert.Contains("OpenThemeCatalogCommand", xaml);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray()));

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root.");
    }
}
