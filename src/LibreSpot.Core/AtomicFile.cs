using System.Collections.Concurrent;
using System.Text;

namespace LibreSpot.Desktop.Services;

/// <summary>
/// Durable temp-then-replace writes. Every JSON/text persistence path should
/// go through this helper so concurrent saves never share a temp name and a
/// crash mid-write cannot leave a truncated destination.
/// </summary>
public static class AtomicFile
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DestinationGates =
        new(StringComparer.OrdinalIgnoreCase);

    public static string CreateTempPath(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        destinationPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("A destination path must have a parent directory.");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
    }

    public static void WriteAllText(string path, string content)
    {
        Write(path, stream =>
        {
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
            writer.Write(content);
            writer.Flush();
        });
    }

    public static Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) =>
        WriteAsync(path, async (stream, ct) =>
        {
            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
            await writer.WriteAsync(content.AsMemory(), ct);
            await writer.FlushAsync(ct);
        }, cancellationToken);

    public static void Write(string path, Action<FileStream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);
        var destinationPath = Path.GetFullPath(path);
        var gate = DestinationGates.GetOrAdd(destinationPath, static _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        string? tempPath = null;
        try
        {
            tempPath = CreateTempPath(destinationPath);
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, destinationPath, overwrite: true);
            tempPath = null;
        }
        finally
        {
            TryDelete(tempPath);
            gate.Release();
        }
    }

    public static async Task WriteAsync(string path, Func<FileStream, CancellationToken, Task> write, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);
        var destinationPath = Path.GetFullPath(path);
        var gate = DestinationGates.GetOrAdd(destinationPath, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        string? tempPath = null;
        try
        {
            tempPath = CreateTempPath(destinationPath);
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await write(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, destinationPath, overwrite: true);
            tempPath = null;
        }
        finally
        {
            TryDelete(tempPath);
            gate.Release();
        }
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort: a leftover temp file is preferable to replacing a
            // good destination with a partial write.
        }
    }
}
