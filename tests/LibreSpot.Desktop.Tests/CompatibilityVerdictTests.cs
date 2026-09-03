using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CompatibilityVerdictTests
{
    [Fact]
    public void Create_ReportsSupportedPinnedTuple()
    {
        var report = CompatibilityVerdictReport.Create(
            HealthReport(
                Spotify("1.2.93.647", HealthSeverity.Ready),
                SpotX(HealthSeverity.Ready),
                Spicetify(AppCatalog.PinnedSpicetifyCliVersion, HealthSeverity.Ready),
                Marketplace(AppCatalog.PinnedMarketplaceVersion, HealthSeverity.Ready)),
            spotifyInstalled: true,
            spicetifyInstalled: true,
            marketplaceFilesPresent: true,
            marketplaceRegistered: true);

        Assert.Equal(CompatibilityVerdictState.Supported, report.OverallVerdict);
        Assert.All(report.Items, item => Assert.Equal(CompatibilityVerdictState.Supported, item.Verdict));
        Assert.Equal("1.2.93", Assert.Single(report.Items, item => item.Id == "spotify").PinnedValue);
        Assert.Equal("2.0 / 1.2.93", Assert.Single(report.Items, item => item.Id == "spotx").PinnedValue);
    }

    [Fact]
    public void Create_ReportsDegradedForDetectedNonPinnedValues()
    {
        var report = CompatibilityVerdictReport.Create(
            HealthReport(
                Spotify("1.2.92", HealthSeverity.Ready),
                SpotX(HealthSeverity.Ready),
                Spicetify("2.43.0", HealthSeverity.Ready),
                Marketplace("1.0.8", HealthSeverity.Ready)),
            spotifyInstalled: true,
            spicetifyInstalled: true,
            marketplaceFilesPresent: true,
            marketplaceRegistered: true);

        Assert.Equal(CompatibilityVerdictState.Degraded, report.OverallVerdict);
        Assert.Equal(CompatibilityVerdictState.Degraded, Item(report, "spotify").Verdict);
        Assert.Equal(CompatibilityVerdictState.Degraded, Item(report, "spicetify-cli").Verdict);
        Assert.Equal(CompatibilityVerdictState.Degraded, Item(report, "marketplace").Verdict);
        Assert.Equal(CompatibilityVerdictState.Supported, Item(report, "spotx").Verdict);
    }

    [Fact]
    public void Create_ReportsUnsupportedForNewSpotifyAndSpicetifyV3()
    {
        var report = CompatibilityVerdictReport.Create(
            HealthReport(
                Spotify("1.2.96", HealthSeverity.Ready),
                SpotX(HealthSeverity.Critical),
                Spicetify("3.0.0", HealthSeverity.Warning),
                Marketplace(null, HealthSeverity.Warning)),
            spotifyInstalled: true,
            spicetifyInstalled: true,
            marketplaceFilesPresent: false,
            marketplaceRegistered: false);

        Assert.Equal(CompatibilityVerdictState.Unsupported, report.OverallVerdict);
        Assert.Equal(CompatibilityVerdictState.Unsupported, Item(report, "spotify").Verdict);
        Assert.Equal(CompatibilityVerdictState.Unsupported, Item(report, "spotx").Verdict);
        Assert.Equal(CompatibilityVerdictState.Unsupported, Item(report, "spicetify-cli").Verdict);
        Assert.Equal(CompatibilityVerdictState.Unsupported, Item(report, "marketplace").Verdict);
    }

    [Fact]
    public void Create_ReportsUnknownWhenDependenciesAreMissing()
    {
        var report = CompatibilityVerdictReport.Create(
            new StackHealthReport([]),
            spotifyInstalled: false,
            spicetifyInstalled: false,
            marketplaceFilesPresent: false,
            marketplaceRegistered: false);

        Assert.Equal(CompatibilityVerdictState.Unknown, report.OverallVerdict);
        Assert.All(report.Items, item => Assert.Equal(CompatibilityVerdictState.Unknown, item.Verdict));
        Assert.Equal(
            CompatibilityDetectionCode.Unavailable,
            Item(report, "marketplace").DetectionCode);
    }

    private static StackHealthReport HealthReport(params StackHealthComponent[] components) =>
        new(components);

    private static StackHealthComponent Spotify(string? version, string severity) =>
        Component("spotify", version, severity);

    private static StackHealthComponent SpotX(string severity) =>
        Component("spotx", null, severity);

    private static StackHealthComponent Spicetify(string? version, string severity) =>
        Component("spicetify-cli", version, severity);

    private static StackHealthComponent Marketplace(string? version, string severity) =>
        Component("marketplace", version, severity);

    private static StackHealthComponent Component(string id, string? version, string severity) =>
        new(
            id,
            id,
            "state",
            severity,
            version,
            null,
            null,
            "evidence",
            Array.Empty<string>());

    private static CompatibilityVerdictItem Item(CompatibilityVerdictReport report, string id) =>
        Assert.Single(report.Items, item => item.Id == id);
}
