using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ReleaseTruthTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void PreviewClaimsMatchProjectsShellAndReadme()
    {
        var desktopVersion = ProjectValue("src/LibreSpot.Desktop/LibreSpot.Desktop.csproj", "Version");
        var desktopInformationalVersion = ProjectValue("src/LibreSpot.Desktop/LibreSpot.Desktop.csproj", "InformationalVersion");
        var cliVersion = ProjectValue("src/LibreSpot.Cli/LibreSpot.Cli.csproj", "Version");
        var viewModel = Read("src/LibreSpot.Desktop/ViewModels/MainViewModel.cs");
        var readme = Read("README.md");

        Assert.Equal(desktopVersion, desktopInformationalVersion);
        Assert.Equal(desktopVersion, cliVersion);
        Assert.Contains("AssemblyInformationalVersionAttribute", viewModel);
        Assert.Contains("public string ShellDisplayVersion => $\"v{ProductVersion}\";", viewModel);
        Assert.DoesNotContain("ShellDisplayVersion => \"v", viewModel);
        Assert.Contains($"Version-{desktopVersion.Replace("-", "--")}-brightgreen.svg", readme);
        Assert.Contains($"## What's New in v{desktopVersion}", readme);
    }

    [Fact]
    public void ReadmeDistinguishesSourceScriptFromPublicStableRelease()
    {
        var readme = Read("README.md");
        var mainVersion = ScriptVersion("LibreSpot.ps1");
        var backendVersion = ScriptVersion("src/LibreSpot.Desktop/Backend/LibreSpot.Backend.ps1");
        var stableBadge = Match(readme, "Stable-(?<value>\\d+\\.\\d+\\.\\d+)-blue\\.svg");

        Assert.Equal(mainVersion, backendVersion);
        Assert.Contains($"Current source script version: **v{mainVersion}**", readme);
        Assert.Contains($"public latest stable release, v{stableBadge}", readme);
        Assert.DoesNotContain($"current latest stable release, v{mainVersion}", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerificationSectionNamesTheOnlyPlaceLibreSpotIsPublished()
    {
        // Several lookalike projects outrank this one on stars in the same
        // searches, so "download it from the official repo" is not enough on its
        // own: a reader has to be able to recognise one, and to check a file
        // rather than trust a page.
        var readme = Read("README.md");
        var security = Read("SECURITY.md");
        var verification = readme.Split("## How to verify a LibreSpot download")[1].Split("## ")[0];

        const string canonical = "https://github.com/SysAdminDoc/LibreSpot/releases";
        Assert.Contains(canonical, verification, StringComparison.Ordinal);
        Assert.Contains(canonical, security, StringComparison.Ordinal);

        Assert.Contains("gh release verify-asset", verification, StringComparison.Ordinal);
        Assert.Contains("activation", verification, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("template", verification, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplicationControlGuidanceCoversEveryShippedArtifactNotJustTheScript()
    {
        // The FAQ answered for LibreSpot.ps1 alone while the desktop executable had
        // become the first install path. Smart App Control blocks unsigned code with
        // no per-app allowance, so a reader on that device needs to be told the
        // executables are blocked too and that there is nothing to click through.
        var readme = Read("README.md");
        var security = Read("SECURITY.md");

        var smartAppControl = readme.Split("**Smart App Control")[1].Split("---")[0];

        foreach (var artifact in new[] { "LibreSpot-Desktop.exe", "LibreSpot.Cli.exe", "LibreSpot.exe", "LibreSpot.ps1" })
        {
            Assert.Contains(artifact, smartAppControl, StringComparison.Ordinal);
        }

        Assert.Contains("no per-app allowance", smartAppControl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evaluation", smartAppControl, StringComparison.OrdinalIgnoreCase);

        // Nothing anywhere may tell a reader to turn the feature off.
        foreach (var text in new[] { readme, security })
        {
            Assert.DoesNotContain("disable Smart App Control", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("turn off Smart App Control", text, StringComparison.OrdinalIgnoreCase);
        }

        // SmartScreen reputation does not carry across releases, and both documents
        // should say so rather than leaving a returning user to wonder.
        Assert.Contains("reputation", security, StringComparison.OrdinalIgnoreCase);
        var smartScreen = readme.Split("**Windows SmartScreen says")[1].Split("**Smart App Control")[0];
        Assert.Contains("next release", smartScreen, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportAndSigningDocsMatchTheStableReleaseLine()
    {
        var desktopVersion = ProjectValue("src/LibreSpot.Desktop/LibreSpot.Desktop.csproj", "Version");
        var security = Read("SECURITY.md");
        var supportTable = security.Split("## Supported Versions")[1].Split("## ")[0];
        var docs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SECURITY.md"] = security,
            ["SIGNPATH.md"] = Read("SIGNPATH.md"),
            ["Roadmap_Blocked.md"] = Read("Roadmap_Blocked.md"),
            ["README.md"] = Read("README.md")
        };

        if (!desktopVersion.Contains('-', StringComparison.Ordinal))
        {
            // Stable metadata must not sit beside preview-only support wording.
            Assert.Matches(@"(?m)^\| v4\.0\.x and later[^|]*\| Yes", supportTable);
            Assert.Contains("v3.7.x", supportTable, StringComparison.Ordinal);
            Assert.Contains("Superseded", supportTable, StringComparison.Ordinal);
            Assert.DoesNotContain("-preview", supportTable, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Best-effort", supportTable, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var (path, content) in docs)
        {
            foreach (var pendingClaim in new[] { "pending-enrollment", "enrollment is pending", "pending signing", "SignPath credentials", "Complete SignPath Foundation enrollment", "once signing is unblocked", "signing enrollment", "signed CLI artifact", "unsigned-gated", "waits until signing" })
            {
                Assert.False(
                    content.Contains(pendingClaim, StringComparison.OrdinalIgnoreCase),
                    $"{path} still describes signing as pending: '{pendingClaim}'.");
            }
        }

        Assert.Contains("unsigned by design", docs["SECURITY.md"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unsigned by design", docs["SIGNPATH.md"], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latest stable release is still `v3.7.2`", docs["Roadmap_Blocked.md"], StringComparison.Ordinal);
        Assert.DoesNotContain("release workflow", docs["Roadmap_Blocked.md"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LiveReleaseValidatorQueriesGithubLatestAndRequiredAssets()
    {
        var buildScript = Read("Build-Scripts.ps1");

        Assert.Contains("repos/SysAdminDoc/LibreSpot/releases/latest", buildScript);
        Assert.Contains("'LibreSpot.ps1', 'LibreSpot.exe', 'checksums.txt'", buildScript);
        Assert.Contains("Test-PublicReleaseTruth", buildScript);
        Assert.Contains("Test-LocalReleaseTruth", buildScript);
        Assert.Contains("Get-LibreSpotProjectInformationalVersion", buildScript);
        Assert.DoesNotContain("must be a literal v-prefixed version", buildScript);
    }

    private static string ScriptVersion(string relativePath) =>
        Match(Read(relativePath), "(?m)^\\$global:VERSION\\s*=\\s*'(?<value>[^']+)'");

    private static string ProjectValue(string relativePath, string elementName)
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return document.Descendants(elementName).Select(element => element.Value.Trim()).First(value => value.Length > 0);
    }

    private static string Match(string content, string pattern)
    {
        var match = Regex.Match(content, pattern);
        Assert.True(match.Success, $"Expected pattern was not found: {pattern}");
        return match.Groups["value"].Value;
    }

    [Fact]
    public void ReadmeUninstallerPhaseCountMatchesTheScript()
    {
        var script = Read("LibreSpot.ps1");
        var readme = Read("README.md");

        var logged = Regex.Matches(script, @"\[Phase (?<index>\d+)/(?<total>\d+)\]")
            .Select(match => (Index: int.Parse(match.Groups["index"].Value), Total: int.Parse(match.Groups["total"].Value)))
            .ToList();
        Assert.NotEmpty(logged);
        var total = Assert.Single(logged.Select(entry => entry.Total).Distinct());
        Assert.Equal(Enumerable.Range(1, total), logged.Select(entry => entry.Index).Distinct().OrderBy(index => index));

        var section = Section(readme, "### Comprehensive Uninstaller");
        var steps = Regex.Matches(section, @"(?m)^(?<index>\d+)\. ").Select(match => int.Parse(match.Groups["index"].Value)).ToList();
        Assert.Equal(Enumerable.Range(1, total), steps);
        Assert.DoesNotMatch(@"\d+-phase uninstaller", section);
    }

    [Fact]
    public void ReadmeLyricsThemeCountMatchesTheCatalog()
    {
        var readme = Read("README.md");
        var expected = AppCatalog.LyricsThemes.Count;

        var heading = Regex.Match(readme, @"### (?<count>\d+) Lyrics Color Themes");
        Assert.True(heading.Success, "The lyrics theme heading is missing from README.md.");
        Assert.Equal(expected, int.Parse(heading.Groups["count"].Value));

        var sentence = Regex.Match(readme, @"exposes all (?<count>\d+) SpotX static lyrics color options: (?<list>[^.]+)\.");
        Assert.True(sentence.Success, "The lyrics theme list is missing from README.md.");
        Assert.Equal(expected, int.Parse(sentence.Groups["count"].Value));

        var listed = sentence.Groups["list"].Value
            .Replace(", and ", ", ")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(AppCatalog.LyricsThemes.OrderBy(name => name, StringComparer.Ordinal),
            listed.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ReadmeNamesTheLaneForClaimsOnlyOneLaneFulfils()
    {
        var readme = Read("README.md");
        var details = Section(readme, "### Other Details");

        // Every claim here held for the script long before the desktop app existed.
        // Presenting them as product-wide misled anyone reading the download page.
        Assert.Contains("Only the script keeps its own window on top", details, StringComparison.Ordinal);
        Assert.DoesNotContain("LibreSpot stays on top until finished", details, StringComparison.Ordinal);
        Assert.Contains("**Self-elevating script**", details, StringComparison.Ordinal);
        Assert.DoesNotContain("- **Self-elevating**, auto-requests admin privileges when needed", details, StringComparison.Ordinal);
        Assert.Contains("built for x64 only", details, StringComparison.Ordinal);
        Assert.DoesNotContain("- **Architecture support**, x64 and ARM64 with per-architecture hash verification", details, StringComparison.Ordinal);
        Assert.Contains("the script runs an install in background runspaces", details, StringComparison.Ordinal);

        // The desktop shell has no reachable global search, so no document may
        // describe one until the collapsed shell decision is made.
        Assert.DoesNotContain("from global search", Read("CHANGELOG.md"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadmePresentsTheSpotXPinByCommitAndExplainsItsRecordedVersion()
    {
        var readme = Read("README.md");

        Assert.Matches(@"\| SpotX \| `[0-9a-f]{8}`, \d{4}-\d{2}-\d{2} \(Spotify \d+\.\d+\.\d+\) \|", readme);
        Assert.Contains("upstream's newest tag is 1.9", readme, StringComparison.Ordinal);
        Assert.Contains("its own adapter version for commit", readme, StringComparison.Ordinal);
    }

    private static string Section(string document, string heading)
    {
        var start = document.IndexOf(heading, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Heading '{heading}' is missing.");
        // Stop at the next heading of ANY level. Scanning only for "### " ran past
        // a following "## " and swallowed unrelated sections, which made every
        // assertion below satisfiable by text somewhere else in the file.
        var next = Regex.Match(document[(start + heading.Length)..], @"(?m)^#{1,6} ");
        var end = next.Success ? start + heading.Length + next.Index : document.Length;
        return document[start..end];
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
