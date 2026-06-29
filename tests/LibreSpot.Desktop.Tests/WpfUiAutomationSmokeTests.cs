using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using LibreSpot.Desktop.Controls;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class WpfUiAutomationSmokeTests
{
    private static readonly HashSet<ControlType> ActionableTypes = new()
    {
        ControlType.Button,
        ControlType.CheckBox,
        ControlType.ComboBox,
        ControlType.Edit,
        ControlType.Hyperlink,
        ControlType.MenuItem,
        ControlType.RadioButton,
        ControlType.Slider,
        ControlType.TabItem
    };

    [Theory]
    [InlineData("recommended", "Recommended setup")]
    [InlineData("custom", "Custom settings")]
    [InlineData("maintenance", "Maintenance")]
    [InlineData("prompt", "UI automation prompt")]
    [InlineData("activity", "UI automation activity")]
    [InlineData("activity-undo", "Reversible changes")]
    public void WpfShell_UiaSmokeStatesExposeNamedActionableControls(string state, string expectedName)
    {
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState(state);
            try
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                var snapshot = WaitForSnapshotContaining(window, expectedName, TimeSpan.FromSeconds(10));

                Assert.Contains(snapshot, node => string.Equals(node.Name, expectedName, StringComparison.Ordinal));

                var unnamedActionable = snapshot
                    .Where(node => node.IsEnabled &&
                                   ActionableTypes.Contains(node.ControlType) &&
                                   string.IsNullOrWhiteSpace(node.Name))
                    .ToArray();

                Assert.True(
                    unnamedActionable.Length == 0,
                    "Enabled actionable controls must have UIA names: " +
                    string.Join(", ", unnamedActionable.Select(node => node.DebugLabel)));
            }
            finally
            {
                app.Dispose();
            }
        });
    }

    [Theory]
    [InlineData("prompt", "Cancel smoke action", "Confirm smoke action")]
    [InlineData("prompt-destructive", "Cancel destructive smoke action", "Confirm destructive smoke action")]
    [InlineData("activity", "Open LibreSpot folder", "Close activity panel")]
    [InlineData("activity-undo", "Open LibreSpot folder", "Close activity panel")]
    public void WpfShell_UiaOverlaysKeepFocusableActionBoundaries(string state, string firstAction, string secondAction)
    {
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState(state);
            try
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                WaitForSnapshotContaining(window, firstAction, TimeSpan.FromSeconds(10));

                var first = FindByName(window, firstAction)
                    ?? throw new InvalidOperationException($"Could not find UIA element '{firstAction}'.");
                var second = FindByName(window, secondAction)
                    ?? throw new InvalidOperationException($"Could not find UIA element '{secondAction}'.");

                Assert.True(IsKeyboardFocusable(first), $"{firstAction} must be keyboard focusable.");
                Assert.True(IsKeyboardFocusable(second), $"{secondAction} must be keyboard focusable.");
            }
            finally
            {
                app.Dispose();
            }
        });
    }

    [Fact]
    public void WpfShell_UiaActivityStateExposesRunStatusName()
    {
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState("activity");
            try
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                WaitForSnapshotContaining(window, "Run status", TimeSpan.FromSeconds(10));

                var runStatus = FindByName(window, "Run status")
                    ?? throw new InvalidOperationException("Could not find the run-status live region.");

                Assert.True(runStatus.Current.IsEnabled, "The run-status element must be present and enabled for assistive technology.");
                Assert.Equal("LiveRegionContentControl", runStatus.Current.ClassName);
                Assert.Equal(ControlType.Text, runStatus.Current.ControlType);
            }
            finally
            {
                app.Dispose();
            }
        });
    }

    [Fact]
    public void WpfShell_UiaCustomStateExposesThemeGallery()
    {
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState("custom");
            try
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                var snapshot = WaitForSnapshotContaining(window, "Search themes and schemes", TimeSpan.FromSeconds(10));

                Assert.Contains(snapshot, node => string.Equals(node.Name, "Local profiles", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Refresh local profiles", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Minimal / Marketplace-only Template profile", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Profile operation status", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Set selected profile active", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Theme pack", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => node.Name.Contains("Marketplace only", StringComparison.Ordinal));
            }
            finally
            {
                app.Dispose();
            }
        });
    }

    [Fact]
    public void WpfShell_UiaActivityUndoStateExposesRollbackHint()
    {
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState("activity-undo");
            try
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                var snapshot = WaitForSnapshotContaining(window, "Unregister the scheduled task to undo.", TimeSpan.FromSeconds(10));

                Assert.Contains(snapshot, node => string.Equals(node.Name, "Reversible changes", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Unregister the scheduled task to undo.", StringComparison.Ordinal));
            }
            finally
            {
                app.Dispose();
            }
        });
    }

    [Fact]
    public void LiveRegionContentControl_AutomationPeerReportsPolite()
    {
        RunOnSta(() =>
        {
            var control = new LiveRegionContentControl { Content = "Run complete" };
            var peer = UIElementAutomationPeer.CreatePeerForElement(control)
                ?? throw new InvalidOperationException("Could not create the live-region automation peer.");

            Assert.Equal(AutomationLiveSetting.Polite, peer.GetLiveSetting());
        });
    }

    private static SmokeApp LaunchSmokeState(string state)
    {
        var appPath = Path.Combine(AppContext.BaseDirectory, "LibreSpot.exe");
        Assert.True(File.Exists(appPath), $"Expected WPF executable at {appPath}.");

        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.UIA.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var startInfo = new ProcessStartInfo
        {
            FileName = appPath,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add($"--uia-smoke={state}");
        startInfo.Environment["LIBRESPOT_UIA_ROOT"] = root;

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start LibreSpot UIA smoke process.");
        return new SmokeApp(process, root);
    }

    private static AutomationElement WaitForMainWindow(Process process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException($"LibreSpot exited before exposing a main window. Exit code: {process.ExitCode}.");
            }

            try { process.WaitForInputIdle(250); } catch { }
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return AutomationElement.FromHandle(process.MainWindowHandle);
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException("Timed out waiting for LibreSpot main window.");
    }

    private static IReadOnlyList<UiaNode> WaitForSnapshotContaining(AutomationElement window, string expectedName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        IReadOnlyList<UiaNode> snapshot = Array.Empty<UiaNode>();
        while (DateTime.UtcNow < deadline)
        {
            snapshot = Snapshot(window);
            if (snapshot.Any(node => string.Equals(node.Name, expectedName, StringComparison.Ordinal)))
            {
                return snapshot;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for UIA element '{expectedName}'. Snapshot: " +
            string.Join(" | ", snapshot.Take(80).Select(node => node.DebugLabel)));
    }

    private static IReadOnlyList<UiaNode> Snapshot(AutomationElement root)
    {
        var nodes = new List<UiaNode>();
        Walk(root, nodes);
        return nodes;
    }

    private static void Walk(AutomationElement element, ICollection<UiaNode> nodes)
    {
        nodes.Add(UiaNode.From(element));

        var child = TreeWalker.ControlViewWalker.GetFirstChild(element);
        while (child is not null)
        {
            Walk(child, nodes);
            child = TreeWalker.ControlViewWalker.GetNextSibling(child);
        }
    }

    private static AutomationElement? FindByName(AutomationElement root, string name) =>
        root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, name));

    private static bool IsKeyboardFocusable(AutomationElement element) =>
        TryGet(element, AutomationElement.IsKeyboardFocusableProperty, false);

    private static AutomationLiveSetting GetLiveSetting(AutomationElement element) =>
        TryGet(element, AutomationElementIdentifiers.LiveSettingProperty, AutomationLiveSetting.Off);

    private static T TryGet<T>(AutomationElement element, AutomationProperty property, T fallback)
    {
        try
        {
            var value = element.GetCurrentPropertyValue(property, ignoreDefaultValue: true);
            return value is T typed ? typed : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed record UiaNode(
        string Name,
        ControlType ControlType,
        bool IsEnabled,
        bool IsKeyboardFocusable,
        string AutomationId)
    {
        public string DebugLabel =>
            $"{ControlType.ProgrammaticName}:{Name}:{AutomationId}:enabled={IsEnabled}:focusable={IsKeyboardFocusable}";

        public static UiaNode From(AutomationElement element) =>
            new(
                TryGet(element, AutomationElement.NameProperty, string.Empty),
                TryGet(element, AutomationElement.ControlTypeProperty, ControlType.Custom),
                TryGet(element, AutomationElement.IsEnabledProperty, false),
                TryGet(element, AutomationElement.IsKeyboardFocusableProperty, false),
                TryGet(element, AutomationElement.AutomationIdProperty, string.Empty));
    }

    private sealed class SmokeApp : IDisposable
    {
        public SmokeApp(Process process, string root)
        {
            Process = process;
            Root = root;
        }

        public Process Process { get; }
        private string Root { get; }

        public void Dispose()
        {
            try
            {
                if (!Process.HasExited)
                {
                    Process.CloseMainWindow();
                    if (!Process.WaitForExit(3000))
                    {
                        Process.Kill(entireProcessTree: true);
                    }
                }
            }
            catch
            {
                try { Process.Kill(entireProcessTree: true); } catch { }
            }
            finally
            {
                Process.Dispose();
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
