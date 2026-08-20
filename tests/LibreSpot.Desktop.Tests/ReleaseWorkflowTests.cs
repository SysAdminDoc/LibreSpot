using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ReleaseWorkflowTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private const string ReleaseTagPattern = @"^v\d+\.\d+\.\d+(-(preview|rc)\.\d+)?$";

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

    [Fact]
    public void ReleaseDocs_DescribeLocalProcedureAndNotRetiredAutomation()
    {
        var docs = new[]
        {
            Path.Combine(RepoRoot, "README.md"),
            Path.Combine(RepoRoot, "SECURITY.md"),
            Path.Combine(RepoRoot, "CHANGELOG.md"),
            Path.Combine(RepoRoot, "Roadmap_Blocked.md"),
            Path.Combine(RepoRoot, ".gitignore")
        };

        foreach (var path in docs)
        {
            var content = File.ReadAllText(path);
            Assert.DoesNotContain(".github/workflows/release.yml", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".github/workflows/scorecard.yml", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("under `packaging/`", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".\\packaging\\", content, StringComparison.OrdinalIgnoreCase);
        }

        var readme = File.ReadAllText(Path.Combine(RepoRoot, "README.md"));
        Assert.Contains("Local release procedure", readme, StringComparison.Ordinal);
        Assert.Contains("-GenerateReleaseManifest", readme, StringComparison.Ordinal);
        Assert.Contains("gh release verify-asset", readme, StringComparison.Ordinal);
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
