using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Properties;

namespace LibreSpot.Desktop.ViewModels;

public sealed class ThemeGalleryItemViewModel
{
    public ThemeGalleryItemViewModel(string name, IReadOnlyList<string> schemes)
    {
        Name = name;
        Label = Prettify.Label(name);
        Schemes = schemes;
        SchemePreview = string.Join(", ", schemes.Take(4).Select(Prettify.Label));
        SchemeCountText = schemes.Count == 1
            ? ViewModelText.Get("Vm_ThemeGalleryOneScheme")
            : ViewModelText.Format("Vm_ThemeGalleryManySchemesFormat", schemes.Count);
        IsMarketplaceOnly = string.Equals(name, "(None - Marketplace Only)", StringComparison.Ordinal);
        IsCommunity = CommunityThemeNames.Contains(name);
        RequiresThemeJs = ThemesNeedingJs.Contains(name);
        SourceBadge = IsMarketplaceOnly
            ? Strings.ThemeGalleryMarketplaceBadge
            : IsCommunity
                ? Strings.ThemeGalleryCommunityBadge
                : Strings.ThemeGalleryOfficialBadge;
        JsBadge = RequiresThemeJs ? Strings.ThemeGalleryRequiresJsBadge : string.Empty;
        HasJsBadge = RequiresThemeJs;

        var hash = StableHash(name);
        SwatchA = Swatch(hash, 0x2B);
        SwatchB = Swatch(hash >> 4, 0x46);
        SwatchC = Swatch(hash >> 8, 0x61);
    }

    private static readonly HashSet<string> CommunityThemeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Catppuccin",
        "Comfy",
        "Bloom",
        "Lucid",
        "Hazy"
    };

    private static readonly HashSet<string> ThemesNeedingJs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dribbblish",
        "StarryNight",
        "Turntable",
        "Catppuccin",
        "Comfy",
        "Bloom",
        "Lucid",
        "Hazy"
    };

    public string Name { get; }
    public string Label { get; }
    public IReadOnlyList<string> Schemes { get; }
    public string SchemePreview { get; }
    public string SchemeCountText { get; }
    public bool IsMarketplaceOnly { get; }
    public bool IsCommunity { get; }
    public bool RequiresThemeJs { get; }
    public string SourceBadge { get; }
    public string JsBadge { get; }
    public bool HasJsBadge { get; }
    public string SwatchA { get; }
    public string SwatchB { get; }
    public string SwatchC { get; }
    public string AutomationName => $"{Label}, {SchemeCountText}";
    public string AutomationHelpText =>
        IsMarketplaceOnly
            ? ViewModelText.Get("Vm_ThemeGalleryMarketplaceHelp")
            : RequiresThemeJs
                ? ViewModelText.Format("Vm_ThemeGalleryRequiresJsHelpFormat", Label, SchemeCountText)
                : ViewModelText.Format("Vm_ThemeGalleryNoJsHelpFormat", Label, SchemeCountText);

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var text = query.Trim();
        return Label.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || SourceBadge.Contains(text, StringComparison.OrdinalIgnoreCase)
            || JsBadge.Contains(text, StringComparison.OrdinalIgnoreCase)
            || SchemePreview.Contains(text, StringComparison.OrdinalIgnoreCase)
            || Schemes.Any(scheme => scheme.Contains(text, StringComparison.OrdinalIgnoreCase)
                || Prettify.Label(scheme).Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static int StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash ^= char.ToUpperInvariant(c);
            hash *= 16777619u;
        }

        return (int)(hash & 0x7FFFFFFF);
    }

    private static string Swatch(int value, byte floor)
    {
        var r = (byte)(floor + (value & 0x5F));
        var g = (byte)(floor + ((value >> 5) & 0x5F));
        var b = (byte)(floor + ((value >> 10) & 0x5F));
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
