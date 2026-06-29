using System.Windows;
using LibreSpot.Desktop.Services;
using Application = System.Windows.Application;

namespace LibreSpot.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CrashReporter.Initialize();
        BackendScriptService.CleanStaleExecutionCopies();
        ThemeManager.Initialize(this);
        base.OnStartup(e);
        ShellIntegrationService.RegisterCurrentUserShellHooksIfPossible();
        ShellIntegrationService.ConfigureJumpListIfPossible();
    }
}
