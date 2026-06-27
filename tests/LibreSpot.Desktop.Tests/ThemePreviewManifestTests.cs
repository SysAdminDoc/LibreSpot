using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ThemePreviewManifestTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void Manifest_AllThemesHaveRequiredFields()
    {
        using var doc = LoadManifest();
        var required = new[] { "id", "source", "schemes", "requiresJs", "marketplaceOnly", "supportState", "preview" };

        foreach (var theme in doc.RootElement.GetProperty("themes").EnumerateArray())
        {
            var id = theme.GetProperty("id").GetString()!;
            foreach (var field in required)
            {
                Assert.True(
                    theme.TryGetProperty(field, out _),
                    $"Theme '{id}' is missing required field '{field}'.");
            }
        }
    }

    [Fact]
    public void Manifest_ThemeIdsAreUnique()
    {
        using var doc = LoadManifest();
        var ids = doc.RootElement.GetProperty("themes").EnumerateArray()
            .Select(t => t.GetProperty("id").GetString()!)
            .ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Manifest_SourcesAreKnown()
    {
        using var doc = LoadManifest();
        var known = new HashSet<string> { "official", "community", "virtual" };

        foreach (var theme in doc.RootElement.GetProperty("themes").EnumerateArray())
        {
            var source = theme.GetProperty("source").GetString()!;
            Assert.Contains(source, known);
        }
    }

    [Fact]
    public void Manifest_PreviewStatusesAreKnown()
    {
        using var doc = LoadManifest();
        var known = new HashSet<string> { "available", "unavailable", "broken", "placeholder" };

        foreach (var theme in doc.RootElement.GetProperty("themes").EnumerateArray())
        {
            var status = theme.GetProperty("preview").GetProperty("status").GetString()!;
            Assert.Contains(status, known);
        }
    }

    [Fact]
    public void Manifest_OfficialThemesUseCommitPinnedUrls()
    {
        using var doc = LoadManifest();

        foreach (var theme in doc.RootElement.GetProperty("themes").EnumerateArray())
        {
            if (theme.GetProperty("source").GetString() != "official") continue;
            var preview = theme.GetProperty("preview");
            if (preview.GetProperty("status").GetString() == "placeholder") continue;

            var url = preview.GetProperty("url").GetString()!;
            Assert.DoesNotContain("/main/", url);
            Assert.DoesNotContain("/master/", url);
            Assert.Matches(@"/[a-f0-9]{8,40}/", url);
        }
    }

    [Fact]
    public void Manifest_RequiresJsMatchesScriptThemesNeedingJS()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "LibreSpot.ps1"));
        var jsMatch = Regex.Match(script, @"\$global:ThemesNeedingJS\s*=\s*@\((?<list>.+?)\)", RegexOptions.Singleline);
        Assert.True(jsMatch.Success, "ThemesNeedingJS not found in LibreSpot.ps1");

        var jsThemes = Regex.Matches(jsMatch.Groups["list"].Value, @"['""](?<name>[\w-]+)['""]")
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using var doc = LoadManifest();
        foreach (var theme in doc.RootElement.GetProperty("themes").EnumerateArray())
        {
            var id = theme.GetProperty("id").GetString()!;
            if (theme.GetProperty("source").GetString() == "virtual") continue;

            var manifestJs = theme.GetProperty("requiresJs").GetBoolean();
            var scriptJs = jsThemes.Contains(id);
            Assert.Equal(scriptJs, manifestJs);
        }
    }

    [Fact]
    public void Manifest_CoversBothOfficialAndCommunityThemes()
    {
        using var doc = LoadManifest();
        var sources = doc.RootElement.GetProperty("themes").EnumerateArray()
            .Select(t => t.GetProperty("source").GetString()!)
            .ToHashSet();

        Assert.Contains("official", sources);
        Assert.Contains("community", sources);
    }

    [Fact]
    public void Manifest_CommunityThemesArePresent()
    {
        using var doc = LoadManifest();
        var ids = doc.RootElement.GetProperty("themes").EnumerateArray()
            .Select(t => t.GetProperty("id").GetString()!)
            .ToHashSet();

        var expected = new[] { "Catppuccin", "Comfy", "Bloom", "Lucid", "Hazy" };
        foreach (var name in expected)
            Assert.Contains(name, ids);
    }

    [Fact]
    public void Manifest_SchemesAreNonEmptyForRealThemes()
    {
        using var doc = LoadManifest();
        foreach (var theme in doc.RootElement.GetProperty("themes").EnumerateArray())
        {
            if (theme.GetProperty("source").GetString() == "virtual") continue;
            var id = theme.GetProperty("id").GetString()!;
            var schemes = theme.GetProperty("schemes").EnumerateArray().Count();
            Assert.True(schemes > 0, $"Theme '{id}' has no schemes.");
        }
    }

    private static JsonDocument LoadManifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "schemas", "theme-preview-manifest.json")));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
