using System.IO;
using System.Text.Json;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ProfileSchemaTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void ProfileSchema_IsValidJsonSchema()
    {
        using var doc = LoadSchema();
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", doc.RootElement.GetProperty("$schema").GetString());
        Assert.Equal("object", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void ProfileSchema_RequiresMetadataFields()
    {
        using var doc = LoadSchema();
        var required = doc.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()!)
            .ToHashSet();

        Assert.Contains("schemaVersion", required);
        Assert.Contains("generator", required);
        Assert.Contains("generatorVersion", required);
        Assert.Contains("createdAt", required);
        Assert.Contains("profileName", required);
        Assert.Contains("settings", required);
    }

    [Fact]
    public void ProfileSchema_SchemaVersionIsConst()
    {
        using var doc = LoadSchema();
        Assert.Equal(1, doc.RootElement.GetProperty("properties")
            .GetProperty("schemaVersion").GetProperty("const").GetInt32());
    }

    [Fact]
    public void ProfileSchema_DoesNotExportRiskAcknowledged()
    {
        using var doc = LoadSchema();
        var settings = doc.RootElement.GetProperty("properties").GetProperty("settings");
        var settingsProps = settings.GetProperty("properties");

        Assert.False(settingsProps.TryGetProperty("RiskAcknowledged", out _),
            "RiskAcknowledged must not be in the profile schema — it is per-user consent.");
    }

    [Fact]
    public void ProfileSchema_HasSettingsObject()
    {
        using var doc = LoadSchema();
        var settings = doc.RootElement.GetProperty("properties").GetProperty("settings");
        Assert.Equal("object", settings.GetProperty("type").GetString());
    }

    [Fact]
    public void ProfileSchema_SettingsIncludesCoreConfigKeys()
    {
        using var doc = LoadSchema();
        var props = doc.RootElement.GetProperty("properties")
            .GetProperty("settings").GetProperty("properties");

        Assert.True(props.TryGetProperty("Mode", out _));
        Assert.True(props.TryGetProperty("Spicetify_Theme", out _));
        Assert.True(props.TryGetProperty("Spicetify_Scheme", out _));
        Assert.True(props.TryGetProperty("Spicetify_CustomApps", out _));
        Assert.True(props.TryGetProperty("AutoReapply_Enabled", out _));
    }

    [Fact]
    public void ProfileSchema_HasDependencyPinsSection()
    {
        using var doc = LoadSchema();
        var props = doc.RootElement.GetProperty("properties");
        Assert.True(props.TryGetProperty("dependencyPins", out var pins));
        Assert.Equal("object", pins.GetProperty("type").GetString());
    }

    [Fact]
    public void ProfileSchema_HasExamples()
    {
        using var doc = LoadSchema();
        Assert.True(doc.RootElement.TryGetProperty("examples", out var examples));
        Assert.True(examples.GetArrayLength() >= 2);
    }

    [Fact]
    public void ProfileSchema_NoCredentialsInvariantDocumented()
    {
        using var doc = LoadSchema();
        var description = doc.RootElement.GetProperty("description").GetString()!;
        Assert.Contains("credentials", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfileSchema_DiffersFromAnswerFileOnConsentFields()
    {
        using var answerDoc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoRoot, "schemas", "librespot-answer.schema.json")));
        using var profileDoc = LoadSchema();

        var answerRequired = answerDoc.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()!).ToHashSet();
        var profileRequired = profileDoc.RootElement.GetProperty("required").EnumerateArray()
            .Select(e => e.GetString()!).ToHashSet();

        Assert.Contains("eulaAccepted", answerRequired);
        Assert.Contains("riskAcknowledged", answerRequired);
        Assert.DoesNotContain("eulaAccepted", profileRequired);
        Assert.DoesNotContain("riskAcknowledged", profileRequired);
    }

    private static JsonDocument LoadSchema() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "schemas", "librespot-profile.schema.json")));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
