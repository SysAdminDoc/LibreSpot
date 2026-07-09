using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace LibreSpot.Desktop.Services;

public sealed record BackendMessage(string Kind, string Level, string Payload);
public sealed record BackendRunResult(bool Success, string? ErrorMessage = null, bool Canceled = false, string? ErrorCode = null, int? ExitCode = null);

internal sealed record BackendWatchdogOptions(
    TimeSpan IdleWarningAfter,
    TimeSpan StallTimeoutAfter,
    TimeSpan PollInterval)
{
    public static BackendWatchdogOptions Default { get; } = new(
        TimeSpan.FromSeconds(20),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(5));
}

public sealed class BackendScriptService
{
    private const string ResourceName = "LibreSpot.Desktop.Backend.LibreSpot.Backend.ps1";
    private const string Prefix = "@@LS@@|";
    private static readonly SemaphoreSlim RuntimeScriptLock = new(1, 1);

    // Validated backend action names. Anything else is rejected before it reaches the
    // shell so a caller mistake can't turn into a "powershell.exe -Action $malicious" call.
    private static readonly HashSet<string> AllowedActions = new(StringComparer.Ordinal)
    {
        "Install", "CheckUpdates", "Reapply", "RepairMarketplace", "OpenMarketplace", "SafeMode", "CreateBackup", "RestoreBackup", "RestoreVanilla", "UninstallSpicetify", "FullReset", "RemoveSelfData", "ClearCache",
        "EnableAutoReapply", "DisableAutoReapply", "WatchAutoReapply", "Plan"
    };

    private readonly string _runtimeDirectory;
    private readonly bool _noBackendMode;
    private readonly BackendWatchdogOptions _watchdogOptions;
    private readonly string? _backendScriptPathOverride;

    public BackendScriptService(string? runtimeDirectory = null, bool noBackendMode = false)
        : this(runtimeDirectory, noBackendMode, BackendWatchdogOptions.Default, backendScriptPathOverride: null)
    {
    }

    internal BackendScriptService(
        string? runtimeDirectory,
        bool noBackendMode,
        BackendWatchdogOptions watchdogOptions,
        string? backendScriptPathOverride)
    {
        _runtimeDirectory = string.IsNullOrWhiteSpace(runtimeDirectory)
            ? DefaultRuntimeDirectory
            : Path.GetFullPath(runtimeDirectory);
        _noBackendMode = noBackendMode;
        _watchdogOptions = ValidateWatchdogOptions(watchdogOptions);
        _backendScriptPathOverride = string.IsNullOrWhiteSpace(backendScriptPathOverride)
            ? null
            : Path.GetFullPath(backendScriptPathOverride);
    }

    public static string DefaultRuntimeDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibreSpot", "Runtime");

    private string RuntimeDirectory => _runtimeDirectory;

    public async Task<BackendRunResult> RunAsync(string action, string configPath, Action<BackendMessage> onMessage, CancellationToken cancellationToken = default)
    {
        if (!AllowedActions.Contains(action))
        {
            return new BackendRunResult(false, $"Unknown backend action '{action}'.");
        }

        if (string.IsNullOrWhiteSpace(configPath))
        {
            return new BackendRunResult(false, "LibreSpot could not resolve the configuration path.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return new BackendRunResult(false, "LibreSpot canceled the backend run.", Canceled: true);
        }

        if (_noBackendMode)
        {
            onMessage(new BackendMessage("status", "INFO", "UI automation no-backend mode"));
            onMessage(new BackendMessage("step", "INFO", $"Skipped backend action: {action}"));
            onMessage(new BackendMessage("progress", "INFO", "100"));
            return new BackendRunResult(true);
        }

        try
        {
            EnsureHardenedRuntimeDirectory();
        }
        catch (Exception ex)
        {
            return new BackendRunResult(false, $"LibreSpot could not prepare the backend runtime folder: {ex.Message}");
        }

        Exception? messageDeliveryException = null;
        var messageDeliveryLock = new object();

        void Notify(BackendMessage message)
        {
            try
            {
                onMessage(message);
            }
            catch (Exception ex)
            {
                lock (messageDeliveryLock)
                {
                    messageDeliveryException ??= ex;
                }
            }
        }

        string scriptPath;
        string? executionCopy = null;
        FileStream? executionCopyGuard = null;
        try
        {
            var (canonicalPath, expectedHash) = await EnsureBackendScriptAsync(cancellationToken);
            executionCopy = Path.Combine(RuntimeDirectory, $"LibreSpot.Backend.{Guid.NewGuid():N}.run.ps1");
            await using var canonicalStream = File.Open(canonicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            executionCopyGuard = new FileStream(
                executionCopy,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read);
            await canonicalStream.CopyToAsync(executionCopyGuard, cancellationToken);
            await executionCopyGuard.FlushAsync(cancellationToken);

            var copyHash = ComputeHash(executionCopyGuard);
            if (copyHash != expectedHash)
            {
                throw new InvalidOperationException("Execution copy hash mismatch — the runtime file was modified between extraction and copy.");
            }

            // Release the guard before launching PowerShell. The hash is verified;
            // holding the stream open with FileShare.Read blocks PS 5.1 from opening
            // the script (it requests broader sharing than Read-only).
            executionCopyGuard.Dispose();
            executionCopyGuard = null;
            scriptPath = executionCopy;
        }
        catch (OperationCanceledException)
        {
            executionCopyGuard?.Dispose();
            TryDeleteExecutionCopy(executionCopy);
            return new BackendRunResult(false, "LibreSpot canceled the backend run.", Canceled: true);
        }
        catch (Exception ex)
        {
            executionCopyGuard?.Dispose();
            TryDeleteExecutionCopy(executionCopy);
            return new BackendRunResult(false, $"LibreSpot could not prepare the backend runtime: {ex.Message}");
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetPowerShellPath(),
                WorkingDirectory = RuntimeDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };

        // Prefer the ArgumentList collection (.NET 6+) so each value is individually quoted.
        // This avoids any chance of argument-smuggling via a weird username in configPath.
        var args = process.StartInfo.ArgumentList;
        args.Add("-NoProfile");
        args.Add("-ExecutionPolicy");
        args.Add("Bypass");
        args.Add("-File");
        args.Add(scriptPath);
        args.Add("-Action");
        args.Add(action);
        args.Add("-ConfigPath");
        args.Add(configPath);

        var watchdogState = new BackendWatchdogRunState();
        var lastBackendActivityTicks = Stopwatch.GetTimestamp();

        void MarkBackendActivity() =>
            Interlocked.Exchange(ref lastBackendActivityTicks, Stopwatch.GetTimestamp());

        long ReadLastBackendActivityTicks() =>
            Interlocked.Read(ref lastBackendActivityTicks);

        process.OutputDataReceived += (_, ev) =>
        {
            if (!string.IsNullOrWhiteSpace(ev.Data))
            {
                MarkBackendActivity();
                Publish(ev.Data, Notify);
            }
        };

        process.ErrorDataReceived += (_, ev) =>
        {
            if (!string.IsNullOrWhiteSpace(ev.Data))
            {
                MarkBackendActivity();
                Notify(new BackendMessage("log", "WARN", ev.Data));
            }
        };

        using var registration = cancellationToken.Register(() => TryKillTree(process));
        using var watchdogStopCts = new CancellationTokenSource();
        using var watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, watchdogStopCts.Token);

        try
        {
            process.Start();
            MarkBackendActivity();
        }
        catch (Exception ex)
        {
            executionCopyGuard?.Dispose();
            TryDeleteExecutionCopy(executionCopy);
            return new BackendRunResult(false, $"LibreSpot could not start the backend runtime: {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var watchdogTask = MonitorBackendLivenessAsync(
            process,
            Notify,
            ReadLastBackendActivityTicks,
            _watchdogOptions,
            watchdogState,
            watchdogCts.Token);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKillTree(process, waitForExit: true);
            try { await Task.Run(process.WaitForExit, CancellationToken.None); } catch { }
            executionCopyGuard?.Dispose();
            TryDeleteExecutionCopy(executionCopy);
            return new BackendRunResult(false, "LibreSpot canceled the backend run.", Canceled: true);
        }
        finally
        {
            watchdogStopCts.Cancel();
            await ObserveWatchdogTaskAsync(watchdogTask);
        }

        // Give the async output pumps a final drain so we don't drop the last few lines.
        try { await Task.Run(process.WaitForExit, CancellationToken.None); } catch { }

        lock (messageDeliveryLock)
        {
            if (messageDeliveryException is not null)
            {
                executionCopyGuard?.Dispose();
                TryDeleteExecutionCopy(executionCopy);
                return new BackendRunResult(false, $"LibreSpot could not update the desktop shell while the backend was running: {messageDeliveryException.Message}");
            }
        }

        executionCopyGuard?.Dispose();
        TryDeleteExecutionCopy(executionCopy);

        if (watchdogState.KilledForStall)
        {
            return new BackendRunResult(
                false,
                $"LibreSpot backend host watchdog stopped the run after {FormatDuration(watchdogState.IdleDurationAtKill)} with no backend output.",
                ErrorCode: "BackendHostStalled");
        }

        return process.ExitCode switch
        {
            0 => new BackendRunResult(true),
            3010 or 1641 => new BackendRunResult(true, ExitCode: process.ExitCode),
            _ => new BackendRunResult(false, $"LibreSpot backend exited with code {process.ExitCode}.", ExitCode: process.ExitCode)
        };
    }

    private static BackendWatchdogOptions ValidateWatchdogOptions(BackendWatchdogOptions options)
    {
        if (options.IdleWarningAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Idle warning budget must be greater than zero.");
        }

        if (options.StallTimeoutAfter <= options.IdleWarningAfter)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Stall timeout budget must be greater than the idle warning budget.");
        }

        if (options.PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Watchdog poll interval must be greater than zero.");
        }

        return options;
    }

    private static async Task MonitorBackendLivenessAsync(
        Process process,
        Action<BackendMessage> notify,
        Func<long> readLastBackendActivityTicks,
        BackendWatchdogOptions options,
        BackendWatchdogRunState state,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (ProcessHasExited(process))
            {
                return;
            }

            var idleFor = GetElapsed(readLastBackendActivityTicks());
            if (!state.WarningPublished && idleFor >= options.IdleWarningAfter)
            {
                state.WarningPublished = true;
                notify(new BackendMessage("status", "WARN", "Still waiting for backend output..."));
                notify(new BackendMessage(
                    "log",
                    "WARN",
                    $"No backend output for {FormatDuration(idleFor)}; LibreSpot is still monitoring the run."));
            }

            if (idleFor < options.StallTimeoutAfter)
            {
                continue;
            }

            state.KilledForStall = true;
            state.IdleDurationAtKill = idleFor;
            notify(new BackendMessage(
                "log",
                "ERROR",
                $"Backend host watchdog stopped the run after {FormatDuration(idleFor)} with no backend output."));
            TryKillTree(process, waitForExit: true);
            return;
        }
    }

    private static bool ProcessHasExited(Process process)
    {
        try { return process.HasExited; }
        catch { return true; }
    }

    private static TimeSpan GetElapsed(long startedAtTicks) =>
        TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - startedAtTicks) / (double)Stopwatch.Frequency);

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.TotalMinutes:F1} minutes";
        }

        return $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))} seconds";
    }

    private static async Task ObserveWatchdogTaskAsync(Task watchdogTask)
    {
        try { await watchdogTask; }
        catch (OperationCanceledException) { }
    }

    private sealed class BackendWatchdogRunState
    {
        public bool WarningPublished { get; set; }
        public bool KilledForStall { get; set; }
        public TimeSpan IdleDurationAtKill { get; set; }
    }

    private static void TryKillTree(Process process, bool waitForExit = false)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (waitForExit)
                {
                    process.WaitForExit(5000);
                }
            }
        }
        catch
        {
            // Process may already be gone. Nothing to do.
        }
    }

    private static void Publish(string line, Action<BackendMessage> onMessage)
    {
        if (!line.StartsWith(Prefix, StringComparison.Ordinal))
        {
            onMessage(new BackendMessage("log", "INFO", line));
            return;
        }

        var parts = line.Split('|', 4, StringSplitOptions.None);
        if (parts.Length < 4)
        {
            onMessage(new BackendMessage("log", "INFO", line));
            return;
        }

        onMessage(new BackendMessage(parts[1], parts[2], parts[3]));
    }

    private static string GetPowerShellPath()
    {
        var systemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        return File.Exists(systemPath) ? systemPath : "powershell.exe";
    }

    private async Task<(string Path, string Hash)> EnsureBackendScriptAsync(CancellationToken cancellationToken)
    {
        var destination = Path.Combine(RuntimeDirectory, "LibreSpot.Backend.ps1");
        var hashSidecar = Path.Combine(RuntimeDirectory, "LibreSpot.Backend.ps1.sha256");
        var tempPath = Path.Combine(RuntimeDirectory, $"LibreSpot.Backend.{Guid.NewGuid():N}.tmp");

        await RuntimeScriptLock.WaitAsync(cancellationToken);
        try
        {
            await using var resourceStream = OpenBackendScriptStream();

            var expectedHash = ComputeHash(resourceStream);
            resourceStream.Position = 0;

            if (File.Exists(destination))
            {
                try
                {
                    await using var existing = File.Open(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (ComputeHash(existing) == expectedHash)
                    {
                        WriteSidecarHash(hashSidecar, expectedHash);
                        return (destination, expectedHash);
                    }
                }
                catch
                {
                }
            }

            try
            {
                await using (var fileStream = File.Create(tempPath))
                {
                    await resourceStream.CopyToAsync(fileStream, cancellationToken);
                    await fileStream.FlushAsync(cancellationToken);
                }

                File.Move(tempPath, destination, overwrite: true);
                WriteSidecarHash(hashSidecar, expectedHash);
                return (destination, expectedHash);
            }
            catch
            {
                try { File.Delete(tempPath); } catch { }
                throw;
            }
        }
        finally
        {
            RuntimeScriptLock.Release();
        }
    }

    private Stream OpenBackendScriptStream()
    {
        if (_backendScriptPathOverride is not null)
        {
            return File.Open(_backendScriptPathOverride, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        return Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("LibreSpot backend resource was not found.");
    }

    private static string ComputeHash(Stream stream)
    {
        stream.Position = 0;
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(stream);
        return Convert.ToHexString(bytes);
    }

    private void EnsureHardenedRuntimeDirectory()
    {
        var dirInfo = new DirectoryInfo(RuntimeDirectory);
        if (!dirInfo.Exists)
        {
            dirInfo.Create();
        }

        try
        {
            var security = dirInfo.GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            var rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier));
            foreach (FileSystemAccessRule rule in rules)
            {
                security.RemoveAccessRule(rule);
            }

            var currentUser = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("Could not resolve the current user SID.");
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                adminsSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dirInfo.SetAccessControl(security);
        }
        catch (UnauthorizedAccessException)
        {
            // Non-elevated runs may not be able to rewrite ACLs; the directory
            // still exists and is usable under inherited permissions.
        }
    }

    private static void WriteSidecarHash(string path, string hash)
    {
        try { File.WriteAllText(path, hash, Encoding.UTF8); } catch { }
    }

    private static void TryDeleteExecutionCopy(string? path)
    {
        if (path is null) return;
        try { File.Delete(path); } catch { }
    }

    public static void CleanStaleExecutionCopies(string runtimeDirectory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(runtimeDirectory, "LibreSpot.Backend.*.run.ps1"))
            {
                try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }

    public static void CleanStaleExecutionCopies() => CleanStaleExecutionCopies(DefaultRuntimeDirectory);
}
