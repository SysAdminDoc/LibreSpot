using System.Globalization;
using System.Windows.Data;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Converters;

/// <summary>
/// Display-only converter that humanizes a slug for ComboBox item templates.
/// Raw value still flows through SelectedItem bindings — only the text shown changes.
/// </summary>
public sealed class PrettifyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string slug ? Prettify.Label(slug) : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value;
}
