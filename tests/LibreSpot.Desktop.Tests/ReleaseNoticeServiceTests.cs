using System.IO;
using System.Text.Json;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ReleaseNoticeServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-02T12:00:00Z");

    [Theory]
    [InlineData("4.1.2", "4.1.3", true)]
    [InlineData("4.1.2", "4.2.0", true)]
    [InlineData("4.1.2", "5.0.0", true)]
    [InlineData("4.1.2", "4.1.10", true)]
    [InlineData("4.0.0-preview.28", "4.0.0", true)]
    [InlineData("4.0.0-preview.9", "4.0.0-preview.28", true)]
    [InlineData("4.1.2", "4.1.2", false)]
    [InlineData("4.1.2", "4.1.1", false)]
    [InlineData("4.1.2", "4.0.9", false)]
    [InlineData("4.1.2", "3.9.9", false)]
    [InlineData("4.1.2", "4.1.3-rc.1", true)]
    [InlineData("4.1.3-rc.1", "4.1.3", true)]
    public void ReleaseVersion_OrdersSemanticVersions(string current, string candidate, bool candidateIsNewer)
    {
        Assert.True(ReleaseVersion.TryParse(current, out var currentVersion));
        Assert.True(ReleaseVersion.TryParse("v" + candidate, out var candidateVersion));

        Assert.Equal(candidateIsNewer, candidateVersion.CompareTo(currentVersion) > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("4")]
    [InlineData("4.1.2.3")]
    [InlineData("4.1.x")]
    [InlineData("4.1.2-")]
    public void ReleaseVersion_RejectsNonVersions(string value)
    {
        Assert.False(ReleaseVersion.TryParse(value, out _));
    }

    [Fact]
    public async Task NewerStableRelease_ProducesNoticeAndWritesCache()
    {
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.Found("v4.2.0", "https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.2.0", false, "\"etag-1\""));
        var service = Create(root, client);

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.True(notice.UpdateAvailable);
        Assert.Equal("4.2.0", notice.LatestVersion);
        Assert.Equal("https://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.2.0", notice.ReleaseUrl);
        Assert.Equal("live", notice.Source);
        Assert.True(File.Exists(root.CachePath));
        using var cache = JsonDocument.Parse(File.ReadAllText(root.CachePath));
        Assert.Equal("\"etag-1\"", cache.RootElement.GetProperty("eTag").GetString());
        Assert.Equal("v4.2.0", cache.RootElement.GetProperty("tagName").GetString());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("http://github.com/SysAdminDoc/LibreSpot/releases/tag/v4.2.0")]
    [InlineData("https://example.com/SysAdminDoc/LibreSpot/releases/tag/v4.2.0")]
    [InlineData("https://github.com/someone-else/LibreSpot/releases/tag/v4.2.0")]
    public async Task UntrustedReleaseUrl_FallsBackToTheReleasesPage(string htmlUrl)
    {
        using var root = new TempRoot();
        var service = Create(root, new FakeClient(ReleaseNoticeLookup.Found("v4.2.0", htmlUrl, false, null)));

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.True(notice.UpdateAvailable);
        Assert.Equal(ReleaseNoticeService.LatestStableReleasePage, notice.ReleaseUrl);
        Assert.False(ReleaseNoticeService.IsTrustedReleaseUrl(htmlUrl));
    }

    [Fact]
    public async Task CurrentRelease_StaysSilent()
    {
        using var root = new TempRoot();
        var service = Create(root, new FakeClient(ReleaseNoticeLookup.Found("v4.1.2", null, false, null)));

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.False(notice.UpdateAvailable);
        Assert.Null(notice.LatestVersion);
    }

    [Fact]
    public async Task PrereleaseLatest_IsNeverOfferedOrCached()
    {
        using var root = new TempRoot();
        var service = Create(root, new FakeClient(ReleaseNoticeLookup.Found("v5.0.0-preview.1", null, true, null)));

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.False(notice.UpdateAvailable);
        Assert.False(File.Exists(root.CachePath));
    }

    [Fact]
    public async Task PrereleaseTagWithoutFlag_IsStillNotOffered()
    {
        using var root = new TempRoot();
        var service = Create(root, new FakeClient(ReleaseNoticeLookup.Found("v5.0.0-rc.1", null, false, null)));

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.False(notice.UpdateAvailable);
    }

    [Fact]
    public async Task FreshCache_SkipsTheNetwork()
    {
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.Offline("should not be called"));
        WriteCache(root, Now.AddHours(-23), "v4.3.0", "\"etag-cached\"");
        var service = Create(root, client);

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.True(notice.UpdateAvailable);
        Assert.Equal("4.3.0", notice.LatestVersion);
        Assert.Equal("cache", notice.Source);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task ExpiredCache_RefetchesWithTheCachedETag()
    {
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.Found("v4.3.1", null, false, "\"etag-new\""));
        WriteCache(root, Now.AddHours(-25), "v4.3.0", "\"etag-cached\"");
        var service = Create(root, client);

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.Equal(1, client.Calls);
        Assert.Equal("\"etag-cached\"", client.LastETag);
        Assert.Equal("4.3.1", notice.LatestVersion);
        Assert.Equal("live", notice.Source);
    }

    [Fact]
    public async Task NotModified_KeepsTheCachedReleaseAndRefreshesItsAge()
    {
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.NotModified("\"etag-cached\""));
        WriteCache(root, Now.AddHours(-30), "v4.3.0", "\"etag-cached\"");
        var service = Create(root, client);

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.True(notice.UpdateAvailable);
        Assert.Equal("4.3.0", notice.LatestVersion);
        Assert.Equal("live-conditional", notice.Source);
        using var cache = JsonDocument.Parse(File.ReadAllText(root.CachePath));
        Assert.Equal(Now, cache.RootElement.GetProperty("checkedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task RateLimited_UsesTheStaleCache()
    {
        using var root = new TempRoot();
        WriteCache(root, Now.AddDays(-3), "v4.3.0", null);
        var service = Create(root, new FakeClient(ReleaseNoticeLookup.RateLimited("HTTP 403")));

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.True(notice.UpdateAvailable);
        Assert.Equal("cache-stale", notice.Source);
    }

    // RateLimited is deliberately not in this set any more. Writing nothing is
    // right for a failure the server did not ask us to slow down over, but for a
    // rate limit it meant asking again on the very next launch, which is what
    // GetNoticeAsync_ARateLimitedResponseBacksOffEvenWithNothingCached now covers.
    // The two are mutually exclusive by design, not by oversight.
    [Theory]
    [InlineData(ReleaseNoticeLookupStatus.Offline)]
    [InlineData(ReleaseNoticeLookupStatus.Missing)]
    [InlineData(ReleaseNoticeLookupStatus.Malformed)]
    [InlineData(ReleaseNoticeLookupStatus.NotModified)]
    public async Task FailuresWithoutCache_StaySilentAndWriteNothing(ReleaseNoticeLookupStatus status)
    {
        using var root = new TempRoot();
        var service = Create(root, new FakeClient(new ReleaseNoticeLookup(status, null, null, false, null, "failure")));

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.False(notice.UpdateAvailable);
        Assert.Null(notice.ReleaseUrl);
        Assert.False(File.Exists(root.CachePath));
    }

    [Fact]
    public async Task Cancellation_StaysSilentWithoutThrowingOrCaching()
    {
        using var root = new TempRoot();
        using var cancellation = new CancellationTokenSource();
        var client = new FakeClient(ReleaseNoticeLookup.Found("v9.9.9", null, false, null)) { CancelOnCall = cancellation };
        var service = Create(root, client);

        var notice = await service.GetNoticeAsync("4.1.2", cancellation.Token);

        Assert.False(notice.UpdateAvailable);
        Assert.Equal("canceled", notice.Source);
        Assert.False(File.Exists(root.CachePath));
    }

    [Fact]
    public async Task CorruptCache_IsTreatedAsAbsent()
    {
        using var root = new TempRoot();
        Directory.CreateDirectory(Path.GetDirectoryName(root.CachePath)!);
        File.WriteAllText(root.CachePath, "{ not json");
        var client = new FakeClient(ReleaseNoticeLookup.Found("v4.1.3", null, false, null));
        var service = Create(root, client);

        var notice = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.Equal(1, client.Calls);
        Assert.Null(client.LastETag);
        Assert.True(notice.UpdateAvailable);
    }

    [Fact]
    public async Task UnreadableCurrentVersion_StaysSilentWithoutANetworkCall()
    {
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.Found("v4.1.3", null, false, null));
        var service = Create(root, client);

        var notice = await service.GetNoticeAsync("unknown", CancellationToken.None);

        Assert.False(notice.UpdateAvailable);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task GetNoticeAsync_InsideTheCacheWindowMakesNoRequestEvenThoughTheServerWouldAnswer304()
    {
        // The ETag saves bandwidth, not budget: an anonymous conditional request
        // still costs one of the 60 an hour. Only the cache window keeps the
        // request count down, so it has to hold even when a 304 is on offer.
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.NotModified("W/\"etag\""));
        var service = Create(root, client);
        WriteCache(root, Now.AddHours(-1), "v4.1.2", "W/\"etag\"");

        var first = await service.GetNoticeAsync("4.1.2", CancellationToken.None);
        var second = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.Equal(0, client.Calls);
        Assert.Equal("cache", first.Source);
        Assert.Equal("cache", second.Source);
    }

    [Fact]
    public async Task GetNoticeAsync_OnceTheCacheExpiresMakesExactlyOneRequestPerCall()
    {
        // The companion to the test above: proves the zero above comes from the
        // cache window and not from the service never calling the client at all.
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.NotModified("W/\"etag\""));
        var service = Create(root, client);
        WriteCache(root, Now.AddHours(-25), "v4.1.2", "W/\"etag\"");

        await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.Equal(1, client.Calls);
        Assert.Equal("W/\"etag\"", client.LastETag);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    public async Task GetNoticeAsync_ARateLimitedResponseBacksOffInsteadOfRetryingOnTheNextCall(int statusCode)
    {
        // Retrying immediately after being told to slow down is how the rest of
        // an anonymous hourly budget disappears. The cache window has to restart.
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.RateLimited($"HTTP {statusCode} while reading the latest release."));
        var service = Create(root, client);
        WriteCache(root, Now.AddHours(-25), "v4.1.3", "W/\"etag\"");

        var first = await service.GetNoticeAsync("4.1.2", CancellationToken.None);
        var second = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.Equal(1, client.Calls);
        Assert.Equal("cache-stale", first.Source);
        Assert.Equal("cache", second.Source);

        // The release already known is still offered while backing off.
        Assert.True(first.UpdateAvailable);
        Assert.True(second.UpdateAvailable);
    }

    [Fact]
    public async Task GetNoticeAsync_ARateLimitedResponseBacksOffEvenWithNothingCached()
    {
        // The machine that needs this most is the one with no cache: a fresh
        // install behind a shared address can arrive already over the anonymous
        // limit, and it used to ask again on every single launch, silently.
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.RateLimited("HTTP 429 while reading the latest release."));
        var service = Create(root, client);

        for (var i = 0; i < 5; i++)
        {
            await service.GetNoticeAsync("4.1.2", CancellationToken.None);
        }

        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task GetNoticeAsync_BackingOffDoesNotMakeAnOldReleaseLookFreshlyConfirmed()
    {
        // The backoff moves the throttle, not the freshness. Storing a refusal as
        // if it were a successful read would let a run of them describe a release
        // confirmed two months ago as less than a day old, forever.
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.RateLimited("HTTP 403 while reading the latest release."));
        var service = Create(root, client);
        WriteCache(root, Now.AddDays(-60), "v4.1.3", "W/\"etag\"");

        var refused = await service.GetNoticeAsync("4.1.2", CancellationToken.None);
        var afterBackoff = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.Equal(1, client.Calls);
        Assert.Equal("cache", afterBackoff.Source);
        Assert.DoesNotContain("less than a day old", afterBackoff.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("refused", afterBackoff.Reason, StringComparison.OrdinalIgnoreCase);

        // The release itself is still offered; only the wording changes.
        Assert.True(refused.UpdateAvailable);
        Assert.True(afterBackoff.UpdateAvailable);
    }

    [Fact]
    public async Task GetNoticeAsync_ReadsLiveThenServesTheCacheItJustWrote()
    {
        // The criterion's literal wording: the first call goes out, the second is
        // served by the cache the first one wrote, not by a cache set up by hand.
        using var root = new TempRoot();
        var client = new FakeClient(ReleaseNoticeLookup.Found("v4.1.3", null, isPrerelease: false, etag: "W/\"etag\""));
        var service = Create(root, client);

        var first = await service.GetNoticeAsync("4.1.2", CancellationToken.None);
        var second = await service.GetNoticeAsync("4.1.2", CancellationToken.None);

        Assert.Equal(1, client.Calls);
        Assert.Equal("live", first.Source);
        Assert.Equal("cache", second.Source);
        Assert.True(second.UpdateAvailable);
    }

    private static ReleaseNoticeService Create(TempRoot root, FakeClient client) =>
        new(client, root.CachePath, () => Now);

    private static void WriteCache(TempRoot root, DateTimeOffset checkedAtUtc, string tag, string? etag)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(root.CachePath)!);
        File.WriteAllText(root.CachePath, JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            checkedAtUtc,
            // A hand-written cache stands for one that was genuinely fetched then,
            // which is what makes the staleness assertions mean anything.
            fetchedAtUtc = checkedAtUtc,
            tagName = tag,
            htmlUrl = (string?)null,
            eTag = etag
        }));
    }

    private sealed class FakeClient : IReleaseNoticeClient
    {
        private readonly ReleaseNoticeLookup _result;

        public FakeClient(ReleaseNoticeLookup result) => _result = result;

        public int Calls { get; private set; }
        public string? LastETag { get; private set; }
        public CancellationTokenSource? CancelOnCall { get; init; }

        public Task<ReleaseNoticeLookup> TryGetLatestStableAsync(string? cachedETag, CancellationToken cancellationToken)
        {
            Calls++;
            LastETag = cachedETag;
            if (CancelOnCall is not null)
            {
                CancelOnCall.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Task.FromResult(_result);
        }
    }

    private sealed class TempRoot : IDisposable
    {
        public TempRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", "release-notice", Guid.NewGuid().ToString("N"));
            CachePath = Path.Combine(Root, "release-notice-cache.json");
        }

        public string Root { get; }
        public string CachePath { get; }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }
}
