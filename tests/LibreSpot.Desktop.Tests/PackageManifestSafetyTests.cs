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
    public void FleetToolsDistributionMatchesImplementedCliContract()
    {
        using var matrix = JsonDocument.Parse(ReadFile("schemas/distribution-matrix.json"));
        using var fleet = JsonDocument.Parse(ReadFile("schemas/fleet-cli-contract.json"));
        var channel = matrix.RootElement.GetProperty("channels")
            .EnumerateArray()
            .Single(c => c.GetProperty("channel").GetString() == "fleet-tools");
        var implemented = fleet.RootElement.GetProperty("verbs")
            .EnumerateArray()
            .Where(verb => verb.GetProperty("implementationStatus").GetString() == "implemented")
            .Select(verb => verb.GetProperty("verb").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var listed = channel.GetProperty("implementedCliVerbs")
            .EnumerateArray()
            .Select(verb => verb.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(implemented, listed);
        Assert.Empty(channel.GetProperty("blockingDecisions").EnumerateArray());

        var blocked = ReadFile("Roadmap_Blocked.md");
        Assert.DoesNotContain("Write the shell-integration registration design", blocked, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("before implementing protocol", blocked, StringComparison.OrdinalIgnoreCase);
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
    public void CapabilityBoundaryDocsKeepPremiumEntitlementsExplicit()
    {
        var readme = ReadFile("README.md");
        var security = ReadFile("SECURITY.md");
        foreach (var document in new[] { readme, security })
        {
            Assert.Contains("does not grant Spotify Premium", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("offline downloads", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("lossless", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("mobile on-demand", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Jams", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Premium account (skip ad-blocking)", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Full Reset", document, StringComparison.OrdinalIgnoreCase);
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
