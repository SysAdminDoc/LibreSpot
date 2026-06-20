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
