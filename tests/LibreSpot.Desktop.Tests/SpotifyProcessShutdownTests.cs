using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class SpotifyProcessShutdownTests
{
    // One table drives every ordering test and the PowerShell parity check.
    // Name, PID, owns a window, accepts the close request, exits after accepting it, already gone.
    private static readonly (string Name, int Id, bool HasWindow, bool AcceptsClose, bool ExitsAfterClose, bool AlreadyExited)[] Table =
    {
        ("Spotify", 1, true, true, true, false),
        ("Spotify", 2, true, true, false, false),
        ("SpotifyWebHelper", 3, false, false, false, false),
        ("Spotify", 4, true, false, false, false),
        ("SpotifyCrashService", 5, false, false, false, true)
    };

    private const string ExpectedSequence =
        "close:Spotify:1,close:Spotify:2,kill:Spotify:2,kill:SpotifyWebHelper:3,kill:Spotify:4";

    private static readonly TimeSpan ShortWait = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(2);

    [Fact]
    public async Task Shutdown_RequestsCloseFirstAndForcesOnlySurvivors()
    {
        var events = new List<string>();
        var source = new FakeSource(Table, events);

        var result = await SpotifyProcessShutdown.ShutdownAsync(source, ShortWait, ShortWait, Poll, CancellationToken.None);

        Assert.Equal(ExpectedSequence, string.Join(",", events));

        var lastClose = events.FindLastIndex(e => e.StartsWith("close:", StringComparison.Ordinal));
        var firstKill = events.FindIndex(e => e.StartsWith("kill:", StringComparison.Ordinal));
        Assert.True(lastClose < firstKill, "Every close request must precede the first forced exit.");

        var forced = result.Events.Where(e => e.Kind == SpotifyShutdownEventKinds.Forced).Select(e => e.Id).ToArray();
        Assert.Equal(new[] { 2, 3, 4 }, forced);
        Assert.DoesNotContain(result.Events, e => e.Kind == SpotifyShutdownEventKinds.Forced && e.Id is 1 or 5);
        Assert.Contains(result.Events, e => e.Kind == SpotifyShutdownEventKinds.Exited && e.Id == 5);
        Assert.Empty(result.Errors);
        Assert.All(source.Handles, handle => Assert.True(handle.DisposeCount >= 1, $"PID {handle.Id} was not disposed."));
    }

    [Theory]
    [InlineData(2, "did not exit within")]
    [InlineData(3, "has no main window")]
    [InlineData(4, "refused the close request")]
    public async Task Shutdown_ExplainsWhyEachSurvivorWasForced(int id, string reasonFragment)
    {
        var source = new FakeSource(Table, new List<string>());

        var result = await SpotifyProcessShutdown.ShutdownAsync(source, ShortWait, ShortWait, Poll, CancellationToken.None);

        var forced = Assert.Single(result.Events, e => e.Kind == SpotifyShutdownEventKinds.Forced && e.Id == id);
        Assert.Contains(reasonFragment, forced.Reason, StringComparison.Ordinal);
        Assert.Equal("WARN", forced.Level);
    }

    [Fact]
    public async Task Shutdown_LogsNameIdElapsedAndReasonOnly()
    {
        var source = new FakeSource(Table, new List<string>());

        var result = await SpotifyProcessShutdown.ShutdownAsync(source, ShortWait, ShortWait, Poll, CancellationToken.None);

        Assert.NotEmpty(result.Events);
        Assert.All(result.Events, e =>
        {
            Assert.Matches(@"^Spotify\w* \(PID \d+\): .+ after \d+ ms\.$", e.Message);
            Assert.Equal(e.Kind is SpotifyShutdownEventKinds.Forced or SpotifyShutdownEventKinds.Error ? "WARN" : "INFO", e.Level);
        });
    }

    [Fact]
    public async Task Shutdown_TotalWaitIsBoundedEvenWhenNothingExits()
    {
        var stubborn = new[] { ("Spotify", 7, true, true, false, false) };
        var source = new FakeSource(stubborn, new List<string>(), survivesKill: true);
        var stopwatch = Stopwatch.StartNew();

        var result = await SpotifyProcessShutdown.ShutdownAsync(source, ShortWait, ShortWait, Poll, CancellationToken.None);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Shutdown took {stopwatch.Elapsed}.");
        Assert.Single(result.Events, e => e.Kind == SpotifyShutdownEventKinds.CloseRequested);
        Assert.Single(result.Events, e => e.Kind == SpotifyShutdownEventKinds.Forced);
    }

    [Fact]
    public async Task Shutdown_CancellationDuringTheCloseWaitForcesNothingAndDisposesHandles()
    {
        var events = new List<string>();
        var source = new FakeSource(new[] { ("Spotify", 9, true, true, false, false) }, events);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SpotifyProcessShutdown.ShutdownAsync(source, TimeSpan.FromSeconds(30), ShortWait, Poll, cancellation.Token));

        Assert.Equal("close:Spotify:9", Assert.Single(events));
        Assert.All(source.Handles, handle => Assert.True(handle.DisposeCount >= 1));
    }

    [Fact]
    public async Task Shutdown_KillFailureIsReportedNotThrown()
    {
        var source = new FakeSource(new[] { ("SpotifyWebHelper", 11, false, false, false, false) }, new List<string>(), killThrows: true);

        var result = await SpotifyProcessShutdown.ShutdownAsync(source, ShortWait, ShortWait, Poll, CancellationToken.None);

        var error = Assert.Single(result.Errors);
        Assert.Contains("PID 11", error, StringComparison.Ordinal);
        Assert.Contains("could not be forced", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shutdown_MatchesThePowerShellPathOnTheSameTable()
    {
        var events = new List<string>();
        await SpotifyProcessShutdown.ShutdownAsync(new FakeSource(Table, events), ShortWait, ShortWait, Poll, CancellationToken.None);
        var desktopSequence = string.Join(",", events);

        var powerShellSequence = await RunPowerShellPathAsync();

        Assert.Equal(ExpectedSequence, desktopSequence);
        Assert.Equal(desktopSequence, powerShellSequence);
    }

    private static async Task<string> RunPowerShellPathAsync()
    {
        var script = new StringBuilder();
        script.AppendLine("$ErrorActionPreference = 'Stop'");
        script.AppendLine($". '{Path.Combine(RepoRoot, "src", "powershell", "shared", "Stop-SpotifyProcesses.ps1")}'");
        script.AppendLine("$script:events = New-Object System.Collections.Generic.List[string]");
        script.AppendLine("$script:enumerations = 0");
        script.AppendLine(@"
function New-FakeProcess {
    param([string]$Name, [int]$Id, [bool]$HasWindow, [bool]$AcceptsClose, [bool]$ExitsAfterClose, [bool]$AlreadyExited)
    $handle = if ($HasWindow) { [IntPtr]1 } else { [IntPtr]::Zero }
    $fake = [pscustomobject]@{ ProcessName = $Name; Id = $Id; MainWindowHandle = $handle; AcceptsClose = $AcceptsClose; ExitsAfterClose = $ExitsAfterClose; Exited = $AlreadyExited }
    $fake | Add-Member -MemberType ScriptProperty -Name HasExited -Value { $this.Exited }
    $fake | Add-Member -MemberType ScriptMethod -Name CloseMainWindow -Value {
        if (-not $this.AcceptsClose) { return $false }
        $script:events.Add(""close:$($this.ProcessName):$($this.Id)"")
        if ($this.ExitsAfterClose) { $this.Exited = $true }
        return $true
    }
    $fake
}
function Get-Process { [CmdletBinding()] param([string[]]$Name)
    $script:enumerations++
    if ($script:enumerations -eq 1) { return $script:table }
    $script:table | Where-Object { -not $_.Exited }
}
function Stop-Process { [CmdletBinding()] param([int]$Id, [switch]$Force)
    $target = $script:table | Where-Object { $_.Id -eq $Id }
    $script:events.Add(""kill:$($target.ProcessName):$Id"")
    $target.Exited = $true
}
function Write-Log { param([string]$Message, [string]$Level = 'INFO') }
function Start-Sleep { param([int]$Seconds, [int]$Milliseconds) }
");
        script.AppendLine("$script:table = @(");
        foreach (var row in Table)
        {
            script.AppendLine($"    (New-FakeProcess -Name '{row.Name}' -Id {row.Id} -HasWindow ${row.HasWindow} -AcceptsClose ${row.AcceptsClose} -ExitsAfterClose ${row.ExitsAfterClose} -AlreadyExited ${row.AlreadyExited})");
        }
        script.AppendLine(")");
        script.AppendLine("Stop-SpotifyProcesses -MaxAttempts 3 -RetryDelay 0 -CloseWaitMs 40 -PollIntervalMs 1");
        script.AppendLine("Write-Output ($script:events -join ',')");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"librespot-shutdown-parity-{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, script.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        try
        {
            var start = new ProcessStartInfo("pwsh")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-NoLogo");
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(scriptPath);

            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start pwsh.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var error = await stderr;
            Assert.True(process.ExitCode == 0, $"pwsh exited {process.ExitCode}: {error}");
            return output.Trim();
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    private static string RepoRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
        }
    }

    private sealed class FakeSource : ISpotifyProcessSource
    {
        private int _enumerations;

        public FakeSource(
            IEnumerable<(string Name, int Id, bool HasWindow, bool AcceptsClose, bool ExitsAfterClose, bool AlreadyExited)> rows,
            List<string> events,
            bool survivesKill = false,
            bool killThrows = false)
        {
            Handles = rows.Select(r => new FakeHandle(r.Name, r.Id, r.HasWindow, r.AcceptsClose, r.ExitsAfterClose, r.AlreadyExited, events, survivesKill, killThrows)).ToList();
        }

        public List<FakeHandle> Handles { get; }

        public IReadOnlyList<ISpotifyProcessHandle> GetRunningProcesses()
        {
            // Process.GetProcessesByName never returns an exited process, but the
            // first enumeration deliberately includes one that died a moment later.
            _enumerations++;
            return _enumerations == 1
                ? Handles.Cast<ISpotifyProcessHandle>().ToList()
                : Handles.Where(h => !h.Exited).Cast<ISpotifyProcessHandle>().ToList();
        }
    }

    private sealed class FakeHandle : ISpotifyProcessHandle
    {
        private readonly bool _acceptsClose;
        private readonly bool _exitsAfterClose;
        private readonly bool _survivesKill;
        private readonly bool _killThrows;
        private readonly List<string> _events;

        public FakeHandle(string name, int id, bool hasWindow, bool acceptsClose, bool exitsAfterClose, bool alreadyExited, List<string> events, bool survivesKill, bool killThrows)
        {
            Name = name;
            Id = id;
            HasMainWindow = hasWindow;
            _acceptsClose = acceptsClose;
            _exitsAfterClose = exitsAfterClose;
            Exited = alreadyExited;
            _events = events;
            _survivesKill = survivesKill;
            _killThrows = killThrows;
        }

        public string Name { get; }
        public int Id { get; }
        public bool Exited { get; private set; }
        public bool HasExited => Exited;
        public bool HasMainWindow { get; }
        public int DisposeCount { get; private set; }

        public bool RequestClose()
        {
            if (!_acceptsClose)
            {
                return false;
            }

            _events.Add($"close:{Name}:{Id}");
            if (_exitsAfterClose)
            {
                Exited = true;
            }

            return true;
        }

        public void Kill()
        {
            if (_killThrows)
            {
                throw new System.ComponentModel.Win32Exception(5, "Access is denied.");
            }

            _events.Add($"kill:{Name}:{Id}");
            if (!_survivesKill)
            {
                Exited = true;
            }
        }

        public void Dispose() => DisposeCount++;
    }
}

/// <summary>
/// Drives the real process adapter against a throwaway console window. Opt in
/// with LIBRESPOT_LIVE_PROCESS_SMOKE=1; the window opens minimized so it never
/// takes the foreground.
/// </summary>
public sealed class SpotifyProcessShutdownLiveSmokeTests
{
    [Fact]
    public async Task LiveAdapter_ClosesAWindowedProcessNormallyWithoutForcing()
    {
        if (Environment.GetEnvironmentVariable("LIBRESPOT_LIVE_PROCESS_SMOKE") != "1")
        {
            Assert.Skip("Set LIBRESPOT_LIVE_PROCESS_SMOKE=1 to run the live process-adapter smoke test.");
        }

        using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/k")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        }) ?? throw new InvalidOperationException("Could not start the throwaway console.");

        var deadline = Stopwatch.StartNew();
        while (process.MainWindowHandle == IntPtr.Zero && deadline.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(100);
            process.Refresh();
        }
        Assert.NotEqual(IntPtr.Zero, process.MainWindowHandle);

        try
        {
            var result = await SpotifyProcessShutdown.ShutdownAsync(
                new SingleProcessSource(process),
                SpotifyProcessShutdown.CloseWait,
                SpotifyProcessShutdown.ForceWait,
                SpotifyProcessShutdown.PollInterval,
                CancellationToken.None);

            Assert.Single(result.Events, e => e.Kind == SpotifyShutdownEventKinds.CloseRequested);
            Assert.Equal(0, result.ForcedCount);
            Assert.Empty(result.Errors);
            Assert.True(process.HasExited, "The console window should have honored the close request.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private sealed class SingleProcessSource : ISpotifyProcessSource
    {
        private readonly Process _process;

        public SingleProcessSource(Process process) => _process = process;

        public IReadOnlyList<ISpotifyProcessHandle> GetRunningProcesses() =>
            _process.HasExited
                ? Array.Empty<ISpotifyProcessHandle>()
                : new ISpotifyProcessHandle[] { new LiveSpotifyProcessHandle(Process.GetProcessById(_process.Id)) };
    }
}
