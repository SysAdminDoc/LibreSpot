using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

internal static class ViewModelText
{
    public static string Get(string key) => LocalizationService.Current.GetString(key);

    public static string Format(string key, params object?[] args) =>
        string.Format(LocalizationService.Current.Culture, Get(key), args);
}
