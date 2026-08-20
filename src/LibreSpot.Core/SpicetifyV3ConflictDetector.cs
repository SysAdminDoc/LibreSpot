using System.Collections.ObjectModel;

namespace LibreSpot.Desktop.Models;

public sealed record SpicetifyV3ConflictReport(IReadOnlyList<string> Markers)
{
    public bool IsConflict => Markers.Count > 0;

    public const string SafeAction = "spicetify restore";

    public string RecommendedAction => SafeAction;
}

/// <summary>
/// Detects the concrete filesystem and version markers that make a Spicetify
/// v3-or-newer installation unsafe to process with LibreSpot's pinned 2.x flow.
/// </summary>
public static class SpicetifyV3ConflictDetector
{
    public static SpicetifyV3SupportContractReport EvaluateSupportContract(
        string? cliVersion,
        string? spotifyVersion,
        string? supportContractJson)
    {
        if (!SpicetifyVersionSupport.TryGetMajor(cliVersion, out var major) || major <= SpicetifyVersionSupport.SupportedMajor)
        {
            return new SpicetifyV3SupportContractReport(false, SpicetifySupportResult.NotApplicable());
        }

        if (!SpicetifySupportContract.TryParse(supportContractJson, out var contract, out var error) || contract is null)
        {
            return new SpicetifyV3SupportContractReport(
                true,
                SpicetifySupportResult.Unavailable(
                    spotifyVersion,
                    $"The v3 support contract is unavailable or invalid, so the check remains fail-open. {error}"));
        }

        return new SpicetifyV3SupportContractReport(true, contract.Evaluate(spotifyVersion));
    }

    public static SpicetifyV3ConflictReport Detect(
        string? spotifyPath,
        string? spicetifyInstallDirectory,
        string? spicetifyConfigDirectory,
        string? cliVersion = null)
    {
        var markers = new List<string>();

        var spotifyDirectory = GetDirectoryName(spotifyPath);
        if (!string.IsNullOrWhiteSpace(spotifyDirectory) &&
            File.Exists(Path.Combine(spotifyDirectory, "Apps", "xpui.spa.backup")))
        {
            AddMarker(markers, "Apps\\xpui.spa.backup");
        }

        AddLayoutMarker(markers, spicetifyInstallDirectory, "spicetify install");
        AddLayoutMarker(markers, spicetifyConfigDirectory, "spicetify config");

        if (SpicetifyVersionSupport.TryGetMajor(cliVersion, out var major) &&
            major > SpicetifyVersionSupport.SupportedMajor)
        {
            AddMarker(markers, $"Spicetify CLI major {major}");
        }

        return new SpicetifyV3ConflictReport(
            new ReadOnlyCollection<string>(markers));
    }

    private static void AddLayoutMarker(List<string> markers, string? root, string label)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        foreach (var directoryName in new[] { "modules", "hooks" })
        {
            if (Directory.Exists(Path.Combine(root, directoryName)))
            {
                AddMarker(markers, $"{label}\\{directoryName}");
            }
        }
    }

    private static void AddMarker(List<string> markers, string marker)
    {
        if (!markers.Any(existing => string.Equals(existing, marker, StringComparison.OrdinalIgnoreCase)))
        {
            markers.Add(marker);
        }
    }

    private static string? GetDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(path));
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
