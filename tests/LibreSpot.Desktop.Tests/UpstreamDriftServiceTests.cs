using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class UpstreamDriftServiceTests
{
    [Fact]
    public async Task GetReportAsync_UsesRestLatestReleaseWhenAvailable()
    {
        var root = NewTempRoot();
        try
        {
            var client = new FakeMetadataClient
            {
                RestResult = UpstreamMetadataLookupResult.Found("v1.0.10", "GitHub REST"),
                GitResult = UpstreamMetadataLookupResult.Unavailable("git ls-remote", "not used")
            };
            var service = CreateService(root, client);

            var report = await service.GetReportAsync();

            var dependency = Assert.Single(report.Dependencies);
            Assert.Equal("marketplace", dependency.Id);
            Assert.Equal("1.0.9", dependency.CurrentValue);
            Assert.Equal("1.0.10", dependency.LatestValue);
            Assert.Equal("behind", dependency.DriftState);
            Assert.Equal("GitHub REST", dependency.MetadataSource);
            Assert.False(dependency.IsDegraded);
            Assert.Equal(1, client.RestCalls);
            Assert.Equal(0, client.GitCalls);
            Assert.True(File.Exists(CachePath(root)));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task GetReportAsync_FallsBackToGitWhenRestRateLimits()
    {
        var root = NewTempRoot();
        try
        {
            var client = new FakeMetadataClient
            {
                RestResult = UpstreamMetadataLookupResult.RateLimited("GitHub REST", "HTTP 403 while reading latest release."),
                GitResult = UpstreamMetadataLookupResult.Found("v1.0.9", "git ls-remote")
            };
            var service = CreateService(root, client);

            var report = await service.GetReportAsync();

            var dependency = Assert.Single(report.Dependencies);
            Assert.Equal("current", dependency.DriftState);
            Assert.Equal("git ls-remote", dependency.MetadataSource);
            Assert.Equal("1.0.9", dependency.LatestValue);
            Assert.False(dependency.IsDegraded);
            Assert.Equal(1, client.RestCalls);
            Assert.Equal(1, client.GitCalls);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task GetReportAsync_UsesCachedMetadataWhenOffline()
    {
        var root = NewTempRoot();
        var now = DateTimeOffset.Parse("2026-06-29T12:00:00Z");
        try
        {
            var onlineClient = new FakeMetadataClient
            {
                RestResult = UpstreamMetadataLookupResult.Found("v1.0.9", "GitHub REST"),
                GitResult = UpstreamMetadataLookupResult.Unavailable("git ls-remote", "not used")
            };
            var online = CreateService(root, onlineClient, () => now.AddHours(-2));
            await online.GetReportAsync();

            var offlineClient = new FakeMetadataClient
            {
                RestResult = UpstreamMetadataLookupResult.Offline("GitHub REST", "DNS failure"),
                GitResult = UpstreamMetadataLookupResult.Offline("git ls-remote", "network unavailable")
            };
            var offline = CreateService(root, offlineClient, () => now);

            var report = await offline.GetReportAsync();

            var dependency = Assert.Single(report.Dependencies);
            Assert.Equal("cache", dependency.MetadataSource);
            Assert.Equal("1.0.9", dependency.LatestValue);
            Assert.Equal("current", dependency.DriftState);
            Assert.True(dependency.IsDegraded);
            Assert.True(dependency.CacheAge >= TimeSpan.FromHours(2));
            Assert.Contains("Live upstream metadata is degraded", dependency.Evidence);
            Assert.Contains("GitHub REST offline", dependency.Evidence);
            Assert.Contains("git ls-remote offline", dependency.Evidence);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static UpstreamDriftService CreateService(
        string root,
        FakeMetadataClient client,
        Func<DateTimeOffset>? clock = null) =>
        new(
            client,
            new[] { MarketplacePin },
            CachePath(root),
            clock ?? (() => DateTimeOffset.Parse("2026-06-29T12:00:00Z")));

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

    private static string CachePath(string root) =>
        Path.Combine(root, "upstream-cache.json");

    private static readonly UpstreamDependencyPin MarketplacePin = new(
        "marketplace",
        "Marketplace",
        "1.0.9",
        "version",
        "https://github.com/spicetify/marketplace.git",
        "refs/tags/v*",
        "https://api.github.com/repos/spicetify/marketplace/releases/latest",
        "v");

    private sealed class FakeMetadataClient : IUpstreamMetadataClient
    {
        public UpstreamMetadataLookupResult RestResult { get; init; } =
            UpstreamMetadataLookupResult.Unavailable("GitHub REST", "not configured");

        public UpstreamMetadataLookupResult GitResult { get; init; } =
            UpstreamMetadataLookupResult.Unavailable("git ls-remote", "not configured");

        public int RestCalls { get; private set; }
        public int GitCalls { get; private set; }

        public Task<UpstreamMetadataLookupResult> TryGetLatestReleaseAsync(
            UpstreamDependencyPin pin,
            CancellationToken cancellationToken)
        {
            RestCalls++;
            return Task.FromResult(RestResult);
        }

        public Task<UpstreamMetadataLookupResult> TryGetGitReferenceAsync(
            UpstreamDependencyPin pin,
            CancellationToken cancellationToken)
        {
            GitCalls++;
            return Task.FromResult(GitResult);
        }
    }
}
