using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class DependencyAutomationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void Dependabot_CoversRuntimeAndTestNuGetProjects()
    {
        var config = ReadRepoFile(".github", "dependabot.yml");

        Assert.Contains("package-ecosystem: \"nuget\"", config);
        Assert.Contains("- \"/src/LibreSpot.Desktop\"", config);
        Assert.Contains("- \"/tests/LibreSpot.Desktop.Tests\"", config);
        Assert.Contains("interval: \"monthly\"", config);
        Assert.Contains("runtime-dependencies:", config);
        Assert.Contains("test-dependencies:", config);
        Assert.Contains("- \"Serilog*\"", config);
        Assert.Contains("- \"Microsoft.NET.Test.Sdk\"", config);
        Assert.Contains("- \"xunit*\"", config);
        Assert.Contains("- \"coverlet.collector\"", config);
    }

    [Fact]
    public void CiWorkflow_RunsReleaseRelevantChecksOnDependencyPullRequests()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yml");

        Assert.Contains("pull_request:", workflow);
        Assert.Contains("dotnet-version: 8.0.x", workflow);
        Assert.Contains("PowerShell syntax check", workflow);
        Assert.Contains("XAML parse smoke", workflow);
        Assert.Contains("dotnet test tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj -c Release --nologo", workflow);
        Assert.Contains("dotnet list $project package --vulnerable --include-transitive", workflow);
        Assert.Contains("has the following vulnerable packages", workflow);
    }

    private static string ReadRepoFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

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
