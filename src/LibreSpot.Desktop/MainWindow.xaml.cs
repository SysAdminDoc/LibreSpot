using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibreSpot.Desktop.Services;
using LibreSpot.Desktop.ViewModels;
using Wpf.Ui.Controls;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LibreSpot.Desktop;

public partial class MainWindow : Window
{
    private const string UiAutomationSmokeArgumentPrefix = "--uia-smoke=";
    private static readonly Regex NumericInput = new("^[0-9]+$", RegexOptions.Compiled);
    private readonly MainViewModel _viewModel;
    private readonly string? _uiAutomationSmokeState;
    private readonly ShellActivationRequest _shellActivation;
    private bool _allowCloseWhileRunning;
    private IInputElement? _focusBeforeActivity;
    private IInputElement? _focusBeforePrompt;
    private bool _wasRunning;
    private bool _syncingCustomPatchEditor;
    private Forms.NotifyIcon? _trayIcon;
    private bool _hasShownTrayMinimizeNotice;

    public MainWindow()
    {
        InitializeComponent();

        _uiAutomationSmokeState = GetUiAutomationSmokeState();
        _shellActivation = ShellActivationService.Parse(Environment.GetCommandLineArgs().Skip(1));
        _viewModel = string.IsNullOrWhiteSpace(_uiAutomationSmokeState)
            ? new MainViewModel(
                new ConfigurationService(),
                new BackendScriptService(),
                new EnvironmentSnapshotService())
            : CreateUiAutomationSmokeViewModel();

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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        InitializeTrayIcon();
        _viewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        try
        {
            await _viewModel.InitializeAsync();
            if (!string.IsNullOrWhiteSpace(_uiAutomationSmokeState))
            {
                _viewModel.ApplyUiAutomationSmokeState(_uiAutomationSmokeState);
            }
            else if (_shellActivation.HasActivation)
            {
                await HandleShellActivationAsync(_shellActivation);
            }
        }
        catch (Exception ex)
        {
            // Prevent async void exception from crashing the app on startup.
            // Log the error; the CrashReporter will also catch it if it propagates,
            // but catching here avoids the unhandled-exception termination path.
            Serilog.Log.Error(ex, "InitializeAsync failed during window load");
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
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
        }
    }

    private void InitializeTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("Open LibreSpot");
        openItem.Click += (_, _) => Dispatcher.BeginInvoke(new Action(RestoreFromTray), DispatcherPriority.Background);
        menu.Items.Add(openItem);

        var folderItem = new Forms.ToolStripMenuItem("Open LibreSpot folder");
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
        var exitItem = new Forms.ToolStripMenuItem("Exit LibreSpot");
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
            ? "LibreSpot is still running. Double-click this icon to reopen the live log."
            : "Double-click this icon to reopen LibreSpot.";
        _trayIcon.ShowBalloonTip(5000, "LibreSpot is minimized", detail, Forms.ToolTipIcon.Info);
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
        if (e.Action != NotifyCollectionChangedAction.Add)
        {
            return;
        }

        // Defer scroll-to-end until after the ItemsControl has laid out the new row.
        Dispatcher.BeginInvoke(new Action(() => LogScrollViewer?.ScrollToEnd()), DispatcherPriority.Background);
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

    private static MainViewModel CreateUiAutomationSmokeViewModel()
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

        Directory.CreateDirectory(configDirectory);
        Directory.CreateDirectory(logDirectory);
        Directory.CreateDirectory(crashDirectory);

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
                spotifyVersionProbe: () => "1.2.92",
                spicetifyVersionProbe: () => "2.43.2",
                spotifyRunningProbe: () => false),
            new SupportBundleService(configDirectory, logDirectory, crashDirectory));
    }
}
