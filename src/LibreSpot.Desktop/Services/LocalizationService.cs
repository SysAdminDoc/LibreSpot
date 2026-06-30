using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Windows.Markup;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.Services;

public sealed record LocalizationOption(string CultureName, string EnglishName, string NativeName)
{
    public string DisplayName => string.Equals(EnglishName, NativeName, StringComparison.Ordinal)
        ? EnglishName
        : $"{EnglishName} / {NativeName}";
}

public sealed class LocalizationService : INotifyPropertyChanged
{
    public const string DefaultCultureName = AppCatalog.DefaultUiCulture;

    public static IReadOnlyList<LocalizationOption> SupportedCultures { get; } =
    [
        new(DefaultCultureName, "English", "English"),
        new("ru", "Russian", "Русский"),
        new("zh-Hans", "Chinese (Simplified)", "简体中文"),
        new("pt-BR", "Portuguese (Brazil)", "Português (Brasil)"),
        new("es", "Spanish", "Español")
    ];

    public static LocalizationService Current { get; } = new();

    private CultureInfo _culture = CultureInfo.GetCultureInfo(DefaultCultureName);

    private LocalizationService()
    {
        ApplyCulture(DefaultCultureName);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CultureChanged;

    public CultureInfo Culture => _culture;

    public string CultureName => _culture.Name;

    public string this[string key] => GetString(key);

    public static bool IsSupportedCulture(string? cultureName) =>
        SupportedCultures.Any(option => string.Equals(option.CultureName, cultureName, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeCultureName(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return DefaultCultureName;
        }

        var normalized = SupportedCultures.FirstOrDefault(option =>
            string.Equals(option.CultureName, cultureName.Trim(), StringComparison.OrdinalIgnoreCase));
        return normalized?.CultureName ?? DefaultCultureName;
    }

    public void ApplyCulture(string? cultureName)
    {
        var normalized = NormalizeCultureName(cultureName);
        var culture = CultureInfo.GetCultureInfo(normalized);
        if (string.Equals(_culture.Name, culture.Name, StringComparison.Ordinal))
        {
            SetThreadCulture(culture);
            return;
        }

        _culture = culture;
        Strings.Culture = culture;
        SetThreadCulture(culture);
        OnPropertyChanged(nameof(Culture));
        OnPropertyChanged(nameof(CultureName));
        OnPropertyChanged("Item[]");
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return Strings.ResourceManager.GetString(key, _culture) ?? key;
    }

    private static void SetThreadCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        Strings.Culture = culture;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        var binding = new System.Windows.Data.Binding($"[{Key}]")
        {
            Source = LocalizationService.Current,
            Mode = System.Windows.Data.BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
