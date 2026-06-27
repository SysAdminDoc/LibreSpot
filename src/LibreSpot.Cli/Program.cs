using System.Reflection;
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
        "--profile",
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
                stdout.WriteLine($"LibreSpot.Cli {ProductVersion}");
                return Success;
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
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(arg);
                continue;
            }

            var name = arg;
            string? value = null;
            var equalsIndex = arg.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex > 0)
            {
                name = arg[..equalsIndex];
                value = arg[(equalsIndex + 1)..];
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

        return new ParseResult(new CliOptions(options), null);
    }

    private static void WriteJson<T>(TextWriter stdout, T value) =>
        stdout.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

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
