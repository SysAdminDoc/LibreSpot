using System.Text.RegularExpressions;
using System.Text.Json;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class DependencyAutomationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void DependencyUpdateBots_AreNotConfigured()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot, ".github", "dependabot.yml")));
        Assert.False(File.Exists(Path.Combine(RepoRoot, ".github", "dependabot.yaml")));
        Assert.False(File.Exists(Path.Combine(RepoRoot, "renovate.json")));
        Assert.False(File.Exists(Path.Combine(RepoRoot, ".github", "renovate.json")));
    }

    [Fact]
    public void GitHubActionsWorkflows_AreNotConfigured()
    {
        Assert.Empty(EnumerateWorkflowFiles());
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

        var testProject = ReadRepoFile("tests", "LibreSpot.Desktop.Tests", "LibreSpot.Desktop.Tests.csproj");
        Assert.Contains("<RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>", testProject);
    }

    [Fact]
    public void BuildScripts_CanEmitDependencyHealthReportAndFailUnapprovedDrift()
    {
        var script = ReadRepoFile("Build-Scripts.ps1");

        Assert.Contains("[switch]$DependencyHealth", script);
        Assert.Contains("dependency-health.json", script);
        Assert.Contains("dependency-health-allowlist.json", script);
        Assert.Contains("--outdated', '--include-transitive", script);
        Assert.Contains("--vulnerable', '--include-transitive", script);
        Assert.Contains("outdatedDirectPackages", script);
        Assert.Contains("outdatedTransitivePackages", script);
        Assert.Contains("vulnerablePackages", script);
        Assert.Contains("Direct package drift", script);
        Assert.Contains("Unapproved transitive package drift", script);
        Assert.Contains("AuditPipeline vulnerability", script);
        Assert.Contains("acceptedTransitiveLag", script);
    }

    [Fact]
    public void DependencyHealthAllowlist_DocumentsAcceptedTestOnlyTransitiveLag()
    {
        using var doc = JsonDocument.Parse(ReadRepoFile("schemas", "dependency-health-allowlist.json"));
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        var entries = root.GetProperty("acceptedTransitiveLag").EnumerateArray().ToArray();
        Assert.NotEmpty(entries);

        var packageIds = entries
            .Select(entry => entry.GetProperty("packageId").GetString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in new[]
                 {
                     "FSharp.Core",
                     "Microsoft.Win32.SystemEvents",
                     "Newtonsoft.Json",
                     "System.CodeDom",
                     "System.Configuration.ConfigurationManager",
                     "System.Diagnostics.EventLog",
                     "System.Diagnostics.PerformanceCounter",
                     "System.Drawing.Common",
                     "System.Management",
                     "System.Security.Cryptography.ProtectedData",
                     "System.Security.Permissions",
                     "System.Windows.Extensions",
                     "xunit.analyzers"
                 })
        {
            Assert.Contains(expected, packageIds);
        }

        foreach (var entry in entries)
        {
            Assert.Equal("test-transitive", entry.GetProperty("scope").GetString());
            Assert.StartsWith("tests/", entry.GetProperty("projectPath").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("owner").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("reason").GetString()));
            Assert.True(DateTime.Parse(entry.GetProperty("recheckDate").GetString()!) >= new DateTime(2026, 9, 1));
        }
    }

    [Fact]
    public void ScorecardBaseline_DocumentsAcceptedSingleMaintainerRisks()
    {
        using var baseline = JsonDocument.Parse(ReadRepoFile("schemas", "scorecard-baseline.json"));
        var root = baseline.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.NotEqual(DateTime.MinValue, DateTime.Parse(root.GetProperty("lastUpdated").GetString()!));

        var floors = root.GetProperty("checkFloors");
        Assert.Equal(10, floors.GetProperty("Dangerous-Workflow").GetInt32());
        Assert.Equal(10, floors.GetProperty("Dependency-Update-Tool").GetInt32());
        Assert.True(floors.GetProperty("Pinned-Dependencies").GetInt32() >= 8);
        Assert.True(floors.GetProperty("SAST").GetInt32() >= 5);
        Assert.True(floors.GetProperty("Token-Permissions").GetInt32() >= 8);

        var acceptedRisks = root.GetProperty("acceptedRisks").EnumerateArray().ToArray();
        var acceptedChecks = acceptedRisks
            .Select(risk => risk.GetProperty("check").GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expected in new[]
                 {
                     "Branch-Protection",
                     "Code-Review",
                     "Contributors",
                     "CII-Best-Practices",
                     "Fuzzing",
                     "Signed-Releases",
                     "Packaging"
                 })
        {
            Assert.Contains(expected, acceptedChecks);
        }

        foreach (var risk in acceptedRisks)
        {
            Assert.False(string.IsNullOrWhiteSpace(risk.GetProperty("reason").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(risk.GetProperty("revisitWhen").GetString()));
        }
    }

    [Fact]
    public void SpicetifyV3CompatibilityGate_PinnedVersionIsV2()
    {
        var script = ReadRepoFile("LibreSpot.ps1");
        var match = Regex.Match(script, @"\$global:PinnedReleases\s*=\s*@\{.*?SpicetifyCLI\s*=\s*@\{[^}]*Version\s*=\s*'([^']+)'",
            RegexOptions.Singleline);
        Assert.True(match.Success, "Could not find SpicetifyCLI version in pinned releases.");
        var version = Version.Parse(match.Groups[1].Value);
        Assert.True(version.Major == 2,
            $"Pinned Spicetify CLI is v{version} — if v3 has shipped, LibreSpot's " +
            "extension sync, theme injection, Marketplace install, and watcher code " +
            "need a compatibility audit before this pin is updated. See spicetify/cli#3038.");
    }

    private static string ReadRepoFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

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
