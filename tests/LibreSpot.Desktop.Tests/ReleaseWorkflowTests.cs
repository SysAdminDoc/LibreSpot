using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ReleaseWorkflowTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private const string ReleaseTagPattern = @"^v\d+\.\d+\.\d+(-(preview|rc)\.\d+)?$";

    [Fact]
    public void GitHubActionsWorkflows_AreNotTrackedForBuildsOrReleases()
    {
        Assert.Empty(EnumerateWorkflowFiles());
    }

    [Fact]
    public void ReleaseNotesConfig_IsRepositoryMetadataOnly()
    {
        var releaseNotesConfig = Path.Combine(RepoRoot, ".github", "release.yml");

        Assert.True(File.Exists(releaseNotesConfig), ".github/release.yml should remain available for GitHub release-note labels.");

        var config = File.ReadAllText(releaseNotesConfig);
        Assert.DoesNotContain("runs-on:", config, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("steps:", config, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("actions/checkout", config, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("v3.7.2", false)]
    [InlineData("v4.0.0-preview.6", true)]
    [InlineData("v4.0.0-rc.1", true)]
    public void ReleaseTagPattern_ClassifiesStableAndPrereleaseTags(string tag, bool expectedPrerelease)
    {
        Assert.Matches(ReleaseTagPattern, tag);

        var version = tag[1..];
        var isPrerelease = Regex.IsMatch(version, @"-(preview|rc)\.\d+$");
        Assert.Equal(expectedPrerelease, isPrerelease);
    }

    [Theory]
    [InlineData("v3.7")]
    [InlineData("v3.7.2-beta.1")]
    [InlineData("3.7.2")]
    [InlineData("v3.7.2-preview")]
    [InlineData("release-v3.7.2")]
    public void ReleaseTagPattern_RejectsMalformedTags(string tag)
    {
        Assert.DoesNotMatch(ReleaseTagPattern, tag);
    }

    private static IEnumerable<string> EnumerateWorkflowFiles()
    {
        var workflowDirectory = Path.Combine(RepoRoot, ".github", "workflows");
        if (!Directory.Exists(workflowDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(workflowDirectory, "*.yml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(workflowDirectory, "*.yaml", SearchOption.AllDirectories))
            .ToArray();
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from the test runner.");
    }
}
