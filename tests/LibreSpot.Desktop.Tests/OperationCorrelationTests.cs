using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class OperationCorrelationTests
{
    [Fact]
    public void BeginAndComplete_PublishLocalEventSourceWithStableId()
    {
        var operationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        using var listener = new CollectingEventListener();
        listener.EnableEvents(LibreSpotOperationEventSource.Log, EventLevel.Verbose);

        OperationCorrelation.ResetForTests();
        OperationCorrelation.Begin("test", "Install", operationId);
        LibreSpotOperationEventSource.Log.BackendMessage(
            operationId.ToString(), "Install", "status", "INFO", "working");
        OperationCorrelation.Complete(operationId, "test", "Install", "Success");

        Assert.Equal(operationId.ToString(), OperationCorrelation.LastOperationId);
        Assert.Null(OperationCorrelation.CurrentOperationId);
        var correlatedEvents = listener.Events
            .Where(item => item.EventId is 1 or 2 or 3)
            .Where(item => string.Equals(item.Payload?[0]?.ToString(), operationId.ToString(), StringComparison.Ordinal))
            .OrderBy(item => item.EventId);
        Assert.Collection(
            correlatedEvents,
            started => Assert.Equal(operationId.ToString(), started.Payload![0]),
            message => Assert.Equal(operationId.ToString(), message.Payload![0]),
            completed => Assert.Equal(operationId.ToString(), completed.Payload![0]));
    }

    private sealed class CollectingEventListener : EventListener
    {
        public ConcurrentBag<EventWrittenEventArgs> Events { get; } = [];

        protected override void OnEventWritten(EventWrittenEventArgs eventData) => Events.Add(eventData);
    }
}
