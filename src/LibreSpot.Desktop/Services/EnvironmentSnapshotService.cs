using System.IO;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public sealed class EnvironmentSnapshotService
{
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
            ConfigFolderExists = Directory.Exists(configDirectory)
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
}
