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
            "{\"operationId\":\"op-new\",\"action\":\"EnableAutoReapply\",\"phase\":\"task\",\"target\":\"LibreSpot\\\\ReapplyWatcher\",\"result\":\"Registered\",\"wouldChange\":true,\"reversible\":true,\"rollbackHint\":\"Unregister the scheduled task to undo.\"}",
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

        public void Dispose()
        {
            if (Directory.Exists(ConfigDirectory))
            {
                Directory.Delete(ConfigDirectory, recursive: true);
            }
        }
    }
}
