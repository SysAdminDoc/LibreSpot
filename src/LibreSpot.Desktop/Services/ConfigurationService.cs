using System.Globalization;
using System.IO;
using System.Text.Json;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public enum ConfigurationLoadState
{
    Loaded,
    Missing,
    RecoveredFromCorrupt
}

public sealed record ConfigurationLoadResult(
    InstallConfiguration Configuration,
    ConfigurationLoadState State,
    string? RecoveredFilePath = null);

public sealed class ConfigurationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true
    };

    // Serializes concurrent Save calls so the on-disk config is never torn.
    // Concurrent saves are unlikely but cheap to defend against.
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly string _configDirectory;

    public ConfigurationService(string? configDirectory = null)
    {
        _configDirectory = string.IsNullOrWhiteSpace(configDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibreSpot")
            : Path.GetFullPath(configDirectory);
    }

    public string ConfigDirectory => _configDirectory;
    public string ConfigPath => Path.Combine(ConfigDirectory, "config.json");
    public string LogPath => Path.Combine(ConfigDirectory, "install.log");

    public async Task<InstallConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
        (await LoadResultAsync(cancellationToken)).Configuration;

    public async Task<ConfigurationLoadResult> LoadResultAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(ConfigPath))
        {
            return new ConfigurationLoadResult(
                AppCatalog.CreateRecommendedConfiguration(),
                ConfigurationLoadState.Missing);
        }

        try
        {
            await using var stream = File.Open(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var config = await JsonSerializer.DeserializeAsync<InstallConfiguration>(stream, SerializerOptions, cancellationToken);
            if (config is not null)
            {
                return new ConfigurationLoadResult(
                    AppCatalog.NormalizeConfiguration(config),
                    ConfigurationLoadState.Loaded);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Preserve the corrupt file for forensics rather than silently overwriting
            // user state with defaults. The PS backend has similar quarantine behavior.
            var recoveredFilePath = QuarantineCorruptConfig();
            return new ConfigurationLoadResult(
                AppCatalog.CreateRecommendedConfiguration(),
                ConfigurationLoadState.RecoveredFromCorrupt,
                recoveredFilePath);
        }

        var nullPayloadRecoveryPath = QuarantineCorruptConfig();
        return new ConfigurationLoadResult(
            AppCatalog.CreateRecommendedConfiguration(),
            ConfigurationLoadState.RecoveredFromCorrupt,
            nullPayloadRecoveryPath);
    }

    public async Task SaveAsync(InstallConfiguration configuration, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ConfigDirectory);
        await _saveLock.WaitAsync(cancellationToken);
        string? tempPath = null;
        try
        {
            var normalizedConfiguration = AppCatalog.NormalizeConfiguration(configuration);
            tempPath = Path.Combine(ConfigDirectory, $"config.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, normalizedConfiguration, SerializerOptions, cancellationToken);
                // Ensure contents hit disk before we swap over the real file.
                await stream.FlushAsync(cancellationToken);
            }

            // Atomic replace. File.Move(source, dest, overwrite) is atomic on the same
            // volume, which APPDATA always is. This prevents torn writes if the process
            // is killed mid-save — either the old config or the new one remains intact.
            File.Move(tempPath, ConfigPath, overwrite: true);
            tempPath = null;
        }
        catch
        {
            // Best-effort cleanup of the temp file; if the move already consumed it this is a no-op.
            try
            {
                if (!string.IsNullOrEmpty(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch { }
            throw;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private string? QuarantineCorruptConfig()
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var suffix = attempt == 0 ? string.Empty : $"-{attempt}";
                var quarantinePath = Path.Combine(ConfigDirectory, $"config.corrupt-{timestamp}{suffix}.json");
                if (File.Exists(quarantinePath))
                {
                    continue;
                }

                // Use overwrite:true to handle the TOCTOU race where another process
                // creates the file between the Exists check and the Move call.
                File.Move(ConfigPath, quarantinePath, overwrite: true);
                return quarantinePath;
            }

            var fallbackPath = Path.Combine(ConfigDirectory, $"config.corrupt-{Guid.NewGuid():N}.json");
            File.Move(ConfigPath, fallbackPath, overwrite: false);
            return fallbackPath;
        }
        catch
        {
            // Quarantine is best-effort. If we can't move it, the next Save will overwrite anyway.
            return null;
        }
    }
}
