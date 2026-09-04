using System.Diagnostics;
using System.Text.Json;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Core.Tests;

public sealed class MinidumpSettingsServiceTests
{
    [Fact]
    public void SetEnabled_PersistsTriagePolicyAndExactRuntimeEnvironment()
    {
        using var fixture = new Fixture();
        var service = fixture.CreateService();

        service.SetEnabled(true);

        Assert.True(service.IsEnabled);
        using var document = JsonDocument.Parse(File.ReadAllText(service.SettingsPath));
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal("triage", document.RootElement.GetProperty("dumpType").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("retainedDumpCount").GetInt32());
        Assert.Equal(
            new Dictionary<string, string>
            {
                [MinidumpSettingsService.EnableVariable] = "1",
                [MinidumpSettingsService.TypeVariable] = "3",
                [MinidumpSettingsService.NameVariable] = Path.Combine(fixture.CrashDirectory, "%e-%p-%t.dmp")
            },
            service.DesiredEnvironment());
    }

    [Fact]
    public void PrepareLaunch_RelaunchesOnlyLibreSpotWithArmedEnvironmentAndPreservedArguments()
    {
        using var fixture = new Fixture();
        ProcessStartInfo? captured = null;
        var service = fixture.CreateService(
            processLauncher: startInfo =>
            {
                captured = startInfo;
                return true;
            });
        service.SetEnabled(true);

        var result = service.PrepareLaunch(["--uia-theme=high-contrast"]);

        Assert.Equal(MinidumpLaunchDisposition.Relaunched, result.Disposition);
        Assert.NotNull(captured);
        Assert.Equal(fixture.ExecutablePath, captured.FileName);
        Assert.False(captured.UseShellExecute);
        Assert.Equal(
            ["--uia-theme=high-contrast", MinidumpSettingsService.ArmedArgument],
            captured.ArgumentList);
        Assert.Equal("1", captured.Environment[MinidumpSettingsService.EnableVariable]);
        Assert.Equal("3", captured.Environment[MinidumpSettingsService.TypeVariable]);
        Assert.Equal(Path.Combine(fixture.CrashDirectory, "%e-%p-%t.dmp"), captured.Environment[MinidumpSettingsService.NameVariable]);
    }

    [Fact]
    public void PrepareLaunch_DoesNotRelaunchWhenCurrentLibreSpotProcessIsAlreadyArmed()
    {
        using var fixture = new Fixture();
        var environment = new Dictionary<string, string?>
        {
            [MinidumpSettingsService.EnableVariable] = "1",
            [MinidumpSettingsService.TypeVariable] = "3",
            [MinidumpSettingsService.NameVariable] = Path.Combine(fixture.CrashDirectory, "%e-%p-%t.dmp")
        };
        var launchCount = 0;
        var service = fixture.CreateService(
            environmentReader: name => environment.GetValueOrDefault(name),
            processLauncher: _ =>
            {
                launchCount++;
                return true;
            });
        service.SetEnabled(true);

        var result = service.PrepareLaunch([]);

        Assert.Equal(MinidumpLaunchDisposition.AlreadyArmed, result.Disposition);
        Assert.Equal(0, launchCount);
    }

    [Fact]
    public void PrepareLaunch_PreventsRestartLoopWhenArmedChildLosesEnvironment()
    {
        using var fixture = new Fixture();
        var written = new Dictionary<string, string?>();
        var launchCount = 0;
        var service = fixture.CreateService(
            environmentWriter: (name, value) => written[name] = value,
            processLauncher: _ =>
            {
                launchCount++;
                return true;
            });
        service.SetEnabled(true);

        var result = service.PrepareLaunch([MinidumpSettingsService.ArmedArgument]);

        Assert.Equal(MinidumpLaunchDisposition.ArmedCurrentProcessAfterRelaunchFailure, result.Disposition);
        Assert.Contains("restart loop was prevented", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, launchCount);
        Assert.Equal("1", written[MinidumpSettingsService.EnableVariable]);
        Assert.Equal("3", written[MinidumpSettingsService.TypeVariable]);
    }

    [Fact]
    public void PrepareLaunch_FallsBackToCurrentProcessWhenRelaunchFails()
    {
        using var fixture = new Fixture();
        var written = new Dictionary<string, string?>();
        var service = fixture.CreateService(
            environmentWriter: (name, value) => written[name] = value,
            processLauncher: _ => throw new InvalidOperationException("blocked"));
        service.SetEnabled(true);

        var result = service.PrepareLaunch([]);

        Assert.Equal(MinidumpLaunchDisposition.ArmedCurrentProcessAfterRelaunchFailure, result.Disposition);
        Assert.Contains("blocked", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("1", written[MinidumpSettingsService.EnableVariable]);
        Assert.Equal("3", written[MinidumpSettingsService.TypeVariable]);
        Assert.Equal(Path.Combine(fixture.CrashDirectory, "%e-%p-%t.dmp"), written[MinidumpSettingsService.NameVariable]);
    }

    [Fact]
    public void MalformedOrDisabledSettings_FailClosed()
    {
        using var fixture = new Fixture();
        var service = fixture.CreateService();
        File.WriteAllText(service.SettingsPath, "{not-json");

        Assert.False(service.IsEnabled);
        Assert.Equal(MinidumpLaunchDisposition.Disabled, service.PrepareLaunch([]).Disposition);

        service.SetEnabled(true);
        service.SetEnabled(false);
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void PrepareLaunch_ReportsUnavailableCrashDirectoryWithoutBlockingAppStartup()
    {
        using var fixture = new Fixture();
        var service = fixture.CreateService();
        service.SetEnabled(true);
        Directory.Delete(fixture.CrashDirectory);
        File.WriteAllText(fixture.CrashDirectory, "directory blocker");

        var result = service.PrepareLaunch([]);

        Assert.Equal(MinidumpLaunchDisposition.Unavailable, result.Disposition);
        Assert.Contains("crash-dump directory", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PruneCrashDumps_KeepsOnlyTwoNewestFiles()
    {
        using var fixture = new Fixture();
        var paths = Enumerable.Range(0, 4)
            .Select(index => fixture.WriteDump($"dump-{index}.dmp", index))
            .ToArray();

        MinidumpSettingsService.PruneCrashDumps(fixture.CrashDirectory);

        Assert.False(File.Exists(paths[0]));
        Assert.False(File.Exists(paths[1]));
        Assert.True(File.Exists(paths[2]));
        Assert.True(File.Exists(paths[3]));
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "LibreSpot.Minidump.Tests", Guid.NewGuid().ToString("N"));
            ConfigDirectory = Path.Combine(Root, "config");
            CrashDirectory = Path.Combine(Root, "crashes");
            ExecutablePath = Path.Combine(Root, "LibreSpot.exe");
            Directory.CreateDirectory(ConfigDirectory);
        }

        public string Root { get; }
        public string ConfigDirectory { get; }
        public string CrashDirectory { get; }
        public string ExecutablePath { get; }

        public MinidumpSettingsService CreateService(
            Func<string, string?>? environmentReader = null,
            Action<string, string?>? environmentWriter = null,
            Func<ProcessStartInfo, bool>? processLauncher = null) =>
            new(
                ConfigDirectory,
                CrashDirectory,
                () => ExecutablePath,
                environmentReader ?? (_ => null),
                environmentWriter ?? ((_, _) => { }),
                processLauncher ?? (_ => true));

        public string WriteDump(string name, int minute)
        {
            Directory.CreateDirectory(CrashDirectory);
            var path = Path.Combine(CrashDirectory, name);
            File.WriteAllBytes(path, [(byte)minute]);
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 7, 1, 12, minute, 0, DateTimeKind.Utc));
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }
}
