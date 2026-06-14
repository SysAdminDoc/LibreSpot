using System.IO;
using System.Diagnostics;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public sealed class EnvironmentSnapshotService
{
    private readonly Func<bool> _autoReapplyTaskProbe;
    private readonly string _spotifyPath;
    private readonly string _spicetifyPath;
    private readonly string _spicetifyConfigDirectory;

    public EnvironmentSnapshotService(
        Func<bool>? autoReapplyTaskProbe = null,
        string? spotifyPath = null,
        string? spicetifyPath = null,
        string? spicetifyConfigDirectory = null)
    {
        _autoReapplyTaskProbe = autoReapplyTaskProbe ?? IsAutoReapplyTaskRegistered;
        _spotifyPath = string.IsNullOrWhiteSpace(spotifyPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe")
            : Path.GetFullPath(spotifyPath);
        _spicetifyPath = string.IsNullOrWhiteSpace(spicetifyPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "spicetify", "spicetify.exe")
            : Path.GetFullPath(spicetifyPath);
        _spicetifyConfigDirectory = string.IsNullOrWhiteSpace(spicetifyConfigDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "spicetify")
            : Path.GetFullPath(spicetifyConfigDirectory);
    }

    public EnvironmentSnapshot GetSnapshot(string configPath)
    {
        var configDirectory = ResolveConfigDirectory(configPath);
        var marketplaceDirectory = Path.Combine(_spicetifyConfigDirectory, "CustomApps", "marketplace");
        var marketplaceFilesPresent =
            File.Exists(Path.Combine(marketplaceDirectory, "extension.js")) &&
            File.Exists(Path.Combine(marketplaceDirectory, "manifest.json"));

        return new EnvironmentSnapshot
        {
            SpotifyInstalled = File.Exists(_spotifyPath),
            SpicetifyInstalled = File.Exists(_spicetifyPath),
            MarketplaceFilesPresent = marketplaceFilesPresent,
            MarketplaceRegistered = IsSpicetifyListEntryEnabled(
                Path.Combine(_spicetifyConfigDirectory, "config-xpui.ini"),
                "custom_apps",
                "marketplace"),
            SavedConfigExists = !string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath),
            ConfigFolderExists = Directory.Exists(configDirectory),
            AutoReapplyTaskRegistered = _autoReapplyTaskProbe()
        };
    }

    private static string ResolveConfigDirectory(string configPath)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(configPath));
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    return directory;
                }
            }
            catch
            {
                // Fall back to the production profile location below.
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibreSpot");
    }

    private static bool IsSpicetifyListEntryEnabled(string configPath, string key, string expectedValue)
    {
        if (!File.Exists(configPath))
        {
            return false;
        }

        try
        {
            foreach (var line in File.ReadLines(configPath))
            {
                var trimmed = line.Trim();
                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex < 0)
                {
                    continue;
                }
                if (!string.Equals(trimmed[..separatorIndex].Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return trimmed[(separatorIndex + 1)..]
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(expectedValue, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsAutoReapplyTaskRegistered()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList =
                {
                    "/Query",
                    "/TN",
                    @"LibreSpot\ReapplyWatcher"
                }
            });

            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(1500))
            {
                try { process.Kill(); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
