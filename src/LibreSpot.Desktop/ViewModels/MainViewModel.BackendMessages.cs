using LibreSpot.Desktop.Properties;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Desktop.ViewModels;

// The backend speaks one line at a time and every one of them lands on the
// dispatcher. Kept beside the view model rather than inside it so the shell
// class stays under the size the composition test enforces.
public sealed partial class MainViewModel
{
    private void HandleBackendMessage(BackendMessage message)
    {
        // Use BeginInvoke (fire-and-forget) instead of synchronous Invoke to
        // prevent deadlock during shutdown: if the dispatcher thread is blocked
        // waiting for the backend process to exit while the process output
        // callback tries to Invoke back onto the dispatcher, both threads block.
        _dispatcher.BeginInvoke(() =>
        {
            switch (message.Kind)
            {
                case "progress":
                    if (double.TryParse(message.Payload, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var value))
                    {
                        ProgressValue = Math.Clamp(value, 0, 100);
                    }
                    break;
                case "status":
                    ActivityStatus = message.Payload;
                    break;
                case "step":
                    ActivityStep = message.Payload;
                    break;
                case "result":
                    if (string.Equals(message.Level, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        _activityOutcome = ActivityOutcome.Success;
                        ActivityStatus = Strings.RunComplete;
                        ActivityStep = L("Vm_LibreSpotReady");
                        ProgressValue = 100;
                    }
                    else if (string.Equals(message.Level, "WARN", StringComparison.OrdinalIgnoreCase))
                    {
                        // The action reached the end but something it was asked to
                        // install is not there. The payload names it, and it goes on
                        // the step line so the shell says which one rather than
                        // leaving it in a log nobody reads.
                        _activityOutcome = ActivityOutcome.Warning;
                        ActivityStatus = Strings.RunNeedsAttention;
                        ActivityStep = message.Payload;
                        ProgressValue = 100;
                    }
                    else
                    {
                        _activityOutcome = ActivityOutcome.Error;
                        ActivityStatus = Strings.RunNeedsAttention;
                    }

                    AppendLog(message.Payload, message.Level);
                    break;
                default:
                    AppendLog(message.Payload, message.Level);
                    break;
            }
        });
    }

    private void AppendLog(string payload, string level)
    {
        _activityState.AppendLog(payload, level, DateTime.Now);
    }

    private void ClearLog() => _activityState.ClearLog();
}
