using Xunit;

namespace LibreSpot.Desktop.Tests;

[CollectionDefinition("WPF UI automation", DisableParallelization = true)]
public sealed class WpfUiAutomationCollection
{
    private static readonly SemaphoreSlim AutomationGate = new(1, 1);

    public const string Name = "WPF UI automation";

    public static IDisposable EnterExclusive()
    {
        if (!AutomationGate.Wait(TimeSpan.FromMinutes(2)))
        {
            throw new TimeoutException("Timed out waiting for the WPF UI automation gate.");
        }

        return new AutomationGateLease();
    }

    private sealed class AutomationGateLease : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            AutomationGate.Release();
        }
    }
}
