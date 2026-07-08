using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.ViewModels;

public sealed class ShellEnvironmentRowViewModel
{
    public ShellEnvironmentRowViewModel(string label, string value, string tone)
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? Strings.DashboardUnknownValue : value;
        Tone = string.IsNullOrWhiteSpace(tone) ? HealthSeverity.Info : tone;
    }

    public string Label { get; }
    public string Value { get; }
    public string Tone { get; }
}
