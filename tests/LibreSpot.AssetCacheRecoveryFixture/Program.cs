using System.Security.Cryptography;
using System.Text.Json;
using LibreSpot.Desktop.Services;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault()?.ToLowerInvariant() switch
            {
                "import" => RunImport(args),
                "recover" => RunRecovery(args),
                _ => throw new ArgumentException("Usage: import <stage> <bundle> <cache> | recover <cache> <expected-hash> <expect-imported>")
            };
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 2;
        }
    }

    private static int RunImport(string[] args)
    {
        if (args.Length != 4)
            throw new ArgumentException("Usage: import <stage> <bundle> <cache>");

        var stage = ParseStage(args[1]);
        var bundlePath = Path.GetFullPath(args[2]);
        var cachePath = Path.GetFullPath(args[3]);
        var service = new AssetCacheBundleService(observedStage =>
        {
            if (observedStage == stage)
                Environment.FailFast($"Asset-cache recovery fixture terminated at {stage}.");
        });

        service.Import(cachePath, bundlePath);
        return 0;
    }

    private static int RunRecovery(string[] args)
    {
        if (args.Length != 4 || !bool.TryParse(args[3], out var expectImported))
            throw new ArgumentException("Usage: recover <cache> <expected-hash> <expect-imported>");

        var cachePath = Path.GetFullPath(args[1]);
        var expectedHash = args[2];
        AssetCacheTransactionRecovery.Recover(cachePath);

        var indexPath = Path.Combine(cachePath, "asset-cache-index.json");
        using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
        var hashes = index.RootElement
            .GetProperty("entries")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("sha256").GetString() ?? string.Empty)
            .ToArray();

        if (hashes.Length == 0 || hashes.Contains(string.Empty, StringComparer.Ordinal))
            throw new InvalidDataException("The recovered index has no usable entries.");

        foreach (var hash in hashes)
        {
            var path = Path.Combine(cachePath, hash);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Recovered cache entry is missing: {hash}", path);

            using var stream = File.OpenRead(path);
            var observedHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!string.Equals(observedHash, hash, StringComparison.Ordinal))
                throw new InvalidDataException($"Recovered cache entry {hash} has hash {observedHash}.");
        }

        var hasImportedEntry = hashes.Contains(expectedHash, StringComparer.Ordinal);
        if (hasImportedEntry != expectImported)
            throw new InvalidDataException($"Recovered cache imported-entry state was {hasImportedEntry}, expected {expectImported}.");

        Console.WriteLine($"Recovered and verified {hashes.Length} cache entries.");
        return 0;
    }

    private static AssetCacheBundleTransactionStage ParseStage(string value) =>
        value.ToLowerInvariant() switch
        {
            "before-first-move" => AssetCacheBundleTransactionStage.BeforeExistingCacheMove,
            "after-first-move" => AssetCacheBundleTransactionStage.ExistingCacheMoved,
            "before-second-move" => AssetCacheBundleTransactionStage.BeforeReplacementMove,
            "after-second-move" => AssetCacheBundleTransactionStage.ReplacementMoved,
            "after-commit-marker" => AssetCacheBundleTransactionStage.CommittedMarkerWritten,
            _ => throw new ArgumentException($"Unknown cache publication stage: {value}")
        };
}
