using System.Diagnostics;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ProcessProbeTests
{
    [Fact]
    public void Run_ReturnsTheDrainedStandardOutputOfASuccessfulChild()
    {
        var probe = ProcessProbe.Run(Cmd("echo present"), exitTimeoutMilliseconds: 5000);

        Assert.True(probe.Exited);
        Assert.True(probe.Drained);
        Assert.Equal(0, probe.ExitCode);
        Assert.True(probe.Succeeded);
        Assert.True(probe.HasOutput);
        Assert.Contains("present", probe.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_ReportsANonZeroExitWithoutClaimingSuccess()
    {
        var probe = ProcessProbe.Run(Cmd("exit 3"), exitTimeoutMilliseconds: 5000);

        Assert.True(probe.Exited);
        Assert.Equal(3, probe.ExitCode);
        Assert.False(probe.Succeeded);
        Assert.False(probe.HasOutput);
    }

    [Fact]
    public void Run_KillsAChildThatOverrunsItsTimeout()
    {
        var stopwatch = Stopwatch.StartNew();
        var probe = ProcessProbe.Run(Cmd("ping -n 30 127.0.0.1 >nul"), exitTimeoutMilliseconds: 700);
        stopwatch.Stop();

        Assert.False(probe.Exited);
        Assert.False(probe.Succeeded);
        Assert.False(probe.HasOutput);
        Assert.Equal(string.Empty, probe.StandardOutput);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(20),
            $"The probe must not wait for the child; it took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Run_ForcesRedirectionOnEvenWhenTheCallerDidNotAskForIt()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = true,
            ArgumentList = { "/c", "echo forced" }
        };

        var probe = ProcessProbe.Run(startInfo, exitTimeoutMilliseconds: 5000);

        Assert.True(probe.HasOutput);
        Assert.Contains("forced", probe.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Failed_DescribesAProbeThatNeverRan()
    {
        var failed = ProcessProbeResult.Failed;

        Assert.False(failed.Exited);
        Assert.False(failed.Drained);
        Assert.False(failed.Succeeded);
        Assert.False(failed.HasOutput);
        Assert.Equal(-1, failed.ExitCode);
        Assert.Equal(string.Empty, failed.StandardOutput);
    }

    [Fact]
    public void Run_ReturnsWithinABoundWhenAGrandchildKeepsTheOutputPipeOpen()
    {
        // The reason the drain is capped at all. cmd exits at once but the ping it
        // detached inherited the stdout handle, so the read never completes. The
        // probe has to give up on the read instead of waiting for the grandchild.
        var stopwatch = Stopwatch.StartNew();
        var probe = ProcessProbe.Run(
            Cmd("start /b ping -n 30 127.0.0.1"),
            exitTimeoutMilliseconds: 2000,
            drainTimeoutMilliseconds: 1000);
        stopwatch.Stop();

        Assert.True(probe.Exited);
        Assert.False(probe.Drained);
        Assert.False(probe.HasOutput);
        Assert.Equal(string.Empty, probe.StandardOutput);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"The probe must abandon the read rather than wait for the grandchild; it took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Run_HonoursACallerSuppliedDrainBudget()
    {
        // Only measurable where the read cannot finish, so this reuses the
        // grandchild. A 20 s exit timeout would default to a 10 s drain; asking
        // for 200 ms has to come back long before that.
        var stopwatch = Stopwatch.StartNew();
        var probe = ProcessProbe.Run(
            Cmd("start /b ping -n 30 127.0.0.1"),
            exitTimeoutMilliseconds: 20000,
            drainTimeoutMilliseconds: 200);
        stopwatch.Stop();

        Assert.True(probe.Exited);
        Assert.False(probe.Drained);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"The caller asked for a 200 ms drain but the probe took {stopwatch.Elapsed}, which is the default budget for this exit timeout.");
    }

    private static ProcessStartInfo Cmd(string command) => new()
    {
        FileName = "cmd.exe",
        ArgumentList = { "/c", command }
    };
}
