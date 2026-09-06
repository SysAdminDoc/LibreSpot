using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ThirdPartyNoticesTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly JsonDocument Notices = LoadNotices();

    [Fact]
    public void Notices_CoversAllRuntimeNuGetPackages()
    {
        var csproj = XDocument.Load(
            Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "LibreSpot.Desktop.csproj"));
        var ns = csproj.Root!.GetDefaultNamespace();

        var nugetPackages = csproj.Descendants(ns + "PackageReference")
            .Where(e => !string.Equals(
                e.Attribute("PrivateAssets")?.Value,
                "all",
                StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Attribute("Include")!.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var noticeNames = GetDependencyNames("runtime");

        var missing = nugetPackages.Except(noticeNames, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(
            missing.Count == 0,
            $"NuGet runtime packages missing from third-party notices: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Notices_CoversPrivateBuildNuGetPackages()
    {
        var csproj = XDocument.Load(
            Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "LibreSpot.Desktop.csproj"));
        var ns = csproj.Root!.GetDefaultNamespace();

        var buildOnlyPackages = csproj.Descendants(ns + "PackageReference")
            .Where(e => string.Equals(
                e.Attribute("PrivateAssets")?.Value,
                "all",
                StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Attribute("Include")!.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var noticeNames = GetDependencyNames("build");
        var missing = buildOnlyPackages.Except(noticeNames, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.True(
            missing.Count == 0,
            $"Private build packages missing from third-party notices: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Notices_CoversAllTestNuGetPackages()
    {
        var csproj = XDocument.Load(
            Path.Combine(RepoRoot, "tests", "LibreSpot.Desktop.Tests", "LibreSpot.Desktop.Tests.csproj"));
        var ns = csproj.Root!.GetDefaultNamespace();

        var nugetPackages = csproj.Descendants(ns + "PackageReference")
            .Select(e => e.Attribute("Include")!.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var noticeNames = GetDependencyNames("test");

        var missing = nugetPackages.Except(noticeNames, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(
            missing.Count == 0,
            $"NuGet test packages missing from third-party notices: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Notices_SpotXVersionMatchesScript()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "LibreSpot.ps1"));

        var spotxEntry = GetDependency("SpotX");
        Assert.NotNull(spotxEntry);

        var commitMatch = Regex.Match(
            script,
            @"SpotX\s*=\s*@\{[^}]*Commit\s*=\s*'([a-f0-9]{40})'",
            RegexOptions.Singleline);
        Assert.True(commitMatch.Success, "SpotX commit not found in script.");

        var shortCommit = commitMatch.Groups[1].Value[..8];
        Assert.Contains(shortCommit, spotxEntry.Value.GetProperty("version").GetString()!);
    }

    [Fact]
    public void Notices_SpicetifyCliVersionMatchesScript()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "LibreSpot.ps1"));

        var cliEntry = GetDependency("Spicetify CLI");
        Assert.NotNull(cliEntry);

        var versionMatch = Regex.Match(
            script,
            @"SpicetifyCLI\s*=\s*@\{[^}]*Version\s*=\s*'([^']+)'",
            RegexOptions.Singleline);
        Assert.True(versionMatch.Success, "Spicetify CLI version not found in script.");

        Assert.Equal(
            versionMatch.Groups[1].Value,
            cliEntry.Value.GetProperty("version").GetString()!);
    }

    [Fact]
    public void Notices_AllEntriesHaveRequiredFields()
    {
        var required = new[] { "name", "category", "version", "sourceUrl", "spdxLicense", "redistributionPosture" };

        foreach (var dep in Notices.RootElement.GetProperty("dependencies").EnumerateArray())
        {
            var name = dep.GetProperty("name").GetString()!;
            foreach (var field in required)
            {
                Assert.True(
                    dep.TryGetProperty(field, out var val) && val.ValueKind != JsonValueKind.Undefined,
                    $"Dependency '{name}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Notices_AllLicensesAreInPolicy()
    {
        var policy = Notices.RootElement.GetProperty("licensePolicy");
        var allowed = policy.GetProperty("autoAllowed").EnumerateArray()
            .Select(v => v.GetString()!).ToHashSet(StringComparer.Ordinal);
        var reviewRequired = policy.GetProperty("reviewRequired").EnumerateArray()
            .Select(v => v.GetString()!).ToHashSet(StringComparer.Ordinal);
        var blocked = policy.GetProperty("blocked").EnumerateArray()
            .Select(v => v.GetString()!).ToHashSet(StringComparer.Ordinal);

        var allKnown = allowed.Union(reviewRequired).Union(blocked).ToHashSet(StringComparer.Ordinal);

        foreach (var dep in Notices.RootElement.GetProperty("dependencies").EnumerateArray())
        {
            var name = dep.GetProperty("name").GetString()!;
            var license = dep.GetProperty("spdxLicense").GetString()!;

            if (license.StartsWith("pending", StringComparison.OrdinalIgnoreCase))
                continue;

            Assert.True(
                allKnown.Contains(license),
                $"Dependency '{name}' has license '{license}' not listed in any policy tier.");
        }
    }

    [Fact]
    public void Notices_NuGetVersionsMatchCsproj()
    {
        var csproj = XDocument.Load(
            Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "LibreSpot.Desktop.csproj"));
        var ns = csproj.Root!.GetDefaultNamespace();

        var nugetVersions = csproj.Descendants(ns + "PackageReference")
            .ToDictionary(
                e => e.Attribute("Include")!.Value,
                e => e.Attribute("Version")!.Value,
                StringComparer.OrdinalIgnoreCase);

        foreach (var dep in Notices.RootElement.GetProperty("dependencies").EnumerateArray())
        {
            var name = dep.GetProperty("name").GetString()!;
            if (nugetVersions.TryGetValue(name, out var csprojVersion))
            {
                var noticeVersion = dep.GetProperty("version").GetString()!;
                Assert.Equal(csprojVersion, noticeVersion);
            }
        }
    }

    [Fact]
    public void Notices_ReferencesCommAssetManifest()
    {
        var refPath = Notices.RootElement.GetProperty("communityAssetsRef").GetString()!;
        Assert.True(
            File.Exists(Path.Combine(RepoRoot, refPath)),
            $"Community assets manifest referenced at '{refPath}' does not exist.");
    }

    [Fact]
    public void AppNotices_ListNoFirstPartyRepositoryAsThirdParty()
    {
        // The table used to credit github.com/SysAdminDoc/LibreSpot-Prism at a
        // pinned commit. That repository was never pushed, so the link answered
        // 404 and the commit lived only in a local folder. The deeper problem is
        // that Prism is this project's own work: a third-party attribution table
        // is the wrong place for it, and a licence notice is the one file that
        // must not point somewhere that does not exist.
        var notices = File.ReadAllText(
            Path.Combine(RepoRoot, "src", "LibreSpot.App", "THIRD_PARTY_NOTICES.md"));

        var rows = Regex.Matches(notices, @"^\| \[(?<label>[^\]]+)\]\((?<url>[^)]+)\)", RegexOptions.Multiline);

        // Guards the regex rather than the content: if the table is reformatted
        // out of this shape, the loop below would pass by matching nothing.
        Assert.True(
            rows.Count >= 20,
            $"Only matched {rows.Count} linked rows in the App third-party notices; the table shape changed and this "
                + "test is no longer reading it.");

        var firstParty = rows
            .Where(row => row.Groups["url"].Value.Contains("github.com/SysAdminDoc/", StringComparison.OrdinalIgnoreCase))
            .Select(row => $"{row.Groups["label"].Value} -> {row.Groups["url"].Value}")
            .ToArray();

        Assert.True(
            firstParty.Length == 0,
            "The App third-party notices credit a repository owned by this project as though it were third party. "
                + "First-party work belongs in the prose above the table, not in it: "
                + string.Join(", ", firstParty));

        // The replacement prose has to keep saying where the origin history went,
        // or the commit it names becomes unreachable trivia.
        Assert.Contains("refs/archive/prism-origin", notices, StringComparison.Ordinal);
        Assert.Contains("72dc0334fdf11a156314c96593539e477aee7028", notices, StringComparison.Ordinal);
    }

    private static HashSet<string> GetDependencyNames(string category)
    {
        return Notices.RootElement.GetProperty("dependencies").EnumerateArray()
            .Where(d => d.GetProperty("category").GetString() == category)
            .Select(d => d.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonElement? GetDependency(string name)
    {
        foreach (var dep in Notices.RootElement.GetProperty("dependencies").EnumerateArray())
        {
            if (dep.GetProperty("name").GetString() == name)
                return dep;
        }
        return null;
    }

    private static JsonDocument LoadNotices()
    {
        var path = Path.Combine(RepoRoot, "schemas", "third-party-notices.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
