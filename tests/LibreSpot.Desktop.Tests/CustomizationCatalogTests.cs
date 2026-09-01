using System.Text.Json.Nodes;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CustomizationCatalogTests
{
    [Fact]
    public void EmbeddedCatalog_CoversPinnedFeaturesAndDesktopControls()
    {
        var catalog = AppCatalog.CustomizationCatalog;

        Assert.Equal(348, catalog.SpotifyFeatures.Count);
        Assert.Equal(104, catalog.SpotXFeatureOverrides.Count);
        Assert.Equal(31, catalog.SpotXSwitches.Count);
        Assert.Equal(21, catalog.SpicetifyOptions.Count);
        Assert.Equal(12, catalog.Snippets.Count);
        Assert.Contains(catalog.BuiltInThemes, theme => theme.Id == "Prism");
        Assert.Contains(catalog.BuiltInThemes, theme => theme.Id == "Compact");
        Assert.Contains(catalog.BuiltInThemes, theme => theme.Id == "Accessibility");
    }

    [Fact]
    public void EmbeddedCatalog_MatchesAppCatalogPins()
    {
        var pins = AppCatalog.CustomizationCatalog.Pins;

        Assert.Equal(AppCatalog.PinnedSpotXSpotifyVersion, pins.SpotifyVersion);
        Assert.Equal(AppCatalog.PinnedSpotXCommit, pins.SpotXCommit);
        Assert.Equal(AppCatalog.PinnedSpicetifyCliVersion, pins.SpicetifyVersion);
        Assert.Equal(AppCatalog.PinnedMarketplaceVersion, pins.MarketplaceVersion);
        Assert.Equal(AppCatalog.PinnedThemesCommit, pins.ThemesCommit);
        Assert.Matches("^[a-f0-9]{64}$", pins.XpuiSha256);
    }

    [Fact]
    public void ProfileCodec_RoundTripsEngineFieldsWithoutDroppingThem()
    {
        const string source = """
            {
              "schemaVersion": 1,
              "name": "Desk",
              "theme": "Compact",
              "scheme": "Dark",
              "schemes": { "Dark": { "main": "000000", "text": "FFFFFF" } },
              "layers": { "palette": true, "layout": true, "effects": true, "accessibility": true },
              "effectsTier": "eco",
              "enabledSnippets": ["compact-track-rows"],
              "featureOverrides": { "enableLyrics": true },
              "spotxSwitches": { "SpotX_BlockUpdate": true },
              "spicetifyOptions": { "replace_colors": true },
              "userPresets": []
            }
            """;

        var profile = LibreSpotProfileCodec.Parse(source);
        var output = LibreSpotProfileCodec.Serialize(profile);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(source), JsonNode.Parse(output)));
    }

    [Fact]
    public void ProfileCodec_RejectsAMissingSelectedScheme()
    {
        const string source = """
            {
              "schemaVersion": 1,
              "name": "Desk",
              "theme": "Prism",
              "scheme": "Missing",
              "schemes": { "Dark": { "main": "000000", "text": "FFFFFF" } }
            }
            """;

        Assert.Throws<InvalidDataException>(() => LibreSpotProfileCodec.Parse(source));
    }
}
