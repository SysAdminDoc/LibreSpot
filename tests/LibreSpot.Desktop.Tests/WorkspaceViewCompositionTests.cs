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
        Assert.Contains("{Binding RecommendedRunDuration}", recommended);
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

        Assert.Contains("services:Loc ModeRecommendedTitle", xaml);
        Assert.Contains("services:Loc ModeCustomTitle", xaml);
        Assert.Contains("services:Loc ModeMaintenanceTitle", xaml);
        Assert.DoesNotContain("services:Loc NavHome", xaml);
        Assert.DoesNotContain("services:Loc NavSetup", xaml);
        Assert.DoesNotContain("services:Loc NavUnblock", xaml);
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
