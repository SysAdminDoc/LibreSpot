# LibreSpot - Comprehensive SpotX + Spicetify Installer
# Easy Mode | Custom Mode | Maintenance Mode
#
# All-in-one installer for SpotX ad-blocking/patching and Spicetify
# themes, extensions, and Marketplace with full GUI configuration.
#
# Credits:
#   SpotX       - github.com/SpotX-Official/SpotX
#   Spicetify   - github.com/spicetify
#   Marketplace - github.com/spicetify/marketplace
#   Themes      - github.com/spicetify/spicetify-themes
#   ohitstom    - github.com/ohitstom/spicetify-extensions

# =============================================================================
# 1. INITIAL SETUP
# =============================================================================
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.IO.Compression.FileSystem

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
    public const int SW_HIDE = 0;
    public const int SW_MINIMIZE = 6;
    [StructLayout(LayoutKind.Sequential)]
    public struct FLASHWINFO {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }
    public const uint FLASHW_ALL = 3;
    public const uint FLASHW_TIMERNOFG = 12;
    public static void FlashTaskbar(IntPtr hwnd) {
        FLASHWINFO fw = new FLASHWINFO();
        fw.cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO));
        fw.hwnd = hwnd;
        fw.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
        fw.uCount = 5;
        fw.dwTimeout = 0;
        FlashWindowEx(ref fw);
    }
}
'@ -ErrorAction SilentlyContinue

$ErrorActionPreference = 'Stop'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch {}

$global:VERSION = '3.6.0'


# CLI argument detection. Supports `irm URL | iex -clean` (PowerShell passes
# trailing args to `iex` as $args inside the invoked script) and also
# `powershell.exe -File LibreSpot.ps1 -clean` via the same $args.
#
# Recognized flags:
#   -clean              Pre-tick Easy mode + CleanInstall for a one-shot rebuild.
#   -watch              Headless auto-reapply check. No UI. Scheduled task uses this.
#   -installwatcher     Register the scheduled task that calls `-watch` and exit.
#   -uninstallwatcher   Remove that scheduled task and exit.
$script:CliClean           = $false
$script:CliWatch           = $false
$script:CliInstallWatcher  = $false
$script:CliUninstallWatcher = $false
try {
    if ($args -and $args.Count -gt 0) {
        foreach ($a in $args) {
            switch -Regex ([string]$a) {
                '^-{1,2}clean$'              { $script:CliClean = $true }
                '^-{1,2}watch$'              { $script:CliWatch = $true }
                '^-{1,2}installwatcher$'     { $script:CliInstallWatcher = $true }
                '^-{1,2}uninstallwatcher$'   { $script:CliUninstallWatcher = $true }
            }
        }
    }
} catch {}

# --- Pinned dependency versions with SHA256 verification ---
# Update these when new versions are tested. Use Maintenance > Check for Updates.
$global:PinnedReleases = @{
    SpotX = @{
        Version = '2.0'
        Commit  = '0abf98a36be501740d774a56d54d5f7fbbafc35c'
        Url     = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/0abf98a36be501740d774a56d54d5f7fbbafc35c/run.ps1'
        SHA256  = '38d4205a2afc2050781bbfe28c6713edd6b0aef2c084304b58d92308b081f569'
    }
    SpicetifyCLI = @{
        Version = '2.43.1'
        SHA256  = @{
            x64   = 'c9b5e677d5b3046d14da09a3f713bd7b864b67b0c4c4b7ea2ab53c261e63b491'
            arm64 = '4cc793a947678ededaa244899c216d60230f535cb8ccaadf683e99c4ae741e13'
        }
    }
    Marketplace = @{
        Version = '1.0.8'
        Url     = 'https://github.com/spicetify/marketplace/releases/download/v1.0.8/marketplace.zip'
        SHA256  = 'ba20cd30896605ec60c272905004673b995162d2c8ca085351971e409cf80ec7'
    }
    Themes = @{
        Commit  = '9af41cf91af6f6093c0e060d57264f08f6bb161c'
        SHA256  = 'fd55e443e88302dfd45e201f35ec67db5f51c4346b58fab5da90faf7b1a66f28'
    }
}

# Computed URLs (derived from pinned versions, do not edit directly)
$global:URL_SPOTX         = $global:PinnedReleases.SpotX.Url
$global:URL_MARKETPLACE   = $global:PinnedReleases.Marketplace.Url
$global:URL_THEMES_REPO   = "https://github.com/spicetify/spicetify-themes/archive/$($global:PinnedReleases.Themes.Commit).zip"
$global:URL_SPICETIFY_FMT = 'https://github.com/spicetify/cli/releases/download/v{0}/spicetify-{0}-windows-{1}.zip'

$global:TEMP_DIR               = $env:TEMP
$global:SPOTIFY_EXE_PATH       = "$env:APPDATA\Spotify\Spotify.exe"
$global:SPICETIFY_DIR          = "$env:LOCALAPPDATA\spicetify"
$global:SPICETIFY_CONFIG_DIR   = "$env:APPDATA\spicetify"
$global:BACKUP_ROOT            = "$env:USERPROFILE\LibreSpot_Backups"
$global:CONFIG_DIR             = "$env:APPDATA\LibreSpot"
$global:CONFIG_PATH            = "$env:APPDATA\LibreSpot\config.json"
$global:LOG_PATH               = "$env:APPDATA\LibreSpot\install.log"
$global:WATCHER_STATE_PATH     = "$env:APPDATA\LibreSpot\watcher-state.json"
$global:WATCHER_LOG_PATH       = "$env:APPDATA\LibreSpot\watcher.log"
$global:WATCHER_TASK_NAME      = 'LibreSpot\ReapplyWatcher'

$global:BrushGreen = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FF22c55e"))
$global:BrushRed   = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFef4444"))
$global:BrushMuted = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFa1a1aa"))
$global:BrushError = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFf87171"))
foreach ($b in @($global:BrushGreen,$global:BrushRed,$global:BrushMuted,$global:BrushError)) { $b.Freeze() }

$script:openRunspaces = [System.Collections.Generic.List[object]]::new()
$script:BrushConverter = [System.Windows.Media.BrushConverter]::new()
$script:EntryInvocation = $MyInvocation
$script:EntryCommandPath = $PSCommandPath
$script:ConfigLoadWarning = $null

# =============================================================================
# 1b. AUTO-REAPPLY WATCHER (Track 4.2)
# =============================================================================
# Headless mode invoked by a scheduled task that detects Spotify.exe version
# bumps and re-runs the saved SpotX config. No UI is loaded in this path —
# anything requiring $window, $ui, or a WPF dispatcher is off-limits.

function Write-WatcherLog {
    param([string]$Message, [string]$Level = 'INFO')
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        $line = "[{0}] [{1}] {2}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::AppendAllText($global:WATCHER_LOG_PATH, $line + [Environment]::NewLine, $utf8NoBom)
        # Trim the watcher log when it exceeds ~1 MB so an unattended machine
        # can't fill the disk with 15-minute polling entries.
        if ((Get-Item -LiteralPath $global:WATCHER_LOG_PATH).Length -gt 1048576) {
            $keep = Get-Content -LiteralPath $global:WATCHER_LOG_PATH -Tail 500
            [System.IO.File]::WriteAllLines($global:WATCHER_LOG_PATH, $keep, $utf8NoBom)
        }
    } catch {}
}

function Get-WatcherState {
    if (-not (Test-Path -LiteralPath $global:WATCHER_STATE_PATH)) {
        return @{ LastKnownVersion = $null; LastRunAt = $null; LastOutcome = $null }
    }
    try {
        $raw = Get-Content -LiteralPath $global:WATCHER_STATE_PATH -Raw -ErrorAction Stop | ConvertFrom-Json
        return @{
            LastKnownVersion = [string]$raw.LastKnownVersion
            LastRunAt        = [string]$raw.LastRunAt
            LastOutcome      = [string]$raw.LastOutcome
        }
    } catch {
        return @{ LastKnownVersion = $null; LastRunAt = $null; LastOutcome = $null }
    }
}

function Set-WatcherState {
    param([hashtable]$State)
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        $State | ConvertTo-Json -Compress | Set-Content -LiteralPath $global:WATCHER_STATE_PATH -Encoding UTF8
    } catch {
        Write-WatcherLog "State save failed: $($_.Exception.Message)" -Level 'WARN'
    }
}

function Get-InstalledSpotifyVersion {
    if (-not (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) { return $null }
    try { return (Get-Item -LiteralPath $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion }
    catch { return $null }
}

function Test-SpotifyRunning {
    try { return [bool](Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue) }
    catch { return $false }
}

function Get-WatcherLaunchCommand {
    # Returns a [string[]]{ FileName, ArgumentList... } suitable for schtasks.exe's
    # /TR value. Prefers the compiled LibreSpot.exe when the user launched from it;
    # falls back to powershell.exe + -File when launched from the raw .ps1. Returns
    # $null when neither path is usable (e.g. `irm | iex`) so the caller can surface
    # a helpful error instead of registering a broken task.
    $entry = [string]$script:EntryCommandPath
    if ([string]::IsNullOrWhiteSpace($entry)) { return $null }
    if (-not (Test-Path -LiteralPath $entry)) { return $null }

    $ext = [System.IO.Path]::GetExtension($entry).ToLowerInvariant()
    if ($ext -eq '.exe') {
        return @{ Command = "`"$entry`" -Watch"; Entry = $entry }
    }
    if ($ext -eq '.ps1') {
        $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if (-not (Test-Path -LiteralPath $ps)) { $ps = 'powershell.exe' }
        return @{ Command = "`"$ps`" -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$entry`" -Watch"; Entry = $entry }
    }
    return $null
}

function Test-AutoReapplyTaskRegistered {
    try {
        $out = & schtasks.exe /Query /TN $global:WATCHER_TASK_NAME 2>$null
        return ($LASTEXITCODE -eq 0) -and ($out -and $out.Length -gt 0)
    } catch { return $false }
}

function Register-AutoReapplyTask {
    # Creates a per-user scheduled task that fires at logon, then again every
    # 30 minutes, invoking LibreSpot in -Watch mode. Returns $true on success.
    $launch = Get-WatcherLaunchCommand
    if (-not $launch) {
        Write-WatcherLog 'Register: no usable LibreSpot entry path (iex launch?). Watcher not registered.' -Level 'ERROR'
        return $false
    }

    # Unregister first so we don't get "task already exists" failures when the
    # user toggles the setting. schtasks /Create /F also overwrites, but the
    # explicit delete keeps the semantics obvious.
    try { Unregister-AutoReapplyTask | Out-Null } catch {}

    # Build an inline XML task definition. schtasks.exe's flag syntax can't
    # express "logon trigger + repetition every 30 minutes for 1 day" cleanly,
    # but the XML schema can. Repetition Duration=PT0S means "forever" per
    # MS-TSCH 2.3.5.2; Interval=PT30M is every 30 minutes.
    $escapedCommand = [System.Security.SecurityElement]::Escape($launch.Command)
    $userId = [System.Security.SecurityElement]::Escape($env:USERNAME)
    $xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>LibreSpot</Author>
    <Description>LibreSpot reapplies SpotX automatically when Spotify updates itself. Toggle from Maintenance inside the app.</Description>
    <URI>\LibreSpot\ReapplyWatcher</URI>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT2M</Delay>
      <Repetition>
        <Interval>PT30M</Interval>
        <Duration>PT0S</Duration>
        <StopAtDurationEnd>false</StopAtDurationEnd>
      </Repetition>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>$userId</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT30M</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$escapedCommand</Command>
    </Exec>
  </Actions>
</Task>
"@

    $xmlPath = Join-Path $global:CONFIG_DIR "watcher-task.xml"
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        # schtasks /Create /XML requires UTF-16 LE with BOM to match the XML header.
        [System.IO.File]::WriteAllText($xmlPath, $xml, [System.Text.Encoding]::Unicode)

        $output = & schtasks.exe /Create /TN $global:WATCHER_TASK_NAME /XML $xmlPath /F 2>&1
        $ok = ($LASTEXITCODE -eq 0)
        if ($ok) {
            Write-WatcherLog "Register: scheduled task created for $($launch.Entry)"
        } else {
            Write-WatcherLog "Register failed (exit $LASTEXITCODE): $($output -join ' ')" -Level 'ERROR'
        }
        return $ok
    } catch {
        Write-WatcherLog "Register exception: $($_.Exception.Message)" -Level 'ERROR'
        return $false
    } finally {
        try { if (Test-Path -LiteralPath $xmlPath) { Remove-Item -LiteralPath $xmlPath -Force -ErrorAction SilentlyContinue } } catch {}
    }
}

function Unregister-AutoReapplyTask {
    try {
        $null = & schtasks.exe /Delete /TN $global:WATCHER_TASK_NAME /F 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-WatcherLog "Unregister: scheduled task removed"
            return $true
        }
        return $false
    } catch { return $false }
}

function Invoke-HeadlessReapply {
    # Minimal reapply pipeline — runs SpotX synchronously with the saved config
    # and reapplies Spicetify if the CLI is present. Intentionally does NOT use
    # any UI / runspace plumbing. Caller runs on the main thread from -Watch.
    param([hashtable]$Config)
    if (-not $Config) { throw 'Invoke-HeadlessReapply: missing config' }

    $tempDir = Join-Path $global:TEMP_DIR ("LibreSpot_Watcher_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    try {
        $spotxRun = Join-Path $tempDir 'spotx_run.ps1'

        # Download + hash-verify SpotX. We DON'T fall back to BITS here because
        # the watcher runs unattended and we'd rather silently skip than use a
        # different download backend than the user-triggered install path.
        Write-WatcherLog "Downloading SpotX run.ps1"
        Invoke-WebRequest -Uri $global:URL_SPOTX -OutFile $spotxRun -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
        $actualHash = (Get-FileHash -LiteralPath $spotxRun -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedHash = [string]$global:PinnedReleases.SpotX.SHA256
        if ($actualHash -ne $expectedHash.ToLowerInvariant()) {
            throw "SpotX hash mismatch. Expected $expectedHash, got $actualHash. Refusing to run."
        }

        $spotxArgs = Build-SpotXParams -Config $Config
        Write-WatcherLog "Invoking SpotX with: $spotxArgs"
        $spotxArgList = $spotxArgs -split ' '

        # Use powershell.exe isolation so SpotX can't leak runtime state into our
        # own script scope. Exit code is the only signal we care about.
        $psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if (-not (Test-Path -LiteralPath $psExe)) { $psExe = 'powershell.exe' }
        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = $psExe
        $pinfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$spotxRun`" $spotxArgs"
        $pinfo.RedirectStandardOutput = $true
        $pinfo.RedirectStandardError  = $true
        $pinfo.UseShellExecute = $false
        $pinfo.CreateNoWindow = $true
        $proc = [System.Diagnostics.Process]::Start($pinfo)
        if (-not $proc.WaitForExit(20 * 60 * 1000)) {
            try { $proc.Kill() } catch {}
            throw "SpotX timed out after 20 minutes."
        }
        if ($proc.ExitCode -ne 0) {
            throw "SpotX exited with code $($proc.ExitCode)."
        }
        Write-WatcherLog "SpotX completed successfully" -Level 'SUCCESS'

        # Reapply Spicetify when it's installed. Missing CLI is fine — it just
        # means the user only patches with SpotX and that part is already done.
        $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
        if (Test-Path -LiteralPath $spicetifyExe) {
            try {
                & $spicetifyExe 'backup' 'apply' 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-WatcherLog "Spicetify reapplied" -Level 'SUCCESS'
                } else {
                    Write-WatcherLog "Spicetify apply exited $LASTEXITCODE" -Level 'WARN'
                }
            } catch {
                Write-WatcherLog "Spicetify apply failed: $($_.Exception.Message)" -Level 'WARN'
            }
        }
    } finally {
        try { Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Invoke-AutoReapplyWatcher {
    # -Watch entry point. Returns an exit code to satisfy schtasks reporting.
    Write-WatcherLog "--- Watcher tick ---"

    $currentVersion = Get-InstalledSpotifyVersion
    if (-not $currentVersion) {
        Write-WatcherLog "Spotify not installed — skipping."
        return 0
    }

    $state = Get-WatcherState

    # First-ever run: record the version and do nothing. Reapplying on the
    # first tick would clobber a freshly-installed unconfigured Spotify.
    if (-not $state.LastKnownVersion) {
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Initialized' }
        Write-WatcherLog "Initialized last-known version to $currentVersion (no reapply this tick)"
        return 0
    }

    if ($currentVersion -eq $state.LastKnownVersion) {
        Write-WatcherLog "Spotify still at $currentVersion — nothing to do"
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'UpToDate' }
        return 0
    }

    Write-WatcherLog "Spotify version bump: $($state.LastKnownVersion) -> $currentVersion" -Level 'STEP'

    if (Test-SpotifyRunning) {
        Write-WatcherLog "Spotify is running — deferring reapply to next tick"
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'DeferredSpotifyRunning' }
        return 0
    }

    $saved = $null
    try { $saved = Load-LibreSpotConfig } catch { Write-WatcherLog "Config load failed: $($_.Exception.Message)" -Level 'ERROR' }
    if (-not $saved) {
        Write-WatcherLog "No saved LibreSpot config — cannot reapply automatically" -Level 'WARN'
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'NoConfig' }
        return 0
    }
    $saved = Normalize-LibreSpotConfig -Config $saved

    try {
        Invoke-HeadlessReapply -Config $saved
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'Reapplied' }
        return 0
    } catch {
        Write-WatcherLog "Reapply failed: $($_.Exception.Message)" -Level 'ERROR'
        # Keep LastKnownVersion unchanged so we'll retry next tick.
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = "Error: $($_.Exception.Message)" }
        return 1
    }
}

# -InstallWatcher / -UninstallWatcher don't depend on Build-SpotXParams or the
# config pipeline, so they can exit immediately. The -Watch entry point runs
# AFTER Build-SpotXParams is defined (search for "CliWatch" later in this file).
if ($script:CliInstallWatcher) {
    Write-WatcherLog "CLI: -installwatcher"
    $ok = Register-AutoReapplyTask
    if ($ok) {
        Write-Host "LibreSpot auto-reapply watcher registered."
        exit 0
    }
    Write-Warning "LibreSpot watcher registration failed; see $($global:WATCHER_LOG_PATH)."
    exit 1
}

if ($script:CliUninstallWatcher) {
    Write-WatcherLog "CLI: -uninstallwatcher"
    $ok = Unregister-AutoReapplyTask
    if ($ok) { Write-Host "LibreSpot auto-reapply watcher removed." } else { Write-Host "LibreSpot watcher was not registered." }
    exit 0
}

# =============================================================================
# 2. ADMIN CHECK
# =============================================================================
function Get-SelfElevationLaunchTarget {
    $pathCandidates = @(
        $script:EntryCommandPath,
        $script:EntryInvocation.MyCommand.Path,
        $script:EntryInvocation.ScriptName
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

    foreach ($candidate in $pathCandidates) {
        try {
            if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
            $resolvedPath = (Resolve-Path -LiteralPath $candidate).Path
            $extension = [System.IO.Path]::GetExtension($resolvedPath)
            if ($extension -ieq '.ps1') { return @{ Kind = 'Script'; Path = $resolvedPath } }
            if ($extension -ieq '.exe') { return @{ Kind = 'Exe'; Path = $resolvedPath } }
        } catch {}
    }

    try {
        $processPath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
        $processName = [System.IO.Path]::GetFileName($processPath)
        if (
            -not [string]::IsNullOrWhiteSpace($processPath) -and
            (Test-Path -LiteralPath $processPath -PathType Leaf) -and
            [System.IO.Path]::GetExtension($processPath) -ieq '.exe' -and
            $processName -notin @('powershell.exe', 'pwsh.exe', 'powershell_ise.exe')
        ) {
            return @{ Kind = 'Exe'; Path = (Resolve-Path -LiteralPath $processPath).Path }
        }
    } catch {}

    $inlineSource = $null
    try {
        if ($script:EntryInvocation.MyCommand.ScriptBlock) {
            $inlineSource = $script:EntryInvocation.MyCommand.ScriptBlock.ToString()
        }
    } catch {}

    if ([string]::IsNullOrWhiteSpace($inlineSource)) {
        try {
            $definition = [string]$script:EntryInvocation.MyCommand.Definition
            if (
                -not [string]::IsNullOrWhiteSpace($definition) -and
                -not (Test-Path -LiteralPath $definition -PathType Leaf)
            ) {
                $inlineSource = $definition
            }
        } catch {}
    }

    if (-not [string]::IsNullOrWhiteSpace($inlineSource)) {
        try {
            $bootstrapDir = Join-Path $env:TEMP 'LibreSpot'
            if (-not (Test-Path -LiteralPath $bootstrapDir)) {
                New-Item -Path $bootstrapDir -ItemType Directory -Force | Out-Null
            }
            $bootstrapPath = Join-Path $bootstrapDir 'LibreSpot-elevated.ps1'
            Set-Content -Path $bootstrapPath -Value $inlineSource -Encoding UTF8 -Force
            return @{ Kind = 'Script'; Path = $bootstrapPath; IsTemp = $true }
        } catch {}
    }

    return $null
}

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $launchTarget = Get-SelfElevationLaunchTarget
    if ($launchTarget) {
        try {
            $workingDir = Split-Path -Path $launchTarget.Path -Parent
            if ($launchTarget.Kind -eq 'Exe') {
                Start-Process -FilePath $launchTarget.Path -Verb RunAs -WorkingDirectory $workingDir
            } else {
                Start-Process -FilePath 'powershell.exe' -ArgumentList @(
                    '-NoProfile',
                    '-ExecutionPolicy', 'Bypass',
                    '-File', $launchTarget.Path
                ) -Verb RunAs -WorkingDirectory $workingDir
            }
        } catch {
            [System.Windows.MessageBox]::Show(
                "LibreSpot needs administrator permission to modify Spotify.`n`nApprove the Windows prompt to continue. If it was dismissed, just launch LibreSpot again.",
                'LibreSpot',
                [System.Windows.MessageBoxButton]::OK,
                [System.Windows.MessageBoxImage]::Warning
            ) | Out-Null
        }
    } else {
        [System.Windows.MessageBox]::Show(
            "LibreSpot could not determine a reusable launch path for self-elevation.`n`nRun the saved LibreSpot.ps1 file or download the latest release and try again.",
            'LibreSpot',
            [System.Windows.MessageBoxButton]::OK,
            [System.Windows.MessageBoxImage]::Warning
        ) | Out-Null
    }
    Exit
}

# =============================================================================
# 3. DATA
# =============================================================================
$global:THEMES_RAW_BASE = "https://raw.githubusercontent.com/spicetify/spicetify-themes/$($global:PinnedReleases.Themes.Commit)"
$global:ThemeData = [ordered]@{
    "(None - Marketplace Only)" = @{ Schemes = @("Default"); Preview = @{} }
    "Sleek"       = @{ Schemes = @("Wealthy","Cherry","Coral","Deep","Greener","Deeper","Psycho","UltraBlack","Nord","Futura","Elementary","BladeRunner","Dracula","VantaBlack","RosePine","Eldritch","Catppuccin","AyuDark","TokyoNight")
                       Preview = @{ _default="Sleek/catppuccin.png"; "BladeRunner"="Sleek/bladerunner.png"; "AyuDark"="Sleek/ayudark.png"; "Catppuccin"="Sleek/catppuccin.png" } }
    "Dribbblish"  = @{ Schemes = @("base","white","dark","dracula","nord-light","nord-dark","purple","samurai","beach-sunset","gruvbox","gruvbox-material-dark","rosepine","lunar","catppuccin-latte","catppuccin-frappe","catppuccin-macchiato","catppuccin-mocha","tokyo-night","kanagawa")
                       Preview = @{ _default="Dribbblish/base.png"; "base"="Dribbblish/base.png"; "beach-sunset"="Dribbblish/beach-sunset.png"; "catppuccin-frappe"="Dribbblish/catppuccin-frappe.png" } }
    "Ziro"        = @{ Schemes = @("blue-dark","blue-light","gray-dark","gray-light","green-dark","green-light","orange-dark","orange-light","purple-dark","purple-light","red-dark","red-light","rose-pine","rose-pine-moon","rose-pine-dawn","tokyo-night")
                       Preview = @{ _default="Ziro/screenshots/rose-pine.jpg"; "rose-pine"="Ziro/screenshots/rose-pine.jpg"; "rose-pine-moon"="Ziro/screenshots/rose-pine-moon.jpg"; "rose-pine-dawn"="Ziro/screenshots/rose-pine-dawn.jpg" } }
    "text"        = @{ Schemes = @("Spotify","Spicetify","CatppuccinMocha","CatppuccinMacchiato","CatppuccinLatte","Dracula","Gruvbox","Kanagawa","Nord","Rigel","RosePine","RosePineMoon","RosePineDawn","Solarized","TokyoNight","TokyoNightStorm","ForestGreen","EverforestDarkHard","EverforestDarkMedium","EverforestDarkSoft")
                       Preview = @{ _default="text/screenshots/Spotify.png" } }
    "StarryNight" = @{ Schemes = @("Base","Cotton-candy","Forest","Galaxy","Orange","Sky","Sunrise")
                       Preview = @{ _default="StarryNight/images/base.png"; "Base"="StarryNight/images/base.png"; "Cotton-candy"="StarryNight/images/cotton-candy.png"; "Forest"="StarryNight/images/forest.png"; "Galaxy"="StarryNight/images/galaxy.png"; "Orange"="StarryNight/images/orange.png" } }
    "Turntable"   = @{ Schemes = @("turntable"); Preview = @{ _default="Turntable/screenshots/turntable.png" } }
    "Blackout"    = @{ Schemes = @("def"); Preview = @{ _default="Blackout/images/home.png" } }
    "Blossom"     = @{ Schemes = @("dark"); Preview = @{ _default="Blossom/images/home.png" } }
    "BurntSienna" = @{ Schemes = @("Base"); Preview = @{ _default="BurntSienna/screenshot.png" } }
    "Default"     = @{ Schemes = @("Ocean"); Preview = @{ _default="Default/ocean.png" } }
    "Dreary"      = @{ Schemes = @("Psycho","Deeper","BIB","Mono","Golden","Graytone-Blue")
                       Preview = @{ _default="Dreary/deeper.png"; "Deeper"="Dreary/deeper.png"; "BIB"="Dreary/bib.png"; "Golden"="Dreary/golden.png" } }
    "Flow"        = @{ Schemes = @("Pink","Green","Silver","Violet","Ocean")
                       Preview = @{ _default="Flow/screenshots/ocean.png"; "Pink"="Flow/screenshots/pink.png"; "Silver"="Flow/screenshots/silver.png"; "Violet"="Flow/screenshots/violet.png"; "Ocean"="Flow/screenshots/ocean.png" } }
    "Matte"       = @{ Schemes = @("matte","periwinkle","periwinkle-dark","porcelain","rose-pine-moon","gray-dark1","gray-dark2","gray-dark3","gray","gray-light")
                       Preview = @{ _default="Matte/screenshots/ylx-gray-dark1.png" } }
    "Nightlight"  = @{ Schemes = @("Nightlight Colors"); Preview = @{ _default="Nightlight/screenshots/nightlight.png" } }
    "Onepunch"    = @{ Schemes = @("dark","light","legacy"); Preview = @{ _default="Onepunch/screenshots/dark_home.png" } }
    "SharkBlue"   = @{ Schemes = @("Base"); Preview = @{ _default="SharkBlue/screenshot.png" } }
}

$global:BuiltInExtensions = [ordered]@{
    "fullAppDisplay.js"    = "Full-screen album art display with blur effect and playback controls"
    "shuffle+.js"          = "True shuffle using Fisher-Yates algorithm instead of Spotify weighted shuffle"
    "trashbin.js"          = "Automatically skip songs and artists you have marked as unwanted"
    "keyboardShortcut.js"  = "Vim-style keyboard navigation bindings for power users"
    "bookmark.js"          = "Save and instantly recall pages, tracks, albums, and timestamps"
    "loopyLoop.js"         = "Set A-B loop points on any track for practice or replay"
    "popupLyrics.js"       = "Display synchronized lyrics in a separate resizable window"
    "autoSkipVideo.js"     = "Automatically skip canvas videos and region-locked content"
    "autoSkipExplicit.js"  = "Automatically skip tracks marked as explicit"
    "webnowplaying.js"     = "Expose now-playing data for Rainmeter widgets and desktop integrations"
}

$global:EasyDefaults = @{
    SpotX_NewTheme=$true; SpotX_PodcastsOff=$true; SpotX_BlockUpdate=$true; SpotX_AdSectionsOff=$true
    SpotX_Premium=$false; SpotX_LyricsEnabled=$true; SpotX_LyricsTheme="spotify"
    SpotX_TopSearch=$false; SpotX_RightSidebarOff=$false; SpotX_RightSidebarClr=$false
    SpotX_CanvasHomeOff=$false; SpotX_HomeSubOff=$false; SpotX_DisableStartup=$true; SpotX_NoShortcut=$false; SpotX_CacheLimit=0
    SpotX_Plus=$false; SpotX_NewFullscreen=$false; SpotX_FunnyProgress=$false; SpotX_ExpSpotify=$false; SpotX_LyricsBlock=$false
    SpotX_SendVersionOff=$true; SpotX_StartSpoti=$false
    SpotX_DevTools=$false; SpotX_Mirror=$false; SpotX_DownloadMethod=""; SpotX_ConfirmUninstall=$false
    SpotX_SpotifyVersionId="auto"
    Spicetify_Theme="(None - Marketplace Only)"; Spicetify_Scheme="Default"; Spicetify_Marketplace=$true
    Spicetify_Extensions=@("fullAppDisplay.js","shuffle+.js","trashbin.js")
    CleanInstall=$true; LaunchAfter=$true
    # Auto-reapply after Spotify updates (Track 4.2). Off by default — we won't
    # register a scheduled task until the user explicitly opts in from Maintenance.
    AutoReapply_Enabled=$false
}

$global:SpotXLyricsThemes = @(
    'spotify','blueberry','blue','discord','forest','fresh','github','lavender',
    'orange','pumpkin','purple','red','strawberry','turquoise','yellow','oceano',
    'royal','krux','pinkle','zing','radium','sandbar','postlight','relish',
    'drot','default','spotify#2'
)

# Curated manifest of Spotify client versions SpotX currently knows how to
# patch cleanly. `Version = ''` means "let SpotX pick the default". Keep this
# list tight — every entry is an explicit compatibility promise.
$global:SpotifyVersionManifest = @(
    @{ Id='auto';            Label='Auto (use SpotX default)';         Version='';                        Notes='Recommended. Lets SpotX pick the most compatible build.' }
    @{ Id='1.2.86.502';      Label='1.2.86.502 (current pinned)';      Version='1.2.86.502.g8cd7fb22';    Notes='Best match for our pinned SpotX commit.' }
    @{ Id='1.2.85.519';      Label='1.2.85.519 (previous stable)';     Version='1.2.85.519.g7c42e2e8';    Notes='Last Windows release before Canvas-home changes.' }
    @{ Id='1.2.53.440.x86';  Label='1.2.53.440 (x86 / 32-bit only)';   Version='1.2.53.440.g7b2f582a';    Notes='For 32-bit Windows. Do not pick on x64.' }
    @{ Id='1.2.5.1006.win7'; Label='1.2.5.1006 (Windows 7 / 8.1)';     Version='1.2.5.1006.g22820f93';    Notes='Last build supported on legacy Windows.' }
)
$global:SpotifyVersionIds = @($global:SpotifyVersionManifest | ForEach-Object { $_.Id })

# =============================================================================
# 4. SETTINGS PERSISTENCE
# =============================================================================
function ConvertTo-PlainHashtable {
    param([object]$InputObject)
    $result = @{}
    if ($null -eq $InputObject) { return $result }
    if ($InputObject -is [hashtable]) {
        foreach ($key in $InputObject.Keys) { $result[[string]$key] = $InputObject[$key] }
        return $result
    }
    foreach ($property in $InputObject.PSObject.Properties) {
        if ($property.Value -is [System.Collections.IEnumerable] -and $property.Value -isnot [string]) {
            $result[$property.Name] = @($property.Value)
        } else {
            $result[$property.Name] = $property.Value
        }
    }
    return $result
}

function ConvertTo-ConfigBoolean {
    param([object]$Value, [bool]$Default = $false)
    if ($null -eq $Value) { return $Default }
    if ($Value -is [bool]) { return [bool]$Value }
    if ($Value -is [int] -or $Value -is [long]) { return ([int64]$Value -ne 0) }
    $text = ([string]$Value).Trim().ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($text)) { return $Default }
    switch -Regex ($text) {
        '^(1|true|yes|on)$' { return $true }
        '^(0|false|no|off)$' { return $false }
        default { return $Default }
    }
}

function ConvertTo-ConfigInt {
    param(
        [object]$Value,
        [int]$Default = 0,
        [int]$Minimum = [int]::MinValue,
        [int]$Maximum = [int]::MaxValue
    )
    $parsed = 0
    if ($null -eq $Value -or -not [int]::TryParse([string]$Value, [ref]$parsed)) {
        $parsed = $Default
    }
    if ($parsed -lt $Minimum) { $parsed = $Minimum }
    if ($parsed -gt $Maximum) { $parsed = $Maximum }
    return $parsed
}

function Normalize-LibreSpotConfig {
    param([hashtable]$Config)

    $normalized = @{ Mode = 'Easy' }
    foreach ($key in $global:EasyDefaults.Keys) {
        $defaultValue = $global:EasyDefaults[$key]
        if ($defaultValue -is [System.Collections.IEnumerable] -and $defaultValue -isnot [string]) {
            $normalized[$key] = @($defaultValue)
        } else {
            $normalized[$key] = $defaultValue
        }
    }

    if ($Config -and $Config.ContainsKey('Mode')) {
        $mode = [string]$Config.Mode
        if ($mode -in @('Easy', 'Custom')) { $normalized.Mode = $mode }
    }

    $booleanKeys = @(
        'CleanInstall','LaunchAfter',
        'SpotX_NewTheme','SpotX_PodcastsOff','SpotX_BlockUpdate','SpotX_AdSectionsOff',
        'SpotX_Premium','SpotX_LyricsEnabled','SpotX_TopSearch','SpotX_RightSidebarOff',
        'SpotX_RightSidebarClr','SpotX_CanvasHomeOff','SpotX_HomeSubOff',
        'SpotX_DisableStartup','SpotX_NoShortcut','SpotX_OldLyrics','SpotX_HideColIconOff',
        'SpotX_Plus','SpotX_NewFullscreen','SpotX_FunnyProgress','SpotX_ExpSpotify','SpotX_LyricsBlock',
        'SpotX_SendVersionOff','SpotX_StartSpoti','SpotX_DevTools','SpotX_Mirror','SpotX_ConfirmUninstall',
        'Spicetify_Marketplace','AutoReapply_Enabled'
    )
    foreach ($key in $booleanKeys) {
        if ($Config -and $Config.ContainsKey($key)) {
            $normalized[$key] = ConvertTo-ConfigBoolean -Value $Config[$key] -Default ([bool]$normalized[$key])
        }
    }

    if ($Config -and $Config.ContainsKey('SpotX_CacheLimit')) {
        $normalized.SpotX_CacheLimit = ConvertTo-ConfigInt -Value $Config.SpotX_CacheLimit -Default ([int]$normalized.SpotX_CacheLimit) -Minimum 0 -Maximum 50000
    }

    $dm = if ($Config -and $Config.ContainsKey('SpotX_DownloadMethod')) { [string]$Config.SpotX_DownloadMethod } else { [string]$normalized.SpotX_DownloadMethod }
    $dm = $dm.Trim().ToLowerInvariant()
    if ($dm -notin @('','curl','webclient')) { $dm = '' }
    $normalized.SpotX_DownloadMethod = $dm

    $svid = if ($Config -and $Config.ContainsKey('SpotX_SpotifyVersionId')) { [string]$Config.SpotX_SpotifyVersionId } else { [string]$normalized.SpotX_SpotifyVersionId }
    if ([string]::IsNullOrWhiteSpace($svid) -or $svid -notin $global:SpotifyVersionIds) { $svid = 'auto' }
    $normalized.SpotX_SpotifyVersionId = $svid

    $lyricsTheme = if ($Config -and $Config.ContainsKey('SpotX_LyricsTheme')) { [string]$Config.SpotX_LyricsTheme } else { [string]$normalized.SpotX_LyricsTheme }
    if ([string]::IsNullOrWhiteSpace($lyricsTheme) -or $lyricsTheme -notin $global:SpotXLyricsThemes) {
        $lyricsTheme = [string]$global:EasyDefaults.SpotX_LyricsTheme
    }
    $normalized.SpotX_LyricsTheme = $lyricsTheme

    $themeName = if ($Config -and $Config.ContainsKey('Spicetify_Theme')) { [string]$Config.Spicetify_Theme } else { [string]$normalized.Spicetify_Theme }
    if ([string]::IsNullOrWhiteSpace($themeName) -or -not $global:ThemeData.Contains($themeName)) {
        $themeName = [string]$global:EasyDefaults.Spicetify_Theme
    }
    $normalized.Spicetify_Theme = $themeName

    $availableSchemes = @()
    if ($global:ThemeData.Contains($themeName)) {
        $availableSchemes = @($global:ThemeData[$themeName].Schemes)
    }
    $defaultScheme = if ($availableSchemes -contains [string]$global:EasyDefaults.Spicetify_Scheme) {
        [string]$global:EasyDefaults.Spicetify_Scheme
    } elseif ($availableSchemes.Count -gt 0) {
        [string]$availableSchemes[0]
    } else {
        'Default'
    }
    $schemeName = if ($Config -and $Config.ContainsKey('Spicetify_Scheme')) { [string]$Config.Spicetify_Scheme } else { $defaultScheme }
    if ([string]::IsNullOrWhiteSpace($schemeName) -or $schemeName -notin $availableSchemes) {
        $schemeName = $defaultScheme
    }
    $normalized.Spicetify_Scheme = $schemeName

    $extensions = [System.Collections.Generic.List[string]]::new()
    $rawExtensions = @()
    if ($Config -and $Config.ContainsKey('Spicetify_Extensions')) {
        if ($Config.Spicetify_Extensions -is [string]) {
            $rawExtensions = @([string]$Config.Spicetify_Extensions)
        } elseif ($Config.Spicetify_Extensions -is [System.Collections.IEnumerable]) {
            $rawExtensions = @($Config.Spicetify_Extensions)
        }
    }
    foreach ($extension in $rawExtensions) {
        $name = [string]$extension
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if (-not $global:BuiltInExtensions.Contains($name)) { continue }
        if (-not $extensions.Contains($name)) { $extensions.Add($name) }
    }
    $normalized.Spicetify_Extensions = @($extensions)

    if ($normalized.SpotX_RightSidebarOff) {
        $normalized.SpotX_RightSidebarClr = $false
    }

    if ($Config -and -not $Config.ContainsKey('Mode')) {
        foreach ($key in $global:EasyDefaults.Keys) {
            $defaultValue = $global:EasyDefaults[$key]
            $currentValue = $normalized[$key]
            $isEnumerableDefault = ($defaultValue -is [System.Collections.IEnumerable] -and $defaultValue -isnot [string])
            if ($isEnumerableDefault) {
                if ((@($currentValue) -join '|') -ne (@($defaultValue) -join '|')) {
                    $normalized.Mode = 'Custom'
                    break
                }
                continue
            }
            if ([string]$currentValue -ne [string]$defaultValue) {
                $normalized.Mode = 'Custom'
                break
            }
        }
    }

    return $normalized
}

function Move-ConfigFileToQuarantine {
    param([string]$Reason)
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $global:CONFIG_PATH) {
            $stamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
            $quarantineName = "config.corrupt.$stamp.json"
            $quarantinePath = Join-Path $global:CONFIG_DIR $quarantineName
            Move-Item -LiteralPath $global:CONFIG_PATH -Destination $quarantinePath -Force
            $script:ConfigLoadWarning = "LibreSpot reset the saved settings because the config file could not be read safely. The previous file was moved to $quarantineName."
        } else {
            $script:ConfigLoadWarning = 'LibreSpot reset the saved settings because the config file could not be read safely.'
        }
    } catch {
        $script:ConfigLoadWarning = 'LibreSpot reset the saved settings because the config file could not be read safely, but it could not move the original file aside automatically.'
    }
    try {
        if ($Reason) { Write-Log "Config reset: $Reason" -Level 'WARN' }
    } catch {}
}

function Save-LibreSpotConfig { param([hashtable]$Config)
    $tempPath = "$global:CONFIG_PATH.tmp"
    $backupPath = "$global:CONFIG_PATH.bak"
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }
        $normalizedConfig = Normalize-LibreSpotConfig -Config $Config
        $json = [ordered]@{}
        foreach ($key in $normalizedConfig.Keys) { $json[$key] = $normalizedConfig[$key] }
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($tempPath, ($json | ConvertTo-Json -Depth 4), $utf8)
        if (Test-Path -LiteralPath $global:CONFIG_PATH) {
            try {
                [System.IO.File]::Replace($tempPath, $global:CONFIG_PATH, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                # Replace() can fail on some filesystems; fall back to atomic Move
                Remove-Item -LiteralPath $global:CONFIG_PATH -Force -ErrorAction Stop
                [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
            }
        } else {
            [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
        }
        return $true
    } catch {
        try { Write-Log "Config save failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
        Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
        return $false
    }
}

function Load-LibreSpotConfig {
    $script:ConfigLoadWarning = $null
    if (-not (Test-Path -LiteralPath $global:CONFIG_PATH)) { return $null }
    try {
        $json = Get-Content -LiteralPath $global:CONFIG_PATH -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        $cfg = ConvertTo-PlainHashtable -InputObject $json
        return (Normalize-LibreSpotConfig -Config $cfg)
    } catch {
        Move-ConfigFileToQuarantine -Reason $_.Exception.Message
    }
    return $null
}

function Get-LibreSpotTempRoot {
    $root = Join-Path $global:TEMP_DIR 'LibreSpot'
    if (-not (Test-Path -LiteralPath $root)) {
        New-Item -Path $root -ItemType Directory -Force | Out-Null
    }
    return $root
}

function New-LibreSpotTempFile {
    param([string]$Name)

    $fileName = if ([string]::IsNullOrWhiteSpace($Name)) { 'artifact.tmp' } else { $Name }
    return (Join-Path (Get-LibreSpotTempRoot) ("{0}-{1}" -f [Guid]::NewGuid().ToString('N'), $fileName))
}

function New-LibreSpotTempDirectory {
    param([string]$Name = 'workspace')

    $directoryName = if ([string]::IsNullOrWhiteSpace($Name)) { 'workspace' } else { $Name }
    $path = Join-Path (Get-LibreSpotTempRoot) ("{0}-{1}" -f [Guid]::NewGuid().ToString('N'), $directoryName)
    New-Item -Path $path -ItemType Directory -Force | Out-Null
    return $path
}

# =============================================================================
# 5. WPF XAML
# =============================================================================
$ErrorActionPreference = 'Continue'  # WPF internals generate non-terminating errors with complex templates

$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LibreSpot" Height="860" Width="1168"
        WindowStyle="None" ResizeMode="NoResize" AllowsTransparency="True"
        Background="#00000000" WindowStartupLocation="CenterScreen"
        FontFamily="Segoe UI" UseLayoutRounding="True" SnapsToDevicePixels="True"
        TextOptions.TextFormattingMode="Display">
    <Window.Resources>
        <!-- Rounded ProgressBar -->
        <ControlTemplate x:Key="RoundProgress" TargetType="ProgressBar">
            <Grid>
                <Border x:Name="PART_Track" CornerRadius="4" Background="{TemplateBinding Background}" Height="8"/>
                <Border x:Name="PART_Indicator" CornerRadius="4" HorizontalAlignment="Left" Height="8" Background="{TemplateBinding Foreground}"/>
            </Grid>
        </ControlTemplate>
        <!-- ComboBox Toggle -->
        <ControlTemplate x:Key="DarkComboBoxToggle" TargetType="ToggleButton">
            <Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="34"/></Grid.ColumnDefinitions>
                <Border x:Name="Border" Grid.ColumnSpan="2" CornerRadius="10" Background="#FF0c131c" BorderBrush="#FF223042" BorderThickness="1"/>
                <Border Grid.Column="0" CornerRadius="10,0,0,10" Background="Transparent"/>
                <Path Grid.Column="1" Fill="#FF94a3b8" HorizontalAlignment="Center" VerticalAlignment="Center" Data="M 0 0 L 4 4 L 8 0 Z"/>
            </Grid>
            <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Border" Property="BorderBrush" Value="#FF3b4d63"/></Trigger>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="Border" Property="BorderBrush" Value="#FF3b4d63"/></Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>
        <!-- ComboBox -->
        <Style x:Key="DarkComboBox" TargetType="ComboBox">
            <Setter Property="Foreground" Value="#FFF8FAFC"/><Setter Property="Background" Value="#FF0B1219"/><Setter Property="Height" Value="40"/><Setter Property="FontSize" Value="12.75"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid>
                <ToggleButton Template="{StaticResource DarkComboBoxToggle}" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}" Focusable="False" ClickMode="Press"/>
                <ContentPresenter IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" Margin="12,0,34,0" VerticalAlignment="Center" HorizontalAlignment="Left"/>
                <Popup IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" Focusable="False" AllowsTransparency="True">
                    <Border Background="#FF081018" BorderBrush="#FF29394B" BorderThickness="1" CornerRadius="12" MaxHeight="320" Margin="0,8,0,0">
                        <Border.Effect><DropShadowEffect BlurRadius="20" ShadowDepth="4" Opacity="0.34" Direction="270"/></Border.Effect>
                        <ScrollViewer><StackPanel IsItemsHost="True"/></ScrollViewer></Border>
                </Popup>
            </Grid></ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- ComboBox Item -->
        <Style x:Key="DarkComboBoxItem" TargetType="ComboBoxItem">
            <Setter Property="Foreground" Value="#FFF8FAFC"/><Setter Property="Background" Value="Transparent"/><Setter Property="Padding" Value="12,8"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem">
                <Border x:Name="Bd" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}" CornerRadius="8" Margin="4,2">
                    <ContentPresenter/></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Background" Value="#FF122030"/></Trigger>
                    <Trigger Property="IsSelected" Value="True"><Setter TargetName="Bd" Property="Background" Value="#FF13283D"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- CheckBox -->
        <Style x:Key="DarkCheckBox" TargetType="CheckBox">
            <Setter Property="Foreground" Value="#FFE2E8F0"/><Setter Property="FontSize" Value="13"/><Setter Property="Margin" Value="0,8,0,0"/><Setter Property="Cursor" Value="Hand"/><Setter Property="MinHeight" Value="28"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal">
                <Border x:Name="box" Width="22" Height="22" CornerRadius="7" Background="#FF0d151f" BorderBrush="#FF334155" BorderThickness="1.5" Margin="0,1,12,0">
                    <Path x:Name="check" Data="M 4 10 L 8 14 L 15 5" Stroke="#FF4ade80" StrokeThickness="2.1" Visibility="Collapsed" Margin="0.5,0.5,0,0"/></Border>
                <ContentPresenter VerticalAlignment="Center"/>
            </StackPanel><ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="check" Property="Visibility" Value="Visible"/><Setter TargetName="box" Property="Background" Value="#FF0c2018"/><Setter TargetName="box" Property="BorderBrush" Value="#FF4ade80"/></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="box" Property="BorderBrush" Value="#FF64748b"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="box" Property="BorderBrush" Value="#FF86efac"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.52"/></Trigger>
            </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- TextBox -->
        <Style x:Key="DarkTextBox" TargetType="TextBox">
            <Setter Property="Foreground" Value="#FFF8FAFC"/><Setter Property="Background" Value="#FF0d151f"/><Setter Property="BorderBrush" Value="#FF253548"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontSize" Value="13"/><Setter Property="Padding" Value="12,7"/><Setter Property="Height" Value="42"/><Setter Property="VerticalContentAlignment" Value="Center"/><Setter Property="CaretBrush" Value="#FF4ade80"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="12"><ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="BorderBrush" Value="#FF3b4d63"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Bd" Property="BorderBrush" Value="#FF67e8f9"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Bd" Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Action Button -->
        <Style x:Key="ActionButton" TargetType="Button">
            <Setter Property="Height" Value="46"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="FontSize" Value="13.5"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Foreground" Value="#FFF8FAFC"/><Setter Property="BorderThickness" Value="1"/><Setter Property="BorderBrush" Value="#FF304458"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="14" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="24,0">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="border" Property="Opacity" Value="0.97"/><Setter TargetName="border" Property="BorderBrush" Value="#FF67e8f9"/></Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="border" Property="BorderBrush" Value="#FFBAE6FD"/></Trigger>
                    <Trigger Property="IsPressed" Value="True"><Setter TargetName="border" Property="Opacity" Value="0.88"/></Trigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter TargetName="border" Property="Opacity" Value="0.38"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Mode Radio Tab -->
        <Style x:Key="ModeRadio" TargetType="RadioButton">
            <Setter Property="Foreground" Value="#FFF8FAFC"/><Setter Property="FontSize" Value="13"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="Cursor" Value="Hand"/><Setter Property="Margin" Value="0,0,12,0"/><Setter Property="MinWidth" Value="228"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton">
                <Grid><Border x:Name="bd" Background="#FF0B1118" CornerRadius="16" BorderBrush="#FF172536" BorderThickness="1" Padding="18,15">
                    <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                        <Border x:Name="dot" Width="8" Height="8" CornerRadius="4" Background="#FF334155" VerticalAlignment="Top" Margin="0,7,0,0"/>
                        <StackPanel Grid.Column="2">
                            <TextBlock x:Name="title" Text="{TemplateBinding Content}" FontSize="14.25" FontWeight="SemiBold" Foreground="#FFD7E0EA"/>
                            <TextBlock x:Name="description" Text="{TemplateBinding Tag}" Foreground="#FF73859A" FontSize="11.25" Margin="0,6,0,0" TextWrapping="Wrap" MaxWidth="240"/>
                        </StackPanel>
                    </Grid>
                </Border></Grid>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsChecked" Value="True"><Setter TargetName="bd" Property="Background" Value="#FF101824"/><Setter TargetName="bd" Property="BorderBrush" Value="#FF315E87"/><Setter TargetName="title" Property="Foreground" Value="#FFF8FAFC"/><Setter TargetName="description" Property="Foreground" Value="#FFA6BCD1"/><Setter TargetName="dot" Property="Background" Value="#FF7DD3FC"/></Trigger>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#FF0D1520"/><Setter TargetName="bd" Property="BorderBrush" Value="#FF203347"/></Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="bd" Property="BorderBrush" Value="#FFBAE6FD"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Maintenance Button -->
        <Style x:Key="MaintButton" TargetType="Button">
            <Setter Property="MinHeight" Value="84"/><Setter Property="Background" Value="#FF0d141d"/><Setter Property="Foreground" Value="#FFF8FAFC"/><Setter Property="FontSize" Value="12"/>
            <Setter Property="FontWeight" Value="Normal"/><Setter Property="Cursor" Value="Hand"/><Setter Property="BorderThickness" Value="1"/><Setter Property="BorderBrush" Value="#FF294d61"/><Setter Property="Margin" Value="0,8,0,0"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="16" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"><Grid>
                    <Rectangle x:Name="accent" Fill="{TemplateBinding BorderBrush}" Width="4" HorizontalAlignment="Left" RadiusX="2" RadiusY="2" Margin="10,16" Opacity="0.72"/>
                    <ContentPresenter HorizontalAlignment="Stretch" VerticalAlignment="Center" Margin="24,20,20,20"/></Grid></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="border" Property="Background" Value="#FF121b25"/><Setter TargetName="border" Property="BorderBrush" Value="#FF5ba4d6"/><Setter TargetName="accent" Property="Opacity" Value="1"/></Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="border" Property="BorderBrush" Value="#FFBAE6FD"/></Trigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter TargetName="border" Property="Opacity" Value="0.36"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <Style x:Key="DangerMaintButton" TargetType="Button" BasedOn="{StaticResource MaintButton}">
            <Setter Property="Background" Value="#FF150d11"/><Setter Property="BorderBrush" Value="#FF7f1d1d"/><Setter Property="Foreground" Value="#FFFFF1F2"/>
        </Style>
        <Style x:Key="SurfaceCard" TargetType="Border">
            <Setter Property="Background" Value="#FF0A1017"/><Setter Property="BorderBrush" Value="#FF162332"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="18"/><Setter Property="Padding" Value="22"/>
        </Style>
        <Style x:Key="InsetPanel" TargetType="Border">
            <Setter Property="Background" Value="#FF091018"/><Setter Property="BorderBrush" Value="#FF132131"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="16"/><Setter Property="Padding" Value="18"/>
        </Style>
        <Style x:Key="StatusCard" TargetType="Border" BasedOn="{StaticResource SurfaceCard}">
            <Setter Property="Padding" Value="16"/>
            <Setter Property="MinHeight" Value="92"/>
        </Style>
        <Style x:Key="SectionEyebrow" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FF93C5FD"/><Setter Property="FontSize" Value="11"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="SectionLead" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FF9AAABD"/><Setter Property="FontSize" Value="12.5"/><Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="ValueTileLabel" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FF94A3B8"/><Setter Property="FontSize" Value="11"/><Setter Property="FontWeight" Value="SemiBold"/>
        </Style>
        <Style x:Key="ValueTileValue" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FFF8FAFC"/><Setter Property="FontSize" Value="15"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="HelperText" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FF73859A"/><Setter Property="FontSize" Value="10.75"/><Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="OptionCard" TargetType="Border" BasedOn="{StaticResource InsetPanel}">
            <Setter Property="Padding" Value="14,12"/>
        </Style>
        <!-- Tooltip -->
        <Style TargetType="ToolTip">
            <Setter Property="Background" Value="#FF0f1720"/><Setter Property="Foreground" Value="#FFE2E8F0"/><Setter Property="BorderBrush" Value="#FF334155"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontSize" Value="11.5"/><Setter Property="Padding" Value="12,9"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ToolTip">
                <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="8" Padding="{TemplateBinding Padding}">
                    <Border.Effect><DropShadowEffect BlurRadius="16" ShadowDepth="2" Opacity="0.35" Direction="270"/></Border.Effect>
                    <ContentPresenter/></Border>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
    </Window.Resources>

    <!-- Outer margin gives room for window shadow -->
    <Grid Margin="18">
        <Border CornerRadius="16" Background="#FF06090f" BorderBrush="#FF17202b" BorderThickness="1" ClipToBounds="False">
            <Border.Effect><DropShadowEffect BlurRadius="32" ShadowDepth="0" Opacity="0.58" Color="#000000"/></Border.Effect>
            <Grid ClipToBounds="True">
                <Ellipse Width="380" Height="380" HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,-150,-110,0">
                    <Ellipse.Fill><RadialGradientBrush GradientOrigin="0.35,0.35"><GradientStop Color="#221E3A5A" Offset="0"/><GradientStop Color="#00000000" Offset="1"/></RadialGradientBrush></Ellipse.Fill>
                </Ellipse>
                <Ellipse Width="240" Height="240" HorizontalAlignment="Left" VerticalAlignment="Bottom" Margin="-80,0,0,-110">
                    <Ellipse.Fill><RadialGradientBrush GradientOrigin="0.55,0.45"><GradientStop Color="#16152938" Offset="0"/><GradientStop Color="#00000000" Offset="1"/></RadialGradientBrush></Ellipse.Fill>
                </Ellipse>
                <Border Height="1" VerticalAlignment="Top" CornerRadius="16,16,0,0" Panel.ZIndex="2"><Border.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                        <GradientStop Color="#00000000" Offset="0"/><GradientStop Color="#665BA4D6" Offset="0.18"/>
                        <GradientStop Color="#AA93C5FD" Offset="0.5"/><GradientStop Color="#665BA4D6" Offset="0.82"/>
                        <GradientStop Color="#00000000" Offset="1"/>
                    </LinearGradientBrush></Border.Background></Border>

                <Grid><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>

                    <!-- ===== TITLE BAR ===== -->
                    <Border Name="TitleBar" Grid.Row="0" Background="#05080d" Padding="28,22,28,16">
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                            <StackPanel Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Center">
                                <Border Width="52" Height="52" CornerRadius="18" Background="#FF091018" BorderBrush="#FF183044" BorderThickness="1" Padding="7">
                                    <Image Name="TitleLogo" Stretch="Uniform"/>
                                </Border>
                                <StackPanel Margin="14,0,0,0" VerticalAlignment="Center">
                                    <TextBlock Name="TitleText" Foreground="#FFF8FAFC" FontSize="22" FontWeight="Bold" VerticalAlignment="Center"/>
                                    <TextBlock Name="TitleSubtext" Text="Pinned SpotX + Spicetify setup and recovery for Spotify." Foreground="#FF73859A" FontSize="11.75" Margin="0,5,0,0"/>
                                </StackPanel>
                            </StackPanel>
                            <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
                                    <TextBlock Name="UpdateBanner" VerticalAlignment="Center" Margin="0,0,16,0" Visibility="Collapsed" ToolTip="A newer LibreSpot release is available on GitHub."><Hyperlink Name="LinkUpdate" Foreground="#FF22c55e" TextDecorations="None" FontSize="10.75" FontWeight="SemiBold" Cursor="Hand">Update available &#x2192;</Hyperlink></TextBlock>
                                    <TextBlock VerticalAlignment="Center" Margin="0,0,16,0"><Hyperlink Name="LinkSpotX" NavigateUri="https://github.com/SpotX-Official/SpotX" Foreground="#FF94a3b8" TextDecorations="None" FontSize="10.75" Cursor="Hand">SpotX</Hyperlink></TextBlock>
                                    <TextBlock VerticalAlignment="Center" Margin="0,0,16,0"><Hyperlink Name="LinkSpicetify" NavigateUri="https://github.com/spicetify" Foreground="#FF94a3b8" TextDecorations="None" FontSize="10.75" Cursor="Hand">Spicetify</Hyperlink></TextBlock>
                                    <Button Name="LinkGitHub" Width="30" Height="30" Background="Transparent" BorderThickness="0" Cursor="Hand" ToolTip="View on GitHub" VerticalAlignment="Center" Margin="0,0,12,0">
                                    <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="8"><Path x:Name="ico" Fill="#FF94a3b8" Data="M8,0 C3.58,0 0,3.58 0,8 c0,3.54 2.29,6.53 5.47,7.59 c.4,.07 .55,-.17 .55,-.38 c0,-.19 -.01,-.82 -.01,-1.49 c-2.01,.37 -2.53,-.49 -2.69,-.94 c-.09,-.23 -.48,-.94 -.82,-1.13 c-.28,-.15 -.68,-.52 -.01,-.53 c.63,-.01 1.08,.58 1.23,.82 c.72,1.21 1.87,.87 2.33,.66 c.07,-.52 .28,-.87 .51,-1.07 c-1.78,-.2 -3.64,-.89 -3.64,-3.95 c0,-.87 .31,-1.59 .82,-2.15 c-.08,-.2 -.36,-1.02 .08,-2.12 c0,0 .67,-.21 2.2,.82 c.64,-.18 1.32,-.27 2,-.27 c.68,0 1.36,.09 2,.27 c1.53,-1.04 2.2,-.82 2.2,-.82 c.44,1.1 .16,1.92 .08,2.12 c.51,.56 .82,1.27 .82,2.15 c0,3.07 -1.87,3.75 -3.65,3.95 c.29,.25 .54,.73 .54,1.48 c0,1.07 -.01,1.93 -.01,2.2 c0,.21 .15,.46 .55,.38 A8.013,8.013,0,0,0,16,8 c0,-4.42 -3.58,-8 -8,-8z" Stretch="Uniform" Width="14" Height="14" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#FF142030"/><Setter TargetName="ico" Property="Fill" Value="#FFE2E8F0"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template></Button>
                                    <Button Name="MinimizeBtn" Content="&#x2013;" Width="34" Height="30" Background="Transparent" Foreground="#FF94a3b8" BorderThickness="0" FontSize="12" FontWeight="Bold" Cursor="Hand" Margin="0,0,6,0">
                                    <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="6"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#FF142030"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template></Button>
                                    <Button Name="CloseTitleBtn" Content="&#x2715;" Width="34" Height="30" Background="Transparent" Foreground="#FF94a3b8" BorderThickness="0" FontSize="11" Cursor="Hand">
                                    <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="6"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#FFdc2626"/><Setter Property="Foreground" Value="#FFfafafa"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template></Button>
                            </StackPanel>
                        </Grid>
                    </Border>

                    <!-- ===== CONTENT ===== -->
                    <Grid Name="PageContainer" Grid.Row="1" Margin="30,20,30,30">
                        <!-- ===== CONFIG PAGE ===== -->
                        <Grid Name="PageConfig" Visibility="Visible"><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                            <Grid Grid.Row="0" Margin="0,0,0,16">
                                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                <StackPanel>
                                    <TextBlock Text="Choose your workflow" Style="{StaticResource SectionEyebrow}"/>
                                    <TextBlock Name="ModeHeadline" Text="Recommended path for a first install" Foreground="#FFF8FAFC" FontSize="28" FontWeight="Bold" Margin="0,8,0,0"/>
                                    <TextBlock Name="ModeSummaryText" Text="LibreSpot handles cleanup, verified downloads, Spotify patching, Marketplace, and a reliable default extension set." Style="{StaticResource SectionLead}" Margin="0,10,0,0" MaxWidth="690"/>
                                </StackPanel>
                                <StackPanel Grid.Column="1" HorizontalAlignment="Right" Margin="24,4,0,0" MaxWidth="280">
                                    <Border Background="#FF0D1520" BorderBrush="#FF20374D" BorderThickness="1" CornerRadius="999" Padding="12,6" HorizontalAlignment="Right">
                                        <TextBlock Text="Pinned downloads • backup-aware defaults" Foreground="#FFBFD8EC" FontSize="10.5" FontWeight="SemiBold"/>
                                    </Border>
                                    <TextBlock Text="Built to keep Spotify in a predictable, recoverable state after updates." Foreground="#FF73859A" FontSize="11" Margin="0,8,0,0" HorizontalAlignment="Right" TextAlignment="Right" TextWrapping="Wrap"/>
                                </StackPanel>
                            </Grid>
                            <StackPanel Grid.Row="1" Orientation="Horizontal">
                                <RadioButton Name="ModeEasy" Content="Easy Install" Tag="Fastest clean setup with the recommended Spotify, SpotX, and Marketplace baseline." IsChecked="True" Style="{StaticResource ModeRadio}" GroupName="Mode"/>
                                <RadioButton Name="ModeCustom" Content="Custom Install" Tag="Tune cleanup, theming, lyrics, extensions, and launch behavior before anything runs." Style="{StaticResource ModeRadio}" GroupName="Mode"/>
                                <RadioButton Name="ModeMaint" Content="Maintenance" Tag="Inspect the current stack, restore backups, reapply patches, or remove modifications safely." Style="{StaticResource ModeRadio}" GroupName="Mode"/>
                            </StackPanel>
                            <Border Grid.Row="2" Background="#080f18" CornerRadius="16" Padding="24" BorderBrush="#FF17212c" BorderThickness="1"><Grid>

                                <!-- ===== EASY PANEL ===== -->
                                <StackPanel Name="PanelEasy" Visibility="Visible" VerticalAlignment="Center" HorizontalAlignment="Stretch">
                                    <Grid Margin="0,0,0,20">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="1.15*"/><ColumnDefinition Width="20"/><ColumnDefinition Width="0.85*"/></Grid.ColumnDefinitions>
                                        <StackPanel Grid.Column="0">
                                            <TextBlock Text="A clean, reliable Spotify setup in one pass" Foreground="#FFF8FAFC" FontSize="23" FontWeight="Bold"/>
                                            <TextBlock Text="Easy Install applies the stable default stack: Spotify cleanup, SpotX patching, Spicetify, Marketplace, and a curated extension set with recovery-focused defaults." Foreground="#FF94A3B8" FontSize="13" TextWrapping="Wrap" Margin="0,10,0,0"/>
                                            <WrapPanel Margin="0,18,0,0">
                                                <Border Background="#120f1b12" BorderBrush="#1f3d2b" BorderThickness="1" CornerRadius="999" Padding="12,6" Margin="0,0,10,10"><TextBlock Text="Clean install" Foreground="#FF86efac" FontSize="10.5" FontWeight="SemiBold"/></Border>
                                                <Border Background="#120f1b12" BorderBrush="#1f3d2b" BorderThickness="1" CornerRadius="999" Padding="12,6" Margin="0,0,10,10"><TextBlock Text="Marketplace included" Foreground="#FF86efac" FontSize="10.5" FontWeight="SemiBold"/></Border>
                                                <Border Background="#11101d2a" BorderBrush="#1d3347" BorderThickness="1" CornerRadius="999" Padding="12,6" Margin="0,0,10,10"><TextBlock Text="3 default extensions" Foreground="#FF7dd3fc" FontSize="10.5" FontWeight="SemiBold"/></Border>
                                                <Border Background="#11111220" BorderBrush="#1b2433" BorderThickness="1" CornerRadius="999" Padding="12,6" Margin="0,0,0,10"><TextBlock Text="Launch when finished" Foreground="#FFCBD5E1" FontSize="10.5" FontWeight="SemiBold"/></Border>
                                            </WrapPanel>
                                        </StackPanel>
                                        <Border Grid.Column="2" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Default preset" Foreground="#FFF8FAFC" FontSize="14.5" FontWeight="Bold"/>
                                                <TextBlock Text="Best when you want Spotify working quickly without tuning every option." Foreground="#FF94A3B8" FontSize="12" TextWrapping="Wrap" Margin="0,8,0,14"/>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Ellipse Width="8" Height="8" Fill="#FF4ade80" Margin="0,6,12,0"/><TextBlock Grid.Column="1" Text="Fresh Spotify with SpotX patching and the new UI theme" Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Ellipse Width="8" Height="8" Fill="#FF4ade80" Margin="0,6,12,0"/><TextBlock Grid.Column="1" Text="Podcasts removed, ad-like sections hidden, and auto-updates blocked" Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Ellipse Width="8" Height="8" Fill="#FF4ade80" Margin="0,6,12,0"/><TextBlock Grid.Column="1" Text="Marketplace plus Full App Display, Shuffle+, and Trash Bin" Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Ellipse Width="8" Height="8" Fill="#FF7dd3fc" Margin="0,6,12,0"/><TextBlock Grid.Column="1" Text="Settings are saved so the same defaults are ready next time" Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                    <Grid>
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="20"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Grid.Column="0" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="What LibreSpot takes care of" Foreground="#FFF8FAFC" FontSize="15" FontWeight="Bold"/>
                                                <TextBlock Text="The goal is a dependable install, not just a pretty wrapper." Foreground="#FF94A3B8" FontSize="12" Margin="0,8,0,14"/>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Width="22" Height="22" CornerRadius="11" Background="#140f1b12" BorderBrush="#1f3d2b" BorderThickness="1"><Path Data="M 5 10 L 9 14 L 16 6" Stroke="#FF4ade80" StrokeThickness="1.8" Margin="0.5,0,0,0"/></Border><TextBlock Grid.Column="1" Margin="12,0,0,0" Text="Verifies pinned downloads before applying anything" Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Width="22" Height="22" CornerRadius="11" Background="#11101d2a" BorderBrush="#1d3347" BorderThickness="1"><Path Data="M 5 10 L 9 14 L 16 6" Stroke="#FF7dd3fc" StrokeThickness="1.8" Margin="0.5,0,0,0"/></Border><TextBlock Grid.Column="1" Margin="12,0,0,0" Text="Runs cleanup first so stale Spotify and Spicetify files do not conflict" Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Width="22" Height="22" CornerRadius="11" Background="#11111220" BorderBrush="#1b2433" BorderThickness="1"><Path Data="M 5 10 L 9 14 L 16 6" Stroke="#FFCBD5E1" StrokeThickness="1.8" Margin="0.5,0,0,0"/></Border><TextBlock Grid.Column="1" Margin="12,0,0,0" Text="Installs themes, extensions, and Marketplace in a safe order" Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Width="22" Height="22" CornerRadius="11" Background="#140f1b12" BorderBrush="#1f3d2b" BorderThickness="1"><Path Data="M 5 10 L 9 14 L 16 6" Stroke="#FF4ade80" StrokeThickness="1.8" Margin="0.5,0,0,0"/></Border><TextBlock Grid.Column="1" Margin="12,0,0,0" Text="Keeps recovery tools close by if Spotify updates later" Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                            </StackPanel>
                                        </Border>
                                        <Border Grid.Column="2" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Before you start" Foreground="#FFF8FAFC" FontSize="15" FontWeight="Bold"/>
                                                <TextBlock Text="A few expectations up front make the whole flow feel more predictable." Foreground="#FF94A3B8" FontSize="12" Margin="0,8,0,14"/>
                                                <TextBlock Text="LibreSpot requests administrator permission because it modifies Spotify files and Windows settings." Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap" Margin="0,0,0,10"/>
                                                <TextBlock Text="A network connection is required for GitHub downloads, preview images, and update checks." Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap" Margin="0,0,0,10"/>
                                                <TextBlock Text="Easy Install removes any existing Spotify and Spicetify setup first so the result is consistent." Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap" Margin="0,0,0,10"/>
                                                <TextBlock Text="If you prefer to keep a current install in place, switch to Custom Install and disable full cleanup." Foreground="#FFE2E8F0" FontSize="12.5" TextWrapping="Wrap"/>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </StackPanel>

                                <!-- ===== CUSTOM PANEL ===== -->
                                <ScrollViewer Name="PanelCustom" Visibility="Collapsed" VerticalScrollBarVisibility="Auto"><StackPanel Margin="4,6,4,0">
                                    <Grid Margin="0,0,0,18">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                        <StackPanel>
                                            <TextBlock Text="Custom install, dialed in" Foreground="#FFF8FAFC" FontSize="21" FontWeight="Bold"/>
                                            <TextBlock Text="Choose exactly how much cleanup, theming, Marketplace support, and extension prep you want before Spotify opens." Foreground="#FF94A3B8" FontSize="12.5" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                        </StackPanel>
                                        <Button Grid.Column="1" Name="BtnResetCustomDefaults" Content="Load recommended defaults" Background="#FF0b1118" Style="{StaticResource ActionButton}" Width="192" Height="40" Margin="18,2,0,0" VerticalAlignment="Top" ToolTip="Apply the Easy Install defaults here so you can keep customizing from a known-good baseline."/>
                                    </Grid>
                                    <Border Style="{StaticResource SurfaceCard}" Margin="0,0,0,18">
                                        <Grid>
                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                            <Border Grid.Column="0" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Install plan" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/>
                                                    <TextBlock Name="CustomSnapshotPlanValue" Text="Clean install" Foreground="#FFF8FAFC" FontSize="14.5" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                </StackPanel>
                                            </Border>
                                            <Border Grid.Column="2" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Theme" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/>
                                                    <TextBlock Name="CustomSnapshotThemeValue" Text="Marketplace only" Foreground="#FFF8FAFC" FontSize="14.5" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                </StackPanel>
                                            </Border>
                                            <Border Grid.Column="4" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Extensions" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/>
                                                    <TextBlock Name="CustomSnapshotExtensionsValue" Text="3 extensions" Foreground="#FFF8FAFC" FontSize="14.5" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                </StackPanel>
                                            </Border>
                                            <Border Grid.Column="6" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Remembered state" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/>
                                                    <TextBlock Name="CustomSnapshotMemoryValue" Text="Will save on install" Foreground="#FFF8FAFC" FontSize="14.5" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                </StackPanel>
                                            </Border>
                                        </Grid>
                                    </Border>
                                    <Grid>
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="20"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Grid.Column="0" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Spotify behavior" Foreground="#FFF8FAFC" FontSize="15" FontWeight="Bold"/>
                                                <TextBlock Text="LibreSpot uses SpotX to handle cleanup, patching, interface tweaks, and a few system-level quality-of-life options." Foreground="#FF94A3B8" FontSize="12" Margin="0,8,0,14" TextWrapping="Wrap"/>
                                                <Border Style="{StaticResource InsetPanel}">
                                                    <StackPanel>
                                                        <TextBlock Text="Core cleanup" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Trim Spotify's default clutter and keep the patched setup stable after future updates." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkNewTheme" Content="Enable the new Spotify interface" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Activates Spotify new sidebar and cover art layout"/>
                                                        <CheckBox Name="ChkPodcastsOff" Content="Remove podcasts from Home" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Hides podcast sections from home feed"/>
                                                        <CheckBox Name="ChkAdSectionsOff" Content="Hide ad-like Home sections" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Removes promotional sections"/>
                                                        <CheckBox Name="ChkBlockUpdate" Content="Block Spotify auto-updates" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Prevents Spotify from overwriting patches"/>
                                                        <CheckBox Name="ChkPremium" Content="Premium account (skip ad-blocking)" Style="{StaticResource DarkCheckBox}" ToolTip="For paid users: skip ad-blocking, keep other mods"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Lyrics" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Use the static lyrics layer if you want cleaner reading and easier theme matching." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkLyrics" Content="Enable a static lyrics theme" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                                        <StackPanel Name="LyricsThemePanel" Orientation="Horizontal" Margin="28,6,0,0">
                                                            <TextBlock Text="Theme:" Foreground="#FFCBD5E1" FontSize="11.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbLyricsTheme" Width="170" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" SelectedIndex="0">
                                                                <ComboBoxItem Content="spotify"/><ComboBoxItem Content="blueberry"/><ComboBoxItem Content="blue"/><ComboBoxItem Content="discord"/>
                                                                <ComboBoxItem Content="forest"/><ComboBoxItem Content="fresh"/><ComboBoxItem Content="github"/><ComboBoxItem Content="lavender"/>
                                                                <ComboBoxItem Content="orange"/><ComboBoxItem Content="pumpkin"/><ComboBoxItem Content="purple"/><ComboBoxItem Content="red"/>
                                                                <ComboBoxItem Content="strawberry"/><ComboBoxItem Content="turquoise"/><ComboBoxItem Content="yellow"/><ComboBoxItem Content="oceano"/>
                                                                <ComboBoxItem Content="royal"/><ComboBoxItem Content="krux"/><ComboBoxItem Content="pinkle"/><ComboBoxItem Content="zing"/>
                                                                <ComboBoxItem Content="radium"/><ComboBoxItem Content="sandbar"/><ComboBoxItem Content="postlight"/><ComboBoxItem Content="relish"/>
                                                                <ComboBoxItem Content="drot"/><ComboBoxItem Content="default"/><ComboBoxItem Content="spotify#2"/></ComboBox>
                                                        </StackPanel>
                                                        <CheckBox Name="ChkOldLyrics" Content="Restore the old lyrics interface" Style="{StaticResource DarkCheckBox}" ToolTip="Revert to previous lyrics interface"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Interface experiments" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Optional layout tweaks. Keep this section conservative if you want the safest possible install." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkTopSearch" Content="Move search to the top bar" Style="{StaticResource DarkCheckBox}" ToolTip="Move search bar to top of window"/>
                                                        <CheckBox Name="ChkRightSidebarOff" Content="Disable the right sidebar" Style="{StaticResource DarkCheckBox}" ToolTip="Remove the Now Playing sidebar panel"/>
                                                        <CheckBox Name="ChkRightSidebarColor" Content="Match right sidebar colors to album art" Style="{StaticResource DarkCheckBox}" ToolTip="Tint sidebar to match album cover"/>
                                                        <CheckBox Name="ChkCanvasHomeOff" Content="Disable canvas on Home" Style="{StaticResource DarkCheckBox}" ToolTip="Disable canvas artwork on the homepage"/>
                                                        <CheckBox Name="ChkHomeSubOff" Content="Hide Home subfeed chips" Style="{StaticResource DarkCheckBox}" ToolTip="Hide genre filter chips on home page"/>
                                                        <CheckBox Name="ChkHideColIconOff" Content="Show collaboration icons in playlists" Style="{StaticResource DarkCheckBox}" ToolTip="Keep collaboration icons visible in playlists"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Experimental features" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="These features are newer or experimental. They may change behavior with future Spotify updates." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkPlus" Content="Enhanced save and destination features" Style="{StaticResource DarkCheckBox}" ToolTip="Enable enhanced save/destination features in Spotify"/>
                                                        <CheckBox Name="ChkNewFullscreen" Content="Experimental fullscreen mode" Style="{StaticResource DarkCheckBox}" ToolTip="Enable the new experimental fullscreen mode"/>
                                                        <CheckBox Name="ChkFunnyProgress" Content="Humorous progress bar" Style="{StaticResource DarkCheckBox}" ToolTip="Replace the standard progress bar with a humorous variant"/>
                                                        <CheckBox Name="ChkExpSpotify" Content="Enable experimental Spotify features" Style="{StaticResource DarkCheckBox}" ToolTip="Unlock experimental features that Spotify is testing but has not yet released"/>
                                                        <CheckBox Name="ChkLyricsBlock" Content="Disable native lyrics entirely" Style="{StaticResource DarkCheckBox}" ToolTip="Block Spotify's built-in lyrics feature completely"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="System behavior" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Set startup behavior, shortcut handling, and the cache-size override SpotX can apply." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkDisableStartup" Content="Disable Spotify on Windows startup" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                                        <CheckBox Name="ChkNoShortcut" Content="Skip the desktop shortcut" Style="{StaticResource DarkCheckBox}"/>
                                                        <CheckBox Name="ChkStartSpoti" Content="Launch Spotify automatically after install" Style="{StaticResource DarkCheckBox}" ToolTip="Let SpotX start Spotify right after the patch finishes"/>
                                                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                                                            <TextBlock Text="Cache limit (MB):" Foreground="#FFE2E8F0" FontSize="12.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <TextBox Name="TxtCacheLimit" Width="96" Text="0" Style="{StaticResource DarkTextBox}" ToolTip="Use 0 or a value of 500 MB and above."/>
                                                        </StackPanel>
                                                        <TextBlock Text="Use 0 to keep Spotify's default behavior. LibreSpot treats any value from 1 to 499 as 500 MB so the override stays in SpotX's safer range." Foreground="#FF64748B" FontSize="10.5" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Privacy" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Limit what SpotX and Spotify can report back. Recommended defaults trim outbound telemetry without breaking patches." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkSendVersionOff" Content="Disable SpotX version reporting" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Blocks SpotX's outbound version notification (added in SpotX April 2026)"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Advanced" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Power-user overrides. Leave defaults unless you have a specific reason to change them." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkDevTools" Content="Enable Spotify Developer Tools" Style="{StaticResource DarkCheckBox}" ToolTip="Unlocks the Chromium DevTools hotkey inside Spotify (useful for Spicetify extension authors)"/>
                                                        <CheckBox Name="ChkMirror" Content="Use GitHub.io mirror for SpotX assets" Style="{StaticResource DarkCheckBox}" ToolTip="Falls back to the github.io mirror if raw.githubusercontent.com is blocked on your network"/>
                                                        <CheckBox Name="ChkConfirmUninstall" Content="Force a clean Spotify uninstall before patching" Style="{StaticResource DarkCheckBox}" ToolTip="Runs SpotX's uninstall-then-reinstall flow even when the current version would otherwise be kept"/>
                                                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                                                            <TextBlock Text="Download method:" Foreground="#FFE2E8F0" FontSize="12.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbDownloadMethod" Width="140" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" SelectedIndex="0" ToolTip="Force SpotX to use a specific downloader when the auto-selected one fails.">
                                                                <ComboBoxItem Content="auto"/>
                                                                <ComboBoxItem Content="curl"/>
                                                                <ComboBoxItem Content="webclient"/>
                                                            </ComboBox>
                                                        </StackPanel>
                                                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                                                            <TextBlock Text="Spotify version:" Foreground="#FFE2E8F0" FontSize="12.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbSpotifyVersion" Width="260" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" SelectedIndex="0" ToolTip="Pin a specific Spotify client version. Leave on 'Auto' unless you know a specific build works better for your system."/>
                                                        </StackPanel>
                                                        <TextBlock Name="SpotifyVersionHint" Text="Lets SpotX pick the most compatible build." Foreground="#FF64748B" FontSize="10.5" Margin="0,6,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>
                                            </StackPanel>
                                        </Border>
                                        <Border Grid.Column="2" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Themes, Marketplace, and extensions" Foreground="#FFF8FAFC" FontSize="15" FontWeight="Bold"/>
                                                <TextBlock Text="Shape the first-run look and decide what should already be installed before Spotify opens." Foreground="#FF94A3B8" FontSize="12" Margin="0,8,0,14" TextWrapping="Wrap"/>
                                                <Border Style="{StaticResource InsetPanel}">
                                                    <StackPanel>
                                                        <TextBlock Text="Theme" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Pick a bundled theme now, or stay Marketplace-only and browse from inside Spotify later." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Theme:" Foreground="#FFCBD5E1" FontSize="11.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbTheme" Width="220" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}"/></StackPanel>
                                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Color Scheme:" Foreground="#FFCBD5E1" FontSize="11.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbScheme" Width="190" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" ToolTip="Choose the color scheme for the selected bundled theme."/></StackPanel>
                                                        <Border Name="PreviewBorder" CornerRadius="12" Background="#FF081018" BorderBrush="#FF1a2634" BorderThickness="1" Margin="0,10,0,0" Height="184" ClipToBounds="True">
                                                            <Grid>
                                                                <Image Name="ThemePreviewImg" Stretch="UniformToFill" RenderOptions.BitmapScalingMode="HighQuality"/>
                                                                <Border Background="#CC081018"><TextBlock Name="PreviewLabel" Text="Select a bundled theme to preview it here." Foreground="#FFCBD5E1" FontSize="11.5" HorizontalAlignment="Center" VerticalAlignment="Center" TextWrapping="Wrap" TextAlignment="Center" MaxWidth="240"/></Border>
                                                            </Grid>
                                                        </Border>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Marketplace" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Keep the in-app browser if you want to add more themes or extensions after the guided install." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkMarketplace" Content="Install the Spicetify Marketplace" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="In-app store for themes and extensions"/>
                                                        <TextBlock Text="Browse and install themes or extensions from inside Spotify after setup." Foreground="#FF64748B" FontSize="10.5" Margin="28,4,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Built-in extensions" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Easy Install ships with Full App Display, Shuffle+, and Trash Bin enabled. Custom Install lets you fine-tune the rest." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <Grid Margin="0,6,0,0">
                                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="20"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                                            <StackPanel Grid.Column="0">
                                                                <CheckBox Name="ChkExt_fullAppDisplay" Content="Full App Display" IsChecked="True" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Full-screen artwork with blur and playback controls." Style="{StaticResource HelperText}" Margin="34,4,0,8"/>
                                                                <CheckBox Name="ChkExt_trashbin" Content="Trash Bin" IsChecked="True" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Skip songs and artists you have already marked as unwanted." Style="{StaticResource HelperText}" Margin="34,4,0,8"/>
                                                                <CheckBox Name="ChkExt_bookmark" Content="Bookmark" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Save favorite pages, tracks, albums, and exact timestamps." Style="{StaticResource HelperText}" Margin="34,4,0,8"/>
                                                                <CheckBox Name="ChkExt_popupLyrics" Content="Pop-up Lyrics" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Open synchronized lyrics in a separate resizable window." Style="{StaticResource HelperText}" Margin="34,4,0,8"/>
                                                                <CheckBox Name="ChkExt_autoSkipExplicit" Content="Auto Skip Explicit" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Automatically avoid tracks flagged as explicit." Style="{StaticResource HelperText}" Margin="34,4,0,0"/>
                                                            </StackPanel>
                                                            <StackPanel Grid.Column="2">
                                                                <CheckBox Name="ChkExt_shuffle" Content="Shuffle+" IsChecked="True" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="True shuffle instead of Spotify's weighted play order." Style="{StaticResource HelperText}" Margin="34,4,0,8"/>
                                                                <CheckBox Name="ChkExt_keyboard" Content="Keyboard Shortcuts" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Vim-style navigation for faster keyboard-driven control." Style="{StaticResource HelperText}" Margin="34,4,0,8"/>
                                                                <CheckBox Name="ChkExt_loopyLoop" Content="Loopy Loop" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Set A-B loop points for practice, study, or repeat listening." Style="{StaticResource HelperText}" Margin="34,4,0,8"/>
                                                                <CheckBox Name="ChkExt_autoSkipVideo" Content="Auto Skip Video" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Skip canvas videos and region-locked video-only content." Style="{StaticResource HelperText}" Margin="34,4,0,8"/>
                                                                <CheckBox Name="ChkExt_webNowPlaying" Content="Web Now Playing (Rainmeter)" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Expose now-playing data for desktop widgets and overlays." Style="{StaticResource HelperText}" Margin="34,4,0,0"/>
                                                            </StackPanel>
                                                        </Grid>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,14,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Install behavior" Foreground="#FFE2E8F0" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Control how aggressively LibreSpot resets the current install and whether Spotify opens when the run is done." Foreground="#FF64748B" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkCleanInstall" Content="Remove the existing setup first" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                                        <CheckBox Name="ChkLaunchAfter" Content="Launch Spotify when finished" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                                        <TextBlock Text="LibreSpot remembers these custom choices after setup starts, so future reapply runs stay consistent." Foreground="#FF64748B" FontSize="10.5" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </StackPanel></ScrollViewer>

                                <!-- ===== MAINTENANCE PANEL ===== -->
                                <ScrollViewer Name="PanelMaint" Visibility="Collapsed" VerticalScrollBarVisibility="Auto"><StackPanel Margin="4,6,4,0">
                                    <TextBlock Text="Maintenance and recovery" Foreground="#FFF8FAFC" FontSize="21" FontWeight="Bold"/>
                                    <TextBlock Text="Check the current install, back up what matters, reapply patches after Spotify updates, or remove everything cleanly when you want to start over." Foreground="#FF94A3B8" FontSize="12.5" Margin="0,8,0,18" TextWrapping="Wrap"/>
                                    <Border Style="{StaticResource SurfaceCard}" Margin="0,0,0,18">
                                        <Grid>
                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                            <StackPanel>
                                                <TextBlock Name="MaintenanceOverviewTitle" Text="Scanning the current setup..." Foreground="#FFF8FAFC" FontSize="15.5" FontWeight="Bold"/>
                                                <TextBlock Name="MaintenanceOverviewText" Text="LibreSpot is checking which parts of the Spotify stack are installed so recovery actions can stay predictable." Foreground="#FF94A3B8" FontSize="12" Margin="0,8,0,0" TextWrapping="Wrap" MaxWidth="620"/>
                                            </StackPanel>
                                            <WrapPanel Grid.Column="1" VerticalAlignment="Top" Margin="20,0,0,0">
                                                <Border Background="#120f1b12" BorderBrush="#1f3d2b" BorderThickness="1" CornerRadius="999" Padding="12,6" Margin="0,0,10,10"><TextBlock Text="Safer recovery" Foreground="#FF86efac" FontSize="10.5" FontWeight="SemiBold"/></Border>
                                                <Border Background="#11101d2a" BorderBrush="#1d3347" BorderThickness="1" CornerRadius="999" Padding="12,6" Margin="0,0,0,10"><TextBlock Text="Pinned versions" Foreground="#FF7dd3fc" FontSize="10.5" FontWeight="SemiBold"/></Border>
                                            </WrapPanel>
                                        </Grid>
                                    </Border>

                                    <Grid Margin="0,0,0,20">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Name="StatusCardSpotify" Grid.Column="0" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="Spotify" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusSpotify" Text="Checking…" Foreground="#FFE2E8F0" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                        <Border Name="StatusCardSpotX" Grid.Column="2" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="SpotX" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusSpotX" Text="Checking…" Foreground="#FFE2E8F0" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                        <Border Name="StatusCardSpicetify" Grid.Column="4" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="Spicetify" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusSpicetify" Text="Checking…" Foreground="#FFE2E8F0" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                        <Border Name="StatusCardMarketplace" Grid.Column="6" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="Marketplace" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusMarketplace" Text="Checking…" Foreground="#FFE2E8F0" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                        <Border Name="StatusCardTheme" Grid.Column="8" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="Theme" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusTheme" Text="Checking…" Foreground="#FFE2E8F0" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                    </Grid>

                                    <Border Style="{StaticResource SurfaceCard}" Margin="0,0,0,20">
                                        <Grid>
                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                            <Border Grid.Column="0" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Detected stack" Style="{StaticResource ValueTileLabel}"/>
                                                    <TextBlock Name="MaintenanceMetricStackValue" Text="Scanning..." Style="{StaticResource ValueTileValue}" Margin="0,8,0,0"/>
                                                    <TextBlock Name="MaintenanceMetricStackDetail" Text="Checking Spotify, SpotX, Spicetify, Marketplace, and active theming." Style="{StaticResource HelperText}" Margin="0,8,0,0"/>
                                                </StackPanel>
                                            </Border>
                                            <Border Grid.Column="2" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Recovery backups" Style="{StaticResource ValueTileLabel}"/>
                                                    <TextBlock Name="MaintenanceMetricBackupValue" Text="Scanning..." Style="{StaticResource ValueTileValue}" Margin="0,8,0,0"/>
                                                    <TextBlock Name="MaintenanceMetricBackupDetail" Text="LibreSpot rotates up to five Spicetify snapshots in your backup folder." Style="{StaticResource HelperText}" Margin="0,8,0,0"/>
                                                </StackPanel>
                                            </Border>
                                            <Border Grid.Column="4" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Best next step" Style="{StaticResource ValueTileLabel}"/>
                                                    <TextBlock Name="MaintenanceMetricNextStepValue" Text="Preparing guidance" Style="{StaticResource ValueTileValue}" Margin="0,8,0,0"/>
                                                    <TextBlock Name="MaintenanceMetricNextStepDetail" Text="LibreSpot will suggest the safest maintenance action once the current setup is detected." Style="{StaticResource HelperText}" Margin="0,8,0,0"/>
                                                </StackPanel>
                                            </Border>
                                        </Grid>
                                    </Border>

                                    <Grid>
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="20"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Grid.Column="0" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Protect and repair" Foreground="#FFF8FAFC" FontSize="15" FontWeight="Bold"/>
                                                <TextBlock Text="Keep your current setup recoverable, compare pinned versions, or reapply patches after an update." Foreground="#FF94A3B8" FontSize="12" TextWrapping="Wrap" Margin="0,8,0,6"/>
                                                <Button Name="BtnBackupConfig" Style="{StaticResource MaintButton}"><StackPanel><TextBlock Text="Create configuration backup" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="13.5" FontWeight="SemiBold"/><TextBlock Text="Save themes, extensions, and Spicetify settings before making a change." Foreground="#FF94A3B8" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnRestoreConfig" Style="{StaticResource MaintButton}"><StackPanel><TextBlock Text="Restore the newest backup" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="13.5" FontWeight="SemiBold"/><TextBlock Text="Bring back the latest saved Spicetify configuration and apply it immediately." Foreground="#FF94A3B8" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnCheckUpdates" Style="{StaticResource MaintButton}"><StackPanel><TextBlock Text="Check pinned versions" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="13.5" FontWeight="SemiBold"/><TextBlock Text="Compare LibreSpot's pinned releases against the latest upstream versions." Foreground="#FF94A3B8" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnReapply" Style="{StaticResource MaintButton}"><StackPanel><TextBlock Text="Reapply after a Spotify update" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="13.5" FontWeight="SemiBold"/><TextBlock Text="Run SpotX again and reapply Spicetify without rebuilding your preferences from scratch." Foreground="#FF94A3B8" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Border Background="#FF0b1118" BorderBrush="#FF1c2a3c" BorderThickness="1" CornerRadius="10" Padding="14,12" Margin="0,10,0,0">
                                                    <StackPanel>
                                                        <CheckBox Name="ChkAutoReapply" Content="Auto-reapply when Spotify updates itself" Style="{StaticResource DarkCheckBox}" ToolTip="Registers a per-user scheduled task that watches Spotify.exe's version number and reruns the saved SpotX config silently whenever it changes."/>
                                                        <TextBlock Name="AutoReapplyStatusText" Text="Scheduled task: not installed" Foreground="#FF94A3B8" FontSize="11" Margin="28,4,0,0" TextWrapping="Wrap"/>
                                                        <TextBlock Text="The watcher only runs when Spotify is closed, skips automatically if no saved LibreSpot config exists, and writes every action to watcher.log next to the install log." Foreground="#FF64748B" FontSize="10.5" Margin="28,4,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>
                                            </StackPanel>
                                        </Border>
                                        <Border Grid.Column="2" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Restore or remove modifications" Foreground="#FFF8FAFC" FontSize="15" FontWeight="Bold"/>
                                                <TextBlock Text="Use the lighter recovery option first. Full Reset is intentionally destructive and best when you want to start clean." Foreground="#FF94A3B8" FontSize="12" TextWrapping="Wrap" Margin="0,8,0,6"/>
                                                <Button Name="BtnSpicetifyRestore" Style="{StaticResource MaintButton}"><StackPanel><TextBlock Text="Restore vanilla Spotify" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="13.5" FontWeight="SemiBold"/><TextBlock Text="Remove Spicetify themes and extensions while keeping SpotX patching in place." Foreground="#FF94A3B8" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnUninstallSpicetify" Style="{StaticResource MaintButton}"><StackPanel><TextBlock Text="Uninstall Spicetify only" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="13.5" FontWeight="SemiBold"/><TextBlock Text="Remove the Spicetify CLI and configuration but leave Spotify and SpotX in place." Foreground="#FF94A3B8" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnFullReset" Style="{StaticResource DangerMaintButton}"><StackPanel><TextBlock Text="Full Reset" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="13.5" FontWeight="SemiBold"/><TextBlock Text="Restore vanilla Spotify, remove SpotX and Spicetify, uninstall Spotify, and clean leftover files." Foreground="#FFFDA4AF" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </StackPanel></ScrollViewer>
                            </Grid></Border>
                            <Grid Grid.Row="3" Margin="0,18,0,0">
                                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="20"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                <Border Grid.Column="0" Style="{StaticResource SurfaceCard}" Padding="18,14">
                                    <StackPanel>
                                        <TextBlock Text="Install snapshot" Foreground="#FF94A3B8" FontSize="11" FontWeight="SemiBold"/>
                                        <TextBlock Name="SelectionSummary" Foreground="#FFE2E8F0" FontSize="12.75" VerticalAlignment="Center" TextWrapping="Wrap" Margin="0,6,0,0"/>
                                        <Grid Margin="0,10,0,0">
                                            <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                            <Border Name="SelectionStateBadge" Background="#11101d2a" BorderBrush="#1d3347" BorderThickness="1" CornerRadius="999" Padding="10,4" VerticalAlignment="Top">
                                                <TextBlock Name="SelectionStateBadgeText" Text="Ready" Foreground="#FF7dd3fc" FontSize="10.5" FontWeight="SemiBold"/>
                                            </Border>
                                            <TextBlock Grid.Column="2" Name="SelectionStateDetail" Foreground="#FF64748B" FontSize="11.25" TextWrapping="Wrap" VerticalAlignment="Center"/>
                                        </Grid>
                                    </StackPanel>
                                </Border>
                                <StackPanel Grid.Column="2" Name="FooterActionPanel" HorizontalAlignment="Right">
                                    <Button Name="BtnInstall" Content="Install recommended setup" Foreground="#FF04130a" BorderBrush="#FF3dd06f" Style="{StaticResource ActionButton}" Width="300" HorizontalAlignment="Right">
                                        <Button.Background><LinearGradientBrush StartPoint="0,0.5" EndPoint="1,0.5">
                                            <GradientStop Color="#FF22c55e" Offset="0"/><GradientStop Color="#FF86efac" Offset="1"/>
                                        </LinearGradientBrush></Button.Background></Button>
                                    <TextBlock Name="ActionFooterNote" Text="Settings save when setup begins." Foreground="#FF64748B" FontSize="11" Margin="0,10,0,0" HorizontalAlignment="Right" TextWrapping="Wrap" MaxWidth="300" TextAlignment="Right"/>
                                </StackPanel>
                            </Grid>
                        </Grid>
                        <!-- ===== INSTALL PAGE ===== -->
                        <Grid Name="PageInstall" Visibility="Collapsed"><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                            <Border Grid.Row="0" Style="{StaticResource SurfaceCard}" Margin="0,0,0,16">
                                <Grid>
                                    <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                                    <Grid>
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                        <StackPanel>
                                            <TextBlock Name="InstallTitle" Text="Preparing setup" Foreground="#FFF8FAFC" FontSize="20" FontWeight="Bold"/>
                                            <TextBlock Name="InstallContext" Text="LibreSpot keeps the interface responsive while it downloads, patches, and applies your selection." Foreground="#FF94A3B8" FontSize="12.5" TextWrapping="Wrap" Margin="0,8,0,0" MaxWidth="700"/>
                                        </StackPanel>
                                        <WrapPanel Grid.Column="1" VerticalAlignment="Top" Margin="20,0,0,0">
                                            <Border Background="#120f1b12" BorderBrush="#1f3d2b" BorderThickness="1" CornerRadius="999" Padding="12,6" Margin="0,0,10,10"><TextBlock Text="Live log" Foreground="#FF86efac" FontSize="10.5" FontWeight="SemiBold"/></Border>
                                            <Border Background="#11101d2a" BorderBrush="#1d3347" BorderThickness="1" CornerRadius="999" Padding="12,6" Margin="0,0,0,10"><TextBlock Text="Safe to leave open" Foreground="#FF7dd3fc" FontSize="10.5" FontWeight="SemiBold"/></Border>
                                        </WrapPanel>
                                    </Grid>
                                    <Grid Grid.Row="1" Margin="0,18,0,0">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="10"/><ColumnDefinition Width="*"/><ColumnDefinition Width="10"/><ColumnDefinition Width="*"/><ColumnDefinition Width="10"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Name="InstallStagePrepare" Grid.Column="0" Style="{StaticResource InsetPanel}" Padding="14,12">
                                            <TextBlock Name="InstallStagePrepareText" Text="Prepare" Foreground="#FFE2E8F0" FontSize="11.5" FontWeight="SemiBold"/>
                                        </Border>
                                        <Border Name="InstallStageRun" Grid.Column="2" Style="{StaticResource InsetPanel}" Padding="14,12">
                                            <TextBlock Name="InstallStageRunText" Text="Run" Foreground="#FF94A3B8" FontSize="11.5" FontWeight="SemiBold"/>
                                        </Border>
                                        <Border Name="InstallStageVerify" Grid.Column="4" Style="{StaticResource InsetPanel}" Padding="14,12">
                                            <TextBlock Name="InstallStageVerifyText" Text="Verify" Foreground="#FF94A3B8" FontSize="11.5" FontWeight="SemiBold"/>
                                        </Border>
                                        <Border Name="InstallStageComplete" Grid.Column="6" Style="{StaticResource InsetPanel}" Padding="14,12">
                                            <TextBlock Name="InstallStageCompleteText" Text="Complete" Foreground="#FF94A3B8" FontSize="11.5" FontWeight="SemiBold"/>
                                        </Border>
                                    </Grid>
                                </Grid>
                            </Border>
                            <Border Grid.Row="1" CornerRadius="14" BorderBrush="#FF17212c" BorderThickness="1" ClipToBounds="True"><Grid>
                                <Grid.RowDefinitions><RowDefinition Height="42"/><RowDefinition Height="*"/></Grid.RowDefinitions>
                                <Border Grid.Row="0" Background="#FF0b1118" Padding="16,0"><Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                                        <Ellipse Width="8" Height="8" Fill="#FF253242" Margin="0,0,6,0"/><Ellipse Width="8" Height="8" Fill="#FF253242" Margin="0,0,6,0"/><Ellipse Width="8" Height="8" Fill="#FF253242" Margin="0,0,14,0"/>
                                        <TextBlock Text="Live setup log" Foreground="#FFE2E8F0" FontSize="11.5" FontWeight="SemiBold"/></StackPanel>
                                    <TextBlock Grid.Column="1" Text="The UI may trim older lines, but the full log always stays copyable." Foreground="#FF64748B" FontSize="11" VerticalAlignment="Center"/></Grid></Border>
                                <Border Grid.Row="1" Background="#FF05090f" Padding="16,14">
                                    <ScrollViewer Name="LogScroller" VerticalScrollBarVisibility="Auto">
                                        <TextBlock Name="LogOutput" Foreground="#FFB7C4D4" FontFamily="Cascadia Mono, Consolas, Courier New" FontSize="11.75" TextWrapping="Wrap"/>
                                    </ScrollViewer></Border>
                            </Grid></Border>
                            <Border Grid.Row="2" Style="{StaticResource SurfaceCard}" Margin="0,16,0,0"><StackPanel><Grid>
                                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
                                    <TextBlock Name="StatusText" Text="Checking prerequisites..." Foreground="#FFF8FAFC" FontSize="13.5" FontWeight="SemiBold"/>
                                    <TextBlock Name="ElapsedTime" Text="" Foreground="#FF64748B" FontSize="11.5" VerticalAlignment="Center" Margin="14,0,0,0"/></StackPanel>
                                <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Right">
                                    <TextBlock Name="ProgressPercentText" Text="0%" Foreground="#FF64748B" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center" Margin="0,0,12,0"/>
                                    <TextBlock Name="StepIndicator" Text="Ready when you are" Foreground="#FF4ade80" FontSize="13.5" FontWeight="SemiBold" HorizontalAlignment="Right"/>
                                </StackPanel></Grid>
                                <ProgressBar Name="MainProgress" Height="8" Margin="0,12,0,0" Template="{StaticResource RoundProgress}" Background="#FF1f2937" Foreground="#FF4ade80" Minimum="0" Maximum="100" Value="0"/>
                                <TextBlock Text="Stage markers above adapt to the action you started, so you can tell at a glance where the run is even while the detailed log keeps scrolling." Foreground="#FF64748B" FontSize="11.5" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                            <StackPanel Grid.Row="3" Margin="0,16,0,0" Orientation="Horizontal" HorizontalAlignment="Right">
                                <Button Name="BtnCopyLog" Content="Copy full log" Background="#FF0f1720" Style="{StaticResource ActionButton}" Width="132" Margin="0,0,8,0" Visibility="Collapsed"/>
                                <Button Name="BtnBackToConfig" Content="Return to setup" Background="#FF0b1118" Style="{StaticResource ActionButton}" Width="140" Margin="0,0,8,0" Visibility="Collapsed"/>
                                <Button Name="CloseBtn" Content="Close" Background="#FF0b1118" Style="{StaticResource ActionButton}" Width="110" Visibility="Collapsed"/></StackPanel>
                        </Grid>
                    </Grid>
                </Grid>
            </Grid>
        </Border>
    </Grid>
</Window>
"@

# =============================================================================
# 6. UI INITIALIZATION
# =============================================================================
try { $reader = New-Object System.Xml.XmlNodeReader ([xml]$xaml); $window = [Windows.Markup.XamlReader]::Load($reader) }
catch { Write-Error "XAML Failed: $($_.Exception.Message)"; Exit }
$ErrorActionPreference = 'Stop'
function Get-LibreSpotBrandFrame {
    $candidatePaths = @(
        (Join-Path $PSScriptRoot 'LibreSpot.ico'),
        (Join-Path $PSScriptRoot 'icon.ico')
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }

    foreach ($candidate in $candidatePaths) {
        try {
            $uri = New-Object System.Uri($candidate)
            $decoder = New-Object System.Windows.Media.Imaging.IconBitmapDecoder(
                $uri,
                [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
                [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
            )
            $frame = $decoder.Frames |
                Sort-Object @{ Expression = { [math]::Abs($_.PixelWidth - 64) } }, @{ Expression = { -1 * $_.PixelWidth } } |
                Select-Object -First 1
            if ($frame) {
                if ($frame.CanFreeze) { $frame.Freeze() }
                return $frame
            }
        } catch {}
    }

    return $null
}

$script:BrandIconFrame = Get-LibreSpotBrandFrame
try {
    if ($script:BrandIconFrame) { $window.Icon = $script:BrandIconFrame }
} catch {}

$ui = @{}
@('LinkSpotX','LinkSpicetify','LinkGitHub','UpdateBanner','LinkUpdate','MinimizeBtn','CloseTitleBtn','PageConfig','PageInstall',
  'ModeHeadline','ModeSummaryText','SelectionSummary','SelectionStateBadge','SelectionStateBadgeText','SelectionStateDetail','InstallTitle','InstallContext',
  'ModeEasy','ModeCustom','ModeMaint','PanelEasy','PanelCustom','PanelMaint','BtnInstall','BtnResetCustomDefaults','LyricsThemePanel',
  'CustomSnapshotPlanValue','CustomSnapshotThemeValue','CustomSnapshotExtensionsValue','CustomSnapshotMemoryValue',
  'ChkNewTheme','ChkPodcastsOff','ChkAdSectionsOff','ChkBlockUpdate','ChkPremium','ChkLyrics','CmbLyricsTheme',
  'ChkTopSearch','ChkRightSidebarOff','ChkRightSidebarColor','ChkCanvasHomeOff','ChkHomeSubOff','ChkOldLyrics','ChkHideColIconOff',
  'ChkPlus','ChkNewFullscreen','ChkFunnyProgress','ChkExpSpotify','ChkLyricsBlock',
  'ChkDisableStartup','ChkNoShortcut','ChkStartSpoti','TxtCacheLimit','CmbTheme','CmbScheme','PreviewBorder','ThemePreviewImg','PreviewLabel','ChkMarketplace',
  'ChkSendVersionOff','ChkDevTools','ChkMirror','ChkConfirmUninstall','CmbDownloadMethod','CmbSpotifyVersion','SpotifyVersionHint',
  'ChkExt_fullAppDisplay','ChkExt_shuffle','ChkExt_trashbin','ChkExt_keyboard','ChkExt_bookmark','ChkExt_loopyLoop',
  'ChkExt_popupLyrics','ChkExt_autoSkipVideo','ChkExt_autoSkipExplicit','ChkExt_webNowPlaying',
  'ChkCleanInstall','ChkLaunchAfter',
  'MaintenanceOverviewTitle','MaintenanceOverviewText',
  'MaintenanceMetricStackValue','MaintenanceMetricStackDetail','MaintenanceMetricBackupValue','MaintenanceMetricBackupDetail','MaintenanceMetricNextStepValue','MaintenanceMetricNextStepDetail',
  'StatusCardSpotify','StatusCardSpotX','StatusCardSpicetify','StatusCardMarketplace','StatusCardTheme',
  'StatusSpotify','StatusSpotX','StatusSpicetify','StatusMarketplace','StatusTheme',
  'BtnBackupConfig','BtnRestoreConfig','BtnCheckUpdates','BtnReapply','BtnSpicetifyRestore','BtnUninstallSpicetify','BtnFullReset',
  'ChkAutoReapply','AutoReapplyStatusText',
  'InstallStagePrepare','InstallStageRun','InstallStageVerify','InstallStageComplete',
  'InstallStagePrepareText','InstallStageRunText','InstallStageVerifyText','InstallStageCompleteText',
  'LogScroller','LogOutput','StatusText','ElapsedTime','ProgressPercentText','StepIndicator','MainProgress','BtnCopyLog','BtnBackToConfig','CloseBtn',
  'FooterActionPanel','ActionFooterNote','TitleText','TitleLogo','TitleBar'
) | ForEach-Object { $el = $window.FindName($_); if ($el) { $ui[$_] = $el } }

try {
    if ($ui.ContainsKey('TitleLogo') -and $script:BrandIconFrame) { $ui['TitleLogo'].Source = $script:BrandIconFrame }
} catch {}

$extCheckboxMap = [ordered]@{
    'ChkExt_fullAppDisplay'='fullAppDisplay.js'; 'ChkExt_shuffle'='shuffle+.js'; 'ChkExt_trashbin'='trashbin.js'
    'ChkExt_keyboard'='keyboardShortcut.js'; 'ChkExt_bookmark'='bookmark.js'; 'ChkExt_loopyLoop'='loopyLoop.js'
    'ChkExt_popupLyrics'='popupLyrics.js'; 'ChkExt_autoSkipVideo'='autoSkipVideo.js'
    'ChkExt_autoSkipExplicit'='autoSkipExplicit.js'; 'ChkExt_webNowPlaying'='webnowplaying.js'
}

foreach ($ck in $extCheckboxMap.Keys) {
    $ef = $extCheckboxMap[$ck]
    if ($ui[$ck] -and $global:BuiltInExtensions.Contains($ef)) { $ui[$ck].ToolTip = $global:BuiltInExtensions[$ef] }
}

foreach ($theme in $global:ThemeData.Keys) {
    $item = New-Object System.Windows.Controls.ComboBoxItem; $item.Content = $theme
    $item.Style = $window.FindResource("DarkComboBoxItem"); $ui['CmbTheme'].Items.Add($item) | Out-Null
}
# Theme preview image cache and loader
$script:previewCache = @{}
$script:previewLoading = $false
function Update-ThemePreview {
    if ($script:previewLoading) { return }  # re-entrancy guard
    $themeName = if ($ui['CmbTheme'].SelectedItem) { $ui['CmbTheme'].SelectedItem.Content } else { $null }
    $schemeName = if ($ui['CmbScheme'].SelectedItem) { $ui['CmbScheme'].SelectedItem.Content } else { $null }
    if (-not $themeName -or $themeName -eq '(None - Marketplace Only)') {
        $ui['ThemePreviewImg'].Source = $null
        $ui['PreviewLabel'].Visibility = 'Visible'
        $ui['PreviewLabel'].Text = 'Marketplace-only keeps Spotify close to stock so you can browse themes later from inside the app.'
        return
    }
    $td = $global:ThemeData[$themeName]; if (-not $td -or -not $td.Preview -or $td.Preview.Count -eq 0) {
        $ui['ThemePreviewImg'].Source = $null
        $ui['PreviewLabel'].Visibility = 'Visible'
        $ui['PreviewLabel'].Text = "No bundled preview is available for $themeName."
        return
    }
    $imgPath = if ($schemeName -and $td.Preview.ContainsKey($schemeName)) { $td.Preview[$schemeName] }
               elseif ($td.Preview.ContainsKey('_default')) { $td.Preview['_default'] } else { $null }
    if (-not $imgPath) {
        $ui['ThemePreviewImg'].Source = $null
        $ui['PreviewLabel'].Visibility = 'Visible'
        $ui['PreviewLabel'].Text = "No bundled preview is available for $themeName."
        return
    }
    $url = "$global:THEMES_RAW_BASE/$imgPath"
    if ($script:previewCache.ContainsKey($url)) {
        $ui['ThemePreviewImg'].Source = $script:previewCache[$url]; $ui['PreviewLabel'].Visibility = 'Collapsed'; return
    }
    $ui['ThemePreviewImg'].Source = $null; $ui['PreviewLabel'].Visibility = 'Visible'; $ui['PreviewLabel'].Text = "Loading the $themeName preview..."
    # Force UI update before blocking download
    [System.Windows.Forms.Application]::DoEvents()
    $script:previewLoading = $true
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $wc = New-Object System.Net.WebClient
        try {
            $wc.Headers.Add('User-Agent', 'LibreSpot')
            $data = $wc.DownloadData($url)
        } finally { $wc.Dispose() }
        $ms = New-Object System.IO.MemoryStream(,$data)
        $bmp = New-Object System.Windows.Media.Imaging.BitmapImage
        $bmp.BeginInit(); $bmp.StreamSource = $ms; $bmp.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad; $bmp.EndInit(); $bmp.Freeze()
        $script:previewCache[$url] = $bmp
        $ui['ThemePreviewImg'].Source = $bmp; $ui['PreviewLabel'].Visibility = 'Collapsed'
    } catch {
        $ui['ThemePreviewImg'].Source = $null
        $ui['PreviewLabel'].Visibility = 'Visible'
        $ui['PreviewLabel'].Text = "Preview unavailable for $themeName right now."
    } finally {
        $script:previewLoading = $false
    }
}

function Get-ComboSelectionText {
    param([string]$Name, [string]$Fallback = '')
    if ($ui.ContainsKey($Name) -and $ui[$Name].SelectedItem) {
        return [string]$ui[$Name].SelectedItem.Content
    }
    return $Fallback
}

function Set-UiEnabledState {
    param(
        [string]$Name,
        [bool]$Enabled,
        [string]$EnabledToolTip = $null,
        [string]$DisabledToolTip = $null
    )
    if (-not $ui.ContainsKey($Name)) { return }
    $control = $ui[$Name]
    $control.IsEnabled = $Enabled
    $control.Opacity = if ($Enabled) { 1.0 } else { 0.48 }
    if ($Enabled -and $EnabledToolTip) {
        $control.ToolTip = $EnabledToolTip
    } elseif (-not $Enabled -and $DisabledToolTip) {
        $control.ToolTip = $DisabledToolTip
    }
}

function Update-DependentControlState {
    $lyricsEnabled = [bool]$ui['ChkLyrics'].IsChecked
    $ui['LyricsThemePanel'].Visibility = if ($lyricsEnabled) { 'Visible' } else { 'Collapsed' }
    if (-not $lyricsEnabled -and [bool]$ui['ChkOldLyrics'].IsChecked) { $ui['ChkOldLyrics'].IsChecked = $false }
    if (-not $lyricsEnabled -and [bool]$ui['ChkLyricsBlock'].IsChecked) { $ui['ChkLyricsBlock'].IsChecked = $false }
    $oldLyricsOn = [bool]$ui['ChkOldLyrics'].IsChecked
    $lyricsBlockOn = [bool]$ui['ChkLyricsBlock'].IsChecked
    Set-UiEnabledState -Name 'ChkOldLyrics' -Enabled ($lyricsEnabled -and -not $lyricsBlockOn) -EnabledToolTip 'Revert to Spotify''s previous lyrics interface.' -DisabledToolTip $(if (-not $lyricsEnabled) { 'Requires the lyrics layer to be enabled.' } else { 'Cannot combine with lyrics block.' })
    Set-UiEnabledState -Name 'ChkLyricsBlock' -Enabled ($lyricsEnabled -and -not $oldLyricsOn) -EnabledToolTip 'Completely disable native lyrics functionality.' -DisabledToolTip $(if (-not $lyricsEnabled) { 'Requires the lyrics layer to be enabled.' } else { 'Cannot combine with old lyrics.' })

    $sidebarEnabled = -not [bool]$ui['ChkRightSidebarOff'].IsChecked
    if (-not $sidebarEnabled -and [bool]$ui['ChkRightSidebarColor'].IsChecked) { $ui['ChkRightSidebarColor'].IsChecked = $false }
    Set-UiEnabledState -Name 'ChkRightSidebarColor' -Enabled $sidebarEnabled -EnabledToolTip 'Tint the Now Playing sidebar to the current album art.' -DisabledToolTip 'Unavailable while the right sidebar is disabled.'

    $themeName = Get-ComboSelectionText -Name 'CmbTheme' -Fallback '(None - Marketplace Only)'
    $hasBundledTheme = ($themeName -ne '(None - Marketplace Only)')
    Set-UiEnabledState -Name 'CmbScheme' -Enabled $hasBundledTheme -EnabledToolTip 'Choose the color scheme for the selected bundled theme.' -DisabledToolTip 'Pick a bundled theme first to unlock color schemes.'
    if ($ui.ContainsKey('PreviewBorder')) {
        $ui['PreviewBorder'].Opacity = if ($hasBundledTheme) { 1.0 } else { 0.82 }
    }
}

$ui['CmbTheme'].Add_SelectionChanged({
    [void]$ui['CmbScheme'].Items.Clear()
    $sel = $ui['CmbTheme'].SelectedItem; if ($sel -eq $null) { return }
    $st = $sel.Content
    if ($st -and $global:ThemeData.Contains($st)) {
        foreach ($s in $global:ThemeData[$st].Schemes) {
            $i = New-Object System.Windows.Controls.ComboBoxItem; $i.Content = $s
            $i.Style = $window.FindResource("DarkComboBoxItem"); $ui['CmbScheme'].Items.Add($i) | Out-Null
        }; $ui['CmbScheme'].SelectedIndex = 0
    }
    Update-ThemePreview
    Update-DependentControlState
})
$ui['CmbScheme'].Add_SelectionChanged({ Update-ThemePreview })
# Trigger initial scheme population via SelectedIndex assignment
$ui['CmbTheme'].SelectedIndex = 0

$ui['ChkLyrics'].Add_Checked({ Update-DependentControlState })
$ui['ChkLyrics'].Add_Unchecked({ Update-DependentControlState })
$ui['ChkOldLyrics'].Add_Checked({ Update-DependentControlState })
$ui['ChkOldLyrics'].Add_Unchecked({ Update-DependentControlState })
$ui['ChkLyricsBlock'].Add_Checked({ Update-DependentControlState })
$ui['ChkLyricsBlock'].Add_Unchecked({ Update-DependentControlState })
$ui['ChkRightSidebarOff'].Add_Checked({ Update-DependentControlState })
$ui['ChkRightSidebarOff'].Add_Unchecked({ Update-DependentControlState })

$ui['TxtCacheLimit'].Add_PreviewTextInput({ param($s,$e); $e.Handled = $e.Text -notmatch '^\d+$' })
$ui['TxtCacheLimit'].Add_LostFocus({
    $raw = [string]$ui['TxtCacheLimit'].Text
    if ([string]::IsNullOrWhiteSpace($raw)) {
        $ui['TxtCacheLimit'].Text = '0'
        return
    }
    $parsed = 0
    if (-not [int]::TryParse($raw, [ref]$parsed)) {
        $ui['TxtCacheLimit'].Text = '0'
        return
    }
    if ($parsed -gt 0 -and $parsed -lt 500) {
        $ui['TxtCacheLimit'].Text = '500'
    } else {
        $ui['TxtCacheLimit'].Text = [string]$parsed
    }
})

$premiumDependents = @('ChkPodcastsOff','ChkAdSectionsOff')
$ui['ChkPremium'].Add_Checked({
    foreach ($n in $premiumDependents) { $ui[$n].IsEnabled = $false; $ui[$n].Opacity = 0.4 }
    Update-DependentControlState
})
$ui['ChkPremium'].Add_Unchecked({
    foreach ($n in $premiumDependents) { $ui[$n].IsEnabled = $true;  $ui[$n].Opacity = 1.0 }
    Update-DependentControlState
})

$savedCfg = Load-LibreSpotConfig
if ($savedCfg) { try {
    if ($savedCfg.ContainsKey('SpotX_NewTheme'))       { $ui['ChkNewTheme'].IsChecked       = [bool]$savedCfg.SpotX_NewTheme }
    if ($savedCfg.ContainsKey('SpotX_PodcastsOff'))    { $ui['ChkPodcastsOff'].IsChecked    = [bool]$savedCfg.SpotX_PodcastsOff }
    if ($savedCfg.ContainsKey('SpotX_AdSectionsOff'))  { $ui['ChkAdSectionsOff'].IsChecked  = [bool]$savedCfg.SpotX_AdSectionsOff }
    if ($savedCfg.ContainsKey('SpotX_BlockUpdate'))    { $ui['ChkBlockUpdate'].IsChecked    = [bool]$savedCfg.SpotX_BlockUpdate }
    if ($savedCfg.ContainsKey('SpotX_Premium'))        { $ui['ChkPremium'].IsChecked        = [bool]$savedCfg.SpotX_Premium }
    if ($savedCfg.ContainsKey('SpotX_LyricsEnabled'))  { $ui['ChkLyrics'].IsChecked         = [bool]$savedCfg.SpotX_LyricsEnabled }
    if ($savedCfg.ContainsKey('SpotX_DisableStartup')) { $ui['ChkDisableStartup'].IsChecked = [bool]$savedCfg.SpotX_DisableStartup }
    if ($savedCfg.ContainsKey('SpotX_NoShortcut'))     { $ui['ChkNoShortcut'].IsChecked     = [bool]$savedCfg.SpotX_NoShortcut }
    if ($savedCfg.ContainsKey('SpotX_CacheLimit'))     { $ui['TxtCacheLimit'].Text          = [string]$savedCfg.SpotX_CacheLimit }
    if ($savedCfg.ContainsKey('SpotX_LyricsTheme')) {
        $lt = $savedCfg.SpotX_LyricsTheme
        for ($i=0; $i -lt $ui['CmbLyricsTheme'].Items.Count; $i++) { if ($ui['CmbLyricsTheme'].Items[$i].Content -eq $lt) { $ui['CmbLyricsTheme'].SelectedIndex = $i; break } }
    }
    if ($savedCfg.ContainsKey('SpotX_TopSearch'))       { $ui['ChkTopSearch'].IsChecked       = [bool]$savedCfg.SpotX_TopSearch }
    if ($savedCfg.ContainsKey('SpotX_RightSidebarOff')) { $ui['ChkRightSidebarOff'].IsChecked = [bool]$savedCfg.SpotX_RightSidebarOff }
    if ($savedCfg.ContainsKey('SpotX_RightSidebarClr')) { $ui['ChkRightSidebarColor'].IsChecked = [bool]$savedCfg.SpotX_RightSidebarClr }
    if ($savedCfg.ContainsKey('SpotX_CanvasHomeOff'))     { $ui['ChkCanvasHomeOff'].IsChecked   = [bool]$savedCfg.SpotX_CanvasHomeOff }
    if ($savedCfg.ContainsKey('SpotX_HomeSubOff'))      { $ui['ChkHomeSubOff'].IsChecked      = [bool]$savedCfg.SpotX_HomeSubOff }
    if ($savedCfg.ContainsKey('SpotX_OldLyrics'))       { $ui['ChkOldLyrics'].IsChecked       = [bool]$savedCfg.SpotX_OldLyrics }
    if ($savedCfg.ContainsKey('SpotX_HideColIconOff'))  { $ui['ChkHideColIconOff'].IsChecked  = [bool]$savedCfg.SpotX_HideColIconOff }
    if ($savedCfg.ContainsKey('SpotX_Plus'))            { $ui['ChkPlus'].IsChecked            = [bool]$savedCfg.SpotX_Plus }
    if ($savedCfg.ContainsKey('SpotX_NewFullscreen'))   { $ui['ChkNewFullscreen'].IsChecked   = [bool]$savedCfg.SpotX_NewFullscreen }
    if ($savedCfg.ContainsKey('SpotX_FunnyProgress'))   { $ui['ChkFunnyProgress'].IsChecked   = [bool]$savedCfg.SpotX_FunnyProgress }
    if ($savedCfg.ContainsKey('SpotX_ExpSpotify'))      { $ui['ChkExpSpotify'].IsChecked      = [bool]$savedCfg.SpotX_ExpSpotify }
    if ($savedCfg.ContainsKey('SpotX_LyricsBlock'))     { $ui['ChkLyricsBlock'].IsChecked     = [bool]$savedCfg.SpotX_LyricsBlock }
    if ($savedCfg.ContainsKey('Spicetify_Marketplace')){ $ui['ChkMarketplace'].IsChecked    = [bool]$savedCfg.Spicetify_Marketplace }
    if ($savedCfg.ContainsKey('CleanInstall'))         { $ui['ChkCleanInstall'].IsChecked   = [bool]$savedCfg.CleanInstall }
    if ($savedCfg.ContainsKey('LaunchAfter'))          { $ui['ChkLaunchAfter'].IsChecked    = [bool]$savedCfg.LaunchAfter }
    if ($savedCfg.ContainsKey('Spicetify_Theme')) {
        for ($i=0; $i -lt $ui['CmbTheme'].Items.Count; $i++) { if ($ui['CmbTheme'].Items[$i].Content -eq $savedCfg.Spicetify_Theme) { $ui['CmbTheme'].SelectedIndex = $i; break } }
    }
    if ($savedCfg.ContainsKey('Spicetify_Scheme')) {
        for ($i=0; $i -lt $ui['CmbScheme'].Items.Count; $i++) { if ($ui['CmbScheme'].Items[$i].Content -eq $savedCfg.Spicetify_Scheme) { $ui['CmbScheme'].SelectedIndex = $i; break } }
    }
    if ($savedCfg.ContainsKey('Spicetify_Extensions')) {
        $se = @($savedCfg.Spicetify_Extensions)
        foreach ($ck in $extCheckboxMap.Keys) { $ui[$ck].IsChecked = ($se -contains $extCheckboxMap[$ck]) }
    }
    if ($savedCfg.ContainsKey('Mode') -and [string]$savedCfg.Mode -eq 'Custom') {
        $ui['ModeCustom'].IsChecked = $true
    }
} catch {} }
$script:SavedConfigMode = if ($savedCfg -and $savedCfg.ContainsKey('Mode')) { [string]$savedCfg.Mode } else { $null }
$script:HasSavedConfig = [bool]$savedCfg
$script:HasSavedCustomConfig = ($script:SavedConfigMode -eq 'Custom')
$script:SavedConfigStamp = if ($script:HasSavedConfig -and (Test-Path $global:CONFIG_PATH)) { (Get-Item $global:CONFIG_PATH).LastWriteTime } else { $null }
$script:MaintenanceComponentCount = 0
$script:MaintenanceBackupCount = 0
$script:BaselineCustomConfig = $null
Update-DependentControlState
function Set-MaintenanceCardTone {
    param(
        [string]$CardName,
        [ValidateSet('success','info','muted','danger')]
        [string]$Tone = 'muted'
    )
    if (-not $ui.ContainsKey($CardName)) { return }
    $palette = switch ($Tone) {
        'success' { @{ Background = '#FF0f1b16'; Border = '#FF24563b' } }
        'info'    { @{ Background = '#FF0d1621'; Border = '#FF2d5e87' } }
        'danger'  { @{ Background = '#FF1a0f12'; Border = '#FF7f1d1d' } }
        default   { @{ Background = '#FF0c1219'; Border = '#FF23303d' } }
    }
    $ui[$CardName].Background = $script:BrushConverter.ConvertFromString($palette.Background)
    $ui[$CardName].BorderBrush = $script:BrushConverter.ConvertFromString($palette.Border)
}

function Set-SelectionSnapshotState {
    param(
        [ValidateSet('success','info','warning','muted','danger')]
        [string]$Tone = 'info',
        [string]$BadgeText = 'Ready',
        [string]$DetailText = ''
    )
    if (-not ($ui.ContainsKey('SelectionStateBadge') -and $ui.ContainsKey('SelectionStateBadgeText') -and $ui.ContainsKey('SelectionStateDetail'))) { return }
    $palette = switch ($Tone) {
        'success' { @{ Background = '#120f1b12'; Border = '#1f3d2b'; Foreground = '#FF86efac'; Detail = '#FF94A3B8' } }
        'warning' { @{ Background = '#FF191409'; Border = '#FF7c5a15'; Foreground = '#FFFCD34D'; Detail = '#FF94A3B8' } }
        'danger'  { @{ Background = '#FF1a0f12'; Border = '#FF7f1d1d'; Foreground = '#FFFDA4AF'; Detail = '#FF94A3B8' } }
        'muted'   { @{ Background = '#11111220'; Border = '#1b2433'; Foreground = '#FFCBD5E1'; Detail = '#FF64748B' } }
        default   { @{ Background = '#11101d2a'; Border = '#1d3347'; Foreground = '#FF7dd3fc'; Detail = '#FF94A3B8' } }
    }
    $ui['SelectionStateBadge'].Background = $script:BrushConverter.ConvertFromString($palette.Background)
    $ui['SelectionStateBadge'].BorderBrush = $script:BrushConverter.ConvertFromString($palette.Border)
    $ui['SelectionStateBadgeText'].Foreground = $script:BrushConverter.ConvertFromString($palette.Foreground)
    $ui['SelectionStateBadgeText'].Text = [string]$BadgeText
    $ui['SelectionStateDetail'].Foreground = $script:BrushConverter.ConvertFromString($palette.Detail)
    $ui['SelectionStateDetail'].Text = [string]$DetailText
    $ui['SelectionStateDetail'].Visibility = if ([string]::IsNullOrWhiteSpace($DetailText)) { 'Collapsed' } else { 'Visible' }
}

function Set-InstallStageState {
    param(
        [string]$BorderName,
        [string]$TextName,
        [ValidateSet('pending','active','complete','attention')]
        [string]$State = 'pending'
    )
    if (-not ($ui.ContainsKey($BorderName) -and $ui.ContainsKey($TextName))) { return }
    $palette = switch ($State) {
        'active'    { @{ Background = '#FF101d2a'; Border = '#FF2d5e87'; Foreground = '#FFE2F4FF' } }
        'complete'  { @{ Background = '#FF122015'; Border = '#FF24563b'; Foreground = '#FF86efac' } }
        'attention' { @{ Background = '#FF2a1115'; Border = '#FF7f1d1d'; Foreground = '#FFFECDD3' } }
        default     { @{ Background = '#FF091019'; Border = '#FF1a2634'; Foreground = '#FF94A3B8' } }
    }
    $ui[$BorderName].Background = $script:BrushConverter.ConvertFromString($palette.Background)
    $ui[$BorderName].BorderBrush = $script:BrushConverter.ConvertFromString($palette.Border)
    $ui[$TextName].Foreground = $script:BrushConverter.ConvertFromString($palette.Foreground)
}

function Set-InstallStageLabels {
    param(
        [string]$Prepare = 'Prepare',
        [string]$Run = 'Run',
        [string]$Verify = 'Verify',
        [string]$Complete = 'Complete'
    )
    $labelMap = @{
        InstallStagePrepareText  = $Prepare
        InstallStageRunText      = $Run
        InstallStageVerifyText   = $Verify
        InstallStageCompleteText = $Complete
    }
    foreach ($name in $labelMap.Keys) {
        if ($ui.ContainsKey($name)) {
            $ui[$name].Text = [string]$labelMap[$name]
        }
    }
}

function Update-InstallStageVisual {
    if (-not $ui.ContainsKey('PageInstall')) { return }
    if ($ui['PageInstall'].Visibility -ne 'Visible') {
        if ($ui.ContainsKey('ProgressPercentText')) { $ui['ProgressPercentText'].Text = '0%' }
        Set-InstallStageState -BorderName 'InstallStagePrepare' -TextName 'InstallStagePrepareText' -State 'pending'
        Set-InstallStageState -BorderName 'InstallStageRun' -TextName 'InstallStageRunText' -State 'pending'
        Set-InstallStageState -BorderName 'InstallStageVerify' -TextName 'InstallStageVerifyText' -State 'pending'
        Set-InstallStageState -BorderName 'InstallStageComplete' -TextName 'InstallStageCompleteText' -State 'pending'
        return
    }

    $progress = [double]$ui['MainProgress'].Value
    if ($ui.ContainsKey('ProgressPercentText')) { $ui['ProgressPercentText'].Text = ('{0:0}%' -f $progress) }
    $statusText = [string]$ui['StatusText'].Text
    $stepText = [string]$ui['StepIndicator'].Text
    $isAttention = ($statusText -match 'stopped|needs attention') -or ($stepText -match 'Needs attention')

    if ($isAttention) {
        Set-InstallStageState -BorderName 'InstallStagePrepare' -TextName 'InstallStagePrepareText' -State 'complete'
        Set-InstallStageState -BorderName 'InstallStageRun' -TextName 'InstallStageRunText' -State 'complete'
        Set-InstallStageState -BorderName 'InstallStageVerify' -TextName 'InstallStageVerifyText' -State 'complete'
        Set-InstallStageState -BorderName 'InstallStageComplete' -TextName 'InstallStageCompleteText' -State 'attention'
        return
    }

    $activeIndex = if ($progress -ge 100) {
        3
    } elseif ($progress -ge 70) {
        2
    } elseif ($progress -ge 30) {
        1
    } else {
        0
    }

    $stages = @(
        @{ Border = 'InstallStagePrepare'; Text = 'InstallStagePrepareText' }
        @{ Border = 'InstallStageRun'; Text = 'InstallStageRunText' }
        @{ Border = 'InstallStageVerify'; Text = 'InstallStageVerifyText' }
        @{ Border = 'InstallStageComplete'; Text = 'InstallStageCompleteText' }
    )

    for ($index = 0; $index -lt $stages.Count; $index++) {
        $state = if ($progress -ge 100) {
            'complete'
        } elseif ($index -lt $activeIndex) {
            'complete'
        } elseif ($index -eq $activeIndex) {
            'active'
        } else {
            'pending'
        }
        Set-InstallStageState -BorderName $stages[$index].Border -TextName $stages[$index].Text -State $state
    }
}

function Get-ConfigFingerprint {
    param([hashtable]$Config)
    if (-not $Config) { return '' }
    $normalized = [ordered]@{
        Mode                   = [string]$Config.Mode
        CleanInstall           = [bool]$Config.CleanInstall
        LaunchAfter            = [bool]$Config.LaunchAfter
        SpotX_NewTheme         = [bool]$Config.SpotX_NewTheme
        SpotX_PodcastsOff      = [bool]$Config.SpotX_PodcastsOff
        SpotX_AdSectionsOff    = [bool]$Config.SpotX_AdSectionsOff
        SpotX_BlockUpdate      = [bool]$Config.SpotX_BlockUpdate
        SpotX_Premium          = [bool]$Config.SpotX_Premium
        SpotX_DisableStartup   = [bool]$Config.SpotX_DisableStartup
        SpotX_NoShortcut       = [bool]$Config.SpotX_NoShortcut
        SpotX_LyricsEnabled    = [bool]$Config.SpotX_LyricsEnabled
        SpotX_LyricsTheme      = [string]$Config.SpotX_LyricsTheme
        SpotX_TopSearch        = [bool]$Config.SpotX_TopSearch
        SpotX_RightSidebarOff  = [bool]$Config.SpotX_RightSidebarOff
        SpotX_RightSidebarClr  = [bool]$Config.SpotX_RightSidebarClr
        SpotX_CanvasHomeOff    = [bool]$Config.SpotX_CanvasHomeOff
        SpotX_HomeSubOff       = [bool]$Config.SpotX_HomeSubOff
        SpotX_OldLyrics        = [bool]$Config.SpotX_OldLyrics
        SpotX_HideColIconOff   = [bool]$Config.SpotX_HideColIconOff
        SpotX_Plus             = [bool]$Config.SpotX_Plus
        SpotX_NewFullscreen    = [bool]$Config.SpotX_NewFullscreen
        SpotX_FunnyProgress    = [bool]$Config.SpotX_FunnyProgress
        SpotX_ExpSpotify       = [bool]$Config.SpotX_ExpSpotify
        SpotX_LyricsBlock      = [bool]$Config.SpotX_LyricsBlock
        SpotX_CacheLimit       = [int]$Config.SpotX_CacheLimit
        SpotX_SendVersionOff   = [bool]$Config.SpotX_SendVersionOff
        SpotX_StartSpoti       = [bool]$Config.SpotX_StartSpoti
        SpotX_DevTools         = [bool]$Config.SpotX_DevTools
        SpotX_Mirror           = [bool]$Config.SpotX_Mirror
        SpotX_ConfirmUninstall = [bool]$Config.SpotX_ConfirmUninstall
        SpotX_DownloadMethod   = [string]$Config.SpotX_DownloadMethod
        SpotX_SpotifyVersionId = [string]$Config.SpotX_SpotifyVersionId
        Spicetify_Theme        = [string]$Config.Spicetify_Theme
        Spicetify_Scheme       = [string]$Config.Spicetify_Scheme
        Spicetify_Marketplace  = [bool]$Config.Spicetify_Marketplace
        Spicetify_Extensions   = @($Config.Spicetify_Extensions)
        AutoReapply_Enabled    = [bool]$Config.AutoReapply_Enabled
    }
    return ($normalized | ConvertTo-Json -Depth 4 -Compress)
}

function Capture-CustomConfigBaseline {
    if (-not (Get-Command Get-InstallConfig -ErrorAction SilentlyContinue)) {
        $script:BaselineCustomConfig = $null
        $script:SavedConfigStamp = if ($script:HasSavedConfig -and (Test-Path $global:CONFIG_PATH)) { (Get-Item $global:CONFIG_PATH).LastWriteTime } else { $null }
        return
    }
    try { $script:BaselineCustomConfig = Get-InstallConfig -EasyMode $false } catch { $script:BaselineCustomConfig = $null }
    $script:SavedConfigStamp = if ($script:HasSavedConfig -and (Test-Path $global:CONFIG_PATH)) { (Get-Item $global:CONFIG_PATH).LastWriteTime } else { $null }
}

function Test-HasUnsavedCustomChanges {
    if (-not (Get-Command Get-InstallConfig -ErrorAction SilentlyContinue)) { return $false }
    if (-not $script:BaselineCustomConfig) { return $false }
    try {
        $currentFingerprint = Get-ConfigFingerprint -Config (Get-InstallConfig -EasyMode $false)
        $baselineFingerprint = Get-ConfigFingerprint -Config $script:BaselineCustomConfig
        return ($currentFingerprint -ne $baselineFingerprint)
    } catch {
        return $false
    }
}

function Apply-ConfigToUi {
    param(
        [hashtable]$Config,
        [switch]$ForceCustomMode
    )
    if (-not $Config) { return }
    $normalized = Normalize-LibreSpotConfig -Config $Config

    $checkboxMap = @{
        'ChkNewTheme'          = 'SpotX_NewTheme'
        'ChkPodcastsOff'       = 'SpotX_PodcastsOff'
        'ChkAdSectionsOff'     = 'SpotX_AdSectionsOff'
        'ChkBlockUpdate'       = 'SpotX_BlockUpdate'
        'ChkPremium'           = 'SpotX_Premium'
        'ChkLyrics'            = 'SpotX_LyricsEnabled'
        'ChkTopSearch'         = 'SpotX_TopSearch'
        'ChkRightSidebarOff'   = 'SpotX_RightSidebarOff'
        'ChkRightSidebarColor' = 'SpotX_RightSidebarClr'
        'ChkCanvasHomeOff'     = 'SpotX_CanvasHomeOff'
        'ChkHomeSubOff'        = 'SpotX_HomeSubOff'
        'ChkOldLyrics'         = 'SpotX_OldLyrics'
        'ChkHideColIconOff'    = 'SpotX_HideColIconOff'
        'ChkDisableStartup'    = 'SpotX_DisableStartup'
        'ChkNoShortcut'        = 'SpotX_NoShortcut'
        'ChkStartSpoti'        = 'SpotX_StartSpoti'
        'ChkPlus'              = 'SpotX_Plus'
        'ChkNewFullscreen'     = 'SpotX_NewFullscreen'
        'ChkFunnyProgress'     = 'SpotX_FunnyProgress'
        'ChkExpSpotify'        = 'SpotX_ExpSpotify'
        'ChkLyricsBlock'       = 'SpotX_LyricsBlock'
        'ChkSendVersionOff'    = 'SpotX_SendVersionOff'
        'ChkDevTools'          = 'SpotX_DevTools'
        'ChkMirror'            = 'SpotX_Mirror'
        'ChkConfirmUninstall'  = 'SpotX_ConfirmUninstall'
        'ChkMarketplace'       = 'Spicetify_Marketplace'
        'ChkCleanInstall'      = 'CleanInstall'
        'ChkLaunchAfter'       = 'LaunchAfter'
        'ChkAutoReapply'       = 'AutoReapply_Enabled'
    }
    foreach ($name in $checkboxMap.Keys) {
        if ($ui.ContainsKey($name)) {
            $ui[$name].IsChecked = [bool]$normalized[$checkboxMap[$name]]
        }
    }

    if ($ui.ContainsKey('TxtCacheLimit')) {
        $ui['TxtCacheLimit'].Text = [string][int]$normalized.SpotX_CacheLimit
    }

    if ($ui.ContainsKey('CmbLyricsTheme')) {
        for ($i = 0; $i -lt $ui['CmbLyricsTheme'].Items.Count; $i++) {
            if ($ui['CmbLyricsTheme'].Items[$i].Content -eq $normalized.SpotX_LyricsTheme) {
                $ui['CmbLyricsTheme'].SelectedIndex = $i
                break
            }
        }
    }

    if ($ui.ContainsKey('CmbDownloadMethod')) {
        $target = if ([string]::IsNullOrWhiteSpace([string]$normalized.SpotX_DownloadMethod)) { 'auto' } else { [string]$normalized.SpotX_DownloadMethod }
        for ($i = 0; $i -lt $ui['CmbDownloadMethod'].Items.Count; $i++) {
            if ($ui['CmbDownloadMethod'].Items[$i].Content -eq $target) {
                $ui['CmbDownloadMethod'].SelectedIndex = $i
                break
            }
        }
    }

    if ($ui.ContainsKey('CmbSpotifyVersion')) {
        $target = [string]$normalized.SpotX_SpotifyVersionId
        for ($i = 0; $i -lt $ui['CmbSpotifyVersion'].Items.Count; $i++) {
            if ([string]$ui['CmbSpotifyVersion'].Items[$i].Tag -eq $target) {
                $ui['CmbSpotifyVersion'].SelectedIndex = $i
                break
            }
        }
    }

    if ($ui.ContainsKey('CmbTheme')) {
        $themeIndex = 0
        for ($i = 0; $i -lt $ui['CmbTheme'].Items.Count; $i++) {
            if ($ui['CmbTheme'].Items[$i].Content -eq $normalized.Spicetify_Theme) {
                $themeIndex = $i
                break
            }
        }
        if ($ui['CmbTheme'].Items.Count -gt 0) {
            $ui['CmbTheme'].SelectedIndex = $themeIndex
        }
    }

    if ($ui.ContainsKey('CmbScheme')) {
        $schemeIndex = 0
        for ($i = 0; $i -lt $ui['CmbScheme'].Items.Count; $i++) {
            if ($ui['CmbScheme'].Items[$i].Content -eq $normalized.Spicetify_Scheme) {
                $schemeIndex = $i
                break
            }
        }
        if ($ui['CmbScheme'].Items.Count -gt 0) {
            $ui['CmbScheme'].SelectedIndex = $schemeIndex
        }
    }

    $selectedExtensions = @($normalized.Spicetify_Extensions)
    foreach ($key in $extCheckboxMap.Keys) {
        if ($ui.ContainsKey($key)) {
            $ui[$key].IsChecked = ($selectedExtensions -contains $extCheckboxMap[$key])
        }
    }

    if ($ForceCustomMode -and $ui.ContainsKey('ModeCustom')) {
        $ui['ModeCustom'].IsChecked = $true
    }

    Update-DependentControlState
    Update-ThemePreview
    Update-ModePresentation
}

function Update-ModePresentation {
    $isEasy = [bool]$ui['ModeEasy'].IsChecked
    $isCustom = [bool]$ui['ModeCustom'].IsChecked
    $isMaint = [bool]$ui['ModeMaint'].IsChecked

    $ui['PanelEasy'].Visibility = if ($isEasy) { 'Visible' } else { 'Collapsed' }
    $ui['PanelCustom'].Visibility = if ($isCustom) { 'Visible' } else { 'Collapsed' }
    $ui['PanelMaint'].Visibility = if ($isMaint) { 'Visible' } else { 'Collapsed' }
    $ui['BtnInstall'].Visibility = if ($isMaint) { 'Collapsed' } else { 'Visible' }
    if ($ui.ContainsKey('FooterActionPanel')) { $ui['FooterActionPanel'].Visibility = if ($isMaint) { 'Collapsed' } else { 'Visible' } }

    if ($isEasy) {
        $ui['ModeHeadline'].Text = 'Recommended path for a first install'
        $ui['ModeSummaryText'].Text = 'LibreSpot handles cleanup, verified downloads, Spotify patching, Marketplace, and a reliable default extension set with recovery-friendly defaults.'
        $ui['SelectionSummary'].Text = 'Recommended setup: clean install, Marketplace included, three starter extensions, and Spotify opens when everything is ready.'
        Set-SelectionSnapshotState -Tone 'success' -BadgeText 'Pinned default stack' -DetailText 'Easy Install uses the verified cleanup path and saves the default recovery baseline when setup begins.'
        if ($ui.ContainsKey('ActionFooterNote')) { $ui['ActionFooterNote'].Text = 'Recommended defaults save when setup begins.' }
        $ui['BtnInstall'].Content = 'Start recommended setup'
        return
    }

    if ($isCustom) {
        $theme = Get-ComboSelectionText -Name 'CmbTheme' -Fallback '(None - Marketplace Only)'
        $scheme = Get-ComboSelectionText -Name 'CmbScheme' -Fallback 'Default'
        $themeLabel = if ($theme -eq '(None - Marketplace Only)') { 'Marketplace only' } elseif ($scheme -and $scheme -ne 'Default') { "$theme / $scheme" } else { $theme }
        $extCount = @($extCheckboxMap.Keys | Where-Object { $ui[$_].IsChecked }).Count
        $extLabel = if ($extCount -eq 1) { '1 extension' } else { "$extCount extensions" }
        $installLabel = if ($ui['ChkCleanInstall'].IsChecked) { 'clean install' } else { 'keep current Spotify install' }
        $marketplaceLabel = if ($ui['ChkMarketplace'].IsChecked) { 'Marketplace included' } else { 'Marketplace skipped' }
        $launchLabel = if ($ui['ChkLaunchAfter'].IsChecked) { 'launches Spotify when finished' } else { 'keeps Spotify closed when finished' }
        $savedStampText = if ($script:SavedConfigStamp) { $script:SavedConfigStamp.ToString('MMM d, yyyy h:mm tt') } else { $null }
        $hasUnsavedCustomChanges = Test-HasUnsavedCustomChanges
        $memoryNote = if ($script:HasSavedCustomConfig) {
            if ($savedStampText) { " Your previous custom choices were restored from disk. Last saved $savedStampText." } else { ' Your previous custom choices were restored from disk.' }
        } elseif ($script:SavedConfigMode -eq 'Easy') {
            ' Easy Install was the last saved mode. These custom choices will be remembered after your first custom setup run.'
        } else {
            ' LibreSpot will remember these choices after your first custom setup run.'
        }
        $unsavedNote = if ($hasUnsavedCustomChanges) { ' Not saved yet.' } else { '' }
        $memorySnapshot = if ($hasUnsavedCustomChanges) {
            'Unsaved edits'
        } elseif ($script:HasSavedCustomConfig) {
            'Saved to disk'
        } elseif ($script:SavedConfigMode -eq 'Easy') {
            'Ready after first run'
        } else {
            'Will save on install'
        }

        if ($ui.ContainsKey('CustomSnapshotPlanValue')) { $ui['CustomSnapshotPlanValue'].Text = if ($ui['ChkCleanInstall'].IsChecked) { 'Clean install' } else { 'Overlay current setup' } }
        if ($ui.ContainsKey('CustomSnapshotThemeValue')) { $ui['CustomSnapshotThemeValue'].Text = $themeLabel }
        if ($ui.ContainsKey('CustomSnapshotExtensionsValue')) { $ui['CustomSnapshotExtensionsValue'].Text = $extLabel }
        if ($ui.ContainsKey('CustomSnapshotMemoryValue')) { $ui['CustomSnapshotMemoryValue'].Text = $memorySnapshot }

        $snapshotTone = 'info'
        $snapshotBadge = 'Will save on install'
        $snapshotDetail = 'Custom choices become the remembered recovery baseline when setup begins.'
        if ($hasUnsavedCustomChanges) {
            $snapshotTone = 'warning'
            $snapshotBadge = 'Unsaved edits'
            $snapshotDetail = 'These changes are only in this window until you start setup.'
        } elseif ($script:HasSavedCustomConfig) {
            $snapshotTone = 'success'
            $snapshotBadge = 'Saved custom setup'
            $snapshotDetail = if ($savedStampText) { "Restored from disk. Last saved $savedStampText." } else { 'Restored from disk so reapply and recovery actions stay aligned.' }
        } elseif ($script:SavedConfigMode -eq 'Easy') {
            $snapshotBadge = 'Switching from Easy'
            $snapshotDetail = 'Start one custom run and LibreSpot will remember this tailored setup afterward.'
        }
        $dependencyNotes = @()
        if ([bool]$ui['ChkPremium'].IsChecked) { $dependencyNotes += 'Premium mode keeps ad-filter toggles off.' }
        if ($theme -eq '(None - Marketplace Only)') { $dependencyNotes += 'Pick a bundled theme if you want color schemes unlocked.' }
        if ($dependencyNotes.Count -gt 0) {
            $snapshotDetail = "{0} {1}" -f $snapshotDetail.TrimEnd('.'), ($dependencyNotes -join ' ')
        }

        $ui['ModeHeadline'].Text = 'Tune the experience without guesswork'
        $ui['ModeSummaryText'].Text = "Adjust Spotify cleanup, interface tweaks, themes, and extensions so follow-up installs stay fast and predictable.$memoryNote"
        $ui['SelectionSummary'].Text = "Custom setup: $installLabel, $themeLabel, $marketplaceLabel, $extLabel, and $launchLabel.$unsavedNote"
        Set-SelectionSnapshotState -Tone $snapshotTone -BadgeText $snapshotBadge -DetailText $snapshotDetail
        if ($ui.ContainsKey('ActionFooterNote')) {
            $ui['ActionFooterNote'].Text = if ($hasUnsavedCustomChanges) { 'Custom edits save when setup begins.' } else { 'Starting setup refreshes the remembered custom baseline.' }
        }
        $ui['BtnInstall'].Content = 'Install this setup'
        return
    }

    $ui['ModeHeadline'].Text = 'Recover, reapply, or clean up'
    Update-MaintenanceStatus
    $componentLabel = if ($script:MaintenanceComponentCount -eq 1) { '1 core component detected' } else { "$($script:MaintenanceComponentCount) core components detected" }
    $backupLabel = if ($script:MaintenanceBackupCount -eq 0) { 'no backups saved yet' } elseif ($script:MaintenanceBackupCount -eq 1) { '1 backup ready' } else { "$($script:MaintenanceBackupCount) backups ready" }
    $ui['ModeSummaryText'].Text = 'Inspect what is installed, restore backups, reapply pinned patches after Spotify updates, or roll the setup back cleanly.'
    $ui['SelectionSummary'].Text = "Maintenance snapshot: $componentLabel, $backupLabel, and destructive actions stay behind confirmation."
}

function Clear-CompletedRunspaceResources {
    if ($script:activeSyncHash -and $script:activeSyncHash.IsRunning) { return $false }
    if ($script:activeSyncHash) {
        $script:activeSyncHash.IsRunning = $false
        Start-Sleep -Milliseconds 150
    }
    foreach ($resource in @($script:openRunspaces)) {
        try { $resource.Dispose() } catch {}
    }
    $script:openRunspaces.Clear()
    if ($script:activeSyncHash -and -not $script:activeSyncHash.IsRunning) {
        $script:activeSyncHash = $null
    }
    return $true
}

# =============================================================================
# 7. UI EVENT HANDLERS
# =============================================================================
$lh = { param($s,$e); try { $psi = New-Object System.Diagnostics.ProcessStartInfo $e.Uri.AbsoluteUri; $psi.UseShellExecute = $true; [System.Diagnostics.Process]::Start($psi) | Out-Null } catch {} }
$ui['LinkSpotX'].Add_RequestNavigate($lh); $ui['LinkSpicetify'].Add_RequestNavigate($lh)
$ui['LinkGitHub'].Add_Click({ try { $psi = New-Object System.Diagnostics.ProcessStartInfo 'https://github.com/SysAdminDoc/LibreSpot'; $psi.UseShellExecute = $true; [System.Diagnostics.Process]::Start($psi) | Out-Null } catch {} })
$ui['LinkUpdate'].Add_Click({
    try {
        $target = if ($script:SelfUpdateTarget) { $script:SelfUpdateTarget } else { 'https://github.com/SysAdminDoc/LibreSpot/releases/latest' }
        $psi = New-Object System.Diagnostics.ProcessStartInfo $target
        $psi.UseShellExecute = $true
        [System.Diagnostics.Process]::Start($psi) | Out-Null
    } catch {}
})
if ($ui['TitleText']) { $ui['TitleText'].Text = "LibreSpot v$global:VERSION" }
$script:copyResetTimer = $null
$ui['BtnCopyLog'].Add_Click({
    try {
        $logText = $null
        if ($global:LOG_PATH -and (Test-Path -LiteralPath $global:LOG_PATH -PathType Leaf)) {
            try { $logText = Get-Content -LiteralPath $global:LOG_PATH -Raw -Encoding UTF8 -ErrorAction Stop } catch {}
        }
        if ([string]::IsNullOrWhiteSpace($logText)) { $logText = $ui['LogOutput'].Text }
        if ([string]::IsNullOrWhiteSpace($logText)) { $logText = '(No log content available)' }
        [System.Windows.Clipboard]::SetText($logText)
        $ui['BtnCopyLog'].Content = 'Log copied'
        if ($script:copyResetTimer) { $script:copyResetTimer.Stop() }
        $script:copyResetTimer = New-Object System.Windows.Threading.DispatcherTimer
        $script:copyResetTimer.Interval = [TimeSpan]::FromSeconds(1.8)
        $script:copyResetTimer.Add_Tick({
            $script:copyResetTimer.Stop()
            $ui['BtnCopyLog'].Content = 'Copy full log'
        })
        $script:copyResetTimer.Start()
    } catch {
        $ui['BtnCopyLog'].Content = 'Copy unavailable'
        if ($script:copyResetTimer) { $script:copyResetTimer.Stop() }
        $script:copyResetTimer = New-Object System.Windows.Threading.DispatcherTimer
        $script:copyResetTimer.Interval = [TimeSpan]::FromSeconds(2.5)
        $script:copyResetTimer.Add_Tick({
            $script:copyResetTimer.Stop()
            $ui['BtnCopyLog'].Content = 'Copy full log'
        })
        $script:copyResetTimer.Start()
    }
})
if ($ui.ContainsKey('BtnResetCustomDefaults')) {
    $ui['BtnResetCustomDefaults'].Add_Click({
        $result = Show-ThemedDialog -Title 'Load recommended defaults' -Message 'LibreSpot will load the Easy Install defaults into Custom Install so you can keep tweaking from a known-good baseline.' -Buttons 'YesNo' -Icon 'Question' -PrimaryText 'Load defaults' -SecondaryText 'Cancel'
        if ($result -ne 'Yes') { return }
        $preset = @{ Mode = 'Custom' }
        foreach ($key in $global:EasyDefaults.Keys) { $preset[$key] = $global:EasyDefaults[$key] }
        Apply-ConfigToUi -Config $preset -ForceCustomMode
    })
}
$ui['TitleBar'].Add_MouseLeftButtonDown({ $window.DragMove() })
$ui['CloseTitleBtn'].Add_Click({ $window.Close() })
$ui['MinimizeBtn'].Add_Click({ $window.WindowState = 'Minimized' })
$ui['ModeEasy'].Add_Checked({ Update-ModePresentation })
$ui['ModeCustom'].Add_Checked({ Update-ModePresentation })
$ui['ModeMaint'].Add_Checked({ Update-ModePresentation })
$ui['MainProgress'].Add_ValueChanged({ Update-InstallStageVisual })

$summaryToggleControls = @(
    'ChkNewTheme','ChkPodcastsOff','ChkAdSectionsOff','ChkBlockUpdate','ChkPremium','ChkLyrics',
    'ChkTopSearch','ChkRightSidebarOff','ChkRightSidebarColor','ChkCanvasHomeOff','ChkHomeSubOff','ChkOldLyrics',
    'ChkHideColIconOff','ChkDisableStartup','ChkNoShortcut','ChkStartSpoti','ChkPlus','ChkNewFullscreen','ChkFunnyProgress','ChkExpSpotify','ChkLyricsBlock',
    'ChkSendVersionOff','ChkDevTools','ChkMirror','ChkConfirmUninstall','ChkMarketplace','ChkCleanInstall','ChkLaunchAfter'
) + @($extCheckboxMap.Keys)
foreach ($controlName in $summaryToggleControls) {
    if ($ui.ContainsKey($controlName)) {
        $ui[$controlName].Add_Checked({ Update-ModePresentation })
        $ui[$controlName].Add_Unchecked({ Update-ModePresentation })
    }
}
foreach ($controlName in @('CmbLyricsTheme','CmbTheme','CmbScheme','CmbDownloadMethod','CmbSpotifyVersion')) {
    if ($ui.ContainsKey($controlName)) {
        $ui[$controlName].Add_SelectionChanged({ Update-ModePresentation })
    }
}

# Populate the Spotify version combo from the manifest and wire a hint text
# that updates as the user switches entries. We do this at runtime rather than
# in XAML so the manifest remains a single source of truth.
if ($ui.ContainsKey('CmbSpotifyVersion')) {
    $ui['CmbSpotifyVersion'].Items.Clear()
    foreach ($entry in $global:SpotifyVersionManifest) {
        $item = New-Object System.Windows.Controls.ComboBoxItem
        $item.Content = $entry.Label
        $item.Tag     = $entry.Id
        $null = $ui['CmbSpotifyVersion'].Items.Add($item)
    }
    $ui['CmbSpotifyVersion'].SelectedIndex = 0
    $ui['CmbSpotifyVersion'].Add_SelectionChanged({
        try {
            $sel = $ui['CmbSpotifyVersion'].SelectedItem
            if (-not $sel) { return }
            $id = [string]$sel.Tag
            $entry = $global:SpotifyVersionManifest | Where-Object { $_.Id -eq $id } | Select-Object -First 1
            if ($entry -and $ui.ContainsKey('SpotifyVersionHint')) {
                $ui['SpotifyVersionHint'].Text = [string]$entry.Notes
            }
        } catch {}
    })
}

$ui['CloseBtn'].Add_Click({ $window.Close() })
$ui['BtnBackToConfig'].Add_Click({
    Clear-CompletedRunspaceResources | Out-Null
    $script:installStartTime = $null
    if ($timer) { $timer.Stop() }
    $ui['PageInstall'].Visibility='Collapsed'
    $ui['PageConfig'].Visibility='Visible'
    $ui['BtnInstall'].IsEnabled=$true
    $ui['LogOutput'].Text=''
    $ui['StatusText'].Text='Checking prerequisites...'
    $ui['StepIndicator'].Text='Ready when you are'
    $ui['ElapsedTime'].Text=''
    if ($ui.ContainsKey('ProgressPercentText')) { $ui['ProgressPercentText'].Text='0%' }
    $ui['MainProgress'].Value=0
    $ui['MainProgress'].Foreground=$global:BrushGreen
    if ($script:copyResetTimer) { $script:copyResetTimer.Stop() }
    $ui['BtnCopyLog'].Content='Copy full log'
    $ui['BtnCopyLog'].Visibility='Collapsed'
    $ui['CloseBtn'].Visibility='Collapsed'
    $ui['BtnBackToConfig'].Visibility='Collapsed'
    $window.Topmost=$false
    Update-InstallStageVisual
    Update-MaintenanceStatus
    Update-ModePresentation
})
$window.Add_Closing({
    # Do NOT name the first parameter `$sender` — it shadows PowerShell's
    # automatic `$Sender` variable and PSScriptAnalyzer flags it as a latent
    # bug in every lint run.
    param($closingSource, $e)
    if ($script:activeSyncHash -and $script:activeSyncHash.IsRunning) {
        $result = Show-ThemedDialog -Title 'Setup still running' -Message 'LibreSpot is still working. Closing now can interrupt cleanup, downloads, or patching and may leave Spotify in a partial state.' -Buttons 'YesNo' -Icon 'Warning' -PrimaryText 'Exit anyway' -SecondaryText 'Keep running' -PrimaryIsDestructive
        if ($result -ne 'Yes') { $e.Cancel = $true; return }
    } elseif ($ui['ModeCustom'].IsChecked -and (Test-HasUnsavedCustomChanges)) {
        $result = Show-ThemedDialog -Title 'Custom choices not saved' -Message 'Custom selections are only saved when you start setup. Close now and these edits will be lost.' -Buttons 'YesNo' -Icon 'Question' -PrimaryText 'Discard changes' -SecondaryText 'Keep editing' -PrimaryIsDestructive
        if ($result -ne 'Yes') { $e.Cancel = $true; return }
    }
    if ($script:copyResetTimer) { $script:copyResetTimer.Stop() }
    if ($script:activeSyncHash) { $script:activeSyncHash.IsRunning = $false }
    Clear-CompletedRunspaceResources | Out-Null
})
Update-ModePresentation

# Apply -Clean CLI flag: pre-tick Easy mode + CleanInstall so users get a
# single-click "nuke and rebuild" experience without touching Custom Install.
if ($script:CliClean) {
    try {
        if ($ui.ContainsKey('ChkCleanInstall')) { $ui['ChkCleanInstall'].IsChecked = $true }
        if ($ui.ContainsKey('ModeEasy')) { $ui['ModeEasy'].IsChecked = $true }
        Update-ModePresentation
    } catch {}
}

# =============================================================================
# 8. CONFIG BUILDER
# =============================================================================
function Get-InstallConfig { param([bool]$EasyMode = $false)
    if ($EasyMode) { $c = @{ Mode='Easy' }; foreach ($k in $global:EasyDefaults.Keys) { $c[$k]=$global:EasyDefaults[$k] }; return $c }
    $lTheme = if($ui['CmbLyricsTheme'].SelectedItem){$ui['CmbLyricsTheme'].SelectedItem.Content}else{'spotify'}
    $sTheme = if($ui['CmbTheme'].SelectedItem){$ui['CmbTheme'].SelectedItem.Content}else{'(None - Marketplace Only)'}
    $sScheme = if($ui['CmbScheme'].SelectedItem){$ui['CmbScheme'].SelectedItem.Content}else{'Default'}
    $dlMethod = if($ui.ContainsKey('CmbDownloadMethod') -and $ui['CmbDownloadMethod'].SelectedItem){[string]$ui['CmbDownloadMethod'].SelectedItem.Content}else{'auto'}
    if ($dlMethod -eq 'auto') { $dlMethod = '' }
    $spotifyVerId = if ($ui.ContainsKey('CmbSpotifyVersion') -and $ui['CmbSpotifyVersion'].SelectedItem) { [string]$ui['CmbSpotifyVersion'].SelectedItem.Tag } else { 'auto' }
    $cacheVal = 0; try { $cacheVal = [int]$ui['TxtCacheLimit'].Text } catch {}
    if ($cacheVal -lt 0) { $cacheVal = 0 }
    $exts = @(); foreach ($k in $extCheckboxMap.Keys) { if ($ui[$k].IsChecked) { $exts += $extCheckboxMap[$k] } }
    $c = @{
        Mode='Custom'; CleanInstall=[bool]$ui['ChkCleanInstall'].IsChecked; LaunchAfter=[bool]$ui['ChkLaunchAfter'].IsChecked
        SpotX_NewTheme=[bool]$ui['ChkNewTheme'].IsChecked; SpotX_PodcastsOff=[bool]$ui['ChkPodcastsOff'].IsChecked
        SpotX_AdSectionsOff=[bool]$ui['ChkAdSectionsOff'].IsChecked; SpotX_BlockUpdate=[bool]$ui['ChkBlockUpdate'].IsChecked
        SpotX_Premium=[bool]$ui['ChkPremium'].IsChecked; SpotX_DisableStartup=[bool]$ui['ChkDisableStartup'].IsChecked
        SpotX_NoShortcut=[bool]$ui['ChkNoShortcut'].IsChecked
        SpotX_StartSpoti=[bool]$ui['ChkStartSpoti'].IsChecked
        SpotX_LyricsEnabled=[bool]$ui['ChkLyrics'].IsChecked; SpotX_LyricsTheme=$lTheme
        SpotX_TopSearch=[bool]$ui['ChkTopSearch'].IsChecked
        SpotX_RightSidebarOff=[bool]$ui['ChkRightSidebarOff'].IsChecked; SpotX_RightSidebarClr=[bool]$ui['ChkRightSidebarColor'].IsChecked
        SpotX_CanvasHomeOff=[bool]$ui['ChkCanvasHomeOff'].IsChecked; SpotX_HomeSubOff=[bool]$ui['ChkHomeSubOff'].IsChecked
        SpotX_OldLyrics=[bool]$ui['ChkOldLyrics'].IsChecked; SpotX_HideColIconOff=[bool]$ui['ChkHideColIconOff'].IsChecked
        SpotX_Plus=[bool]$ui['ChkPlus'].IsChecked; SpotX_NewFullscreen=[bool]$ui['ChkNewFullscreen'].IsChecked
        SpotX_FunnyProgress=[bool]$ui['ChkFunnyProgress'].IsChecked; SpotX_ExpSpotify=[bool]$ui['ChkExpSpotify'].IsChecked
        SpotX_LyricsBlock=[bool]$ui['ChkLyricsBlock'].IsChecked
        SpotX_CacheLimit=$cacheVal
        SpotX_SendVersionOff=[bool]$ui['ChkSendVersionOff'].IsChecked
        SpotX_DevTools=[bool]$ui['ChkDevTools'].IsChecked
        SpotX_Mirror=[bool]$ui['ChkMirror'].IsChecked
        SpotX_ConfirmUninstall=[bool]$ui['ChkConfirmUninstall'].IsChecked
        SpotX_DownloadMethod=$dlMethod
        SpotX_SpotifyVersionId=$spotifyVerId
        Spicetify_Theme=$sTheme; Spicetify_Scheme=$sScheme
        Spicetify_Marketplace=[bool]$ui['ChkMarketplace'].IsChecked; Spicetify_Extensions=$exts
        # Maintenance-mode control but persisted in the shared config so both
        # Easy and Custom saves carry the preference forward.
        AutoReapply_Enabled = if ($ui.ContainsKey('ChkAutoReapply')) { [bool]$ui['ChkAutoReapply'].IsChecked } else { $false }
    }
    return $c
}

Capture-CustomConfigBaseline
Update-ModePresentation

# Post-launch housekeeping. Self-update runs truly async (ThreadPool) so a
# slow GitHub API response never freezes the UI; foreign-patch detection is
# filesystem-only and stays on the dispatcher at idle priority so the warning
# dialog doesn't appear before the main window has finished painting.
try {
    Start-SelfUpdateBannerRefresh
    $null = $window.Dispatcher.BeginInvoke([System.Windows.Threading.DispatcherPriority]::ApplicationIdle, [System.Action]{
        try { Test-ForeignPatchWarningIfNeeded } catch {}
    })
} catch {}

function Build-SpotXParams { param($Config)
    $p = @()
    # Always auto-remove MS Store Spotify without prompt (prevents stdin hang)
    $p += "-confirm_uninstall_ms_spoti"
    # Let SpotX manage Spotify version compatibility (auto-overwrite unsupported versions)
    $p += "-confirm_spoti_recomended_over"
    if ($Config.SpotX_NewTheme)        { $p += "-new_theme" }
    if ($Config.SpotX_PodcastsOff)     { $p += "-podcasts_off" } else { $p += "-podcasts_on" }
    if ($Config.SpotX_AdSectionsOff)   { $p += "-adsections_off" }
    if ($Config.SpotX_BlockUpdate)     { $p += "-block_update_on" } else { $p += "-block_update_off" }
    if ($Config.SpotX_Premium)         { $p += "-premium" }
    if ($Config.SpotX_DisableStartup)  { $p += "-DisableStartup" }
    if ($Config.SpotX_NoShortcut)      { $p += "-no_shortcut" }
    if ($Config.SpotX_StartSpoti)      { $p += "-start_spoti" }
    if ($Config.SpotX_LyricsEnabled)   { $p += "-lyrics_stat $($Config.SpotX_LyricsTheme)" }
    if ($Config.SpotX_TopSearch)       { $p += "-topsearchbar" }
    if ($Config.SpotX_RightSidebarOff) { $p += "-rightsidebar_off" }
    if ($Config.SpotX_RightSidebarClr) { $p += "-rightsidebarcolor" }
    if ($Config.SpotX_CanvasHomeOff)   { $p += "-canvashome_off" }
    if ($Config.SpotX_HomeSubOff)      { $p += "-homesub_off" }
    if ($Config.SpotX_OldLyrics)       { $p += "-old_lyrics" }
    if ($Config.SpotX_HideColIconOff)  { $p += "-hide_col_icon_off" }
    if ($Config.SpotX_Plus)             { $p += "-plus" }
    if ($Config.SpotX_NewFullscreen)    { $p += "-newFullscreenMode" }
    if ($Config.SpotX_FunnyProgress)    { $p += "-funnyprogressBar" }
    if ($Config.SpotX_ExpSpotify)       { $p += "-exp_spotify" }
    if ($Config.SpotX_LyricsBlock)      { $p += "-lyrics_block" }
    if ($Config.SpotX_SendVersionOff)   { $p += "-sendversion_off" }
    if ($Config.SpotX_DevTools)         { $p += "-devtools" }
    if ($Config.SpotX_Mirror)           { $p += "-mirror" }
    if ($Config.SpotX_ConfirmUninstall) { $p += "-confirm_spoti_recomended_uninstall" }
    if (-not [string]::IsNullOrWhiteSpace([string]$Config.SpotX_DownloadMethod)) {
        $p += "-download_method $($Config.SpotX_DownloadMethod)"
    }
    $versionId = [string]$Config.SpotX_SpotifyVersionId
    if (-not [string]::IsNullOrWhiteSpace($versionId) -and $versionId -ne 'auto') {
        $entry = $global:SpotifyVersionManifest | Where-Object { $_.Id -eq $versionId } | Select-Object -First 1
        if ($entry -and -not [string]::IsNullOrWhiteSpace([string]$entry.Version)) {
            $p += "-version $($entry.Version)"
        }
    }
    if ($Config.SpotX_CacheLimit -ge 500) { $p += "-cache_limit $($Config.SpotX_CacheLimit)" }
    return ($p -join " ")
}

# -Watch CLI exit point. Placed here (not at the top of the file) because
# Invoke-AutoReapplyWatcher depends on Build-SpotXParams / Load-LibreSpotConfig /
# Normalize-LibreSpotConfig — all of which must already be defined when the call
# fires. PowerShell resolves function names lazily, but they still have to exist
# by the time the call is reached. No WPF has been instantiated at this point
# (XamlReader::Load runs much further down), so the watcher stays truly headless.
if ($script:CliWatch) {
    $code = 0
    try { $code = Invoke-AutoReapplyWatcher }
    catch { Write-WatcherLog "Fatal: $($_.Exception.Message)" -Level 'ERROR'; $code = 1 }
    exit $code
}

function Get-SpicetifyConfigEntries {
    $configPath = Join-Path $global:SPICETIFY_CONFIG_DIR 'config-xpui.ini'
    $entries = @{}
    if (-not (Test-Path -LiteralPath $configPath)) { return $entries }
    try {
        foreach ($line in Get-Content -LiteralPath $configPath -ErrorAction Stop) {
            if ($line -match '^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$') {
                $entries[$Matches[1].Trim()] = $Matches[2].Trim()
            }
        }
    } catch {
        if (Get-Command Write-Log -ErrorAction SilentlyContinue) {
            Write-Log "Could not read Spicetify config: $($_.Exception.Message)" -Level 'WARN'
        }
    }
    return $entries
}

function Get-SpicetifyConfigListValue {
    param([string]$Key)
    $entries = Get-SpicetifyConfigEntries
    if (-not $entries.ContainsKey($Key)) { return @() }
    $raw = [string]$entries[$Key]
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }
    return @(
        $raw -split '\|' |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
    )
}

function Invoke-SpicetifyCli {
    param(
        [string[]]$Arguments,
        [string]$FailureMessage = 'Spicetify command failed.'
    )
    $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
    if (-not (Test-Path -LiteralPath $spicetifyExe)) {
        throw 'Spicetify CLI is not installed.'
    }
    $output = & $spicetifyExe @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($output) { Write-Log "  $($output -join ' ')" }
    if ($exitCode -ne 0) {
        throw "$FailureMessage Exit code: $exitCode."
    }
    return $output
}

function Sync-SpicetifyListSetting {
    param(
        [string]$Key,
        [string[]]$DesiredItems,
        [string[]]$ManagedItems
    )
    $desired = @($DesiredItems | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $managed = @($ManagedItems | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $current = @(Get-SpicetifyConfigListValue -Key $Key)

    $currentLookup = @{}
    foreach ($item in $current) { $currentLookup[$item.ToLowerInvariant()] = $true }

    $desiredLookup = @{}
    foreach ($item in $desired) { $desiredLookup[$item.ToLowerInvariant()] = $true }

    $managedLookup = @{}
    foreach ($item in $managed) { $managedLookup[$item.ToLowerInvariant()] = $true }

    $changed = $false
    foreach ($item in $desired) {
        if ($currentLookup.ContainsKey($item.ToLowerInvariant())) { continue }
        Invoke-SpicetifyCli -Arguments @('config', $Key, $item, '--bypass-admin') -FailureMessage "Could not enable $Key item '$item'."
        Write-Log "Enabled $Key item: $item"
        $changed = $true
    }

    foreach ($item in $current) {
        $itemKey = $item.ToLowerInvariant()
        if (-not $managedLookup.ContainsKey($itemKey)) { continue }
        if ($desiredLookup.ContainsKey($itemKey)) { continue }
        Invoke-SpicetifyCli -Arguments @('config', $Key, "$item-", '--bypass-admin') -FailureMessage "Could not remove $Key item '$item'."
        Write-Log "Removed $Key item: $item"
        $changed = $true
    }

    if (-not $changed) {
        Write-Log "No $Key changes were needed."
    }
}

function Test-SpicetifyCliInstalled {
    $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
    return (Test-Path -LiteralPath $spicetifyExe)
}

function Restore-SpotifyIfSpicetifyPresent {
    param(
        [string]$FailureMessage,
        [string]$MissingMessage
    )

    if (-not (Test-SpicetifyCliInstalled)) {
        if ($MissingMessage) {
            Write-Log $MissingMessage -Level 'WARN'
        }
        return $false
    }

    Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage $FailureMessage
    return $true
}

function Get-NormalizedPathString {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $expanded = [Environment]::ExpandEnvironmentVariables($Path.Trim())
    try { return ([System.IO.Path]::GetFullPath($expanded)).TrimEnd('\') }
    catch { return $expanded.TrimEnd('\') }
}

function Get-PathEntries {
    param([ValidateSet('User','Process')] [string]$Scope = 'User')
    $rawPath = if ($Scope -eq 'Process') { $env:PATH } else { [Environment]::GetEnvironmentVariable('PATH', $Scope) }
    if ([string]::IsNullOrWhiteSpace($rawPath)) { return @() }
    return @($rawPath -split ';' | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Set-PathEntries {
    param(
        [ValidateSet('User','Process')] [string]$Scope = 'User',
        [string[]]$Entries
    )
    $orderedEntries = [System.Collections.Generic.List[string]]::new()
    $seen = @{}
    foreach ($entry in @($Entries)) {
        if ([string]::IsNullOrWhiteSpace($entry)) { continue }
        $normalized = Get-NormalizedPathString -Path $entry
        if ([string]::IsNullOrWhiteSpace($normalized)) { continue }
        $key = $normalized.ToLowerInvariant()
        if ($seen.ContainsKey($key)) { continue }
        $seen[$key] = $true
        $orderedEntries.Add($entry.Trim())
    }
    $pathValue = ($orderedEntries -join ';')
    if ($Scope -eq 'Process') {
        $env:PATH = $pathValue
    } else {
        [Environment]::SetEnvironmentVariable('PATH', $pathValue, $Scope)
    }
}

function Add-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User','Process')] [string]$Scope = 'User'
    )
    $normalized = Get-NormalizedPathString -Path $Entry
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $false }
    $entries = @(Get-PathEntries -Scope $Scope)
    foreach ($existing in $entries) {
        $existingNormalized = Get-NormalizedPathString -Path $existing
        if ($existingNormalized -and $existingNormalized.ToLowerInvariant() -eq $normalized.ToLowerInvariant()) {
            return $false
        }
    }
    Set-PathEntries -Scope $Scope -Entries (@($entries) + @($Entry))
    return $true
}

function Remove-PathEntry {
    param(
        [string]$Entry,
        [ValidateSet('User','Process')] [string]$Scope = 'User'
    )
    $normalized = Get-NormalizedPathString -Path $Entry
    if ([string]::IsNullOrWhiteSpace($normalized)) { return $false }
    $entries = @(Get-PathEntries -Scope $Scope)
    $remaining = @()
    $removed = $false
    foreach ($existing in $entries) {
        $existingNormalized = Get-NormalizedPathString -Path $existing
        if ($existingNormalized -and $existingNormalized.ToLowerInvariant() -eq $normalized.ToLowerInvariant()) {
            $removed = $true
            continue
        }
        $remaining += $existing
    }
    if ($removed) {
        Set-PathEntries -Scope $Scope -Entries $remaining
    }
    return $removed
}

# =============================================================================
# 9. MAINTENANCE
# =============================================================================

# Self-update check: compare $global:VERSION against the latest GitHub release.
# Result is cached for 24h under $env:APPDATA\LibreSpot\update-check.json to
# stay under the 60 req/hr anonymous GitHub API limit even if users open/close
# LibreSpot frequently. No telemetry — a single GET and nothing else.
$global:SelfUpdateCachePath = Join-Path $global:CONFIG_DIR 'update-check.json'
$global:SelfUpdateCacheMaxAgeHours = 24

function Read-SelfUpdateCache {
    # Returns the cached result hashtable if fresh, otherwise $null.
    # Split out so both the sync (cache-only) and async (network) paths share it.
    if (-not (Test-Path -LiteralPath $global:SelfUpdateCachePath)) { return $null }
    try {
        $cacheAge = (Get-Date) - (Get-Item -LiteralPath $global:SelfUpdateCachePath).LastWriteTime
        if ($cacheAge.TotalHours -ge $global:SelfUpdateCacheMaxAgeHours) { return $null }
        $cached = Get-Content -LiteralPath $global:SelfUpdateCachePath -Raw -ErrorAction Stop | ConvertFrom-Json
        return @{
            UpdateAvailable = [bool]$cached.UpdateAvailable
            LatestTag       = [string]$cached.LatestTag
            LatestUrl       = [string]$cached.LatestUrl
        }
    } catch { return $null }
}

function Save-SelfUpdateCache {
    param([hashtable]$Result)
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        $Result | ConvertTo-Json -Compress | Set-Content -LiteralPath $global:SelfUpdateCachePath -Encoding UTF8
    } catch {}
}

function Compare-LibreSpotVersions {
    # Semver-ish compare that tolerates `-preview.N` / `-rc.N` suffixes and
    # string-compare them as a tie-breaker when the numeric prefixes match.
    # Returns $true iff $Latest is strictly newer than $Current.
    param([string]$Latest, [string]$Current)
    if ([string]::IsNullOrWhiteSpace($Latest)) { return $false }
    if ([string]::IsNullOrWhiteSpace($Current)) { return $true }
    $stripLatest  = ($Latest  -replace '-preview.*','' -replace '-rc.*','')
    $stripCurrent = ($Current -replace '-preview.*','' -replace '-rc.*','')
    try {
        $l = [Version]$stripLatest
        $c = [Version]$stripCurrent
        if ($l -gt $c) { return $true }
        if ($l -lt $c) { return $false }
        # Numeric prefixes equal: the one WITHOUT a pre-release suffix is newer.
        $latestIsStable  = ($Latest  -eq $stripLatest)
        $currentIsStable = ($Current -eq $stripCurrent)
        if ($latestIsStable -and -not $currentIsStable) { return $true }
        if (-not $latestIsStable -and $currentIsStable) { return $false }
        # Both stable or both pre-release: fall back to direct string inequality.
        return ($Latest -ne $Current)
    } catch {
        return ($Latest -ne $Current)
    }
}

function Invoke-SelfUpdateHttp {
    # Pure-.NET GET + pure-regex JSON extract so the caller can run us on a
    # ThreadPool thread without contending with the main PowerShell runspace.
    # We deliberately avoid every PS cmdlet (ConvertFrom-Json, Compare-*, etc.)
    # because they serialize against the main thread via the shared runspace.
    # Returns the parsed hashtable on success or $null on any failure.
    try {
        $req = [System.Net.HttpWebRequest]::Create('https://api.github.com/repos/SysAdminDoc/LibreSpot/releases/latest')
        $req.Method = 'GET'
        $req.Timeout = 5000
        $req.ReadWriteTimeout = 5000
        $req.UserAgent = "LibreSpot/$($global:VERSION)"
        $req.Accept = 'application/vnd.github+json'
        $resp = $req.GetResponse()
        try {
            $stream = $resp.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            try { $json = $reader.ReadToEnd() } finally { $reader.Dispose() }
        } finally { $resp.Dispose() }

        # GitHub's releases endpoint returns tag_name and html_url as top-level
        # string fields. A lenient regex is enough — we don't need to interpret
        # the rest of the payload.
        $tagMatch = [regex]::Match($json, '"tag_name"\s*:\s*"([^"]+)"')
        $urlMatch = [regex]::Match($json, '"html_url"\s*:\s*"([^"]+)"')
        if (-not $tagMatch.Success) { return $null }
        $tag = $tagMatch.Groups[1].Value
        $url = if ($urlMatch.Success) { $urlMatch.Groups[1].Value } else { 'https://github.com/SysAdminDoc/LibreSpot/releases/latest' }
        $latest = $tag -replace '^v',''

        # Inline the compare instead of calling Compare-LibreSpotVersions so we
        # never cross the runspace boundary from this thread.
        $isNewer = $false
        try {
            $latestVer  = [Version](($latest          -replace '-preview.*','') -replace '-rc.*','')
            $currentVer = [Version](($global:VERSION  -replace '-preview.*','') -replace '-rc.*','')
            if     ($latestVer -gt $currentVer) { $isNewer = $true }
            elseif ($latestVer -lt $currentVer) { $isNewer = $false }
            else {
                $latestIsStable  = ($latest         -eq (($latest          -replace '-preview.*','') -replace '-rc.*',''))
                $currentIsStable = ($global:VERSION -eq (($global:VERSION  -replace '-preview.*','') -replace '-rc.*',''))
                if     ($latestIsStable -and -not $currentIsStable) { $isNewer = $true }
                elseif (-not $latestIsStable -and $currentIsStable) { $isNewer = $false }
                else { $isNewer = ($latest -ne $global:VERSION) }
            }
        } catch { $isNewer = ($latest -ne $global:VERSION) }

        return @{
            UpdateAvailable = $isNewer
            LatestTag       = $tag
            LatestUrl       = $url
        }
    } catch {
        return $null
    }
}

function Start-SelfUpdateBannerRefresh {
    # Fire-and-forget async check. If we have a fresh cached result, apply it
    # immediately on the UI thread; otherwise queue a ThreadPool work item
    # that hits GitHub and marshals the result back via Dispatcher.BeginInvoke.
    if (-not $ui.ContainsKey('UpdateBanner')) { return }

    $applyResult = {
        param($check)
        if (-not $check -or -not $check.UpdateAvailable) { return }
        if (-not $ui.ContainsKey('UpdateBanner')) { return }
        $ui['UpdateBanner'].Visibility = 'Visible'
        if ($check.LatestTag) {
            $ui['UpdateBanner'].ToolTip = "New release $($check.LatestTag) — click to open GitHub."
        }
        $script:SelfUpdateTarget = if ($check.LatestUrl) { $check.LatestUrl } else { 'https://github.com/SysAdminDoc/LibreSpot/releases/latest' }
    }

    $cached = Read-SelfUpdateCache
    if ($cached) { & $applyResult $cached; return }

    try {
        $null = [System.Threading.ThreadPool]::QueueUserWorkItem([System.Threading.WaitCallback]{
            param($state)
            # Invoke-SelfUpdateHttp uses only .NET primitives so it's safe on a
            # ThreadPool thread. All PowerShell cmdlet work (cache save + UI
            # update) is marshaled back to the dispatcher to avoid races on
            # the shared runspace.
            $check = Invoke-SelfUpdateHttp
            try {
                $window.Dispatcher.BeginInvoke([System.Windows.Threading.DispatcherPriority]::ApplicationIdle, [Action]{
                    if ($window.Dispatcher.HasShutdownStarted) { return }
                    if ($check) { Save-SelfUpdateCache -Result $check }
                    & $applyResult $check
                }) | Out-Null
            } catch {}
        })
    } catch {}
}

# Detect non-LibreSpot Spotify modifications so we can warn the user before
# patching on top. Only checks for files that BlockTheSpot / similar third-party
# modders drop — NOT chrome_elf.dll (ships with every Spotify install; we
# explicitly require it at install time) and NOT xpui.spa.bak (created by our
# own SpotX runs). Returns a display label or $null.
function Get-ExistingSpotifyPatchSignature {
    if (-not (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) { return $null }
    $spotifyDir = Split-Path -LiteralPath $global:SPOTIFY_EXE_PATH -Parent

    $signatures = @(
        @{ Path = (Join-Path $spotifyDir 'dpapi.dll');      Label = 'BlockTheSpot (dpapi.dll injected next to Spotify.exe)' }
        @{ Path = (Join-Path $spotifyDir 'config.ini');     Label = 'BlockTheSpot config.ini present in the Spotify install directory' }
        @{ Path = (Join-Path $spotifyDir 'version.dll');    Label = 'Third-party injector (version.dll hijack)' }
        @{ Path = (Join-Path $spotifyDir 'winmm.dll');      Label = 'Third-party injector (winmm.dll hijack)' }
    )
    foreach ($sig in $signatures) {
        if (Test-Path -LiteralPath $sig.Path) { return [string]$sig.Label }
    }
    return $null
}

# Shown once per session when Spotify looks like it was patched outside of
# LibreSpot. SpotX can recover, but the user deserves an explicit heads-up.
function Test-ForeignPatchWarningIfNeeded {
    if ($script:ForeignPatchWarningShown) { return }
    $signature = Get-ExistingSpotifyPatchSignature
    if (-not $signature) { return }
    $script:ForeignPatchWarningShown = $true

    $message = "Spotify at $global:SPOTIFY_EXE_PATH looks like it was already patched by another tool.`n`nDetected: $signature`n`nLibreSpot can safely overwrite this during install, but you may want to run Maintenance > Full Reset first if you see blank screens or failed playback after patching."
    try {
        Show-ThemedDialog -Title 'Third-party Spotify patch detected' -Message $message -Buttons 'OK' -Icon 'Warning' -PrimaryText 'Got it' | Out-Null
    } catch {}
}

function Update-MaintenanceStatus {
    $spicetifyConfig = Get-SpicetifyConfigEntries
    $themeInstalled = $false
    if (Test-Path $global:SPOTIFY_EXE_PATH) {
        try {
            $v = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            $ui['StatusSpotify'].Text = "Installed`nv$v"
            $ui['StatusSpotify'].Foreground = $global:BrushGreen
        }
        catch { $ui['StatusSpotify'].Text = 'Installed'; $ui['StatusSpotify'].Foreground = $global:BrushGreen }
        Set-MaintenanceCardTone -CardName 'StatusCardSpotify' -Tone 'success'
    } else {
        $ui['StatusSpotify'].Text = 'Not installed'
        $ui['StatusSpotify'].Foreground = $global:BrushRed
        Set-MaintenanceCardTone -CardName 'StatusCardSpotify' -Tone 'danger'
    }

    $spotxFound = $false
    if (Test-Path "$env:APPDATA\Spotify\Apps\xpui.spa.bak") { $spotxFound = $true }
    if (-not $spotxFound) { try { if (Get-ChildItem (Join-Path $global:TEMP_DIR "SpotX_Temp*") -EA SilentlyContinue) { $spotxFound = $true } } catch {} }
    if ($spotxFound) {
        $ui['StatusSpotX'].Text = 'Patched and ready'
        $ui['StatusSpotX'].Foreground = $global:BrushGreen
        Set-MaintenanceCardTone -CardName 'StatusCardSpotX' -Tone 'success'
    }
    elseif (Test-Path $global:SPOTIFY_EXE_PATH) {
        $ui['StatusSpotX'].Text = 'Spotify only'
        $ui['StatusSpotX'].Foreground = $global:BrushMuted
        Set-MaintenanceCardTone -CardName 'StatusCardSpotX' -Tone 'info'
    }
    else {
        $ui['StatusSpotX'].Text = 'Unavailable'
        $ui['StatusSpotX'].Foreground = $global:BrushMuted
        Set-MaintenanceCardTone -CardName 'StatusCardSpotX' -Tone 'muted'
    }

    $sExe = Join-Path $global:SPICETIFY_DIR "spicetify.exe"
    if (Test-Path $sExe) {
        $tmpOut = Join-Path $global:TEMP_DIR "spicetify_ver.txt"
        try {
            $pr = Start-Process -FilePath $sExe -ArgumentList "-v" -NoNewWindow -PassThru -RedirectStandardOutput $tmpOut -EA Stop
            if (-not $pr.WaitForExit(5000)) {
                try { $pr.Kill() } catch {}
            }
            $vo = if (Test-Path $tmpOut) { (Get-Content $tmpOut -Raw -EA SilentlyContinue).Trim() } else { $null }
            if ($vo) { $ui['StatusSpicetify'].Text = "Installed`n$vo" } else { $ui['StatusSpicetify'].Text = 'Installed' }
        } catch { $ui['StatusSpicetify'].Text = 'Installed' }
        finally { Remove-Item $tmpOut -Force -EA SilentlyContinue }
        $ui['StatusSpicetify'].Foreground = $global:BrushGreen
        Set-MaintenanceCardTone -CardName 'StatusCardSpicetify' -Tone 'success'
    } else {
        $ui['StatusSpicetify'].Text = 'Not installed'
        $ui['StatusSpicetify'].Foreground = $global:BrushMuted
        Set-MaintenanceCardTone -CardName 'StatusCardSpicetify' -Tone 'muted'
    }

    $mp = Join-Path $global:SPICETIFY_CONFIG_DIR "CustomApps\marketplace"
    if (-not (Test-Path $mp)) { $mp = Join-Path $global:SPICETIFY_DIR "CustomApps\marketplace" }
    $marketplaceInstalled = (Test-Path $mp) -or (@(Get-SpicetifyConfigListValue -Key 'custom_apps') -contains 'marketplace')
    if ($marketplaceInstalled) {
        $ui['StatusMarketplace'].Text = 'Installed'
        $ui['StatusMarketplace'].Foreground = $global:BrushGreen
        Set-MaintenanceCardTone -CardName 'StatusCardMarketplace' -Tone 'success'
    }
    else {
        $ui['StatusMarketplace'].Text = 'Not installed'
        $ui['StatusMarketplace'].Foreground = $global:BrushMuted
        Set-MaintenanceCardTone -CardName 'StatusCardMarketplace' -Tone 'muted'
    }

    $tn = if ($spicetifyConfig.ContainsKey('current_theme')) { [string]$spicetifyConfig['current_theme'] } else { '' }
    $injectCss = if ($spicetifyConfig.ContainsKey('inject_css')) { [string]$spicetifyConfig['inject_css'] } else { '0' }
    $replaceColors = if ($spicetifyConfig.ContainsKey('replace_colors')) { [string]$spicetifyConfig['replace_colors'] } else { '0' }
    $themeInstalled = (
        -not [string]::IsNullOrWhiteSpace($tn) -and
        (($injectCss -eq '1') -or ($replaceColors -eq '1'))
    )
    if ($themeInstalled) {
        $ui['StatusTheme'].Text = $tn
        $ui['StatusTheme'].Foreground = $global:BrushGreen
        Set-MaintenanceCardTone -CardName 'StatusCardTheme' -Tone 'success'
    } else {
        $ui['StatusTheme'].Text = 'Marketplace or stock'
        $ui['StatusTheme'].Foreground = $global:BrushMuted
        Set-MaintenanceCardTone -CardName 'StatusCardTheme' -Tone 'muted'
    }

    $si = Test-Path $sExe; $sp = Test-Path $global:SPOTIFY_EXE_PATH
    $backupCount = if (Test-Path $global:BACKUP_ROOT) { (Get-ChildItem $global:BACKUP_ROOT -Directory -EA SilentlyContinue).Count } else { 0 }
    $bk = ($backupCount -gt 0)
    $script:MaintenanceBackupCount = $backupCount
    $script:MaintenanceComponentCount = @($sp, $spotxFound, $si, $marketplaceInstalled, $themeInstalled) | Where-Object { $_ } | Measure-Object | Select-Object -ExpandProperty Count

    if ($ui.ContainsKey('MaintenanceMetricStackValue')) {
        $ui['MaintenanceMetricStackValue'].Text = "$($script:MaintenanceComponentCount) of 5 ready"
    }
    if ($ui.ContainsKey('MaintenanceMetricStackDetail')) {
        $ui['MaintenanceMetricStackDetail'].Text = if ($script:MaintenanceComponentCount -eq 5) {
            'Spotify, SpotX, Spicetify, Marketplace, and an active theme are all present.'
        } elseif ($script:MaintenanceComponentCount -eq 0) {
            'Nothing from the customization stack is installed yet.'
        } else {
            'Spotify, SpotX, Spicetify, Marketplace, and theme detection are partially present.'
        }
    }
    if ($ui.ContainsKey('MaintenanceMetricBackupValue')) {
        $ui['MaintenanceMetricBackupValue'].Text = if ($backupCount -eq 0) { 'None yet' } elseif ($backupCount -eq 1) { '1 backup ready' } else { "$backupCount backups ready" }
    }
    if ($ui.ContainsKey('MaintenanceMetricBackupDetail')) {
        $ui['MaintenanceMetricBackupDetail'].Text = if ($backupCount -eq 0) {
            'Create a backup before major repair or removal work so you have a quick rollback point.'
        } else {
            'Stored in %USERPROFILE%\\LibreSpot_Backups for quick restore and reapply flows.'
        }
    }
    if ($ui.ContainsKey('MaintenanceMetricNextStepValue')) {
        $ui['MaintenanceMetricNextStepValue'].Text = if (-not $sp -and -not $si) {
            'Run Easy Install'
        } elseif (-not $bk -and $si) {
            'Create a backup'
        } elseif ($sp -and ($spotxFound -or $si) -and $script:MaintenanceComponentCount -lt 5) {
            'Reapply setup'
        } elseif ($si) {
            'Maintenance is ready'
        } else {
            'Review the stack'
        }
    }
    if ($ui.ContainsKey('MaintenanceMetricNextStepDetail')) {
        $ui['MaintenanceMetricNextStepDetail'].Text = if (-not $sp -and -not $si) {
            'Start with the recommended setup to give LibreSpot a clean baseline to manage.'
        } elseif (-not $bk -and $si) {
            'Save the current Spicetify configuration before you make deeper changes.'
        } elseif ($sp -and ($spotxFound -or $si) -and $script:MaintenanceComponentCount -lt 5) {
            'Reapply can rebuild missing pieces without forcing you through a full reset.'
        } else {
            'Pinned versions and backups are in place, so lighter maintenance actions should stay predictable.'
        }
    }

    $ui['BtnCheckUpdates'].IsEnabled=$true
    $pv = $global:PinnedReleases
    $ui['StatusSpotX'].ToolTip = "Pinned: SpotX v$($pv.SpotX.Version) | CLI v$($pv.SpicetifyCLI.Version) | Marketplace v$($pv.Marketplace.Version)"
    $hasConfigSnapshot = Test-Path -LiteralPath (Join-Path $global:SPICETIFY_CONFIG_DIR 'config-xpui.ini')
    $ui['BtnBackupConfig'].IsEnabled=($si -and $hasConfigSnapshot); $ui['BtnRestoreConfig'].IsEnabled=($bk -and $si); $ui['BtnReapply'].IsEnabled=$sp
    $ui['BtnSpicetifyRestore'].IsEnabled=$si; $ui['BtnUninstallSpicetify'].IsEnabled=$si; $ui['BtnFullReset'].IsEnabled=($sp -or $si)
    $ui['BtnBackupConfig'].ToolTip = if ($ui['BtnBackupConfig'].IsEnabled) { 'Create a timestamped backup of the active Spicetify configuration.' } elseif (-not $si) { 'Install Spicetify before backing up its configuration.' } else { 'Run a setup first so LibreSpot has a clean Spicetify config to back up.' }
    $ui['BtnRestoreConfig'].ToolTip = if ($ui['BtnRestoreConfig'].IsEnabled) { 'Restore the newest saved Spicetify backup and apply it immediately.' } elseif (-not $si) { 'Install Spicetify before restoring a backup.' } else { 'Create at least one backup before restoring.' }
    $ui['BtnCheckUpdates'].ToolTip = 'Compare LibreSpot''s pinned versions against the latest upstream releases.'
    $ui['BtnReapply'].ToolTip = if ($ui['BtnReapply'].IsEnabled) { 'Run SpotX again and then reapply Spicetify with the saved LibreSpot configuration.' } else { 'Spotify needs to be installed before LibreSpot can reapply anything.' }
    $ui['BtnSpicetifyRestore'].ToolTip = if ($ui['BtnSpicetifyRestore'].IsEnabled) { 'Remove active Spicetify customizations and restore vanilla Spotify while leaving SpotX in place.' } else { 'Install Spicetify before using this restore action.' }
    $ui['BtnUninstallSpicetify'].ToolTip = if ($ui['BtnUninstallSpicetify'].IsEnabled) { 'Remove the Spicetify CLI, configuration, and PATH entry after restoring vanilla Spotify.' } else { 'Install Spicetify before uninstalling it.' }
    $ui['BtnFullReset'].ToolTip = if ($ui['BtnFullReset'].IsEnabled) { 'Remove the full Spotify customization stack and clean leftover files.' } else { 'Nothing is installed yet, so there is nothing to reset.' }

    if ($ui.ContainsKey('MaintenanceOverviewTitle') -and $ui.ContainsKey('MaintenanceOverviewText')) {
        if (-not $sp -and -not $si) {
            $ui['MaintenanceOverviewTitle'].Text = 'Nothing looks installed yet'
            $ui['MaintenanceOverviewText'].Text = 'LibreSpot did not detect Spotify or Spicetify. You can still review versions here, but backup and restore actions will stay unavailable until a setup has been applied.'
        } elseif ($sp -and $spotxFound -and $si -and $marketplaceInstalled -and $themeInstalled) {
            $ui['MaintenanceOverviewTitle'].Text = 'Current setup looks complete'
            $ui['MaintenanceOverviewText'].Text = 'Spotify, SpotX, Spicetify, Marketplace, and a theme are all present. Create a backup before making deeper changes so you have a quick way back.'
        } elseif ($sp -and ($spotxFound -or $si)) {
            $ui['MaintenanceOverviewTitle'].Text = 'Partial setup detected'
            $ui['MaintenanceOverviewText'].Text = 'LibreSpot found some of the Spotify customization stack, but not all of it. Reapply or the lighter restore actions should help you get back to a known-good state.'
        } else {
            $ui['MaintenanceOverviewTitle'].Text = 'Spotify is present, but customization is limited'
            $ui['MaintenanceOverviewText'].Text = 'LibreSpot can repair missing pieces from here or remove the rest of the modification stack if you want a clean baseline again.'
        }
    }
    if ($ui.ContainsKey('ModeMaint') -and [bool]$ui['ModeMaint'].IsChecked) {
        $componentLabel = if ($script:MaintenanceComponentCount -eq 1) { '1 of 5 core components detected' } else { "$($script:MaintenanceComponentCount) of 5 core components detected" }
        $backupLabel = if ($script:MaintenanceBackupCount -eq 0) { 'no backups saved yet' } elseif ($script:MaintenanceBackupCount -eq 1) { '1 backup ready' } else { "$($script:MaintenanceBackupCount) backups ready" }
        $ui['SelectionSummary'].Text = "Maintenance snapshot: $componentLabel, $backupLabel, and destructive actions stay behind confirmation."

        $selectionTone = if (-not $sp -and -not $si) {
            'muted'
        } elseif ($backupCount -gt 0 -and $script:MaintenanceComponentCount -ge 3) {
            'success'
        } elseif ($script:MaintenanceComponentCount -ge 2) {
            'info'
        } else {
            'warning'
        }
        $selectionBadge = if (-not $sp -and -not $si) {
            'Waiting for first setup'
        } elseif ($backupCount -gt 0) {
            'Recovery ready'
        } elseif ($script:MaintenanceComponentCount -ge 3) {
            'Setup detected'
        } else {
            'Needs attention'
        }
        $selectionDetail = if (-not $sp -and -not $si) {
            'Backup and restore actions unlock after LibreSpot applies a setup for the first time.'
        } elseif ($backupCount -gt 0) {
            "Detected $($script:MaintenanceComponentCount) of 5 core components and $backupLabel."
        } elseif ($script:MaintenanceComponentCount -ge 3) {
            "Detected $($script:MaintenanceComponentCount) of 5 core components. Create a backup before making deeper changes."
        } else {
            "Detected $($script:MaintenanceComponentCount) of 5 core components. Reapply or the lighter restore actions can help rebuild a stable state."
        }
        Set-SelectionSnapshotState -Tone $selectionTone -BadgeText $selectionBadge -DetailText $selectionDetail
    }
}

function Copy-DirectorySnapshot {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        throw "Directory not found: $SourcePath"
    }
    $destinationParent = Split-Path -Path $DestinationPath -Parent
    if ($destinationParent -and -not (Test-Path -LiteralPath $destinationParent)) {
        New-Item -Path $destinationParent -ItemType Directory -Force | Out-Null
    }
    if (Test-Path -LiteralPath $DestinationPath) {
        $null = Remove-PathSafely -Path $DestinationPath -Label $DestinationPath
    }
    New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
    Get-ChildItem -LiteralPath $SourcePath -Force -ErrorAction Stop | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $DestinationPath -Recurse -Force
    }
}

function Restore-SpicetifyBackupSnapshot {
    param([string]$SourcePath)

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        throw "The selected backup folder is missing."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $SourcePath 'config-xpui.ini') -PathType Leaf)) {
        throw 'The selected backup is missing config-xpui.ini, so LibreSpot cannot safely restore it.'
    }

    $tempRoot = Join-Path $global:TEMP_DIR ("LibreSpot-Restore-" + [Guid]::NewGuid().ToString('N'))
    $stagedSource = Join-Path $tempRoot 'incoming'
    $rollbackPath = Join-Path $tempRoot 'rollback'
    $rollbackAvailable = $false
    $rollbackRestored = $false

    try {
        New-Item -Path $tempRoot -ItemType Directory -Force | Out-Null
        Copy-DirectorySnapshot -SourcePath $SourcePath -DestinationPath $stagedSource

        if (Test-Path -LiteralPath $global:SPICETIFY_CONFIG_DIR -PathType Container) {
            Copy-DirectorySnapshot -SourcePath $global:SPICETIFY_CONFIG_DIR -DestinationPath $rollbackPath
            $rollbackAvailable = $true
        }

        if (Test-Path -LiteralPath $global:SPICETIFY_CONFIG_DIR) {
            $null = Remove-PathSafely -Path $global:SPICETIFY_CONFIG_DIR -Label 'Current Spicetify config'
        }
        Copy-DirectorySnapshot -SourcePath $stagedSource -DestinationPath $global:SPICETIFY_CONFIG_DIR

        $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
        if (Test-Path -LiteralPath $spicetifyExe) {
            Invoke-SpicetifyCli -Arguments @('backup','apply','--bypass-admin') -FailureMessage 'Could not apply the restored Spicetify backup.'
        }
    } catch {
        $originalError = $_.Exception.Message
        if ($rollbackAvailable) {
            try {
                if (Test-Path -LiteralPath $global:SPICETIFY_CONFIG_DIR) {
                    $null = Remove-PathSafely -Path $global:SPICETIFY_CONFIG_DIR -Label 'Failed restore state'
                }
                Copy-DirectorySnapshot -SourcePath $rollbackPath -DestinationPath $global:SPICETIFY_CONFIG_DIR
                $spicetifyExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
                if (Test-Path -LiteralPath $spicetifyExe) {
                    Invoke-SpicetifyCli -Arguments @('backup','apply','--bypass-admin') -FailureMessage 'Could not reapply the rollback snapshot after the restore failed.'
                }
                $rollbackRestored = $true
            } catch {
                throw "LibreSpot could not restore the selected backup, and the automatic rollback also failed. Original error: $originalError Rollback error: $($_.Exception.Message)"
            }
        }
        if ($rollbackRestored) {
            throw "LibreSpot could not restore the selected backup, but it put your previous Spicetify config back. Original error: $originalError"
        }
        throw "LibreSpot could not restore the selected backup. $originalError"
    } finally {
        if (Test-Path -LiteralPath $tempRoot) {
            $null = Remove-PathSafely -Path $tempRoot -Label 'Temporary restore workspace'
        }
    }
}

$ui['BtnBackupConfig'].Add_Click({ try {
    if (-not (Test-Path -LiteralPath $global:SPICETIFY_CONFIG_DIR -PathType Container)) {
        Show-ThemedDialog -Message "LibreSpot could not find the active Spicetify configuration folder yet. Apply a setup first, then return here to create a backup." -Title "Nothing To Back Up" -Icon "Error" -PrimaryText "Close"
        return
    }
    if (-not (Test-Path -LiteralPath (Join-Path $global:SPICETIFY_CONFIG_DIR 'config-xpui.ini') -PathType Leaf)) {
        Show-ThemedDialog -Message "LibreSpot found the Spicetify folder, but the main config file is missing. Reapply your setup first so a clean backup can be created." -Title "Backup Not Ready" -Icon "Error" -PrimaryText "Close"
        return
    }
    $stamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"; $dest = Join-Path $global:BACKUP_ROOT $stamp
    New-Item -Path $dest -ItemType Directory -Force | Out-Null
    Copy-DirectorySnapshot -SourcePath $global:SPICETIFY_CONFIG_DIR -DestinationPath (Join-Path $dest 'spicetify')
    $all = Get-ChildItem $global:BACKUP_ROOT -Directory | Sort-Object Name -Descending
    if ($all.Count -gt 5) {
        $all | Select-Object -Skip 5 | ForEach-Object {
            $null = Remove-PathSafely -Path $_.FullName -Label "Old backup $($_.Name)"
        }
    }
    Show-ThemedDialog -Message "LibreSpot saved a new Spicetify backup as $stamp in %USERPROFILE%\\LibreSpot_Backups." -Title "Backup Saved" -Icon "Information" -PrimaryText "Done"; Update-MaintenanceStatus
} catch { Show-ThemedDialog -Message "LibreSpot could not create the backup.`n`n$($_.Exception.Message)" -Title "Backup Failed" -Icon "Error" -PrimaryText "Close" } })

$ui['BtnRestoreConfig'].Add_Click({ try {
    $sExe = Join-Path $global:SPICETIFY_DIR 'spicetify.exe'
    if (-not (Test-Path -LiteralPath $sExe -PathType Leaf)) {
        Show-ThemedDialog -Message "LibreSpot needs the Spicetify CLI installed before it can restore and reapply a backup. Reinstall Spicetify first, then try the restore again." -Title "Spicetify Required" -Icon "Error" -PrimaryText "Close"
        return
    }
    $all = Get-ChildItem $global:BACKUP_ROOT -Directory | Sort-Object Name -Descending
    if ($all.Count -eq 0) { Show-ThemedDialog -Message "LibreSpot could not find any saved backups yet. Create one first, then return here when you need it." -Title "No Backups Available" -Icon "Error" -PrimaryText "Close"; return }
    $list = ($all | ForEach-Object { $_.Name }) -join "`n"
    $r = Show-ThemedDialog -Message "LibreSpot found these backups:`n`n$list`n`nRestore the newest backup ($($all[0].Name)) and apply it now?" -Title "Restore Backup" -Buttons "YesNo" -Icon "Question" -PrimaryText "Restore newest" -SecondaryText "Cancel"
    if ($r -eq 'Yes') {
        $src = Join-Path $all[0].FullName "spicetify"
        if (-not (Test-Path -LiteralPath $src -PathType Container)) { Show-ThemedDialog -Message "LibreSpot found the backup folder, but the Spicetify data inside it is missing." -Title "Backup Incomplete" -Icon "Error" -PrimaryText "Close"; return }
        Restore-SpicetifyBackupSnapshot -SourcePath $src
        Show-ThemedDialog -Message "The newest backup was restored and reapplied successfully." -Title "Backup Restored" -Icon "Information" -PrimaryText "Done"; Update-MaintenanceStatus
    }
} catch { Show-ThemedDialog -Message "LibreSpot could not restore the backup.`n`n$($_.Exception.Message)" -Title "Restore Failed" -Icon "Error" -PrimaryText "Close" } })

$ui['BtnCheckUpdates'].Add_Click({
    if (-not (Test-NetworkReady)) { Show-ThemedDialog -Message "LibreSpot could not reach GitHub to compare pinned versions. Check the connection, then try again." -Title "No Internet Connection" -Icon "Error" -PrimaryText "Close"; return }
    try {
        Switch-ToInstallPage -Title 'Checking pinned versions' -Context 'LibreSpot is comparing the pinned LibreSpot, SpotX, Spicetify, Marketplace, and theme versions against upstream releases.' -PrepareLabel 'Prepare' -RunLabel 'Compare' -VerifyLabel 'Review' -CompleteLabel 'Complete'
        Start-MaintenanceJob -Action 'CheckUpdates'
    } catch {
        Reset-UiAfterLaunchFailure -Title 'Could not start maintenance' -Message "LibreSpot couldn't start the update check.`n`n$($_.Exception.Message)"
    }
})
$ui['BtnReapply'].Add_Click({
    if (-not (Test-NetworkReady)) { Show-ThemedDialog -Message "LibreSpot needs an internet connection to download the pinned SpotX script before it can reapply your setup." -Title "No Internet Connection" -Icon "Error" -PrimaryText "Close"; return }
    $r = Show-ThemedDialog -Message "LibreSpot will run SpotX again and then reapply Spicetify. Your saved LibreSpot settings will be used when available." -Title "Reapply Setup" -Buttons "YesNo" -Icon "Question" -PrimaryText "Reapply now" -SecondaryText "Cancel"
    if ($r -eq 'Yes') {
        try {
            Switch-ToInstallPage -Title 'Reapplying your setup' -Context 'LibreSpot is refreshing SpotX first, then reapplying Spicetify so you can recover quickly after a Spotify update.' -PrepareLabel 'Prepare' -RunLabel 'Refresh' -VerifyLabel 'Apply' -CompleteLabel 'Complete'
            Start-MaintenanceJob -Action 'Reapply'
        } catch {
            Reset-UiAfterLaunchFailure -Title 'Could not start maintenance' -Message "LibreSpot couldn't start the reapply flow.`n`n$($_.Exception.Message)"
        }
    }
})
function Update-AutoReapplyStatusLabel {
    if (-not $ui.ContainsKey('AutoReapplyStatusText')) { return }
    try {
        if (Test-AutoReapplyTaskRegistered) {
            $ui['AutoReapplyStatusText'].Text = 'Scheduled task: active (runs at logon and every 30 minutes)'
            $ui['AutoReapplyStatusText'].Foreground = $global:BrushGreen
        } else {
            $ui['AutoReapplyStatusText'].Text = 'Scheduled task: not installed'
            $ui['AutoReapplyStatusText'].Foreground = $global:BrushMuted
        }
    } catch {}
}

# Wire the Maintenance auto-reapply toggle. Checked -> register the scheduled
# task and persist the preference. Unchecked -> unregister and persist.
# We suppress the checked/unchecked handler during programmatic assignment
# (applying the saved config at launch) to avoid a spurious register call.
$script:SuppressAutoReapplyHandler = $false
if ($ui.ContainsKey('ChkAutoReapply')) {
    $onToggle = {
        if ($script:SuppressAutoReapplyHandler) { return }
        $wantOn = [bool]$ui['ChkAutoReapply'].IsChecked
        try {
            if ($wantOn) {
                $ok = Register-AutoReapplyTask
                if (-not $ok) {
                    $script:SuppressAutoReapplyHandler = $true
                    try { $ui['ChkAutoReapply'].IsChecked = $false } finally { $script:SuppressAutoReapplyHandler = $false }
                    Show-ThemedDialog -Title 'Could not install the watcher' -Message "LibreSpot couldn't create the scheduled task that watches Spotify for updates. See the watcher log for details.`n`n$($global:WATCHER_LOG_PATH)" -Icon 'Warning' -PrimaryText 'Close' | Out-Null
                }
            } else {
                $null = Unregister-AutoReapplyTask
            }
        } catch {
            Show-ThemedDialog -Title 'Could not update the watcher' -Message "LibreSpot hit an error toggling the auto-reapply task.`n`n$($_.Exception.Message)" -Icon 'Error' -PrimaryText 'Close' | Out-Null
        } finally {
            Update-AutoReapplyStatusLabel
            # Persist to disk so the preference survives a restart and so the WPF
            # shell sees the same value. Saving a fresh config snapshot here runs
            # in Custom mode if the user was editing — that matches what Save-
            # LibreSpotConfig does elsewhere.
            try {
                $current = Get-InstallConfig -EasyMode ([bool]$ui['ModeEasy'].IsChecked)
                $null = Save-LibreSpotConfig -Config (Normalize-LibreSpotConfig -Config $current)
            } catch {}
        }
    }
    $ui['ChkAutoReapply'].Add_Checked($onToggle)
    $ui['ChkAutoReapply'].Add_Unchecked($onToggle)

    # Initial sync: reflect the actual on-disk task state in the checkbox, even
    # if config.json has gone stale. The task wins — it's what actually fires.
    try {
        $script:SuppressAutoReapplyHandler = $true
        $ui['ChkAutoReapply'].IsChecked = (Test-AutoReapplyTaskRegistered)
    } finally { $script:SuppressAutoReapplyHandler = $false }
    Update-AutoReapplyStatusLabel
}

$ui['BtnSpicetifyRestore'].Add_Click({
    $r = Show-ThemedDialog -Message "LibreSpot will remove Spicetify themes and extensions, then restore vanilla Spotify while keeping SpotX in place." -Title "Restore Vanilla Spotify" -Buttons "YesNo" -Icon "Question" -PrimaryText "Restore Spotify" -SecondaryText "Cancel"
    if ($r -eq 'Yes') {
        try {
            Switch-ToInstallPage -Title 'Restoring vanilla Spotify' -Context 'LibreSpot is removing Spicetify customizations and returning Spotify to its vanilla interface while leaving SpotX untouched.' -PrepareLabel 'Prepare' -RunLabel 'Restore' -VerifyLabel 'Verify' -CompleteLabel 'Complete'
            Start-MaintenanceJob -Action 'RestoreVanilla'
        } catch {
            Reset-UiAfterLaunchFailure -Title 'Could not start maintenance' -Message "LibreSpot couldn't start the restore flow.`n`n$($_.Exception.Message)"
        }
    }
})
$ui['BtnUninstallSpicetify'].Add_Click({
    $r = Show-ThemedDialog -Message "LibreSpot will restore vanilla Spotify first, then remove the Spicetify CLI, configuration folder, and PATH entry." -Title "Uninstall Spicetify" -Buttons "YesNo" -Icon "Warning" -PrimaryText "Uninstall" -SecondaryText "Cancel" -PrimaryIsDestructive
    if ($r -eq 'Yes') {
        try {
            Switch-ToInstallPage -Title 'Removing Spicetify' -Context 'LibreSpot is restoring Spotify first, then cleaning out the Spicetify CLI, configuration, and PATH changes.' -PrepareLabel 'Prepare' -RunLabel 'Restore' -VerifyLabel 'Remove' -CompleteLabel 'Complete'
            Start-MaintenanceJob -Action 'UninstallSpicetify'
        } catch {
            Reset-UiAfterLaunchFailure -Title 'Could not start maintenance' -Message "LibreSpot couldn't start the uninstall flow.`n`n$($_.Exception.Message)"
        }
    }
})
$ui['BtnFullReset'].Add_Click({
    $r = Show-ThemedDialog -Message "LibreSpot will restore vanilla Spotify, remove SpotX and Spicetify, uninstall Spotify, and clean leftover files. This is the deepest reset available." -Title "Full Reset" -Buttons "YesNo" -Icon "Warning" -PrimaryText "Reset everything" -SecondaryText "Cancel" -PrimaryIsDestructive
    if ($r -eq 'Yes') {
        try {
            Switch-ToInstallPage -Title 'Preparing full reset' -Context 'LibreSpot is rolling the setup all the way back: restoring vanilla Spotify, removing patches, uninstalling Spotify, and cleaning leftover files.' -PrepareLabel 'Prepare' -RunLabel 'Restore' -VerifyLabel 'Clean' -CompleteLabel 'Complete'
            Start-MaintenanceJob -Action 'FullReset'
        } catch {
            Reset-UiAfterLaunchFailure -Title 'Could not start maintenance' -Message "LibreSpot couldn't start the full reset flow.`n`n$($_.Exception.Message)"
        }
    }
})

# =============================================================================
# 10. PAGE SWITCH + INSTALL TRIGGER
# =============================================================================
function Show-ThemedDialog {
    param(
        [string]$Message,
        [string]$Title = "LibreSpot",
        [string]$Buttons = "OK",
        [string]$Icon = "Information",
        [string]$PrimaryText,
        [string]$SecondaryText,
        [switch]$PrimaryIsDestructive
    )
    if (-not $PrimaryText) { $PrimaryText = if ($Buttons -eq 'YesNo') { 'Continue' } else { 'OK' } }
    if (-not $SecondaryText) { $SecondaryText = 'Cancel' }
    $dlgXaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" SizeToContent="WidthAndHeight" MinWidth="420" MaxWidth="560"
        WindowStartupLocation="CenterOwner" ShowInTaskbar="False" Topmost="True" SnapsToDevicePixels="True">
    <Border CornerRadius="16" Background="#FF06090f" BorderBrush="#FF182331" BorderThickness="1" Padding="0" Margin="14">
        <Border.Effect><DropShadowEffect BlurRadius="26" ShadowDepth="8" Opacity="0.45" Direction="270" Color="#000000"/></Border.Effect>
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <Border Grid.Row="0" Background="#FF0b1118" CornerRadius="16,16,0,0" Padding="20,16">
                <TextBlock Name="DlgTitle" FontSize="14.5" FontWeight="SemiBold" Foreground="#FFF8FAFC" FontFamily="Segoe UI"/>
            </Border>
            <Grid Grid.Row="1" Margin="24,22,24,18">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="18"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Border Name="IconHost" Width="44" Height="44" CornerRadius="22" Background="#190c2018" BorderBrush="#553dd06f" BorderThickness="1" VerticalAlignment="Top">
                    <Canvas Name="IconCanvas" Width="24" Height="24" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <TextBlock Grid.Column="2" Name="DlgMessage" FontSize="13" LineHeight="19" Foreground="#FFD6DEE8" FontFamily="Segoe UI" TextWrapping="Wrap" MaxWidth="430" VerticalAlignment="Center"/>
            </Grid>
            <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="24,0,24,24">
                <Button Name="BtnNo" Content="Cancel" Width="108" Height="38" FontSize="12.5" FontWeight="SemiBold" Cursor="Hand" Margin="0,0,10,0" Visibility="Collapsed" Background="#FF0b1118" BorderBrush="#FF334155" BorderThickness="1" Foreground="#FFE2E8F0">
                    <Button.Template><ControlTemplate TargetType="Button">
                        <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="10" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="14,0"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Opacity" Value="0.92"/><Setter TargetName="bd" Property="BorderBrush" Value="#FF64748B"/></Trigger>
                            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="bd" Property="BorderBrush" Value="#FFBAE6FD"/></Trigger>
                            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="bd" Property="Opacity" Value="0.5"/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate></Button.Template>
                </Button>
                <Button Name="BtnYes" Content="Continue" Width="132" Height="38" FontSize="12.5" FontWeight="SemiBold" Cursor="Hand" Margin="0,0,0,0" Visibility="Collapsed" Background="#FF22c55e" BorderBrush="#FF3dd06f" BorderThickness="1" Foreground="#FF04130a">
                    <Button.Template><ControlTemplate TargetType="Button">
                        <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="10" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="16,0"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Opacity" Value="0.92"/></Trigger>
                            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="bd" Property="BorderBrush" Value="#FFDCFCE7"/></Trigger>
                            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="bd" Property="Opacity" Value="0.5"/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate></Button.Template>
                </Button>
                <Button Name="BtnOK" Content="OK" Width="132" Height="38" FontSize="12.5" FontWeight="SemiBold" Cursor="Hand" Visibility="Collapsed" Background="#FF22c55e" BorderBrush="#FF3dd06f" BorderThickness="1" Foreground="#FF04130a">
                    <Button.Template><ControlTemplate TargetType="Button">
                        <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="10" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="16,0"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Opacity" Value="0.92"/></Trigger>
                            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="bd" Property="BorderBrush" Value="#FFDCFCE7"/></Trigger>
                            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="bd" Property="Opacity" Value="0.5"/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate></Button.Template>
                </Button>
            </StackPanel>
        </Grid>
    </Border>
</Window>
"@
    $dlgReader = New-Object System.Xml.XmlNodeReader ([xml]$dlgXaml)
    $dlg = [Windows.Markup.XamlReader]::Load($dlgReader)
    try {
        if ($script:BrandIconFrame) { $dlg.Icon = $script:BrandIconFrame }
    } catch {}
    $dlg.FindName("DlgTitle").Text = $Title
    $dlg.FindName("DlgMessage").Text = $Message
    $script:dlgResult = if ($Buttons -eq 'YesNo') { 'No' } else { 'OK' }
    $iconHost = $dlg.FindName("IconHost")
    $canvas = $dlg.FindName("IconCanvas")
    $iconColor = "#FF4ade80"
    $iconHost.Background = $script:BrushConverter.ConvertFromString('#190c2018')
    $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#553dd06f')
    switch ($Icon) {
        "Error" {
            $iconColor = "#FFf87171"
            $iconHost.Background = $script:BrushConverter.ConvertFromString('#19f87171')
            $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#55f87171')
        }
        "Warning" {
            $iconColor = "#FFfbbf24"
            $iconHost.Background = $script:BrushConverter.ConvertFromString('#19f59e0b')
            $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#55f59e0b')
        }
        "Question" {
            $iconColor = "#FF4ade80"
            $iconHost.Background = $script:BrushConverter.ConvertFromString('#190c2018')
            $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#553dd06f')
        }
        "Information" {
            $iconColor = "#FF7dd3fc"
            $iconHost.Background = $script:BrushConverter.ConvertFromString('#190f1d2a')
            $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#5538bdf8')
        }
    }
    $ellipse = New-Object System.Windows.Shapes.Ellipse
    $ellipse.Width = 24; $ellipse.Height = 24; $ellipse.Fill = $script:BrushConverter.ConvertFromString($iconColor); $ellipse.Opacity = 0.16
    $canvas.Children.Add($ellipse) | Out-Null
    $path = New-Object System.Windows.Shapes.Path
    $path.Stroke = $script:BrushConverter.ConvertFromString($iconColor); $path.StrokeThickness = 2
    switch ($Icon) {
        "Error"       { $path.Data = [System.Windows.Media.Geometry]::Parse("M 7.5,7.5 L 16.5,16.5 M 16.5,7.5 L 7.5,16.5"); [System.Windows.Controls.Canvas]::SetLeft($path,0); [System.Windows.Controls.Canvas]::SetTop($path,0) }
        "Warning"     { $path.Data = [System.Windows.Media.Geometry]::Parse("M 12,5.5 L 12,12.5 M 12,16.5 L 12,17"); $path.StrokeThickness = 2.5; $path.StrokeStartLineCap = "Round"; $path.StrokeEndLineCap = "Round"; [System.Windows.Controls.Canvas]::SetLeft($path,0); [System.Windows.Controls.Canvas]::SetTop($path,0) }
        "Question"    { $path.Data = [System.Windows.Media.Geometry]::Parse("M 9,7.5 C 9,4.8 15,4.8 15,7.5 C 15,9.8 12,10 12,13 M 12,16.5 L 12,17"); $path.StrokeThickness = 2; $path.StrokeStartLineCap = "Round"; $path.StrokeEndLineCap = "Round"; [System.Windows.Controls.Canvas]::SetLeft($path,0); [System.Windows.Controls.Canvas]::SetTop($path,0) }
        "Information" { $path.Data = [System.Windows.Media.Geometry]::Parse("M 12,6.5 L 12,7 M 12,10 L 12,17"); $path.StrokeThickness = 2.5; $path.StrokeStartLineCap = "Round"; $path.StrokeEndLineCap = "Round"; [System.Windows.Controls.Canvas]::SetLeft($path,0); [System.Windows.Controls.Canvas]::SetTop($path,0) }
    }
    $canvas.Children.Add($path) | Out-Null

    $btnOK = $dlg.FindName("BtnOK"); $btnYes = $dlg.FindName("BtnYes"); $btnNo = $dlg.FindName("BtnNo")
    $primaryBackground = if ($PrimaryIsDestructive) { '#FF7f1d1d' } else { '#FF22c55e' }
    $primaryBorder = if ($PrimaryIsDestructive) { '#FFb91c1c' } else { '#FF3dd06f' }
    $primaryForeground = if ($PrimaryIsDestructive) { '#FFFFF1F2' } else { '#FF04130a' }
    foreach ($btn in @($btnYes, $btnOK)) {
        $btn.Background = $script:BrushConverter.ConvertFromString($primaryBackground)
        $btn.BorderBrush = $script:BrushConverter.ConvertFromString($primaryBorder)
        $btn.Foreground = $script:BrushConverter.ConvertFromString($primaryForeground)
    }
    $btnNo.Content = $SecondaryText
    $btnYes.Content = $PrimaryText
    $btnOK.Content = $PrimaryText
    if ($Buttons -eq "YesNo") {
        $btnYes.Visibility = "Visible"
        $btnNo.Visibility = "Visible"
        $btnYes.IsDefault = $true
        $btnNo.IsCancel = $true
    } else {
        $btnOK.Visibility = "Visible"
        $btnOK.IsDefault = $true
        $btnOK.IsCancel = $true
    }
    $btnOK.Add_Click({ $script:dlgResult = "OK"; $dlg.Close() })
    $btnYes.Add_Click({ $script:dlgResult = "Yes"; $dlg.Close() })
    $btnNo.Add_Click({ $script:dlgResult = "No"; $dlg.Close() })
    try { $dlg.Owner = $window } catch {}
    $dlg.Add_MouseLeftButtonDown({ $dlg.DragMove() })
    if ($Buttons -eq 'YesNo') { $btnYes.Focus() } else { $btnOK.Focus() }
    $dlg.ShowDialog() | Out-Null
    return $script:dlgResult
}

$window.Add_ContentRendered({
    if (-not [string]::IsNullOrWhiteSpace($script:ConfigLoadWarning)) {
        $warningMessage = $script:ConfigLoadWarning
        $script:ConfigLoadWarning = $null
        Show-ThemedDialog -Message $warningMessage -Title 'Saved settings were reset' -Icon 'Warning' -PrimaryText 'Continue' | Out-Null
    }
})

function Test-NetworkReady {
    $resp = $null
    try {
        $r = [System.Net.WebRequest]::Create("https://raw.githubusercontent.com"); $r.Timeout = 5000; $r.Method = 'HEAD'
        $resp = $r.GetResponse(); return $true
    } catch { return $false }
    finally { if ($resp) { $resp.Close() } }
}

function Reset-UiAfterLaunchFailure {
    param(
        [string]$Title,
        [string]$Message
    )
    try { if ($timer) { $timer.Stop() } } catch {}
    $script:installStartTime = $null
    if ($script:copyResetTimer) { $script:copyResetTimer.Stop() }
    if ($script:activeSyncHash) { $script:activeSyncHash.IsRunning = $false }
    Clear-CompletedRunspaceResources | Out-Null
    $ui['PageInstall'].Visibility='Collapsed'
    $ui['PageConfig'].Visibility='Visible'
    $ui['BtnInstall'].IsEnabled=$true
    $ui['BtnCopyLog'].Visibility='Collapsed'
    $ui['BtnCopyLog'].Content='Copy full log'
    $window.Topmost=$false
    Update-ModePresentation
    Show-ThemedDialog -Title $Title -Message $Message -Icon 'Error' -PrimaryText 'Close' | Out-Null
}
function Switch-ToInstallPage {
    param(
        [string]$Title = 'Preparing setup',
        [string]$Context = 'LibreSpot keeps the interface responsive while it downloads, patches, and applies your selection.',
        [string]$PrepareLabel = 'Prepare',
        [string]$RunLabel = 'Run',
        [string]$VerifyLabel = 'Verify',
        [string]$CompleteLabel = 'Complete'
    )
    try { if ($global:LOG_PATH) { if (-not (Test-Path $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }; $utf8NoBom = New-Object System.Text.UTF8Encoding($false); [System.IO.File]::WriteAllText($global:LOG_PATH, "--- LibreSpot v$global:VERSION $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ---`n", $utf8NoBom) } } catch {}
    $ui['PageConfig'].Visibility='Collapsed'; $ui['PageInstall'].Visibility='Visible'
    $ui['LogOutput'].Text=''; $ui['StatusText'].Text='Checking prerequisites...'; $ui['StepIndicator'].Text='Ready when you are'
    $ui['InstallTitle'].Text = $Title; $ui['InstallContext'].Text = $Context
    $ui['ElapsedTime'].Text=''; if ($ui.ContainsKey('ProgressPercentText')) { $ui['ProgressPercentText'].Text='0%' }; $ui['MainProgress'].Value=0; $ui['MainProgress'].Foreground=$global:BrushGreen
    $ui['CloseBtn'].Visibility='Collapsed'; $ui['BtnBackToConfig'].Visibility='Collapsed'; $ui['BtnCopyLog'].Visibility='Collapsed'; $ui['BtnCopyLog'].Content='Copy full log'
    $window.Topmost = $true
    Set-InstallStageLabels -Prepare $PrepareLabel -Run $RunLabel -Verify $VerifyLabel -Complete $CompleteLabel
    Update-InstallStageVisual
}

$ui['BtnInstall'].Add_Click({
    if ($ui['BtnInstall'].IsEnabled -eq $false) { return }
    if (-not (Test-NetworkReady)) {
        Show-ThemedDialog -Message "LibreSpot could not reach the download sources it needs. Check the connection, then try the setup again." -Title "No Internet Connection" -Icon "Error" -PrimaryText "Close"
        return
    }
    $ui['BtnInstall'].IsEnabled = $false
    $isEasy = $ui['ModeEasy'].IsChecked
    if ($isEasy) {
        $r = Show-ThemedDialog -Message "LibreSpot will remove the current Spotify and Spicetify setup, then rebuild the recommended default stack from scratch." -Title "Start Recommended Setup" -Buttons "YesNo" -Icon "Question" -PrimaryText "Start setup" -SecondaryText "Cancel"
        if ($r -ne 'Yes') { $ui['BtnInstall'].IsEnabled = $true; return }
    }
    $script:InstallConfig = Normalize-LibreSpotConfig -Config (Get-InstallConfig -EasyMode $isEasy)
    $saveSucceeded = Save-LibreSpotConfig -Config $script:InstallConfig
    if ($saveSucceeded) {
        $script:HasSavedConfig = $true
        $script:SavedConfigMode = [string]$script:InstallConfig.Mode
        $script:HasSavedCustomConfig = ($script:SavedConfigMode -eq 'Custom')
        Capture-CustomConfigBaseline
    } else {
        $script:HasSavedConfig = $false
        $script:SavedConfigMode = $null
        $script:HasSavedCustomConfig = $false
        $script:SavedConfigStamp = $null
        $r = Show-ThemedDialog -Message 'LibreSpot could not save these settings to disk. Setup can still continue, but later reapply and recovery actions will not remember this selection until a save succeeds.' -Title 'Could not save settings' -Buttons 'YesNo' -Icon 'Warning' -PrimaryText 'Continue anyway' -SecondaryText 'Cancel'
        if ($r -ne 'Yes') {
            $ui['BtnInstall'].IsEnabled = $true
            Update-ModePresentation
            return
        }
    }
    $installTitle = if ($isEasy) { 'Preparing recommended setup' } else { 'Preparing custom install' }
    $installContext = if ($isEasy) {
        'LibreSpot is refreshing Spotify, applying the pinned default stack, and installing the Marketplace-ready extension set.'
    } else {
        'LibreSpot is validating your selected Spotify tweaks, theme, Marketplace choice, and extension set before it applies them in one pass.'
    }
    try {
        Switch-ToInstallPage -Title $installTitle -Context $installContext -PrepareLabel 'Prepare' -RunLabel 'Build' -VerifyLabel 'Apply' -CompleteLabel 'Complete'
        Start-InstallJob -Config $script:InstallConfig
    } catch {
        Reset-UiAfterLaunchFailure -Title 'Could not start setup' -Message "LibreSpot couldn't start the setup run.`n`n$($_.Exception.Message)"
    }
})

# =============================================================================
# 11. TIMER
# =============================================================================
$script:stepIndex = 0; $script:installStartTime = $null
$stepStates = @("Processing","Processing.","Processing..","Processing...")
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(400)
$timer.Add_Tick({
    $script:stepIndex = ($script:stepIndex + 1) % $stepStates.Count
    if ($ui['PageInstall'].Visibility -eq 'Visible') {
        $cur = $ui['StepIndicator'].Text
        if ($cur -match '^Processing') { $ui['StepIndicator'].Text = $stepStates[$script:stepIndex] }
        Update-InstallStageVisual
    }
    if ($script:installStartTime) { $ui['ElapsedTime'].Text = "Elapsed: {0:mm\:ss}" -f ((Get-Date) - $script:installStartTime) }
})

# =============================================================================
# 12. HELPERS
# =============================================================================
function Update-UI { param([string]$Message,[string]$Level="INFO",[bool]$IsHeader=$false,[string]$StepText=$null)
    $ts = Get-Date -Format "HH:mm:ss"; $lt = "[$ts] [$Level] $Message`n"; $sh = $script:syncHash
    try { if ($global:LOG_PATH) {
        if (-not (Test-Path $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }
        [System.IO.File]::AppendAllText($global:LOG_PATH, $lt)
    } } catch {}
    try { if ($sh) { $sh.Dispatcher.Invoke([Action]{
        $existingText = [string]$sh.LogBlock.Text
        $maxUiLogChars = 60000
        $trimNotice = "[UI] Earlier log lines were trimmed here. Use 'Copy full log' for the full saved log.`n"
        if ($existingText.StartsWith($trimNotice)) {
            $existingText = $existingText.Substring($trimNotice.Length)
        }
        $combinedText = if ([string]::IsNullOrEmpty($existingText)) { $lt } else { $existingText + $lt }
        $trimBudget = $maxUiLogChars - $trimNotice.Length
        if ($trimBudget -lt 1000) { $trimBudget = 1000 }
        if ($combinedText.Length -gt $maxUiLogChars) {
            $tail = $combinedText.Substring($combinedText.Length - $trimBudget)
            $combinedText = $trimNotice + $tail
        }
        $sh.LogBlock.Text = $combinedText
        $sh.Scroller.ScrollToBottom()
        if ($IsHeader -or $Level -eq 'STEP') { $sh.StatusLabel.Text = $Message }
        if ($StepText) { $sh.StepLabel.Text = $StepText }
    }) } } catch {}
}
function Write-Log { param([string]$Message,[string]$Level='INFO'); Update-UI -Message $Message -Level $Level -IsHeader ($Level -eq 'STEP' -or $Level -eq 'HEADER') }

function Read-ProcessOutputDelta {
    param(
        [string]$Path,
        [long]$Offset = 0,
        [string]$Remainder = ''
    )
    $result = @{
        Offset = $Offset
        Remainder = $Remainder
        Lines = @()
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $result }
    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        $reader = $null
        try {
            if ($result.Offset -gt $stream.Length) { $result.Offset = 0; $result.Remainder = '' }
            $null = $stream.Seek($result.Offset, [System.IO.SeekOrigin]::Begin)
            $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
            $chunk = $reader.ReadToEnd()
            $result.Offset = $stream.Position
        } finally {
            if ($reader) { try { $reader.Dispose() } catch {} }
            try { $stream.Dispose() } catch {}
        }
        if ([string]::IsNullOrEmpty($chunk)) { return $result }
        $text = [string]$result.Remainder + $chunk
        $parts = $text -split "\r?\n"
        $hasTrailingNewline = $text.EndsWith("`n") -or $text.EndsWith("`r")
        if ($hasTrailingNewline) {
            $result.Remainder = ''
            $result.Lines = @($parts | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        } elseif ($parts.Count -gt 0) {
            $result.Remainder = [string]$parts[-1]
            if ($parts.Count -gt 1) {
                $result.Lines = @($parts[0..($parts.Count - 2)] | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            }
        }
    } catch {}
    return $result
}

function Download-FileSafe { param([string]$Uri,[string]$OutFile)
    Write-Log "Downloading: $Uri"
    $headers = @{'User-Agent'="LibreSpot/$global:VERSION"}
    try {
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        } catch {}
        $outDir = Split-Path -Path $OutFile -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $OutFile) {
            Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
        }
        try {
            Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing -Headers $headers -TimeoutSec 120 -ErrorAction Stop
        }
        catch {
            Write-Log "Web request failed, trying BITS..." -Level 'WARN'
            try {
                Import-Module BitsTransfer -EA SilentlyContinue
                $bitsJob = Start-BitsTransfer -Source $Uri -Destination $OutFile -Asynchronous -EA Stop
                $deadline = (Get-Date).AddSeconds(120)
                while ($bitsJob.JobState -in @('Transferring','Connecting','Queued','TransientError')) {
                    if ((Get-Date) -gt $deadline) { Remove-BitsTransfer $bitsJob -EA SilentlyContinue; throw "BITS transfer timed out (120s)" }
                    Start-Sleep -Milliseconds 500
                }
                if ($bitsJob.JobState -ne 'Transferred') { $js=$bitsJob.JobState; Remove-BitsTransfer $bitsJob -EA SilentlyContinue; throw "BITS state: $js" }
                Complete-BitsTransfer $bitsJob
            } catch { throw "Download failed: $($_.Exception.Message)" }
        }
        if (-not (Test-Path -LiteralPath $OutFile)) { throw "Download produced no file: $OutFile" }
        if ((Get-Item -LiteralPath $OutFile).Length -eq 0) { throw "Download produced empty file: $OutFile" }
    } catch {
        if (Test-Path -LiteralPath $OutFile) {
            Remove-Item -LiteralPath $OutFile -Force -ErrorAction SilentlyContinue
        }
        throw
    }
}
function Confirm-FileHash { param([string]$Path, [string]$ExpectedHash, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($ExpectedHash)) {
        Write-Log "  Hash verification skipped for $Label (no hash pinned)" -Level 'WARN'
        return
    }
    $actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLower()
    $expected = $ExpectedHash.ToLower()
    if ($actual -ne $expected) {
        throw "SHA256 mismatch for ${Label}`n  Expected: $expected`n  Actual:   $actual`n  File may be corrupted or tampered with. Update pinned hash if this is a legitimate new version."
    }
    Write-Log "  SHA256 verified: $Label"
}

function Hide-SpotifyWindows {
    Get-Process -Name Spotify -EA SilentlyContinue | ForEach-Object {
        if ($_.MainWindowHandle -ne [IntPtr]::Zero) {
            [Win32]::ShowWindowAsync($_.MainWindowHandle, [Win32]::SW_HIDE) | Out-Null
        }
    }
}

function Invoke-ExternalScriptIsolated { param([string]$FilePath,[string]$Arguments,[int]$TimeoutSeconds=600)
    Write-Log "Spawning: $FilePath"
    $stdoutPath = Join-Path $global:TEMP_DIR ("LibreSpot-stdout-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stderrPath = Join-Path $global:TEMP_DIR ("LibreSpot-stderr-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stdoutState = @{ Offset = 0L; Remainder = '' }
    $stderrState = @{ Offset = 0L; Remainder = '' }
    $p = $null
    try {
        $argString = "-NoProfile -ExecutionPolicy Bypass -File `"$FilePath`" $Arguments"
        $p = Start-Process -FilePath 'powershell.exe' -ArgumentList $argString -NoNewWindow -PassThru -Wait:$false -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -ErrorAction Stop
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while (-not $p.HasExited) {
            if ((Get-Date) -gt $deadline) {
                Write-Log "Process exceeded ${TimeoutSeconds}s timeout - terminating." -Level 'WARN'
                try { $p.Kill() } catch {}
                try { $p.WaitForExit(5000) } catch {}
                throw "External process timed out after ${TimeoutSeconds} seconds. It may have hung or entered an interactive prompt."
            }
            $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
            $stdoutState = @{ Offset = $stdoutRead.Offset; Remainder = $stdoutRead.Remainder }
            foreach ($line in $stdoutRead.Lines) { Write-Log $line -Level 'OUT' }

            $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
            $stderrState = @{ Offset = $stderrRead.Offset; Remainder = $stderrRead.Remainder }
            foreach ($line in $stderrRead.Lines) { Write-Log "[STDERR] $line" -Level 'WARN' }
            Start-Sleep -Milliseconds 200
        }
        $p.WaitForExit()

        $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
        foreach ($line in $stdoutRead.Lines + @($stdoutRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log $line -Level 'OUT'
        }
        $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
        foreach ($line in $stderrRead.Lines + @($stderrRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log "[STDERR] $line" -Level 'WARN'
        }

        if ($p.ExitCode -ne 0) { throw "Process exited with code $($p.ExitCode)" }
    } finally {
        if ($p) { try { $p.Dispose() } catch {} }
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

# =============================================================================
# 13. UPDATE CHECKER
# =============================================================================
function Check-ForUpdates {
    Write-Log '=== Checking for dependency updates ===' -Level 'STEP'
    $headers = @{'User-Agent'="LibreSpot/$global:VERSION"}
    $updates = @()

    # SpotX (pinned to a specific commit on main, check for newer commits)
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/SpotX-Official/SpotX/commits/main' -Headers $headers -TimeoutSec 15
        $latestSha = $rel.sha
        $pinnedSha = $global:PinnedReleases.SpotX.Commit
        if ($latestSha -ne $pinnedSha) {
            $short = $latestSha.Substring(0,10)
            $msg = ($rel.commit.message -split "`n")[0]
            $updates += "SpotX: new commit $short"
            Write-Log "  SpotX: new commit $short ($msg)" -Level 'WARN'
        } else { Write-Log "  SpotX: $($pinnedSha.Substring(0,10)) (up to date)" }
    } catch { Write-Log "  SpotX: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Spicetify CLI
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/cli/releases/latest' -Headers $headers -TimeoutSec 15
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.SpicetifyCLI.Version
        if (Compare-LibreSpotVersions -Latest $latest -Current $pinned) { $updates += "CLI: $pinned -> $latest"; Write-Log "  Spicetify CLI: $pinned -> $latest available" -Level 'WARN' }
        else { Write-Log "  Spicetify CLI: v$pinned (up to date)" }
    } catch { Write-Log "  Spicetify CLI: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Marketplace
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/marketplace/releases/latest' -Headers $headers -TimeoutSec 15
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.Marketplace.Version
        if (Compare-LibreSpotVersions -Latest $latest -Current $pinned) { $updates += "Marketplace: $pinned -> $latest"; Write-Log "  Marketplace: $pinned -> $latest available" -Level 'WARN' }
        else { Write-Log "  Marketplace: v$pinned (up to date)" }
    } catch { Write-Log "  Marketplace: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Themes
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/spicetify-themes/commits/master' -Headers $headers -TimeoutSec 15
        $latest = $rel.sha
        $pinned = $global:PinnedReleases.Themes.Commit
        if ($latest -ne $pinned) {
            $short = $latest.Substring(0,10)
            $msg = ($rel.commit.message -split "`n")[0]
            $updates += "Themes: new commit $short"
            Write-Log "  Themes: new commit $short ($msg)" -Level 'WARN'
        } else { Write-Log "  Themes: $($pinned.Substring(0,10)) (up to date)" }
    } catch { Write-Log "  Themes: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # LibreSpot itself
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/SysAdminDoc/LibreSpot/releases/latest' -Headers $headers -TimeoutSec 15
        $latest = $rel.tag_name -replace '^v',''
        if (Compare-LibreSpotVersions -Latest $latest -Current $global:VERSION) {
            $updates += "LibreSpot: $($global:VERSION) -> $latest"
            Write-Log "  LibreSpot: $($global:VERSION) -> $latest available" -Level 'WARN'
        } else {
            Write-Log "  LibreSpot: v$($global:VERSION) (up to date)"
        }
    } catch { Write-Log "  LibreSpot: check failed ($($_.Exception.Message))" -Level 'WARN' }

    if ($updates.Count -eq 0) {
        Write-Log "All dependencies are up to date." -Level 'SUCCESS'
    } else {
        Write-Log "$($updates.Count) update(s) available. Update the PinnedReleases block in the script to upgrade." -Level 'WARN'
        Write-Log "After updating versions, re-download each component and update its SHA256 hash." -Level 'WARN'
    }
    Write-Log '=== Update check complete ===' -Level 'STEP'
}

# =============================================================================
# 14. SPOTIFY NUKER - Comprehensive Self-Contained Uninstaller
# =============================================================================
function Stop-SpotifyProcesses { param([int]$MaxAttempts=5,[int]$RetryDelay=500)
    for ($a=1; $a -le $MaxAttempts; $a++) {
        $procs = Get-Process -Name "Spotify","SpotifyWebHelper","SpotifyMigrator","SpotifyCrashService" -EA SilentlyContinue
        if (-not $procs) { return }
        Write-Log "Killing Spotify processes (attempt $a/$MaxAttempts)..."
        $procs | ForEach-Object { try { Stop-Process -Id $_.Id -Force -EA Stop } catch {} }
        Start-Sleep -Milliseconds $RetryDelay
    }
    $still = Get-Process -Name "Spotify" -EA SilentlyContinue
    if ($still) { Write-Log "Some Spotify processes survived kill attempts." -Level 'WARN' }
}

function Unlock-SpotifyUpdateFolder {
    $updateDir = Join-Path $env:LOCALAPPDATA "Spotify\Update"
    if (-not (Test-Path $updateDir -PathType Container)) { return }
    try {
        $acl = Get-Acl $updateDir
        $changed = $false
        foreach ($rule in $acl.Access) {
            if ($rule.AccessControlType -eq 'Deny') {
                $null = $acl.RemoveAccessRule($rule); $changed = $true
            }
        }
        if ($changed) { Set-Acl $updateDir $acl; Write-Log "Unlocked Update folder ACLs." }
    } catch { Write-Log "Could not unlock Update folder: $($_.Exception.Message)" -Level 'WARN' }
}

function Get-DesktopPath {
    try {
        $shell = (Get-ItemProperty "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" -EA Stop).Desktop
        if ($shell) { $shell = [Environment]::ExpandEnvironmentVariables($shell) }
        if ($shell -and (Test-Path $shell)) { return $shell }
    } catch {}
    return [Environment]::GetFolderPath('Desktop')
}

function Test-SafeRemovalTarget {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    try {
        $resolved = (Get-Item -LiteralPath $Path -Force -ErrorAction Stop).FullName
    } catch {
        return $false
    }
    if ([string]::IsNullOrWhiteSpace($resolved)) { return $false }

    $normalized = $resolved.TrimEnd('\')
    $root = [System.IO.Path]::GetPathRoot($resolved).TrimEnd('\')
    if ($normalized -eq $root) { return $false }

    $blockedTargets = @(
        $env:USERPROFILE,
        $env:APPDATA,
        $env:LOCALAPPDATA,
        $env:TEMP,
        $env:SystemRoot,
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)}
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.TrimEnd('\') }

    return ($normalized -notin $blockedTargets)
}

function Clear-DirectoryContentsSafely {
    param([string]$Path, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Container)) { return 0 }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-Log "  Refusing to clear unsafe directory target: $Path" -Level 'WARN'
        return 0
    }
    $removedCount = 0
    Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue | ForEach-Object {
        $itemLabel = if ($Label) { "${Label}: $($_.Name)" } else { $_.FullName }
        $removedCount += Remove-PathSafely -Path $_.FullName -Label $itemLabel
    }
    return $removedCount
}

function Remove-PathSafely { param([string]$Path,[string]$Label)
    if ([string]::IsNullOrWhiteSpace($Path)) { return 0 }
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-Log "  Refusing to remove unsafe target: $Path" -Level 'WARN'
        return 0
    }
    try {
        $null = & icacls.exe $Path /reset /T /C /Q 2>$null
        Remove-Item -LiteralPath $Path -Recurse -Force -EA Stop
        Write-Log "  Removed: $(if($Label){$Label}else{$Path})"
        return 1
    } catch {
        Write-Log "  Failed to remove: $Path ($($_.Exception.Message))" -Level 'WARN'
        return 0
    }
}

function Module-NukeSpotify {
    Write-Log "=== LibreSpot Comprehensive Spotify Uninstaller ===" -Level 'STEP'
    $rc = 0

    # --- Phase 1: Kill all Spotify processes ---
    Write-Log "[Phase 1/8] Terminating Spotify processes..."
    Stop-SpotifyProcesses

    # --- Phase 2: Remove Spotify Store (UWP/AppX) ---
    Write-Log "[Phase 2/8] Checking for Microsoft Store Spotify..."
    try {
        if ($PSVersionTable.PSVersion.Major -ge 7) { Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue }
        $storeApp = Get-AppxPackage -Name "SpotifyAB.SpotifyMusic" -EA SilentlyContinue
        if ($storeApp) {
            $savedPP = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
            try { Remove-AppxPackage -Package $storeApp.PackageFullName -EA Stop } finally { $ProgressPreference = $savedPP }
            Write-Log "  Removed Spotify Store app."; $rc++
        } else { Write-Log "  No Store version found." }
    } catch { Write-Log "  Store removal failed: $($_.Exception.Message)" -Level 'WARN' }

    # --- Phase 3: Run Spotify native uninstaller (silent) ---
    Write-Log "[Phase 3/8] Running native uninstaller..."
    $spotifyExe = Join-Path $env:APPDATA "Spotify\Spotify.exe"
    if (Test-Path $spotifyExe) {
        try {
            Unlock-SpotifyUpdateFolder
            $null = cmd /c "`"$spotifyExe`" /UNINSTALL /SILENT" 2>$null
            $deadline = (Get-Date).AddSeconds(15)
            while ((Get-Process -Name "SpotifyUninstall" -EA SilentlyContinue) -and (Get-Date) -lt $deadline) {
                Start-Sleep -Milliseconds 500
            }
            Start-Sleep -Milliseconds 500
            Write-Log "  Native uninstaller completed."; $rc++
        } catch { Write-Log "  Native uninstaller error: $($_.Exception.Message)" -Level 'WARN' }
    } else { Write-Log "  No native Spotify.exe found, skipping." }
    Stop-SpotifyProcesses -MaxAttempts 3

    # --- Phase 4: Nuke file system ---
    Write-Log "[Phase 4/8] Removing Spotify files and folders..."
    $desktopPath = Get-DesktopPath
    $filesToNuke = @(
        @{ Path = (Join-Path $env:APPDATA "Spotify");        Label = "Spotify Roaming (%APPDATA%)" }
        @{ Path = (Join-Path $env:LOCALAPPDATA "Spotify");   Label = "Spotify Local (%LOCALAPPDATA%)" }
        @{ Path = (Join-Path $env:APPDATA "spicetify");      Label = "Spicetify Config (%APPDATA%)" }
        @{ Path = (Join-Path $env:LOCALAPPDATA "spicetify"); Label = "Spicetify CLI (%LOCALAPPDATA%)" }
        @{ Path = (Join-Path $env:TEMP "SpotifyUninstall.exe"); Label = "Spotify uninstaller (TEMP)" }
        @{ Path = (Join-Path $desktopPath "Spotify.lnk");    Label = "Desktop shortcut" }
        @{ Path = (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Spotify.lnk"); Label = "Start Menu shortcut" }
    )
    foreach ($f in $filesToNuke) { $rc += Remove-PathSafely -Path $f.Path -Label $f.Label }

    # Glob targets: SpotX temp folders, Spotify installers, spicetify temp
    @(
        @{ Pattern = (Join-Path $env:TEMP "SpotX_Temp*");  Label = "SpotX temp" }
        @{ Pattern = (Join-Path $env:TEMP "Spotify_*");    Label = "Spotify temp installer" }
        @{ Pattern = (Join-Path $env:TEMP "spicetify*");   Label = "Spicetify temp" }
    ) | ForEach-Object {
        $lbl = $_.Label
        Get-ChildItem -Path $_.Pattern -EA SilentlyContinue | ForEach-Object {
            $rc += Remove-PathSafely -Path $_.FullName -Label "${lbl}: $($_.Name)"
        }
    }

    # IE/Edge cached Spotify installers
    $ieCache = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\INetCache"
    if (Test-Path $ieCache) {
        Get-ChildItem -Path $ieCache -Recurse -Force -Filter "SpotifyFullSetup*" -EA SilentlyContinue | ForEach-Object {
            $rc += Remove-PathSafely -Path $_.FullName -Label "Cached installer: $($_.Name)"
        }
    }

    # --- Phase 5: Registry cleanup ---
    Write-Log "[Phase 5/8] Cleaning registry..."
    $regKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify"
        "HKCU:\Software\Spotify"
        "HKCU:\Software\Classes\spotify"
        "HKCU:\Software\Classes\spotify-client"
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\ElevationPolicy\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\DragDrop\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\Spotify.exe"
    )
    foreach ($key in $regKeys) {
        if (Test-Path $key) {
            try { Remove-Item -Path $key -Recurse -Force -EA Stop; Write-Log "  Removed: $key"; $rc++ }
            catch { Write-Log "  Failed: $key" -Level 'WARN' }
        }
    }
    $regValues = @(
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify" }
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify Web Helper" }
    )
    foreach ($rv in $regValues) {
        if (Get-ItemProperty -Path $rv.Path -Name $rv.Name -EA SilentlyContinue) {
            try { Remove-ItemProperty -Path $rv.Path -Name $rv.Name -Force -EA Stop; Write-Log "  Removed startup: $($rv.Name)"; $rc++ }
            catch {}
        }
    }

    # --- Phase 6: Scheduled tasks ---
    Write-Log "[Phase 6/8] Removing scheduled tasks..."
    try {
        Get-ScheduledTask -EA SilentlyContinue | Where-Object { $_.TaskName -match 'Spotify' } | ForEach-Object {
            try { Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -EA Stop; Write-Log "  Removed task: $($_.TaskName)"; $rc++ }
            catch {}
        }
    } catch { Write-Log "  Task cleanup skipped." }

    # --- Phase 7: Firewall rules ---
    Write-Log "[Phase 7/8] Removing firewall rules..."
    try {
        Get-NetFirewallRule -EA SilentlyContinue | Where-Object { $_.DisplayName -match 'Spotify' } | ForEach-Object {
            try { Remove-NetFirewallRule -Name $_.Name -EA Stop; Write-Log "  Removed firewall: $($_.DisplayName)"; $rc++ }
            catch {}
        }
    } catch { Write-Log "  Firewall cleanup skipped." }

    # --- Phase 8: Verification sweep ---
    Write-Log "[Phase 8/8] Verification sweep..."
    $survivors = @()
    @((Join-Path $env:APPDATA "Spotify"), (Join-Path $env:LOCALAPPDATA "Spotify")) | ForEach-Object {
        if (Test-Path $_) {
            Start-Sleep -Milliseconds 1500
            if (Remove-PathSafely -Path $_ -Label "Spotify cleanup retry") {
                $rc++
            } else {
                $survivors += $_
            }
        }
    }
    if ($survivors.Count -gt 0) {
        Write-Log "  Could not fully remove $($survivors.Count) path(s) (may need reboot):" -Level 'WARN'
        $survivors | ForEach-Object { Write-Log "    - $_" -Level 'WARN' }
    }

    Write-Log "=== Nuke complete: $rc items removed ===" -Level 'STEP'
}

# =============================================================================
# 15. INSTALL MODULES
# =============================================================================
function Module-InstallSpotX { param($Config,$SyncHash)
    Write-Log "Installing SpotX v$($global:PinnedReleases.SpotX.Version)..." -Level 'STEP'
    $dest = New-LibreSpotTempFile -Name 'spotx_run.ps1'
    try {
        Download-FileSafe -Uri $global:URL_SPOTX -OutFile $dest
        Confirm-FileHash -Path $dest -ExpectedHash $global:PinnedReleases.SpotX.SHA256 -Label "SpotX run.ps1"
        $params = Build-SpotXParams -Config $Config
        if (Test-Path $global:SPOTIFY_EXE_PATH) {
            $ver = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            Write-Log "Spotify $ver detected - SpotX will verify version compatibility"
        } else {
            Write-Log "Spotify not installed - SpotX will download recommended version"
        }
        Write-Log "Params: $params"
        if ($SyncHash) { $SyncHash.AllowSpotify = $true }
        try {
            Invoke-ExternalScriptIsolated -FilePath $dest -Arguments $params
            # Verify SpotX patching succeeded
            if (-not (Test-Path $global:SPOTIFY_EXE_PATH)) {
                throw "SpotX failed - Spotify.exe not found at $global:SPOTIFY_EXE_PATH. Check the log above for errors."
            }
            $elfDll = Join-Path (Split-Path $global:SPOTIFY_EXE_PATH) "chrome_elf.dll"
            if (-not (Test-Path $elfDll)) {
                throw "Spotify installation is incomplete - chrome_elf.dll is missing. This usually means the Spotify download failed or was corrupted."
            }
            $patchedVer = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            Write-Log "Spotify $patchedVer patched successfully." -Level 'SUCCESS'
            Write-Log "Launching Spotify (hidden) to generate config files..."
            if (Test-Path $global:SPOTIFY_EXE_PATH) {
                $sp = Start-Process $global:SPOTIFY_EXE_PATH -WindowStyle Minimized -PassThru
                Start-Sleep -Milliseconds 800
                Hide-SpotifyWindows
            }
            $prefsPath = Join-Path $env:APPDATA "Spotify\prefs"
            $waited = 0; $maxWait = 45
            while ($waited -lt $maxWait) {
                if ((Test-Path $prefsPath) -and ((Get-Item $prefsPath).Length -gt 10)) {
                    Write-Log "Config files detected after ${waited}s."; break
                }
                Hide-SpotifyWindows
                Start-Sleep -Seconds 2; $waited += 2
            }
            if ($waited -ge $maxWait) { Write-Log "Timed out waiting for config (${maxWait}s). Continuing..." -Level 'WARN' }
            Start-Sleep -Seconds 3; Stop-SpotifyProcesses -maxAttempts 3
        } finally {
            if ($SyncHash) { $SyncHash.AllowSpotify = $false }
        }
    } finally {
        Remove-Item -LiteralPath $dest -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallSpicetifyCLI {
    $ver = $global:PinnedReleases.SpicetifyCLI.Version
    Write-Log "Installing Spicetify CLI v$ver..." -Level 'STEP'
    New-Item -Path $global:SPICETIFY_DIR -ItemType Directory -Force | Out-Null
    $arch = switch ($env:PROCESSOR_ARCHITECTURE) { 'ARM64' {'arm64'} default {'x64'} }
    $zip = $global:URL_SPICETIFY_FMT -f $ver, $arch
    $zp = New-LibreSpotTempFile -Name 'spicetify.zip'
    try {
        Download-FileSafe -Uri $zip -OutFile $zp
        $expectedHash = $global:PinnedReleases.SpicetifyCLI.SHA256[$arch]
        Confirm-FileHash -Path $zp -ExpectedHash $expectedHash -Label "Spicetify CLI ($arch)"
        if (Test-Path -LiteralPath $global:SPICETIFY_DIR) {
            $null = Clear-DirectoryContentsSafely -Path $global:SPICETIFY_DIR -Label 'Spicetify CLI'
        }
        [System.IO.Compression.ZipFile]::ExtractToDirectory($zp, $global:SPICETIFY_DIR)
        $sExe = Join-Path $global:SPICETIFY_DIR "spicetify.exe"
        if (-not (Test-Path $sExe)) { throw "spicetify.exe not found after extraction - ZIP may be corrupted" }
        $null = Add-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'Process'
        if (Add-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'User') {
            Write-Log "Added Spicetify to user PATH."
        }
        Write-Log "Generating config..."
        Invoke-SpicetifyCli -Arguments @('config', '--bypass-admin') -FailureMessage 'Could not generate the initial Spicetify config.'
        Write-Log "Spicetify CLI v$ver installed."
    } finally {
        Remove-Item -LiteralPath $zp -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallThemes { param($Config)
    $tn = $Config.Spicetify_Theme; if ($tn -eq '(None - Marketplace Only)') { Write-Log "No theme selected."; return }
    Write-Log "Installing theme: $tn..." -Level 'STEP'
    $tz = New-LibreSpotTempFile -Name 'themes.zip'
    $tu = New-LibreSpotTempDirectory -Name 'themes-unpack'
    $td=Join-Path $global:SPICETIFY_CONFIG_DIR "Themes"
    if (-not (Test-Path $td)) { New-Item -Path $td -ItemType Directory -Force | Out-Null }
    try {
        Download-FileSafe -Uri $global:URL_THEMES_REPO -OutFile $tz
        Confirm-FileHash -Path $tz -ExpectedHash $global:PinnedReleases.Themes.SHA256 -Label "Themes archive"
        [System.IO.Compression.ZipFile]::ExtractToDirectory($tz, $tu)
        $root = Get-ChildItem -LiteralPath $tu -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $root) { throw "Theme archive did not contain an unpacked root folder." }
        $src = Join-Path $root.FullName $tn
        if (-not (Test-Path -LiteralPath $src -PathType Container)) {
            throw "Theme '$tn' was not found in the pinned theme archive."
        }
        $dst=Join-Path $td $tn
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        Copy-Item $src -Destination $dst -Recurse -Force
        Write-Log "Theme copied to $dst"
        if (-not (Test-Path (Join-Path $td $tn))) { return }
        $sc = $Config.Spicetify_Scheme; Write-Log "Setting theme=$tn, scheme=$sc"
        Invoke-SpicetifyCli -Arguments @('config', 'current_theme', $tn, '--bypass-admin') -FailureMessage "Could not set Spicetify theme '$tn'."
        if (-not [string]::IsNullOrWhiteSpace($sc)) {
            Invoke-SpicetifyCli -Arguments @('config', 'color_scheme', $sc, '--bypass-admin') -FailureMessage "Could not set color scheme '$sc'."
        }
        $needsThemeJs = @("Dribbblish","StarryNight","Turntable") -contains $tn
        $jsVal = if ($needsThemeJs) { "1" } else { "0" }
        Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '1', 'replace_colors', '1', 'overwrite_assets', '1', 'inject_theme_js', $jsVal, '--bypass-admin') -FailureMessage 'Could not enable the selected theme assets.'
    } finally {
        Remove-Item -LiteralPath $tz -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $tu -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallExtensions { param($Config)
    $exts = @($Config.Spicetify_Extensions)
    if ($exts.Count -eq 0) {
        Write-Log "Extensions: none selected. Removing LibreSpot-managed extensions if they are still enabled..." -Level 'STEP'
    } else {
        Write-Log "Extensions: $($exts -join ', ')..." -Level 'STEP'
    }
    Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems $exts -ManagedItems @($global:BuiltInExtensions.Keys)
}

function Module-InstallMarketplace { param($Config)
    $managedApps = @('marketplace')
    $marketplaceDirs = @(
        (Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps\marketplace'),
        (Join-Path $global:SPICETIFY_DIR 'CustomApps\marketplace')
    )
    if (-not $Config.Spicetify_Marketplace) {
        Write-Log "Marketplace: disabled. Removing LibreSpot-managed Marketplace state if present..." -Level 'STEP'
        foreach ($dir in $marketplaceDirs) {
            $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        return
    }

    Write-Log "Installing Marketplace..." -Level 'STEP'
    $ca = Join-Path $global:SPICETIFY_CONFIG_DIR 'CustomApps'
    if (-not (Test-Path -LiteralPath $ca)) { $ca = Join-Path $global:SPICETIFY_DIR 'CustomApps' }
    New-Item -Path $ca -ItemType Directory -Force | Out-Null
    $md=Join-Path $ca "marketplace"
    $mz = New-LibreSpotTempFile -Name 'marketplace.zip'
    $mu = New-LibreSpotTempDirectory -Name 'marketplace-unpack'
    if (Test-Path -LiteralPath $md) { $null = Remove-PathSafely -Path $md -Label 'Marketplace app' }
    New-Item -Path $md -ItemType Directory -Force | Out-Null
    try {
        Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $mz
        Confirm-FileHash -Path $mz -ExpectedHash $global:PinnedReleases.Marketplace.SHA256 -Label "Marketplace"
        [System.IO.Compression.ZipFile]::ExtractToDirectory($mz, $mu)
        $sp = if (Test-Path (Join-Path $mu "marketplace-dist")) { Join-Path $mu "marketplace-dist\*" } else { Join-Path $mu "*" }
        Copy-Item -Path $sp -Destination $md -Recurse -Force
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @('marketplace') -ManagedItems $managedApps
        Write-Log "Marketplace enabled."
    } finally {
        Remove-Item -LiteralPath $mz -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $mu -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-SpicetifyDiagnosticSnapshot {
    $snapshot = [ordered]@{}
    $configPath = Join-Path $global:SPICETIFY_CONFIG_DIR 'config-xpui.ini'
    if (Test-Path -LiteralPath $configPath) {
        try {
            foreach ($line in Get-Content -LiteralPath $configPath -ErrorAction Stop) {
                if ($line -match '^\s*(spotify_path|prefs_path)\s*=\s*(.+?)\s*$') {
                    $snapshot[$Matches[1]] = $Matches[2].Trim()
                }
            }
        } catch {}
    }
    $snapshot['xpui_spa_exists'] = Test-Path -LiteralPath (Join-Path (Split-Path $global:SPOTIFY_EXE_PATH -Parent) 'Apps\xpui.spa')
    $snapshot['spotify_exe_exists'] = Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH
    return $snapshot
}

function Module-ApplySpicetify { param($Config)
    Write-Log "Applying Spicetify changes..." -Level 'STEP'
    if ($Config.Spicetify_Theme -eq '(None - Marketplace Only)') {
        try {
            Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '0', 'replace_colors', '0', 'overwrite_assets', '0', 'inject_theme_js', '0', '--bypass-admin') -FailureMessage 'Could not disable theme asset injection for the Marketplace-only setup.'
        } catch {
            Write-Log "Pre-apply config tweak failed: $($_.Exception.Message)" -Level 'WARN'
        }
    }

    $diag = Get-SpicetifyDiagnosticSnapshot
    foreach ($key in $diag.Keys) {
        Write-Log "  diag: $key = $($diag[$key])"
    }

    $backupSucceeded = $false
    try {
        Invoke-SpicetifyCli -Arguments @('backup', '--bypass-admin') -FailureMessage 'Could not create a Spicetify backup.'
        $backupSucceeded = $true
    } catch {
        Write-Log "Spicetify backup step reported: $($_.Exception.Message)" -Level 'WARN'
    }

    $applyError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('apply', '--bypass-admin') -FailureMessage 'Could not apply the selected Spicetify setup.'
        Write-Log "Spicetify applied successfully."
        return
    } catch {
        $applyError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown Spicetify apply error.' }
        if (-not $backupSucceeded) {
            $applyError = "$applyError (Backup step also failed, which usually means Spotify's xpui.spa is missing or unreadable.)"
        }
        Write-Log "Spicetify apply failed: $applyError" -Level 'WARN'
    }

    Write-Log "Apply failed. Rolling back to prevent a blank screen..." -Level 'WARN'
    $restoreError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage 'Could not restore Spotify after the failed apply.'
    } catch {
        $restoreError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown restore error.' }
    }

    if ([string]::IsNullOrWhiteSpace($restoreError)) {
        throw "Spicetify apply failed, but LibreSpot restored Spotify to vanilla to keep the app usable. $applyError"
    }

    throw "Spicetify apply failed and the automatic restore also failed. Spotify may show a blank screen until you run Maintenance > Full Reset. Apply error: $applyError Restore error: $restoreError"
}

function Reapply-SavedSpicetifySetup { param($Config)
    if (-not (Test-SpicetifyCliInstalled)) {
        Write-Log "Spicetify CLI is missing, so LibreSpot will reinstall it before restoring your saved setup." -Level 'WARN'
        Module-InstallSpicetifyCLI
    }

    Module-InstallThemes -Config $Config
    Module-InstallExtensions -Config $Config
    Module-InstallMarketplace -Config $Config
    Module-ApplySpicetify -Config $Config
}

# =============================================================================
# 16. THREADING
# =============================================================================
$watcherBlock = { param($sh)
    # Load Win32 P/Invoke in watcher runspace
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32W {
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    public const int SW_HIDE = 0;
}
'@ -ErrorAction SilentlyContinue

    while ($sh.IsRunning) {
        $procs = Get-Process -Name Spotify,SpotifyInstaller -EA SilentlyContinue
        if ($sh.AllowSpotify) {
            # SpotX/install is running - don't kill Spotify but keep it hidden
            foreach ($p in $procs) {
                if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
                    try { [Win32W]::ShowWindowAsync($p.MainWindowHandle, [Win32W]::SW_HIDE) | Out-Null } catch {}
                }
            }
            Start-Sleep -Milliseconds 500; continue
        }
        if ($procs) {
            for ($i=0;$i -lt 30;$i++) { if (-not $sh.IsRunning -or $sh.AllowSpotify) { break }; Start-Sleep -Milliseconds 100 }
            if (-not $sh.AllowSpotify) { Stop-Process -Name Spotify -Force -EA SilentlyContinue }
        }
        Start-Sleep -Milliseconds 500
    }
}

$installBlock = { param($sh,$cfg)
    $script:syncHash = $sh
    $ErrorActionPreference = 'Stop'
    try {
        Write-Log "--- LibreSpot Installation Started ---" -Level 'HEADER'; Write-Log "Mode: $($cfg.Mode)"
        $steps = @('SpotX','SpicetifyCLI','Themes','Extensions','Marketplace','Apply')
        if ($cfg.CleanInstall) { $steps = @('Cleanup') + $steps }
        $stepLabels = @{
            Cleanup      = 'Removing the old setup'
            SpotX        = 'Applying SpotX'
            SpicetifyCLI = 'Installing Spicetify CLI'
            Themes       = 'Adding bundled themes'
            Extensions   = 'Preparing extensions'
            Marketplace  = 'Installing Marketplace'
            Apply        = 'Applying your setup'
        }
        $total = $steps.Count; $n = 0
        foreach ($s in $steps) { $n++
            $stepLabel = if ($stepLabels.ContainsKey($s)) { $stepLabels[$s] } else { $s }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text = "Step $n of ${total}: $stepLabel"; $sh.ProgressBar.Value = [int]((($n-1)/$total)*100) })
            switch ($s) {
                'Cleanup'      { Module-NukeSpotify }
                'SpotX'        { Module-InstallSpotX -Config $cfg -SyncHash $sh }
                'SpicetifyCLI' { Module-InstallSpicetifyCLI }
                'Themes'       { Module-InstallThemes -Config $cfg }
                'Extensions'   { Module-InstallExtensions -Config $cfg }
                'Marketplace'  { Module-InstallMarketplace -Config $cfg }
                'Apply'        { Module-ApplySpicetify -Config $cfg }
            }
        }
        # Cleanup temp files
        @("spotx_run.ps1","SpotifySetup.exe","spicetify.zip","themes.zip","mp.zip") | ForEach-Object {
            $tf = Join-Path $global:TEMP_DIR $_; if (Test-Path $tf) { Remove-Item $tf -Force -EA SilentlyContinue }
        }
        @("themes-unpack","mp_unpack") | ForEach-Object {
            $td = Join-Path $global:TEMP_DIR $_; if (Test-Path $td) { Remove-Item $td -Recurse -Force -EA SilentlyContinue }
        }
        Write-Log "Temp files cleaned up."
        $finalStep = 'Ready when you are'
        if ($cfg.LaunchAfter -and (Test-Path $global:SPOTIFY_EXE_PATH)) { Write-Log "Launching Spotify..." -Level 'SUCCESS'; Start-Process $global:SPOTIFY_EXE_PATH; $finalStep = 'Spotify is opening' }
        Write-Log "--- Installation Complete ---" -Level 'SUCCESS'; $sh.IsRunning=$false
        $installDoneContext = if ($cfg.LaunchAfter -and (Test-Path $global:SPOTIFY_EXE_PATH)) {
            'LibreSpot finished applying your selected setup and is handing off to Spotify now.'
        } else {
            'LibreSpot finished applying your selected setup. You can close the window or copy the detailed log for reference.'
        }
        $sh.Dispatcher.Invoke([Action]{ $sh.ProgressBar.Value=100; $sh.StatusLabel.Text="Setup complete"; $sh.StepLabel.Text=$finalStep; $sh.InstallTitle.Text='Setup complete'; $sh.InstallContext.Text=$installDoneContext; $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Visibility="Visible"; if($sh.Timer){$sh.Timer.Stop()}; $sh.Window.Topmost=$false; $sh.Window.Activate(); try{[Win32]::FlashTaskbar($sh.WindowHandle)}catch{} })
    } catch { $sh.IsRunning=$false; $em=$_.Exception.Message; $st=$_.ScriptStackTrace
        $sh.Dispatcher.Invoke([Action]{ if($sh.Timer){$sh.Timer.Stop()}; $sh.LogBlock.Text+="`n[FATAL] $em`n$st"; $sh.StatusLabel.Text="Setup stopped"
            $sh.StepLabel.Text="Needs attention"; $sh.InstallTitle.Text='Setup needs attention'; $sh.InstallContext.Text='LibreSpot stopped before the install finished. Review the log below, then go back to setup or copy the details if you want to troubleshoot.'; $sh.ProgressBar.Foreground=$global:BrushError; $sh.ProgressBar.Value=100; $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Visibility="Visible"; $sh.Window.Topmost=$false; $sh.Window.Activate(); try{[Win32]::FlashTaskbar($sh.WindowHandle)}catch{} })
    }
}

$maintBlock = { param($sh,$action)
    $script:syncHash = $sh
    $ErrorActionPreference = 'Stop'
    try {
        if ($action -eq 'CheckUpdates') {
            Write-Log "--- Dependency Update Check ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Checking upstream releases"; $sh.ProgressBar.Value=20 })
            Check-ForUpdates
            Write-Log "--- Check Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'Reapply') {
            Write-Log "--- Reapply After Update ---" -Level 'HEADER'
            if (-not (Test-Path $global:SPOTIFY_EXE_PATH)) { throw "Spotify not found at $global:SPOTIFY_EXE_PATH - install Spotify first" }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Refreshing SpotX"; $sh.ProgressBar.Value=25 })
            $dest = New-LibreSpotTempFile -Name 'spotx_run.ps1'
            $saved = $null
            try { $saved = Load-LibreSpotConfig } catch {}
            if (-not $saved) {
                $saved = Normalize-LibreSpotConfig -Config @{}
                Write-Log "Using defaults (no saved config)" -Level 'WARN'
            } else {
                Write-Log "Using saved config"
            }
            $sp=Build-SpotXParams -Config $saved
            try {
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $dest
                Confirm-FileHash -Path $dest -ExpectedHash $global:PinnedReleases.SpotX.SHA256 -Label "SpotX run.ps1"
                Write-Log "SpotX will verify version compatibility and overwrite if needed"
                $sh.AllowSpotify=$true
                try { Invoke-ExternalScriptIsolated -FilePath $dest -Arguments $sp } finally { $sh.AllowSpotify=$false }
            } finally {
                Remove-Item -LiteralPath $dest -Force -ErrorAction SilentlyContinue
            }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring saved Spicetify state"; $sh.ProgressBar.Value=70 })
            Reapply-SavedSpicetifySetup -Config $saved
            Write-Log "Saved Spicetify setup restored."
            Write-Log "--- Reapply Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'RestoreVanilla') {
            Write-Log "--- Restore Vanilla Spotify ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring vanilla files"; $sh.ProgressBar.Value=30 })
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Could not restore vanilla Spotify.' -MissingMessage 'Spicetify CLI was not found, so LibreSpot cannot run a restore. Spotify may already be vanilla.') {
                Write-Log "Vanilla Spotify restored successfully."
            }
            Write-Log "--- Restore Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'UninstallSpicetify') {
            Write-Log "--- Uninstall Spicetify ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring Spotify"; $sh.ProgressBar.Value=15 })
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Could not restore Spotify before uninstalling Spicetify.' -MissingMessage 'Spicetify CLI was already missing, so LibreSpot will remove any leftover files and PATH entries directly.') {
                Write-Log "Spicetify mods restored."
            }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Removing Spicetify files"; $sh.ProgressBar.Value=45 })
            if (Remove-PathSafely -Path $global:SPICETIFY_CONFIG_DIR -Label 'Spicetify config directory') { Write-Log "Removed config dir." }
            if (Remove-PathSafely -Path $global:SPICETIFY_DIR -Label 'Spicetify CLI directory') { Write-Log "Removed CLI dir." }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Cleaning PATH"; $sh.ProgressBar.Value=75 })
            if (Remove-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'Process') { Write-Log "Removed Spicetify from the current session PATH." }
            if (Remove-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'User') {
                Write-Log "Removed Spicetify from user PATH."
            }
            Write-Log "--- Uninstall Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'FullReset') {
            Write-Log "--- Full Reset ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring vanilla Spotify"; $sh.ProgressBar.Value=10 })
            try {
                Invoke-SpicetifyCli -Arguments @('restore','--bypass-admin') -FailureMessage 'Could not restore Spotify before the full reset.'
                Write-Log "Spicetify restored."
            } catch {
                Write-Log "$($_.Exception.Message) Continuing with the full reset because Spotify will be removed next." -Level 'WARN'
            }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Removing Spicetify"; $sh.ProgressBar.Value=30 })
            $null = Remove-PathSafely -Path $global:SPICETIFY_CONFIG_DIR -Label 'Spicetify config directory'
            $null = Remove-PathSafely -Path $global:SPICETIFY_DIR -Label 'Spicetify CLI directory'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Cleaning Spotify files"; $sh.ProgressBar.Value=50 }); Module-NukeSpotify
            $null = Remove-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'Process'
            if (Remove-PathEntry -Entry $global:SPICETIFY_DIR -Scope 'User') { Write-Log "Removed Spicetify from user PATH." }
            Write-Log "--- Full Reset Complete ---" -Level 'SUCCESS'
        }
        $doneStatus = switch ($action) {
            'CheckUpdates' { 'Version check complete' }
            'Reapply' { 'Setup reapplied' }
            'RestoreVanilla' { 'Spotify restored' }
            'UninstallSpicetify' { 'Spicetify removed' }
            'FullReset' { 'Full reset complete' }
            default { 'Action complete' }
        }
        $doneStep = switch ($action) {
            'CheckUpdates' { 'Pinned versions reviewed' }
            'Reapply' { 'Ready for Spotify' }
            'RestoreVanilla' { 'Vanilla interface restored' }
            'UninstallSpicetify' { 'Spotify is back to vanilla' }
            'FullReset' { 'System is ready for a fresh start' }
            default { 'Ready for next step' }
        }
        $sh.IsRunning=$false
        $doneContext = switch ($action) {
            'CheckUpdates' { 'LibreSpot compared the pinned releases against upstream versions. Review the log for anything newer before you decide to update the script pins.' }
            'Reapply' { 'LibreSpot refreshed the saved SpotX and Spicetify setup so Spotify should be back in sync with your last chosen configuration.' }
            'RestoreVanilla' { 'LibreSpot removed the active Spicetify customizations and brought Spotify back to its vanilla interface while leaving SpotX in place.' }
            'UninstallSpicetify' { 'LibreSpot removed the Spicetify CLI, configuration, and PATH changes after restoring vanilla Spotify first.' }
            'FullReset' { 'LibreSpot completed the deepest cleanup path and removed the Spotify customization stack so you can start fresh.' }
            default { 'LibreSpot finished the requested maintenance action.' }
        }
        $sh.Dispatcher.Invoke([Action]{ $sh.ProgressBar.Value=100; $sh.StatusLabel.Text=$doneStatus; $sh.StepLabel.Text=$doneStep; $sh.InstallTitle.Text=$doneStatus; $sh.InstallContext.Text=$doneContext
            $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Visibility="Visible"; if($sh.Timer){$sh.Timer.Stop()}; $sh.Window.Topmost=$false; $sh.Window.Activate(); try{[Win32]::FlashTaskbar($sh.WindowHandle)}catch{} })
    } catch { $sh.IsRunning=$false; $em=$_.Exception.Message; $st=$_.ScriptStackTrace
        $sh.Dispatcher.Invoke([Action]{ if($sh.Timer){$sh.Timer.Stop()}; $sh.LogBlock.Text+="`n[FATAL] $em`n$st"; $sh.StatusLabel.Text="Maintenance stopped"; $sh.StepLabel.Text="Needs attention"; $sh.InstallTitle.Text='Maintenance needs attention'; $sh.InstallContext.Text='LibreSpot stopped before the maintenance action finished. Review the live log below, then go back when you are ready to try again.'
            $sh.ProgressBar.Foreground=$global:BrushError; $sh.ProgressBar.Value=100; $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Visibility="Visible"; $sh.Window.Topmost=$false; $sh.Window.Activate(); try{[Win32]::FlashTaskbar($sh.WindowHandle)}catch{} })
    }
}

# =============================================================================
# 17. RUNSPACE INFRASTRUCTURE
# =============================================================================
$functionNamesForWorker = @(
    'ConvertTo-PlainHashtable','ConvertTo-ConfigBoolean','ConvertTo-ConfigInt','Normalize-LibreSpotConfig','Move-ConfigFileToQuarantine',
    'Get-LibreSpotTempRoot','New-LibreSpotTempFile','New-LibreSpotTempDirectory',
    'Update-UI','Write-Log','Download-FileSafe','Confirm-FileHash','Hide-SpotifyWindows','Invoke-ExternalScriptIsolated','Read-ProcessOutputDelta','Test-NetworkReady','Check-ForUpdates','Compare-LibreSpotVersions',
    'Stop-SpotifyProcesses','Unlock-SpotifyUpdateFolder','Get-DesktopPath','Test-SafeRemovalTarget','Clear-DirectoryContentsSafely','Remove-PathSafely',
    'Get-SpicetifyConfigEntries','Get-SpicetifyConfigListValue','Invoke-SpicetifyCli','Sync-SpicetifyListSetting',
    'Test-SpicetifyCliInstalled','Restore-SpotifyIfSpicetifyPresent','Get-SpicetifyDiagnosticSnapshot','Reapply-SavedSpicetifySetup',
    'Get-NormalizedPathString','Get-PathEntries','Set-PathEntries','Add-PathEntry','Remove-PathEntry',
    'Module-NukeSpotify','Module-InstallSpotX','Module-InstallSpicetifyCLI',
    'Module-InstallThemes','Module-InstallExtensions',
    'Module-InstallMarketplace','Module-ApplySpicetify',
    'Build-SpotXParams','Load-LibreSpotConfig'
)

$issMain = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault()
$issMain.Assemblies.Add([System.Management.Automation.Runspaces.SessionStateAssemblyEntry]::new("System.IO.Compression.FileSystem"))
foreach ($fname in $functionNamesForWorker) {
    $cmd = Get-Command -Name $fname -CommandType Function -ErrorAction Stop
    $entry = New-Object System.Management.Automation.Runspaces.SessionStateFunctionEntry($cmd.Name, $cmd.Definition)
    $null = $issMain.Commands.Add($entry)
}

$varNamesForWorker = @(
    'URL_SPOTX','URL_MARKETPLACE','URL_THEMES_REPO','URL_SPICETIFY_FMT','PinnedReleases',
    'TEMP_DIR','SPOTIFY_EXE_PATH','SPICETIFY_DIR','SPICETIFY_CONFIG_DIR',
    'BACKUP_ROOT','CONFIG_DIR','CONFIG_PATH','LOG_PATH',
    'BrushGreen','BrushRed','BrushMuted','BrushError',
    'EasyDefaults','ThemeData','BuiltInExtensions','SpotXLyricsThemes','VERSION'
)
foreach ($vname in $varNamesForWorker) {
    $val = (Get-Variable -Name $vname -Scope Global -ErrorAction Stop).Value
    $varEntry = New-Object System.Management.Automation.Runspaces.SessionStateVariableEntry($vname, $val, "")
    $null = $issMain.Variables.Add($varEntry)
}
$script:WorkerInitialState = $issMain

# =============================================================================
# 18. JOB LAUNCHERS
# =============================================================================
function New-SyncHash {
    $wih = New-Object System.Windows.Interop.WindowInteropHelper($window)
    $script:activeSyncHash = [hashtable]::Synchronized(@{
        Dispatcher=$window.Dispatcher; LogBlock=$ui['LogOutput']; Scroller=$ui['LogScroller']
        StatusLabel=$ui['StatusText']; StepLabel=$ui['StepIndicator']; ProgressBar=$ui['MainProgress']
        InstallTitle=$ui['InstallTitle']; InstallContext=$ui['InstallContext']
        CloseBtn=$ui['CloseBtn']; BackBtn=$ui['BtnBackToConfig']; CopyLogBtn=$ui['BtnCopyLog']; Timer=$timer
        Window=$window; WindowHandle=$wih.Handle
        IsRunning=$true; AllowSpotify=$false; Errors=[System.Collections.Generic.List[string]]::new()
    })
    return $script:activeSyncHash
}

function Start-InstallJob { param($Config)
    Clear-CompletedRunspaceResources | Out-Null
    try {
        $script:installStartTime = Get-Date; $timer.Start()
        $syncHash = New-SyncHash
        $rsMain = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace($script:WorkerInitialState)
        $rsMain.ApartmentState = 'STA'; $rsMain.Open(); $script:openRunspaces.Add($rsMain)
        $psMain = [PowerShell]::Create(); $psMain.Runspace = $rsMain; $script:openRunspaces.Add($psMain)
        $psMain.Runspace.SessionStateProxy.SetVariable('syncHash', $syncHash)
        $null = $psMain.AddScript($installBlock.ToString()).AddArgument($syncHash).AddArgument($Config)
        $null = $psMain.BeginInvoke()
        $rsW = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace()
        $rsW.ApartmentState = 'STA'; $rsW.Open(); $script:openRunspaces.Add($rsW)
        $psW = [PowerShell]::Create(); $psW.Runspace = $rsW; $script:openRunspaces.Add($psW)
        $psW.Runspace.SessionStateProxy.SetVariable('syncHash', $syncHash)
        $null = $psW.AddScript($watcherBlock.ToString()).AddArgument($syncHash); $null = $psW.BeginInvoke()
    } catch {
        if ($script:activeSyncHash) { $script:activeSyncHash.IsRunning = $false }
        Clear-CompletedRunspaceResources | Out-Null
        throw
    }
}

function Start-MaintenanceJob { param([string]$Action)
    Clear-CompletedRunspaceResources | Out-Null
    try {
        $script:installStartTime = Get-Date; $timer.Start()
        $syncHash = New-SyncHash
        $rsMain = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace($script:WorkerInitialState)
        $rsMain.ApartmentState = 'STA'; $rsMain.Open(); $script:openRunspaces.Add($rsMain)
        $psMain = [PowerShell]::Create(); $psMain.Runspace = $rsMain; $script:openRunspaces.Add($psMain)
        $psMain.Runspace.SessionStateProxy.SetVariable('syncHash', $syncHash)
        $null = $psMain.AddScript($maintBlock.ToString()).AddArgument($syncHash).AddArgument($Action)
        $null = $psMain.BeginInvoke()
        $rsW = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace()
        $rsW.ApartmentState = 'STA'; $rsW.Open(); $script:openRunspaces.Add($rsW)
        $psW = [PowerShell]::Create(); $psW.Runspace = $rsW; $script:openRunspaces.Add($psW)
        $psW.Runspace.SessionStateProxy.SetVariable('syncHash', $syncHash)
        $null = $psW.AddScript($watcherBlock.ToString()).AddArgument($syncHash); $null = $psW.BeginInvoke()
    } catch {
        if ($script:activeSyncHash) { $script:activeSyncHash.IsRunning = $false }
        Clear-CompletedRunspaceResources | Out-Null
        throw
    }
}

# =============================================================================
# 19. LAUNCH
# =============================================================================
$null = $window.ShowDialog()
