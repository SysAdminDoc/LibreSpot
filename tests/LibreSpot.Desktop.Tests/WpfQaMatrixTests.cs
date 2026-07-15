using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using LibreSpot.Desktop.Properties;
using Xunit;
using Drawing = System.Drawing;

namespace LibreSpot.Desktop.Tests;

[Collection(WpfUiAutomationCollection.Name)]
[Trait("Category", "WpfQaMatrix")]
public sealed class WpfQaMatrixTests
{
    private static readonly TimeSpan MainWindowTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(45);
    private static readonly HashSet<ControlType> ActionableTypes =
    [
        ControlType.Button,
        ControlType.CheckBox,
        ControlType.ComboBox,
        ControlType.Edit,
        ControlType.Hyperlink,
        ControlType.MenuItem,
        ControlType.RadioButton,
        ControlType.Slider,
        ControlType.TabItem
    ];

    public static TheoryData<string, string, string, string, string> SurfaceMatrix()
    {
        var surfaces = new (string State, string ExpectedResource, string FocusTarget)[]
        {
            ("recommended", "ButtonRunRecommendedSetup", "RunRecommendedSetupButton"),
            ("custom", "ButtonApplyCustomProfile", "ApplyCustomProfileButton"),
            ("maintenance", "Maintenance_CheckUpdates_ButtonText", "MaintenanceAction_CheckUpdates"),
            ("activity-undo", "UndoPaneTitle", "ActivityCloseButton"),
            ("support-bundle", "SupportBundleTitle", "SupportBundleExportButton"),
            ("profile", "Ui_SetSelectedProfileActive", "ProfileSetActiveButton"),
            ("prompt", "ButtonContinue", "PromptConfirmButton"),
            ("activity-empty", "Vm_ShellNoActiveTasks", "WorkspaceNavRecommended"),
            ("custom-no-results", "SearchNoResults", "SettingsSearchClearButton"),
            ("global-search", "Vm_GlobalSearchResultsLabel", "GlobalSearchBox"),
            ("snapshot-loading", "Vm_ShellCheckingSystem", "WorkspaceNavRecommended"),
            ("snapshot-error", "Vm_ShellSnapshotUnavailable", "InspectorRetryEnvironmentButton"),
            ("activity-error", "RunNeedsAttention", "ActivityExportFailureBundleButton"),
            ("activity", "Ui_CloseActivityPanel", "ActivityCloseButton")
        };
        var themes = new[] { "dark", "high-contrast" };
        var cultures = new[] { "en", "es" };
        var quick = string.Equals(Environment.GetEnvironmentVariable("LIBRESPOT_QA_QUICK"), "1", StringComparison.Ordinal);
        var data = new TheoryData<string, string, string, string, string>();

        foreach (var surface in surfaces)
        {
            data.Add(surface.State, "dark", "en", surface.ExpectedResource, surface.FocusTarget);
            if (quick)
            {
                continue;
            }

            data.Add(surface.State, "dark", "es", surface.ExpectedResource, surface.FocusTarget);
            data.Add(surface.State, "high-contrast", "en", surface.ExpectedResource, surface.FocusTarget);
            data.Add(surface.State, "high-contrast", "es", surface.ExpectedResource, surface.FocusTarget);
        }

        if (quick)
        {
            data.Add("recommended", themes[1], cultures[1], "ButtonRunRecommendedSetup", "RunRecommendedSetupButton");
        }

        foreach (var culture in new[] { "es", "pt-BR", "ru", "zh-Hans" })
        {
            data.Add("prompt", "dark", culture, "Maintenance_CheckUpdates_Description", "PromptConfirmButton");
        }

        return data;
    }

    public static TheoryData<string, string> CrashDialogMatrix()
    {
        var data = new TheoryData<string, string> { { "dark", "en" } };
        if (!string.Equals(Environment.GetEnvironmentVariable("LIBRESPOT_QA_QUICK"), "1", StringComparison.Ordinal))
        {
            data.Add("dark", "es");
            data.Add("high-contrast", "en");
            data.Add("high-contrast", "es");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SurfaceMatrix))]
    public void WpfShell_QaMatrixCapturesNamedFocusedUnclippedSurface(
        string state,
        string theme,
        string culture,
        string expectedResource,
        string focusTarget)
    {
        var capture = CreateCapturePath(state, theme, culture, out var isTemporary);
        try
        {
            RunOnSta(() =>
            {
                using var app = LaunchCaptureState(state, theme, culture, focusTarget, capture, keepOpen: true);
                try
                {
                    var window = WaitForMainWindow(app.Process, MainWindowTimeout);
                    var metadata = WaitForCapture(capture, CaptureTimeout);
                    var expectedName = GetResource(expectedResource, culture);
                    var snapshot = WaitForSnapshot(window, expectedName, focusTarget, TimeSpan.FromSeconds(10));
                    var windowBounds = UiaNode.From(window).BoundingRectangle;

                    var primary = snapshot.FirstOrDefault(node =>
                        string.Equals(node.Name, expectedName, StringComparison.Ordinal) && HasUsableBounds(node));
                    Assert.NotNull(primary);
                    AssertWithinWindow(primary!, windowBounds, $"primary text '{expectedName}'");

                    var focus = snapshot.FirstOrDefault(node =>
                        string.Equals(node.AutomationId, focusTarget, StringComparison.Ordinal) && HasUsableBounds(node));
                    Assert.NotNull(focus);
                    Assert.True(focus!.IsKeyboardFocusable, $"{focusTarget} must be keyboard focusable.");
                    AssertWithinWindow(focus, windowBounds, $"focus target '{focusTarget}'");
                    AssertNoUnnamedActionableControls(snapshot);

                    AssertCapture(capture, state, theme, culture, focusTarget, expectedFocusVisual: true, metadata);
                }
                finally
                {
                    app.Dispose();
                }
            });
        }
        finally
        {
            DeleteTemporaryCapture(capture, isTemporary);
        }
    }

    [Theory]
    [MemberData(nameof(CrashDialogMatrix))]
    public void WpfShell_QaMatrixCapturesNestedCrashDialog(string theme, string culture)
    {
        var capture = CreateCapturePath("crash", theme, culture, out var isTemporary);
        using (var app = LaunchCaptureState("crash", theme, culture, null, capture, keepOpen: false))
        {
            Assert.True(app.Process.WaitForExit((int)CaptureTimeout.TotalMilliseconds), "Crash-dialog capture did not exit.");
            var metadata = WaitForCapture(capture, CaptureTimeout);
            AssertCapture(capture, "crash", theme, culture, string.Empty, expectedFocusVisual: false, metadata, 500, 360);
        }
        DeleteTemporaryCapture(capture, isTemporary);
    }

    private static SmokeApp LaunchCaptureState(
        string state,
        string theme,
        string culture,
        string? focusTarget,
        string capture,
        bool keepOpen)
    {
        var appPath = Path.Combine(AppContext.BaseDirectory, "LibreSpot.exe");
        Assert.True(File.Exists(appPath), $"Expected WPF executable at {appPath}.");

        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.WpfQa", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var startInfo = new ProcessStartInfo
        {
            FileName = appPath,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add($"--uia-smoke={state}");
        startInfo.ArgumentList.Add($"--uia-culture={culture}");
        startInfo.ArgumentList.Add($"--uia-theme={theme}");
        startInfo.ArgumentList.Add("--uia-background");
        startInfo.ArgumentList.Add("--uia-size=1600x1000");
        startInfo.ArgumentList.Add($"--uia-capture={capture}");
        if (!string.IsNullOrWhiteSpace(focusTarget))
        {
            startInfo.ArgumentList.Add($"--uia-focus={focusTarget}");
        }
        if (keepOpen)
        {
            startInfo.ArgumentList.Add("--uia-capture-keep-open");
        }
        startInfo.Environment["LIBRESPOT_UIA_ROOT"] = root;

        var gate = WpfUiAutomationCollection.EnterExclusive();
        try
        {
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start LibreSpot WPF QA process.");
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
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return AutomationElement.FromHandle(process.MainWindowHandle);
            }

            var window = AutomationElement.RootElement.FindAll(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id))
                .OfType<AutomationElement>()
                .FirstOrDefault();
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException("Timed out waiting for LibreSpot main window.");
    }

    private static IReadOnlyList<UiaNode> Snapshot(AutomationElement root)
    {
        var processId = TryGet(root, AutomationElement.ProcessIdProperty, 0);
        try
        {
            return AutomationElement.RootElement.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, processId))
                .OfType<AutomationElement>()
                .Select(UiaNode.From)
                .ToArray();
        }
        catch
        {
            var nodes = new List<UiaNode>();
            Walk(root, nodes, TreeWalker.ControlViewWalker);
            return nodes;
        }
    }

    private static IReadOnlyList<UiaNode> WaitForSnapshot(
        AutomationElement root,
        string expectedName,
        string focusTarget,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        IReadOnlyList<UiaNode> snapshot = Array.Empty<UiaNode>();
        while (DateTime.UtcNow < deadline)
        {
            snapshot = Snapshot(root);
            if (snapshot.Any(node => string.Equals(node.Name, expectedName, StringComparison.Ordinal) && HasUsableBounds(node)) &&
                snapshot.Any(node => string.Equals(node.AutomationId, focusTarget, StringComparison.Ordinal) && HasUsableBounds(node)))
            {
                return snapshot;
            }

            Thread.Sleep(100);
        }

        return snapshot;
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

    private static Dictionary<string, string> WaitForCapture(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    var metadata = ReadPngTextMetadata(path);
                    if (metadata.ContainsKey("LibreSpotCaptureUtc"))
                    {
                        return metadata;
                    }
                }
            }
            catch (IOException)
            {
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Timed out waiting for WPF QA capture '{path}'.");
    }

    private static void AssertCapture(
        string path,
        string state,
        string theme,
        string culture,
        string focusTarget,
        bool expectedFocusVisual,
        IReadOnlyDictionary<string, string> metadata,
        int minimumWidth = 1_000,
        int minimumHeight = 700)
    {
        var png = File.ReadAllBytes(path);
        Assert.True(png.Length >= 30_000, $"{path} is unexpectedly small ({png.Length:N0} bytes).");
        Assert.True(png.Length >= 24 && Encoding.ASCII.GetString(png, 12, 4) == "IHDR", $"{path} has no valid IHDR header.");
        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        Assert.True(width >= minimumWidth && height >= minimumHeight, $"{path} rendered at {width}x{height}.");
        if (string.Equals(theme, "high-contrast", StringComparison.Ordinal))
        {
            AssertNoRenderDropout(path);
        }

        Assert.Equal(state, metadata["LibreSpotCaptureState"]);
        Assert.Equal(theme, metadata["LibreSpotCaptureTheme"]);
        Assert.Equal(culture, metadata["LibreSpotCaptureCulture"]);
        Assert.Equal(focusTarget, metadata["LibreSpotCaptureFocusTarget"]);
        Assert.Equal(expectedFocusVisual.ToString(CultureInfo.InvariantCulture), metadata["LibreSpotCaptureFocusVisualApplied"]);
        Assert.True(DateTimeOffset.TryParse(metadata["LibreSpotCaptureUtc"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
    }

    private static void AssertNoRenderDropout(string path)
    {
        using var bitmap = new Drawing.Bitmap(path);
        var samples = 0;
        var missing = 0;
        for (var y = 0; y < bitmap.Height; y += 12)
        {
            for (var x = 0; x < bitmap.Width; x += 12)
            {
                var color = bitmap.GetPixel(x, y);
                samples++;
                if (color.A < 240 || (color.R < 3 && color.G < 3 && color.B < 3))
                {
                    missing++;
                }
            }
        }

        Assert.True(missing / (double)samples <= 0.12, $"{path} contains an incomplete render ({missing:N0}/{samples:N0} samples missing).");
    }

    private static Dictionary<string, string> ReadPngTextMetadata(string path)
    {
        var png = File.ReadAllBytes(path);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < signature.Length || !png.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            return values;
        }

        var offset = signature.Length;
        while (offset + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            if (length < 0 || offset + 12 + length > png.Length)
            {
                return values;
            }

            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (type == "tEXt")
            {
                var data = png.AsSpan(offset + 8, length);
                var split = data.IndexOf((byte)0);
                if (split > 0)
                {
                    values[Encoding.ASCII.GetString(data[..split])] = Encoding.ASCII.GetString(data[(split + 1)..]);
                }
            }
            if (type == "IEND")
            {
                break;
            }
            offset += 12 + length;
        }

        return values;
    }

    private static string CreateCapturePath(string state, string theme, string culture, out bool isTemporary)
    {
        var configuredRoot = Environment.GetEnvironmentVariable("LIBRESPOT_QA_CAPTURE_ROOT");
        isTemporary = string.IsNullOrWhiteSpace(configuredRoot);
        var root = isTemporary
            ? Path.Combine(Path.GetTempPath(), "LibreSpot.WpfQa.Captures", Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(configuredRoot!);
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{state}--{theme}--{culture}.png");
    }

    private static void DeleteTemporaryCapture(string path, bool isTemporary)
    {
        if (!isTemporary)
        {
            return;
        }

        try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); } catch { }
    }

    private static string GetResource(string key, string culture) =>
        Strings.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(culture)) ?? key;

    private static bool HasUsableBounds(UiaNode node) =>
        node.BoundingRectangle.Width > 1 && node.BoundingRectangle.Height > 1;

    private static void AssertWithinWindow(UiaNode node, Rect window, string label) =>
        Assert.True(
            node.BoundingRectangle.Left >= window.Left - 2 &&
            node.BoundingRectangle.Top >= window.Top - 2 &&
            node.BoundingRectangle.Right <= window.Right + 2 &&
            node.BoundingRectangle.Bottom <= window.Bottom + 2,
            $"{label} is clipped. Element={node.BoundingRectangle}; window={window}.");

    private static void AssertNoUnnamedActionableControls(IEnumerable<UiaNode> snapshot)
    {
        var unnamed = snapshot.Where(node =>
            node.IsEnabled && HasUsableBounds(node) && ActionableTypes.Contains(node.ControlType) && string.IsNullOrWhiteSpace(node.Name)).ToArray();
        Assert.True(unnamed.Length == 0, "Enabled visible actionable controls need names: " + string.Join(", ", unnamed.Select(node => node.DebugLabel)));
    }

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
            try { action(); } catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed record UiaNode(
        string Name,
        ControlType ControlType,
        bool IsEnabled,
        bool IsKeyboardFocusable,
        string AutomationId,
        Rect BoundingRectangle)
    {
        public string DebugLabel => $"{ControlType.ProgrammaticName}:{Name}:{AutomationId}:bounds={BoundingRectangle}";

        public static UiaNode From(AutomationElement element) =>
            new(
                TryGet(element, AutomationElement.NameProperty, string.Empty),
                TryGet(element, AutomationElement.ControlTypeProperty, ControlType.Custom),
                TryGet(element, AutomationElement.IsEnabledProperty, false),
                TryGet(element, AutomationElement.IsKeyboardFocusableProperty, false),
                TryGet(element, AutomationElement.AutomationIdProperty, string.Empty),
                TryGet(element, AutomationElement.BoundingRectangleProperty, Rect.Empty));
    }

    private sealed class SmokeApp : IDisposable
    {
        public SmokeApp(Process process, string root, IDisposable gate)
        {
            Process = process;
            _root = root;
            _gate = gate;
        }

        public Process Process { get; }
        private readonly string _root;
        private readonly IDisposable _gate;
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
                    Process.Kill(entireProcessTree: true);
                    Process.WaitForExit(2000);
                }
            }
            catch
            {
                try { Process.Kill(entireProcessTree: true); } catch { }
            }
            finally
            {
                try { Process.Dispose(); } finally { _gate.Dispose(); }
                try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
            }
        }
    }
}
