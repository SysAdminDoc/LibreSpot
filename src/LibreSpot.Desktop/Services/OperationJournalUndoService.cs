using System.IO;
using System.Text.Json;

namespace LibreSpot.Desktop.Services;

public sealed record OperationJournalUndoItem(
    string OperationId,
    string Action,
    string Phase,
    string Target,
    string Result,
    string RollbackHint);

public sealed class OperationJournalUndoService
{
    private const int MaxJournalLines = 500;

    private static readonly HashSet<string> IgnoredResults = new(StringComparer.OrdinalIgnoreCase)
    {
        "Planned",
        "Started",
        "Skipped",
        "Failed",
        "Refused",
        "Canceled",
        "Cancelled"
    };

    public IReadOnlyList<OperationJournalUndoItem> ReadLatestUndoItems(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return Array.Empty<OperationJournalUndoItem>();
        }

        var journalPath = Path.Combine(configDirectory, "operation-journal.jsonl");
        if (!File.Exists(journalPath))
        {
            return Array.Empty<OperationJournalUndoItem>();
        }

        var entries = ReadEntries(journalPath).ToArray();
        if (entries.Length == 0)
        {
            return Array.Empty<OperationJournalUndoItem>();
        }

        var latestSuccessfulOperationId = entries
            .Where(entry => string.Equals(entry.Phase, "complete", StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Result, "Succeeded", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.OperationId))
            .Select(entry => entry.OperationId)
            .Reverse()
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(latestSuccessfulOperationId))
        {
            return Array.Empty<OperationJournalUndoItem>();
        }

        return entries
            .Where(entry => string.Equals(entry.OperationId, latestSuccessfulOperationId, StringComparison.OrdinalIgnoreCase))
            .Where(IsUndoCandidate)
            .GroupBy(
                entry => $"{entry.Phase}\u001f{entry.Target}\u001f{entry.Result}\u001f{entry.RollbackHint}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .Select(entry => new OperationJournalUndoItem(
                entry.OperationId,
                entry.Action,
                entry.Phase,
                entry.Target,
                entry.Result,
                entry.RollbackHint))
            .ToArray();
    }

    private static bool IsUndoCandidate(OperationJournalEntry entry) =>
        entry.WouldChange
        && entry.Reversible
        && !string.IsNullOrWhiteSpace(entry.RollbackHint)
        && !IgnoredResults.Contains(entry.Result);

    private static IEnumerable<OperationJournalEntry> ReadEntries(string journalPath)
    {
        foreach (var line in File.ReadLines(journalPath).TakeLast(MaxJournalLines))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            OperationJournalEntry? entry;
            try
            {
                entry = ParseEntry(line);
            }
            catch (JsonException)
            {
                continue;
            }

            if (entry is not null)
            {
                yield return entry;
            }
        }
    }

    private static OperationJournalEntry? ParseEntry(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        var operationId = GetString(root, "operationId");
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        return new OperationJournalEntry(
            operationId,
            GetString(root, "action"),
            GetString(root, "phase"),
            GetString(root, "target"),
            GetString(root, "result"),
            GetBoolean(root, "wouldChange"),
            GetBoolean(root, "reversible"),
            GetString(root, "rollbackHint"));
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static bool GetBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private sealed record OperationJournalEntry(
        string OperationId,
        string Action,
        string Phase,
        string Target,
        string Result,
        bool WouldChange,
        bool Reversible,
        string RollbackHint);
}
