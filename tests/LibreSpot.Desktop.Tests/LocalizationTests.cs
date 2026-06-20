using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class LocalizationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void StringsResx_ExistsAndIsValidXml()
    {
        var path = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", "Strings.resx");
        Assert.True(File.Exists(path), "Strings.resx not found at expected path.");

        var doc = XDocument.Load(path);
        Assert.NotNull(doc.Root);
        Assert.Equal("root", doc.Root!.Name.LocalName);
    }

    [Fact]
    public void StringsResx_HasUniqueKeys()
    {
        var doc = LoadResx();
        var keys = doc.Root!.Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(n => n is not null)
            .ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void StringsResx_NoEmptyValues()
    {
        var doc = LoadResx();
        foreach (var data in doc.Root!.Elements("data"))
        {
            var name = data.Attribute("name")?.Value ?? "(unnamed)";
            var value = data.Element("value")?.Value;
            Assert.False(
                string.IsNullOrWhiteSpace(value),
                $"Resource key '{name}' has an empty or whitespace-only value.");
        }
    }

    [Fact]
    public void StringsResx_ContainsCoreUiStrings()
    {
        var doc = LoadResx();
        var keys = doc.Root!.Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(n => n is not null)
            .ToHashSet();

        var required = new[]
        {
            "AppTitle", "ActivityReady", "ModeRecommendedTitle", "ModeCustomTitle",
            "ModeMaintenanceTitle", "ReadyToRun", "AdminStepNeeded",
            "SearchPlaceholder", "SearchNoResults", "ButtonCancel", "ButtonContinue",
            "ActionFullReset", "ProgressComplete"
        };

        foreach (var key in required)
            Assert.Contains(key, keys);
    }

    [Fact]
    public void StringsResx_AllEntriesHaveComments()
    {
        var doc = LoadResx();
        foreach (var data in doc.Root!.Elements("data"))
        {
            var name = data.Attribute("name")?.Value ?? "(unnamed)";
            var comment = data.Element("comment")?.Value;
            Assert.False(
                string.IsNullOrWhiteSpace(comment),
                $"Resource key '{name}' should have a translator comment.");
        }
    }

    [Fact]
    public void Csproj_ConfiguresResxCodeGeneration()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "LibreSpot.Desktop.csproj"));
        Assert.Contains("Strings.resx", csproj);
        Assert.Contains("PublicResXFileCodeGenerator", csproj);
        Assert.Contains("Strings.Designer.cs", csproj);
    }

    private static XDocument LoadResx()
    {
        var path = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", "Strings.resx");
        return XDocument.Load(path);
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
