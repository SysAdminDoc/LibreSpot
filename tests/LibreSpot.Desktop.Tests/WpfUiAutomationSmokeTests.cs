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

    /// <summary>
    /// The states both accessibility rules scan. Overlays, destructive
    /// confirmations, and the error and empty states are where an accessible
    /// name or a hit target is most likely to go missing, and for a long time
    /// the two rules only ever saw the three top-level workspaces.
    /// </summary>
    private static readonly string[] ScanStates =
    [
        "recommended",
        "custom",
        "maintenance",
        "prompt",
        "prompt-destructive",
        "activity",
        "activity-error",
        "activity-undo",
        "activity-empty",
        "snapshot-error",
        "snapshot-loading",
        "custom-no-results",
        "home-destructive",
    ];

    public static TheoryData<string> AccessibilityScanStates()
    {
        var data = new TheoryData<string>();
        foreach (var state in ScanStates)
        {
            data.Add(state);
        }

        return data;
    }

    /// <summary>
    /// What proves a state finished drawing. A workspace is named, but an
    /// overlay has to be waited on by automation id: the shell underneath it is
    /// already present, so waiting on "Home" would scan before the overlay drew
    /// and quietly report on the wrong tree.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Witness, bool IsAutomationId)> ScanWitnesses =
        new Dictionary<string, (string, bool)>(StringComparer.Ordinal)
        {
            ["recommended"] = ("Home", false),
            ["custom"] = ("Settings", false),
            ["maintenance"] = ("Maintenance", false),
            ["prompt"] = ("PromptConfirmButton", true),
            ["prompt-destructive"] = ("PromptConfirmButton", true),
            ["activity"] = ("ActivityCloseButton", true),
            ["activity-error"] = ("ActivityExportFailureBundleButton", true),
            ["activity-undo"] = ("ActivityCloseButton", true),
            ["activity-empty"] = ("Home", false),
            // The snapshot-error surface offers its retry through the Home
            // primary action, not the inspector's own retry button.
            ["snapshot-error"] = ("HomePrimaryActionButton", true),
            ["snapshot-loading"] = ("Home", false),
            ["custom-no-results"] = ("SettingsSearchClearButton", true),
            ["home-destructive"] = ("Home", false),
        };

    private static IReadOnlyList<UiaNode> WaitForScanState(AutomationElement window, string state, TimeSpan timeout)
    {
        Assert.True(
            ScanWitnesses.TryGetValue(state, out var witness),
            $"No readiness witness is recorded for the '{state}' scan state, so the scan would run against whatever "
                + "happened to be drawn. Add one to ScanWitnesses.");

        return witness.IsAutomationId
            ? WaitForSnapshotContainingAutomationId(window, witness.Witness, timeout)
            : WaitForSnapshotContaining(window, witness.Witness, timeout);
    }

    [Theory]
    [MemberData(nameof(AccessibilityScanStates))]
    public void AxeWindowsScan_FindsNoViolationOutsideTheRecordedBaseline(string state)
    {
        using var app = LaunchSmokeState(state);
        var window = WaitForMainWindow(app.Process, MainWindowTimeout);

        WaitForScanState(window, state, SmokeReadyTimeout);

        var baseline = LoadAxeBaseline(state);
        var scan = ScanUntilSettled(app.Process, window, SmokeReadyTimeout);

        // Without this, a scan that returns nothing at all satisfies every
        // assertion below and the state goes quietly green forever.
        Assert.True(
            scan.WindowsScanned > 0,
            "Axe.Windows scanned no windows, so this state proved nothing. The shell was probably gone or never charted.");

        // The element count is what lets a state with no recorded violations tell
        // a half-drawn window from a finished one. If the walk ever degrades to
        // the root alone, settling silently goes back to agreeing immediately.
        Assert.True(
            scan.ElementsCharted > 1,
            $"The window walk charted {scan.ElementsCharted} elements, so the settle rule has no signal left for a "
                + "state with an empty baseline and would accept the first pair of scans.");

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

        var scan = ScanUntilSettled(app.Process, window, SmokeReadyTimeout);
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

    /// <summary>
    /// WCAG 2.2 success criterion 2.5.8 asks for a 24 by 24 minimum target,
    /// and WCAG2ICT applies it to desktop software. Axe.Windows 2.4.2 has no
    /// rule for it, so a shrunken button or a tiny disclosure chevron would
    /// pass every existing scan.
    /// </summary>
    private const double MinimumTargetSizeDips = 24d;

    /// <summary>
    /// Automation ids WPF gives the parts of a platform scrollbar. Their size
    /// comes from Windows, which is the user-agent exception in WCAG 2.5.8.
    /// Nothing else may be added here without the same justification.
    /// </summary>
    private static readonly HashSet<string> ScrollBarParts = new(StringComparer.Ordinal)
    {
        "PageUp",
        "PageDown",
        "PageLeft",
        "PageRight",
        "LineUp",
        "LineDown",
        "LineLeft",
        "LineRight",
        "UpButton",
        "DownButton",
        "LeftButton",
        "RightButton",
        "Thumb",
        "HorizontalThumb",
        "VerticalThumb",
    };

    [Theory]
    [MemberData(nameof(AccessibilityScanStates))]
    public void InteractiveTargets_AreAtLeastTwentyFourByTwentyFourDips(string state)
    {
        using var app = LaunchSmokeState(state);
        var window = WaitForMainWindow(app.Process, MainWindowTimeout);
        WaitForScanState(window, state, SmokeReadyTimeout);

        var undersized = UndersizedTargets(window, out var scale, out var charted);

        // Without this the rule could report nothing because the walk
        // charted nothing, which is not the same as a clean result.
        Assert.True(charted > 1, $"The walk charted {charted} elements for {state}, so this proved nothing.");
        Assert.True(scale > 0, "Could not determine the display scale.");

        Assert.True(
            undersized.Count == 0,
            $"These targets in the {state} state are smaller than {MinimumTargetSizeDips} by {MinimumTargetSizeDips} "
                + "device-independent pixels, which WCAG 2.2 success criterion 2.5.8 asks for:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, undersized));
    }

    [Fact]
    public void TargetSizeRule_ReportsAPlantedButtonThatIsTooSmall()
    {
        // Positive control. The planted button carries a real accessible name,
        // so only the size rule can report it and a pass here cannot be
        // borrowed from the unnamed-button control beside it.
        using var app = LaunchSmokeState("axe-positive-control");
        var window = WaitForMainWindow(app.Process, MainWindowTimeout);
        WaitForSnapshotContaining(window, "Home", SmokeReadyTimeout);

        var undersized = UndersizedTargets(window, out _, out var charted);
        Assert.True(charted > 1, "The walk charted nothing, so this control proved nothing.");

        Assert.True(
            undersized.Any(entry => entry.Contains(MainWindow.UiAutomationTargetSizeControlAutomationId, StringComparison.Ordinal)),
            "The rule did not report the undersized button planted in the axe-positive-control state, so it is "
                + "not checking anything. Reported: "
                + string.Join(", ", undersized));
    }

    private static IReadOnlyList<string> UndersizedTargets(
        AutomationElement window,
        out double scale,
        out int charted)
    {
        var nodes = Snapshot(window);
        charted = nodes.Count;
        var displayScale = DisplayScale();
        scale = displayScale;
        var minimum = MinimumTargetSizeDips * displayScale;

        return nodes
            .Where(node => node.IsInteractive && node.IsEnabled)
            // A row that offers only SelectionItemPattern and cannot take focus
            // is not something a person points at. The run log is a ListBox so it
            // can virtualise thousands of lines; its ListBoxItem style sets
            // Focusable="False" and templates the container down to a bare
            // ContentPresenter, so the rows carry no selection affordance at all.
            // WCAG 2.5.8 governs pointer targets, and growing a log line to 24
            // dips would treble the height of every line to no one's benefit.
            .Where(node => node.HasNonSelectionPattern || node.IsKeyboardFocusable)
            // WCAG 2.5.8 exempts a control whose size the user agent determines.
            // These are the parts of the platform scrollbar, which is 17 pixels
            // wide because Windows says so; LibreSpot neither sets nor can set
            // their size, and widening the app's scrollbars would not make the
            // target any easier to hit than the page scroll it already has.
            .Where(node => !ScrollBarParts.Contains(node.AutomationId))
            // An offscreen or collapsed element has an empty rect; it is not a
            // target until it is shown, and states that show it will catch it.
            .Where(node => node.BoundingRectangle.Width > 0 && node.BoundingRectangle.Height > 0)
            .Where(node => node.BoundingRectangle.Width < minimum || node.BoundingRectangle.Height < minimum)
            .Select(node => $"{node.AutomationId}|{node.Name}|{node.ControlType.ProgrammaticName}|"
                + $"{node.BoundingRectangle.Width / displayScale:0.#}x{node.BoundingRectangle.Height / displayScale:0.#} dips")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Bounding rectangles come back in physical pixels, so the rule needs the
    /// scale to talk in device-independent ones. At 125% a 20 dip button
    /// measures 25 physical pixels and would slip past a raw comparison.
    /// </summary>
    private static double DisplayScale()
    {
        var dpi = GetDpiForSystem();
        return dpi > 0 ? dpi / 96d : 1d;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    private sealed record AxeScan(int WindowsScanned, int ElementsCharted, IReadOnlyList<AxeViolation> Violations)
    {
        public AxeScanShape Shape =>
            new(WindowsScanned, ElementsCharted, Violations.Select(violation => violation.Key).ToArray());
    }

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
    ///
    /// The violation counts cannot carry that signal on their own. A state with
    /// an empty baseline reports zero of them while it is half drawn and zero
    /// again when it is finished, so two passes agree on the first comparison
    /// and the scan is taken before the cards exist. The charted element count
    /// is what keeps moving, so the window is walked alongside every scan.
    /// </remarks>
    private static AxeScan ScanUntilSettled(Process process, AutomationElement window, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var previous = ScanForAccessibilityViolations(process, window);

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(400);
            var current = ScanForAccessibilityViolations(process, window);
            if (previous.Shape.HasSameShapeAs(current.Shape))
            {
                return current;
            }

            previous = current;
        }

        return previous;
    }

    private static AxeScan ScanForAccessibilityViolations(Process process, AutomationElement window)
    {
        var config = Config.Builder
            .ForProcessId(process.Id)
            .WithOutputFileFormat(OutputFileFormat.None)
            .Build();

        var output = ScannerFactory.CreateScanner(config).Scan(null);

        // Axe reports no element count of its own, so the tree is walked here.
        // Taken after the scan so a window that grew during it reads as changed
        // and the loop goes round again rather than accepting a partial pass.
        var elementsCharted = Snapshot(window).Count;

        // The window count travels with the result so callers can tell "this
        // window is clean" from "nothing was looked at".
        return new AxeScan(
            output.WindowScanOutputs.Count,
            elementsCharted,
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

    [Fact]
    public void AxeBaseline_RecordsEveryScannedStateAndNothingElse()
    {
        // Two ways this drifts and both are silent. A state added to the scan
        // without a baseline key fails one row with a confusing message, and a
        // baseline key left behind after a state is dropped looks like coverage
        // that no longer exists. This is a source gate so it costs no shell
        // launches.
        var path = Path.Combine(ResolveRepoRoot(), "schemas", "axe-windows-baseline.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var recorded = document.RootElement
            .GetProperty("states")
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var scanned = ScanStates
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(scanned, recorded);
    }

    [Fact]
    public void AccessibilityScanStates_AllHaveAReadinessWitness()
    {
        // A scanned state with no witness would fall through to whatever the
        // shell happened to have drawn, which is how an accessibility scan goes
        // green against the wrong tree.
        foreach (var state in ScanStates)
        {
            Assert.True(ScanWitnesses.ContainsKey(state), $"No readiness witness recorded for '{state}'.");
        }
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

        // The settle loop walks the window on every scan, so a shell that dies
        // mid-pass throws from here. Returning what was charted lets the
        // caller's "scanned no windows, so this proved nothing" assertion be
        // the reported failure, which says what happened; the exception did not.
        AutomationElement? child;
        try
        {
            child = walker.GetFirstChild(element);
        }
        catch (ElementNotAvailableException)
        {
            return;
        }

        while (child is not null)
        {
            Walk(child, nodes, walker);
            try
            {
                child = walker.GetNextSibling(child);
            }
            catch (ElementNotAvailableException)
            {
                return;
            }
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
        Rect BoundingRectangle,
        bool IsInteractive,
        bool HasNonSelectionPattern)
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
                TryGet(element, AutomationElement.BoundingRectangleProperty, Rect.Empty),
                SupportsAnInteractionPattern(element),
                SupportsAPatternOtherThanSelection(element));

        /// <summary>
        /// Whether a person can operate this element. WCAG 2.2's target-size
        /// criterion applies to the thing being pointed at, not to every node
        /// in the tree, and the patterns are what say which is which.
        /// </summary>
        private static bool SupportsAnInteractionPattern(AutomationElement element)
        {
            try
            {
                foreach (var pattern in element.GetSupportedPatterns())
                {
                    if (pattern == InvokePattern.Pattern
                        || pattern == TogglePattern.Pattern
                        || pattern == SelectionItemPattern.Pattern
                        || pattern == ExpandCollapsePattern.Pattern)
                    {
                        return true;
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                // A node that vanished mid-walk is not a target.
            }

            return false;
        }

        /// <summary>
        /// Whether the element can be operated by something other than being
        /// selected. WPF hands every ListBoxItem a SelectionItemPattern whether
        /// or not the list is meant to be operated, so selection alone does not
        /// make a row a pointer target.
        /// </summary>
        private static bool SupportsAPatternOtherThanSelection(AutomationElement element)
        {
            try
            {
                foreach (var pattern in element.GetSupportedPatterns())
                {
                    if (pattern == InvokePattern.Pattern
                        || pattern == TogglePattern.Pattern
                        || pattern == ExpandCollapsePattern.Pattern)
                    {
                        return true;
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                // A node that vanished mid-walk is not a target.
            }

            return false;
        }
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
