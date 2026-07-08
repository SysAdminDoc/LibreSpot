using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed class LocalProfileCardViewModel : ObservableObject
{
    public LocalProfileCardViewModel(LocalProfileSummary summary)
    {
        Id = summary.Id;
        Name = summary.Name;
        Description = summary.Description;
        IsBuiltIn = summary.IsBuiltIn;
        IsActive = summary.IsActive;
        UpdatedAt = summary.UpdatedAt;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public bool IsBuiltIn { get; }
    public bool IsActive { get; }
    public DateTimeOffset UpdatedAt { get; }
    public bool IsEditable => !IsBuiltIn;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public string KindBadge => IsBuiltIn ? ViewModelText.Get("Vm_ProfileKindBundled") : ViewModelText.Get("Vm_ProfileKindLocal");
    public string StateBadge => IsActive
        ? ViewModelText.Get("Vm_ProfileStateActive")
        : IsBuiltIn
            ? ViewModelText.Get("Vm_ProfileStateTemplate")
            : ViewModelText.Get("Vm_ProfileStateSaved");
    public string StateTone => IsActive ? "active" : IsBuiltIn ? "template" : "local";
    public string CapabilityText =>
        IsBuiltIn
            ? ViewModelText.Get("Vm_ProfileCapabilityReadOnly")
            : IsActive
                ? ViewModelText.Get("Vm_ProfileCapabilityEditableActive")
                : ViewModelText.Get("Vm_ProfileCapabilityEditable");
    public string UpdatedText => IsBuiltIn
        ? ViewModelText.Get("Vm_ProfileUpdatedBundledTemplate")
        : ViewModelText.Format("Vm_ProfileUpdatedFormat", UpdatedAt.LocalDateTime);
    public string AutomationName => ViewModelText.Format(
        "Vm_ProfileAutomationNameFormat",
        Name,
        IsBuiltIn ? ViewModelText.Get("Vm_ProfileStateTemplate") : ViewModelText.Get("Vm_ProfileKindLocal"));
    public string AutomationHelpText =>
        IsActive
            ? ViewModelText.Format("Vm_ProfileAutomationHelpActiveFormat", Name)
            : IsBuiltIn
                ? ViewModelText.Format("Vm_ProfileAutomationHelpBuiltInFormat", Name)
                : ViewModelText.Format("Vm_ProfileAutomationHelpLocalFormat", Name);

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(KindBadge));
        OnPropertyChanged(nameof(StateBadge));
        OnPropertyChanged(nameof(CapabilityText));
        OnPropertyChanged(nameof(UpdatedText));
        OnPropertyChanged(nameof(AutomationName));
        OnPropertyChanged(nameof(AutomationHelpText));
    }
}
