using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.ViewModels;

public sealed class PromptStateViewModel : ObservableObject
{
    private bool _isVisible;
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _confirmText = Strings.ButtonContinue;
    private string _cancelText = Strings.ButtonCancel;
    private string _summaryTitle = string.Empty;
    private string _summaryBody = string.Empty;
    private bool _isDestructive;
    private Func<Task>? _confirmAction;

    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (SetProperty(ref _isVisible, value))
            {
                OnPropertyChanged(nameof(IsConfirmDefault));
            }
        }
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Body
    {
        get => _body;
        private set => SetProperty(ref _body, value);
    }

    public string ConfirmText
    {
        get => _confirmText;
        private set => SetProperty(ref _confirmText, value);
    }

    public string CancelText
    {
        get => _cancelText;
        private set => SetProperty(ref _cancelText, value);
    }

    public string SummaryTitle
    {
        get => _summaryTitle;
        private set => SetProperty(ref _summaryTitle, value);
    }

    public string SummaryBody
    {
        get => _summaryBody;
        private set => SetProperty(ref _summaryBody, value);
    }

    public bool IsDestructive
    {
        get => _isDestructive;
        private set
        {
            if (SetProperty(ref _isDestructive, value))
            {
                OnPropertyChanged(nameof(IsConfirmDefault));
            }
        }
    }

    public bool IsConfirmDefault => IsVisible && !IsDestructive;

    public void Show(
        string title,
        string body,
        string confirmText,
        string cancelText,
        bool isDestructive,
        Func<Task> confirmAction,
        string? summaryTitle = null,
        string? summaryBody = null)
    {
        Title = title;
        Body = body;
        ConfirmText = confirmText;
        CancelText = cancelText;
        SummaryTitle = summaryTitle ?? "What happens next";
        SummaryBody = summaryBody ??
            (isDestructive
                ? "LibreSpot will make the requested change and leave the result visible here so you can review it afterward."
                : "LibreSpot will keep the window open, stream progress here, and leave the result easy to review afterward.");
        IsDestructive = isDestructive;
        _confirmAction = confirmAction;
        IsVisible = true;
    }

    public async Task ConfirmAsync()
    {
        var action = _confirmAction;
        Clear();

        if (action is not null)
        {
            await action();
        }
    }

    public void Cancel() => Clear();

    public void Clear()
    {
        IsVisible = false;
        Title = string.Empty;
        Body = string.Empty;
        ConfirmText = Strings.ButtonContinue;
        CancelText = Strings.ButtonCancel;
        SummaryTitle = string.Empty;
        SummaryBody = string.Empty;
        IsDestructive = false;
        _confirmAction = null;
    }
}
