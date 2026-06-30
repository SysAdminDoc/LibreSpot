using System.Diagnostics;
using System.Globalization;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using LibreSpot.Desktop.Properties;
using Xunit;

namespace LibreSpot.Desktop.Tests;

[Collection(WpfUiAutomationCollection.Name)]
public sealed class WpfFlaUiSmokeTests
{
    private static readonly string[] SupportedCultures = ["en", "ru", "zh-Hans", "pt-BR", "es"];

    public static TheoryData<string> SupportedCultureData()
    {
        var data = new TheoryData<string>();
        foreach (var culture in SupportedCultures)
        {
            data.Add(culture);
        }

        return data;
    }

    [Fact]
    public void FlaUiModeNavigation_SwitchesBetweenAllWorkspaces()
    {
        var text = LocalizedSmokeText.For("en");
        WithSmokeWindow("recommended", window =>
        {
            InvokeOrClick(AssertControl(window, "WorkspaceTabCustom", ControlType.TabItem, text.CustomTab));
            AssertNamedVisible(window, WaitForElementByAutomationId(window, "CustomSettingsEditor"), text.CustomSettingsEditor);

            InvokeOrClick(AssertControl(window, "WorkspaceTabMaintenance", ControlType.TabItem, text.MaintenanceTab));
            AssertNamedVisible(window, WaitForElementByAutomationId(window, "MaintenanceWorkspace"), text.MaintenanceWorkspace);

            InvokeOrClick(AssertControl(window, "WorkspaceTabRecommended", ControlType.TabItem, text.RecommendedTab));
            AssertNamedVisible(window, WaitForElementByAutomationId(window, "RecommendedWorkspace"), text.RecommendedWorkspace);
        });
    }

    [Fact]
    public void FlaUiCustomSearch_AcceptsInputAndClearButtonResetsIt()
    {
        var text = LocalizedSmokeText.For("en");
        WithSmokeWindow("custom", window =>
        {
            var search = AssertControl(window, "SettingsSearchBox", ControlType.Edit, text.SearchPlaceholder).AsTextBox();
            search.Patterns.Value.Pattern.SetValue("lyrics");

            WaitUntil(() => string.Equals(search.Text, "lyrics", StringComparison.Ordinal), "settings search input to update");
            InvokeOrClick(AssertControl(window, "SettingsSearchClearButton", ControlType.Button, text.ClearSearch));
            WaitUntil(() => string.IsNullOrWhiteSpace(search.Text), "settings search input to clear");
        });
    }

    [Fact]
    public void FlaUiMaintenanceAction_ClickOpensActivityOverlay()
    {
        var text = LocalizedSmokeText.For("en");
        WithSmokeWindow("maintenance", window =>
        {
            var action = WaitForControlByAutomationId(window, "MaintenanceAction_CheckUpdates", ControlType.Button);
            Assert.Equal(text.CheckMatrix, action.Name);
            InvokeOrClick(action);

            AssertNamedVisible(window, AssertControl(window, "PromptCancelButton", ControlType.Button, text.Cancel), text.Cancel);
            InvokeOrClick(AssertControl(window, "PromptConfirmButton", ControlType.Button, text.CheckMatrix));

            Assert.NotNull(AssertControl(window, "ActivityCloseButton", ControlType.Button, text.CloseActivityPanel));
        });
    }

    [Fact]
    public void FlaUiActivityOverlay_CloseButtonDismissesCompletedRunPanel()
    {
        var text = LocalizedSmokeText.For("en");
        WithSmokeWindow("activity", window =>
        {
            var close = AssertControl(window, "ActivityCloseButton", ControlType.Button, text.CloseActivityPanel);
            InvokeOrClick(close);

            WaitUntilMissingByAutomationId(window, "ActivityCloseButton");
        });
    }

    [Theory]
    [InlineData("PromptCancelButton")]
    [InlineData("PromptConfirmButton")]
    public void FlaUiPromptOverlay_ConfirmAndCancelDismissThePrompt(string buttonAutomationId)
    {
        var text = LocalizedSmokeText.For("en");
        WithSmokeWindow("prompt", window =>
        {
            AssertNamedVisible(window, AssertControl(window, "PromptCancelButton", ControlType.Button, text.Cancel), text.Cancel);
            InvokeOrClick(WaitForElementByAutomationId(window, buttonAutomationId));

            WaitUntilMissingByAutomationId(window, "PromptCancelButton");
        });
    }

    [Theory]
    [MemberData(nameof(SupportedCultureData))]
    public void FlaUiSupportedCultures_RenderRecommendedSurfaceWithLocalizedNamesAndBounds(string culture)
    {
        var text = LocalizedSmokeText.For(culture);
        WithSmokeWindow("recommended", culture, window =>
        {
            AssertControl(window, "WorkspaceTabRecommended", ControlType.TabItem, text.RecommendedTab);
            AssertControl(window, "WorkspaceTabCustom", ControlType.TabItem, text.CustomTab);
            AssertControl(window, "WorkspaceTabMaintenance", ControlType.TabItem, text.MaintenanceTab);
            AssertNamedVisible(window, WaitForElementByAutomationId(window, "RecommendedWorkspace"), text.RecommendedWorkspace);
            AssertControl(window, "RunRecommendedSetupButton", ControlType.Button, text.RunRecommendedSetup);
        });
    }

    private static void WithSmokeWindow(string state, Action<Window> action) =>
        WithSmokeWindow(state, "en", action);

    private static void WithSmokeWindow(string state, string culture, Action<Window> action)
    {
        using var app = LaunchSmokeState(state, culture);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app.Application, automation, TimeSpan.FromSeconds(20));

        action(window);
    }

    private static SmokeApp LaunchSmokeState(string state, string culture)
    {
        var appPath = Path.Combine(AppContext.BaseDirectory, "LibreSpot.exe");
        Assert.True(File.Exists(appPath), $"Expected WPF executable at {appPath}.");

        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.FlaUI.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var startInfo = new ProcessStartInfo
        {
            FileName = appPath,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add($"--uia-smoke={state}");
        startInfo.ArgumentList.Add($"--uia-culture={culture}");
        startInfo.Environment["LIBRESPOT_UIA_ROOT"] = root;

        return new SmokeApp(Application.Launch(startInfo), root);
    }

    private static Window WaitForMainWindow(Application application, UIA3Automation automation, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (application.HasExited)
            {
                throw new InvalidOperationException("LibreSpot exited before exposing a main window.");
            }

            var window = application.GetMainWindow(automation, TimeSpan.FromMilliseconds(250));
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException("Timed out waiting for LibreSpot main window.");
    }

    private static AutomationElement AssertControl(
        Window window,
        string automationId,
        ControlType controlType,
        string expectedName,
        int timeoutSeconds = 10)
    {
        var element = WaitForControlByAutomationId(window, automationId, controlType, timeoutSeconds);
        AssertNamedVisible(window, element, expectedName);
        return element;
    }

    private static void AssertNamedVisible(Window window, AutomationElement element, string expectedName)
    {
        if (element.Patterns.ScrollItem.IsSupported)
        {
            element.Patterns.ScrollItem.Pattern.ScrollIntoView();
            Thread.Sleep(100);
        }

        Assert.False(string.IsNullOrWhiteSpace(element.Name), $"Element '{element.AutomationId}' must expose a UIA name.");
        Assert.Equal(expectedName, element.Name);

        var bounds = element.BoundingRectangle;
        var windowBounds = window.BoundingRectangle;
        Assert.True(bounds.Width > 1 && bounds.Height > 1, $"Element '{element.AutomationId}' has empty bounds: {bounds}.");
        Assert.True(
            bounds.Left >= windowBounds.Left - 2 &&
            bounds.Top >= windowBounds.Top - 2 &&
            bounds.Right <= windowBounds.Right + 2 &&
            bounds.Bottom <= windowBounds.Bottom + 2,
            $"Element '{element.AutomationId}' is clipped outside the window. Element={bounds}; window={windowBounds}.");
    }

    private static AutomationElement WaitForElementByAutomationId(AutomationElement root, string automationId, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = FindByAutomationId(root, automationId);
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for FlaUI element '{automationId}'. Snapshot: {BuildSnapshot(root)}");
    }

    private static AutomationElement WaitForControlByAutomationId(
        AutomationElement root,
        string automationId,
        ControlType controlType,
        int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = FindByAutomationIdAndType(root, automationId, controlType);
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for FlaUI {controlType} element '{automationId}'. Snapshot: {BuildSnapshot(root)}");
    }

    private static void WaitUntilMissingByAutomationId(AutomationElement root, string automationId, int timeoutSeconds = 5)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (FindByAutomationId(root, automationId) is null)
            {
                return;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"FlaUI element '{automationId}' remained visible.");
    }

    private static void WaitUntil(Func<bool> condition, string description, int timeoutSeconds = 5)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
    }

    private static AutomationElement? FindByAutomationId(AutomationElement root, string automationId) =>
        root.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    private static AutomationElement? FindByAutomationIdAndType(AutomationElement root, string automationId, ControlType controlType) =>
        root.FindAllDescendants()
            .FirstOrDefault(element =>
                string.Equals(element.AutomationId, automationId, StringComparison.Ordinal)
                && element.ControlType == controlType);

    private static void InvokeOrClick(AutomationElement element)
    {
        if (element.Patterns.ScrollItem.IsSupported)
        {
            element.Patterns.ScrollItem.Pattern.ScrollIntoView();
        }

        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
            return;
        }

        if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
            return;
        }

        element.Click();
    }

    private static string BuildSnapshot(AutomationElement root)
    {
        try
        {
            return string.Join(
                " | ",
                root.FindAllDescendants()
                    .Select(element => $"{element.ControlType}:{element.Name}:{element.AutomationId}")
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Take(80));
        }
        catch
        {
            return "<snapshot unavailable>";
        }
    }

    private sealed record LocalizedSmokeText(
        string RecommendedTab,
        string CustomTab,
        string MaintenanceTab,
        string RecommendedWorkspace,
        string RunRecommendedSetup,
        string CustomWorkspace,
        string CustomSettingsEditor,
        string SearchPlaceholder,
        string ClearSearch,
        string ApplyCustomProfile,
        string MaintenanceWorkspace,
        string CheckMatrix,
        string DecisionPrompt,
        string Cancel,
        string ActivityDialog,
        string RunStatus,
        string CloseActivityPanel)
    {
        public static LocalizedSmokeText For(string culture)
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return new LocalizedSmokeText(
                Get("ModeRecommendedTitle", info),
                Get("ModeCustomTitle", info),
                Get("ModeMaintenanceTitle", info),
                Get("Ui_RecommendedWorkspace", info),
                Get("ButtonRunRecommendedSetup", info),
                Get("Ui_CustomWorkspace", info),
                Get("Ui_CustomSettingsEditor", info),
                Get("SearchPlaceholder", info),
                Get("ButtonClearSearchName", info),
                Get("ButtonApplyCustomProfile", info),
                Get("Ui_MaintenanceWorkspace", info),
                Get("Maintenance_CheckUpdates_ButtonText", info),
                Get("Ui_DecisionPrompt", info),
                Get("ButtonCancel", info),
                Get("ActivityDialogName", info),
                Get("RunStatus", info),
                Get("Ui_CloseActivityPanel", info));
        }

        private static string Get(string key, CultureInfo culture) =>
            Strings.ResourceManager.GetString(key, culture) ?? key;
    }

    private sealed class SmokeApp : IDisposable
    {
        public SmokeApp(Application application, string root)
        {
            Application = application;
            Root = root;
        }

        public Application Application { get; }
        private string Root { get; }

        public void Dispose()
        {
            try
            {
                if (!Application.HasExited)
                {
                    Application.Close();
                    Thread.Sleep(500);
                    if (!Application.HasExited)
                    {
                        Application.Kill();
                    }
                }
            }
            catch
            {
                try { Application.Kill(); } catch { }
            }
            finally
            {
                Application.Dispose();
                try
                {
                    if (Directory.Exists(Root))
                    {
                        Directory.Delete(Root, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
