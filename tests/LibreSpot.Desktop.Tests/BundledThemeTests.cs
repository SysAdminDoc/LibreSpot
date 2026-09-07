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
    public void UserCssMapsSpotifysHardCodedContextMenuWhitesToTheScheme()
    {
        // Spotify's own xpui.css sets .main-contextMenu-menuItemButton to
        // #ffffffe6, disabled items to #ffffff80 and the expanded fill to
        // #ffffff1a. replace_colors only rewrites --spice-* variables, so on a
        // light scheme those literals stay white on near-white
        // (spicetify/cli#3918). Prism maps them to the scheme's own text
        // colour at the same alpha, which is identical on Dark and readable on
        // Light. This fails if the override is dropped or hard-coded again.
        var css = File.ReadAllText(Path.Combine(ThemeDirectory, "user.css"));

        Assert.Contains(".main-contextMenu-menuItemButton", css, StringComparison.Ordinal);
        Assert.Contains("rgba(var(--spice-rgb-text), 0.9)", css, StringComparison.Ordinal);
        Assert.Contains("rgba(var(--spice-rgb-text), 0.5)", css, StringComparison.Ordinal);
        Assert.Contains("rgba(var(--spice-rgb-text), 0.1)", css, StringComparison.Ordinal);

        // A literal alpha white anywhere in the theme would reintroduce the
        // defect the override exists to fix.
        Assert.DoesNotMatch(new Regex(@"#ffffff[0-9a-f]{2}", RegexOptions.IgnoreCase), css);
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
                @"if \(\$isBundled\) \{(?<body>.+?)\n\s+\} elseif \(\$isCommunity\)",
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

    [Fact]
    public void TheThemeSourceExistsOnceInTheTree()
    {
        // src/LibreSpot.App/vendor/librespot-prism held a second copy that
        // nothing referenced, and it drifted ahead: a settings-menu retry landed
        // there, was announced in the changelog as shipped, and never reached
        // the pinned theme users actually get. The pin gates above cannot see a
        // problem like that, because they only ever look at the copy they pin.
        var bundled = Path.Combine(ThemeDirectory, "theme.js");

        // Searching for a marker string inside the file was the first attempt,
        // and it missed the case that matters: a reintroduced copy that also
        // renames the marker, which is exactly what a diverging copy carries.
        // The question is a hash question, so ask it by hash. Every theme.js in
        // the tree has to be one the pins record; an unpinned one is either a
        // stale duplicate or a copy that has drifted, and both are the defect.
        var pinnedHashes = PinnedSources
            .SelectMany(source => ReadPinnedFiles(ReadFile(source)))
            .Where(pin => pin.Key.Equals("theme.js", StringComparison.OrdinalIgnoreCase))
            .Select(pin => pin.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Guards the lookup rather than the tree: with no pins read, every file
        // below would be reported and the failure would point at the wrong thing.
        Assert.True(
            pinnedHashes.Count > 0,
            "No theme.js hash was read from any pinned source, so this test is not comparing against anything.");

        var skip = new[] { ".git", "node_modules", "bin", "obj", "dist", "publish", "StrykerOutput", "TestResults", "work" };

        // Other themes in the tree are vendored third-party ones with their own
        // theme.js, so the question is narrower than "is this file pinned": it is
        // whether a second copy of *ours* exists anywhere outside the bundled
        // folder. Three signals, because a diverging copy defeats any one of
        // them: the same bytes as a pinned file, our marker, or our name on the
        // directory. The vendor copy that caused this would have matched all
        // three on the day it was made and the last two after it drifted.
        const string marker = "prism:settings";
        const string signature = "function Prism()";
        var bundledRelative = Path.GetRelativePath(RepoRoot, bundled);
        var bundledText = File.ReadAllText(bundled);

        // Guards the two content signals rather than the tree: renaming either
        // would otherwise quietly reduce this to a hash check without saying so.
        Assert.Contains(marker, bundledText, StringComparison.Ordinal);
        Assert.Contains(signature, bundledText, StringComparison.Ordinal);

        // Every .js file, not just ones named theme.js. Narrowing to that name
        // was a coverage loss: the historical copy happened to be called
        // theme.js, and nothing stops the next one being prism.js or theme.min.js.
        var duplicates = Directory.EnumerateFiles(RepoRoot, "*.js", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(RepoRoot, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => skip.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            .Where(path => !Path.GetRelativePath(RepoRoot, path).Equals(bundledRelative, StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                // Four signals, because a copy that drifts defeats any one of
                // them. A rename of the storage key leaves the IIFE name; a
                // rewrite of both still matches a pin until a byte changes; and
                // a copy kept under a Prism-named folder is caught by its path
                // whatever its contents say.
                if (pinnedHashes.Contains(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant())
                    || (Path.GetDirectoryName(path) ?? string.Empty).Contains(ThemeId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var text = File.ReadAllText(path);
                return text.Contains(marker, StringComparison.Ordinal)
                    || text.Contains(signature, StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(RepoRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            $"The bundled {ThemeId} theme source must exist once, so a fix cannot land on a copy nobody ships. "
                + "These look like a second copy of it: " + string.Join(", ", duplicates));

        Assert.True(File.Exists(bundled), $"The bundled theme source is missing from {bundledRelative}.");
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
