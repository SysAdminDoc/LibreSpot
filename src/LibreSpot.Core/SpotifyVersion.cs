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
    private const int MaximumComponents = 4;

    private static readonly char[] PrereleaseSeparators = ['-', '+'];
    private static readonly char[] Whitespace = [' ', '\t', '\r', '\n', '\f', '\v'];

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
        TryParseComponents(value, prefixToStrip, minimumComponents: 1, maximumComponents: MaximumComponents, out version);

    /// <summary>
    /// Reads just the leading major from a version string. Deliberately looser
    /// than <see cref="TryParse"/>: the Spicetify major guard has to fire on a
    /// partial report like <c>3.0</c> or <c>3</c>, where there is no full
    /// version to compare, so it takes the leading digits of the first
    /// component rather than requiring a well-formed version.
    /// </summary>
    public static bool TryParseMajor(string? value, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var digits = new string(Normalize(value, prefixToStrip: null).TakeWhile(char.IsAsciiDigit).ToArray());
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out major);
    }

    private static string Normalize(string value, string? prefixToStrip)
    {
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

        // A prerelease or build suffix ends the version, and so does whitespace:
        // FileVersionInfo hands back free-form vendor text like "1.2.3 rc1".
        var separator = text.IndexOfAny(PrereleaseSeparators);
        var space = text.IndexOfAny(Whitespace);
        if (space >= 0 && (separator < 0 || space < separator))
        {
            separator = space;
        }

        return separator >= 0 ? text[..separator] : text;
    }

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

        var pieces = Normalize(value, prefixToStrip).Split('.', StringSplitOptions.RemoveEmptyEntries);
        // Validity is the same question for both entry points: at most four
        // numeric components, which is all System.Version can hold. They differ
        // only in how much of that they keep.
        if (pieces.Length < minimumComponents || pieces.Length > MaximumComponents)
        {
            return false;
        }

        // Validate every piece before truncating. Checking only the first
        // maximumComponents would accept trailing junk whenever it happened to
        // land past the cap, so the two entry points would disagree about the
        // same string.
        var count = Math.Min(pieces.Length, maximumComponents);
        var numbers = new int[Math.Max(count, 3)];
        for (var index = 0; index < pieces.Length; index++)
        {
            if (!int.TryParse(pieces[index], NumberStyles.None, CultureInfo.InvariantCulture, out var component))
            {
                return false;
            }

            if (index < count)
            {
                numbers[index] = component;
            }
        }

        version = numbers.Length > 3
            ? new Version(numbers[0], numbers[1], numbers[2], numbers[3])
            : new Version(numbers[0], numbers[1], numbers[2]);
        return true;
    }
}
