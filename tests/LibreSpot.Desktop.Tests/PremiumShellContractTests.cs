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

        Assert.Contains("ShellReadinessChecks", xaml);
        Assert.Contains("ShellReadinessCheckItemTemplate", xaml);
        Assert.DoesNotContain("ShellCheckStatusLabel", xaml);
        Assert.Contains("checks.Count(check => check.IsPassing)", viewModel);
        Assert.Contains("CycleShellLogFilterCommand", xaml);
        Assert.Contains("ShellLogFilterHint", xaml);
        Assert.Contains("ShellClearLogHint", xaml);
        Assert.Contains("ShowShellActivityEmptyState", xaml);
        Assert.DoesNotContain("DateTime.Now.AddSeconds(-4)", viewModel);
        Assert.Contains("TokenKind: \"ScheduledTask\"", viewModel);
    }

    [Fact]
    public void LiveRegionsExposeChangingContentAndPromptsConstrainLongCopy()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var liveRegion = ReadFile("src", "LibreSpot.Desktop", "Controls", "LiveRegionContentControl.cs");

        Assert.Contains("Content=\"{Binding ActivityLiveAnnouncement}\"", xaml);
        Assert.Contains("AutomationProperties.HelpText=\"{services:Loc RunStatus}\"", xaml);
        Assert.DoesNotContain("AutomationProperties.Name=\"{services:Loc RunStatus}\"", xaml);
        Assert.Contains("owner.Content?.ToString()", liveRegion);
        Assert.Contains("protected override string GetNameCore()", liveRegion);
        Assert.Contains("x:Name=\"PromptDialogRoot\"", xaml);
        Assert.Contains("MaxHeight=\"660\"", xaml);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml);
        Assert.Contains("Text=\"{Binding PromptTitle}\"", xaml);
        Assert.Contains("Text=\"{Binding PromptBody}\"", xaml);
    }

    [Fact]
    public void StartupFailuresBecomeRetryableShellState()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var codeBehind = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");
        var viewModel = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("_viewModel.ApplyInitializationFailure();", codeBehind);
        Assert.Contains("public void ApplyInitializationFailure()", viewModel);
        Assert.Contains("SetSnapshotQueryState(isLoading: false, loadFailed: true)", viewModel);
        Assert.Contains("AutomationProperties.AutomationId=\"InspectorRetryEnvironmentButton\"", xaml);
        Assert.Contains("Command=\"{Binding RefreshSnapshotCommand}\"", xaml);
    }

    [Fact]
    public void Shell_HasCompactWorkAreaLayoutAndNonIntrusiveCaptureMode()
    {
        var codeBehind = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");

        Assert.Contains("ConstrainToWorkArea", codeBehind);
        Assert.Contains("ApplyResponsiveShellLayout", codeBehind);
        Assert.Contains("shellWidth < 1520", codeBehind);
        Assert.Contains("PrepareUiAutomationCapture", codeBehind);
        Assert.Contains("Task.Delay(1600)", codeBehind);
        Assert.Contains("DispatcherPriority.Render", codeBehind);
        Assert.DoesNotContain("RenderMode.SoftwareOnly", codeBehind);
        Assert.Contains("--uia-size=", codeBehind);
        Assert.Contains("GetUiAutomationCaptureSize", codeBehind);
        Assert.Contains("if (!_uiAutomationBackgroundMode)", codeBehind);
        Assert.Contains("x:Name=\"ActivityDock\"", xaml);
        Assert.Contains("x:Name=\"ShellInspectorColumn\"", xaml);
    }

    [Fact]
    public void Shell_SeparatesPrimaryNavigationFromQuickLinksAndUsesActionChevrons()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var controls = ReadFile("src", "LibreSpot.Desktop", "Themes", "Controls.xaml");

        Assert.Contains("AutomationProperties.Name=\"{services:Loc NavHome}\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{services:Loc NavSetup}\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"{services:Loc NavUnblock}\"", xaml);
        Assert.Contains("ShellQuickLinkButtonStyle", xaml);
        Assert.Contains("InspectorActionButtonStyle", xaml);
        Assert.Contains("x:Key=\"ShellQuickLinkButtonStyle\"", controls);
        Assert.Contains("x:Key=\"InspectorActionButtonStyle\"", controls);
        Assert.True(xaml.Split(["Data=\"M 1 1 L 6 6 L 1 11\""], StringSplitOptions.None).Length >= 4);
    }

    [Fact]
    public void TrustPanel_ExposesPinnedProvenanceAndFreshnessActions()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var viewModel = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");

        Assert.Contains("ProvenanceItemTemplate", xaml);
        Assert.Contains("ItemsSource=\"{Binding ShellProvenanceItems}\"", xaml);
        Assert.Contains("Text=\"{Binding SourceUrl}\"", xaml);
        Assert.Contains("Text=\"{Binding VerifiedDetail}\"", xaml);
        Assert.Contains("Command=\"{Binding OpenReleaseNotesCommand}\"", xaml);
        Assert.Contains("x:Name=\"InspectorPanel\"", xaml);
        Assert.Contains("x:Name=\"ShellProvenanceItemsControl\"", xaml);
        Assert.Contains("AppCatalog.UpstreamDependencyPins.Select(BuildProvenanceItem)", viewModel);
        Assert.Contains("ProvenanceFreshness.Indeterminate", viewModel);
    }

    [Fact]
    public void SuccessfulRuns_RemainReviewableUntilDismissed()
    {
        var codeBehind = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");

        Assert.DoesNotContain("ExitAfterSuccessfulSetup = true", codeBehind);
    }

    [Fact]
    public void CrashRecoveryWindowUsesSharedThemeLocalizationAndResponsiveScrolling()
    {
        var crashReporter = ReadFile("src", "LibreSpot.Desktop", "Services", "CrashReporter.cs");

        Assert.Contains("ThemeBrush(\"WorkspaceBackdropBrush\"", crashReporter);
        Assert.Contains("ThemeStyle(isPrimary ? \"PrimaryButtonStyle\" : \"SecondaryButtonStyle\")", crashReporter);
        Assert.Contains("Win11ShellIntegration.ApplyMicaAndDarkChrome(dialog)", crashReporter);
        Assert.Contains("L(\"CrashRecoverableTitle\")", crashReporter);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", crashReporter);
        Assert.Contains("AutomationProperties.SetName(button, text)", crashReporter);
        Assert.Contains("OperationCorrelation.CurrentOrLastOperationId", crashReporter);
        Assert.Contains("operation-id:", crashReporter);
        Assert.DoesNotContain("CreateBrush(", crashReporter);
        Assert.DoesNotContain("new BrushConverter", crashReporter);
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
        Assert.Contains("StandardDuration=\"{StaticResource IndeterminateSweepDuration}\"", controls);
        Assert.Contains("PopupAnimation=\"None\"", controls);
        Assert.Contains("Storyboard.TargetProperty=\"ScaleX\"", controls);
        Assert.Contains("Storyboard.TargetProperty=\"ScaleY\"", controls);
        Assert.Contains("To=\"1\"", controls);
    }

    [Fact]
    public void UiAutomationCanRenderTheRealHighContrastPalette()
    {
        var app = ReadFile("src", "LibreSpot.Desktop", "App.xaml.cs");
        var themeManager = ReadFile("src", "LibreSpot.Desktop", "Services", "ThemeManager.cs");

        Assert.Contains("--uia-theme=high-contrast", app);
        Assert.Contains("forceHighContrast: e.Args.Any", app);
        Assert.Contains("forceHighContrast || IsHighContrast", themeManager);
        Assert.Contains("useHighContrast ? HighContrastPaletteSource : PaletteSource", themeManager);
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
