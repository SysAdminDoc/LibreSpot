using System.Windows.Input;

namespace LibreSpot.Desktop.ViewModels;

public sealed class HealthIssueActionViewModel
{
    public HealthIssueActionViewModel(string action, string buttonText, string description, bool isDestructive, ICommand command)
    {
        Action = action;
        ButtonText = buttonText;
        Description = description;
        IsDestructive = isDestructive;
        Command = command;
    }

    public string Action { get; }
    public string ButtonText { get; }
    public string Description { get; }
    public bool IsDestructive { get; }
    public ICommand Command { get; }
}
