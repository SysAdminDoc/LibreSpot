using System.Diagnostics;

namespace LibreSpot.Desktop.Services;

/// <summary>
/// One running Spotify process as the shutdown sequence sees it. The live
/// implementation wraps <see cref="Process"/>; tests supply fakes.
/// </summary>
public interface ISpotifyProcessHandle : IDisposable
{
    string Name { get; }
    int Id { get; }
    bool HasExited { get; }
    bool HasMainWindow { get; }

    /// <summary>Sends the normal window close request. True when it was delivered.</summary>
    bool RequestClose();

    /// <summary>Forces the process and its children to exit.</summary>
    void Kill();
}

public interface ISpotifyProcessSource
{
    IReadOnlyList<ISpotifyProcessHandle> GetRunningProcesses();
}

public static class SpotifyShutdownEventKinds
{
    public const string CloseRequested = "close";
    public const string Skipped = "skipped";
    public const string Exited = "exited";
    public const string Forced = "forced";
    public const string Error = "error";
}

public sealed record SpotifyShutdownEvent(string Kind, string Name, int Id, long ElapsedMs, string Reason)
{
    public string Level => Kind is SpotifyShutdownEventKinds.Forced or SpotifyShutdownEventKinds.Error ? "WARN" : "INFO";

    public string Message => $"{Name} (PID {Id}): {Reason} after {ElapsedMs} ms.";
}

public sealed record SpotifyShutdownResult(IReadOnlyList<SpotifyShutdownEvent> Events, long ElapsedMs)
{
    public int ForcedCount => Events.Count(e => e.Kind == SpotifyShutdownEventKinds.Forced);

    public IReadOnlyList<string> Errors =>
        Events.Where(e => e.Kind == SpotifyShutdownEventKinds.Error).Select(e => e.Message).ToList();
}

/// <summary>
/// Closes Spotify the way Windows expects: every process that owns a window is
/// asked to close, the sequence waits a bounded interval, and only the
/// survivors (plus the windowless helpers) are forced. Each decision is
/// recorded with the process name, ID, elapsed time, and reason so the run log
/// can show what happened without touching user data.
/// </summary>
public static class SpotifyProcessShutdown
{
    /// <summary>Spotify gets this long to honor a normal close request before anything is forced.</summary>
    public static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(5);

    /// <summary>Forced processes get this long to disappear before the restart continues.</summary>
    public static readonly TimeSpan ForceWait = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static readonly string[] ProcessNames =
    {
        "Spotify",
        "SpotifyWebHelper",
        "SpotifyMigrator",
        "SpotifyCrashService"
    };

    public static Task<SpotifyShutdownResult> ShutdownAsync(ISpotifyProcessSource source, CancellationToken cancellationToken) =>
        ShutdownAsync(source, CloseWait, ForceWait, PollInterval, cancellationToken);

    public static async Task<SpotifyShutdownResult> ShutdownAsync(
        ISpotifyProcessSource source,
        TimeSpan closeWait,
        TimeSpan forceWait,
        TimeSpan pollInterval,
        CancellationToken cancellationToken)
    {
        var events = new List<SpotifyShutdownEvent>();
        var closeRequested = new HashSet<int>();
        var handles = new List<ISpotifyProcessHandle>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var running = source.GetRunningProcesses();
            handles.AddRange(running);

            foreach (var process in running)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (HasExited(process))
                {
                    events.Add(Event(SpotifyShutdownEventKinds.Exited, process, stopwatch, "already exited before the close request"));
                    continue;
                }

                if (!HasMainWindow(process))
                {
                    events.Add(Event(SpotifyShutdownEventKinds.Skipped, process, stopwatch, "has no main window to receive a close request"));
                    continue;
                }

                try
                {
                    if (process.RequestClose())
                    {
                        closeRequested.Add(process.Id);
                        events.Add(Event(SpotifyShutdownEventKinds.CloseRequested, process, stopwatch, "was asked to close normally"));
                    }
                    else
                    {
                        events.Add(Event(SpotifyShutdownEventKinds.Skipped, process, stopwatch, "refused the close request"));
                    }
                }
                catch (InvalidOperationException)
                {
                    events.Add(Event(SpotifyShutdownEventKinds.Exited, process, stopwatch, "exited while the close request was sent"));
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    events.Add(Event(SpotifyShutdownEventKinds.Skipped, process, stopwatch, $"could not be asked to close ({ex.GetType().Name}: {ex.Message})"));
                }
            }

            if (closeRequested.Count > 0)
            {
                // A window that honors the request takes its helpers and
                // renderer children down with it a moment later, so the whole
                // family gets the bounded wait, not just the windowed process.
                await WaitUntilAsync(
                    () => running.All(HasExited),
                    closeWait,
                    pollInterval,
                    cancellationToken);
            }

            // Re-enumerate so helpers or children that appeared during the wait are covered too.
            var survivors = source.GetRunningProcesses();
            handles.AddRange(survivors);
            var forced = new List<ISpotifyProcessHandle>();

            foreach (var process in survivors)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (HasExited(process))
                {
                    continue;
                }

                var reason = closeRequested.Contains(process.Id)
                    ? $"did not exit within {closeWait.TotalMilliseconds:0} ms of the close request, forcing it"
                    : HasMainWindow(process)
                        ? "refused the close request, forcing it"
                        : "has no main window to receive a close request, forcing it";

                try
                {
                    process.Kill();
                    forced.Add(process);
                    events.Add(Event(SpotifyShutdownEventKinds.Forced, process, stopwatch, reason));
                }
                catch (InvalidOperationException)
                {
                    events.Add(Event(SpotifyShutdownEventKinds.Exited, process, stopwatch, "exited before it had to be forced"));
                }
                catch (Exception ex)
                {
                    events.Add(Event(SpotifyShutdownEventKinds.Error, process, stopwatch, $"could not be forced ({ex.GetType().Name}: {ex.Message})"));
                }
            }

            if (forced.Count > 0)
            {
                await WaitUntilAsync(() => forced.All(HasExited), forceWait, pollInterval, cancellationToken);
            }
        }
        finally
        {
            foreach (var handle in handles)
            {
                try { handle.Dispose(); } catch { }
            }
        }

        return new SpotifyShutdownResult(events, stopwatch.ElapsedMilliseconds);
    }

    private static async Task WaitUntilAsync(Func<bool> done, TimeSpan limit, TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        var phase = Stopwatch.StartNew();
        while (!done() && phase.Elapsed < limit)
        {
            await Task.Delay(pollInterval, cancellationToken);
        }
    }

    private static bool HasExited(ISpotifyProcessHandle process)
    {
        try { return process.HasExited; }
        catch (InvalidOperationException) { return true; }
    }

    private static bool HasMainWindow(ISpotifyProcessHandle process)
    {
        try { return process.HasMainWindow; }
        catch (InvalidOperationException) { return false; }
    }

    private static SpotifyShutdownEvent Event(string kind, ISpotifyProcessHandle process, Stopwatch stopwatch, string reason) =>
        new(kind, process.Name, process.Id, stopwatch.ElapsedMilliseconds, reason);
}
