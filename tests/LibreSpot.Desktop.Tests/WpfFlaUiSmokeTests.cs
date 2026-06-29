using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;

namespace LibreSpot.Desktop.Tests;

[Collection(WpfUiAutomationCollection.Name)]
public sealed class WpfFlaUiSmokeTests
{
    [Fact]
    public void FlaUiModeNavigation_SwitchesBetweenAllWorkspaces()
    {
        WithSmokeWindow("recommended", window =>
        {
            InvokeOrClick(WaitForControl(window, "Custom", ControlType.TabItem));
            Assert.NotNull(WaitForControl(window, "Custom settings editor", ControlType.Pane));

            InvokeOrClick(WaitForControl(window, "Maintenance", ControlType.TabItem));
            Assert.NotNull(WaitForControl(window, "Maintenance workspace", ControlType.Pane));

            InvokeOrClick(WaitForControl(window, "Recommended", ControlType.TabItem));
            Assert.NotNull(WaitForControl(window, "Recommended workspace", ControlType.Pane));
        });
    }

    [Fact]
    public void FlaUiCustomSearch_AcceptsInputAndClearButtonResetsIt()
    {
        WithSmokeWindow("custom", window =>
        {
            var search = WaitForControl(window, "Find a setting", ControlType.Edit).AsTextBox();
            search.Patterns.Value.Pattern.SetValue("lyrics");

            WaitUntil(() => string.Equals(search.Text, "lyrics", StringComparison.Ordinal), "settings search input to update");
            InvokeOrClick(WaitForControl(window, "Clear settings search", ControlType.Button));
            WaitUntil(() => string.IsNullOrWhiteSpace(search.Text), "settings search input to clear");
        });
    }

    [Fact]
    public void FlaUiMaintenanceAction_ClickOpensActivityOverlay()
    {
        WithSmokeWindow("maintenance", window =>
        {
            InvokeOrClick(WaitForControl(window, "Check matrix", ControlType.Button));
            Assert.NotNull(WaitForElement(window, "Check compatibility matrix"));
            InvokeOrClick(WaitForLastControl(window, "Check matrix", ControlType.Button));

            Assert.NotNull(WaitForControl(window, "Close activity panel", ControlType.Button));
        });
    }

    [Fact]
    public void FlaUiActivityOverlay_CloseButtonDismissesCompletedRunPanel()
    {
        WithSmokeWindow("activity", window =>
        {
            Assert.NotNull(WaitForElement(window, "Close activity panel"));
            InvokeOrClick(WaitForElement(window, "Close activity panel"));

            WaitUntilMissing(window, "Close activity panel");
        });
    }

    [Theory]
    [InlineData("Cancel smoke action")]
    [InlineData("Confirm smoke action")]
    public void FlaUiPromptOverlay_ConfirmAndCancelDismissThePrompt(string buttonName)
    {
        WithSmokeWindow("prompt", window =>
        {
            Assert.NotNull(WaitForElement(window, "UI automation prompt"));
            InvokeOrClick(WaitForControl(window, buttonName, ControlType.Button));

            WaitUntilMissing(window, "UI automation prompt");
        });
    }

    private static void WithSmokeWindow(string state, Action<Window> action)
    {
        using var app = LaunchSmokeState(state);
        using var automation = new UIA3Automation();
        var window = WaitForMainWindow(app.Application, automation, TimeSpan.FromSeconds(20));

        action(window);
    }

    private static SmokeApp LaunchSmokeState(string state)
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

    private static AutomationElement WaitForElement(AutomationElement root, string name, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = FindByName(root, name);
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for FlaUI element '{name}'. Snapshot: {BuildSnapshot(root)}");
    }

    private static AutomationElement WaitForControl(AutomationElement root, string name, ControlType controlType, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = FindByNameAndType(root, name, controlType);
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for FlaUI {controlType} element '{name}'. Snapshot: {BuildSnapshot(root)}");
    }

    private static AutomationElement WaitForLastControl(AutomationElement root, string name, ControlType controlType, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var element = FindAllByNameAndType(root, name, controlType).LastOrDefault();
            if (element is not null)
            {
                return element;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for final FlaUI {controlType} element '{name}'. Snapshot: {BuildSnapshot(root)}");
    }

    private static void WaitUntilMissing(AutomationElement root, string name, int timeoutSeconds = 5)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (FindByName(root, name) is null)
            {
                return;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"FlaUI element '{name}' remained visible.");
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

    private static AutomationElement? FindByName(AutomationElement root, string name) =>
        root.FindFirstDescendant(cf => cf.ByName(name));

    private static AutomationElement? FindByNameAndType(AutomationElement root, string name, ControlType controlType) =>
        FindAllByNameAndType(root, name, controlType).FirstOrDefault();

    private static IEnumerable<AutomationElement> FindAllByNameAndType(AutomationElement root, string name, ControlType controlType) =>
        root.FindAllDescendants()
            .Where(element =>
                string.Equals(element.Name, name, StringComparison.Ordinal)
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
                    .Select(element => $"{element.ControlType}:{element.Name}")
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Take(80));
        }
        catch
        {
            return "<snapshot unavailable>";
        }
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
