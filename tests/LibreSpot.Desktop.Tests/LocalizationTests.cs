using System.Globalization;
using System.IO;
using System.Text.Json;
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
    public void RestoreVanillaCopy_DescribesSpicetifyCleanupAndSpotXRetention()
    {
        var expected = new Dictionary<string, (string Action, string Description, string Button, string Rail, string Summary)>(StringComparer.Ordinal)
        {
            ["en"] = (
                "Remove Spicetify customizations",
                "Remove active Spicetify customizations while leaving SpotX in place. This does not restore an unpatched Spotify client.",
                "Remove customizations",
                "Eligible changes have backups",
                "This removes the visible Spicetify layer and leaves SpotX in place. It does not restore an unpatched Spotify client."),
            ["es"] = (
                "Quitar personalizaciones de Spicetify",
                "Quita las personalizaciones activas de Spicetify y mantiene SpotX. Esto no restaura un cliente de Spotify sin parches.",
                "Quitar personalizaciones",
                "Los cambios compatibles tienen copias de seguridad",
                "Esto elimina la capa visible de Spicetify y mantiene SpotX. No restaura un cliente de Spotify sin parches."),
            ["pt-BR"] = (
                "Remover personalizações do Spicetify",
                "Remove as personalizações ativas do Spicetify e mantém o SpotX. Isso não restaura um cliente do Spotify sem patches.",
                "Remover personalizações",
                "Alterações compatíveis têm backup",
                "Isso remove a camada visível do Spicetify e mantém o SpotX. Não restaura um cliente do Spotify sem patches."),
            ["ru"] = (
                "Удалить настройки Spicetify",
                "Удаляет активные настройки Spicetify, сохраняя SpotX. Это не возвращает Spotify в состояние без патчей.",
                "Удалить настройки",
                "Для поддерживаемых изменений есть резервные копии",
                "Удаляет видимый слой Spicetify и сохраняет SpotX. Spotify не возвращается к состоянию без патчей."),
            ["zh-Hans"] = (
                "移除 Spicetify 自定义内容",
                "移除当前的 Spicetify 自定义内容并保留 SpotX。此操作不会恢复未打补丁的 Spotify 客户端。",
                "移除自定义内容",
                "支持的更改有备份",
                "移除可见的 Spicetify 层并保留 SpotX。此操作不会恢复未打补丁的 Spotify 客户端。")
        };

        var resourcePaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["source"] = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", "Strings.resx")
        };
        foreach (var culture in SupportedCultures)
        {
            resourcePaths[culture] = Path.Combine(
                RepoRoot,
                "src",
                "LibreSpot.Desktop",
                "Properties",
                $"Strings.{culture}.resx");
        }

        foreach (var (culture, path) in resourcePaths)
        {
            var values = XDocument.Load(path).Root!.Elements("data").ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);
            var locale = culture == "source" ? "en" : culture;
            var contract = expected[locale];

            Assert.Equal(contract.Action, values["ActionRestoreVanilla"]);
            Assert.Equal(contract.Action, values["Maintenance_RestoreVanilla_Title"]);
            Assert.Equal(contract.Description, values["Maintenance_RestoreVanilla_Description"]);
            Assert.Equal(contract.Button, values["Maintenance_RestoreVanilla_ButtonText"]);
            Assert.Equal(contract.Rail, values["Vm_SimpleHomeReversible"]);
            Assert.Equal(contract.Summary, values["Vm_MaintenanceSummaryRestoreVanilla"]);
            Assert.Contains("SpotX", contract.Description, StringComparison.Ordinal);
            Assert.Contains("SpotX", contract.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain("Everything is reversible", values["Vm_SimpleHomeReversible"], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AllResources_UseRealEllipsesAndNoHyphenAsADash()
    {
        // Three dots is a typing shortcut, not the character; a spaced hyphen
        // between two clauses is a dash the keyboard could not produce. Both
        // read as drift and both were mixed with the correct forms.
        var spacedHyphen = new Regex(@"\S \- \S", RegexOptions.Compiled);
        var offenders = new List<string>();
        var scanned = 0;

        foreach (var path in EnumerateResourceFiles())
        {
            foreach (var data in XDocument.Load(path).Root!.Elements("data"))
            {
                var name = data.Attribute("name")?.Value ?? "(unnamed)";
                var value = data.Element("value")?.Value ?? string.Empty;
                scanned++;

                if (value.Contains("...", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(path)} '{name}': use … rather than three dots.");
                }

                if (spacedHyphen.IsMatch(value))
                {
                    offenders.Add($"{Path.GetFileName(path)} '{name}': a spaced hyphen is not a dash; split the sentence or use a colon.");
                }
            }
        }

        Assert.True(scanned > 0, "The punctuation scan read no resource values.");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void PlaceholderKeys_HaveACommentNamingEveryPlaceholder()
    {
        // A translator can reorder or drop a {n} without seeing the call site.
        // These are the keys they can break, so each placeholder has to be named.
        var placeholder = new Regex(@"\{(?<index>\d+)(?::[^}]*)?\}", RegexOptions.Compiled);
        var offenders = new List<string>();
        var checkedKeys = 0;

        foreach (var name in new[] { "Strings.resx", "Strings.en.resx" })
        {
            var path = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", name);
            foreach (var data in XDocument.Load(path).Root!.Elements("data"))
            {
                var value = data.Element("value")?.Value ?? string.Empty;
                var indexes = placeholder.Matches(value)
                    .Select(match => match.Groups["index"].Value)
                    .ToHashSet(StringComparer.Ordinal);
                if (indexes.Count == 0)
                {
                    continue;
                }

                checkedKeys++;
                var key = data.Attribute("name")?.Value ?? "(unnamed)";
                var comment = data.Element("comment")?.Value ?? string.Empty;
                var described = placeholder.Matches(comment)
                    .Select(match => match.Groups["index"].Value)
                    .ToHashSet(StringComparer.Ordinal);

                var missing = indexes.Except(described).OrderBy(index => index).ToList();
                if (missing.Count > 0)
                {
                    offenders.Add($"{name} '{key}': the comment does not name {string.Join(", ", missing.Select(index => "{" + index + "}"))}.");
                }
            }
        }

        Assert.True(checkedKeys > 0, "The placeholder scan found no format strings.");
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void SourceResources_UseOneTermPerConcept()
    {
        // Both halves of each pair named the same thing on different surfaces.
        var retired = new (string Term, string Preferred)[]
        {
            ("Spotify build", "Spotify version"),
            ("patch posture", "Premium account mode"),
        };
        var offenders = new List<string>();

        foreach (var name in new[] { "Strings.resx", "Strings.en.resx" })
        {
            var path = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", name);
            foreach (var data in XDocument.Load(path).Root!.Elements("data"))
            {
                var value = data.Element("value")?.Value ?? string.Empty;
                foreach (var (term, preferred) in retired)
                {
                    if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        offenders.Add($"{name} '{data.Attribute("name")?.Value}': says \"{term}\"; the product term is \"{preferred}\".");
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void SatelliteResources_SourceIdenticalValuesHaveReviewedAllowlistEntries()
    {
        var sourceValues = LoadResx().Root!.Elements("data").ToDictionary(
            element => element.Attribute("name")!.Value,
            element => element.Element("value")!.Value,
            StringComparer.Ordinal);
        var allowlistPath = Path.Combine(RepoRoot, "schemas", "localization-identical-allowlist.json");
        using var allowlist = JsonDocument.Parse(File.ReadAllText(allowlistPath));
        Assert.Equal(1, allowlist.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(
            DateOnly.TryParseExact(
                allowlist.RootElement.GetProperty("reviewedOn").GetString(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _));

        var entries = allowlist.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        var protectedTerms = allowlist.RootElement.GetProperty("protectedTerms")
            .EnumerateArray()
            .Select(term => term.GetString()!)
            .ToArray();
        Assert.NotEmpty(protectedTerms);
        Assert.Equal(protectedTerms.Length, protectedTerms.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(protectedTerms, string.IsNullOrWhiteSpace);
        var allowedKeys = entries
            .Select(entry => entry.GetProperty("key").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(entries.Length, allowedKeys.Count);
        Assert.All(entries, entry =>
        {
            var key = entry.GetProperty("key").GetString();
            var reason = entry.GetProperty("reason").GetString();
            Assert.Contains(key!, sourceValues.Keys);
            Assert.False(string.IsNullOrWhiteSpace(reason));
        });
        var observedAllowedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var culture in SupportedCultures.Where(culture => culture != "en"))
        {
            var path = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties", $"Strings.{culture}.resx");
            var targetValues = XDocument.Load(path).Root!.Elements("data").ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")!.Value,
                StringComparer.Ordinal);
            var unreviewed = sourceValues
                .Where(pair => targetValues[pair.Key] == pair.Value && !allowedKeys.Contains(pair.Key))
                .Select(pair => pair.Key)
                .Order(StringComparer.Ordinal)
                .ToArray();
            foreach (var key in sourceValues.Keys.Where(key =>
                         targetValues[key] == sourceValues[key] && allowedKeys.Contains(key)))
            {
                observedAllowedKeys.Add(key);
            }

            Assert.True(
                unreviewed.Length == 0,
                $"Strings.{culture}.resx has source-identical values without review: {string.Join(", ", unreviewed.Take(20))}");

            var changedProtectedTerms = sourceValues
                .SelectMany(pair => protectedTerms
                    .Where(term => pair.Value.Contains(term, StringComparison.Ordinal))
                    .Where(term => !targetValues[pair.Key].Contains(term, StringComparison.Ordinal))
                    .Select(term => $"{pair.Key}: {term}"))
                .ToArray();
            Assert.True(
                changedProtectedTerms.Length == 0,
                $"Strings.{culture}.resx changed protected product/token values: {string.Join(", ", changedProtectedTerms.Take(20))}");
        }

        Assert.Empty(allowedKeys.Except(observedAllowedKeys));
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
            "LibreSpot.Core",
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
        // Localization ownership moved to LibreSpot.Core (RD-35): Core compiles the
        // Strings.Designer.cs and embeds the resx/satellites while the physical resx
        // files stay in the desktop Properties folder for the localization tooling.
        var csproj = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Core", "LibreSpot.Core.csproj"));
        Assert.Contains("Strings.resx", csproj);
        Assert.Contains("PublicResXFileCodeGenerator", csproj);
        Assert.Contains("Strings.Designer.cs", csproj);

        // The desktop project must NOT also own Strings (would create a duplicate type).
        var desktop = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "LibreSpot.Desktop.csproj"));
        Assert.Contains("<Compile Remove=\"Properties\\Strings.Designer.cs\" />", desktop);
    }

    private static IEnumerable<string> EnumerateResourceFiles()
    {
        var properties = Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Properties");
        // SupportedCultures already includes "en", so Strings.en.resx comes
        // from the loop.
        yield return Path.Combine(properties, "Strings.resx");
        foreach (var culture in SupportedCultures)
        {
            yield return Path.Combine(properties, $"Strings.{culture}.resx");
        }
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
