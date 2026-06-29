using System.IO;
using System.Net;
using System.Net.Http;
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

    public IReadOnlyList<string> Findings => Errors.Concat(Warnings).ToArray();
}

public sealed class CustomPatchService
{
    public const int MaxPatchJsonBytes = 64 * 1024;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true
    };
    private static readonly HttpClient ImportClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
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

    public async Task<string> ImportFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Enter a complete HTTPS URL for the custom patches.json file.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Custom patches can only be imported from HTTPS URLs.");
        }

        using var response = await ImportClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("The patches.json URL returned 404 Not Found.");
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxPatchJsonBytes)
        {
            throw new InvalidOperationException($"The remote patches.json is larger than {MaxPatchJsonBytes / 1024} KB.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxPatchJsonBytes)
            {
                throw new InvalidOperationException($"The remote patches.json is larger than {MaxPatchJsonBytes / 1024} KB.");
            }
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
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
}
