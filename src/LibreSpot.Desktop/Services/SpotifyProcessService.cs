using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace LibreSpot.Desktop.Services;

public sealed record SpotifyRestartResult(bool Reopened, string Message, IReadOnlyList<SpotifyShutdownEvent>? ShutdownEvents = null);
public sealed record SpotifyOpenResult(bool Opened, string Message);

public interface ISpotifyProcessService
{
    Task<SpotifyOpenResult> OpenAsync(string? preferredSpotifyPath, CancellationToken cancellationToken);
    Task<SpotifyRestartResult> RestartAsync(string? preferredSpotifyPath, TimeSpan reopenDelay, CancellationToken cancellationToken);
}

public sealed class SpotifyProcessService : ISpotifyProcessService
{
    public Task<SpotifyOpenResult> OpenAsync(string? preferredSpotifyPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var spotifyPath = ResolveSpotifyPath(preferredSpotifyPath);
        if (spotifyPath is null)
        {
            return Task.FromResult(new SpotifyOpenResult(false, "LibreSpot could not find Spotify.exe to open it."));
        }

        try
        {
            StartThroughShell(spotifyPath);
            return Task.FromResult(new SpotifyOpenResult(true, "Spotify opened without changing the current setup."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new SpotifyOpenResult(false, $"LibreSpot could not open Spotify: {ex.Message}"));
        }
    }

    public async Task<SpotifyRestartResult> RestartAsync(string? preferredSpotifyPath, TimeSpan reopenDelay, CancellationToken cancellationToken)
    {
        var shutdown = await SpotifyProcessShutdown.ShutdownAsync(new LiveSpotifyProcessSource(), cancellationToken);
        await Task.Delay(reopenDelay, cancellationToken);

        var spotifyPath = ResolveSpotifyPath(preferredSpotifyPath);
        if (spotifyPath is null)
        {
            return new SpotifyRestartResult(false, "Spotify was closed, but LibreSpot could not find Spotify.exe to reopen it.", shutdown.Events);
        }

        try
        {
            StartThroughShell(spotifyPath);
        }
        catch (Exception ex)
        {
            return new SpotifyRestartResult(false, $"Spotify was closed, but LibreSpot could not reopen it: {ex.Message}", shutdown.Events);
        }

        var message = shutdown.Errors.Count > 0
            ? $"Spotify was reopened after the run completed. Close warnings: {string.Join("; ", shutdown.Errors)}"
            : shutdown.ForcedCount > 0
                ? $"Spotify was reopened after the run completed. {shutdown.ForcedCount} process(es) did not close normally and had to be forced."
                : "Spotify was closed and reopened after the run completed.";
        return new SpotifyRestartResult(true, message, shutdown.Events);
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

/// <summary>Enumerates the live Spotify process family by name.</summary>
public sealed class LiveSpotifyProcessSource : ISpotifyProcessSource
{
    public IReadOnlyList<ISpotifyProcessHandle> GetRunningProcesses() =>
        SpotifyProcessShutdown.ProcessNames
            .SelectMany(Process.GetProcessesByName)
            .Select(process => (ISpotifyProcessHandle)new LiveSpotifyProcessHandle(process))
            .ToList();
}

/// <summary>Wraps a real <see cref="Process"/> for the shutdown sequence.</summary>
public sealed class LiveSpotifyProcessHandle : ISpotifyProcessHandle
{
    private readonly Process _process;

    public LiveSpotifyProcessHandle(Process process)
    {
        _process = process;
        Id = process.Id;
        Name = process.ProcessName;
    }

    public string Name { get; }

    public int Id { get; }

    public bool HasExited
    {
        get
        {
            try { return _process.HasExited; }
            catch (Win32Exception) { return false; }
        }
    }

    public bool HasMainWindow
    {
        get
        {
            _process.Refresh();
            return _process.MainWindowHandle != IntPtr.Zero;
        }
    }

    public bool RequestClose() => _process.CloseMainWindow();

    public void Kill() => _process.Kill(entireProcessTree: true);

    public void Dispose() => _process.Dispose();
}
