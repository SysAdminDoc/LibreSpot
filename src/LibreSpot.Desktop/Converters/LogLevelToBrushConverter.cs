using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace LibreSpot.Desktop.Converters;

/// <summary>
/// Maps a log entry level string (ERROR / WARN / SUCCESS / INFO / …) to an accent brush,
/// sourced from the global palette so every semantic color stays in one place.
/// </summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    private static readonly Brush FallbackBrush = CreateFrozenBrush(Colors.Gray);
    private static Brush CreateFrozenBrush(Color color) { var b = new SolidColorBrush(color); b.Freeze(); return b; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = (value as string ?? string.Empty).Trim().ToUpperInvariant();
        var key = level switch
        {
            "ERROR" or "FATAL" => "DangerBrush",
            "WARN" or "WARNING" => "WarningBrush",
            "SUCCESS" or "OK" or "DONE" => "AccentBrush",
            "DEBUG" or "TRACE" => "SubtleTextBrush",
            _ => "MutedTextBrush"
        };

        return (Application.Current?.TryFindResource(key) as Brush)
               ?? FallbackBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
