using System.IO;
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

        public void Dispose()
        {
            if (Directory.Exists(ConfigDirectory))
            {
                Directory.Delete(ConfigDirectory, recursive: true);
            }
        }
    }
}
