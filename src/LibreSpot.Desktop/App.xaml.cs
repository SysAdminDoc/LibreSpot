using System.Windows;
using LibreSpot.Desktop.Services;
using Application = System.Windows.Application;

namespace LibreSpot.Desktop;

public partial class App : Application
{
    private const string UiAutomationCultureArgumentPrefix = "--uia-culture=";
    private const string UiAutomationHighContrastArgument = "--uia-theme=high-contrast";
    private const string UiAutomationReducedMotionArgument = "--uia-reduced-motion";

    protected override void OnStartup(StartupEventArgs e)
    {
        var minidumpLaunch = new MinidumpSettingsService().PrepareLaunch(e.Args);
        if (minidumpLaunch.Relaunched)
        {
            Shutdown(0);
            return;
        }

        if (!string.IsNullOrWhiteSpace(minidumpLaunch.ErrorMessage))
        {
            Console.Error.WriteLine($"LibreSpot minidump bootstrap: {minidumpLaunch.ErrorMessage}");
        }

        CrashReporter.Initialize();
        BackendScriptService.CleanStaleExecutionCopies();
        LocalizationService.Current.ApplyCulture(GetStartupCulture(e.Args));
        ThemeManager.Initialize(
            this,
            forceHighContrast: e.Args.Any(IsUiAutomationHighContrastArgument),
            forceReducedMotion: e.Args.Any(IsUiAutomationReducedMotionArgument));
        base.OnStartup(e);
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

    private static bool IsUiAutomationReducedMotionArgument(string arg) =>
        string.Equals(arg.Trim(), UiAutomationReducedMotionArgument, StringComparison.OrdinalIgnoreCase);
}
