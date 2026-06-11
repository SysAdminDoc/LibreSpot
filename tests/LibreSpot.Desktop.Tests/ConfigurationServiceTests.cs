using System.IO;
using System.Text.Json;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public async Task LoadResultAsync_ReturnsMissingStateWithRecommendedDefaults()
    {
        var configDirectory = CreateTempDirectory();
        try
        {
            var service = new ConfigurationService(configDirectory);

            var result = await service.LoadResultAsync();

            Assert.Equal(ConfigurationLoadState.Missing, result.State);
            Assert.Null(result.RecoveredFilePath);
            AssertRecommendedDefaults(result.Configuration);
        }
        finally
        {
            DeleteDirectory(configDirectory);
        }
    }

    [Fact]
    public async Task LoadResultAsync_RecoversUnreadableConfigAndPreservesBackup()
    {
        var configDirectory = CreateTempDirectory();
        try
        {
            var service = new ConfigurationService(configDirectory);
            Directory.CreateDirectory(configDirectory);
            await File.WriteAllTextAsync(service.ConfigPath, "{ definitely-not-json");

            var result = await service.LoadResultAsync();
            var recoveredFilePath = Assert.IsType<string>(result.RecoveredFilePath);

            Assert.Equal(ConfigurationLoadState.RecoveredFromCorrupt, result.State);
            Assert.False(string.IsNullOrWhiteSpace(result.RecoveryReason));
            AssertRecommendedDefaults(result.Configuration);
            Assert.False(File.Exists(service.ConfigPath));
            Assert.True(File.Exists(recoveredFilePath));
            Assert.Equal("{ definitely-not-json", await File.ReadAllTextAsync(recoveredFilePath));
        }
        finally
        {
            DeleteDirectory(configDirectory);
        }
    }

    [Fact]
    public async Task LoadResultAsync_QuarantinesFutureSchemaVersion()
    {
        var configDirectory = CreateTempDirectory();
        try
        {
            var service = new ConfigurationService(configDirectory);
            Directory.CreateDirectory(configDirectory);
            await File.WriteAllTextAsync(service.ConfigPath, "{\"ConfigSchemaVersion\":999}");

            var result = await service.LoadResultAsync();
            var recoveredFilePath = Assert.IsType<string>(result.RecoveredFilePath);

            Assert.Equal(ConfigurationLoadState.RecoveredFromCorrupt, result.State);
            Assert.Contains("Saved config schema version 999", result.RecoveryReason);
            AssertRecommendedDefaults(result.Configuration);
            Assert.False(File.Exists(service.ConfigPath));
            Assert.True(File.Exists(recoveredFilePath));
            Assert.Equal("{\"ConfigSchemaVersion\":999}", await File.ReadAllTextAsync(recoveredFilePath));
        }
        finally
        {
            DeleteDirectory(configDirectory);
        }
    }

    [Fact]
    public async Task LoadResultAsync_QuarantinesRepeatedCorruptConfigsWithUniqueBackups()
    {
        var configDirectory = CreateTempDirectory();
        try
        {
            var service = new ConfigurationService(configDirectory);
            Directory.CreateDirectory(configDirectory);

            await File.WriteAllTextAsync(service.ConfigPath, "{ first-corrupt-file");
            var firstResult = await service.LoadResultAsync();
            var firstRecoveredPath = Assert.IsType<string>(firstResult.RecoveredFilePath);

            await File.WriteAllTextAsync(service.ConfigPath, "{ second-corrupt-file");
            var secondResult = await service.LoadResultAsync();
            var secondRecoveredPath = Assert.IsType<string>(secondResult.RecoveredFilePath);

            Assert.Equal(ConfigurationLoadState.RecoveredFromCorrupt, firstResult.State);
            Assert.Equal(ConfigurationLoadState.RecoveredFromCorrupt, secondResult.State);
            Assert.NotEqual(firstRecoveredPath, secondRecoveredPath);
            Assert.False(File.Exists(service.ConfigPath));
            Assert.True(File.Exists(firstRecoveredPath));
            Assert.True(File.Exists(secondRecoveredPath));
            Assert.Equal("{ first-corrupt-file", await File.ReadAllTextAsync(firstRecoveredPath));
            Assert.Equal("{ second-corrupt-file", await File.ReadAllTextAsync(secondRecoveredPath));
            Assert.Equal(2, Directory.GetFiles(configDirectory, "config.corrupt-*.json").Length);
        }
        finally
        {
            DeleteDirectory(configDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_WritesCurrentSchemaVersion()
    {
        var configDirectory = CreateTempDirectory();
        try
        {
            var service = new ConfigurationService(configDirectory);

            await service.SaveAsync(new InstallConfiguration { ConfigSchemaVersion = 0 });

            await using var stream = File.OpenRead(service.ConfigPath);
            using var json = await JsonDocument.ParseAsync(stream);
            var version = json.RootElement.GetProperty(nameof(InstallConfiguration.ConfigSchemaVersion)).GetInt32();
            Assert.Equal(AppCatalog.CurrentConfigSchemaVersion, version);
        }
        finally
        {
            DeleteDirectory(configDirectory);
        }
    }

    [Fact]
    public async Task SaveAsync_NormalizesConfigurationAndLeavesNoTempFile()
    {
        var configDirectory = CreateTempDirectory();
        try
        {
            var service = new ConfigurationService(configDirectory);
            var configuration = new InstallConfiguration
            {
                SpotX_LyricsEnabled = false,
                SpotX_LyricsBlock = true,
                SpotX_OldLyrics = true
            };

            await service.SaveAsync(configuration);
            var saved = await service.LoadAsync();

            Assert.False(saved.SpotX_LyricsBlock);
            Assert.False(saved.SpotX_OldLyrics);
            Assert.Empty(Directory.GetFiles(configDirectory, "*.tmp"));
        }
        finally
        {
            DeleteDirectory(configDirectory);
        }
    }

    private static void AssertRecommendedDefaults(InstallConfiguration configuration)
    {
        var recommended = AppCatalog.CreateRecommendedConfiguration();

        Assert.Equal(AppCatalog.CurrentConfigSchemaVersion, configuration.ConfigSchemaVersion);
        Assert.Equal(recommended.ConfigSchemaVersion, configuration.ConfigSchemaVersion);
        Assert.Equal(recommended.Mode, configuration.Mode);
        Assert.Equal(recommended.CleanInstall, configuration.CleanInstall);
        Assert.Equal(recommended.LaunchAfter, configuration.LaunchAfter);
        Assert.Equal(recommended.SpotX_LyricsTheme, configuration.SpotX_LyricsTheme);
        Assert.Equal(recommended.Spicetify_Theme, configuration.Spicetify_Theme);
        Assert.Equal(recommended.Spicetify_Scheme, configuration.Spicetify_Scheme);
        Assert.Equal(recommended.SpotX_DownloadMethod, configuration.SpotX_DownloadMethod);
        Assert.Equal(recommended.SpotX_SpotifyVersionId, configuration.SpotX_SpotifyVersionId);
        Assert.Equal(recommended.Spicetify_Extensions, configuration.Spicetify_Extensions);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
