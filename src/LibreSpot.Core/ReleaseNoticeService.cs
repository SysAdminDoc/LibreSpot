using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibreSpot.Desktop.Services;

public enum ReleaseNoticeLookupStatus
{
    Found,
    NotModified,
    RateLimited,
    Offline,
    Missing,
    Malformed
}

public sealed record ReleaseNoticeLookup(
    ReleaseNoticeLookupStatus Status,
    string? TagName,
    string? HtmlUrl,
    bool IsPrerelease,
    string? ETag,
    string Message)
{
    public static ReleaseNoticeLookup Found(string tagName, string? htmlUrl, bool isPrerelease, string? etag) =>
        new(ReleaseNoticeLookupStatus.Found, tagName, htmlUrl, isPrerelease, etag, "Latest release read from GitHub.");

    public static ReleaseNoticeLookup NotModified(string? etag) =>
        new(ReleaseNoticeLookupStatus.NotModified, null, null, false, etag, "GitHub reported the cached release is still current.");

    public static ReleaseNoticeLookup RateLimited(string message) =>
        new(ReleaseNoticeLookupStatus.RateLimited, null, null, false, null, message);

    public static ReleaseNoticeLookup Offline(string message) =>
        new(ReleaseNoticeLookupStatus.Offline, null, null, false, null, message);

    public static ReleaseNoticeLookup Missing(string message) =>
        new(ReleaseNoticeLookupStatus.Missing, null, null, false, null, message);

    public static ReleaseNoticeLookup Malformed(string message) =>
        new(ReleaseNoticeLookupStatus.Malformed, null, null, false, null, message);
}

public interface IReleaseNoticeClient
{
    /// <summary>
    /// Reads the latest stable LibreSpot release. <paramref name="cachedETag"/>
    /// is sent as If-None-Match, which saves bandwidth but not budget: GitHub
    /// exempts a conditional 304 from the rate limit only for authenticated
    /// requests, and these are anonymous, so every call counts against the 60 an
    /// hour an unauthenticated client gets. What actually bounds how often this
    /// runs is the 24 hour cache in <see cref="ReleaseNoticeService"/>.
    /// </summary>
    Task<ReleaseNoticeLookup> TryGetLatestStableAsync(string? cachedETag, CancellationToken cancellationToken);
}

/// <summary>What Home shows: nothing, or one quiet link to a newer stable release.</summary>
public sealed record ReleaseNotice(bool UpdateAvailable, string? LatestVersion, string? ReleaseUrl, string Source, string Reason)
{
    public static ReleaseNotice Silent(string source, string reason) => new(false, null, null, source, reason);
}

/// <summary>
/// Semantic version used for release ordering: major.minor.patch with an
/// optional prerelease suffix. A stable version outranks any prerelease of the
/// same core, and numeric prerelease identifiers compare numerically.
/// </summary>
public readonly record struct ReleaseVersion(int Major, int Minor, int Patch, string? Prerelease) : IComparable<ReleaseVersion>
{
    public bool IsStable => Prerelease is null;

    public static bool TryParse(string? value, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var buildSeparator = text.IndexOf('+', StringComparison.Ordinal);
        if (buildSeparator >= 0)
        {
            text = text[..buildSeparator];
        }

        string? prerelease = null;
        var prereleaseSeparator = text.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseSeparator >= 0)
        {
            prerelease = text[(prereleaseSeparator + 1)..];
            text = text[..prereleaseSeparator];
            if (prerelease.Length == 0)
            {
                return false;
            }
        }

        var parts = text.Split('.');
        if (parts.Length is < 2 or > 3)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out numbers[i]))
            {
                return false;
            }
        }

        version = new ReleaseVersion(numbers[0], numbers[1], numbers[2], prerelease);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        var core = Major.CompareTo(other.Major);
        if (core == 0) core = Minor.CompareTo(other.Minor);
        if (core == 0) core = Patch.CompareTo(other.Patch);
        if (core != 0)
        {
            return core;
        }

        if (IsStable || other.IsStable)
        {
            return IsStable.CompareTo(other.IsStable);
        }

        var left = Prerelease!.Split('.');
        var right = other.Prerelease!.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var leftNumeric = int.TryParse(left[i], out var leftNumber);
            var rightNumeric = int.TryParse(right[i], out var rightNumber);
            var part = (leftNumeric, rightNumeric) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(left[i], right[i])
            };
            if (part != 0)
            {
                return part;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    public override string ToString() =>
        Prerelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Prerelease}";
}

/// <summary>
/// Decides whether Home should show the quiet "Update LibreSpot" link. The
/// check reads the latest stable GitHub release through a conditional
/// request, keeps a 24-hour local cache, never selects a prerelease, and
/// stays silent on any failure that has no valid cache behind it.
/// </summary>
public sealed class ReleaseNoticeService
{
    public const string LatestStableReleaseApi = "https://api.github.com/repos/SysAdminDoc/LibreSpot/releases/latest";
    public const string LatestStableReleasePage = "https://github.com/SysAdminDoc/LibreSpot/releases/latest";
    public static readonly TimeSpan DefaultCacheLifetime = TimeSpan.FromHours(24);

    private const int CacheSchemaVersion = 2;
    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static ReleaseNoticeService Default { get; } = new();

    private readonly IReleaseNoticeClient _client;
    private readonly string _cachePath;
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _cacheLifetime;

    public ReleaseNoticeService(
        IReleaseNoticeClient? client = null,
        string? cachePath = null,
        Func<DateTimeOffset>? clock = null,
        TimeSpan? cacheLifetime = null)
    {
        _client = client ?? new GitHubReleaseNoticeClient();
        _cachePath = string.IsNullOrWhiteSpace(cachePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LibreSpot",
                "release-notice-cache.json")
            : Path.GetFullPath(cachePath);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _cacheLifetime = cacheLifetime ?? DefaultCacheLifetime;
    }

    public async Task<ReleaseNotice> GetNoticeAsync(string currentVersion, CancellationToken cancellationToken)
    {
        if (!ReleaseVersion.TryParse(currentVersion, out var current))
        {
            return ReleaseNotice.Silent("local", $"The running version '{currentVersion}' is not a release version.");
        }

        var now = _clock();
        var cache = ReadCache();
        if (cache is not null && now >= cache.CheckedAtUtc && now - cache.CheckedAtUtc < _cacheLifetime)
        {
            if (string.IsNullOrWhiteSpace(cache.TagName))
            {
                return ReleaseNotice.Silent("cache", "The last release check was refused and the next one is not due yet.");
            }

            // Age is reported from when the release was actually read, not from
            // the last time the service spoke to GitHub, so a run of refusals
            // cannot describe a month-old answer as less than a day old.
            var fetchedAt = cache.FetchedAtUtc ?? cache.CheckedAtUtc;
            var reason = now - fetchedAt < _cacheLifetime
                ? "The cached release is less than a day old."
                : $"The cached release was last confirmed {FormatAge(now - fetchedAt)} ago; checks since then were refused.";
            return Evaluate(current, cache.TagName, cache.HtmlUrl, "cache", reason);
        }

        ReleaseNoticeLookup lookup;
        try
        {
            lookup = await _client.TryGetLatestStableAsync(cache?.ETag, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ReleaseNotice.Silent("canceled", "The release check was canceled.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ReleaseNotice.Silent("canceled", "The release check was canceled.");
        }

        switch (lookup.Status)
        {
            case ReleaseNoticeLookupStatus.Found when !lookup.IsPrerelease && !string.IsNullOrWhiteSpace(lookup.TagName):
                WriteCache(new CacheDocument(CacheSchemaVersion, now, lookup.TagName, lookup.HtmlUrl, lookup.ETag, now));
                return Evaluate(current, lookup.TagName, lookup.HtmlUrl, "live", lookup.Message);

            case ReleaseNoticeLookupStatus.Found:
                // A prerelease or an empty tag is never offered; fall back like any other miss.
                return FallBack(current, cache, "live", "The latest release response was a prerelease or had no tag.");

            case ReleaseNoticeLookupStatus.NotModified when cache is not null:
                WriteCache(cache with { CheckedAtUtc = now, ETag = lookup.ETag ?? cache.ETag, FetchedAtUtc = now });
                return Evaluate(current, cache.TagName, cache.HtmlUrl, "live-conditional", lookup.Message);

            case ReleaseNoticeLookupStatus.RateLimited:
                // Being told to slow down and then retrying on the next launch is
                // how an anonymous client burns the rest of its hourly budget. The
                // machine with nothing cached is the one that needs this most: a
                // fresh install behind a shared address can arrive already over
                // the limit and would otherwise ask again every time it starts.
                // FetchedAtUtc is deliberately not moved.
                WriteCache(cache is null
                    ? new CacheDocument(CacheSchemaVersion, now, null, null, null)
                    : cache with { CheckedAtUtc = now });
                return cache is null
                    ? ReleaseNotice.Silent("rate-limited", lookup.Message)
                    : Evaluate(current, cache.TagName, cache.HtmlUrl, "cache-stale", lookup.Message);

            default:
                return FallBack(current, cache, lookup.Status.ToString().ToLowerInvariant(), lookup.Message);
        }
    }

    private static ReleaseNotice FallBack(ReleaseVersion current, CacheDocument? cache, string source, string reason) =>
        cache is null
            ? ReleaseNotice.Silent(source, reason)
            : Evaluate(current, cache.TagName, cache.HtmlUrl, "cache-stale", reason);

    private static string FormatAge(TimeSpan age) =>
        age.TotalDays >= 2
            ? $"{(int)age.TotalDays} days"
            : age.TotalHours >= 2
                ? $"{(int)age.TotalHours} hours"
                : "under an hour";

    private static ReleaseNotice Evaluate(ReleaseVersion current, string? tagName, string? htmlUrl, string source, string reason)
    {
        if (!ReleaseVersion.TryParse(tagName, out var latest))
        {
            return ReleaseNotice.Silent(source, $"The release tag '{tagName}' is not a release version.");
        }

        if (!latest.IsStable)
        {
            return ReleaseNotice.Silent(source, $"The release tag '{tagName}' is a prerelease.");
        }

        return latest.CompareTo(current) > 0
            ? new ReleaseNotice(true, latest.ToString(), IsTrustedReleaseUrl(htmlUrl) ? htmlUrl! : LatestStableReleasePage, source, reason)
            : ReleaseNotice.Silent(source, $"Version {current} is current; latest stable is {latest}.");
    }

    /// <summary>Only an https GitHub page for this repository may be opened from the notice.</summary>
    public static bool IsTrustedReleaseUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith("/SysAdminDoc/LibreSpot/", StringComparison.OrdinalIgnoreCase);

    private CacheDocument? ReadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            var cache = JsonSerializer.Deserialize<CacheDocument>(File.ReadAllText(_cachePath), CacheJsonOptions);
            // A record with no tag is a backoff marker and is still meaningful: it
            // is the only thing standing between a rate-limited machine with no
            // cached release and a request on every single launch.
            return cache is { SchemaVersion: CacheSchemaVersion } ? cache : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void WriteCache(CacheDocument cache)
    {
        try
        {
            DriftCacheFile.WriteAllTextAtomically(_cachePath, JsonSerializer.Serialize(cache, CacheJsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A cache that cannot be written only costs one more request tomorrow.
        }
    }

    /// <param name="CheckedAtUtc">When the service last spoke to GitHub. Throttles requests.</param>
    /// <param name="FetchedAtUtc">
    /// When <paramref name="TagName"/> was actually read from a response. A rate-limited
    /// answer moves CheckedAtUtc and leaves this alone, so backing off cannot make an old
    /// release look freshly confirmed.
    /// </param>
    /// <param name="TagName">Null for a record that carries nothing but a backoff.</param>
    private sealed record CacheDocument(
        int SchemaVersion,
        DateTimeOffset CheckedAtUtc,
        string? TagName,
        string? HtmlUrl,
        string? ETag,
        DateTimeOffset? FetchedAtUtc = null);
}

public sealed class GitHubReleaseNoticeClient : IReleaseNoticeClient
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);
    private static readonly HttpClient SharedHttpClient = new() { Timeout = DefaultTimeout };

    private readonly HttpClient _httpClient;

    public GitHubReleaseNoticeClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<ReleaseNoticeLookup> TryGetLatestStableAsync(string? cachedETag, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleaseNoticeService.LatestStableReleaseApi);
            request.Headers.UserAgent.ParseAdd("LibreSpot-ReleaseNotice/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrWhiteSpace(cachedETag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", cachedETag);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var etag = response.Headers.ETag?.ToString();

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return ReleaseNoticeLookup.NotModified(etag ?? cachedETag);
            }

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                return ReleaseNoticeLookup.RateLimited($"HTTP {(int)response.StatusCode} while reading the latest release.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ReleaseNoticeLookup.Missing($"HTTP {(int)response.StatusCode} while reading the latest release.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("tag_name", out var tag)
                || tag.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(tag.GetString()))
            {
                return ReleaseNoticeLookup.Malformed("The latest release response did not contain tag_name.");
            }

            var htmlUrl = root.TryGetProperty("html_url", out var url) && url.ValueKind == JsonValueKind.String ? url.GetString() : null;
            var isPrerelease = (root.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True)
                || (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True);
            return ReleaseNoticeLookup.Found(tag.GetString()!, htmlUrl, isPrerelease, etag);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ReleaseNoticeLookup.Offline("The release API request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return ReleaseNoticeLookup.Offline(ex.Message);
        }
        catch (JsonException ex)
        {
            return ReleaseNoticeLookup.Malformed($"Invalid release JSON: {ex.Message}");
        }
    }
}
