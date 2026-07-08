using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.ViewModels;

public sealed class MaintenanceActionCardViewModel : ObservableObject
{
    private bool _isRelevant = true;

    public MaintenanceActionCardViewModel(
        MaintenanceActionDefinition definition,
        Func<MaintenanceActionDefinition, Task> runAsync,
        Func<bool> canRun,
        Action<Exception> onException)
    {
        Definition = definition;
        Command = new AsyncRelayCommand(
            async () =>
            {
                try
                {
                    await runAsync(Definition);
                }
                catch (Exception ex)
                {
                    onException(ex);
                }
            },
            () => IsRelevant && canRun());
    }

    public MaintenanceActionDefinition Definition { get; }
    public string Action => Definition.Action;
    public string AutomationId => $"MaintenanceAction_{Definition.Action}";
    public string Title => Definition.Title;
    public string Description => Definition.Description;
    public string ButtonText => Definition.ButtonText;
    public bool IsDestructive => Definition.IsDestructive;
    public IAsyncRelayCommand Command { get; }

    public bool IsRelevant
    {
        get => _isRelevant;
        private set
        {
            if (SetProperty(ref _isRelevant, value))
            {
                Command.NotifyCanExecuteChanged();
            }
        }
    }

    public void RefreshRelevance(bool isRelevant)
    {
        IsRelevant = isRelevant;
        Command.NotifyCanExecuteChanged();
    }
}
