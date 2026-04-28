using System.IO;
using System.Diagnostics;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public sealed class EnvironmentSnapshotService
{
    private readonly Func<bool> _autoReapplyTaskProbe;

    public EnvironmentSnapshotService(Func<bool>? autoReapplyTaskProbe = null)
    {
        _autoReapplyTaskProbe = autoReapplyTaskProbe ?? IsAutoReapplyTaskRegistered;
    }

    public EnvironmentSnapshot GetSnapshot(string configPath)
    {
        var spotifyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe");
        var spicetifyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "spicetify", "spicetify.exe");
        var configDirectory = ResolveConfigDirectory(configPath);

        return new EnvironmentSnapshot
        {
            SpotifyInstalled = File.Exists(spotifyPath),
            SpicetifyInstalled = File.Exists(spicetifyPath),
            SavedConfigExists = File.Exists(configPath),
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
