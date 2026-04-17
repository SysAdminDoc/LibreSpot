using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using Serilog.Events;

namespace LibreSpot.Desktop.Services;

public static class CrashReporter
{
    private static readonly string LogRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LibreSpot", "logs");

    private static readonly string CrashRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LibreSpot", "crashes");

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        Directory.CreateDirectory(LogRoot);
        Directory.CreateDirectory(CrashRoot);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Information)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .WriteTo.File(
                path: Path.Combine(LogRoot, "librespot-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();

        Log.Information("LibreSpot desktop shell starting. Version {Version} on {OS}",
            typeof(CrashReporter).Assembly.GetName().Version?.ToString() ?? "unknown",
            Environment.OSVersion.VersionString);

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;
        if (Application.Current is { } app)
        {
            app.DispatcherUnhandledException += OnDispatcherUnhandled;
            app.Exit += OnExit;
        }
    }

    private static void OnExit(object sender, ExitEventArgs e)
    {
        Log.Information("LibreSpot desktop shell exiting with code {ExitCode}", e.ApplicationExitCode);
        Log.CloseAndFlush();
    }

    private static void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrash("Dispatcher", e.Exception);
        // Let the default handler terminate — a dispatcher crash leaves WPF in
        // an inconsistent state and recovery rarely succeeds cleanly.
    }

    private static void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrash("UnobservedTask", e.Exception);
        // Mark observed so finalizer doesn't escalate further (the log file is now authoritative).
        e.SetObserved();
    }

    private static void OnAppDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrash("AppDomain", ex, e.IsTerminating);
        }
    }

    private static void WriteCrash(string source, Exception exception, bool isTerminating = false)
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(CrashRoot, $"crash-{stamp}-{source}.log");

            var body = new StringBuilder();
            body.AppendLine($"LibreSpot crash report");
            body.AppendLine($"  timestamp:      {DateTime.Now:o}");
            body.AppendLine($"  source:         {source}");
            body.AppendLine($"  terminating:    {isTerminating}");
            body.AppendLine($"  version:        {typeof(CrashReporter).Assembly.GetName().Version}");
            body.AppendLine($"  os:             {Environment.OSVersion.VersionString}");
            body.AppendLine($"  clr:            {Environment.Version}");
            body.AppendLine($"  working-set:    {Environment.WorkingSet / 1024 / 1024} MB");
            body.AppendLine();
            body.AppendLine("Exception:");
            body.AppendLine(exception.ToString());

            File.WriteAllText(path, body.ToString());
            Log.Fatal(exception, "Unhandled exception from {Source} (terminating={IsTerminating}). Report at {Path}",
                source, isTerminating, path);

            ShowCrashDialog(path, exception);
        }
        catch
        {
            // Crash-while-crashing. Nothing useful we can do; avoid rethrowing and taking down the process faster.
        }
    }

    private static void ShowCrashDialog(string crashPath, Exception exception)
    {
        // If the Dispatcher is gone, fall back to console. MessageBox on a dead dispatcher would throw.
        if (Application.Current?.Dispatcher is not { } dispatcher || dispatcher.HasShutdownStarted)
        {
            Console.Error.WriteLine($"LibreSpot crashed. Report written to {crashPath}");
            return;
        }

        dispatcher.Invoke(() =>
        {
            var message =
                $"LibreSpot encountered an unexpected error.\n\n" +
                $"{exception.GetType().Name}: {exception.Message}\n\n" +
                $"Full report: {crashPath}\n\n" +
                $"Click OK to copy the crash path to the clipboard and open the folder.";
            var result = MessageBox.Show(
                message,
                "LibreSpot crash",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Error,
                MessageBoxResult.OK);

            if (result == MessageBoxResult.OK)
            {
                try { Clipboard.SetText(crashPath); } catch { }
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = CrashRoot,
                        UseShellExecute = true,
                    });
                }
                catch { }
            }
        });
    }
}
