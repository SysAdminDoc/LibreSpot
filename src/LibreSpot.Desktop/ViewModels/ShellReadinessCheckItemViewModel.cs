namespace LibreSpot.Desktop.ViewModels;

public sealed class ShellReadinessCheckItemViewModel(
    string label,
    string status,
    string tone,
    bool isPassing)
{
    public string Label { get; } = label;
    public string Status { get; } = status;
    public string Tone { get; } = tone;
    public bool IsPassing { get; } = isPassing;
    public string AutomationText => $"{Label}: {Status}";
}
