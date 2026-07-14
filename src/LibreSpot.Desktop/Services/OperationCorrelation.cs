using System.Diagnostics.Tracing;

namespace LibreSpot.Desktop.Services;

public static class OperationCorrelation
{
    private static string? _currentOperationId;
    private static string? _lastOperationId;

    public static string? CurrentOperationId => Volatile.Read(ref _currentOperationId);
    public static string? LastOperationId => Volatile.Read(ref _lastOperationId);
    public static string? CurrentOrLastOperationId => CurrentOperationId ?? LastOperationId;

    public static Guid Begin(string surface, string action, Guid? operationId = null)
    {
        var id = operationId ?? Guid.NewGuid();
        var value = id.ToString();
        Volatile.Write(ref _lastOperationId, value);
        Volatile.Write(ref _currentOperationId, value);
        LibreSpotOperationEventSource.Log.OperationStarted(value, surface, action);
        return id;
    }

    public static void Complete(Guid operationId, string surface, string action, string outcome)
    {
        var value = operationId.ToString();
        Volatile.Write(ref _lastOperationId, value);
        while (true)
        {
            var current = Volatile.Read(ref _currentOperationId);
            if (!string.Equals(current, value, StringComparison.Ordinal) ||
                ReferenceEquals(Interlocked.CompareExchange(ref _currentOperationId, null, current), current))
            {
                break;
            }
        }
        LibreSpotOperationEventSource.Log.OperationCompleted(value, surface, action, outcome);
    }

    internal static void ResetForTests()
    {
        Volatile.Write(ref _currentOperationId, null);
        Volatile.Write(ref _lastOperationId, null);
    }
}

[EventSource(Name = "LibreSpot-Operations")]
public sealed class LibreSpotOperationEventSource : EventSource
{
    public static LibreSpotOperationEventSource Log { get; } = new();

    private LibreSpotOperationEventSource()
    {
    }

    [Event(1, Level = EventLevel.Informational)]
    public void OperationStarted(string operationId, string surface, string action)
    {
        if (IsEnabled())
        {
            WriteEvent(1, operationId, surface, action);
        }
    }

    [Event(2, Level = EventLevel.Verbose)]
    public void BackendMessage(string operationId, string action, string kind, string level, string payload)
    {
        if (IsEnabled())
        {
            WriteEvent(2, operationId, action, kind, level, payload);
        }
    }

    [Event(3, Level = EventLevel.Informational)]
    public void OperationCompleted(string operationId, string surface, string action, string outcome)
    {
        if (IsEnabled())
        {
            WriteEvent(3, operationId, surface, action, outcome);
        }
    }
}
