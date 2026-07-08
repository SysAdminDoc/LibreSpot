using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.ViewModels;

public sealed class ShellDependencyRowViewModel
{
    public ShellDependencyRowViewModel(string component, string installed, string recommended, string tone)
    {
        Component = component;
        Installed = string.IsNullOrWhiteSpace(installed) ? Strings.DashboardUnknownValue : installed;
        Recommended = string.IsNullOrWhiteSpace(recommended) ? Strings.DashboardUnknownValue : recommended;
        Tone = string.IsNullOrWhiteSpace(tone) ? HealthSeverity.Info : tone;
    }

    public string Component { get; }
    public string Installed { get; }
    public string Recommended { get; }
    public string Tone { get; }
}
