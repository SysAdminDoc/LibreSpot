using System.Globalization;
using System.Text.RegularExpressions;
using LibreSpot.Desktop.Models;
using Xunit;

namespace LibreSpot.Desktop.Tests;

/// <summary>
/// Diagnostic timestamps are read by whoever is handed a support bundle, so
/// they have to mean the same thing on every machine. Formatting them with the
/// current culture put a Buddhist or Hijri year into health output on a
/// th-TH or ar-SA host, and a local-time crash filename collides across a DST
/// fall-back hour and sorts against the UTC journal wrongly.
/// </summary>
[Collection("Localization")]
public sealed class DiagnosticTimestampTests
{
    [Theory]
    [InlineData("th-TH")]
    [InlineData("ar-SA")]
    [InlineData("en-US")]
    public void ComponentLastChanged_IsGregorianIsoUnderEveryCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            var component = new StackHealthComponent(
                "spotify",
                "Spotify",
                "Detected",
                "Ready",
                "1.2.93",
                @"C:\Spotify\Spotify.exe",
                new DateTime(2026, 9, 4, 13, 45, 0, DateTimeKind.Local),
                "evidence",
                []);

            Assert.Equal("2026-09-04 13:45", component.LastChangedDisplay);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void CrashReportNames_UseUtcSoTheyCannotCollideAcrossADstFallBack()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot(), "src", "LibreSpot.Desktop", "Services", "CrashReporter.cs"));

        var stamp = Regex.Match(source, @"var stamp = (?<expression>[^;]+);");
        Assert.True(stamp.Success, "CrashReporter must build a filename stamp.");

        var expression = stamp.Groups["expression"].Value;
        Assert.Contains("DateTime.UtcNow", expression, StringComparison.Ordinal);
        Assert.Contains("CultureInfo.InvariantCulture", expression, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", expression, StringComparison.Ordinal);
    }

    [Fact]
    public void HealthValueFormatting_DoesNotFallBackToTheCurrentCulture()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot(), "src", "LibreSpot.Core", "EnvironmentSnapshotService.cs"));

        foreach (Match match in Regex.Matches(source, @"ToString\(""yyyy-MM-dd[^""]*""(?<rest>[^)]*)\)"))
        {
            Assert.Contains("CultureInfo.InvariantCulture", match.Groups["rest"].Value, StringComparison.Ordinal);
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
