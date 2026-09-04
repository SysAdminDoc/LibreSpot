using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    public bool ApplyStoreSelection(string rawUri)
    {
        if (!StoreSelectionService.TryParse(rawUri, out var request) || request is null)
        {
            return false;
        }

        var selected = request.Kind switch
        {
            StoreAssetKind.Theme => SelectStoreTheme(request),
            StoreAssetKind.Extension => SelectStoreAsset(Extensions, request.Id, isApp: false),
            StoreAssetKind.App => SelectStoreAsset(CustomApps, request.Id, isApp: true),
            _ => false
        };
        if (selected)
        {
            SelectedWorkspaceIndex = 1;
        }

        return selected;
    }

    private bool SelectStoreTheme(StoreSelectionRequest request)
    {
        if (!AppCatalog.ThemeSchemes.TryGetValue(request.Id, out var schemes))
        {
            return false;
        }

        SettingsSearchText = string.Empty;
        ThemeSearchText = string.Empty;
        SelectedTheme = request.Id;
        SelectedScheme = request.Scheme is not null && schemes.Contains(request.Scheme, StringComparer.OrdinalIgnoreCase)
            ? schemes.First(scheme => string.Equals(scheme, request.Scheme, StringComparison.OrdinalIgnoreCase))
            : schemes[0];
        ThemeSearchText = request.Id;
        return true;
    }

    private bool SelectStoreAsset(IEnumerable<ExtensionToggleViewModel> assets, string id, bool isApp)
    {
        var asset = assets.FirstOrDefault(candidate => string.Equals(candidate.Key, id, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            return false;
        }

        SettingsSearchText = string.Empty;
        ThemeSearchText = string.Empty;
        asset.IsSelected = true;
        SettingsSearchText = asset.Title;
        if (isApp)
        {
            IsCustomAppsExpanded = true;
        }
        else
        {
            IsExtensionsExpanded = true;
        }

        return true;
    }
}
