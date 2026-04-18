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
        try
        {
            var normalizedConfiguration = AppCatalog.NormalizeConfiguration(configuration);
            var tempPath = ConfigPath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, normalizedConfiguration, SerializerOptions, cancellationToken);
                // Ensure contents hit disk before we swap over the real file.
                await stream.FlushAsync(cancellationToken);
            }

            // Atomic replace. File.Move(source, dest, overwrite) is atomic on the same
            // volume, which APPDATA always is. This prevents torn writes if the process
            // is killed mid-save — either the old config or the new one remains intact.
            File.Move(tempPath, ConfigPath, overwrite: true);
        }
        catch
        {
            // Best-effort cleanup of the temp file; if the move already consumed it this is a no-op.
            try { File.Delete(ConfigPath + ".tmp"); } catch { }
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
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var quarantinePath = Path.Combine(ConfigDirectory, $"config.corrupt-{timestamp}.json");
            File.Move(ConfigPath, quarantinePath, overwrite: false);
            return quarantinePath;
        }
        catch
        {
            // Quarantine is best-effort. If we can't move it, the next Save will overwrite anyway.
            return null;
        }
    }
}
