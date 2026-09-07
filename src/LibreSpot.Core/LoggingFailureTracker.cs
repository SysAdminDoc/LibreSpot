namespace LibreSpot.Desktop.Services;

public sealed class LoggingFailureTracker
{
    public const int MaxDiagnosticLength = 512;

    private readonly object _gate = new();
    private SupportBundleLoggingStatus? _status;

    public SupportBundleLoggingStatus? Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public SupportBundleLoggingStatus? TryRecord(
        string? diagnostic,
        string primaryDirectory,
        string? fallbackDirectory)
    {
        lock (_gate)
        {
            if (_status is not null)
            {
                return null;
            }

            var boundedDiagnostic = BoundDiagnostic(diagnostic);
            var fallbackActive = !string.IsNullOrWhiteSpace(fallbackDirectory);
            _status = new SupportBundleLoggingStatus(
                State: "degraded",
                UserMessage: fallbackActive
                    ? "Desktop file logging failed. New diagnostics are being written to a temporary fallback location."
                    : "Desktop file logging failed. Some log files may be missing from support exports.",
                Diagnostic: boundedDiagnostic,
                PrimaryDirectory: primaryDirectory,
                FallbackDirectory: fallbackDirectory,
                FallbackActive: fallbackActive,
                OccurredAtUtc: DateTimeOffset.UtcNow);
            return _status;
        }
    }

    private static string BoundDiagnostic(string? diagnostic)
    {
        var value = string.IsNullOrWhiteSpace(diagnostic)
            ? "The logging sink reported an unspecified failure."
            : diagnostic.Trim();
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        return value.Length <= MaxDiagnosticLength
            ? value
            : value[..(MaxDiagnosticLength - 1)] + "…";
    }
}
