using System.IO;
using System.Xml.Linq;
using Xunit;

namespace LibreSpot.Desktop.Tests;

/// <summary>
/// Static-analysis guard (no UI launch, matching the house test style) that
/// every interactive control in MainWindow.xaml exposes a UIA-discoverable
/// name, and that the one custom control keeps its live-region automation peer.
/// This is the durable, regression-proof half of WCAG 2.2 4.1.2 discoverability;
/// the runtime FlaUI smoke tests exercise it live but cannot run headless in CI.
/// </summary>
public sealed class AutomationNameContractTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    // Controls a keyboard/AT user lands on and that therefore need a name.
    private static readonly HashSet<string> InteractiveControls = new(StringComparer.Ordinal)
    {
        "Button", "ToggleButton", "RadioButton", "CheckBox", "ComboBox",
        "TextBox", "PasswordBox", "Slider", "TabItem"
    };

    // Any one of these attributes gives the control an accessible name:
    // an explicit UIA name, a label association, textual Content, or a Header.
    private static readonly HashSet<string> NameSourceAttributes = new(StringComparer.Ordinal)
    {
        "AutomationProperties.Name", "AutomationProperties.LabeledBy", "Content", "Header"
    };

    [Fact]
    public void EveryInteractiveControl_ExposesAnAutomationName()
    {
        var xaml = XDocument.Load(Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "MainWindow.xaml"));

        var unnamed = xaml.Descendants()
            .Where(element => InteractiveControls.Contains(element.Name.LocalName))
            .Where(element => !HasNameSource(element))
            .Select(DescribeElement)
            .ToList();

        Assert.True(
            unnamed.Count == 0,
            "Interactive controls without a UIA-discoverable name (add AutomationProperties.Name, " +
            "Content, or Header):" + Environment.NewLine + string.Join(Environment.NewLine, unnamed));
    }

    [Fact]
    public void LiveRegionContentControl_KeepsPoliteLiveRegionPeer()
    {
        var control = File.ReadAllText(
            Path.Combine(RepoRoot, "src", "LibreSpot.Desktop", "Controls", "LiveRegionContentControl.cs"));

        // Async status changes must announce via a live region, not silently.
        Assert.Contains("OnCreateAutomationPeer", control);
        Assert.Contains("AutomationLiveSetting.Polite", control);
        Assert.Contains("AutomationEvents.LiveRegionChanged", control);
    }

    private static bool HasNameSource(XElement element)
    {
        foreach (var attribute in element.Attributes())
        {
            if (NameSourceAttributes.Contains(attribute.Name.LocalName) &&
                !string.IsNullOrWhiteSpace(attribute.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribeElement(XElement element)
    {
        var name = element.Attributes()
            .FirstOrDefault(a => a.Name.LocalName == "Name")?.Value ?? "(anonymous)";
        return $"  {element.Name.LocalName} Name={name}";
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
