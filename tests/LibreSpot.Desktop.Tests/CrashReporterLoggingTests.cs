using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class CrashReporterLoggingTests
{
    [Fact]
    public void LoggingFailureTracker_BoundsTheDiagnosticAndReportsOnlyOnce()
    {
        var tracker = new LoggingFailureTracker();
        var first = tracker.TryRecord(new string('x', 2048), @"C:\primary", @"C:\fallback");
        var second = tracker.TryRecord("a later failure", @"C:\primary", @"C:\fallback");

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Same(first, tracker.Status);
        Assert.Equal(LoggingFailureTracker.MaxDiagnosticLength, first!.Diagnostic.Length);
        Assert.True(first.FallbackActive);
        Assert.Equal(@"C:\fallback", first.FallbackDirectory);
    }

    [Fact]
    public void LoggingFailureTracker_UsesAnExplicitMessageWhenSerilogProvidesNone()
    {
        var tracker = new LoggingFailureTracker();
        var status = tracker.TryRecord(null, @"C:\primary", null);

        Assert.NotNull(status);
        Assert.Contains("unspecified failure", status!.Diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.False(status.FallbackActive);
        Assert.Contains("Some log files may be missing", status.UserMessage, StringComparison.Ordinal);
    }
}
