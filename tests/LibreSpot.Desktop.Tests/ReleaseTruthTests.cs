using System.Text.RegularExpressions;
using System.Xml.Linq;
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
        var shellVersion = Match(viewModel, "ShellDisplayVersion\\s*=>\\s*\"(?<value>v[^\"]+)\"");

        Assert.Equal(desktopVersion, desktopInformationalVersion);
        Assert.Equal(desktopVersion, cliVersion);
        Assert.Equal($"v{desktopVersion}", shellVersion);
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
    public void LiveReleaseValidatorQueriesGithubLatestAndRequiredAssets()
    {
        var buildScript = Read("Build-Scripts.ps1");

        Assert.Contains("repos/SysAdminDoc/LibreSpot/releases/latest", buildScript);
        Assert.Contains("'LibreSpot.ps1', 'LibreSpot.exe', 'checksums.txt'", buildScript);
        Assert.Contains("Test-PublicReleaseTruth", buildScript);
        Assert.Contains("Test-LocalReleaseTruth", buildScript);
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
