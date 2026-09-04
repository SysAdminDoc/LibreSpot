using System.Globalization;
using System.IO;
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

        // Pinning Spotify pins the Chromium inside it, and CEF does not backport
        // security fixes, so the recorded engine and the disclosure that quotes
        // it have to move together with the pin.
        var spotifyEntry = root.GetProperty("spotify");
        var engine = spotifyEntry.GetProperty("embeddedChromium").GetString()!;
        var engineMajor = spotifyEntry.GetProperty("embeddedChromiumMajor").GetInt32();
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", engine);
        Assert.Equal(
            engineMajor.ToString(CultureInfo.InvariantCulture),
            engine.Split('.')[0]);
        Assert.True(
            DateTimeOffset.TryParse(
                spotifyEntry.GetProperty("embeddedChromiumReadAtUtc").GetString(),
                out _),
            "embeddedChromiumReadAtUtc must say when the engine was read.");

        var readme = File.ReadAllText(Path.Combine(RepoRoot, "README.md"));
        Assert.Contains(
            $"ships **Chromium {engineMajor}**",
            readme,
            StringComparison.Ordinal);

        var spicetify = root.GetProperty("spicetifyCli");
        Assert.Equal(AppCatalog.PinnedSpicetifyCliVersion, spicetify.GetProperty("version").GetString());
        Assert.Equal(AppCatalog.SpicetifyWindowsDeclaredMinSpotify, spicetify.GetProperty("windowsMinSpotify").GetString());
        Assert.Equal(AppCatalog.SpicetifyWindowsDeclaredMaxSpotify, spicetify.GetProperty("windowsDeclaredMaxSpotify").GetString());
        Assert.Equal(AppCatalog.LibreSpotVerifiedMaxSpotify, spicetify.GetProperty("libreSpotVerifiedMaxSpotify").GetString());

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

    [Fact]
    public void DeclaredWindowsCeiling_MatchesTheSupportedVersionsFixture()
    {
        // The declared ceiling is upstream's claim, so it has to be recorded
        // where upstream's support data lives rather than only in a constant.
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot, "schemas", "spicetify-supported-versions-v2.json")));
        var declared = document.RootElement.GetProperty("cli_declared_windows_range");

        Assert.Equal(AppCatalog.SpicetifyWindowsDeclaredMinSpotify, declared.GetProperty("min").GetString());
        Assert.Equal(AppCatalog.SpicetifyWindowsDeclaredMaxSpotify, declared.GetProperty("max").GetString());
        Assert.Equal(
            $"https://github.com/spicetify/cli/releases/tag/v{AppCatalog.PinnedSpicetifyCliVersion}",
            declared.GetProperty("source").GetString());

        // The css-map window below it records what LibreSpot has verified, so it
        // cannot claim more than upstream declares. Keeping this here is what
        // stops the two numbers being conflated again.
        var declaredMax = Version.Parse(declared.GetProperty("max").GetString()!);
        foreach (var range in document.RootElement.GetProperty("ranges").EnumerateArray())
        {
            var mapped = Version.Parse(range.GetProperty("max").GetString()!);
            Assert.True(
                mapped <= declaredMax,
                $"css-map range max {mapped} claims more than Spicetify declares ({declaredMax}).");
        }
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
