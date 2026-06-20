using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class KeyboardFocusContractTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void PromptOverlay_HasTabNavigationCycle()
    {
        var xaml = ReadXaml();
        var promptSection = ExtractSection(xaml, "PromptDialogRoot", 300);
        Assert.Contains("TabNavigation=\"Cycle\"", promptSection);
    }

    [Fact]
    public void ActivityOverlay_HasTabNavigationCycle()
    {
        var xaml = ReadXaml();
        var activitySection = ExtractSection(xaml, "ActivityDialogRoot", 300);
        Assert.Contains("TabNavigation=\"Cycle\"", activitySection);
    }

    [Fact]
    public void PromptCancelButton_IsCancel()
    {
        var xaml = ReadXaml();
        Assert.Matches(@"Name=""PromptCancelButton""[^>]*IsCancel=""True""", xaml);
    }

    [Fact]
    public void PromptConfirmButton_HasIsDefault()
    {
        var xaml = ReadXaml();
        Assert.Matches(@"Name=""PromptConfirmButton""[^>]*IsDefault=""\{", xaml);
    }

    [Fact]
    public void Window_HasEscapeKeyBinding()
    {
        var xaml = ReadXaml();
        Assert.Contains("Key=\"Escape\"", xaml);
        Assert.Contains("EscapeCommand", xaml);
    }

    [Fact]
    public void Window_HasRefreshKeyBinding()
    {
        var xaml = ReadXaml();
        Assert.Contains("Key=\"F5\"", xaml);
        Assert.Contains("RefreshSnapshotCommand", xaml);
    }

    [Fact]
    public void ActivityDialogRoot_IsFocusable()
    {
        var xaml = ReadXaml();
        var activitySection = ExtractSection(xaml, "ActivityDialogRoot", 50);
        Assert.Contains("Focusable=\"True\"", activitySection);
    }

    [Theory]
    [InlineData("Button")]
    [InlineData("ComboBox")]
    [InlineData("CheckBox")]
    public void ControlStyles_HaveCustomFocusRing(string controlType)
    {
        var controls = ReadFile("src", "LibreSpot.Desktop", "Themes", "Controls.xaml");
        Assert.Contains($"TargetType=\"{controlType}\"", controls);
        Assert.Contains("IsKeyboardFocused", controls);
        Assert.Contains("AccentRingBrush", controls);
    }

    [Fact]
    public void CodeBehind_SavesAndRestoresFocusForPrompt()
    {
        var codeBehind = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");
        Assert.Contains("_focusBeforePrompt", codeBehind);
        Assert.Contains("UpdatePromptFocus", codeBehind);
        Assert.Contains("RestoreFocus", codeBehind);
    }

    [Fact]
    public void CodeBehind_SavesAndRestoresFocusForActivity()
    {
        var codeBehind = ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml.cs");
        Assert.Contains("_focusBeforeActivity", codeBehind);
        Assert.Contains("UpdateActivityFocus", codeBehind);
        Assert.Contains("RestoreFocus", codeBehind);
    }

    [Fact]
    public void KeyboardContract_SchemaIsValid()
    {
        var path = Path.Combine(RepoRoot, "schemas", "keyboard-focus-contract.json");
        Assert.True(File.Exists(path), "Keyboard focus contract schema not found.");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(doc.RootElement.TryGetProperty("overlays", out var overlays));

        var overlayNames = overlays.EnumerateArray()
            .Select(o => o.GetProperty("name").GetString()!)
            .ToHashSet();

        Assert.Contains("PromptOverlay", overlayNames);
        Assert.Contains("ActivityOverlay", overlayNames);
    }

    [Fact]
    public void KeyboardContract_PagesAreDocumented()
    {
        var path = Path.Combine(RepoRoot, "schemas", "keyboard-focus-contract.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var pages = doc.RootElement.GetProperty("pages").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString()!)
            .ToHashSet();

        Assert.Contains("Recommended", pages);
        Assert.Contains("Custom", pages);
        Assert.Contains("Maintenance", pages);
    }

    private static string ReadXaml() =>
        ReadFile("src", "LibreSpot.Desktop", "MainWindow.xaml");

    private static string ExtractSection(string text, string markerName, int charWindow)
    {
        var idx = text.IndexOf($"\"{markerName}\"", StringComparison.Ordinal);
        if (idx < 0) idx = text.IndexOf($"'{markerName}'", StringComparison.Ordinal);
        Assert.True(idx >= 0, $"Marker '{markerName}' not found in XAML.");
        var start = Math.Max(0, idx - 200);
        var end = Math.Min(text.Length, idx + charWindow);
        return text[start..end];
    }

    private static string ReadFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot }.Concat(relativeParts).ToArray()));

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }
}
