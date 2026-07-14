using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LibreSpot.Desktop.Services;
using Strings = LibreSpot.Desktop.Properties.Strings;
using Xunit;

namespace LibreSpot.Desktop.Tests;

[CollectionDefinition("Localization", DisableParallelization = true)]
public sealed class LocalizationTestCollection;

[Collection("Localization")]
public sealed class LocalizationTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly string[] SupportedCultures = ["en", "ru", "zh-Hans", "pt-BR", "es"];

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
    public void SatelliteResources_ExistAndMatchSourceKeys()
    {
        var sourceKeys = GetResourceKeys(LoadResx());

        foreach (var culture in SupportedCultures)
        {
            var path = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", $"Strings.{culture}.resx");
            Assert.True(File.Exists(path), $"Missing localization resource file for {culture}.");
            var doc = XDocument.Load(path);
            var targetKeys = GetResourceKeys(doc);

            Assert.Empty(sourceKeys.Except(targetKeys));
            Assert.Empty(targetKeys.Except(sourceKeys));

            foreach (var data in doc.Root!.Elements("data"))
            {
                var name = data.Attribute("name")?.Value ?? "(unnamed)";
                Assert.False(
                    string.IsNullOrWhiteSpace(data.Element("value")?.Value),
                    $"{Path.GetFileName(path)} key '{name}' has an empty value.");
            }
        }
    }

    [Fact]
    public void SatelliteResources_AvoidKnownMachineTranslationRegressions()
    {
        var ru = XDocument.Load(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", "Strings.ru.resx"));
        var zhHans = XDocument.Load(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", "Strings.zh-Hans.resx"));
        var ruText = string.Join("\n", ru.Root!.Elements("data").Select(e => e.Element("value")?.Value));
        var zhHansText = string.Join("\n", zhHans.Root!.Elements("data").Select(e => e.Element("value")?.Value));

        Assert.DoesNotContain("Закрывать", ruText);
        Assert.DoesNotContain("заявк", ruText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("重新申请", zhHansText);
        Assert.DoesNotContain("观察者", zhHansText);
        Assert.DoesNotContain("观察程序", zhHansText);
        Assert.DoesNotContain("Spisetify", zhHansText);
        Assert.DoesNotContain("维修市场", zhHansText);
    }

    [Fact]
    public void ProductionXaml_UserFacingAttributesUseLocalizedBindings()
    {
        var desktopRoot = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop");
        var matches = Directory.EnumerateFiles(desktopRoot, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    "\\b(?:Text|Content|Header|ToolTip|Description|Title|AutomationProperties\\.Name|AutomationProperties\\.HelpText)=\"(?<value>[^\\{][^\"]*)\"")
                .Cast<Match>()
                .Where(match => !match.Groups["value"].Value.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                .Select(match => $"{Path.GetRelativePath(RepoRoot, path)}: {match.Value}"))
            .ToArray();

        Assert.Empty(matches);
    }

    [Fact]
    public void HealthComponents_UserFacingConstructionTextUsesResources()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "LibreSpot.Desktop",
            "Services",
            "EnvironmentSnapshotService.cs"));
        var violations = Regex.Matches(source, @"(?ms)(?:Component|new StackHealthComponent)\(.+?\);")
            .Cast<Match>()
            .SelectMany(block => Regex.Matches(
                    block.Value,
                    "(?<![A-Za-z])\"(?<value>[^\"\\r\\n]*[^\\S\\r\\n]+[^\"\\r\\n]*)\"")
                .Cast<Match>())
            .Where(match => Regex.IsMatch(match.Groups["value"].Value, @"[\p{L}\p{N}]"))
            .Select(match => match.Groups["value"].Value)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void HealthSnapshot_UsesSelectedNonEnglishResources()
    {
        var originalCulture = Strings.Culture;
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Localization.Tests", Guid.NewGuid().ToString("N"));

        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo("es");
            var service = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => false,
                spotifyPath: Path.Combine(root, "Spotify", "Spotify.exe"),
                spicetifyPath: Path.Combine(root, "Spicetify", "spicetify.exe"),
                spicetifyConfigDirectory: Path.Combine(root, "SpicetifyConfig"),
                backupDirectory: Path.Combine(root, "Backups"),
                rollingLogDirectory: Path.Combine(root, "Logs"),
                crashDirectory: Path.Combine(root, "Crashes"),
                upstreamDriftProbe: () => LibreSpot.Desktop.Models.UpstreamDriftReport.Empty,
                communityAssetDriftProbe: () => LibreSpot.Desktop.Models.CommunityAssetDriftReport.Empty);

            var snapshot = service.GetSnapshot(Path.Combine(root, "Profile", "config.json"));
            var spotify = Assert.Single(snapshot.HealthReport.Components, component => component.Id == "spotify");
            var spotX = Assert.Single(snapshot.HealthReport.Components, component => component.Id == "spotx");

            Assert.Equal("No instalado", spotify.Status);
            Assert.Contains("No se encontró Spotify.exe", spotify.Evidence);
            Assert.Equal("Ejecute la configuración recomendada", spotify.RecommendedActionText);
            Assert.Equal("Parche de SpotX", spotX.Name);
        }
        finally
        {
            Strings.Culture = originalCulture;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ViewModels_RuntimeLocalizationKeysExist()
    {
        var source = string.Join(
            "\n",
            ReadViewModelSource("MainViewModel.cs"),
            ReadViewModelSource("ActivityRunStateViewModel.cs"),
            ReadViewModelSource("EnvironmentSnapshotStateViewModel.cs"),
            ReadViewModelSource("PromptStateViewModel.cs"));
        var usedKeys = Regex.Matches(source, "\"(?<key>Vm_[A-Za-z0-9_]+)\"")
            .Cast<Match>()
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var resourceKeys = GetResourceKeys(LoadResx());

        Assert.NotEmpty(usedKeys);
        Assert.DoesNotContain(usedKeys, key => !resourceKeys.Contains(key));
    }

    [Fact]
    public void ViewModels_UserFacingComputedTextUsesResources()
    {
        var checks = new[]
        {
            (
                Source: ReadViewModelSource("MainViewModel.cs"),
                Phrases: new[]
                {
                    "No profile selected",
                    "Quick actions",
                    "Ready for upkeep or reset",
                    "Search titles and descriptions across Custom.",
                    "LibreSpot keeps the live log and diagnostics on disk while this runs.",
                    "Estimated local zip size before compression:",
                    "Best on an existing install",
                    "Copied profile share link.",
                    "Clipboard was unavailable. Use Export to share the profile file instead.",
                    "Copied profile comparison.",
                    "Clipboard was unavailable. The comparison remains visible here.",
                    "Clipboard was unavailable. Log is still saved to install.log.",
                    "Health report",
                    "Custom patches dry run passed",
                    "Run recommended setup",
                    "Apply custom profile",
                    "LibreSpot will download, verify, and apply the selected setup.",
                    "Keep current setup",
                    "Administrator permission required",
                    "Risk acknowledgment",
                    "Close LibreSpot now?",
                    "Close and stop run",
                    "Keep LibreSpot open",
                    "Safer choice",
                    "Configuration save was canceled.",
                    "Backend run was canceled.",
                    "Resuming the confirmed setup after the administrator relaunch.",
                    "What happens next",
                    "LibreSpot will make the requested change and leave the result visible here so you can review it afterward.",
                    "LibreSpot will keep the window open, stream progress here, and leave the result easy to review afterward.",
                    "Desktop command failed:",
                    "Could not save configuration:",
                    "Backend run failed:",
                    "Couldn't open the LibreSpot folder:",
                    "Could not load the staged setup to resume after elevation:"
                }
            ),
            (
                Source: ReadViewModelSource("ActivityRunStateViewModel.cs"),
                Phrases: new[]
                {
                    "No log output yet",
                    "1 log line"
                }
            ),
            (
                Source: ReadViewModelSource("EnvironmentSnapshotStateViewModel.cs"),
                Phrases: new[]
                {
                    "Last refreshed ",
                    "Status not checked yet",
                    "Environment checked just now",
                    "Environment checked recently",
                    "Refresh recommended",
                    "Environment may have changed",
                    "Use Refresh environment",
                    "Last checked at ",
                    "Refresh after you change Spotify",
                    "Recheck before you repair or reset"
                }
            )
        };

        foreach (var check in checks)
        {
            foreach (var phrase in check.Phrases)
            {
                Assert.DoesNotContain(phrase, check.Source);
            }
        }
    }

    [Fact]
    public void CrowdinConfig_MapsSupportedResourceFiles()
    {
        var path = Path.Combine(RepoRoot, ".crowdin.yml");
        Assert.True(File.Exists(path), ".crowdin.yml is required for localization sync.");
        var config = File.ReadAllText(path);

        Assert.Contains("/src/LibreSpot.Desktop/Properties/Strings.resx", config);
        Assert.Contains("/src/LibreSpot.Desktop/Properties/Strings.%locale%.resx", config);
        foreach (var culture in SupportedCultures)
        {
            Assert.Contains(culture, config);
        }
    }

    [Fact]
    public void LocalizationService_SwitchesCulturesAtRuntime()
    {
        var service = LocalizationService.Current;
        service.ApplyCulture("es");
        Assert.Equal("es", service.CultureName);
        Assert.False(string.IsNullOrWhiteSpace(service["AppTitle"]));

        service.ApplyCulture("not-real");
        Assert.Equal(LocalizationService.DefaultCultureName, service.CultureName);
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

    private static HashSet<string> GetResourceKeys(XDocument doc) =>
        doc.Root!.Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    private static string ReadViewModelSource(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "ViewModels", fileName));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
