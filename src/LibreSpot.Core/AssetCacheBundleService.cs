using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LibreSpot.Desktop.Services;

public sealed class AssetCacheBundleService
{
    private const int SchemaVersion = 1;
    private const int MaxEntryCount = 2048;
    private const long MaxAssetBytes = 1024L * 1024 * 1024;
    private const long MaxBundleBytes = 4L * 1024 * 1024 * 1024;
    private const int MaxManifestBytes = 4 * 1024 * 1024;
    private const int MaxIndexBytes = 4 * 1024 * 1024;
    private const string BundleType = "librespot-asset-cache";
    private const string SpotifyRequirementId = "spotify-installer";
    private const string SpotifyRequirement = "Spotify itself is not stored in LibreSpot's asset cache. SpotX's Spotify installer chain still needs access to Spotify's vendor download.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AssetCacheBundleResult Export(string cacheDirectory, string outputPath, string productVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(productVersion);

        var cacheRoot = Path.GetFullPath(cacheDirectory);
        var indexPath = Path.Combine(cacheRoot, "asset-cache-index.json");
        var entries = ReadCompleteIndex(indexPath, cacheRoot);
        var totalBytes = entries.Sum(entry => entry.ByteSize);
        var fullOutputPath = Path.GetFullPath(outputPath);
        if (IsPathWithin(fullOutputPath, cacheRoot))
        {
            throw new AssetCacheBundleException("The exported bundle must be written outside the asset-cache directory.");
        }
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new AssetCacheBundleException("The bundle output path has no parent directory.");
        Directory.CreateDirectory(outputDirectory);

        var manifest = new AssetCacheBundleManifest(
            SchemaVersion,
            BundleType,
            productVersion,
            DateTimeOffset.UtcNow,
            entries.Count,
            totalBytes,
            entries,
            [new AssetCacheExternalRequirement(SpotifyRequirementId, SpotifyRequirement)]);
        var temporaryPath = Path.Combine(outputDirectory, $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var stream = manifestEntry.Open())
                {
                    JsonSerializer.Serialize(stream, manifest, JsonOptions);
                }

                foreach (var entry in entries)
                {
                    var archiveEntry = archive.CreateEntry($"assets/{entry.Sha256}", CompressionLevel.Optimal);
                    using var source = OpenVerifiedAsset(Path.Combine(cacheRoot, entry.Sha256), entry);
                    using var destination = archiveEntry.Open();
                    source.CopyTo(destination);
                }
            }

            File.Move(temporaryPath, fullOutputPath, overwrite: true);
            return new AssetCacheBundleResult(
                fullOutputPath,
                entries.Count,
                totalBytes,
                productVersion,
                SpotifyRequirementId,
                SpotifyRequirement);
        }
        catch (AssetCacheBundleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            throw new AssetCacheBundleException($"Could not export the asset-cache bundle: {ex.Message}", ex);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    public AssetCacheBundleResult Import(string cacheDirectory, string bundlePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundlePath);

        var cacheRoot = Path.GetFullPath(cacheDirectory);
        var fullBundlePath = Path.GetFullPath(bundlePath);
        if (IsPathWithin(fullBundlePath, cacheRoot))
        {
            throw new AssetCacheBundleException("The imported bundle must be stored outside the target asset-cache directory.");
        }
        if (!File.Exists(fullBundlePath))
        {
            throw new AssetCacheBundleException($"Asset-cache bundle not found: {fullBundlePath}");
        }

        var configRoot = Path.GetDirectoryName(cacheRoot)
            ?? throw new AssetCacheBundleException("The asset-cache directory has no parent directory.");
        Directory.CreateDirectory(configRoot);
        var stagingRoot = Path.Combine(configRoot, $".asset-cache-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);

        try
        {
            using var file = new FileStream(fullBundlePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false, Encoding.UTF8);
            var manifest = ReadAndValidateManifest(archive);
            var expectedNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "manifest.json"
            };
            foreach (var entry in manifest.Entries)
            {
                expectedNames.Add($"assets/{entry.Sha256}");
            }

            var archiveNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var archiveEntry in archive.Entries)
            {
                if (!archiveNames.Add(archiveEntry.FullName))
                {
                    throw new AssetCacheBundleException($"The bundle contains duplicate ZIP entry '{archiveEntry.FullName}'.");
                }

                if (!expectedNames.Contains(archiveEntry.FullName))
                {
                    throw new AssetCacheBundleException($"The bundle contains unexpected ZIP entry '{archiveEntry.FullName}'.");
                }

                RejectLinkEntry(archiveEntry);
            }

            if (!archiveNames.SetEquals(expectedNames))
            {
                throw new AssetCacheBundleException("The bundle does not contain exactly the assets declared by its manifest.");
            }

            foreach (var entry in manifest.Entries)
            {
                var archiveEntry = archive.GetEntry($"assets/{entry.Sha256}")
                    ?? throw new AssetCacheBundleException($"The bundle is missing asset {entry.Sha256}.");
                if (archiveEntry.Length != entry.ByteSize)
                {
                    throw new AssetCacheBundleException($"Asset {entry.Sha256} has size {archiveEntry.Length}, expected {entry.ByteSize}.");
                }

                var stagedPath = Path.Combine(stagingRoot, entry.Sha256);
                using (var source = archiveEntry.Open())
                using (var destination = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    CopyExactly(source, destination, entry.ByteSize);
                }

                var observedHash = ComputeSha256(stagedPath);
                if (!observedHash.Equals(entry.Sha256, StringComparison.Ordinal))
                {
                    throw new AssetCacheBundleException($"Asset {entry.Sha256} failed SHA256 verification. Observed {observedHash}.");
                }
            }

            var existingEntries = ReadExistingIndexForMerge(Path.Combine(cacheRoot, "asset-cache-index.json"));
            var now = DateTimeOffset.UtcNow;
            foreach (var entry in manifest.Entries)
            {
                existingEntries[entry.Sha256] = entry with
                {
                    LastVerifiedAtUtc = now.ToString("O"),
                    Status = "present",
                    QuarantinedPath = null
                };
            }

            Directory.CreateDirectory(cacheRoot);
            foreach (var entry in manifest.Entries)
            {
                var destinationPath = Path.Combine(cacheRoot, entry.Sha256);
                var temporaryAssetPath = Path.Combine(cacheRoot, $".{entry.Sha256}.{Guid.NewGuid():N}.tmp");
                try
                {
                    File.Copy(Path.Combine(stagingRoot, entry.Sha256), temporaryAssetPath, overwrite: false);
                    File.Move(temporaryAssetPath, destinationPath, overwrite: true);
                }
                finally
                {
                    TryDeleteFile(temporaryAssetPath);
                }
            }

            WriteIndexAtomically(cacheRoot, existingEntries.Values.OrderBy(entry => entry.Sha256, StringComparer.Ordinal).ToArray(), now);
            return new AssetCacheBundleResult(
                fullBundlePath,
                manifest.EntryCount,
                manifest.TotalBytes,
                manifest.ProductVersion,
                SpotifyRequirementId,
                SpotifyRequirement);
        }
        catch (AssetCacheBundleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            throw new AssetCacheBundleException($"Could not import the asset-cache bundle: {ex.Message}", ex);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private static IReadOnlyList<AssetCacheBundleEntry> ReadCompleteIndex(string indexPath, string cacheRoot)
    {
        if (!File.Exists(indexPath))
        {
            throw new AssetCacheBundleException("The asset cache has no index. Run an online LibreSpot install before exporting it.");
        }

        var index = ReadIndex(indexPath);
        if (index.Entries.Count == 0)
        {
            throw new AssetCacheBundleException("The asset cache index is empty.");
        }

        var entries = new List<AssetCacheBundleEntry>(index.Entries.Count);
        long totalBytes = 0;
        foreach (var entry in index.Entries.OrderBy(entry => entry.Sha256, StringComparer.Ordinal))
        {
            ValidateIndexedEntry(entry, requirePresent: true);
            if (totalBytes > MaxBundleBytes - entry.ByteSize)
            {
                throw new AssetCacheBundleException("The asset cache exceeds the supported bundle safety limit.");
            }

            totalBytes += entry.ByteSize;
            var path = Path.Combine(cacheRoot, entry.Sha256);
            using var _ = OpenVerifiedAsset(path, entry);
            entries.Add(entry);
        }

        return entries;
    }

    private static FileStream OpenVerifiedAsset(string path, AssetCacheBundleEntry entry)
    {
        if (!File.Exists(path))
        {
            throw new AssetCacheBundleException($"The asset cache is incomplete. Missing {entry.Sha256} ({entry.Label}).");
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new AssetCacheBundleException($"Cached asset {entry.Sha256} is a reparse point and cannot be exported.");
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (stream.Length != entry.ByteSize)
            {
                throw new AssetCacheBundleException($"Cached asset {entry.Sha256} has size {stream.Length}, expected {entry.ByteSize}.");
            }

            var observedHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!observedHash.Equals(entry.Sha256, StringComparison.Ordinal))
            {
                throw new AssetCacheBundleException($"Cached asset {entry.Sha256} failed SHA256 verification. Observed {observedHash}.");
            }

            stream.Position = 0;
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static AssetCacheBundleManifest ReadAndValidateManifest(ZipArchive archive)
    {
        if (archive.Entries.Count > MaxEntryCount + 1)
        {
            throw new AssetCacheBundleException($"The bundle contains too many ZIP entries. Maximum: {MaxEntryCount + 1}.");
        }

        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new AssetCacheBundleException("The bundle has no manifest.json entry.");
        RejectLinkEntry(manifestEntry);
        if (manifestEntry.Length <= 0 || manifestEntry.Length > MaxManifestBytes)
        {
            throw new AssetCacheBundleException($"The bundle manifest must be between 1 and {MaxManifestBytes} bytes.");
        }

        AssetCacheBundleManifest? manifest;
        using (var stream = manifestEntry.Open())
        {
            manifest = JsonSerializer.Deserialize<AssetCacheBundleManifest>(stream, JsonOptions);
        }

        if (manifest is null || manifest.SchemaVersion != SchemaVersion || !string.Equals(manifest.BundleType, BundleType, StringComparison.Ordinal))
        {
            throw new AssetCacheBundleException("The bundle manifest type or schema version is not supported.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ProductVersion) || manifest.ProductVersion.Length > 64)
        {
            throw new AssetCacheBundleException("The bundle manifest has an invalid product version.");
        }

        if (manifest.Entries is null || manifest.Entries.Count == 0 || manifest.Entries.Count > MaxEntryCount || manifest.EntryCount != manifest.Entries.Count)
        {
            throw new AssetCacheBundleException($"The bundle manifest must declare between 1 and {MaxEntryCount} entries and a matching entryCount.");
        }

        var hashes = new HashSet<string>(StringComparer.Ordinal);
        long totalBytes = 0;
        foreach (var entry in manifest.Entries)
        {
            ValidateIndexedEntry(entry, requirePresent: false);
            if (!hashes.Add(entry.Sha256))
            {
                throw new AssetCacheBundleException($"The bundle manifest contains duplicate asset {entry.Sha256}.");
            }

            if (entry.ByteSize < 0 || entry.ByteSize > MaxAssetBytes || totalBytes > MaxBundleBytes - entry.ByteSize)
            {
                throw new AssetCacheBundleException("The bundle's declared asset sizes exceed the supported safety limit.");
            }

            totalBytes += entry.ByteSize;
        }

        if (manifest.TotalBytes != totalBytes)
        {
            throw new AssetCacheBundleException($"The bundle manifest totalBytes is {manifest.TotalBytes}, expected {totalBytes}.");
        }

        if (manifest.ExternalRequirements is null || !manifest.ExternalRequirements.Any(requirement => requirement.Id == SpotifyRequirementId))
        {
            throw new AssetCacheBundleException("The bundle manifest does not disclose Spotify's external installer requirement.");
        }

        return manifest with { Entries = manifest.Entries.OrderBy(entry => entry.Sha256, StringComparer.Ordinal).ToArray() };
    }

    private static AssetCacheIndexDocument ReadIndex(string indexPath)
    {
        var info = new FileInfo(indexPath);
        if (info.Length <= 0 || info.Length > MaxIndexBytes)
        {
            throw new AssetCacheBundleException($"The asset-cache index must be between 1 and {MaxIndexBytes} bytes.");
        }

        using var stream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var index = JsonSerializer.Deserialize<AssetCacheIndexDocument>(stream, JsonOptions);
        if (index is null || index.SchemaVersion != SchemaVersion || index.Entries is null || index.Entries.Count > MaxEntryCount)
        {
            throw new AssetCacheBundleException("The asset-cache index is malformed or uses an unsupported schema version.");
        }

        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in index.Entries)
        {
            ValidateIndexedEntry(entry, requirePresent: false);
            if (!hashes.Add(entry.Sha256))
            {
                throw new AssetCacheBundleException($"The asset-cache index contains duplicate asset {entry.Sha256}.");
            }
        }

        return index;
    }

    private static Dictionary<string, AssetCacheBundleEntry> ReadExistingIndexForMerge(string indexPath)
    {
        if (!File.Exists(indexPath))
        {
            return new Dictionary<string, AssetCacheBundleEntry>(StringComparer.Ordinal);
        }

        var index = ReadIndex(indexPath);
        return index.Entries.ToDictionary(entry => entry.Sha256, StringComparer.Ordinal);
    }

    private static void ValidateIndexedEntry(AssetCacheBundleEntry entry, bool requirePresent)
    {
        if (!IsSha256(entry.Sha256))
        {
            throw new AssetCacheBundleException("The asset-cache metadata contains an invalid SHA256 value.");
        }

        if (string.IsNullOrWhiteSpace(entry.Label) || entry.Label.Length > 256)
        {
            throw new AssetCacheBundleException($"Asset {entry.Sha256} has an invalid label.");
        }

        if (entry.SourceUrl?.Length > 2048 || entry.ByteSize < 0 || entry.ByteSize > MaxAssetBytes)
        {
            throw new AssetCacheBundleException($"Asset {entry.Sha256} has invalid source or size metadata.");
        }

        if (requirePresent && (!string.Equals(entry.Status, "present", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(entry.LastVerifiedAtUtc)))
        {
            throw new AssetCacheBundleException($"The asset cache is incomplete. {entry.Sha256} ({entry.Label}) is not a verified present entry.");
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void RejectLinkEntry(ZipArchiveEntry entry)
    {
        var unixFileType = (entry.ExternalAttributes >> 16) & 0xF000;
        if (unixFileType == 0xA000)
        {
            throw new AssetCacheBundleException($"ZIP entry '{entry.FullName}' is a symbolic link.");
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void CopyExactly(Stream source, Stream destination, long expectedBytes)
    {
        var buffer = new byte[81920];
        long copied = 0;
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            if (copied > expectedBytes - read)
            {
                throw new AssetCacheBundleException("A ZIP asset expanded beyond its declared size.");
            }

            destination.Write(buffer, 0, read);
            copied += read;
        }

        if (copied != expectedBytes)
        {
            throw new AssetCacheBundleException($"A ZIP asset expanded to {copied} bytes, expected {expectedBytes}.");
        }
    }

    private static bool IsPathWithin(string candidatePath, string directoryPath)
    {
        var candidate = Path.GetFullPath(candidatePath);
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        return candidate.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteIndexAtomically(string cacheRoot, IReadOnlyList<AssetCacheBundleEntry> entries, DateTimeOffset generatedAtUtc)
    {
        var indexPath = Path.Combine(cacheRoot, "asset-cache-index.json");
        var temporaryPath = Path.Combine(cacheRoot, $".asset-cache-index.{Guid.NewGuid():N}.tmp");
        try
        {
            var index = new AssetCacheIndexDocument(SchemaVersion, generatedAtUtc, entries);
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, index, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, indexPath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record AssetCacheIndexDocument(
        int SchemaVersion,
        DateTimeOffset GeneratedAtUtc,
        IReadOnlyList<AssetCacheBundleEntry> Entries);

    private sealed record AssetCacheBundleManifest(
        int SchemaVersion,
        string BundleType,
        string ProductVersion,
        DateTimeOffset GeneratedAtUtc,
        int EntryCount,
        long TotalBytes,
        IReadOnlyList<AssetCacheBundleEntry> Entries,
        IReadOnlyList<AssetCacheExternalRequirement> ExternalRequirements);

    private sealed record AssetCacheExternalRequirement(string Id, string Reason);
}

public sealed record AssetCacheBundleEntry(
    string Sha256,
    string Label,
    string? SourceUrl,
    long ByteSize,
    string? FirstSeenAtUtc,
    string? LastUsedAtUtc,
    string? LastVerifiedAtUtc,
    string Status = "present",
    string? QuarantinedPath = null);

public sealed record AssetCacheBundleResult(
    string Path,
    int EntryCount,
    long TotalBytes,
    string ProductVersion,
    string ExternalRequirementId,
    string ExternalRequirement);

public sealed class AssetCacheBundleException : Exception
{
    public AssetCacheBundleException(string message)
        : base(message)
    {
    }

    public AssetCacheBundleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
