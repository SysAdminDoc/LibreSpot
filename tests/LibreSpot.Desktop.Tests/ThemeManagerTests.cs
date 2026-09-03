using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
    public void SwappingThePaletteAtRuntimeFlattensEffectsAndRecolorsTheFocusRing()
    {
        RunSta(() =>
        {
            var host = new Border();
            host.Resources.MergedDictionaries.Add(LoadPalette("Palette.xaml"));
            var card = new Border();
            host.Child = card;

            // The shell binds these the same way: a DynamicResource in XAML and
            // SetResourceReference from code behind.
            card.SetResourceReference(UIElement.EffectProperty, "OverlayShadow");
            card.SetResourceReference(Border.BorderBrushProperty, "AccentRingBrush");

            var shadow = Assert.IsType<DropShadowEffect>(card.Effect);
            Assert.True(shadow.BlurRadius > 0, "The dark palette must ship a real drop shadow.");
            var darkRing = Assert.IsType<SolidColorBrush>(card.BorderBrush).Color;

            host.Resources.MergedDictionaries[0] = LoadPalette("HighContrastPalette.xaml");

            var flattened = Assert.IsType<DropShadowEffect>(card.Effect);
            Assert.Equal(0d, flattened.BlurRadius);
            Assert.Equal(0d, flattened.Opacity);
            Assert.NotEqual(darkRing, Assert.IsType<SolidColorBrush>(card.BorderBrush).Color);
        });
    }

    [Fact]
    public void WindowChromeReappliesOnAHighContrastToggle()
    {
        // MainWindow drops its SourceInitialized handler after the first call,
        // which reads like the chrome is applied once. It is not: the
        // integration installs its own lasting subscription and clears the
        // custom caption, border, and backdrop when high contrast comes on.
        var source = ReadFile("src", "LibreSpot.Desktop", "Services", "Win11ShellIntegration.cs");

        Assert.Contains("SystemParameters.StaticPropertyChanged +=", source);
        Assert.Contains("SystemParameters.StaticPropertyChanged -=", source);
        Assert.Contains($"e.PropertyName == nameof(SystemParameters.HighContrast)", source);

        // The chrome must match the loaded palette, which --uia-theme=high-contrast
        // can force while SystemParameters.HighContrast stays false. Only the
        // change notification may read SystemParameters directly.
        Assert.Contains("ThemeManager.IsHighContrastPaletteActive", source);
        Assert.DoesNotContain("if (SystemParameters.HighContrast)", source);

        var themeManager = ReadFile("src", "LibreSpot.Desktop", "Services", "ThemeManager.cs");
        Assert.Contains("IsHighContrastPaletteActive => _forceHighContrast || IsHighContrast", themeManager);

        var subscribe = source.IndexOf("SystemParameters.StaticPropertyChanged +=", StringComparison.Ordinal);
        var clearDefinition = source.IndexOf("private static void ClearCustomChrome", StringComparison.Ordinal);
        var clearCall = source.IndexOf("ClearCustomChrome(window, hwnd);", StringComparison.Ordinal);
        Assert.True(subscribe > 0 && clearDefinition > 0 && clearCall > 0);
        Assert.True(clearCall < clearDefinition, "ClearCustomChrome must be reachable from ApplyChrome, not just defined.");

        // ThemeManager must have swapped the palette before the chrome handler
        // reads CanvasColor / TextColor / StrokeColor, which holds because
        // App.OnStartup subscribes it before the window HWND exists.
        var app = ReadFile("src", "LibreSpot.Desktop", "App.xaml.cs");
        Assert.Contains("ThemeManager.Initialize(", app);
        Assert.True(
            app.IndexOf("ThemeManager.Initialize(", StringComparison.Ordinal) <
            app.IndexOf("base.OnStartup(e);", StringComparison.Ordinal),
            "ThemeManager must subscribe before the shell starts, so the palette is swapped first.");
    }

    [Fact]
    public void ScrollingCardListsFadeTheirBottomEdgeExceptInHighContrast()
    {
        // The theme gallery is the one card list that still scrolls inside the
        // page; the profile list flows with the page since the Settings recomposition.
        var view = ReadFile("src", "LibreSpot.Desktop", "Views", "CustomAppearanceSection.xaml");
        Assert.Contains("ScrollFadeBrush", view);
        Assert.Contains("IsHitTestVisible=\"False\"", view);

        RunSta(() =>
        {
            var host = new Border();
            host.Resources.MergedDictionaries.Add(LoadPalette("Palette.xaml"));
            var fade = new Border();
            host.Child = fade;
            fade.SetResourceReference(Border.BackgroundProperty, "ScrollFadeBrush");

            var gradient = Assert.IsType<LinearGradientBrush>(fade.Background);
            Assert.Equal(0, gradient.GradientStops[0].Color.A);
            Assert.Equal(255, gradient.GradientStops[^1].Color.A);

            // High contrast has no soft edges; the scrollbar is the affordance.
            host.Resources.MergedDictionaries[0] = LoadPalette("HighContrastPalette.xaml");
            Assert.Equal(0, Assert.IsType<SolidColorBrush>(fade.Background).Color.A);
        });
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
    public void StoryboardDurations_UseFreezeSafeMotionAnimations()
    {
        var xamlFiles = new[]
        {
            ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml"),
            ReadFile("src", "LibreSpot.Desktop", "Themes", "Controls.xaml")
        };
        var references = xamlFiles
            .SelectMany(content => Regex.Matches(
                content,
                @"StandardDuration=""\{(?<kind>StaticResource|DynamicResource) (?<key>Motion(?:Fast|Med|Slow)Duration|IndeterminateSweepDuration)\}""")
                .Cast<Match>())
            .ToList();

        Assert.Equal(38, references.Count);
        Assert.All(references, reference => Assert.Equal("StaticResource", reference.Groups["kind"].Value));
        Assert.All(xamlFiles, content => Assert.DoesNotContain("<DoubleAnimation", content));
        Assert.Contains("HoldWhenMotionSuppressed=\"True\"", xamlFiles[1]);
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
    public void ThemeManager_UsesRuntimeMotionStateInsteadOfResourceOverrides()
    {
        var source = ReadFile("src", "LibreSpot.Desktop", "Services", "ThemeManager.cs");
        Assert.Contains("ShouldSuppressMotion", source);
        Assert.DoesNotContain("ApplyReducedMotion", source);
        Assert.DoesNotContain("app.Resources[\"Motion", source);
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
    public void RunLogSeverity_UsesLiveSemanticResourcesInsteadOfFrozenConverterBrushes()
    {
        var appXaml = ReadFile("src", "LibreSpot.Desktop", "App.xaml");
        var mainWindow = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");

        Assert.DoesNotContain("LogLevelToBrushConverter", appXaml);
        Assert.DoesNotContain("LogLevelToBrushConverter", mainWindow);
        Assert.Contains("Value=\"{DynamicResource DangerTextBrush}\"", mainWindow);
        Assert.Contains("Value=\"{DynamicResource WarningBrush}\"", mainWindow);
        Assert.Contains("Value=\"{DynamicResource AccentBrush}\"", mainWindow);
        Assert.Contains("Value=\"{DynamicResource SubtleTextBrush}\"", mainWindow);
    }

    [Fact]
    public void XamlCornerRadii_DoNotExceedDocumentedRadiusMaximum()
    {
        var offenders = new List<string>();
        var scanned = 0;

        foreach (var file in EnumerateDesktopXaml())
        {
            scanned++;
            var content = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(content, @"CornerRadius\s*=\s*""(?<value>\d+)""|Property=""CornerRadius""\s+Value=""(?<value>\d+)"""))
            {
                var value = int.Parse(match.Groups["value"].Value);
                if (value > 12)
                {
                    offenders.Add($"{ToRelativePath(file)}: CornerRadius {value} exceeds the 12 px radius token maximum.");
                }
            }
        }

        Assert.True(scanned > 2, $"The radius gate must read every XAML file; it read {scanned}.");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// Every XAML file the desktop ships. Both design gates below used to name
    /// two files, which left the workspace views unchecked.
    /// </summary>
    private static IEnumerable<string> EnumerateDesktopXaml() =>
        Directory.EnumerateFiles(
            Path.Combine(RepoRoot, "src", "LibreSpot.Desktop"),
            "*.xaml",
            SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static string ToRelativePath(string fullPath) =>
        Path.GetRelativePath(RepoRoot, fullPath).Replace('\\', '/');

    [Fact]
    public void XamlCornerRadii_DoNotBypassTheSharedScaleWithRawTwoOrFivePixelValues()
    {
        var palette = ReadFile("src", "LibreSpot.Desktop", "Themes", "Palette.xaml");
        var highContrast = ReadFile("src", "LibreSpot.Desktop", "Themes", "HighContrastPalette.xaml");
        var productionXaml = string.Join(
            Environment.NewLine,
            ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml"),
            ReadFile("src", "LibreSpot.Desktop", "Themes", "Controls.xaml"));

        Assert.Contains("x:Key=\"RadiusXs\">2</CornerRadius>", palette);
        Assert.Contains("x:Key=\"RadiusXs\">2</CornerRadius>", highContrast);
        Assert.DoesNotMatch(@"CornerRadius\s*=\s*""(?:2|5)""", productionXaml);
        Assert.DoesNotMatch(@"Property=""CornerRadius""\s+Value=""(?:2|5)""", productionXaml);
    }

    [Fact]
    public void WpfTypography_UsesTheTenStepProductTypeScale()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "10.5", "11", "12", "13", "14", "16", "18", "20", "24", "30",
            // Two display steps above the text scale, used only by the Home
            // readiness hero: its glyph and its headline.
            "46", "78"
        };
        var offenders = new List<string>();
        var scanned = 0;

        foreach (var file in EnumerateDesktopXaml())
        {
            scanned++;
            var content = File.ReadAllText(file);
            foreach (Match match in Regex.Matches(
                         content,
                         @"(?:FontSize|TextElement\.FontSize)=""(?<value>[0-9.]+)""|Property=""FontSize""\s+Value=""(?<value>[0-9.]+)"""))
            {
                var value = match.Groups["value"].Value;
                if (!allowed.Contains(value))
                {
                    offenders.Add($"{ToRelativePath(file)}: FontSize {value} is outside the product type scale.");
                }
            }
        }

        Assert.True(scanned > 2, $"The typography gate must read every XAML file; it read {scanned}.");
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
        // The dark palette layers text over four card surfaces plus the rail.
        // Every readable text tier — including the dimmest (Subtle / Disabled) —
        // must clear WCAG AA (4.5:1) on all of them, so a caption never lands
        // below the accessibility floor no matter which card it sits in.
        var palette = ReadFile("src", "LibreSpot.Desktop", "Themes", "Palette.xaml");
        var surfaces = new[]
        {
            PaletteColor(palette, "Surface1Color"),
            PaletteColor(palette, "Surface2Color"),
            PaletteColor(palette, "Surface3Color"),
            PaletteColor(palette, "SurfaceRaisedColor"),
            PaletteColor(palette, "RailColor"),
        };
        var textTiers = new[] { "TextColor", "TextMutedColor", "TextSubtleColor", "DisabledTextColor", "DangerTextColor" };

        var offenders = new List<string>();
        foreach (var tier in textTiers)
        {
            var fg = PaletteColor(palette, tier);
            foreach (var (name, bg) in new[] { ("Surface1", surfaces[0]), ("Surface2", surfaces[1]), ("Surface3", surfaces[2]), ("SurfaceRaised", surfaces[3]), ("Rail", surfaces[4]) })
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

    [Fact]
    public void TextOnFillPairs_ClearWcagAaContrast()
    {
        // The primary CTA, destructive, and caution surfaces paint dark "text-on-fill"
        // tokens over saturated accent/danger/warning fills. The palette comments claim
        // these pairs clear WCAG AA (>= 4.5:1) and warn that TextBrush on a Danger fill
        // is only 3.43:1 — but only text-over-surface tiers were gated, so a future tweak
        // to a fill or its on-fill text could silently drop the button/snackbar contrast
        // below the floor. Lock every documented on-fill pairing here.
        var palette = ReadFile("src", "LibreSpot.Desktop", "Themes", "Palette.xaml");
        var pairs = new (string Text, string Fill)[]
        {
            ("TextOnAccentColor", "AccentColor"),
            ("TextOnDangerColor", "DangerFillColor"),
            ("TextOnWarningColor", "WarningFillColor"),
        };

        var offenders = new List<string>();
        foreach (var (textKey, fillKey) in pairs)
        {
            var ratio = ContrastRatio(PaletteColor(palette, textKey), PaletteColor(palette, fillKey));
            if (ratio < 4.5)
            {
                offenders.Add($"{textKey} on {fillKey}: {ratio:F2}:1 (< 4.5:1)");
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

    private static ResourceDictionary LoadPalette(string fileName)
    {
        using var stream = File.OpenRead(
            Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Themes", fileName));
        return (ResourceDictionary)XamlReader.Load(stream);
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

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
