using System.Diagnostics;
using System.IO;

namespace LibreSpot.Desktop.Services;

public sealed record SpotifyRestartResult(bool Reopened, string Message);

public interface ISpotifyProcessService
{
    Task<SpotifyRestartResult> RestartAsync(string? preferredSpotifyPath, TimeSpan reopenDelay, CancellationToken cancellationToken);
}

public sealed class SpotifyProcessService : ISpotifyProcessService
{
    public async Task<SpotifyRestartResult> RestartAsync(string? preferredSpotifyPath, TimeSpan reopenDelay, CancellationToken cancellationToken)
    {
        var closeErrors = CloseSpotifyProcesses();
        await Task.Delay(reopenDelay, cancellationToken);

        var spotifyPath = ResolveSpotifyPath(preferredSpotifyPath);
        if (spotifyPath is null)
        {
            return new SpotifyRestartResult(false, "Spotify was closed, but LibreSpot could not find Spotify.exe to reopen it.");
        }

        try
        {
            StartThroughShell(spotifyPath);
        }
        catch (Exception ex)
        {
            return new SpotifyRestartResult(false, $"Spotify was closed, but LibreSpot could not reopen it: {ex.Message}");
        }

        var message = closeErrors.Count == 0
            ? "Spotify was closed and reopened after the run completed."
            : $"Spotify was reopened after the run completed. Close warnings: {string.Join("; ", closeErrors)}";
        return new SpotifyRestartResult(true, message);
    }

    private static List<string> CloseSpotifyProcesses()
    {
        var errors = new List<string>();
        foreach (var process in Process.GetProcessesByName("Spotify"))
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch (InvalidOperationException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (Exception ex)
                {
                    errors.Add($"PID {process.Id}: {ex.Message}");
                }
            }
        }

        return errors;
    }

    private static string? ResolveSpotifyPath(string? preferredSpotifyPath)
    {
        foreach (var candidate in BuildSpotifyPathCandidates(preferredSpotifyPath))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildSpotifyPathCandidates(string? preferredSpotifyPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredSpotifyPath))
        {
            string? normalizedPreferredPath = null;
            try
            {
                normalizedPreferredPath = Path.GetFullPath(preferredSpotifyPath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
            {
            }

            if (!string.IsNullOrWhiteSpace(normalizedPreferredPath))
            {
                yield return normalizedPreferredPath;
                yield return Path.Combine(normalizedPreferredPath, "Spotify.exe");
            }
        }

        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Spotify", "Spotify.exe");

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Spotify", "Spotify.exe");
        }
    }

    private static void StartThroughShell(string spotifyPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = QuoteForExplorer(spotifyPath),
            UseShellExecute = true
        })?.Dispose();
    }

    private static string QuoteForExplorer(string path) => '"' + path.Replace("\"", "\\\"", StringComparison.Ordinal) + '"';
}
