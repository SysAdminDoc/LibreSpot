using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class RemoveSelfDataRegressionTests
{
    [Fact]
    public async Task BackendRemoveSelfData_ErasesLibreSpotDataLeavesSpotifyAndWritesReceipt()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.RemoveSelfData.Tests", Guid.NewGuid().ToString("N"));
        var appData = Path.Combine(root, "AppData", "Roaming");
        var localAppData = Path.Combine(root, "AppData", "Local");
        var programData = Path.Combine(root, "ProgramData");
        var temp = Path.Combine(root, "Temp");
        var userProfile = Path.Combine(root, "UserProfile");
        var configDirectory = Path.Combine(appData, "LibreSpot");
        var configPath = Path.Combine(configDirectory, "config.json");
        var localLibreSpot = Path.Combine(localAppData, "LibreSpot");
        var machineLibreSpot = Path.Combine(programData, "LibreSpot");
        var backupRoot = Path.Combine(userProfile, "LibreSpot_Backups");
        var spotifyPath = Path.Combine(appData, "Spotify", "Spotify.exe");
        var spicetifyExe = Path.Combine(localAppData, "spicetify", "spicetify.exe");
        var spicetifyConfigDirectory = Path.Combine(appData, "spicetify");
        var spicetifyConfig = Path.Combine(spicetifyConfigDirectory, "config-xpui.ini");
        var canary = "remove-self-data-canary-" + Guid.NewGuid().ToString("N");

        try
        {
            var config = AppCatalog.CreateRecommendedConfiguration();
            config.RiskAcknowledged = true;
            WriteFile(configPath, JsonSerializer.Serialize(config));
            WriteFile(Path.Combine(configDirectory, "profiles", "local.json"), canary);
            WriteFile(Path.Combine(configDirectory, "active-profile.json"), canary);
            WriteFile(Path.Combine(configDirectory, "active-profile.previous.json"), canary);
            WriteFile(Path.Combine(configDirectory, "profile-activation.lock"), canary);
            WriteFile(Path.Combine(configDirectory, "profile-activation.pending.json"), canary);
            WriteFile(Path.Combine(configDirectory, "profile-activation.test.previous.staged.json"), canary);
            WriteFile(Path.Combine(configDirectory, "profile-activation.test.next.staged.json"), canary);
            WriteFile(Path.Combine(configDirectory, "operation-journal.jsonl"), canary);
            WriteFile(Path.Combine(configDirectory, "run-receipt.latest.json"), canary);
            WriteFile(Path.Combine(configDirectory, "install.log"), canary);
            WriteFile(Path.Combine(configDirectory, "watcher-state.json"), $$"""{"Canary":"{{canary}}"}""");
            WriteFile(Path.Combine(configDirectory, "watcher.log"), canary);
            WriteFile(Path.Combine(configDirectory, "cache", "asset"), canary);
            WriteFile(Path.Combine(configDirectory, "config.corrupt.test.json"), canary);
            WriteFile(Path.Combine(configDirectory, "update-check.json"), canary);
            WriteFile(Path.Combine(configDirectory, "upstream-freshness-cache.json"), canary);
            WriteFile(Path.Combine(configDirectory, "marketplace-evidence.json"), canary);
            WriteFile(Path.Combine(configDirectory, "spicetify-preservation-latest.json"), canary);
            WriteFile(Path.Combine(configDirectory, "LibreSpot-support-test.zip"), canary);
            WriteFile(Path.Combine(localLibreSpot, "logs", "librespot-test.log"), canary);
            WriteFile(Path.Combine(localLibreSpot, "crashes", "crash-test.log"), canary);
            WriteFile(Path.Combine(localLibreSpot, "runtime", "LibreSpot.Backend.test.run.ps1"), canary);
            WriteFile(Path.Combine(localLibreSpot, "upstream-drift-cache.json"), canary);
            WriteFile(Path.Combine(localLibreSpot, "community-asset-drift-cache.json"), canary);
            WriteFile(Path.Combine(machineLibreSpot, "config.json"), canary);
            WriteFile(Path.Combine(machineLibreSpot, "logs", "librespot-install-test.ndjson"), canary);
            WriteFile(Path.Combine(backupRoot, "20260701-010101", "config-xpui.ini"), canary);
            WriteFile(spotifyPath, "spotify-stays");
            WriteFile(spicetifyExe, "spicetify-stays");
            WriteFile(spicetifyConfig, "custom_apps = marketplace");

            var result = await RunBackendRemoveSelfDataAsync(root, configPath);

            Assert.True(
                result.ExitCode == 0,
                $"Backend exited {result.ExitCode}\nSTDOUT:\n{result.Stdout}\nSTDERR:\n{result.Stderr}");
            Assert.Contains("LibreSpot self-cleanup complete", result.Stdout);
            Assert.False(Directory.Exists(configDirectory), result.Stdout);
            Assert.False(Directory.Exists(localLibreSpot), result.Stdout);
            Assert.False(Directory.Exists(machineLibreSpot), result.Stdout);
            Assert.False(Directory.Exists(backupRoot), result.Stdout);
            Assert.True(File.Exists(spotifyPath));
            Assert.True(File.Exists(spicetifyExe));
            Assert.True(File.Exists(spicetifyConfig));

            var receiptPath = Path.Combine(temp, "LibreSpot", "remove-self-data-receipt.latest.json");
            Assert.True(File.Exists(receiptPath));
            var receipt = File.ReadAllText(receiptPath);
            using var receiptJson = JsonDocument.Parse(receipt);
            Assert.Equal("RemoveSelfData", receiptJson.RootElement.GetProperty("action").GetString());
            Assert.False(receiptJson.RootElement.GetProperty("reversible").GetBoolean());
            Assert.False(receiptJson.RootElement.GetProperty("spotifyTouched").GetBoolean());
            Assert.False(receiptJson.RootElement.GetProperty("spicetifyTouched").GetBoolean());
            Assert.DoesNotContain(canary, receipt);
            Assert.DoesNotContain(root, receipt, StringComparison.OrdinalIgnoreCase);

            var snapshot = new EnvironmentSnapshotService(
                autoReapplyTaskProbe: () => false,
                spotifyPath: spotifyPath,
                spicetifyPath: spicetifyExe,
                spicetifyConfigDirectory: spicetifyConfigDirectory,
                rollingLogDirectory: Path.Combine(localLibreSpot, "logs"),
                crashDirectory: Path.Combine(localLibreSpot, "crashes"))
                .GetSnapshot(configPath);
            var supportPath = Path.Combine(root, "support-after-removal.zip");
            var bundle = await new SupportBundleService(
                    configDirectory,
                    Path.Combine(localLibreSpot, "logs"),
                    Path.Combine(localLibreSpot, "crashes"))
                .ExportAsync(supportPath, snapshot, new SupportBundleOptions());
            var supportText = string.Join("\n", ReadZipText(bundle.Path).Values);

            Assert.DoesNotContain(canary, supportText);
            Assert.DoesNotContain(configDirectory, supportText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(localLibreSpot, supportText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(machineLibreSpot, supportText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(backupRoot, supportText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<BackendRun> RunBackendRemoveSelfDataAsync(string root, string configPath)
    {
        var backend = Path.Combine(ResolveRepoRoot(), "src", "LibreSpot.Desktop", "Backend", "LibreSpot.Backend.ps1");
        var start = new ProcessStartInfo("powershell")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(backend);
        start.ArgumentList.Add("-Action");
        start.ArgumentList.Add("RemoveSelfData");
        start.ArgumentList.Add("-ConfigPath");
        start.ArgumentList.Add(configPath);
        start.Environment["LIBRESPOT_TEST_ROOT"] = root;

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start PowerShell.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new BackendRun(process.ExitCode, await stdout, await stderr);
    }

    private static IReadOnlyDictionary<string, string> ReadZipText(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.ToDictionary(
            entry => entry.FullName,
            entry =>
            {
                using var reader = new StreamReader(entry.Open());
                return reader.ReadToEnd();
            },
            StringComparer.Ordinal);
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LibreSpot.ps1")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root.");
    }

    private sealed record BackendRun(int ExitCode, string Stdout, string Stderr);
}
