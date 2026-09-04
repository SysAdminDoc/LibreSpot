extern alias Cli;

using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibreSpot.Desktop.Services;
using Xunit;
using CliApp = Cli::LibreSpot.Cli.CliApplication;
using CliEnvironmentSnapshot = LibreSpot.Desktop.Models.EnvironmentSnapshot;

namespace LibreSpot.Desktop.Tests;

/// <summary>
/// Four schemas under schemas/ describe files LibreSpot actually writes, and
/// none of them had a test that read the schema and checked a real output
/// against it. A field could be renamed on either side and nothing would say
/// so. These tests produce the real artifact and validate it against the
/// contract file rather than against a copy of the contract.
/// </summary>
public sealed class SchemaOutputContractTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    // ------------------------------------------------------------- ndjson

    [Fact]
    public void NdjsonLog_EveryLineMatchesTheDeclaredLineSchema()
    {
        // A dry run mutates nothing and emits the real NDJSON stream, which is
        // what an endpoint tool parses.
        var sample = Path.Combine(RepoRoot, "samples", "minimal.json");
        var (_, stdout) = RunCli("install", "--dry-run", "--answer-file", sample, "--ndjson");

        var lines = stdout
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("{", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(lines);

        using var schema = LoadSchema("ndjson-log-format.json");
        var lineSchema = schema.RootElement.GetProperty("lineSchema");
        var required = StringArray(lineSchema.GetProperty("required"));
        var optional = StringArray(lineSchema.GetProperty("optional"));
        var known = required.Concat(optional).ToHashSet(StringComparer.Ordinal);
        var eventIdPattern = new Regex(
            lineSchema.GetProperty("fields").EnumerateArray()
                .Single(field => field.GetProperty("name").GetString() == "eventId")
                .GetProperty("pattern").GetString()!);

        using var eventIds = LoadSchema("diagnostic-event-ids.json");
        var declaredEventIds = CollectStrings(eventIds.RootElement, "id")
            .Concat(CollectStrings(eventIds.RootElement, "eventId"))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            foreach (var field in required)
            {
                Assert.True(
                    root.TryGetProperty(field, out var value) && value.ValueKind != JsonValueKind.Null,
                    $"NDJSON line is missing required field '{field}': {line}");
            }

            foreach (var property in root.EnumerateObject())
            {
                Assert.True(
                    known.Contains(property.Name),
                    $"NDJSON line carries '{property.Name}', which schemas/ndjson-log-format.json does not declare.");
            }

            Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
            var eventId = root.GetProperty("eventId").GetString()!;
            Assert.Matches(eventIdPattern, eventId);
            if (declaredEventIds.Count > 0)
            {
                Assert.True(
                    declaredEventIds.Contains(eventId),
                    $"Event id '{eventId}' is not declared in schemas/diagnostic-event-ids.json.");
            }
        }
    }

    // ------------------------------------------------- asset cache bundle

    [Fact]
    public void AssetCacheBundle_ManifestMatchesItsJsonSchema()
    {
        using var scratch = new Scratch();
        var cacheDirectory = scratch.At("cache");
        var payload = Encoding.UTF8.GetBytes("librespot asset cache bundle contract fixture");
        var sha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        Directory.CreateDirectory(cacheDirectory);
        File.WriteAllBytes(Path.Combine(cacheDirectory, sha), payload);
        File.WriteAllText(
            Path.Combine(cacheDirectory, "asset-cache-index.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generatedAtUtc = "2026-09-04T10:00:00Z",
                entries = new[]
                {
                    new
                    {
                        sha256 = sha,
                        label = "spicetify-cli",
                        sourceUrl = "https://example.invalid/spicetify.zip",
                        byteSize = payload.Length,
                        present = true,
                        firstSeenAtUtc = "2026-09-01T10:00:00Z",
                        lastUsedAtUtc = "2026-09-02T10:00:00Z",
                        lastVerifiedAtUtc = "2026-09-03T10:00:00Z"
                    }
                }
            }));

        var bundlePath = scratch.At("bundle.zip");
        new AssetCacheBundleService().Export(cacheDirectory, bundlePath, "4.5.0");

        using var archive = ZipFile.OpenRead(bundlePath);
        using var manifestStream = archive.GetEntry("manifest.json")!.Open();
        using var manifest = JsonDocument.Parse(manifestStream);

        using var schema = LoadSchema("asset-cache-bundle.json");
        AssertMatchesObjectSchema(manifest.RootElement, schema.RootElement, schema.RootElement, "manifest");
    }

    // --------------------------------------------------- operation tokens

    [Fact]
    public void OperationTokenKinds_EveryKindProductionWritesIsDeclared()
    {
        using var schema = LoadSchema("operation-token-types.json");
        var declared = schema.RootElement.GetProperty("tokenTypes").EnumerateArray()
            .Select(entry => entry.GetProperty("kind").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(declared);

        // The undo path is what reads these back, so a kind written by either
        // host with no declaration is an undo the fleet contract cannot explain.
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in ProductionSources())
        {
            var text = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(text, @"tokenKind\s*[:=]\s*[""']([A-Za-z]+)[""']"))
            {
                used.Add(match.Groups[1].Value);
            }
        }

        Assert.NotEmpty(used);
        var undeclared = used.Where(kind => !declared.Contains(kind)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.True(
            undeclared.Length == 0,
            "These operation token kinds are written by production code but are not declared in "
                + "schemas/operation-token-types.json, so undo cannot describe them: "
                + string.Join(", ", undeclared));
    }

    // -------------------------------------------------------- run receipt

    [Fact]
    public void RunReceiptSchema_DeclaresEveryFieldTheHostsWrite()
    {
        using var schema = LoadSchema("run-receipt-format.json");
        var receiptFields = schema.RootElement.GetProperty("receiptFields").EnumerateArray()
            .Select(field => field.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var statusValues = schema.RootElement.GetProperty("statusValues").EnumerateArray()
            .Select(status => status.GetProperty("value").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        // Complete-OperationJournalRun is the writer both PowerShell hosts use.
        var writer = File.ReadAllText(
            Path.Combine(RepoRoot, "src", "powershell", "shared", "Complete-OperationJournalRun.ps1"));
        var receiptBody = Regex.Match(
            writer,
            @"(?sm)^\s*\$receipt = \[ordered\]@\{(.+?)^\s*\}").Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(receiptBody), "Could not locate the receipt hashtable.");

        var written = Regex.Matches(receiptBody, @"(?m)^\s+([A-Za-z]+)\s*=")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(written);

        var undeclared = written.Where(name => !receiptFields.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            undeclared.Length == 0,
            "Complete-OperationJournalRun writes receipt fields that schemas/run-receipt-format.json does not "
                + "declare: " + string.Join(", ", undeclared));

        // And the other direction, so a required field the writer drops is
        // caught too rather than only an undeclared one it adds.
        var requiredNames = schema.RootElement.GetProperty("receiptFields").EnumerateArray()
            .Where(field => field.GetProperty("required").GetBoolean())
            .Select(field => field.GetProperty("name").GetString()!)
            .ToArray();
        var missing = requiredNames.Where(name => !written.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            missing.Length == 0,
            "schemas/run-receipt-format.json requires receipt fields that Complete-OperationJournalRun never "
                + "writes: " + string.Join(", ", missing));

        foreach (var status in Regex.Matches(writer, @"status\s*=\s*'([a-zA-Z]+)'").Select(m => m.Groups[1].Value))
        {
            Assert.True(
                statusValues.Contains(status),
                $"Receipt status '{status}' is not one of the declared statusValues.");
        }
    }

    // ------------------------------------------------------------ helpers

    private static void AssertMatchesObjectSchema(
        JsonElement value,
        JsonElement schema,
        JsonElement root,
        string path)
    {
        schema = Resolve(schema, root);

        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in StringArray(required))
            {
                Assert.True(
                    value.TryGetProperty(name, out _),
                    $"{path} is missing required property '{name}'.");
            }
        }

        if (schema.TryGetProperty("additionalProperties", out var additional)
            && additional.ValueKind == JsonValueKind.False
            && schema.TryGetProperty("properties", out var declaredProperties))
        {
            foreach (var property in value.EnumerateObject())
            {
                Assert.True(
                    declaredProperties.TryGetProperty(property.Name, out _),
                    $"{path} carries undeclared property '{property.Name}'.");
            }
        }

        if (!schema.TryGetProperty("properties", out var properties))
        {
            return;
        }

        foreach (var property in properties.EnumerateObject())
        {
            if (!value.TryGetProperty(property.Name, out var actual))
            {
                continue;
            }

            var propertySchema = Resolve(property.Value, root);
            var childPath = $"{path}.{property.Name}";

            if (propertySchema.TryGetProperty("const", out var expected))
            {
                Assert.Equal(expected.ToString(), actual.ToString());
            }

            if (propertySchema.TryGetProperty("pattern", out var pattern)
                && actual.ValueKind == JsonValueKind.String)
            {
                Assert.Matches(new Regex(pattern.GetString()!), actual.GetString()!);
            }

            if (propertySchema.TryGetProperty("minimum", out var minimum)
                && actual.ValueKind == JsonValueKind.Number)
            {
                Assert.True(
                    actual.GetInt64() >= minimum.GetInt64(),
                    $"{childPath} is below the declared minimum.");
            }

            if (propertySchema.TryGetProperty("minItems", out var minItems)
                && actual.ValueKind == JsonValueKind.Array)
            {
                Assert.True(
                    actual.GetArrayLength() >= minItems.GetInt32(),
                    $"{childPath} has fewer items than the schema allows.");
            }

            if (actual.ValueKind == JsonValueKind.Array
                && propertySchema.TryGetProperty("items", out var items))
            {
                var index = 0;
                foreach (var element in actual.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        AssertMatchesObjectSchema(element, items, root, $"{childPath}[{index}]");
                    }

                    index++;
                }
            }
            else if (actual.ValueKind == JsonValueKind.Object)
            {
                AssertMatchesObjectSchema(actual, propertySchema, root, childPath);
            }
        }
    }

    private static JsonElement Resolve(JsonElement schema, JsonElement root)
    {
        if (schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("$ref", out var reference))
        {
            return schema;
        }

        var pointer = reference.GetString()!;
        Assert.StartsWith("#/", pointer, StringComparison.Ordinal);
        var current = root;
        foreach (var segment in pointer[2..].Split('/'))
        {
            current = current.GetProperty(segment);
        }

        return current;
    }

    private static IEnumerable<string> CollectStrings(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals(propertyName) && property.Value.ValueKind == JsonValueKind.String)
                    {
                        yield return property.Value.GetString()!;
                    }

                    foreach (var nested in CollectStrings(property.Value, propertyName))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in CollectStrings(item, propertyName))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static IEnumerable<string> ProductionSources()
    {
        foreach (var relative in new[] { "src", })
        {
            var root = Path.Combine(RepoRoot, relative);
            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file);
                if (extension is not (".cs" or ".ps1"))
                {
                    continue;
                }

                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return file;
            }
        }

        yield return Path.Combine(RepoRoot, "LibreSpot.ps1");
    }

    private static string[] StringArray(JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static JsonDocument LoadSchema(string filename) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "schemas", filename)));

    private static (int ExitCode, string Stdout) RunCli(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliApp.Run(args, stdout, stderr, _ => HealthySnapshot());
        return (exitCode, stdout.ToString());
    }

    /// <summary>A snapshot complete enough that detect reaches its own logging.</summary>
    private static CliEnvironmentSnapshot HealthySnapshot() =>
        new()
        {
            SpotifyInstalled = true,
            SpicetifyInstalled = true,
            MarketplaceFilesPresent = true,
            MarketplaceRegistered = true,
            SavedConfigExists = true,
            ConfigFolderExists = true,
            AutoReapplyTaskRegistered = false,
            HostArchitecture = "x64",
            ProcessArchitecture = "x64",
            HealthReport = new LibreSpot.Desktop.Models.StackHealthReport([]),
            UpstreamDriftReport = LibreSpot.Desktop.Models.UpstreamDriftReport.Empty,
            CommunityAssetDriftReport = LibreSpot.Desktop.Models.CommunityAssetDriftReport.Empty,
            AssetCacheInventory = LibreSpot.Desktop.Models.AssetCacheInventoryReport.Empty
        };

    private sealed class Scratch : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "LibreSpot.SchemaContract",
            Guid.NewGuid().ToString("N"));

        public Scratch() => Directory.CreateDirectory(_root);

        public string At(string relative) => System.IO.Path.Combine(_root, relative);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
