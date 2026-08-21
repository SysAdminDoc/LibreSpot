using System.Diagnostics;

namespace LibreSpot.Desktop.Services;

/// <summary>
/// The outcome of a <see cref="ProcessProbe"/> run.
/// </summary>
/// <param name="Exited">The child started and exited within its timeout.</param>
/// <param name="Drained">Both output pipes finished reading within the drain cap.</param>
/// <param name="ExitCode">The child's exit code, or -1 when it never exited.</param>
/// <param name="StandardOutput">The child's stdout, empty unless <paramref name="Drained"/>.</param>
public readonly record struct ProcessProbeResult(bool Exited, bool Drained, int ExitCode, string StandardOutput)
{
    public static ProcessProbeResult Failed => new(false, false, -1, string.Empty);

    /// <summary>The child exited cleanly. Says nothing about the output.</summary>
    public bool Succeeded => Exited && ExitCode == 0;

    /// <summary>The child exited cleanly and <see cref="StandardOutput"/> is complete.</summary>
    public bool HasOutput => Succeeded && Drained;
}

/// <summary>
/// Runs a short read-only child process and collects its output under a bound.
/// A grandchild that inherits the stdout handle keeps the pipe open after the
/// child exits, so the reads get their own cap and the caller is told whether
/// they finished instead of blocking on <c>Task.Result</c>.
/// </summary>
public static class ProcessProbe
{
    private const int DrainTimeoutMilliseconds = 500;
    private const int KillWaitMilliseconds = 500;

    /// <summary>
    /// Starts <paramref name="startInfo"/> with redirection forced on, waits up
    /// to <paramref name="exitTimeoutMilliseconds"/>, and kills the child if it
    /// overruns. Never throws for a child that misbehaves; a failure to start
    /// propagates as it would from <see cref="Process.Start()"/>.
    /// </summary>
    public static ProcessProbeResult Run(ProcessStartInfo startInfo, int exitTimeoutMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return ProcessProbeResult.Failed;
        }

        var stdoutDrain = process.StandardOutput.ReadToEndAsync();
        var stderrDrain = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(exitTimeoutMilliseconds))
        {
            try { process.Kill(); } catch { }
            try { process.WaitForExit(KillWaitMilliseconds); } catch { }
            return ProcessProbeResult.Failed;
        }

        var drained = false;
        try
        {
            drained = Task.WaitAll(new Task[] { stdoutDrain, stderrDrain }, DrainTimeoutMilliseconds);
        }
        catch
        {
            // A faulted read is a failed drain, not a failed probe.
        }

        // Only touch .Result once the read actually completed. If a grandchild
        // inherited the handle the read never finishes, and .Result would block
        // with no bound despite the cap above.
        return new ProcessProbeResult(
            Exited: true,
            Drained: drained,
            ExitCode: process.ExitCode,
            StandardOutput: drained ? stdoutDrain.Result : string.Empty);
    }
}
