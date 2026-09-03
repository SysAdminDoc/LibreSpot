using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using Axe.Windows.Automation;
using LibreSpot.Desktop.Controls;
using LibreSpot.Desktop.Properties;
using Xunit;

namespace LibreSpot.Desktop.Tests;

[Collection(WpfUiAutomationCollection.Name)]
public sealed class WpfUiAutomationSmokeTests
{
    private static readonly TimeSpan MainWindowTimeout = TimeSpan.FromSeconds(45);
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
    [InlineData("recommended", "Home")]
    [InlineData("custom", "Settings")]
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
                var window = WaitForMainWindow(app.Process, MainWindowTimeout);
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
    [InlineData("activity-running", "ActivityOpenLibreSpotFolderButton", "ActivityCancelRunButton")]
    [InlineData("activity-error", "ActivityExportFailureBundleButton", "ActivityCloseButton")]
    [InlineData("activity-undo", "ActivityOpenLibreSpotFolderButton", "ActivityCloseButton")]
    public void WpfShell_UiaOverlaysKeepFocusableActionBoundaries(string state, string firstActionId, string secondActionId)
    {
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState(state);
            try
            {
                var window = WaitForMainWindow(app.Process, MainWindowTimeout);
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
                var window = WaitForMainWindow(app.Process, MainWindowTimeout);
                var snapshot = WaitForSnapshotContainingAutomationId(window, "ActivityCloseButton", SmokeReadyTimeout);

                Assert.DoesNotContain(snapshot, node => string.Equals(node.AutomationId, "ActivityExportFailureBundleButton", StringComparison.Ordinal));
            }

            using (var app = LaunchSmokeState("activity-error"))
            {
                var window = WaitForMainWindow(app.Process, MainWindowTimeout);
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
                var window = WaitForMainWindow(app.Process, MainWindowTimeout);
                var snapshot = WaitForSnapshotContainingAutomationId(window, "ActivityCloseButton", SmokeReadyTimeout);
                var windowBounds = UiaNode.From(window).BoundingRectangle;

                AssertLocalizedNode(snapshot, "RunStatus", text.RunStatusAnnouncement, windowBounds);
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
                var window = WaitForMainWindow(app.Process, MainWindowTimeout);
                var snapshot = WaitForSnapshotContainingAutomationId(window, "RunStatus", SmokeReadyTimeout);

                var runStatus = FindSnapshotNode(snapshot, "RunStatus");

                Assert.True(runStatus.IsEnabled, "The run-status element must be present and enabled for assistive technology.");
                Assert.Equal("LiveRegionContentControl", runStatus.ClassName);
                Assert.Equal(ControlType.Text, runStatus.ControlType);
                Assert.Contains("Run complete", runStatus.Name, StringComparison.Ordinal);
                Assert.Contains("Spotify is ready", runStatus.Name, StringComparison.Ordinal);
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
        // Profile tools are one click away behind a closed group in the default
        // Settings view; this state opens that group so its controls are present.
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState("custom-profiles");
            try
            {
                    var window = WaitForMainWindow(app.Process, MainWindowTimeout);
                var snapshot = WaitForSnapshotContaining(window, "Search themes and schemes", SmokeReadyTimeout);

                Assert.Contains(snapshot, node => string.Equals(node.Name, "Local profiles", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Refresh local profiles", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Minimal / Marketplace-only Template profile", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => node.Name.Contains("profile choice", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(snapshot, node => string.Equals(node.Name, "Profile operation status", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Set selected profile active", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Theme pack", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => node.Name.Contains("Marketplace only", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Profile tools", StringComparison.Ordinal));
                Assert.Contains(snapshot, node => string.Equals(node.Name, "Apply custom profile", StringComparison.Ordinal));
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
                var window = WaitForMainWindow(app.Process, MainWindowTimeout);
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
    public void WpfShell_UiaActivityUndoStateKeepsRunLogReadable()
    {
        RunOnSta(() =>
        {
            using var app = LaunchSmokeState("activity-undo");
            try
            {
                var window = WaitForMainWindow(app.Process, MainWindowTimeout);
                var snapshot = WaitForSnapshotContainingAutomationId(window, "ActivityRunLogList", SmokeReadyTimeout);
                var runLog = FindSnapshotNode(snapshot, "ActivityRunLogList");

                Assert.True(runLog.BoundingRectangle.Width >= 320, $"Activity run log is too narrow: {runLog.BoundingRectangle}.");
                Assert.True(runLog.BoundingRectangle.Height >= 120, $"Activity run log is vertically collapsed: {runLog.BoundingRectangle}.");
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
            Assert.Equal("Run complete", peer.GetName());

            control.Content = "Retry environment check";
            Assert.Equal("Retry environment check", peer.GetName());
        });
    }

    [Theory]
    [InlineData("recommended", "Home")]
    [InlineData("custom", "Settings")]
    [InlineData("maintenance", "Maintenance")]
    public void AxeWindowsScan_FindsNoViolationOutsideTheRecordedBaseline(string state, string expectedName)
    {
        using var app = LaunchSmokeState(state);
        var window = WaitForMainWindow(app.Process, MainWindowTimeout);

        WaitForSnapshotContaining(window, expectedName, SmokeReadyTimeout);

        var baseline = LoadAxeBaseline(state);
        var scan = ScanUntilSettled(app.Process, SmokeReadyTimeout);

        // Without this, a scan that returns nothing at all satisfies every
        // assertion below and the state goes quietly green forever.
        Assert.True(
            scan.WindowsScanned > 0,
            "Axe.Windows scanned no windows, so this state proved nothing. The shell was probably gone or never charted.");

        var observed = scan.Violations
            .GroupBy(violation => violation.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        // Counting matters. Every one of these violations sits on generated chrome
        // with no automation id, so keying on the rule alone would let one recorded
        // entry absorb every future one of the same shape.
        var unexpected = new List<string>();
        foreach (var (key, violations) in observed)
        {
            if (!baseline.TryGetValue(key, out var allowed))
            {
                unexpected.Add(violations.Length + "x " + key + " (not in the baseline)");
                continue;
            }

            if (violations.Length > allowed)
            {
                unexpected.Add(violations.Length + "x " + key + " (baseline records " + allowed + ")");
            }
        }

        var report = string.Join(Environment.NewLine, unexpected.Select(line => "  " + line));
        var sample = string.Join(
            Environment.NewLine,
            observed.Values.SelectMany(group => group).Take(3).Select(violation => "  " + violation.Detail));

        Assert.True(
            unexpected.Count == 0,
            "Axe.Windows reported accessibility violations for the "
                + state
                + " state that schemas/axe-windows-baseline.json does not record:"
                + Environment.NewLine
                + report
                + Environment.NewLine
                + "First few elements seen:"
                + Environment.NewLine
                + sample);
    }

    [Fact]
    public void AxeWindowsScan_ReportsAPlantedButtonThatHasNoAccessibleName()
    {
        // Positive control. Without it the three baseline tests would pass just as
        // happily against a scan that returned nothing. It has to name the planted
        // button specifically: a scan that charts the wrong window, or one that has
        // quietly stopped charting, still returns a result set, so matching on the
        // rule alone would not say the plant is what was found.
        using var app = LaunchSmokeState("axe-positive-control");
        var window = WaitForMainWindow(app.Process, MainWindowTimeout);
        WaitForSnapshotContaining(window, "Home", SmokeReadyTimeout);

        var scan = ScanUntilSettled(app.Process, SmokeReadyTimeout);
        Assert.True(scan.WindowsScanned > 0, "Axe.Windows scanned no windows, so this control proved nothing.");
        var violations = scan.Violations;

        // Matched on the plant's own automation id. Matching on "some unnamed
        // button" would be satisfied by a real regression elsewhere in the shell,
        // and the control would quietly stop controlling anything.
        Assert.True(
            violations.Any(violation =>
                violation.AutomationId == MainWindow.UiAutomationPositiveControlAutomationId &&
                violation.RuleId.Contains("Name", StringComparison.OrdinalIgnoreCase)),
            "The scan did not report the unnamed button planted in the axe-positive-control state, so it is not "
                + "checking anything. Keys reported: "
                + string.Join(", ", violations.Select(violation => violation.Key).Distinct().OrderBy(key => key, StringComparer.Ordinal)));
    }

    private sealed record AxeScan(int WindowsScanned, IReadOnlyList<AxeViolation> Violations);

    /// <summary>
    /// Scans until two consecutive scans agree, or the timeout runs out.
    /// </summary>
    /// <remarks>
    /// Waiting for a named element is not enough. The shell keeps populating
    /// after its title is charted, so at full CPU the Maintenance state has been
    /// seen reporting six icon glyphs where a quiet machine reports two, and the
    /// Home list has been caught mid-virtualization with DataItems that have no
    /// bounding rectangle yet. A count-based baseline cannot be built on a
    /// moving tree, so the scan settles rather than the test guessing a delay.
    /// A tree that never settles falls through and is reported by the caller's
    /// assertion, which is the honest outcome for a UI that is still churning.
    /// </remarks>
    private static AxeScan ScanUntilSettled(Process process, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var previous = ScanForAccessibilityViolations(process);

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(400);
            var current = ScanForAccessibilityViolations(process);
            if (HaveSameShape(previous, current))
            {
                return current;
            }

            previous = current;
        }

        return previous;
    }

    private static bool HaveSameShape(AxeScan left, AxeScan right)
    {
        if (left.WindowsScanned != right.WindowsScanned || left.Violations.Count != right.Violations.Count)
        {
            return false;
        }

        var leftCounts = left.Violations.GroupBy(v => v.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var rightCounts = right.Violations.GroupBy(v => v.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return leftCounts.Count == rightCounts.Count
            && leftCounts.All(pair => rightCounts.TryGetValue(pair.Key, out var count) && count == pair.Value);
    }

    private static AxeScan ScanForAccessibilityViolations(Process process)
    {
        var config = Config.Builder
            .ForProcessId(process.Id)
            .WithOutputFileFormat(OutputFileFormat.None)
            .Build();

        var output = ScannerFactory.CreateScanner(config).Scan(null);

        // The window count travels with the result so callers can tell "this
        // window is clean" from "nothing was looked at".
        return new AxeScan(
            output.WindowScanOutputs.Count,
            output.WindowScanOutputs
                .SelectMany(window => window.Errors)
                .Select(AxeViolation.FromScanResult)
                .OrderBy(violation => violation.Key, StringComparer.Ordinal)
                .ToArray());
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

    private static Dictionary<string, int> LoadAxeBaseline(string state)
    {
        var path = Path.Combine(ResolveRepoRoot(), "schemas", "axe-windows-baseline.json");
        Assert.True(File.Exists(path), $"Axe.Windows baseline was not found at {path}.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var states = document.RootElement.GetProperty("states");
        Assert.True(
            states.TryGetProperty(state, out var entries),
            $"schemas/axe-windows-baseline.json records no baseline for the {state} state.");

        return entries.EnumerateArray().ToDictionary(
            entry => entry.GetProperty("key").GetString() ?? string.Empty,
            entry => entry.GetProperty("count").GetInt32(),
            StringComparer.Ordinal);
    }

    private sealed record AxeViolation(string RuleId, string ControlType, string AutomationId, string Key, string Detail)
    {
        public static AxeViolation FromScanResult(ScanResult result)
        {
            var ruleId = result.Rule?.ID.ToString() ?? "UnknownRule";
            var properties = result.Element?.Properties ?? new Dictionary<string, string>();

            var automationId = Lookup(properties, "AutomationId");
            var controlType = Lookup(properties, "ControlType", "LocalizedControlType");

            var detail = string.Join(
                ", ",
                properties
                    .Where(pair => pair.Key is "ClassName" or "ControlType" or "Name" or "AutomationId" or "BoundingRectangle")
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "=" + pair.Value));

            return new AxeViolation(
                ruleId,
                controlType,
                automationId,
                ruleId + "|" + controlType + "|" + automationId,
                detail);
        }

        private static string Lookup(IReadOnlyDictionary<string, string> properties, params string[] names)
        {
            foreach (var name in names)
            {
                if (properties.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return "(none)";
        }
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
        string RunStatusAnnouncement,
        string OpenLibreSpotFolder,
        string CloseActivityPanel)
    {
        public static LocalizedSmokeText For(string culture)
        {
            var info = CultureInfo.GetCultureInfo(culture);
            return new LocalizedSmokeText(
                Get("ActivityDialogName", info),
                $"{Get("RunComplete", info)}. {Get("ProgressSpotifyReady", info)}",
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
