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
    public void ShellStackCardDescribesMaintenanceInsteadOfReleaseFreshness()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var viewModel = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");
        var resources = ReadFile("src", "LibreSpot.Desktop", "Properties", "Strings.resx");

        Assert.Contains("ShellStackStatusTitle", xaml);
        Assert.Contains("ShellStackStatusDetail", xaml);
        Assert.Contains("Vm_ShellStackDetectedTitle", viewModel);
        Assert.Contains("Vm_ShellStackNotDetectedTitle", viewModel);
        Assert.Contains("Vm_ShellStackDetectedDetail", viewModel);
        Assert.Contains("Vm_ShellStackNotDetectedDetail", viewModel);
        Assert.DoesNotContain("ShellUpdateStatus", xaml);
        Assert.DoesNotContain("ShellUpdateStatus", viewModel);
        Assert.DoesNotContain("Vm_ShellUpdate", resources);
    }

    [Fact]
    public void WpfQaMatrixCoversPromptRunningAndReducedMotionStates()
    {
        var matrix = ReadFile("tests", "LibreSpot.Desktop.Tests", "WpfQaMatrixTests.cs");
        var runner = ReadFile("tools", "Invoke-WpfQaMatrix.ps1");
        var app = ReadFile("src", "LibreSpot.Desktop", "App.xaml.cs");
        var themeManager = ReadFile("src", "LibreSpot.Desktop", "Services", "ThemeManager.cs");
        var mainWindow = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");

        Assert.Contains("(\"prompt-destructive\", \"PromptActionReset\", \"PromptCancelButton\")", matrix);
        Assert.Contains("(\"activity-running\", \"Ui_RunState\", \"ActivityCancelRunButton\")", matrix);
        Assert.Contains("(\"reduced-motion\", \"ButtonStartRecommendedSetup\", \"HomePrimaryActionButton\")", matrix);
        Assert.Contains("(\"custom-live\", \"LiveCustomizationTitle\", \"LiveCustomizationFeatureSearch\")", matrix);
        Assert.Contains("--uia-reduced-motion", matrix);
        Assert.Contains("AssertNoUnnamedActionableControls(snapshot)", matrix);
        Assert.Contains("if ($Quick) { 24 } else { 80 }", runner);
        Assert.Contains("forceReducedMotion", app);
        Assert.Contains("_forceReducedMotion", themeManager);
        Assert.Contains("ThemeManager.ShouldSuppressMotion", mainWindow);
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
    public void ActivityDialogFooterKeepsRecoveryGuidanceAboveActions()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var dialogStart = xaml.IndexOf("x:Name=\"ActivityDialogRoot\"", StringComparison.Ordinal);
        var footerStart = xaml.IndexOf("<Grid Grid.Row=\"4\" Margin=\"0,18,0,0\">", dialogStart, StringComparison.Ordinal);
        var promptStart = xaml.IndexOf("<!-- ═══ Prompt overlay ═══ -->", footerStart, StringComparison.Ordinal);

        Assert.True(dialogStart >= 0 && footerStart > dialogStart && promptStart > footerStart);
        var footer = xaml[footerStart..promptStart];
        Assert.Contains("<Grid.RowDefinitions>", footer);
        Assert.Contains("<WrapPanel Grid.Row=\"1\"", footer);
        Assert.Contains("Text=\"{Binding ActivityLogPathText}\"", footer);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", footer);
        Assert.DoesNotContain("<Grid.ColumnDefinitions>", footer);
    }

    [Fact]
    public void ActivityDialogGivesUndoAndRunLogIndependentColumns()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var dialogStart = xaml.IndexOf("x:Name=\"ActivityDialogRoot\"", StringComparison.Ordinal);
        var logStart = xaml.IndexOf("<!-- Log -->", dialogStart, StringComparison.Ordinal);
        var footerStart = xaml.IndexOf("<Grid Grid.Row=\"4\"", logStart, StringComparison.Ordinal);

        Assert.True(dialogStart >= 0 && logStart > dialogStart && footerStart > logStart);
        var log = xaml[logStart..footerStart];
        Assert.Contains("<Grid.ColumnDefinitions>", log);
        Assert.Contains("Width=\"420\"", log);
        Assert.Contains("AutomationProperties.AutomationId=\"ActivityRunLogList\"", log);
        Assert.Contains("MinHeight=\"160\"", log);
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
        Assert.Contains("x:Name=\"SimpleShellRailColumn\"", xaml);
        Assert.Contains("x:Name=\"SimpleWorkspaceSurface\"", xaml);
        Assert.Contains("shellWidth < 1280", codeBehind);
    }

    [Fact]
    public void Shell_UsesAFocusedHomeSurfaceAndKeepsAdvancedChromeHidden()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var recommended = ReadFile("src", "LibreSpot.Desktop", "Views", "RecommendedWorkspaceView.xaml");
        var homeActionSource = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.Maintenance.cs");
        var simpleShell = ExtractSimpleShell(xaml);

        Assert.Contains("x:Name=\"SimpleShellHost\"", xaml);
        Assert.Contains("Visibility=\"Collapsed\"", xaml[xaml.IndexOf("x:Name=\"ShellWorkspaceHost\"", StringComparison.Ordinal)..]);
        Assert.DoesNotContain("GlobalSearchBox", simpleShell);
        Assert.DoesNotContain("ActivityDock", simpleShell);
        Assert.DoesNotContain("InspectorPanel", simpleShell);
        Assert.Contains("{Binding HomeAction.Title}", recommended);
        Assert.Contains("{Binding SimpleHomeReadinessChecks}", recommended);
        Assert.Contains("AutomationProperties.AutomationId=\"HomePrimaryActionButton\"", recommended);
        Assert.Contains("AutomationProperties.Name=\"{Binding HomeAction.AutomationName}\"", recommended);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding HomeAction.HelpText}\"", recommended);
        Assert.Contains("Command=\"{Binding HomeAction.Command}\"", recommended);
        Assert.Contains("AutomationProperties.AutomationId=\"RecommendedDetailsExpander\"", recommended);
        Assert.Contains("Vm_SimpleHomeReadyTitle", homeActionSource);
        Assert.Contains("HomeActionKind.OpenSpotify", homeActionSource);
    }

    [Fact]
    public void Shell_UsesThreePlainLanguageNavigationChoices()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var simpleShell = ExtractSimpleShell(xaml);

        Assert.Equal(3, simpleShell.Split("AutomationProperties.AutomationId=\"WorkspaceNav", StringSplitOptions.None).Length - 1);
        Assert.Contains("AutomationProperties.Name=\"{services:Loc NavHome}\"", simpleShell);
        Assert.Contains("AutomationProperties.Name=\"{services:Loc NavSettings}\"", simpleShell);
        Assert.Contains("AutomationProperties.Name=\"{services:Loc ModeMaintenanceTitle}\"", simpleShell);
        Assert.DoesNotContain("ShellQuickLinkButtonStyle", simpleShell);
        Assert.DoesNotContain("InspectorActionButtonStyle", simpleShell);
    }

    [Fact]
    public void TrustPanel_ExposesPinnedProvenanceAndFreshnessActions()
    {
        var xaml = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");
        var viewModel = string.Join(
            Environment.NewLine,
            new[]
            {
                "MainViewModel.cs",
                "MainViewModel.CustomInstall.cs",
                "MainViewModel.Maintenance.cs",
                "MainViewModel.Profiles.cs"
            }.Select(fileName => ReadFile("src", "LibreSpot.Desktop", "ViewModels", fileName)));

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
        Assert.Contains("ResolveWritableCrashRoot()", crashReporter);
        Assert.Contains("Path.GetTempPath()", crashReporter);
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

    private static string ExtractSimpleShell(string xaml)
    {
        var start = xaml.IndexOf("x:Name=\"SimpleShellHost\"", StringComparison.Ordinal);
        var end = xaml.IndexOf("x:Name=\"ShellWorkspaceHost\"", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return xaml[start..end];
    }

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
