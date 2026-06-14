using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ThemeManagerTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void HighContrastPalette_DefinesEveryKeyFromNormalPalette()
    {
        var normalKeys = ExtractResourceKeys(ReadFile("src", "LibreSpot.Desktop", "Themes", "Palette.xaml"));
        var hcKeys = ExtractResourceKeys(ReadFile("src", "LibreSpot.Desktop", "Themes", "HighContrastPalette.xaml"));

        var missing = normalKeys.Except(hcKeys).ToList();
        Assert.True(
            missing.Count == 0,
            $"HighContrastPalette.xaml is missing keys present in Palette.xaml: {string.Join(", ", missing)}");
    }

    [Fact]
    public void HighContrastPalette_DisablesDropShadows()
    {
        var content = ReadFile("src", "LibreSpot.Desktop", "Themes", "HighContrastPalette.xaml");

        Assert.Contains("CardShadow", content);
        Assert.Contains("OverlayShadow", content);
        Assert.Contains("AccentGlow", content);
        Assert.Contains("TabSelectedGlow", content);
        Assert.DoesNotContain("Opacity=\"0.55\"", content);
        Assert.DoesNotContain("Opacity=\"0.75\"", content);
        Assert.DoesNotContain("Opacity=\"0.45\"", content);
        Assert.DoesNotContain("Opacity=\"0.35\"", content);
    }

    [Fact]
    public void HighContrastPalette_SetsMotionDurationsToNearZero()
    {
        var content = ReadFile("src", "LibreSpot.Desktop", "Themes", "HighContrastPalette.xaml");

        Assert.Contains("MotionFastDuration", content);
        Assert.Contains("MotionMedDuration", content);
        Assert.Contains("MotionSlowDuration", content);
        Assert.DoesNotContain("0:0:0.090", content);
        Assert.DoesNotContain("0:0:0.150", content);
        Assert.DoesNotContain("0:0:0.220", content);
    }

    [Fact]
    public void HighContrastPalette_MapsToSystemColors()
    {
        var content = ReadFile("src", "LibreSpot.Desktop", "Themes", "HighContrastPalette.xaml");

        Assert.Contains("SystemColors.WindowColorKey", content);
        Assert.Contains("SystemColors.WindowTextColorKey", content);
        Assert.Contains("SystemColors.HighlightColorKey", content);
        Assert.Contains("SystemColors.HighlightTextColorKey", content);
        Assert.Contains("SystemColors.ControlColorKey", content);
    }

    [Fact]
    public void ThemeManager_PaletteSearchUsesEndsWith()
    {
        var source = ReadFile("src", "LibreSpot.Desktop", "Services", "ThemeManager.cs");
        Assert.Contains("EndsWith(PaletteSource", source);
        Assert.Contains("EndsWith(HighContrastPaletteSource", source);
        Assert.DoesNotContain(".Contains(\"Palette.xaml\")", source);
    }

    [Fact]
    public void ThemeManager_ClearsReducedMotionOverridesWhenReenabled()
    {
        var source = ReadFile("src", "LibreSpot.Desktop", "Services", "ThemeManager.cs");
        Assert.Contains("ClearReducedMotionOverrides", source);
        Assert.Contains("app.Resources.Remove(key)", source);
    }

    [Fact]
    public void ThemeManager_IsInitializedBeforeWindowInAppStartup()
    {
        var appCs = ReadFile("src", "LibreSpot.Desktop", "App.xaml.cs");

        Assert.Contains("ThemeManager.Initialize", appCs);

        var crashIndex = appCs.IndexOf("CrashReporter.Initialize");
        var themeIndex = appCs.IndexOf("ThemeManager.Initialize");
        Assert.True(themeIndex > crashIndex, "ThemeManager must initialize after CrashReporter.");
    }

    [Fact]
    public void AppResources_MergePaletteBeforeControls()
    {
        var appXaml = ReadFile("src", "LibreSpot.Desktop", "App.xaml");

        var paletteIndex = appXaml.IndexOf("Palette.xaml");
        var controlsIndex = appXaml.IndexOf("Controls.xaml");
        Assert.True(paletteIndex >= 0 && controlsIndex >= 0, "Both Palette and Controls dictionaries must be merged.");
        Assert.True(paletteIndex < controlsIndex, "Palette must be merged before Controls so tokens resolve.");
    }

    private static HashSet<string> ExtractResourceKeys(string xamlContent)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(xamlContent, @"x:Key=""([^""]+)"""))
        {
            keys.Add(match.Groups[1].Value);
        }
        return keys;
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
