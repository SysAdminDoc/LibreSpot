using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibreSpot.Desktop.Services;
using LibreSpot.Desktop.ViewModels;

namespace LibreSpot.Desktop;

public partial class MainWindow : Window
{
    private static readonly Regex NumericInput = new("^[0-9]+$", RegexOptions.Compiled);
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(
            new ConfigurationService(),
            new BackendScriptService(),
            new EnvironmentSnapshotService());

        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        _viewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;

        await _viewModel.InitializeAsync();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
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
        _viewModel.Dispose();
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
}
