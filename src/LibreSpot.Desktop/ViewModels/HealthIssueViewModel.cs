using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.ViewModels;

public sealed class HealthIssueViewModel
{
    public HealthIssueViewModel(StackHealthComponent component, IReadOnlyList<HealthIssueActionViewModel> actions)
    {
        Component = component;
        Actions = actions;
    }

    public StackHealthComponent Component { get; }
    public IReadOnlyList<HealthIssueActionViewModel> Actions { get; }
    public string Id => Component.Id;
    public string Name => Component.Name;
    public string Status => Component.Status;
    public string Severity => Component.Severity;
    public string? Path => Component.Path;
    public string Evidence => Component.Evidence;
    public string LastChangedDisplay => Component.LastChangedDisplay;
    public string RecommendedActionText => Component.RecommendedActionText;
    public bool HasPath => Component.HasPath;
    public bool HasLastChanged => Component.HasLastChanged;
    public bool HasActions => Actions.Count > 0;
    public bool ShowRecommendedActionText => Component.HasRecommendedActions && !HasActions;
}
