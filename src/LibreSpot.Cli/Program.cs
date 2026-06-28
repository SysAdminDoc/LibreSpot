using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
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

    public static int Run(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, EnvironmentSnapshot>? snapshotFactory = null)
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

            var verb = args[0].Trim().ToLowerInvariant();
            var parse = Parse(args.Skip(1).ToArray());
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
                "install" => RunPlannedOperation("install", options, stdout, stderr),
                "reapply" => RunPlannedOperation("reapply", options, stdout, stderr),
                "uninstall" => RunPlannedOperation("uninstall", options, stdout, stderr),
                "plan" => RunPlannedOperation("install", options, stdout, stderr, planVerb: true),
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

        var configPath = options.GetValue("--config-path") ?? DefaultConfigPath;
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

        var configPath = options.GetValue("--config-path") ?? DefaultConfigPath;
        var snapshot = GetSnapshot(configPath, snapshotFactory);
        var detection = BuildDetectionDocument(snapshot, configPath);

        if (options.HasFlag("--intune"))
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

        if (options.HasFlag("--json"))
        {
            WriteJson(stdout, detection);
        }
        else
        {
            stdout.WriteLine($"{detection.State}: {detection.Summary}");
        }

        return detection.ExitCode;
    }

    private static int RunValidate(CliOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (!options.OnlyContains("--json", "--answer-file", "--config-path", "--correlation-id", "--log-dir"))
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

        var result = ValidateAnswerFile(answerFile);
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

    private static int RunPlannedOperation(
        string operation,
        CliOptions options,
        TextWriter stdout,
        TextWriter stderr,
        bool planVerb = false)
    {
        if (!options.OnlyContains(
                "--answer-file",
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

        if (!planVerb && !options.HasFlag("--dry-run"))
        {
            stderr.WriteLine($"{operation} is currently available only with --dry-run in LibreSpot.Cli.");
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
        var operationId = Guid.NewGuid();
        if (!string.IsNullOrWhiteSpace(answerFile))
        {
            validation = ValidateAnswerFile(answerFile);
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
            stderr.WriteLine($"{operation} dry-run requires --answer-file <path> so fleet intent is explicit.");
            return ValidationError;
        }

        var plan = BuildPlan(operation, options, validation);
        if (options.HasFlag("--ndjson"))
        {
            WriteJson(stdout, NdjsonEvent(
                "LS1001",
                "info",
                "lifecycle",
                "LibreSpot dry-run plan started.",
                operation,
                new { plan.Operation, plan.DryRun, plan.Mutates },
                options,
                operationId));
            foreach (var step in plan.Steps)
            {
                WriteJson(stdout, NdjsonEvent(
                    "LS8001",
                    "info",
                    "journal",
                    "Dry-run plan step recorded.",
                    operation,
                    step,
                    options,
                    operationId,
                    target: step.Target));
            }

            WriteJson(stdout, NdjsonEvent(
                "LS1002",
                "success",
                "lifecycle",
                "LibreSpot dry-run plan completed.",
                operation,
                new { stepCount = plan.Steps.Count },
                options,
                operationId,
                exitCode: Success));
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
            1,
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
            snapshot.AutoReapplyTaskRegistered,
            BackupCount(snapshot),
            ComponentLastChanged(snapshot, "post-spotify-update"),
            ComponentStatus(snapshot, "auto-reapply-watcher"),
            IssueIds(snapshot),
            RecommendedRepairIds(snapshot),
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

    private static ValidationDocument ValidateAnswerFile(string answerFile)
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
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true });
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new ValidationErrorDocument("$", "Answer file root must be a JSON object."));
                return new ValidationDocument(1, fullPath, false, errors);
            }

            RequireSchemaVersion(doc.RootElement, errors);
            RequireBooleanTrue(doc.RootElement, "eulaAccepted", errors);
            RequireBooleanTrue(doc.RootElement, "riskAcknowledged", errors);
            ValidateInstallMode(doc.RootElement, errors);
        }
        catch (JsonException ex)
        {
            errors.Add(new ValidationErrorDocument("$", $"Invalid JSON: {ex.Message}"));
        }
        catch (IOException ex)
        {
            errors.Add(new ValidationErrorDocument("$", $"Could not read answer file: {ex.Message}"));
        }

        return new ValidationDocument(1, fullPath, errors.Count == 0, errors);
    }

    private static PlanDocument BuildPlan(string operation, CliOptions options, ValidationDocument? validation)
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
            options.GetValue("--config-path") ?? DefaultConfigPath,
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

    private static void ValidateInstallMode(JsonElement root, ICollection<ValidationErrorDocument> errors)
    {
        if (!root.TryGetProperty("installMode", out var value))
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            errors.Add(new ValidationErrorDocument("$.installMode", "installMode must be a string."));
            return;
        }

        var mode = value.GetString();
        if (mode is not ("recommended" or "custom" or "reapply"))
        {
            errors.Add(new ValidationErrorDocument("$.installMode", "installMode must be recommended, custom, or reapply."));
        }
    }

    private static EnvironmentSnapshot GetSnapshot(string configPath, Func<string, EnvironmentSnapshot>? snapshotFactory) =>
        snapshotFactory is not null
            ? snapshotFactory(configPath)
            : new EnvironmentSnapshotService().GetSnapshot(configPath);

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
        writer.WriteLine("  LibreSpot.Cli install --dry-run --answer-file <path> [--ndjson]");
        writer.WriteLine("  LibreSpot.Cli plan --answer-file <path> [--json]");
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

    private static string DefaultConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibreSpot", "config.json");

    private sealed record ParseResult(CliOptions? Options, string? Error);

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

public sealed record StatusDocument(
    int SchemaVersion,
    string ProductVersion,
    DateTimeOffset GeneratedAtUtc,
    string ConfigPath,
    string HostArchitecture,
    string ProcessArchitecture,
    string StatusTitle,
    string StatusDetail,
    string IssueSummary,
    bool SpotifyInstalled,
    bool SpicetifyInstalled,
    bool MarketplaceReady,
    bool AutoReapplyTaskRegistered,
    int BackupCount,
    DateTimeOffset? LastPatchTimeUtc,
    string? LastWatcherOutcome,
    IReadOnlyList<string> IssueIds,
    IReadOnlyList<string> RecommendedRepairIds,
    IReadOnlyList<ComponentDocument> Components);

public sealed record ComponentDocument(
    string Id,
    string Name,
    string Status,
    string Severity,
    string? DetectedVersion,
    string? Path,
    DateTimeOffset? LastChangedUtc,
    string Evidence,
    IReadOnlyList<string> RecommendedActionIds)
{
    public static ComponentDocument From(StackHealthComponent component) =>
        new(
            component.Id,
            component.Name,
            component.Status,
            component.Severity,
            component.DetectedVersion,
            component.Path,
            component.LastChanged.HasValue ? new DateTimeOffset(component.LastChanged.Value.ToUniversalTime()) : null,
            component.Evidence,
            component.RecommendedActionIds);
}

public sealed record DetectionDocument(
    int SchemaVersion,
    string ProductVersion,
    DateTimeOffset GeneratedAtUtc,
    string ConfigPath,
    string State,
    int ExitCode,
    string Summary,
    IReadOnlyList<string> IssueIds,
    IReadOnlyList<string> RecommendedRepairIds);

public sealed record ValidationDocument(
    int SchemaVersion,
    string AnswerFile,
    bool Valid,
    IReadOnlyList<ValidationErrorDocument> Errors);

public sealed record ValidationErrorDocument(string Path, string Message);

public sealed record PlanDocument(
    int SchemaVersion,
    string ProductVersion,
    DateTimeOffset GeneratedAtUtc,
    string Operation,
    bool DryRun,
    bool Mutates,
    string? CorrelationId,
    string? AnswerFile,
    string? Profile,
    IReadOnlyList<PlanStepDocument> Steps);

public sealed record PlanStepDocument(
    string Id,
    string Title,
    bool RequiresAdmin,
    bool Mutates,
    string Target,
    string Detail);

public sealed record VersionDocument(
    int SchemaVersion,
    string ProductVersion,
    string AssemblyVersion,
    DateTimeOffset GeneratedAtUtc,
    string FrameworkDescription,
    string RuntimeIdentifier,
    string ProcessArchitecture,
    string OsDescription,
    DependencyPinsDocument Dependencies);

public sealed record DependencyPinsDocument(
    SpotXPinDocument SpotX,
    SpicetifyPinDocument SpicetifyCli,
    string MarketplaceVersion,
    string ThemesCommit);

public sealed record SpotXPinDocument(
    string Version,
    string Commit,
    string SpotifyTargetId,
    string SpotifyTargetVersion);

public sealed record SpicetifyPinDocument(
    string Version,
    string WindowsMinTestedSpotify,
    string WindowsMaxTestedSpotify);

public sealed record NdjsonLogLine(
    int SchemaVersion,
    string EventId,
    DateTimeOffset Timestamp,
    string Level,
    string Verb,
    Guid OperationId,
    string? CorrelationId,
    string Component,
    string? Target,
    string Message,
    object Payload,
    int? ExitCode);
