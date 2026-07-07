using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ColorLintTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static readonly HashSet<string> PaletteXamlFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "src/LibreSpot.Desktop/Themes/Palette.xaml",
        "src/LibreSpot.Desktop/Themes/HighContrastPalette.xaml"
    };

    private static readonly HashSet<string> AllowedNamedXamlColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "Transparent"
    };

    private static readonly Regex XamlHexColorPattern = new(
        @"#[0-9A-Fa-f]{3,4}(?![0-9A-Fa-f])|#[0-9A-Fa-f]{6,8}(?![0-9A-Fa-f])",
        RegexOptions.Compiled);

    private static readonly Regex XamlNamedColorPattern = new(
        @"\b(?:Background|Foreground|BorderBrush|Fill|Stroke|Color|CaretBrush|SelectionBrush|SelectionTextBrush)\s*=\s*""(?<value>[A-Za-z][A-Za-z0-9]*)""|<Setter\s+Property=""(?:Background|Foreground|BorderBrush|Fill|Stroke|Color|CaretBrush|SelectionBrush|SelectionTextBrush)""\s+Value=""(?<value>[A-Za-z][A-Za-z0-9]*)""",
        RegexOptions.Compiled);

    private static readonly CSharpColorPattern[] CSharpColorPatterns =
    {
        new("hex string", new Regex(@"""#[0-9A-Fa-f]{3,8}""", RegexOptions.Compiled)),
        new("interpolated swatch", new Regex(@"\$""#\{[^""\r\n]+""", RegexOptions.Compiled)),
        new("color api", new Regex(@"\bColor\.From(?:Rgb|Argb)\s*\(", RegexOptions.Compiled)),
        new("named color", new Regex(@"\bColors\.[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled)),
        new("named brush", new Regex(@"\bBrushes\.[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled)),
        new("solid brush", new Regex(@"\bnew\s+SolidColorBrush\s*\(", RegexOptions.Compiled)),
        new("brush converter", new Regex(@"\bnew\s+BrushConverter\s*\(", RegexOptions.Compiled)),
        new("converter parsing", new Regex(@"\.ConvertFromString\s*\(", RegexOptions.Compiled)),
        new("colorref bytes", new Regex(@"\bToColorRef\s*\(\s*0x[0-9A-Fa-f]{1,2}\s*,\s*0x[0-9A-Fa-f]{1,2}\s*,\s*0x[0-9A-Fa-f]{1,2}", RegexOptions.Compiled))
    };

    [Fact]
    public void WpfXaml_ColorLiteralsStayInsidePaletteFiles()
    {
        var xamlRoot = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop");
        var offenders = Directory
            .EnumerateFiles(xamlRoot, "*.xaml", SearchOption.AllDirectories)
            .Select(file => new
            {
                Path = ToRelativePath(file),
                Content = File.ReadAllText(file)
            })
            .Where(file => !PaletteXamlFiles.Contains(file.Path))
            .SelectMany(file => FindXamlColorLiterals(file.Path, file.Content))
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void DesktopCSharp_ColorConstructionIsExplicitlyAllowlisted()
    {
        var sourceRoot = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop");
        var offenders = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGeneratedOrBuildOutput(file))
            .Select(file => new
            {
                Path = ToRelativePath(file),
                Content = File.ReadAllText(file)
            })
            .SelectMany(file => FindCSharpColorOccurrences(file.Path, file.Content)
                .Where(occurrence => !IsAllowedCSharpColorOccurrence(occurrence)))
            .ToList();

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [InlineData(@"<Border Background=""#FFF"" />", "#FFF")]
    [InlineData(@"<Border Background=""#FFFF"" />", "#FFFF")]
    [InlineData(@"<Border Background=""#FFFFFF"" />", "#FFFFFF")]
    [InlineData(@"<Border Background=""#80FFFFFF"" />", "#80FFFFFF")]
    [InlineData(@"<Border Background=""Black"" />", "Black")]
    [InlineData(@"<Setter Property=""Foreground"" Value=""White"" />", "White")]
    public void XamlColorLint_DetectsBlindSpotLiterals(string xaml, string expected)
    {
        var offenders = FindXamlColorLiterals("src/LibreSpot.Desktop/Fake.xaml", xaml).ToList();

        Assert.Contains(offenders, offender => offender.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void XamlColorLint_AllowsTransparentHitTestBackgrounds()
    {
        var offenders = FindXamlColorLiterals(
                "src/LibreSpot.Desktop/Fake.xaml",
                @"<Border Background=""Transparent"" BorderBrush=""Transparent"" />")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void CSharpColorLint_DetectsBlindSpotConstructions()
    {
        var source = string.Join(
            Environment.NewLine,
            "var hex = \"#FFF\";",
            "var argb = Color.FromArgb(255, 1, 2, 3);",
            "var namedColor = Colors.Black;",
            "var namedBrush = Brushes.White;",
            "var solidBrush = new SolidColorBrush(color);",
            "var parsed = new BrushConverter().ConvertFromString(\"#FFFFFF\");",
            "var chrome = ToColorRef(0x01, 0x02, 0x03);",
            "var swatch = $\"#{r:X2}{g:X2}{b:X2}\";");

        var kinds = FindCSharpColorOccurrences("src/LibreSpot.Desktop/Fake.cs", source)
            .Select(occurrence => occurrence.Kind)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("hex string", kinds);
        Assert.Contains("color api", kinds);
        Assert.Contains("named color", kinds);
        Assert.Contains("named brush", kinds);
        Assert.Contains("solid brush", kinds);
        Assert.Contains("brush converter", kinds);
        Assert.Contains("converter parsing", kinds);
        Assert.Contains("colorref bytes", kinds);
        Assert.Contains("interpolated swatch", kinds);
    }

    private static IEnumerable<string> FindXamlColorLiterals(string relativePath, string content)
    {
        foreach (Match match in XamlHexColorPattern.Matches(content))
        {
            yield return $"{relativePath}:{LineNumber(content, match.Index)}: hardcoded XAML color {match.Value}";
        }

        foreach (Match match in XamlNamedColorPattern.Matches(content))
        {
            var value = match.Groups["value"].Value;
            if (AllowedNamedXamlColors.Contains(value))
            {
                continue;
            }

            yield return $"{relativePath}:{LineNumber(content, match.Index)}: named XAML color {value}";
        }
    }

    private static IEnumerable<CSharpColorOccurrence> FindCSharpColorOccurrences(string relativePath, string content)
    {
        foreach (var pattern in CSharpColorPatterns)
        {
            foreach (Match match in pattern.Regex.Matches(content))
            {
                yield return new CSharpColorOccurrence(
                    relativePath,
                    LineNumber(content, match.Index),
                    pattern.Kind,
                    match.Value);
            }
        }
    }

    private static bool IsAllowedCSharpColorOccurrence(CSharpColorOccurrence occurrence)
    {
        var path = occurrence.RelativePath;
        return path switch
        {
            "src/LibreSpot.Desktop/Services/CrashReporter.cs" =>
                occurrence.Kind is "hex string" or "brush converter" or "converter parsing",
            "src/LibreSpot.Desktop/Converters/LogLevelToBrushConverter.cs" =>
                (occurrence.Kind == "named color" && occurrence.Value == "Colors.Gray")
                || occurrence.Kind == "solid brush",
            "src/LibreSpot.Desktop/ViewModels/MainViewModel.cs" =>
                occurrence.Kind == "interpolated swatch",
            "src/LibreSpot.Desktop/Services/Win11ShellIntegration.cs" =>
                occurrence.Kind == "colorref bytes",
            _ => false
        };
    }

    private static bool IsGeneratedOrBuildOutput(string path)
    {
        var normalized = NormalizePath(path);
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/Properties/Strings.Designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToRelativePath(string path) =>
        NormalizePath(Path.GetRelativePath(RepoRoot, path));

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static int LineNumber(string content, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (content[i] == '\n')
            {
                line++;
            }
        }

        return line;
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

    private sealed record CSharpColorPattern(string Kind, Regex Regex);

    private sealed record CSharpColorOccurrence(string RelativePath, int Line, string Kind, string Value)
    {
        public override string ToString() =>
            $"{RelativePath}:{Line}: unexpected C# {Kind} {Value}";
    }
}
