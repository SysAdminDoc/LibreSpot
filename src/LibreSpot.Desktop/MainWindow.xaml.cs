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

namespace LibreSpot.Desktop;

public partial class MainWindow : Window
{
    private const string UiAutomationSmokeArgumentPrefix = "--uia-smoke=";
    private static readonly Regex NumericInput = new("^[0-9]+$", RegexOptions.Compiled);
    private readonly MainViewModel _viewModel;
    private readonly string? _uiAutomationSmokeState;
    private bool _allowCloseWhileRunning;
    private IInputElement? _focusBeforeActivity;
    private IInputElement? _focusBeforePrompt;

    public MainWindow()
    {
        InitializeComponent();

        _uiAutomationSmokeState = GetUiAutomationSmokeState();
        _viewModel = string.IsNullOrWhiteSpace(_uiAutomationSmokeState)
            ? new MainViewModel(
                new ConfigurationService(),
                new BackendScriptService(),
                new EnvironmentSnapshotService())
            : CreateUiAutomationSmokeViewModel();

        DataContext = _viewModel;
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
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

        _viewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        try
        {
            await _viewModel.InitializeAsync();
            if (!string.IsNullOrWhiteSpace(_uiAutomationSmokeState))
            {
                _viewModel.ApplyUiAutomationSmokeState(_uiAutomationSmokeState);
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
        _viewModel.Dispose();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsPromptVisible) or nameof(MainViewModel.IsPromptDestructive))
        {
            Dispatcher.BeginInvoke(UpdatePromptFocus, DispatcherPriority.Input);
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.IsActivityVisible) or nameof(MainViewModel.IsRunning))
        {
            Dispatcher.BeginInvoke(UpdateActivityFocus, DispatcherPriority.Input);
        }
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
