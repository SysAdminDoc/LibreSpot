namespace LibreSpot.Desktop.Services;

/// <summary>
/// Cross-process lease for the asset cache. PowerShell uses the same sibling
/// lock path and opens it with FileShare.None, so both runtimes serialize cache
/// mutations and snapshots. Callers that already hold the RD-230 mutation lease
/// acquire this lease second.
/// </summary>
internal sealed class AssetCacheLease : IDisposable
{
    internal const string LockFileName = ".asset-cache.lock";
    private const int DefaultTimeoutMilliseconds = 30_000;
    private const int RetryMilliseconds = 50;

    private readonly FileStream stream;
    private bool disposed;

    private AssetCacheLease(FileStream stream)
    {
        this.stream = stream;
    }

    internal static AssetCacheLease Acquire(string cacheDirectory, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        var cacheRoot = Path.GetFullPath(cacheDirectory);
        var parent = Path.GetDirectoryName(cacheRoot)
            ?? throw new IOException("The asset-cache directory has no parent directory.");
        Directory.CreateDirectory(parent);
        var lockPath = Path.Combine(parent, LockFileName);
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMilliseconds(DefaultTimeoutMilliseconds));

        while (true)
        {
            try
            {
                return new AssetCacheLease(new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.WriteThrough));
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(RetryMilliseconds);
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        stream.Dispose();
    }
}
