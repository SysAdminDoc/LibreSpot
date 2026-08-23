using System.Windows.Input;

namespace LibreSpot.Desktop.ViewModels;

public enum HomeActionKind
{
    Checking,
    Retry,
    RecommendedSetup,
    OpenSpotify,
    HealthRepair,
    Maintenance,
    NoActionNeeded,
    ReviewNeeded
}

public sealed class HomeActionViewModel
{
    public HomeActionViewModel(
        HomeActionKind kind,
        string actionId,
        string title,
        string body,
        string primaryLabel,
        ICommand? command,
        bool isEnabled,
        string automationName,
        string helpText,
        string tone,
        bool showsDuration)
    {
        Kind = kind;
        ActionId = actionId;
        Title = title;
        Body = body;
        PrimaryLabel = primaryLabel;
        Command = command;
        IsEnabled = isEnabled;
        AutomationName = automationName;
        HelpText = helpText;
        Tone = tone;
        ShowsDuration = showsDuration;
    }

    public HomeActionKind Kind { get; }
    public string ActionId { get; }
    public string Title { get; }
    public string Body { get; }
    public string PrimaryLabel { get; }
    public ICommand? Command { get; }
    public bool HasCommand => Command is not null;
    public bool IsEnabled { get; }
    public string AutomationName { get; }
    public string HelpText { get; }
    public string Tone { get; }
    public bool ShowsDuration { get; }
}
