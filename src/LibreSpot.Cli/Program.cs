using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;

namespace LibreSpot.Cli;

[SupportedOSPlatform("windows")]
public static class Program
{
    public static int Main(string[] args) =>
        CliApplication.Run(args, Console.Out, Console.Error);
}

[SupportedOSPlatform("windows")]
public static class CliApplication
{
    private const int Success = 0;
    private const int UnhandledFailure = 1;
    private const int ValidationError = 2;
    private const int NotInstalled = 10;
    private const int DriftDetected = 11;
    private const int RepairNeeded = 12;
    private const int MaxCustomPatchesJsonBytes = 65536;
    private const int MaxAnswerFileBytes = 1024 * 1024;
    private const int MaxConfigurationBytes = 1024 * 1024;

    private static readonly HashSet<int> PublicBackendExitCodes = new()
    {
        20, 30, 40, 50, 60, 1618, 3010, 1641
    };

    private static readonly HashSet<string> SupportedCultures = new(StringComparer.Ordinal)
    {
        "en", "ru", "zh-Hans", "pt-BR", "es"
    };

    private static readonly HashSet<string> SpotXLyricsThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "spotify", "blueberry", "blue", "discord", "forest", "fresh", "github", "lavender",
        "orange", "pumpkin", "purple", "red", "strawberry", "turquoise", "yellow", "oceano",
        "royal", "krux", "pinkle", "zing", "radium", "sandbar", "postlight", "relish",
        "drot", "default", "spotify#2"
    };

    private static readonly HashSet<string> SpotXDownloadMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty, "curl", "webclient"
    };

    private static readonly HashSet<string> SpicetifyExtensions = new(StringComparer.Ordinal)
    {
        "fullAppDisplay.js", "shuffle+.js", "trashbin.js", "keyboardShortcut.js", "bookmark.js",
        "loopyLoop.js", "popupLyrics.js", "autoSkipVideo.js", "autoSkipExplicit.js",
        "webnowplaying.js", "hidePodcasts.js", "beautiful-lyrics.mjs", "playlist-icons.js",
        "volumePercentage.js", "adblock.js"
    };

    private static readonly HashSet<string> SpicetifyCustomApps = new(StringComparer.Ordinal)
    {
        "stats"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions ConfigurationJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true
    };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--answer-file",
        "--config-path",
        "--correlation-id",
        "--log-dir",
        "--output",
        "--profile",
        "--repair-id",
        "--scope"
    };

    private static readonly IReadOnlyList<string> KnownRepairActions =
    [
        "Install",
        "Reapply",
        "RepairMarketplace",
        "OpenMarketplace",
        "SafeMode",
        "CreateBackup",
        "RestoreBackup",
        "RestoreVanilla",
        "UninstallSpicetify",
        "FullReset",
        "RemoveSelfData",
        "ClearCache",
        "EnableAutoReapply",
        "DisableAutoReapply"
    ];

    public static int Run(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, EnvironmentSnapshot>? snapshotFactory = null,
        Func<string, string, Action<BackendMessage>, CancellationToken, Task<BackendRunResult>>? backendRunner = null)
    {
        try
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                WriteUsage(stderr);
                return args.Length == 0 ? ValidationError : Success;
            }

            if (IsVersion(args[0]))
            {
                var versionParse = Parse(args.Skip(1).ToArray());
                if (versionParse.Error is not null)
                {
                    stderr.WriteLine(versionParse.Error);
                    return ValidationError;
                }

                return RunVersion(versionParse.Options ?? new CliOptions(new Dictionary<string, string?>()), stdout, stderr);
            }

            var (verb, optionArgs) = NormalizeVerb(args);
            var parse = Parse(optionArgs);
            if (parse.Error is not null)
            {
                stderr.WriteLine(parse.Error);
                return ValidationError;
            }

            var options = parse.Options ?? throw new InvalidOperationException("CLI parser returned no options.");
            return verb switch
            {
                "status" => RunStatus(options, stdout, stderr, snapshotFactory),
                "detect" => RunDetect(options, stdout, stderr, snapshotFactory),
                "validate" => RunValidate(options, stdout, stderr),
                "install" => RunPlannedOperation("install", options, stdout, stderr, backendRunner),
                "reapply" => RunPlannedOperation("reapply", options, stdout, stderr, backendRunner),
                "uninstall" => RunPlannedOperation("uninstall", options, stdout, stderr, backendRunner),
                "repair" => RunPlannedOperation("repair", options, stdout, stderr, backendRunner),
                "plan" => RunPlannedOperation("install", options, stdout, stderr, planVerb: true),
                "export-support" => RunExportSupport(options, stdout, stderr, snapshotFactory),
                "watcher install" => RunWatcher("install", options, stdout, stderr, backendRunner),
                "watcher remove" => RunWatcher("remove", options, stdout, stderr, backendRunner),
                "version" => RunVersion(options, stdout, stderr),
                _ => UnknownVerb(verb, stderr)
            };
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Unhandled LibreSpot CLI failure: {ex.Message}");
            return UnhandledFailure;
        }
    }

    private static (string Verb, string[] OptionArgs) NormalizeVerb(string[] args)
    {
        var verb = args[0].Trim().ToLowerInvariant();
        if (string.Equals(verb, "watcher", StringComparison.OrdinalIgnoreCase) && args.Length >= 2)
        {
            var subverb = args[1].Trim().ToLowerInvariant();
            if (subverb is "install" or "remove")
            {
                return ($"watcher {subverb}", args.Skip(2).ToArray());
            }
        }

        return (verb, args.Skip(1).ToArray());
    }

    private static int RunStatus(
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, EnvironmentSnapshot>? snapshotFactory)
    {
        if (!options.OnlyContains("--json", "--config-path", "--correlation-id", "--log-dir", "--scope"))
        {
            stderr.WriteLine("status received an unsupported flag.");
            return ValidationError;
        }

        if (!TryResolveConfigPath(options, out var configPath, out var configPathError))
        {
            stderr.WriteLine(configPathError);
            return ValidationError;
        }

        var snapshot = GetSnapshot(configPath, snapshotFactory);
        var document = BuildStatusDocument(snapshot, configPath);

        if (options.HasFlag("--json"))
        {
            WriteJson(stdout, document);
        }
        else
        {
            stdout.WriteLine($"{snapshot.HealthReport.StatusTitle}: {snapshot.HealthReport.IssueSummary}");
        }

        return Success;
    }

    private static int RunVersion(CliOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (!options.OnlyContains("--json"))
        {
            stderr.WriteLine("version received an unsupported flag.");
            return ValidationError;
        }

        if (options.HasFlag("--json"))
        {
            WriteJson(stdout, BuildVersionDocument());
        }
        else
        {
            stdout.WriteLine($"LibreSpot.Cli {ProductVersion}");
        }

        return Success;
    }

    private static int RunDetect(
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, EnvironmentSnapshot>? snapshotFactory)
    {
        if (!options.OnlyContains("--json", "--intune", "--config-path", "--answer-file", "--profile", "--correlation-id", "--log-dir", "--scope"))
        {
            stderr.WriteLine("detect received an unsupported flag.");
            return ValidationError;
        }

        if (!TryResolveConfigPath(options, out var configPath, out var configPathError))
        {
            stderr.WriteLine(configPathError);
            return ValidationError;
        }

        var snapshot = GetSnapshot(configPath, snapshotFactory);
        var detection = BuildDetectionDocument(snapshot, configPath);

        if (options.HasFlag("--json"))
        {
            WriteJson(stdout, detection);
            return detection.ExitCode;
        }
        else if (options.HasFlag("--intune"))
        {
            if (detection.ExitCode == Success)
            {
                stdout.WriteLine("LibreSpot compliant");
            }
            else
            {
                stderr.WriteLine($"LibreSpot {detection.State}: {detection.Summary}");
            }

            return detection.ExitCode;
        }

        stdout.WriteLine($"{detection.State}: {detection.Summary}");

        return detection.ExitCode;
    }

    private static int RunValidate(CliOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (!options.OnlyContains("--json", "--answer-file", "--profile", "--config-path", "--correlation-id", "--log-dir"))
        {
            stderr.WriteLine("validate received an unsupported flag.");
            return ValidationError;
        }

        var answerFile = options.GetValue("--answer-file");
        if (string.IsNullOrWhiteSpace(answerFile))
        {
            stderr.WriteLine("validate requires --answer-file <path>.");
            return ValidationError;
        }

        var result = ValidateAnswerFile(answerFile, options.GetValue("--profile"));
        if (options.HasFlag("--json"))
        {
            WriteJson(stdout, result);
        }
        else if (result.Valid)
        {
            stdout.WriteLine("Answer file valid");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                stderr.WriteLine($"{error.Path}: {error.Message}");
            }
        }

        return result.Valid ? Success : ValidationError;
    }

    private static int RunExportSupport(
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, EnvironmentSnapshot>? snapshotFactory)
    {
        if (!options.OnlyContains("--output", "--correlation-id", "--log-dir"))
        {
            stderr.WriteLine("export-support received an unsupported flag.");
            return ValidationError;
        }

        var configPath = DefaultConfigPath;
        var configDirectory = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
        var outputPath = options.GetValue("--output");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            outputPath = Path.Combine(Environment.CurrentDirectory, $"LibreSpot-support-{stamp}.zip");
        }

        var snapshot = GetSnapshot(configPath, snapshotFactory);
        var service = new SupportBundleService(
            configDirectory,
            rollingLogDirectory: options.GetValue("--log-dir"));
        var result = service.ExportAsync(
                outputPath,
                snapshot,
                new SupportBundleOptions(
                    IncludeOperationJournal: true,
                    IncludeLogs: true,
                    IncludeCrashReports: true))
            .GetAwaiter()
            .GetResult();

        stdout.WriteLine($"Support bundle exported: {result.Path}");
        stdout.WriteLine($"Entries: {result.EntryCount}; Bytes: {result.BytesWritten}");
        return Success;
    }

    private static int RunWatcher(
        string operation,
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, string, Action<BackendMessage>, CancellationToken, Task<BackendRunResult>>? backendRunner)
    {
        if (!options.OnlyContains("--silent", "--quiet", "--yes", "--correlation-id", "--log-dir", "--scope"))
        {
            stderr.WriteLine($"watcher {operation} received an unsupported flag.");
            return ValidationError;
        }

        var action = operation == "install" ? "EnableAutoReapply" : "DisableAutoReapply";
        if (!TryResolveConfigPath(options, out var configPath, out var configPathError))
        {
            stderr.WriteLine(configPathError);
            return ValidationError;
        }

        var quiet = options.HasFlag("--quiet") || options.HasFlag("--silent");
        var runner = backendRunner ?? ((backendAction, backendConfigPath, onMessage, token) =>
            new BackendScriptService().RunAsync(backendAction, backendConfigPath, onMessage, token));

        var result = runner(
                action,
                configPath,
                message =>
                {
                    if (!quiet && message.Kind is "status" or "step")
                    {
                        stdout.WriteLine(message.Payload);
                    }

                    if (message.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase) ||
                        message.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        stderr.WriteLine(message.Payload);
                    }
                },
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (!result.Success)
        {
            stderr.WriteLine(result.ErrorMessage ?? $"watcher {operation} failed.");
            return ResolveBackendExitCode(result);
        }

        if (!quiet)
        {
            stdout.WriteLine(operation == "install"
                ? "Auto-reapply watcher installed."
                : "Auto-reapply watcher removed.");
        }

        return Success;
    }

    private static int RunPlannedOperation(
        string operation,
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, string, Action<BackendMessage>, CancellationToken, Task<BackendRunResult>>? backendRunner = null,
        bool planVerb = false)
    {
        if (!options.OnlyContains(
                "--answer-file",
                "--repair-id",
                "--profile",
                "--config-path",
                "--silent",
                "--quiet",
                "--accept-eula",
                "--no-restart",
                "--dry-run",
                "--yes",
                "--correlation-id",
                "--log-dir",
                "--ndjson",
                "--json",
                "--scope",
                "--purge",
                "--keep-spotify"))
        {
            stderr.WriteLine($"{operation} received an unsupported flag.");
            return ValidationError;
        }

        if (!planVerb && options.HasFlag("--json"))
        {
            stderr.WriteLine($"{operation} does not support --json. Use --ndjson for dry-run events or the plan verb for a JSON plan.");
            return ValidationError;
        }

        if (planVerb && options.HasFlag("--ndjson"))
        {
            stderr.WriteLine("plan does not support --ndjson. Use --json for a single plan document.");
            return ValidationError;
        }

        var answerFile = options.GetValue("--answer-file");
        ValidationDocument? validation = null;
        string? repairError = null;
        if (!TryResolveConfigPath(options, out var configPath, out var configPathError))
        {
            stderr.WriteLine(configPathError);
            return ValidationError;
        }

        var repairAction = operation == "repair"
            ? ResolveRepairAction(options.GetValue("--repair-id"), out repairError)
            : null;
        if (repairError is not null)
        {
            stderr.WriteLine(repairError);
            return ValidationError;
        }

        var operationId = Guid.NewGuid();
        if (!string.IsNullOrWhiteSpace(answerFile))
        {
            validation = ValidateAnswerFile(answerFile, options.GetValue("--profile"));
            if (!validation.Valid)
            {
                if (options.HasFlag("--json"))
                {
                    WriteJson(stdout, validation);
                }
                else if (options.HasFlag("--ndjson"))
                {
                    WriteJson(stdout, NdjsonEvent(
                        "LS1003",
                        "error",
                        "lifecycle",
                        "Answer file validation failed.",
                        operation,
                        validation,
                        options,
                        operationId,
                        exitCode: ValidationError));
                }
                else
                {
                    foreach (var error in validation.Errors)
                    {
                        stderr.WriteLine($"{error.Path}: {error.Message}");
                    }
                }

                return ValidationError;
            }
        }
        else if (operation is "install" or "reapply")
        {
            stderr.WriteLine($"{operation} requires --answer-file <path> so fleet intent is explicit.");
            return ValidationError;
        }
        else if (string.Equals(repairAction, "Install", StringComparison.Ordinal))
        {
            stderr.WriteLine("repair --repair-id Install requires --answer-file <path> so fleet intent is explicit.");
            return ValidationError;
        }

        if (!planVerb && !options.HasFlag("--dry-run"))
        {
            return RunBackendOperation(operation, options, stdout, stderr, validation, repairAction, backendRunner);
        }

        var plan = BuildPlan(operation, options, validation, configPath, repairAction);
        if (options.HasFlag("--ndjson"))
        {
            using var ndjsonLog = CreateNdjsonLogWriter(options, operation, defaultToFleetDirectory: false, stderr);
            if (ndjsonLog?.InitializationFailed == true)
            {
                return UnhandledFailure;
            }

            WriteNdjson(stdout, NdjsonEvent(
                    "LS1001",
                    "info",
                    "lifecycle",
                    "LibreSpot dry-run plan started.",
                    operation,
                    new { plan.Operation, plan.DryRun, plan.Mutates, logPath = ndjsonLog?.Path },
                    options,
                    operationId),
                ndjsonLog);
            foreach (var step in plan.Steps)
            {
                WriteNdjson(stdout, NdjsonEvent(
                        "LS8001",
                        "info",
                        "journal",
                        "Dry-run plan step recorded.",
                        operation,
                        step,
                        options,
                        operationId,
                        target: step.Target),
                    ndjsonLog);
            }

            WriteNdjson(stdout, NdjsonEvent(
                    "LS1002",
                    "success",
                    "lifecycle",
                    "LibreSpot dry-run plan completed.",
                    operation,
                    new { stepCount = plan.Steps.Count, logPath = ndjsonLog?.Path },
                    options,
                    operationId,
                    exitCode: Success),
                ndjsonLog);
        }
        else if (options.HasFlag("--json") || planVerb)
        {
            WriteJson(stdout, plan);
        }
        else
        {
            stdout.WriteLine($"{operation} dry-run plan: {plan.Steps.Count} steps, no changes will be made.");
        }

        return Success;
    }

    private static int RunBackendOperation(
        string operation,
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        ValidationDocument? validation,
        string? repairAction,
        Func<string, string, Action<BackendMessage>, CancellationToken, Task<BackendRunResult>>? backendRunner)
    {
        if ((operation == "uninstall" || operation == "repair") && !options.HasFlag("--yes") && !options.HasFlag("--silent"))
        {
            stderr.WriteLine($"{operation} requires --yes or --silent before changes are made.");
            return ValidationError;
        }

        if (!TryResolveConfigPath(options, out var configPath, out var configPathError))
        {
            stderr.WriteLine(configPathError);
            return ValidationError;
        }

        try
        {
            if (operation is "install" or "reapply")
            {
                PersistAnswerFileConfiguration(validation?.AnswerFile ?? string.Empty, configPath, operation, options);
            }
            else if (operation == "repair" && string.Equals(repairAction, "Install", StringComparison.Ordinal))
            {
                PersistAnswerFileConfiguration(validation?.AnswerFile ?? string.Empty, configPath, operation, options);
            }
            else if (operation == "repair")
            {
                PersistRepairAcknowledgmentConfig(configPath, options);
            }
            else if (operation == "uninstall")
            {
                PersistUninstallAcknowledgmentConfig(configPath);
            }
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Could not prepare LibreSpot config for {operation}: {ex.Message}");
            return UnhandledFailure;
        }

        var operationId = Guid.NewGuid();
        var quiet = options.HasFlag("--quiet") || options.HasFlag("--silent");
        var ndjson = options.HasFlag("--ndjson");
        var actions = BackendActionsFor(operation, options, repairAction);
        var runner = backendRunner ?? ((backendAction, backendConfigPath, onMessage, token) =>
            new BackendScriptService().RunAsync(backendAction, backendConfigPath, onMessage, token));
        using var ndjsonLog = ndjson ? CreateNdjsonLogWriter(options, operation, defaultToFleetDirectory: true, stderr) : null;
        if (ndjsonLog?.InitializationFailed == true)
        {
            return UnhandledFailure;
        }

        if (ndjson)
        {
            WriteNdjson(stdout, NdjsonEvent(
                    "LS1001",
                    "info",
                    "lifecycle",
                    "LibreSpot backend run started.",
                    operation,
                    new { actions, logPath = ndjsonLog?.Path },
                    options,
                    operationId),
                ndjsonLog);
        }
        else if (!quiet)
        {
            stdout.WriteLine($"Starting LibreSpot {operation}...");
        }

        var finalExitCode = Success;
        foreach (var action in actions)
        {
            var result = runner(
                    action,
                    configPath,
                    message => WriteBackendMessage(message, operation, options, operationId, stdout, stderr, quiet, ndjson, ndjsonLog),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (!result.Success)
            {
                var message = result.ErrorMessage ?? $"LibreSpot backend action {action} failed.";
                var exitCode = ResolveBackendExitCode(result);
                if (ndjson)
                {
                    WriteNdjson(stdout, NdjsonEvent(
                            "LS1003",
                            "error",
                            "lifecycle",
                            "LibreSpot backend run failed.",
                            operation,
                            new { action, error = message, logPath = ndjsonLog?.Path },
                            options,
                            operationId,
                            exitCode: exitCode),
                        ndjsonLog);
                }

                stderr.WriteLine(message);
                return exitCode;
            }

            if (result.ExitCode is 3010 or 1641)
            {
                finalExitCode = result.ExitCode.Value;
            }
        }

        if (ndjson)
        {
            WriteNdjson(stdout, NdjsonEvent(
                    "LS1002",
                    "success",
                    "lifecycle",
                    "LibreSpot backend run completed.",
                    operation,
                    new { actionCount = actions.Count, logPath = ndjsonLog?.Path },
                    options,
                    operationId,
                    exitCode: finalExitCode),
                ndjsonLog);
        }
        else if (!quiet)
        {
            stdout.WriteLine($"LibreSpot {operation} completed.");
        }

        return finalExitCode;
    }

    private static IReadOnlyList<string> BackendActionsFor(string operation, CliOptions options, string? repairAction = null) =>
        operation switch
        {
            "install" => new[] { "Install" },
            "reapply" => new[] { "Reapply" },
            "repair" when repairAction is not null => new[] { repairAction },
            "uninstall" when options.HasFlag("--purge") => new[] { "UninstallSpicetify", "RemoveSelfData" },
            "uninstall" => new[] { "UninstallSpicetify" },
            _ => throw new InvalidOperationException($"No backend action mapping exists for {operation}.")
        };

    private static string? ResolveRepairAction(string? repairId, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(repairId))
        {
            error = "repair requires --repair-id <id>.";
            return null;
        }

        var normalized = repairId.Trim();
        var action = normalized.Equals("WatchAutoReapply", StringComparison.OrdinalIgnoreCase)
            ? "EnableAutoReapply"
            : KnownRepairActions.FirstOrDefault(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (action is not null)
        {
            return action;
        }

        error = normalized.Equals("OpenLogs", StringComparison.OrdinalIgnoreCase)
            ? "repair --repair-id OpenLogs is informational only; use export-support or inspect the log directory."
            : $"Unsupported repair id '{repairId}'.";
        return null;
    }

    private static void WriteBackendMessage(
        BackendMessage message,
        string operation,
        CliOptions options,
        Guid operationId,
        TextWriter stdout,
        TextWriter stderr,
        bool quiet,
        bool ndjson,
        NdjsonLogWriter? ndjsonLog)
    {
        var level = NormalizeLogLevel(message.Level);
        if (ndjson)
        {
            WriteNdjson(stdout, NdjsonEvent(
                    "LS9001",
                    level,
                    "backend",
                    message.Payload,
                    operation,
                    new { message.Kind, message.Level, message.Payload },
                    options,
                    operationId),
                ndjsonLog);
            return;
        }

        if (!quiet && message.Kind is "status" or "step")
        {
            stdout.WriteLine(message.Payload);
        }

        if (message.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase) ||
            message.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine(message.Payload);
        }
    }

    private static string NormalizeLogLevel(string level) =>
        level.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
            ? "success"
            : level.Equals("WARN", StringComparison.OrdinalIgnoreCase)
                ? "warn"
                : level.Equals("ERROR", StringComparison.OrdinalIgnoreCase)
                    ? "error"
                    : "info";

    private static void PersistAnswerFileConfiguration(string answerFile, string configPath, string operation, CliOptions options)
    {
        if (string.IsNullOrWhiteSpace(answerFile))
        {
            throw new InvalidOperationException("Answer file path was not provided.");
        }

        using var stream = File.OpenRead(answerFile);
        EnsureFileSize(stream, MaxAnswerFileBytes, "Answer file");
        using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = doc.RootElement;
        var settings = ResolveProfileSettings(root, options.GetValue("--profile"));
        var config = AppCatalog.CreateRecommendedConfiguration();
        ApplyAnswerSettings(root, config);
        if (settings.HasValue)
        {
            ApplyAnswerSettings(settings.Value, config);
        }

        var installMode = settings.HasValue
            ? GetString(settings.Value, "installMode") ?? GetString(root, "installMode")
            : GetString(root, "installMode");
        config.Mode = string.Equals(installMode, "custom", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(installMode, "reapply", StringComparison.OrdinalIgnoreCase)
            ? "Custom"
            : "Easy";
        config.CleanInstall = operation == "install" && !string.Equals(installMode, "reapply", StringComparison.OrdinalIgnoreCase);
        config.LaunchAfter = !options.HasFlag("--no-restart");
        config.RiskAcknowledged = true;
        WriteConfiguration(configPath, config);
    }

    private static void ApplyAnswerSettings(JsonElement root, InstallConfiguration config)
    {
        SetString(root, "uiCulture", value => config.UiCulture = value);
        ApplySpotifyTarget(root, config);
        ApplySpotXOptions(root, config);
        ApplySpicetifyOptions(root, config);
        ApplyWatcherOptions(root, config);
    }

    private static void PersistUninstallAcknowledgmentConfig(string configPath)
    {
        var config = ReadConfigurationOrDefault(configPath);
        config.RiskAcknowledged = true;
        config.LaunchAfter = false;
        WriteConfiguration(configPath, config);
    }

    private static void PersistRepairAcknowledgmentConfig(string configPath, CliOptions options)
    {
        var config = ReadConfigurationOrDefault(configPath);
        config.RiskAcknowledged = true;
        config.LaunchAfter = !options.HasFlag("--no-restart");
        WriteConfiguration(configPath, config);
    }

    private static InstallConfiguration ReadConfigurationOrDefault(string configPath)
    {
        try
        {
            if (File.Exists(configPath))
            {
                using var stream = File.OpenRead(configPath);
                EnsureFileSize(stream, MaxConfigurationBytes, "Configuration file");
                var config = JsonSerializer.Deserialize<InstallConfiguration>(stream, ConfigurationJsonOptions);
                if (config is not null)
                {
                    return AppCatalog.NormalizeConfiguration(config);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: could not read config at {configPath}: {ex.Message}. Using defaults.");
        }

        return AppCatalog.CreateRecommendedConfiguration();
    }

    private static void WriteConfiguration(string configPath, InstallConfiguration config)
    {
        var fullPath = Path.GetFullPath(configPath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Configuration path has no directory.");
        Directory.CreateDirectory(directory);
        var normalized = AppCatalog.NormalizeConfiguration(config);
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, normalized, ConfigurationJsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    private static void ApplySpotifyTarget(JsonElement root, InstallConfiguration config)
    {
        if (!root.TryGetProperty("spotifyTarget", out var target) || target.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var version = GetString(target, "version");
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        var match = AppCatalog.SpotifyVersionManifest.FirstOrDefault(entry =>
            entry.Id.Equals(version, StringComparison.OrdinalIgnoreCase) ||
            entry.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        config.SpotX_SpotifyVersionId = match?.Id ?? version;
    }

    private static void ApplySpotXOptions(JsonElement root, InstallConfiguration config)
    {
        if (!root.TryGetProperty("spotx", out var spotx) || spotx.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        SetBool(spotx, "newTheme", value => config.SpotX_NewTheme = value);
        SetBool(spotx, "podcastsOff", value => config.SpotX_PodcastsOff = value);
        SetBool(spotx, "blockUpdate", value => config.SpotX_BlockUpdate = value);
        SetBool(spotx, "adSectionsOff", value => config.SpotX_AdSectionsOff = value);
        SetBool(spotx, "premium", value => config.SpotX_Premium = value);
        SetBool(spotx, "lyricsEnabled", value => config.SpotX_LyricsEnabled = value);
        SetBool(spotx, "topSearch", value => config.SpotX_TopSearch = value);
        SetBool(spotx, "rightSidebarOff", value => config.SpotX_RightSidebarOff = value);
        SetBool(spotx, "rightSidebarClr", value => config.SpotX_RightSidebarClr = value);
        SetBool(spotx, "canvasHomeOff", value => config.SpotX_CanvasHomeOff = value);
        SetBool(spotx, "homeSubOff", value => config.SpotX_HomeSubOff = value);
        SetBool(spotx, "disableStartup", value => config.SpotX_DisableStartup = value);
        SetBool(spotx, "noShortcut", value => config.SpotX_NoShortcut = value);
        SetBool(spotx, "plus", value => config.SpotX_Plus = value);
        SetBool(spotx, "newFullscreen", value => config.SpotX_NewFullscreen = value);
        SetBool(spotx, "funnyProgress", value => config.SpotX_FunnyProgress = value);
        SetBool(spotx, "expSpotify", value => config.SpotX_ExpSpotify = value);
        SetBool(spotx, "lyricsBlock", value => config.SpotX_LyricsBlock = value);
        SetBool(spotx, "oldLyrics", value => config.SpotX_OldLyrics = value);
        SetBool(spotx, "hideColIconOff", value => config.SpotX_HideColIconOff = value);
        SetBool(spotx, "sendVersionOff", value => config.SpotX_SendVersionOff = value);
        SetBool(spotx, "startSpoti", value => config.SpotX_StartSpoti = value);
        SetBool(spotx, "devTools", value => config.SpotX_DevTools = value);
        SetBool(spotx, "mirror", value => config.SpotX_Mirror = value);
        SetBool(spotx, "confirmUninstall", value => config.SpotX_ConfirmUninstall = value);
        SetInt(spotx, "cacheLimit", value => config.SpotX_CacheLimit = value);
        SetString(spotx, "lyricsTheme", value => config.SpotX_LyricsTheme = value);
        SetString(spotx, "downloadMethod", value => config.SpotX_DownloadMethod = value);
        SetString(spotx, "language", value => config.SpotX_Language = value);
        SetBool(spotx, "customPatchesEnabled", value => config.SpotX_CustomPatchesEnabled = value);
        SetString(spotx, "customPatchesJson", value => config.SpotX_CustomPatchesJson = value);
    }

    private static void ApplySpicetifyOptions(JsonElement root, InstallConfiguration config)
    {
        if (!root.TryGetProperty("spicetify", out var spicetify) || spicetify.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        SetString(spicetify, "theme", value => config.Spicetify_Theme = value);
        SetString(spicetify, "scheme", value => config.Spicetify_Scheme = value);
        SetBool(spicetify, "marketplace", value => config.Spicetify_Marketplace = value);
        if (spicetify.TryGetProperty("extensions", out var extensions) && extensions.ValueKind == JsonValueKind.Array)
        {
            config.Spicetify_Extensions = extensions
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        if (spicetify.TryGetProperty("customApps", out var customApps) && customApps.ValueKind == JsonValueKind.Array)
        {
            config.Spicetify_CustomApps = customApps
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }
    }

    private static void ApplyWatcherOptions(JsonElement root, InstallConfiguration config)
    {
        if (!root.TryGetProperty("watcher", out var watcher) || watcher.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        SetBool(watcher, "enabled", value => config.AutoReapply_Enabled = value);
    }

    private static string? GetString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void SetString(JsonElement root, string property, Action<string> setter)
    {
        var value = GetString(root, property);
        if (value is not null)
        {
            setter(value);
        }
    }

    private static void SetBool(JsonElement root, string property, Action<bool> setter)
    {
        if (root.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            setter(value.GetBoolean());
        }
    }

    private static void SetInt(JsonElement root, string property, Action<int> setter)
    {
        if (root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            setter(number);
        }
    }

    private static VersionDocument BuildVersionDocument() =>
        new(
            1,
            ProductVersion,
            typeof(CliApplication).Assembly.GetName().Version?.ToString() ?? "unknown",
            DateTimeOffset.UtcNow,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.RuntimeIdentifier,
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.OSDescription,
            new DependencyPinsDocument(
                new SpotXPinDocument(
                    AppCatalog.PinnedSpotXVersion,
                    AppCatalog.PinnedSpotXCommit,
                    AppCatalog.PinnedSpotXSpotifyVersionId,
                    AppCatalog.PinnedSpotXSpotifyVersion),
                new SpicetifyPinDocument(
                    AppCatalog.PinnedSpicetifyCliVersion,
                    AppCatalog.SpicetifyWindowsMinTestedSpotify,
                    AppCatalog.SpicetifyWindowsMaxTestedSpotify),
                AppCatalog.PinnedMarketplaceVersion,
                AppCatalog.PinnedThemesCommit));

    private static StatusDocument BuildStatusDocument(EnvironmentSnapshot snapshot, string configPath) =>
        new(
            2,
            ProductVersion,
            DateTimeOffset.UtcNow,
            configPath,
            snapshot.HostArchitecture,
            snapshot.ProcessArchitecture,
            snapshot.HealthReport.StatusTitle,
            snapshot.HealthReport.StatusDetail,
            snapshot.HealthReport.IssueSummary,
            snapshot.SpotifyInstalled,
            snapshot.SpicetifyInstalled,
            snapshot.MarketplaceReady,
            snapshot.MarketplaceLikelyVisible,
            snapshot.MarketplaceVisibilityEvidence is null ? null : MarketplaceVisibilityDocument.From(snapshot.MarketplaceVisibilityEvidence),
            AssetCacheDocument.From(snapshot.AssetCacheInventory),
            snapshot.AutoReapplyTaskRegistered,
            BackupCount(snapshot),
            ComponentLastChanged(snapshot, "post-spotify-update"),
            ComponentStatus(snapshot, "auto-reapply-watcher"),
            IssueIds(snapshot),
            RecommendedRepairIds(snapshot),
            snapshot.CommunityAssetDriftReport.Assets.Select(CommunityAssetDocument.From).ToArray(),
            snapshot.UpstreamDriftReport.Dependencies.Select(UpstreamDependencyDocument.From).ToArray(),
            snapshot.HealthReport.Components.Select(ComponentDocument.From).ToArray());

    private static DetectionDocument BuildDetectionDocument(EnvironmentSnapshot snapshot, string configPath)
    {
        var issueIds = IssueIds(snapshot);
        var recommendedRepairIds = RecommendedRepairIds(snapshot);

        var (state, exitCode) =
            snapshot.HealthReport.Components.Count == 0
                ? ("unknown", UnhandledFailure)
                : !snapshot.SpotifyInstalled && !snapshot.SpicetifyInstalled
                ? ("notInstalled", NotInstalled)
                : HasBlockingUserState(snapshot)
                    ? ("blocked", 20)
                : snapshot.HealthReport.HasCriticalIssues
                    ? ("needsRepair", RepairNeeded)
                    : snapshot.SpotifyInstalled != snapshot.SpicetifyInstalled
                        ? ("partial", DriftDetected)
                        : snapshot.HealthReport.HasWarningIssues
                            ? ("drifted", DriftDetected)
                            : ("compliant", Success);

        return new DetectionDocument(
            1,
            ProductVersion,
            DateTimeOffset.UtcNow,
            configPath,
            state,
            exitCode,
            snapshot.HealthReport.IssueSummary,
            issueIds,
            recommendedRepairIds);
    }

    private static IReadOnlyList<string> IssueIds(EnvironmentSnapshot snapshot) =>
        snapshot.HealthReport.CriticalIssues
            .Concat(snapshot.HealthReport.WarningIssues)
            .Select(component => component.Id)
            .ToArray();

    private static IReadOnlyList<string> RecommendedRepairIds(EnvironmentSnapshot snapshot) =>
        snapshot.HealthReport.Components
            .SelectMany(component => component.RecommendedActionIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool HasBlockingUserState(EnvironmentSnapshot snapshot) =>
        snapshot.HealthReport.WarningIssues.Any(component =>
            string.Equals(component.Id, "post-spotify-update", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(component.Status, "Close Spotify first", StringComparison.OrdinalIgnoreCase));

    private static int BackupCount(EnvironmentSnapshot snapshot)
    {
        var backupStatus = ComponentStatus(snapshot, "backups");
        if (string.IsNullOrWhiteSpace(backupStatus))
        {
            return 0;
        }

        var firstToken = backupStatus.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(firstToken, out var count) ? count : 0;
    }

    private static DateTimeOffset? ComponentLastChanged(EnvironmentSnapshot snapshot, string id)
    {
        var value = snapshot.HealthReport.Components
            .FirstOrDefault(component => string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase))
            ?.LastChanged;
        return value.HasValue ? new DateTimeOffset(value.Value.ToUniversalTime()) : null;
    }

    private static string? ComponentStatus(EnvironmentSnapshot snapshot, string id) =>
        snapshot.HealthReport.Components
            .FirstOrDefault(component => string.Equals(component.Id, id, StringComparison.OrdinalIgnoreCase))
            ?.Status;

    private static ValidationDocument ValidateAnswerFile(string answerFile, string? profile = null)
    {
        var errors = new List<ValidationErrorDocument>();
        var fullPath = Path.GetFullPath(answerFile);

        if (!File.Exists(fullPath))
        {
            errors.Add(new ValidationErrorDocument("$", $"Answer file not found: {fullPath}"));
            return new ValidationDocument(1, fullPath, false, errors);
        }

        try
        {
            using var stream = File.OpenRead(fullPath);
            EnsureFileSize(stream, MaxAnswerFileBytes, "Answer file");
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ValidationErrorDocument("$", "Answer file root must be a JSON object."));
                return new ValidationDocument(1, fullPath, false, errors);
            }

            var root = doc.RootElement;
            RequireSchemaVersion(root, errors);
            RequireBooleanTrue(root, "eulaAccepted", errors);
            RequireBooleanTrue(root, "riskAcknowledged", errors);
            ValidateAnswerSettings(root, "$", errors);
            ValidateProfiles(root, errors);
            var settings = ResolveProfileSettings(root, profile, errors);
            ValidateCustomPatches(root, settings, profile, errors);
        }
        catch (JsonException ex)
        {
            errors.Add(new ValidationErrorDocument("$", $"Invalid JSON: {ex.Message}"));
        }
        catch (IOException ex)
        {
            errors.Add(new ValidationErrorDocument("$", $"Could not read answer file: {ex.Message}"));
        }
        catch (UnauthorizedAccessException ex)
        {
            errors.Add(new ValidationErrorDocument("$", $"Could not read answer file: {ex.Message}"));
        }

        return new ValidationDocument(1, fullPath, errors.Count == 0, errors);
    }

    private static void EnsureFileSize(FileStream stream, int maxBytes, string label)
    {
        if (stream.Length > maxBytes)
        {
            throw new IOException($"{label} is {stream.Length} bytes; the maximum is {maxBytes} bytes.");
        }
    }

    private static JsonElement? ResolveProfileSettings(JsonElement root, string? profile)
    {
        var errors = new List<ValidationErrorDocument>();
        var settings = ResolveProfileSettings(root, profile, errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(errors[0].Message);
        }

        return settings;
    }

    private static JsonElement? ResolveProfileSettings(JsonElement root, string? profile, ICollection<ValidationErrorDocument> errors)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return null;
        }

        if (!root.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new ValidationErrorDocument("$.profiles", $"Profile '{profile}' was requested, but the answer file does not contain a profiles object."));
            return null;
        }

        if (!profiles.TryGetProperty(profile, out var selected))
        {
            errors.Add(new ValidationErrorDocument($"$.profiles.{profile}", $"Profile '{profile}' was not found in the answer file."));
            return null;
        }

        if (selected.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new ValidationErrorDocument($"$.profiles.{profile}", $"Profile '{profile}' must be a JSON object."));
            return null;
        }

        return selected;
    }

    private static PlanDocument BuildPlan(
        string operation,
        CliOptions options,
        ValidationDocument? validation,
        string configPath,
        string? repairAction = null)
    {
        var answerFile = validation?.AnswerFile;
        var steps = new List<PlanStepDocument>();

        if (!string.IsNullOrWhiteSpace(answerFile))
        {
            steps.Add(new PlanStepDocument(
                "validate-answer-file",
                "Validate answer file",
                false,
                false,
                answerFile,
                "The answer file schema version and consent fields were validated before planning."));
        }

        steps.Add(new PlanStepDocument(
            "read-health-report",
            "Read local health report",
            false,
            false,
            configPath,
            "The same environment snapshot used by the WPF dashboard informs fleet planning."));

        if (operation is "install" or "reapply")
        {
            steps.Add(new PlanStepDocument(
                "verify-compatibility",
                "Check pinned compatibility",
                false,
                false,
                "SpotX, Spicetify CLI, Marketplace, and themes pins",
                "The backend compatibility matrix will be checked before downloads or patching."));
            steps.Add(new PlanStepDocument(
                "run-backend-plan",
                "Prepare backend operation",
                true,
                false,
                operation,
                "The GUI backend owns mutation ordering; this dry-run stops before invoking it."));
        }
        else if (operation == "uninstall")
        {
            steps.Add(new PlanStepDocument(
                "plan-uninstall",
                "Plan uninstall cleanup",
                true,
                false,
                options.HasFlag("--purge") ? "purge" : "keep LibreSpot data",
                "Dry-run reports the intended cleanup posture without removing Spotify, Spicetify, or LibreSpot data."));
        }
        else if (operation == "repair")
        {
            steps.Add(new PlanStepDocument(
                "plan-repair",
                "Resolve repair action",
                RepairRequiresAdmin(repairAction),
                false,
                repairAction ?? options.GetValue("--repair-id") ?? "unknown",
                "Dry-run reports the backend action that would run for this repair ID without changing local state."));
        }

        return new PlanDocument(
            1,
            ProductVersion,
            DateTimeOffset.UtcNow,
            operation,
            true,
            false,
            options.GetValue("--correlation-id"),
            answerFile,
            options.GetValue("--profile"),
            steps);
    }

    private static bool RepairRequiresAdmin(string? repairAction) =>
        repairAction is "Install" or "Reapply" or "RepairMarketplace" or "SafeMode" or "RestoreBackup" or "RestoreVanilla" or "UninstallSpicetify" or "FullReset";

    private static void RequireSchemaVersion(JsonElement root, ICollection<ValidationErrorDocument> errors)
    {
        if (!root.TryGetProperty("schemaVersion", out var value))
        {
            errors.Add(new ValidationErrorDocument("$.schemaVersion", "schemaVersion is required."));
            return;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var version) || version != 1)
        {
            errors.Add(new ValidationErrorDocument("$.schemaVersion", "schemaVersion must be 1."));
        }
    }

    private static void RequireBooleanTrue(JsonElement root, string property, ICollection<ValidationErrorDocument> errors)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            errors.Add(new ValidationErrorDocument($"$.{property}", $"{property} is required."));
            return;
        }

        if (value.ValueKind is not JsonValueKind.True)
        {
            errors.Add(new ValidationErrorDocument($"$.{property}", $"{property} must be true."));
        }
    }

    private static void ValidateAnswerSettings(JsonElement root, string pathPrefix, ICollection<ValidationErrorDocument> errors)
    {
        ValidateInstallMode(root, pathPrefix, errors);
        ValidateStringEnum(root, "uiCulture", SupportedCultures, pathPrefix, errors);

        if (TryGetObject(root, "spotifyTarget", pathPrefix, errors, out var spotifyTarget))
        {
            ValidateKnownProperties(spotifyTarget, ChildPath(pathPrefix, "spotifyTarget"), new[] { "version", "architecture" }, errors);
            ValidateStringProperty(spotifyTarget, "version", ChildPath(pathPrefix, "spotifyTarget"), errors);
            ValidateStringEnum(spotifyTarget, "architecture", new HashSet<string>(StringComparer.Ordinal) { "x64", "x86" }, ChildPath(pathPrefix, "spotifyTarget"), errors);
        }

        if (TryGetObject(root, "spotx", pathPrefix, errors, out var spotx))
        {
            var spotxPath = ChildPath(pathPrefix, "spotx");
            var booleans = new[]
            {
                "newTheme", "podcastsOff", "blockUpdate", "adSectionsOff", "premium", "lyricsEnabled",
                "topSearch", "rightSidebarOff", "rightSidebarClr", "canvasHomeOff", "homeSubOff",
                "disableStartup", "noShortcut", "plus", "newFullscreen", "funnyProgress", "expSpotify",
                "lyricsBlock", "oldLyrics", "hideColIconOff", "sendVersionOff", "startSpoti", "devTools",
                "mirror", "confirmUninstall", "customPatchesEnabled"
            };
            ValidateKnownProperties(spotx, spotxPath, booleans.Concat(new[] { "cacheLimit", "lyricsTheme", "downloadMethod", "language", "customPatchesJson" }), errors);
            foreach (var property in booleans)
            {
                ValidateBooleanProperty(spotx, property, spotxPath, errors);
            }

            ValidateIntegerRange(spotx, "cacheLimit", 0, 50000, spotxPath, errors);
            ValidateStringEnum(spotx, "lyricsTheme", SpotXLyricsThemes, spotxPath, errors);
            ValidateStringEnum(spotx, "downloadMethod", SpotXDownloadMethods, spotxPath, errors);
            ValidateStringProperty(spotx, "language", spotxPath, errors);
            ValidateStringProperty(spotx, "customPatchesJson", spotxPath, errors);
        }

        if (TryGetObject(root, "spicetify", pathPrefix, errors, out var spicetify))
        {
            var spicetifyPath = ChildPath(pathPrefix, "spicetify");
            ValidateKnownProperties(spicetify, spicetifyPath, new[] { "theme", "scheme", "extensions", "customApps", "marketplace" }, errors);
            ValidateStringProperty(spicetify, "theme", spicetifyPath, errors);
            ValidateStringProperty(spicetify, "scheme", spicetifyPath, errors);
            ValidateStringArray(spicetify, "extensions", SpicetifyExtensions, spicetifyPath, errors);
            ValidateStringArray(spicetify, "customApps", SpicetifyCustomApps, spicetifyPath, errors);
            ValidateBooleanProperty(spicetify, "marketplace", spicetifyPath, errors);
        }

        if (TryGetObject(root, "watcher", pathPrefix, errors, out var watcher))
        {
            var watcherPath = ChildPath(pathPrefix, "watcher");
            ValidateKnownProperties(watcher, watcherPath, new[] { "enabled", "intervalMinutes" }, errors);
            ValidateBooleanProperty(watcher, "enabled", watcherPath, errors);
            ValidateIntegerRange(watcher, "intervalMinutes", 1, int.MaxValue, watcherPath, errors);
        }

        ValidateStringEnum(root, "repairPolicy", new HashSet<string>(StringComparer.Ordinal) { "skip", "warn", "auto" }, pathPrefix, errors);
        ValidateStringEnum(root, "rebootPolicy", new HashSet<string>(StringComparer.Ordinal) { "never", "ifNeeded", "always" }, pathPrefix, errors);

        if (TryGetObject(root, "logging", pathPrefix, errors, out var logging))
        {
            var loggingPath = ChildPath(pathPrefix, "logging");
            ValidateKnownProperties(logging, loggingPath, new[] { "level", "directory", "ndjson" }, errors);
            ValidateStringEnum(logging, "level", new HashSet<string>(StringComparer.Ordinal) { "silent", "error", "warn", "info", "debug", "trace" }, loggingPath, errors);
            ValidateStringProperty(logging, "directory", loggingPath, errors);
            ValidateBooleanProperty(logging, "ndjson", loggingPath, errors);
        }
    }

    private static void ValidateProfiles(JsonElement root, ICollection<ValidationErrorDocument> errors)
    {
        if (!root.TryGetProperty("profiles", out var profiles))
        {
            return;
        }

        if (profiles.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new ValidationErrorDocument("$.profiles", "profiles must be a JSON object."));
            return;
        }

        foreach (var profile in profiles.EnumerateObject())
        {
            var profilePath = ProfilePath(profile.Name, string.Empty);
            if (profile.Value.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ValidationErrorDocument(profilePath, $"Profile '{profile.Name}' must be a JSON object."));
                continue;
            }

            ValidateKnownProperties(
                profile.Value,
                profilePath,
                new[] { "installMode", "spotifyTarget", "spotx", "spicetify", "watcher", "repairPolicy", "logging", "rebootPolicy" },
                errors);
            ValidateAnswerSettings(profile.Value, profilePath, errors);
        }
    }

    private static void ValidateInstallMode(JsonElement root, string pathPrefix, ICollection<ValidationErrorDocument> errors)
    {
        if (!root.TryGetProperty("installMode", out var value))
        {
            return;
        }

        var path = ChildPath(pathPrefix, "installMode");
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationErrorDocument(path, "installMode must be a string."));
            return;
        }

        var mode = value.GetString();
        if (mode is not ("recommended" or "custom" or "reapply"))
        {
            errors.Add(new ValidationErrorDocument(path, "installMode must be recommended, custom, or reapply."));
        }
    }

    private static bool TryGetObject(
        JsonElement root,
        string property,
        string pathPrefix,
        ICollection<ValidationErrorDocument> errors,
        out JsonElement value)
    {
        value = default;
        if (!root.TryGetProperty(property, out var candidate))
        {
            return false;
        }

        if (candidate.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new ValidationErrorDocument(ChildPath(pathPrefix, property), $"{property} must be a JSON object."));
            return false;
        }

        value = candidate;
        return true;
    }

    private static void ValidateKnownProperties(
        JsonElement root,
        string pathPrefix,
        IEnumerable<string> allowedProperties,
        ICollection<ValidationErrorDocument> errors)
    {
        var allowed = allowedProperties.ToHashSet(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                errors.Add(new ValidationErrorDocument(ChildPath(pathPrefix, property.Name), $"{property.Name} is not a supported answer-file property."));
            }
        }
    }

    private static void ValidateStringProperty(JsonElement root, string property, string pathPrefix, ICollection<ValidationErrorDocument> errors)
    {
        if (root.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationErrorDocument(ChildPath(pathPrefix, property), $"{property} must be a string."));
        }
    }

    private static void ValidateStringEnum(
        JsonElement root,
        string property,
        IReadOnlySet<string> allowedValues,
        string pathPrefix,
        ICollection<ValidationErrorDocument> errors)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            return;
        }

        var path = ChildPath(pathPrefix, property);
        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationErrorDocument(path, $"{property} must be a string."));
            return;
        }

        var actual = value.GetString() ?? string.Empty;
        if (!allowedValues.Contains(actual))
        {
            errors.Add(new ValidationErrorDocument(path, $"{property} has unsupported value '{actual}'."));
        }
    }

    private static void ValidateBooleanProperty(JsonElement root, string property, string pathPrefix, ICollection<ValidationErrorDocument> errors)
    {
        if (root.TryGetProperty(property, out var value) && value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            errors.Add(new ValidationErrorDocument(ChildPath(pathPrefix, property), $"{property} must be true or false."));
        }
    }

    private static void ValidateIntegerRange(
        JsonElement root,
        string property,
        int minimum,
        int maximum,
        string pathPrefix,
        ICollection<ValidationErrorDocument> errors)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            return;
        }

        var path = ChildPath(pathPrefix, property);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            errors.Add(new ValidationErrorDocument(path, $"{property} must be an integer."));
            return;
        }

        if (number < minimum || number > maximum)
        {
            errors.Add(new ValidationErrorDocument(path, $"{property} must be between {minimum} and {maximum}."));
        }
    }

    private static void ValidateStringArray(
        JsonElement root,
        string property,
        IReadOnlySet<string> allowedValues,
        string pathPrefix,
        ICollection<ValidationErrorDocument> errors)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            return;
        }

        var path = ChildPath(pathPrefix, property);
        if (value.ValueKind != JsonValueKind.Array)
        {
            errors.Add(new ValidationErrorDocument(path, $"{property} must be an array."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemPath = $"{path}[{index}]";
            if (item.ValueKind != JsonValueKind.String)
            {
                errors.Add(new ValidationErrorDocument(itemPath, $"{property} entries must be strings."));
            }
            else
            {
                var actual = item.GetString() ?? string.Empty;
                if (!allowedValues.Contains(actual))
                {
                    errors.Add(new ValidationErrorDocument(itemPath, $"{property} entry '{actual}' is not supported."));
                }
                else if (!seen.Add(actual))
                {
                    errors.Add(new ValidationErrorDocument(itemPath, $"{property} entries must be unique."));
                }
            }

            index++;
        }
    }

    private static string ChildPath(string pathPrefix, string property) =>
        pathPrefix == "$" ? $"$.{property}" : $"{pathPrefix}.{property}";

    private static void ValidateCustomPatches(JsonElement root, JsonElement? profileSettings, string? profile, ICollection<ValidationErrorDocument> errors)
    {
        var enabled = TryGetSpotXBoolean(root, "customPatchesEnabled");
        var patches = TryGetSpotXString(root, "customPatchesJson", "$.spotx.customPatchesJson");

        if (profileSettings.HasValue)
        {
            enabled = TryGetSpotXBoolean(profileSettings.Value, "customPatchesEnabled") ?? enabled;
            patches = TryGetSpotXString(profileSettings.Value, "customPatchesJson", ProfilePath(profile, "spotx.customPatchesJson")) ?? patches;
        }

        if (enabled is not true)
        {
            return;
        }

        var path = patches?.Path ?? (profileSettings.HasValue ? ProfilePath(profile, "spotx.customPatchesJson") : "$.spotx.customPatchesJson");
        var json = patches?.Value;
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add(new ValidationErrorDocument(path, "customPatchesJson is required when customPatchesEnabled is true."));
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > MaxCustomPatchesJsonBytes)
        {
            errors.Add(new ValidationErrorDocument(path, $"customPatchesJson is {byteCount} bytes; the maximum is {MaxCustomPatchesJsonBytes} bytes."));
            return;
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            errors.Add(new ValidationErrorDocument(path, $"customPatchesJson is not valid JSON: {ex.Message}"));
        }
    }

    private static bool? TryGetSpotXBoolean(JsonElement root, string property)
    {
        if (!root.TryGetProperty("spotx", out var spotx) ||
            spotx.ValueKind != JsonValueKind.Object ||
            !spotx.TryGetProperty(property, out var value) ||
            value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            return null;
        }

        return value.GetBoolean();
    }

    private static AnswerStringValue? TryGetSpotXString(JsonElement root, string property, string path)
    {
        if (!root.TryGetProperty("spotx", out var spotx) ||
            spotx.ValueKind != JsonValueKind.Object ||
            !spotx.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return new AnswerStringValue(value.GetString() ?? string.Empty, path);
    }

    private static string ProfilePath(string? profile, string suffix)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return string.IsNullOrWhiteSpace(suffix) ? "$" : $"$.{suffix}";
        }

        var root = $"$.profiles.{profile}";
        return string.IsNullOrWhiteSpace(suffix) ? root : $"{root}.{suffix}";
    }

    private sealed record AnswerStringValue(string Value, string Path);

    private static EnvironmentSnapshot GetSnapshot(string configPath, Func<string, EnvironmentSnapshot>? snapshotFactory) =>
        snapshotFactory is not null
            ? snapshotFactory(configPath)
            : new EnvironmentSnapshotService(
                upstreamDriftProbe: () => UpstreamDriftService.Default.GetReport(),
                communityAssetDriftProbe: () => CommunityAssetDriftService.Default.GetReport(),
                antivirusProbe: EnvironmentSnapshotService.QueryDefenderExclusionStatus,
                storeSpotifyProbe: EnvironmentSnapshotService.QueryStoreSpotifyPresent).GetSnapshot(configPath);

    private static ParseResult Parse(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            var normalizedArg = NormalizeFlagAlias(arg);
            if (!normalizedArg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            var name = normalizedArg;
            string? value = null;
            var equalsIndex = normalizedArg.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex > 0)
            {
                name = normalizedArg[..equalsIndex];
                value = normalizedArg[(equalsIndex + 1)..];
            }
            else if (ValueFlags.Contains(name))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return new ParseResult(null, $"{name} requires a value.");
                }

                value = args[++i];
            }

            options[name] = value;
        }

        if (positional.Count > 0)
        {
            return new ParseResult(null, $"Unexpected positional argument: {positional[0]}");
        }

        if (options.ContainsKey("--json") && options.ContainsKey("--ndjson"))
        {
            return new ParseResult(null, "--json and --ndjson cannot be used together.");
        }

        return new ParseResult(new CliOptions(options), null);
    }

    private static string NormalizeFlagAlias(string arg) =>
        arg switch
        {
            "/S" or "/s" or "-s" => "--silent",
            "-q" => "--quiet",
            "-y" => "--yes",
            "-o" => "--output",
            _ => arg
        };

    private static void WriteJson<T>(TextWriter stdout, T value) =>
        stdout.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private static void WriteNdjson<T>(TextWriter stdout, T value, NdjsonLogWriter? logWriter)
    {
        var line = JsonSerializer.Serialize(value, JsonOptions);
        stdout.WriteLine(line);
        logWriter?.WriteLine(line);
    }

    private static NdjsonLogWriter? CreateNdjsonLogWriter(
        CliOptions options,
        string operation,
        bool defaultToFleetDirectory,
        TextWriter stderr)
    {
        var logDirectory = options.GetValue("--log-dir");
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            if (!defaultToFleetDirectory)
            {
                return null;
            }

            logDirectory = DefaultFleetLogDirectory;
        }

        try
        {
            return NdjsonLogWriter.Create(logDirectory, operation);
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Could not prepare NDJSON log directory: {ex.Message}");
            return NdjsonLogWriter.Failed;
        }
    }

    private static NdjsonLogLine NdjsonEvent(
        string eventId,
        string level,
        string component,
        string message,
        string verb,
        object payload,
        CliOptions options,
        Guid operationId,
        string? target = null,
        int? exitCode = null) =>
        new(
            1,
            eventId,
            DateTimeOffset.UtcNow,
            level,
            verb,
            operationId,
            options.GetValue("--correlation-id"),
            component,
            target,
            message,
            payload,
            exitCode);

    private static int UnknownVerb(string verb, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown LibreSpot CLI verb: {verb}");
        WriteUsage(stderr);
        return ValidationError;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  LibreSpot.Cli --version");
        writer.WriteLine("  LibreSpot.Cli status [--json] [--config-path <path>]");
        writer.WriteLine("  LibreSpot.Cli detect [--json|--intune] [--config-path <path>]");
        writer.WriteLine("  LibreSpot.Cli validate --answer-file <path> [--json]");
        writer.WriteLine("  LibreSpot.Cli install [--dry-run] --answer-file <path> [--ndjson]");
        writer.WriteLine("  LibreSpot.Cli reapply [--dry-run] --answer-file <path> [--ndjson]");
        writer.WriteLine("  LibreSpot.Cli uninstall [--dry-run] [--keep-spotify] [--ndjson]");
        writer.WriteLine("  LibreSpot.Cli repair --repair-id <id> [--dry-run] [--ndjson]");
        writer.WriteLine("  LibreSpot.Cli plan --answer-file <path> [--json]");
        writer.WriteLine("  LibreSpot.Cli export-support [--output <path>]");
        writer.WriteLine("  LibreSpot.Cli watcher install [--silent]");
        writer.WriteLine("  LibreSpot.Cli watcher remove [--silent]");
        writer.WriteLine("Common flags: --silent --yes --keep-spotify --log-dir <path>");
    }

    private static bool IsHelp(string arg) =>
        string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase);

    private static bool IsVersion(string arg) =>
        string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "version", StringComparison.OrdinalIgnoreCase);

    private static string ProductVersion =>
        typeof(CliApplication).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(CliApplication).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    private static bool TryResolveConfigPath(CliOptions options, out string configPath, out string? error)
    {
        var explicitPath = options.GetValue("--config-path");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            configPath = explicitPath;
            error = null;
            return true;
        }

        var scope = options.GetValue("--scope");
        if (string.IsNullOrWhiteSpace(scope) || scope.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            configPath = DefaultConfigPath;
            error = null;
            return true;
        }

        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase))
        {
            configPath = MachineConfigPath;
            error = null;
            return true;
        }

        configPath = string.Empty;
        error = "--scope must be user or machine.";
        return false;
    }

    private static int ResolveBackendExitCode(BackendRunResult result)
    {
        if (result.Canceled)
        {
            return 60;
        }

        if (result.ExitCode is { } exitCode && PublicBackendExitCodes.Contains(exitCode))
        {
            return exitCode;
        }

        return UnhandledFailure;
    }

    private static string DefaultConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibreSpot", "config.json");

    private static string MachineConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LibreSpot", "config.json");

    private static string DefaultFleetLogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LibreSpot", "logs");

    private sealed record ParseResult(CliOptions? Options, string? Error);

    private sealed class NdjsonLogWriter : IDisposable
    {
        private const int MaxLogFiles = 20;
        private readonly StreamWriter? _writer;

        private NdjsonLogWriter(string? path, StreamWriter? writer, bool initializationFailed = false)
        {
            Path = path;
            _writer = writer;
            InitializationFailed = initializationFailed;
        }

        public static NdjsonLogWriter Failed { get; } = new(null, null, initializationFailed: true);

        public string? Path { get; }
        public bool InitializationFailed { get; }

        public static NdjsonLogWriter Create(string directory, string operation)
        {
            var fullDirectory = System.IO.Path.GetFullPath(directory);
            Directory.CreateDirectory(fullDirectory);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var safeOperation = new string(operation.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
            if (string.IsNullOrWhiteSpace(safeOperation))
            {
                safeOperation = "operation";
            }

            var path = System.IO.Path.Combine(fullDirectory, $"librespot-{safeOperation}-{stamp}-{Guid.NewGuid():N}.ndjson");
            var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
            Rotate(fullDirectory);
            return new NdjsonLogWriter(path, writer);
        }

        public void WriteLine(string line)
        {
            if (!InitializationFailed)
            {
                _writer?.WriteLine(line);
            }
        }

        public void Dispose() => _writer?.Dispose();

        private static void Rotate(string directory)
        {
            var files = Directory
                .EnumerateFiles(directory, "librespot-*.ndjson")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var file in files.Skip(MaxLogFiles))
            {
                try { file.Delete(); } catch { }
            }
        }
    }

    private sealed class CliOptions
    {
        private readonly IReadOnlyDictionary<string, string?> _values;

        public CliOptions(IReadOnlyDictionary<string, string?> values)
        {
            _values = values;
        }

        public bool HasFlag(string name) => _values.ContainsKey(name);

        public string? GetValue(string name) =>
            _values.TryGetValue(name, out var value) ? value : null;

        public bool OnlyContains(params string[] names)
        {
            var allowed = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            return _values.Keys.All(allowed.Contains);
        }
    }
}

