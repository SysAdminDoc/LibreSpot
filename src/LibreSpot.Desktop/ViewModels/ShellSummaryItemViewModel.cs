using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.ViewModels;

public sealed class ShellSummaryItemViewModel
{
    public ShellSummaryItemViewModel(string label, string value, string detail, string iconKey, string tone)
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? Strings.DashboardUnknownValue : value;
        Detail = detail;
        IconKey = iconKey;
        Tone = string.IsNullOrWhiteSpace(tone) ? HealthSeverity.Info : tone;
    }

    public string Label { get; }
    public string Value { get; }
    public string Detail { get; }
    public string IconKey { get; }
    public string Tone { get; }
}
