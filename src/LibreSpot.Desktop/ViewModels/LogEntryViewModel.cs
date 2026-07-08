namespace LibreSpot.Desktop.ViewModels;

public sealed class LogEntryViewModel
{
    public LogEntryViewModel(DateTime timestamp, string level, string message)
    {
        Timestamp = timestamp;
        Level = string.IsNullOrWhiteSpace(level) ? "INFO" : level.Trim().ToUpperInvariant();
        Message = message;
    }

    public DateTime Timestamp { get; }
    public string Level { get; }
    public string Message { get; }

    public string TimestampDisplay => Timestamp.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    public string CopyLine => $"[{TimestampDisplay}] [{Level}] {Message}";
}
