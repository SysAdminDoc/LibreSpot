using System.Globalization;

namespace LibreSpot.Desktop.Models;

/// <summary>
/// The one place Core turns a Spotify build string or an upstream release tag
/// into a <see cref="Version"/>.
///
/// Spotify ships four-component builds (1.2.96.518) but every compatibility
/// statement in this repository is made at three-component precision, so
/// <see cref="TryParse"/> truncates: 1.2.93.400 must still compare equal to a
/// "supported through 1.2.93" claim. Upstream release tags are sorted against
/// each other instead of against a claim, so <see cref="TryParseReleaseTag"/>
/// keeps the fourth component and pads short tags.
/// </summary>
public static class SpotifyVersion
{
    private static readonly char[] PrereleaseSeparators = ['-', '+'];

    /// <summary>
    /// Parses a Spotify version. Accepts an optional <c>v</c> prefix, a
    /// prerelease or build suffix, and a fourth component, and requires at
    /// least three numeric components.
    /// </summary>
    public static bool TryParse(string? value, out Version version) =>
        TryParseComponents(value, prefixToStrip: null, minimumComponents: 3, maximumComponents: 3, out version);

    /// <summary>
    /// Parses an upstream release tag, first removing <paramref name="prefixToStrip"/>
    /// when the tag starts with it. Keeps up to four components and pads
    /// shorter tags to three so they order correctly.
    /// </summary>
    public static bool TryParseReleaseTag(string? value, string? prefixToStrip, out Version version) =>
        TryParseComponents(value, prefixToStrip, minimumComponents: 1, maximumComponents: 4, out version);

    private static bool TryParseComponents(
        string? value,
        string? prefixToStrip,
        int minimumComponents,
        int maximumComponents,
        out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (!string.IsNullOrEmpty(prefixToStrip) &&
            text.StartsWith(prefixToStrip, StringComparison.OrdinalIgnoreCase))
        {
            text = text[prefixToStrip.Length..];
        }

        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var separator = text.IndexOfAny(PrereleaseSeparators);
        if (separator >= 0)
        {
            text = text[..separator];
        }

        var pieces = text.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (pieces.Length < minimumComponents)
        {
            return false;
        }

        var count = Math.Min(pieces.Length, maximumComponents);
        var numbers = new int[Math.Max(count, 3)];
        for (var index = 0; index < count; index++)
        {
            if (!int.TryParse(pieces[index], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return false;
            }
        }

        version = numbers.Length > 3
            ? new Version(numbers[0], numbers[1], numbers[2], numbers[3])
            : new Version(numbers[0], numbers[1], numbers[2]);
        return true;
    }
}
