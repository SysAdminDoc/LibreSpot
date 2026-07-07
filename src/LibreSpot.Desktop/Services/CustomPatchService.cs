using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LibreSpot.Desktop.Services;

public sealed record CustomPatchValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    int PatchGroupCount,
    int PatternCount,
    int ReplacementCount)
{
    public string Summary =>
        IsValid
            ? $"Dry run passed: {PatchGroupCount} patch group(s), {PatternCount} regex pattern(s), and {ReplacementCount} replacement value(s) are ready to stage."
            : $"Dry run blocked: {Errors.Count} issue(s) need review before SpotX can receive this patches.json.";

    public IReadOnlyList<string> Findings { get; } = Errors.Concat(Warnings).ToArray();
}

public sealed record CustomPatchImportResult(
    string Json,
    string SourceUrl,
    DateTimeOffset FetchedAtUtc,
    int ByteCount,
    string Sha256);

public interface ICustomPatchImportTransport
{
    Task<CustomPatchImportResponse> GetAsync(Uri uri, CancellationToken cancellationToken);
}

public sealed class CustomPatchImportResponse : IDisposable
{
    private readonly IDisposable? _owner;

    public CustomPatchImportResponse(
        HttpStatusCode statusCode,
        long? contentLength,
        Stream content,
        IDisposable? owner = null)
    {
        StatusCode = statusCode;
        ContentLength = contentLength;
        Content = content;
        _owner = owner;
    }

    public HttpStatusCode StatusCode { get; }
    public long? ContentLength { get; }
    public Stream Content { get; }

    public void Dispose()
    {
        Content.Dispose();
        _owner?.Dispose();
    }
}

public sealed class CustomPatchService
{
    public const int MaxPatchJsonBytes = 64 * 1024;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly UTF8Encoding StrictUtf8NoBom = new(false, true);
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly HashSet<string> PatternPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "match",
        "matches",
        "regex",
        "pattern",
        "patterns",
        "search",
        "find",
        "old",
        "from"
    };
    private static readonly HashSet<string> ReplacementPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "replace",
        "replacement",
        "replacements",
        "new",
        "to"
    };

    private readonly ICustomPatchImportTransport _importTransport;
    private readonly Func<DateTimeOffset> _clock;

    public CustomPatchService(ICustomPatchImportTransport? importTransport = null, Func<DateTimeOffset>? clock = null)
    {
        _importTransport = importTransport ?? new HttpClientCustomPatchImportTransport();
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public CustomPatchValidationResult Validate(string? json, bool enabled)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var text = json ?? string.Empty;

        if (!enabled && string.IsNullOrWhiteSpace(text))
        {
            return new CustomPatchValidationResult(true, errors, warnings, 0, 0, 0);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add("Paste a SpotX patches.json document or turn off custom patches.");
            return new CustomPatchValidationResult(false, errors, warnings, 0, 0, 0);
        }

        if (Encoding.UTF8.GetByteCount(text) > MaxPatchJsonBytes)
        {
            errors.Add($"Custom patches are limited to {MaxPatchJsonBytes / 1024} KB so SpotX receives a reviewable file.");
            return new CustomPatchValidationResult(false, errors, warnings, 0, 0, 0);
        }

        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 64
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add("SpotX custom patches must be a JSON object at the root.");
                return new CustomPatchValidationResult(false, errors, warnings, 0, 0, 0);
            }

            var metrics = new PatchMetrics();
            foreach (var section in document.RootElement.EnumerateObject())
            {
                metrics.PatchGroupCount++;
                if (string.IsNullOrWhiteSpace(section.Name))
                {
                    warnings.Add("One root patch group has an empty name.");
                }

                InspectElement(section.Value, section.Name, errors, warnings, metrics);
            }

            if (metrics.PatchGroupCount == 0)
            {
                errors.Add("The patches.json root object does not contain any patch groups.");
            }

            if (metrics.PatternCount == 0)
            {
                warnings.Add("No match, regex, pattern, search, or find entries were detected.");
            }

            if (metrics.ReplacementCount == 0)
            {
                warnings.Add("No replace, replacement, new, or to entries were detected.");
            }

            return new CustomPatchValidationResult(errors.Count == 0, errors, warnings, metrics.PatchGroupCount, metrics.PatternCount, metrics.ReplacementCount);
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON could not be parsed near line {ex.LineNumber ?? 0}, byte {ex.BytePositionInLine ?? 0}: {ex.Message}");
            return new CustomPatchValidationResult(false, errors, warnings, 0, 0, 0);
        }
    }

    public string Format(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
            MaxDepth = 64
        });

        return JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
    }

    public async Task<CustomPatchImportResult> ImportFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate((url ?? string.Empty).Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Enter a complete HTTPS URL for the custom patches.json file.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Custom patches can only be imported from HTTPS URLs.");
        }

        using var response = await _importTransport.GetAsync(uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("The patches.json URL returned 404 Not Found.");
        }

        var statusCode = (int)response.StatusCode;
        if (statusCode is < 200 or > 299)
        {
            throw new InvalidOperationException($"The patches.json URL returned HTTP {statusCode} {response.StatusCode}.");
        }

        if (response.ContentLength is > MaxPatchJsonBytes)
        {
            throw new InvalidOperationException($"The remote patches.json is larger than {MaxPatchJsonBytes / 1024} KB.");
        }

        await using var stream = response.Content;
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxPatchJsonBytes)
            {
                throw new InvalidOperationException($"The remote patches.json is larger than {MaxPatchJsonBytes / 1024} KB.");
            }

            buffer.Write(chunk, 0, read);
        }

        var bytes = buffer.ToArray();
        var text = DecodeUtf8(bytes);
        AssertImportJsonIsValid(text);

        return new CustomPatchImportResult(
            text,
            uri.ToString(),
            _clock().ToUniversalTime(),
            bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        try
        {
            return StrictUtf8NoBom.GetString(bytes);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidOperationException("The remote patches.json is not valid UTF-8 text.", ex);
        }
    }

    private static void AssertImportJsonIsValid(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 64
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Imported patches.json must be a JSON object at the root.");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Imported patches.json is not valid JSON: {ex.Message}", ex);
        }
    }

    private static void InspectElement(
        JsonElement element,
        string path,
        List<string> errors,
        List<string> warnings,
        PatchMetrics metrics)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                InspectObject(element, path, errors, warnings, metrics);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    InspectElement(item, $"{path}[{index}]", errors, warnings, metrics);
                    index++;
                }
                break;
        }
    }

    private static void InspectObject(
        JsonElement element,
        string path,
        List<string> errors,
        List<string> warnings,
        PatchMetrics metrics)
    {
        List<string>? matchValues = null;
        List<string>? replaceValues = null;

        foreach (var property in element.EnumerateObject())
        {
            var childPath = $"{path}.{property.Name}";
            if (PatternPropertyNames.Contains(property.Name))
            {
                matchValues = ExtractStringValues(property.Value);
                foreach (var pattern in matchValues)
                {
                    metrics.PatternCount++;
                    ValidateRegex(pattern, childPath, errors, warnings);
                }
            }
            else if (ReplacementPropertyNames.Contains(property.Name))
            {
                replaceValues = ExtractStringValues(property.Value);
                metrics.ReplacementCount += replaceValues.Count;
            }

            InspectElement(property.Value, childPath, errors, warnings, metrics);
        }

        if (matchValues is { Count: > 0 } && replaceValues is { Count: > 0 } && matchValues.Count != replaceValues.Count)
        {
            errors.Add($"{path} has {matchValues.Count} match value(s) but {replaceValues.Count} replacement value(s). Keep SpotX match and replace arrays aligned.");
        }
    }

    private static List<string> ExtractStringValues(JsonElement element)
    {
        var values = new List<string>();
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                values.Add(element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        values.Add(item.GetString() ?? string.Empty);
                    }
                }
                break;
        }

        return values;
    }

    private static void ValidateRegex(string pattern, string path, List<string> errors, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            errors.Add($"{path} contains an empty regex pattern.");
            return;
        }

        if (pattern.Length > 2_000)
        {
            warnings.Add($"{path} contains a long regex pattern; keep custom patches small and reviewable.");
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.None, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"{path} is not a valid .NET regex: {ex.Message}");
        }
    }

    private sealed class PatchMetrics
    {
        public int PatchGroupCount { get; set; }
        public int PatternCount { get; set; }
        public int ReplacementCount { get; set; }
    }

    private sealed class HttpClientCustomPatchImportTransport : ICustomPatchImportTransport
    {
        private static readonly HttpClient ImportClient = new()
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        public async Task<CustomPatchImportResponse> GetAsync(Uri uri, CancellationToken cancellationToken)
        {
            var response = await ImportClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            try
            {
                // The HTTPS-only rule must survive redirects: re-check the
                // scheme of the URI the client actually landed on.
                if (!string.Equals(response.RequestMessage?.RequestUri?.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Import URL redirected to a non-HTTPS address.");
                }

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return new CustomPatchImportResponse(
                    response.StatusCode,
                    response.Content.Headers.ContentLength,
                    stream,
                    response);
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
    }
}
