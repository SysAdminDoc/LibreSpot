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
        var recommended = ReadView("recommended-workspace-view.xaml");
        var custom = ReadView("custom-workspace-view.xaml");
        var maintenance = ReadView("maintenance-workspace-view.xaml");

        Assert.Contains("AutomationProperties.AutomationId=\"RecommendedWorkspace\"", recommended);
        Assert.Contains("AutomationProperties.AutomationId=\"CustomWorkspace\"", custom);
        Assert.Contains("AutomationProperties.AutomationId=\"MaintenanceWorkspace\"", maintenance);
        Assert.Contains("x:Name=\"CustomPatchesTextEditor\"", custom);
        Assert.Contains("x:Name=\"ProfileQaSurface\"", custom);
        Assert.Contains("x:Name=\"CompatibilityVerdictQaSurface\"", maintenance);
        Assert.Contains("AutomationProperties.AutomationId=\"CompatibilityVerdictMatrix\"", maintenance);
        Assert.Contains("x:Name=\"SupportBundleQaSurface\"", maintenance);
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
