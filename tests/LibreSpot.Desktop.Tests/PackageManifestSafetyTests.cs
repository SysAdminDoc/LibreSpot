using System.Text.Json;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class PackageManifestSafetyTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

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

    [Fact]
    public void PackagingDirectoryDoesNotExist()
    {
        Assert.False(
            Directory.Exists(Path.Combine(RepoRoot, "packaging")),
            "Draft packaging manifests were removed — regenerate from release-artifact-contract.json when signing and identity are finalized.");
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
