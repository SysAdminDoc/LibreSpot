using System.Text.RegularExpressions;
using System.Text.Json;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class DependencyAutomationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly Regex WorkflowUsesPattern = new(
        @"^\s*uses:\s*(?<target>[^\s#]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ShaPinnedActionPattern = new(
        @"^[^@]+@[0-9a-f]{40}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionCommentPattern = new(
        @"\bv\d+(\.\d+){0,2}\b",
        RegexOptions.Compiled);

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
        Assert.Contains("package-ecosystem: \"github-actions\"", config);
        Assert.Contains("directory: \"/\"", config);
        Assert.Contains("workflow-actions-minor:", config);
        Assert.Contains("workflow-actions-major:", config);
        Assert.Contains("- \"github-actions\"", config);
    }

    [Fact]
    public void CiWorkflow_RunsReleaseRelevantChecksOnDependencyPullRequests()
    {
        var workflow = ReadRepoFile(".github", "workflows", "ci.yml");

        Assert.Contains("pull_request:", workflow);
        Assert.Contains("dotnet-version: 10.0.x", workflow);
        Assert.Contains("PowerShell syntax check (Windows PowerShell 5.1)", workflow);
        Assert.Contains("PowerShell syntax check (PowerShell 7)", workflow);
        Assert.Contains("shell: powershell", workflow);
        Assert.Contains("shell: pwsh", workflow);
        Assert.Contains("XAML parse smoke", workflow);
        Assert.Contains("dotnet restore src/LibreSpot.Desktop/LibreSpot.Desktop.csproj --locked-mode -p:AuditPipeline=true", workflow);
        Assert.Contains("dotnet restore tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj --no-dependencies -p:AuditPipeline=true", workflow);
        Assert.Contains("dotnet test tests/LibreSpot.Desktop.Tests/LibreSpot.Desktop.Tests.csproj -c Release --nologo --no-restore --logger", workflow);
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

        var testProject = ReadRepoFile("tests", "LibreSpot.Desktop.Tests", "LibreSpot.Desktop.Tests.csproj");
        Assert.Contains("<RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>", testProject);
    }

    [Fact]
    public void Workflows_PinRemoteActionsToFullCommitShas()
    {
        var workflowDirectory = Path.Combine(RepoRoot, ".github", "workflows");
        var offenders = new List<string>();

        foreach (var workflowPath in Directory.EnumerateFiles(workflowDirectory, "*.yml"))
        {
            var workflow = File.ReadAllText(workflowPath);
            foreach (Match match in WorkflowUsesPattern.Matches(workflow))
            {
                var target = match.Groups["target"].Value.Trim('\'', '"');
                if (target.StartsWith("./", StringComparison.Ordinal) ||
                    target.StartsWith("../", StringComparison.Ordinal))
                {
                    continue;
                }

                var lineNumber = workflow[..match.Index].Count(c => c == '\n') + 1;
                var relativePath = Path.GetRelativePath(RepoRoot, workflowPath);
                if (!ShaPinnedActionPattern.IsMatch(target))
                {
                    offenders.Add($"{relativePath}:{lineNumber} uses {target}; pin remote actions to a full 40-character commit SHA.");
                    continue;
                }

                var actionName = target.Split('@', 2)[0];
                var versionComment = PreviousNonEmptyLine(workflow, match.Index);
                if (!versionComment.StartsWith("# ", StringComparison.Ordinal) ||
                    !versionComment.Contains(actionName, StringComparison.Ordinal) ||
                    !VersionCommentPattern.IsMatch(versionComment))
                {
                    offenders.Add($"{relativePath}:{lineNumber} pins {target} but is missing a preceding '# {actionName} v...' version comment.");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void ScorecardWorkflow_RunsOnScheduleAndPublishesResults()
    {
        var workflow = ReadRepoFile(".github", "workflows", "scorecard.yml");

        // Scheduled + push(main) coverage; PR triggers are intentionally absent
        // because Scorecard's publish/OIDC steps need repo-scoped tokens.
        Assert.Contains("schedule:", workflow);
        Assert.Contains("cron:", workflow);
        Assert.Contains("branches: [ \"main\" ]", workflow);
        // Least-privilege top-level token.
        Assert.Contains("permissions: read-all", workflow);
        // Publishes to the OSSF API so the README badge resolves.
        Assert.Contains("ossf/scorecard-action@", workflow);
        Assert.Contains("publish_results: true", workflow);
        Assert.Contains("id-token: write", workflow);
        Assert.Contains("Run Scorecard analysis (JSON)", workflow);
        Assert.Contains("results_format: json", workflow);
        Assert.Contains("schemas/scorecard-baseline.json", workflow);
        Assert.Contains("scorecard-triage.json", workflow);
        Assert.Contains("Unaccepted regressions:", workflow);
        Assert.Contains("exit 1", workflow);
        Assert.Contains("if: ${{ always() }}", workflow);
        Assert.DoesNotContain("regressions=$((regressions + 1))", workflow);
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

    private static string PreviousNonEmptyLine(string content, int index)
    {
        var prefix = content[..index].Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = prefix.Split('\n');
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.Length > 0)
            {
                return line;
            }
        }

        return string.Empty;
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
