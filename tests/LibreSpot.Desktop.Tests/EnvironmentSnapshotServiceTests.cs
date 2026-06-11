using System.IO;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class EnvironmentSnapshotServiceTests
{
    [Fact]
    public void GetSnapshot_UsesDirectoryFromSuppliedConfigPath()
    {
        var configDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(configDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(configDirectory);
            var service = new EnvironmentSnapshotService();

            var snapshotBeforeSave = service.GetSnapshot(configPath);
            Assert.True(snapshotBeforeSave.ConfigFolderExists);
            Assert.False(snapshotBeforeSave.SavedConfigExists);

            File.WriteAllText(configPath, "{}");

            var snapshotAfterSave = service.GetSnapshot(configPath);
            Assert.True(snapshotAfterSave.ConfigFolderExists);
            Assert.True(snapshotAfterSave.SavedConfigExists);
        }
        finally
        {
            if (Directory.Exists(configDirectory))
            {
                Directory.Delete(configDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GetSnapshot_ReportsAutoReapplyTaskProbeState()
    {
        var configDirectory = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(configDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(configDirectory);
            var service = new EnvironmentSnapshotService(autoReapplyTaskProbe: () => true);

            var snapshot = service.GetSnapshot(configPath);

            Assert.True(snapshot.AutoReapplyTaskRegistered);
        }
        finally
        {
            if (Directory.Exists(configDirectory))
            {
                Directory.Delete(configDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void GetSnapshot_ReportsMarketplaceReadyWhenFilesAndConfigRegistrationExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var appConfigDirectory = Path.Combine(root, "LibreSpot");
        var spicetifyConfigDirectory = Path.Combine(root, "spicetify-config");
        var spicetifyExe = Path.Combine(root, "spicetify", "spicetify.exe");
        var marketplaceDirectory = Path.Combine(spicetifyConfigDirectory, "CustomApps", "marketplace");
        var configPath = Path.Combine(appConfigDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(appConfigDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(spicetifyExe)!);
            Directory.CreateDirectory(marketplaceDirectory);
            File.WriteAllText(spicetifyExe, "");
            File.WriteAllText(Path.Combine(marketplaceDirectory, "extension.js"), "");
            File.WriteAllText(Path.Combine(marketplaceDirectory, "manifest.json"), "{}");
            File.WriteAllText(Path.Combine(spicetifyConfigDirectory, "config-xpui.ini"), "custom_apps = history | marketplace");

            var service = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => false,
                spicetifyPath: spicetifyExe,
                spicetifyConfigDirectory: spicetifyConfigDirectory);

            var snapshot = service.GetSnapshot(configPath);

            Assert.True(snapshot.SpicetifyInstalled);
            Assert.True(snapshot.MarketplaceFilesPresent);
            Assert.True(snapshot.MarketplaceRegistered);
            Assert.True(snapshot.MarketplaceReady);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void GetSnapshot_ReportsHiddenMarketplaceWhenFilesExistWithoutRegistration()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.Tests", Guid.NewGuid().ToString("N"));
        var appConfigDirectory = Path.Combine(root, "LibreSpot");
        var spicetifyConfigDirectory = Path.Combine(root, "spicetify-config");
        var spicetifyExe = Path.Combine(root, "spicetify", "spicetify.exe");
        var marketplaceDirectory = Path.Combine(spicetifyConfigDirectory, "CustomApps", "marketplace");
        var configPath = Path.Combine(appConfigDirectory, "config.json");

        try
        {
            Directory.CreateDirectory(appConfigDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(spicetifyExe)!);
            Directory.CreateDirectory(marketplaceDirectory);
            File.WriteAllText(spicetifyExe, "");
            File.WriteAllText(Path.Combine(marketplaceDirectory, "extension.js"), "");
            File.WriteAllText(Path.Combine(marketplaceDirectory, "manifest.json"), "{}");
            File.WriteAllText(Path.Combine(spicetifyConfigDirectory, "config-xpui.ini"), "custom_apps = history");

            var service = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => false,
                spicetifyPath: spicetifyExe,
                spicetifyConfigDirectory: spicetifyConfigDirectory);

            var snapshot = service.GetSnapshot(configPath);

            Assert.True(snapshot.MarketplaceFilesPresent);
            Assert.False(snapshot.MarketplaceRegistered);
            Assert.False(snapshot.MarketplaceReady);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
