using System.Net;
using System.Security.Cryptography;
using System.Text;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CustomPatchServiceTests
{
    [Fact]
    public void Validate_AcceptsSpotXStyleMatchReplaceJson()
    {
        var service = new CustomPatchService();
        const string json = """
        {
          "xpui": {
            "ads": {
              "match": ["adsEnabled\\s*:\\s*true"],
              "replace": ["adsEnabled:false"]
            }
          }
        }
        """;

        var result = service.Validate(json, enabled: true);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.PatchGroupCount);
        Assert.Equal(1, result.PatternCount);
        Assert.Equal(1, result.ReplacementCount);
    }

    [Fact]
    public void Validate_BlocksInvalidRegex()
    {
        var service = new CustomPatchService();
        const string json = """
        {
          "xpui": {
            "match": ["("],
            "replace": ["noop"]
          }
        }
        """;

        var result = service.Validate(json, enabled: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("valid .NET regex", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BlocksMismatchedMatchReplaceArrays()
    {
        var service = new CustomPatchService();
        const string json = """
        {
          "xpui": {
            "match": ["one", "two"],
            "replace": ["one"]
          }
        }
        """;

        var result = service.Validate(json, enabled: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("match value", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BlocksOversizedJson()
    {
        var service = new CustomPatchService();
        var json = "{\"xpui\":{\"match\":\"" + new string('a', CustomPatchService.MaxPatchJsonBytes) + "\",\"replace\":\"noop\"}}";

        var result = service.Validate(json, enabled: true);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("limited to 64 KB", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Format_PrettyPrintsValidJson()
    {
        var service = new CustomPatchService();

        var formatted = service.Format("{\"xpui\":{\"match\":\"one\",\"replace\":\"two\"}}");

        Assert.Contains(Environment.NewLine, formatted);
        Assert.Contains("\"xpui\"", formatted);
    }

    [Fact]
    public async Task ImportFromUrlAsync_RecordsSourceProvenance()
    {
        var fetchedAt = DateTimeOffset.Parse("2026-06-30T12:34:56Z");
        const string json = "{\"xpui\":{\"match\":\"one\",\"replace\":\"two\"}}";
        var bytes = Encoding.UTF8.GetBytes(json);
        var service = new CustomPatchService(
            new FakeImportTransport((uri, _) =>
            {
                Assert.Equal("https://example.test/patches.json", uri.ToString());
                return Task.FromResult(Response(json));
            }),
            () => fetchedAt);

        var imported = await service.ImportFromUrlAsync(" https://example.test/patches.json ");

        Assert.Equal(json, imported.Json);
        Assert.Equal("https://example.test/patches.json", imported.SourceUrl);
        Assert.Equal(fetchedAt, imported.FetchedAtUtc);
        Assert.Equal(bytes.Length, imported.ByteCount);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), imported.Sha256);
    }

    [Fact]
    public async Task ImportFromUrlAsync_BlocksNonHttpsUrls()
    {
        var service = new CustomPatchService(new FakeImportTransport((_, _) => throw new InvalidOperationException("should not fetch")));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportFromUrlAsync("http://example.test/patches.json"));

        Assert.Contains("HTTPS", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportFromUrlAsync_BlocksNonSuccessStatus()
    {
        var service = new CustomPatchService(new FakeImportTransport((_, _) =>
            Task.FromResult(Response("{}", HttpStatusCode.BadGateway))));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportFromUrlAsync("https://example.test/patches.json"));

        Assert.Contains("HTTP 502", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportFromUrlAsync_BlocksOversizeContentLength()
    {
        var service = new CustomPatchService(new FakeImportTransport((_, _) =>
            Task.FromResult(Response("{}", contentLength: CustomPatchService.MaxPatchJsonBytes + 1))));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportFromUrlAsync("https://example.test/patches.json"));

        Assert.Contains("larger than 64 KB", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportFromUrlAsync_BlocksOversizeStreamingResponse()
    {
        var payload = "{\"xpui\":\"" + new string('a', CustomPatchService.MaxPatchJsonBytes) + "\"}";
        var service = new CustomPatchService(new FakeImportTransport((_, _) =>
            Task.FromResult(Response(payload, omitContentLength: true))));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportFromUrlAsync("https://example.test/patches.json"));

        Assert.Contains("larger than 64 KB", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportFromUrlAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new CustomPatchService(new FakeImportTransport((_, token) =>
            Task.FromCanceled<CustomPatchImportResponse>(token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ImportFromUrlAsync("https://example.test/patches.json", cts.Token));
    }

    [Fact]
    public async Task ImportFromUrlAsync_BlocksInvalidJson()
    {
        var service = new CustomPatchService(new FakeImportTransport((_, _) =>
            Task.FromResult(Response("{ not json"))));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportFromUrlAsync("https://example.test/patches.json"));

        Assert.Contains("not valid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CustomPatchImportResponse Response(
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        long? contentLength = null,
        bool omitContentLength = false)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new CustomPatchImportResponse(
            statusCode,
            omitContentLength ? null : contentLength ?? bytes.Length,
            new MemoryStream(bytes));
    }

    private sealed class FakeImportTransport(Func<Uri, CancellationToken, Task<CustomPatchImportResponse>> handler) : ICustomPatchImportTransport
    {
        public Task<CustomPatchImportResponse> GetAsync(Uri uri, CancellationToken cancellationToken) =>
            handler(uri, cancellationToken);
    }
}
