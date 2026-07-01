using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

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
        var level = (value as string ?? string.Empty).Trim();
        var key = level switch
        {
            _ when level.Equals("ERROR", StringComparison.OrdinalIgnoreCase) || level.Equals("FATAL", StringComparison.OrdinalIgnoreCase) => "DangerBrush",
            _ when level.Equals("WARN", StringComparison.OrdinalIgnoreCase) || level.Equals("WARNING", StringComparison.OrdinalIgnoreCase) => "WarningBrush",
            _ when level.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase) || level.Equals("OK", StringComparison.OrdinalIgnoreCase) || level.Equals("DONE", StringComparison.OrdinalIgnoreCase) => "AccentBrush",
            _ when level.Equals("DEBUG", StringComparison.OrdinalIgnoreCase) || level.Equals("TRACE", StringComparison.OrdinalIgnoreCase) => "SubtleTextBrush",
            _ => "MutedTextBrush"
        };

        return (Application.Current?.TryFindResource(key) as Brush)
               ?? FallbackBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
