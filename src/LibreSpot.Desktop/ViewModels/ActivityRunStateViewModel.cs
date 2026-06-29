using System.Collections.ObjectModel;
using LibreSpot.Desktop.Properties;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed partial class ActivityRunStateViewModel : ObservableObject
{
    private const int MaxLogEntries = 2000;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isCancelRequested;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _title = Strings.ActivityReady;

    [ObservableProperty]
    private string _status = Strings.ActivityPickPath;

    [ObservableProperty]
    private string _step = "Idle";

    public ActivityRunStateViewModel()
    {
        LogEntries = new ObservableCollection<LogEntryViewModel>();
        UndoActionItems = new ObservableCollection<UndoActionItemViewModel>();
    }

    public ObservableCollection<LogEntryViewModel> LogEntries { get; }
    public ObservableCollection<UndoActionItemViewModel> UndoActionItems { get; }

    public string LogLineCountText =>
        LogEntries.Count switch
        {
            0 => "No log output yet",
            1 => "1 log line",
            _ => $"{LogEntries.Count} log lines"
        };

    public bool IsLogEmpty => LogEntries.Count == 0;

    public bool HasUndoActionItems => UndoActionItems.Count > 0;

    public void Begin(string title, string status, string step)
    {
        Title = title;
        Status = status;
        Step = step;
        ProgressValue = 0;
        IsVisible = true;
        IsRunning = true;
        IsCancelRequested = false;
    }

    public void ShowNotice(string title, string status, string step)
    {
        Title = title;
        Status = status;
        Step = step;
        ProgressValue = 0;
        IsVisible = true;
    }

    public void AppendLog(string payload, string level, DateTime timestamp)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return;
        }

        LogEntries.Add(new LogEntryViewModel(timestamp, level, payload));
        while (LogEntries.Count > MaxLogEntries)
        {
            LogEntries.RemoveAt(0);
        }

        RaiseLogStateChanged();
    }

    public void ClearLog()
    {
        if (LogEntries.Count == 0)
        {
            return;
        }

        LogEntries.Clear();
        RaiseLogStateChanged();
    }

    public void ClearUndoActionItems()
    {
        if (UndoActionItems.Count == 0)
        {
            return;
        }

        UndoActionItems.Clear();
        OnPropertyChanged(nameof(HasUndoActionItems));
    }

    public void ReplaceUndoActionItems(IEnumerable<OperationJournalUndoItem> items)
    {
        UndoActionItems.Clear();
        foreach (var item in items)
        {
            UndoActionItems.Add(new UndoActionItemViewModel(item));
        }

        OnPropertyChanged(nameof(HasUndoActionItems));
    }

    private void RaiseLogStateChanged()
    {
        OnPropertyChanged(nameof(LogLineCountText));
        OnPropertyChanged(nameof(IsLogEmpty));
    }
}
