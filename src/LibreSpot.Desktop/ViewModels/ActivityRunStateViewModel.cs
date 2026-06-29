using System.Collections.ObjectModel;
using LibreSpot.Desktop.Properties;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed class ActivityRunStateViewModel : ObservableObject
{
    private const int MaxLogEntries = 2000;

    private bool _isVisible;
    private bool _isRunning;
    private bool _isCancelRequested;
    private double _progressValue;
    private string _title = Strings.ActivityReady;
    private string _status = Strings.ActivityPickPath;
    private string _step = "Idle";

    public ActivityRunStateViewModel()
    {
        LogEntries = new ObservableCollection<LogEntryViewModel>();
        UndoActionItems = new ObservableCollection<UndoActionItemViewModel>();
    }

    public ObservableCollection<LogEntryViewModel> LogEntries { get; }
    public ObservableCollection<UndoActionItemViewModel> UndoActionItems { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public bool IsCancelRequested
    {
        get => _isCancelRequested;
        set => SetProperty(ref _isCancelRequested, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Step
    {
        get => _step;
        set => SetProperty(ref _step, value);
    }

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
        RaisePropertyChanged(nameof(HasUndoActionItems));
    }

    public void ReplaceUndoActionItems(IEnumerable<OperationJournalUndoItem> items)
    {
        UndoActionItems.Clear();
        foreach (var item in items)
        {
            UndoActionItems.Add(new UndoActionItemViewModel(item));
        }

        RaisePropertyChanged(nameof(HasUndoActionItems));
    }

    private void RaiseLogStateChanged()
    {
        RaisePropertyChanged(nameof(LogLineCountText));
        RaisePropertyChanged(nameof(IsLogEmpty));
    }
}
