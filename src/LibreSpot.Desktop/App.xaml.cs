using System.Windows;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CrashReporter.Initialize();
        ThemeManager.Initialize(this);
        base.OnStartup(e);
    }
}
