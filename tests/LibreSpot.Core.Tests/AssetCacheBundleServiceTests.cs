using System.Diagnostics;
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

        var exported = service.Export(fixture.SourceCache, bundlePath, "4.6.0");
        var imported = service.Import(fixture.TargetCache, bundlePath);

        Assert.Equal(2, exported.EntryCount);
        Assert.Equal(2, imported.EntryCount);
        Assert.Equal("4.6.0", imported.ProductVersion);
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
        var imported = fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        var existing = fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        var indexPath = Path.Combine(fixture.TargetCache, "asset-cache-index.json");
        var originalIndex = File.ReadAllBytes(indexPath);
        var bundlePath = Path.Combine(fixture.Root, "tampered.zip");
        var service = new AssetCacheBundleService();
        service.Export(fixture.SourceCache, bundlePath, "4.6.0");

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

        var missing = Assert.Throws<AssetCacheBundleException>(() => service.Export(fixture.SourceCache, missingBundle, "4.6.0"));
        Assert.Contains("incomplete", missing.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(missingBundle));

        File.WriteAllText(Path.Combine(fixture.SourceCache, asset.Hash), "wrong bytes");
        var corruptBundle = Path.Combine(fixture.Root, "corrupt.zip");
        var corrupt = Assert.Throws<AssetCacheBundleException>(() => service.Export(fixture.SourceCache, corruptBundle, "4.6.0"));
        Assert.True(
            corrupt.Message.Contains("size", StringComparison.OrdinalIgnoreCase) ||
            corrupt.Message.Contains("SHA256", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(corruptBundle));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"entries\":null}")]
    [InlineData("{\"schemaVersion\":1,\"entries\":{\"sha256\":\"not-an-array\"}}")]
    public void Export_RejectsIndexWithoutAnEntriesArray(string indexJson)
    {
        using var fixture = new Fixture();
        fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        var indexPath = Path.Combine(fixture.SourceCache, "asset-cache-index.json");
        File.WriteAllText(indexPath, indexJson);
        var bundlePath = Path.Combine(fixture.Root, "invalid-index.zip");

        var error = Assert.Throws<AssetCacheBundleException>(() =>
            new AssetCacheBundleService().Export(fixture.SourceCache, bundlePath, "4.6.0"));

        Assert.Contains("index", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(bundlePath));
        Assert.Equal(indexJson, File.ReadAllText(indexPath));
    }

    [Fact]
    public void Import_RejectsCacheRootReparsePointBeforeWritingOutsideTheRoot()
    {
        using var fixture = new Fixture();
        var imported = fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        var bundlePath = Path.Combine(fixture.Root, "reparse.zip");
        new AssetCacheBundleService().Export(fixture.SourceCache, bundlePath, "4.6.0");

        var external = Path.Combine(fixture.Root, "external-cache");
        var sentinel = Path.Combine(external, "sentinel.txt");
        Directory.CreateDirectory(external);
        File.WriteAllText(sentinel, "leave me");
        Directory.Delete(fixture.TargetCache, recursive: true);
        CreateDirectoryJunction(fixture.TargetCache, external);

        try
        {
            var error = Assert.Throws<AssetCacheBundleException>(() =>
                new AssetCacheBundleService().Import(fixture.TargetCache, bundlePath));

            Assert.Contains("reparse", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("leave me", File.ReadAllText(sentinel));
            Assert.False(File.Exists(Path.Combine(external, "asset-cache-index.json")));
            Assert.False(File.Exists(Path.Combine(external, imported.Hash)));
        }
        finally
        {
            DeleteDirectoryJunction(fixture.TargetCache);
        }
    }

    [Fact]
    public void Import_RejectsCacheParentReparsePointBeforeCreatingLeaseOrStaging()
    {
        using var fixture = new Fixture();
        fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        var bundlePath = Path.Combine(fixture.Root, "parent-reparse.zip");
        new AssetCacheBundleService().Export(fixture.SourceCache, bundlePath, "4.6.0");

        var external = Path.Combine(fixture.Root, "external-config");
        var sentinel = Path.Combine(external, "sentinel.txt");
        Directory.CreateDirectory(external);
        File.WriteAllText(sentinel, "leave me");
        var targetConfig = Path.GetDirectoryName(fixture.TargetCache)!;
        Directory.Delete(targetConfig, recursive: true);
        CreateDirectoryJunction(targetConfig, external);

        try
        {
            var error = Assert.Throws<AssetCacheBundleException>(() =>
                new AssetCacheBundleService().Import(fixture.TargetCache, bundlePath));

            Assert.Contains("reparse", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("leave me", File.ReadAllText(sentinel));
            Assert.False(File.Exists(Path.Combine(external, AssetCacheLease.LockFileName)));
            Assert.Empty(Directory.EnumerateDirectories(external, ".asset-cache-*", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            DeleteDirectoryJunction(targetConfig);
        }
    }

    [Fact]
    public void Import_FlushesCopiedPreExistingFilesBeforePublication()
    {
        using var fixture = new Fixture();
        fixture.AddSourceAsset("Imported", "https://example.invalid/imported", "imported bytes");
        var existing = fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        var bundlePath = Path.Combine(fixture.Root, "durable-copy.zip");
        new AssetCacheBundleService().Export(fixture.SourceCache, bundlePath, "4.6.0");
        var observed = new List<(string Source, string Destination)>();
        var service = new AssetCacheBundleService(
            transactionObserver: null,
            durableCopyObserver: (source, destination) => observed.Add((source, destination)));

        service.Import(fixture.TargetCache, bundlePath);

        Assert.Contains(observed, copy =>
            string.Equals(copy.Source, Path.Combine(fixture.TargetCache, existing.Hash), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(existing.Bytes, File.ReadAllBytes(Path.Combine(fixture.TargetCache, existing.Hash)));
    }

    [Fact]
    public void Import_RejectsUndeclaredZipEntryBeforeCreatingCache()
    {
        using var fixture = new Fixture();
        fixture.AddSourceAsset("Alpha", "https://example.invalid/alpha", "alpha bytes");
        var bundlePath = Path.Combine(fixture.Root, "extra-entry.zip");
        var target = Path.Combine(fixture.Root, "empty-target", "cache");
        var service = new AssetCacheBundleService();
        service.Export(fixture.SourceCache, bundlePath, "4.6.0");
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
        service.Export(fixture.SourceCache, bundlePath, "4.6.0");
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
        new AssetCacheBundleService().Export(fixture.SourceCache, bundlePath, "4.6.0");
        var original = SnapshotFiles(fixture.TargetCache);
        var observedExistingMove = false;
        var service = new AssetCacheBundleService(stage =>
        {
            if (stage == AssetCacheBundleTransactionStage.ExistingCacheMoved)
            {
                observedExistingMove = true;
                throw new IOException("Simulated commit interruption.");
            }
        });

        var error = Assert.Throws<AssetCacheBundleException>(() => service.Import(fixture.TargetCache, bundlePath));

        Assert.Contains("Simulated commit interruption", error.Message, StringComparison.Ordinal);
        Assert.True(observedExistingMove);
        Assert.Equal(original, SnapshotFiles(fixture.TargetCache));
        Assert.False(File.Exists(Path.Combine(fixture.TargetCache, imported.Hash)));
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(fixture.TargetCache)!, ".asset-cache-rollback-*"));
    }

    [Fact]
    public void Import_RecoversAfterHelperProcessTerminationAtEveryPublicationBoundary()
    {
        using var fixture = new Fixture();
        var imported = fixture.AddSourceAsset("Imported", "https://example.invalid/imported", "imported bytes");
        fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        File.WriteAllText(Path.Combine(fixture.TargetCache, "unindexed-note.txt"), "preserve me");
        var bundlePath = Path.Combine(fixture.Root, "process-death.zip");
        new AssetCacheBundleService().Export(fixture.SourceCache, bundlePath, "4.6.0");
        var original = SnapshotFiles(fixture.TargetCache);
        var stages = new[]
        {
            (Name: "before-first-move", Imported: false),
            (Name: "after-first-move", Imported: false),
            (Name: "before-second-move", Imported: false),
            (Name: "after-second-move", Imported: true),
            (Name: "after-commit-marker", Imported: true)
        };

        foreach (var stage in stages)
        {
            var stageRoot = Path.Combine(fixture.Root, "process-death", stage.Name);
            var cachePath = Path.Combine(stageRoot, "cache");
            var reachedMarkerPath = Path.Combine(stageRoot, "reached.txt");
            CopyDirectory(fixture.TargetCache, cachePath);

            using (var importProcess = StartRecoveryFixture("import", stage.Name, bundlePath, cachePath, reachedMarkerPath))
            {
                var exited = importProcess.WaitForExit(TimeSpan.FromSeconds(30));
                if (!exited)
                {
                    importProcess.Kill(entireProcessTree: true);
                    importProcess.WaitForExit();
                }

                Assert.True(exited, $"The recovery fixture did not terminate at {stage.Name}.");
                Assert.NotEqual(0, importProcess.ExitCode);
            }
            Assert.Equal(stage.Name, File.ReadAllText(reachedMarkerPath));

            using (var recoveryProcess = StartRecoveryFixture(
                       "recover",
                       cachePath,
                       imported.Hash,
                       stage.Imported.ToString()))
            {
                var exited = recoveryProcess.WaitForExit(TimeSpan.FromSeconds(30));
                if (!exited)
                {
                    recoveryProcess.Kill(entireProcessTree: true);
                    recoveryProcess.WaitForExit();
                }

                var output = recoveryProcess.StandardOutput.ReadToEnd();
                var error = recoveryProcess.StandardError.ReadToEnd();
                Assert.True(exited, $"The recovery fixture did not finish for {stage.Name}. {error}");
                Assert.True(
                    recoveryProcess.ExitCode == 0,
                    $"{stage.Name}: {output}{Environment.NewLine}{error}");
            }

            if (stage.Imported)
            {
                Assert.Equal(imported.Bytes, File.ReadAllBytes(Path.Combine(cachePath, imported.Hash)));
                Assert.Equal("preserve me", File.ReadAllText(Path.Combine(cachePath, "unindexed-note.txt")));
                using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(cachePath, "asset-cache-index.json")));
                Assert.Equal(2, index.RootElement.GetProperty("entries").GetArrayLength());
            }
            else
            {
                Assert.Equal(original, SnapshotFiles(cachePath));
            }

            var configDirectory = Path.GetDirectoryName(cachePath)!;
            Assert.False(File.Exists(Path.Combine(configDirectory, AssetCacheTransactionRecovery.MarkerFileName)));
            Assert.Empty(Directory.EnumerateDirectories(configDirectory, ".asset-cache-*", SearchOption.TopDirectoryOnly));
        }
    }

    [Fact]
    public void TransactionRecovery_RestoresOriginalAfterProcessDeathBeforeReplacementMove()
    {
        using var fixture = new Fixture();
        fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        File.WriteAllText(Path.Combine(fixture.TargetCache, "unindexed-note.txt"), "preserve me");
        var original = SnapshotFiles(fixture.TargetCache);
        var paths = CreateOwnedTransactionPaths(fixture.TargetCache);
        var imported = new AssetCacheBundleEntry(
            new string('a', 64),
            "Imported",
            null,
            1,
            null,
            null,
            "2026-09-04T00:00:00Z");

        _ = AssetCacheTransactionRecovery.Begin(
            fixture.TargetCache,
            paths.Staging,
            paths.Replacement,
            paths.Rollback,
            [imported]);
        Directory.Move(fixture.TargetCache, paths.Rollback);

        AssetCacheTransactionRecovery.Recover(fixture.TargetCache);

        Assert.Equal(original, SnapshotFiles(fixture.TargetCache));
        Assert.False(AssetCacheTransactionRecovery.Exists(fixture.TargetCache));
        Assert.False(Directory.Exists(paths.Staging));
        Assert.False(Directory.Exists(paths.Replacement));
        Assert.False(Directory.Exists(paths.Rollback));
    }

    [Fact]
    public void TransactionRecovery_PublishesVerifiedReplacementAfterProcessDeathAfterSecondMove()
    {
        using var fixture = new Fixture();
        var imported = fixture.AddSourceAsset("Imported", "https://example.invalid/imported", "imported bytes");
        fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        File.WriteAllText(Path.Combine(fixture.TargetCache, "unindexed-note.txt"), "preserve me");
        var paths = CreateOwnedTransactionPaths(fixture.TargetCache);
        CopyDirectory(fixture.TargetCache, paths.Replacement);
        File.Copy(
            Path.Combine(fixture.SourceCache, imported.Hash),
            Path.Combine(paths.Replacement, imported.Hash));
        var replacementIndex = JsonNode.Parse(File.ReadAllText(Path.Combine(paths.Replacement, "asset-cache-index.json")))!.AsObject();
        replacementIndex["entries"]!.AsArray().Add(JsonSerializer.SerializeToNode(new
        {
            sha256 = imported.Hash,
            label = imported.Label,
            sourceUrl = imported.SourceUrl,
            byteSize = imported.Bytes.LongLength,
            firstSeenAtUtc = "2026-09-04T00:00:00Z",
            lastUsedAtUtc = "2026-09-04T00:00:00Z",
            lastVerifiedAtUtc = "2026-09-04T00:00:00Z",
            status = "present",
            quarantinedPath = (string?)null
        }));
        File.WriteAllText(
            Path.Combine(paths.Replacement, "asset-cache-index.json"),
            replacementIndex.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var transaction = AssetCacheTransactionRecovery.Begin(
            fixture.TargetCache,
            paths.Staging,
            paths.Replacement,
            paths.Rollback,
            [new AssetCacheBundleEntry(
                imported.Hash,
                imported.Label,
                imported.SourceUrl,
                imported.Bytes.LongLength,
                "2026-09-04T00:00:00Z",
                "2026-09-04T00:00:00Z",
                "2026-09-04T00:00:00Z")]);
        Directory.Move(fixture.TargetCache, paths.Rollback);
        transaction.MarkExistingMoved();
        Directory.Move(paths.Replacement, fixture.TargetCache);

        AssetCacheTransactionRecovery.Recover(fixture.TargetCache);

        Assert.Equal(imported.Bytes, File.ReadAllBytes(Path.Combine(fixture.TargetCache, imported.Hash)));
        Assert.Equal("preserve me", File.ReadAllText(Path.Combine(fixture.TargetCache, "unindexed-note.txt")));
        Assert.Equal(2, JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.TargetCache, "asset-cache-index.json"))).RootElement.GetProperty("entries").GetArrayLength());
        Assert.False(AssetCacheTransactionRecovery.Exists(fixture.TargetCache));
        Assert.False(Directory.Exists(paths.Staging));
        Assert.False(Directory.Exists(paths.Replacement));
        Assert.False(Directory.Exists(paths.Rollback));
    }

    [Fact]
    public void TransactionRecovery_CleansIncompleteReplacementWhenOriginalCacheWasAbsent()
    {
        using var fixture = new Fixture();
        Directory.Delete(fixture.TargetCache);
        var paths = CreateOwnedTransactionPaths(fixture.TargetCache);
        var imported = new AssetCacheBundleEntry(
            new string('b', 64),
            "Imported",
            null,
            1,
            null,
            null,
            "2026-09-04T00:00:00Z");

        _ = AssetCacheTransactionRecovery.Begin(
            fixture.TargetCache,
            paths.Staging,
            paths.Replacement,
            paths.Rollback,
            [imported]);

        AssetCacheTransactionRecovery.Recover(fixture.TargetCache);

        Assert.False(Directory.Exists(fixture.TargetCache));
        Assert.False(AssetCacheTransactionRecovery.Exists(fixture.TargetCache));
        Assert.False(Directory.Exists(paths.Staging));
        Assert.False(Directory.Exists(paths.Replacement));
        Assert.False(Directory.Exists(paths.Rollback));
    }

    [Fact]
    public void TransactionRecovery_RetainsMarkerWhenRecordedSiblingIsOutsideConfigurationDirectory()
    {
        using var fixture = new Fixture();
        fixture.AddTargetAsset("Existing", "https://example.invalid/existing", "existing bytes");
        var paths = CreateOwnedTransactionPaths(fixture.TargetCache);
        var imported = new AssetCacheBundleEntry(
            new string('c', 64),
            "Imported",
            null,
            1,
            null,
            null,
            "2026-09-04T00:00:00Z");
        _ = AssetCacheTransactionRecovery.Begin(
            fixture.TargetCache,
            paths.Staging,
            paths.Replacement,
            paths.Rollback,
            [imported]);

        var markerPath = Path.Combine(Path.GetDirectoryName(fixture.TargetCache)!, AssetCacheTransactionRecovery.MarkerFileName);
        var outsidePath = Path.Combine(fixture.Root, "outside", $".asset-cache-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outsidePath);
        File.WriteAllText(Path.Combine(outsidePath, "sentinel.txt"), "leave me");
        var marker = JsonNode.Parse(File.ReadAllText(markerPath))!.AsObject();
        marker["stagingDirectory"] = outsidePath;
        File.WriteAllText(markerPath, marker.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var error = Assert.Throws<AssetCacheBundleException>(() => AssetCacheTransactionRecovery.Recover(fixture.TargetCache));

        Assert.Contains("unowned sibling", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(markerPath));
        Assert.True(File.Exists(Path.Combine(outsidePath, "sentinel.txt")));
        Assert.True(Directory.Exists(fixture.TargetCache));
    }

    private static (string Staging, string Replacement, string Rollback) CreateOwnedTransactionPaths(string cachePath)
    {
        var config = Path.GetDirectoryName(cachePath)!;
        var paths = (
            Staging: Path.Combine(config, $".asset-cache-import-{Guid.NewGuid():N}"),
            Replacement: Path.Combine(config, $".asset-cache-ready-{Guid.NewGuid():N}"),
            Rollback: Path.Combine(config, $".asset-cache-rollback-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(paths.Staging);
        Directory.CreateDirectory(paths.Replacement);
        return paths;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
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

    private static Process StartRecoveryFixture(params string[] arguments)
    {
        var fixturePath = FindRecoveryFixture();
        var startInfo = new ProcessStartInfo
        {
            FileName = fixturePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "dotnet" : fixturePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (fixturePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            startInfo.ArgumentList.Add(fixturePath);
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the asset-cache recovery fixture.");
    }

    private static string FindRecoveryFixture()
    {
        var repoRoot = ResolveRepoRoot();
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "LibreSpot.AssetCacheRecoveryFixture.exe"),
            Path.Combine(AppContext.BaseDirectory, "LibreSpot.AssetCacheRecoveryFixture.dll"),
            Path.Combine(repoRoot, "tests", "LibreSpot.AssetCacheRecoveryFixture", "bin", "Debug", "net10.0-windows", "LibreSpot.AssetCacheRecoveryFixture.exe"),
            Path.Combine(repoRoot, "tests", "LibreSpot.AssetCacheRecoveryFixture", "bin", "Debug", "net10.0-windows", "LibreSpot.AssetCacheRecoveryFixture.dll")
        };

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("The asset-cache recovery fixture was not built.");
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "README.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the LibreSpot repository root.");
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{targetPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start junction fixture process.");
        using (process)
        {
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Could not create junction fixture: {process.StandardError.ReadToEnd()}");
            }
        }
    }

    private static void DeleteDirectoryJunction(string junctionPath)
    {
        try
        {
            if (Directory.Exists(junctionPath) || File.Exists(junctionPath))
            {
                Directory.Delete(junctionPath);
            }
        }
        catch
        {
        }
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
