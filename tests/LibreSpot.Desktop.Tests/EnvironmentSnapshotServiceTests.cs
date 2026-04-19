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
}
