using System.Windows;
using Application = System.Windows.Application;

namespace LibreSpot.Desktop.Services;

public static class ThemeManager
{
    private const string PaletteSource = "Themes/Palette.xaml";
    private const string HighContrastPaletteSource = "Themes/HighContrastPalette.xaml";
    private static readonly Duration InstantDuration = new(TimeSpan.FromMilliseconds(1));

    public static bool IsHighContrast => SystemParameters.HighContrast;
    public static bool IsMotionReduced => !SystemParameters.ClientAreaAnimation;

    public static void Initialize(Application app, bool forceHighContrast = false)
    {
        ApplyTheme(app, forceHighContrast);
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SystemParameters.HighContrast)
                              or nameof(SystemParameters.ClientAreaAnimation))
            {
                ApplyTheme(app, forceHighContrast);
            }
        };
    }

    private static void ApplyTheme(Application app, bool forceHighContrast)
    {
        var dictionaries = app.Resources.MergedDictionaries;
        var useHighContrast = forceHighContrast || IsHighContrast;
        var targetSource = useHighContrast ? HighContrastPaletteSource : PaletteSource;
        var targetUri = new Uri(targetSource, UriKind.Relative);

        var paletteIndex = -1;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source;
            if (source is not null &&
                (source.OriginalString.EndsWith(PaletteSource, StringComparison.OrdinalIgnoreCase) ||
                 source.OriginalString.EndsWith(HighContrastPaletteSource, StringComparison.OrdinalIgnoreCase)))
            {
                paletteIndex = i;
                break;
            }
        }

        if (paletteIndex >= 0)
        {
            dictionaries[paletteIndex] = new ResourceDictionary { Source = targetUri };
        }

        if (IsMotionReduced && !useHighContrast)
        {
            ApplyReducedMotion(app);
        }
        else
        {
            ClearReducedMotionOverrides(app);
        }
    }

    private static void ApplyReducedMotion(Application app)
    {
        app.Resources["MotionFastDuration"] = InstantDuration;
        app.Resources["MotionMedDuration"] = InstantDuration;
        app.Resources["MotionSlowDuration"] = InstantDuration;
        app.Resources["IndeterminateSweepDuration"] = InstantDuration;
        app.Resources["MotionFast"] = 0.0;
        app.Resources["MotionMed"] = 0.0;
        app.Resources["MotionSlow"] = 0.0;
    }

    private static void ClearReducedMotionOverrides(Application app)
    {
        string[] motionKeys =
        [
            "MotionFast", "MotionMed", "MotionSlow",
            "MotionFastDuration", "MotionMedDuration", "MotionSlowDuration",
            "IndeterminateSweepDuration"
        ];
        foreach (var key in motionKeys)
        {
            if (app.Resources.Contains(key))
            {
                app.Resources.Remove(key);
            }
        }
    }
}
