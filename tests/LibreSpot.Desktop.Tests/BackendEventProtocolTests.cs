using System.IO;
using System.Reflection;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class BackendEventProtocolTests
{
    private static readonly MethodInfo PublishMethod =
        typeof(BackendScriptService).GetMethod("Publish", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find BackendScriptService.Publish method.");

    private static readonly string FixtureDirectory =
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "BackendProtocol");

    private static List<BackendMessage> ParseLines(IEnumerable<string> lines)
    {
        var messages = new List<BackendMessage>();
        foreach (var line in lines)
        {
            PublishMethod.Invoke(null, new object[] { line, (Action<BackendMessage>)(m => messages.Add(m)) });
        }
        return messages;
    }

    private static List<BackendMessage> ReplayFixture(string fixtureName)
    {
        var path = Path.Combine(FixtureDirectory, fixtureName);
        Assert.True(File.Exists(path), $"Fixture file not found: {path}");
        var lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l));
        return ParseLines(lines);
    }

    [Fact]
    public void Publish_PlainTextLineBecomesLogInfo()
    {
        var messages = ParseLines(new[] { "Some plain text output" });

        Assert.Single(messages);
        Assert.Equal("log", messages[0].Kind);
        Assert.Equal("INFO", messages[0].Level);
        Assert.Equal("Some plain text output", messages[0].Payload);
    }

    [Fact]
    public void Publish_StructuredMessageParsesCorrectly()
    {
        var messages = ParseLines(new[] { "@@LS@@|status|INFO|Preparing install" });

        Assert.Single(messages);
        Assert.Equal("status", messages[0].Kind);
        Assert.Equal("INFO", messages[0].Level);
        Assert.Equal("Preparing install", messages[0].Payload);
    }

    [Fact]
    public void Publish_InsufficientPartsBecomesLogInfo()
    {
        var messages = ParseLines(new[] { "@@LS@@|only-two-parts" });

        Assert.Single(messages);
        Assert.Equal("log", messages[0].Kind);
        Assert.Equal("INFO", messages[0].Level);
        Assert.Contains("@@LS@@", messages[0].Payload);
    }

    [Fact]
    public void Publish_PayloadWithPipesPreservesFourthSegment()
    {
        var messages = ParseLines(new[] { "@@LS@@|status|INFO|Payload with pipe|characters|inside" });

        Assert.Single(messages);
        Assert.Equal("status", messages[0].Kind);
        Assert.Equal("INFO", messages[0].Level);
        Assert.Equal("Payload with pipe|characters|inside", messages[0].Payload);
    }

    [Fact]
    public void Publish_EmptyPayloadIsValid()
    {
        var messages = ParseLines(new[] { "@@LS@@|log|INFO|" });

        Assert.Single(messages);
        Assert.Equal("log", messages[0].Kind);
        Assert.Equal("INFO", messages[0].Level);
        Assert.Equal("", messages[0].Payload);
    }

    [Fact]
    public void Publish_UnknownKindIsForwarded()
    {
        var messages = ParseLines(new[] { "@@LS@@|futureKind|INFO|Unknown kind from a newer backend" });

        Assert.Single(messages);
        Assert.Equal("futureKind", messages[0].Kind);
        Assert.Equal("INFO", messages[0].Level);
        Assert.Equal("Unknown kind from a newer backend", messages[0].Payload);
    }

    [Fact]
    public void Publish_ProgressNumericPayload()
    {
        var messages = ParseLines(new[] { "@@LS@@|progress|INFO|50.5" });

        Assert.Single(messages);
        Assert.Equal("progress", messages[0].Kind);
        Assert.Equal("50.5", messages[0].Payload);
    }

    [Fact]
    public void Publish_ResultSuccessLevel()
    {
        var messages = ParseLines(new[] { "@@LS@@|result|SUCCESS|Installation completed" });

        Assert.Single(messages);
        Assert.Equal("result", messages[0].Kind);
        Assert.Equal("SUCCESS", messages[0].Level);
        Assert.Equal("Installation completed", messages[0].Payload);
    }

    [Fact]
    public void Fixture_SuccessfulInstall_ProducesExpectedSequence()
    {
        var messages = ReplayFixture("successful-install.txt");

        Assert.True(messages.Count > 0, "Fixture must produce at least one message.");

        var statusMessages = messages.Where(m => m.Kind == "status").ToArray();
        Assert.Contains(statusMessages, m => m.Payload == "Preparing install");

        var progressMessages = messages.Where(m => m.Kind == "progress").ToArray();
        Assert.True(progressMessages.Length >= 5, "Expected multiple progress updates.");
        Assert.Equal("100", progressMessages.Last().Payload);

        var result = messages.Last();
        Assert.Equal("result", result.Kind);
        Assert.Equal("SUCCESS", result.Level);
    }

    [Fact]
    public void Fixture_CanceledRun_NoResultMessage()
    {
        var messages = ReplayFixture("canceled-run.txt");

        Assert.True(messages.Count > 0);
        Assert.DoesNotContain(messages, m => m.Kind == "result");
    }

    [Fact]
    public void Fixture_FailedDownload_EndsWithErrorResult()
    {
        var messages = ReplayFixture("failed-download.txt");

        var result = messages.Last();
        Assert.Equal("result", result.Kind);
        Assert.Equal("ERROR", result.Level);
        Assert.Contains("download", result.Payload, StringComparison.OrdinalIgnoreCase);

        var warnings = messages.Where(m => m.Level == "WARN").ToArray();
        Assert.True(warnings.Length >= 1, "Expected at least one warning before failure.");
    }

    [Fact]
    public void Fixture_WatcherDeferred_ReportsDeferredOutcome()
    {
        var messages = ReplayFixture("watcher-deferred.txt");

        var result = messages.Last();
        Assert.Equal("result", result.Kind);
        Assert.Contains("Deferred", result.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fixture_WatcherReapplied_CompletesSuccessfully()
    {
        var messages = ReplayFixture("watcher-reapplied.txt");

        var result = messages.Last();
        Assert.Equal("result", result.Kind);
        Assert.Equal("SUCCESS", result.Level);
        Assert.Contains("Reapplied", result.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fixture_BackendException_EndsWithErrorResult()
    {
        var messages = ReplayFixture("backend-exception.txt");

        var result = messages.Last();
        Assert.Equal("result", result.Kind);
        Assert.Equal("ERROR", result.Level);

        var errors = messages.Where(m => m.Level == "ERROR").ToArray();
        Assert.True(errors.Length >= 2, "Expected the exception log and the final result.");
    }

    [Fact]
    public void Fixture_MalformedAndEdgeCases_HandledDeterministically()
    {
        var messages = ReplayFixture("malformed-and-edge-cases.txt");

        Assert.True(messages.Count >= 7, $"Expected at least 7 parsed messages, got {messages.Count}.");

        Assert.Equal("log", messages[0].Kind);
        Assert.Equal("Plain text line without prefix", messages[0].Payload);

        Assert.Equal("status", messages[1].Kind);
        Assert.Equal("Normal structured message", messages[1].Payload);

        Assert.Equal("log", messages[2].Kind);

        var fractional = messages.First(m => m.Payload == "50.5");
        Assert.Equal("progress", fractional.Kind);

        var negative = messages.First(m => m.Payload == "-10");
        Assert.Equal("progress", negative.Kind);

        var overflow = messages.First(m => m.Payload == "200");
        Assert.Equal("progress", overflow.Kind);

        var nan = messages.First(m => m.Payload == "not-a-number");
        Assert.Equal("progress", nan.Kind);

        var unknown = messages.First(m => m.Kind == "futureKind");
        Assert.Equal("Unknown kind from a newer backend", unknown.Payload);
    }

    [Fact]
    public void Fixture_AllFixtureFilesExist()
    {
        var expectedFixtures = new[]
        {
            "successful-install.txt",
            "canceled-run.txt",
            "failed-download.txt",
            "watcher-deferred.txt",
            "watcher-reapplied.txt",
            "backend-exception.txt",
            "malformed-and-edge-cases.txt"
        };

        foreach (var fixture in expectedFixtures)
        {
            Assert.True(
                File.Exists(Path.Combine(FixtureDirectory, fixture)),
                $"Missing protocol fixture: {fixture}");
        }
    }

    [Fact]
    public void Fixture_HighVolume_ParsesWithoutError()
    {
        var lines = Enumerable.Range(0, 1000)
            .Select(i => $"@@LS@@|progress|INFO|{i * 100.0 / 999:F1}");

        var messages = ParseLines(lines);

        Assert.Equal(1000, messages.Count);
        Assert.All(messages, m => Assert.Equal("progress", m.Kind));
    }
}
