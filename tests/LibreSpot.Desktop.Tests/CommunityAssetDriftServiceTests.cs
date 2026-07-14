using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using System.Text.Json.Nodes;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CommunityAssetDriftServiceTests
{
    [Fact]
    public async Task GetReportAsync_ClassifiesCurrentBehindMissingAndReviewRequiredAssets()
    {
        var root = NewTempRoot();
        try
        {
            var pins = new[]
            {
                Pin("extension:current.js", "Current Extension", CurrentCommit, "MIT", requiresTrustReview: false),
                Pin("theme:behind", "Behind Theme", OldCommit, "AGPL-3.0-only", requiresTrustReview: true),
                Pin("custom-app:missing", "Missing App", MissingCommit, "MIT", requiresTrustReview: false)
            };
            var client = new FakeMetadataClient();
            client.GitResults["extension:current.js"] = UpstreamMetadataLookupResult.Found(CurrentCommit, "git ls-remote");
            client.GitResults["theme:behind"] = UpstreamMetadataLookupResult.Found(NewCommit, "git ls-remote");
            client.GitResults["custom-app:missing"] = UpstreamMetadataLookupResult.Missing("git ls-remote", "No matching ref was returned.");
            var service = CreateService(root, client, pins);

            var report = await service.GetReportAsync();

            var current = Assert.Single(report.Assets, asset => asset.Id == "extension:current.js");
            Assert.Equal("current", current.DriftState);
            Assert.Equal(HealthSeverity.Ready, SeverityForTest(current));
            Assert.False(current.RequiresTrustReview);
            Assert.Equal(ProvenanceFreshness.Current, current.FreshnessStatus);
            Assert.Equal(pins[0].ReleaseNotesUrl, current.ReleaseNotesUrl);
            Assert.Equal(pins[0].LastVerifiedAtUtc, current.LastVerifiedAtUtc);
            Assert.Contains(CurrentCommit, current.Evidence);
            Assert.Contains("network local-only", current.Evidence);

            var behind = Assert.Single(report.Assets, asset => asset.Id == "theme:behind");
            Assert.Equal("behind", behind.DriftState);
            Assert.Equal(NewCommit, behind.LatestCommit);
            Assert.True(behind.RequiresTrustReview);
            Assert.Equal(ProvenanceFreshness.Stale, behind.FreshnessStatus);
            Assert.Contains("license AGPL-3.0-only", behind.Evidence);

            var missing = Assert.Single(report.Assets, asset => asset.Id == "custom-app:missing");
            Assert.Equal("missing", missing.DriftState);
            Assert.True(missing.IsDegraded);
            Assert.Equal(ProvenanceFreshness.Missing, missing.FreshnessStatus);
            Assert.Null(missing.LatestCommit);
            Assert.Contains("No matching ref", missing.Evidence);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task GetReportAsync_UsesCachedCommunityMetadataWhenOffline()
    {
        var root = NewTempRoot();
        var now = DateTimeOffset.Parse("2026-06-30T12:00:00Z");
        var pins = new[] { Pin("extension:cached.js", "Cached Extension", CurrentCommit, "MIT", requiresTrustReview: false) };
        try
        {
            var onlineClient = new FakeMetadataClient();
            onlineClient.GitResults["extension:cached.js"] = UpstreamMetadataLookupResult.Found(CurrentCommit, "git ls-remote");
            var online = CreateService(root, onlineClient, pins, () => now.AddHours(-3));
            await online.GetReportAsync();

            var offlineClient = new FakeMetadataClient();
            offlineClient.GitResults["extension:cached.js"] = UpstreamMetadataLookupResult.Offline("git ls-remote", "network unavailable");
            var offline = CreateService(root, offlineClient, pins, () => now);

            var report = await offline.GetReportAsync();

            var asset = Assert.Single(report.Assets);
            Assert.Equal("cache", asset.MetadataSource);
            Assert.Equal("current", asset.DriftState);
            Assert.True(asset.IsDegraded);
            Assert.Equal(ProvenanceFreshness.Indeterminate, asset.FreshnessStatus);
            Assert.True(asset.CacheAge >= TimeSpan.FromHours(3));
            Assert.Contains("Live community asset metadata is degraded", asset.Evidence);
            Assert.Contains("network unavailable", asset.Evidence);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task GetReportAsync_IgnoresNullAndDuplicateCommunityCacheEntries()
    {
        var root = NewTempRoot();
        var pins = new[] { Pin("extension:cached.js", "Cached Extension", CurrentCommit, "MIT", requiresTrustReview: false) };
        try
        {
            var onlineClient = new FakeMetadataClient();
            onlineClient.GitResults["extension:cached.js"] = UpstreamMetadataLookupResult.Found(CurrentCommit, "git ls-remote");
            await CreateService(root, onlineClient, pins).GetReportAsync();

            var cachePath = Path.Combine(root, "community-cache.json");
            var cache = JsonNode.Parse(await File.ReadAllTextAsync(cachePath))!.AsObject();
            var assets = cache["assets"]!.AsArray();
            assets.Add(assets[0]!.DeepClone());
            assets.Add(null);
            var missingId = assets[0]!.DeepClone().AsObject();
            missingId["id"] = null;
            assets.Add(missingId);
            await File.WriteAllTextAsync(cachePath, cache.ToJsonString());

            var report = await CreateService(root, new FakeMetadataClient(), pins)
                .GetReportAsync(allowNetwork: false);

            var asset = Assert.Single(report.Assets);
            Assert.Equal("cache", asset.MetadataSource);
            Assert.Equal(CurrentCommit, asset.LatestCommit);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void LoadPinsFromManifest_IncludesEveryCommunityAssetWithBranchAndTrustMetadata()
    {
        var pins = CommunityAssetDriftService.LoadPinsFromManifest();

        Assert.Contains(pins, pin => pin.Id == "extension:beautiful-lyrics.mjs" && pin.Branch == "main" && pin.RequiresTrustReview);
        Assert.Contains(pins, pin => pin.Id == "extension:volumePercentage.js" && pin.Branch == "master");
        Assert.Contains(pins, pin => pin.Id == "theme:Hazy" && pin.RequiresTrustReview);
        Assert.Contains(pins, pin => pin.Id == "custom-app:stats" && pin.NetworkBehavior == "third-party-service");
        Assert.All(pins, pin =>
        {
            Assert.Matches(@"^https://github\.com/.+\.git$", $"https://github.com/{pin.Owner}/{pin.Repository}.git");
            Assert.Matches(@"^[a-f0-9]{40}$", pin.PinnedCommit);
            Assert.False(string.IsNullOrWhiteSpace(pin.Branch));
            Assert.False(string.IsNullOrWhiteSpace(pin.FallbackBehavior));
            Assert.False(string.IsNullOrWhiteSpace(pin.NetworkBehavior));
            Assert.StartsWith("https://github.com/", pin.ReleaseNotesUrl);
            Assert.True(pin.LastVerifiedAtUtc.HasValue);
        });
    }

    private static CommunityAssetDriftService CreateService(
        string root,
        FakeMetadataClient client,
        IReadOnlyList<CommunityAssetPin> pins,
        Func<DateTimeOffset>? clock = null) =>
        new(
            client,
            pins,
            Path.Combine(root, "community-cache.json"),
            clock ?? (() => DateTimeOffset.Parse("2026-06-30T12:00:00Z")));

    private static CommunityAssetPin Pin(
        string id,
        string name,
        string commit,
        string license,
        bool requiresTrustReview) =>
        new(
            id,
            id.StartsWith("theme:", StringComparison.Ordinal) ? "theme" : id.StartsWith("custom-app:", StringComparison.Ordinal) ? "custom-app" : "extension",
            name,
            "owner",
            "repo",
            "main",
            commit,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "https://example.test/source",
            license,
            "active",
            "skip-with-warning",
            "local-only",
            null,
            requiresTrustReview)
        {
            ReleaseNotesUrl = $"https://github.com/owner/repo/compare/{commit}...main",
            LastVerifiedAtUtc = DateTimeOffset.Parse("2026-06-29T00:00:00Z")
        };

    private static string NewTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string SeverityForTest(CommunityAssetState asset) =>
        asset.IsDegraded || asset.RequiresTrustReview || asset.DriftState == "behind"
            ? HealthSeverity.Info
            : HealthSeverity.Ready;

    private const string CurrentCommit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OldCommit = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string NewCommit = "cccccccccccccccccccccccccccccccccccccccc";
    private const string MissingCommit = "dddddddddddddddddddddddddddddddddddddddd";

    private sealed class FakeMetadataClient : IUpstreamMetadataClient
    {
        public Dictionary<string, UpstreamMetadataLookupResult> GitResults { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<UpstreamMetadataLookupResult> TryGetLatestReleaseAsync(
            UpstreamDependencyPin pin,
            CancellationToken cancellationToken) =>
            Task.FromResult(UpstreamMetadataLookupResult.Unavailable("GitHub REST", "not configured"));

        public Task<UpstreamMetadataLookupResult> TryGetGitReferenceAsync(
            UpstreamDependencyPin pin,
            CancellationToken cancellationToken) =>
            Task.FromResult(GitResults.TryGetValue(pin.Id, out var result)
                ? result
                : UpstreamMetadataLookupResult.Unavailable("git ls-remote", "not configured"));
    }
}
