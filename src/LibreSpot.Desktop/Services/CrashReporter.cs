using System.IO;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using Serilog;
using Serilog.Events;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace LibreSpot.Desktop.Services;

public static class CrashReporter
{
    private static readonly string LogRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LibreSpot", "logs");

    private static readonly string CrashRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LibreSpot", "crashes");

    private static int _initialized;
    private static int _crashDialogOpen;

    public static void Initialize()
    {
        // Atomic check-and-set to prevent double-initialization from concurrent callers.
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(LogRoot);
            Directory.CreateDirectory(CrashRoot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CrashReporter: could not create log/crash directories: {ex.Message}");
        }

        // Clean up crash reports older than 30 days to prevent unbounded growth.
        try
        {
            foreach (var file in Directory.GetFiles(CrashRoot, "crash-*.log"))
            {
                if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-30))
                {
                    try { File.Delete(file); } catch { /* best-effort cleanup */ }
                }
            }
        }
        catch { /* non-critical */ }

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
            var path = CreateCrashReportPath(source);

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

    private static string CreateCrashReportPath(string source)
    {
        var safeSource = string.Join(
            "_",
            (string.IsNullOrWhiteSpace(source) ? "Unknown" : source)
                .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeSource))
        {
            safeSource = "Unknown";
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = attempt == 0 ? string.Empty : $"-{attempt}";
            var candidate = Path.Combine(CrashRoot, $"crash-{stamp}-{safeSource}{suffix}.log");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(CrashRoot, $"crash-{Guid.NewGuid():N}-{safeSource}.log");
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

        // Use BeginInvoke to avoid re-entrancy deadlock: if the crash occurred during
        // a dispatcher frame (binding update, converter), synchronous Invoke would push
        // a nested message loop that can cascade into secondary exceptions.
        dispatcher.BeginInvoke(() =>
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

        var canvasBrush = ThemeBrush("WorkspaceBackdropBrush", System.Windows.SystemColors.WindowBrush);
        var surfaceBrush = ThemeBrush("SurfaceCardBrush", System.Windows.SystemColors.ControlBrush);
        var strokeBrush = ThemeBrush("StrokeBrush", System.Windows.SystemColors.ActiveBorderBrush);
        var accentBrush = ThemeBrush("AccentBrush", System.Windows.SystemColors.HighlightBrush);
        var dangerBrush = ThemeBrush("DangerBrush", System.Windows.SystemColors.HotTrackBrush);
        var warningBrush = ThemeBrush("WarningBrush", System.Windows.SystemColors.HotTrackBrush);
        var textBrush = ThemeBrush("TextBrush", System.Windows.SystemColors.WindowTextBrush);
        var mutedBrush = ThemeBrush("MutedTextBrush", System.Windows.SystemColors.GrayTextBrush);
        var subtleBrush = ThemeBrush("SubtleTextBrush", System.Windows.SystemColors.GrayTextBrush);
        var headingFont = ThemeFont("HeadingFont", System.Windows.SystemFonts.MessageFontFamily);
        var bodyFont = ThemeFont("BodyFont", System.Windows.SystemFonts.MessageFontFamily);

        var title = isRecoverable
            ? L("CrashRecoverableTitle")
            : L("CrashFatalTitle");
        var eyebrow = isRecoverable ? L("CrashRecoverableEyebrow") : L("CrashFatalEyebrow");
        var body = isRecoverable
            ? L("CrashRecoverableBody")
            : L("CrashFatalBody");
        var summaryTitle = isRecoverable ? L("CrashRecoverableSummaryTitle") : L("CrashFatalSummaryTitle");
        var summaryBody = isRecoverable
            ? L("CrashRecoverableSummaryBody")
            : L("CrashFatalSummaryBody");
        var exceptionSummary = BuildExceptionSummary(exception);

        var dialog = new Window
        {
            Title = L("CrashWindowTitle"),
            Width = 700,
            SizeToContent = SizeToContent.Height,
            MinHeight = 420,
            MaxHeight = Math.Max(520, SystemParameters.WorkArea.Height - 80),
            ResizeMode = ResizeMode.CanResizeWithGrip,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
            Background = canvasBrush,
            Foreground = textBrush,
            FontFamily = bodyFont
        };
        dialog.SourceInitialized += (_, _) => Win11ShellIntegration.ApplyMicaAndDarkChrome(dialog);
        // Setting Owner on a closing/disposed window throws InvalidOperationException.
        try { if (owner is { IsVisible: true }) dialog.Owner = owner; } catch { /* fall back to CenterScreen */ }

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
            FontFamily = headingFont,
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
            CornerRadius = ThemeCornerRadius("RadiusXl", new CornerRadius(12)),
            Background = surfaceBrush,
            BorderBrush = strokeBrush,
            BorderThickness = new Thickness(1)
        };
        var detailsStack = new StackPanel();
        detailsStack.Children.Add(new TextBlock
        {
            Text = L("CrashExceptionSummaryLabel"),
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
            Text = L("CrashReportPathLabel"),
            Margin = new Thickness(0, 16, 0, 0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = subtleBrush
        });
        var reportPathBox = new TextBox
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
        };
        reportPathBox.Style = ThemeStyle("TextBoxStylePremium");
        AutomationProperties.SetName(reportPathBox, L("CrashReportPathLabel"));
        detailsStack.Children.Add(reportPathBox);
        detailsStack.Children.Add(new TextBlock
        {
            Text = LF("CrashSourceFolderFormat", source, CrashRoot),
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
            Text = L("CrashFooter"),
            Margin = new Thickness(0, 16, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = subtleBrush,
            FontSize = 12
        };
        Grid.SetRow(footerText, 3);
        grid.Children.Add(footerText);

        var buttonRow = new WrapPanel
        {
            Margin = new Thickness(0, 22, 0, 0),
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var copyButton = CreateDialogButton(L("CrashCopyReportPath"), false, () => TryCopyTextToClipboard(crashPath));
        var openButton = CreateDialogButton(L("CrashOpenFolder"), false, TryOpenCrashFolder);
        var closeButton = CreateDialogButton(isRecoverable ? L("CrashContinue") : L("CrashClose"), true, () => dialog.Close());

        copyButton.Margin = new Thickness(0, 0, 10, 0);
        openButton.Margin = new Thickness(0, 0, 0, 0);
        closeButton.Margin = new Thickness(10, 0, 0, 0);

        buttonRow.Children.Add(copyButton);
        buttonRow.Children.Add(openButton);
        buttonRow.Children.Add(closeButton);
        Grid.SetRow(buttonRow, 4);
        grid.Children.Add(buttonRow);

        dialog.Content = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = Math.Max(480, SystemParameters.WorkArea.Height - 120),
            Background = canvasBrush
        };
        return dialog;
    }

    internal static Window BuildPreviewDialogForUiAutomation() =>
        BuildCrashDialog(
            Path.Combine(Path.GetTempPath(), "LibreSpot", "crashes", "crash-preview.log"),
            new InvalidOperationException(L("CrashNoExceptionMessage")),
            nameof(BuildPreviewDialogForUiAutomation),
            isTerminating: false);

    private static Button CreateDialogButton(string text, bool isPrimary, Action action)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            },
            MinWidth = isPrimary ? 154 : 142,
            MinHeight = 42,
            Padding = new Thickness(18, 0, 18, 0),
            FontWeight = FontWeights.SemiBold,
            Style = ThemeStyle(isPrimary ? "PrimaryButtonStyle" : "SecondaryButtonStyle")
        };
        AutomationProperties.SetName(button, text);
        button.Click += (_, _) => action();
        return button;
    }

    private static string BuildExceptionSummary(Exception exception)
    {
        var baseException = exception.GetBaseException();
        var message = string.IsNullOrWhiteSpace(baseException.Message)
            ? L("CrashNoExceptionMessage")
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
            })?.Dispose();
        }
        catch
        {
            // Opening the folder is a convenience, not a requirement.
        }
    }

    private static System.Windows.Media.Brush ThemeBrush(string key, System.Windows.Media.Brush fallback) =>
        Application.Current?.TryFindResource(key) as System.Windows.Media.Brush ?? fallback;

    private static System.Windows.Media.FontFamily ThemeFont(string key, System.Windows.Media.FontFamily fallback) =>
        Application.Current?.TryFindResource(key) as System.Windows.Media.FontFamily ?? fallback;

    private static Style? ThemeStyle(string key) =>
        Application.Current?.TryFindResource(key) as Style;

    private static CornerRadius ThemeCornerRadius(string key, CornerRadius fallback) =>
        Application.Current?.TryFindResource(key) is CornerRadius radius ? radius : fallback;

    private static string L(string key) => LocalizationService.Current.GetString(key);

    private static string LF(string key, params object?[] args) =>
        string.Format(LocalizationService.Current.Culture, L(key), args);
}
