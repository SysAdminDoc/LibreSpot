using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace LibreSpot.Desktop.Services;

public sealed record OperationJournalUndoItem(
    string OperationId,
    string Action,
    string Phase,
    string Target,
    string Result,
    string RollbackHint,
    string TokenKind = "",
    string PreviousStateRef = "",
    string NewState = "",
    string UndoAction = "",
    string Risk = "");

public sealed class OperationJournalUndoService
{
    private const int MaxJournalLines = 500;
    private const int MaxJournalReadBytes = 1024 * 1024;
    private const int MaxReceiptBytes = 1024 * 1024;
    private const string ReceiptFileName = "run-receipt.latest.json";
    private const string TokenTypesResourceName = "LibreSpot.Desktop.Schemas.operation-token-types.json";
    private const string RunReceiptResourceName = "LibreSpot.Desktop.Schemas.run-receipt-format.json";

    private readonly IReadOnlyDictionary<string, OperationTokenMetadata> _tokenTypes;
    private readonly HashSet<string> _receiptStatuses;

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

    public OperationJournalUndoService()
        : this(LoadTokenTypes(), LoadReceiptStatuses())
    {
    }

    internal OperationJournalUndoService(
        IReadOnlyDictionary<string, OperationTokenMetadata> tokenTypes,
        HashSet<string> receiptStatuses)
    {
        _tokenTypes = tokenTypes;
        _receiptStatuses = receiptStatuses;
    }

    public IReadOnlyList<OperationJournalUndoItem> ReadLatestUndoItems(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return Array.Empty<OperationJournalUndoItem>();
        }

        var receiptPath = Path.Combine(configDirectory, ReceiptFileName);
        if (File.Exists(receiptPath))
        {
            var receiptItems = TryReadReceiptUndoItems(receiptPath);
            if (receiptItems is not null)
            {
                return receiptItems;
            }
        }

        var journalPath = Path.Combine(configDirectory, "operation-journal.jsonl");
        if (!File.Exists(journalPath))
        {
            return Array.Empty<OperationJournalUndoItem>();
        }

        OperationJournalEntry[] entries;
        try
        {
            entries = ReadEntries(journalPath).ToArray();
        }
        catch (IOException)
        {
            return Array.Empty<OperationJournalUndoItem>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<OperationJournalUndoItem>();
        }
        catch (DecoderFallbackException)
        {
            return Array.Empty<OperationJournalUndoItem>();
        }
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
            .Select(CreateUndoItem)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    private IReadOnlyList<OperationJournalUndoItem>? TryReadReceiptUndoItems(string receiptPath)
    {
        try
        {
            using var stream = File.Open(receiptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length > MaxReceiptBytes)
            {
                return null;
            }

            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            var status = GetString(root, "status");
            if (!_receiptStatuses.Contains(status)
                || !status.Equals("success", StringComparison.OrdinalIgnoreCase)
                    && !status.Equals("partialSuccess", StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<OperationJournalUndoItem>();
            }

            if (!root.TryGetProperty("operations", out var operations)
                || operations.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<OperationJournalUndoItem>();
            }

            var operationId = GetString(root, "operationId");
            var action = GetString(root, "action");
            return operations.EnumerateArray()
                .Select(operation => ParseReceiptOperation(operationId, action, operation))
                .Where(entry => entry is not null)
                .Select(entry => CreateUndoItem(entry!))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToArray();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private OperationJournalUndoItem? CreateUndoItem(OperationJournalEntry entry)
    {
        if (!IsUndoCandidate(entry))
        {
            return null;
        }

        var tokenKind = FirstNonEmpty(entry.TokenKind, InferTokenKind(entry.Phase, entry.Result, entry.Target));
        if (string.IsNullOrWhiteSpace(tokenKind) || !_tokenTypes.TryGetValue(tokenKind, out var token))
        {
            return null;
        }

        var undoAction = FirstNonEmpty(entry.UndoAction, entry.RollbackHint, token.UndoAction);
        var risk = FirstNonEmpty(entry.Risk, token.Risk);
        if (string.IsNullOrWhiteSpace(undoAction))
        {
            return null;
        }

        if (!token.Reversible || !entry.Reversible)
        {
            return null;
        }

        if (token.CapturePreviousState && string.IsNullOrWhiteSpace(entry.PreviousStateRef))
        {
            return null;
        }

        return new OperationJournalUndoItem(
            entry.OperationId,
            entry.Action,
            entry.Phase,
            entry.Target,
            entry.Result,
            entry.RollbackHint,
            token.Kind,
            entry.PreviousStateRef,
            entry.NewState,
            undoAction,
            risk);
    }

    private static bool IsUndoCandidate(OperationJournalEntry entry) =>
        entry.WouldChange
        && !IgnoredResults.Contains(entry.Result);

    private static IEnumerable<OperationJournalEntry> ReadEntries(string journalPath)
    {
        foreach (var line in ReadTailLines(journalPath, MaxJournalReadBytes).TakeLast(MaxJournalLines))
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

    private static IEnumerable<string> ReadTailLines(string path, int maxBytes)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var start = Math.Max(0, stream.Length - maxBytes);
        stream.Seek(start, SeekOrigin.Begin);

        var buffer = new byte[(int)Math.Min(maxBytes, stream.Length - start)];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        var decodeOffset = 0;
        if (start > 0)
        {
            var firstNewline = Array.IndexOf(buffer, (byte)'\n', 0, total);
            if (firstNewline < 0)
            {
                yield break;
            }

            decodeOffset = firstNewline + 1;
        }

        var text = new UTF8Encoding(false, true).GetString(buffer, decodeOffset, total - decodeOffset);
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            yield return line;
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
            GetString(root, "rollbackHint"),
            GetString(root, "tokenKind"),
            GetString(root, "previousStateRef"),
            GetString(root, "newState"),
            GetString(root, "undoAction"),
            GetString(root, "risk"));
    }

    private static OperationJournalEntry? ParseReceiptOperation(
        string operationId,
        string action,
        JsonElement operation)
    {
        var tokenKind = GetString(operation, "tokenKind");
        if (string.IsNullOrWhiteSpace(tokenKind))
        {
            return null;
        }

        // Run-receipt operation entries have no "phase" concept (see
        // run-receipt-format.json operationEntryFields), so Phase/RollbackHint
        // stay empty rather than being mislabelled with the token kind -- the
        // token kind flows through the dedicated TokenKind slot instead.
        return new OperationJournalEntry(
            operationId,
            action,
            Phase: string.Empty,
            GetString(operation, "target"),
            GetString(operation, "result"),
            WouldChange: true,
            GetBoolean(operation, "reversible"),
            RollbackHint: string.Empty,
            tokenKind,
            GetString(operation, "previousStateRef"),
            GetString(operation, "newState"),
            GetString(operation, "undoAction"),
            GetString(operation, "risk"));
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static bool GetBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string InferTokenKind(string phase, string result, string target)
    {
        if (phase.Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            return "configWrite";
        }

        if (phase.Equals("path", StringComparison.OrdinalIgnoreCase))
        {
            return result.Equals("Removed", StringComparison.OrdinalIgnoreCase)
                ? "pathEntryRemove"
                : "pathEntryAdd";
        }

        if (phase.Equals("task", StringComparison.OrdinalIgnoreCase))
        {
            return result.Equals("Removed", StringComparison.OrdinalIgnoreCase)
                ? "watcherTaskRemove"
                : "watcherTaskRegister";
        }

        if (phase.Equals("cache", StringComparison.OrdinalIgnoreCase))
        {
            return "cacheCleared";
        }

        if (phase.Equals("appx", StringComparison.OrdinalIgnoreCase))
        {
            return "spotifyUninstall";
        }

        if (phase.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            return target.Contains("Spicetify", StringComparison.OrdinalIgnoreCase)
                ? "spicetifyUninstall"
                : "selfDataRemoved";
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, OperationTokenMetadata> LoadTokenTypes()
    {
        using var document = LoadEmbeddedJson(TokenTypesResourceName);
        return document.RootElement.GetProperty("tokenTypes").EnumerateArray()
            .Select(token => new OperationTokenMetadata(
                GetString(token, "kind"),
                GetBoolean(token, "reversible"),
                GetBoolean(token, "capturePreviousState"),
                GetString(token, "undoAction"),
                GetString(token, "risk")))
            .Where(token => !string.IsNullOrWhiteSpace(token.Kind))
            .ToDictionary(token => token.Kind, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> LoadReceiptStatuses()
    {
        using var document = LoadEmbeddedJson(RunReceiptResourceName);
        return document.RootElement.GetProperty("statusValues").EnumerateArray()
            .Select(status => GetString(status, "value"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonDocument LoadEmbeddedJson(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded schema resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return JsonDocument.Parse(reader.ReadToEnd());
    }

    private sealed record OperationJournalEntry(
        string OperationId,
        string Action,
        string Phase,
        string Target,
        string Result,
        bool WouldChange,
        bool Reversible,
        string RollbackHint,
        string TokenKind,
        string PreviousStateRef,
        string NewState,
        string UndoAction,
        string Risk);

    internal sealed record OperationTokenMetadata(
        string Kind,
        bool Reversible,
        bool CapturePreviousState,
        string UndoAction,
        string Risk);
}
