using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace LibreSpot.Desktop.Services;

public sealed record BackendMessage(string Kind, string Level, string Payload);
public sealed record BackendRunResult(bool Success, string? ErrorMessage = null);

public sealed class BackendScriptService
{
    private const string ResourceName = "LibreSpot.Desktop.Backend.LibreSpot.Backend.ps1";
    private const string Prefix = "@@LS@@|";

    // Validated backend action names. Anything else is rejected before it reaches the
    // shell so a caller mistake can't turn into a "powershell.exe -Action $malicious" call.
    private static readonly HashSet<string> AllowedActions = new(StringComparer.Ordinal)
    {
        "Install", "CheckUpdates", "Reapply", "RestoreVanilla", "UninstallSpicetify", "FullReset"
    };

    private static string RuntimeDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibreSpot", "Runtime");

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

        Directory.CreateDirectory(RuntimeDirectory);
        string scriptPath;
        try
        {
            scriptPath = await EnsureBackendScriptAsync(cancellationToken);
        }
        catch (Exception ex)
        {
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
                Publish(ev.Data, onMessage);
            }
        };

        process.ErrorDataReceived += (_, ev) =>
        {
            if (!string.IsNullOrWhiteSpace(ev.Data))
            {
                onMessage(new BackendMessage("log", "WARN", ev.Data));
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
            TryKillTree(process);
            return new BackendRunResult(false, "LibreSpot canceled the backend run.");
        }

        // Give the async output pumps a final drain so we don't drop the last few lines.
        try { await Task.Run(process.WaitForExit, CancellationToken.None); } catch { }

        return process.ExitCode == 0
            ? new BackendRunResult(true)
            : new BackendRunResult(false, $"LibreSpot backend exited with code {process.ExitCode}.");
    }

    private static void TryKillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
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

    private static async Task<string> EnsureBackendScriptAsync(CancellationToken cancellationToken)
    {
        var destination = Path.Combine(RuntimeDirectory, "LibreSpot.Backend.ps1");

        await using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("LibreSpot backend resource was not found.");

        // Compute the embedded script's hash once, then only re-extract when the
        // on-disk copy is missing or stale. Avoids gratuitous file writes on every
        // run and removes a race against any stray file handle left by an earlier run.
        var expectedHash = ComputeHash(resourceStream);
        resourceStream.Position = 0;

        if (File.Exists(destination))
        {
            try
            {
                await using var existing = File.Open(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (ComputeHash(existing) == expectedHash)
                {
                    return destination;
                }
            }
            catch
            {
                // If we can't read it for any reason, fall through and rewrite it.
            }
        }

        await using var fileStream = File.Create(destination);
        await resourceStream.CopyToAsync(fileStream, cancellationToken);
        return destination;
    }

    private static string ComputeHash(Stream stream)
    {
        stream.Position = 0;
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(stream);
        return Convert.ToHexString(bytes);
    }
}
