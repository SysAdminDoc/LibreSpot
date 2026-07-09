using System.IO;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class PremiumShellContractTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void MainWindow_UsesLiveLocalizationAndModalShellBoundary()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");

        Assert.DoesNotContain("x:Static props:Strings", xaml);
        Assert.Contains("services:Loc", xaml);
        Assert.Contains("IsEnabled=\"{Binding IsShellInteractionEnabled}\"", xaml);
    }

    [Fact]
    public void ReadinessAndActivityControlsReflectRealState()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var viewModel = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("ShellCheckStatusLabel", xaml);
        Assert.Contains("CycleShellLogFilterCommand", xaml);
        Assert.Contains("ShowShellActivityEmptyState", xaml);
        Assert.DoesNotContain("DateTime.Now.AddSeconds(-4)", viewModel);
        Assert.Contains("TokenKind: \"ScheduledTask\"", viewModel);
    }

    [Fact]
    public void Shell_HasCompactWorkAreaLayoutAndNonIntrusiveCaptureMode()
    {
        var codeBehind = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");

        Assert.Contains("ConstrainToWorkArea", codeBehind);
        Assert.Contains("ApplyResponsiveShellLayout", codeBehind);
        Assert.Contains("if (!_uiAutomationBackgroundMode)", codeBehind);
        Assert.Contains("x:Name=\"ActivityDock\"", xaml);
        Assert.Contains("x:Name=\"ShellInspectorColumn\"", xaml);
    }

    [Fact]
    public void SuccessfulRuns_RemainReviewableUntilDismissed()
    {
        var codeBehind = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");

        Assert.DoesNotContain("ExitAfterSuccessfulSetup = true", codeBehind);
    }

    [Fact]
    public void Theme_UsesPremiumLayersAndMotionAwareShimmer()
    {
        var palette = ReadFile("src", "LibreSpot.Desktop", "Themes", "Palette.xaml");
        var highContrast = ReadFile("src", "LibreSpot.Desktop", "Themes", "HighContrastPalette.xaml");
        var controls = ReadFile("src", "LibreSpot.Desktop", "Themes", "Controls.xaml");

        foreach (var key in new[] { "WorkspaceBackdropBrush", "RailPanelBrush", "SurfaceCardBrush", "IndeterminateSweepDuration" })
        {
            Assert.Contains($"x:Key=\"{key}\"", palette);
            Assert.Contains($"x:Key=\"{key}\"", highContrast);
        }

        Assert.Contains("CardListBoxItemStyle", controls);
        Assert.Contains("TitleBarCloseButtonStyle", controls);
        Assert.Contains("Duration=\"{StaticResource IndeterminateSweepDuration}\"", controls);
    }

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray()));

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not resolve the LibreSpot repository root.");
    }
}
