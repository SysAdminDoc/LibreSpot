using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class WpfUiTemplateContractTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static readonly HashSet<string> LeakTargetTypes = new(StringComparer.Ordinal)
    {
        "Button", "ToggleButton", "RepeatButton", "TextBox"
    };

    [Fact]
    public void ControlTemplateChildren_SetAnExplicitStyleSoWpfUiImplicitsCannotLeak()
    {
        var offenders = new List<string>();
        foreach (var path in EnumerateDesktopXaml())
        {
            var document = XDocument.Load(path);
            var relative = ToRelative(path);
            foreach (var template in document.Descendants().Where(element => element.Name.LocalName == "ControlTemplate"))
            {
                var targetType = template.Attribute("TargetType")?.Value ?? "(untyped)";
                foreach (var child in template.Descendants().Where(element => LeakTargetTypes.Contains(element.Name.LocalName)))
                {
                    if (HasAttribute(child, "Style"))
                    {
                        continue;
                    }

                    offenders.Add($"{relative}: <{child.Name.LocalName}> inside ControlTemplate TargetType={targetType} has no Style (use Style=\"{{x:Null}}\" or an explicit keyed style).");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void SearchBoxes_UseThePremiumWatermarkInsteadOfASiblingLabel()
    {
        var controls = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Themes", "Controls.xaml"));
        Assert.Contains("x:Name=\"Watermark\"", controls);
        Assert.Contains("TemplateBinding Tag", controls);

        var appearance = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Views", "CustomAppearanceSection.xaml"));
        Assert.Contains("Tag=\"{services:Loc ThemeGallerySearchName}\"", appearance);
        Assert.Contains("Text=\"{services:Loc ThemePackLabel}\"", appearance);

        var custom = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Views", "CustomWorkspaceView.xaml"));
        Assert.Contains("Tag=\"{services:Loc SearchPlaceholder}\"", custom);
        Assert.DoesNotContain("Style=\"{StaticResource LabelTextStyle}\"", custom);
    }

    [Fact]
    public void SnapshotProbes_ResolvePowerShellThroughTheSharedHostPath()
    {
        var snapshot = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Core", "EnvironmentSnapshotService.cs"));
        var backend = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Core", "BackendScriptService.cs"));

        Assert.DoesNotContain("FileName = \"powershell.exe\"", snapshot);
        Assert.Contains("FileName = PowerShellHostPath.Resolve()", snapshot);
        Assert.Contains("PowerShellHostPath.Resolve()", backend);
        Assert.Contains("LibreSpotPaths.LogsDirectory", snapshot);
        Assert.Contains("LibreSpotPaths.CrashesDirectory", snapshot);
        Assert.Contains("LibreSpotPaths.ConfigDirectory", snapshot);
        Assert.Contains("LibreSpotPaths.RuntimeDirectory", File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Core", "BackendScriptService.cs")));
    }

    [Fact]
    public void Maintenance_ShowsRetryWhenSnapshotLoadFails()
    {
        var maintenance = File.ReadAllText(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Views", "MaintenanceWorkspaceView.xaml"));
        Assert.Contains("MaintenanceRetrySystemCheckButton", maintenance);
        Assert.Contains("HasSnapshotLoadError", maintenance);
    }

    [Fact]
    public void InteractiveControls_DisableWithTokenNotRootOpacity()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "PrimaryButtonStyle",
            "SecondaryButtonStyle",
            "SettingCheckBoxStyle",
            "ComboBoxStylePremium"
        };

        var document = XDocument.Load(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Themes", "Controls.xaml"));
        var styles = document.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Where(style => keys.Contains(style.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value ?? string.Empty))
            .ToList();

        Assert.Equal(keys.Count, styles.Count);

        var offenders = new List<string>();
        foreach (var style in styles)
        {
            var key = style.Attributes().First(attribute => attribute.Name.LocalName == "Key").Value;
            var disabledTriggers = style.Descendants()
                .Where(element => element.Name.LocalName == "Trigger")
                .Where(trigger =>
                    trigger.Attributes().Any(attribute => attribute.Name.LocalName == "Property" && attribute.Value == "IsEnabled") &&
                    trigger.Attributes().Any(attribute => attribute.Name.LocalName == "Value" && attribute.Value == "False"));

            foreach (var trigger in disabledTriggers)
            {
                var setters = trigger.Elements().Where(element => element.Name.LocalName == "Setter").ToList();
                if (setters.Any(setter =>
                        setter.Attributes().Any(attribute => attribute.Name.LocalName == "Property" && attribute.Value == "Opacity")))
                {
                    offenders.Add($"{key}: disabled trigger still sets Opacity");
                }

                if (!setters.Any(setter =>
                        setter.Attributes().Any(attribute => attribute.Name.LocalName == "Property" && attribute.Value == "Foreground") &&
                        setter.Attributes().Any(attribute => attribute.Name.LocalName == "Value" && attribute.Value.Contains("DisabledTextBrush", StringComparison.Ordinal))))
                {
                    offenders.Add($"{key}: disabled trigger does not set DisabledTextBrush");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void CatalogChecklist_BloomDecisionMatchesTheReviewedManifest()
    {
        using var assets = LoadJson("schemas", "community-assets.json");
        using var checklist = LoadJson("schemas", "catalog-refresh-checklist.json");

        var bloom = assets.RootElement
            .GetProperty("themes")
            .EnumerateArray()
            .Single(theme => theme.GetProperty("themeId").GetString() == "Bloom")
            .GetProperty("catalogReview");

        var bloomChecklist = checklist.RootElement
            .GetProperty("evaluatedCandidates")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("candidate").GetString() == "nimsandu/spicetify-bloom");

        Assert.Equal(bloom.GetProperty("decision").GetString(), bloomChecklist.GetProperty("decision").GetString());
        Assert.Equal(bloom.GetProperty("lastPush").GetString(), bloomChecklist.GetProperty("lastPush").GetString());
    }

    private static JsonDocument LoadJson(params string[] relativeParts) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray())));

    private static IEnumerable<string> EnumerateDesktopXaml() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop"), "*.xaml", SearchOption.AllDirectories);

    private static bool HasAttribute(XElement element, string localName) =>
        element.Attributes().Any(attribute => attribute.Name.LocalName == localName);

    private static string ToRelative(string path) =>
        Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');

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
