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
        Assert.Contains("PowerShell syntax check (Windows PowerShell 5.1)", workflow);
        Assert.Contains("PowerShell syntax check (PowerShell 7)", workflow);
        Assert.Contains("shell: powershell", workflow);
        Assert.Contains("shell: pwsh", workflow);
        Assert.Contains("XAML parse smoke", workflow);
        Assert.Contains("dotnet restore src/LibreSpot.Desktop/LibreSpot.Desktop.csproj --locked-mode -p:AuditPipeline=true", workflow);
        Assert.Contains("dotnet restore tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj --locked-mode --no-dependencies -p:AuditPipeline=true", workflow);
        Assert.Contains("dotnet test tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj -c Release --nologo --no-restore", workflow);
        Assert.Contains("NUGET_AUDIT_LEVEL: moderate", workflow);
        Assert.Contains("dotnet list $project package --vulnerable --include-transitive --format json --output-version 1 --no-restore", workflow);
        Assert.Contains("or-higher finding(s)", workflow);
    }

    [Fact]
    public void DirectoryBuildProps_EnablesLockedNuGetAuditPolicy()
    {
        var props = ReadRepoFile("Directory.Build.props");

        Assert.Contains("<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>", props);
        Assert.Contains("<NuGetAudit>true</NuGetAudit>", props);
        Assert.Contains("<NuGetAuditMode>all</NuGetAuditMode>", props);
        Assert.Contains("<NuGetAuditLevel>moderate</NuGetAuditLevel>", props);
        Assert.Contains("NU1902;NU1903;NU1904", props);
        Assert.Contains("AuditPipeline", props);
        Assert.Contains("NuGetAuditSuppress", props);
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
