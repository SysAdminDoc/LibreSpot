using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

/// <summary>
/// Prism ships inside LibreSpot rather than being downloaded, so nothing external
/// can prove it is intact. These gates hold the theme's files, its pinned hashes,
/// the embed list, and every catalog that offers it to a user against each other.
/// </summary>
public sealed class BundledThemeTests
{
    private const string ThemeId = "Prism";
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly string ThemeDirectory = Path.Combine(RepoRoot, "resources", "themes", ThemeId);

    private static readonly string[] ExpectedSchemes = ["Dark", "Light", "OLED", "HighContrast"];

    private static readonly string[][] PinnedSources =
    [
        ["src", "powershell", "data", "BundledThemes.ps1"],
        ["src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1"],
        ["LibreSpot.ps1"]
    ];

    [Fact]
    public void ThemeFilesAreOnDisk()
    {
        Assert.True(Directory.Exists(ThemeDirectory), $"Bundled theme folder was not found at {ThemeDirectory}.");

        // A Spicetify theme is only a theme when it carries a palette and a stylesheet.
        Assert.True(File.Exists(Path.Combine(ThemeDirectory, "color.ini")));
        Assert.True(File.Exists(Path.Combine(ThemeDirectory, "user.css")));
        Assert.True(File.Exists(Path.Combine(ThemeDirectory, "theme.js")));
    }

    [Fact]
    public void ColorIniDeclaresEverySchemeTheCatalogsOffer()
    {
        var sections = Regex.Matches(File.ReadAllText(Path.Combine(ThemeDirectory, "color.ini")), @"(?m)^\[(?<name>[^\]]+)\]")
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        // A scheme offered in the picker that color.ini does not define leaves the
        // client on Spotify's own colours with no error anywhere.
        Assert.Equal(ExpectedSchemes.ToHashSet(StringComparer.Ordinal), sections);
    }

    [Fact]
    public void PinnedHashesMatchTheFilesOnDiskInEveryHost()
    {
        var actual = Directory.EnumerateFiles(ThemeDirectory)
            .ToDictionary(
                path => Path.GetFileName(path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                StringComparer.Ordinal);

        foreach (var source in PinnedSources)
        {
            var pinned = ReadPinnedFiles(ReadFile(source));
            Assert.True(
                pinned.Count > 0,
                $"No pinned {ThemeId} files were found in {string.Join('/', source)}.");

            // Derived from the folder, not a hand-written list, so a file added to
            // the theme without a pin fails here instead of shipping unverified.
            Assert.Equal(actual.Keys.OrderBy(name => name, StringComparer.Ordinal), pinned.Keys.OrderBy(name => name, StringComparer.Ordinal));
            foreach (var (fileName, hash) in pinned)
            {
                Assert.Equal(actual[fileName], hash);
            }
        }
    }

    [Fact]
    public void EveryThemeFileIsEmbeddedAndExtractedByTheDesktopHost()
    {
        var project = ReadFile("src", "LibreSpot.Core", "LibreSpot.Core.csproj");
        var extracted = BackendScriptService.BundledThemeFiles.Values
            .Select(path => path.Replace(Path.DirectorySeparatorChar, '/'))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(ThemeDirectory))
        {
            var fileName = Path.GetFileName(path);

            // The single-file desktop and CLI builds carry no resources folder, so a
            // theme file that is not embedded simply is not there at install time.
            Assert.Contains($@"resources\themes\{ThemeId}\{fileName}", project, StringComparison.Ordinal);
            Assert.Contains($"LibreSpot.Desktop.Resources.themes.{ThemeId}.{fileName}", project, StringComparison.Ordinal);
            Assert.Contains($"themes/{ThemeId}/{fileName}", extracted);
        }

        Assert.Equal(Directory.EnumerateFiles(ThemeDirectory).Count(), BackendScriptService.BundledThemeFiles.Count);
    }

    [Fact]
    public void DesktopHostExtractsTheThemeWhereTheScriptLooksForItWithThePinnedBytes()
    {
        var runtimeDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new BackendScriptService(runtimeDirectory);

            var assetsDirectory = service.TryEnsureBundledAssets();
            Assert.Equal(Path.Combine(runtimeDirectory, "assets"), assetsDirectory);

            foreach (var sourcePath in Directory.EnumerateFiles(ThemeDirectory))
            {
                // The script reads <assets>\themes\<theme> and rejects anything whose
                // hash is not the pin, so an extracted copy that differs by a byte is
                // the same as no theme at all.
                var extracted = Path.Combine(assetsDirectory!, "themes", ThemeId, Path.GetFileName(sourcePath));
                Assert.True(File.Exists(extracted), $"The bundled theme file was not written to {extracted}.");
                Assert.Equal(
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant(),
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(extracted))).ToLowerInvariant());
            }

            // A second call reuses what is already there rather than rewriting it.
            var probe = Path.Combine(assetsDirectory!, "themes", ThemeId, "color.ini");
            var writtenAt = File.GetLastWriteTimeUtc(probe);
            Assert.Equal(assetsDirectory, service.TryEnsureBundledAssets());
            Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(probe));
        }
        finally
        {
            try { Directory.Delete(runtimeDirectory, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ThemeIsOfferedWithTheSameSchemesEverywhere()
    {
        Assert.True(AppCatalog.ThemeSchemes.ContainsKey(ThemeId), $"{ThemeId} is missing from AppCatalog.ThemeSchemes.");
        Assert.Equal(ExpectedSchemes, AppCatalog.ThemeSchemes[ThemeId]);

        var monolithSchemes = ReadQuotedList(
            ReadFile("LibreSpot.ps1"),
            $@"""{ThemeId}""\s*=\s*@\{{\s*Schemes\s*=\s*@\((?<list>[^)]*)\)");
        Assert.Equal(ExpectedSchemes, monolithSchemes);

        var backendSchemes = ReadQuotedList(
            ReadFile("src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1"),
            $@"'{ThemeId}'\s*=\s*@\((?<list>[^)]*)\)");
        Assert.Equal(ExpectedSchemes, backendSchemes);

        using var previews = JsonDocument.Parse(ReadFile("schemas", "theme-preview-manifest.json"));
        var preview = previews.RootElement.GetProperty("themes").EnumerateArray()
            .Single(theme => theme.GetProperty("id").GetString() == ThemeId);
        Assert.Equal("bundled", preview.GetProperty("source").GetString());
        Assert.True(preview.GetProperty("requiresJs").GetBoolean());
        Assert.False(preview.GetProperty("marketplaceOnly").GetBoolean());
        Assert.Equal(ExpectedSchemes, preview.GetProperty("schemes").EnumerateArray().Select(value => value.GetString()!).ToArray());

        using var catalog = JsonDocument.Parse(ReadFile("schemas", "librespot-customization.json"));
        var catalogTheme = catalog.RootElement.GetProperty("themes").EnumerateArray()
            .Single(theme => theme.GetProperty("id").GetString() == ThemeId);
        Assert.True(catalogTheme.GetProperty("requiresJs").GetBoolean());
        Assert.Equal(ExpectedSchemes, catalogTheme.GetProperty("schemes").EnumerateArray().Select(value => value.GetString()!).ToArray());
    }

    [Fact]
    public void ThemeJsInjectionIsOnInBothHostsAndTheGallery()
    {
        // Prism's scheduled schemes, artwork accent, and effect tiers all live in
        // theme.js. Without inject_theme_js the theme installs and does nothing.
        foreach (var source in new[]
                 {
                     new[] { "LibreSpot.ps1" },
                     ["src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1"]
                 })
        {
            var list = Regex.Match(ReadFile(source), @"\$global:ThemesNeedingJS\s*=\s*@\((?<list>[^)]*)\)").Groups["list"].Value;
            Assert.Contains($"'{ThemeId}'", list.Replace('"', '\''), StringComparison.Ordinal);
        }

        var gallery = ReadFile("src", "LibreSpot.Desktop", "ViewModels", "ThemeGalleryItemViewModel.cs");
        var bundledSet = Regex.Match(gallery, @"BundledThemeNames\s*=\s*new\([^)]*\)\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
        Assert.Contains($"\"{ThemeId}\"", bundledSet, StringComparison.Ordinal);
        var jsSet = Regex.Match(gallery, @"ThemesNeedingJs\s*=\s*new\([^)]*\)\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
        Assert.Contains($"\"{ThemeId}\"", jsSet, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeInstallsFromTheBundleWithoutTouchingTheNetwork()
    {
        foreach (var source in new[]
                 {
                     new[] { "src", "powershell", "shared", "Module-InstallThemes.ps1" },
                     ["LibreSpot.ps1"],
                     ["src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1"]
                 })
        {
            var script = ReadFile(source);
            var bundledBranch = Regex.Match(
                script,
                @"if \(\$isBundled\) \{(?<body>.+?)\n    \} elseif \(\$isCommunity\)",
                RegexOptions.Singleline);
            Assert.True(bundledBranch.Success, $"The bundled theme branch is missing from {string.Join('/', source)}.");

            var body = bundledBranch.Groups["body"].Value;
            Assert.DoesNotContain("Download-FileSafe", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Get-FromAssetCache", body, StringComparison.Ordinal);
            Assert.Contains("Get-FileSha256Lower", body, StringComparison.Ordinal);
            Assert.Contains("LIBRESPOT_BUNDLED_ASSETS", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WorkerRunspaceReceivesTheRegistryAndTheScriptRoot()
    {
        // The install runs in a runspace seeded from $varNamesForWorker. A global
        // missing from that list is simply absent there, which is how a bundled
        // lookup silently ends up searching powershell.exe's own folder.
        var script = ReadFile("LibreSpot.ps1");
        var exported = Regex.Match(script, @"\$varNamesForWorker\s*=\s*@\((?<list>.+?)\n\)", RegexOptions.Singleline).Groups["list"].Value;

        Assert.Contains("'BundledThemes'", exported, StringComparison.Ordinal);
        Assert.Contains("'LibreSpotScriptRoot'", exported, StringComparison.Ordinal);
        Assert.Contains("$global:LibreSpotScriptRoot = $script:ScriptRoot", script, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadPinnedFiles(string script)
    {
        var block = Regex.Match(
            script,
            $@"'{ThemeId}'\s*=\s*@\{{.+?Files\s*=\s*\[ordered\]@\{{(?<files>.+?)\n        \}}",
            RegexOptions.Singleline);

        var pinned = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!block.Success)
        {
            return pinned;
        }

        foreach (Match match in Regex.Matches(block.Groups["files"].Value, @"'(?<name>[^']+)'\s*=\s*'(?<hash>[a-f0-9]{64})'"))
        {
            pinned[match.Groups["name"].Value] = match.Groups["hash"].Value;
        }

        return pinned;
    }

    private static string[] ReadQuotedList(string script, string pattern)
    {
        var match = Regex.Match(script, pattern);
        Assert.True(match.Success, $"Could not find the {ThemeId} scheme list.");
        return Regex.Matches(match.Groups["list"].Value, @"['""](?<value>[^'""]+)['""]")
            .Select(entry => entry.Groups["value"].Value)
            .ToArray();
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine([RepoRoot, .. relativeParts]));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
