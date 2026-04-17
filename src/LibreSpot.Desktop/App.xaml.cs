using System.Windows;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Wire structured logging + crash dump writers *before* any other code runs
        // so we capture failures in initialization and XAML parsing.
        CrashReporter.Initialize();
        base.OnStartup(e);
    }
}
