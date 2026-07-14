using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace LibreSpot.Desktop.Tests;

public sealed class SpotXDefenderPolicyRegressionTests
{
    [Fact]
    public async Task ExternalScriptGate_BlocksDefenderMutationsUnlessSpotXOptOutIsDeclaredAndPassed()
    {
        var root = Path.Combine(Path.GetTempPath(), "LibreSpot.SpotX.Defender.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var harnessPath = Path.Combine(root, "spotx-defender-policy-harness.ps1");
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

            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start PowerShell Defender-policy harness.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var error = await stderr;
            Assert.True(process.ExitCode == 0, $"Harness exited {process.ExitCode}\nSTDOUT:\n{output}\nSTDERR:\n{error}");

            using var report = JsonDocument.Parse(output);
            var rootElement = report.RootElement;
            Assert.True(rootElement.GetProperty("safeAllowed").GetBoolean());
            Assert.False(rootElement.GetProperty("unsafeWithoutOptOutAllowed").GetBoolean());
            Assert.Contains("Refusing to run", rootElement.GetProperty("unsafeWithoutOptOutMessage").GetString());
            Assert.True(rootElement.GetProperty("unsafeWithOptOutAllowed").GetBoolean());
            Assert.False(rootElement.GetProperty("mutationWithoutDeclarationAllowed").GetBoolean());
            Assert.False(rootElement.GetProperty("nonSpotXMutationAllowed").GetBoolean());
            Assert.DoesNotContain("-defender_exclusions_off", rootElement.GetProperty("currentPinArguments").GetString());
            Assert.Contains("-defender_exclusions_off", rootElement.GetProperty("futurePinArguments").GetString());
            Assert.False(rootElement.GetProperty("invalidAdapterAllowed").GetBoolean());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
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

    private const string HarnessScript = """
param(
    [Parameter(Mandatory = $true)][string]$RepoRoot,
    [Parameter(Mandatory = $true)][string]$Root
)

$ErrorActionPreference = 'Stop'
$sharedRoot = Join-Path $RepoRoot 'src\powershell\shared'
. (Join-Path $sharedRoot 'Assert-LibreSpotExternalScriptDefenderPolicy.ps1')
. (Join-Path $sharedRoot 'Open-VerifiedScriptForExecution.ps1')
. (Join-Path $sharedRoot 'Build-SpotXParams.ps1')

function Set-TestScript {
    param([string]$Name, [string]$Content)
    $path = Join-Path $Root $Name
    [System.IO.File]::WriteAllText($path, $Content, (New-Object System.Text.UTF8Encoding($false)))
    return $path
}

function Test-Open {
    param([string]$Path, [string]$Arguments, [string]$Label)
    try {
        $guard = Open-VerifiedScriptForExecution -FilePath $Path -Label $Label -Arguments $Arguments
        $guard.Dispose()
        return [pscustomobject]@{ allowed = $true; message = '' }
    } catch {
        return [pscustomobject]@{ allowed = $false; message = $_.Exception.Message }
    }
}

$safe = Set-TestScript -Name 'safe.ps1' -Content "param()`n'no Defender changes'"
$unsafe = Set-TestScript -Name 'unsafe.ps1' -Content @'
param([switch]$defender_exclusions_off)
if (-not $defender_exclusions_off) {
    Add-MpPreference -ExclusionPath 'C:\Spotify'
    Set-MpPreference -ExclusionProcess 'powershell.exe'
}
'@
$undeclared = Set-TestScript -Name 'undeclared.ps1' -Content "Add-MpPreference -ExclusionPath 'C:\Spotify'"

$safeResult = Test-Open -Path $safe -Arguments '' -Label 'SpotX run.ps1'
$unsafeWithout = Test-Open -Path $unsafe -Arguments '' -Label 'SpotX run.ps1'
$unsafeWith = Test-Open -Path $unsafe -Arguments '-defender_exclusions_off' -Label 'SpotX run.ps1'
$undeclaredResult = Test-Open -Path $undeclared -Arguments '-defender_exclusions_off' -Label 'SpotX run.ps1'
$nonSpotXResult = Test-Open -Path $unsafe -Arguments '-defender_exclusions_off' -Label 'community installer'

$config = [pscustomobject]@{
    SpotX_SpotifyVersionId = 'auto'
    SpotX_DownloadMethod = ''
    SpotX_Language = ''
    SpotX_LyricsTheme = 'spotify'
}
$global:SpotifyVersionManifest = @()
$global:PinnedReleases = @{ SpotX = @{ DefenderMutations = $false; DefenderOptOut = '' } }
$currentPinArguments = Build-SpotXParams -Config $config
$global:PinnedReleases.SpotX.DefenderMutations = $true
$global:PinnedReleases.SpotX.DefenderOptOut = '-defender_exclusions_off'
$futurePinArguments = Build-SpotXParams -Config $config
$global:PinnedReleases.SpotX.DefenderOptOut = ''
try {
    $null = Build-SpotXParams -Config $config
    $invalidAdapterAllowed = $true
} catch {
    $invalidAdapterAllowed = $false
}

[ordered]@{
    safeAllowed = $safeResult.allowed
    unsafeWithoutOptOutAllowed = $unsafeWithout.allowed
    unsafeWithoutOptOutMessage = $unsafeWithout.message
    unsafeWithOptOutAllowed = $unsafeWith.allowed
    mutationWithoutDeclarationAllowed = $undeclaredResult.allowed
    nonSpotXMutationAllowed = $nonSpotXResult.allowed
    currentPinArguments = $currentPinArguments
    futurePinArguments = $futurePinArguments
    invalidAdapterAllowed = $invalidAdapterAllowed
} | ConvertTo-Json -Depth 4
""";
}
