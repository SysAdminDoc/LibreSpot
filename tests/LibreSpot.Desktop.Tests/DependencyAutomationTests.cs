using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class DependencyAutomationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    // How a test actually starts the shell: the published executable next to
    // the test binaries, handed to Process.Start or a FlaUI Application.Launch.
    private static readonly Regex ShellLaunchPattern = new(
        @"AppContext\.BaseDirectory\s*,\s*""LibreSpot\.exe""|Process\.Start\s*\(\s*[^)]*LibreSpot\.exe|Application\.Launch",
        RegexOptions.Compiled);

    // Only public classes: xunit does not discover private nested helpers such
    // as the SmokeApp process wrappers, so the class filter never sees them.
    private static readonly Regex ClassDeclarationPattern = new(
        @"\bpublic\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void DependencyUpdateBots_AreNotConfigured()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot, ".github", "dependabot.yml")));
        Assert.False(File.Exists(Path.Combine(RepoRoot, ".github", "dependabot.yaml")));
        Assert.False(File.Exists(Path.Combine(RepoRoot, "renovate.json")));
        Assert.False(File.Exists(Path.Combine(RepoRoot, ".github", "renovate.json")));

        var releaseNotesConfig = ReadRepoFile(".github", "release.yml");
        Assert.DoesNotContain("dependabot", releaseNotesConfig, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalQualityGates_AreDocumentedAndNoWorkflowsAreTracked()
    {
        var readme = ReadRepoFile("README.md");
        Assert.Contains("Local release procedure", readme, StringComparison.Ordinal);
        Assert.Contains("Build-Scripts.ps1 -Validate", readme, StringComparison.Ordinal);
        Assert.Contains("Build-Scripts.ps1 -Lint", readme, StringComparison.Ordinal);
        Assert.Contains("Build-Scripts.ps1 -DependencyHealth", readme, StringComparison.Ordinal);
        Assert.Contains("--filter-not-class \"*Wpf*\"", readme, StringComparison.Ordinal);
        Assert.Contains("Invoke-Pester", readme, StringComparison.Ordinal);
        Assert.Contains("gh release verify-asset", readme, StringComparison.Ordinal);

        var workflowDirectory = Path.Combine(RepoRoot, ".github", "workflows");
        if (Directory.Exists(workflowDirectory))
        {
            Assert.Empty(Directory.EnumerateFiles(workflowDirectory, "*", SearchOption.AllDirectories));
        }
    }

    [Fact]
    public void ShellLaunchingTests_AllSitInClassesTheClassFilterExcludes()
    {
        // The documented local suite skips the shell-launching tests with
        // --filter-not-class "*Wpf*" alone. A matching method filter used to be
        // documented too, and it silently skipped every source lint that
        // happened to be named Wpf* — including one that was failing. Keep the
        // class filter sufficient so the method filter never comes back.
        var testRoot = Path.Combine(RepoRoot, "tests", "LibreSpot.Desktop.Tests");
        var offenders = new List<string>();
        var launchers = 0;

        foreach (var file in Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file) ||
                Path.GetFileNameWithoutExtension(file) == nameof(DependencyAutomationTests))
            {
                // This file spells the marker out, so it would match itself.
                continue;
            }

            var content = File.ReadAllText(file);
            // The launch primitive, not the flag: --uia-smoke only tells an
            // already-started shell to behave, so keying on it would miss a
            // test that opens the production shell instead.
            if (!ShellLaunchPattern.IsMatch(content))
            {
                continue;
            }

            launchers++;
            // --filter-not-class matches the declared type name, not the file
            // name, so read the type names out of the file.
            foreach (Match declaration in ClassDeclarationPattern.Matches(content))
            {
                var typeName = declaration.Groups["name"].Value;
                if (!typeName.Contains("Wpf", StringComparison.Ordinal))
                {
                    offenders.Add(
                        $"{typeName} (in {Path.GetFileName(file)}) can start LibreSpot.exe, but 'Wpf' is not in its type name, " +
                        "so --filter-not-class does not exclude it from the documented local run.");
                }
            }
        }

        Assert.True(launchers > 0, "No shell-launching test was found; the marker scan is broken.");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void TestToolVersions_AreRecordedAndXunitFourIsUsed()
    {
        var project = ReadRepoFile("tests", "LibreSpot.Desktop.Tests", "LibreSpot.Desktop.Tests.csproj");
        Assert.Contains("FsCheck.Xunit.v3\" Version=\"3.4.0", project, StringComparison.Ordinal);
        Assert.Contains("Microsoft.NET.Test.Sdk\" Version=\"18.9.0", project, StringComparison.Ordinal);
        Assert.Contains("xunit.v3\" Version=\"4.0.0", project, StringComparison.Ordinal);
        Assert.Contains("xunit.runner.visualstudio\" Version=\"4.0.0", project, StringComparison.Ordinal);
        Assert.Contains("FsCheck.Xunit.v3 3.4.0 supports the xUnit v4 adapter path", project, StringComparison.Ordinal);
        Assert.Contains("\"runner\": \"Microsoft.Testing.Platform\"", ReadRepoFile("global.json"), StringComparison.Ordinal);

        var coreProject = ReadRepoFile("tests", "LibreSpot.Core.Tests", "LibreSpot.Core.Tests.csproj");
        Assert.Contains("xunit.v3\" Version=\"4.0.0", coreProject, StringComparison.Ordinal);
        Assert.Contains("xunit.runner.visualstudio\" Version=\"4.0.0", coreProject, StringComparison.Ordinal);
        Assert.Contains("UseMicrosoftTestingPlatformRunner>true", coreProject, StringComparison.Ordinal);

        using var notices = JsonDocument.Parse(ReadRepoFile("schemas", "third-party-notices.json"));
        var dependencies = notices.RootElement.GetProperty("dependencies").EnumerateArray();
        var versions = dependencies
            .Where(dependency => dependency.GetProperty("name").GetString() is "FsCheck.Xunit.v3" or "Microsoft.NET.Test.Sdk" or "xunit.v3" or "xunit.runner.visualstudio")
            .ToDictionary(
                dependency => dependency.GetProperty("name").GetString()!,
                dependency => dependency.GetProperty("version").GetString()!,
                StringComparer.Ordinal);

        Assert.Equal("3.4.0", versions["FsCheck.Xunit.v3"]);
        Assert.Equal("18.9.0", versions["Microsoft.NET.Test.Sdk"]);
        Assert.Equal("4.0.0", versions["xunit.v3"]);
        Assert.Equal("4.0.0", versions["xunit.runner.visualstudio"]);
    }

    [Fact]
    public void PowerShellLint_PinsPssaAndRecordsConstrainedLanguageDecision()
    {
        var buildScripts = ReadRepoFile("Build-Scripts.ps1");
        var settings = ReadRepoFile(".psscriptanalyzerrc.psd1");

        Assert.Contains("$requiredPssaVersion = [Version]'1.25.0'", buildScripts, StringComparison.Ordinal);
        Assert.Contains("-RequiredVersion $requiredPssaVersion", buildScripts, StringComparison.Ordinal);
        Assert.Contains("PSUseConstrainedLanguageMode", settings, StringComparison.Ordinal);
        Assert.Contains("Enable = $false", settings, StringComparison.Ordinal);
        Assert.Contains("zero diagnostics", settings, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("[switch]$SpotXSecurityPolicy", script);
        Assert.Contains("[string]$SpotXCandidateCommit", script);
        Assert.Contains("[switch]$SpotXCandidatePostDefenderPolicy", script);
        Assert.Contains("Test-SpotXPinAdvanceSecurityPolicy", script);
        Assert.Contains("Get-PinnedSpotXSecurityPolicy", script);
        Assert.Contains("Add-MpPreference", script);
        Assert.Contains("Set-MpPreference", script);
        Assert.Contains("ExclusionPath", script);
        Assert.Contains("ExclusionProcess", script);
        Assert.Contains("spotXSecurityPolicy", script);
        Assert.Contains("Get-LibreSpotDotnetRuntimeStatus", script);
        Assert.Contains("dotnetRuntime", script);
        Assert.Contains("CVE-patched .NET runtime floor", script);
    }

    [Fact]
    public void SelfContainedProjects_PinLatestRuntimePatchForServicingCves()
    {
        foreach (var project in new[]
                 {
                     Path.Combine("src", "LibreSpot.Desktop", "LibreSpot.Desktop.csproj"),
                     Path.Combine("src", "LibreSpot.Cli", "LibreSpot.Cli.csproj")
                 })
        {
            var csproj = ReadRepoFile(project.Split(Path.DirectorySeparatorChar));
            Assert.Contains("<TargetLatestRuntimePatch>true</TargetLatestRuntimePatch>", csproj);
        }
    }

    [Fact]
    public void DependencyHealthAllowlist_DeclaresCvePatchedDotnetRuntimeFloor()
    {
        using var doc = JsonDocument.Parse(ReadRepoFile("schemas", "dependency-health-allowlist.json"));
        var floor = doc.RootElement.GetProperty("dotnetRuntimeFloor");

        var version = floor.GetProperty("version").GetString();
        Assert.True(Version.TryParse(version, out var parsed) && parsed == new Version(10, 0, 11),
            $"dotnetRuntimeFloor.version must be .NET 10.0.11; was '{version}'.");
        var reason = floor.GetProperty("reason").GetString();
        Assert.Contains("2026-08-11", reason, StringComparison.Ordinal);
        Assert.Contains("CVE-2026-70354", reason, StringComparison.Ordinal);
        Assert.Contains("CVE-2026-62897", reason, StringComparison.Ordinal);
        Assert.True(DateTime.TryParse(floor.GetProperty("recheckDate").GetString(), out _));
    }

    [Fact]
    public void SpotXPinHold_IsDocumentedAndRecentlyVerified()
    {
        Assert.True(
            AppCatalog.UpstreamPinsLastVerifiedAtUtc >= new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero),
            "Upstream pins must record a re-verification on or after 2026-07-22.");

        var rationale = AppCatalog.PinnedSpotXHoldRationale;
        Assert.False(string.IsNullOrWhiteSpace(rationale));
        Assert.Contains("-defender_exclusions_off", rationale, StringComparison.Ordinal);
        Assert.Contains("afb4c3fc", rationale, StringComparison.Ordinal);
        Assert.Contains("1.2.94", rationale, StringComparison.Ordinal);

        // The hold is only coherent while the pin still predates the Defender
        // commit; the current pin declares no Defender mutations.
        Assert.False(AppCatalog.PinnedSpotXContainsDefenderMutations);
    }

    [Fact]
    public void SpotXDefenderPolicy_CurrentPinStaysArgumentCompatibleAndMetadataMatchesDesktop()
    {
        var script = ReadRepoFile("LibreSpot.ps1");
        var block = Regex.Match(
            script,
            @"(?ms)^\s{4}SpotX\s*=\s*@\{(?<body>.+?)^\s{4}\}");
        Assert.True(block.Success, "PinnedReleases.SpotX block was not found.");
        var body = block.Groups["body"].Value;

        Assert.Matches(@"(?mi)^\s*DefenderMutations\s*=\s*\$false\s*$", body);
        Assert.Matches(@"(?mi)^\s*DefenderOptOut\s*=\s*''\s*$", body);
        Assert.Matches(@"(?mi)^\s*DefenderPolicyCommit\s*=\s*'afb4c3fc'\s*$", body);
        Assert.Matches(@"(?mi)^\s*DefenderPolicyOptOut\s*=\s*'-defender_exclusions_off'\s*$", body);
        Assert.Matches(@"(?mi)^\s*DefenderPolicyActive\s*=\s*\$false\s*$", body);
        Assert.False(AppCatalog.PinnedSpotXContainsDefenderMutations);
        Assert.Empty(AppCatalog.PinnedSpotXDefenderOptOutArgument);
        Assert.Equal("afb4c3fc", AppCatalog.PinnedSpotXDefenderPolicyCommit);
        Assert.Equal("-defender_exclusions_off", AppCatalog.PinnedSpotXDefenderPolicyOptOutArgument);
        Assert.False(AppCatalog.PinnedSpotXDefenderPolicyActive);

        var buildParams = Regex.Match(
            script,
            @"(?ms)^function\s+Build-SpotXParams\s*\{(?<body>.+?)^\}");
        Assert.True(buildParams.Success);
        Assert.Contains("PinnedReleases.SpotX.DefenderMutations", buildParams.Groups["body"].Value);
        Assert.Contains("PinnedReleases.SpotX.DefenderOptOut", buildParams.Groups["body"].Value);
        Assert.Contains("PinnedReleases.SpotX.DefenderPolicyActive", buildParams.Groups["body"].Value);
        Assert.Contains("PinnedReleases.SpotX.DefenderPolicyOptOut", buildParams.Groups["body"].Value);
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
