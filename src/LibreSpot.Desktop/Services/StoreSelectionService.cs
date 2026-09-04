using System.Net;

namespace LibreSpot.Desktop.Services;

public enum StoreAssetKind
{
    Theme,
    Extension,
    App
}

public sealed record StoreSelectionRequest(StoreAssetKind Kind, string Id, string? Scheme = null);

public static class StoreSelectionService
{
    public const string StoreUriPrefix = "librespot://store";

    public static bool TryParse(string? rawUri, out StoreSelectionRequest? request)
    {
        request = null;
        if (string.IsNullOrWhiteSpace(rawUri) ||
            !Uri.TryCreate(rawUri.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "librespot", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "store", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/"))
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                return false;
            }

            var key = WebUtility.UrlDecode(pair[..separator]);
            var value = WebUtility.UrlDecode(pair[(separator + 1)..]);
            if (key is not ("kind" or "id" or "scheme") || !values.TryAdd(key, value))
            {
                return false;
            }
        }

        if (!values.TryGetValue("kind", out var rawKind) ||
            !values.TryGetValue("id", out var id) ||
            !TryParseKind(rawKind, out var kind) ||
            !IsSafeValue(id, 200))
        {
            return false;
        }

        values.TryGetValue("scheme", out var scheme);
        if (scheme is not null && (!IsSafeValue(scheme, 100) || kind != StoreAssetKind.Theme))
        {
            return false;
        }

        request = new StoreSelectionRequest(kind, id.Trim(), scheme?.Trim());
        return true;
    }

    private static bool TryParseKind(string value, out StoreAssetKind kind)
    {
        kind = value.Trim().ToLowerInvariant() switch
        {
            "theme" => StoreAssetKind.Theme,
            "extension" => StoreAssetKind.Extension,
            "app" => StoreAssetKind.App,
            _ => (StoreAssetKind)(-1)
        };
        return Enum.IsDefined(kind);
    }

    private static bool IsSafeValue(string value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        !value.Any(char.IsControl);
}
