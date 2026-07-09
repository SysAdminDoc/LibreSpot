using System.Collections.ObjectModel;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.ViewModels;

public sealed class MaintenanceActionsStateViewModel
{
    private readonly IReadOnlyList<MaintenanceActionCardViewModel> _cards;

    public MaintenanceActionsStateViewModel(
        IEnumerable<MaintenanceActionDefinition> definitions,
        Func<MaintenanceActionDefinition, Task> runAsync,
        Func<bool> canRun,
        Action<Exception> onException)
    {
        _cards = definitions
            .Select(definition => new MaintenanceActionCardViewModel(definition, runAsync, canRun, onException))
            .ToArray();

        SafeActions = new ObservableCollection<MaintenanceActionCardViewModel>(_cards.Where(card => !card.IsDestructive));
        DestructiveActions = new ObservableCollection<MaintenanceActionCardViewModel>(_cards.Where(card => card.IsDestructive));
    }

    public ObservableCollection<MaintenanceActionCardViewModel> SafeActions { get; }
    public ObservableCollection<MaintenanceActionCardViewModel> DestructiveActions { get; }

    public MaintenanceActionCardViewModel? Find(string action) =>
        _cards.FirstOrDefault(card => string.Equals(card.Action, action, StringComparison.Ordinal));

    public void RefreshRelevance(Func<string, bool> isRelevant)
    {
        foreach (var card in _cards)
        {
            card.RefreshRelevance(isRelevant(card.Action));
        }
    }

    public void RaiseCanExecuteChanged()
    {
        foreach (var card in _cards)
        {
            card.Command.NotifyCanExecuteChanged();
        }
    }

    public void RefreshLocalizedText()
    {
        foreach (var card in _cards)
        {
            card.RefreshLocalizedText();
        }
    }
}
