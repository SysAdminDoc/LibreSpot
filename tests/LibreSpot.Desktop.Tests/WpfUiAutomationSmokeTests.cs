using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using LibreSpot.Desktop.Controls;
using LibreSpot.Desktop.Properties;
using Xunit;

namespace LibreSpot.Desktop.Tests;

[Collection(WpfUiAutomationCollection.Name)]
public sealed class WpfUiAutomationSmokeTests
{
    private static readonly TimeSpan SmokeReadyTimeout = TimeSpan.FromSeconds(30);

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

    [Theory]
    [InlineData("recommended", "Recommended setup")]
    [InlineData("custom", "Custom settings")]
    [InlineData("maintenance", "Maintenance")]
    [InlineData("prompt", "Decision prompt")]
    [InlineData("activity", "Run activity dialog")]
    [InlineData("activity-error", "Run activity dialog")]
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
                AssertNoUnnamedActionableControls(snapshot);
            }
            finally
            {
                app.Dispose();
            }
        });
    }

    [Theory]
    [InlineData("prompt", "PromptCancelButton", "PromptConfirmButton")]
    [InlineData("activity", "ActivityOpenLibreSpotFolderButton", "ActivityCloseButton")]
    [InlineData("activity-error", "ActivityExportFailureBundleButton", "ActivityCloseButton")]
    [InlineData("activity-undo", "ActivityOpenLibreSpotFolderButton", "ActivityCloseButton")]
    public void WpfShell_UiaOverlaysKeepFocusableActionBoundaries(string state, string firstActionId, string secondActionId)
    {
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState(state);
            try
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                var snapshot = WaitForSnapshotContainingAutomationId(window, firstActionId, SmokeReadyTimeout);

                var first = FindSnapshotNode(snapshot, firstActionId);
                var second = FindSnapshotNode(snapshot, secondActionId);

                Assert.True(first.IsKeyboardFocusable, $"{firstActionId} must be keyboard focusable.");
                Assert.True(second.IsKeyboardFocusable, $"{secondActionId} must be keyboard focusable.");
            }
            finally
            {
                app.Dispose();
            }
        });
    }

    [Fact]
    public void WpfShell_UiaFailureBundleActionOnlyAppearsOnFailedActivity()
    {
        RunOnSta(() =>
        {
            using (var app = LaunchSmokeState("activity"))
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                var snapshot = WaitForSnapshotContainingAutomationId(window, "ActivityCloseButton", SmokeReadyTimeout);

                Assert.DoesNotContain(snapshot, node => string.Equals(node.AutomationId, "ActivityExportFailureBundleButton", StringComparison.Ordinal));
            }

            using (var app = LaunchSmokeState("activity-error"))
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                var snapshot = WaitForSnapshotContainingAutomationId(window, "ActivityExportFailureBundleButton", SmokeReadyTimeout);

                var export = FindSnapshotNode(snapshot, "ActivityExportFailureBundleButton");

                Assert.True(export.IsKeyboardFocusable, "Failure bundle export must be keyboard focusable.");
                Assert.Equal(Strings.ButtonExportFailureBundleName, export.Name);
            }
        });
    }

    [Theory]
    [MemberData(nameof(SupportedCultureData))]
    public void WpfShell_UiaSupportedCulturesExposeLocalizedFocusTargetsAndSafeBounds(string culture)
    {
        var text = LocalizedSmokeText.For(culture);
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState("activity", culture);
            try
            {
                var window = WaitForMainWindow(app.Process, TimeSpan.FromSeconds(20));
                var snapshot = WaitForSnapshotContainingAutomationId(window, "ActivityCloseButton", SmokeReadyTimeout);
                var windowBounds = UiaNode.From(window).BoundingRectangle;

                AssertLocalizedNode(snapshot, "RunStatus", text.RunStatus, windowBounds);
                AssertLocalizedNode(snapshot, "ActivityOpenLibreSpotFolderButton", text.OpenLibreSpotFolder, windowBounds, requireFocusable: true);
                AssertLocalizedNode(snapshot, "ActivityCloseButton", text.CloseActivityPanel, windowBounds, requireFocusable: true);
                AssertNoUnnamedActionableControls(snapshot);
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
                var snapshot = WaitForSnapshotContainingAutomationId(window, "RunStatus", SmokeReadyTimeout);

                var runStatus = FindSnapshotNode(snapshot, "RunStatus");

                Assert.True(runStatus.IsEnabled, "The run-status element must be present and enabled for assistive technology.");
                Assert.Equal("LiveRegionContentControl", runStatus.ClassName);
                Assert.Equal(ControlType.Text, runStatus.ControlType);
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
                var snapshot = WaitForSnapshotContaining(window, "Search themes and schemes", SmokeReadyTimeout);

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
                var snapshot = WaitForSnapshotContaining(window, "Unregister the scheduled task to undo.", SmokeReadyTimeout);

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

    private static SmokeApp LaunchSmokeState(string state, string culture = "en")
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
        startInfo.ArgumentList.Add($"--uia-culture={culture}");
        startInfo.ArgumentList.Add("--uia-background");
        startInfo.Environment["LIBRESPOT_UIA_ROOT"] = root;

        var gate = WpfUiAutomationCollection.EnterExclusive();
        try
        {
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start LibreSpot UIA smoke process.");
            return new SmokeApp(process, root, gate);
        }
        catch
        {
            gate.Dispose();
            throw;
        }
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
            var handle = process.MainWindowHandle;
            if (handle != IntPtr.Zero)
            {
                return AutomationElement.FromHandle(handle);
            }

            var window = FindTopLevelWindowByProcessId(process.Id);
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException("Timed out waiting for LibreSpot main window.");
    }

    private static AutomationElement? FindTopLevelWindowByProcessId(int processId)
    {
        var windows = AutomationElement.RootElement.FindAll(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ProcessIdProperty, processId));

        return windows
            .OfType<AutomationElement>()
            .FirstOrDefault();
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

    private static IReadOnlyList<UiaNode> WaitForSnapshotContainingAutomationId(
        AutomationElement window,
        string automationId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        IReadOnlyList<UiaNode> snapshot = Array.Empty<UiaNode>();
        while (DateTime.UtcNow < deadline)
        {
            snapshot = Snapshot(window);
            if (snapshot.Any(node => string.Equals(node.AutomationId, automationId, StringComparison.Ordinal)))
            {
                return snapshot;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException(
            $"Timed out waiting for UIA automation id '{automationId}'. Snapshot: " +
            string.Join(" | ", snapshot.Take(80).Select(node => node.DebugLabel)));
    }

    private static IReadOnlyList<UiaNode> Snapshot(AutomationElement root)
    {
        var nodes = new List<UiaNode>();
        Walk(root, nodes, TreeWalker.ControlViewWalker);
        if (nodes.Count > 1)
        {
            return nodes;
        }

        var processNodes = SnapshotByProcessId(root);
        if (processNodes.Count > nodes.Count)
        {
            return processNodes;
        }

        var rawNodes = new List<UiaNode>();
        Walk(root, rawNodes, TreeWalker.RawViewWalker);
        return rawNodes.Count > nodes.Count ? rawNodes : nodes;
    }

    private static IReadOnlyList<UiaNode> SnapshotByProcessId(AutomationElement root)
    {
        var processId = TryGet(root, AutomationElement.ProcessIdProperty, 0);
        if (processId <= 0)
        {
            return Array.Empty<UiaNode>();
        }

        try
        {
            return AutomationElement.RootElement
                .FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, processId))
                .OfType<AutomationElement>()
                .Select(UiaNode.From)
                .ToArray();
        }
        catch
        {
            return Array.Empty<UiaNode>();
        }
    }

    private static void Walk(AutomationElement element, ICollection<UiaNode> nodes, TreeWalker walker)
    {
        nodes.Add(UiaNode.From(element));

        var child = walker.GetFirstChild(element);
        while (child is not null)
        {
            Walk(child, nodes, walker);
            child = walker.GetNextSibling(child);
        }
    }

    private static UiaNode FindSnapshotNode(IReadOnlyList<UiaNode> snapshot, string automationId) =>
        snapshot.FirstOrDefault(node => string.Equals(node.AutomationId, automationId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Could not find UIA node '{automationId}'.");

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

    private static void AssertLocalizedNode(
        IReadOnlyList<UiaNode> snapshot,
        string automationId,
        string expectedName,
        Rect windowBounds,
        bool requireFocusable = false)
    {
        var node = snapshot.FirstOrDefault(item => string.Equals(item.AutomationId, automationId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Could not find UIA node '{automationId}'.");

        Assert.Equal(expectedName, node.Name);
        Assert.True(node.BoundingRectangle.Width > 1 && node.BoundingRectangle.Height > 1, $"{automationId} has empty bounds.");
        Assert.True(
            node.BoundingRectangle.Left >= windowBounds.Left - 2 &&
            node.BoundingRectangle.Top >= windowBounds.Top - 2 &&
            node.BoundingRectangle.Right <= windowBounds.Right + 2 &&
            node.BoundingRectangle.Bottom <= windowBounds.Bottom + 2,
            $"{automationId} is clipped outside the window. Element={node.BoundingRectangle}; window={windowBounds}.");

        if (requireFocusable)
        {
            Assert.True(node.IsKeyboardFocusable, $"{automationId} must be keyboard focusable.");
        }
    }

    private static void AssertNoUnnamedActionableControls(IReadOnlyList<UiaNode> snapshot)
    {
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

    private sealed record LocalizedSmokeText(
        string ActivityDialog,
        string RunStatus,
        string OpenLibreSpotFolder,
        string CloseActivityPanel)
    {
        public static LocalizedSmokeText For(string culture)
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return new LocalizedSmokeText(
                Get("ActivityDialogName", info),
                Get("RunStatus", info),
                Get("ButtonOpenLibreSpotFolder", info),
                Get("Ui_CloseActivityPanel", info));
        }

        private static string Get(string key, CultureInfo culture) =>
            Strings.ResourceManager.GetString(key, culture) ?? key;
    }

    private sealed record UiaNode(
        string Name,
        ControlType ControlType,
        bool IsEnabled,
        bool IsKeyboardFocusable,
        string AutomationId,
        string ClassName,
        Rect BoundingRectangle)
    {
        public string DebugLabel =>
            $"{ControlType.ProgrammaticName}:{Name}:{AutomationId}:class={ClassName}:enabled={IsEnabled}:focusable={IsKeyboardFocusable}:bounds={BoundingRectangle}";

        public static UiaNode From(AutomationElement element) =>
            new(
                TryGet(element, AutomationElement.NameProperty, string.Empty),
                TryGet(element, AutomationElement.ControlTypeProperty, ControlType.Custom),
                TryGet(element, AutomationElement.IsEnabledProperty, false),
                TryGet(element, AutomationElement.IsKeyboardFocusableProperty, false),
                TryGet(element, AutomationElement.AutomationIdProperty, string.Empty),
                TryGet(element, AutomationElement.ClassNameProperty, string.Empty),
                TryGet(element, AutomationElement.BoundingRectangleProperty, Rect.Empty));
    }

    private sealed class SmokeApp : IDisposable
    {
        public SmokeApp(Process process, string root, IDisposable gate)
        {
            Process = process;
            Root = root;
            Gate = gate;
        }

        public Process Process { get; }
        private string Root { get; }
        private IDisposable Gate { get; }
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
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
                try { Process.Dispose(); } finally { Gate.Dispose(); }
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
