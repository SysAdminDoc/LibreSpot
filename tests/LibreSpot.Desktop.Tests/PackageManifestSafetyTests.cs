using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class PackageManifestSafetyTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    public static TheoryData<string> DraftManifestFiles => new()
    {
        "packaging/winget/SysAdminDoc.LibreSpot.yaml",
        "packaging/winget/SysAdminDoc.LibreSpot.installer.yaml",
        "packaging/winget/SysAdminDoc.LibreSpot.locale.en-US.yaml",
        "packaging/scoop/librespot.json",
        "packaging/chocolatey/librespot.nuspec",
        "packaging/chocolatey/tools/chocolateyInstall.ps1"
    };

    [Theory]
    [MemberData(nameof(DraftManifestFiles))]
    public void PackageManagerDraftsRemainExplicitlyBlocked(string relativePath)
    {
        var content = ReadFile(relativePath);

        Assert.Contains("DRAFT", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blocked", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WingetDraftsKeepPlaceholderVersionAndInstallerHash()
    {
        var versionManifest = ParseSimpleYaml(ReadFile("packaging/winget/SysAdminDoc.LibreSpot.yaml"));
        var installerManifest = ReadFile("packaging/winget/SysAdminDoc.LibreSpot.installer.yaml");
        var installerFields = ParseSimpleYaml(installerManifest);
        var localeManifest = ParseSimpleYaml(ReadFile("packaging/winget/SysAdminDoc.LibreSpot.locale.en-US.yaml"));

        Assert.Equal("SysAdminDoc.LibreSpot", versionManifest["PackageIdentifier"]);
        Assert.Equal("3.7.2", versionManifest["PackageVersion"]);
        Assert.Equal("version", versionManifest["ManifestType"]);

        Assert.Equal(versionManifest["PackageIdentifier"], installerFields["PackageIdentifier"]);
        Assert.Equal(versionManifest["PackageVersion"], installerFields["PackageVersion"]);
        Assert.Equal("installer", installerFields["ManifestType"]);
        Assert.Contains("PLACEHOLDER_SHA256", installerManifest, StringComparison.Ordinal);

        Assert.Equal(versionManifest["PackageIdentifier"], localeManifest["PackageIdentifier"]);
        Assert.Equal(versionManifest["PackageVersion"], localeManifest["PackageVersion"]);
        Assert.Equal("defaultLocale", localeManifest["ManifestType"]);
    }

    [Fact]
    public void ScoopDraftKeepsPlaceholderHashAndReleaseManifestAutoupdateSource()
    {
        using var scoop = JsonDocument.Parse(ReadFile("packaging/scoop/librespot.json"));
        var root = scoop.RootElement;

        Assert.Equal("3.7.2", root.GetProperty("version").GetString());
        Assert.Equal("PLACEHOLDER_SHA256", root.GetProperty("hash").GetString());
        Assert.Contains("v3.7.2/LibreSpot.exe", root.GetProperty("url").GetString(), StringComparison.Ordinal);

        var autoupdateHashUrl = root
            .GetProperty("autoupdate")
            .GetProperty("hash")
            .GetProperty("url")
            .GetString();

        Assert.Contains("checksums.txt", autoupdateHashUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChocolateyDraftKeepsPlaceholderVersionAndChecksum()
    {
        var nuspec = XDocument.Parse(ReadFile("packaging/chocolatey/librespot.nuspec"));
        XNamespace ns = "http://schemas.microsoft.com/packaging/2015/06/nuspec.xsd";

        var metadata = nuspec.Root?.Element(ns + "metadata")
            ?? throw new InvalidOperationException("Chocolatey nuspec metadata is missing.");

        Assert.Equal("librespot", metadata.Element(ns + "id")?.Value);
        Assert.Equal("3.7.2", metadata.Element(ns + "version")?.Value);
        Assert.Contains("PLACEHOLDER", ReadFile("packaging/chocolatey/librespot.nuspec"), StringComparison.OrdinalIgnoreCase);

        var installScript = ReadFile("packaging/chocolatey/tools/chocolateyInstall.ps1");
        var checksum = Regex.Match(installScript, @"checksum64\s*=\s*'(?<value>[^']+)'", RegexOptions.IgnoreCase);

        Assert.True(checksum.Success, "Chocolatey install script must declare checksum64.");
        Assert.Equal("PLACEHOLDER_SHA256", checksum.Groups["value"].Value);
        Assert.Contains("v3.7.2/LibreSpot.exe", installScript, StringComparison.Ordinal);
    }

    [Fact]
    public void DistributionMatrixKeepsPackageChannelsDraftAndDecisionBlocked()
    {
        using var matrix = JsonDocument.Parse(ReadFile("schemas/distribution-matrix.json"));
        var channels = matrix.RootElement.GetProperty("channels").EnumerateArray().ToArray();

        foreach (var channelName in new[] { "winget", "scoop", "chocolatey", "velopack", "psgallery" })
        {
            var channel = channels.Single(c => c.GetProperty("channel").GetString() == channelName);

            Assert.Equal("draft", channel.GetProperty("supportStatus").GetString());
            Assert.NotEmpty(channel.GetProperty("blockingDecisions").EnumerateArray());
            Assert.Contains("pending", channel.GetProperty("packageId").GetString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void BlockedRoadmapRetainsPackageIdentityAndSigningDecisions()
    {
        var blocked = ReadFile("Roadmap_Blocked.md");

        Assert.Contains("Finalize package identity before any public distribution manifest", blocked, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Signing (SignPath Foundation enrollment)", blocked, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidationGuideIsParserOnlyUntilGeneratedFromReleaseManifest()
    {
        var validation = ReadFile("packaging/VALIDATION.txt");
        var releaseContract = ReadFile("schemas/release-artifact-contract.json");

        Assert.Contains("parser-only", validation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not submit", validation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("librespot-release-manifest.json", validation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("librespot-release-manifest.json", releaseContract, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadmeDoesNotAdvertiseDraftPackageManagersAsInstallable()
    {
        var readme = ReadFile("README.md");
        var forbiddenCommands = new[]
        {
            "winget install",
            "scoop install librespot",
            "choco install librespot",
            "chocolatey install librespot"
        };

        foreach (var command in forbiddenCommands)
        {
            Assert.DoesNotContain(command, readme, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static Dictionary<string, string> ParseSimpleYaml(string content)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#') || line.StartsWith('-'))
                continue;

            var delimiter = line.IndexOf(':', StringComparison.Ordinal);
            if (delimiter <= 0)
                continue;

            var key = line[..delimiter].Trim();
            var value = line[(delimiter + 1)..].Split('#')[0].Trim().Trim('"');
            if (value.Length > 0)
                fields[key] = value;
        }

        return fields;
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

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
