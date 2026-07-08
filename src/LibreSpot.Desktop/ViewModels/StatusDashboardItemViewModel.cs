using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.ViewModels;

public sealed class StatusDashboardItemViewModel
{
    public StatusDashboardItemViewModel(string label, string value, string detail, string tone)
    {
        Label = label;
        Value = string.IsNullOrWhiteSpace(value) ? Strings.DashboardUnknownValue : value;
        Detail = detail;
        Tone = tone;
    }

    public string Label { get; }
    public string Value { get; }
    public string Detail { get; }
    public string Tone { get; }
}
