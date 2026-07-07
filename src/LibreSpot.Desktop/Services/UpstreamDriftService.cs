using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public interface IUpstreamMetadataClient
{
    Task<UpstreamMetadataLookupResult> TryGetLatestReleaseAsync(
        UpstreamDependencyPin pin,
        CancellationToken cancellationToken);

    Task<UpstreamMetadataLookupResult> TryGetGitReferenceAsync(
        UpstreamDependencyPin pin,
        CancellationToken cancellationToken);
}

public sealed record UpstreamMetadataLookupResult(
    bool Success,
    string? Value,
    string MetadataSource,
    string? FailureKind,
    string? Message)
{
    public static UpstreamMetadataLookupResult Found(string value, string source) =>
        new(true, value, source, null, null);

    public static UpstreamMetadataLookupResult RateLimited(string source, string message) =>
        new(false, null, source, "rate-limit", message);

    public static UpstreamMetadataLookupResult Offline(string source, string message) =>
        new(false, null, source, "offline", message);

    public static UpstreamMetadataLookupResult Missing(string source, string message) =>
        new(false, null, source, "missing", message);

    public static UpstreamMetadataLookupResult Unavailable(string source, string message) =>
        new(false, null, source, "unavailable", message);
}

public sealed class UpstreamDriftService
{
    private const int CacheSchemaVersion = 1;
    private const string SourceCache = "cache";
    private const string SourceUnavailable = "unavailable";

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static UpstreamDriftService Default { get; } = new();

    private readonly IUpstreamMetadataClient _metadataClient;
    private readonly IReadOnlyList<UpstreamDependencyPin> _pins;
    private readonly string _cachePath;
    private readonly Func<DateTimeOffset> _clock;

    public UpstreamDriftService(
        IUpstreamMetadataClient? metadataClient = null,
        IReadOnlyList<UpstreamDependencyPin>? pins = null,
        string? cachePath = null,
        Func<DateTimeOffset>? clock = null)
    {
        _metadataClient = metadataClient ?? new GitHubUpstreamMetadataClient();
        _pins = pins ?? AppCatalog.UpstreamDependencyPins;
        _cachePath = string.IsNullOrWhiteSpace(cachePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LibreSpot",
                "upstream-drift-cache.json")
            : Path.GetFullPath(cachePath);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public UpstreamDriftReport GetReport(bool allowNetwork = true) =>
        GetReportAsync(allowNetwork, CancellationToken.None).GetAwaiter().GetResult();

    public UpstreamDriftReport GetCachedReport() =>
        GetReport(allowNetwork: false);

    public async Task<UpstreamDriftReport> GetReportAsync(
        bool allowNetwork = true,
        CancellationToken cancellationToken = default)
    {
        var now = _clock();
        var cache = ReadCache();
        var cacheById = cache?.Dependencies.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, UpstreamDependencyState>(StringComparer.OrdinalIgnoreCase);
        var dependencies = new List<UpstreamDependencyState>(_pins.Count);
        var hasLiveResult = false;

        foreach (var pin in _pins)
        {
            var cached = cacheById.GetValueOrDefault(pin.Id);
            var state = allowNetwork
                ? await ResolveOnlineAsync(pin, cached, now, cancellationToken).ConfigureAwait(false)
                : ResolveFromCache(pin, cached, now, "No live upstream check was requested.");

            hasLiveResult |= !state.IsDegraded && !string.Equals(state.MetadataSource, SourceCache, StringComparison.OrdinalIgnoreCase);
            dependencies.Add(state);
        }

        var report = new UpstreamDriftReport(dependencies, now);
        if (hasLiveResult)
        {
            WriteCache(report);
        }

        return report;
    }

    private async Task<UpstreamDependencyState> ResolveOnlineAsync(
        UpstreamDependencyPin pin,
        UpstreamDependencyState? cached,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();

        if (!string.IsNullOrWhiteSpace(pin.RestLatestReleaseApi))
        {
            var rest = await _metadataClient.TryGetLatestReleaseAsync(pin, cancellationToken).ConfigureAwait(false);
            if (rest.Success && !string.IsNullOrWhiteSpace(rest.Value))
            {
                return BuildState(pin, rest.Value, rest.MetadataSource, now, null, isDegraded: false, reason: null);
            }

            failures.Add(DescribeFailure(rest));
        }

        var git = await _metadataClient.TryGetGitReferenceAsync(pin, cancellationToken).ConfigureAwait(false);
        if (git.Success && !string.IsNullOrWhiteSpace(git.Value))
        {
            return BuildState(pin, git.Value, git.MetadataSource, now, null, isDegraded: false, reason: null);
        }

        failures.Add(DescribeFailure(git));
        return ResolveFromCache(pin, cached, now, string.Join("; ", failures.Where(item => !string.IsNullOrWhiteSpace(item))));
    }

    private UpstreamDependencyState ResolveFromCache(
        UpstreamDependencyPin pin,
        UpstreamDependencyState? cached,
        DateTimeOffset now,
        string reason)
    {
        if (cached is not null && !string.IsNullOrWhiteSpace(cached.LatestValue))
        {
            return BuildState(
                pin,
                cached.LatestValue,
                SourceCache,
                now,
                now - cached.CheckedAtUtc,
                isDegraded: true,
                reason);
        }

        return BuildState(pin, null, SourceUnavailable, now, null, isDegraded: true, reason);
    }

    private UpstreamDependencyState BuildState(
        UpstreamDependencyPin pin,
        string? latestRaw,
        string metadataSource,
        DateTimeOffset checkedAtUtc,
        TimeSpan? cacheAge,
        bool isDegraded,
        string? reason)
    {
        var current = NormalizeValue(pin, pin.PinnedValue) ?? pin.PinnedValue;
        var latest = NormalizeValue(pin, latestRaw);
        var driftState = DetermineDriftState(pin, current, latest);
        var evidence = BuildEvidence(pin, current, latest, driftState, metadataSource, cacheAge, isDegraded, reason);

        return new UpstreamDependencyState(
            pin.Id,
            pin.Name,
            pin.PinnedValue,
            current,
            latest,
            driftState,
            metadataSource,
            checkedAtUtc,
            cacheAge,
            isDegraded,
            evidence);
    }

    private static string BuildEvidence(
        UpstreamDependencyPin pin,
        string current,
        string? latest,
        string driftState,
        string metadataSource,
        TimeSpan? cacheAge,
        bool isDegraded,
        string? reason)
    {
        var latestDisplay = string.IsNullOrWhiteSpace(latest) ? "unknown" : latest;
        var cacheDisplay = cacheAge.HasValue ? FormatAge(cacheAge.Value) : "none";
        var evidence =
            $"Pinned {pin.PinnedValue}; current {current}; latest {latestDisplay}; drift {driftState}; source {metadataSource}; cache age {cacheDisplay}.";

        if (isDegraded)
        {
            evidence += " Live upstream metadata is degraded; the report is using cached data or an unknown latest value.";
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            evidence += $" Detail: {reason}";
        }

        return evidence;
    }

    private static string DetermineDriftState(UpstreamDependencyPin pin, string current, string? latest)
    {
        if (string.IsNullOrWhiteSpace(latest))
        {
            return "unknown";
        }

        if (string.Equals(pin.ValueKind, "commit", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(current, latest, StringComparison.OrdinalIgnoreCase)
                ? "current"
                : "behind";
        }

        var currentVersion = TryParseVersion(current);
        var latestVersion = TryParseVersion(latest);
        if (currentVersion is not null && latestVersion is not null)
        {
            var compare = latestVersion.CompareTo(currentVersion);
            return compare switch
            {
                > 0 => "behind",
                < 0 => "ahead",
                _ => "current"
            };
        }

        return string.Equals(current, latest, StringComparison.OrdinalIgnoreCase)
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
            return cache?.SchemaVersion == CacheSchemaVersion ? cache : null;
        }
        catch
        {
            return null;
        }
    }

    private void WriteCache(UpstreamDriftReport report)
    {
        try
        {
            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var cache = new CacheDocument(CacheSchemaVersion, report.GeneratedAtUtc, report.Dependencies.ToArray());
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache, CacheJsonOptions));
        }
        catch
        {
            // Upstream drift reporting must not make the health snapshot fail.
        }
    }

    private static string? NormalizeValue(UpstreamDependencyPin pin, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!string.IsNullOrWhiteSpace(pin.ValuePrefixToStrip) &&
            trimmed.StartsWith(pin.ValuePrefixToStrip, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[pin.ValuePrefixToStrip.Length..];
        }

        return string.Equals(pin.ValueKind, "commit", StringComparison.OrdinalIgnoreCase)
            ? trimmed.ToLowerInvariant()
            : trimmed;
    }

    private static Version? TryParseVersion(string value)
    {
        var numeric = value.Trim();
        var separator = numeric.IndexOfAny(new[] { '-', '+' });
        if (separator > 0)
        {
            numeric = numeric[..separator];
        }

        var pieces = numeric.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 0 || pieces.Any(piece => !int.TryParse(piece, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return null;
        }

        while (pieces.Length < 3)
        {
            pieces = pieces.Append("0").ToArray();
        }

        return Version.TryParse(string.Join('.', pieces), out var parsed) ? parsed : null;
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
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        if (duration.TotalDays < 1)
        {
            var hours = (int)Math.Round(duration.TotalHours);
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        var days = (int)Math.Round(duration.TotalDays);
        return days == 1 ? "1 day" : $"{days} days";
    }

    private sealed record CacheDocument(
        int SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        UpstreamDependencyState[] Dependencies);
}

public sealed class GitHubUpstreamMetadataClient : IUpstreamMetadataClient
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    // UpstreamDriftService and CommunityAssetDriftService both hit the same
    // GitHub endpoints; a single shared client keeps one connection pool and
    // avoids socket churn from per-service HttpClient instances.
    private static readonly HttpClient SharedHttpClient = new() { Timeout = DefaultTimeout };

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _gitTimeout;

    public GitHubUpstreamMetadataClient(HttpClient? httpClient = null, TimeSpan? timeout = null)
    {
        _httpClient = httpClient ?? (timeout is null ? SharedHttpClient : new HttpClient { Timeout = timeout.Value });
        _gitTimeout = timeout ?? DefaultTimeout;
    }

    public async Task<UpstreamMetadataLookupResult> TryGetLatestReleaseAsync(
        UpstreamDependencyPin pin,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pin.RestLatestReleaseApi))
        {
            return UpstreamMetadataLookupResult.Unavailable("GitHub REST", "No release API endpoint is configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, pin.RestLatestReleaseApi);
            request.Headers.UserAgent.ParseAdd("LibreSpot-UpstreamDriftMonitor/1.0");
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429)
            {
                return UpstreamMetadataLookupResult.RateLimited("GitHub REST", $"HTTP {(int)response.StatusCode} while reading latest release.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return UpstreamMetadataLookupResult.Unavailable("GitHub REST", $"HTTP {(int)response.StatusCode} while reading latest release.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (root.TryGetProperty("tag_name", out var tag) && tag.ValueKind == JsonValueKind.String)
            {
                var value = tag.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return UpstreamMetadataLookupResult.Found(value, "GitHub REST");
                }
            }

            return UpstreamMetadataLookupResult.Unavailable("GitHub REST", "The latest release response did not contain tag_name.");
        }
        catch (OperationCanceledException)
        {
            return UpstreamMetadataLookupResult.Offline("GitHub REST", "The release API request timed out or was canceled.");
        }
        catch (HttpRequestException ex)
        {
            return UpstreamMetadataLookupResult.Offline("GitHub REST", ex.Message);
        }
        catch (JsonException ex)
        {
            return UpstreamMetadataLookupResult.Unavailable("GitHub REST", $"Invalid release JSON: {ex.Message}");
        }
    }

    public async Task<UpstreamMetadataLookupResult> TryGetGitReferenceAsync(
        UpstreamDependencyPin pin,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_gitTimeout);
        Process? process = null;

        try
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    ArgumentList =
                    {
                        "ls-remote",
                        "--refs",
                        pin.GitRepository,
                        pin.GitReferencePattern
                    }
                }
            };

            if (!process.Start())
            {
                return UpstreamMetadataLookupResult.Unavailable("git ls-remote", "git could not be started.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                if (stderr.Contains("Repository not found", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                    stderr.Contains("could not read from remote repository", StringComparison.OrdinalIgnoreCase))
                {
                    return UpstreamMetadataLookupResult.Missing(
                        "git ls-remote",
                        string.IsNullOrWhiteSpace(stderr) ? "Repository or ref was not found." : stderr.Trim());
                }

                return UpstreamMetadataLookupResult.Unavailable("git ls-remote", string.IsNullOrWhiteSpace(stderr) ? "git returned a nonzero exit code." : stderr.Trim());
            }

            var value = SelectGitValue(pin, stdout);
            return string.IsNullOrWhiteSpace(value)
                ? UpstreamMetadataLookupResult.Missing("git ls-remote", "No matching ref was returned.")
                : UpstreamMetadataLookupResult.Found(value, "git ls-remote");
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (process is { HasExited: false })
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort cleanup; the caller receives degraded metadata.
            }

            return UpstreamMetadataLookupResult.Offline("git ls-remote", "The git fallback timed out or was canceled.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return UpstreamMetadataLookupResult.Offline("git ls-remote", ex.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static string? SelectGitValue(UpstreamDependencyPin pin, string stdout)
    {
        var refs = stdout
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2)
            .Select(parts => new GitRef(parts[0], parts[1]))
            .ToArray();

        if (string.Equals(pin.ValueKind, "commit", StringComparison.OrdinalIgnoreCase))
        {
            return refs.FirstOrDefault()?.Sha;
        }

        return refs
            .Select(item => item.RefName.StartsWith("refs/tags/", StringComparison.OrdinalIgnoreCase)
                ? item.RefName["refs/tags/".Length..]
                : item.RefName)
            .OrderByDescending(tag => ParseSortableVersion(pin, tag))
            .ThenByDescending(tag => tag, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static Version ParseSortableVersion(UpstreamDependencyPin pin, string tag)
    {
        var normalized = tag;
        if (!string.IsNullOrWhiteSpace(pin.ValuePrefixToStrip) &&
            normalized.StartsWith(pin.ValuePrefixToStrip, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[pin.ValuePrefixToStrip.Length..];
        }

        var separator = normalized.IndexOfAny(new[] { '-', '+' });
        if (separator > 0)
        {
            normalized = normalized[..separator];
        }

        var pieces = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length == 0 ||
            pieces.Any(piece => !int.TryParse(piece, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            return new Version(0, 0, 0);
        }

        while (pieces.Length < 3)
        {
            pieces = pieces.Append("0").ToArray();
        }

        return Version.TryParse(string.Join('.', pieces), out var parsed) ? parsed : new Version(0, 0, 0);
    }

    private sealed record GitRef(string Sha, string RefName);
}
