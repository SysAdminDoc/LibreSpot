using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;

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
                    await runAsync(CreateLocalizedDefinition());
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
    public string Title => LocalizedValue("Title", Definition.Title);
    public string Description => LocalizedValue("Description", Definition.Description);
    public string ButtonText => LocalizedValue("ButtonText", Definition.ButtonText);
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

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ButtonText));
    }

    private MaintenanceActionDefinition CreateLocalizedDefinition() =>
        new(Action, Title, Description, ButtonText, IsDestructive);

    private string LocalizedValue(string suffix, string fallback)
    {
        var key = $"Maintenance_{Action}_{suffix}";
        var value = LocalizationService.Current.GetString(key);
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
