using System.IO;
using System.Text.Json;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class FleetSchemaTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void ExitCodes_AllHaveRequiredFields()
    {
        using var doc = LoadJson("fleet-exit-codes.json");
        var required = new[] { "code", "name", "category", "description", "retryable", "intuneBehavior" };

        foreach (var entry in doc.RootElement.GetProperty("exitCodes").EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString()!;
            foreach (var field in required)
            {
                Assert.True(
                    entry.TryGetProperty(field, out _),
                    $"Exit code '{name}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void ExitCodes_CodesAreUnique()
    {
        using var doc = LoadJson("fleet-exit-codes.json");
        var codes = doc.RootElement.GetProperty("exitCodes").EnumerateArray()
            .Select(e => e.GetProperty("code").GetInt32())
            .ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void ExitCodes_NamesAreUnique()
    {
        using var doc = LoadJson("fleet-exit-codes.json");
        var names = doc.RootElement.GetProperty("exitCodes").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()!)
            .ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void ExitCodes_ContainsRoadmapTaxonomy()
    {
        using var doc = LoadJson("fleet-exit-codes.json");
        var codes = doc.RootElement.GetProperty("exitCodes").EnumerateArray()
            .Select(e => e.GetProperty("code").GetInt32())
            .ToHashSet();

        var expected = new[] { 0, 1, 2, 10, 11, 12, 20, 30, 40, 50, 60, 1618, 3010, 1641 };
        foreach (var code in expected)
        {
            Assert.Contains(code, codes);
        }
    }

    [Fact]
    public void ExitCodes_CategoriesAreKnown()
    {
        using var doc = LoadJson("fleet-exit-codes.json");
        var known = new HashSet<string> { "success", "failure", "blocked", "retry", "reboot" };

        foreach (var entry in doc.RootElement.GetProperty("exitCodes").EnumerateArray())
        {
            var category = entry.GetProperty("category").GetString()!;
            Assert.Contains(category, known);
        }
    }

    [Fact]
    public void ExitCodes_IntuneBehaviorsAreKnown()
    {
        using var doc = LoadJson("fleet-exit-codes.json");
        var known = new HashSet<string> { "success", "failure", "retry", "softReboot", "hardReboot" };

        foreach (var entry in doc.RootElement.GetProperty("exitCodes").EnumerateArray())
        {
            var behavior = entry.GetProperty("intuneBehavior").GetString()!;
            Assert.Contains(behavior, known);
        }
    }

    [Fact]
    public void ExitCodes_SuccessCodeIsZero()
    {
        using var doc = LoadJson("fleet-exit-codes.json");
        var first = doc.RootElement.GetProperty("exitCodes").EnumerateArray().First();
        Assert.Equal(0, first.GetProperty("code").GetInt32());
        Assert.Equal("success", first.GetProperty("category").GetString());
    }

    [Fact]
    public void AnswerSchema_IsValidJsonSchema()
    {
        using var doc = LoadJson("librespot-answer.schema.json");
        var root = doc.RootElement;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.Equal("object", root.GetProperty("type").GetString());
    }

    [Fact]
    public void AnswerSchema_RequiresConsentFields()
    {
        using var doc = LoadJson("librespot-answer.schema.json");
        var required = doc.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet();

        Assert.Contains("schemaVersion", required);
        Assert.Contains("eulaAccepted", required);
        Assert.Contains("riskAcknowledged", required);
    }

    [Fact]
    public void AnswerSchema_SchemaVersionIsConst()
    {
        using var doc = LoadJson("librespot-answer.schema.json");
        var props = doc.RootElement.GetProperty("properties");
        var sv = props.GetProperty("schemaVersion");

        Assert.Equal(1, sv.GetProperty("const").GetInt32());
    }

    [Fact]
    public void AnswerSchema_InstallModeUsesStrictEnum()
    {
        using var doc = LoadJson("librespot-answer.schema.json");
        var props = doc.RootElement.GetProperty("properties");
        var modes = props.GetProperty("installMode").GetProperty("enum").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        Assert.Contains("recommended", modes);
        Assert.Contains("custom", modes);
        Assert.Contains("reapply", modes);
    }

    [Fact]
    public void AnswerSchema_HasSpotxAndSpicetifySections()
    {
        using var doc = LoadJson("librespot-answer.schema.json");
        var props = doc.RootElement.GetProperty("properties");

        Assert.True(props.TryGetProperty("spotx", out var spotx));
        Assert.Equal("object", spotx.GetProperty("type").GetString());

        Assert.True(props.TryGetProperty("spicetify", out var spicetify));
        Assert.Equal("object", spicetify.GetProperty("type").GetString());
    }

    [Fact]
    public void AnswerSchema_HasLoggingSection()
    {
        using var doc = LoadJson("librespot-answer.schema.json");
        var props = doc.RootElement.GetProperty("properties");

        Assert.True(props.TryGetProperty("logging", out var logging));
        var logProps = logging.GetProperty("properties");
        Assert.True(logProps.TryGetProperty("ndjson", out _));
        Assert.True(logProps.TryGetProperty("level", out _));
    }

    // -----------------------------------------------------------------
    // Fleet CLI verb contract
    // -----------------------------------------------------------------
    [Fact]
    public void CliContract_AllVerbsHaveRequiredFields()
    {
        using var doc = LoadJson("fleet-cli-contract.json");
        var required = new[] { "verb", "description", "requiresAdmin", "mutates", "supportsDryRun", "supportsJson", "supportsNdjson" };

        foreach (var entry in doc.RootElement.GetProperty("verbs").EnumerateArray())
        {
            var verb = entry.GetProperty("verb").GetString()!;
            foreach (var field in required)
            {
                Assert.True(
                    entry.TryGetProperty(field, out _),
                    $"Verb '{verb}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void CliContract_VerbNamesAreUnique()
    {
        using var doc = LoadJson("fleet-cli-contract.json");
        var names = doc.RootElement.GetProperty("verbs").EnumerateArray()
            .Select(e => e.GetProperty("verb").GetString()!)
            .ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void CliContract_ContainsRoadmapVerbs()
    {
        using var doc = LoadJson("fleet-cli-contract.json");
        var verbs = doc.RootElement.GetProperty("verbs").EnumerateArray()
            .Select(e => e.GetProperty("verb").GetString()!)
            .ToHashSet();

        var expected = new[] { "install", "reapply", "detect", "status", "validate", "plan",
            "repair", "watcher install", "watcher remove", "uninstall", "export-support", "version" };

        foreach (var verb in expected)
            Assert.Contains(verb, verbs);
    }

    [Fact]
    public void CliContract_ReadOnlyVerbsDoNotMutate()
    {
        using var doc = LoadJson("fleet-cli-contract.json");
        var readOnlyVerbs = new[] { "detect", "status", "validate", "plan", "export-support", "version" };

        foreach (var entry in doc.RootElement.GetProperty("verbs").EnumerateArray())
        {
            var verb = entry.GetProperty("verb").GetString()!;
            if (readOnlyVerbs.Contains(verb))
            {
                Assert.False(
                    entry.GetProperty("mutates").GetBoolean(),
                    $"Read-only verb '{verb}' should not mutate.");
            }
        }
    }

    [Fact]
    public void CliContract_ExitCodesReferenceKnownCodes()
    {
        using var exitDoc = LoadJson("fleet-exit-codes.json");
        using var cliDoc = LoadJson("fleet-cli-contract.json");

        var knownCodes = exitDoc.RootElement.GetProperty("exitCodes").EnumerateArray()
            .Select(e => e.GetProperty("code").GetInt32())
            .ToHashSet();

        foreach (var verb in cliDoc.RootElement.GetProperty("verbs").EnumerateArray())
        {
            if (!verb.TryGetProperty("defaultExitCodes", out var codes))
                continue;

            var verbName = verb.GetProperty("verb").GetString()!;
            foreach (var code in codes.EnumerateArray())
            {
                Assert.True(
                    knownCodes.Contains(code.GetInt32()),
                    $"Verb '{verbName}' references unknown exit code {code.GetInt32()}.");
            }
        }
    }

    [Fact]
    public void CliContract_GlobalFlagsHaveTypes()
    {
        using var doc = LoadJson("fleet-cli-contract.json");

        foreach (var flag in doc.RootElement.GetProperty("globalFlags").EnumerateArray())
        {
            var name = flag.GetProperty("flag").GetString()!;
            Assert.True(
                flag.TryGetProperty("type", out _),
                $"Global flag '{name}' is missing type.");
        }
    }

    // -----------------------------------------------------------------
    // Diagnostic event IDs
    // -----------------------------------------------------------------
    [Fact]
    public void EventIds_AllHaveRequiredFields()
    {
        using var doc = LoadJson("diagnostic-event-ids.json");
        var required = new[] { "id", "category", "severity", "name", "description" };

        foreach (var ev in doc.RootElement.GetProperty("events").EnumerateArray())
        {
            var id = ev.GetProperty("id").GetString()!;
            foreach (var field in required)
            {
                Assert.True(
                    ev.TryGetProperty(field, out _),
                    $"Event '{id}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void EventIds_IdsAreUnique()
    {
        using var doc = LoadJson("diagnostic-event-ids.json");
        var ids = doc.RootElement.GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("id").GetString()!)
            .ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void EventIds_CategoriesReferenceKnownCategories()
    {
        using var doc = LoadJson("diagnostic-event-ids.json");
        var knownCategories = doc.RootElement.GetProperty("eventCategories").EnumerateArray()
            .Select(c => c.GetProperty("key").GetString()!)
            .ToHashSet();

        foreach (var ev in doc.RootElement.GetProperty("events").EnumerateArray())
        {
            var id = ev.GetProperty("id").GetString()!;
            var category = ev.GetProperty("category").GetString()!;
            Assert.True(
                knownCategories.Contains(category),
                $"Event '{id}' references unknown category '{category}'.");
        }
    }

    [Fact]
    public void EventIds_SeveritiesAreKnown()
    {
        using var doc = LoadJson("diagnostic-event-ids.json");
        var known = new HashSet<string> { "info", "success", "warning", "error", "critical" };

        foreach (var ev in doc.RootElement.GetProperty("events").EnumerateArray())
        {
            var severity = ev.GetProperty("severity").GetString()!;
            Assert.Contains(severity, known);
        }
    }

    [Fact]
    public void EventIds_CoverCoreOperations()
    {
        using var doc = LoadJson("diagnostic-event-ids.json");
        var categories = doc.RootElement.GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("category").GetString()!)
            .ToHashSet();

        var required = new[] { "lifecycle", "download", "spotx", "spicetify", "marketplace",
            "watcher", "health", "config", "network", "security" };
        foreach (var cat in required)
            Assert.Contains(cat, categories);
    }

    private static JsonDocument LoadJson(string filename) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "schemas", filename)));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
