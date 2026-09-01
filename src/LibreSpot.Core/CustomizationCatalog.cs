using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibreSpot.Desktop.Models;

public sealed class CustomizationCatalogDocument
{
    public int SchemaVersion { get; init; }
    public string CatalogVersion { get; init; } = string.Empty;
    public CustomizationCatalogPins Pins { get; init; } = new();
    public List<string> FeatureGroups { get; init; } = [];
    public List<CustomizationFeatureDefinition> SpotifyFeatures { get; init; } = [];
    public List<SpotXFeatureOverrideDefinition> SpotXFeatureOverrides { get; init; } = [];
    public List<CustomizationControlDefinition> SpotXSwitches { get; init; } = [];
    public List<SpicetifyOptionDefinition> SpicetifyOptions { get; init; } = [];
    public List<CustomizationSnippetDefinition> Snippets { get; init; } = [];
    public List<CustomizationPresetDefinition> Presets { get; init; } = [];
    public List<CustomizationThemeDefinition> BuiltInThemes { get; init; } = [];
    public List<CustomizationThemeDefinition> Themes { get; init; } = [];
    public List<CustomizationAssetDefinition> Extensions { get; init; } = [];
    public List<CustomizationAssetDefinition> CustomApps { get; init; } = [];
}

public sealed class CustomizationCatalogPins
{
    public string SpotifyVersion { get; init; } = string.Empty;
    public string XpuiSha256 { get; init; } = string.Empty;
    public string SpotXCommit { get; init; } = string.Empty;
    public string SpicetifyVersion { get; init; } = string.Empty;
    public string MarketplaceVersion { get; init; } = string.Empty;
    public string ThemesCommit { get; init; } = string.Empty;
}

public sealed class CustomizationFeatureDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public JsonElement Default { get; init; }
    public List<string>? Values { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public string Group { get; init; } = string.Empty;
    public bool ServerGated { get; init; }
    public string Source { get; init; } = string.Empty;
    public SpotXForcedValueDefinition? SpotXForced { get; init; }
}

public sealed class SpotXForcedValueDefinition
{
    public JsonElement Value { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
}

public sealed class SpotXFeatureOverrideDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public JsonElement Value { get; init; }
    public string Mode { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public bool DeclaredBySpotify { get; init; }
}

public sealed class CustomizationControlDefinition
{
    public string Id { get; init; } = string.Empty;
    public string ConfigKey { get; init; } = string.Empty;
    public List<string>? RelatedConfigKeys { get; init; }
    public List<string> CliArguments { get; init; } = [];
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public JsonElement Default { get; init; }
    public List<string>? Values { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public bool Live { get; init; }
}

public sealed class SpicetifyOptionDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public JsonElement Default { get; init; }
    public bool Live { get; init; }
}

public sealed class CustomizationSnippetDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Css { get; init; } = string.Empty;
    public string Preview { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string SourceTitle { get; init; } = string.Empty;
    public string LastVerifiedSpotify { get; init; } = string.Empty;
    public bool Live { get; init; }
}

public sealed class CustomizationPresetDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public JsonElement Profile { get; init; }
}

public sealed class CustomizationThemeDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public List<string> Schemes { get; init; } = [];
    public string? ClassName { get; init; }
    public string? Css { get; init; }
    public string? Source { get; init; }
    public string? Commit { get; init; }
    public bool RequiresJs { get; init; }
    public bool MarketplaceOnly { get; init; }
    public string? SupportState { get; init; }
    public string? LastVerifiedSpotify { get; init; }
}

public sealed class CustomizationAssetDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? Commit { get; init; }
    public string? Version { get; init; }
    public string? Sha256 { get; init; }
    public string? License { get; init; }
    public string? SupportState { get; init; }
    public string LastVerifiedSpotify { get; init; } = string.Empty;
    public bool LiveToggle { get; init; }
}

public static class CustomizationCatalogLoader
{
    public const string ResourceName = "LibreSpot.Desktop.Schemas.librespot-customization.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static CustomizationCatalogDocument LoadEmbedded(Assembly? assembly = null)
    {
        assembly ??= typeof(CustomizationCatalogLoader).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidDataException($"Embedded customization catalog is missing: {ResourceName}");
        var catalog = JsonSerializer.Deserialize<CustomizationCatalogDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("The customization catalog is empty.");
        var errors = Validate(catalog);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                "The customization catalog is invalid: " + string.Join(" ", errors));
        }

        return catalog;
    }

    public static IReadOnlyList<string> Validate(CustomizationCatalogDocument catalog)
    {
        var errors = new List<string>();
        if (catalog.SchemaVersion != 1)
        {
            errors.Add($"Schema {catalog.SchemaVersion} is not supported.");
        }

        if (!string.Equals(catalog.Pins.SpotifyVersion, AppCatalog.PinnedSpotXSpotifyVersion, StringComparison.Ordinal) ||
            !string.Equals(catalog.Pins.SpotXCommit, AppCatalog.PinnedSpotXCommit, StringComparison.Ordinal) ||
            !string.Equals(catalog.Pins.SpicetifyVersion, AppCatalog.PinnedSpicetifyCliVersion, StringComparison.Ordinal) ||
            !string.Equals(catalog.Pins.MarketplaceVersion, AppCatalog.PinnedMarketplaceVersion, StringComparison.Ordinal) ||
            !string.Equals(catalog.Pins.ThemesCommit, AppCatalog.PinnedThemesCommit, StringComparison.Ordinal))
        {
            errors.Add("Catalog dependency pins do not match AppCatalog.");
        }

        if (catalog.Pins.XpuiSha256.Length != 64 ||
            !catalog.Pins.XpuiSha256.All(Uri.IsHexDigit))
        {
            errors.Add("The xpui.js SHA256 pin is invalid.");
        }

        ValidateUnique(catalog.SpotifyFeatures.Select(item => item.Name), "Spotify feature", errors);
        ValidateUnique(catalog.SpotXSwitches.Select(item => item.Id), "SpotX control", errors);
        ValidateUnique(catalog.SpicetifyOptions.Select(item => item.Id), "Spicetify option", errors);
        ValidateUnique(catalog.Snippets.Select(item => item.Id), "snippet", errors);
        if (catalog.SpotifyFeatures.Count < 300)
        {
            errors.Add("The Spotify feature catalog is incomplete.");
        }

        if (catalog.SpotXSwitches.Count < 30 || catalog.SpicetifyOptions.Count < 20)
        {
            errors.Add("The desktop customization controls are incomplete.");
        }

        if (catalog.Snippets.Any(item =>
                string.IsNullOrWhiteSpace(item.Source) ||
                item.LastVerifiedSpotify != catalog.Pins.SpotifyVersion ||
                item.Css.Contains(":has(", StringComparison.Ordinal)))
        {
            errors.Add("A reviewed snippet is missing provenance or uses an unsupported selector.");
        }

        var builtIns = catalog.BuiltInThemes.Select(theme => theme.Id).ToHashSet(StringComparer.Ordinal);
        if (!builtIns.IsSupersetOf(["Prism", "Compact", "Accessibility"]) || catalog.Presets.Count < 4)
        {
            errors.Add("The built-in theme or preset set is incomplete.");
        }

        return errors;
    }

    private static void ValidateUnique(IEnumerable<string> values, string label, ICollection<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                errors.Add($"A {label} ID is empty or duplicated.");
                return;
            }
        }
    }
}

public sealed class LibreSpotProfileDocument
{
    public int SchemaVersion { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Theme { get; init; } = string.Empty;
    public string Scheme { get; init; } = string.Empty;
    public Dictionary<string, Dictionary<string, string>> Schemes { get; init; } = new(StringComparer.Ordinal);

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; init; } = new(StringComparer.Ordinal);
}

public static class LibreSpotProfileCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static LibreSpotProfileDocument Parse(string source)
    {
        var profile = JsonSerializer.Deserialize<LibreSpotProfileDocument>(source, JsonOptions)
            ?? throw new InvalidDataException("The LibreSpot profile is empty.");
        if (profile.SchemaVersion != 1)
        {
            throw new InvalidDataException($"LibreSpot profile schema {profile.SchemaVersion} is not supported.");
        }

        if (string.IsNullOrWhiteSpace(profile.Name) ||
            string.IsNullOrWhiteSpace(profile.Theme) ||
            string.IsNullOrWhiteSpace(profile.Scheme) ||
            !profile.Schemes.ContainsKey(profile.Scheme))
        {
            throw new InvalidDataException("The LibreSpot profile identity or selected scheme is invalid.");
        }

        return profile;
    }

    public static string Serialize(LibreSpotProfileDocument profile) =>
        JsonSerializer.Serialize(profile, JsonOptions) + Environment.NewLine;
}
