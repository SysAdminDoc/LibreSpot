namespace LibreSpot.Desktop.ViewModels;

public sealed class CompatibilityVerdictItemViewModel
{
    public CompatibilityVerdictItemViewModel(
        string automationId,
        string label,
        string detectedValue,
        string pinnedValue,
        string verdict,
        string nextStep,
        string tone)
    {
        AutomationId = automationId;
        Label = label;
        DetectedValue = detectedValue;
        PinnedValue = pinnedValue;
        Verdict = verdict;
        NextStep = nextStep;
        Tone = tone;
    }

    public string AutomationId { get; }
    public string Label { get; }
    public string DetectedValue { get; }
    public string PinnedValue { get; }
    public string Verdict { get; }
    public string NextStep { get; }
    public string Tone { get; }
}
