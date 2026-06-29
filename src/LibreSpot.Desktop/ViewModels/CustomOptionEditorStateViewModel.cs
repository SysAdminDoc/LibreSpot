using System.Collections.ObjectModel;
using System.Reflection;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.ViewModels;

public sealed class CustomOptionEditorStateViewModel : ObservableObject
{
    private string _selectedTheme = "(None - Marketplace Only)";
    private string _selectedScheme = "Default";
    private string _selectedLyricsTheme = "spotify";
    private string _selectedSpotifyVersionId = "auto";
    private string _selectedDownloadMethod = string.Empty;
    private string _cacheLimitText = "0";
    private string _themeSearchText = string.Empty;

    public CustomOptionEditorStateViewModel(InstallConfiguration recommendedBaseline)
    {
        ThemeNames = new ObservableCollection<string>(AppCatalog.ThemeSchemes.Keys);
        ThemeGalleryItems = new ObservableCollection<ThemeGalleryItemViewModel>(
            AppCatalog.ThemeSchemes.Select(pair => new ThemeGalleryItemViewModel(pair.Key, pair.Value)));
        SchemeOptions = new ObservableCollection<string>(AppCatalog.ThemeSchemes[_selectedTheme]);
        LyricsThemes = new ObservableCollection<string>(AppCatalog.LyricsThemes);
        SpotifyVersionOptions = new ObservableCollection<AppCatalog.SpotifyVersionEntry>(AppCatalog.SpotifyVersionManifest);
        DownloadMethodOptions = new ObservableCollection<AppCatalog.DownloadMethodEntry>(AppCatalog.DownloadMethods);

        InstallOptions = CreateOptions("Install", recommendedBaseline);
        CoreOptions = CreateOptions("Core", recommendedBaseline);
        InterfaceOptions = CreateOptions("Interface", recommendedBaseline);
        AdvancedOptions = CreateOptions("Advanced", recommendedBaseline);
        ExperienceOptions = CreateOptions("Experience", recommendedBaseline);
        Extensions = new ObservableCollection<ExtensionToggleViewModel>(
            AppCatalog.ExtensionDefinitions.Select(definition => new ExtensionToggleViewModel(
                definition.Key,
                definition.Title,
                definition.Description,
                recommendedBaseline.Spicetify_Extensions.Contains(definition.Key, StringComparer.OrdinalIgnoreCase))));
        CustomApps = new ObservableCollection<ExtensionToggleViewModel>(
            AppCatalog.CustomAppDefinitions.Select(definition => new ExtensionToggleViewModel(
                definition.Key,
                definition.Title,
                definition.Description,
                recommendedBaseline.Spicetify_CustomApps.Contains(definition.Key, StringComparer.OrdinalIgnoreCase))));
    }

    public ObservableCollection<string> ThemeNames { get; }
    public ObservableCollection<ThemeGalleryItemViewModel> ThemeGalleryItems { get; }
    public ObservableCollection<string> SchemeOptions { get; }
    public ObservableCollection<string> LyricsThemes { get; }
    public ObservableCollection<AppCatalog.SpotifyVersionEntry> SpotifyVersionOptions { get; }
    public ObservableCollection<AppCatalog.DownloadMethodEntry> DownloadMethodOptions { get; }
    public ObservableCollection<OptionToggleViewModel> InstallOptions { get; }
    public ObservableCollection<OptionToggleViewModel> CoreOptions { get; }
    public ObservableCollection<OptionToggleViewModel> InterfaceOptions { get; }
    public ObservableCollection<OptionToggleViewModel> AdvancedOptions { get; }
    public ObservableCollection<OptionToggleViewModel> ExperienceOptions { get; }
    public ObservableCollection<ExtensionToggleViewModel> Extensions { get; }
    public ObservableCollection<ExtensionToggleViewModel> CustomApps { get; }

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                RebuildSchemes();
                RaisePropertyChanged(nameof(SelectedThemeGalleryItem));
            }
        }
    }

    public ThemeGalleryItemViewModel? SelectedThemeGalleryItem
    {
        get => ThemeGalleryItems.FirstOrDefault(item => string.Equals(item.Name, SelectedTheme, StringComparison.Ordinal));
        set
        {
            if (value is not null && !string.Equals(value.Name, SelectedTheme, StringComparison.Ordinal))
            {
                SelectedTheme = value.Name;
            }
        }
    }

    public string ThemeSearchText
    {
        get => _themeSearchText;
        set
        {
            if (SetProperty(ref _themeSearchText, value))
            {
                RaiseThemeFilterChanged();
            }
        }
    }

    public IReadOnlyList<ThemeGalleryItemViewModel> FilteredThemeGalleryItems =>
        ThemeGalleryItems.Where(item => item.Matches(ThemeSearchText)).ToArray();

    public bool HasThemeSearchText => !string.IsNullOrWhiteSpace(ThemeSearchText);
    public bool ShowThemeGalleryEmptyState => !FilteredThemeGalleryItems.Any();

    public string ThemeGalleryEmptyText =>
        HasThemeSearchText
            ? Strings.ThemeGalleryNoResults
            : Strings.ThemeGalleryEmpty;

    public string SelectedScheme
    {
        get => _selectedScheme;
        set => SetProperty(ref _selectedScheme, value);
    }

    public string SelectedLyricsTheme
    {
        get => _selectedLyricsTheme;
        set => SetProperty(ref _selectedLyricsTheme, value);
    }

    public string SelectedSpotifyVersionId
    {
        get => _selectedSpotifyVersionId;
        set => SetProperty(ref _selectedSpotifyVersionId, value);
    }

    public string SelectedDownloadMethod
    {
        get => _selectedDownloadMethod;
        set => SetProperty(ref _selectedDownloadMethod, value);
    }

    public string CacheLimitText
    {
        get => _cacheLimitText;
        set => SetProperty(ref _cacheLimitText, value);
    }

    public IEnumerable<OptionToggleViewModel> EnumerateAllOptions() =>
        InstallOptions
            .Concat(CoreOptions)
            .Concat(InterfaceOptions)
            .Concat(AdvancedOptions)
            .Concat(ExperienceOptions);

    public void RefreshLocalizedText()
    {
        foreach (var option in EnumerateAllOptions())
        {
            option.RefreshText(
                Strings.ResourceManager.GetString($"Option_{option.Key}_Title", Strings.Culture) ?? option.Title,
                Strings.ResourceManager.GetString($"Option_{option.Key}_Description", Strings.Culture) ?? option.Description);
        }

        foreach (var extension in Extensions)
        {
            var resourceKey = ToExtensionResourceKey(extension.Key);
            extension.RefreshText(
                Strings.ResourceManager.GetString($"{resourceKey}_Title", Strings.Culture) ?? extension.Title,
                Strings.ResourceManager.GetString($"{resourceKey}_Description", Strings.Culture) ?? extension.Description);
        }

        foreach (var customApp in CustomApps)
        {
            var resourceKey = ToCustomAppResourceKey(customApp.Key);
            customApp.RefreshText(
                Strings.ResourceManager.GetString($"{resourceKey}_Title", Strings.Culture) ?? customApp.Title,
                Strings.ResourceManager.GetString($"{resourceKey}_Description", Strings.Culture) ?? customApp.Description);
        }

        var selectedTheme = SelectedTheme;
        ThemeGalleryItems.Clear();
        foreach (var item in AppCatalog.ThemeSchemes.Select(pair => new ThemeGalleryItemViewModel(pair.Key, pair.Value)))
        {
            ThemeGalleryItems.Add(item);
        }

        SelectedTheme = selectedTheme;
        RaiseThemeFilterChanged();
    }

    private static ObservableCollection<OptionToggleViewModel> CreateOptions(string section, InstallConfiguration recommendedBaseline) =>
        new(AppCatalog.OptionDefinitions
            .Where(definition => definition.Section == section)
            .Select(definition => new OptionToggleViewModel(
                definition.Key,
                definition.Title,
                definition.Description,
                typeof(InstallConfiguration).GetProperty(definition.Key, BindingFlags.Public | BindingFlags.Instance)?.GetValue(recommendedBaseline) is bool value && value)));

    private void RebuildSchemes()
    {
        SchemeOptions.Clear();
        if (AppCatalog.ThemeSchemes.TryGetValue(SelectedTheme, out var schemes))
        {
            foreach (var scheme in schemes)
            {
                SchemeOptions.Add(scheme);
            }
        }

        if (!SchemeOptions.Contains(SelectedScheme))
        {
            SelectedScheme = SchemeOptions.FirstOrDefault() ?? "Default";
        }
    }

    private void RaiseThemeFilterChanged()
    {
        RaisePropertyChanged(nameof(FilteredThemeGalleryItems));
        RaisePropertyChanged(nameof(ThemeGalleryEmptyText));
        RaisePropertyChanged(nameof(ShowThemeGalleryEmptyState));
        RaisePropertyChanged(nameof(HasThemeSearchText));
    }

    private static string ToExtensionResourceKey(string key)
    {
        var name = key
            .Replace(".mjs", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".js", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("+", "_plus", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal);
        return $"Extension_{name}";
    }

    private static string ToCustomAppResourceKey(string key) =>
        $"CustomApp_{key.Replace("-", "_", StringComparison.Ordinal)}";
}
