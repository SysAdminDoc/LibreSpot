using System.Xml.Linq;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class WorkspaceViewCompositionTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void MainWindow_ComposesTheThreeActualWorkspaceTabsAsViews()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "MainWindow.xaml"));
        var tabs = document.Descendants()
            .Where(element => element.Name.LocalName == "TabItem")
            .Where(element =>
            {
                var viewName = element.Elements().Single().Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name")?.Value;
                return viewName is "RecommendedWorkspaceView" or "CustomWorkspaceView" or "MaintenanceWorkspaceView";
            })
            .ToArray();

        Assert.Equal(
            new[] { "RecommendedWorkspaceView", "CustomWorkspaceView", "MaintenanceWorkspaceView" },
            tabs.Select(tab => tab.Elements().Single().Name.LocalName));
    }

    [Fact]
    public void WorkspaceViews_PreserveAutomationAndCodeBehindSurfaces()
    {
        var recommended = ReadView("RecommendedWorkspaceView.xaml");
        var custom = string.Join(
            Environment.NewLine,
            new[]
            {
                "CustomWorkspaceView.xaml",
                "CustomInstallSection.xaml",
                "CustomAppearanceSection.xaml",
                "CustomBehaviorSection.xaml",
                "CustomAdvancedSection.xaml",
                "CustomPatchesSection.xaml",
                "CustomBuiltInExtensionsSection.xaml",
                "CustomAppsSection.xaml",
                "CustomProfileSummarySection.xaml"
            }.Select(ReadView));
        var maintenance = ReadView("MaintenanceWorkspaceView.xaml");

        Assert.Contains("AutomationProperties.AutomationId=\"RecommendedWorkspace\"", recommended);
        Assert.Contains("AutomationProperties.AutomationId=\"CustomWorkspace\"", custom);
        Assert.Contains("AutomationProperties.AutomationId=\"MaintenanceWorkspace\"", maintenance);
        Assert.Contains("x:Name=\"CustomPatchesTextEditor\"", custom);
        Assert.Contains("x:Name=\"ProfileQaSurface\"", custom);
        Assert.Contains("<views:CustomInstallSection", custom);
        Assert.Contains("<views:CustomProfileSummarySection", custom);
        Assert.Contains("x:Name=\"CompatibilityVerdictQaSurface\"", maintenance);
        Assert.Contains("AutomationProperties.AutomationId=\"CompatibilityVerdictMatrix\"", maintenance);
        Assert.Contains("x:Name=\"SupportBundleQaSurface\"", maintenance);
    }

    [Fact]
    public void RecommendedWorkspace_ExplainsFirstRunWithoutReplacingEnvironmentStates()
    {
        var recommended = ReadView("RecommendedWorkspaceView.xaml");

        Assert.Contains("AutomationProperties.AutomationId=\"RecommendedFirstRunNarrative\"", recommended);
        Assert.Contains("AutomationProperties.Name=\"{services:Loc Ui_RecommendedFirstRunChecklist}\"", recommended);
        Assert.Contains("{services:Loc Vm_RecommendedFirstRunInstall}", recommended);
        Assert.Contains("{services:Loc Vm_RecommendedFirstRunUpdates}", recommended);
        Assert.Contains("{services:Loc Vm_RecommendedFirstRunReversible}", recommended);
        Assert.Contains("{services:Loc Vm_RecommendedFirstRunRisk}", recommended);
        Assert.Contains("{services:Loc Vm_SimpleHomeDuration}", recommended);
        Assert.Contains("{Binding HomeAction.Title}", recommended);
        Assert.Contains("{Binding HomeAction.Body}", recommended);
        Assert.Contains("{Binding HomeAction.PrimaryLabel}", recommended);
        Assert.Contains("{Binding HomeAction.IsEnabled}", recommended);
        Assert.Contains("{Binding HomeAction.Command}", recommended);
        Assert.Contains("AutomationProperties.Name=\"{Binding HomeAction.AutomationName}\"", recommended);
        Assert.Contains("AutomationProperties.HelpText=\"{Binding HomeAction.HelpText}\"", recommended);
        Assert.Contains("AutomationProperties.AutomationId=\"RecommendedDetailsExpander\"", recommended);
        Assert.Contains("{Binding RecommendedFollowUpText}", recommended);
        Assert.Contains("ItemsSource=\"{Binding ShellEnvironmentRows}\"", recommended);
        Assert.Contains("ItemsSource=\"{Binding ShellDependencyRows}\"", recommended);
    }

    [Fact]
    public void CustomWorkspace_UsesNamedSectionControlsAndKeepsTheViewModelSatelliteSmall()
    {
        var custom = ReadView("CustomWorkspaceView.xaml");
        foreach (var section in new[]
                 {
                     "CustomInstallSection",
                     "CustomAppearanceSection",
                     "CustomBehaviorSection",
                     "CustomAdvancedSection",
                     "CustomPatchesSection",
                     "CustomBuiltInExtensionsSection",
                     "CustomAppsSection",
                     "CustomProfileSummarySection"
                 })
        {
            Assert.Contains($"<views:{section}", custom);
        }

        var mainViewModel = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "MainViewModel.cs");
        Assert.True(mainViewModel.Split('\n').Length < 3000);
    }

    [Fact]
    public void MainWindow_UsesOneWorkspaceVocabularyForNavigation()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "MainWindow.xaml"));
        var simpleShellStart = xaml.IndexOf("x:Name=\"SimpleShellHost\"", StringComparison.Ordinal);
        var legacyShellStart = xaml.IndexOf("x:Name=\"ShellWorkspaceHost\"", StringComparison.Ordinal);
        Assert.True(simpleShellStart >= 0 && legacyShellStart > simpleShellStart);
        var simpleShell = xaml[simpleShellStart..legacyShellStart];

        Assert.Contains("services:Loc NavHome", simpleShell);
        Assert.Contains("services:Loc NavSettings", simpleShell);
        Assert.Contains("services:Loc ModeMaintenanceTitle", simpleShell);
        Assert.DoesNotContain("services:Loc ModeRecommendedTitle", simpleShell);
        Assert.DoesNotContain("services:Loc ModeCustomTitle", simpleShell);
    }

    [Fact]
    public void MainWindow_ShowsTheShellVersionInsideTheReachableShell()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "MainWindow.xaml"));
        var simpleShellStart = xaml.IndexOf("x:Name=\"SimpleShellHost\"", StringComparison.Ordinal);
        var legacyShellStart = xaml.IndexOf("x:Name=\"ShellWorkspaceHost\"", StringComparison.Ordinal);
        Assert.True(simpleShellStart >= 0 && legacyShellStart > simpleShellStart);
        var simpleShell = xaml[simpleShellStart..legacyShellStart];

        // The legacy shell is permanently collapsed, so a ShellDisplayVersion binding
        // that only lives there leaves users with no way to read the version they are
        // asked for in the bug-report template.
        Assert.Contains("{Binding ShellDisplayVersion}", simpleShell);
        Assert.Contains("SimpleShellVersionLabel", simpleShell);
    }

    private static string ReadView(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Views", fileName));

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
