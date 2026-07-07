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
    public void ThemeManager_ClearsAllMotionKeysSetByApplyReducedMotion()
    {
        var source = ReadFile("src", "LibreSpot.Desktop", "Services", "ThemeManager.cs");

        var setKeys = new[] { "MotionFast", "MotionMed", "MotionSlow", "MotionFastDuration", "MotionMedDuration", "MotionSlowDuration" };
        foreach (var key in setKeys)
        {
            Assert.True(
                source.Contains($"\"{key}\""),
                $"ClearReducedMotionOverrides must include \"{key}\" so it clears everything ApplyReducedMotion sets.");
        }
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

    [Fact]
    public void XamlCornerRadii_DoNotExceedDocumentedRadiusMaximum()
    {
        var files = new[]
        {
            Path.Combine("src", "LibreSpot.Desktop", "MainWindow.xaml"),
            Path.Combine("src", "LibreSpot.Desktop", "Themes", "Controls.xaml")
        };
        var offenders = new List<string>();

        foreach (var file in files)
        {
            var content = ReadFile(file.Split(Path.DirectorySeparatorChar));
            foreach (Match match in Regex.Matches(content, @"CornerRadius\s*=\s*""(?<value>\d+)""|Property=""CornerRadius""\s+Value=""(?<value>\d+)"""))
            {
                var value = int.Parse(match.Groups["value"].Value);
                if (value > 12)
                {
                    offenders.Add($"{file}: CornerRadius {value} exceeds the 12 px radius token maximum.");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void WpfXaml_HardcodedColorsStayInsidePaletteFiles()
    {
        var xamlRoot = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(xamlRoot, "Themes", "Palette.xaml"),
            Path.Combine(xamlRoot, "Themes", "HighContrastPalette.xaml")
        };
        var offenders = Directory
            .EnumerateFiles(xamlRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(file => !allowed.Contains(file))
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"#[0-9A-Fa-f]{6,8}")
                .Select(match => $"{Path.GetRelativePath(RepoRoot, file)}: hardcoded color {match.Value}"))
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void BodyTextTiers_ClearWcagAaOnEverySurface()
    {
        // The dark palette layers text over four surface tiers (Surface1..
        // SurfaceRaised). Every readable text tier — including the dimmest
        // (Subtle) — must clear WCAG AA (4.5:1) on all of them, so a caption
        // never lands below the accessibility floor no matter which card it
        // sits in.
        var palette = ReadFile("src", "LibreSpot.Desktop", "Themes", "Palette.xaml");
        var surfaces = new[]
        {
            PaletteColor(palette, "Surface1Color"),
            PaletteColor(palette, "Surface2Color"),
            PaletteColor(palette, "Surface3Color"),
            PaletteColor(palette, "SurfaceRaisedColor"),
        };
        var textTiers = new[] { "TextColor", "TextMutedColor", "TextSubtleColor" };

        var offenders = new List<string>();
        foreach (var tier in textTiers)
        {
            var fg = PaletteColor(palette, tier);
            foreach (var (name, bg) in new[] { ("Surface1", surfaces[0]), ("Surface2", surfaces[1]), ("Surface3", surfaces[2]), ("SurfaceRaised", surfaces[3]) })
            {
                var ratio = ContrastRatio(fg, bg);
                if (ratio < 4.5)
                {
                    offenders.Add($"{tier} on {name}: {ratio:F2}:1 (< 4.5:1)");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    private static (double R, double G, double B) PaletteColor(string palette, string key)
    {
        var match = Regex.Match(palette, $@"x:Key=""{Regex.Escape(key)}"">#(?<hex>[0-9A-Fa-f]{{6}})<");
        Assert.True(match.Success, $"Palette color '{key}' not found (expected a 6-digit hex).");
        var hex = match.Groups["hex"].Value;
        return (
            Convert.ToInt32(hex.Substring(0, 2), 16),
            Convert.ToInt32(hex.Substring(2, 2), 16),
            Convert.ToInt32(hex.Substring(4, 2), 16));
    }

    private static double ContrastRatio((double R, double G, double B) a, (double R, double G, double B) b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double RelativeLuminance((double R, double G, double B) c)
    {
        static double Channel(double v)
        {
            v /= 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
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
