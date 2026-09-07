using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LibreSpot.Desktop.Services;

internal static class AssetCacheTransactionRecovery
{
    internal const int SchemaVersion = 1;
    internal const string MarkerFileName = ".asset-cache-transaction.json";
    internal const int MaxMarkerBytes = 1 * 1024 * 1024;

    private const int MaxEntryCount = 2048;
    private const long MaxAssetBytes = 1024L * 1024 * 1024;
    private const long MaxFingerprintBytes = 4L * 1024 * 1024 * 1024;
    private const int MaxFingerprintFiles = 65_536;
    private const string Prepared = "prepared";
    private const string ExistingMoved = "existing-moved";
    private const string Committed = "committed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    internal static void Recover(string cacheDirectory)
    {
        var cacheRoot = Path.GetFullPath(cacheDirectory);
        AssetCacheLease.ValidateCacheRoot(cacheRoot);
        var configRoot = GetConfigRoot(cacheRoot);
        var markerPath = Path.Combine(configRoot, MarkerFileName);
        if (!File.Exists(markerPath))
        {
            if (Directory.Exists(markerPath))
            {
                throw new AssetCacheBundleException("The asset-cache transaction marker is a directory; it was retained.");
            }

            return;
        }

        ValidateRegularPath(markerPath, "asset-cache transaction marker");
        var markerInfo = new FileInfo(markerPath);
        if (markerInfo.Length <= 0 || markerInfo.Length > MaxMarkerBytes)
        {
            throw new AssetCacheBundleException("The asset-cache transaction marker is too large or empty; it was retained for inspection.");
        }

        TransactionDocument document;
        try
        {
            using var stream = new FileStream(markerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            document = JsonSerializer.Deserialize<TransactionDocument>(stream, JsonOptions)
                ?? throw new JsonException("The transaction marker is empty.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new AssetCacheBundleException($"The asset-cache transaction marker is malformed and was retained: {ex.Message}", ex);
        }

        try
        {
            ValidateDocument(document, cacheRoot, configRoot, markerPath);
            RecoverDocument(document, cacheRoot, markerPath);
        }
        catch (AssetCacheBundleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            throw new AssetCacheBundleException($"The asset-cache transaction could not be recovered safely; its marker was retained: {ex.Message}", ex);
        }
    }

    internal static bool Exists(string cacheDirectory)
    {
        var cacheRoot = Path.GetFullPath(cacheDirectory);
        var markerPath = Path.Combine(GetConfigRoot(cacheRoot), MarkerFileName);
        return File.Exists(markerPath) || Directory.Exists(markerPath);
    }

    internal static AssetCacheTransaction Begin(
        string cacheDirectory,
        string stagingDirectory,
        string replacementDirectory,
        string rollbackDirectory,
        IReadOnlyList<AssetCacheBundleEntry> importedEntries)
    {
        var cacheRoot = Path.GetFullPath(cacheDirectory);
        var configRoot = GetConfigRoot(cacheRoot);
        var markerPath = Path.Combine(configRoot, MarkerFileName);
        if (File.Exists(markerPath) || Directory.Exists(markerPath))
        {
            throw new AssetCacheBundleException("An asset-cache transaction marker already exists. Recovery did not complete, so the new import was not started.");
        }

        ValidateCachePath(cacheRoot, configRoot);
        ValidateOwnedSiblingPath(stagingDirectory, configRoot, ".asset-cache-import-");
        ValidateOwnedSiblingPath(replacementDirectory, configRoot, ".asset-cache-ready-");
        ValidateOwnedSiblingPath(rollbackDirectory, configRoot, ".asset-cache-rollback-");
        ValidateEntries(importedEntries.Select(entry => new TransactionEntry(entry.Sha256, entry.ByteSize)).ToArray());

        var originalExists = Directory.Exists(cacheRoot);
        if (originalExists)
        {
            ValidateTree(cacheRoot);
        }
        ValidateTree(replacementDirectory);
        var document = new TransactionDocument(
            SchemaVersion,
            Prepared,
            cacheRoot,
            Path.GetFullPath(stagingDirectory),
            Path.GetFullPath(replacementDirectory),
            Path.GetFullPath(rollbackDirectory),
            originalExists,
            originalExists ? ComputeTreeHash(cacheRoot) : null,
            ComputeTreeHash(replacementDirectory),
            importedEntries
                .Select(entry => new TransactionEntry(entry.Sha256, entry.ByteSize))
                .ToArray());

        WriteMarker(markerPath, document);
        return new AssetCacheTransaction(markerPath, document);
    }

    private static void RecoverDocument(
        TransactionDocument document,
        string cacheRoot,
        string markerPath)
    {
        var cacheExists = Directory.Exists(cacheRoot);
        var stagingExists = Directory.Exists(document.StagingDirectory);
        var replacementExists = Directory.Exists(document.ReplacementDirectory);
        var rollbackExists = Directory.Exists(document.RollbackDirectory);

        if (File.Exists(cacheRoot) || File.Exists(document.StagingDirectory) ||
            File.Exists(document.ReplacementDirectory) || File.Exists(document.RollbackDirectory))
        {
            throw new AssetCacheBundleException("The asset-cache transaction path is occupied by a file; its marker was retained.");
        }

        if (cacheExists) ValidateTree(cacheRoot);
        if (stagingExists) ValidateTree(document.StagingDirectory);
        if (replacementExists) ValidateTree(document.ReplacementDirectory);
        if (rollbackExists) ValidateTree(document.RollbackDirectory);

        if (string.Equals(document.State, Committed, StringComparison.Ordinal))
        {
            if (cacheExists && VerifyCommittedCache(cacheRoot, document))
            {
                Finish(document, cacheRoot, markerPath);
                return;
            }

            if (!cacheExists && document.OriginalCacheExists && rollbackExists)
            {
                RestoreOriginal(document, cacheRoot, rollbackExists);
                Finish(document, cacheRoot, markerPath);
                return;
            }

            if (cacheExists && rollbackExists && TryRestoreOriginalAfterUnexpectedCache(document, cacheRoot))
            {
                Finish(document, cacheRoot, markerPath);
                return;
            }

            throw new AssetCacheBundleException("The committed asset-cache transaction could not be verified; its marker and owned recovery paths were retained.");
        }

        if (cacheExists && rollbackExists && !replacementExists)
        {
            if (VerifyCommittedCache(cacheRoot, document))
            {
                Finish(document, cacheRoot, markerPath);
                return;
            }

            if (TryRestoreOriginalAfterUnexpectedCache(document, cacheRoot))
            {
                Finish(document, cacheRoot, markerPath);
                return;
            }

            throw new AssetCacheBundleException("The asset-cache swap left an unverified cache beside the original; its transaction marker was retained.");
        }

        if (!cacheExists && document.OriginalCacheExists && rollbackExists)
        {
            RestoreOriginal(document, cacheRoot, rollbackExists);
            Finish(document, cacheRoot, markerPath);
            return;
        }

        if (!cacheExists && !rollbackExists && !document.OriginalCacheExists && replacementExists)
        {
            DeleteOwnedDirectory(document.ReplacementDirectory);
            DeleteOwnedDirectory(document.StagingDirectory);
            DeleteMarker(markerPath);
            return;
        }

        if (cacheExists && !rollbackExists && !document.OriginalCacheExists && VerifyCommittedCache(cacheRoot, document))
        {
            Finish(document, cacheRoot, markerPath);
            return;
        }

        if (cacheExists && replacementExists && !rollbackExists)
        {
            DeleteOwnedDirectory(document.ReplacementDirectory);
            DeleteOwnedDirectory(document.StagingDirectory);
            DeleteMarker(markerPath);
            return;
        }

        throw new AssetCacheBundleException("The asset-cache transaction marker describes an unsafe filesystem state; it and its owned paths were retained.");
    }

    private static void RestoreOriginal(TransactionDocument document, string cacheRoot, bool rollbackExists)
    {
        if (!rollbackExists || !Directory.Exists(document.RollbackDirectory))
        {
            throw new AssetCacheBundleException("The asset-cache rollback directory is missing; the transaction marker was retained.");
        }

        if (Directory.Exists(cacheRoot))
        {
            throw new AssetCacheBundleException("The asset-cache target is occupied while its rollback directory is present; the transaction marker was retained.");
        }

        if (document.OriginalCacheExists && !string.Equals(ComputeTreeHash(document.RollbackDirectory), document.OriginalTreeSha256, StringComparison.Ordinal))
        {
            throw new AssetCacheBundleException("The asset-cache rollback directory failed its recorded fingerprint; the transaction marker was retained.");
        }

        Directory.Move(document.RollbackDirectory, cacheRoot);
    }

    private static bool TryRestoreOriginalAfterUnexpectedCache(TransactionDocument document, string cacheRoot)
    {
        if (!document.OriginalCacheExists || !Directory.Exists(document.RollbackDirectory))
        {
            return false;
        }

        var displaced = document.ReplacementDirectory;
        if (Directory.Exists(displaced))
        {
            return false;
        }

        Directory.Move(cacheRoot, displaced);
        try
        {
            RestoreOriginal(document, cacheRoot, rollbackExists: true);
            return true;
        }
        catch
        {
            if (!Directory.Exists(cacheRoot) && Directory.Exists(displaced))
            {
                Directory.Move(displaced, cacheRoot);
            }

            throw;
        }
    }

    private static void Finish(
        TransactionDocument document,
        string cacheRoot,
        string markerPath)
    {
        if (!Directory.Exists(cacheRoot))
        {
            throw new AssetCacheBundleException("The recovered asset-cache target is missing; the transaction marker was retained.");
        }

        if (Directory.Exists(document.StagingDirectory))
        {
            DeleteOwnedDirectory(document.StagingDirectory);
        }

        if (Directory.Exists(document.ReplacementDirectory))
        {
            DeleteOwnedDirectory(document.ReplacementDirectory);
        }

        if (Directory.Exists(document.RollbackDirectory))
        {
            DeleteOwnedDirectory(document.RollbackDirectory);
        }

        DeleteMarker(markerPath);
    }

    private static bool VerifyCommittedCache(string cacheRoot, TransactionDocument document)
    {
        if (!Directory.Exists(cacheRoot) || !string.Equals(ComputeTreeHash(cacheRoot), document.ReplacementTreeSha256, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var entry in document.ImportedEntries)
        {
            var path = Path.Combine(cacheRoot, entry.Sha256);
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            var info = new FileInfo(path);
            if (info.Length != entry.ByteSize || !string.Equals(ComputeFileHash(path), entry.Sha256, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateDocument(TransactionDocument document, string cacheRoot, string configRoot, string markerPath)
    {
        if (document.SchemaVersion != SchemaVersion ||
            document.State is not (Prepared or ExistingMoved or Committed) ||
            !string.Equals(Path.GetFullPath(document.CacheDirectory), cacheRoot, StringComparison.OrdinalIgnoreCase) ||
            document.ImportedEntries is null ||
            document.ImportedEntries.Count > MaxEntryCount ||
            !IsSha256(document.ReplacementTreeSha256) ||
            (document.OriginalCacheExists && !IsSha256(document.OriginalTreeSha256)) ||
            (!document.OriginalCacheExists && document.OriginalTreeSha256 is not null))
        {
            throw new AssetCacheBundleException("The asset-cache transaction marker is invalid; it was retained.");
        }

        ValidateCachePath(document.CacheDirectory, configRoot);
        ValidateOwnedSiblingPath(document.StagingDirectory, configRoot, ".asset-cache-import-");
        ValidateOwnedSiblingPath(document.ReplacementDirectory, configRoot, ".asset-cache-ready-");
        ValidateOwnedSiblingPath(document.RollbackDirectory, configRoot, ".asset-cache-rollback-");
        ValidateEntries(document.ImportedEntries);
        if (!string.Equals(Path.GetFullPath(markerPath), Path.Combine(configRoot, MarkerFileName), StringComparison.OrdinalIgnoreCase))
        {
            throw new AssetCacheBundleException("The asset-cache transaction marker path is invalid; it was retained.");
        }
    }

    private static void ValidateEntries(IReadOnlyList<TransactionEntry>? entries)
    {
        if (entries is null || entries.Count > MaxEntryCount)
        {
            throw new AssetCacheBundleException("The asset-cache transaction contains too many entries; it was retained.");
        }

        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null || !IsSha256(entry.Sha256) || !hashes.Add(entry.Sha256) || entry.ByteSize < 0 || entry.ByteSize > MaxAssetBytes)
            {
                throw new AssetCacheBundleException("The asset-cache transaction contains an invalid entry; it was retained.");
            }
        }
    }

    private static void ValidateCachePath(string cacheRoot, string configRoot)
    {
        var fullCache = Path.GetFullPath(cacheRoot);
        var parent = Path.GetDirectoryName(fullCache);
        if (!string.Equals(parent, configRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssetCacheBundleException("The asset-cache transaction target is not a direct child of its configuration directory; it was retained.");
        }

        ValidateRegularPath(configRoot, "asset-cache configuration directory");
        if (Directory.Exists(fullCache) || File.Exists(fullCache))
        {
            ValidateRegularPath(fullCache, "asset-cache directory");
        }
    }

    private static void ValidateOwnedSiblingPath(string path, string configRoot, string prefix)
    {
        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath);
        var name = Path.GetFileName(fullPath);
        if (!string.Equals(parent, configRoot, StringComparison.OrdinalIgnoreCase) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..], "N", out _))
        {
            throw new AssetCacheBundleException($"The asset-cache transaction contains an unowned sibling path '{path}'; it was retained.");
        }

        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            ValidateRegularPath(fullPath, "asset-cache transaction directory");
        }
    }

    private static void ValidateRegularPath(string path, string label)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new AssetCacheBundleException($"The {label} is a reparse point; the transaction marker was retained.");
        }
    }

    private static void ValidateTree(string root)
    {
        var files = GetValidatedFiles(root);
        if (files.Length > MaxFingerprintFiles)
        {
            throw new AssetCacheBundleException("The asset-cache transaction tree contains too many files; its marker was retained.");
        }

        long totalBytes = 0;
        foreach (var file in files)
        {
            var length = new FileInfo(file).Length;
            if (length < 0 || totalBytes > MaxFingerprintBytes - length)
            {
                throw new AssetCacheBundleException("The asset-cache transaction tree exceeds the fingerprint safety limit; its marker was retained.");
            }

            totalBytes += length;
        }
    }

    private static string ComputeTreeHash(string root)
    {
        var files = GetValidatedFiles(root)
            .Select(path => (Path: path, Relative: NormalizeRelativePath(Path.GetRelativePath(root, path))))
            .OrderBy(item => item.Relative, StringComparer.Ordinal)
            .ToArray();
        if (files.Length > MaxFingerprintFiles)
        {
            throw new AssetCacheBundleException("The asset-cache transaction tree contains too many files; its marker was retained.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long totalBytes = 0;
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.Relative));
            hash.AppendData([0]);
            using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                totalBytes += read;
                if (totalBytes > MaxFingerprintBytes)
                {
                    throw new AssetCacheBundleException("The asset-cache transaction tree exceeds the fingerprint safety limit; its marker was retained.");
                }
            }

            hash.AppendData([0]);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string NormalizeRelativePath(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');

    private static string[] GetValidatedFiles(string root)
    {
        ValidateRegularPath(root, "asset-cache transaction path");
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var path in Directory.EnumerateFileSystemEntries(current))
            {
                ValidateRegularPath(path, "asset-cache transaction path");
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(path);
                }
                else
                {
                    files.Add(path);
                }
            }
        }

        return files.ToArray();
    }

    private static string GetConfigRoot(string cacheRoot) =>
        Path.GetDirectoryName(cacheRoot)
        ?? throw new AssetCacheBundleException("The asset-cache directory has no parent configuration directory.");

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void WriteMarker(string markerPath, TransactionDocument document)
    {
        var configRoot = Path.GetDirectoryName(markerPath)
            ?? throw new AssetCacheBundleException("The asset-cache transaction marker has no parent directory.");
        Directory.CreateDirectory(configRoot);
        var temporaryPath = Path.Combine(configRoot, $".asset-cache-transaction.{Guid.NewGuid():N}.tmp");
        try
        {
            var markerBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            if (markerBytes.Length <= 0 || markerBytes.Length > MaxMarkerBytes)
            {
                throw new AssetCacheBundleException("The asset-cache transaction marker exceeds the bounded recovery record limit.");
            }

            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(markerBytes, 0, markerBytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(markerPath))
            {
                var backupPath = temporaryPath + ".bak";
                try
                {
                    File.Replace(temporaryPath, markerPath, backupPath, ignoreMetadataErrors: true);
                }
                finally
                {
                    TryDeleteFile(backupPath);
                }
            }
            else
            {
                File.Move(temporaryPath, markerPath);
            }

            temporaryPath = string.Empty;
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static void DeleteOwnedDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        ValidateTree(path);
        Directory.Delete(path, recursive: true);
    }

    private static void DeleteMarker(string markerPath)
    {
        ValidateRegularPath(markerPath, "asset-cache transaction marker");
        File.Delete(markerPath);
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    internal sealed class AssetCacheTransaction
    {
        private readonly string markerPath;
        private TransactionDocument document;

        internal AssetCacheTransaction(string markerPath, TransactionDocument document)
        {
            this.markerPath = markerPath;
            this.document = document;
        }

        internal void MarkExistingMoved()
        {
            document = document with { State = ExistingMoved };
            WriteMarker(markerPath, document);
        }

        internal void MarkCommitted()
        {
            document = document with { State = Committed };
            WriteMarker(markerPath, document);
        }

        internal void Complete()
        {
            if (Directory.Exists(document.StagingDirectory)) DeleteOwnedDirectory(document.StagingDirectory);
            if (Directory.Exists(document.ReplacementDirectory)) DeleteOwnedDirectory(document.ReplacementDirectory);
            if (Directory.Exists(document.RollbackDirectory)) DeleteOwnedDirectory(document.RollbackDirectory);
            DeleteMarker(markerPath);
        }

        internal void Abort()
        {
            try
            {
                if (Directory.Exists(document.StagingDirectory)) DeleteOwnedDirectory(document.StagingDirectory);
                if (Directory.Exists(document.ReplacementDirectory)) DeleteOwnedDirectory(document.ReplacementDirectory);
                if (Directory.Exists(document.RollbackDirectory)) DeleteOwnedDirectory(document.RollbackDirectory);
                DeleteMarker(markerPath);
            }
            catch
            {
                // Preserve the marker for the next invocation when cleanup cannot be proven safe.
            }
        }
    }

    internal sealed record TransactionDocument(
        int SchemaVersion,
        string State,
        string CacheDirectory,
        string StagingDirectory,
        string ReplacementDirectory,
        string RollbackDirectory,
        bool OriginalCacheExists,
        string? OriginalTreeSha256,
        string ReplacementTreeSha256,
        IReadOnlyList<TransactionEntry> ImportedEntries);

    internal sealed record TransactionEntry(string Sha256, long ByteSize);
}
