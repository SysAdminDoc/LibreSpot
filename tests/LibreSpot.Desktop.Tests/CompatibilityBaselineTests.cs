using System.Text.Json;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CompatibilityBaselineTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void Fixture_MatchesTheCoreCompatibilityTuple()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot, "schemas", "compatibility-baseline.json")));
        var root = document.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(
            AppCatalog.UpstreamPinsLastVerifiedAtUtc,
            DateTimeOffset.Parse(root.GetProperty("lastVerifiedAtUtc").GetString()!));

        var spotX = root.GetProperty("spotx");
        Assert.Equal(AppCatalog.PinnedSpotXVersion, spotX.GetProperty("version").GetString());
        Assert.Equal(AppCatalog.PinnedSpotXCommit, spotX.GetProperty("commit").GetString());
        Assert.Equal(AppCatalog.PinnedSpotXContainsDefenderMutations, spotX.GetProperty("defenderMutations").GetBoolean());
        Assert.Equal(AppCatalog.PinnedSpotXDefenderOptOutArgument, spotX.GetProperty("defenderOptOut").GetString());
        Assert.Equal(AppCatalog.PinnedSpotXDefenderPolicyCommit, spotX.GetProperty("defenderPolicyCommit").GetString());
        Assert.Equal(AppCatalog.PinnedSpotXDefenderPolicyOptOutArgument, spotX.GetProperty("defenderPolicyOptOut").GetString());
        Assert.Equal(AppCatalog.PinnedSpotXDefenderPolicyActive, spotX.GetProperty("defenderPolicyActive").GetBoolean());

        var spotify = root.GetProperty("spotify").GetProperty("version").GetString();
        Assert.Equal(AppCatalog.PinnedSpotXSpotifyVersionId, spotify);
        Assert.Equal(AppCatalog.PinnedSpotXSpotifyVersion, spotify);

        var spicetify = root.GetProperty("spicetifyCli");
        Assert.Equal(AppCatalog.PinnedSpicetifyCliVersion, spicetify.GetProperty("version").GetString());
        Assert.Equal(AppCatalog.SpicetifyWindowsMinTestedSpotify, spicetify.GetProperty("windowsMinSpotify").GetString());
        Assert.Equal(AppCatalog.SpicetifyWindowsMaxTestedSpotify, spicetify.GetProperty("windowsMaxTestedSpotify").GetString());

        var v3Support = root.GetProperty("spicetifyV3Support");
        Assert.Equal(2, v3Support.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("allowlist", v3Support.GetProperty("policy").GetString());
        Assert.Equal("classic", v3Support.GetProperty("defaultMapStatus").GetString());
        Assert.Equal(3, v3Support.GetProperty("featureDetectionMajor").GetInt32());
        Assert.Equal("schemas/spicetify-supported-versions-v2.json", v3Support.GetProperty("fixture").GetString());

        Assert.Equal(AppCatalog.PinnedMarketplaceVersion, root.GetProperty("marketplace").GetProperty("version").GetString());
        Assert.Equal(AppCatalog.PinnedThemesCommit, root.GetProperty("themes").GetProperty("commit").GetString());
    }

    [Fact]
    public void Fixture_DescribesTheSamePinsExposedByStatusContracts()
    {
        var pins = AppCatalog.UpstreamDependencyPins.ToDictionary(pin => pin.Id, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(AppCatalog.PinnedSpotXCommit, pins["spotx"].PinnedValue);
        Assert.Equal(AppCatalog.PinnedSpicetifyCliVersion, pins["spicetify-cli"].PinnedValue);
        Assert.Equal(AppCatalog.PinnedMarketplaceVersion, pins["marketplace"].PinnedValue);
        Assert.Equal(AppCatalog.PinnedThemesCommit, pins["themes"].PinnedValue);
        Assert.All(pins.Values, pin =>
        {
            Assert.NotNull(pin.LastVerifiedAtUtc);
            Assert.Equal(AppCatalog.UpstreamPinsLastVerifiedAtUtc, pin.LastVerifiedAtUtc!.Value);
        });
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
