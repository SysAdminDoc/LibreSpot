using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

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
    string Risk = "",
    bool PolicyExecutable = false,
    bool RequiresAdmin = false,
    string PolicyRefusalReason = "");

public sealed record OperationUndoPreview(
    bool CanExecute,
    bool IsAlreadyUndone,
    string Reason,
    OperationJournalUndoItem Item);

public sealed record OperationUndoExecutionResult(
    bool Success,
    bool Changed,
    bool Refused,
    string OperationId,
    string Status,
    string Message);

internal interface IOperationUndoEnvironment
{
    UserPathRegistryState GetUserPath();
    void SetUserPath(UserPathRegistryState state);
}

internal sealed record UserPathRegistryState(bool Exists, string Value, RegistryValueKind Kind);

public sealed class OperationJournalUndoService
{
    private const int MaxJournalLines = 500;
    private const int MaxJournalReadBytes = 1024 * 1024;
    private const int MaxReceiptBytes = 1024 * 1024;
    private const int MaxUndoStateBytes = 1024 * 1024;
    private const string ReceiptFileName = "run-receipt.latest.json";
    private const string TokenTypesResourceName = "LibreSpot.Desktop.Schemas.operation-token-types.json";
    private const string RunReceiptResourceName = "LibreSpot.Desktop.Schemas.run-receipt-format.json";

    private readonly IReadOnlyDictionary<string, OperationTokenMetadata> _tokenTypes;
    private readonly HashSet<string> _receiptStatuses;
    private readonly IOperationUndoEnvironment _environment;

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
        : this(LoadTokenTypes(), LoadReceiptStatuses(), new WindowsOperationUndoEnvironment())
    {
    }

    internal OperationJournalUndoService(IOperationUndoEnvironment environment)
        : this(LoadTokenTypes(), LoadReceiptStatuses(), environment)
    {
    }

    internal OperationJournalUndoService(
        IReadOnlyDictionary<string, OperationTokenMetadata> tokenTypes,
        HashSet<string> receiptStatuses,
        IOperationUndoEnvironment? environment = null)
    {
        _tokenTypes = tokenTypes;
        _receiptStatuses = receiptStatuses;
        _environment = environment ?? new WindowsOperationUndoEnvironment();
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
            var action = GetString(root, "action");
            var undoSourceOperationId = GetString(root, "undoSourceOperationId");
            var isUndoReceipt = string.Equals(action, "Undo", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(undoSourceOperationId);
            if (!_receiptStatuses.Contains(status)
                || !isUndoReceipt
                    && !status.Equals("success", StringComparison.OrdinalIgnoreCase)
                    && !status.Equals("partialSuccess", StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<OperationJournalUndoItem>();
            }

            if (!root.TryGetProperty("operations", out var operations)
                || operations.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<OperationJournalUndoItem>();
            }

            var operationId = isUndoReceipt ? undoSourceOperationId : GetString(root, "operationId");
            var sourceAction = isUndoReceipt ? GetString(root, "undoSourceAction") : action;
            return operations.EnumerateArray()
                .Select(operation => isUndoReceipt
                    ? ParseUndoSourceOperation(operationId, sourceAction, operation)
                    : ParseReceiptOperation(operationId, action, operation))
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
            risk,
            token.ExecutableUndo && token.Reversible && string.Equals(token.Risk, "low", StringComparison.OrdinalIgnoreCase) && !token.RequiresAdmin,
            token.RequiresAdmin,
            UndoPolicyRefusal(token, token.Risk));
    }

    public OperationUndoPreview PreviewUndoItem(OperationJournalUndoItem item, string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return RefusedPreview(item, "LibreSpot could not resolve its configuration directory.");
        }

        if (!_tokenTypes.TryGetValue(item.TokenKind, out var token))
        {
            return RefusedPreview(item, $"Unknown undo token '{item.TokenKind}'. Only policy-listed low-risk tokens can run.");
        }

        var policyReason = UndoPolicyRefusal(token, token.Risk);
        if (!string.IsNullOrWhiteSpace(policyReason))
        {
            return RefusedPreview(item, policyReason);
        }

        if (!string.Equals(token.ValidationPolicy, "exact-user-path-snapshot", StringComparison.Ordinal))
        {
            return RefusedPreview(item, $"Undo token '{item.TokenKind}' has no supported state-validation policy.");
        }

        if (!TryReadPathUndoState(item, configDirectory, out var state, out var error))
        {
            return RefusedPreview(item, error);
        }

        if (token.ExpiresAfterHours is not { } expiresAfterHours || expiresAfterHours <= 0)
        {
            return RefusedPreview(item, $"Undo token '{item.TokenKind}' has no bounded snapshot-retention policy.");
        }

        var now = DateTimeOffset.UtcNow;
        if (state!.CreatedAtUtc > now.AddMinutes(5) || state.CreatedAtUtc < now.AddHours(-expiresAfterHours))
        {
            return RefusedPreview(item, "The captured undo state is expired or has an invalid timestamp. LibreSpot will not restore stale PATH data.");
        }

        var current = _environment.GetUserPath();
        if (MatchesState(current, state!.PreviousValueExists, state.PreviousValueKind, state.PreviousSha256))
        {
            return new OperationUndoPreview(
                true,
                true,
                "This PATH change is already undone; repeating it is safe and makes no further change.",
                item);
        }

        if (!MatchesState(current, state.ExpectedValueExists, state.ExpectedValueKind, state.ExpectedSha256))
        {
            return RefusedPreview(
                item,
                "The current user PATH changed after this receipt was created. Refresh the activity state and review PATH manually; LibreSpot will not overwrite newer changes.");
        }

        return new OperationUndoPreview(
            true,
            false,
            $"Restore the exact user PATH captured before '{state.Entry}' was added. The current PATH fingerprint matches the receipt.",
            item);
    }

    public async Task<OperationUndoExecutionResult> ExecuteUndoAsync(
        OperationJournalUndoItem item,
        string configDirectory,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        try
        {
            return await ExecuteUndoCoreAsync(item, configDirectory, operationId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = $"Undo could not start safely: {ex.Message} The captured state was not removed.";
            try { await WriteJournalEntryAsync(configDirectory, operationId, item, "Failed", message, cancellationToken); } catch { }
            try { await WriteReceiptAsync(configDirectory, operationId, item, "failed", "failed", message, cancellationToken); } catch { }
            return new OperationUndoExecutionResult(false, false, false, operationId, "failed", message);
        }
    }

    private async Task<OperationUndoExecutionResult> ExecuteUndoCoreAsync(
        OperationJournalUndoItem item,
        string configDirectory,
        string operationId,
        CancellationToken cancellationToken)
    {
        var preview = PreviewUndoItem(item, configDirectory);
        if (!preview.CanExecute)
        {
            await WriteJournalEntryAsync(
                configDirectory,
                operationId,
                item,
                "Refused",
                preview.Reason,
                cancellationToken);
            await WriteReceiptAsync(configDirectory, operationId, item, "failed", "failed", preview.Reason, cancellationToken);
            return new OperationUndoExecutionResult(false, false, true, operationId, "refused", preview.Reason);
        }

        if (!TryReadPathUndoState(item, configDirectory, out var state, out var stateError))
        {
            return new OperationUndoExecutionResult(false, false, true, operationId, "refused", stateError);
        }

        await using var executionLock = await AcquireExecutionLockAsync(configDirectory, cancellationToken);
        preview = PreviewUndoItem(item, configDirectory);
        if (!preview.CanExecute)
        {
            await WriteJournalEntryAsync(configDirectory, operationId, item, "Refused", preview.Reason, cancellationToken);
            await WriteReceiptAsync(configDirectory, operationId, item, "failed", "failed", preview.Reason, cancellationToken);
            return new OperationUndoExecutionResult(false, false, true, operationId, "refused", preview.Reason);
        }

        await WriteJournalEntryAsync(configDirectory, operationId, item, "Started", preview.Reason, cancellationToken);
        if (preview.IsAlreadyUndone)
        {
            await WriteJournalEntryAsync(configDirectory, operationId, item, "AlreadyUndone", preview.Reason, cancellationToken);
            await WriteReceiptAsync(configDirectory, operationId, item, "success", "rolledBack", preview.Reason, cancellationToken);
            return new OperationUndoExecutionResult(true, false, false, operationId, "alreadyUndone", preview.Reason);
        }

        var changed = false;
        try
        {
            _environment.SetUserPath(new UserPathRegistryState(
                state!.PreviousValueExists,
                state.PreviousValue,
                ParseRegistryValueKind(state.PreviousValueKind)));
            changed = true;
            var restored = _environment.GetUserPath();
            if (!MatchesState(restored, state.PreviousValueExists, state.PreviousValueKind, state.PreviousSha256))
            {
                throw new IOException("The user PATH did not match the captured previous-state fingerprint after restoration.");
            }

            await WriteJournalEntryAsync(
                configDirectory,
                operationId,
                item,
                "RolledBack",
                "Restored the captured user PATH after exact-state validation.",
                cancellationToken);
            await WriteReceiptAsync(
                configDirectory,
                operationId,
                item,
                "success",
                "rolledBack",
                "Restored the captured user PATH after exact-state validation.",
                cancellationToken);
            return new OperationUndoExecutionResult(
                true,
                true,
                false,
                operationId,
                "succeeded",
                "The selected PATH change was undone. The recovery snapshot remains available for audit and retry safety.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var recovery = "The captured undo state remains available.";
            if (changed)
            {
                try
                {
                    var current = _environment.GetUserPath();
                    if (!MatchesState(current, state!.PreviousValueExists, state.PreviousValueKind, state.PreviousSha256))
                    {
                        _environment.SetUserPath(new UserPathRegistryState(
                            state.ExpectedValueExists,
                            state.ExpectedValue,
                            ParseRegistryValueKind(state.ExpectedValueKind)));
                        recovery = "LibreSpot restored the pre-undo PATH after the partial failure; the undo state remains available for retry.";
                    }
                }
                catch (Exception recoveryError)
                {
                    recovery = $"Automatic recovery also failed: {recoveryError.Message} The undo state remains available for manual recovery.";
                }
            }

            var message = $"Undo failed: {ex.Message} {recovery}";
            try { await WriteJournalEntryAsync(configDirectory, operationId, item, "Failed", message, cancellationToken); } catch { }
            try { await WriteReceiptAsync(configDirectory, operationId, item, "failed", "failed", message, cancellationToken); } catch { }
            return new OperationUndoExecutionResult(false, false, false, operationId, "failed", message);
        }
    }

    private static string UndoPolicyRefusal(OperationTokenMetadata token, string risk)
    {
        if (!token.Reversible)
        {
            return $"Undo token '{token.Kind}' is destructive or not reversible and is never executed automatically.";
        }

        if (token.RequiresAdmin)
        {
            return $"Undo token '{token.Kind}' requires elevation. This low-risk executor never elevates without a separate explicit consent flow.";
        }

        if (!string.Equals(risk, "low", StringComparison.OrdinalIgnoreCase))
        {
            return $"Undo token '{token.Kind}' is classified as {risk}; only low-risk tokens are executable.";
        }

        if (!token.ExecutableUndo)
        {
            return $"Undo token '{token.Kind}' is not on the executable allowlist. Follow its manual recovery guidance instead.";
        }

        return string.Empty;
    }

    private static OperationUndoPreview RefusedPreview(OperationJournalUndoItem item, string reason) =>
        new(false, false, reason, item);

    private static bool TryReadPathUndoState(
        OperationJournalUndoItem item,
        string configDirectory,
        out PathUndoState? state,
        out string error)
    {
        state = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(item.PreviousStateRef))
        {
            error = "The receipt does not contain a captured previous-state reference, so no automatic undo is safe.";
            return false;
        }

        try
        {
            var statePath = Path.GetFullPath(item.PreviousStateRef);
            var stateRoot = Path.GetFullPath(Path.Combine(configDirectory, "undo-states"));
            var stateRootPrefix = stateRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!statePath.StartsWith(stateRootPrefix, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(statePath), stateRoot, StringComparison.OrdinalIgnoreCase))
            {
                error = "The previous-state reference is outside LibreSpot's protected undo-state directory.";
                return false;
            }

            if (!File.Exists(statePath))
            {
                error = "The captured undo-state file is missing. LibreSpot will not guess the previous value.";
                return false;
            }

            if ((File.GetAttributes(statePath) & FileAttributes.ReparsePoint) != 0 ||
                Directory.Exists(stateRoot) && (File.GetAttributes(stateRoot) & FileAttributes.ReparsePoint) != 0)
            {
                error = "The undo-state path contains a reparse point and was refused for safety.";
                return false;
            }

            using var stream = File.Open(statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxUndoStateBytes)
            {
                error = "The captured undo-state file exceeds the 1 MiB safety limit.";
                return false;
            }

            state = JsonSerializer.Deserialize<PathUndoState>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (state is null || state.SchemaVersion != 2 ||
                !string.Equals(state.OperationId, item.OperationId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(state.TokenKind, item.TokenKind, StringComparison.Ordinal) ||
                !string.Equals(state.Scope, "User", StringComparison.Ordinal) ||
                !string.Equals(state.Target, "User PATH", StringComparison.Ordinal) ||
                !string.Equals(item.Target, "User PATH", StringComparison.OrdinalIgnoreCase))
            {
                error = "The captured undo state does not match the selected operation token.";
                state = null;
                return false;
            }

            if (!IsSupportedPathValueKind(state.PreviousValueKind) ||
                !IsSupportedPathValueKind(state.ExpectedValueKind) ||
                !state.ExpectedValueExists ||
                !string.Equals(HashText(state.PreviousValue), state.PreviousSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(HashText(state.ExpectedValue), state.ExpectedSha256, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(item.NewState, $"sha256:{state.ExpectedSha256}", StringComparison.OrdinalIgnoreCase))
            {
                error = "The captured undo state or receipt fingerprint is invalid. Automatic undo was refused.";
                state = null;
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            error = $"LibreSpot could not validate the captured undo state: {ex.Message}";
            state = null;
            return false;
        }
    }

    private static async Task<FileStream> AcquireExecutionLockAsync(string configDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(configDirectory);
        var lockPath = Path.Combine(configDirectory, "undo-execution.lock");
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException) when (attempt < 19)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        throw new IOException("Another LibreSpot undo operation is still running.");
    }

    private static async Task WriteJournalEntryAsync(
        string configDirectory,
        string operationId,
        OperationJournalUndoItem item,
        string result,
        string message,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(configDirectory);
        var entry = new
        {
            schemaVersion = 1,
            timestamp = DateTimeOffset.UtcNow,
            operationId,
            action = "Undo",
            phase = "undo",
            target = item.Target,
            safetyDecision = result == "Refused" ? "Refused" : "Allowed",
            result,
            wouldChange = result is "Started" or "RolledBack",
            reversible = false,
            rollbackHint = message,
            tokenKind = item.TokenKind,
            previousStateRef = string.Empty,
            newState = result,
            undoAction = "No automatic redo is offered.",
            risk = "low",
            data = new { sourceOperationId = item.OperationId }
        };
        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        var journalPath = Path.Combine(configDirectory, "operation-journal.jsonl");
        var bytes = Encoding.UTF8.GetBytes(line);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(journalPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true);
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(50, cancellationToken);
            }
        }
    }

    private static async Task WriteReceiptAsync(
        string configDirectory,
        string operationId,
        OperationJournalUndoItem item,
        string status,
        string operationResult,
        string message,
        CancellationToken cancellationToken)
    {
        var receipt = new
        {
            schemaVersion = 1,
            receiptId = Guid.NewGuid(),
            runId = Guid.Parse(operationId),
            operationId = Guid.Parse(operationId),
            startedAt = DateTimeOffset.UtcNow,
            completedAt = DateTimeOffset.UtcNow,
            action = "Undo",
            status,
            errorSummary = status == "success" ? null : message,
            undoAvailable = false,
            undoSourceOperationId = item.OperationId,
            undoSourceAction = item.Action,
            operations = new[]
            {
                new
                {
                    tokenKind = item.TokenKind,
                    target = item.Target,
                    previousStateRef = string.Empty,
                    newState = operationResult,
                    result = operationResult,
                    reversible = false,
                    undoAction = "No automatic redo is offered.",
                    risk = "low",
                    sourceResult = item.Result,
                    sourcePreviousStateRef = item.PreviousStateRef,
                    sourceNewState = item.NewState,
                    sourceReversible = true,
                    sourceUndoAction = item.UndoAction,
                    sourceRisk = item.Risk
                }
            }
        };
        var path = Path.Combine(configDirectory, ReceiptFileName);
        var tempPath = Path.Combine(configDirectory, $"{ReceiptFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false), cancellationToken);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    private static string HashText(string? value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();

    private static bool MatchesState(
        UserPathRegistryState current,
        bool expectedExists,
        string expectedKind,
        string expectedHash) =>
        current.Exists == expectedExists
        && (!expectedExists || string.Equals(current.Kind.ToString(), expectedKind, StringComparison.Ordinal))
        && string.Equals(HashText(current.Value), expectedHash, StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedPathValueKind(string value) =>
        value is nameof(RegistryValueKind.String) or nameof(RegistryValueKind.ExpandString);

    private static RegistryValueKind ParseRegistryValueKind(string value) =>
        value == nameof(RegistryValueKind.ExpandString)
            ? RegistryValueKind.ExpandString
            : RegistryValueKind.String;

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

    private static OperationJournalEntry? ParseUndoSourceOperation(
        string operationId,
        string action,
        JsonElement operation)
    {
        var tokenKind = GetString(operation, "tokenKind");
        if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(tokenKind))
        {
            return null;
        }

        return new OperationJournalEntry(
            operationId,
            action,
            Phase: string.Empty,
            GetString(operation, "target"),
            GetString(operation, "sourceResult"),
            WouldChange: true,
            GetBoolean(operation, "sourceReversible"),
            RollbackHint: string.Empty,
            tokenKind,
            GetString(operation, "sourcePreviousStateRef"),
            GetString(operation, "sourceNewState"),
            GetString(operation, "sourceUndoAction"),
            GetString(operation, "sourceRisk"));
    }

    private static string GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static bool GetBoolean(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static int? GetNullableInt32(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

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
                GetString(token, "risk"),
                GetBoolean(token, "requiresAdmin"),
                GetBoolean(token, "executableUndo"),
                GetString(token, "validationPolicy"),
                GetNullableInt32(token, "expiresAfterHours")))
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
        string Risk,
        bool RequiresAdmin = false,
        bool ExecutableUndo = false,
        string ValidationPolicy = "",
        int? ExpiresAfterHours = null);

    private sealed record PathUndoState(
        int SchemaVersion,
        string OperationId,
        string TokenKind,
        string Scope,
        string Target,
        string Entry,
        bool PreviousValueExists,
        string PreviousValue,
        string PreviousValueKind,
        bool ExpectedValueExists,
        string ExpectedValue,
        string ExpectedValueKind,
        string PreviousSha256,
        string ExpectedSha256,
        DateTimeOffset CreatedAtUtc);

    private sealed class WindowsOperationUndoEnvironment : IOperationUndoEnvironment
    {
        public UserPathRegistryState GetUserPath()
        {
            using var key = Registry.CurrentUser.OpenSubKey("Environment", writable: false);
            var exists = key?.GetValueNames().Any(name => string.Equals(name, "Path", StringComparison.OrdinalIgnoreCase)) == true;
            if (!exists || key is null)
            {
                return new UserPathRegistryState(false, string.Empty, RegistryValueKind.String);
            }

            return new UserPathRegistryState(
                true,
                key.GetValue("Path", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? string.Empty,
                key.GetValueKind("Path"));
        }

        public void SetUserPath(UserPathRegistryState state)
        {
            using var key = Registry.CurrentUser.CreateSubKey("Environment", writable: true)
                ?? throw new IOException("LibreSpot could not open the current-user environment registry key.");
            if (state.Exists)
            {
                if (state.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
                {
                    throw new IOException($"The captured PATH registry type '{state.Kind}' is not safe to restore.");
                }

                key.SetValue("Path", state.Value, state.Kind);
            }
            else
            {
                key.DeleteValue("Path", throwOnMissingValue: false);
            }
            UIntPtr result;
            _ = SendMessageTimeout(
                new IntPtr(0xffff),
                0x001A,
                UIntPtr.Zero,
                "Environment",
                0x0002,
                5000,
                out result);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint message,
            UIntPtr wParam,
            string lParam,
            uint flags,
            uint timeout,
            out UIntPtr result);
    }
}
