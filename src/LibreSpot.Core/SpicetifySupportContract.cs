using System.Text.Json;
using System.Text.RegularExpressions;

namespace LibreSpot.Desktop.Models;

public enum SpicetifySupportVerdict
{
    Unknown,
    Allowlisted,
    Degraded,
    Refused
}

public sealed record SpicetifySupportRange(Version Minimum, Version Maximum, string? Note);

public sealed record SpicetifySupportMap(
    string Version,
    string ClassmapKey,
    string Status,
    string? Note);

public sealed record SpicetifySupportResult(
    string? RawVersion,
    string? NormalizedVersion,
    SpicetifySupportVerdict Verdict,
    bool ListAvailable,
    bool VersionDetected,
    string? MapStatus,
    string? ClassmapKey,
    string? FallbackVersion,
    string? FallbackClassmapKey,
    string Reason)
{
    public bool CanApply => !ListAvailable || Verdict != SpicetifySupportVerdict.Refused;

    public bool CanAutoApply => VersionDetected && (!ListAvailable || Verdict != SpicetifySupportVerdict.Refused);

    public int SupportCommandExitCode => ListAvailable && Verdict == SpicetifySupportVerdict.Refused ? 1 : 0;

    public static SpicetifySupportResult NotApplicable() => new(
        null,
        null,
        SpicetifySupportVerdict.Unknown,
        false,
        false,
        null,
        null,
        null,
        null,
        "The v3 Spotify support contract is inactive for the pinned Spicetify 2.x integration.");

    public static SpicetifySupportResult Unavailable(string? rawVersion, string reason) => new(
        rawVersion,
        TryNormalizeVersion(rawVersion, out var normalized) ? normalized.ToString(3) : null,
        SpicetifySupportVerdict.Unknown,
        false,
        TryNormalizeVersion(rawVersion, out _),
        null,
        null,
        null,
        null,
        reason);

    private static bool TryNormalizeVersion(string? rawVersion, out Version version) =>
        SpicetifySupportContract.TryNormalizeVersion(rawVersion, out version);
}

public sealed class SpicetifySupportContract
{
    private static readonly Regex SpotifyVersionPattern = new(
        @"^\s*[vV]?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:[.\-+]|\s|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> MapStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "classic",
        "modular",
        "none"
    };

    private readonly IReadOnlyDictionary<string, SpicetifySupportMap> _maps;
    private readonly IReadOnlyDictionary<string, string> _notes;

    private SpicetifySupportContract(
        int schemaVersion,
        string policy,
        string defaultMapStatus,
        IReadOnlyList<Version> versions,
        IReadOnlyList<SpicetifySupportRange> ranges,
        IReadOnlyDictionary<string, SpicetifySupportMap> maps,
        IReadOnlyDictionary<string, string> notes)
    {
        SchemaVersion = schemaVersion;
        Policy = policy;
        DefaultMapStatus = defaultMapStatus;
        ExactVersions = versions;
        Ranges = ranges;
        _maps = maps;
        _notes = notes;
    }

    public int SchemaVersion { get; }
    public string Policy { get; }
    public string DefaultMapStatus { get; }
    public IReadOnlyList<Version> ExactVersions { get; }
    public IReadOnlyList<SpicetifySupportRange> Ranges { get; }
    public IReadOnlyDictionary<string, SpicetifySupportMap> Maps => _maps;
    public IReadOnlyDictionary<string, string> Notes => _notes;

    public static bool TryParse(string? json, out SpicetifySupportContract? contract, out string error)
    {
        contract = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The Spicetify supported-versions document is empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The Spicetify supported-versions document must be a JSON object.";
                return false;
            }

            if (!TryGetInt(root, "schema_version", out var schemaVersion) || schemaVersion is not (1 or 2))
            {
                error = "The Spicetify supported-versions schema version is unsupported.";
                return false;
            }

            var policy = GetOptionalString(root, "policy") ?? "allowlist";
            if (!string.Equals(policy, "allowlist", StringComparison.OrdinalIgnoreCase))
            {
                error = "The Spicetify supported-versions policy is unsupported.";
                return false;
            }

            var defaultMapStatus = GetOptionalString(root, "default_map_status") ?? "classic";
            if (!MapStatuses.Contains(defaultMapStatus))
            {
                error = "The Spicetify supported-versions default map status is invalid.";
                return false;
            }

            if (!root.TryGetProperty("versions", out var versionsElement) || versionsElement.ValueKind != JsonValueKind.Array)
            {
                error = "The Spicetify supported-versions document has no versions array.";
                return false;
            }

            var versions = new List<Version>();
            var versionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in versionsElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String || !TryNormalizeVersion(element.GetString(), out var version))
                {
                    error = "The Spicetify supported-versions document contains an invalid exact version.";
                    return false;
                }

                var key = VersionKey(version);
                if (!versionKeys.Add(key))
                {
                    error = $"The Spicetify supported-versions document repeats exact version {key}.";
                    return false;
                }
                versions.Add(version);
            }

            var ranges = new List<SpicetifySupportRange>();
            if (root.TryGetProperty("ranges", out var rangesElement))
            {
                if (rangesElement.ValueKind != JsonValueKind.Array)
                {
                    error = "The Spicetify supported-versions ranges value is not an array.";
                    return false;
                }

                foreach (var rangeElement in rangesElement.EnumerateArray())
                {
                    if (rangeElement.ValueKind != JsonValueKind.Object ||
                        !TryGetVersionProperty(rangeElement, "min", out var minimum) ||
                        !TryGetVersionProperty(rangeElement, "max", out var maximum) ||
                        minimum.CompareTo(maximum) > 0)
                    {
                        error = "The Spicetify supported-versions document contains an invalid range.";
                        return false;
                    }

                    ranges.Add(new SpicetifySupportRange(
                        minimum,
                        maximum,
                        GetOptionalString(rangeElement, "note")));
                }
            }

            var maps = new Dictionary<string, SpicetifySupportMap>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("maps", out var mapsElement))
            {
                if (schemaVersion < 2 || mapsElement.ValueKind != JsonValueKind.Object)
                {
                    error = "The Spicetify supported-versions maps value is invalid for this schema version.";
                    return false;
                }

                foreach (var mapProperty in mapsElement.EnumerateObject())
                {
                    if (!TryNormalizeVersion(mapProperty.Name, out var version) ||
                        mapProperty.Value.ValueKind != JsonValueKind.Object)
                    {
                        error = "The Spicetify supported-versions document contains an invalid map key.";
                        return false;
                    }

                    var key = VersionKey(version);
                    if (maps.ContainsKey(key))
                    {
                        error = $"The Spicetify supported-versions document repeats map version {key}.";
                        return false;
                    }

                    var status = GetOptionalString(mapProperty.Value, "status") ?? defaultMapStatus;
                    if (!MapStatuses.Contains(status))
                    {
                        error = $"The Spicetify map status for {key} is invalid.";
                        return false;
                    }

                    var classmapKey = GetOptionalString(mapProperty.Value, "classmap_key") ?? DefaultClassmapKey(version);
                    maps[key] = new SpicetifySupportMap(
                        key,
                        classmapKey,
                        status.ToLowerInvariant(),
                        GetOptionalString(mapProperty.Value, "note"));
                }
            }

            var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("notes", out var notesElement))
            {
                if (notesElement.ValueKind != JsonValueKind.Object)
                {
                    error = "The Spicetify supported-versions notes value is invalid.";
                    return false;
                }

                foreach (var note in notesElement.EnumerateObject())
                {
                    if (note.Value.ValueKind != JsonValueKind.String)
                    {
                        error = "The Spicetify supported-versions notes must contain strings.";
                        return false;
                    }
                    notes[note.Name] = note.Value.GetString() ?? string.Empty;
                }
            }

            contract = new SpicetifySupportContract(
                schemaVersion,
                policy.ToLowerInvariant(),
                defaultMapStatus.ToLowerInvariant(),
                versions.AsReadOnly(),
                ranges.AsReadOnly(),
                new Dictionary<string, SpicetifySupportMap>(maps, StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(notes, StringComparer.OrdinalIgnoreCase));
            return true;
        }
        catch (JsonException exception)
        {
            error = $"The Spicetify supported-versions document is malformed: {exception.Message}";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            error = $"The Spicetify supported-versions document is invalid: {exception.Message}";
            return false;
        }
    }

    public SpicetifySupportResult Evaluate(string? rawVersion)
    {
        if (!TryNormalizeVersion(rawVersion, out var version))
        {
            return new SpicetifySupportResult(
                rawVersion,
                null,
                SpicetifySupportVerdict.Unknown,
                true,
                false,
                null,
                null,
                null,
                null,
                "The installed Spotify version could not be normalized, so the v3 support check remains fail-open.");
        }

        var normalized = version.ToString(3);
        if (ExactVersions.Any(candidate => candidate.CompareTo(version) == 0) ||
            Ranges.Any(range => range.Minimum.CompareTo(version) <= 0 && range.Maximum.CompareTo(version) >= 0))
        {
            _maps.TryGetValue(normalized, out var map);
            return new SpicetifySupportResult(
                rawVersion,
                normalized,
                SpicetifySupportVerdict.Allowlisted,
                true,
                true,
                map?.Status ?? DefaultMapStatus,
                map?.ClassmapKey,
                null,
                null,
                "The Spotify version is allowlisted by the v3 support contract.");
        }

        var fallback = _maps.Values
            .Where(map => string.Equals(map.Status, "modular", StringComparison.OrdinalIgnoreCase) &&
                          TryNormalizeVersion(map.Version, out var mapVersion) &&
                          mapVersion.Major == version.Major &&
                          mapVersion.Minor == version.Minor &&
                          mapVersion.CompareTo(version) < 0)
            .OrderByDescending(map => Version.Parse(map.Version))
            .FirstOrDefault();
        if (fallback is not null)
        {
            return new SpicetifySupportResult(
                rawVersion,
                normalized,
                SpicetifySupportVerdict.Degraded,
                true,
                true,
                fallback.Status,
                fallback.ClassmapKey,
                fallback.Version,
                fallback.ClassmapKey,
                $"The Spotify version is outside the allowlist, so the nearest lower modular classmap {fallback.Version} is used.");
        }

        return new SpicetifySupportResult(
            rawVersion,
            normalized,
            SpicetifySupportVerdict.Refused,
            true,
            true,
            null,
            null,
            null,
            null,
            "The Spotify version is outside the allowlist and has no same-minor lower modular classmap fallback.");
    }

    internal static bool TryNormalizeVersion(string? rawVersion, out Version version)
    {
        version = new Version(0, 0, 0);
        var match = SpotifyVersionPattern.Match(rawVersion ?? string.Empty);
        if (!match.Success ||
            !int.TryParse(match.Groups["major"].Value, out var major) ||
            !int.TryParse(match.Groups["minor"].Value, out var minor) ||
            !int.TryParse(match.Groups["patch"].Value, out var patch))
        {
            return false;
        }

        version = new Version(major, minor, patch);
        return true;
    }

    private static bool TryGetVersionProperty(JsonElement element, string propertyName, out Version version)
    {
        version = new Version(0, 0, 0);
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               TryNormalizeVersion(property.GetString(), out version);
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string VersionKey(Version version) => version.ToString(3);

    private static string DefaultClassmapKey(Version version) =>
        $"{version.Major}{version.Minor:D2}{version.Build:D4}";
}

public sealed record SpicetifyV3SupportContractReport(
    bool IsFeatureActive,
    SpicetifySupportResult Result);
