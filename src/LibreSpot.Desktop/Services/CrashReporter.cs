using System.IO;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
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
    private static int _crashDialogOpen;

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
        WriteCrash("Dispatcher", e.Exception, isTerminating: true);
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

            ShowCrashDialog(path, exception, source, isTerminating);
        }
        catch
        {
            // Crash-while-crashing. Nothing useful we can do; avoid rethrowing and taking down the process faster.
        }
    }

    private static void ShowCrashDialog(string crashPath, Exception exception, string source, bool isTerminating)
    {
        if (Interlocked.Exchange(ref _crashDialogOpen, 1) == 1)
        {
            return;
        }

        // If the Dispatcher is gone, fall back to console. MessageBox on a dead dispatcher would throw.
        if (Application.Current?.Dispatcher is not { } dispatcher || dispatcher.HasShutdownStarted)
        {
            Console.Error.WriteLine($"LibreSpot crashed. Report written to {crashPath}");
            Interlocked.Exchange(ref _crashDialogOpen, 0);
            return;
        }

        dispatcher.Invoke(() =>
        {
            try
            {
                BuildCrashDialog(crashPath, exception, source, isTerminating).ShowDialog();
            }
            catch
            {
                Console.Error.WriteLine($"LibreSpot crashed. Report written to {crashPath}");
            }
            finally
            {
                Interlocked.Exchange(ref _crashDialogOpen, 0);
            }
        });
    }

    private static Window BuildCrashDialog(string crashPath, Exception exception, string source, bool isTerminating)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsVisible);
        var isRecoverable = !isTerminating && !string.Equals(source, "Dispatcher", StringComparison.OrdinalIgnoreCase);

        var canvasBrush = CreateBrush("#09110E");
        var surfaceBrush = CreateBrush("#121A16");
        var sectionBrush = CreateBrush("#18221D");
        var strokeBrush = CreateBrush("#26362D");
        var accentBrush = CreateBrush("#1ED760");
        var dangerBrush = CreateBrush("#EB5757");
        var warningBrush = CreateBrush("#D6A548");
        var textBrush = CreateBrush("#F3F7F4");
        var mutedBrush = CreateBrush("#A6B9AF");
        var subtleBrush = CreateBrush("#81938A");

        var title = isRecoverable
            ? "LibreSpot saved a crash report"
            : "LibreSpot hit an unexpected error and needs to close";
        var eyebrow = isRecoverable ? "RECOVERY AVAILABLE" : "CRASH REPORT SAVED";
        var body = isRecoverable
            ? "LibreSpot can keep running, but something failed in the background. Review the report if anything looks off before you continue."
            : "LibreSpot wrote a crash report so you can inspect what failed, reopen safely, and share the details if you need help.";
        var summaryTitle = isRecoverable ? "What you can do now" : "Before you reopen";
        var summaryBody = isRecoverable
            ? "Copy the report path or open the crash folder now. If the app still behaves strangely, restart LibreSpot before you keep working."
            : "Copy the report path or open the crash folder now. When you reopen LibreSpot, the saved profile and logs will still be available for troubleshooting.";
        var exceptionSummary = BuildExceptionSummary(exception);

        var dialog = new Window
        {
            Title = "LibreSpot recovery",
            Width = 640,
            SizeToContent = SizeToContent.Height,
            MinHeight = 390,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner is not null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Owner = owner,
            ShowInTaskbar = false,
            Background = canvasBrush,
            Foreground = textBrush
        };

        var root = new Border
        {
            Margin = new Thickness(0),
            Padding = new Thickness(22),
            Background = canvasBrush,
            Child = new Grid()
        };

        var grid = (Grid)root.Child;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = eyebrow,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = isRecoverable ? warningBrush : dangerBrush
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 26,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = textBrush
        });
        header.Children.Add(new TextBlock
        {
            Text = body,
            Margin = new Thickness(0, 12, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = mutedBrush,
            FontSize = 14
        });
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        var summaryStack = new StackPanel
        {
            Margin = new Thickness(0, 18, 0, 0)
        };
        summaryStack.Children.Add(new TextBlock
        {
            Text = summaryTitle,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = isRecoverable ? accentBrush : dangerBrush
        });
        summaryStack.Children.Add(new TextBlock
        {
            Text = summaryBody,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = mutedBrush,
            FontSize = 13
        });
        summaryStack.Children.Add(new Border
        {
            Height = 1,
            Background = strokeBrush,
            Margin = new Thickness(0, 16, 0, 0)
        });
        Grid.SetRow(summaryStack, 1);
        grid.Children.Add(summaryStack);

        var detailsCard = new Border
        {
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(12),
            Background = surfaceBrush,
            BorderBrush = strokeBrush,
            BorderThickness = new Thickness(1)
        };
        var detailsStack = new StackPanel();
        detailsStack.Children.Add(new TextBlock
        {
            Text = "EXCEPTION SUMMARY",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = subtleBrush
        });
        detailsStack.Children.Add(new TextBlock
        {
            Text = exceptionSummary,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = textBrush,
            FontSize = 13
        });
        detailsStack.Children.Add(new TextBlock
        {
            Text = "REPORT PATH",
            Margin = new Thickness(0, 16, 0, 0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = subtleBrush
        });
        detailsStack.Children.Add(new TextBox
        {
            Text = crashPath,
            Margin = new Thickness(0, 8, 0, 0),
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            Background = canvasBrush,
            BorderBrush = strokeBrush,
            BorderThickness = new Thickness(1),
            Foreground = textBrush,
            Padding = new Thickness(10, 8, 10, 8)
        });
        detailsStack.Children.Add(new TextBlock
        {
            Text = $"Source: {source}  •  Crash folder: {CrashRoot}",
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = subtleBrush,
            FontSize = 12
        });
        detailsCard.Child = detailsStack;
        Grid.SetRow(detailsCard, 2);
        grid.Children.Add(detailsCard);

        var footerText = new TextBlock
        {
            Text = "LibreSpot keeps the crash report and rolling session logs on disk after this dialog closes.",
            Margin = new Thickness(0, 16, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = subtleBrush,
            FontSize = 12
        };
        Grid.SetRow(footerText, 3);
        grid.Children.Add(footerText);

        var buttonRow = new StackPanel
        {
            Margin = new Thickness(0, 22, 0, 0),
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var copyButton = CreateDialogButton("Copy report path", false, () => TryCopyTextToClipboard(crashPath));
        var openButton = CreateDialogButton("Open crash folder", false, TryOpenCrashFolder);
        var closeButton = CreateDialogButton(isRecoverable ? "Continue" : "Close LibreSpot", true, () => dialog.Close());

        copyButton.Margin = new Thickness(0, 0, 10, 0);
        openButton.Margin = new Thickness(0, 0, 0, 0);
        closeButton.Margin = new Thickness(10, 0, 0, 0);

        buttonRow.Children.Add(copyButton);
        buttonRow.Children.Add(openButton);
        buttonRow.Children.Add(closeButton);
        Grid.SetRow(buttonRow, 4);
        grid.Children.Add(buttonRow);

        dialog.Content = root;
        return dialog;
    }

    private static Button CreateDialogButton(string text, bool isPrimary, Action action)
    {
        var background = isPrimary ? CreateBrush("#1ED760") : CreateBrush("#18221D");
        var borderBrush = isPrimary ? CreateBrush("#1ED760") : CreateBrush("#2A3B32");
        var foreground = isPrimary ? CreateBrush("#08100C") : CreateBrush("#F3F7F4");

        var button = new Button
        {
            Content = text,
            MinWidth = isPrimary ? 160 : 148,
            Height = 40,
            Padding = new Thickness(18, 0, 18, 0),
            Background = background,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            Foreground = foreground,
            FontWeight = FontWeights.SemiBold
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static string BuildExceptionSummary(Exception exception)
    {
        var baseException = exception.GetBaseException();
        var message = string.IsNullOrWhiteSpace(baseException.Message)
            ? "No exception message was provided."
            : baseException.Message.Trim();

        return $"{baseException.GetType().Name}: {message}";
    }

    private static void TryCopyTextToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard failures should never make a crash dialog worse.
        }
    }

    private static void TryOpenCrashFolder()
    {
        try
        {
            Directory.CreateDirectory(CrashRoot);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = CrashRoot,
                UseShellExecute = true
            });
        }
        catch
        {
            // Opening the folder is a convenience, not a requirement.
        }
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
