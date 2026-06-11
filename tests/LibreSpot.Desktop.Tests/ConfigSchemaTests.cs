using System.Text.Json;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ConfigSchemaTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void ConfigSchema_DeclaresCurrentVersionAndStrictProperties()
    {
        using var schema = LoadSchema();
        var root = schema.RootElement;
        var properties = root.GetProperty("properties");
        var schemaProperties = properties
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var modelProperties = typeof(InstallConfiguration)
            .GetProperties()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(AppCatalog.CurrentConfigSchemaVersion, properties.GetProperty(nameof(InstallConfiguration.ConfigSchemaVersion)).GetProperty("const").GetInt32());
        Assert.Equal(modelProperties, schemaProperties);
    }

    [Fact]
    public void ConfigSchema_UsesCatalogEnumsForBoundedSelections()
    {
        using var schema = LoadSchema();
        var properties = schema.RootElement.GetProperty("properties");

        Assert.Equal(["Easy", "Custom"], GetStringArray(properties.GetProperty(nameof(InstallConfiguration.Mode)).GetProperty("enum")));
        Assert.Equal(AppCatalog.DownloadMethods.Select(method => method.Id), GetStringArray(properties.GetProperty(nameof(InstallConfiguration.SpotX_DownloadMethod)).GetProperty("enum")));
        Assert.Equal(AppCatalog.SpotifyVersionManifest.Select(version => version.Id), GetStringArray(properties.GetProperty(nameof(InstallConfiguration.SpotX_SpotifyVersionId)).GetProperty("enum")));
        Assert.Equal(AppCatalog.LyricsThemes, GetStringArray(properties.GetProperty(nameof(InstallConfiguration.SpotX_LyricsTheme)).GetProperty("enum")));
        Assert.Equal(AppCatalog.ExtensionDefinitions.Select(extension => extension.Key), GetStringArray(properties.GetProperty(nameof(InstallConfiguration.Spicetify_Extensions)).GetProperty("items").GetProperty("enum")));
        Assert.True(properties.GetProperty(nameof(InstallConfiguration.SpotX_CacheLimit)).GetProperty("minimum").GetInt32() == 0);
        Assert.True(properties.GetProperty(nameof(InstallConfiguration.SpotX_CacheLimit)).GetProperty("maximum").GetInt32() == 50_000);
    }

    private static JsonDocument LoadSchema()
    {
        var path = Path.Combine(RepoRoot, "schemas", "librespot-config.schema.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string[] GetStringArray(JsonElement element) =>
        element.EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate LibreSpot.ps1 from the test runner.");
    }
}
