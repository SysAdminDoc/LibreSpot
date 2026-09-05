using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Core.Tests;

public sealed class AssetCacheBundleServiceTests
{
    [Fact]
    public void ExportAndImport_RoundTripVerifiedEntriesAndMergeExistingCache()
    {
        using var fixture = new Fixture();
        var alpha = fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        var beta = fixture.AddSourceAsset("Beta", "https://example.invalid/beta", "beta bytes");
        var existing = fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        var bundlePath = Path.Combine(fixture.Root, "cache.zip");
        var service = new AssetCacheBundleService();

        var exported = service.Export(fixture.SourceCache, bundlePath, "4.5.0");
        var imported = service.Import(fixture.TargetCache, bundlePath);

        Assert.Equal(2, exported.EntryCount);
        Assert.Equal(2, imported.EntryCount);
        Assert.Equal("4.5.0", imported.ProductVersion);
        Assert.Equal("spotify-installer", imported.ExternalRequirementId);
        Assert.Contains("SpotX's Spotify installer chain", imported.ExternalRequirement, StringComparison.Ordinal);
        Assert.Equal(alpha.Bytes, File.ReadAllBytes(Path.Combine(fixture.TargetCache, alpha.Hash)));
        Assert.Equal(beta.Bytes, File.ReadAllBytes(Path.Combine(fixture.TargetCache, beta.Hash)));
        Assert.Equal(existing.Bytes, File.ReadAllBytes(Path.Combine(fixture.TargetCache, existing.Hash)));

        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.TargetCache, "asset-cache-index.json")));
        var entries = index.RootElement.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal(3, entries.Length);
        var alphaIndex = Assert.Single(entries, entry => entry.GetProperty("sha256").GetString() == alpha.Hash);
        Assert.Equal("Alpha", alphaIndex.GetProperty("label").GetString());
        Assert.Equal("https://example.invalid/alpha", alphaIndex.GetProperty("sourceUrl").GetString());
        Assert.Equal("present", alphaIndex.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(alphaIndex.GetProperty("lastVerifiedAtUtc").GetString()));

        using var archive = ZipFile.OpenRead(bundlePath);
        Assert.Equal(3, archive.Entries.Count);
        using var manifest = JsonDocument.Parse(ReadEntry(archive.GetEntry("manifest.json")!));
        Assert.Equal("librespot-asset-cache", manifest.RootElement.GetProperty("bundleType").GetString());
        Assert.Equal(2, manifest.RootElement.GetProperty("entryCount").GetInt32());
        Assert.Equal("spotify-installer", manifest.RootElement.GetProperty("externalRequirements")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void Import_RejectsTamperedAssetBeforeChangingTargetCache()
    {
        using var fixture = new Fixture();
        fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        var existing = fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        var indexPath = Path.Combine(fixture.TargetCache, "asset-cache-index.json");
        var originalIndex = File.ReadAllBytes(indexPath);
        var bundlePath = Path.Combine(fixture.Root, "tampered.zip");
        var service = new AssetCacheBundleService();
        service.Export(fixture.SourceCache, bundlePath, "4.5.0");

        using (var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Update))
        {
            var asset = Assert.Single(archive.Entries, entry => entry.FullName.StartsWith("assets/", StringComparison.Ordinal));
            var name = asset.FullName;
            var length = asset.Length;
            asset.Delete();
            var replacement = archive.CreateEntry(name);
            using var stream = replacement.Open();
            stream.Write(Enumerable.Repeat((byte)'x', checked((int)length)).ToArray());
        }

        var error = Assert.Throws<AssetCacheBundleException>(() => service.Import(fixture.TargetCache, bundlePath));

        Assert.Contains("failed SHA256 verification", error.Message, StringComparison.Ordinal);
        Assert.Equal(existing.Bytes, File.ReadAllBytes(Path.Combine(fixture.TargetCache, existing.Hash)));
        Assert.Equal(originalIndex, File.ReadAllBytes(indexPath));
        Assert.Equal(2, Directory.EnumerateFiles(fixture.TargetCache).Count());
    }

    [Fact]
    public void Export_RejectsIncompleteOrCorruptIndexedCache()
    {
        using var fixture = new Fixture();
        var asset = fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        var service = new AssetCacheBundleService();
        var missingBundle = Path.Combine(fixture.Root, "missing.zip");
        File.Delete(Path.Combine(fixture.SourceCache, asset.Hash));

        var missing = Assert.Throws<AssetCacheBundleException>(() => service.Export(fixture.SourceCache, missingBundle, "4.5.0"));
        Assert.Contains("incomplete", missing.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(missingBundle));

        File.WriteAllText(Path.Combine(fixture.SourceCache, asset.Hash), "wrong bytes");
        var corruptBundle = Path.Combine(fixture.Root, "corrupt.zip");
        var corrupt = Assert.Throws<AssetCacheBundleException>(() => service.Export(fixture.SourceCache, corruptBundle, "4.5.0"));
        Assert.True(
            corrupt.Message.Contains("size", StringComparison.OrdinalIgnoreCase) ||
            corrupt.Message.Contains("SHA256", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(corruptBundle));
    }

    [Fact]
    public void Import_RejectsUndeclaredZipEntryBeforeCreatingCache()
    {
        using var fixture = new Fixture();
        fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        var bundlePath = Path.Combine(fixture.Root, "extra-entry.zip");
        var target = Path.Combine(fixture.Root, "empty-target", "cache");
        var service = new AssetCacheBundleService();
        service.Export(fixture.SourceCache, bundlePath, "4.5.0");
        using (var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Update))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open());
            writer.Write("no");
        }

        var error = Assert.Throws<AssetCacheBundleException>(() => service.Import(target, bundlePath));

        Assert.Contains("unexpected ZIP entry", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(target));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "outside.txt")));
    }

    [Theory]
    [InlineData("stale", false)]
    [InlineData("present", true)]
    public void Import_RejectsNonPresentOrUnverifiedManifestEntriesBeforeChangingTargetCache(
        string status,
        bool removeVerificationTimestamp)
    {
        using var fixture = new Fixture();
        var imported = fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        var bundlePath = Path.Combine(fixture.Root, "invalid-state.zip");
        var service = new AssetCacheBundleService();
        service.Export(fixture.SourceCache, bundlePath, "4.5.0");
        RewriteManifest(bundlePath, manifestEntry =>
        {
            manifestEntry["status"] = status;
            if (removeVerificationTimestamp)
            {
                manifestEntry["lastVerifiedAtUtc"] = null;
            }
        });
        var original = SnapshotFiles(fixture.TargetCache);

        var error = Assert.Throws<AssetCacheBundleException>(() => service.Import(fixture.TargetCache, bundlePath));

        Assert.Contains("not a verified present entry", error.Message, StringComparison.Ordinal);
        Assert.Equal(original, SnapshotFiles(fixture.TargetCache));
        Assert.False(File.Exists(Path.Combine(fixture.TargetCache, imported.Hash)));
    }

    [Fact]
    public void Import_WhenCommitFailsAfterMovingExistingCache_RestoresEveryOriginalByte()
    {
        using var fixture = new Fixture();
        var imported = fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        File.WriteAllText(Path.Combine(fixture.TargetCache, "unindexed-note.txt"), "preserve me");
        var bundlePath = Path.Combine(fixture.Root, "rollback.zip");
        new AssetCacheBundleService().Export(fixture.SourceCache, bundlePath, "4.5.0");
        var original = SnapshotFiles(fixture.TargetCache);
        var service = new AssetCacheBundleService(stage =>
        {
            Assert.Equal(AssetCacheBundleTransactionStage.ExistingCacheMoved, stage);
            throw new IOException("Simulated commit interruption.");
        });

        var error = Assert.Throws<AssetCacheBundleException>(() => service.Import(fixture.TargetCache, bundlePath));

        Assert.Contains("Simulated commit interruption", error.Message, StringComparison.Ordinal);
        Assert.Equal(original, SnapshotFiles(fixture.TargetCache));
        Assert.False(File.Exists(Path.Combine(fixture.TargetCache, imported.Hash)));
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(fixture.TargetCache)!, ".asset-cache-rollback-*"));
    }

    private static void RewriteManifest(string bundlePath, Action<JsonObject> mutateEntry)
    {
        using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Update);
        var manifestArchiveEntry = archive.GetEntry("manifest.json")!;
        var manifest = JsonNode.Parse(ReadEntry(manifestArchiveEntry))!.AsObject();
        mutateEntry(manifest["entries"]!.AsArray()[0]!.AsObject());
        manifestArchiveEntry.Delete();
        using var writer = new StreamWriter(
            archive.CreateEntry("manifest.json").Open(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IReadOnlyDictionary<string, string> SnapshotFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                path => Convert.ToBase64String(File.ReadAllBytes(path)),
                StringComparer.OrdinalIgnoreCase);

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<Asset> sourceAssets = [];
        private readonly List<Asset> targetAssets = [];

        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "LibreSpot.AssetCacheBundle.Tests", Guid.NewGuid().ToString("N"));
            SourceCache = Path.Combine(Root, "source", "cache");
            TargetCache = Path.Combine(Root, "target", "cache");
            Directory.CreateDirectory(SourceCache);
            Directory.CreateDirectory(TargetCache);
        }

        public string Root { get; }
        public string SourceCache { get; }
        public string TargetCache { get; }

        public Asset AddSourceAsset(string label, string sourceUrl, string content) =>
            AddAsset(SourceCache, sourceAssets, label, sourceUrl, content);

        public Asset AddTargetAsset(string label, string sourceUrl, string content) =>
            AddAsset(TargetCache, targetAssets, label, sourceUrl, content);

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }

        private static Asset AddAsset(string cache, List<Asset> assets, string label, string sourceUrl, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var asset = new Asset(hash, label, sourceUrl, bytes);
            assets.Add(asset);
            File.WriteAllBytes(Path.Combine(cache, hash), bytes);
            WriteIndex(cache, assets);
            return asset;
        }

        private static void WriteIndex(string cache, IReadOnlyList<Asset> assets)
        {
            var now = DateTimeOffset.Parse("2026-09-04T00:00:00Z");
            var index = new
            {
                schemaVersion = 1,
                generatedAtUtc = now,
                entries = assets.Select(asset => new
                {
                    sha256 = asset.Hash,
                    label = asset.Label,
                    sourceUrl = asset.SourceUrl,
                    byteSize = asset.Bytes.LongLength,
                    firstSeenAtUtc = now.ToString("O"),
                    lastUsedAtUtc = now.ToString("O"),
                    lastVerifiedAtUtc = now.ToString("O"),
                    status = "present",
                    quarantinedPath = (string?)null
                })
            };
            File.WriteAllText(
                Path.Combine(cache, "asset-cache-index.json"),
                JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed record Asset(string Hash, string Label, string SourceUrl, byte[] Bytes);
}
