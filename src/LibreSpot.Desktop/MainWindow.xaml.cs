using System.Buffers.Binary;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using LibreSpot.Desktop.ViewModels;
using Wpf.Ui.Controls;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LibreSpot.Desktop;

public partial class MainWindow : Window
{
    private const string UiAutomationSmokeArgumentPrefix = "--uia-smoke=";
    private const string UiAutomationCultureArgumentPrefix = "--uia-culture=";
    private const string UiAutomationCaptureArgumentPrefix = "--uia-capture=";
    private const string UiAutomationBackgroundArgument = "--uia-background";
    private static readonly Regex NumericInput = new("^[0-9]+$", RegexOptions.Compiled);
    private readonly MainViewModel _viewModel;
    private readonly string? _uiAutomationSmokeState;
    private readonly string _uiAutomationSmokeCulture;
    private readonly string? _uiAutomationCapturePath;
    private readonly bool _uiAutomationBackgroundMode;
    private readonly ShellActivationRequest _shellActivation;
    private bool _allowCloseWhileRunning;
    private IInputElement? _focusBeforeActivity;
    private IInputElement? _focusBeforePrompt;
    private bool _wasRunning;
    private bool _syncingCustomPatchEditor;
    private bool _isLogScrollPending;
    private Forms.NotifyIcon? _trayIcon;
    private bool _hasShownTrayMinimizeNotice;

    public MainWindow()
    {
        InitializeComponent();

        _uiAutomationSmokeState = GetUiAutomationSmokeState();
        _uiAutomationSmokeCulture = GetUiAutomationSmokeCulture();
        _uiAutomationCapturePath = GetUiAutomationCapturePath();
        _uiAutomationBackgroundMode = GetUiAutomationBackgroundMode();
        if (_uiAutomationBackgroundMode)
        {
            ShowActivated = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth + 64;
            Top = SystemParameters.VirtualScreenTop + 64;
        }
        else
        {
            ConstrainToWorkArea();
            SizeChanged += MainWindow_SizeChanged;
        }

        _shellActivation = ShellActivationService.Parse(Environment.GetCommandLineArgs().Skip(1));
        _viewModel = string.IsNullOrWhiteSpace(_uiAutomationSmokeState)
            ? new MainViewModel(
                new ConfigurationService(),
                new BackendScriptService(),
                new EnvironmentSnapshotService(
                    upstreamDriftProbe: () => UpstreamDriftService.Default.GetReport(),
                    communityAssetDriftProbe: () => CommunityAssetDriftService.Default.GetReport(),
                    antivirusProbe: EnvironmentSnapshotService.QueryDefenderExclusionStatus,
                    storeSpotifyProbe: EnvironmentSnapshotService.QueryStoreSpotifyPresent))
            : CreateUiAutomationSmokeViewModel(_uiAutomationSmokeCulture);

        DataContext = _viewModel;
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= MainWindow_SourceInitialized;
        // Dark title bar + Win11 Mica must be applied once the HWND exists but
        // before the window becomes visible, or the chrome flashes in light mode.
        Win11ShellIntegration.ApplyMicaAndDarkChrome(this);
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if WPF receives an already-completed mouse gesture.
        }
    }

    private void MinimizeWindow_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeWindow_OnClick(object sender, RoutedEventArgs e) =>
        ToggleMaximizeRestore();

    private void CloseWindow_OnClick(object sender, RoutedEventArgs e) =>
        Close();

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void ConstrainToWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, Math.Max(MinWidth, workArea.Width - 24));
        Height = Math.Min(Height, Math.Max(MinHeight, workArea.Height - 24));
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyResponsiveShellLayout();

    private void ApplyResponsiveShellLayout()
    {
        var shellWidth = ActualWidth > 0 ? ActualWidth : Width;
        var shellHeight = ActualHeight > 0 ? ActualHeight : Height;
        var isNarrow = shellWidth < 1520;
        var isCompact = shellWidth < 1580;
        var isShort = shellHeight < 800;

        ShellRailColumn.Width = new GridLength(isCompact ? 220 : 248);
        ShellRailGutterColumn.Width = new GridLength(isCompact ? 20 : 28);
        ShellInspectorGutterColumn.Width = new GridLength(isNarrow ? 0 : isCompact ? 14 : 18);
        ShellInspectorColumn.Width = new GridLength(isNarrow ? 0 : isCompact ? 296 : 320);
        InspectorPanel.Visibility = isNarrow ? Visibility.Collapsed : Visibility.Visible;

        var activityHeight = isShort ? 92 : shellHeight < 900 ? 132 : 184;
        var activityBottom = isShort ? 20 : 38;
        var activityOffset = activityHeight + activityBottom - 2;
        var workspaceTop = isShort ? 52 : 64;
        ActivityDock.Height = activityHeight;
        ActivityDock.Margin = new Thickness(0, 0, 16, activityBottom);
        WorkspaceSurface.Margin = new Thickness(0, workspaceTop, 0, activityOffset);
        InspectorPanel.Margin = new Thickness(0, workspaceTop, 16, activityOffset);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        ApplyResponsiveShellLayout();
        if (!_uiAutomationBackgroundMode)
        {
            InitializeTrayIcon();
        }
        _viewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        try
        {
            var uiAutomationSmokeState = _uiAutomationSmokeState;
            var hasUiAutomationSmokeState = !string.IsNullOrWhiteSpace(uiAutomationSmokeState);

            await _viewModel.InitializeAsync();
            if (hasUiAutomationSmokeState)
            {
                _viewModel.ApplyUiAutomationSmokeState(uiAutomationSmokeState!);
                if (!string.IsNullOrWhiteSpace(_uiAutomationCapturePath))
                {
                    // Selection/card animations run for up to 220 ms and a dense
                    // Custom workspace can enqueue another layout pass afterward.
                    // Waiting through two dispatcher drains avoids partially empty
                    // RenderTargetBitmap captures without changing the live renderer.
                    await Dispatcher.InvokeAsync(PrepareUiAutomationCapture, DispatcherPriority.Loaded);
                    await Task.Delay(900);
                    await Dispatcher.InvokeAsync(PrepareUiAutomationCapture, DispatcherPriority.ApplicationIdle);
                    SaveUiAutomationCapture(_uiAutomationCapturePath);
                    _allowCloseWhileRunning = true;
                    Close();
                    return;
                }
            }
            else if (_shellActivation.HasActivation)
            {
                await HandleShellActivationAsync(_shellActivation);
            }
        }
        catch (Exception ex)
        {
            // Prevent async void exception from crashing the app on startup.
            Serilog.Log.Error(ex, "InitializeAsync failed during window load");
            _viewModel.ApplyInitializationFailure();
        }
    }

    private void PrepareUiAutomationCapture()
    {
        ApplyResponsiveShellLayout();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
        UpdateLayout();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_viewModel.IsRunning && _viewModel.IsCancelRequested)
        {
            _allowCloseWhileRunning = true;
        }

        if (_viewModel.IsRunning && !_allowCloseWhileRunning)
        {
            e.Cancel = true;
            RestoreFromTray();
            _viewModel.PresentCloseWhileRunningPrompt(() =>
            {
                _allowCloseWhileRunning = true;
                _viewModel.CancelRunningBackend();
                Dispatcher.BeginInvoke(Close, DispatcherPriority.Background);
                return Task.CompletedTask;
            });
            return;
        }

        // Cancel any in-flight backend run so we don't orphan a powershell.exe process.
        // The CancellationToken chain tears down the child process tree cleanly.
        _viewModel.CancelRunningBackend();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (_viewModel.LogEntries is not null)
        {
            _viewModel.LogEntries.CollectionChanged -= OnLogEntriesChanged;
        }
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        StateChanged -= MainWindow_StateChanged;
        DisposeTrayIcon();
        _viewModel.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CustomPatchesJson))
        {
            Dispatcher.BeginInvoke(SyncCustomPatchesEditorText, DispatcherPriority.Background);
        }

        if (e.PropertyName is nameof(MainViewModel.IsPromptVisible) or nameof(MainViewModel.IsPromptDestructive))
        {
            Dispatcher.BeginInvoke(UpdatePromptFocus, DispatcherPriority.Input);
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.IsActivityVisible) or nameof(MainViewModel.IsRunning))
        {
            if (e.PropertyName == nameof(MainViewModel.IsRunning))
            {
                var runJustFinished = _wasRunning && !_viewModel.IsRunning;
                _wasRunning = _viewModel.IsRunning;
                if (runJustFinished)
                {
                    Dispatcher.BeginInvoke(ShowCompletionSnackbar, DispatcherPriority.Background);
                }
            }

            Dispatcher.BeginInvoke(UpdateActivityFocus, DispatcherPriority.Input);
        }
    }

    private void CustomPatchesTextEditor_OnTextChanged(object? sender, EventArgs e)
    {
        if (_syncingCustomPatchEditor)
        {
            return;
        }

        _viewModel.CustomPatchesJson = CustomPatchesTextEditor.Text ?? string.Empty;
    }

    private void SyncCustomPatchesEditorText()
    {
        if (!IsLoaded)
        {
            return;
        }

        var next = _viewModel.CustomPatchesJson ?? string.Empty;
        if (string.Equals(CustomPatchesTextEditor.Text, next, StringComparison.Ordinal))
        {
            return;
        }

        _syncingCustomPatchEditor = true;
        try
        {
            CustomPatchesTextEditor.Text = next;
        }
        finally
        {
            _syncingCustomPatchEditor = false;
        }
    }

    private void NestedScrollRegion_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || sender is not DependencyObject source)
        {
            return;
        }

        var scrollViewer = FindDescendantScrollViewer(source);
        if (scrollViewer is not null && CanScrollVertically(scrollViewer, e.Delta))
        {
            return;
        }

        if (VisualTreeHelper.GetParent(source) is not UIElement parent)
        {
            return;
        }

        e.Handled = true;
        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = sender
        });
    }

    private static bool CanScrollVertically(System.Windows.Controls.ScrollViewer scrollViewer, int delta) =>
        delta < 0
            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
            : scrollViewer.VerticalOffset > 0;

    private static System.Windows.Controls.ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        if (root is System.Windows.Controls.ScrollViewer scrollViewer)
        {
            return scrollViewer;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            var descendant = FindDescendantScrollViewer(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void ShowCompletionSnackbar()
    {
        if (!IsLoaded || !_viewModel.IsActivityVisible)
        {
            return;
        }

        var appearance = _viewModel.IsActivityError
            ? ControlAppearance.Danger
            : _viewModel.IsActivityCanceled
                ? ControlAppearance.Caution
                : ControlAppearance.Success;

        var snackbar = new Snackbar(CompletionSnackbarPresenter)
        {
            Title = _viewModel.ActivityBadgeText,
            Content = _viewModel.ActivityStatus,
            Appearance = appearance,
            Timeout = TimeSpan.FromSeconds(6),
            IsCloseButtonEnabled = true
        };

        if (IsVisible && WindowState != WindowState.Minimized)
        {
            snackbar.Show();
        }

        if (!IsActive || !IsVisible || WindowState == WindowState.Minimized)
        {
            ShowTrayCompletionNotification(appearance);
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            HideToTray();
        }
    }

    private async Task HandleShellActivationAsync(ShellActivationRequest activation)
    {
        switch (activation.Kind)
        {
            case ShellActivationKind.NavigateRecommended:
                _viewModel.SelectedWorkspaceIndex = 0;
                break;
            case ShellActivationKind.NavigateCustom:
                _viewModel.SelectedWorkspaceIndex = 1;
                break;
            case ShellActivationKind.NavigateMaintenance:
                _viewModel.SelectedWorkspaceIndex = 2;
                break;
            case ShellActivationKind.ImportProfile:
                _viewModel.SelectedWorkspaceIndex = 1;
                if (_viewModel.ImportProfileCommand.CanExecute(null))
                {
                    _viewModel.ImportProfileCommand.Execute(null);
                }
                break;
            case ShellActivationKind.OpenLibreSpotFolder:
                if (_viewModel.OpenLibreSpotFolderCommand.CanExecute(null))
                {
                    _viewModel.OpenLibreSpotFolderCommand.Execute(null);
                }
                break;
            case ShellActivationKind.ProfileShareUri when !string.IsNullOrWhiteSpace(activation.Value):
                _viewModel.SelectedWorkspaceIndex = 1;
                await _viewModel.PreviewSharedProfileUriAsync(activation.Value);
                break;
            case ShellActivationKind.ResumeInstallElevated:
                await _viewModel.ResumeElevatedInstallAsync();
                break;
        }
    }

    private void InitializeTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem(Properties.Strings.Tray_Open);
        openItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(RestoreFromTray), DispatcherPriority.Background);
        menu.Items.Add(openItem);

        var folderItem = new Forms.ToolStripMenuItem(Properties.Strings.Tray_OpenFolder);
        folderItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            RestoreFromTray();
            if (_viewModel.OpenLibreSpotFolderCommand.CanExecute(null))
            {
                _viewModel.OpenLibreSpotFolderCommand.Execute(null);
            }
        }), DispatcherPriority.Background);
        menu.Items.Add(folderItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        var exitItem = new Forms.ToolStripMenuItem(Properties.Strings.Tray_Exit);
        exitItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            RestoreFromTray();
            Close();
        }), DispatcherPriority.Background);
        menu.Items.Add(exitItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "LibreSpot",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(new Action(RestoreFromTray), DispatcherPriority.Background);
        _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.BeginInvoke(new Action(RestoreFromTray), DispatcherPriority.Background);
    }

    private void HideToTray()
    {
        if (_trayIcon is null)
        {
            return;
        }

        Hide();
        if (_hasShownTrayMinimizeNotice)
        {
            return;
        }

        _hasShownTrayMinimizeNotice = true;
        var detail = _viewModel.IsRunning
            ? Properties.Strings.Tray_BalloonTextRunning
            : Properties.Strings.Tray_BalloonText;
        _trayIcon.ShowBalloonTip(5000, Properties.Strings.Tray_BalloonTitle, detail, Forms.ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        Activate();
    }

    private void ShowTrayCompletionNotification(ControlAppearance appearance)
    {
        if (_trayIcon is null)
        {
            return;
        }

        var icon = appearance switch
        {
            ControlAppearance.Danger => Forms.ToolTipIcon.Error,
            ControlAppearance.Caution => Forms.ToolTipIcon.Warning,
            _ => Forms.ToolTipIcon.Info
        };
        _trayIcon.ShowBalloonTip(10000, _viewModel.ActivityBadgeText, _viewModel.ActivityStatus, icon);
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null)
        {
            return;
        }

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath) && File.Exists(executablePath))
            {
                var icon = Drawing.Icon.ExtractAssociatedIcon(executablePath);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch
        {
            // Fall back to the platform icon; the tray affordance should never block startup.
        }

        return Drawing.SystemIcons.Application;
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _isLogScrollPending)
        {
            return;
        }

        _isLogScrollPending = true;
        // Defer until after the virtualized list has realized the newest row.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (LogListBox?.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
                }
            }
            finally
            {
                _isLogScrollPending = false;
            }
        }), DispatcherPriority.Background);
    }

    private void CacheLimitTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !NumericInput.IsMatch(e.Text);
    }

    private void CacheLimitTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        // PreviewTextInput does not fire for clipboard paste. Without this handler
        // a user could paste non-numeric text into the cache-limit field.
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var pasted = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        if (!NumericInput.IsMatch(pasted))
        {
            e.CancelCommand();
        }
    }

    private void UpdatePromptFocus()
    {
        if (_viewModel.IsPromptVisible)
        {
            _focusBeforePrompt ??= Keyboard.FocusedElement;
            FocusElement(_viewModel.IsPromptDestructive ? PromptCancelButton : PromptConfirmButton, PromptDialogRoot);
            return;
        }

        RestoreFocus(ref _focusBeforePrompt);
    }

    private void UpdateActivityFocus()
    {
        if (_viewModel.IsPromptVisible)
        {
            return;
        }

        if (_viewModel.IsActivityVisible)
        {
            _focusBeforeActivity ??= Keyboard.FocusedElement;

            if (_viewModel.IsRunning)
            {
                FocusElement(ActivityDialogRoot);
                return;
            }

            if (FocusElement(ActivityCloseButton, ActivityCopyLogButton, ActivityOpenLibreSpotFolderButton, ActivityDialogRoot))
            {
                return;
            }
        }
        else
        {
            RestoreFocus(ref _focusBeforeActivity);
        }
    }

    private static bool FocusElement(params IInputElement?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            switch (candidate)
            {
                case UIElement element when element.IsVisible && element.IsEnabled && element.Focus():
                    return true;
                case ContentElement contentElement when contentElement.IsEnabled && contentElement.Focus():
                    return true;
            }
        }

        return false;
    }

    private static void RestoreFocus(ref IInputElement? previousFocus)
    {
        if (previousFocus is null)
        {
            return;
        }

        try
        {
            FocusElement(previousFocus);
        }
        finally
        {
            previousFocus = null;
        }
    }

    private static string? GetUiAutomationSmokeState()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (arg.StartsWith(UiAutomationSmokeArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[UiAutomationSmokeArgumentPrefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(value) ? "recommended" : value;
            }
        }

        return null;
    }

    private static string GetUiAutomationSmokeCulture()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (arg.StartsWith(UiAutomationCultureArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return LocalizationService.NormalizeCultureName(arg[UiAutomationCultureArgumentPrefix.Length..].Trim());
            }
        }

        return LocalizationService.DefaultCultureName;
    }

    private static string? GetUiAutomationCapturePath()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (arg.StartsWith(UiAutomationCaptureArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[UiAutomationCaptureArgumentPrefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
            }
        }

        return null;
    }

    private static bool GetUiAutomationBackgroundMode()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (string.Equals(arg.Trim(), UiAutomationBackgroundArgument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SaveUiAutomationCapture(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var dpiX = 96.0 * transform.M11;
        var dpiY = 96.0 * transform.M22;
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * transform.M11));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * transform.M22));

        UpdateLayout();
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        bitmap.Render(this);

        var metadata = CreateUiAutomationCaptureMetadata();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path))
        {
            encoder.Save(stream);
        }

        WritePngTextChunks(path, metadata);
    }

    private Dictionary<string, string> CreateUiAutomationCaptureMetadata() =>
        new(StringComparer.Ordinal)
        {
            ["LibreSpotShellVersion"] = _viewModel.ShellDisplayVersion,
            ["LibreSpotCaptureAssemblyVersion"] = GetAssemblyInformationalVersion(),
            ["LibreSpotCaptureState"] = _uiAutomationSmokeState ?? "live",
            ["LibreSpotCaptureUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };

    private static void WritePngTextChunks(string path, IReadOnlyDictionary<string, string> metadata)
    {
        var png = File.ReadAllBytes(path);
        var iendOffset = FindPngIendOffset(png);
        using var stream = File.Create(path);
        stream.Write(png, 0, iendOffset);
        foreach (var (key, value) in metadata)
        {
            var chunk = CreatePngTextChunk(key, value);
            stream.Write(chunk, 0, chunk.Length);
        }

        stream.Write(png, iendOffset, png.Length - iendOffset);
    }

    private static int FindPngIendOffset(byte[] png)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < signature.Length || !png.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            throw new InvalidOperationException("Capture output is not a PNG file.");
        }

        var offset = signature.Length;
        while (offset + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            if (length < 0 || offset + 12 + length > png.Length)
            {
                throw new InvalidOperationException("Capture output PNG has an invalid chunk table.");
            }

            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (string.Equals(type, "IEND", StringComparison.Ordinal))
            {
                return offset;
            }

            offset += 12 + length;
        }

        throw new InvalidOperationException("Capture output PNG is missing an IEND chunk.");
    }

    private static byte[] CreatePngTextChunk(string key, string value)
    {
        var typeBytes = Encoding.ASCII.GetBytes("tEXt");
        var keyBytes = Encoding.ASCII.GetBytes(key);
        var valueBytes = Encoding.ASCII.GetBytes(value);
        var data = new byte[keyBytes.Length + 1 + valueBytes.Length];
        Buffer.BlockCopy(keyBytes, 0, data, 0, keyBytes.Length);
        Buffer.BlockCopy(valueBytes, 0, data, keyBytes.Length + 1, valueBytes.Length);

        var chunk = new byte[12 + data.Length];
        BinaryPrimitives.WriteInt32BigEndian(chunk.AsSpan(0, 4), data.Length);
        typeBytes.CopyTo(chunk.AsSpan(4, 4));
        data.CopyTo(chunk.AsSpan(8, data.Length));

        var crcInput = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length, 4), ComputeCrc32(crcInput));
        return chunk;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ 0xEDB88320u
                    : crc >> 1;
            }
        }

        return ~crc;
    }

    private static string GetAssemblyInformationalVersion() =>
        typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MainWindow).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    private static MainViewModel CreateUiAutomationSmokeViewModel(string culture)
    {
        var root = Environment.GetEnvironmentVariable("LIBRESPOT_UIA_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(Path.GetTempPath(), "LibreSpot.UIA", Guid.NewGuid().ToString("N"));
        }

        root = Path.GetFullPath(root);
        var configDirectory = Path.Combine(root, "config");
        var logDirectory = Path.Combine(root, "logs");
        var crashDirectory = Path.Combine(root, "crashes");
        var runtimeDirectory = Path.Combine(root, "runtime");
        var spotifyPath = Path.Combine(root, "Spotify", "Spotify.exe");
        var spicetifyPath = Path.Combine(root, "Spicetify", "spicetify.exe");
        var spicetifyConfigDirectory = Path.Combine(root, "spicetify-config");
        var backupDirectory = Path.Combine(root, "backups");
        var spotifyAppsDirectory = Path.Combine(Path.GetDirectoryName(spotifyPath) ?? root, "Apps");
        var marketplaceDirectory = Path.Combine(spicetifyConfigDirectory, "CustomApps", "marketplace");

        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(logDirectory);
        Directory.CreateDirectory(crashDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(spotifyPath) ?? root);
        Directory.CreateDirectory(Path.GetDirectoryName(spicetifyPath) ?? root);
        Directory.CreateDirectory(spotifyAppsDirectory);
        Directory.CreateDirectory(marketplaceDirectory);
        Directory.CreateDirectory(backupDirectory);
        File.WriteAllText(spotifyPath, string.Empty);
        File.WriteAllText(spicetifyPath, string.Empty);
        File.WriteAllText(Path.Combine(spotifyAppsDirectory, "xpui.spa"), string.Empty);
        File.WriteAllText(Path.Combine(spotifyAppsDirectory, "xpui.bak"), string.Empty);
        File.WriteAllText(Path.Combine(spotifyAppsDirectory, "xpui.spa.bak"), string.Empty);
        Directory.CreateDirectory(Path.Combine(backupDirectory, "2026-07-07_104213"));
        File.WriteAllText(Path.Combine(spicetifyConfigDirectory, "config-xpui.ini"), "[AdditionalOptions]\ncustom_apps = marketplace\n");
        File.WriteAllText(Path.Combine(marketplaceDirectory, "extension.js"), string.Empty);
        File.WriteAllText(Path.Combine(marketplaceDirectory, "manifest.json"), "{\"version\":\"1.0.9\"}");
        File.WriteAllText(
            Path.Combine(configDirectory, "marketplace-evidence.json"),
            "{\"schemaVersion\":1,\"generatedAtUtc\":\"2026-07-07T10:42:13Z\",\"source\":\"UiAutomationSmoke\",\"filesPresent\":true,\"registered\":true,\"likelyVisible\":true,\"marketplaceStatus\":\"Likely visible\",\"manifestVersion\":\"1.0.9\",\"applyStage\":\"apply\",\"applySucceeded\":true,\"openUriSucceeded\":true}");
        File.WriteAllText(
            Path.Combine(configDirectory, "config.json"),
            $"{{\"UiCulture\":\"{LocalizationService.NormalizeCultureName(culture)}\"}}");

        return new MainViewModel(
            new ConfigurationService(configDirectory),
            new BackendScriptService(runtimeDirectory, noBackendMode: true),
            new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => false,
                spotifyPath: spotifyPath,
                spicetifyPath: spicetifyPath,
                spicetifyConfigDirectory: spicetifyConfigDirectory,
                backupDirectory: backupDirectory,
                rollingLogDirectory: logDirectory,
                crashDirectory: crashDirectory,
                spotifyVersionProbe: () => "1.2.93",
                spicetifyVersionProbe: () => "2.44.0",
                spotifyRunningProbe: () => false,
                upstreamDriftProbe: () => UpstreamDriftReport.Empty,
                communityAssetDriftProbe: () => CommunityAssetDriftReport.Empty),
            new SupportBundleService(configDirectory, logDirectory, crashDirectory));
    }
}
