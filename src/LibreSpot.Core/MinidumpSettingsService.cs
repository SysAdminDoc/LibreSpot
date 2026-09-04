using System.Diagnostics;
using System.Text.Json;

namespace LibreSpot.Desktop.Services;

public enum MinidumpLaunchDisposition
{
    Disabled,
    Unavailable,
    AlreadyArmed,
    Relaunched,
    ArmedCurrentProcessAfterRelaunchFailure
}

public sealed record MinidumpLaunchResult(
    MinidumpLaunchDisposition Disposition,
    string? ErrorMessage = null)
{
    public bool Relaunched => Disposition == MinidumpLaunchDisposition.Relaunched;
}

public sealed class MinidumpSettingsService
{
    public const string EnableVariable = "DOTNET_DbgEnableMiniDump";
    public const string TypeVariable = "DOTNET_DbgMiniDumpType";
    public const string NameVariable = "DOTNET_DbgMiniDumpName";
    public const string ArmedArgument = "--librespot-minidump-armed";
    public const int RetainedDumpCount = 2;

    private const int SchemaVersion = 1;
    private const int MaxSettingsBytes = 64 * 1024;
    private const string SettingsFileName = "minidump-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _configDirectory;
    private readonly string _crashDirectory;
    private readonly Func<string?> _processPathProvider;
    private readonly Func<string, string?> _environmentReader;
    private readonly Action<string, string?> _environmentWriter;
    private readonly Func<ProcessStartInfo, bool> _processLauncher;

    public MinidumpSettingsService(
        string? configDirectory = null,
        string? crashDirectory = null,
        Func<string?>? processPathProvider = null,
        Func<string, string?>? environmentReader = null,
        Action<string, string?>? environmentWriter = null,
        Func<ProcessStartInfo, bool>? processLauncher = null)
    {
        _configDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(configDirectory)
            ? LibreSpotPaths.ConfigDirectory
            : configDirectory);
        _crashDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(crashDirectory)
            ? LibreSpotPaths.CrashesDirectory
            : crashDirectory);
        _processPathProvider = processPathProvider ?? (() => Environment.ProcessPath);
        _environmentReader = environmentReader ?? Environment.GetEnvironmentVariable;
        _environmentWriter = environmentWriter ?? ((name, value) => Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process));
        _processLauncher = processLauncher ?? StartProcess;
    }

    public string SettingsPath => Path.Combine(_configDirectory, SettingsFileName);

    public string DumpNameTemplate => Path.Combine(_crashDirectory, "%e-%p-%t.dmp");

    public bool IsEnabled => ReadSettings()?.Enabled == true;

    public void SetEnabled(bool enabled)
    {
        Directory.CreateDirectory(_configDirectory);
        if (File.Exists(SettingsPath) && (File.GetAttributes(SettingsPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("The local crash-dump settings file is a reparse point.");
        }

        if (enabled)
        {
            Directory.CreateDirectory(_crashDirectory);
            PruneCrashDumps(_crashDirectory, RetainedDumpCount);
        }

        var document = new MinidumpSettingsDocument(
            SchemaVersion,
            enabled,
            "triage",
            RetainedDumpCount,
            DateTimeOffset.UtcNow);
        WriteSettingsAtomically(document);
    }

    public MinidumpLaunchResult PrepareLaunch(IEnumerable<string> arguments)
    {
        if (!IsEnabled)
        {
            return new MinidumpLaunchResult(MinidumpLaunchDisposition.Disabled);
        }

        try
        {
            Directory.CreateDirectory(_crashDirectory);
        }
        catch (Exception ex)
        {
            return new MinidumpLaunchResult(
                MinidumpLaunchDisposition.Unavailable,
                $"LibreSpot could not prepare the local crash-dump directory: {ex.Message}");
        }

        PruneCrashDumps(_crashDirectory, RetainedDumpCount);
        var desiredEnvironment = DesiredEnvironment();
        if (desiredEnvironment.All(pair => string.Equals(_environmentReader(pair.Key), pair.Value, StringComparison.Ordinal)))
        {
            return new MinidumpLaunchResult(MinidumpLaunchDisposition.AlreadyArmed);
        }

        var launchArguments = arguments.ToArray();
        if (launchArguments.Contains(ArmedArgument, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var pair in desiredEnvironment)
            {
                _environmentWriter(pair.Key, pair.Value);
            }

            return new MinidumpLaunchResult(
                MinidumpLaunchDisposition.ArmedCurrentProcessAfterRelaunchFailure,
                "The armed LibreSpot process started without the expected crash-dump environment. A restart loop was prevented.");
        }

        try
        {
            var processPath = _processPathProvider();
            if (string.IsNullOrWhiteSpace(processPath) || !Path.IsPathFullyQualified(processPath))
            {
                throw new InvalidOperationException("LibreSpot could not resolve its executable path for the armed restart.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory
            };
            foreach (var argument in launchArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add(ArmedArgument);
            foreach (var pair in desiredEnvironment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }

            if (!_processLauncher(startInfo))
            {
                throw new InvalidOperationException("Windows did not start the armed LibreSpot process.");
            }

            return new MinidumpLaunchResult(MinidumpLaunchDisposition.Relaunched);
        }
        catch (Exception ex)
        {
            foreach (var pair in desiredEnvironment)
            {
                _environmentWriter(pair.Key, pair.Value);
            }

            return new MinidumpLaunchResult(
                MinidumpLaunchDisposition.ArmedCurrentProcessAfterRelaunchFailure,
                ex.Message);
        }
    }

    public IReadOnlyDictionary<string, string> DesiredEnvironment() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EnableVariable] = "1",
            [TypeVariable] = "3",
            [NameVariable] = DumpNameTemplate
        };

    public static void PruneCrashDumps(string crashDirectory, int keep = RetainedDumpCount)
    {
        if (keep < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keep));
        }

        var root = Path.GetFullPath(crashDirectory);
        if (!Directory.Exists(root))
        {
            return;
        }

        IEnumerable<FileInfo> files;
        try
        {
            files = new DirectoryInfo(root)
                .EnumerateFiles("*.dmp", SearchOption.TopDirectoryOnly)
                .Where(file => (file.Attributes & FileAttributes.ReparsePoint) == 0)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ThenByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return;
        }

        foreach (var file in files.Skip(keep))
        {
            try { file.Delete(); } catch { }
        }
    }

    private static bool StartProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        return process is not null;
    }

    private MinidumpSettingsDocument? ReadSettings()
    {
        try
        {
            var info = new FileInfo(SettingsPath);
            if (!info.Exists || info.Length is <= 0 or > MaxSettingsBytes || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }

            using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var settings = JsonSerializer.Deserialize<MinidumpSettingsDocument>(stream, JsonOptions);
            return settings is { SchemaVersion: SchemaVersion, DumpType: "triage", RetainedDumpCount: RetainedDumpCount }
                ? settings
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void WriteSettingsAtomically(MinidumpSettingsDocument settings)
    {
        var temporaryPath = Path.Combine(_configDirectory, $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); } catch { }
        }
    }

    private sealed record MinidumpSettingsDocument(
        int SchemaVersion,
        bool Enabled,
        string DumpType,
        int RetainedDumpCount,
        DateTimeOffset UpdatedAtUtc);
}
