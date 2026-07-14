using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public sealed class CommunityAssetDriftService
{
    private const int CacheSchemaVersion = 1;
    private const string SourceCache = "cache";
    private const string SourceUnavailable = "unavailable";
    private const string ManifestResourceName = "LibreSpot.Desktop.Schemas.community-assets.json";

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static CommunityAssetDriftService Default { get; } = new();

    private readonly IUpstreamMetadataClient _metadataClient;
    private readonly IReadOnlyList<CommunityAssetPin> _pins;
    private readonly string _cachePath;
    private readonly Func<DateTimeOffset> _clock;

    public CommunityAssetDriftService(
        IUpstreamMetadataClient? metadataClient = null,
        IReadOnlyList<CommunityAssetPin>? pins = null,
        string? cachePath = null,
        Func<DateTimeOffset>? clock = null)
    {
        _metadataClient = metadataClient ?? new GitHubUpstreamMetadataClient();
        _pins = pins ?? LoadPinsFromManifest();
        _cachePath = string.IsNullOrWhiteSpace(cachePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LibreSpot",
                "community-asset-drift-cache.json")
            : Path.GetFullPath(cachePath);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<CommunityAssetPin> Pins => _pins;

    public CommunityAssetDriftReport GetReport(bool allowNetwork = true) =>
        GetReportAsync(allowNetwork, CancellationToken.None).GetAwaiter().GetResult();

    public CommunityAssetDriftReport GetCachedReport() =>
        GetReport(allowNetwork: false);

    public async Task<CommunityAssetDriftReport> GetReportAsync(
        bool allowNetwork = true,
        CancellationToken cancellationToken = default)
    {
        var now = _clock();
        var cache = ReadCache();
        var cacheById = (cache?.Assets ?? Array.Empty<CommunityAssetState>())
            .OfType<CommunityAssetState>()
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var assets = new List<CommunityAssetState>(_pins.Count);
        var hasLiveResult = false;

        foreach (var pin in _pins)
        {
            var cached = cacheById.GetValueOrDefault(pin.Id);
            var state = allowNetwork
                ? await ResolveOnlineAsync(pin, cached, now, cancellationToken).ConfigureAwait(false)
                : ResolveFromCache(pin, cached, now, "No live community asset check was requested.");

            hasLiveResult |= !state.IsDegraded &&
                !string.Equals(state.MetadataSource, SourceCache, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(state.MetadataSource, SourceUnavailable, StringComparison.OrdinalIgnoreCase);
            assets.Add(state);
        }

        var report = new CommunityAssetDriftReport(assets, now);
        if (hasLiveResult)
        {
            WriteCache(report);
        }

        return report;
    }

    public static IReadOnlyList<CommunityAssetPin> LoadPinsFromManifest()
    {
        try
        {
            using var stream = OpenManifestStream();
            if (stream is null)
            {
                return Array.Empty<CommunityAssetPin>();
            }

            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var reviewRequiredLicenses = ReadReviewRequiredLicenses(root);
            var pins = new List<CommunityAssetPin>();

            foreach (var extension in root.GetProperty("extensions").EnumerateArray())
            {
                pins.Add(BuildPin(
                    extension,
                    id: "extension:" + RequiredString(extension, "filename"),
                    kind: "extension",
                    hashPropertyName: "sha256",
                    sourceUrl: RequiredString(extension, "sourceUrl"),
                    reviewRequiredLicenses));
            }

            foreach (var theme in root.GetProperty("themes").EnumerateArray())
            {
                var owner = RequiredString(theme, "owner");
                var repo = RequiredString(theme, "repo");
                var commit = RequiredString(theme, "commitSha");
                var folder = OptionalString(theme, "themeFolder");
                var sourceUrl = string.IsNullOrWhiteSpace(folder) || string.Equals(folder, ".", StringComparison.Ordinal)
                    ? $"https://github.com/{owner}/{repo}/tree/{commit}"
                    : $"https://github.com/{owner}/{repo}/tree/{commit}/{folder}";

                pins.Add(BuildPin(
                    theme,
                    id: "theme:" + RequiredString(theme, "themeId"),
                    kind: "theme",
                    hashPropertyName: "archiveSha256",
                    sourceUrl,
                    reviewRequiredLicenses));
            }

            foreach (var app in root.GetProperty("customApps").EnumerateArray())
            {
                pins.Add(BuildPin(
                    app,
                    id: "custom-app:" + RequiredString(app, "appId"),
                    kind: "custom-app",
                    hashPropertyName: "sha256",
                    sourceUrl: RequiredString(app, "sourceUrl"),
                    reviewRequiredLicenses));
            }

            return pins;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            System.Diagnostics.Debug.WriteLine($"CommunityAssetDriftService: manifest load failed: {ex.Message}");
            return Array.Empty<CommunityAssetPin>();
        }
    }

    private async Task<CommunityAssetState> ResolveOnlineAsync(
        CommunityAssetPin pin,
        CommunityAssetState? cached,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var lookup = await _metadataClient
            .TryGetGitReferenceAsync(ToDependencyPin(pin), cancellationToken)
            .ConfigureAwait(false);

        if (lookup.Success && !string.IsNullOrWhiteSpace(lookup.Value))
        {
            return BuildState(pin, lookup.Value, lookup.MetadataSource, now, null, isDegraded: false, reason: null);
        }

        if (string.Equals(lookup.FailureKind, "missing", StringComparison.OrdinalIgnoreCase))
        {
            return BuildState(
                pin,
                null,
                lookup.MetadataSource,
                now,
                null,
                isDegraded: true,
                reason: lookup.Message ?? "The source repository or branch was not found.",
                forcedDriftState: "missing");
        }

        return ResolveFromCache(pin, cached, now, DescribeFailure(lookup));
    }

    private CommunityAssetState ResolveFromCache(
        CommunityAssetPin pin,
        CommunityAssetState? cached,
        DateTimeOffset now,
        string reason)
    {
        if (cached is not null && !string.IsNullOrWhiteSpace(cached.LatestCommit))
        {
            return BuildState(
                pin,
                cached.LatestCommit,
                SourceCache,
                now,
                now - cached.CheckedAtUtc,
                isDegraded: true,
                reason);
        }

        return BuildState(pin, null, SourceUnavailable, now, null, isDegraded: true, reason, "degraded");
    }

    private static CommunityAssetState BuildState(
        CommunityAssetPin pin,
        string? latestRaw,
        string metadataSource,
        DateTimeOffset checkedAtUtc,
        TimeSpan? cacheAge,
        bool isDegraded,
        string? reason,
        string? forcedDriftState = null)
    {
        var latest = NormalizeCommit(latestRaw);
        var pinned = NormalizeCommit(pin.PinnedCommit) ?? pin.PinnedCommit;
        var driftState = forcedDriftState ?? DetermineDriftState(pinned, latest);
        var gitRepository = GitRepository(pin);
        var gitReference = GitReference(pin);
        var evidence = BuildEvidence(pin, latest, driftState, metadataSource, cacheAge, isDegraded, reason);

        return new CommunityAssetState(
            pin.Id,
            pin.Kind,
            pin.Name,
            pin.SourceUrl ?? gitRepository,
            gitRepository,
            gitReference,
            pinned,
            pin.PinnedHash,
            latest,
            driftState,
            metadataSource,
            checkedAtUtc,
            cacheAge,
            isDegraded,
            pin.License,
            pin.SupportState,
            pin.FallbackBehavior,
            pin.NetworkBehavior,
            pin.NetworkDetail,
            pin.RequiresTrustReview,
            evidence)
        {
            ReleaseNotesUrl = pin.ReleaseNotesUrl,
            LastVerifiedAtUtc = pin.LastVerifiedAtUtc
        };
    }

    private static string BuildEvidence(
        CommunityAssetPin pin,
        string? latest,
        string driftState,
        string metadataSource,
        TimeSpan? cacheAge,
        bool isDegraded,
        string? reason)
    {
        var hash = string.IsNullOrWhiteSpace(pin.PinnedHash) ? "none" : pin.PinnedHash;
        var latestDisplay = string.IsNullOrWhiteSpace(latest) ? "unknown" : latest;
        var cacheDisplay = cacheAge.HasValue ? FormatAge(cacheAge.Value) : "none";
        var review = pin.RequiresTrustReview ? "review required" : "review not required";
        var evidence =
            $"Pinned commit {pin.PinnedCommit}; latest {latestDisplay}; drift {driftState}; hash {hash}; license {pin.License}; support {pin.SupportState}; fallback {pin.FallbackBehavior}; network {pin.NetworkBehavior}; trust {review}; source {metadataSource}; cache age {cacheDisplay}.";

        if (!string.IsNullOrWhiteSpace(pin.NetworkDetail))
        {
            evidence += $" Network detail: {pin.NetworkDetail}";
        }

        if (isDegraded)
        {
            evidence += " Live community asset metadata is degraded; the report is using cached data or an unknown latest value.";
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            evidence += $" Detail: {reason}";
        }

        return evidence;
    }

    private static string DetermineDriftState(string pinned, string? latest)
    {
        if (string.IsNullOrWhiteSpace(latest))
        {
            return "degraded";
        }

        return string.Equals(pinned, latest, StringComparison.OrdinalIgnoreCase)
            ? "current"
            : "behind";
    }

    private CacheDocument? ReadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            var cache = JsonSerializer.Deserialize<CacheDocument>(File.ReadAllText(_cachePath), CacheJsonOptions);
            return cache is { SchemaVersion: CacheSchemaVersion, Assets: not null }
                ? cache
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void WriteCache(CommunityAssetDriftReport report)
    {
        try
        {
            var cache = new CacheDocument(CacheSchemaVersion, report.GeneratedAtUtc, report.Assets.ToArray());
            DriftCacheFile.WriteAllTextAtomically(
                _cachePath,
                JsonSerializer.Serialize(cache, CacheJsonOptions));
        }
        catch
        {
            // Community asset drift must not make the health snapshot fail.
        }
    }

    private static UpstreamDependencyPin ToDependencyPin(CommunityAssetPin pin) =>
        new(
            pin.Id,
            pin.Name,
            pin.PinnedCommit,
            "commit",
            GitRepository(pin),
            GitReference(pin),
            null,
            null);

    private static string GitRepository(CommunityAssetPin pin) =>
        $"https://github.com/{pin.Owner}/{pin.Repository}.git";

    private static string GitReference(CommunityAssetPin pin) =>
        $"refs/heads/{pin.Branch}";

    private static CommunityAssetPin BuildPin(
        JsonElement asset,
        string id,
        string kind,
        string hashPropertyName,
        string sourceUrl,
        IReadOnlySet<string> reviewRequiredLicenses)
    {
        var license = RequiredString(asset, "spdxLicense");
        var requiresTrustReview =
            string.Equals(license, "NOASSERTION", StringComparison.OrdinalIgnoreCase) ||
            reviewRequiredLicenses.Contains(license);

        return new CommunityAssetPin(
            id,
            kind,
            RequiredString(asset, "displayName"),
            RequiredString(asset, "owner"),
            RequiredString(asset, "repo"),
            RequiredString(asset, "branch"),
            RequiredString(asset, "commitSha"),
            OptionalString(asset, hashPropertyName),
            sourceUrl,
            license,
            RequiredString(asset, "supportState"),
            RequiredString(asset, "fallbackBehavior"),
            RequiredString(asset, "networkBehavior"),
            OptionalString(asset, "networkDetail"),
            requiresTrustReview)
        {
            ReleaseNotesUrl = RequiredString(asset, "releaseNotesUrl"),
            LastVerifiedAtUtc = RequiredDate(asset, "lastVerifiedDate")
        };
    }

    private static Stream? OpenManifestStream()
    {
        var assembly = typeof(CommunityAssetDriftService).Assembly;
        var embedded = assembly.GetManifestResourceStream(ManifestResourceName);
        if (embedded is not null)
        {
            return embedded;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "schemas", "community-assets.json");
            if (File.Exists(candidate))
            {
                return File.OpenRead(candidate);
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static IReadOnlySet<string> ReadReviewRequiredLicenses(JsonElement root)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("policy", out var policy) &&
            policy.TryGetProperty("reviewRequiredLicenses", out var reviewRequired) &&
            reviewRequired.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in reviewRequired.EnumerateArray())
            {
                var license = item.GetString();
                if (!string.IsNullOrWhiteSpace(license))
                {
                    values.Add(license);
                }
            }
        }

        return values;
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = OptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Community asset manifest entry is missing '{propertyName}'.");
        }

        return value;
    }

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.ToString();
    }

    private static DateTimeOffset RequiredDate(JsonElement element, string propertyName)
    {
        var value = RequiredString(element, propertyName);
        if (!DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new InvalidDataException(
                $"Community asset manifest entry has invalid '{propertyName}' value '{value}'; expected YYYY-MM-DD.");
        }

        return parsed;
    }

    private static string? NormalizeCommit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string DescribeFailure(UpstreamMetadataLookupResult result)
    {
        var kind = string.IsNullOrWhiteSpace(result.FailureKind) ? "failed" : result.FailureKind;
        return $"{result.MetadataSource} {kind}: {result.Message ?? "no value returned"}";
    }

    private static string FormatAge(TimeSpan age)
    {
        var duration = age.Duration();
        if (duration.TotalMinutes < 1)
        {
            return "less than 1 minute";
        }

        if (duration.TotalHours < 1)
        {
            var minutes = (int)Math.Round(duration.TotalMinutes);
            return minutes == 1 ? "1 minute" : $"{minutes.ToString(CultureInfo.InvariantCulture)} minutes";
        }

        if (duration.TotalDays < 1)
        {
            var hours = (int)Math.Round(duration.TotalHours);
            return hours == 1 ? "1 hour" : $"{hours.ToString(CultureInfo.InvariantCulture)} hours";
        }

        var days = (int)Math.Round(duration.TotalDays);
        return days == 1 ? "1 day" : $"{days.ToString(CultureInfo.InvariantCulture)} days";
    }

    private sealed record CacheDocument(
        int SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        CommunityAssetState[] Assets);
}
