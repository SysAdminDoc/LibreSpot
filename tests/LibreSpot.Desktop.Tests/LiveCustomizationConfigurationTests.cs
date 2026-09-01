using System.Text.Json;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.ViewModels;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class LiveCustomizationConfigurationTests
{
    [Fact]
    public void EditorState_ExposesEveryCatalogFlagSnippetAndGroup()
    {
        var editor = new CustomOptionEditorStateViewModel(AppCatalog.CreateRecommendedConfiguration());

        Assert.Equal(348, editor.CustomizationFeatures.Count);
        Assert.Equal(AppCatalog.CustomizationCatalog.SpotifyFeatures.Select(feature => feature.Name),
            editor.CustomizationFeatures.Select(feature => feature.Name));
        Assert.Equal(AppCatalog.CustomizationCatalog.Snippets.Count, editor.CustomizationSnippets.Count);
        Assert.Equal(AppCatalog.CustomizationCatalog.FeatureGroups.Count + 1, editor.FeatureGroups.Count);
        Assert.Equal("*", editor.SelectedFeatureGroup.Key);
        Assert.True(Assert.Single(editor.CustomApps, app => app.Key == "librespot").IsRecommendedDefault);
    }

    [Fact]
    public void FeatureEditor_SupportsBooleanEnumNumberAndStringValues()
    {
        var boolFeature = new CustomizationFeatureOptionViewModel(
            AppCatalog.CustomizationCatalog.SpotifyFeatures.First(feature => feature.Type == "bool"));
        var enumFeature = new CustomizationFeatureOptionViewModel(
            AppCatalog.CustomizationCatalog.SpotifyFeatures.First(feature => feature.Type == "enum"));
        var numberFeature = new CustomizationFeatureOptionViewModel(
            AppCatalog.CustomizationCatalog.SpotifyFeatures.First(feature => feature.Type == "number"));
        var stringDefinition = new CustomizationFeatureDefinition
        {
            Name = "testString",
            Description = "String contract test",
            Type = "string",
            Default = JsonDocument.Parse("\"default\"").RootElement.Clone(),
            Group = "Everything else"
        };
        var stringFeature = new CustomizationFeatureOptionViewModel(stringDefinition);

        boolFeature.LoadOverride(JsonDocument.Parse("true").RootElement.Clone());
        enumFeature.LoadOverride(JsonDocument.Parse(JsonSerializer.Serialize(enumFeature.Choices[0])).RootElement.Clone());
        numberFeature.LoadOverride(JsonDocument.Parse("7").RootElement.Clone());
        stringFeature.LoadOverride(JsonDocument.Parse("\"custom\"").RootElement.Clone());

        Assert.True(boolFeature.IsBoolean);
        Assert.True(boolFeature.BooleanValue);
        Assert.True(enumFeature.IsEnum);
        Assert.NotEmpty(enumFeature.Choices);
        Assert.True(numberFeature.IsNumber);
        Assert.IsType<double>(numberFeature.GetSerializableValue());
        Assert.True(stringFeature.IsString);
        Assert.Equal("custom", stringFeature.GetSerializableValue());
    }

    [Fact]
    public void NormalizeConfiguration_KeepsKnownEngineStateAndDropsUnknownOverlayEntries()
    {
        var snippetId = AppCatalog.CustomizationCatalog.Snippets[0].Id;
        var featureName = AppCatalog.CustomizationCatalog.SpotifyFeatures.First(feature => feature.Type == "bool").Name;
        var engineState = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            name = "Desktop",
            theme = "Prism",
            scheme = "Dark",
            schemes = new Dictionary<string, Dictionary<string, string>>
            {
                ["Dark"] = new() { ["text"] = "ffffff" }
            }
        });
        var configuration = new InstallConfiguration
        {
            Spicetify_CustomApps = ["librespot", "unknown"],
            LibreSpot_EngineProfileJson = engineState,
            LibreSpot_EnabledSnippets = [snippetId, snippetId, "unknown-snippet"],
            LibreSpot_FeatureOverridesJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                [featureName] = true,
                ["unknownFlag"] = false
            })
        };

        var normalized = AppCatalog.NormalizeConfiguration(configuration);
        using var overrides = JsonDocument.Parse(normalized.LibreSpot_FeatureOverridesJson);

        Assert.Equal(["librespot"], normalized.Spicetify_CustomApps);
        Assert.Equal([snippetId], normalized.LibreSpot_EnabledSnippets);
        Assert.True(overrides.RootElement.TryGetProperty(featureName, out var known));
        Assert.True(known.GetBoolean());
        Assert.False(overrides.RootElement.TryGetProperty("unknownFlag", out _));
        Assert.Contains("\"theme\": \"Prism\"", normalized.LibreSpot_EngineProfileJson);
        Assert.Equal(normalized.LibreSpot_EngineProfileJson, normalized.Clone().LibreSpot_EngineProfileJson);
    }

    [Fact]
    public void LiveCustomizationSection_IsVirtualizedSearchableAndCoversAllValueEditors()
    {
        var root = ResolveRepoRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "LibreSpot.Desktop", "Views", "CustomLiveCustomizationSection.xaml"));

        Assert.Contains("ItemsSource=\"{Binding FilteredCustomizationFeatures}\"", xaml);
        Assert.Contains("VirtualizingPanel.IsVirtualizing=\"True\"", xaml);
        Assert.Contains("VirtualizingPanel.VirtualizationMode=\"Recycling\"", xaml);
        Assert.Contains("FeatureSearchText", xaml);
        Assert.Contains("SelectedFeatureGroup", xaml);
        Assert.Contains("IsBoolean", xaml);
        Assert.Contains("IsEnum", xaml);
        Assert.Contains("IsNumber", xaml);
        Assert.Contains("IsString", xaml);
        Assert.Contains("IsServerGated", xaml);
        Assert.Contains("HasSpotXForcedValue", xaml);
        Assert.Contains("FilteredCustomizationSnippets", xaml);
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
