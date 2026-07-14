using System.Diagnostics;
using LibreSpot.Desktop.Models;
using LibreSpot.Desktop.Services;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class ProfileActivationCrossHostTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "LibreSpot.Profile.CrossHost.Tests", Guid.NewGuid().ToString("N"));
    private readonly ConfigurationService configurationService;
    private readonly LocalProfileService profileService;

    public ProfileActivationCrossHostTests()
    {
        configurationService = new ConfigurationService(root);
        profileService = new LocalProfileService(configurationService);
    }

    [Fact]
    public async Task PowerShellHost_RecoversDesktopTransactionInterruptedAfterConfigCommit()
    {
        var oldConfiguration = AppCatalog.CreateRecommendedConfiguration();
        oldConfiguration.SpotX_LyricsTheme = "github";
        var newConfiguration = AppCatalog.CreateRecommendedConfiguration();
        newConfiguration.SpotX_LyricsTheme = "lavender";
        var oldProfile = await profileService.CreateFromConfigurationAsync("Cross Host Old", "", oldConfiguration);
        var newProfile = await profileService.CreateFromConfigurationAsync("Cross Host New", "", newConfiguration);
        await profileService.ApplyProfileAsync(oldProfile.Summary.Id);

        var faultingService = new LocalProfileService(
            configurationService,
            stage =>
            {
                if (stage == ProfileActivationStage.ConfigWritten)
                {
                    throw new SimulatedProcessTerminationException();
                }
            });
        await Assert.ThrowsAsync<SimulatedProcessTerminationException>(() =>
            faultingService.ApplyProfileAsync(newProfile.Summary.Id));

        var run = await RunPowerShellHarnessAsync("Recover");
        Assert.True(run.ExitCode == 0, run.Details);

        var recovered = new LocalProfileService(configurationService);
        var active = Assert.Single(await recovered.GetProfilesAsync(), profile => profile.IsActive);
        Assert.Equal(oldProfile.Summary.Id, active.Id);
        Assert.Equal("github", (await configurationService.LoadAsync()).SpotX_LyricsTheme);
        Assert.Equal("recommended", await recovered.ReadPreviousActiveProfileIdAsync());
        Assert.False(File.Exists(Path.Combine(root, "profile-activation.pending.json")));
    }

    [Fact]
    public async Task DesktopAndPowerShellHosts_SerializeSimultaneousActivations()
    {
        var firstConfiguration = AppCatalog.CreateRecommendedConfiguration();
        firstConfiguration.SpotX_LyricsTheme = "github";
        var powerShellConfiguration = AppCatalog.CreateRecommendedConfiguration();
        powerShellConfiguration.SpotX_LyricsTheme = "lavender";
        var desktopConfiguration = AppCatalog.CreateRecommendedConfiguration();
        desktopConfiguration.SpotX_LyricsTheme = "spotify";
        desktopConfiguration.SpotX_Premium = true;
        var first = await profileService.CreateFromConfigurationAsync("Cross Host First", "", firstConfiguration);
        var powerShell = await profileService.CreateFromConfigurationAsync("PowerShell", "", powerShellConfiguration);
        var desktop = await profileService.CreateFromConfigurationAsync("Desktop Final", "", desktopConfiguration);
        await profileService.ApplyProfileAsync(first.Summary.Id);

        var readyPath = Path.Combine(root, "powershell-ready");
        var releasePath = Path.Combine(root, "powershell-release");
        var harnessTask = RunPowerShellHarnessAsync("Apply", readyPath, releasePath);
        await WaitForFileAsync(readyPath, TimeSpan.FromSeconds(10));

        var desktopApply = new LocalProfileService(new ConfigurationService(root)).ApplyProfileAsync(desktop.Summary.Id);
        await Task.Delay(150);
        Assert.False(desktopApply.IsCompleted, "Desktop activation should wait while PowerShell owns the shared activation lock.");

        await File.WriteAllTextAsync(releasePath, "continue");
        var run = await harnessTask;
        Assert.True(run.ExitCode == 0, run.Details);
        await desktopApply;

        var finalService = new LocalProfileService(configurationService);
        var active = Assert.Single(await finalService.GetProfilesAsync(), profile => profile.IsActive);
        var finalConfiguration = await configurationService.LoadAsync();
        Assert.Equal(desktop.Summary.Id, active.Id);
        Assert.Equal("spotify", finalConfiguration.SpotX_LyricsTheme);
        Assert.True(finalConfiguration.SpotX_Premium);
        Assert.Equal(powerShell.Summary.Id, await finalService.ReadPreviousActiveProfileIdAsync());
        Assert.False(File.Exists(Path.Combine(root, "profile-activation.pending.json")));
    }

    private async Task<PowerShellRun> RunPowerShellHarnessAsync(
        string mode,
        string? readyPath = null,
        string? releasePath = null)
    {
        Directory.CreateDirectory(root);
        var harnessPath = Path.Combine(root, $"profile-harness-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(harnessPath, HarnessScript);
        var start = new ProcessStartInfo("powershell")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = ResolveRepoRoot()
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(harnessPath);
        start.ArgumentList.Add("-RepoRoot");
        start.ArgumentList.Add(ResolveRepoRoot());
        start.ArgumentList.Add("-Root");
        start.ArgumentList.Add(root);
        start.ArgumentList.Add("-Mode");
        start.ArgumentList.Add(mode);
        if (readyPath is not null)
        {
            start.ArgumentList.Add("-ReadyPath");
            start.ArgumentList.Add(readyPath);
        }
        if (releasePath is not null)
        {
            start.ArgumentList.Add("-ReleasePath");
            start.ArgumentList.Add(releasePath);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start PowerShell profile harness.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await process.WaitForExitAsync(timeout.Token);
        return new PowerShellRun(process.ExitCode, await stdout, await stderr);
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!File.Exists(path))
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for {path}.");
            }
            await Task.Delay(25);
        }
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LibreSpot.ps1")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }

    private sealed class SimulatedProcessTerminationException : Exception;

    private sealed record PowerShellRun(int ExitCode, string Stdout, string Stderr)
    {
        public string Details => $"PowerShell exited {ExitCode}\nSTDOUT:\n{Stdout}\nSTDERR:\n{Stderr}";
    }

    private const string HarnessScript = """
param(
    [Parameter(Mandatory = $true)][string]$RepoRoot,
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][ValidateSet('Apply','Recover')][string]$Mode,
    [string]$ReadyPath,
    [string]$ReleasePath
)

$ErrorActionPreference = 'Stop'
$global:CONFIG_DIR = $Root
$global:CONFIG_PATH = Join-Path $Root 'config.json'
$global:ACTIVE_PROFILE_PATH = Join-Path $Root 'active-profile.json'
$global:PREVIOUS_PROFILE_PATH = Join-Path $Root 'active-profile.previous.json'
$global:PROFILE_ACTIVATION_LOCK_PATH = Join-Path $Root 'profile-activation.lock'
$global:PROFILE_ACTIVATION_TRANSACTION_PATH = Join-Path $Root 'profile-activation.pending.json'

$sharedRoot = Join-Path $RepoRoot 'src\powershell\shared'
foreach ($name in @(
    'Get-FileSha256Lower',
    'ConvertTo-LibreSpotProfileId',
    'Enter-LibreSpotProfileActivationLock',
    'Write-LibreSpotFileDurable',
    'Copy-LibreSpotFileDurable',
    'Install-LibreSpotStagedConfig',
    'Write-LibreSpotJsonAtomically',
    'Get-LibreSpotFileFingerprint',
    'Read-LibreSpotProfilePointer',
    'Write-LibreSpotProfilePointer',
    'Test-LibreSpotProfileActivationTransaction',
    'Complete-LibreSpotProfileActivationTransaction',
    'Resolve-LibreSpotProfileActivationTransaction',
    'Apply-LibreSpotProfile'
)) {
    . (Join-Path $sharedRoot "$name.ps1")
}

if ($Mode -eq 'Recover') {
    $activationLock = Enter-LibreSpotProfileActivationLock
    try { Resolve-LibreSpotProfileActivationTransaction } finally { $activationLock.Dispose() }
    'recovered'
    exit 0
}

function Normalize-LibreSpotConfig {
    param([object]$Config)
    return @{ SpotX_LyricsTheme = [string]$Config.SpotX_LyricsTheme }
}
function Get-LibreSpotProfileById {
    param([string]$Id, [switch]$LockHeld)
    if ($Id -ne 'powershell') { return $null }
    return [pscustomobject]@{
        Id = 'powershell'
        Name = 'PowerShell'
        Configuration = [pscustomobject]@{ SpotX_LyricsTheme = 'lavender' }
    }
}

$originalResolve = ${function:Resolve-LibreSpotProfileActivationTransaction}
function Resolve-LibreSpotProfileActivationTransaction {
    [System.IO.File]::WriteAllText($ReadyPath, 'ready')
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $ReleasePath -PathType Leaf)) {
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Timed out waiting for the cross-host release signal.' }
        Start-Sleep -Milliseconds 25
    }
    & $originalResolve
}

Apply-LibreSpotProfile -Id 'powershell' | Out-Null
'applied'
""";
}
