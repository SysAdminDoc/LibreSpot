using System.Collections.ObjectModel;

namespace LibreSpot.Desktop.Models;

public static class CompatibilityVerdictState
{
    public const string Supported = "supported";
    public const string Degraded = "degraded";
    public const string Unsupported = "unsupported";
    public const string Unknown = "unknown";
}

public static class CompatibilityDetectionCode
{
    public const string Version = "version";
    public const string Missing = "missing";
    public const string Unknown = "unknown";
    public const string NotChecked = "not-checked";
    public const string Verified = "verified";
    public const string Unverified = "unverified";
    public const string Files = "files";
    public const string Unavailable = "unavailable";
}

public sealed record CompatibilityVerdictItem(
    string Id,
    string? DetectedValue,
    string PinnedValue,
    string Verdict,
    string DetectionCode);

public sealed class CompatibilityVerdictReport
{
    public static CompatibilityVerdictReport Empty { get; } = new(Array.Empty<CompatibilityVerdictItem>());

    public CompatibilityVerdictReport(IEnumerable<CompatibilityVerdictItem> items)
    {
        Items = new ReadOnlyCollection<CompatibilityVerdictItem>(items.ToArray());
    }

    public IReadOnlyList<CompatibilityVerdictItem> Items { get; }

    public string OverallVerdict =>
        Items.Count == 0
            ? CompatibilityVerdictState.Unknown
            : Items.Any(item => item.Verdict == CompatibilityVerdictState.Unsupported)
            ? CompatibilityVerdictState.Unsupported
            : Items.Any(item => item.Verdict == CompatibilityVerdictState.Degraded)
                ? CompatibilityVerdictState.Degraded
                : Items.Any(item => item.Verdict == CompatibilityVerdictState.Unknown)
                    ? CompatibilityVerdictState.Unknown
                    : CompatibilityVerdictState.Supported;

    public static CompatibilityVerdictReport Create(
        StackHealthReport healthReport,
        bool spotifyInstalled,
        bool spicetifyInstalled,
        bool marketplaceFilesPresent,
        bool marketplaceRegistered)
    {
        return new CompatibilityVerdictReport(
        [
            BuildSpotify(Find(healthReport, "spotify"), spotifyInstalled),
            BuildSpotX(Find(healthReport, "spotx"), spotifyInstalled),
            BuildSpicetify(Find(healthReport, "spicetify-cli"), spicetifyInstalled),
            BuildMarketplace(
                Find(healthReport, "marketplace"),
                spicetifyInstalled,
                marketplaceFilesPresent,
                marketplaceRegistered)
        ]);
    }

    private static CompatibilityVerdictItem BuildSpotify(
        StackHealthComponent? component,
        bool installed)
    {
        const string pinned = AppCatalog.PinnedSpotXSpotifyVersion;
        if (!installed || component is null)
        {
            return new(
                "spotify",
                null,
                pinned,
                CompatibilityVerdictState.Unknown,
                CompatibilityDetectionCode.Missing);
        }

        if (!TryParseVersion(component.DetectedVersion, out var detected) ||
            !TryParseVersion(pinned, out var pinnedVersion) ||
            !TryParseVersion(AppCatalog.SpicetifyWindowsMinTestedSpotify, out var minimum) ||
            !TryParseVersion(AppCatalog.SpicetifyWindowsMaxTestedSpotify, out var maximum))
        {
            return new(
                "spotify",
                component.DetectedVersion,
                pinned,
                CompatibilityVerdictState.Unknown,
                CompatibilityDetectionCode.Unknown);
        }

        var verdict = detected < minimum || detected > maximum
            ? CompatibilityVerdictState.Unsupported
            : detected == pinnedVersion && component.Severity == HealthSeverity.Ready
                ? CompatibilityVerdictState.Supported
                : CompatibilityVerdictState.Degraded;
        if (component.Severity == HealthSeverity.Critical)
        {
            verdict = CompatibilityVerdictState.Unsupported;
        }

        return new(
            "spotify",
            component.DetectedVersion,
            pinned,
            verdict,
            CompatibilityDetectionCode.Version);
    }

    private static CompatibilityVerdictItem BuildSpotX(
        StackHealthComponent? component,
        bool spotifyInstalled)
    {
        var pinned = $"{AppCatalog.PinnedSpotXVersion} / {AppCatalog.PinnedSpotXSpotifyVersion}";
        if (!spotifyInstalled || component is null)
        {
            return new(
                "spotx",
                null,
                pinned,
                CompatibilityVerdictState.Unknown,
                CompatibilityDetectionCode.NotChecked);
        }

        var (verdict, detectionCode) = component.Severity switch
        {
            HealthSeverity.Ready => (CompatibilityVerdictState.Supported, CompatibilityDetectionCode.Verified),
            HealthSeverity.Warning => (CompatibilityVerdictState.Degraded, CompatibilityDetectionCode.Unverified),
            HealthSeverity.Critical => (CompatibilityVerdictState.Unsupported, CompatibilityDetectionCode.Missing),
            _ => (CompatibilityVerdictState.Unknown, CompatibilityDetectionCode.NotChecked)
        };

        return new("spotx", null, pinned, verdict, detectionCode);
    }

    private static CompatibilityVerdictItem BuildSpicetify(
        StackHealthComponent? component,
        bool installed)
    {
        const string pinned = AppCatalog.PinnedSpicetifyCliVersion;
        if (!installed || component is null)
        {
            return new(
                "spicetify-cli",
                null,
                pinned,
                CompatibilityVerdictState.Unknown,
                CompatibilityDetectionCode.Missing);
        }

        if (!TryParseVersion(component.DetectedVersion, out var detected) ||
            !TryParseVersion(pinned, out var pinnedVersion))
        {
            return new(
                "spicetify-cli",
                component.DetectedVersion,
                pinned,
                CompatibilityVerdictState.Unknown,
                CompatibilityDetectionCode.Unknown);
        }

        var verdict = detected.Major > SpicetifyVersionSupport.SupportedMajor
            ? CompatibilityVerdictState.Unsupported
            : component.Severity switch
            {
                HealthSeverity.Critical => CompatibilityVerdictState.Unsupported,
                HealthSeverity.Warning => CompatibilityVerdictState.Degraded,
                _ when detected == pinnedVersion => CompatibilityVerdictState.Supported,
                _ => CompatibilityVerdictState.Degraded
            };

        return new(
            "spicetify-cli",
            component.DetectedVersion,
            pinned,
            verdict,
            CompatibilityDetectionCode.Version);
    }

    private static CompatibilityVerdictItem BuildMarketplace(
        StackHealthComponent? component,
        bool spicetifyInstalled,
        bool filesPresent,
        bool registered)
    {
        const string pinned = AppCatalog.PinnedMarketplaceVersion;
        if (!spicetifyInstalled || component is null)
        {
            return new(
                "marketplace",
                null,
                pinned,
                CompatibilityVerdictState.Unknown,
                CompatibilityDetectionCode.Unavailable);
        }

        if (!filesPresent || !registered)
        {
            var verdict = component.Severity is HealthSeverity.Warning or HealthSeverity.Critical
                ? CompatibilityVerdictState.Unsupported
                : CompatibilityVerdictState.Unknown;
            return new(
                "marketplace",
                null,
                pinned,
                verdict,
                filesPresent ? CompatibilityDetectionCode.Files : CompatibilityDetectionCode.Missing);
        }

        if (!TryParseVersion(component.DetectedVersion, out var detected) ||
            !TryParseVersion(pinned, out var pinnedVersion))
        {
            return new(
                "marketplace",
                component.DetectedVersion,
                pinned,
                CompatibilityVerdictState.Unknown,
                CompatibilityDetectionCode.Files);
        }

        var verdictWithHealth = component.Severity switch
        {
            HealthSeverity.Critical => CompatibilityVerdictState.Unsupported,
            HealthSeverity.Warning => CompatibilityVerdictState.Degraded,
            _ when detected == pinnedVersion => CompatibilityVerdictState.Supported,
            _ => CompatibilityVerdictState.Degraded
        };
        return new(
            "marketplace",
            component.DetectedVersion,
            pinned,
            verdictWithHealth,
            CompatibilityDetectionCode.Version);
    }

    private static StackHealthComponent? Find(StackHealthReport report, string id) =>
        report.Components.FirstOrDefault(component =>
            string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase));

    private static bool TryParseVersion(string? value, out Version version) =>
        SpotifyVersion.TryParse(value, out version);
}
