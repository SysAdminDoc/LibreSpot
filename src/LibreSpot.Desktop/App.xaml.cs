using System.Windows;
using LibreSpot.Desktop.Services;
using Application = System.Windows.Application;

namespace LibreSpot.Desktop;

public partial class App : Application
{
    private const string UiAutomationCultureArgumentPrefix = "--uia-culture=";
    private const string UiAutomationHighContrastArgument = "--uia-theme=high-contrast";

    protected override void OnStartup(StartupEventArgs e)
    {
        CrashReporter.Initialize();
        BackendScriptService.CleanStaleExecutionCopies();
        LocalizationService.Current.ApplyCulture(GetStartupCulture(e.Args));
        ThemeManager.Initialize(this, forceHighContrast: e.Args.Any(IsUiAutomationHighContrastArgument));
        base.OnStartup(e);
        ShellIntegrationService.RegisterCurrentUserShellHooksIfPossible();
        ShellIntegrationService.ConfigureJumpListIfPossible();
    }

    private static string GetStartupCulture(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith(UiAutomationCultureArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = arg[UiAutomationCultureArgumentPrefix.Length..].Trim();
            return LocalizationService.NormalizeCultureName(value);
        }

        return LocalizationService.DefaultCultureName;
    }

    private static bool IsUiAutomationHighContrastArgument(string arg) =>
        string.Equals(arg.Trim(), UiAutomationHighContrastArgument, StringComparison.OrdinalIgnoreCase);
}
