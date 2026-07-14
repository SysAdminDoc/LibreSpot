using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class OperationJournalUndoServiceTests
{
    [Fact]
    public void ReadLatestUndoItems_ReturnsCompletedReversibleEntriesFromLatestSuccessfulRun()
    {
        using var fixture = new OperationJournalFixture();
        fixture.WriteJournal(
            "{\"operationId\":\"op-old\",\"action\":\"Install\",\"phase\":\"path\",\"target\":\"User PATH\",\"result\":\"Updated\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Restore old path.\"}",
            "{\"operationId\":\"op-old\",\"action\":\"Install\",\"phase\":\"complete\",\"target\":\"Backend action: Install\",\"result\":\"Succeeded\",\"wouldChange\":false,\"reversible\":false}",
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"task\",\"target\":\"LibreSpot\\\\ReapplyWatcher\",\"result\":\"Planned\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Unregister the scheduled task to undo.\"}",
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"task\",\"target\":\"LibreSpot\\\\ReapplyWatcher\",\"result\":\"Registered\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Unregister the scheduled task to undo.\",\"tokenKind\":\"watcherTaskRegister\",\"previousStateRef\":\"target:LibreSpot\\\\ReapplyWatcher\",\"undoAction\":\"Unregister the scheduled task to undo.\",\"risk\":\"low\"}",
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"config\",\"target\":\"config.json\",\"result\":\"Failed\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Restore the config backup.\"}",
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"cache\",\"target\":\"cache\",\"result\":\"Cleared\",\"wouldChange\":true,\"reversible\":false,\"rollbackHint\":\"Cache rebuilds automatically.\"}",
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"complete\",\"target\":\"Backend action: EnableAutoReapply\",\"result\":\"Succeeded\",\"wouldChange\":false,\"reversible\":false}");

        var items = new OperationJournalUndoService().ReadLatestUndoItems(fixture.ConfigDirectory);

        var item = Assert.Single(items);
        Assert.Equal("op-new", item.OperationId);
        Assert.Equal("EnableAutoReapply", item.Action);
        Assert.Equal("task", item.Phase);
        Assert.Equal("LibreSpot\\ReapplyWatcher", item.Target);
        Assert.Equal("Registered", item.Result);
        Assert.Equal("Unregister the scheduled task to undo.", item.RollbackHint);
        Assert.Equal("watcherTaskRegister", item.TokenKind);
        Assert.Equal("target:LibreSpot\\ReapplyWatcher", item.PreviousStateRef);
        Assert.Equal("Unregister the scheduled task to undo.", item.UndoAction);
        Assert.Equal("low", item.Risk);
    }

    [Fact]
    public void ReadLatestUndoItems_DoesNotFallBackToOlderSuccessfulRun()
    {
        using var fixture = new OperationJournalFixture();
        fixture.WriteJournal(
            "{\"operationId\":\"op-old\",\"action\":\"Install\",\"phase\":\"path\",\"target\":\"User PATH\",\"result\":\"Updated\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Restore old path.\"}",
            "{\"operationId\":\"op-old\",\"action\":\"Install\",\"phase\":\"complete\",\"target\":\"Backend action: Install\",\"result\":\"Succeeded\",\"wouldChange\":false,\"reversible\":false}",
            "{\"operationId\":\"op-new\",\"action\":\"ClearCache\",\"phase\":\"cache\",\"target\":\"cache\",\"result\":\"Cleared\",\"wouldChange\":true,\"reversible\":false,\"rollbackHint\":\"Cache rebuilds automatically.\"}",
            "{\"operationId\":\"op-new\",\"action\":\"ClearCache\",\"phase\":\"complete\",\"target\":\"Backend action: ClearCache\",\"result\":\"Succeeded\",\"wouldChange\":false,\"reversible\":false}");

        var items = new OperationJournalUndoService().ReadLatestUndoItems(fixture.ConfigDirectory);

        Assert.Empty(items);
    }

    [Fact]
    public void ReadLatestUndoItems_IgnoresMissingMalformedAndIncompleteJournalEntries()
    {
        using var fixture = new OperationJournalFixture();

        Assert.Empty(new OperationJournalUndoService().ReadLatestUndoItems(fixture.ConfigDirectory));

        fixture.WriteJournal(
            "not json",
            "{\"operationId\":\"op-new\",\"action\":\"Install\",\"phase\":\"path\",\"target\":\"User PATH\",\"result\":\"Updated\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Restore old path.\"}");

        Assert.Empty(new OperationJournalUndoService().ReadLatestUndoItems(fixture.ConfigDirectory));
    }

    [Fact]
    public void ReadLatestUndoItems_ReadsOnlyBoundedTailOfOversizedJournal()
    {
        using var fixture = new OperationJournalFixture();
        fixture.WriteJournal(
            new string('x', (1024 * 1024) + 128),
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"task\",\"target\":\"LibreSpot\\\\ReapplyWatcher\",\"result\":\"Registered\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Unregister task.\",\"tokenKind\":\"watcherTaskRegister\",\"previousStateRef\":\"target:LibreSpot\\\\ReapplyWatcher\"}",
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"complete\",\"target\":\"Backend action: EnableAutoReapply\",\"result\":\"Succeeded\",\"wouldChange\":false,\"reversible\":false}");

        var item = Assert.Single(new OperationJournalUndoService().ReadLatestUndoItems(fixture.ConfigDirectory));

        Assert.Equal("op-new", item.OperationId);
        Assert.Equal("LibreSpot\\ReapplyWatcher", item.Target);
    }

    [Fact]
    public void ReadLatestUndoItems_FallsBackToJournalWhenReceiptIsOversized()
    {
        using var fixture = new OperationJournalFixture();
        fixture.WriteReceipt(new string('x', (1024 * 1024) + 1));
        fixture.WriteJournal(
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"task\",\"target\":\"LibreSpot\\\\ReapplyWatcher\",\"result\":\"Registered\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Unregister task.\",\"tokenKind\":\"watcherTaskRegister\",\"previousStateRef\":\"target:LibreSpot\\\\ReapplyWatcher\"}",
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"complete\",\"target\":\"Backend action: EnableAutoReapply\",\"result\":\"Succeeded\",\"wouldChange\":false,\"reversible\":false}");

        var item = Assert.Single(new OperationJournalUndoService().ReadLatestUndoItems(fixture.ConfigDirectory));

        Assert.Equal("op-new", item.OperationId);
    }

    [Fact]
    public void ReadLatestUndoItems_UsesRunReceiptAndSchemaTokenMetadata()
    {
        using var fixture = new OperationJournalFixture();
        fixture.WriteReceipt(
            """
            {
              "schemaVersion": 1,
              "receiptId": "4f5f51e3-8585-4a7b-b8c4-5e702f1da7ec",
              "runId": "08fe72fb-bafa-43f8-b39a-078a30cc84bc",
              "operationId": "08fe72fb-bafa-43f8-b39a-078a30cc84bc",
              "startedAt": "2026-06-29T12:00:00.0000000Z",
              "completedAt": "2026-06-29T12:01:00.0000000Z",
              "action": "EnableAutoReapply",
              "status": "success",
              "undoAvailable": true,
              "operations": [
                {
                  "tokenKind": "watcherTaskRegister",
                  "target": "LibreSpot\\ReapplyWatcher",
                  "previousStateRef": "",
                  "newState": "Registered",
                  "result": "applied",
                  "reversible": true,
                  "undoAction": "",
                  "risk": ""
                },
                {
                  "tokenKind": "configWrite",
                  "target": "config.json",
                  "previousStateRef": "",
                  "newState": "Saved",
                  "result": "applied",
                  "reversible": true,
                  "undoAction": "Restore config backup.",
                  "risk": "low"
                },
                {
                  "tokenKind": "unknownToken",
                  "target": "unknown",
                  "previousStateRef": "target:unknown",
                  "newState": "Updated",
                  "result": "applied",
                  "reversible": true,
                  "undoAction": "Undo unknown.",
                  "risk": "low"
                }
              ]
            }
            """);

        var items = new OperationJournalUndoService().ReadLatestUndoItems(fixture.ConfigDirectory);

        var item = Assert.Single(items);
        Assert.Equal("08fe72fb-bafa-43f8-b39a-078a30cc84bc", item.OperationId);
        Assert.Equal("EnableAutoReapply", item.Action);
        Assert.Equal("watcherTaskRegister", item.TokenKind);
        Assert.Equal("LibreSpot\\ReapplyWatcher", item.Target);
        Assert.Equal("Unregister the scheduled task by name.", item.UndoAction);
        Assert.Equal("low", item.Risk);
    }

    [Fact]
    public void ReadLatestUndoItems_RequiresPreviousStateWhenTokenSchemaRequiresCapture()
    {
        using var fixture = new OperationJournalFixture();
        fixture.WriteReceipt(
            """
            {
              "schemaVersion": 1,
              "receiptId": "4f5f51e3-8585-4a7b-b8c4-5e702f1da7ec",
              "runId": "08fe72fb-bafa-43f8-b39a-078a30cc84bc",
              "operationId": "08fe72fb-bafa-43f8-b39a-078a30cc84bc",
              "startedAt": "2026-06-29T12:00:00.0000000Z",
              "completedAt": "2026-06-29T12:01:00.0000000Z",
              "action": "SaveConfig",
              "status": "success",
              "undoAvailable": true,
              "operations": [
                {
                  "tokenKind": "configWrite",
                  "target": "config.json",
                  "previousStateRef": "target:config.json",
                  "newState": "Saved",
                  "result": "applied",
                  "reversible": true,
                  "undoAction": "Restore config backup.",
                  "risk": "low"
                }
              ]
            }
            """);

        var item = Assert.Single(new OperationJournalUndoService().ReadLatestUndoItems(fixture.ConfigDirectory));

        Assert.Equal("configWrite", item.TokenKind);
        Assert.Equal("target:config.json", item.PreviousStateRef);
        Assert.Equal("Restore config backup.", item.UndoAction);
    }

    [Fact]
    public void PreviewUndoItem_AllowsOnlyExactCurrentPathSnapshot()
    {
        using var fixture = new OperationJournalFixture();
        var item = fixture.CreatePathUndoItem("old;%TOOLS%", "old;%TOOLS%;C:\\LibreSpot\\bin");
        var environment = new FakeUndoEnvironment("old;%TOOLS%;C:\\LibreSpot\\bin");
        var service = new OperationJournalUndoService(environment);

        var preview = service.PreviewUndoItem(item, fixture.ConfigDirectory);

        Assert.True(preview.CanExecute);
        Assert.False(preview.IsAlreadyUndone);
        Assert.Contains("fingerprint matches", preview.Reason);

        environment.Value = "old;%TOOLS%;C:\\OtherTool";
        var stale = service.PreviewUndoItem(item, fixture.ConfigDirectory);
        Assert.False(stale.CanExecute);
        Assert.Contains("changed after this receipt", stale.Reason);
    }

    [Theory]
    [InlineData("unknownToken", "low", "Unknown undo token")]
    [InlineData("watcherTaskRegister", "low", "requires elevation")]
    [InlineData("spotxPatch", "medium", "classified as medium")]
    [InlineData("fullReset", "destructive", "not reversible")]
    public void PreviewUndoItem_RefusesUnknownElevatedNonLowRiskAndDestructiveTokens(
        string tokenKind,
        string risk,
        string expectedReason)
    {
        using var fixture = new OperationJournalFixture();
        var item = new OperationJournalUndoItem(
            "source-op",
            "Install",
            "test",
            "target",
            "applied",
            "manual recovery",
            tokenKind,
            "missing-state.json",
            "state",
            "undo",
            risk);

        var preview = new OperationJournalUndoService(new FakeUndoEnvironment(string.Empty))
            .PreviewUndoItem(item, fixture.ConfigDirectory);

        Assert.False(preview.CanExecute);
        Assert.Contains(expectedReason, preview.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewUndoItem_RefusesAllowlistedTokenWhenCapturedStateIsMissing()
    {
        using var fixture = new OperationJournalFixture();
        var item = new OperationJournalUndoItem(
            Guid.NewGuid().ToString(),
            "Install",
            "path",
            "User PATH",
            "Updated",
            "Restore PATH.",
            "pathEntryAdd",
            Path.Combine(fixture.ConfigDirectory, "undo-states", "missing.json"),
            "sha256:missing",
            "Restore PATH.",
            "low");

        var preview = new OperationJournalUndoService(new FakeUndoEnvironment(string.Empty))
            .PreviewUndoItem(item, fixture.ConfigDirectory);

        Assert.False(preview.CanExecute);
        Assert.Contains("missing", preview.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewUndoItem_RefusesExpiredCapturedState()
    {
        using var fixture = new OperationJournalFixture();
        var item = fixture.CreatePathUndoItem(
            "before",
            "before;C:\\LibreSpot\\bin",
            createdAtUtc: DateTimeOffset.UtcNow.AddDays(-31));

        var preview = new OperationJournalUndoService(new FakeUndoEnvironment("before;C:\\LibreSpot\\bin"))
            .PreviewUndoItem(item, fixture.ConfigDirectory);

        Assert.False(preview.CanExecute);
        Assert.Contains("expired", preview.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteUndoAsync_RecordsPolicyRefusalWithNewOperationId()
    {
        using var fixture = new OperationJournalFixture();
        var item = new OperationJournalUndoItem(
            Guid.NewGuid().ToString(),
            "Install",
            "patch",
            "Spotify",
            "applied",
            "Restore manually.",
            "spotxPatch",
            "missing.json",
            "patched",
            "Restore manually.",
            "medium");
        var service = new OperationJournalUndoService(new FakeUndoEnvironment(string.Empty));

        var result = await service.ExecuteUndoAsync(item, fixture.ConfigDirectory);

        Assert.False(result.Success);
        Assert.True(result.Refused);
        Assert.True(Guid.TryParse(result.OperationId, out _));
        Assert.Contains(result.OperationId, File.ReadAllText(Path.Combine(fixture.ConfigDirectory, "operation-journal.jsonl")));
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.ConfigDirectory, "run-receipt.latest.json")));
        Assert.Equal("failed", receipt.RootElement.GetProperty("status").GetString());
        Assert.Equal(item.OperationId, receipt.RootElement.GetProperty("undoSourceOperationId").GetString());
    }

    [Fact]
    public async Task ExecuteUndoAsync_RestoresPathIdempotentlyAndWritesNewEvidence()
    {
        using var fixture = new OperationJournalFixture();
        const string previous = "%USERPROFILE%\\bin;%JAVA_HOME%\\bin";
        const string expected = "%USERPROFILE%\\bin;%JAVA_HOME%\\bin;C:\\LibreSpot\\bin";
        var item = fixture.CreatePathUndoItem(previous, expected);
        var environment = new FakeUndoEnvironment(expected);
        var service = new OperationJournalUndoService(environment);

        var result = await service.ExecuteUndoAsync(item, fixture.ConfigDirectory);

        Assert.True(result.Success);
        Assert.True(result.Changed);
        Assert.Equal(previous, environment.Value);
        Assert.True(File.Exists(item.PreviousStateRef));
        Assert.Contains(result.OperationId, File.ReadAllText(Path.Combine(fixture.ConfigDirectory, "operation-journal.jsonl")));
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.ConfigDirectory, "run-receipt.latest.json")));
        Assert.Equal(result.OperationId, receipt.RootElement.GetProperty("operationId").GetString());
        Assert.Equal("success", receipt.RootElement.GetProperty("status").GetString());
        Assert.Equal(item.OperationId, receipt.RootElement.GetProperty("undoSourceOperationId").GetString());

        var persistedItem = Assert.Single(service.ReadLatestUndoItems(fixture.ConfigDirectory));
        Assert.Equal(item.OperationId, persistedItem.OperationId);
        Assert.Equal(item.PreviousStateRef, persistedItem.PreviousStateRef);
        var repeated = await service.ExecuteUndoAsync(persistedItem, fixture.ConfigDirectory);
        Assert.True(repeated.Success);
        Assert.False(repeated.Changed);
        Assert.Equal("alreadyUndone", repeated.Status);
        Assert.Equal(previous, environment.Value);
    }

    [Fact]
    public async Task ExecuteUndoAsync_RecoversExpectedPathAfterInjectedPartialFailure()
    {
        using var fixture = new OperationJournalFixture();
        const string previous = "before";
        const string expected = "before;C:\\LibreSpot\\bin";
        var item = fixture.CreatePathUndoItem(previous, expected);
        var environment = new FakeUndoEnvironment(expected) { CorruptNextWrite = true };
        var service = new OperationJournalUndoService(environment);

        var result = await service.ExecuteUndoAsync(item, fixture.ConfigDirectory);

        Assert.False(result.Success);
        Assert.Equal("failed", result.Status);
        Assert.Equal(expected, environment.Value);
        Assert.True(File.Exists(item.PreviousStateRef));
        Assert.Contains("restored the pre-undo PATH", result.Message);
        var retryItem = Assert.Single(service.ReadLatestUndoItems(fixture.ConfigDirectory));
        Assert.True(service.PreviewUndoItem(retryItem, fixture.ConfigDirectory).CanExecute);
    }

    [Fact]
    public async Task ExecuteUndoAsync_RestoresMissingPathValueInsteadOfCreatingEmptyValue()
    {
        using var fixture = new OperationJournalFixture();
        const string expected = "C:\\LibreSpot\\bin";
        var item = fixture.CreatePathUndoItem(string.Empty, expected, previousValueExists: false, previousValueKind: "String");
        var environment = new FakeUndoEnvironment(expected);
        var service = new OperationJournalUndoService(environment);

        var result = await service.ExecuteUndoAsync(item, fixture.ConfigDirectory);

        Assert.True(result.Success);
        Assert.True(result.Changed);
        Assert.False(environment.State.Exists);
        Assert.Equal(string.Empty, environment.State.Value);
    }

    private sealed class OperationJournalFixture : IDisposable
    {
        public OperationJournalFixture()
        {
            ConfigDirectory = Path.Combine(Path.GetTempPath(), "LibreSpotTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ConfigDirectory);
        }

        public string ConfigDirectory { get; }

        public void WriteJournal(params string[] lines) =>
            File.WriteAllLines(Path.Combine(ConfigDirectory, "operation-journal.jsonl"), lines);

        public void WriteReceipt(string content) =>
            File.WriteAllText(Path.Combine(ConfigDirectory, "run-receipt.latest.json"), content);

        public OperationJournalUndoItem CreatePathUndoItem(
            string previousValue,
            string expectedValue,
            bool previousValueExists = true,
            string previousValueKind = "ExpandString",
            DateTimeOffset? createdAtUtc = null)
        {
            const string operationId = "source-operation";
            var directory = Path.Combine(ConfigDirectory, "undo-states");
            Directory.CreateDirectory(directory);
            var statePath = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
            var previousHash = Hash(previousValue);
            var expectedHash = Hash(expectedValue);
            File.WriteAllText(
                statePath,
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 2,
                    operationId,
                    tokenKind = "pathEntryAdd",
                    scope = "User",
                    target = "User PATH",
                    entry = "C:\\LibreSpot\\bin",
                    previousValueExists,
                    previousValue,
                    previousValueKind,
                    expectedValueExists = true,
                    expectedValue,
                    expectedValueKind = "ExpandString",
                    previousSha256 = previousHash,
                    expectedSha256 = expectedHash,
                    createdAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow
                }));

            return new OperationJournalUndoItem(
                operationId,
                "Install",
                "path",
                "User PATH",
                "Updated",
                "Restore the exact previous PATH value.",
                "pathEntryAdd",
                statePath,
                $"sha256:{expectedHash}",
                "Restore the exact previous user PATH snapshot.",
                "low");
        }

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        public void Dispose()
        {
            if (Directory.Exists(ConfigDirectory))
            {
                Directory.Delete(ConfigDirectory, recursive: true);
            }
        }
    }

    private sealed class FakeUndoEnvironment(string value) : IOperationUndoEnvironment
    {
        private UserPathRegistryState _state = new(true, value, Microsoft.Win32.RegistryValueKind.ExpandString);

        public string Value
        {
            get => _state.Value;
            set => _state = new UserPathRegistryState(true, value, Microsoft.Win32.RegistryValueKind.ExpandString);
        }

        public UserPathRegistryState State => _state;

        public bool CorruptNextWrite { get; set; }

        public UserPathRegistryState GetUserPath() => _state;

        public void SetUserPath(UserPathRegistryState state)
        {
            if (CorruptNextWrite)
            {
                CorruptNextWrite = false;
                _state = new UserPathRegistryState(true, "injected-corruption", Microsoft.Win32.RegistryValueKind.ExpandString);
                return;
            }

            _state = state;
        }
    }
}
