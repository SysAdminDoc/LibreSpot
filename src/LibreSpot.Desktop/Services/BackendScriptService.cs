using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace LibreSpot.Desktop.Services;

public sealed record BackendMessage(string Kind, string Level, string Payload);
public sealed record BackendRunResult(bool Success, string? ErrorMessage = null);

public sealed class BackendScriptService
{
    private const string ResourceName = "LibreSpot.Desktop.Backend.LibreSpot.Backend.ps1";
    private const string Prefix = "@@LS@@|";
    private static readonly SemaphoreSlim RuntimeScriptLock = new(1, 1);

    // Validated backend action names. Anything else is rejected before it reaches the
    // shell so a caller mistake can't turn into a "powershell.exe -Action $malicious" call.
    private static readonly HashSet<string> AllowedActions = new(StringComparer.Ordinal)
    {
        "Install", "CheckUpdates", "Reapply", "RepairMarketplace", "RestoreVanilla", "UninstallSpicetify", "FullReset",
        "EnableAutoReapply", "DisableAutoReapply", "WatchAutoReapply"
    };

    private readonly string _runtimeDirectory;

    public BackendScriptService(string? runtimeDirectory = null)
    {
        _runtimeDirectory = string.IsNullOrWhiteSpace(runtimeDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibreSpot", "Runtime")
            : Path.GetFullPath(runtimeDirectory);
    }

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
            return new BackendRunResult(false, "LibreSpot canceled the backend run.");
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
        try
        {
            var (canonicalPath, expectedHash) = await EnsureBackendScriptAsync(cancellationToken);
            executionCopy = Path.Combine(RuntimeDirectory, $"LibreSpot.Backend.{Guid.NewGuid():N}.run.ps1");
            File.Copy(canonicalPath, executionCopy, overwrite: true);

            await using var verifyStream = File.Open(executionCopy, FileMode.Open, FileAccess.Read, FileShare.None);
            var copyHash = ComputeHash(verifyStream);
            if (copyHash != expectedHash)
            {
                throw new InvalidOperationException("Execution copy hash mismatch — the runtime file was modified between extraction and copy.");
            }

            scriptPath = executionCopy;
        }
        catch (OperationCanceledException)
        {
            TryDeleteExecutionCopy(executionCopy);
            return new BackendRunResult(false, "LibreSpot canceled the backend run.");
        }
        catch (Exception ex)
        {
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

        process.OutputDataReceived += (_, ev) =>
        {
            if (!string.IsNullOrWhiteSpace(ev.Data))
            {
                Publish(ev.Data, Notify);
            }
        };

        process.ErrorDataReceived += (_, ev) =>
        {
            if (!string.IsNullOrWhiteSpace(ev.Data))
            {
                Notify(new BackendMessage("log", "WARN", ev.Data));
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new BackendRunResult(false, $"LibreSpot could not start the backend runtime: {ex.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() => TryKillTree(process));

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKillTree(process, waitForExit: true);
            return new BackendRunResult(false, "LibreSpot canceled the backend run.");
        }

        // Give the async output pumps a final drain so we don't drop the last few lines.
        try { await Task.Run(process.WaitForExit, CancellationToken.None); } catch { }

        lock (messageDeliveryLock)
        {
            if (messageDeliveryException is not null)
            {
                return new BackendRunResult(false, $"LibreSpot could not update the desktop shell while the backend was running: {messageDeliveryException.Message}");
            }
        }

        TryDeleteExecutionCopy(executionCopy);

        return process.ExitCode == 0
            ? new BackendRunResult(true)
            : new BackendRunResult(false, $"LibreSpot backend exited with code {process.ExitCode}.");
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
            await using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("LibreSpot backend resource was not found.");

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
}
