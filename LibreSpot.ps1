# LibreSpot - Comprehensive SpotX + Spicetify Installer
# Recommended setup | Custom Mode | Maintenance Mode
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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    public const int DWMWCP_ROUND = 2;
    public const int DWMSBT_MAINWINDOW = 2;
    public const int DWMSBT_TRANSIENTWINDOW = 3;
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
    public static bool TryEnableMicaBackdrop(IntPtr hwnd) {
        try {
            int dark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            int backdrop = DWMSBT_MAINWINDOW;
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
            return hr == 0;
        } catch { return false; }
    }
}
public sealed class LibreSpotNativeOutputCollector {
    private readonly ConcurrentQueue<string> lines = new ConcurrentQueue<string>();
    private readonly DataReceivedEventHandler handler;

    public LibreSpotNativeOutputCollector() {
        handler = OnDataReceived;
    }

    public void Attach(Process process) {
        process.OutputDataReceived += handler;
        process.ErrorDataReceived += handler;
    }

    public void Detach(Process process) {
        process.OutputDataReceived -= handler;
        process.ErrorDataReceived -= handler;
    }

    public bool TryDequeue(out string line) {
        return lines.TryDequeue(out line);
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs eventArgs) {
        if (eventArgs != null && eventArgs.Data != null) {
            lines.Enqueue(eventArgs.Data);
        }
    }
}
'@ -ErrorAction SilentlyContinue

$ErrorActionPreference = 'Stop'
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch {}

$global:VERSION = '3.7.4'
$global:CONFIG_SCHEMA_VERSION = 1


# CLI argument detection. Supports `irm URL | iex -clean` (PowerShell passes
# trailing args to `iex` as $args inside the invoked script) and also
# `powershell.exe -File LibreSpot.ps1 -clean` via the same $args.
#
# Recognized flags:
#   -clean              Pre-tick Recommended setup + CleanInstall for a one-shot rebuild.
#   -watch              Headless auto-reapply check. No UI. Scheduled task uses this.
#   -installwatcher     Register the scheduled task that calls `-watch` and exit.
#   -uninstallwatcher   Remove that scheduled task and exit.
#   -removeselfdata     Unregister the watcher task and remove all LibreSpot-owned data, then exit.
$script:CliClean           = $false
$script:CliWatch           = $false
$script:CliInstallWatcher  = $false
$script:CliUninstallWatcher = $false
$script:CliRemoveSelfData  = $false
try {
    if ($args -and $args.Count -gt 0) {
        foreach ($a in $args) {
            switch -Regex ([string]$a) {
                '^-{1,2}clean$'              { $script:CliClean = $true }
                '^-{1,2}watch$'              { $script:CliWatch = $true }
                '^-{1,2}installwatcher$'     { $script:CliInstallWatcher = $true }
                '^-{1,2}uninstallwatcher$'   { $script:CliUninstallWatcher = $true }
                '^-{1,2}removeselfdata$'     { $script:CliRemoveSelfData = $true }
            }
        }
    }
} catch {}

# --- Pinned dependency versions with SHA256 verification ---
# Update these when new versions are tested. Use Maintenance > Check for Updates.
$global:PinnedReleases = @{
    SpotX = @{
        Version = '2.0'
        Commit  = '550bc72cd15f6e2a172a6ecc0873d0991eb1c83c'
        Url     = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/550bc72cd15f6e2a172a6ecc0873d0991eb1c83c/run.ps1'
        SHA256  = '863cd19429160c911ce7439426d9e2127064028ccabbaf3007b233a393607606'
    }
    SpicetifyCLI = @{
        Version = '2.44.0'
        WindowsMinSpotify = '1.2.14'
        WindowsMaxTestedSpotify = '1.2.93'
        CompatibilityUrl = 'https://github.com/spicetify/cli/releases/tag/v2.44.0'
        SHA256  = @{
            x64   = '215435095420e3804001a650c072f51befde897b414b0dac054edc2ea258ebea'
            arm64 = 'a6f827ae6387203bb87ff4af1f5ab21e4671a542ce1a0e3cb82ddc77d2ac7444'
        }
    }
    Marketplace = @{
        Version = '1.0.9'
        Url     = 'https://github.com/spicetify/marketplace/releases/download/v1.0.9/marketplace.zip'
        SHA256  = '2713054703c2365e391658a58c782dd2ebdd8d573f2015b5a2bab58b7eee8685'
    }
    Themes = @{
        Commit  = 'df033493a7dae30ca6e371de9cec1897871dbb0c'
        SHA256  = 'c837828c71d7a938898f87965b1fe9e5812cec831bd9cb1619bd8feb6020fdc3'
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
$global:SPICETIFY_INTEGRATION_VERSION = 'v2'
$global:BACKUP_ROOT            = "$env:USERPROFILE\LibreSpot_Backups"
$global:CONFIG_DIR             = "$env:APPDATA\LibreSpot"
$global:CONFIG_PATH            = "$env:APPDATA\LibreSpot\config.json"
$global:PROFILE_DIR            = "$env:APPDATA\LibreSpot\profiles"
$global:ACTIVE_PROFILE_PATH    = "$env:APPDATA\LibreSpot\active-profile.json"
$global:PREVIOUS_PROFILE_PATH  = "$env:APPDATA\LibreSpot\active-profile.previous.json"
$global:LOG_PATH               = "$env:APPDATA\LibreSpot\install.log"
$global:OPERATION_JOURNAL_PATH = "$env:APPDATA\LibreSpot\operation-journal.jsonl"
$global:RUN_RECEIPT_PATH       = "$env:APPDATA\LibreSpot\run-receipt.latest.json"
$global:OPERATION_JOURNAL_MAX_BYTES = 1048576
$global:OPERATION_JOURNAL_RETAIN_BYTES = 786432
$global:CURRENT_OPERATION_ID   = $null
$global:CURRENT_OPERATION_ACTION = $null
$global:CACHE_DIR              = "$env:APPDATA\LibreSpot\cache"
$global:WATCHER_STATE_PATH     = "$env:APPDATA\LibreSpot\watcher-state.json"
$global:WATCHER_LOG_PATH       = "$env:APPDATA\LibreSpot\watcher.log"
$global:WATCHER_TASK_NAME      = 'LibreSpot\ReapplyWatcher'

$global:BrushGreen = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FF22C55E"))
$global:BrushRed   = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFEF4444"))
$global:BrushMuted = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFA6B0BB"))
$global:BrushError = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFF87171"))
foreach ($b in @($global:BrushGreen,$global:BrushRed,$global:BrushMuted,$global:BrushError)) { $b.Freeze() }

$script:openRunspaces = [System.Collections.Generic.List[object]]::new()
$script:BrushConverter = [System.Windows.Media.BrushConverter]::new()
$script:EntryInvocation = $MyInvocation
$script:EntryCommandPath = $PSCommandPath
# PS2EXE leaves $PSScriptRoot empty. Compute a reliable script root for both
# .ps1 (where $PSScriptRoot works) and .exe (where we use the process path).
$script:ScriptRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot
} elseif (-not [string]::IsNullOrWhiteSpace($PSCommandPath)) {
    Split-Path -Parent $PSCommandPath
} else {
    try { Split-Path -Parent ([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName) } catch { $PWD.Path }
}
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
    $tempPath = $null
    $backupPath = $null
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        # Merge over the existing file so fields written by the WPF backend
        # lane (LastAppliedSpotifyVersion, LastSuccessfulApplyAt, ...) survive
        # a save from this lane. Both lanes share the same watcher-state.json.
        $merged = @{}
        if (Test-Path -LiteralPath $global:WATCHER_STATE_PATH) {
            try {
                $existing = Get-Content -LiteralPath $global:WATCHER_STATE_PATH -Raw -ErrorAction Stop | ConvertFrom-Json
                foreach ($prop in $existing.PSObject.Properties) { $merged[$prop.Name] = $prop.Value }
            } catch {}
        }
        foreach ($key in @($State.Keys)) { $merged[$key] = $State[$key] }
        # Use [UTF8Encoding]($false) to avoid the BOM that PS 5.1's
        # `-Encoding UTF8` produces, which can trip up ConvertFrom-Json.
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        $json = $merged | ConvertTo-Json -Compress
        $tempPath = Join-Path $global:CONFIG_DIR ("watcher-state.{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
        $backupPath = Join-Path $global:CONFIG_DIR ("watcher-state.{0}.bak" -f [Guid]::NewGuid().ToString('N'))
        [System.IO.File]::WriteAllText($tempPath, $json, $utf8NoBom)
        if (Test-Path -LiteralPath $global:WATCHER_STATE_PATH) {
            try {
                [System.IO.File]::Replace($tempPath, $global:WATCHER_STATE_PATH, $backupPath, $true)
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            } catch {
                $rescuePath = "$($global:WATCHER_STATE_PATH).rescue"
                Move-Item -LiteralPath $global:WATCHER_STATE_PATH -Destination $rescuePath -Force
                try {
                    [System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)
                    Remove-Item -LiteralPath $rescuePath -Force -ErrorAction SilentlyContinue
                } catch {
                    Move-Item -LiteralPath $rescuePath -Destination $global:WATCHER_STATE_PATH -Force -ErrorAction SilentlyContinue
                    throw
                }
            }
        } else {
            [System.IO.File]::Move($tempPath, $global:WATCHER_STATE_PATH)
        }
    } catch {
        if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
        if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
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

function Test-SpotifySessionStability {
    param([int]$WaitSeconds = 20)
    if (-not (Test-Path -LiteralPath $global:SPOTIFY_EXE_PATH)) { return $true }
    try {
        $procs = @(Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)
        if ($procs.Count -eq 0) { return $true }
        $initialPid = $procs[0].Id
        Start-Sleep -Seconds $WaitSeconds
        $afterProcs = @(Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)
        if ($afterProcs.Count -eq 0) {
            Write-Log "Spotify exited within ${WaitSeconds}s of patched launch. This may indicate server-side enforcement. If Spotify keeps closing after patching, use Maintenance > Restore vanilla or Full reset before retrying." -Level 'WARN'
            return $false
        }
        $afterPids = @($afterProcs | ForEach-Object { $_.Id })
        if ($afterPids -notcontains $initialPid) {
            Write-Log "Spotify restarted within ${WaitSeconds}s of patched launch (initial PID $initialPid was replaced). This may indicate server-side enforcement or a self-repair restart. If Spotify keeps restarting after patching, use Maintenance > Restore vanilla or Full reset before retrying." -Level 'WARN'
            return $false
        }
        return $true
    } catch { return $true }
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
        return @{ Command = $entry; Arguments = '-Watch'; Entry = $entry }
    }
    if ($ext -eq '.ps1') {
        $ps = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if (-not (Test-Path -LiteralPath $ps)) { $ps = 'powershell.exe' }
        return @{ Command = $ps; Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$entry`" -Watch"; Entry = $entry }
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
    [CmdletBinding(SupportsShouldProcess)]
    param()
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
    $escapedArguments = [System.Security.SecurityElement]::Escape($launch.Arguments)
    # Use the current user's SID for domain-joined machines where bare USERNAME
    # may not resolve.  Fall back to USERDOMAIN\USERNAME, then bare USERNAME.
    $userId = $null
    try {
        $currentIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
        $userId = $currentIdentity.User.Value   # SID string, e.g. S-1-5-21-...
    } catch {}
    if ([string]::IsNullOrWhiteSpace($userId)) {
        $userId = if ($env:USERDOMAIN -and $env:USERDOMAIN -ne $env:COMPUTERNAME) {
            "$env:USERDOMAIN\$env:USERNAME"
        } else { $env:USERNAME }
    }
    $userId = [System.Security.SecurityElement]::Escape($userId)
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
      <Arguments>$escapedArguments</Arguments>
    </Exec>
  </Actions>
</Task>
"@

    $xmlPath = Join-Path $global:CONFIG_DIR "watcher-task.xml"
    if ($PSCmdlet.ShouldProcess($global:WATCHER_TASK_NAME, 'Register scheduled task')) {
        Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Unregister the scheduled task to undo.'
        try {
            if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
                New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
            }
            # schtasks /Create /XML requires UTF-16 LE with BOM to match the XML header.
            [System.IO.File]::WriteAllText($xmlPath, $xml, [System.Text.Encoding]::Unicode)

            $output = & schtasks.exe /Create /TN $global:WATCHER_TASK_NAME /XML $xmlPath /F 2>&1
            $ok = ($LASTEXITCODE -eq 0)
            if ($ok) {
                Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Registered' -WouldChange $true -Reversible $true -RollbackHint 'Unregister the scheduled task to undo.'
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
    return $false
}

function Unregister-AutoReapplyTask {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if ($PSCmdlet.ShouldProcess($global:WATCHER_TASK_NAME, 'Remove scheduled task')) {
        Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Re-register the scheduled task to undo.'
        try {
            $null = & schtasks.exe /Delete /TN $global:WATCHER_TASK_NAME /F 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-OperationJournalEntry -Phase 'task' -Target $global:WATCHER_TASK_NAME -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $true -RollbackHint 'Re-register the scheduled task to undo.'
                Write-WatcherLog "Unregister: scheduled task removed"
                return $true
            }
            return $false
        } catch { return $false }
    }
    return $false
}

function Invoke-HeadlessReapply {
    # Minimal reapply pipeline — runs SpotX synchronously with the saved config
    # and reapplies Spicetify if the CLI is present. Intentionally does NOT use
    # any UI / runspace plumbing. Caller runs on the main thread from -Watch.
    param([hashtable]$Config)
    if (-not $Config) { throw 'Invoke-HeadlessReapply: missing config' }

    $tempDir = Join-Path $global:TEMP_DIR ("LibreSpot_Watcher_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    $customPatchesPath = ''
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    try {
        $spotxRun = Join-Path $tempDir 'spotx_run.ps1'

        # Download + hash-verify SpotX through the same guarded downloader as
        # user-triggered install/reapply so CVE and network diagnostics stay consistent.
        $expectedHash = [string]$global:PinnedReleases.SpotX.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $spotxRun -Label 'SpotX run.ps1 (watcher)')) {
            $downloadFailed = $false
            try {
                Write-WatcherLog "Downloading SpotX run.ps1"
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $spotxRun
            } catch {
                $downloadFailed = $true
                if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $spotxRun -Label 'SpotX run.ps1 (watcher)') {
                    Write-WatcherLog 'Network download failed; using verified cached copy.' -Level 'WARN'
                    $downloadFailed = $false
                } else { throw }
            }
            if (-not $downloadFailed) {
                $actualHash = Get-FileSha256Lower -Path $spotxRun
                if ($actualHash -ne $expectedHash.ToLowerInvariant()) {
                    throw "SpotX hash mismatch. Expected $expectedHash, got $actualHash. Refusing to run."
                }
                Save-ToAssetCache -SourcePath $spotxRun -SHA256Hash $expectedHash -Label 'SpotX run.ps1 (watcher)' -SourceUrl $global:URL_SPOTX
            }
        }

        $spotxArgs = Build-SpotXParams -Config $Config
        $customPatchesPath = New-SpotXCustomPatchesFile -Config $Config
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            $spotxArgs = "$spotxArgs -CustomPatchesPath `"$customPatchesPath`""
            Write-WatcherLog "Custom SpotX patches staged at $customPatchesPath"
        }
        Write-WatcherLog "Invoking SpotX with: $spotxArgs"

        # Use powershell.exe isolation so SpotX can't leak runtime state into our
        # own script scope. Exit code is the only signal we care about.
        $psExe = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if (-not (Test-Path -LiteralPath $psExe)) { $psExe = 'powershell.exe' }
        $spotxGuard = $null
        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = $psExe
        $pinfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$spotxRun`" $spotxArgs"
        $pinfo.RedirectStandardOutput = $true
        $pinfo.RedirectStandardError  = $true
        $pinfo.UseShellExecute = $false
        $pinfo.CreateNoWindow = $true
        try {
            $spotxGuard = Open-VerifiedScriptForExecution -FilePath $spotxRun -ExpectedHash $expectedHash -Label 'SpotX run.ps1 (watcher)'
            $proc = [System.Diagnostics.Process]::Start($pinfo)
            # Drain stdout/stderr asynchronously to prevent buffer deadlock.
            # If SpotX writes more than the OS pipe buffer (~4KB) the process
            # hangs forever waiting for the buffer to be read.
            $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
            $stderrTask = $proc.StandardError.ReadToEndAsync()
            if (-not $proc.WaitForExit(20 * 60 * 1000)) {
                try { $proc.Kill() } catch {}
                throw "SpotX timed out after 20 minutes."
            }
            $proc.WaitForExit()  # Ensure async streams are fully flushed
            if ($proc.ExitCode -ne 0) {
                $stderrText = if ($stderrTask.IsCompleted) { $stderrTask.Result } else { '(not available)' }
                throw "SpotX exited with code $($proc.ExitCode). Stderr: $stderrText"
            }
        } finally {
            if ($spotxGuard) { try { $spotxGuard.Dispose() } catch {} }
        }
        Write-WatcherLog "SpotX completed successfully" -Level 'SUCCESS'

        # Reapply Spicetify when it's installed. Missing CLI is fine — it just
        # means the user only patches with SpotX and that part is already done.
        if (Test-SpicetifyCliInstalled) {
            try {
                Invoke-SpicetifyCli -Arguments @('backup','apply','--bypass-admin') -FailureMessage 'Watcher Spicetify apply failed.'
                Write-WatcherLog "Spicetify reapplied" -Level 'SUCCESS'
            } catch {
                Write-WatcherLog "Spicetify apply failed: $($_.Exception.Message)" -Level 'WARN'
            }
        }
    } finally {
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            Remove-Item -LiteralPath $customPatchesPath -Force -ErrorAction SilentlyContinue
        }
        try { Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue } catch {}
    }
}

function Invoke-AutoReapplyWatcher {
    # -Watch entry point. Returns an exit code to satisfy schtasks reporting.
    Write-WatcherLog "--- Watcher tick ---"

    $currentVersion = Get-InstalledSpotifyVersion
    if (-not $currentVersion) {
        Write-WatcherLog "Spotify not installed - skipping."
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
        Write-WatcherLog "Spotify still at $currentVersion - nothing to do"
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'UpToDate' }
        return 0
    }

    Write-WatcherLog "Spotify version bump: $($state.LastKnownVersion) -> $currentVersion" -Level 'STEP'

    if (Test-SpotifyRunning) {
        Write-WatcherLog "Spotify is running - deferring reapply to next tick"
        Set-WatcherState -State @{ LastKnownVersion = $state.LastKnownVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'DeferredSpotifyRunning' }
        return 0
    }

    $saved = $null
    try { $saved = Load-LibreSpotConfig } catch { Write-WatcherLog "Config load failed: $($_.Exception.Message)" -Level 'ERROR' }
    if (-not $saved) {
        Write-WatcherLog "No saved LibreSpot config - cannot reapply automatically" -Level 'WARN'
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'NoConfig' }
        return 0
    }
    $saved = Normalize-LibreSpotConfig -Config $saved

    if (-not (ConvertTo-ConfigBoolean -Value $saved['AutoReapply_Enabled'] -Default $false)) {
        Write-WatcherLog 'Auto-reapply preference is off; skipping.'
        Set-WatcherState -State @{ LastKnownVersion = $currentVersion; LastRunAt = (Get-Date -Format 'o'); LastOutcome = 'PreferenceOff' }
        return 0
    }

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

if ($script:CliRemoveSelfData) {
    Write-Host "Removing all LibreSpot-owned data..."
    $removed = @()
    $null = Unregister-AutoReapplyTask
    $removed += "Watcher scheduled task"
    $selfPaths = @(
        @{ Path = $global:CONFIG_DIR;    Label = 'Config directory (%APPDATA%\LibreSpot)' }
        @{ Path = $global:BACKUP_ROOT;   Label = 'Backup directory (%USERPROFILE%\LibreSpot_Backups)' }
        @{ Path = (Join-Path $env:LOCALAPPDATA 'LibreSpot'); Label = 'Log/crash directory (%LOCALAPPDATA%\LibreSpot)' }
    )
    foreach ($entry in $selfPaths) {
        if (Test-Path -LiteralPath $entry.Path) {
            try {
                Remove-Item -LiteralPath $entry.Path -Recurse -Force -ErrorAction Stop
                $removed += $entry.Label
                Write-Host "  Removed: $($entry.Label)"
            } catch {
                Write-Warning "  Failed to remove $($entry.Label): $($_.Exception.Message)"
            }
        } else {
            Write-Host "  Not found: $($entry.Label)"
        }
    }
    Write-Host ""
    Write-Host "LibreSpot self-cleanup complete. Removed $($removed.Count) item(s):"
    foreach ($r in $removed) { Write-Host "  - $r" }
    Write-Host ""
    Write-Host "Spotify and Spicetify are NOT affected. Use Maintenance > Full Reset to remove those."
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
            $bootstrapDir = Join-Path $env:TEMP ("LibreSpot-Elevate-{0}" -f [guid]::NewGuid().ToString('N'))
            if (-not (Test-Path -LiteralPath $bootstrapDir)) {
                New-Item -Path $bootstrapDir -ItemType Directory -Force | Out-Null
            }
            $payloadPath = Join-Path $bootstrapDir ("LibreSpot-elevation-payload-{0}.ps1" -f [guid]::NewGuid().ToString('N'))
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($payloadPath, $inlineSource, $utf8NoBom)
            $payloadStream = [System.IO.File]::OpenRead($payloadPath)
            $payloadSha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $payloadHash = (($payloadSha.ComputeHash($payloadStream) | ForEach-Object { $_.ToString('x2') }) -join '')
            } finally {
                $payloadStream.Dispose()
                $payloadSha.Dispose()
            }
            return @{ Kind = 'InlinePayload'; Path = $payloadPath; Hash = $payloadHash; IsTemp = $true }
        } catch {}
    }

    return $null
}

function Show-BootstrapNotice {
    param(
        [string]$Title = 'LibreSpot',
        [Parameter(Mandatory = $true)][string]$Message
    )

    try {
        $surfaceBase = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#FF0B0E14'))
        $surfacePanel = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#FF151A24'))
        $borderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#FF2A3442'))
        $primaryText = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#FFE7EDF3'))
        $secondaryText = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#FFA6B0BB'))
        $accentBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#FF22D3EE'))
        $buttonText = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString('#FF05131A'))
        foreach ($brush in @($surfaceBase, $surfacePanel, $borderBrush, $primaryText, $secondaryText, $accentBrush, $buttonText)) {
            $brush.Freeze()
        }

        $dialog = [System.Windows.Window]::new()
        $dialog.Title = $Title
        $dialog.Width = 460
        $dialog.SizeToContent = [System.Windows.SizeToContent]::Height
        $dialog.ResizeMode = [System.Windows.ResizeMode]::NoResize
        $dialog.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterScreen
        $dialog.Background = $surfaceBase
        $dialog.Foreground = $primaryText
        $dialog.Topmost = $true
        $dialog.ShowInTaskbar = $true
        try {
            $iconPath = Join-Path $script:ScriptRoot 'icon.ico'
            if (Test-Path -LiteralPath $iconPath -PathType Leaf) {
                $dialog.Icon = [System.Windows.Media.Imaging.BitmapFrame]::Create([Uri]::new($iconPath))
            }
        } catch {}
        $dialog.Add_SourceInitialized({
            try {
                $hwnd = [System.Windows.Interop.WindowInteropHelper]::new($dialog).Handle
                [Win32]::TryEnableMicaBackdrop($hwnd) | Out-Null
            } catch {}
        })

        $shell = [System.Windows.Controls.Border]::new()
        $shell.Background = $surfacePanel
        $shell.BorderBrush = $borderBrush
        $shell.BorderThickness = [System.Windows.Thickness]::new(1)
        $shell.CornerRadius = [System.Windows.CornerRadius]::new(8)
        $shell.Padding = [System.Windows.Thickness]::new(22)
        $dialog.Content = $shell

        $panel = [System.Windows.Controls.StackPanel]::new()
        $shell.Child = $panel

        $heading = [System.Windows.Controls.TextBlock]::new()
        $heading.Text = $Title
        $heading.FontFamily = [System.Windows.Media.FontFamily]::new('Segoe UI Variable Display, Segoe UI')
        $heading.FontSize = 18
        $heading.FontWeight = [System.Windows.FontWeights]::SemiBold
        $heading.Foreground = $primaryText
        $heading.Margin = [System.Windows.Thickness]::new(0, 0, 0, 10)
        $panel.Children.Add($heading) | Out-Null

        $body = [System.Windows.Controls.TextBlock]::new()
        $body.Text = $Message
        $body.FontFamily = [System.Windows.Media.FontFamily]::new('Segoe UI Variable, Segoe UI')
        $body.FontSize = 13
        $body.LineHeight = 20
        $body.TextWrapping = [System.Windows.TextWrapping]::Wrap
        $body.Foreground = $secondaryText
        $panel.Children.Add($body) | Out-Null

        $button = [System.Windows.Controls.Button]::new()
        $button.Content = 'Close'
        $button.MinWidth = 108
        $button.MinHeight = 36
        $button.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
        $button.Margin = [System.Windows.Thickness]::new(0, 22, 0, 0)
        $button.Padding = [System.Windows.Thickness]::new(18, 6, 18, 6)
        $button.Background = $accentBrush
        $button.Foreground = $buttonText
        $button.BorderThickness = [System.Windows.Thickness]::new(0)
        $button.FontWeight = [System.Windows.FontWeights]::SemiBold
        $button.Add_Click({
            try { $dialog.DialogResult = $true } catch {}
            $dialog.Close()
        })
        $panel.Children.Add($button) | Out-Null

        $dialog.ShowDialog() | Out-Null
    } catch {
        Write-Warning $Message
    }
}

# The -watch scheduled task runs unattended at LeastPrivilege every 30 minutes.
# It must NEVER pass through the self-elevation gate: forwarding it via RunAs
# pops a UAC prompt on every tick (or a modal bootstrap dialog on failure)
# from a supposedly headless task. SpotX patches the per-user Spotify install,
# so the watcher does not need elevation — the WPF backend watcher lane runs
# non-elevated the same way.
if (-not $script:CliWatch -and -not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $launchTarget = Get-SelfElevationLaunchTarget
    if ($launchTarget) {
        try {
            $workingDir = Split-Path -Path $launchTarget.Path -Parent
            $elevationArgs = @()
            if ($script:CliClean) { $elevationArgs += '-clean' }
            if ($script:CliInstallWatcher) { $elevationArgs += '-installwatcher' }
            if ($script:CliUninstallWatcher) { $elevationArgs += '-uninstallwatcher' }
            if ($launchTarget.Kind -eq 'Exe') {
                $startArgs = @{
                    FilePath = $launchTarget.Path
                    Verb = 'RunAs'
                    WorkingDirectory = $workingDir
                }
                if ($elevationArgs.Count -gt 0) { $startArgs.ArgumentList = $elevationArgs }
                Start-Process @startArgs
            } elseif ($launchTarget.Kind -eq 'InlinePayload') {
                $literalPayloadPath = "'" + ([string]$launchTarget.Path).Replace("'", "''") + "'"
                $literalPayloadHash = "'" + ([string]$launchTarget.Hash).Replace("'", "''") + "'"
                $literalArgs = @($elevationArgs | ForEach-Object { "'" + ([string]$_).Replace("'", "''") + "'" })
                $forwardedArgsExpression = if ($literalArgs.Count -gt 0) { "@($($literalArgs -join ', '))" } else { '@()' }
                $bootstrapCommand = @"
`$ErrorActionPreference = 'Stop'
`$payloadPath = $literalPayloadPath
`$expectedHash = $literalPayloadHash
`$forwardedArgs = $forwardedArgsExpression
`$payloadStream = [System.IO.File]::OpenRead(`$payloadPath)
`$payloadSha = [System.Security.Cryptography.SHA256]::Create()
try {
    `$actualHash = ((`$payloadSha.ComputeHash(`$payloadStream) | ForEach-Object { `$_.ToString('x2') }) -join '')
} finally {
    `$payloadStream.Dispose()
    `$payloadSha.Dispose()
}
if (`$actualHash -ne `$expectedHash.ToLowerInvariant()) { throw 'LibreSpot elevation payload hash mismatch. Refusing to run.' }
`$payload = [System.IO.File]::ReadAllText(`$payloadPath, [System.Text.Encoding]::UTF8)
try {
    & ([scriptblock]::Create(`$payload)) @forwardedArgs
} finally {
    Remove-Item -LiteralPath `$payloadPath -Force -ErrorAction SilentlyContinue
    `$payloadDir = Split-Path -Path `$payloadPath -Parent
    if (`$payloadDir -and (Test-Path -LiteralPath `$payloadDir -PathType Container) -and -not (Get-ChildItem -LiteralPath `$payloadDir -Force -ErrorAction SilentlyContinue)) {
        Remove-Item -LiteralPath `$payloadDir -Force -ErrorAction SilentlyContinue
    }
}
"@
                $encodedBootstrap = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($bootstrapCommand))
                $scriptArgs = @(
                    '-NoProfile',
                    '-ExecutionPolicy', 'Bypass',
                    '-EncodedCommand', $encodedBootstrap
                )
                Start-Process -FilePath 'powershell.exe' -ArgumentList $scriptArgs -Verb RunAs -WorkingDirectory $workingDir
            } else {
                $scriptArgs = @(
                    '-NoProfile',
                    '-ExecutionPolicy', 'Bypass',
                    '-File', $launchTarget.Path
                ) + $elevationArgs
                Start-Process -FilePath 'powershell.exe' -ArgumentList $scriptArgs -Verb RunAs -WorkingDirectory $workingDir
            }
        } catch {
            Show-BootstrapNotice -Title 'LibreSpot' -Message "LibreSpot needs administrator permission to modify Spotify.`n`nApprove the Windows prompt to continue. If it was dismissed, launch LibreSpot again."
        }
    } else {
        Show-BootstrapNotice -Title 'LibreSpot' -Message "LibreSpot could not determine a reusable launch path for self-elevation.`n`nRun the saved LibreSpot.ps1 file or download the latest release and try again."
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
    # --- Community themes (downloaded from individual GitHub repos) ---
    "Catppuccin"  = @{ Schemes = @("mocha","macchiato","frappe","latte")
                       Preview = @{ _default="https://raw.githubusercontent.com/catppuccin/spicetify/main/assets/mocha.webp"; "latte"="https://raw.githubusercontent.com/catppuccin/spicetify/main/assets/latte.webp"; "frappe"="https://raw.githubusercontent.com/catppuccin/spicetify/main/assets/frappe.webp"; "macchiato"="https://raw.githubusercontent.com/catppuccin/spicetify/main/assets/macchiato.webp"; "mocha"="https://raw.githubusercontent.com/catppuccin/spicetify/main/assets/mocha.webp" } }
    "Comfy"       = @{ Schemes = @("Comfy","Mono","Chromatic")
                       Preview = @{ _default="https://raw.githubusercontent.com/Comfy-Themes/Spicetify/main/screenshots/Comfy.png"; "Mono"="https://raw.githubusercontent.com/Comfy-Themes/Spicetify/main/screenshots/Mono.png"; "Chromatic"="https://raw.githubusercontent.com/Comfy-Themes/Spicetify/main/screenshots/Chromatic.png" } }
    "Bloom"       = @{ Schemes = @("dark","light","darkMono","darkGreen","coffee","comfy","violet")
                       Preview = @{ _default="https://raw.githubusercontent.com/nimsandu/spicetify-bloom/main/screenshots/dark.png"; "light"="https://raw.githubusercontent.com/nimsandu/spicetify-bloom/main/screenshots/light.png" } }
    "Lucid"       = @{ Schemes = @("dark","light","dark-green","coffee","comfy","dark-fluent","greenland","biscuit","macos","rosepine","dracula","dracula-pro")
                       Preview = @{ _default="https://raw.githubusercontent.com/sanoojes/Spicetify-Lucid/main/screenshots/dark.webp"; "light"="https://raw.githubusercontent.com/sanoojes/Spicetify-Lucid/main/screenshots/light.webp" } }
    "Hazy"        = @{ Schemes = @("dark","light")
                       Preview = @{ _default="https://raw.githubusercontent.com/Astromations/Hazy/main/screenshots/dark.png" } }
}

# Community themes are hosted in individual GitHub repos, not the official
# spicetify-themes collection.  Each entry maps a theme name (matching a key
# in $ThemeData above) to its repo owner/name and the subfolder inside the
# archive that contains color.ini + user.css.  Module-InstallThemes checks
# this table to decide whether to pull from the official themes archive or
# from the community repo.
$global:CommunityThemeRepos = @{
    "Catppuccin" = @{ Owner="catppuccin"; Repo="spicetify"; CommitSha="1ec645c4cf7f42f9792b9eeb1bb7930f94593277"; SHA256="59432d5dfba871f288331e72ca5eb9ae48783e94d96cc3835a2992b3df71ed65"; ThemeFolder="." }
    "Comfy"      = @{ Owner="Comfy-Themes"; Repo="Spicetify"; CommitSha="32ff101e27cfd33d85b7cc587f7f95db6b2df8b0"; SHA256="d82afe89be0a58c7c2d83a85a0dfa24b473d48d4f63241178e37c94c1fd1e7c6"; ThemeFolder="." }
    "Bloom"      = @{ Owner="nimsandu"; Repo="spicetify-bloom"; CommitSha="654cfed682b94613b0029997ffafc1eadccc5bef"; SHA256="12cb8678f7226b2a014a10fdef8ea462e0ac0a866f84b2de48050004fcd50a70"; ThemeFolder="." }
    "Lucid"      = @{ Owner="sanoojes"; Repo="Spicetify-Lucid"; CommitSha="5c28e9f955d5ca84a82d06084cc6652e5655ea2d"; SHA256="af3f1ed718b3deda7c52ebf7e0ca4bf7c07f03f212a88dd0534c2ebe81803bf8"; ThemeFolder="." }
    "Hazy"       = @{ Owner="Astromations"; Repo="Hazy"; CommitSha="1926d9db3e0313b68ca6e2193c2b278e733ac3c4"; SHA256="372938c3fea3cbac7850afeb6b66b15673236e248436a7afaacb2ab1d814c4bf"; ThemeFolder="." }
}

# Themes that require inject_theme_js = 1 (they ship a theme.js file).
$global:ThemesNeedingJS = @("Dribbblish","StarryNight","Turntable","Catppuccin","Comfy","Bloom","Lucid","Hazy")

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

# Community extensions are downloaded from GitHub repos to the Spicetify
# Extensions folder before being registered. Each entry maps the registered
# filename to a commit-pinned raw URL and hash. These are NOT bundled with the
# Spicetify CLI — LibreSpot fetches them on demand.
$global:CommunityExtensions = [ordered]@{
    "hidePodcasts.js"   = @{
        Description = "Remove podcast, episode, and audiobook UI elements from the Spotify interface"
        Url         = "https://raw.githubusercontent.com/theRealPadster/spicetify-hide-podcasts/b89365dd86fba24d610fae65d882d7e14a69f2fa/hidePodcasts.js"
        Source      = "theRealPadster/spicetify-hide-podcasts"
        SHA256      = "727e5a2f9137f4be77eac83d234a0ce858c5d618e7ff56116a6def01793fc3f8"
    }
    "beautiful-lyrics.mjs" = @{
        Description = "Immersive synced lyrics with dynamic backgrounds, romanization, and blur effects"
        Url         = "https://raw.githubusercontent.com/surfbryce/beautiful-lyrics/61ac582da092311e893423269ca7f09003108705/Extension/Builds/Release/beautiful-lyrics.mjs"
        Source      = "surfbryce/beautiful-lyrics"
        SHA256      = "93c9ecfcb0a83c832c5ee7ca8fe826bcfaeec7cdd129c0bf05bab84b8ba6ba72"
    }
    "playlist-icons.js"  = @{
        Description = "Add custom icons and folder images to your playlists and library"
        Url         = "https://raw.githubusercontent.com/jeroentvb/spicetify-playlist-icons/8f401f923a5c25f530935faaceb39089a25b701a/playlist-icons.js"
        Source      = "jeroentvb/spicetify-playlist-icons"
        SHA256      = "79bbe2bd6a52a521a382a73ef1c8c7ff0b0b9bd7674c48bb0ed44c5d2c944c8d"
    }
    "volumePercentage.js" = @{
        Description = "Display the exact volume percentage next to the volume slider"
        Url         = "https://raw.githubusercontent.com/daksh2k/spicetify-stuff/89e609d933946a888cdff9cc3d7c4f1e9b88cfde/Extensions/volumePercentage.js"
        Source      = "daksh2k/spicetify-stuff"
        SHA256      = "b88dcde894f4998abc4473773333015c09f0450ec563d256ed5af45db7129aca"
    }
    "adblock.js" = @{
        Description = "Spicetify-layer ad blocking - a fallback for when SpotX patching fails on a newer Spotify build. Not a SpotX replacement."
        Url         = "https://raw.githubusercontent.com/rxri/spicetify-extensions/60554c512739c6f2084879efe9d8a88f1dd16646/adblock/adblock.js"
        Source      = "rxri/spicetify-extensions"
        SHA256      = "fb6dc4dfc09ee369638ffaf47a9f36202bb99c1555edc79772d7fbb235114623"
    }
}
$global:CommunityExtensionAliases = @{
    "beautifulLyrics.js" = "beautiful-lyrics.mjs"
    "playlistIcons.js" = "playlist-icons.js"
}
$global:DeprecatedCommunityExtensionNames = @("beautifulLyrics.js", "playlistIcons.js", "songStats.js")
$global:CommunityCustomApps = [ordered]@{
    "stats" = @{
        DisplayName = "Stats"
        Description = "Detailed listening statistics with top tracks, artists, genres, library charts, and optional Last.fm-backed views."
        Url         = "https://github.com/harbassan/spicetify-apps/releases/download/stats-v1.1.3/spicetify-stats.release.zip"
        Source      = "harbassan/spicetify-apps"
        Version     = "1.1.3"
        ReleaseTag  = "stats-v1.1.3"
        AssetPath   = "stats"
        SHA256      = "c5611ff8caafe9c673ed43de07fbae77296d42fbd14fab868e9cbeac5d2b6cb7"
    }
}

$global:EasyDefaults = @{
    UiCulture="en"
    SpotX_NewTheme=$true; SpotX_PodcastsOff=$true; SpotX_BlockUpdate=$true; SpotX_AdSectionsOff=$true
    SpotX_Premium=$false; SpotX_LyricsEnabled=$true; SpotX_LyricsTheme="spotify"
    SpotX_TopSearch=$false; SpotX_RightSidebarOff=$false; SpotX_RightSidebarClr=$false
    SpotX_CanvasHomeOff=$false; SpotX_HomeSubOff=$false; SpotX_DisableStartup=$true; SpotX_NoShortcut=$false; SpotX_CacheLimit=0
    SpotX_Plus=$false; SpotX_NewFullscreen=$false; SpotX_FunnyProgress=$false; SpotX_ExpSpotify=$false; SpotX_LyricsBlock=$false
    SpotX_SendVersionOff=$true; SpotX_StartSpoti=$false
    SpotX_DevTools=$false; SpotX_Mirror=$false; SpotX_DownloadMethod=""; SpotX_ConfirmUninstall=$false
    SpotX_SpotifyVersionId="auto"
    SpotX_Language=""
    SpotX_CustomPatchesEnabled=$false; SpotX_CustomPatchesJson=""
    Spicetify_Theme="(None - Marketplace Only)"; Spicetify_Scheme="Default"; Spicetify_Marketplace=$true
    Spicetify_Extensions=@("fullAppDisplay.js","shuffle+.js","trashbin.js")
    Spicetify_CustomApps=@()
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
    @{ Id='1.2.93';          Label='1.2.93 (current pinned)';          Version='1.2.93';                  Notes='Best match for our pinned SpotX commit and Spicetify CLI support range.' }
    @{ Id='1.2.92';          Label='1.2.92 (previous fallback)';       Version='1.2.92';                  Notes='Prior pinned build kept for rollback and comparison.' }
    @{ Id='1.2.90.451';      Label='1.2.90.451 (older fallback)';      Version='1.2.90.451.gb094aab0';    Notes='Older pinned build kept for rollback and comparison.' }
    @{ Id='1.2.85.519';      Label='1.2.85.519 (older stable)';        Version='1.2.85.519.g7c42e2e8';    Notes='Last Windows release before Canvas-home changes.' }
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

function Get-LibreSpotConfigSchemaVersion {
    param([hashtable]$Config)
    if (-not $Config -or -not $Config.ContainsKey('ConfigSchemaVersion')) { return 0 }
    return (ConvertTo-ConfigInt -Value $Config.ConfigSchemaVersion -Default 0 -Minimum 0 -Maximum ([int]::MaxValue))
}

function Assert-LibreSpotConfigSchemaSupported {
    param([hashtable]$Config)
    $schemaVersion = Get-LibreSpotConfigSchemaVersion -Config $Config
    if ($schemaVersion -gt $global:CONFIG_SCHEMA_VERSION) {
        throw "Saved config schema version $schemaVersion is newer than this LibreSpot build supports ($global:CONFIG_SCHEMA_VERSION)."
    }
    return $schemaVersion
}

function Normalize-LibreSpotConfig {
    param([hashtable]$Config)

    $null = Assert-LibreSpotConfigSchemaSupported -Config $Config

    $normalized = @{
        ConfigSchemaVersion = $global:CONFIG_SCHEMA_VERSION
        Mode = 'Easy'
    }
    foreach ($key in $global:EasyDefaults.Keys) {
        $defaultValue = $global:EasyDefaults[$key]
        if ($defaultValue -is [System.Collections.IEnumerable] -and $defaultValue -isnot [string]) {
            $normalized[$key] = @($defaultValue)
        } else {
            $normalized[$key] = $defaultValue
        }
    }

    # RiskAcknowledged is a UX-only flag not part of EasyDefaults; default false.
    if (-not $normalized.ContainsKey('RiskAcknowledged')) { $normalized['RiskAcknowledged'] = $false }

    if ($Config -and $Config.ContainsKey('Mode')) {
        $mode = [string]$Config.Mode
        if ($mode -in @('Easy', 'Custom')) { $normalized.Mode = $mode }
    }

    $uiCulture = if ($Config -and $Config.ContainsKey('UiCulture')) { [string]$Config.UiCulture } else { [string]$normalized.UiCulture }
    $allowedUiCultures = @('en','ru','zh-Hans','pt-BR','es')
    $normalized.UiCulture = if ($allowedUiCultures -contains $uiCulture) { $uiCulture } else { 'en' }

    $booleanKeys = @(
        'CleanInstall','LaunchAfter',
        'SpotX_NewTheme','SpotX_PodcastsOff','SpotX_BlockUpdate','SpotX_AdSectionsOff',
        'SpotX_Premium','SpotX_LyricsEnabled','SpotX_TopSearch','SpotX_RightSidebarOff',
        'SpotX_RightSidebarClr','SpotX_CanvasHomeOff','SpotX_HomeSubOff',
        'SpotX_DisableStartup','SpotX_NoShortcut','SpotX_OldLyrics','SpotX_HideColIconOff',
        'SpotX_Plus','SpotX_NewFullscreen','SpotX_FunnyProgress','SpotX_ExpSpotify','SpotX_LyricsBlock',
        'SpotX_SendVersionOff','SpotX_StartSpoti','SpotX_DevTools','SpotX_Mirror','SpotX_ConfirmUninstall',
        'SpotX_CustomPatchesEnabled','Spicetify_Marketplace','AutoReapply_Enabled','RiskAcknowledged'
    )
    foreach ($key in $booleanKeys) {
        if ($Config -and $Config.ContainsKey($key)) {
            $normalized[$key] = ConvertTo-ConfigBoolean -Value $Config[$key] -Default ([bool]$normalized[$key])
        }
    }

    if ($Config -and $Config.ContainsKey('SpotX_CacheLimit')) {
        $normalized.SpotX_CacheLimit = ConvertTo-ConfigInt -Value $Config.SpotX_CacheLimit -Default ([int]$normalized.SpotX_CacheLimit) -Minimum 0 -Maximum 50000
    }

    if ($Config -and $Config.ContainsKey('SpotX_CustomPatchesJson')) {
        $patchJson = [string]$Config.SpotX_CustomPatchesJson
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        if ($utf8.GetByteCount($patchJson) -le 65536) {
            $normalized.SpotX_CustomPatchesJson = $patchJson.Trim()
        }
    }

    $dm = if ($Config -and $Config.ContainsKey('SpotX_DownloadMethod')) { [string]$Config.SpotX_DownloadMethod } else { [string]$normalized.SpotX_DownloadMethod }
    $dm = $dm.Trim().ToLowerInvariant()
    if ($dm -notin @('','curl','webclient')) { $dm = '' }
    $normalized.SpotX_DownloadMethod = $dm

    $lang = if ($Config -and $Config.ContainsKey('SpotX_Language')) { [string]$Config.SpotX_Language } else { [string]$normalized.SpotX_Language }
    $allowedLanguages = @('en','ru','de','fr','es','pt','pt-BR','it','nl','pl','sv','no','da','fi','ja','ko','zh-CN','zh-TW','ar','tr','cs','hu','ro','uk','id','th','vi')
    $normalized.SpotX_Language = if ($allowedLanguages -contains $lang) { $lang } else { '' }

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
        if ($global:CommunityExtensionAliases.ContainsKey($name)) { $name = [string]$global:CommunityExtensionAliases[$name] }
        if (-not $global:BuiltInExtensions.Contains($name) -and -not $global:CommunityExtensions.Contains($name)) { continue }
        if (-not $extensions.Contains($name)) { $extensions.Add($name) }
    }
    $normalized.Spicetify_Extensions = @($extensions)

    $customApps = [System.Collections.Generic.List[string]]::new()
    $rawCustomApps = @()
    if ($Config -and $Config.ContainsKey('Spicetify_CustomApps')) {
        if ($Config.Spicetify_CustomApps -is [string]) {
            $rawCustomApps = @([string]$Config.Spicetify_CustomApps)
        } elseif ($Config.Spicetify_CustomApps -is [System.Collections.IEnumerable]) {
            $rawCustomApps = @($Config.Spicetify_CustomApps)
        }
    }
    foreach ($customApp in $rawCustomApps) {
        $name = [string]$customApp
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if (-not $global:CommunityCustomApps.Contains($name)) { continue }
        if (-not $customApps.Contains($name)) { $customApps.Add($name) }
    }
    $normalized.Spicetify_CustomApps = @($customApps)

    if ($normalized.SpotX_RightSidebarOff) {
        $normalized.SpotX_RightSidebarClr = $false
    }

    if (-not $normalized.SpotX_LyricsEnabled) {
        $normalized.SpotX_OldLyrics = $false
        $normalized.SpotX_LyricsBlock = $false
    } elseif ($normalized.SpotX_LyricsBlock) {
        $normalized.SpotX_OldLyrics = $false
    }

    if ($Config -and -not $Config.ContainsKey('Mode')) {
        foreach ($key in $global:EasyDefaults.Keys) {
            if ($key -eq 'UiCulture') { continue }
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
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Reason)
    $reasonSuffix = if ([string]::IsNullOrWhiteSpace($Reason)) { '' } else { " Reason: $Reason" }
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $global:CONFIG_PATH) {
            $stamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
            $quarantinePath = $null
            for ($attempt = 0; $attempt -lt 10; $attempt++) {
                $suffix = if ($attempt -eq 0) { '' } else { "-$attempt" }
                $candidateName = "config.corrupt.$stamp$suffix.json"
                $candidatePath = Join-Path $global:CONFIG_DIR $candidateName
                if (-not (Test-Path -LiteralPath $candidatePath)) {
                    $quarantinePath = $candidatePath
                    break
                }
            }
            if (-not $quarantinePath) {
                $quarantinePath = Join-Path $global:CONFIG_DIR ("config.corrupt.{0}.json" -f [Guid]::NewGuid().ToString('N'))
            }

            if ($PSCmdlet.ShouldProcess($global:CONFIG_PATH, 'Quarantine corrupted config')) {
                # Journal writes are best-effort here: this runs during startup
                # config load, BEFORE Write-OperationJournalEntry is defined.
                # A CommandNotFound must not abort the quarantine move, or the
                # corrupt file stays put and every launch repeats the reset.
                try { Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore the quarantined file manually.' } catch {}
                Move-Item -LiteralPath $global:CONFIG_PATH -Destination $quarantinePath -ErrorAction Stop
                try { Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Quarantined' -WouldChange $true -Reversible $true -RollbackHint 'Restore the quarantined file manually.' } catch {}
                $quarantineName = Split-Path -Path $quarantinePath -Leaf
                $script:ConfigLoadWarning = "LibreSpot reset the saved settings because the config file could not be read safely.$reasonSuffix The previous file was moved to $quarantineName."
            }
        } else {
            $script:ConfigLoadWarning = "LibreSpot reset the saved settings because the config file could not be read safely.$reasonSuffix"
        }
    } catch {
        $script:ConfigLoadWarning = 'LibreSpot reset the saved settings because the config file could not be read safely, but it could not move the original file aside automatically.'
    }
    try {
        if ($Reason) { Write-Log "Config reset: $Reason" -Level 'WARN' }
    } catch {}
}

function Save-LibreSpotConfig {
    [CmdletBinding(SupportsShouldProcess)]
    param([hashtable]$Config)
    if (-not $PSCmdlet.ShouldProcess($global:CONFIG_PATH, 'Save configuration')) {
        return $true
    }
    Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
    $tempPath = $null
    $backupPath = $null
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }
        $tempPath = Join-Path $global:CONFIG_DIR ("config.{0}.tmp" -f [Guid]::NewGuid().ToString('N'))
        $backupPath = Join-Path $global:CONFIG_DIR ("config.{0}.bak" -f [Guid]::NewGuid().ToString('N'))
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
                $rescuePath = "$($global:CONFIG_PATH).rescue"
                Move-Item -LiteralPath $global:CONFIG_PATH -Destination $rescuePath -Force
                try {
                    [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
                    Remove-Item -LiteralPath $rescuePath -Force -ErrorAction SilentlyContinue
                } catch {
                    Move-Item -LiteralPath $rescuePath -Destination $global:CONFIG_PATH -Force -ErrorAction SilentlyContinue
                    throw
                }
            }
        } else {
            [System.IO.File]::Move($tempPath, $global:CONFIG_PATH)
        }
        Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Saved' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
        return $true
    } catch {
        Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $true -RollbackHint 'Restore from the most recent config backup.'
        try { Write-Log "Config save failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
        if ($tempPath) { Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue }
        if ($backupPath) { Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue }
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

function ConvertTo-LibreSpotProfileId {
    param([string]$Name)
    $text = if ([string]::IsNullOrWhiteSpace($Name)) { 'profile' } else { $Name.Trim().ToLowerInvariant() }
    $chars = foreach ($ch in $text.ToCharArray()) {
        if ([char]::IsLetterOrDigit($ch)) { $ch } else { '-' }
    }
    $slug = (($chars -join '') -split '-' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join '-'
    if ([string]::IsNullOrWhiteSpace($slug)) { return 'profile' }
    return $slug
}

function New-LibreSpotProfileDocument {
    param(
        [string]$Id,
        [string]$Name,
        [string]$Description,
        [hashtable]$Configuration
    )
    $normalized = Normalize-LibreSpotConfig -Config $Configuration
    $normalized.RiskAcknowledged = $false
    return [ordered]@{
        SchemaVersion = 1
        Id            = $Id
        Name          = $Name
        Description   = $Description
        CreatedAt     = (Get-Date).ToUniversalTime().ToString('o')
        UpdatedAt     = (Get-Date).ToUniversalTime().ToString('o')
        Configuration = $normalized
    }
}

function Get-LibreSpotBuiltInProfiles {
    $recommended = Normalize-LibreSpotConfig -Config @{ Mode = 'Easy' }

    $minimal = Normalize-LibreSpotConfig -Config $recommended
    $minimal.Mode = 'Custom'
    $minimal.Spicetify_Theme = '(None - Marketplace Only)'
    $minimal.Spicetify_Extensions = @()

    $visual = Normalize-LibreSpotConfig -Config $recommended
    $visual.Mode = 'Custom'
    $visual.Spicetify_Theme = 'Dribbblish'
    $visual.Spicetify_Scheme = 'catppuccin-mocha'
    $visual.Spicetify_Extensions = @('fullAppDisplay.js','shuffle+.js')

    $lyrics = Normalize-LibreSpotConfig -Config $recommended
    $lyrics.Mode = 'Custom'
    $lyrics.SpotX_LyricsEnabled = $true
    $lyrics.SpotX_LyricsTheme = 'lavender'
    $lyrics.Spicetify_Extensions = @('beautiful-lyrics.mjs','popupLyrics.js')

    $premium = Normalize-LibreSpotConfig -Config $recommended
    $premium.Mode = 'Custom'
    $premium.SpotX_Premium = $true
    $premium.SpotX_PodcastsOff = $false
    $premium.SpotX_AdSectionsOff = $true

    $recovery = Normalize-LibreSpotConfig -Config $recommended
    $recovery.Mode = 'Custom'
    $recovery.CleanInstall = $false
    $recovery.LaunchAfter = $false
    $recovery.AutoReapply_Enabled = $true

    return @(
        [ordered]@{ Id='recommended'; Name='Recommended'; Description='Opinionated defaults for first installs.'; IsBuiltIn=$true; Configuration=$recommended }
        [ordered]@{ Id='minimal-marketplace'; Name='Minimal / Marketplace-only'; Description='Marketplace with no bundled theme or extension choices.'; IsBuiltIn=$true; Configuration=$minimal }
        [ordered]@{ Id='visual-theme'; Name='Visual Theme'; Description='A visual setup with Dribbblish and useful interface extensions.'; IsBuiltIn=$true; Configuration=$visual }
        [ordered]@{ Id='lyrics-focus'; Name='Lyrics Focus'; Description='Lyrics-focused SpotX and Spicetify settings.'; IsBuiltIn=$true; Configuration=$lyrics }
        [ordered]@{ Id='premium-account'; Name='Premium Account'; Description='Keeps premium-account UI expectations calmer while blocking ad sections.'; IsBuiltIn=$true; Configuration=$premium }
        [ordered]@{ Id='recovery-reapply'; Name='Recovery / Reapply'; Description='Conservative settings for reapply and watcher recovery runs.'; IsBuiltIn=$true; Configuration=$recovery }
    )
}

function Read-LibreSpotProfilePointer {
    param([string]$Path)
    try {
        if (-not (Test-Path -LiteralPath $Path)) { return $null }
        $json = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
        return [string]$json.ProfileId
    } catch { return $null }
}

function Write-LibreSpotProfilePointer {
    param([string]$Path, [string]$ProfileId)
    if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }
    $document = [ordered]@{
        SchemaVersion = 1
        ProfileId     = $ProfileId
        UpdatedAt     = (Get-Date).ToUniversalTime().ToString('o')
    }
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, ($document | ConvertTo-Json -Depth 4), $utf8)
}

function Initialize-LibreSpotProfileStore {
    if (-not (Test-Path -LiteralPath $global:PROFILE_DIR)) { New-Item -Path $global:PROFILE_DIR -ItemType Directory -Force | Out-Null }
    if (Test-Path -LiteralPath $global:ACTIVE_PROFILE_PATH) { return }

    $activeId = 'recommended'
    $currentConfig = $null
    try { $currentConfig = Load-LibreSpotConfig } catch { $currentConfig = $null }
    if ($currentConfig) {
        $activeId = 'current'
        $profilePath = Join-Path $global:PROFILE_DIR 'current.json'
        if (Test-Path -LiteralPath $profilePath) {
            $activeId = "current-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
            $profilePath = Join-Path $global:PROFILE_DIR "$activeId.json"
        }
        $document = New-LibreSpotProfileDocument -Id $activeId -Name 'Current' -Description 'Migrated from the existing config.json.' -Configuration $currentConfig
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($profilePath, ($document | ConvertTo-Json -Depth 8), $utf8)
    }

    Write-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH -ProfileId $activeId
}

function Get-LibreSpotProfiles {
    Initialize-LibreSpotProfileStore
    $activeId = Read-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH
    $profiles = @()
    foreach ($profileEntry in Get-LibreSpotBuiltInProfiles) {
        $profileEntry.IsActive = ([string]$profileEntry.Id -eq [string]$activeId)
        $profiles += $profileEntry
    }

    if (Test-Path -LiteralPath $global:PROFILE_DIR) {
        foreach ($path in Get-ChildItem -LiteralPath $global:PROFILE_DIR -Filter '*.json' -File -ErrorAction SilentlyContinue) {
            try {
                $json = Get-Content -LiteralPath $path.FullName -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
                $cfg = ConvertTo-PlainHashtable -InputObject $json.Configuration
                $profiles += [ordered]@{
                    Id            = [string]$json.Id
                    Name          = [string]$json.Name
                    Description   = [string]$json.Description
                    IsBuiltIn     = $false
                    IsActive      = ([string]$json.Id -eq [string]$activeId)
                    Configuration = (Normalize-LibreSpotConfig -Config $cfg)
                }
            } catch {}
        }
    }

    return $profiles
}

function Get-LibreSpotProfileById {
    param([string]$Id)
    foreach ($profileEntry in Get-LibreSpotProfiles) {
        if ([string]$profileEntry.Id -eq [string]$Id) { return $profileEntry }
    }
    return $null
}

function Save-LibreSpotLocalProfile {
    param([string]$Name, [string]$Description, [hashtable]$Configuration)
    Initialize-LibreSpotProfileStore
    $normalizedName = if ([string]::IsNullOrWhiteSpace($Name)) { 'Custom profile' } else { $Name.Trim() }
    $baseName = $normalizedName
    $idBase = ConvertTo-LibreSpotProfileId -Name $baseName
    $id = $idBase
    $suffix = 2
    while ((Get-LibreSpotProfiles | Where-Object { [string]$_.Id -eq $id -or [string]$_.Name -eq $normalizedName } | Select-Object -First 1)) {
        $normalizedName = "$baseName $suffix"
        $id = "$idBase-$suffix"
        $suffix++
    }
    $document = New-LibreSpotProfileDocument -Id $id -Name $normalizedName -Description $Description -Configuration $Configuration
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $global:PROFILE_DIR "$id.json"), ($document | ConvertTo-Json -Depth 8), $utf8)
    return $document
}

function Apply-LibreSpotProfile {
    param([string]$Id)
    $profileEntry = Get-LibreSpotProfileById -Id $Id
    if (-not $profileEntry) { throw "Profile '$Id' was not found." }
    $previousId = Read-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH
    if (-not [string]::IsNullOrWhiteSpace($previousId)) {
        Write-LibreSpotProfilePointer -Path $global:PREVIOUS_PROFILE_PATH -ProfileId $previousId
    }
    $saved = Save-LibreSpotConfig -Config $profileEntry.Configuration
    if (-not $saved) { throw "LibreSpot could not write config.json for profile '$($profileEntry.Name)'." }
    Write-LibreSpotProfilePointer -Path $global:ACTIVE_PROFILE_PATH -ProfileId $profileEntry.Id
    return $profileEntry
}

function Get-LibreSpotTempRoot {
    $root = Join-Path $global:TEMP_DIR 'LibreSpot'
    if (Test-Path -LiteralPath $root -PathType Leaf) {
        $root = Join-Path $global:TEMP_DIR ("LibreSpot-{0}" -f [System.Diagnostics.Process]::GetCurrentProcess().Id)
    }

    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        New-Item -Path $root -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    return $root
}

function New-LibreSpotTempFile {
    param([string]$Name)

    $fileName = if ([string]::IsNullOrWhiteSpace($Name)) { 'artifact.tmp' } else { $Name }
    return (Join-Path (Get-LibreSpotTempRoot) ("{0}-{1}" -f [Guid]::NewGuid().ToString('N'), $fileName))
}

function New-SpotXCustomPatchesFile {
    param([hashtable]$Config)

    if (-not $Config -or -not $Config.ContainsKey('SpotX_CustomPatchesEnabled')) { return '' }
    if (-not [bool]$Config.SpotX_CustomPatchesEnabled) { return '' }

    $patchJson = if ($Config.ContainsKey('SpotX_CustomPatchesJson')) { [string]$Config.SpotX_CustomPatchesJson } else { '' }
    if ([string]::IsNullOrWhiteSpace($patchJson)) {
        throw 'Custom SpotX patches are enabled, but SpotX_CustomPatchesJson is empty.'
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $byteCount = $utf8.GetByteCount($patchJson)
    if ($byteCount -gt 65536) {
        throw "Custom SpotX patches are $byteCount bytes; the maximum is 65536 bytes."
    }

    try {
        $null = $patchJson | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "Custom SpotX patches JSON is invalid: $($_.Exception.Message)"
    }

    $patchPath = New-LibreSpotTempFile -Name 'spotx-custom-patches.json'
    $patchDir = Split-Path -Path $patchPath -Parent
    if (-not (Test-Path -LiteralPath $patchDir)) {
        New-Item -ItemType Directory -Path $patchDir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($patchPath, $patchJson, $utf8)
    return $patchPath
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
        xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
        Title="LibreSpot" MinHeight="720" MinWidth="1120" MaxWidth="1520"
        WindowStyle="None" ResizeMode="CanResize" AllowsTransparency="False"
        Background="#FF0B0E14" WindowStartupLocation="CenterScreen"
        FontFamily="Segoe UI Variable Display, Segoe UI Variable, Segoe UI, sans-serif" UseLayoutRounding="True" SnapsToDevicePixels="True"
        TextOptions.TextFormattingMode="Display" TextOptions.TextRenderingMode="ClearType"
        AutomationProperties.Name="LibreSpot — SpotX + Spicetify Installer">
    <shell:WindowChrome.WindowChrome>
        <shell:WindowChrome CaptionHeight="0" GlassFrameThickness="0" ResizeBorderThickness="6" UseAeroCaptionButtons="False" CornerRadius="0"/>
    </shell:WindowChrome.WindowChrome>
    <Window.Resources>
        <!-- ============================================================
             DESIGN TOKENS  (v3.7.0 premium UI overhaul)
             Surfaces ascend from base to overlay. Accents and semantic
             tones are aligned to the brushes used by Set-MaintenanceCardTone,
             Set-SelectionSnapshotState, and Set-InstallStageState in PS,
             so changing them here updates both static and dynamic chrome.
             ============================================================ -->
        <Color x:Key="SurfaceBaseColor">#FF0B0E14</Color>
        <Color x:Key="SurfaceElevatedColor">#FF111722</Color>
        <Color x:Key="SurfaceElevated2Color">#FF182030</Color>
        <Color x:Key="SurfaceOverlayColor">#FF1F2937</Color>
        <Color x:Key="SurfaceSidebarColor">#FF0A0D13</Color>
        <Color x:Key="BorderSubtleColor">#FF1F2A37</Color>
        <Color x:Key="BorderStrongColor">#FF2D3A47</Color>
        <Color x:Key="BorderHoverColor">#FF3A4654</Color>
        <Color x:Key="AccentColor">#FF22C55E</Color>
        <Color x:Key="AccentHoverColor">#FF34D376</Color>
        <Color x:Key="AccentPressedColor">#FF16A34A</Color>
        <Color x:Key="AccentSoftBgColor">#FF111A22</Color>
        <Color x:Key="AccentSoftBorderColor">#FF2D5A3F</Color>
        <Color x:Key="AccentMutedColor">#FF86EFAC</Color>
        <Color x:Key="InfoColor">#FF93C5FD</Color>
        <Color x:Key="InfoSoftBgColor">#FF111C2A</Color>
        <Color x:Key="InfoSoftBorderColor">#FF2E4964</Color>
        <Color x:Key="WarningColor">#FFFCD34D</Color>
        <Color x:Key="WarningSoftBgColor">#FF211A0E</Color>
        <Color x:Key="WarningSoftBorderColor">#FF6B4E16</Color>
        <Color x:Key="DangerColor">#FFEF4444</Color>
        <Color x:Key="DangerSoftBgColor">#FF2B1117</Color>
        <Color x:Key="FgPrimaryColor">#FFE7EDF3</Color>
        <Color x:Key="FgSecondaryColor">#FFA6B0BB</Color>
        <Color x:Key="FgMutedColor">#FF778390</Color>
        <Color x:Key="FgInverseColor">#FF04130A</Color>
        <Color x:Key="ShimmerHighlightColor">#3322C55E</Color>

        <SolidColorBrush x:Key="SurfaceBaseBrush" Color="{StaticResource SurfaceBaseColor}"/>
        <SolidColorBrush x:Key="SurfaceElevatedBrush" Color="{StaticResource SurfaceElevatedColor}"/>
        <SolidColorBrush x:Key="SurfaceElevated2Brush" Color="{StaticResource SurfaceElevated2Color}"/>
        <SolidColorBrush x:Key="SurfaceOverlayBrush" Color="{StaticResource SurfaceOverlayColor}"/>
        <SolidColorBrush x:Key="SurfaceSidebarBrush" Color="{StaticResource SurfaceSidebarColor}"/>
        <SolidColorBrush x:Key="BorderSubtleBrush" Color="{StaticResource BorderSubtleColor}"/>
        <SolidColorBrush x:Key="BorderStrongBrush" Color="{StaticResource BorderStrongColor}"/>
        <SolidColorBrush x:Key="BorderHoverBrush" Color="{StaticResource BorderHoverColor}"/>
        <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}"/>
        <SolidColorBrush x:Key="AccentHoverBrush" Color="{StaticResource AccentHoverColor}"/>
        <SolidColorBrush x:Key="AccentPressedBrush" Color="{StaticResource AccentPressedColor}"/>
        <SolidColorBrush x:Key="AccentSoftBgBrush" Color="{StaticResource AccentSoftBgColor}"/>
        <SolidColorBrush x:Key="AccentSoftBorderBrush" Color="{StaticResource AccentSoftBorderColor}"/>
        <SolidColorBrush x:Key="AccentMutedBrush" Color="{StaticResource AccentMutedColor}"/>
        <SolidColorBrush x:Key="InfoBrush" Color="{StaticResource InfoColor}"/>
        <SolidColorBrush x:Key="WarningBrush" Color="{StaticResource WarningColor}"/>
        <SolidColorBrush x:Key="DangerBrush" Color="{StaticResource DangerColor}"/>
        <SolidColorBrush x:Key="FgPrimaryBrush" Color="{StaticResource FgPrimaryColor}"/>
        <SolidColorBrush x:Key="FgSecondaryBrush" Color="{StaticResource FgSecondaryColor}"/>
        <SolidColorBrush x:Key="FgMutedBrush" Color="{StaticResource FgMutedColor}"/>
        <SolidColorBrush x:Key="FgInverseBrush" Color="{StaticResource FgInverseColor}"/>

        <!-- Inverted-direction shimmer brush for the install progress bar -->
        <LinearGradientBrush x:Key="ShimmerOverlayBrush" StartPoint="0,0.5" EndPoint="1,0.5">
            <GradientStop Color="#0022C55E" Offset="0.0"/>
            <GradientStop Color="{StaticResource ShimmerHighlightColor}" Offset="0.45"/>
            <GradientStop Color="#88FFFFFF" Offset="0.5"/>
            <GradientStop Color="{StaticResource ShimmerHighlightColor}" Offset="0.55"/>
            <GradientStop Color="#0022C55E" Offset="1.0"/>
        </LinearGradientBrush>

        <!-- Type tokens -->
        <Style x:Key="TypeHeroH1" TargetType="TextBlock">
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/>
            <Setter Property="FontSize" Value="32"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="LineHeight" Value="38"/>
            <Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="TypeH1" TargetType="TextBlock">
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/>
            <Setter Property="FontSize" Value="22"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="TypeH2" TargetType="TextBlock">
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/>
            <Setter Property="FontSize" Value="15.5"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="TypeBody" TargetType="TextBlock">
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="LineHeight" Value="20"/>
            <Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="TypeCaption" TargetType="TextBlock">
            <Setter Property="Foreground" Value="{StaticResource FgMutedBrush}"/>
            <Setter Property="FontSize" Value="11.5"/>
            <Setter Property="TextWrapping" Value="Wrap"/>
        </Style>

        <!-- Lucide icon paths shared across nav and content -->
        <Geometry x:Key="IconHome">M3 9.75 12 2l9 7.75V20a2 2 0 0 1-2 2h-4v-7h-6v7H5a2 2 0 0 1-2-2V9.75z</Geometry>
        <Geometry x:Key="IconSliders">M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6</Geometry>
        <Geometry x:Key="IconWrench">M14.7 6.3a4 4 0 0 1 5.4 5.4L13 19a3 3 0 0 1-4.2 0l-4-4A3 3 0 0 1 5 11l8-8 4 4-2.3 2.3a1.5 1.5 0 0 0 0 2.1l1.5 1.5a1.5 1.5 0 0 0 2.1 0L20.1 11.7</Geometry>
        <Geometry x:Key="IconShield">M12 2 4 5v6c0 5.5 3.8 10.7 8 12 4.2-1.3 8-6.5 8-12V5l-8-3z</Geometry>
        <Geometry x:Key="IconSparkle">M12 3l1.6 4.6 4.6 1.6-4.6 1.6L12 15.4l-1.6-4.6L5.8 9.2l4.6-1.6L12 3z</Geometry>
        <Geometry x:Key="IconCheck">M5 12.5l4.5 4.5L20 6.5</Geometry>
        <Geometry x:Key="IconDownload">M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3</Geometry>
        <Geometry x:Key="IconClock">M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z M12 6v6l4 2</Geometry>
        <Geometry x:Key="IconExternal">M14 3h7v7 M21 3l-9 9 M19 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h6</Geometry>
        <Geometry x:Key="IconDot">M12 12m-3 0a3 3 0 1 0 6 0 3 3 0 1 0-6 0</Geometry>
        <Geometry x:Key="IconRefresh">M3 12a9 9 0 0 1 14.5-7L21 8 M21 4v4h-4 M21 12a9 9 0 0 1-14.5 7L3 16 M3 20v-4h4</Geometry>

        <!-- Rounded ProgressBar with shimmer -->
        <ControlTemplate x:Key="RoundProgress" TargetType="ProgressBar">
            <Grid>
                <Border x:Name="PART_Track" CornerRadius="4" Background="{TemplateBinding Background}" Height="8"/>
                <Border x:Name="PART_Indicator" CornerRadius="4" HorizontalAlignment="Left" Height="8" Background="{TemplateBinding Foreground}" ClipToBounds="True">
                    <Border.Effect><DropShadowEffect BlurRadius="14" ShadowDepth="0" Opacity="0.55" Color="{StaticResource AccentColor}"/></Border.Effect>
                </Border>
                <Border x:Name="ShimmerHost" CornerRadius="4" HorizontalAlignment="Left" Height="8" IsHitTestVisible="False" ClipToBounds="True" Width="{Binding ActualWidth, ElementName=PART_Indicator}">
                    <Border Width="120" HorizontalAlignment="Left" Background="{StaticResource ShimmerOverlayBrush}">
                        <Border.RenderTransform><TranslateTransform x:Name="ShimmerXform" X="-140"/></Border.RenderTransform>
                        <Border.Triggers>
                            <EventTrigger RoutedEvent="Border.Loaded">
                                <BeginStoryboard>
                                    <Storyboard RepeatBehavior="Forever">
                                        <DoubleAnimation Storyboard.TargetName="ShimmerXform" Storyboard.TargetProperty="X" From="-140" To="900" Duration="0:0:1.6"/>
                                    </Storyboard>
                                </BeginStoryboard>
                            </EventTrigger>
                        </Border.Triggers>
                    </Border>
                </Border>
            </Grid>
        </ControlTemplate>
        <!-- ComboBox Toggle -->
        <ControlTemplate x:Key="DarkComboBoxToggle" TargetType="ToggleButton">
            <Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="34"/></Grid.ColumnDefinitions>
                <Border x:Name="Border" Grid.ColumnSpan="2" CornerRadius="8" Background="#FF111821" BorderBrush="#FF2D3A47" BorderThickness="1"/>
                <Border Grid.Column="0" CornerRadius="10,0,0,10" Background="Transparent"/>
                <Path Grid.Column="1" Fill="#FFA6B0BB" HorizontalAlignment="Center" VerticalAlignment="Center" Data="M 0 0 L 4 4 L 8 0 Z"/>
            </Grid>
            <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Border" Property="BorderBrush" Value="#FF465564"/></Trigger>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="Border" Property="BorderBrush" Value="#FF16A34A"/></Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>
        <!-- ComboBox -->
        <Style x:Key="DarkComboBox" TargetType="ComboBox">
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/><Setter Property="Background" Value="#FF111821"/><Setter Property="Height" Value="32"/><Setter Property="FontSize" Value="12.5"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid>
                <ToggleButton Template="{StaticResource DarkComboBoxToggle}" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}" Focusable="False" ClickMode="Press"/>
                <ContentPresenter IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" Margin="12,0,34,0" VerticalAlignment="Center" HorizontalAlignment="Left"/>
                <Popup IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" Focusable="False" AllowsTransparency="True">
                    <Border Background="#FF111821" BorderBrush="#FF2D3A47" BorderThickness="1" CornerRadius="8" MaxHeight="320" Margin="0,8,0,0">
                        <Border.Effect><DropShadowEffect BlurRadius="24" ShadowDepth="2" Opacity="0.45" Direction="270" Color="#05070A"/></Border.Effect>
                        <ScrollViewer><StackPanel IsItemsHost="True"/></ScrollViewer></Border>
                </Popup>
            </Grid></ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- ComboBox Item -->
        <Style x:Key="DarkComboBoxItem" TargetType="ComboBoxItem">
            <Setter Property="Foreground" Value="#FFE7EDF3"/><Setter Property="Background" Value="Transparent"/><Setter Property="Padding" Value="12,8"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem">
                <Border x:Name="Bd" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}" CornerRadius="6" Margin="4,2">
                    <ContentPresenter/></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Background" Value="#FF17202A"/></Trigger>
                    <Trigger Property="IsSelected" Value="True"><Setter TargetName="Bd" Property="Background" Value="#FF111A22"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- CheckBox -->
        <Style x:Key="DarkCheckBox" TargetType="CheckBox">
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/><Setter Property="FontSize" Value="12.5"/><Setter Property="Margin" Value="0,5,0,0"/><Setter Property="Cursor" Value="Hand"/><Setter Property="MinHeight" Value="22"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal">
                <Border x:Name="box" Width="18" Height="18" CornerRadius="5" Background="#FF111821" BorderBrush="#FF2D3A47" BorderThickness="1.5" Margin="0,1,10,0">
                    <Path x:Name="check" Data="M 3.5 9 L 7 13 L 13.5 4.5" Stroke="#FF22C55E" StrokeThickness="2" Visibility="Collapsed" Margin="0.5,0.5,0,0"/></Border>
                <ContentPresenter VerticalAlignment="Center"/>
            </StackPanel><ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="check" Property="Visibility" Value="Visible"/><Setter TargetName="box" Property="Background" Value="#FF111A22"/><Setter TargetName="box" Property="BorderBrush" Value="#FF22C55E"/></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="box" Property="BorderBrush" Value="#FF5F6D7B"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="box" Property="BorderBrush" Value="#FF22C55E"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.52"/></Trigger>
            </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- TextBox -->
        <Style x:Key="DarkTextBox" TargetType="TextBox">
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/><Setter Property="Background" Value="#FF111821"/><Setter Property="BorderBrush" Value="#FF2D3A47"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontSize" Value="12.5"/><Setter Property="Padding" Value="10,5"/><Setter Property="Height" Value="32"/><Setter Property="VerticalContentAlignment" Value="Center"/><Setter Property="CaretBrush" Value="{StaticResource AccentBrush}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border x:Name="Bd" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8"><ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="BorderBrush" Value="#FF465564"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Bd" Property="BorderBrush" Value="#FF22C55E"/></Trigger><Trigger Property="IsEnabled" Value="False"><Setter TargetName="Bd" Property="Opacity" Value="0.45"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Action Button (with hover-lift micro-interaction) -->
        <Style x:Key="ActionButton" TargetType="Button">
            <Setter Property="Height" Value="40"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="FontSize" Value="12.5"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/><Setter Property="BorderThickness" Value="1"/><Setter Property="BorderBrush" Value="{StaticResource BorderStrongBrush}"/>
            <Setter Property="RenderTransformOrigin" Value="0.5,0.5"/>
            <Setter Property="RenderTransform"><Setter.Value><TranslateTransform Y="0"/></Setter.Value></Setter>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="8" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="24,0">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="border" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                        <Setter TargetName="border" Property="Effect"><Setter.Value><DropShadowEffect BlurRadius="14" ShadowDepth="0" Opacity="0.4" Color="{StaticResource AccentColor}"/></Setter.Value></Setter>
                    </Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True">
                        <Setter TargetName="border" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                        <Setter TargetName="border" Property="Effect"><Setter.Value><DropShadowEffect BlurRadius="14" ShadowDepth="0" Opacity="0.55" Color="{StaticResource AccentColor}"/></Setter.Value></Setter>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True"><Setter TargetName="border" Property="Opacity" Value="0.84"/></Trigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter TargetName="border" Property="Opacity" Value="0.38"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Trigger.EnterActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.Y)" To="-1.5" Duration="0:0:0.12"/></Storyboard></BeginStoryboard></Trigger.EnterActions>
                    <Trigger.ExitActions><BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.Y)" To="0" Duration="0:0:0.12"/></Storyboard></BeginStoryboard></Trigger.ExitActions>
                </Trigger>
            </Style.Triggers>
        </Style>
        <Style x:Key="SecondaryActionButton" TargetType="Button" BasedOn="{StaticResource ActionButton}">
            <Setter Property="Background" Value="#FF111821"/>
            <Setter Property="BorderBrush" Value="#FF2D3A47"/>
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/>
        </Style>
        <!-- Sidebar Nav Item (RadioButton) -->
        <Style x:Key="ModeRadio" TargetType="RadioButton">
            <Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/>
            <Setter Property="FontSize" Value="13"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
            <Setter Property="Margin" Value="0,2,0,2"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton">
                <Border x:Name="bd" Background="Transparent" CornerRadius="10" BorderBrush="Transparent" BorderThickness="1" Padding="14,12">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="3"/>
                            <ColumnDefinition Width="14"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <Rectangle x:Name="accent" Grid.Column="0" Width="3" RadiusX="1.5" RadiusY="1.5" Fill="{StaticResource AccentBrush}" Opacity="0" HorizontalAlignment="Left" Margin="-14,4,0,4"/>
                        <ContentPresenter Grid.Column="2" VerticalAlignment="Center"/>
                    </Grid>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="bd" Property="Background" Value="{StaticResource SurfaceElevatedBrush}"/>
                    </Trigger>
                    <Trigger Property="IsChecked" Value="True">
                        <Setter TargetName="bd" Property="Background" Value="{StaticResource SurfaceElevated2Brush}"/>
                        <Setter TargetName="bd" Property="BorderBrush" Value="{StaticResource AccentSoftBorderBrush}"/>
                        <Setter TargetName="accent" Property="Opacity" Value="1"/>
                    </Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True">
                        <Setter TargetName="bd" Property="BorderBrush" Value="{StaticResource AccentBrush}"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Maintenance Button -->
        <Style x:Key="MaintButton" TargetType="Button">
            <Setter Property="MinHeight" Value="58"/><Setter Property="Background" Value="#FF111821"/><Setter Property="Foreground" Value="{StaticResource FgPrimaryBrush}"/><Setter Property="FontSize" Value="12"/>
            <Setter Property="FontWeight" Value="Normal"/><Setter Property="Cursor" Value="Hand"/><Setter Property="BorderThickness" Value="1"/><Setter Property="BorderBrush" Value="#FF2D3A47"/><Setter Property="Margin" Value="0,8,0,0"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="8" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"><Grid>
                    <Rectangle x:Name="accent" Fill="{TemplateBinding BorderBrush}" Width="3" HorizontalAlignment="Left" RadiusX="1.5" RadiusY="1.5" Margin="9,14" Opacity="0.72"/>
                    <ContentPresenter HorizontalAlignment="Stretch" VerticalAlignment="Center" Margin="24,20,20,20"/></Grid></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="border" Property="Background" Value="#FF151C24"/><Setter TargetName="border" Property="BorderBrush" Value="#FF16A34A"/><Setter TargetName="accent" Property="Opacity" Value="1"/></Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="border" Property="BorderBrush" Value="#FF22C55E"/></Trigger>
                    <MultiTrigger>
                        <MultiTrigger.Conditions>
                            <Condition Property="Tag" Value="Warning"/>
                            <Condition Property="IsMouseOver" Value="True"/>
                        </MultiTrigger.Conditions>
                        <Setter TargetName="border" Property="Background" Value="#FF2A2112"/>
                        <Setter TargetName="border" Property="BorderBrush" Value="#FFF59E0B"/>
                        <Setter TargetName="accent" Property="Opacity" Value="1"/>
                    </MultiTrigger>
                    <MultiTrigger>
                        <MultiTrigger.Conditions>
                            <Condition Property="Tag" Value="Warning"/>
                            <Condition Property="IsKeyboardFocused" Value="True"/>
                        </MultiTrigger.Conditions>
                        <Setter TargetName="border" Property="BorderBrush" Value="#FFF59E0B"/>
                    </MultiTrigger>
                    <MultiTrigger>
                        <MultiTrigger.Conditions>
                            <Condition Property="Tag" Value="Danger"/>
                            <Condition Property="IsMouseOver" Value="True"/>
                        </MultiTrigger.Conditions>
                        <Setter TargetName="border" Property="Background" Value="#FF2B1117"/>
                        <Setter TargetName="border" Property="BorderBrush" Value="#FFEF4444"/>
                        <Setter TargetName="accent" Property="Opacity" Value="1"/>
                    </MultiTrigger>
                    <MultiTrigger>
                        <MultiTrigger.Conditions>
                            <Condition Property="Tag" Value="Danger"/>
                            <Condition Property="IsKeyboardFocused" Value="True"/>
                        </MultiTrigger.Conditions>
                        <Setter TargetName="border" Property="BorderBrush" Value="#FFEF4444"/>
                    </MultiTrigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter TargetName="border" Property="Opacity" Value="0.36"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <Style x:Key="WarningMaintButton" TargetType="Button" BasedOn="{StaticResource MaintButton}">
            <Setter Property="Tag" Value="Warning"/><Setter Property="Background" Value="#FF211A0E"/><Setter Property="BorderBrush" Value="#FFF59E0B"/><Setter Property="Foreground" Value="#FFFFE8B0"/>
        </Style>
        <Style x:Key="DangerMaintButton" TargetType="Button" BasedOn="{StaticResource MaintButton}">
            <Setter Property="Tag" Value="Danger"/><Setter Property="Background" Value="#FF1F1016"/><Setter Property="BorderBrush" Value="#FFEF4444"/><Setter Property="Foreground" Value="#FFFFF1F2"/>
        </Style>
        <Style x:Key="SurfaceCard" TargetType="Border">
            <Setter Property="Background" Value="#FF121821"/><Setter Property="BorderBrush" Value="#FF25313D"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="8"/><Setter Property="Padding" Value="14"/>
        </Style>
        <Style x:Key="InsetPanel" TargetType="Border">
            <Setter Property="Background" Value="#FF0F151D"/><Setter Property="BorderBrush" Value="#FF25313D"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="8"/><Setter Property="Padding" Value="12"/>
        </Style>
        <Style x:Key="StatusCard" TargetType="Border" BasedOn="{StaticResource SurfaceCard}">
            <Setter Property="Padding" Value="12"/>
            <Setter Property="MinHeight" Value="68"/>
        </Style>
        <Style x:Key="SectionEyebrow" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FF86EFAC"/><Setter Property="FontSize" Value="11"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="SectionLead" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FFA6B0BB"/><Setter Property="FontSize" Value="12.5"/><Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="ValueTileLabel" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FFA6B0BB"/><Setter Property="FontSize" Value="11"/><Setter Property="FontWeight" Value="SemiBold"/>
        </Style>
        <Style x:Key="ValueTileValue" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FFE7EDF3"/><Setter Property="FontSize" Value="15"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="HelperText" TargetType="TextBlock">
            <Setter Property="Foreground" Value="#FF778390"/><Setter Property="FontSize" Value="10.75"/><Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
        <Style x:Key="OptionCard" TargetType="Border" BasedOn="{StaticResource InsetPanel}">
            <Setter Property="Padding" Value="14,12"/>
        </Style>
        <!-- Tooltip -->
        <Style TargetType="ToolTip">
            <Setter Property="Background" Value="#FF151C24"/><Setter Property="Foreground" Value="#FFE7EDF3"/><Setter Property="BorderBrush" Value="#FF55616F"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontSize" Value="11.5"/><Setter Property="Padding" Value="12,9"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ToolTip">
                <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="8" Padding="{TemplateBinding Padding}">
                    <Border.Effect><DropShadowEffect BlurRadius="18" ShadowDepth="2" Opacity="0.45" Direction="270" Color="#05070A"/></Border.Effect>
                    <ContentPresenter/></Border>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <Style x:Key="LibreSpotScrollBar" TargetType="ScrollBar">
            <Setter Property="Width" Value="10"/>
            <Setter Property="MinWidth" Value="10"/>
            <Setter Property="Background" Value="#FF090D13"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                        <Grid Background="{TemplateBinding Background}" Width="{TemplateBinding Width}">
                            <Track x:Name="PART_Track" IsDirectionReversed="True">
                                <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Opacity="0"/></Track.DecreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType="Thumb">
                                                <Border x:Name="ThumbBorder" Background="#FF465564" CornerRadius="5" Margin="2"/>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="ThumbBorder" Property="Background" Value="#FF5F6D7B"/></Trigger>
                                                    <Trigger Property="IsDragging" Value="True"><Setter TargetName="ThumbBorder" Property="Background" Value="#FF16A34A"/></Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                                <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Opacity="0"/></Track.IncreaseRepeatButton>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style TargetType="{x:Type ScrollBar}" BasedOn="{StaticResource LibreSpotScrollBar}"/>
        <Style x:Key="DarkScrollViewer" TargetType="ScrollViewer">
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ScrollViewer">
                        <Grid Background="{TemplateBinding Background}">
                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="10"/></Grid.ColumnDefinitions>
                            <ScrollContentPresenter x:Name="PART_ScrollContentPresenter" Grid.Column="0" CanContentScroll="{TemplateBinding CanContentScroll}" Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" ContentStringFormat="{TemplateBinding ContentStringFormat}"/>
                            <ScrollBar x:Name="PART_VerticalScrollBar" Grid.Column="1" Orientation="Vertical" Value="{TemplateBinding VerticalOffset}" Maximum="{TemplateBinding ScrollableHeight}" ViewportSize="{TemplateBinding ViewportHeight}" Visibility="{TemplateBinding ComputedVerticalScrollBarVisibility}" Style="{StaticResource LibreSpotScrollBar}"/>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <!-- Root: 2-col layout. Sidebar carries nav. Main content carries title bar, panels, footer. -->
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="252"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- ===== SIDEBAR ===== -->
        <Border Grid.Column="0" Background="{StaticResource SurfaceSidebarBrush}" BorderBrush="{StaticResource BorderSubtleBrush}" BorderThickness="0,0,1,0">
            <Grid Margin="22,28,22,22">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <!-- Brand block -->
                <StackPanel Grid.Row="0" Orientation="Horizontal">
                    <Border Width="44" Height="44" CornerRadius="11" Background="{StaticResource SurfaceElevatedBrush}" BorderBrush="{StaticResource AccentSoftBorderBrush}" BorderThickness="1" Padding="6">
                        <Image Name="TitleLogo" Stretch="Uniform"/>
                    </Border>
                    <StackPanel Margin="12,0,0,0" VerticalAlignment="Center">
                        <TextBlock Name="TitleText" Foreground="{StaticResource FgPrimaryBrush}" FontSize="16.5" FontWeight="SemiBold"/>
                        <TextBlock Name="TitleSubtext" Text="Premium Spotify toolkit" Foreground="{StaticResource FgMutedBrush}" FontSize="10.75" FontWeight="SemiBold" Margin="0,2,0,0"/>
                    </StackPanel>
                </StackPanel>

                <!-- Nav header -->
                <TextBlock Grid.Row="1" Text="WORKFLOW" Foreground="{StaticResource FgMutedBrush}" FontSize="10" FontWeight="SemiBold" Margin="4,30,0,10">
                    <TextBlock.RenderTransform><TranslateTransform/></TextBlock.RenderTransform>
                </TextBlock>

                <!-- Nav items -->
                <StackPanel Grid.Row="2">
                    <RadioButton Name="ModeEasy" Style="{StaticResource ModeRadio}" GroupName="Mode" IsChecked="True" Tag="Fastest clean setup with the recommended Spotify, SpotX, and Marketplace baseline." ToolTip="Fastest clean setup with the recommended baseline." AutomationProperties.Name="Recommended setup — verified baseline">
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="22"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                            <Path Grid.Column="0" Data="{StaticResource IconSparkle}" Stroke="{StaticResource AccentBrush}" Fill="{StaticResource AccentSoftBgBrush}" StrokeThickness="1.6" Stretch="Uniform" Width="20" Height="20"/>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="Recommended setup" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold"/>
                                <TextBlock Text="Recommended baseline" Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,2,0,0"/>
                            </StackPanel>
                        </Grid>
                    </RadioButton>
                    <RadioButton Name="ModeCustom" Style="{StaticResource ModeRadio}" GroupName="Mode" Tag="Tune cleanup, theming, lyrics, extensions, and launch behavior before anything runs." ToolTip="Tune every option before installing." AutomationProperties.Name="Custom Install — Per-option control">
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="22"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                            <Path Grid.Column="0" Data="{StaticResource IconSliders}" Stroke="{StaticResource FgPrimaryBrush}" StrokeThickness="1.6" Stretch="Uniform" Width="20" Height="20"/>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="Custom Install" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold"/>
                                <TextBlock Text="Per-option control" Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,2,0,0"/>
                            </StackPanel>
                        </Grid>
                    </RadioButton>
                    <RadioButton Name="ModeMaint" Style="{StaticResource ModeRadio}" GroupName="Mode" Tag="Inspect the current stack, restore backups, reapply patches, or remove modifications safely." ToolTip="Inspect, back up, reapply, or reset." AutomationProperties.Name="Maintenance — Repair and recover">
                        <Grid>
                            <Grid.ColumnDefinitions><ColumnDefinition Width="22"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                            <Path Grid.Column="0" Data="{StaticResource IconWrench}" Stroke="{StaticResource FgPrimaryBrush}" StrokeThickness="1.6" Stretch="Uniform" Width="20" Height="20"/>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="Maintenance" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold"/>
                                <TextBlock Text="Repair and recover" Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,2,0,0"/>
                            </StackPanel>
                        </Grid>
                    </RadioButton>
                </StackPanel>

                <!-- Update banner (slot 3) -->
                <Border Grid.Row="3" Name="UpdateBanner" Background="{StaticResource AccentSoftBgBrush}" BorderBrush="{StaticResource AccentSoftBorderBrush}" BorderThickness="1" CornerRadius="10" Padding="14,11" Margin="0,0,0,14" Visibility="Collapsed" ToolTip="A newer LibreSpot release is available on GitHub.">
                    <StackPanel>
                        <TextBlock Text="UPDATE AVAILABLE" Foreground="{StaticResource AccentMutedBrush}" FontSize="9.75" FontWeight="SemiBold"/>
                        <TextBlock Margin="0,4,0,0"><Hyperlink Name="LinkUpdate" Foreground="{StaticResource FgPrimaryBrush}" TextDecorations="None" FontSize="12" FontWeight="SemiBold" Cursor="Hand">View latest release &#x2192;</Hyperlink></TextBlock>
                    </StackPanel>
                </Border>

                <!-- Sidebar footer links -->
                <StackPanel Grid.Row="4">
                    <Border Height="1" Background="{StaticResource BorderSubtleBrush}" Margin="0,0,0,14"/>
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
                        <Button Name="LinkGitHub" Width="34" Height="34" Background="Transparent" BorderThickness="0" Cursor="Hand" ToolTip="View LibreSpot on GitHub" Margin="0,0,4,0">
                            <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="8"><Path x:Name="ico" Fill="{StaticResource FgSecondaryBrush}" Data="M8,0 C3.58,0 0,3.58 0,8 c0,3.54 2.29,6.53 5.47,7.59 c.4,.07 .55,-.17 .55,-.38 c0,-.19 -.01,-.82 -.01,-1.49 c-2.01,.37 -2.53,-.49 -2.69,-.94 c-.09,-.23 -.48,-.94 -.82,-1.13 c-.28,-.15 -.68,-.52 -.01,-.53 c.63,-.01 1.08,.58 1.23,.82 c.72,1.21 1.87,.87 2.33,.66 c.07,-.52 .28,-.87 .51,-1.07 c-1.78,-.2 -3.64,-.89 -3.64,-3.95 c0,-.87 .31,-1.59 .82,-2.15 c-.08,-.2 -.36,-1.02 .08,-2.12 c0,0 .67,-.21 2.2,.82 c.64,-.18 1.32,-.27 2,-.27 c.68,0 1.36,.09 2,.27 c1.53,-1.04 2.2,-.82 2.2,-.82 c.44,1.1 .16,1.92 .08,2.12 c.51,.56 .82,1.27 .82,2.15 c0,3.07 -1.87,3.75 -3.65,3.95 c.29,.25 .54,.73 .54,1.48 c0,1.07 -.01,1.93 -.01,2.2 c0,.21 .15,.46 .55,.38 A8.013,8.013,0,0,0,16,8 c0,-4.42 -3.58,-8 -8,-8z" Stretch="Uniform" Width="15" Height="15" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="{StaticResource SurfaceElevatedBrush}"/><Setter TargetName="ico" Property="Fill" Value="{StaticResource FgPrimaryBrush}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template>
                        </Button>
                        <TextBlock VerticalAlignment="Center" Margin="6,0,0,0"><Hyperlink Name="LinkSpotX" NavigateUri="https://github.com/SpotX-Official/SpotX" Foreground="{StaticResource FgMutedBrush}" TextDecorations="None" FontSize="10.5" Cursor="Hand">SpotX</Hyperlink></TextBlock>
                        <Border Width="1" Height="10" Background="{StaticResource BorderSubtleBrush}" Margin="10,0"/>
                        <TextBlock VerticalAlignment="Center"><Hyperlink Name="LinkSpicetify" NavigateUri="https://github.com/spicetify" Foreground="{StaticResource FgMutedBrush}" TextDecorations="None" FontSize="10.5" Cursor="Hand">Spicetify</Hyperlink></TextBlock>
                    </StackPanel>
                </StackPanel>
            </Grid>
        </Border>

        <!-- ===== MAIN COLUMN ===== -->
        <Grid Grid.Column="1">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- Title bar: drag handle + mode headline + window controls -->
            <Border Name="TitleBar" Grid.Row="0" Background="Transparent" Padding="28,12,16,10">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <StackPanel VerticalAlignment="Center">
                        <TextBlock Text="LIBRESPOT" Foreground="{StaticResource FgMutedBrush}" FontSize="9.5" FontWeight="SemiBold" Margin="0,0,0,2"/>
                        <TextBlock Name="ModeHeadline" Text="Recommended path for a first install" Foreground="{StaticResource FgPrimaryBrush}" FontSize="18" FontWeight="SemiBold" TextWrapping="Wrap"/>
                        <TextBlock Name="ModeSummaryText" Text="LibreSpot handles cleanup, verified downloads, Spotify patching, Marketplace, and a reliable default extension set." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.75" Margin="0,3,0,0" MaxWidth="820" TextWrapping="Wrap"/>
                    </StackPanel>
                    <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Top" Margin="20,2,0,0">
                        <Button Name="MinimizeBtn" Content="&#x2013;" Width="38" Height="34" Background="Transparent" Foreground="{StaticResource FgSecondaryBrush}" BorderThickness="0" FontSize="14" FontWeight="SemiBold" Cursor="Hand" Margin="0,0,4,0" ToolTip="Minimize" AutomationProperties.Name="Minimize window">
                            <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="8"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="{StaticResource SurfaceElevatedBrush}"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template>
                        </Button>
                        <Button Name="CloseTitleBtn" Content="&#x2715;" Width="38" Height="34" Background="Transparent" Foreground="{StaticResource FgSecondaryBrush}" BorderThickness="0" FontSize="11" Cursor="Hand" ToolTip="Close" AutomationProperties.Name="Close window">
                            <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="8"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="{StaticResource DangerBrush}"/><Setter Property="Foreground" Value="#FFFFFFFF"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template>
                        </Button>
                    </StackPanel>
                </Grid>
            </Border>

            <!-- ===== CONTENT ===== -->
            <Grid Name="PageContainer" Grid.Row="1" Margin="24,0,24,16">
                <!-- ===== CONFIG PAGE ===== -->
                <Grid Name="PageConfig" Visibility="Visible"><Grid.RowDefinitions><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                    <Border Grid.Row="0" Background="{StaticResource SurfaceElevatedBrush}" CornerRadius="12" Padding="16" BorderBrush="{StaticResource BorderSubtleBrush}" BorderThickness="1">
                        <Border.Effect><DropShadowEffect BlurRadius="22" ShadowDepth="0" Opacity="0.32" Color="#FF000000"/></Border.Effect>
                        <Grid>

                                <!-- ===== EASY PANEL ===== -->
                                <ScrollViewer Name="PanelEasy" Visibility="Visible" VerticalScrollBarVisibility="Auto" Style="{StaticResource DarkScrollViewer}"><StackPanel Margin="4,6,4,0">
                                    <Grid Margin="0,0,0,12">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="1.15*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="0.85*"/></Grid.ColumnDefinitions>
                                        <StackPanel Grid.Column="0">
                                            <TextBlock Text="A clean, reliable Spotify setup in one pass" Foreground="{StaticResource FgPrimaryBrush}" FontSize="17" FontWeight="SemiBold"/>
                                            <TextBlock Text="Recommended setup applies the stable default stack: Spotify cleanup, SpotX patching, Spicetify, Marketplace, and a curated extension set with recovery-focused defaults." Foreground="{StaticResource FgSecondaryBrush}" FontSize="13" TextWrapping="Wrap" Margin="0,10,0,0"/>
                                            <WrapPanel Margin="0,18,0,0">
                                                <StackPanel Orientation="Horizontal" Margin="0,0,18,10"><Rectangle Width="3" Height="14" Fill="#FF22C55E" RadiusX="1.5" RadiusY="1.5" VerticalAlignment="Center"/><TextBlock Text="Clean install" Foreground="#FF86EFAC" FontSize="10.75" FontWeight="SemiBold" Margin="8,0,0,0" VerticalAlignment="Center"/></StackPanel>
                                                <StackPanel Orientation="Horizontal" Margin="0,0,18,10"><Rectangle Width="3" Height="14" Fill="#FF22C55E" RadiusX="1.5" RadiusY="1.5" VerticalAlignment="Center"/><TextBlock Text="Marketplace included" Foreground="#FF86EFAC" FontSize="10.75" FontWeight="SemiBold" Margin="8,0,0,0" VerticalAlignment="Center"/></StackPanel>
                                                <StackPanel Orientation="Horizontal" Margin="0,0,18,10"><Rectangle Width="3" Height="14" Fill="#FF93C5FD" RadiusX="1.5" RadiusY="1.5" VerticalAlignment="Center"/><TextBlock Text="3 default extensions" Foreground="#FF93C5FD" FontSize="10.75" FontWeight="SemiBold" Margin="8,0,0,0" VerticalAlignment="Center"/></StackPanel>
                                                <StackPanel Orientation="Horizontal" Margin="0,0,0,10"><Rectangle Width="3" Height="14" Fill="#FFFCD34D" RadiusX="1.5" RadiusY="1.5" VerticalAlignment="Center"/><TextBlock Text="Launch when finished" Foreground="#FFFCD34D" FontSize="10.75" FontWeight="SemiBold" Margin="8,0,0,0" VerticalAlignment="Center"/></StackPanel>
                                            </WrapPanel>
                                        </StackPanel>
                                        <Border Grid.Column="2" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Default preset" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold"/>
                                                <TextBlock Text="Best when you want Spotify working quickly without tuning every option." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" TextWrapping="Wrap" Margin="0,4,0,8"/>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Ellipse Width="8" Height="8" Fill="#FF22C55E" Margin="0,6,12,0"/><TextBlock Grid.Column="1" Text="Fresh Spotify with SpotX patching and the new UI theme" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Ellipse Width="8" Height="8" Fill="#FF22C55E" Margin="0,6,12,0"/><TextBlock Grid.Column="1" Text="Podcasts removed, ad-like sections hidden, and auto-updates blocked" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Ellipse Width="8" Height="8" Fill="#FF22C55E" Margin="0,6,12,0"/><TextBlock Grid.Column="1" Text="Marketplace plus Full App Display, True Shuffle, and Trash Bin" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Ellipse Width="8" Height="8" Fill="#FF93C5FD" Margin="0,6,12,0"/><TextBlock Grid.Column="1" Text="Settings are saved so the same defaults are ready next time" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                    <Grid>
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Grid.Column="0" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="What LibreSpot takes care of" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" FontWeight="SemiBold"/>
                                                <TextBlock Text="The goal is a dependable install, not just a pretty wrapper." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" Margin="0,4,0,8"/>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Width="22" Height="22" CornerRadius="6" Background="#FF111A22" BorderBrush="#FF2D5A3F" BorderThickness="1"><Path Data="M 5 10 L 9 14 L 16 6" Stroke="#FF22C55E" StrokeThickness="1.8" Margin="0.5,0,0,0"/></Border><TextBlock Grid.Column="1" Margin="12,0,0,0" Text="Verifies pinned downloads before applying anything" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Width="22" Height="22" CornerRadius="6" Background="#FF111C2A" BorderBrush="#FF2E4964" BorderThickness="1"><Path Data="M 5 10 L 9 14 L 16 6" Stroke="#FF93C5FD" StrokeThickness="1.8" Margin="0.5,0,0,0"/></Border><TextBlock Grid.Column="1" Margin="12,0,0,0" Text="Runs cleanup first so stale Spotify and Spicetify files do not conflict" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid Margin="0,0,0,10"><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Width="22" Height="22" CornerRadius="6" Background="#FF211A0E" BorderBrush="#FF6B4E16" BorderThickness="1"><Path Data="M 5 10 L 9 14 L 16 6" Stroke="#FFFCD34D" StrokeThickness="1.8" Margin="0.5,0,0,0"/></Border><TextBlock Grid.Column="1" Margin="12,0,0,0" Text="Installs themes, extensions, and Marketplace in a safe order" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions><Border Width="22" Height="22" CornerRadius="6" Background="#FF111A22" BorderBrush="#FF2D5A3F" BorderThickness="1"><Path Data="M 5 10 L 9 14 L 16 6" Stroke="#FF22C55E" StrokeThickness="1.8" Margin="0.5,0,0,0"/></Border><TextBlock Grid.Column="1" Margin="12,0,0,0" Text="Keeps recovery tools close by if Spotify updates later" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/></Grid>
                                            </StackPanel>
                                        </Border>
                                        <Border Grid.Column="2" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Before you start" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" FontWeight="SemiBold"/>
                                                <TextBlock Text="A few expectations up front make the whole flow feel more predictable." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" Margin="0,4,0,8"/>
                                                <TextBlock Text="LibreSpot requests administrator permission because it modifies Spotify files and Windows settings." Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap" Margin="0,0,0,10"/>
                                                <TextBlock Text="A network connection is required for GitHub downloads, preview images, and update checks." Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap" Margin="0,0,0,10"/>
                                                <TextBlock Text="Recommended setup removes any existing Spotify and Spicetify setup first so the result is consistent." Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap" Margin="0,0,0,10"/>
                                                <TextBlock Text="If you prefer to keep a current install in place, switch to Custom Install and disable full cleanup." Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" TextWrapping="Wrap"/>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </StackPanel></ScrollViewer>

                                <!-- ===== CUSTOM PANEL ===== -->
                                <ScrollViewer Name="PanelCustom" Visibility="Collapsed" VerticalScrollBarVisibility="Auto" Style="{StaticResource DarkScrollViewer}"><StackPanel Margin="4,6,4,0">
                                    <Grid Margin="0,0,0,10">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                        <StackPanel>
                                            <TextBlock Text="Custom install, dialed in" Foreground="{StaticResource FgPrimaryBrush}" FontSize="16" FontWeight="SemiBold"/>
                                            <TextBlock Text="Choose exactly how much cleanup, theming, Marketplace support, and extension prep you want before Spotify opens." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12.5" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                        </StackPanel>
                                        <Button Grid.Column="1" Name="BtnResetCustomDefaults" Content="Recommended defaults" Style="{StaticResource SecondaryActionButton}" Width="216" Height="40" Margin="18,2,0,0" VerticalAlignment="Top" ToolTip="Apply the Recommended setup defaults here so you can keep customizing from a known-good baseline." AutomationProperties.Name="Load recommended defaults"/>
                                    </Grid>
                                    <Border Style="{StaticResource SurfaceCard}" Margin="0,0,0,10">
                                        <Grid>
                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                            <Border Grid.Column="0" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Install plan" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/>
                                                    <TextBlock Name="CustomSnapshotPlanValue" Text="Clean install" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                </StackPanel>
                                            </Border>
                                            <Border Grid.Column="2" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Theme" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/>
                                                    <TextBlock Name="CustomSnapshotThemeValue" Text="Marketplace only" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                </StackPanel>
                                            </Border>
                                            <Border Grid.Column="4" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Extensions" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/>
                                                    <TextBlock Name="CustomSnapshotExtensionsValue" Text="3 extensions" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                </StackPanel>
                                            </Border>
                                            <Border Grid.Column="6" Style="{StaticResource InsetPanel}">
                                                <StackPanel>
                                                    <TextBlock Text="Remembered state" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/>
                                                    <TextBlock Name="CustomSnapshotMemoryValue" Text="Will save on install" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                </StackPanel>
                                            </Border>
                                        </Grid>
                                    </Border>
                                    <Border Style="{StaticResource SurfaceCard}" Margin="0,0,0,10">
                                        <StackPanel>
                                            <TextBlock Text="Local profiles" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" FontWeight="SemiBold"/>
                                            <TextBlock Text="Preview bundled templates, save the current Custom selections, or set a selected profile active without hand-editing AppData." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" Margin="0,4,0,10" TextWrapping="Wrap"/>
                                            <Grid>
                                                <Grid.ColumnDefinitions><ColumnDefinition Width="2*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                                <ComboBox Grid.Column="0" Name="CmbLocalProfiles" Height="36" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" ToolTip="Named LibreSpot profiles and bundled templates." AutomationProperties.Name="Local profiles"/>
                                                <Button Grid.Column="2" Name="BtnProfilePreview" Content="Preview" Style="{StaticResource SecondaryActionButton}" Height="36" ToolTip="Load the selected profile into Custom without writing config.json." AutomationProperties.Name="Preview selected profile"/>
                                                <Button Grid.Column="4" Name="BtnProfileApply" Content="Set active" Background="{StaticResource AccentBrush}" Foreground="{StaticResource FgInverseBrush}" BorderBrush="{StaticResource AccentBrush}" Style="{StaticResource ActionButton}" Height="36" ToolTip="Confirm and write the selected profile to config.json." AutomationProperties.Name="Set selected profile active"/>
                                            </Grid>
                                            <Grid Margin="0,10,0,0">
                                                <Grid.ColumnDefinitions><ColumnDefinition Width="2*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                                <TextBox Grid.Column="0" Name="TxtProfileName" Text="Custom profile" Style="{StaticResource DarkTextBox}" Height="36" ToolTip="Name for a new local profile saved from the current Custom selections." AutomationProperties.Name="Profile name"/>
                                                <Button Grid.Column="2" Name="BtnProfileSaveCurrent" Content="Save current" Style="{StaticResource SecondaryActionButton}" Height="36" ToolTip="Save the current Custom selections as a new local profile." AutomationProperties.Name="Save current selections as profile"/>
                                            </Grid>
                                            <TextBlock Name="ProfileStatusText" Text="Select a profile to preview or set active. Saving current creates a local profile." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                        </StackPanel>
                                    </Border>
                                    <Grid>
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Grid.Column="0" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Spotify behavior" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" FontWeight="SemiBold"/>
                                                <TextBlock Text="LibreSpot uses SpotX to handle cleanup, patching, interface tweaks, and a few system-level quality-of-life options." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                <Border Style="{StaticResource InsetPanel}">
                                                    <StackPanel>
                                                        <TextBlock Text="Core cleanup" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Trim Spotify's default clutter and keep the patched setup stable after future updates." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkNewTheme" Content="Enable the new Spotify interface" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Activates Spotify new sidebar and cover art layout"/>
                                                        <CheckBox Name="ChkPodcastsOff" Content="Remove podcasts from Home" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Hides podcast sections from home feed"/>
                                                        <CheckBox Name="ChkAdSectionsOff" Content="Hide ad-like Home sections" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Removes promotional sections"/>
                                                        <CheckBox Name="ChkBlockUpdate" Content="Block Spotify auto-updates" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Prevents Spotify from overwriting patches"/>
                                                        <CheckBox Name="ChkPremium" Content="Premium account (skip ad-blocking)" Style="{StaticResource DarkCheckBox}" ToolTip="For paid users: skip ad-blocking, keep other mods"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Lyrics" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Use the static lyrics layer if you want cleaner reading and easier theme matching." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkLyrics" Content="Enable a static lyrics theme" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                                        <StackPanel Name="LyricsThemePanel" Orientation="Horizontal" Margin="28,6,0,0">
                                                            <TextBlock Text="Theme:" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
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

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Interface experiments" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Optional layout tweaks. Keep this section conservative if you want the safest possible install." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkTopSearch" Content="Move search to the top bar" Style="{StaticResource DarkCheckBox}" ToolTip="Move search bar to top of window"/>
                                                        <CheckBox Name="ChkRightSidebarOff" Content="Disable the right sidebar" Style="{StaticResource DarkCheckBox}" ToolTip="Remove the Now Playing sidebar panel"/>
                                                        <CheckBox Name="ChkRightSidebarColor" Content="Match right sidebar colors to album art" Style="{StaticResource DarkCheckBox}" ToolTip="Tint sidebar to match album cover"/>
                                                        <CheckBox Name="ChkCanvasHomeOff" Content="Disable canvas on Home" Style="{StaticResource DarkCheckBox}" ToolTip="Disable canvas artwork on the homepage"/>
                                                        <CheckBox Name="ChkHomeSubOff" Content="Hide Home subfeed chips" Style="{StaticResource DarkCheckBox}" ToolTip="Hide genre filter chips on home page"/>
                                                        <CheckBox Name="ChkHideColIconOff" Content="Show collaboration icons in playlists" Style="{StaticResource DarkCheckBox}" ToolTip="Keep collaboration icons visible in playlists"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Experimental features" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="These features are newer or experimental. They may change behavior with future Spotify updates." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkPlus" Content="Enhanced save and destination features" Style="{StaticResource DarkCheckBox}" ToolTip="Enable enhanced save/destination features in Spotify"/>
                                                        <CheckBox Name="ChkNewFullscreen" Content="Experimental fullscreen mode" Style="{StaticResource DarkCheckBox}" ToolTip="Enable the new experimental fullscreen mode"/>
                                                        <CheckBox Name="ChkFunnyProgress" Content="Humorous progress bar" Style="{StaticResource DarkCheckBox}" ToolTip="Replace the standard progress bar with a humorous variant"/>
                                                        <CheckBox Name="ChkExpSpotify" Content="Enable experimental Spotify features" Style="{StaticResource DarkCheckBox}" ToolTip="Unlock experimental features that Spotify is testing but has not yet released"/>
                                                        <CheckBox Name="ChkLyricsBlock" Content="Disable native lyrics entirely" Style="{StaticResource DarkCheckBox}" ToolTip="Block Spotify's built-in lyrics feature completely"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="System behavior" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Set startup behavior, shortcut handling, and the cache-size override SpotX can apply." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkDisableStartup" Content="Disable Spotify on Windows startup" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                                        <CheckBox Name="ChkNoShortcut" Content="Skip the desktop shortcut" Style="{StaticResource DarkCheckBox}"/>
                                                        <CheckBox Name="ChkStartSpoti" Content="Launch Spotify automatically after install" Style="{StaticResource DarkCheckBox}" ToolTip="Let SpotX start Spotify right after the patch finishes"/>
                                                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                                                            <TextBlock Text="Cache limit (MB):" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <TextBox Name="TxtCacheLimit" Width="96" Text="0" Style="{StaticResource DarkTextBox}" ToolTip="Use 0 or a value of 500 MB and above."/>
                                                        </StackPanel>
                                                        <TextBlock Text="Use 0 to keep Spotify's default behavior. LibreSpot treats any value from 1 to 499 as 500 MB so the override stays in SpotX's safer range." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Privacy" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Limit what SpotX and Spotify can report back. Recommended defaults trim outbound telemetry without breaking patches." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkSendVersionOff" Content="Disable SpotX version reporting" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Blocks SpotX's outbound version notification (added in SpotX April 2026)"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Advanced" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Power-user overrides. Leave defaults unless you have a specific reason to change them." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkDevTools" Content="Enable Spotify Developer Tools" Style="{StaticResource DarkCheckBox}" ToolTip="Unlocks the Chromium DevTools hotkey inside Spotify (useful for Spicetify extension authors)"/>
                                                        <CheckBox Name="ChkMirror" Content="Use GitHub.io mirror for SpotX assets" Style="{StaticResource DarkCheckBox}" ToolTip="Falls back to the github.io mirror if raw.githubusercontent.com is blocked on your network"/>
                                                        <CheckBox Name="ChkConfirmUninstall" Content="Force a clean Spotify uninstall before patching" Style="{StaticResource DarkCheckBox}" ToolTip="Runs SpotX's uninstall-then-reinstall flow even when the current version would otherwise be kept"/>
                                                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                                                            <TextBlock Text="Download method:" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbDownloadMethod" Width="140" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" SelectedIndex="0" ToolTip="Force SpotX to use a specific downloader when the auto-selected one fails.">
                                                                <ComboBoxItem Content="auto"/>
                                                                <ComboBoxItem Content="curl"/>
                                                                <ComboBoxItem Content="webclient"/>
                                                            </ComboBox>
                                                        </StackPanel>
                                                        <StackPanel Orientation="Horizontal" Margin="0,10,0,0">
                                                            <TextBlock Text="Spotify version:" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbSpotifyVersion" Width="260" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" SelectedIndex="0" ToolTip="Pin a specific Spotify client version. Leave on 'Auto' unless you know a specific build works better for your system."/>
                                                        </StackPanel>
                                                        <TextBlock Name="SpotifyVersionHint" Text="Lets SpotX pick the most compatible build." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,6,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>
                                            </StackPanel>
                                        </Border>
                                        <Border Grid.Column="2" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Themes, Marketplace, and extensions" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" FontWeight="SemiBold"/>
                                                <TextBlock Text="Shape the first-run look and decide what should already be installed before Spotify opens." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                <Border Style="{StaticResource InsetPanel}">
                                                    <StackPanel>
                                                        <TextBlock Text="Theme" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Pick a bundled theme now, or stay Marketplace-only and browse from inside Spotify later." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Theme:" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbTheme" Width="220" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}"/></StackPanel>
                                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Color Scheme:" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                                            <ComboBox Name="CmbScheme" Width="190" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" ToolTip="Choose the color scheme for the selected bundled theme."/></StackPanel>
                                                        <Border Name="PreviewBorder" CornerRadius="12" Background="#FF090D13" BorderBrush="#FF25313D" BorderThickness="1" Margin="0,10,0,0" Height="184" ClipToBounds="True">
                                                            <Grid>
                                                                <Image Name="ThemePreviewImg" Stretch="UniformToFill" RenderOptions.BitmapScalingMode="HighQuality"/>
                                                                <Border Background="#CC0B0F0D"><TextBlock Name="PreviewLabel" Text="Select a bundled theme to preview it here." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" HorizontalAlignment="Center" VerticalAlignment="Center" TextWrapping="Wrap" TextAlignment="Center" MaxWidth="240"/></Border>
                                                            </Grid>
                                                        </Border>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Marketplace (optional)" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="LibreSpot installs your selected themes and extensions directly — Marketplace adds an in-app browser for discovering more after setup. Skip it if you only want the bundled catalog." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkMarketplace" Content="Install the Spicetify Marketplace" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Optional: adds an in-app store for themes and extensions. Your selected themes and extensions above are installed directly regardless of this setting."/>
                                                        <TextBlock Text="Browse and install additional themes or extensions from inside Spotify after setup. Not required for the selections above." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="28,4,0,0" TextWrapping="Wrap"/>
                                                        <TextBlock Name="MarketplaceHealthNote" Text="" Foreground="#FFEAB308" FontSize="10" Margin="28,4,0,0" TextWrapping="Wrap" Visibility="Collapsed"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Built-in extensions" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Recommended setup ships with Full App Display, True Shuffle, and Trash Bin enabled. Custom Install lets you fine-tune the rest." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <Grid Margin="0,6,0,0">
                                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                                            <StackPanel Grid.Column="0">
                                                                <CheckBox Name="ChkExt_fullAppDisplay" Content="Full App Display" IsChecked="True" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Full-screen artwork with blur and playback controls." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_trashbin" Content="Trash Bin" IsChecked="True" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Skip songs and artists you have already marked as unwanted." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_bookmark" Content="Bookmark" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Save favorite pages, tracks, albums, and exact timestamps." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_popupLyrics" Content="Pop-up Lyrics" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Open synchronized lyrics in a separate resizable window." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_autoSkipExplicit" Content="Auto Skip Explicit" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Automatically avoid tracks flagged as explicit." Style="{StaticResource HelperText}" Margin="30,2,0,0"/>
                                                            </StackPanel>
                                                            <StackPanel Grid.Column="2">
                                                                <CheckBox Name="ChkExt_shuffle" Content="True Shuffle" IsChecked="True" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="True shuffle instead of Spotify's weighted play order." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_keyboard" Content="Keyboard Shortcuts" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Vim-style navigation for faster keyboard-driven control." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_loopyLoop" Content="Loopy Loop" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Set A-B loop points for practice, study, or repeat listening." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_autoSkipVideo" Content="Auto Skip Video" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Skip canvas videos and region-locked video-only content." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_webNowPlaying" Content="Web Now Playing (Rainmeter)" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Expose now-playing data for desktop widgets and overlays." Style="{StaticResource HelperText}" Margin="30,2,0,0"/>
                                                            </StackPanel>
                                                        </Grid>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Community extensions" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Popular third-party extensions downloaded from GitHub. These are not bundled with Spicetify and may need manual updates." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <Grid Margin="0,6,0,0">
                                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                                            <StackPanel Grid.Column="0">
                                                                <CheckBox Name="ChkExt_hidePodcasts" Content="Hide Podcasts" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Remove podcast, episode, and audiobook sections from the UI." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_beautifulLyrics" Content="Beautiful Lyrics" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Immersive synced lyrics with dynamic backgrounds and blur. Connects to a third-party lyrics service (not just GitHub/Spotify)." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_playlistIcons" Content="Playlist Icons" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Add custom icons and folder images to playlists." Style="{StaticResource HelperText}" Margin="30,2,0,0"/>
                                                            </StackPanel>
                                                            <StackPanel Grid.Column="2">
                                                                <CheckBox Name="ChkExt_volumePercentage" Content="Volume Percentage" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Display exact volume percentage next to the slider." Style="{StaticResource HelperText}" Margin="30,2,0,4"/>
                                                                <CheckBox Name="ChkExt_adblock" Content="Ad-block (Spicetify fallback)" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                                <TextBlock Text="Spicetify-layer ad blocking for when SpotX patching fails on a newer Spotify build. Not a SpotX replacement." Style="{StaticResource HelperText}" Margin="30,2,0,0"/>
                                                            </StackPanel>
                                                        </Grid>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Community custom apps" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Opt-in Spicetify custom apps installed from pinned release ZIPs. These appear as Spotify apps, not normal extensions." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkCustomApp_stats" Content="Stats" Style="{StaticResource DarkCheckBox}" Margin="0"/>
                                                        <TextBlock Text="Listening charts and library statistics from harbassan/spicetify-apps. Some views can contact Last.fm when you use those features inside Spotify." Style="{StaticResource HelperText}" Margin="30,2,0,0"/>
                                                    </StackPanel>
                                                </Border>

                                                <Border Style="{StaticResource InsetPanel}" Margin="0,8,0,0">
                                                    <StackPanel>
                                                        <TextBlock Text="Install behavior" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.5" FontWeight="SemiBold"/>
                                                        <TextBlock Text="Control how aggressively LibreSpot resets the current install and whether Spotify opens when the run is done." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,4,0,8" TextWrapping="Wrap"/>
                                                        <CheckBox Name="ChkCleanInstall" Content="Remove the existing setup first" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                                        <CheckBox Name="ChkLaunchAfter" Content="Launch Spotify when finished" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                                        <TextBlock Text="LibreSpot remembers these custom choices after setup starts, so future reapply runs stay consistent." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="0,8,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </StackPanel></ScrollViewer>

                                <!-- ===== MAINTENANCE PANEL ===== -->
                                <ScrollViewer Name="PanelMaint" Visibility="Collapsed" VerticalScrollBarVisibility="Auto" Style="{StaticResource DarkScrollViewer}"><StackPanel Margin="4,6,4,0">
                                    <TextBlock Text="Maintenance and recovery" Foreground="{StaticResource FgPrimaryBrush}" FontSize="16" FontWeight="SemiBold"/>
                                    <TextBlock Text="Check the current install, back up what matters, reapply patches after Spotify updates, or remove everything cleanly when you want to start over." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12.5" Margin="0,8,0,18" TextWrapping="Wrap"/>
                                    <Border Style="{StaticResource SurfaceCard}" Margin="0,0,0,10">
                                        <Grid>
                                            <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                            <StackPanel>
                                                <TextBlock Name="MaintenanceOverviewTitle" Text="Scanning the current setup..." Foreground="{StaticResource FgPrimaryBrush}" FontSize="15.5" FontWeight="SemiBold"/>
                                                <TextBlock Name="MaintenanceOverviewText" Text="LibreSpot is checking which parts of the Spotify stack are installed so recovery actions can stay predictable." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" Margin="0,8,0,0" TextWrapping="Wrap" MaxWidth="620"/>
                                            </StackPanel>
                                            <StackPanel Grid.Column="1" VerticalAlignment="Top" Margin="20,0,0,0" HorizontalAlignment="Right">
                                                <TextBlock Text="Safer recovery" Foreground="#FF86EFAC" FontSize="10.75" FontWeight="SemiBold" TextAlignment="Right"/>
                                                <TextBlock Text="Pinned versions" Foreground="#FF93C5FD" FontSize="10.75" FontWeight="SemiBold" Margin="0,5,0,0" TextAlignment="Right"/>
                                            </StackPanel>
                                        </Grid>
                                    </Border>

                                    <Grid Margin="0,0,0,12">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Name="StatusCardSpotify" Grid.Column="0" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="Spotify" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusSpotify" Text="Checking…" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                        <Border Name="StatusCardSpotX" Grid.Column="2" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="SpotX" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusSpotX" Text="Checking…" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                        <Border Name="StatusCardSpicetify" Grid.Column="4" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="Spicetify" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusSpicetify" Text="Checking…" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                        <Border Name="StatusCardMarketplace" Grid.Column="6" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="Marketplace" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusMarketplace" Text="Checking…" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                        <Border Name="StatusCardTheme" Grid.Column="8" Style="{StaticResource StatusCard}"><StackPanel><TextBlock Text="Theme" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/><TextBlock Name="StatusTheme" Text="Checking…" Foreground="{StaticResource FgPrimaryBrush}" FontSize="13" FontWeight="SemiBold" TextWrapping="Wrap" Margin="0,10,0,0"/></StackPanel></Border>
                                    </Grid>

                                    <Border Style="{StaticResource SurfaceCard}" Margin="0,0,0,12">
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
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Grid.Column="0" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Protect and repair" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" FontWeight="SemiBold"/>
                                                <TextBlock Text="Keep your current setup recoverable, compare pinned versions, or reapply patches after an update." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" TextWrapping="Wrap" Margin="0,8,0,6"/>
                                                <Button Name="BtnBackupConfig" Style="{StaticResource MaintButton}" AutomationProperties.Name="Create configuration backup"><StackPanel><TextBlock Text="Create configuration backup" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Save themes, extensions, and Spicetify settings before making a change." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnRestoreConfig" Style="{StaticResource MaintButton}" AutomationProperties.Name="Restore the newest backup"><StackPanel><TextBlock Text="Restore the newest backup" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Bring back the latest saved Spicetify configuration and apply it immediately." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnCheckUpdates" Style="{StaticResource MaintButton}" AutomationProperties.Name="Check pinned versions"><StackPanel><TextBlock Text="Check pinned versions" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Compare LibreSpot's pinned releases against the latest upstream versions." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnRepairMarketplace" Style="{StaticResource MaintButton}" AutomationProperties.Name="Repair and open Marketplace"><StackPanel><TextBlock Text="Repair and open Marketplace" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Reinstall the custom app, enable it in Spicetify, apply changes, then open spotify:app:marketplace." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnReapply" Style="{StaticResource MaintButton}" AutomationProperties.Name="Reapply after a Spotify update"><StackPanel><TextBlock Text="Reapply after a Spotify update" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Run SpotX again and reapply Spicetify without rebuilding your preferences from scratch." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Border Background="#FF111821" BorderBrush="#FF25313D" BorderThickness="1" CornerRadius="10" Padding="14,12" Margin="0,10,0,0">
                                                    <StackPanel>
                                                        <CheckBox Name="ChkAutoReapply" Content="Auto-reapply when Spotify updates itself" Style="{StaticResource DarkCheckBox}" ToolTip="Registers a per-user scheduled task that watches Spotify.exe's version number and reruns the saved SpotX config silently whenever it changes."/>
                                                        <TextBlock Name="AutoReapplyStatusText" Text="Scheduled task: not installed" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" Margin="28,4,0,0" TextWrapping="Wrap"/>
                                                        <TextBlock Text="The watcher only runs when Spotify is closed, skips automatically if no saved LibreSpot config exists, and writes every action to watcher.log next to the install log." Foreground="{StaticResource FgMutedBrush}" FontSize="10.5" Margin="28,4,0,0" TextWrapping="Wrap"/>
                                                    </StackPanel>
                                                </Border>
                                            </StackPanel>
                                        </Border>
                                        <Border Grid.Column="2" Style="{StaticResource SurfaceCard}">
                                            <StackPanel>
                                                <TextBlock Text="Restore or remove modifications" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" FontWeight="SemiBold"/>
                                                <TextBlock Text="Use the lighter recovery option first. Full Reset is intentionally destructive and best when you want to start clean." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12" TextWrapping="Wrap" Margin="0,8,0,6"/>
                                                <Button Name="BtnSafeMode" Style="{StaticResource MaintButton}" AutomationProperties.Name="Safe mode — disable all customizations"><StackPanel><TextBlock Text="Safe mode (disable all customizations)" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Disable all themes and extensions without uninstalling. Spotify will load in its stock look — use Reapply to restore your setup." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnSpicetifyRestore" Style="{StaticResource MaintButton}" AutomationProperties.Name="Restore vanilla Spotify"><StackPanel><TextBlock Text="Restore vanilla Spotify" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Remove Spicetify themes and extensions while keeping SpotX patching in place." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnUninstallSpicetify" Style="{StaticResource WarningMaintButton}" AutomationProperties.Name="Uninstall Spicetify"><StackPanel><TextBlock Text="Uninstall Spicetify" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Restore vanilla first, then remove the CLI, config, and PATH entry while leaving Spotify and SpotX in place." Foreground="#FFEABF67" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                                <Button Name="BtnFullReset" Style="{StaticResource DangerMaintButton}" AutomationProperties.Name="Full Reset — remove all modifications"><StackPanel><TextBlock Text="Full Reset" Foreground="{Binding RelativeSource={RelativeSource AncestorType=Button}, Path=Foreground}" FontSize="12.75" FontWeight="SemiBold"/><TextBlock Text="Restore vanilla Spotify, remove SpotX and Spicetify, uninstall Spotify, and clean leftover files." Foreground="#FFFCA5A5" FontSize="11.5" Margin="0,6,0,0" TextWrapping="Wrap"/></StackPanel></Button>
                                            </StackPanel>
                                        </Border>
                                    </Grid>
                                </StackPanel></ScrollViewer>
                            </Grid></Border>
                    <Grid Grid.Row="1" Margin="0,10,0,0">
                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="14"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                        <Border Grid.Column="0" Name="SelectionSummaryCard" Style="{StaticResource SurfaceCard}" Padding="14,10">
                            <StackPanel>
                                <TextBlock Name="SelectionSummaryTitle" Text="Install snapshot" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11" FontWeight="SemiBold"/>
                                <TextBlock Name="SelectionSummary" Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" VerticalAlignment="Center" TextWrapping="Wrap" Margin="0,6,0,0"/>
                                <Grid Margin="0,10,0,0">
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                    <Border Name="SelectionStateBadge" Background="#FF111C2A" BorderBrush="#FF2E4964" BorderThickness="1" CornerRadius="6" Padding="9,4" VerticalAlignment="Top">
                                        <TextBlock Name="SelectionStateBadgeText" Text="Ready" Foreground="#FF93C5FD" FontSize="10.5" FontWeight="SemiBold"/>
                                    </Border>
                                    <TextBlock Grid.Column="2" Name="SelectionStateDetail" Foreground="{StaticResource FgMutedBrush}" FontSize="11.25" TextWrapping="Wrap" VerticalAlignment="Center"/>
                                </Grid>
                            </StackPanel>
                        </Border>
                        <StackPanel Grid.Column="2" Name="FooterActionPanel" HorizontalAlignment="Right">
                            <Button Name="BtnInstall" Content="Install recommended setup" Background="{StaticResource AccentBrush}" Foreground="{StaticResource FgInverseBrush}" BorderBrush="{StaticResource AccentBrush}" Style="{StaticResource ActionButton}" Width="300" HorizontalAlignment="Right"/>
                            <TextBlock Name="ActionFooterNote" Text="Settings save when setup begins." Foreground="{StaticResource FgMutedBrush}" FontSize="11" Margin="0,10,0,0" HorizontalAlignment="Right" TextWrapping="Wrap" MaxWidth="300" TextAlignment="Right"/>
                            <TextBlock Name="CompatibilityWarning" Foreground="#FFEAB308" FontSize="10.5" Margin="0,6,0,0" HorizontalAlignment="Right" TextWrapping="Wrap" MaxWidth="300" TextAlignment="Right" Visibility="Collapsed" AutomationProperties.Name="Compatibility warning"/>
                        </StackPanel>
                    </Grid>
                </Grid>
                        <!-- ===== INSTALL PAGE ===== -->
                        <Grid Name="PageInstall" Visibility="Collapsed"><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                            <Border Grid.Row="0" Style="{StaticResource SurfaceCard}" Margin="0,0,0,14">
                                <Grid>
                                    <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                                    <Grid>
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                        <StackPanel>
                                            <TextBlock Name="InstallTitle" Text="Preparing setup" Foreground="{StaticResource FgPrimaryBrush}" FontSize="20" FontWeight="SemiBold"/>
                                            <TextBlock Name="InstallContext" Text="LibreSpot keeps the interface responsive while it downloads, patches, and applies your selection." Foreground="{StaticResource FgSecondaryBrush}" FontSize="12.5" TextWrapping="Wrap" Margin="0,8,0,0" MaxWidth="760"/>
                                        </StackPanel>
                                        <StackPanel Grid.Column="1" VerticalAlignment="Top" Margin="20,0,0,0" HorizontalAlignment="Right">
                                            <TextBlock Text="Live log" Foreground="#FF86EFAC" FontSize="10.75" FontWeight="SemiBold" TextAlignment="Right"/>
                                            <TextBlock Text="Safe to minimize" Foreground="#FF93C5FD" FontSize="10.75" FontWeight="SemiBold" Margin="0,5,0,0" TextAlignment="Right"/>
                                        </StackPanel>
                                    </Grid>
                                    <Grid Grid.Row="1" Margin="0,16,0,0">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="10"/><ColumnDefinition Width="*"/><ColumnDefinition Width="10"/><ColumnDefinition Width="*"/><ColumnDefinition Width="10"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                        <Border Name="InstallStagePrepare" Grid.Column="0" Style="{StaticResource InsetPanel}" Padding="12,10">
                                            <TextBlock Name="InstallStagePrepareText" Text="Prepare" Foreground="{StaticResource FgPrimaryBrush}" FontSize="11.5" FontWeight="SemiBold"/>
                                        </Border>
                                        <Border Name="InstallStageRun" Grid.Column="2" Style="{StaticResource InsetPanel}" Padding="12,10">
                                            <TextBlock Name="InstallStageRunText" Text="Run" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" FontWeight="SemiBold"/>
                                        </Border>
                                        <Border Name="InstallStageVerify" Grid.Column="4" Style="{StaticResource InsetPanel}" Padding="12,10">
                                            <TextBlock Name="InstallStageVerifyText" Text="Verify" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" FontWeight="SemiBold"/>
                                        </Border>
                                        <Border Name="InstallStageComplete" Grid.Column="6" Style="{StaticResource InsetPanel}" Padding="12,10">
                                            <TextBlock Name="InstallStageCompleteText" Text="Complete" Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" FontWeight="SemiBold"/>
                                        </Border>
                                    </Grid>
                                </Grid>
                            </Border>
                            <Border Grid.Row="1" CornerRadius="8" BorderBrush="#FF25313D" BorderThickness="1" ClipToBounds="True"><Grid>
                                <Grid.RowDefinitions><RowDefinition Height="40"/><RowDefinition Height="*"/></Grid.RowDefinitions>
                                <Border Grid.Row="0" Background="#FF0F151D" BorderBrush="#FF25313D" BorderThickness="0,0,0,1" Padding="14,0"><Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                                        <Rectangle Width="3" Height="16" Fill="#FF22C55E" RadiusX="1.5" RadiusY="1.5" Margin="0,0,10,0"/>
                                        <TextBlock Text="Live setup log" Foreground="{StaticResource FgPrimaryBrush}" FontSize="11.5" FontWeight="SemiBold"/></StackPanel>
                                    <TextBlock Grid.Column="1" Text="Full log is saved and copyable." Foreground="{StaticResource FgMutedBrush}" FontSize="11" VerticalAlignment="Center"/></Grid></Border>
                                <Border Grid.Row="1" Background="#FF070A0F" Padding="16,14">
                                    <ScrollViewer Name="LogScroller" VerticalScrollBarVisibility="Auto" Style="{StaticResource DarkScrollViewer}">
                                        <TextBlock Name="LogOutput" Foreground="#FFC9D3DD" FontFamily="Cascadia Mono, Consolas, Courier New" FontSize="11.75" TextWrapping="Wrap" LineHeight="17"/>
                                    </ScrollViewer></Border>
                            </Grid></Border>
                            <Border Grid.Row="2" Style="{StaticResource SurfaceCard}" Margin="0,8,0,0"><StackPanel><Grid>
                                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
                                    <TextBlock Name="StatusText" Text="Checking prerequisites..." Foreground="{StaticResource FgPrimaryBrush}" FontSize="12.75" FontWeight="SemiBold"/>
                                    <TextBlock Name="ElapsedTime" Text="" Foreground="{StaticResource FgMutedBrush}" FontSize="11.5" VerticalAlignment="Center" Margin="14,0,0,0"/></StackPanel>
                                <StackPanel Grid.Column="1" Orientation="Horizontal" HorizontalAlignment="Right">
                                    <TextBlock Name="ProgressPercentText" Text="0%" Foreground="{StaticResource FgMutedBrush}" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center" Margin="0,0,12,0"/>
                                    <TextBlock Name="StepIndicator" Text="Ready when you are" Foreground="#FF22C55E" FontSize="12.75" FontWeight="SemiBold" HorizontalAlignment="Right"/>
                                </StackPanel></Grid>
                                <ProgressBar Name="MainProgress" Height="8" Margin="0,12,0,0" Template="{StaticResource RoundProgress}" Background="#FF25313D" Foreground="#FF22C55E" Minimum="0" Maximum="100" Value="0"/>
                                <Grid Margin="0,10,0,0">
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                    <Border Background="#FF111C2A" BorderBrush="#FF2E4964" BorderThickness="1" CornerRadius="6" Padding="9,3" VerticalAlignment="Top">
                                        <TextBlock Text="Last event" Foreground="#FF93C5FD" FontSize="10.5" FontWeight="SemiBold"/>
                                    </Border>
                                    <TextBlock Name="LastLogEventText" Grid.Column="1" Text="Waiting for setup to start." Foreground="{StaticResource FgSecondaryBrush}" FontSize="11.5" TextWrapping="Wrap" Margin="10,2,0,0"/>
                                </Grid>
                                <TextBlock Text="Stage markers update with the selected action. You can minimize LibreSpot while setup runs; it will return focus when action is needed or complete." Foreground="{StaticResource FgMutedBrush}" FontSize="11.5" TextWrapping="Wrap" Margin="0,8,0,0"/></StackPanel></Border>
                            <StackPanel Grid.Row="3" Margin="0,16,0,0" Orientation="Horizontal" HorizontalAlignment="Right">
                                <Button Name="BtnCopyLog" Content="Copy log" Tag="Copy log" Style="{StaticResource SecondaryActionButton}" Width="132" Margin="0,0,8,0" Visibility="Collapsed"/>
                                <Button Name="BtnBackToConfig" Content="Return to setup" Style="{StaticResource SecondaryActionButton}" Width="140" Margin="0,0,8,0" Visibility="Collapsed"/>
                                <Button Name="CloseBtn" Content="Close" Style="{StaticResource SecondaryActionButton}" Width="110" Visibility="Collapsed"/></StackPanel>
                        </Grid>
                    </Grid>
                </Grid>
            </Grid>
</Window>
"@

# =============================================================================
# 6. UI INITIALIZATION
# =============================================================================
try { $reader = New-Object System.Xml.XmlNodeReader ([xml]$xaml); $window = [Windows.Markup.XamlReader]::Load($reader) }
catch { Write-Error "XAML Failed: $($_.Exception.Message)"; Exit }
$ErrorActionPreference = 'Stop'

# Win11 Mica backdrop + dark titlebar + rounded corners. Quietly degrades to the
# solid SurfaceBase fallback baked into Window.Background on older Windows.
$script:MicaEnabled = $false
$window.Add_SourceInitialized({
    try {
        $hwnd = (New-Object System.Windows.Interop.WindowInteropHelper($window)).Handle
        if ($hwnd -ne [IntPtr]::Zero) {
            $script:MicaEnabled = [Win32]::TryEnableMicaBackdrop($hwnd)
            if ($script:MicaEnabled) {
                # Mica needs the WPF Window background to be transparent so the
                # DWM-rendered backdrop shows through; we keep cards opaque.
                $window.Background = [System.Windows.Media.Brushes]::Transparent
            }
        }
    } catch {}
    # High-contrast mode: override custom dark-theme brushes with SystemColors
    # so text, backgrounds, and borders remain readable. Mica is disabled under
    # high-contrast because the transparent backdrop makes text invisible.
    if ([System.Windows.SystemParameters]::HighContrast) {
        try {
            $script:MicaEnabled = $false
            $window.Background = [System.Windows.SystemColors]::WindowBrush
            $res = $window.Resources
            $res['SurfaceBaseBrush']     = [System.Windows.SystemColors]::WindowBrush
            $res['SurfaceElevatedBrush'] = [System.Windows.SystemColors]::WindowBrush
            $res['SurfaceElevated2Brush']= [System.Windows.SystemColors]::ControlBrush
            $res['SurfaceOverlayBrush']  = [System.Windows.SystemColors]::ControlBrush
            $res['SurfaceSidebarBrush']  = [System.Windows.SystemColors]::WindowBrush
            $res['BorderSubtleBrush']    = [System.Windows.SystemColors]::ControlDarkBrush
            $res['BorderStrongBrush']    = [System.Windows.SystemColors]::ActiveBorderBrush
            $res['AccentBrush']          = [System.Windows.SystemColors]::HighlightBrush
            $res['FgPrimaryBrush']       = [System.Windows.SystemColors]::WindowTextBrush
            $res['FgSecondaryBrush']     = [System.Windows.SystemColors]::WindowTextBrush
            $res['FgMutedBrush']         = [System.Windows.SystemColors]::GrayTextBrush
            $res['FgInverseBrush']       = [System.Windows.SystemColors]::HighlightTextBrush
        } catch {}
    }
})
function Get-LibreSpotBrandFrame {
    # Returns an ImageSource usable for both Window.Icon and Image.Source.
    # logo.png wins because the PNG renders crisper at the 44px sidebar tile
    # and dialog title bar than the multi-resolution .ico.
    $pngPath = Join-Path $script:ScriptRoot 'logo.png'
    if (Test-Path -LiteralPath $pngPath -PathType Leaf) {
        try {
            $bmp = New-Object System.Windows.Media.Imaging.BitmapImage
            $bmp.BeginInit()
            $bmp.UriSource = New-Object System.Uri($pngPath)
            $bmp.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
            $bmp.EndInit()
            if ($bmp.CanFreeze) { $bmp.Freeze() }
            return $bmp
        } catch {}
    }

    $icoCandidates = @(
        (Join-Path $script:ScriptRoot 'LibreSpot.ico'),
        (Join-Path $script:ScriptRoot 'icon.ico')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }

    foreach ($candidate in $icoCandidates) {
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
  'ModeHeadline','ModeSummaryText','SelectionSummaryCard','SelectionSummaryTitle','SelectionSummary','SelectionStateBadge','SelectionStateBadgeText','SelectionStateDetail','InstallTitle','InstallContext',
  'ModeEasy','ModeCustom','ModeMaint','PanelEasy','PanelCustom','PanelMaint','BtnInstall','BtnResetCustomDefaults','LyricsThemePanel',
  'CustomSnapshotPlanValue','CustomSnapshotThemeValue','CustomSnapshotExtensionsValue','CustomSnapshotMemoryValue',
  'CmbLocalProfiles','BtnProfilePreview','BtnProfileApply','TxtProfileName','BtnProfileSaveCurrent','ProfileStatusText',
  'ChkNewTheme','ChkPodcastsOff','ChkAdSectionsOff','ChkBlockUpdate','ChkPremium','ChkLyrics','CmbLyricsTheme',
  'ChkTopSearch','ChkRightSidebarOff','ChkRightSidebarColor','ChkCanvasHomeOff','ChkHomeSubOff','ChkOldLyrics','ChkHideColIconOff',
  'ChkPlus','ChkNewFullscreen','ChkFunnyProgress','ChkExpSpotify','ChkLyricsBlock',
  'ChkDisableStartup','ChkNoShortcut','ChkStartSpoti','TxtCacheLimit','CmbTheme','CmbScheme','PreviewBorder','ThemePreviewImg','PreviewLabel','ChkMarketplace','MarketplaceHealthNote',
  'ChkSendVersionOff','ChkDevTools','ChkMirror','ChkConfirmUninstall','CmbDownloadMethod','CmbSpotifyVersion','SpotifyVersionHint',
  'ChkExt_fullAppDisplay','ChkExt_shuffle','ChkExt_trashbin','ChkExt_keyboard','ChkExt_bookmark','ChkExt_loopyLoop',
  'ChkExt_popupLyrics','ChkExt_autoSkipVideo','ChkExt_autoSkipExplicit','ChkExt_webNowPlaying',
  'ChkExt_hidePodcasts','ChkExt_beautifulLyrics','ChkExt_playlistIcons','ChkExt_volumePercentage','ChkExt_adblock',
  'ChkCustomApp_stats',
  'ChkCleanInstall','ChkLaunchAfter',
  'MaintenanceOverviewTitle','MaintenanceOverviewText',
  'MaintenanceMetricStackValue','MaintenanceMetricStackDetail','MaintenanceMetricBackupValue','MaintenanceMetricBackupDetail','MaintenanceMetricNextStepValue','MaintenanceMetricNextStepDetail',
  'StatusCardSpotify','StatusCardSpotX','StatusCardSpicetify','StatusCardMarketplace','StatusCardTheme',
  'StatusSpotify','StatusSpotX','StatusSpicetify','StatusMarketplace','StatusTheme',
  'BtnBackupConfig','BtnRestoreConfig','BtnCheckUpdates','BtnRepairMarketplace','BtnReapply','BtnSafeMode','BtnSpicetifyRestore','BtnUninstallSpicetify','BtnFullReset',
  'ChkAutoReapply','AutoReapplyStatusText',
  'InstallStagePrepare','InstallStageRun','InstallStageVerify','InstallStageComplete',
  'InstallStagePrepareText','InstallStageRunText','InstallStageVerifyText','InstallStageCompleteText',
  'LogScroller','LogOutput','StatusText','ElapsedTime','ProgressPercentText','StepIndicator','LastLogEventText','MainProgress','BtnCopyLog','BtnBackToConfig','CloseBtn',
  'FooterActionPanel','ActionFooterNote','CompatibilityWarning','TitleText','TitleLogo','TitleBar'
) | ForEach-Object { $el = $window.FindName($_); if ($el) { $ui[$_] = $el } }

try {
    if ($ui.ContainsKey('TitleLogo') -and $script:BrandIconFrame) { $ui['TitleLogo'].Source = $script:BrandIconFrame }
} catch {}

function Update-LocalProfilePicker {
    if (-not $ui.ContainsKey('CmbLocalProfiles')) { return }
    try {
        $profiles = @(Get-LibreSpotProfiles | Sort-Object `
            @{ Expression = { if ($_.IsActive) { 0 } else { 1 } } },
            @{ Expression = { if ($_.IsBuiltIn) { 0 } else { 1 } } },
            @{ Expression = { [string]$_.Name } })
        $ui['CmbLocalProfiles'].Items.Clear()
        $activeIndex = -1
        for ($i = 0; $i -lt $profiles.Count; $i++) {
            $profileEntry = $profiles[$i]
            $item = New-Object System.Windows.Controls.ComboBoxItem
            $state = if ($profileEntry.IsActive) { 'Active' } elseif ($profileEntry.IsBuiltIn) { 'Template' } else { 'Local' }
            $item.Content = "$($profileEntry.Name) - $state"
            $item.Tag = [string]$profileEntry.Id
            $item.ToolTip = [string]$profileEntry.Description
            $null = $ui['CmbLocalProfiles'].Items.Add($item)
            if ($profileEntry.IsActive) { $activeIndex = $i }
        }
        if ($profiles.Count -gt 0) {
            $ui['CmbLocalProfiles'].SelectedIndex = if ($activeIndex -ge 0) { $activeIndex } else { 0 }
        }
        foreach ($controlName in @('BtnProfilePreview','BtnProfileApply')) {
            if ($ui.ContainsKey($controlName)) { $ui[$controlName].IsEnabled = ($profiles.Count -gt 0) }
        }
        if ($ui.ContainsKey('ProfileStatusText')) {
            if ($profiles.Count -gt 0) {
                $ui['ProfileStatusText'].Text = Get-LocalProfileStatusText -ProfileEntry (Get-SelectedLocalProfileFromUi)
            } else {
                $ui['ProfileStatusText'].Text = 'No profiles are available yet. Save the current Custom selections to create one.'
            }
        }
    } catch {
        foreach ($controlName in @('BtnProfilePreview','BtnProfileApply')) {
            if ($ui.ContainsKey($controlName)) { $ui[$controlName].IsEnabled = $false }
        }
        if ($ui.ContainsKey('ProfileStatusText')) { $ui['ProfileStatusText'].Text = "Profiles could not be loaded: $($_.Exception.Message)" }
    }
}

function Get-LocalProfileStatusText {
    param([object]$ProfileEntry)

    if (-not $ProfileEntry) {
        return 'Select a profile to preview or set active. Saving current creates a local profile.'
    }

    $description = [string]$ProfileEntry.Description
    if ($ProfileEntry.IsActive) {
        return "Active profile. Applying another profile keeps this as the rollback point. $description"
    }

    if ($ProfileEntry.IsBuiltIn) {
        return "Bundled template. Preview it first, then save a local copy if you want to customize it. $description"
    }

    return "Local profile. Preview it first to inspect settings without writing config.json. $description"
}

function Get-SelectedLocalProfileFromUi {
    if (-not $ui.ContainsKey('CmbLocalProfiles') -or -not $ui['CmbLocalProfiles'].SelectedItem) { return $null }
    $id = [string]$ui['CmbLocalProfiles'].SelectedItem.Tag
    return (Get-LibreSpotProfileById -Id $id)
}

function Select-LocalProfileInPicker {
    param([string]$Id)
    if (-not $ui.ContainsKey('CmbLocalProfiles')) { return }
    for ($i = 0; $i -lt $ui['CmbLocalProfiles'].Items.Count; $i++) {
        if ([string]$ui['CmbLocalProfiles'].Items[$i].Tag -eq [string]$Id) {
            $ui['CmbLocalProfiles'].SelectedIndex = $i
            return
        }
    }
}

function Set-StableConfigStateFromProfile {
    param([hashtable]$Config)
    $script:HasSavedConfig = $true
    $script:SavedConfigMode = if ($Config -and $Config.ContainsKey('Mode')) { [string]$Config.Mode } else { $null }
    $script:HasSavedCustomConfig = ($script:SavedConfigMode -eq 'Custom')
    $script:SavedConfigStamp = if (Test-Path $global:CONFIG_PATH) { (Get-Item $global:CONFIG_PATH).LastWriteTime } else { $null }
    Capture-CustomConfigBaseline
    Update-ModePresentation
}

$extCheckboxMap = [ordered]@{
    'ChkExt_fullAppDisplay'='fullAppDisplay.js'; 'ChkExt_shuffle'='shuffle+.js'; 'ChkExt_trashbin'='trashbin.js'
    'ChkExt_keyboard'='keyboardShortcut.js'; 'ChkExt_bookmark'='bookmark.js'; 'ChkExt_loopyLoop'='loopyLoop.js'
    'ChkExt_popupLyrics'='popupLyrics.js'; 'ChkExt_autoSkipVideo'='autoSkipVideo.js'
    'ChkExt_autoSkipExplicit'='autoSkipExplicit.js'; 'ChkExt_webNowPlaying'='webnowplaying.js'
    'ChkExt_hidePodcasts'='hidePodcasts.js'; 'ChkExt_beautifulLyrics'='beautiful-lyrics.mjs'
    'ChkExt_playlistIcons'='playlist-icons.js'
    'ChkExt_volumePercentage'='volumePercentage.js'
    'ChkExt_adblock'='adblock.js'
}

$customAppCheckboxMap = [ordered]@{
    'ChkCustomApp_stats'='stats'
}

foreach ($ck in $extCheckboxMap.Keys) {
    $ef = $extCheckboxMap[$ck]
    if ($ui[$ck] -and $global:BuiltInExtensions.Contains($ef)) { $ui[$ck].ToolTip = $global:BuiltInExtensions[$ef] }
    if ($ui[$ck] -and $global:CommunityExtensions.Contains($ef)) { $ui[$ck].ToolTip = $global:CommunityExtensions[$ef].Description }
}
foreach ($ck in $customAppCheckboxMap.Keys) {
    $appId = $customAppCheckboxMap[$ck]
    if ($ui[$ck] -and $global:CommunityCustomApps.Contains($appId)) { $ui[$ck].ToolTip = $global:CommunityCustomApps[$appId].Description }
}

foreach ($theme in $global:ThemeData.Keys) {
    $item = New-Object System.Windows.Controls.ComboBoxItem; $item.Content = $theme
    $item.Style = $window.FindResource("DarkComboBoxItem"); $ui['CmbTheme'].Items.Add($item) | Out-Null
}
# Theme preview image cache and loader
$script:previewCache = @{}
$script:previewRequestId = 0
function Update-ThemePreview {
    $themeName = if ($ui['CmbTheme'].SelectedItem) { $ui['CmbTheme'].SelectedItem.Content } else { $null }
    $schemeName = if ($ui['CmbScheme'].SelectedItem) { $ui['CmbScheme'].SelectedItem.Content } else { $null }
    if (-not $themeName -or $themeName -eq '(None - Marketplace Only)') {
        $ui['ThemePreviewImg'].Source = $null
        $ui['PreviewLabel'].Visibility = 'Visible'
        $ui['PreviewLabel'].Text = 'Marketplace-only keeps Spotify close to stock so you can browse themes later from inside the app.'
        $script:previewRequestId++
        return
    }
    $td = $global:ThemeData[$themeName]; if (-not $td -or -not $td.Preview -or $td.Preview.Count -eq 0) {
        $ui['ThemePreviewImg'].Source = $null
        $ui['PreviewLabel'].Visibility = 'Visible'
        $ui['PreviewLabel'].Text = "No bundled preview is available for $themeName."
        $script:previewRequestId++
        return
    }
    $imgPath = if ($schemeName -and $td.Preview.ContainsKey($schemeName)) { $td.Preview[$schemeName] }
               elseif ($td.Preview.ContainsKey('_default')) { $td.Preview['_default'] } else { $null }
    if (-not $imgPath) {
        $ui['ThemePreviewImg'].Source = $null
        $ui['PreviewLabel'].Visibility = 'Visible'
        $ui['PreviewLabel'].Text = "No bundled preview is available for $themeName."
        $script:previewRequestId++
        return
    }
    $url = if ($imgPath -match '^https?://') { $imgPath } else { "$global:THEMES_RAW_BASE/$imgPath" }
    if ($script:previewCache.ContainsKey($url)) {
        $ui['ThemePreviewImg'].Source = $script:previewCache[$url]; $ui['PreviewLabel'].Visibility = 'Collapsed'; return
    }
    $ui['ThemePreviewImg'].Source = $null; $ui['PreviewLabel'].Visibility = 'Visible'; $ui['PreviewLabel'].Text = "Loading the $themeName preview..."
    $script:previewRequestId++
    $requestId = $script:previewRequestId
    $maxBytes = 4 * 1024 * 1024
    $dispatcher = $sh.Dispatcher
    $cache = $script:previewCache
    [System.Threading.ThreadPool]::QueueUserWorkItem({
        param($state)
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
            $request = [System.Net.HttpWebRequest]::Create($state.Url)
            $request.UserAgent = 'LibreSpot'
            $request.Timeout = 15000
            $response = $request.GetResponse()
            try {
                if ($response.ContentLength -gt $state.MaxBytes) {
                    $response.Close()
                    throw "Preview image exceeds $([int]($state.MaxBytes / 1MB)) MB limit"
                }
                $stream = $response.GetResponseStream()
                $ms = New-Object System.IO.MemoryStream
                try {
                    $buffer = New-Object byte[] 8192
                    $total = 0
                    while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        $total += $read
                        if ($total -gt $state.MaxBytes) { throw "Preview image exceeds $([int]($state.MaxBytes / 1MB)) MB limit" }
                        $ms.Write($buffer, 0, $read)
                    }
                    $ms.Position = 0
                    $bmp = New-Object System.Windows.Media.Imaging.BitmapImage
                    $bmp.BeginInit()
                    $bmp.StreamSource = $ms
                    $bmp.CacheOption = [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
                    $bmp.DecodePixelWidth = 640
                    $bmp.EndInit()
                    $bmp.Freeze()
                    $state.Cache[$state.Url] = $bmp
                    $state.Dispatcher.Invoke([Action]{
                        if ($script:previewRequestId -eq $state.RequestId) {
                            $ui['ThemePreviewImg'].Source = $bmp
                            $ui['PreviewLabel'].Visibility = 'Collapsed'
                        }
                    })
                } finally { $ms.Dispose(); $stream.Dispose() }
            } finally { $response.Close() }
        } catch {
            $errorTheme = $state.ThemeName
            try {
                $state.Dispatcher.Invoke([Action]{
                    if ($script:previewRequestId -eq $state.RequestId) {
                        $ui['ThemePreviewImg'].Source = $null
                        $ui['PreviewLabel'].Visibility = 'Visible'
                        $ui['PreviewLabel'].Text = "Preview unavailable for $errorTheme right now."
                    }
                })
            } catch {}
        }
    }, @{ Url = $url; RequestId = $requestId; ThemeName = $themeName; MaxBytes = $maxBytes; Dispatcher = $dispatcher; Cache = $cache }) | Out-Null
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
    if ($savedCfg.ContainsKey('Spicetify_CustomApps')) {
        $sca = @($savedCfg.Spicetify_CustomApps)
        foreach ($ck in $customAppCheckboxMap.Keys) { $ui[$ck].IsChecked = ($sca -contains $customAppCheckboxMap[$ck]) }
    }
    if ($savedCfg.ContainsKey('Mode') -and [string]$savedCfg.Mode -eq 'Custom') {
        $ui['ModeCustom'].IsChecked = $true
    }
} catch { Write-Log "Config restore warning: some saved settings could not be applied to the UI. Defaults will be shown. Error: $($_.Exception.Message)" -Level 'WARN' } }
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
        [ValidateSet('success','info','warning','muted','danger')]
        [string]$Tone = 'muted'
    )
    if (-not $ui.ContainsKey($CardName)) { return }
    $palette = switch ($Tone) {
        'success' { @{ Background = '#FF111A22'; Border = '#FF2D5A3F' } }
        'info'    { @{ Background = '#FF111C2A'; Border = '#FF2E4964' } }
        'warning' { @{ Background = '#FF211A0E'; Border = '#FF6B4E16' } }
        'danger'  { @{ Background = '#FF2B1117'; Border = '#FFEF4444' } }
        default   { @{ Background = '#FF111821'; Border = '#FF25313D' } }
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
        'success' { @{ Background = '#FF111A22'; Border = '#FF2D5A3F'; Foreground = '#FF86EFAC'; Detail = '#FFA6B0BB' } }
        'warning' { @{ Background = '#FF211A0E'; Border = '#FF6B4E16'; Foreground = '#FFFCD34D'; Detail = '#FFA6B0BB' } }
        'danger'  { @{ Background = '#FF2B1117'; Border = '#FFEF4444'; Foreground = '#FFFFE4E6'; Detail = '#FFA6B0BB' } }
        'muted'   { @{ Background = '#FF111821'; Border = '#FF25313D'; Foreground = '#FFA6B0BB'; Detail = '#FF778390' } }
        default   { @{ Background = '#FF111C2A'; Border = '#FF2E4964'; Foreground = '#FF93C5FD'; Detail = '#FFA6B0BB' } }
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
        'active'    { @{ Background = '#FF111C2A'; Border = '#FF3D6A8A'; Foreground = '#FFE6F3FF' } }
        'complete'  { @{ Background = '#FF111A22'; Border = '#FF2D5A3F'; Foreground = '#FF86EFAC' } }
        'attention' { @{ Background = '#FF2B1117'; Border = '#FFEF4444'; Foreground = '#FFFFE4E6' } }
        default     { @{ Background = '#FF111821'; Border = '#FF25313D'; Foreground = '#FFA6B0BB' } }
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

function Set-WindowChromeHints {
    param(
        [ValidateSet('Idle','Running','Complete')]
        [string]$State = 'Idle'
    )

    if ($ui.ContainsKey('MinimizeBtn')) {
        $ui['MinimizeBtn'].ToolTip = if ($State -eq 'Running') { 'Minimize LibreSpot while the current action continues.' } else { 'Minimize' }
    }
    if ($ui.ContainsKey('CloseTitleBtn')) {
        $ui['CloseTitleBtn'].ToolTip = switch ($State) {
            'Running'  { 'Setup is running. Closing will ask before interrupting it.' }
            'Complete' { 'Close LibreSpot' }
            default    { 'Close' }
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
        SpotX_Language         = [string]$Config.SpotX_Language
        Spicetify_Theme        = [string]$Config.Spicetify_Theme
        Spicetify_Scheme       = [string]$Config.Spicetify_Scheme
        Spicetify_Marketplace  = [bool]$Config.Spicetify_Marketplace
        Spicetify_Extensions   = @($Config.Spicetify_Extensions)
        Spicetify_CustomApps   = @($Config.Spicetify_CustomApps)
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

    $selectedCustomApps = @($normalized.Spicetify_CustomApps)
    foreach ($key in $customAppCheckboxMap.Keys) {
        if ($ui.ContainsKey($key)) {
            $ui[$key].IsChecked = ($selectedCustomApps -contains $customAppCheckboxMap[$key])
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
    $selectionSummarySpan = if ($isMaint) { 3 } else { 1 }
    if ($ui.ContainsKey('SelectionSummaryCard')) { [System.Windows.Controls.Grid]::SetColumnSpan($ui['SelectionSummaryCard'], $selectionSummarySpan) }

    if ($isEasy) {
        if ($ui.ContainsKey('SelectionSummaryTitle')) { $ui['SelectionSummaryTitle'].Text = 'Install snapshot' }
        $ui['ModeHeadline'].Text = 'Recommended path for a first install'
        $ui['ModeSummaryText'].Text = 'LibreSpot handles cleanup, verified downloads, Spotify patching, Marketplace, and a reliable default extension set with recovery-friendly defaults.'
        $ui['SelectionSummary'].Text = 'Recommended setup: clean install, Marketplace included, three starter extensions, and Spotify opens when everything is ready.'
        Set-SelectionSnapshotState -Tone 'success' -BadgeText 'Pinned default stack' -DetailText 'Recommended setup uses the verified cleanup path and saves the default recovery baseline when setup begins.'
        if ($ui.ContainsKey('ActionFooterNote')) { $ui['ActionFooterNote'].Text = 'Recommended defaults save when setup begins.' }
        $ui['BtnInstall'].Content = 'Start recommended setup'
        Update-CompatibilityWarningBadge
        return
    }

    if ($isCustom) {
        if ($ui.ContainsKey('SelectionSummaryTitle')) { $ui['SelectionSummaryTitle'].Text = 'Custom snapshot' }
        $theme = Get-ComboSelectionText -Name 'CmbTheme' -Fallback '(None - Marketplace Only)'
        $scheme = Get-ComboSelectionText -Name 'CmbScheme' -Fallback 'Default'
        $themeLabel = if ($theme -eq '(None - Marketplace Only)') { 'Marketplace only' } elseif ($scheme -and $scheme -ne 'Default') { "$theme / $scheme" } else { $theme }
        $extCount = @($extCheckboxMap.Keys | Where-Object { $ui[$_].IsChecked }).Count
        $extLabel = if ($extCount -eq 1) { '1 extension' } else { "$extCount extensions" }
        $installLabel = if ($ui['ChkCleanInstall'].IsChecked) { 'clean install' } else { 'keep current Spotify install' }
        $marketplaceLabel = if ($ui['ChkMarketplace'].IsChecked) { 'Marketplace included' } else { 'Marketplace skipped' }
        if ($ui.ContainsKey('MarketplaceHealthNote')) {
            if ([bool]$ui['ChkMarketplace'].IsChecked) {
                $ui['MarketplaceHealthNote'].Text = 'Note: Spicetify Marketplace may reset installed themes/extensions when Spotify closes (upstream issue spicetify/cli#3837). Your selected themes and extensions above are installed directly by LibreSpot and are not affected.'
                $ui['MarketplaceHealthNote'].Visibility = 'Visible'
            } else {
                $ui['MarketplaceHealthNote'].Visibility = 'Collapsed'
            }
        }
        $launchLabel = if ($ui['ChkLaunchAfter'].IsChecked) { 'launches Spotify when finished' } else { 'keeps Spotify closed when finished' }
        $savedStampText = if ($script:SavedConfigStamp) { $script:SavedConfigStamp.ToString('MMM d, yyyy h:mm tt') } else { $null }
        $hasUnsavedCustomChanges = Test-HasUnsavedCustomChanges
        $memoryNote = if ($script:HasSavedCustomConfig) {
            if ($savedStampText) { " Your previous custom choices were restored from disk. Last saved $savedStampText." } else { ' Your previous custom choices were restored from disk.' }
        } elseif ($script:SavedConfigMode -eq 'Easy') {
            ' Recommended setup was the last saved mode. These custom choices will be remembered after your first custom setup run.'
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
            $snapshotBadge = 'Switching from Recommended'
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
        Update-CompatibilityWarningBadge
        return
    }

    $ui['ModeHeadline'].Text = 'Recover, reapply, or clean up'
    if ($ui.ContainsKey('SelectionSummaryTitle')) { $ui['SelectionSummaryTitle'].Text = 'Maintenance snapshot' }
    Update-MaintenanceStatus
    $componentLabel = if ($script:MaintenanceComponentCount -eq 1) { '1 core component detected' } else { "$($script:MaintenanceComponentCount) core components detected" }
    $backupLabel = if ($script:MaintenanceBackupCount -eq 0) { 'no backups saved yet' } elseif ($script:MaintenanceBackupCount -eq 1) { '1 backup ready' } else { "$($script:MaintenanceBackupCount) backups ready" }
    $ui['ModeSummaryText'].Text = 'Inspect what is installed, restore backups, reapply pinned patches after Spotify updates, or roll the setup back cleanly.'
    $ui['SelectionSummary'].Text = "Current state: $componentLabel, $backupLabel, and destructive actions stay behind confirmation."
}

function Clear-CompletedRunspaceResources {
    if ($script:activeSyncHash -and $script:activeSyncHash.IsRunning) { return $false }
    if ($script:activeSyncHash) {
        $script:activeSyncHash.IsRunning = $false
    }
    $pending = @($script:openRunspaces)
    $script:openRunspaces.Clear()
    if ($script:activeSyncHash -and -not $script:activeSyncHash.IsRunning) {
        $script:activeSyncHash = $null
    }
    # CloseAsync is non-blocking — avoids deadlock when a worker runspace is
    # stuck inside Dispatcher.Invoke while the UI thread is in Add_Closing.
    foreach ($resource in $pending) {
        try { $resource.CloseAsync() } catch {}
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
$script:copyLogDefaultText = 'Copy log'
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
            $ui['BtnCopyLog'].Content = if ($ui['BtnCopyLog'].Tag) { [string]$ui['BtnCopyLog'].Tag } else { $script:copyLogDefaultText }
        })
        $script:copyResetTimer.Start()
    } catch {
        $ui['BtnCopyLog'].Content = 'Copy unavailable'
        if ($script:copyResetTimer) { $script:copyResetTimer.Stop() }
        $script:copyResetTimer = New-Object System.Windows.Threading.DispatcherTimer
        $script:copyResetTimer.Interval = [TimeSpan]::FromSeconds(2.5)
        $script:copyResetTimer.Add_Tick({
            $script:copyResetTimer.Stop()
            $ui['BtnCopyLog'].Content = if ($ui['BtnCopyLog'].Tag) { [string]$ui['BtnCopyLog'].Tag } else { $script:copyLogDefaultText }
        })
        $script:copyResetTimer.Start()
    }
})
if ($ui.ContainsKey('BtnResetCustomDefaults')) {
    $ui['BtnResetCustomDefaults'].Add_Click({
        $result = Show-ThemedDialog -Title 'Load recommended defaults' -Message 'LibreSpot will load the Recommended setup defaults into Custom Install so you can keep tweaking from a known-good baseline.' -Buttons 'YesNo' -Icon 'Question' -PrimaryText 'Load defaults' -SecondaryText 'Cancel'
        if ($result -ne 'Yes') { return }
        $preset = @{ Mode = 'Custom' }
        foreach ($key in $global:EasyDefaults.Keys) { $preset[$key] = $global:EasyDefaults[$key] }
        Apply-ConfigToUi -Config $preset -ForceCustomMode
    })
}
if ($ui.ContainsKey('CmbLocalProfiles')) {
    $ui['CmbLocalProfiles'].Add_SelectionChanged({
        $selectedProfile = Get-SelectedLocalProfileFromUi
        if ($selectedProfile -and $ui.ContainsKey('ProfileStatusText')) {
            $ui['ProfileStatusText'].Text = Get-LocalProfileStatusText -ProfileEntry $selectedProfile
        }
    })
}
if ($ui.ContainsKey('BtnProfilePreview')) {
    $ui['BtnProfilePreview'].Add_Click({
        $selectedProfile = Get-SelectedLocalProfileFromUi
        if (-not $selectedProfile) { return }
        Apply-ConfigToUi -Config $selectedProfile.Configuration -ForceCustomMode
        if ($ui.ContainsKey('ProfileStatusText')) { $ui['ProfileStatusText'].Text = "Previewing $($selectedProfile.Name) in Custom. config.json was not changed." }
        Update-ModePresentation
    })
}
if ($ui.ContainsKey('BtnProfileSaveCurrent')) {
    $ui['BtnProfileSaveCurrent'].Add_Click({
        try {
            $name = if ($ui.ContainsKey('TxtProfileName')) { [string]$ui['TxtProfileName'].Text } else { 'Custom profile' }
            $savedProfile = Save-LibreSpotLocalProfile -Name $name -Description 'Saved from PowerShell Custom mode.' -Configuration (Get-InstallConfig -EasyMode $false)
            Update-LocalProfilePicker
            Select-LocalProfileInPicker -Id $savedProfile.Id
            if ($ui.ContainsKey('ProfileStatusText')) { $ui['ProfileStatusText'].Text = "Saved $($savedProfile.Name) as a local profile. Preview or set it active when ready." }
        } catch {
            Show-ThemedDialog -Title 'Could not save profile' -Message $_.Exception.Message -Icon 'Error' -PrimaryText 'Close' | Out-Null
        }
    })
}
if ($ui.ContainsKey('BtnProfileApply')) {
    $ui['BtnProfileApply'].Add_Click({
        $selectedProfile = Get-SelectedLocalProfileFromUi
        if (-not $selectedProfile) { return }
        $result = Show-ThemedDialog -Title "Set active profile" -Message "LibreSpot will write '$($selectedProfile.Name)' to config.json and keep the previous active profile pointer for rollback. This does not start setup by itself." -Buttons 'YesNo' -Icon 'Question' -PrimaryText 'Set active' -SecondaryText 'Cancel'
        if ($result -ne 'Yes') { return }
        try {
            $applied = Apply-LibreSpotProfile -Id $selectedProfile.Id
            Apply-ConfigToUi -Config $applied.Configuration -ForceCustomMode
            Set-StableConfigStateFromProfile -Config $applied.Configuration
            Update-LocalProfilePicker
            Select-LocalProfileInPicker -Id $applied.Id
            if ($ui.ContainsKey('ProfileStatusText')) { $ui['ProfileStatusText'].Text = "$($applied.Name) is active. The previous active profile pointer is kept for rollback." }
        } catch {
            Show-ThemedDialog -Title 'Could not set profile active' -Message $_.Exception.Message -Icon 'Error' -PrimaryText 'Close' | Out-Null
        }
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
    if ($ui.ContainsKey('LastLogEventText')) { $ui['LastLogEventText'].Text='Waiting for setup to start.' }
    $ui['ElapsedTime'].Text=''
    if ($ui.ContainsKey('ProgressPercentText')) { $ui['ProgressPercentText'].Text='0%' }
    $ui['MainProgress'].Value=0
    $ui['MainProgress'].Foreground=$global:BrushGreen
    if ($script:copyResetTimer) { $script:copyResetTimer.Stop() }
    $script:copyLogDefaultText='Copy log'
    $ui['BtnCopyLog'].Tag=$script:copyLogDefaultText
    $ui['BtnCopyLog'].Content=$script:copyLogDefaultText
    $ui['BtnCopyLog'].Visibility='Collapsed'
    $ui['CloseBtn'].Visibility='Collapsed'
    $ui['BtnBackToConfig'].Visibility='Collapsed'
    $window.Topmost=$false
    Set-WindowChromeHints -State 'Idle'
    Update-InstallStageVisual
    Update-MaintenanceStatus
    Update-ModePresentation
})
Set-WindowChromeHints -State 'Idle'
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

# Apply -Clean CLI flag: pre-tick Recommended setup + CleanInstall so users get a
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
    # Start from the saved config so config-only settings with no UI control
    # (RiskAcknowledged, UiCulture, SpotX_Language, SpotX_CustomPatchesEnabled/
    # Json) survive UI-driven saves. Every UI-editable key is overwritten
    # below, so this changes nothing about what the controls produce.
    $saved = @{}
    try {
        $loaded = Load-LibreSpotConfig
        if ($loaded) { foreach ($k in $loaded.Keys) { $saved[$k] = $loaded[$k] } }
    } catch {}
    if ($EasyMode) {
        $c = $saved; $c['Mode'] = 'Easy'
        foreach ($k in $global:EasyDefaults.Keys) { $c[$k]=$global:EasyDefaults[$k] }
        return $c
    }
    $lTheme = if($ui['CmbLyricsTheme'].SelectedItem){$ui['CmbLyricsTheme'].SelectedItem.Content}else{'spotify'}
    $sTheme = if($ui['CmbTheme'].SelectedItem){$ui['CmbTheme'].SelectedItem.Content}else{'(None - Marketplace Only)'}
    $sScheme = if($ui['CmbScheme'].SelectedItem){$ui['CmbScheme'].SelectedItem.Content}else{'Default'}
    $dlMethod = if($ui.ContainsKey('CmbDownloadMethod') -and $ui['CmbDownloadMethod'].SelectedItem){[string]$ui['CmbDownloadMethod'].SelectedItem.Content}else{'auto'}
    if ($dlMethod -eq 'auto') { $dlMethod = '' }
    $spotifyVerId = if ($ui.ContainsKey('CmbSpotifyVersion') -and $ui['CmbSpotifyVersion'].SelectedItem) { [string]$ui['CmbSpotifyVersion'].SelectedItem.Tag } else { 'auto' }
    $cacheVal = 0; try { $cacheVal = [int]$ui['TxtCacheLimit'].Text } catch {}
    if ($cacheVal -lt 0) { $cacheVal = 0 }
    $exts = @(); foreach ($k in $extCheckboxMap.Keys) { if ($ui[$k].IsChecked) { $exts += $extCheckboxMap[$k] } }
    $customApps = @(); foreach ($k in $customAppCheckboxMap.Keys) { if ($ui[$k].IsChecked) { $customApps += $customAppCheckboxMap[$k] } }
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
        Spicetify_CustomApps=$customApps
        # Maintenance-mode control but persisted in the shared config so both
        # Easy and Custom saves carry the preference forward.
        AutoReapply_Enabled = if ($ui.ContainsKey('ChkAutoReapply')) { [bool]$ui['ChkAutoReapply'].IsChecked } else { $false }
    }
    foreach ($k in $c.Keys) { $saved[$k] = $c[$k] }
    return $saved
}

Capture-CustomConfigBaseline
Update-LocalProfilePicker
Update-ModePresentation

# Post-launch housekeeping. Network probes run async without executing cmdlet
# pipelines inside raw ThreadPool delegates; UI and cache writes marshal back
# to the dispatcher. Foreign-patch detection is filesystem-only and stays on
# the dispatcher at idle priority so the warning dialog doesn't appear before
# the main window has finished painting.
try {
    Start-SelfUpdateBannerRefresh
    Start-UpstreamStalenessNoticeRefresh
    $null = $window.Dispatcher.BeginInvoke([System.Windows.Threading.DispatcherPriority]::ApplicationIdle, [System.Action]{
        try { Test-ForeignPatchWarningIfNeeded } catch {}
    })
} catch {}

# SECURITY: see SECURITY.md "External process execution contract". $Config MUST
# be a Normalize-LibreSpotConfig output: the only interpolated values here are
# SpotX_LyricsTheme (allowlist), SpotX_DownloadMethod (allowlist),
# SpotX_Language (allowlist), SpotX_CacheLimit (integer), and a manifest-supplied
# version. Do NOT interpolate any new free-form/user value into this string
# without normalizing it first.
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
    if ($Config.SpotX_LyricsEnabled) {
        $p += "-lyrics_stat $($Config.SpotX_LyricsTheme)"
        if ($Config.SpotX_LyricsBlock) {
            $p += "-lyrics_block"
        } elseif ($Config.SpotX_OldLyrics) {
            $p += "-old_lyrics"
        }
    }
    if ($Config.SpotX_TopSearch)       { $p += "-topsearchbar" }
    if ($Config.SpotX_RightSidebarOff) { $p += "-rightsidebar_off" }
    if ($Config.SpotX_RightSidebarClr) { $p += "-rightsidebarcolor" }
    if ($Config.SpotX_CanvasHomeOff)   { $p += "-canvashome_off" }
    if ($Config.SpotX_HomeSubOff)      { $p += "-homesub_off" }
    if ($Config.SpotX_HideColIconOff)  { $p += "-hide_col_icon_off" }
    if ($Config.SpotX_Plus)             { $p += "-plus" }
    if ($Config.SpotX_NewFullscreen)    { $p += "-newFullscreenMode" }
    if ($Config.SpotX_FunnyProgress)    { $p += "-funnyprogressBar" }
    if ($Config.SpotX_ExpSpotify)       { $p += "-exp_spotify" }
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
    if (-not [string]::IsNullOrWhiteSpace([string]$Config.SpotX_Language)) {
        $p += "-language $($Config.SpotX_Language)"
    }
    return ($p -join " ")
}

# NOTE: the -Watch CLI exit point lives just before section 19 (LAUNCH). The
# reapply pipeline (Invoke-HeadlessReapply) calls Get-FromAssetCache,
# Save-ToAssetCache, Invoke-SpicetifyCli, and Test-SpicetifyCliInstalled —
# functions defined in later sections — so the exit must run after ALL
# function definitions. An earlier placement here made every actual reapply
# tick die with CommandNotFound while "nothing changed" ticks kept passing.

function Get-SpicetifyIntegrationContext {
    $version = if ($global:SPICETIFY_INTEGRATION_VERSION) { [string]$global:SPICETIFY_INTEGRATION_VERSION } else { 'v2' }
    if ($version -notin @('v2','v3-preview')) {
        throw "Unsupported Spicetify integration version '$version'."
    }

    $installDir = [string]$global:SPICETIFY_DIR
    $configDir = [string]$global:SPICETIFY_CONFIG_DIR
    return [pscustomobject]@{
        Version                    = $version
        InstallDirectory           = $installDir
        ConfigDirectory            = $configDir
        CliPath                    = Join-Path $installDir 'spicetify.exe'
        ConfigPath                 = Join-Path $configDir 'config-xpui.ini'
        ThemesDirectory            = Join-Path $configDir 'Themes'
        ExtensionsDirectory        = Join-Path $configDir 'Extensions'
        CustomAppsDirectory        = Join-Path $configDir 'CustomApps'
        MarketplaceDirectory       = Join-Path $configDir 'CustomApps\marketplace'
        LegacyMarketplaceDirectory = Join-Path $installDir 'CustomApps\marketplace'
    }
}

function Get-SpicetifyConfigEntries {
    $configPath = (Get-SpicetifyIntegrationContext).ConfigPath
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

function Get-MarketplaceHealth {
    $integration = Get-SpicetifyIntegrationContext
    $configDir = $integration.MarketplaceDirectory
    $legacyDir = $integration.LegacyMarketplaceDirectory
    $activeDir = if (Test-Path -LiteralPath $configDir -PathType Container) { $configDir } elseif (Test-Path -LiteralPath $legacyDir -PathType Container) { $legacyDir } else { $configDir }
    $hasConfigDir = Test-Path -LiteralPath $configDir -PathType Container
    $hasLegacyDir = Test-Path -LiteralPath $legacyDir -PathType Container
    $hasExtension = Test-Path -LiteralPath (Join-Path $activeDir 'extension.js') -PathType Leaf
    $hasManifest = Test-Path -LiteralPath (Join-Path $activeDir 'manifest.json') -PathType Leaf
    $isEnabled = @(Get-SpicetifyConfigListValue -Key 'custom_apps') -contains 'marketplace'
    $hasFiles = $hasExtension -and $hasManifest

    $status = if ($hasConfigDir -and $hasFiles -and $isEnabled) {
        'Ready'
    } elseif ($hasConfigDir -and $hasFiles -and -not $isEnabled) {
        'Hidden'
    } elseif ($isEnabled -and -not $hasFiles) {
        'FilesMissing'
    } elseif ($hasLegacyDir -and -not $hasConfigDir) {
        'LegacyPath'
    } else {
        'Missing'
    }

    return [pscustomobject]@{
        Status       = $status
        Path         = $activeDir
        HasConfigDir = $hasConfigDir
        HasLegacyDir = $hasLegacyDir
        HasFiles     = $hasFiles
        IsEnabled    = $isEnabled
        IsReady      = ($status -eq 'Ready')
        NeedsRepair  = ($status -in @('Hidden','FilesMissing','LegacyPath','Missing'))
    }
}

function ConvertTo-NativeArgumentString {
    param([string[]]$Arguments)

    $parts = @()
    foreach ($argument in @($Arguments)) {
        $value = if ($null -eq $argument) { '' } else { [string]$argument }
        if ($value.Length -gt 0 -and $value -notmatch '[\s"]') {
            $parts += $value
            continue
        }

        $builder = New-Object System.Text.StringBuilder
        [void]$builder.Append('"')
        $backslashes = 0
        foreach ($character in $value.ToCharArray()) {
            if ($character -eq [char]92) {
                $backslashes++
                continue
            }
            if ($character -eq [char]34) {
                if ($backslashes -gt 0) {
                    [void]$builder.Append(('\' * ($backslashes * 2)))
                    $backslashes = 0
                }
                [void]$builder.Append('\"')
                continue
            }
            if ($backslashes -gt 0) {
                [void]$builder.Append(('\' * $backslashes))
                $backslashes = 0
            }
            [void]$builder.Append($character)
        }
        if ($backslashes -gt 0) {
            [void]$builder.Append(('\' * ($backslashes * 2)))
        }
        [void]$builder.Append('"')
        $parts += $builder.ToString()
    }

    return ($parts -join ' ')
}

function Remove-ConsoleEscapeSequences {
    param([string]$Text)

    if ($null -eq $Text) { return '' }
    $escapePattern = [regex]::Escape([string][char]27) + '\[[0-?]*[ -/]*[@-~]'
    return [regex]::Replace([string]$Text, $escapePattern, '')
}

function Update-SpicetifyCliProgress {
    param([string]$Line)

    $plain = Remove-ConsoleEscapeSequences -Text $Line
    $sh = $script:syncHash
    if (-not $sh -or -not $sh.Dispatcher) { return }
    if ($plain -match 'Patching files\s*\[\s*(\d+)\s*/\s*(\d+)\s*\]') {
        $done = [int]$matches[1]
        $total = [Math]::Max(1, [int]$matches[2])
        $percent = [int][Math]::Min(99, [Math]::Floor(($done / $total) * 100))
        $progressValue = [int][Math]::Min(99, 86 + [Math]::Floor(($done / $total) * 12))
        $progressBar = $sh.ProgressBar
        $statusLabel = $sh.StatusLabel
        $stepLabel = $sh.StepLabel
        $installContext = $sh.InstallContext
        try {
            $sh.Dispatcher.Invoke([Action]{
                try {
                    if ($progressBar -and $progressBar.Value -lt $progressValue) { $progressBar.Value = $progressValue }
                    if ($statusLabel) { $statusLabel.Text = "Spicetify is patching Spotify files ($percent%)" }
                    if ($stepLabel) { $stepLabel.Text = "Applying setup: patching file $done of $total" }
                    if ($installContext) { $installContext.Text = "Spicetify is rebuilding Spotify's UI package. This can take several minutes on slower disks." }
                } catch {}
            }) | Out-Null
        } catch {}
    } elseif ($plain -match 'Extracting backup|Preprocessing|Fetching remote CSS map|Patching files') {
        $statusLabel = $sh.StatusLabel
        $installContext = $sh.InstallContext
        try {
            $sh.Dispatcher.Invoke([Action]{
                try {
                    if ($statusLabel) { $statusLabel.Text = 'Spicetify is preparing Spotify files' }
                    if ($installContext) { $installContext.Text = "Spicetify is rebuilding Spotify's UI package. This can take several minutes on slower disks." }
                } catch {}
            }) | Out-Null
        } catch {}
    }
}

function Write-SpicetifyCliOutputLine {
    param(
        [string]$Line,
        [hashtable]$ProgressState
    )

    $plain = Remove-ConsoleEscapeSequences -Text $Line
    $plain = [regex]::Replace($plain, '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]', '')
    $plain = [regex]::Replace($plain, '\s+', ' ').Trim()
    if ([string]::IsNullOrWhiteSpace($plain)) { return $null }

    if ($plain -match 'Patching files\s*\[\s*0*(\d+)\s*/\s*0*(\d+)\s*\]') {
        $done = [int]$matches[1]
        $total = [Math]::Max(1, [int]$matches[2])
        $percent = [int][Math]::Min(100, [Math]::Floor(($done / $total) * 100))
        $bucket = if ($percent -ge 100) { 100 } else { [int]([Math]::Floor($percent / 10) * 10) }

        if ($ProgressState -and (-not $ProgressState.ContainsKey('LastUiPatchPercent') -or [int]$ProgressState['LastUiPatchPercent'] -ne $percent)) {
            Update-SpicetifyCliProgress -Line $plain
            $ProgressState['LastUiPatchPercent'] = $percent
        }

        $shouldLog = (-not $ProgressState) -or (-not $ProgressState.ContainsKey('LastPatchBucket')) -or ([int]$ProgressState['LastPatchBucket'] -ne $bucket)
        if ($shouldLog) {
            if ($ProgressState) { $ProgressState['LastPatchBucket'] = $bucket }
            $message = "Patching files: $done/$total ($percent%)"
            Write-Log "  $message"
            return $message
        }
        return $null
    }

    if ($plain -match '^(?:[-\\|/]\s*)?(Backing up app files|Extracting backup|Fetching remote CSS map|Copying raw assets|Updating theme''s styles|Applying additional modifications|Refreshing extensions|Refreshing custom apps)$') {
        $stage = $matches[1]
        Update-SpicetifyCliProgress -Line $stage
        if ((-not $ProgressState) -or (-not $ProgressState.ContainsKey('LastStage')) -or ([string]$ProgressState['LastStage'] -ne $stage)) {
            if ($ProgressState) { $ProgressState['LastStage'] = $stage }
            Write-Log "  $stage"
            return $stage
        }
        return $null
    }

    Update-SpicetifyCliProgress -Line $plain
    Write-Log "  $plain"
    return $plain
}

function Invoke-SpicetifyCli {
    param(
        [string[]]$Arguments,
        [string]$FailureMessage = 'Spicetify command failed.',
        [int]$TimeoutSeconds = 900,
        [int]$IdleTimeoutSeconds = 90
    )
    $integration = Get-SpicetifyIntegrationContext
    $spicetifyExe = $integration.CliPath
    if (-not (Test-Path -LiteralPath $spicetifyExe)) {
        throw 'Spicetify CLI is not installed.'
    }

    $progressState = @{ LastPatchBucket = -1; LastUiPatchPercent = -1; LastStage = '' }
    $outputLines = [System.Collections.Generic.List[string]]::new()
    $process = $null
    $collector = $null

    # Keep PowerShell from turning redirected native stderr into its own
    # terminating error. The .NET process object avoids PowerShell handle
    # bugs seen with redirected files while a C# collector drains both streams
    # without running PowerShell scriptblocks on process output threads.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $argumentString = ConvertTo-NativeArgumentString -Arguments $Arguments
        $displayArguments = ($Arguments | ForEach-Object { [string]$_ }) -join ' '
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $spicetifyExe
        $startInfo.Arguments = $argumentString
        $startInfo.WorkingDirectory = $integration.InstallDirectory
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        $collector = New-Object LibreSpotNativeOutputCollector
        $collector.Attach($process)

        $null = $process.Start()
        Write-Log "  Spicetify ($($integration.Version)) command: spicetify $displayArguments"
        Write-Log "  Spicetify PID: $($process.Id)"
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        $startedAt = Get-Date
        $lastOutputAt = $startedAt
        $lastHeartbeatAt = $startedAt
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $statusIntervalSeconds = if ($IdleTimeoutSeconds -gt 0) { [Math]::Min([Math]::Max($IdleTimeoutSeconds, 5), 15) } else { 15 }
        $heartbeatSeconds = [Math]::Min($statusIntervalSeconds, 10)

        $drainOutput = {
            $count = 0
            [string]$queuedLine = $null
            while ($collector.TryDequeue([ref]$queuedLine)) {
                if (-not [string]::IsNullOrWhiteSpace($queuedLine)) {
                    $processed = Write-SpicetifyCliOutputLine -Line $queuedLine -ProgressState $progressState
                    if ($processed) { [void]$outputLines.Add($processed) }
                    $count++
                }
                $queuedLine = $null
            }
            return $count
        }

        $getTail = {
            if ($outputLines.Count -le 0) { return '' }
            $start = [Math]::Max(0, $outputLines.Count - 4)
            $slice = for ($i = $start; $i -lt $outputLines.Count; $i++) { $outputLines[$i] }
            return ' Output: ' + ((($slice | ForEach-Object { Remove-ConsoleEscapeSequences -Text $_ }) -replace '\s+', ' ') -join ' | ')
        }

        while (-not $process.WaitForExit(250)) {
            $drained = & $drainOutput
            if ($drained -gt 0) { $lastOutputAt = Get-Date }

            $now = Get-Date
            if ($now -gt $deadline) {
                Write-Log "Spicetify command exceeded ${TimeoutSeconds}s timeout and will be terminated." -Level 'WARN'
                try { $process.Kill(); $process.WaitForExit(5000) } catch {}
                $tail = & $getTail
                throw "$FailureMessage Timed out after $TimeoutSeconds seconds.$tail"
            }

            if ($IdleTimeoutSeconds -gt 0 -and $now -ge $lastOutputAt.AddSeconds($IdleTimeoutSeconds)) {
                $idleSeconds = [int]($now - $lastOutputAt).TotalSeconds
                Write-Log "  Spicetify has not emitted a new line for ${idleSeconds}s; still waiting until the ${TimeoutSeconds}s hard timeout." -Level 'WARN'
                $lastOutputAt = $now
            }

            if ($now -ge $lastHeartbeatAt.AddSeconds($heartbeatSeconds)) {
                $elapsedSeconds = [int]($now - $startedAt).TotalSeconds
                $idleSeconds = [int]($now - $lastOutputAt).TotalSeconds
                Write-Log "  Spicetify still running (${elapsedSeconds}s elapsed, ${idleSeconds}s since last output)."
                Update-SpicetifyCliProgress -Line 'Patching files'
                $lastHeartbeatAt = $now
            }
        }

        Start-Sleep -Milliseconds 200
        $null = & $drainOutput

        $exitCode = $null
        try { $exitCode = $process.ExitCode } catch { $exitCode = $null }
        if ($null -eq $exitCode) {
            Write-Log 'Spicetify process finished but ExitCode was unavailable; treating as success.' -Level 'WARN'
        } elseif ($exitCode -ne 0) {
            $tail = & $getTail
            throw "$FailureMessage Exit code: $exitCode.$tail"
        } else {
            Write-Log "  Spicetify exited with code 0."
        }
    } finally {
        $ErrorActionPreference = $previousPreference
        if ($process) {
            if ($collector) { try { $collector.Detach($process) } catch {} }
            try { $process.CancelOutputRead() } catch {}
            try { $process.CancelErrorRead() } catch {}
            try { $process.Dispose() } catch {}
        }
    }
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
    return (Test-Path -LiteralPath (Get-SpicetifyIntegrationContext).CliPath)
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
    [CmdletBinding(SupportsShouldProcess)]
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
    if ($PSCmdlet.ShouldProcess("$Scope PATH", 'Update PATH entries')) {
        Write-OperationJournalEntry -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore the previous PATH value.'
        if ($Scope -eq 'Process') {
            $env:PATH = $pathValue
        } else {
            [Environment]::SetEnvironmentVariable('PATH', $pathValue, $Scope)
        }
        Write-OperationJournalEntry -Phase 'path' -Target "$Scope PATH" -SafetyDecision 'Allowed' -Result 'Updated' -WouldChange $true -Reversible $true -RollbackHint 'Restore the previous PATH value.'
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
        # Both stable or both pre-release with same numeric prefix: extract the
        # trailing number from the suffix (e.g. `-preview.10` -> 10) and compare
        # numerically so `-preview.10` > `-preview.9` instead of the wrong lexical
        # ordering where "1" < "9".
        if ($Latest -eq $Current) { return $false }
        $latestSuffixNum = 0; $currentSuffixNum = 0
        if ($Latest -match '\.(\d+)$') { [int]::TryParse($Matches[1], [ref]$latestSuffixNum) | Out-Null }
        if ($Current -match '\.(\d+)$') { [int]::TryParse($Matches[1], [ref]$currentSuffixNum) | Out-Null }
        if ($latestSuffixNum -ne $currentSuffixNum) { return ($latestSuffixNum -gt $currentSuffixNum) }
        return ([string]::CompareOrdinal($Latest, $Current) -gt 0)
    } catch {
        # Non-parseable versions: lexical compare is better than claiming all
        # non-equal versions are "newer".
        if ($Latest -eq $Current) { return $false }
        return ([string]::CompareOrdinal($Latest, $Current) -gt 0)
    }
}

function Get-LibreSpotCurrentSpotifyTarget {
    $entry = $global:SpotifyVersionManifest | Where-Object { $_.Id -ne 'auto' } | Select-Object -First 1
    if (-not $entry) {
        return [pscustomobject]@{ Id = 'unknown'; Version = '' }
    }
    return [pscustomobject]@{
        Id      = [string]$entry.Id
        Version = [string]$entry.Version
    }
}

function Test-CompatibilityGate {
    $installedVersion = Get-InstalledSpotifyVersion
    if ([string]::IsNullOrWhiteSpace($installedVersion)) { return $true }
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spicetifyMax = [string]$global:PinnedReleases.SpicetifyCLI.WindowsMaxTestedSpotify
    $issues = @()
    if (-not [string]::IsNullOrWhiteSpace($spicetifyMax)) {
        $installedNorm = ($installedVersion -replace '\.(\d+)$','') -replace '^(\d+\.\d+\.\d+).*','$1'
        if (Compare-LibreSpotVersions -Latest $installedNorm -Current $spicetifyMax) {
            $issues += "Installed Spotify $installedVersion is newer than Spicetify CLI's max-tested version ($spicetifyMax). Themes and extensions may not apply correctly."
        }
    }
    if ($issues.Count -eq 0) { return $true }
    $msg = ($issues -join "`n`n") + "`n`nProceeding may produce a broken Spotify client. You can downgrade Spotify by choosing a pinned version in Custom Install, or continue at your own risk."
    $r = Show-ThemedDialog -Title 'Version compatibility warning' -Message $msg -Buttons 'YesNo' -Icon 'Warning' -PrimaryText 'Continue anyway' -SecondaryText 'Cancel'
    return ($r -eq 'Yes')
}

function Update-CompatibilityWarningBadge {
    if (-not $ui.ContainsKey('CompatibilityWarning')) { return }
    $warnings = @(Get-LibreSpotCompatibilityWarnings)
    if ($warnings.Count -gt 0) {
        $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
        $spicetifyMax = [string]$global:PinnedReleases.SpicetifyCLI.WindowsMaxTestedSpotify
        $ui['CompatibilityWarning'].Text = "⚠ SpotX targets Spotify $($spotxTarget.Id) but Spicetify max-tested is $spicetifyMax"
        $ui['CompatibilityWarning'].Visibility = 'Visible'
    } else {
        $ui['CompatibilityWarning'].Visibility = 'Collapsed'
    }
}

function Get-LibreSpotCompatibilityWarnings {
    $warnings = @()
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spicetifyMax = [string]$global:PinnedReleases.SpicetifyCLI.WindowsMaxTestedSpotify
    if (-not [string]::IsNullOrWhiteSpace($spotxTarget.Id) -and
        -not [string]::IsNullOrWhiteSpace($spicetifyMax) -and
        (Compare-LibreSpotVersions -Latest $spotxTarget.Id -Current $spicetifyMax)) {
        $warnings += "SpotX target Spotify $($spotxTarget.Id) is newer than Spicetify CLI v$($global:PinnedReleases.SpicetifyCLI.Version) max-tested Windows/Microsoft Store Spotify $spicetifyMax; Spicetify CSS maps may need validation after patching."
    }
    return $warnings
}

function Write-LibreSpotCompatibilityMatrix {
    $spotxTarget = Get-LibreSpotCurrentSpotifyTarget
    $spotxLabel = if ([string]::IsNullOrWhiteSpace($spotxTarget.Version)) {
        $spotxTarget.Id
    } else {
        "$($spotxTarget.Id) ($($spotxTarget.Version))"
    }
    $spicetify = $global:PinnedReleases.SpicetifyCLI

    Write-Log '  Compatibility matrix:'
    Write-Log "    SpotX: commit $($global:PinnedReleases.SpotX.Commit.Substring(0,10)) targets Spotify $spotxLabel"
    Write-Log "    Spicetify CLI: v$($spicetify.Version) max-tested Windows/Microsoft Store Spotify $($spicetify.WindowsMinSpotify) -> $($spicetify.WindowsMaxTestedSpotify)"
    Write-Log "    Marketplace: v$($global:PinnedReleases.Marketplace.Version) checked as a custom app package independent of Spotify CSS-map coverage"
    Write-Log "    Themes: commit $($global:PinnedReleases.Themes.Commit.Substring(0,10)) checked as a theme archive independent of Spotify CSS-map coverage"

    $warnings = @(Get-LibreSpotCompatibilityWarnings)
    foreach ($warning in $warnings) {
        Write-Log "    Compatibility warning: $warning" -Level 'WARN'
    }
    return $warnings
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

function Get-UpstreamStalenessCachePath {
    Join-Path $global:CONFIG_DIR 'upstream-freshness-cache.json'
}

function Read-UpstreamStalenessCache {
    $cachePath = Get-UpstreamStalenessCachePath
    $cacheMaxAge = [TimeSpan]::FromHours(24)
    if (-not (Test-Path -LiteralPath $cachePath)) { return $null }

    try {
        $cache = Get-Content -LiteralPath $cachePath -Raw | ConvertFrom-Json
        $cacheAge = (Get-Date) - [datetime]$cache.checkedAt
        if ($cacheAge -ge $cacheMaxAge) { return $null }

        $notices = @()
        if ($cache.notices -and $cache.notices.Count -gt 0) {
            $notices = @($cache.notices | ForEach-Object { [string]$_ })
        }
        return @{ Notices = [string[]]$notices }
    } catch {
        return $null
    }
}

function Save-UpstreamStalenessCache {
    param([string[]]$Notices)

    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -ItemType Directory -Path $global:CONFIG_DIR -Force | Out-Null
        }
        $cacheData = @{ checkedAt = (Get-Date).ToString('o'); notices = @($Notices) }
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText((Get-UpstreamStalenessCachePath), ($cacheData | ConvertTo-Json -Compress), $utf8NoBom)
    } catch {}
}

function Invoke-UpstreamStalenessJsonGet {
    param([string]$Uri)

    $req = [System.Net.HttpWebRequest]::Create($Uri)
    $req.Method = 'GET'
    $req.Timeout = 5000
    $req.ReadWriteTimeout = 5000
    $req.UserAgent = "LibreSpot/$($global:VERSION)"
    $req.Accept = 'application/vnd.github+json'
    $resp = $req.GetResponse()
    try {
        $stream = $resp.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($stream)
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally {
        $resp.Dispose()
    }
}

function Test-UpstreamThreadPoolVersionNewer {
    param([string]$Latest, [string]$Current)

    try {
        $latestVer  = [Version](($Latest  -replace '-preview.*','') -replace '-rc.*','')
        $currentVer = [Version](($Current -replace '-preview.*','') -replace '-rc.*','')
        if     ($latestVer -gt $currentVer) { return $true }
        elseif ($latestVer -lt $currentVer) { return $false }

        $latestIsStable  = ($Latest  -eq (($Latest  -replace '-preview.*','') -replace '-rc.*',''))
        $currentIsStable = ($Current -eq (($Current -replace '-preview.*','') -replace '-rc.*',''))
        if     ($latestIsStable -and -not $currentIsStable) { return $true }
        elseif (-not $latestIsStable -and $currentIsStable) { return $false }
        return ($Latest -ne $Current)
    } catch {
        return ($Latest -ne $Current)
    }
}

function Invoke-UpstreamStalenessHttp {
    # Pure-.NET HTTP + regex parsing so ThreadPool callers never run cmdlet-heavy
    # web or JSON pipelines on a borrowed CLR thread.
    try {
        $staleItems = [System.Collections.Generic.List[string]]::new()

        try {
            $json = Invoke-UpstreamStalenessJsonGet -Uri 'https://api.github.com/repos/spicetify/cli/releases/latest'
            $tagMatch = [regex]::Match($json, '"tag_name"\s*:\s*"([^"]+)"')
            if ($tagMatch.Success) {
                $latest = $tagMatch.Groups[1].Value -replace '^v',''
                $pinned = $global:PinnedReleases.SpicetifyCLI.Version
                if (Test-UpstreamThreadPoolVersionNewer -Latest $latest -Current $pinned) {
                    $staleItems.Add("Spicetify CLI v$latest is available (LibreSpot pins v$pinned)")
                }
            }
        } catch {}

        try {
            $json = Invoke-UpstreamStalenessJsonGet -Uri 'https://api.github.com/repos/SpotX-Official/SpotX/commits/main'
            $shaMatch = [regex]::Match($json, '"sha"\s*:\s*"([0-9a-fA-F]{40})"')
            if ($shaMatch.Success) {
                $latestSha = $shaMatch.Groups[1].Value
                if ($latestSha -ne $global:PinnedReleases.SpotX.Commit) {
                    $short = $latestSha.Substring(0,10)
                    $staleItems.Add("SpotX has newer commits (latest: $short)")
                }
            }
        } catch {}

        return @{ Notices = [string[]]$staleItems.ToArray() }
    } catch {
        return $null
    }
}

function Add-UpstreamStalenessNoticesToWarning {
    param([string[]]$Notices)

    if (-not $Notices -or $Notices.Count -eq 0) { return }
    if (-not $ui.ContainsKey('CompatibilityWarning')) { return }

    $existing = $ui['CompatibilityWarning'].Text
    $staleText = ($Notices -join ' | ')
    if ([string]::IsNullOrWhiteSpace($existing)) {
        $ui['CompatibilityWarning'].Text = "ℹ Newer upstream: $staleText"
    } else {
        $ui['CompatibilityWarning'].Text = "$existing | $staleText"
    }
    $ui['CompatibilityWarning'].Visibility = 'Visible'
}

function Start-UpstreamStalenessNoticeRefresh {
    if (-not $ui.ContainsKey('CompatibilityWarning')) { return }

    $cached = Read-UpstreamStalenessCache
    if ($cached) {
        Add-UpstreamStalenessNoticesToWarning -Notices ([string[]]$cached.Notices)
        return
    }

    try {
        $null = [System.Threading.ThreadPool]::QueueUserWorkItem([System.Threading.WaitCallback]{
            param($state)
            $result = Invoke-UpstreamStalenessHttp
            try {
                $window.Dispatcher.BeginInvoke([System.Windows.Threading.DispatcherPriority]::ApplicationIdle, [Action]{
                    if ($window.Dispatcher.HasShutdownStarted) { return }
                    if ($result) {
                        Save-UpstreamStalenessCache -Notices ([string[]]$result.Notices)
                        Add-UpstreamStalenessNoticesToWarning -Notices ([string[]]$result.Notices)
                    }
                }) | Out-Null
            } catch {}
        }, $null) | Out-Null
    } catch {}
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
            $ui['UpdateBanner'].ToolTip = "New release $($check.LatestTag) - click to open GitHub."
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
    $spotifyDir = [System.IO.Path]::GetDirectoryName($global:SPOTIFY_EXE_PATH)

    $signatures = @(
        @{ Path = (Join-Path $spotifyDir 'dpapi.dll');      Label = 'BlockTheSpot-family legacy patcher (dpapi.dll injected next to Spotify.exe)' }
        @{ Path = (Join-Path $spotifyDir 'config.ini');     Label = 'Legacy BlockTheSpot config.ini present in the Spotify install directory' }
        @{ Path = (Join-Path $spotifyDir 'version.dll');    Label = 'Third-party DLL injector (version.dll hijack)' }
        @{ Path = (Join-Path $spotifyDir 'winmm.dll');      Label = 'Third-party DLL injector (winmm.dll hijack)' }
    )
    foreach ($sig in $signatures) {
        if (Test-Path -LiteralPath $sig.Path) { return [string]$sig.Label }
    }
    return $null
}

# Post-patch effectiveness check. A clean SpotX exit code does NOT prove the
# patch landed: Spotify's signature protection on newer builds (>=1.2.70) can
# let SpotX run to completion without actually patching xpui (SpotX issue #760).
# SpotX backs up the original bundle to Apps\xpui.spa.bak *before* it patches,
# so a successfully patched install leaves BOTH the patched xpui.spa AND the
# .bak alongside Spotify.exe. We assert those on-disk markers and return a
# structured verdict so callers can surface "patched & verified" vs "ran but
# unverified" with a recovery hint instead of trusting exit code 0 alone.
# Pure and side-effect free so it can be unit-tested against a synthetic dir.
function Get-SpotXPatchVerification {
    param([string]$SpotifyExePath = $global:SPOTIFY_EXE_PATH)

    $result = [ordered]@{
        Verified = $false
        Status   = 'Missing'   # Missing | Unverified | Verified
        Reason   = ''
        Signals  = @()
    }

    if ([string]::IsNullOrWhiteSpace($SpotifyExePath) -or -not (Test-Path -LiteralPath $SpotifyExePath)) {
        $result.Reason = 'Spotify.exe was not found, so SpotX could not have patched anything.'
        return [pscustomobject]$result
    }

    $spotifyDir = [System.IO.Path]::GetDirectoryName($SpotifyExePath)
    $appsDir    = Join-Path $spotifyDir 'Apps'
    $signals    = New-Object System.Collections.Generic.List[string]

    # SpotX backs up the original app bundle before patching. Current SpotX names
    # that backup Apps\xpui.bak; older SpotX builds used Apps\xpui.spa.bak. Either
    # one proves SpotX rewrote the bundle. (Checking only xpui.spa.bak produced a
    # false "patch could not be verified" warning on every successful install.)
    $hasXpuiBak    = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.bak')
    $hasXpuiSpaBak = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa.bak')
    $hasBackup     = $hasXpuiBak -or $hasXpuiSpaBak

    # The bundle is a packed xpui.spa, or an extracted Apps\xpui directory once
    # Spicetify has applied on top of the SpotX-patched client.
    $hasSpaBundle = Test-Path -LiteralPath (Join-Path $appsDir 'xpui.spa') -PathType Leaf
    $hasDirBundle = Test-Path -LiteralPath (Join-Path $appsDir 'xpui') -PathType Container
    $hasBundle    = $hasSpaBundle -or $hasDirBundle

    # SpotX also patches the native binaries and leaves durable .bak copies next to
    # Spotify.exe. Spicetify's later apply consumes/renames the xpui backup, but the
    # binary backups persist, so they corroborate a SpotX run after the fact.
    $hasBinBackup = (Test-Path -LiteralPath (Join-Path $spotifyDir 'Spotify.bak')) -or `
                    (Test-Path -LiteralPath (Join-Path $spotifyDir 'chrome_elf.dll.bak'))

    if ($hasXpuiBak)    { $signals.Add('xpui.bak (SpotX backed up the original bundle before patching)') }
    if ($hasXpuiSpaBak) { $signals.Add('xpui.spa.bak (legacy SpotX bundle backup)') }
    if ($hasBinBackup)  { $signals.Add('Spotify.bak/chrome_elf.dll.bak (SpotX patched the native binaries)') }
    if ($hasSpaBundle)  { $signals.Add('xpui.spa (Spotify app bundle present)') }
    elseif ($hasDirBundle) { $signals.Add('Apps\xpui (bundle extracted by Spicetify)') }
    $result.Signals = @($signals)

    if (($hasBackup -or $hasBinBackup) -and $hasBundle) {
        $result.Verified = $true
        $result.Status   = 'Verified'
        $result.Reason   = 'SpotX left a patched app bundle and a backup of the original, so the patch was applied.'
    }
    elseif ($hasBundle) {
        $result.Status = 'Unverified'
        $result.Reason = 'Spotify is present but no SpotX backup (Apps\xpui.bak or a patched-binary backup) was found, so the patch may not have been applied. Signature protection on newer Spotify builds can let SpotX exit cleanly without patching.'
    }
    else {
        $result.Status = 'Unverified'
        $result.Reason = 'The Spotify app bundle (Apps\xpui.spa) is missing, so SpotX patching could not be confirmed.'
    }

    return [pscustomobject]$result
}

# Shown once per session when Spotify looks like it was patched outside of
# LibreSpot. SpotX can recover, but the user deserves an explicit heads-up.
function Test-ForeignPatchWarningIfNeeded {
    if ($script:ForeignPatchWarningShown) { return }
    $signature = Get-ExistingSpotifyPatchSignature
    if (-not $signature) { return }
    $script:ForeignPatchWarningShown = $true

    $message = "Spotify at $global:SPOTIFY_EXE_PATH looks like it was already patched by another tool.`n`nDetected: $signature`n`nLibreSpot can cleanly replace BlockTheSpot-family DLL-injection artifacts during install. If you see blank screens or failed playback after patching, run Maintenance > Full Reset to remove remaining third-party patch files before reinstalling."
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
    if (Test-Path $global:SPOTIFY_EXE_PATH) {
        # Reuse the shared verifier so Maintenance agrees with the install log
        # (checks xpui.bak / xpui.spa.bak / patched-binary backups, not just the
        # stale xpui.spa.bak filename SpotX no longer writes).
        try { if ((Get-SpotXPatchVerification -SpotifyExePath $global:SPOTIFY_EXE_PATH).Verified) { $spotxFound = $true } } catch {}
    }
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

    $integration = Get-SpicetifyIntegrationContext
    $sExe = $integration.CliPath
    if (Test-Path $sExe) {
        # Randomized name: a fixed spicetify_ver.txt collides when two
        # LibreSpot instances refresh maintenance status at the same time.
        $tmpOut = Join-Path $global:TEMP_DIR ("spicetify_ver.{0}.txt" -f [Guid]::NewGuid().ToString('N'))
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

    $marketplaceHealth = Get-MarketplaceHealth
    $marketplaceInstalled = [bool]$marketplaceHealth.IsReady
    switch ($marketplaceHealth.Status) {
        'Ready' {
            $ui['StatusMarketplace'].Text = 'Installed'
            $ui['StatusMarketplace'].Foreground = $global:BrushGreen
            Set-MaintenanceCardTone -CardName 'StatusCardMarketplace' -Tone 'success'
        }
        'Hidden' {
            $ui['StatusMarketplace'].Text = "Hidden`nopen URI"
            $ui['StatusMarketplace'].Foreground = $global:BrushError
            Set-MaintenanceCardTone -CardName 'StatusCardMarketplace' -Tone 'warning'
        }
        'FilesMissing' {
            $ui['StatusMarketplace'].Text = "Enabled`nfiles missing"
            $ui['StatusMarketplace'].Foreground = $global:BrushError
            Set-MaintenanceCardTone -CardName 'StatusCardMarketplace' -Tone 'danger'
        }
        'LegacyPath' {
            $ui['StatusMarketplace'].Text = "Legacy path`nrepair"
            $ui['StatusMarketplace'].Foreground = $global:BrushError
            Set-MaintenanceCardTone -CardName 'StatusCardMarketplace' -Tone 'warning'
        }
        default {
            $ui['StatusMarketplace'].Text = 'Not installed'
            $ui['StatusMarketplace'].Foreground = $global:BrushMuted
            Set-MaintenanceCardTone -CardName 'StatusCardMarketplace' -Tone 'muted'
        }
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
            'Run Recommended setup'
        } elseif ($si -and $marketplaceHealth.Status -in @('Hidden','FilesMissing','LegacyPath')) {
            'Repair Marketplace'
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
        } elseif ($si -and $marketplaceHealth.Status -in @('Hidden','FilesMissing','LegacyPath')) {
            'Use Repair and open Marketplace to reinstall files, re-enable custom_apps, and launch spotify:app:marketplace directly.'
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
    $hasConfigSnapshot = Test-Path -LiteralPath $integration.ConfigPath
    $ui['BtnBackupConfig'].IsEnabled=($si -and $hasConfigSnapshot); $ui['BtnRestoreConfig'].IsEnabled=($bk -and $si); $ui['BtnReapply'].IsEnabled=$sp
    if ($ui.ContainsKey('BtnRepairMarketplace')) { $ui['BtnRepairMarketplace'].IsEnabled=$si }
    $ui['BtnSafeMode'].IsEnabled=$si; $ui['BtnSpicetifyRestore'].IsEnabled=$si; $ui['BtnUninstallSpicetify'].IsEnabled=$si; $ui['BtnFullReset'].IsEnabled=($sp -or $si)
    $ui['BtnBackupConfig'].ToolTip = if ($ui['BtnBackupConfig'].IsEnabled) { 'Create a timestamped backup of the active Spicetify configuration.' } elseif (-not $si) { 'Install Spicetify before backing up its configuration.' } else { 'Run a setup first so LibreSpot has a clean Spicetify config to back up.' }
    $ui['BtnRestoreConfig'].ToolTip = if ($ui['BtnRestoreConfig'].IsEnabled) { 'Restore the newest saved Spicetify backup and apply it immediately.' } elseif (-not $si) { 'Install Spicetify before restoring a backup.' } else { 'Create at least one backup before restoring.' }
    $ui['BtnCheckUpdates'].ToolTip = 'Compare LibreSpot''s pinned versions against the latest upstream releases.'
    if ($ui.ContainsKey('BtnRepairMarketplace')) { $ui['BtnRepairMarketplace'].ToolTip = if ($ui['BtnRepairMarketplace'].IsEnabled) { 'Reinstall Marketplace files, re-enable custom_apps, apply Spicetify, and open spotify:app:marketplace.' } else { 'Install Spicetify before repairing Marketplace.' } }
    $ui['BtnReapply'].ToolTip = if ($ui['BtnReapply'].IsEnabled) { 'Run SpotX again and then reapply Spicetify with the saved LibreSpot configuration.' } else { 'Spotify needs to be installed before LibreSpot can reapply anything.' }
    $ui['BtnSafeMode'].ToolTip = if ($ui['BtnSafeMode'].IsEnabled) { 'Disable all themes and extensions without uninstalling — use Reapply to restore your setup.' } else { 'Install Spicetify before entering safe mode.' }
    $ui['BtnSpicetifyRestore'].ToolTip = if ($ui['BtnSpicetifyRestore'].IsEnabled) { 'Remove active Spicetify customizations and restore vanilla Spotify while leaving SpotX in place.' } else { 'Install Spicetify before using this restore action.' }
    $ui['BtnUninstallSpicetify'].ToolTip = if ($ui['BtnUninstallSpicetify'].IsEnabled) { 'Remove the Spicetify CLI, configuration, and PATH entry after restoring vanilla Spotify.' } else { 'Install Spicetify before uninstalling it.' }
    $ui['BtnFullReset'].ToolTip = if ($ui['BtnFullReset'].IsEnabled) { 'Remove the full Spotify customization stack and clean leftover files.' } else { 'Nothing is installed yet, so there is nothing to reset.' }

    if ($ui.ContainsKey('MaintenanceOverviewTitle') -and $ui.ContainsKey('MaintenanceOverviewText')) {
        if (-not $sp -and -not $si) {
            $ui['MaintenanceOverviewTitle'].Text = 'Nothing looks installed yet'
            $ui['MaintenanceOverviewText'].Text = 'LibreSpot did not detect Spotify or Spicetify. You can still review versions here, but backup and restore actions will stay unavailable until a setup has been applied.'
        } elseif ($si -and $marketplaceHealth.Status -in @('Hidden','FilesMissing','LegacyPath')) {
            $ui['MaintenanceOverviewTitle'].Text = 'Marketplace needs attention'
            $ui['MaintenanceOverviewText'].Text = 'LibreSpot found Marketplace state that may not appear in Spotify. Use Repair and open Marketplace to reinstall the custom app, reapply Spicetify, and open the direct Marketplace page.'
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
        $ui['SelectionSummary'].Text = "Current state: $componentLabel, $backupLabel, and destructive actions stay behind confirmation."

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
        $compatWarnings = @(Get-LibreSpotCompatibilityWarnings)
        if ($compatWarnings.Count -gt 0) {
            $selectionDetail = "$selectionDetail SpotX/Spicetify version gap detected."
            if ($selectionTone -ne 'danger') { $selectionTone = 'warning' }
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

    $integration = Get-SpicetifyIntegrationContext
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

        if (Test-Path -LiteralPath $integration.ConfigDirectory -PathType Container) {
            Copy-DirectorySnapshot -SourcePath $integration.ConfigDirectory -DestinationPath $rollbackPath
            $rollbackAvailable = $true
        }

        if (Test-Path -LiteralPath $integration.ConfigDirectory) {
            $null = Remove-PathSafely -Path $integration.ConfigDirectory -Label 'Current Spicetify config'
        }
        Copy-DirectorySnapshot -SourcePath $stagedSource -DestinationPath $integration.ConfigDirectory

        $spicetifyExe = $integration.CliPath
        if (Test-Path -LiteralPath $spicetifyExe) {
            Invoke-SpicetifyCli -Arguments @('backup','apply','--bypass-admin') -FailureMessage 'Could not apply the restored Spicetify backup.'
        }
    } catch {
        $originalError = $_.Exception.Message
        if ($rollbackAvailable) {
            try {
                if (Test-Path -LiteralPath $integration.ConfigDirectory) {
                    $null = Remove-PathSafely -Path $integration.ConfigDirectory -Label 'Failed restore state'
                }
                Copy-DirectorySnapshot -SourcePath $rollbackPath -DestinationPath $integration.ConfigDirectory
                $spicetifyExe = $integration.CliPath
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
    $integration = Get-SpicetifyIntegrationContext
    if (-not (Test-Path -LiteralPath $integration.ConfigDirectory -PathType Container)) {
        Show-ThemedDialog -Message "LibreSpot could not find the active Spicetify configuration folder yet. Apply a setup first, then return here to create a backup." -Title "Nothing To Back Up" -Icon "Error" -PrimaryText "Close"
        return
    }
    if (-not (Test-Path -LiteralPath $integration.ConfigPath -PathType Leaf)) {
        Show-ThemedDialog -Message "LibreSpot found the Spicetify folder, but the main config file is missing. Reapply your setup first so a clean backup can be created." -Title "Backup Not Ready" -Icon "Error" -PrimaryText "Close"
        return
    }
    $stamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"; $dest = Join-Path $global:BACKUP_ROOT $stamp
    New-Item -Path $dest -ItemType Directory -Force | Out-Null
    Copy-DirectorySnapshot -SourcePath $integration.ConfigDirectory -DestinationPath (Join-Path $dest 'spicetify')
    $all = Get-ChildItem $global:BACKUP_ROOT -Directory | Sort-Object Name -Descending
    if ($all.Count -gt 5) {
        $all | Select-Object -Skip 5 | ForEach-Object {
            $null = Remove-PathSafely -Path $_.FullName -Label "Old backup $($_.Name)"
        }
    }
    Show-ThemedDialog -Message "LibreSpot saved a new Spicetify backup as $stamp in %USERPROFILE%\\LibreSpot_Backups." -Title "Backup Saved" -Icon "Information" -PrimaryText "Done"; Update-MaintenanceStatus
} catch { Show-ThemedDialog -Message "LibreSpot could not create the backup.`n`n$($_.Exception.Message)" -Title "Backup Failed" -Icon "Error" -PrimaryText "Close" } })

$ui['BtnRestoreConfig'].Add_Click({ try {
    $sExe = (Get-SpicetifyIntegrationContext).CliPath
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
    if (-not (Confirm-NetworkReadyForAction -Message "LibreSpot could not reach GitHub to compare pinned versions." -Purpose "GitHub update checks")) { return }
    try {
        Switch-ToInstallPage -Title 'Checking pinned versions' -Context 'LibreSpot is comparing the pinned LibreSpot, SpotX, Spicetify, Marketplace, and theme versions against upstream releases.' -PrepareLabel 'Prepare' -RunLabel 'Compare' -VerifyLabel 'Review' -CompleteLabel 'Complete'
        Start-MaintenanceJob -Action 'CheckUpdates'
    } catch {
        Reset-UiAfterLaunchFailure -Title 'Could not start maintenance' -Message "LibreSpot couldn't start the update check.`n`n$($_.Exception.Message)"
    }
})
$ui['BtnRepairMarketplace'].Add_Click({
    if (-not (Confirm-NetworkReadyForAction -Message "LibreSpot needs an internet connection to download the pinned Marketplace archive before it can repair the custom app." -Purpose "Marketplace repair downloads")) { return }
    if (-not (Assert-RiskAcknowledged)) { return }
    $r = Show-ThemedDialog -Message "LibreSpot will reinstall the pinned Marketplace custom app, re-enable it in Spicetify, apply the change, and open spotify:app:marketplace. Use this when the Marketplace files exist but the sidebar icon is missing or only partial Marketplace content appears." -Title "Repair Marketplace" -Buttons "YesNo" -Icon "Question" -PrimaryText "Repair and open" -SecondaryText "Cancel"
    if ($r -eq 'Yes') {
        try {
            Switch-ToInstallPage -Title 'Repairing Marketplace' -Context 'LibreSpot is reinstalling the Marketplace custom app, reapplying Spicetify, and opening the direct Marketplace URI.' -PrepareLabel 'Prepare' -RunLabel 'Repair' -VerifyLabel 'Open' -CompleteLabel 'Complete'
            Start-MaintenanceJob -Action 'RepairMarketplace'
        } catch {
            Reset-UiAfterLaunchFailure -Title 'Could not start maintenance' -Message "LibreSpot couldn't start the Marketplace repair flow.`n`n$($_.Exception.Message)"
        }
    }
})
$ui['BtnReapply'].Add_Click({
    if (-not (Confirm-NetworkReadyForAction -Message "LibreSpot needs an internet connection to download the pinned SpotX script before it can reapply your setup." -Purpose "SpotX reapply downloads")) { return }
    if (-not (Assert-RiskAcknowledged)) { return }
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

$ui['BtnSafeMode'].Add_Click({
    # NOTE: don't guard on $si here — that's a local of Update-MaintenanceStatus
    # and is always $null in this scope (it made the button permanently dead).
    # The button's IsEnabled already tracks Spicetify presence; re-check the
    # real condition defensively.
    if (-not (Test-SpicetifyCliInstalled)) { return }
    try {
        Switch-ToInstallPage -Title 'Entering safe mode' -Context 'Disabling all themes and extensions — use Reapply to restore your setup.' -PrepareLabel 'Prepare' -RunLabel 'Disable' -VerifyLabel 'Verify' -CompleteLabel 'Complete'
        Start-MaintenanceJob -Action 'SafeMode'
    } catch {
        Reset-UiAfterLaunchFailure -Title 'Could not start maintenance' -Message "LibreSpot couldn't start safe mode.`n`n$($_.Exception.Message)"
    }
})
$ui['BtnSpicetifyRestore'].Add_Click({
    if (-not (Assert-RiskAcknowledged)) { return }
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
    if (-not (Assert-RiskAcknowledged)) { return }
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
    if (-not (Assert-RiskAcknowledged)) { return }
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
    <Border CornerRadius="10" Background="#FF090D13" BorderBrush="#FF25313D" BorderThickness="1" Padding="0" Margin="14">
        <Border.Effect><DropShadowEffect BlurRadius="30" ShadowDepth="4" Opacity="0.48" Direction="270" Color="#05070A"/></Border.Effect>
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <Border Grid.Row="0" Background="#FF0C1118" CornerRadius="10,10,0,0" Padding="20,16">
                <TextBlock Name="DlgTitle" FontSize="13" FontWeight="SemiBold" Foreground="#FFE7EDF3" FontFamily="Segoe UI"/>
            </Border>
            <Grid Grid.Row="1" Margin="24,22,24,18">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="18"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Border Name="IconHost" Width="44" Height="44" CornerRadius="22" Background="#FF111A22" BorderBrush="#FF2D5A3F" BorderThickness="1" VerticalAlignment="Top">
                    <Canvas Name="IconCanvas" Width="24" Height="24" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <TextBlock Grid.Column="2" Name="DlgMessage" FontSize="13" LineHeight="19" Foreground="#FFE7EDF3" FontFamily="Segoe UI" TextWrapping="Wrap" MaxWidth="430" VerticalAlignment="Center"/>
            </Grid>
            <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="24,0,24,24">
                <Button Name="BtnNo" Content="Cancel" Width="108" Height="38" FontSize="12.5" FontWeight="SemiBold" Cursor="Hand" Margin="0,0,10,0" Visibility="Collapsed" Background="#FF111821" BorderBrush="#FF2D3A47" BorderThickness="1" Foreground="#FFE7EDF3">
                    <Button.Template><ControlTemplate TargetType="Button">
                        <Border x:Name="bd" Background="{TemplateBinding Background}" CornerRadius="10" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="14,0"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Opacity" Value="0.92"/><Setter TargetName="bd" Property="BorderBrush" Value="#FF778390"/></Trigger>
                            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="bd" Property="BorderBrush" Value="#FF22C55E"/></Trigger>
                            <Trigger Property="IsEnabled" Value="False"><Setter TargetName="bd" Property="Opacity" Value="0.5"/></Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate></Button.Template>
                </Button>
                <Button Name="BtnYes" Content="Continue" Width="132" Height="38" FontSize="12.5" FontWeight="SemiBold" Cursor="Hand" Margin="0,0,0,0" Visibility="Collapsed" Background="#FF22C55E" BorderBrush="#FF16A34A" BorderThickness="1" Foreground="#FF04130A">
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
                <Button Name="BtnOK" Content="OK" Width="132" Height="38" FontSize="12.5" FontWeight="SemiBold" Cursor="Hand" Visibility="Collapsed" Background="#FF22C55E" BorderBrush="#FF16A34A" BorderThickness="1" Foreground="#FF04130A">
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
    $iconColor = "#FF22C55E"
    $iconHost.Background = $script:BrushConverter.ConvertFromString('#FF111A22')
    $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#FF2D5A3F')
    switch ($Icon) {
        "Error" {
            $iconColor = "#FFF87171"
            $iconHost.Background = $script:BrushConverter.ConvertFromString('#FF2B1117')
            $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#FFEF4444')
        }
        "Warning" {
            $iconColor = "#FFF59E0B"
            $iconHost.Background = $script:BrushConverter.ConvertFromString('#FF211A0E')
            $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#FF6B4E16')
        }
        "Question" {
            $iconColor = "#FF22C55E"
            $iconHost.Background = $script:BrushConverter.ConvertFromString('#FF111A22')
            $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#FF2D5A3F')
        }
        "Information" {
            $iconColor = "#FF93C5FD"
            $iconHost.Background = $script:BrushConverter.ConvertFromString('#FF111C2A')
            $iconHost.BorderBrush = $script:BrushConverter.ConvertFromString('#FF2E4964')
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
    $primaryBackground = if ($PrimaryIsDestructive) { '#FFEF4444' } else { '#FF22C55E' }
    $primaryBorder = if ($PrimaryIsDestructive) { '#FFEF4444' } else { '#FF16A34A' }
    $primaryForeground = if ($PrimaryIsDestructive) { '#FFFFF1F2' } else { '#FF04130A' }
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

function Assert-RiskAcknowledged {
    <#
    .SYNOPSIS
    Returns $true when the user has acknowledged the ToS risk. Shows a
    first-run dialog when needed and persists the acknowledgment.
    #>
    $cfg = $null
    try { $cfg = Load-LibreSpotConfig } catch {}
    if ($cfg -and (ConvertTo-ConfigBoolean -Value $cfg['RiskAcknowledged'] -Default $false)) {
        return $true
    }

    $riskMessage = "LibreSpot modifies your Spotify installation to remove ads and apply themes. This violates Spotify's Terms of Service and User Guidelines (https://spotify.com/legal/user-guidelines/). While enforcement against individual users has not been publicly documented, your account could be affected.`n`nBy continuing, you acknowledge this risk and agree to proceed at your own discretion.`n`nYou can restore stock Spotify at any time using Maintenance > Full Reset."
    $r = Show-ThemedDialog -Title 'Risk acknowledgment' -Message $riskMessage -Buttons 'YesNo' -Icon 'Warning' -PrimaryText 'I understand, continue' -SecondaryText 'Cancel'
    if ($r -ne 'Yes') { return $false }

    if (-not $cfg) { $cfg = Normalize-LibreSpotConfig -Config @{} }
    $cfg['RiskAcknowledged'] = $true
    try { $null = Save-LibreSpotConfig -Config $cfg } catch {}
    return $true
}

$window.Add_ContentRendered({
    if (-not [string]::IsNullOrWhiteSpace($script:ConfigLoadWarning)) {
        $warningMessage = $script:ConfigLoadWarning
        $script:ConfigLoadWarning = $null
        Show-ThemedDialog -Message $warningMessage -Title 'Saved settings were reset' -Icon 'Warning' -PrimaryText 'Continue' | Out-Null
    }
})

function Get-NetworkDiagnosticCode {
    param(
        [string]$Uri,
        [object]$ErrorRecord
    )
    $message = ''
    try { $message = [string]$ErrorRecord.Exception.Message } catch { $message = [string]$ErrorRecord }
    $statusCode = $null
    try {
        if ($ErrorRecord.Exception.Response -and $ErrorRecord.Exception.Response.StatusCode) {
            $statusCode = [int]$ErrorRecord.Exception.Response.StatusCode
        }
    } catch {}
    $target = $Uri
    try { $target = ([uri]$Uri).Host } catch {}
    $lowerMessage = $message.ToLowerInvariant()

    if ($statusCode -eq 407 -or $lowerMessage -match 'proxy.*auth|407|proxy authentication') { return 'ProxyAuthRequired' }
    if ($statusCode -eq 429 -or (($statusCode -eq 403) -and ($target -match 'github'))) { return 'GitHubRateLimitOrBlock' }
    if ($lowerMessage -match 'could not be resolved|name resolution|no such host|\bdns\b') { return 'DnsFailure' }
    if ($lowerMessage -match 'ssl|tls|certificate|trust relationship') { return 'TlsFailure' }
    if ($lowerMessage -match 'timed out|timeout') { return 'Timeout' }
    if ($lowerMessage -match 'sha256 mismatch|hash mismatch|checksum') { return 'HashMismatch' }
    return 'NetworkFailure'
}

function Get-NetworkPreflightStatus {
    param(
        [string]$Uri = 'https://raw.githubusercontent.com',
        [string]$Purpose = 'download sources',
        [int]$TimeoutMilliseconds = 5000
    )
    $resp = $null
    $target = $Uri
    try { $target = ([uri]$Uri).Host } catch {}
    $result = [ordered]@{
        Ready   = $false
        Code    = 'Unknown'
        Target  = $target
        Message = ''
        Detail  = ''
    }
    try {
        $request = [System.Net.WebRequest]::Create($Uri)
        $request.Timeout = $TimeoutMilliseconds
        $request.Method = 'HEAD'
        try { $request.UserAgent = "LibreSpot/$global:VERSION" } catch {}
        $resp = $request.GetResponse()
        $statusCode = $null
        try { $statusCode = [int]$resp.StatusCode } catch {}
        if ($null -eq $statusCode -or ($statusCode -ge 200 -and $statusCode -lt 400)) {
            $result.Ready = $true
            $result.Code = 'Ready'
            $result.Message = "LibreSpot can reach $target for $Purpose."
            $result.Detail = if ($null -eq $statusCode) { 'HTTP status unavailable' } else { "HTTP $statusCode" }
        } elseif ($statusCode -eq 407) {
            $result.Code = 'ProxyAuthRequired'
            $result.Message = "Network preflight failed: proxy authentication is required for $target. Configure the system or WinHTTP proxy before retrying."
            $result.Detail = "HTTP $statusCode"
        } elseif (($statusCode -eq 403 -or $statusCode -eq 429) -and ($target -match 'github')) {
            $result.Code = 'GitHubRateLimitOrBlock'
            $result.Message = "Network preflight failed: GitHub rate limit or access block for $target. Wait for the rate-limit reset or retry from a network with GitHub access."
            $result.Detail = "HTTP $statusCode"
        } else {
            $result.Code = "Http$statusCode"
            $result.Message = "Network preflight failed: $target returned HTTP $statusCode while checking $Purpose."
            $result.Detail = "HTTP $statusCode"
        }
    } catch {
        $result.Code = Get-NetworkDiagnosticCode -Uri $Uri -ErrorRecord $_
        $result.Message = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'Network preflight'
        try { $result.Detail = [string]$_.Exception.Message } catch {}
    }
    finally { if ($resp) { try { $resp.Close() } catch {} } }
    return [pscustomobject]$result
}

function Test-NetworkReady {
    $status = Get-NetworkPreflightStatus
    return [bool]$status.Ready
}

function Confirm-NetworkReadyForAction {
    param(
        [string]$Message,
        [string]$Purpose = 'download sources',
        [string]$Uri = 'https://raw.githubusercontent.com'
    )
    $status = Get-NetworkPreflightStatus -Uri $Uri -Purpose $Purpose
    if ($status.Ready) { return $true }
    try { Write-Log $status.Message -Level 'WARN' } catch {}
    Show-ThemedDialog -Message "$Message`n`n$($status.Message)" -Title "Network Check Failed" -Icon "Error" -PrimaryText "Close" | Out-Null
    return $false
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
    $script:copyLogDefaultText='Copy log'
    $ui['BtnCopyLog'].Tag=$script:copyLogDefaultText
    $ui['BtnCopyLog'].Content=$script:copyLogDefaultText
    $window.Topmost=$false
    Set-WindowChromeHints -State 'Idle'
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
    if ($ui.ContainsKey('LastLogEventText')) { $ui['LastLogEventText'].Text='Waiting for setup to start.' }
    $ui['InstallTitle'].Text = $Title; $ui['InstallContext'].Text = $Context
    $ui['ElapsedTime'].Text=''; if ($ui.ContainsKey('ProgressPercentText')) { $ui['ProgressPercentText'].Text='0%' }; $ui['MainProgress'].Value=0; $ui['MainProgress'].Foreground=$global:BrushGreen
    $script:copyLogDefaultText='Copy log'
    $ui['BtnCopyLog'].Tag=$script:copyLogDefaultText
    $ui['CloseBtn'].Visibility='Collapsed'; $ui['BtnBackToConfig'].Visibility='Collapsed'; $ui['BtnCopyLog'].Visibility='Visible'; $ui['BtnCopyLog'].Content=$script:copyLogDefaultText
    $window.Topmost = $false
    Set-WindowChromeHints -State 'Running'
    Set-InstallStageLabels -Prepare $PrepareLabel -Run $RunLabel -Verify $VerifyLabel -Complete $CompleteLabel
    Update-InstallStageVisual
}

$ui['BtnInstall'].Add_Click({
    if ($ui['BtnInstall'].IsEnabled -eq $false) { return }
    if (-not (Confirm-NetworkReadyForAction -Message "LibreSpot could not reach the download sources it needs." -Purpose "setup downloads")) { return }
    if (-not (Assert-RiskAcknowledged)) { return }
    if (-not (Test-CompatibilityGate)) { return }
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
    $eventText = ''
    try {
        $eventText = Remove-ConsoleEscapeSequences -Text $Message
        $eventText = [regex]::Replace($eventText, '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]', '')
        $eventText = [regex]::Replace($eventText, '\s+', ' ').Trim()
        if (-not [string]::IsNullOrWhiteSpace($eventText) -and $eventText.Length -gt 180) {
            $eventText = $eventText.Substring(0,177).TrimEnd() + '...'
        }
        if (-not [string]::IsNullOrWhiteSpace($eventText) -and $Level -in @('WARN','ERROR','SUCCESS')) {
            $eventText = "${Level}: $eventText"
        }
    } catch {}
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
        if ($sh.ContainsKey('LastLogEventLabel') -and $sh.LastLogEventLabel -and -not [string]::IsNullOrWhiteSpace($eventText)) {
            $sh.LastLogEventLabel.Text = "$ts  $eventText"
        }
        if ($IsHeader -or $Level -eq 'STEP') { $sh.StatusLabel.Text = $Message }
        if ($StepText) { $sh.StepLabel.Text = $StepText }
    }) } } catch {}
}
function Write-Log { param([string]$Message,[string]$Level='INFO'); Update-UI -Message $Message -Level $Level -IsHeader ($Level -eq 'STEP' -or $Level -eq 'HEADER') }

function Optimize-OperationJournalRetention {
    try {
        $maxBytes = [int64]$global:OPERATION_JOURNAL_MAX_BYTES
        $retainBytes = [int64]$global:OPERATION_JOURNAL_RETAIN_BYTES
        if ($maxBytes -le 0 -or $retainBytes -le 0 -or $retainBytes -ge $maxBytes) { return }
        if (-not (Test-Path -LiteralPath $global:OPERATION_JOURNAL_PATH -PathType Leaf)) { return }

        $file = Get-Item -LiteralPath $global:OPERATION_JOURNAL_PATH -ErrorAction Stop
        if ($file.Length -le $maxBytes) { return }

        $bytesToRead = [int][Math]::Min($retainBytes, $file.Length)
        $buffer = New-Object 'System.Byte[]' $bytesToRead
        $stream = [System.IO.File]::Open($global:OPERATION_JOURNAL_PATH, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $null = $stream.Seek(-1 * $bytesToRead, [System.IO.SeekOrigin]::End)
            $read = $stream.Read($buffer, 0, $buffer.Length)
        } finally {
            try { $stream.Dispose() } catch {}
        }

        $tail = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $read)
        $firstNewline = $tail.IndexOf("`n")
        if ($firstNewline -ge 0 -and $firstNewline -lt ($tail.Length - 1)) {
            $tail = $tail.Substring($firstNewline + 1)
        }

        $entry = [ordered]@{
            schemaVersion  = 1
            timestamp      = (Get-Date).ToUniversalTime().ToString('o')
            operationId    = 'journal-retention'
            action         = 'OperationJournal'
            phase          = 'retention'
            target         = $global:OPERATION_JOURNAL_PATH
            safetyDecision = 'Allowed'
            result         = 'Trimmed'
            wouldChange    = $true
            reversible     = $false
            rollbackHint   = 'Older operation journal entries were trimmed to keep local diagnostics bounded.'
            data           = @{
                previousBytes = $file.Length
                retainedBytes = [System.Text.Encoding]::UTF8.GetByteCount($tail)
                maxBytes      = $maxBytes
            }
        }
        $json = $entry | ConvertTo-Json -Compress -Depth 6
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($global:OPERATION_JOURNAL_PATH, $json + [Environment]::NewLine + $tail, $utf8NoBom)
    } catch {
        try { Write-Log "Operation journal retention failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}

function Write-OperationJournalEntry {
    param(
        [string]$OperationId = $global:CURRENT_OPERATION_ID,
        [string]$Action = $global:CURRENT_OPERATION_ACTION,
        [string]$Phase = 'event',
        [string]$Target = '',
        [string]$SafetyDecision = 'NotEvaluated',
        [string]$Result = 'Info',
        [bool]$WouldChange = $false,
        [bool]$Reversible = $false,
        [string]$RollbackHint = '',
        [string]$TokenKind = '',
        [string]$PreviousStateRef = '',
        [string]$NewState = '',
        [string]$UndoAction = '',
        [string]$Risk = '',
        [hashtable]$Data = $null
    )
    try {
        if ([string]::IsNullOrWhiteSpace($OperationId)) { $OperationId = [Guid]::NewGuid().ToString('N') }
        if ([string]::IsNullOrWhiteSpace($Action)) { $Action = 'Unknown' }
        if ([string]::IsNullOrWhiteSpace($TokenKind)) {
            switch ($Phase) {
                'config' { $TokenKind = 'configWrite'; break }
                'path' { $TokenKind = if ($Result -eq 'Removed') { 'pathEntryRemove' } else { 'pathEntryAdd' }; break }
                'task' { $TokenKind = if ($Result -eq 'Removed') { 'watcherTaskRemove' } else { 'watcherTaskRegister' }; break }
                'cache' { $TokenKind = 'cacheCleared'; break }
                'appx' { $TokenKind = 'spotifyUninstall'; break }
                'remove' {
                    $TokenKind = if ($Target -match 'Spicetify') { 'spicetifyUninstall' } elseif ($Target -match 'LibreSpot|Config') { 'selfDataRemoved' } else { 'fullReset' }
                    break
                }
            }
        }
        if ([string]::IsNullOrWhiteSpace($UndoAction)) { $UndoAction = $RollbackHint }
        if ([string]::IsNullOrWhiteSpace($Risk)) {
            $Risk = switch ($TokenKind) {
                'fullReset' { 'destructive' }
                'spotifyUninstall' { 'destructive' }
                'spicetifyUninstall' { 'destructive' }
                'selfDataRemoved' { 'high' }
                'watcherTaskRemove' { 'medium' }
                'spotxPatch' { 'medium' }
                'spicetifyApply' { 'medium' }
                default { 'low' }
            }
        }
        if ([string]::IsNullOrWhiteSpace($NewState)) { $NewState = $Result }
        if ($Reversible -and [string]::IsNullOrWhiteSpace($PreviousStateRef)) {
            $PreviousStateRef = if ([string]::IsNullOrWhiteSpace($Target)) { "operation:$OperationId" } else { "target:$Target" }
        }
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        Optimize-OperationJournalRetention
        $entry = [ordered]@{
            schemaVersion  = 1
            timestamp      = (Get-Date).ToUniversalTime().ToString('o')
            operationId    = $OperationId
            action         = $Action
            phase          = $Phase
            target         = $Target
            safetyDecision = $SafetyDecision
            result         = $Result
            wouldChange    = $WouldChange
            reversible     = $Reversible
            rollbackHint   = $RollbackHint
            tokenKind      = $TokenKind
            previousStateRef = $PreviousStateRef
            newState       = $NewState
            undoAction     = $UndoAction
            risk           = $Risk
        }
        if ($Data) { $entry.data = $Data }
        $json = $entry | ConvertTo-Json -Compress -Depth 6
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::AppendAllText($global:OPERATION_JOURNAL_PATH, $json + [Environment]::NewLine, $utf8NoBom)
    } catch {
        try { Write-Log "Operation journal write failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}

function Start-OperationJournalRun {
    param(
        [string]$Action,
        [string]$Target = '',
        [bool]$WouldChange = $true,
        [bool]$Reversible = $false,
        [string]$RollbackHint = ''
    )
    $global:CURRENT_OPERATION_ID = [Guid]::NewGuid().ToString()
    $global:CURRENT_OPERATION_ACTION = $Action
    Write-OperationJournalEntry -OperationId $global:CURRENT_OPERATION_ID -Action $Action -Phase 'planned' -Target $Target -SafetyDecision 'Pending' -Result 'Started' -WouldChange $WouldChange -Reversible $Reversible -RollbackHint $RollbackHint
    Write-Log "Operation id: $global:CURRENT_OPERATION_ID"
    return $global:CURRENT_OPERATION_ID
}

function Complete-OperationJournalRun {
    param(
        [string]$Result = 'Succeeded',
        [string]$Message = ''
    )
    Write-OperationJournalEntry -Phase 'complete' -Target $Message -SafetyDecision 'NotEvaluated' -Result $Result -WouldChange $false -Reversible $false
    try {
        if ([string]::IsNullOrWhiteSpace($global:RUN_RECEIPT_PATH) -or [string]::IsNullOrWhiteSpace($global:CURRENT_OPERATION_ID)) { return }
        if (-not (Test-Path -LiteralPath $global:OPERATION_JOURNAL_PATH -PathType Leaf)) { return }

        $entries = @()
        foreach ($line in (Get-Content -LiteralPath $global:OPERATION_JOURNAL_PATH -Tail 500 -ErrorAction SilentlyContinue)) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $entry = $line | ConvertFrom-Json -ErrorAction Stop
                if ($entry.operationId -eq $global:CURRENT_OPERATION_ID) { $entries += $entry }
            } catch {}
        }

        $operationEntries = @($entries |
            Where-Object { $_.tokenKind -and $_.phase -ne 'planned' -and $_.phase -ne 'complete' } |
            ForEach-Object {
                [ordered]@{
                    tokenKind        = [string]$_.tokenKind
                    target           = [string]$_.target
                    previousStateRef = [string]$_.previousStateRef
                    newState         = [string]$_.newState
                    result           = if ($_.result -eq 'Failed') { 'failed' } elseif ($_.result -eq 'Skipped') { 'skipped' } else { 'applied' }
                    reversible       = [bool]$_.reversible
                    undoAction       = [string]$_.undoAction
                    risk             = [string]$_.risk
                }
            })

        $status = switch ($Result) {
            'Succeeded' { 'success' }
            'Canceled' { 'canceled' }
            'Cancelled' { 'canceled' }
            'DryRun' { 'dryRun' }
            'PartialSuccess' { 'partialSuccess' }
            default { 'failed' }
        }

        $firstEntry = @($entries | Select-Object -First 1)
        $startedAt = if ($firstEntry.Count -gt 0 -and $firstEntry[0].timestamp) { [string]$firstEntry[0].timestamp } else { (Get-Date).ToUniversalTime().ToString('o') }
        $undoAvailable = @($operationEntries | Where-Object { $_.reversible -and -not [string]::IsNullOrWhiteSpace($_.previousStateRef) }).Count -gt 0
        $receipt = [ordered]@{
            schemaVersion = 1
            receiptId     = [Guid]::NewGuid().ToString()
            runId         = $global:CURRENT_OPERATION_ID
            operationId   = $global:CURRENT_OPERATION_ID
            startedAt     = $startedAt
            completedAt   = (Get-Date).ToUniversalTime().ToString('o')
            action        = $global:CURRENT_OPERATION_ACTION
            status        = $status
            errorSummary  = if ($status -eq 'failed') { $Message } else { $null }
            undoAvailable = $undoAvailable
            logRef        = $global:LOG_PATH
            operations    = $operationEntries
        }

        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($global:RUN_RECEIPT_PATH, ($receipt | ConvertTo-Json -Depth 6), $utf8NoBom)
    } catch {
        try { Write-Log "Run receipt write failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}

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
        $parts = $text -split "\r\n|\n|\r"
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

# CVE-2025-54100: a Windows PowerShell 5.1 remote-code-execution flaw (CVSS 7.8,
# fixed in the December 2025 Windows cumulative updates) in web-content handling.
# Content fetched by Invoke-WebRequest can execute at parse time on an unpatched
# host -- exactly LibreSpot's download pattern. SHA256 pinning guarantees payload
# *integrity* but does not remove the parse-time vector, so we surface a
# non-blocking heads-up when the host looks unpatched. PowerShell 7+ (Core) is a
# separate product and is not affected. Pure and side-effect free for unit tests.
function Get-DownloaderCveExposure {
    $result = [ordered]@{
        Exposed = $false
        Status  = 'NotAffected'   # NotAffected | Patched | PossiblyExposed | Unknown
        Reason  = ''
        Edition = [string]$PSVersionTable.PSEdition
        OSBuild = ''
    }
    # Only Windows PowerShell 5.1 (Desktop edition) is in scope for this CVE.
    if ($PSVersionTable.PSEdition -and $PSVersionTable.PSEdition -ne 'Desktop') {
        $result.Reason = 'PowerShell 7+ (Core) is in use; CVE-2025-54100 affects Windows PowerShell 5.1 only.'
        return [pscustomobject]$result
    }

    try {
        $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop
        if ($cv.CurrentBuild) { $result.OSBuild = "$($cv.CurrentBuild).$($cv.UBR)" }
    } catch {}

    # Heuristic: the newest installed update vs the December 2025 patch wave.
    # We never claim certainty -- this only flags a host that is plainly behind.
    $patchWave = [datetime]'2025-12-09'
    $latest = $null
    try {
        $latest = Get-HotFix -ErrorAction Stop |
            Where-Object { $_.InstalledOn } |
            Sort-Object InstalledOn -Descending |
            Select-Object -First 1
    } catch {}

    if ($null -eq $latest -or $null -eq $latest.InstalledOn) {
        $result.Status = 'Unknown'
        $result.Reason = 'Could not read the host update history to confirm the December 2025 PowerShell fix (CVE-2025-54100). Keep Windows fully updated.'
        return [pscustomobject]$result
    }
    if ($latest.InstalledOn -ge $patchWave) {
        $result.Status = 'Patched'
        $result.Reason = "Latest Windows update ($($latest.HotFixID), $($latest.InstalledOn.ToString('yyyy-MM-dd'))) is at or past the December 2025 fix for CVE-2025-54100."
        return [pscustomobject]$result
    }

    $result.Exposed = $true
    $result.Status  = 'PossiblyExposed'
    $result.Reason  = "The newest Windows update on this host is from $($latest.InstalledOn.ToString('yyyy-MM-dd')), before the December 2025 cumulative update that fixes CVE-2025-54100 (a Windows PowerShell 5.1 web-content RCE). LibreSpot still hash-verifies every download, but install pending Windows updates to close the parse-time vector."
    return [pscustomobject]$result
}

# Emits the CVE heads-up at most once per run, into whatever log is active. Never
# blocks: a possibly-exposed host still installs, it just gets told to patch.
function Write-DownloaderCveWarningIfNeeded {
    if ($global:CveDownloaderWarned) { return }
    $global:CveDownloaderWarned = $true
    try {
        $exposure = Get-DownloaderCveExposure
        if ($exposure.Exposed) {
            Write-Log "Security: $($exposure.Reason)" -Level 'WARN'
        }
    } catch {}
}

# Records the PowerShell security context for support diagnostics. Execution
# policy is a SAFETY feature, not a security boundary (Microsoft docs): running
# with -ExecutionPolicy Bypass does NOT defeat AppLocker or Windows Defender
# Application Control (WDAC), which force ConstrainedLanguage mode. We surface
# language mode + execution-policy scopes so CLM/WDAC blocks can be told apart
# from ordinary script errors. Pure and side-effect free for unit testing.
function Get-PowerShellSecurityContext {
    $ctx = [ordered]@{
        Edition             = [string]$PSVersionTable.PSEdition
        Version             = [string]$PSVersionTable.PSVersion
        LanguageMode        = ''
        ExecutionPolicies   = ''
        ConstrainedLanguage = $false
        AppControlEnforced  = $false
    }
    try { $ctx.LanguageMode = [string]$ExecutionContext.SessionState.LanguageMode } catch {}
    if ($ctx.LanguageMode -eq 'ConstrainedLanguage') {
        $ctx.ConstrainedLanguage = $true
        # CLM is forced by AppLocker, WDAC, or Smart App Control (SAC on Win11).
        $ctx.AppControlEnforced = $true
    }
    try {
        $scopes = Get-ExecutionPolicy -List -ErrorAction Stop |
            ForEach-Object { "$($_.Scope)=$($_.ExecutionPolicy)" }
        $ctx.ExecutionPolicies = ($scopes -join '; ')
    } catch {}
    return [pscustomobject]$ctx
}

# Logs the PS security context once per run and warns (without telling users to
# weaken enterprise controls) when application control is enforced.
function Write-PowerShellSecurityContext {
    if ($global:PsSecurityContextLogged) { return }
    $global:PsSecurityContextLogged = $true
    try {
        $ctx = Get-PowerShellSecurityContext
        Write-Log "PowerShell context: $($ctx.Edition) $($ctx.Version); language mode $($ctx.LanguageMode); execution policy [$($ctx.ExecutionPolicies)]."
        if ($ctx.AppControlEnforced) {
            Write-Log "This host enforces ConstrainedLanguage mode (AppLocker, Windows Defender Application Control, or Smart App Control). LibreSpot's scripts may be blocked. This is a platform-level control, not a LibreSpot error, and -ExecutionPolicy Bypass does not bypass it. On managed devices, ask your administrator to allow LibreSpot/SpotX. On personal devices with Smart App Control (Windows 11), open Settings > Privacy & security > Windows Security > App & browser control > Smart App Control settings to adjust. Alternatively, use the pre-compiled LibreSpot.exe from the Releases page." -Level 'WARN'
        }
    } catch {}
}

# Classifies whether a failure looks like a language-mode / app-control block
# rather than an ordinary script error, so support copy can be specific.
function Test-IsLanguageModeOrAppControlError {
    param([string]$Message)
    if ([string]::IsNullOrWhiteSpace($Message)) {
        try { return ([string]$ExecutionContext.SessionState.LanguageMode -eq 'ConstrainedLanguage') } catch { return $false }
    }
    return ($Message -match 'ConstrainedLanguage|language mode|AppLocker|Application Control|\bWDAC\b')
}

# Guidance shown when a file LibreSpot verified moments ago has vanished, or a
# known extension / custom-app file is missing. The usual cause is a security
# product (Microsoft Defender or another endpoint suite) quarantining it.
# LibreSpot only DETECTS and GUIDES -- it never disables AV, adds exclusions, or
# auto-restores quarantined files. Pure and side-effect free for unit testing.
function Get-QuarantineGuidance {
    param([string]$What = 'A verified file')
    return "$What is missing right after LibreSpot verified it. A security product (for example Microsoft Defender) may have quarantined it. Open Windows Security > Virus & threat protection > Protection history; if the file is listed, restore it and re-run LibreSpot. LibreSpot will not disable your antivirus, add exclusions, or restore quarantined files for you."
}

function Get-DownloadFailureHint {
    param(
        [string]$Uri,
        [object]$ErrorRecord,
        [string]$Stage = 'Download'
    )
    $message = ''
    try { $message = [string]$ErrorRecord.Exception.Message } catch { $message = [string]$ErrorRecord }
    $statusCode = $null
    try {
        if ($ErrorRecord.Exception.Response -and $ErrorRecord.Exception.Response.StatusCode) {
            $statusCode = [int]$ErrorRecord.Exception.Response.StatusCode
        }
    } catch {}
    $target = $Uri
    try { $target = ([uri]$Uri).Host } catch {}
    $lowerMessage = $message.ToLowerInvariant()
    if ($statusCode -eq 407 -or $lowerMessage -match 'proxy.*auth|407|proxy authentication') {
        return "$Stage failed: proxy authentication is required for $target. Configure the system or WinHTTP proxy before retrying."
    }
    if ($statusCode -eq 429 -or (($statusCode -eq 403) -and ($target -match 'github'))) {
        return "$Stage failed: GitHub rate limit or access block for $target. Wait for the rate-limit reset or retry from a network with GitHub access."
    }
    if ($lowerMessage -match 'could not be resolved|name resolution|no such host|\bdns\b') {
        return "$Stage failed: DNS could not resolve $target. Check DNS, VPN, firewall, or content-filtering rules."
    }
    if ($lowerMessage -match 'ssl|tls|certificate|trust relationship') {
        return "$Stage failed: TLS or certificate validation failed for $target. Check system time, enterprise TLS inspection, and root certificates."
    }
    if ($lowerMessage -match 'timed out|timeout') {
        return "$Stage failed: the connection to $target timed out. Check connectivity or retry after the network is stable."
    }
    if ($lowerMessage -match 'sha256 mismatch|hash mismatch|checksum') {
        return "$Stage hash verification failed for $target. The downloaded file does not match the expected SHA256 checksum. Try clearing the asset cache and re-downloading."
    }
    if ([string]::IsNullOrWhiteSpace($message)) {
        return "$Stage failed for $target."
    }
    return "$Stage failed for ${target}: $message"
}

function Download-FileSafe { param([string]$Uri,[string]$OutFile)
    Write-DownloaderCveWarningIfNeeded
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
            $webHint = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'Web request'
            Write-Log "$webHint Trying BITS fallback." -Level 'WARN'
            try {
                Import-Module BitsTransfer -EA SilentlyContinue
                $bitsJob = Start-BitsTransfer -Source $Uri -Destination $OutFile -Asynchronous -EA Stop
                $deadline = (Get-Date).AddSeconds(120)
                while ($bitsJob.JobState -in @('Transferring','Connecting','Queued','TransientError')) {
                    if ((Get-Date) -gt $deadline) { Remove-BitsTransfer $bitsJob -EA SilentlyContinue; throw "BITS transfer timed out (120s)" }
                    Start-Sleep -Milliseconds 500
                }
                if ($bitsJob.JobState -ne 'Transferred') {
                    $js=$bitsJob.JobState
                    $bitsDetail = "BITS state: $js"
                    try { if ($bitsJob.ErrorDescription) { $bitsDetail = "$bitsDetail - $($bitsJob.ErrorDescription)" } } catch {}
                    Remove-BitsTransfer $bitsJob -EA SilentlyContinue
                    throw $bitsDetail
                }
                Complete-BitsTransfer $bitsJob
            } catch {
                $bitsHint = Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage 'BITS'
                throw "Download failed after WebRequest and BITS fallback. $webHint $bitsHint"
            }
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
function Open-VerifiedScriptForExecution {
    param(
        [string]$FilePath,
        [string]$ExpectedHash = '',
        [string]$Label = 'script'
    )

    if ([string]::IsNullOrWhiteSpace($FilePath)) {
        throw "No script path was provided for $Label."
    }

    $fullPath = [System.IO.Path]::GetFullPath($FilePath)
    $stream = $null
    try {
        $stream = [System.IO.File]::Open($fullPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHash)) {
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $actualHash = -join ($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') })
            } finally {
                if ($sha) { $sha.Dispose() }
            }

            if ($actualHash -ne $ExpectedHash.ToLowerInvariant()) {
                throw "$Label hash mismatch immediately before execution. Expected $ExpectedHash, got $actualHash. Refusing to run."
            }

            if ($stream.CanSeek) {
                $stream.Position = 0
            }
        }

        return $stream
    } catch {
        if ($stream) { $stream.Dispose() }
        throw
    }
}

function Get-FileSha256Lower {
    param([Parameter(Mandatory)][string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return (($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) -join '')
    } finally {
        $stream.Dispose()
        $sha.Dispose()
    }
}

function Confirm-FileHash { param([string]$Path, [string]$ExpectedHash, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($ExpectedHash)) {
        Write-Log "  Hash verification skipped for $Label (no hash pinned)" -Level 'WARN'
        return
    }
    $actual = Get-FileSha256Lower -Path $Path
    $expected = $ExpectedHash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "SHA256 hash mismatch for ${Label}`n  Expected: $expected`n  Actual:   $actual`n  File may be corrupted or tampered with. Update pinned hash if this is a legitimate new version."
    }
    Write-Log "  SHA256 verified: $Label"
}

function Update-AssetCacheIndexEntry {
    param(
        [string]$SHA256Hash,
        [string]$Label = '',
        [string]$SourceUrl = '',
        [object]$ByteSize = $null,
        [string]$Status = 'present',
        [switch]$MarkUsed,
        [switch]$MarkVerified,
        [string]$QuarantinedPath = ''
    )

    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return }

    try {
        if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
            New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
        }

        $indexPath = Join-Path $global:CACHE_DIR 'asset-cache-index.json'
        $now = (Get-Date).ToUniversalTime().ToString('o')
        $entries = @()
        if (Test-Path -LiteralPath $indexPath -PathType Leaf) {
            try {
                $existingDoc = Get-Content -LiteralPath $indexPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
                if ($existingDoc.entries) {
                    $entries = @($existingDoc.entries)
                }
            } catch {
                $entries = @()
            }
        }

        $existing = $entries | Where-Object { $_.sha256 -eq $hash } | Select-Object -First 1
        $remaining = @($entries | Where-Object { $_.sha256 -ne $hash })
        $cachePath = Join-Path $global:CACHE_DIR $hash
        $resolvedByteSize = $ByteSize
        if ($null -eq $resolvedByteSize -and (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
            $resolvedByteSize = (Get-Item -LiteralPath $cachePath).Length
        }
        if ($null -eq $resolvedByteSize -and $existing) {
            $resolvedByteSize = $existing.byteSize
        }
        if ($null -eq $resolvedByteSize) {
            $resolvedByteSize = 0
        }

        $entry = [ordered]@{
            sha256            = $hash
            label             = if (-not [string]::IsNullOrWhiteSpace($Label)) { $Label } elseif ($existing -and $existing.label) { [string]$existing.label } else { 'Cached asset' }
            sourceUrl         = if (-not [string]::IsNullOrWhiteSpace($SourceUrl)) { $SourceUrl } elseif ($existing -and $existing.sourceUrl) { [string]$existing.sourceUrl } else { $null }
            byteSize          = [int64]$resolvedByteSize
            firstSeenAtUtc    = if ($existing -and $existing.firstSeenAtUtc) { [string]$existing.firstSeenAtUtc } else { $now }
            lastUsedAtUtc     = if ($MarkUsed) { $now } elseif ($existing -and $existing.lastUsedAtUtc) { [string]$existing.lastUsedAtUtc } else { $null }
            lastVerifiedAtUtc = if ($MarkVerified) { $now } elseif ($existing -and $existing.lastVerifiedAtUtc) { [string]$existing.lastVerifiedAtUtc } else { $null }
            status            = if ([string]::IsNullOrWhiteSpace($Status)) { 'present' } else { $Status }
            quarantinedPath   = if ([string]::IsNullOrWhiteSpace($QuarantinedPath)) { $null } else { $QuarantinedPath }
        }

        $doc = [ordered]@{
            schemaVersion  = 1
            generatedAtUtc = $now
            entries        = @($remaining + [pscustomobject]$entry | Sort-Object sha256)
        }

        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($indexPath, ($doc | ConvertTo-Json -Depth 8), $utf8)
    } catch {
        try { Write-Log "  Asset cache index update failed: $($_.Exception.Message)" -Level 'WARN' } catch {}
    }
}

function Save-ToAssetCache { param([string]$SourcePath, [string]$SHA256Hash, [string]$Label = '', [string]$SourceUrl = '')
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return }
    try {
        if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
            New-Item -Path $global:CACHE_DIR -ItemType Directory -Force | Out-Null
        }
        $cachePath = Join-Path $global:CACHE_DIR $hash
        Copy-Item -LiteralPath $SourcePath -Destination $cachePath -Force
        $byteSize = (Get-Item -LiteralPath $cachePath).Length
        Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -SourceUrl $SourceUrl -ByteSize $byteSize -Status 'present' -MarkVerified -MarkUsed
        Write-Log "  Cached verified asset (SHA256: $hash)"
    } catch {
        Write-Log "  Asset cache save failed: $($_.Exception.Message)" -Level 'WARN'
    }
}

function Get-FromAssetCache { param([string]$SHA256Hash, [string]$DestinationPath, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($SHA256Hash)) { return $false }
    $hash = $SHA256Hash.ToLowerInvariant()
    if ($hash.Length -ne 64) { return $false }
    $cachePath = Join-Path $global:CACHE_DIR $hash
    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf)) {
        Write-Log "  Cache miss for $Label (SHA256: $hash)"
        return $false
    }
    try {
        $actual = Get-FileSha256Lower -Path $cachePath
        if ($actual -ne $hash) {
            Write-Log "  Cached asset for $Label failed re-verification (expected $hash, got $actual). Quarantining stale entry." -Level 'WARN'
            $byteSize = (Get-Item -LiteralPath $cachePath).Length
            $corruptDirectory = Join-Path $global:CACHE_DIR 'corrupt'
            if (-not (Test-Path -LiteralPath $corruptDirectory -PathType Container)) {
                New-Item -Path $corruptDirectory -ItemType Directory -Force | Out-Null
            }
            $quarantinePath = Join-Path $corruptDirectory ("$hash-" + (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss') + '.bad')
            Move-Item -LiteralPath $cachePath -Destination $quarantinePath -Force -ErrorAction SilentlyContinue
            Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -ByteSize $byteSize -Status 'corrupt' -MarkVerified -QuarantinedPath $quarantinePath
            Write-OperationJournalEntry -Phase 'cache' -Target $cachePath -SafetyDecision 'Allowed' -Result 'Quarantined' -WouldChange $true -Reversible $false -RollbackHint 'The corrupt cached asset was moved aside and will be downloaded again on demand.' -Data @{
                label = $Label
                expectedSha256 = $hash
                observedSha256 = $actual
                quarantinePath = $quarantinePath
            }
            return $false
        }
        $outDir = Split-Path -Path $DestinationPath -Parent
        if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
            New-Item -Path $outDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -LiteralPath $cachePath -Destination $DestinationPath -Force
        $byteSize = (Get-Item -LiteralPath $cachePath).Length
        Update-AssetCacheIndexEntry -SHA256Hash $hash -Label $Label -ByteSize $byteSize -Status 'present' -MarkVerified -MarkUsed
        Write-Log "  Using verified cached copy for $Label (SHA256: $hash)"
        return $true
    } catch {
        Write-Log "  Cache retrieval failed for ${Label}: $($_.Exception.Message)" -Level 'WARN'
        return $false
    }
}

function Clear-LibreSpotCache {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    if (-not (Test-Path -LiteralPath $global:CACHE_DIR -PathType Container)) {
        Write-Log 'Asset cache directory does not exist. Nothing to clear.'
        return
    }
    if ($PSCmdlet.ShouldProcess($global:CACHE_DIR, 'Clear asset cache')) {
        $cacheFiles = @(Get-ChildItem -LiteralPath $global:CACHE_DIR -File -Recurse -ErrorAction SilentlyContinue)
        $byteMeasure = $cacheFiles | Measure-Object -Property Length -Sum
        $totalBytes = if ($null -eq $byteMeasure.Sum) { [int64]0 } else { [int64]$byteMeasure.Sum }
        Write-OperationJournalEntry -Phase 'cache' -Target $global:CACHE_DIR -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Cache will be rebuilt automatically on next download.' -Data @{
            fileCount = $cacheFiles.Count
            totalBytes = $totalBytes
        }
        try {
            Remove-Item -LiteralPath $global:CACHE_DIR -Recurse -Force -ErrorAction Stop
            Write-OperationJournalEntry -Phase 'cache' -Target $global:CACHE_DIR -SafetyDecision 'Allowed' -Result 'Cleared' -WouldChange $true -Reversible $false -RollbackHint 'Cache will be rebuilt automatically on next download.' -Data @{
                fileCount = $cacheFiles.Count
                totalBytes = $totalBytes
            }
            Write-Log "Asset cache cleared ($($cacheFiles.Count) file(s), $totalBytes bytes)."
        } catch {
            Write-Log "Failed to clear asset cache: $($_.Exception.Message)" -Level 'WARN'
        }
    }
}

function Expand-ArchiveSafely { param([string]$ZipPath,[string]$DestinationPath,[string]$Label='archive',[int]$MaxEntries=10000,[long]$MaxExpandedBytes=500MB)
    # ZipFile/ZipFileExtensions live in System.IO.Compression.FileSystem on .NET
    # Framework (PS 5.1); loading only System.IO.Compression leaves them
    # unresolvable in a clean powershell.exe process.
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $zip = $null
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
        if ($zip.Entries.Count -gt $MaxEntries) {
            throw "Archive '$Label' contains $($zip.Entries.Count) entries (limit $MaxEntries)."
        }
        $fullDest = [System.IO.Path]::GetFullPath($DestinationPath).TrimEnd('\') + '\'
        $totalDeclaredBytes = 0L
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $normalized = $name.Replace('/', '\')
            if ([System.IO.Path]::IsPathRooted($normalized)) {
                throw "Archive '$Label' contains an absolute path entry: $name"
            }
            if ($normalized.Contains('..\') -or $normalized.StartsWith('..') -or $normalized.EndsWith('..')) {
                throw "Archive '$Label' contains a path traversal entry: $name"
            }
            $fullTarget = [System.IO.Path]::GetFullPath((Join-Path $DestinationPath $normalized))
            if (-not $fullTarget.StartsWith($fullDest, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Archive '$Label' entry escapes destination: $name"
            }
            $totalDeclaredBytes += $entry.Length
            if ($totalDeclaredBytes -gt $MaxExpandedBytes) {
                throw "Archive '$Label' declared expanded size exceeds limit ($([math]::Round($MaxExpandedBytes / 1MB))MB)."
            }
        }
        $totalActualBytes = 0L
        $copyBuffer = New-Object byte[] 81920
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $targetPath = [System.IO.Path]::GetFullPath((Join-Path $DestinationPath ($name.Replace('/', '\'))))
            if ($name.EndsWith('/') -or $name.EndsWith('\')) {
                [System.IO.Directory]::CreateDirectory($targetPath) | Out-Null
                continue
            }
            $parentDir = [System.IO.Path]::GetDirectoryName($targetPath)
            if (-not [string]::IsNullOrWhiteSpace($parentDir)) {
                [System.IO.Directory]::CreateDirectory($parentDir) | Out-Null
            }
            $tempTargetPath = "$targetPath.librespot-extract-$([guid]::NewGuid().ToString('N')).tmp"
            $entryStream = $null
            $targetStream = $null
            $entrySucceeded = $false
            try {
                $entryStream = $entry.Open()
                $targetStream = [System.IO.File]::Open($tempTargetPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
                while (($bytesRead = $entryStream.Read($copyBuffer, 0, $copyBuffer.Length)) -gt 0) {
                    $totalActualBytes += $bytesRead
                    if ($totalActualBytes -gt $MaxExpandedBytes) {
                        throw "Archive '$Label' actual expanded size exceeds limit ($([math]::Round($MaxExpandedBytes / 1MB))MB)."
                    }
                    $targetStream.Write($copyBuffer, 0, $bytesRead)
                }
                $entrySucceeded = $true
            } finally {
                if ($targetStream) { $targetStream.Dispose() }
                if ($entryStream) { $entryStream.Dispose() }
                if (-not $entrySucceeded -and (Test-Path -LiteralPath $tempTargetPath -PathType Leaf)) {
                    Remove-Item -LiteralPath $tempTargetPath -Force -ErrorAction SilentlyContinue
                }
            }
            try {
                if (Test-Path -LiteralPath $targetPath -PathType Leaf) {
                    Remove-Item -LiteralPath $targetPath -Force
                }
                [System.IO.File]::Move($tempTargetPath, $targetPath)
            } catch {
                if (Test-Path -LiteralPath $tempTargetPath -PathType Leaf) {
                    Remove-Item -LiteralPath $tempTargetPath -Force -ErrorAction SilentlyContinue
                }
                throw
            }
        }
    } finally {
        if ($zip) { $zip.Dispose() }
    }
}

function Hide-SpotifyWindows {
    Get-Process -Name Spotify -EA SilentlyContinue | ForEach-Object {
        if ($_.MainWindowHandle -ne [IntPtr]::Zero) {
            [Win32]::ShowWindowAsync($_.MainWindowHandle, [Win32]::SW_HIDE) | Out-Null
        }
    }
}

function Get-SpotXChildFailureClassification {
    # SpotX can fail inside its OWN downloader after LibreSpot has already
    # hash-verified run.ps1 (SpotX issues #870, #836). Without classification
    # those runs surface as a generic "Process exited with code N". Returns
    # $null when no known signature matches, otherwise a stable category id
    # plus sanitized guidance (never echoes raw child output, which can
    # contain attacker-influenced mirror HTML).
    param([string]$Line)
    if ([string]::IsNullOrWhiteSpace($Line)) { return $null }

    if ($Line -match 'curl exit code 28|ERR_CONNECTION_TIMED_OUT|Operation timed out after') {
        return [pscustomobject]@{
            Category = 'SpotXChildDownloadTimeout'
            Guidance = "SpotX's own downloader timed out while fetching Spotify components. LibreSpot already verified the SpotX script itself, so this is an upstream network or CDN outage - retry in a few minutes, or choose a different download method under Custom Install > Advanced adjustments."
        }
    }

    if ($Line -match 'loadspot\.amd64fox1\.workers\.dev') {
        return [pscustomobject]@{
            Category = 'SpotXWorkerEndpointFailure'
            Guidance = "SpotX's Cloudflare worker download endpoint failed. This is an upstream SpotX outage (see SpotX issues #870/#836), not a problem on this machine - retry later, or choose a different download method under Custom Install > Advanced adjustments."
        }
    }

    if ($Line -match 'suspected phishing|reported for potential phishing|This website has been blocked') {
        return [pscustomobject]@{
            Category = 'SpotXMirrorBlockedPhishing'
            Guidance = 'A SpotX download mirror is currently flagged by Cloudflare as suspected phishing, so the download was blocked upstream. Turn off the mirror option (or retry without it) and run the setup again.'
        }
    }

    return $null
}

function Get-SpotXDownloadRetryPlan {
    # Maps a classified SpotX child-download failure (from
    # Get-SpotXChildFailureClassification) to a single automatic-retry plan.
    # Timeouts and Cloudflare-worker outages retry once through the SpotX
    # mirror; a mirror flagged as phishing retries once WITHOUT the mirror.
    # Returns $null when the failure is not download-retryable, or when the
    # useful mirror toggle was already the state of the failed attempt - this
    # guarantees at most one automatic retry and that the retry changes the
    # download path (a same-path retry would just fail the same way).
    param(
        [string]$Category,
        [bool]$MirrorAlreadyUsed
    )

    switch ($Category) {
        'SpotXChildDownloadTimeout' {
            if ($MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $true
                Reason    = "SpotX's download timed out; retrying once through the SpotX mirror."
            }
        }
        'SpotXWorkerEndpointFailure' {
            if ($MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $true
                Reason    = "SpotX's primary download endpoint failed; retrying once through the SpotX mirror."
            }
        }
        'SpotXMirrorBlockedPhishing' {
            if (-not $MirrorAlreadyUsed) { return $null }
            return [pscustomobject]@{
                UseMirror = $false
                Reason    = 'The SpotX mirror was blocked upstream; retrying once without the mirror.'
            }
        }
        default { return $null }
    }
}

function Invoke-ExternalScriptIsolated { param([string]$FilePath,[string]$Arguments,[int]$TimeoutSeconds=600,[string]$ExpectedHash='',[string]$Label='external script')
    Write-Log "Spawning: $FilePath"
    Write-PowerShellSecurityContext
    $stdoutPath = Join-Path $global:TEMP_DIR ("LibreSpot-stdout-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stderrPath = Join-Path $global:TEMP_DIR ("LibreSpot-stderr-" + [Guid]::NewGuid().ToString('N') + '.log')
    $stdoutState = @{ Offset = 0L; Remainder = '' }
    $stderrState = @{ Offset = 0L; Remainder = '' }
    # The spawned powershell.exe can be forced into ConstrainedLanguage by WDAC /
    # AppLocker even when this host is FullLanguage; classify that from stderr.
    $appControlHintShown = $false
    # SpotX child-download outages (timeouts, Cloudflare worker failures,
    # phishing-flagged mirrors) otherwise surface as a bare exit code.
    $childFailure = $null
    $scriptGuard = $null
    $p = $null
    try {
        $scriptGuard = Open-VerifiedScriptForExecution -FilePath $FilePath -ExpectedHash $ExpectedHash -Label $Label
        if (-not [string]::IsNullOrWhiteSpace($ExpectedHash)) {
            Write-Log "  Execution copy verified and locked for $Label"
        }
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
            foreach ($line in $stdoutRead.Lines) {
                Write-Log $line -Level 'OUT'
                if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
            }

            $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
            $stderrState = @{ Offset = $stderrRead.Offset; Remainder = $stderrRead.Remainder }
            foreach ($line in $stderrRead.Lines) {
                Write-Log "[STDERR] $line" -Level 'WARN'
                if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
                if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                    $appControlHintShown = $true
                    Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker, Windows Defender Application Control, or Smart App Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these controls. On managed devices, ask your administrator. On personal devices with Smart App Control (Windows 11), adjust it in Settings > Privacy & security > Windows Security. Alternatively, use LibreSpot.exe from the Releases page." -Level 'WARN'
                }
            }
            Start-Sleep -Milliseconds 200
        }
        $p.WaitForExit()

        $stdoutRead = Read-ProcessOutputDelta -Path $stdoutPath -Offset $stdoutState.Offset -Remainder $stdoutState.Remainder
        foreach ($line in $stdoutRead.Lines + @($stdoutRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log $line -Level 'OUT'
            if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
        }
        $stderrRead = Read-ProcessOutputDelta -Path $stderrPath -Offset $stderrState.Offset -Remainder $stderrState.Remainder
        foreach ($line in $stderrRead.Lines + @($stderrRead.Remainder) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
            Write-Log "[STDERR] $line" -Level 'WARN'
            if (-not $childFailure) { $childFailure = Get-SpotXChildFailureClassification -Line $line }
            if (-not $appControlHintShown -and (Test-IsLanguageModeOrAppControlError -Message $line)) {
                $appControlHintShown = $true
                Write-Log "This looks like a PowerShell application-control / ConstrainedLanguage block (AppLocker, Windows Defender Application Control, or Smart App Control), not a normal LibreSpot error. -ExecutionPolicy Bypass does not bypass these controls. On managed devices, ask your administrator. On personal devices with Smart App Control (Windows 11), adjust it in Settings > Privacy & security > Windows Security. Alternatively, use LibreSpot.exe from the Releases page." -Level 'WARN'
            }
        }

        # Capture ExitCode defensively. Windows PowerShell can occasionally lose
        # the Process handle when Start-Process is combined with redirected output.
        $exitCode = $null
        try { $exitCode = $p.ExitCode } catch { $exitCode = $null }

        if ($null -eq $exitCode) {
            # Windows PowerShell can drop the ExitCode when Start-Process is paired
            # with redirected output. Don't blindly assume success: if the child's
            # own output already classified a failure (download outage, phishing
            # mirror, patch abort), surface it instead of masking it.
            if ($childFailure) {
                Write-Log $childFailure.Guidance -Level 'WARN'
                try {
                    Write-OperationJournalEntry -Phase 'external' -Target $FilePath -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint $childFailure.Guidance -Data @{ failureCategory = $childFailure.Category; exitCode = 'unavailable' }
                } catch {}
                throw "Process reported a failure and its exit code was unavailable [$($childFailure.Category)]"
            }
            Write-Log 'External process finished but ExitCode was unavailable and no failure signal was found in its output; treating as success. The caller verifies the result independently.' -Level 'WARN'
        } elseif ($exitCode -ne 0) {
            if ($childFailure) {
                Write-Log $childFailure.Guidance -Level 'WARN'
                try {
                    Write-OperationJournalEntry -Phase 'external' -Target $FilePath -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint $childFailure.Guidance -Data @{ failureCategory = $childFailure.Category; exitCode = $exitCode }
                } catch {}
                throw "Process exited with code $exitCode [$($childFailure.Category)]"
            }
            throw "Process exited with code $exitCode"
        }
    } finally {
        if ($p) { try { $p.Dispose() } catch {} }
        if ($scriptGuard) { try { $scriptGuard.Dispose() } catch {} }
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

# =============================================================================
# 13. UPDATE CHECKER
# =============================================================================
function Invoke-GitHubApiSafe { param([string]$Uri,[hashtable]$Headers,[int]$TimeoutSec=15,[string]$Label='GitHub API')
    try {
        $response = Invoke-WebRequest -Uri $Uri -Headers $Headers -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
        $remaining = $response.Headers['x-ratelimit-remaining']
        if ($remaining -and [int]$remaining -le 5) {
            $resetEpoch = $response.Headers['x-ratelimit-reset']
            $resetTime = if ($resetEpoch) { ([DateTimeOffset]::FromUnixTimeSeconds([long]$resetEpoch)).LocalDateTime.ToString('HH:mm:ss') } else { 'unknown' }
            Write-Log "GitHub API rate limit nearly exhausted ($remaining remaining, resets at $resetTime). Subsequent checks may fail." -Level 'WARN'
        }
        return ($response.Content | ConvertFrom-Json)
    } catch {
        $statusCode = $null
        if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        if ($statusCode -eq 403 -or $statusCode -eq 429) {
            $resetEpoch = $null
            try { $resetEpoch = $_.Exception.Response.Headers['x-ratelimit-reset'] } catch {}
            $resetMsg = ''
            if ($resetEpoch) {
                $resetTime = ([DateTimeOffset]::FromUnixTimeSeconds([long]$resetEpoch)).LocalDateTime.ToString('HH:mm:ss')
                $resetMsg = " Rate limit resets at $resetTime."
            }
            throw "GitHub API rate limit reached for $Label (HTTP $statusCode).$resetMsg Try again later or use an authenticated request."
        }
        throw (Get-DownloadFailureHint -Uri $Uri -ErrorRecord $_ -Stage $Label)
    }
}

function Check-ForUpdates {
    Write-Log '=== Checking for dependency updates ===' -Level 'STEP'
    $headers = @{'User-Agent'="LibreSpot/$global:VERSION"}
    $updates = @()
    $compatWarnings = @()

    # SpotX (pinned to a specific commit on main, check for newer commits)
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/SpotX-Official/SpotX/commits/main' -Headers $headers -Label 'SpotX'
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
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/cli/releases/latest' -Headers $headers -Label 'Spicetify CLI'
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.SpicetifyCLI.Version
        if (Compare-LibreSpotVersions -Latest $latest -Current $pinned) { $updates += "CLI: $pinned -> $latest"; Write-Log "  Spicetify CLI: $pinned -> $latest available" -Level 'WARN' }
        else { Write-Log "  Spicetify CLI: v$pinned (up to date)" }
    } catch { Write-Log "  Spicetify CLI: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Marketplace
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/marketplace/releases/latest' -Headers $headers -Label 'Marketplace'
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.Marketplace.Version
        if (Compare-LibreSpotVersions -Latest $latest -Current $pinned) { $updates += "Marketplace: $pinned -> $latest"; Write-Log "  Marketplace: $pinned -> $latest available" -Level 'WARN' }
        else { Write-Log "  Marketplace: v$pinned (up to date)" }
    } catch { Write-Log "  Marketplace: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Themes
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/spicetify/spicetify-themes/commits/master' -Headers $headers -Label 'Themes'
        $latest = $rel.sha
        $pinned = $global:PinnedReleases.Themes.Commit
        if ($latest -ne $pinned) {
            $short = $latest.Substring(0,10)
            $msg = ($rel.commit.message -split "`n")[0]
            $updates += "Themes: new commit $short"
            Write-Log "  Themes: new commit $short ($msg)" -Level 'WARN'
        } else { Write-Log "  Themes: $($pinned.Substring(0,10)) (up to date)" }
    } catch { Write-Log "  Themes: check failed ($($_.Exception.Message))" -Level 'WARN' }

    $compatWarnings = @(Write-LibreSpotCompatibilityMatrix)

    # LibreSpot itself
    try {
        $rel = Invoke-GitHubApiSafe -Uri 'https://api.github.com/repos/SysAdminDoc/LibreSpot/releases/latest' -Headers $headers -Label 'LibreSpot'
        $latest = $rel.tag_name -replace '^v',''
        if (Compare-LibreSpotVersions -Latest $latest -Current $global:VERSION) {
            $updates += "LibreSpot: $($global:VERSION) -> $latest"
            Write-Log "  LibreSpot: $($global:VERSION) -> $latest available" -Level 'WARN'
        } else {
            Write-Log "  LibreSpot: v$($global:VERSION) (up to date)"
        }
    } catch { Write-Log "  LibreSpot: check failed ($($_.Exception.Message))" -Level 'WARN' }

    if ($updates.Count -eq 0 -and $compatWarnings.Count -eq 0) {
        Write-Log "All dependencies and compatibility baselines are up to date." -Level 'SUCCESS'
    } else {
        if ($updates.Count -eq 0) {
            Write-Log "All pinned dependency versions are current." -Level 'SUCCESS'
        }
        if ($updates.Count -gt 0) {
            Write-Log "$($updates.Count) update(s) available. Update the PinnedReleases block in the script to upgrade." -Level 'WARN'
        }
        if ($compatWarnings.Count -gt 0) {
            Write-Log "$($compatWarnings.Count) compatibility warning(s) detected; review the matrix above before repatching newer Spotify builds." -Level 'WARN'
        }
        if ($updates.Count -gt 0) {
            Write-Log "After updating versions, re-download each component and update its SHA256 hash." -Level 'WARN'
        }
    }
    Write-Log '=== Update check complete ===' -Level 'STEP'
}

function Get-UpstreamStalenessNotice {
    $cached = Read-UpstreamStalenessCache
    if ($cached) { return [string[]]$cached.Notices }

    $result = Invoke-UpstreamStalenessHttp
    if (-not $result) { return @() }

    $notices = [string[]]$result.Notices
    Save-UpstreamStalenessCache -Notices $notices
    return $notices
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

    $blockedRaw = @(
        $env:USERPROFILE,
        $env:APPDATA,
        $env:LOCALAPPDATA,
        $env:TEMP,
        $env:SystemRoot,
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        $env:ProgramData,
        $env:ALLUSERSPROFILE,
        $env:PUBLIC,
        $env:OneDrive,
        $env:OneDriveConsumer,
        $env:OneDriveCommercial,
        [Environment]::GetFolderPath('Desktop'),
        [Environment]::GetFolderPath('Personal'),
        [Environment]::GetFolderPath('CommonDesktopDirectory'),
        [Environment]::GetFolderPath('CommonStartMenu')
    )
    $blockedTargets = @($blockedRaw | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.TrimEnd('\') } | Sort-Object -Unique)

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

function Remove-PathSafely {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Path,[string]$Label)
    $displayLabel = if ($Label) { $Label } else { $Path }
    $journalData = @{ label = $displayLabel }
    if ([string]::IsNullOrWhiteSpace($Path)) { return 0 }
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'SkippedMissingTarget' -Result 'Skipped' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target did not exist.' -Data $journalData
        return 0
    }
    if (-not (Test-SafeRemovalTarget -Path $Path)) {
        Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'RefusedUnsafeTarget' -Result 'Refused' -WouldChange $false -Reversible $false -RollbackHint 'No files were removed because the target failed LibreSpot safe-removal checks.' -Data $journalData
        Write-Log "  Refusing to remove unsafe target: $Path" -Level 'WARN'
        return 0
    }
    Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
    if ($PSCmdlet.ShouldProcess($Path, 'Remove file or directory')) {
        try {
            # Junctions/symlinks planted inside removal roots must be deleted
            # as links, never traversed: PS 5.1 Remove-Item -Recurse follows
            # directory junctions into their targets, and icacls /T would
            # reset ACLs on the target tree — an elevated delete-anything
            # primitive for anyone who can write a link into these folders.
            $item = Get-Item -LiteralPath $Path -Force -EA Stop
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                $item.Delete()
                Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
                Write-Log "  Removed link (target untouched): $displayLabel"
                return 1
            }
            $null = & icacls.exe "$Path" /reset /T /C /Q 2>$null
            Remove-Item -LiteralPath $Path -Recurse -Force -EA Stop
            Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Restore from a backup if one exists.' -Data $journalData
            Write-Log "  Removed: $displayLabel"
            return 1
        } catch {
            $journalData['error'] = [string]$_.Exception.Message
            Write-OperationJournalEntry -Phase 'remove' -Target $Path -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'The target may be partially unchanged; review the error before retrying.' -Data $journalData
            Write-Log "  Failed to remove: $Path ($($_.Exception.Message))" -Level 'WARN'
            return 0
        }
    }
    return 0
}

function Module-NukeSpotify {
    Write-Log "=== LibreSpot Comprehensive Spotify Uninstaller ===" -Level 'STEP'
    $rc = 0

    # --- Phase 1: Kill all Spotify processes ---
    Write-Log "[Phase 1/7] Terminating Spotify processes..."
    Stop-SpotifyProcesses

    # --- Phase 2: Remove Spotify Store (UWP/AppX) ---
    Write-Log "[Phase 2/7] Checking for Microsoft Store Spotify..."
    try {
        if ($PSVersionTable.PSVersion.Major -ge 7) { Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue }
        $storeApp = Get-AppxPackage -Name "SpotifyAB.SpotifyMusic" -EA SilentlyContinue
        if ($storeApp) {
            Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedPP = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxPackage -Package $storeApp.PackageFullName -EA Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            } catch {
                Write-OperationJournalEntry -Phase 'appx' -Target $storeApp.PackageFullName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry removal or reinstall from the Microsoft Store.'
                throw
            } finally { $ProgressPreference = $savedPP }
            Write-Log "  Removed Spotify Store app."; $rc++
        } else { Write-Log "  No Store version found." }
    } catch { Write-Log "  Store removal failed: $($_.Exception.Message)" -Level 'WARN' }

    # Remove the provisioned package so new user profiles don't get Spotify pre-installed
    try {
        $provisioned = Get-AppxProvisionedPackage -Online -EA SilentlyContinue | Where-Object { $_.DisplayName -eq 'SpotifyAB.SpotifyMusic' }
        if ($provisioned) {
            Write-OperationJournalEntry -Phase 'appx' -Target $provisioned.PackageName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
            $savedPP = $ProgressPreference; $ProgressPreference = 'SilentlyContinue'
            try {
                Remove-AppxProvisionedPackage -Online -PackageName $provisioned.PackageName -EA Stop
                Write-OperationJournalEntry -Phase 'appx' -Target $provisioned.PackageName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Reinstall Spotify from the Microsoft Store.'
                Write-Log "  Removed provisioned Spotify package."; $rc++
            } finally { $ProgressPreference = $savedPP }
        }
    } catch { Write-Log "  Provisioned package removal skipped: $($_.Exception.Message)" -Level 'WARN' }

    # --- Phase 3: Nuke file system ---
    Write-Log "[Phase 3/7] Removing Spotify files and folders..."
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

    # --- Phase 4: Registry cleanup ---
    Write-Log "[Phase 4/7] Cleaning registry..."
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
            Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
            try {
                Remove-Item -Path $key -Recurse -Force -EA Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry key cannot be automatically restored.'
                Write-Log "  Removed: $key"; $rc++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $key -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
                Write-Log "  Failed: $key" -Level 'WARN'
            }
        }
    }
    $regValues = @(
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify" }
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify Web Helper" }
    )
    foreach ($rv in $regValues) {
        if (Get-ItemProperty -Path $rv.Path -Name $rv.Name -EA SilentlyContinue) {
            $regTarget = "$($rv.Path)\$($rv.Name)"
            Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
            try {
                Remove-ItemProperty -Path $rv.Path -Name $rv.Name -Force -EA Stop
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Registry value cannot be automatically restored.'
                Write-Log "  Removed startup: $($rv.Name)"; $rc++
            } catch {
                Write-OperationJournalEntry -Phase 'registry' -Target $regTarget -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry registry removal manually.'
            }
        }
    }

    # --- Phase 5: Scheduled tasks ---
    Write-Log "[Phase 5/7] Removing scheduled tasks..."
    try {
        $spotifyTaskNames = @('SpotifyMigrator', 'SpotifyUpdateTask', 'Spotify')
        Get-ScheduledTask -EA SilentlyContinue |
            Where-Object { $_.TaskName -in $spotifyTaskNames -or $_.TaskName -like 'Spotify-*' } |
            ForEach-Object {
                Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                try {
                    Unregister-ScheduledTask -TaskName $_.TaskName -Confirm:$false -EA Stop
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Removed' -WouldChange $true -Reversible $false -RollbackHint 'Re-register the scheduled task manually if needed.'
                    Write-Log "  Removed task: $($_.TaskName)"; $rc++
                } catch {
                    Write-OperationJournalEntry -Phase 'task' -Target $_.TaskName -SafetyDecision 'Allowed' -Result 'Failed' -WouldChange $true -Reversible $false -RollbackHint 'Retry scheduled task removal manually.'
                }
            }
    } catch { Write-Log "  Task cleanup skipped." }

    # --- Phase 6: Firewall rules ---
    Write-Log "[Phase 6/7] Removing firewall rules..."
    try {
        Get-NetFirewallRule -EA SilentlyContinue | Where-Object { $_.DisplayName -match 'Spotify' } | ForEach-Object {
            try { Remove-NetFirewallRule -Name $_.Name -EA Stop; Write-Log "  Removed firewall: $($_.DisplayName)"; $rc++ }
            catch {}
        }
    } catch { Write-Log "  Firewall cleanup skipped." }

    # --- Phase 7: Verification sweep (amd64fox/Uninstall-Spotify retry pattern) ---
    Write-Log "[Phase 7/7] Verification sweep..."
    $verifyPaths = @(
        (Join-Path $env:APPDATA "Spotify")
        (Join-Path $env:LOCALAPPDATA "Spotify")
        (Join-Path $env:APPDATA "spicetify")
        (Join-Path $env:LOCALAPPDATA "spicetify")
    )
    $maxRetries = 5
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        $remaining = @($verifyPaths | Where-Object { Test-Path $_ })
        if ($remaining.Count -eq 0) { break }
        if ($attempt -gt 1) { Write-Log "  Retry $attempt/$maxRetries ($($remaining.Count) path(s) still locked)..." }
        Start-Sleep -Milliseconds 1500
        foreach ($path in $remaining) {
            if (Remove-PathSafely -Path $path -Label "Cleanup retry: $(Split-Path $path -Leaf)") { $rc++ }
        }
    }
    $survivors = @($verifyPaths | Where-Object { Test-Path $_ })
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
    $customPatchesPath = ''
    try {
        $spotxHash = $global:PinnedReleases.SpotX.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $dest -Label 'SpotX run.ps1')) {
            try {
                Download-FileSafe -Uri $global:URL_SPOTX -OutFile $dest
            } catch {
                if (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $dest -Label 'SpotX run.ps1') {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $dest -ExpectedHash $spotxHash -Label "SpotX run.ps1"
            Save-ToAssetCache -SourcePath $dest -SHA256Hash $spotxHash -Label 'SpotX run.ps1' -SourceUrl $global:URL_SPOTX
        }
        $baseParams = Build-SpotXParams -Config $Config
        $customPatchesPath = New-SpotXCustomPatchesFile -Config $Config
        $patchSuffix = ''
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            $patchSuffix = " -CustomPatchesPath `"$customPatchesPath`""
            Write-Log "Custom SpotX patches staged at $customPatchesPath"
        }
        if (Test-Path $global:SPOTIFY_EXE_PATH) {
            $ver = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            Write-Log "Spotify $ver detected - SpotX will verify version compatibility"
        } else {
            Write-Log "Spotify not installed - SpotX will download recommended version"
        }
        Write-Log "Params: $($baseParams + $patchSuffix)"
        if ($SyncHash) { $SyncHash.AllowSpotify = $true }
        try {
            # SpotX can fail inside its own downloader after LibreSpot already
            # hash-verified run.ps1 (timeout, Cloudflare-worker outage, or a
            # mirror flagged as phishing). Invoke-ExternalScriptIsolated tags
            # those with a [SpotX...] category. On a classified download
            # failure, retry exactly once through the SpotX mirror (or, for a
            # phishing-blocked mirror, without it) before surfacing the error.
            $spotxMirrorInUse = [bool]$Config.SpotX_Mirror
            $spotxAttempt = 0
            while ($true) {
                $spotxAttempt++
                try {
                    Invoke-ExternalScriptIsolated -FilePath $dest -Arguments ($baseParams + $patchSuffix) -ExpectedHash $spotxHash -Label 'SpotX run.ps1'
                    break
                } catch {
                    $spotxCategory = if ($_.Exception.Message -match '\[(SpotX\w+)\]') { $Matches[1] } else { $null }
                    $spotxRetry = if ($spotxCategory -and $spotxAttempt -eq 1) {
                        Get-SpotXDownloadRetryPlan -Category $spotxCategory -MirrorAlreadyUsed $spotxMirrorInUse
                    } else { $null }
                    if (-not $spotxRetry) { throw }
                    Write-Log $spotxRetry.Reason -Level 'WARN'
                    $hasMirror = $baseParams -match '(^|\s)-mirror(\s|$)'
                    if ($spotxRetry.UseMirror -and -not $hasMirror) {
                        $baseParams = ($baseParams.Trim() + ' -mirror').Trim()
                    } elseif ((-not $spotxRetry.UseMirror) -and $hasMirror) {
                        $baseParams = ($baseParams -replace '(^|\s)-mirror(\s|$)', ' ').Trim()
                    }
                    $spotxMirrorInUse = $spotxRetry.UseMirror
                }
            }
            # Verify SpotX patching succeeded
            if (-not (Test-Path $global:SPOTIFY_EXE_PATH)) {
                throw "SpotX failed - Spotify.exe not found at $global:SPOTIFY_EXE_PATH. Check the log above for errors."
            }
            $elfDll = Join-Path (Split-Path $global:SPOTIFY_EXE_PATH) "chrome_elf.dll"
            if (-not (Test-Path $elfDll)) {
                throw "Spotify installation is incomplete - chrome_elf.dll is missing. This usually means the Spotify download failed or was corrupted."
            }
            $patchedVer = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion
            $verify = Get-SpotXPatchVerification -SpotifyExePath $global:SPOTIFY_EXE_PATH
            if ($verify.Verified) {
                Write-Log "Spotify $patchedVer patched and verified ($($verify.Signals -join '; '))." -Level 'SUCCESS'
            } else {
                Write-Log "Spotify ${patchedVer}: SpotX ran but the patch could not be verified. $($verify.Reason)" -Level 'WARN'
                Write-Log "If ads still play or the UI is blank, this Spotify build may resist SpotX patching (SpotX issue #760). Try Maintenance > Reapply, or Maintenance > Full Reset to start clean. As a fallback, enable 'Ad-block (Spicetify fallback)' in Custom Install to keep ad-blocking working at the Spicetify layer." -Level 'WARN'
            }
            Write-Log "Launching Spotify (hidden) to generate config files..."
            if (Test-Path $global:SPOTIFY_EXE_PATH) {
                # Force-close any Spotify the user (or SpotX) left running so this
                # first launch starts from a clean, freshly patched process, then
                # reopen it to generate the config files.
                Write-Log "Force-closing any running Spotify before the first launch..."
                Stop-SpotifyProcesses -maxAttempts 5
                Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$global:SPOTIFY_EXE_PATH`""
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
        if (-not [string]::IsNullOrWhiteSpace($customPatchesPath)) {
            Remove-Item -LiteralPath $customPatchesPath -Force -ErrorAction SilentlyContinue
        }
        Remove-Item -LiteralPath $dest -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallSpicetifyCLI {
    $integration = Get-SpicetifyIntegrationContext
    $ver = $global:PinnedReleases.SpicetifyCLI.Version
    Write-Log "Installing Spicetify CLI v$ver..." -Level 'STEP'
    New-Item -Path $integration.InstallDirectory -ItemType Directory -Force | Out-Null
    $arch = switch ($env:PROCESSOR_ARCHITECTURE) { 'ARM64' {'arm64'} default {'x64'} }
    $zip = $global:URL_SPICETIFY_FMT -f $ver, $arch
    $zp = New-LibreSpotTempFile -Name 'spicetify.zip'
    try {
        $expectedHash = $global:PinnedReleases.SpicetifyCLI.SHA256[$arch]
        if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zp -Label "Spicetify CLI ($arch)")) {
            try {
                Download-FileSafe -Uri $zip -OutFile $zp
            } catch {
                if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zp -Label "Spicetify CLI ($arch)") {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $zp -ExpectedHash $expectedHash -Label "Spicetify CLI ($arch)"
            Save-ToAssetCache -SourcePath $zp -SHA256Hash $expectedHash -Label "Spicetify CLI ($arch)" -SourceUrl $zip
        }
        if (Test-Path -LiteralPath $integration.InstallDirectory) {
            $null = Clear-DirectoryContentsSafely -Path $integration.InstallDirectory -Label 'Spicetify CLI'
        }
        Expand-ArchiveSafely -ZipPath $zp -DestinationPath $integration.InstallDirectory -Label 'Spicetify CLI'
        $sExe = $integration.CliPath
        if (-not (Test-Path $sExe)) { throw "spicetify.exe not found after extraction - ZIP may be corrupted" }
        $null = Add-PathEntry -Entry $integration.InstallDirectory -Scope 'Process'
        if (Add-PathEntry -Entry $integration.InstallDirectory -Scope 'User') {
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
    $td = (Get-SpicetifyIntegrationContext).ThemesDirectory
    if (-not (Test-Path $td)) { New-Item -Path $td -ItemType Directory -Force | Out-Null }

    $isCommunity = $global:CommunityThemeRepos.ContainsKey($tn)

    if ($isCommunity) {
        # Community theme — download commit-pinned archive and verify hash
        $repo = $global:CommunityThemeRepos[$tn]
        $archiveUrl = "https://github.com/$($repo.Owner)/$($repo.Repo)/archive/$($repo.CommitSha).zip"
        $safeName = ($tn -replace '[^a-zA-Z0-9_-]','_')
        $tz = New-LibreSpotTempFile -Name "community-theme-$safeName.zip"
        $tu = New-LibreSpotTempDirectory -Name "community-theme-$safeName-unpack"
        try {
            Write-Log "Downloading community theme from $($repo.Owner)/$($repo.Repo) @ $($repo.CommitSha.Substring(0,10))..."
            $themeHash = $repo.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themeHash -DestinationPath $tz -Label "Community theme '$tn'")) {
                try {
                    Download-FileSafe -Uri $archiveUrl -OutFile $tz
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themeHash -DestinationPath $tz -Label "Community theme '$tn'") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $tz -ExpectedHash $themeHash -Label "Community theme '$tn'"
                Save-ToAssetCache -SourcePath $tz -SHA256Hash $themeHash -Label "Community theme '$tn'" -SourceUrl $archiveUrl
            }
            Expand-ArchiveSafely -ZipPath $tz -DestinationPath $tu -Label "Community theme '$tn'"
            $root = Get-ChildItem -LiteralPath $tu -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw "Community theme archive for '$tn' did not contain a root folder." }
            $src = if ($repo.ThemeFolder -eq '.') { $root.FullName } else { Join-Path $root.FullName $repo.ThemeFolder }
            if (-not (Test-Path -LiteralPath $src -PathType Container)) {
                throw "Theme folder '$($repo.ThemeFolder)' was not found in the $($repo.Owner)/$($repo.Repo) archive."
            }
            # Verify the archive actually contains Spicetify theme files
            $hasColorIni = Test-Path -LiteralPath (Join-Path $src 'color.ini')
            $hasUserCss  = Test-Path -LiteralPath (Join-Path $src 'user.css')
            if (-not ($hasColorIni -or $hasUserCss)) {
                throw "Community theme '$tn' archive does not contain color.ini or user.css - not a valid Spicetify theme."
            }
            $dst = Join-Path $td $tn
            if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
            # Copy only theme-relevant files, not repo metadata (.git, .github, etc.)
            New-Item -Path $dst -ItemType Directory -Force | Out-Null
            $themeFiles = @('color.ini','user.css','theme.js','theme.script.js','assets','README.md')
            foreach ($tf in $themeFiles) {
                $tfSrc = Join-Path $src $tf
                if (Test-Path -LiteralPath $tfSrc) {
                    Copy-Item $tfSrc -Destination (Join-Path $dst $tf) -Recurse -Force
                }
            }
            Write-Log "Community theme '$tn' copied to $dst"
        } catch {
            Write-Log "Community theme '$tn' failed to install: $($_.Exception.Message). The install will continue without this theme." -Level 'WARN'
            return
        } finally {
            Remove-Item -LiteralPath $tz -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $tu -Recurse -Force -ErrorAction SilentlyContinue
        }
    } else {
        # Official theme — extract from the pinned spicetify-themes archive
        $tz = New-LibreSpotTempFile -Name 'themes.zip'
        $tu = New-LibreSpotTempDirectory -Name 'themes-unpack'
        try {
            $themesHash = $global:PinnedReleases.Themes.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $tz -Label 'Themes archive')) {
                try {
                    Download-FileSafe -Uri $global:URL_THEMES_REPO -OutFile $tz
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $themesHash -DestinationPath $tz -Label 'Themes archive') {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $tz -ExpectedHash $themesHash -Label "Themes archive"
                Save-ToAssetCache -SourcePath $tz -SHA256Hash $themesHash -Label 'Themes archive' -SourceUrl $global:URL_THEMES_REPO
            }
            Expand-ArchiveSafely -ZipPath $tz -DestinationPath $tu -Label 'Themes archive'
            $root = Get-ChildItem -LiteralPath $tu -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $root) { throw "Theme archive did not contain an unpacked root folder." }
            $src = Join-Path $root.FullName $tn
            if (-not (Test-Path -LiteralPath $src -PathType Container)) {
                throw "Theme '$tn' was not found in the pinned theme archive."
            }
            $dst = Join-Path $td $tn
            if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
            Copy-Item $src -Destination $dst -Recurse -Force
            Write-Log "Theme copied to $dst"
        } finally {
            Remove-Item -LiteralPath $tz -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $tu -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    if (-not (Test-Path (Join-Path $td $tn))) { return }
    $sc = $Config.Spicetify_Scheme; Write-Log "Setting theme=$tn, scheme=$sc"
    Invoke-SpicetifyCli -Arguments @('config', 'current_theme', $tn, '--bypass-admin') -FailureMessage "Could not set Spicetify theme '$tn'."
    if (-not [string]::IsNullOrWhiteSpace($sc)) {
        Invoke-SpicetifyCli -Arguments @('config', 'color_scheme', $sc, '--bypass-admin') -FailureMessage "Could not set color scheme '$sc'."
    }
    $needsThemeJs = $global:ThemesNeedingJS -contains $tn
    $jsVal = if ($needsThemeJs) { "1" } else { "0" }
    Invoke-SpicetifyCli -Arguments @('config', 'inject_css', '1', 'replace_colors', '1', 'overwrite_assets', '1', 'inject_theme_js', $jsVal, '--bypass-admin') -FailureMessage 'Could not enable the selected theme assets.'
}

function Download-CommunityExtensions { param($Config)
    $exts = @($Config.Spicetify_Extensions)
    $extDir = (Get-SpicetifyIntegrationContext).ExtensionsDirectory
    if (-not (Test-Path $extDir)) { New-Item -Path $extDir -ItemType Directory -Force | Out-Null }
    $verifiedPaths = @()
    foreach ($ext in $exts) {
        if (-not $global:CommunityExtensions.Contains($ext)) { continue }
        $info = $global:CommunityExtensions[$ext]
        $destFile = Join-Path $extDir $ext
        $tempFile = Join-Path $extDir (".librespot-$ext.$PID.$([Guid]::NewGuid().ToString('N')).tmp")
        try {
            Write-Log "Downloading community extension: $ext from $($info.Source)..."
            $extHash = $info.SHA256
            $fromCache = Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $tempFile -Label "Community extension $ext"
            if (-not $fromCache) {
                try {
                    Download-FileSafe -Uri $info.Url -OutFile $tempFile
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $extHash -DestinationPath $tempFile -Label "Community extension $ext") {
                        $fromCache = $true
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
            }
            # Sanity check: make sure we got JavaScript, not a 404 HTML page.
            # Read just the first 512 bytes to avoid loading a huge file.
            $head = Get-Content -LiteralPath $tempFile -TotalCount 5 -ErrorAction SilentlyContinue
            $headStr = ($head -join "`n").TrimStart()
            if ($headStr -match '^<(!DOCTYPE|html)' -or $headStr -match '^404:') {
                Write-Log "Community extension '$ext' downloaded but appears to be an HTML error page, not JavaScript. The URL may have changed. Skipping." -Level 'WARN'
                continue
            }
            Confirm-FileHash -Path $tempFile -ExpectedHash $extHash -Label "Community extension $ext"
            if (-not $fromCache) {
                Save-ToAssetCache -SourcePath $tempFile -SHA256Hash $extHash -Label "Community extension $ext" -SourceUrl $info.Url
            }
            Move-Item -LiteralPath $tempFile -Destination $destFile -Force
            Write-Log "Community extension '$ext' saved to $destFile"
            $verifiedPaths += $destFile
        } catch {
            Write-Log "Could not download community extension '$ext': $($_.Exception.Message). Skipping." -Level 'WARN'
        } finally {
            Remove-Item -LiteralPath $tempFile -Force -ErrorAction SilentlyContinue
        }
    }
    # A file LibreSpot just verified that has since vanished is the classic
    # antivirus-quarantine signal. Detect and guide; never auto-restore.
    $quarantineWarned = $false
    foreach ($vp in $verifiedPaths) {
        if (-not $quarantineWarned -and -not (Test-Path -LiteralPath $vp)) {
            $quarantineWarned = $true
            Write-Log (Get-QuarantineGuidance -What "The verified extension file '$(Split-Path -Leaf $vp)'") -Level 'WARN'
        }
    }
}

function Module-InstallExtensions { param($Config)
    $exts = @($Config.Spicetify_Extensions)
    if ($exts.Count -eq 0) {
        Write-Log "Extensions: none selected. Removing LibreSpot-managed extensions if they are still enabled..." -Level 'STEP'
    } else {
        Write-Log "Extensions: $($exts -join ', ')..." -Level 'STEP'
    }
    # Download any selected community extensions to the Extensions folder first
    Download-CommunityExtensions -Config $Config
    $allManaged = @($global:BuiltInExtensions.Keys) + @($global:CommunityExtensions.Keys) + @($global:DeprecatedCommunityExtensionNames)
    Sync-SpicetifyListSetting -Key 'extensions' -DesiredItems $exts -ManagedItems $allManaged
}

function Module-InstallMarketplace { param($Config)
    $integration = Get-SpicetifyIntegrationContext
    $managedApps = @('marketplace')
    $marketplaceDirs = @(
        $integration.MarketplaceDirectory,
        $integration.LegacyMarketplaceDirectory
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
    $ca = $integration.CustomAppsDirectory
    New-Item -Path $ca -ItemType Directory -Force | Out-Null
    $md=Join-Path $ca "marketplace"
    $mz = New-LibreSpotTempFile -Name 'marketplace.zip'
    $mu = New-LibreSpotTempDirectory -Name 'marketplace-unpack'
    foreach ($dir in $marketplaceDirs) {
        $null = Remove-PathSafely -Path $dir -Label 'Marketplace app'
    }
    New-Item -Path $md -ItemType Directory -Force | Out-Null
    try {
        $marketplaceHash = $global:PinnedReleases.Marketplace.SHA256
        if (-not (Get-FromAssetCache -SHA256Hash $marketplaceHash -DestinationPath $mz -Label 'Marketplace archive')) {
            try {
                Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $mz
            } catch {
                if (Get-FromAssetCache -SHA256Hash $marketplaceHash -DestinationPath $mz -Label 'Marketplace archive') {
                    Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                } else { throw }
            }
            Confirm-FileHash -Path $mz -ExpectedHash $marketplaceHash -Label "Marketplace"
            Save-ToAssetCache -SourcePath $mz -SHA256Hash $marketplaceHash -Label 'Marketplace archive' -SourceUrl $global:URL_MARKETPLACE
        }
        Expand-ArchiveSafely -ZipPath $mz -DestinationPath $mu -Label 'Marketplace'
        $sp = if (Test-Path (Join-Path $mu "marketplace-dist")) { Join-Path $mu "marketplace-dist\*" } else { Join-Path $mu "*" }
        Copy-Item -Path $sp -Destination $md -Recurse -Force
        $health = Get-MarketplaceHealth
        if (-not $health.HasFiles) {
            throw 'Marketplace archive did not produce expected Spicetify custom app files.'
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @('marketplace') -ManagedItems $managedApps
        $health = Get-MarketplaceHealth
        if ($health.IsReady) {
            Write-Log "Marketplace enabled. If Spotify hides the sidebar icon, open spotify:app:marketplace directly."
        } else {
            Write-Log "Marketplace files were installed but status is '$($health.Status)'. Use Maintenance > Repair and open Marketplace if the sidebar icon is hidden." -Level 'WARN'
        }
    } finally {
        Remove-Item -LiteralPath $mz -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $mu -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Open-SpicetifyMarketplace {
    $requestedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    try {
        Start-Process -FilePath 'explorer.exe' -ArgumentList 'spotify:app:marketplace'
        Write-Log 'Requested Spotify Marketplace via spotify:app:marketplace.'
        Start-Sleep -Milliseconds 500
        $spotifyRunning = try { @((Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)).Count -gt 0 } catch { $false }
        $result = [pscustomobject]@{
            Succeeded               = $true
            Message                 = 'spotify:app:marketplace was handed to Windows.'
            RequestedAtUtc          = $requestedAtUtc
            SpotifyRunningAfterOpen = $spotifyRunning
        }
    } catch {
        $message = "Could not open spotify:app:marketplace automatically: $($_.Exception.Message)"
        Write-Log $message -Level 'WARN'
        $result = [pscustomobject]@{
            Succeeded               = $false
            Message                 = $message
            RequestedAtUtc          = $requestedAtUtc
            SpotifyRunningAfterOpen = $null
        }
    }
    Write-MarketplaceVisibilityEvidence -Source 'OpenMarketplace' -OpenUriSucceeded $result.Succeeded -OpenUriMessage $result.Message -OpenUriRequestedAtUtc $result.RequestedAtUtc -SpotifyRunningAfterOpen $result.SpotifyRunningAfterOpen | Out-Null
    return $result
}

function Module-InstallCustomApps { param($Config)
    $requestedApps = @($Config.Spicetify_CustomApps | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $managedApps = @($global:CommunityCustomApps.Keys)
    $integration = Get-SpicetifyIntegrationContext
    $customAppsDirectory = $integration.CustomAppsDirectory

    if ($requestedApps.Count -eq 0) {
        Write-Log 'Custom apps: none selected. Removing LibreSpot-managed custom apps if present...' -Level 'STEP'
        foreach ($appId in $managedApps) {
            $null = Remove-PathSafely -Path (Join-Path $customAppsDirectory $appId) -Label "Custom app $appId"
        }
        Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @() -ManagedItems $managedApps
        return
    }

    Write-Log "Custom apps: $($requestedApps -join ', ')..." -Level 'STEP'
    New-Item -Path $customAppsDirectory -ItemType Directory -Force | Out-Null
    $installedApps = [System.Collections.Generic.List[string]]::new()

    foreach ($appId in $requestedApps) {
        if (-not $global:CommunityCustomApps.Contains($appId)) {
            Write-Log "Unknown custom app '$appId'. Skipping." -Level 'WARN'
            continue
        }

        $info = $global:CommunityCustomApps[$appId]
        $safeName = ($appId -replace '[^a-zA-Z0-9_-]', '_')
        $zipPath = New-LibreSpotTempFile -Name "custom-app-$safeName.zip"
        $unpackPath = New-LibreSpotTempDirectory -Name "custom-app-$safeName-unpack"
        $destinationPath = Join-Path $customAppsDirectory $appId

        try {
            Write-Log "Downloading custom app '$($info.DisplayName)' from $($info.Source)..."
            $expectedHash = [string]$info.SHA256
            if (-not (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zipPath -Label "Custom app $appId archive")) {
                try {
                    Download-FileSafe -Uri $info.Url -OutFile $zipPath
                } catch {
                    if (Get-FromAssetCache -SHA256Hash $expectedHash -DestinationPath $zipPath -Label "Custom app $appId archive") {
                        Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                    } else { throw }
                }
                Confirm-FileHash -Path $zipPath -ExpectedHash $expectedHash -Label "Custom app $appId"
                Save-ToAssetCache -SourcePath $zipPath -SHA256Hash $expectedHash -Label "Custom app $appId archive" -SourceUrl $info.Url
            }

            Expand-ArchiveSafely -ZipPath $zipPath -DestinationPath $unpackPath -Label "Custom app $appId" -MaxExpandedBytes 250MB
            $sourcePath = Join-Path $unpackPath ([string]$info.AssetPath)
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
                $candidate = Get-ChildItem -LiteralPath $unpackPath -Directory -ErrorAction SilentlyContinue |
                    Where-Object {
                        (Test-Path -LiteralPath (Join-Path $_.FullName 'manifest.json') -PathType Leaf) -and
                        (Test-Path -LiteralPath (Join-Path $_.FullName 'extension.js') -PathType Leaf)
                    } |
                    Select-Object -First 1
                if ($candidate) { $sourcePath = $candidate.FullName }
            }

            if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
                throw "Custom app archive did not contain expected folder '$($info.AssetPath)'."
            }

            foreach ($requiredFile in @('manifest.json', 'extension.js')) {
                if (-not (Test-Path -LiteralPath (Join-Path $sourcePath $requiredFile) -PathType Leaf)) {
                    throw "Custom app '$appId' is missing required file '$requiredFile'."
                }
            }

            $null = Remove-PathSafely -Path $destinationPath -Label "Custom app $appId"
            New-Item -Path $destinationPath -ItemType Directory -Force | Out-Null
            Copy-Item -Path (Join-Path $sourcePath '*') -Destination $destinationPath -Recurse -Force
            $installedApps.Add($appId)
            Write-Log "Custom app '$($info.DisplayName)' installed to $destinationPath"
        } catch {
            Write-Log "Could not install custom app '$appId': $($_.Exception.Message). Skipping." -Level 'WARN'
        } finally {
            Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $unpackPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Sync-SpicetifyListSetting -Key 'custom_apps' -DesiredItems @($installedApps) -ManagedItems $managedApps
}

function Write-MarketplaceVisibilityEvidence {
    param(
        [string]$Source = 'Unknown',
        [string]$ApplyStage = '',
        [object]$ApplySucceeded = $null,
        [string]$ApplyMessage = '',
        [object]$OpenUriSucceeded = $null,
        [string]$OpenUriMessage = '',
        [object]$OpenUriRequestedAtUtc = $null,
        [object]$SpotifyRunningAfterOpen = $null
    )

    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }

        $health = Get-MarketplaceHealth
        $manifestPath = Join-Path $health.Path 'manifest.json'
        $manifestVersion = $null
        if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
            try {
                $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json -ErrorAction Stop
                foreach ($property in @('version','Version','marketplaceVersion')) {
                    if ($manifest.PSObject.Properties.Name -contains $property) {
                        $value = [string]$manifest.$property
                        if (-not [string]::IsNullOrWhiteSpace($value)) {
                            $manifestVersion = $value
                            break
                        }
                    }
                }
            } catch {
                $manifestVersion = $null
            }
        }

        $applySucceededValue = if ($null -ne $ApplySucceeded) { [bool]$ApplySucceeded } else { $null }
        $openSucceededValue = if ($null -ne $OpenUriSucceeded) { [bool]$OpenUriSucceeded } else { $null }
        $spotifyRunningValue = if ($null -ne $SpotifyRunningAfterOpen) {
            [bool]$SpotifyRunningAfterOpen
        } else {
            try { @((Get-Process -Name 'Spotify' -ErrorAction SilentlyContinue)).Count -gt 0 } catch { $null }
        }
        $openRequestedAt = if ($OpenUriRequestedAtUtc) { [string]$OpenUriRequestedAtUtc } else { $null }
        $applyCompletedAt = if ($null -ne $applySucceededValue) { (Get-Date).ToUniversalTime().ToString('o') } else { $null }
        $lastObservedAt = if ($null -ne $spotifyRunningValue) { (Get-Date).ToUniversalTime().ToString('o') } else { $null }
        $lastObservedSession = if ($null -eq $spotifyRunningValue) {
            'not observed'
        } elseif ($spotifyRunningValue) {
            'spotify-process-running'
        } else {
            'spotify-process-not-running'
        }
        $likelyVisible = [bool]($health.HasFiles -and $health.IsEnabled -and ($applySucceededValue -eq $true) -and ($openSucceededValue -eq $true))

        $doc = [ordered]@{
            schemaVersion              = 1
            generatedAtUtc             = (Get-Date).ToUniversalTime().ToString('o')
            source                     = $Source
            filesPresent               = [bool]$health.HasFiles
            registered                 = [bool]$health.IsEnabled
            likelyVisible              = $likelyVisible
            marketplaceStatus          = [string]$health.Status
            marketplacePath            = [string]$health.Path
            manifestVersion            = $manifestVersion
            applyStage                 = $ApplyStage
            applySucceeded             = $applySucceededValue
            applyMessage               = $ApplyMessage
            applyCompletedAtUtc        = $applyCompletedAt
            openUriSucceeded           = $openSucceededValue
            openUriMessage             = $OpenUriMessage
            openUriRequestedAtUtc      = $openRequestedAt
            spotifyRunningAfterOpen    = $spotifyRunningValue
            lastObservedSpotifySession = $lastObservedSession
            lastObservedAtUtc          = $lastObservedAt
        }

        $path = Join-Path $global:CONFIG_DIR 'marketplace-evidence.json'
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($path, ($doc | ConvertTo-Json -Depth 5), $utf8)
        Write-OperationJournalEntry -Phase 'marketplace' -Target $path -SafetyDecision 'Allowed' -Result 'Recorded' -WouldChange $true -Reversible $false -RollbackHint 'Re-run Repair Marketplace or Reapply to refresh Marketplace visibility evidence.' -Data @{
            source = $Source
            marketplaceStatus = $health.Status
            likelyVisible = $likelyVisible
            applySucceeded = $applySucceededValue
            openUriSucceeded = $openSucceededValue
        }
        return [pscustomobject]$doc
    } catch {
        try { Write-Log "Marketplace visibility evidence could not be recorded: $($_.Exception.Message)" -Level 'WARN' } catch {}
        return $null
    }
}

function Repair-Marketplace {
    param($Config)
    if (-not (Test-SpicetifyCliInstalled)) {
        throw 'Spicetify CLI is not installed, so LibreSpot cannot repair Marketplace yet. Run Recommended setup or Reapply first.'
    }
    if (-not $Config) {
        $Config = Normalize-LibreSpotConfig -Config @{}
    }
    $Config.Spicetify_Marketplace = $true

    Write-Log 'Repairing Marketplace files and custom_apps registration...' -Level 'STEP'
    Module-InstallMarketplace -Config $Config
    Write-Log 'Applying Spicetify so Marketplace is discoverable in Spotify...' -Level 'STEP'
    $applyResult = Module-ApplySpicetify -Config $Config -EvidenceSource 'RepairMarketplace'

    $health = Get-MarketplaceHealth
    if ($health.IsReady) {
        Write-Log "Marketplace repair verified at $($health.Path)." -Level 'SUCCESS'
    } else {
        Write-Log "Marketplace repair finished, but status is '$($health.Status)'. Open spotify:app:marketplace directly if the sidebar icon remains hidden." -Level 'WARN'
    }
    $openResult = Open-SpicetifyMarketplace
    Write-MarketplaceVisibilityEvidence -Source 'RepairMarketplace' -ApplyStage $applyResult.Stage -ApplySucceeded $applyResult.Succeeded -ApplyMessage $applyResult.Message -OpenUriSucceeded $openResult.Succeeded -OpenUriMessage $openResult.Message -OpenUriRequestedAtUtc $openResult.RequestedAtUtc -SpotifyRunningAfterOpen $openResult.SpotifyRunningAfterOpen | Out-Null
}

function Get-SpicetifyDiagnosticSnapshot {
    $snapshot = [ordered]@{}
    $configPath = (Get-SpicetifyIntegrationContext).ConfigPath
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

function Module-ApplySpicetify {
    param(
        $Config,
        [string]$EvidenceSource = 'Module-ApplySpicetify'
    )
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

    Write-Log "Ensuring Spotify is fully closed before patching files..."
    Stop-SpotifyProcesses -MaxAttempts 3

    # Spicetify expects `backup apply` as a combined invocation — especially after
    # SpotX has patched the client (version mismatch between Spotify and any prior
    # backup). Running them separately causes "version mismatch" failures.
    $applyError = $null
    $applyStage = 'backup apply'
    try {
        Invoke-SpicetifyCli -Arguments @('backup', 'apply', '--bypass-admin') -FailureMessage 'Could not apply the selected Spicetify setup.'
        Write-Log "Spicetify applied successfully."
        $message = 'Spicetify backup apply succeeded.'
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $true -ApplyMessage $message | Out-Null
        return [pscustomobject]@{
            Stage     = $applyStage
            Succeeded = $true
            Message   = $message
        }
    } catch {
        $applyError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown Spicetify apply error.' }
        Write-Log "Spicetify backup apply failed: $applyError" -Level 'WARN'
    }

    Write-Log "Attempting rollback to keep Spotify usable..." -Level 'WARN'
    $restoreError = $null
    try {
        Invoke-SpicetifyCli -Arguments @('restore', '--bypass-admin') -FailureMessage 'Could not restore Spotify after the failed apply.'
    } catch {
        $restoreError = if ($_.Exception -and $_.Exception.Message) { [string]$_.Exception.Message } else { 'Unknown restore error.' }
    }

    if ([string]::IsNullOrWhiteSpace($restoreError)) {
        Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $false -ApplyMessage $applyError | Out-Null
        throw "Spicetify apply failed but LibreSpot restored Spotify to a usable state. Apply error: $applyError"
    }

    Write-MarketplaceVisibilityEvidence -Source $EvidenceSource -ApplyStage $applyStage -ApplySucceeded $false -ApplyMessage "$applyError | Rollback error: $restoreError" | Out-Null
    throw "Spicetify apply failed and rollback also failed. Apply error: $applyError | Rollback error: $restoreError"
}

function Reapply-SavedSpicetifySetup { param($Config)
    if (-not (Test-SpicetifyCliInstalled)) {
        Write-Log "Spicetify CLI is missing, so LibreSpot will reinstall it before restoring your saved setup." -Level 'WARN'
        Module-InstallSpicetifyCLI
    }

    Module-InstallThemes -Config $Config
    Module-InstallExtensions -Config $Config
    Module-InstallMarketplace -Config $Config
    Module-InstallCustomApps -Config $Config
    Module-ApplySpicetify -Config $Config -EvidenceSource 'Reapply' | Out-Null
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
            # Re-check IsRunning too: when the job finishes while we are in the
            # grace loop (e.g. Repair just opened the Marketplace), the freshly
            # launched Spotify must not be killed on the way out.
            if ($sh.IsRunning -and -not $sh.AllowSpotify) { Stop-Process -Name Spotify -Force -EA SilentlyContinue }
        }
        Start-Sleep -Milliseconds 500
    }
}

$installBlock = { param($sh,$cfg)
    $script:syncHash = $sh
    $ErrorActionPreference = 'Stop'
    try {
        $modeName = if ($cfg -and $cfg.Mode) { [string]$cfg.Mode } else { 'Unknown' }
        Start-OperationJournalRun -Action 'Install' -Target "LibreSpot install ($modeName)" -WouldChange $true -Reversible $false -RollbackHint 'Use Maintenance > Restore Vanilla or Full Reset to reverse applied customizations.' | Out-Null
        Write-Log "--- LibreSpot Installation Started ---" -Level 'HEADER'; Write-Log "Mode: $($cfg.Mode)"
        $steps = @('SpotX','SpicetifyCLI','Themes','Extensions','Marketplace','CustomApps','Apply')
        if ($cfg.CleanInstall) { $steps = @('Cleanup') + $steps }
        $stepLabels = @{
            Cleanup      = 'Removing the old setup'
            SpotX        = 'Applying SpotX'
            SpicetifyCLI = 'Installing Spicetify CLI'
            Themes       = 'Adding bundled themes'
            Extensions   = 'Preparing extensions'
            Marketplace  = 'Installing Marketplace'
            CustomApps   = 'Adding custom apps'
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
                'CustomApps'   { Module-InstallCustomApps -Config $cfg }
                'Apply'        { Module-ApplySpicetify -Config $cfg | Out-Null }
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
        if ($cfg.LaunchAfter -and (Test-Path $global:SPOTIFY_EXE_PATH)) {
            # Stop the killer/hider watcher BEFORE the handoff: with IsRunning
            # still true it force-closed the Spotify we just launched during
            # the 20-second stability window (and with AllowSpotify it would
            # hide the window instead). All install steps are done here.
            $sh.IsRunning = $false
            Write-Log "Launching Spotify..." -Level 'SUCCESS'
            Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$global:SPOTIFY_EXE_PATH`""
            $finalStep = 'Spotify is opening'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Verifying Spotify session stability" })
            Test-SpotifySessionStability -WaitSeconds 20 | Out-Null
        }
        Complete-OperationJournalRun -Result 'Succeeded' -Message 'Install completed.'
        Write-Log "--- Installation Complete ---" -Level 'SUCCESS'; $sh.IsRunning=$false
        $installDoneContext = if ($cfg.LaunchAfter -and (Test-Path $global:SPOTIFY_EXE_PATH)) {
            'LibreSpot finished applying your selected setup and is handing off to Spotify now.'
        } else {
            'LibreSpot finished applying your selected setup. You can close the window or copy the detailed log for reference.'
        }
        $sh.Dispatcher.Invoke([Action]{ $sh.ProgressBar.Value=100; $sh.StatusLabel.Text="Setup complete"; $sh.StepLabel.Text=$finalStep; $sh.InstallTitle.Text='Setup complete'; $sh.InstallContext.Text=$installDoneContext; $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Tag="Copy full log"; $sh.CopyLogBtn.Content="Copy full log"; $sh.CopyLogBtn.Visibility="Visible"; if($sh.TitleCloseBtn){$sh.TitleCloseBtn.ToolTip="Close LibreSpot"}; if($sh.MinimizeBtn){$sh.MinimizeBtn.ToolTip="Minimize"}; if($sh.Timer){$sh.Timer.Stop()}; $sh.Window.Topmost=$false; $sh.Window.Activate(); try{[Win32]::FlashTaskbar($sh.WindowHandle)}catch{} })
    } catch { $sh.IsRunning=$false; $em=$_.Exception.Message; $st=$_.ScriptStackTrace
        try { Complete-OperationJournalRun -Result 'Failed' -Message $em } catch {}
        try { Write-Log "[FATAL] $em`n$st" -Level 'ERROR' } catch {}
        $sh.Dispatcher.Invoke([Action]{ if($sh.Timer){$sh.Timer.Stop()}; $sh.LogBlock.Text+="`n[FATAL] $em`n$st"; $sh.StatusLabel.Text="Setup stopped"
            $sh.StepLabel.Text="Needs attention"; $sh.InstallTitle.Text='Setup needs attention'; $sh.InstallContext.Text='LibreSpot stopped before the install finished. Review the log below, then go back to setup or copy the details if you want to troubleshoot.'; $sh.ProgressBar.Foreground=$global:BrushError; $sh.ProgressBar.Value=100; $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Tag="Copy full log"; $sh.CopyLogBtn.Content="Copy full log"; $sh.CopyLogBtn.Visibility="Visible"; if($sh.TitleCloseBtn){$sh.TitleCloseBtn.ToolTip="Close LibreSpot"}; if($sh.MinimizeBtn){$sh.MinimizeBtn.ToolTip="Minimize"}; $sh.Window.Topmost=$false; $sh.Window.Activate(); try{[Win32]::FlashTaskbar($sh.WindowHandle)}catch{} })
    }
}

$maintBlock = { param($sh,$action)
    $script:syncHash = $sh
    $ErrorActionPreference = 'Stop'
    try {
        $maintenanceWouldChange = ($action -notin @('CheckUpdates','OpenMarketplace'))
        Start-OperationJournalRun -Action $action -Target "Maintenance action: $action" -WouldChange $maintenanceWouldChange -Reversible $false -RollbackHint 'Review individual journal entries for action-specific rollback hints.' | Out-Null
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
                $spotxHash = $global:PinnedReleases.SpotX.SHA256
                if (-not (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $dest -Label 'SpotX run.ps1')) {
                    try {
                        Download-FileSafe -Uri $global:URL_SPOTX -OutFile $dest
                    } catch {
                        if (Get-FromAssetCache -SHA256Hash $spotxHash -DestinationPath $dest -Label 'SpotX run.ps1') {
                            Write-Log 'Network download failed; using verified cached copy.' -Level 'WARN'
                        } else { throw }
                    }
                    Confirm-FileHash -Path $dest -ExpectedHash $spotxHash -Label "SpotX run.ps1"
                    Save-ToAssetCache -SourcePath $dest -SHA256Hash $spotxHash -Label 'SpotX run.ps1' -SourceUrl $global:URL_SPOTX
                }
                Write-Log "SpotX will verify version compatibility and overwrite if needed"
                $sh.AllowSpotify=$true
                try { Invoke-ExternalScriptIsolated -FilePath $dest -Arguments $sp -ExpectedHash $spotxHash -Label 'SpotX run.ps1' } finally { $sh.AllowSpotify=$false }
            } finally {
                Remove-Item -LiteralPath $dest -Force -ErrorAction SilentlyContinue
            }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring saved Spicetify state"; $sh.ProgressBar.Value=70 })
            Reapply-SavedSpicetifySetup -Config $saved
            Write-Log "Saved Spicetify setup restored."
            Write-Log "--- Reapply Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'RepairMarketplace') {
            Write-Log "--- Repair Marketplace ---" -Level 'HEADER'
            $saved = $null
            try { $saved = Load-LibreSpotConfig } catch {}
            if (-not $saved) {
                $saved = Normalize-LibreSpotConfig -Config @{}
                Write-Log "Using defaults (no saved config)" -Level 'WARN'
            }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Repairing Marketplace custom app"; $sh.ProgressBar.Value=35 })
            Repair-Marketplace -Config $saved
            Write-Log "--- Marketplace Repair Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'SafeMode') {
            Write-Log "--- Safe Mode ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Disabling all customizations"; $sh.ProgressBar.Value=30 })
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Spicetify restore failed - try Reapply or Restore Vanilla.' -MissingMessage 'Spicetify CLI was not found, so there are no customizations to disable.') {
                Write-Log "Safe mode active - all customizations disabled. Use Reapply to restore your setup." -Level 'SUCCESS'
            }
            Write-Log "--- Safe Mode Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'RestoreVanilla') {
            Write-Log "--- Restore Vanilla Spotify ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring vanilla files"; $sh.ProgressBar.Value=30 })
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Could not restore vanilla Spotify.' -MissingMessage 'Spicetify CLI was not found, so LibreSpot cannot run a restore. Spotify may already be vanilla.') {
                Write-Log "Vanilla Spotify restored successfully."
            }
            Write-Log "--- Restore Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'UninstallSpicetify') {
            Write-Log "--- Uninstall Spicetify ---" -Level 'HEADER'
            $integration = Get-SpicetifyIntegrationContext
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring Spotify"; $sh.ProgressBar.Value=15 })
            if (Restore-SpotifyIfSpicetifyPresent -FailureMessage 'Could not restore Spotify before uninstalling Spicetify.' -MissingMessage 'Spicetify CLI was already missing, so LibreSpot will remove any leftover files and PATH entries directly.') {
                Write-Log "Spicetify mods restored."
            }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Removing Spicetify files"; $sh.ProgressBar.Value=45 })
            if (Remove-PathSafely -Path $integration.ConfigDirectory -Label 'Spicetify config directory') { Write-Log "Removed config dir." }
            if (Remove-PathSafely -Path $integration.InstallDirectory -Label 'Spicetify CLI directory') { Write-Log "Removed CLI dir." }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Cleaning PATH"; $sh.ProgressBar.Value=75 })
            if (Remove-PathEntry -Entry $integration.InstallDirectory -Scope 'Process') { Write-Log "Removed Spicetify from the current session PATH." }
            if (Remove-PathEntry -Entry $integration.InstallDirectory -Scope 'User') {
                Write-Log "Removed Spicetify from user PATH."
            }
            Write-Log "--- Uninstall Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'FullReset') {
            Write-Log "--- Full Reset ---" -Level 'HEADER'
            $integration = Get-SpicetifyIntegrationContext
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring vanilla Spotify"; $sh.ProgressBar.Value=10 })
            try {
                Invoke-SpicetifyCli -Arguments @('restore','--bypass-admin') -FailureMessage 'Could not restore Spotify before the full reset.'
                Write-Log "Spicetify restored."
            } catch {
                Write-Log "$($_.Exception.Message) Continuing with the full reset because Spotify will be removed next." -Level 'WARN'
            }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Removing Spicetify"; $sh.ProgressBar.Value=30 })
            $null = Remove-PathSafely -Path $integration.ConfigDirectory -Label 'Spicetify config directory'
            $null = Remove-PathSafely -Path $integration.InstallDirectory -Label 'Spicetify CLI directory'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Removing watcher task"; $sh.ProgressBar.Value=45 })
            if (Unregister-AutoReapplyTask) { Write-Log "Auto-reapply scheduled task removed." }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Cleaning Spotify files"; $sh.ProgressBar.Value=50 }); Module-NukeSpotify
            $null = Remove-PathEntry -Entry $integration.InstallDirectory -Scope 'Process'
            if (Remove-PathEntry -Entry $integration.InstallDirectory -Scope 'User') { Write-Log "Removed Spicetify from user PATH." }
            Write-Log "--- Full Reset Complete ---" -Level 'SUCCESS'
        }
        $doneStatus = switch ($action) {
            'CheckUpdates' { 'Version check complete' }
            'Reapply' { 'Setup reapplied' }
            'RepairMarketplace' { 'Marketplace repaired' }
            'RestoreVanilla' { 'Spotify restored' }
            'UninstallSpicetify' { 'Spicetify removed' }
            'FullReset' { 'Full reset complete' }
            default { 'Action complete' }
        }
        $doneStep = switch ($action) {
            'CheckUpdates' { 'Pinned versions reviewed' }
            'Reapply' { 'Ready for Spotify' }
            'RepairMarketplace' { 'Marketplace opened' }
            'RestoreVanilla' { 'Vanilla interface restored' }
            'UninstallSpicetify' { 'Spotify is back to vanilla' }
            'FullReset' { 'System is ready for a fresh start' }
            default { 'Ready for next step' }
        }
        Complete-OperationJournalRun -Result 'Succeeded' -Message "Maintenance action $action completed."
        $sh.IsRunning=$false
        $doneContext = switch ($action) {
            'CheckUpdates' { 'LibreSpot compared the pinned releases against upstream versions. Review the log for anything newer before you decide to update the script pins.' }
            'Reapply' { 'LibreSpot refreshed the saved SpotX and Spicetify setup so Spotify should be back in sync with your last chosen configuration.' }
            'RepairMarketplace' { 'LibreSpot reinstalled the Marketplace custom app, re-enabled it in Spicetify, applied the change, and requested the direct Marketplace URI.' }
            'RestoreVanilla' { 'LibreSpot removed the active Spicetify customizations and brought Spotify back to its vanilla interface while leaving SpotX in place.' }
            'UninstallSpicetify' { 'LibreSpot removed the Spicetify CLI, configuration, and PATH changes after restoring vanilla Spotify first.' }
            'FullReset' { 'LibreSpot completed the deepest cleanup path and removed the Spotify customization stack so you can start fresh.' }
            default { 'LibreSpot finished the requested maintenance action.' }
        }
        $sh.Dispatcher.Invoke([Action]{ $sh.ProgressBar.Value=100; $sh.StatusLabel.Text=$doneStatus; $sh.StepLabel.Text=$doneStep; $sh.InstallTitle.Text=$doneStatus; $sh.InstallContext.Text=$doneContext
            $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Tag="Copy full log"; $sh.CopyLogBtn.Content="Copy full log"; $sh.CopyLogBtn.Visibility="Visible"; if($sh.TitleCloseBtn){$sh.TitleCloseBtn.ToolTip="Close LibreSpot"}; if($sh.MinimizeBtn){$sh.MinimizeBtn.ToolTip="Minimize"}; if($sh.Timer){$sh.Timer.Stop()}; $sh.Window.Topmost=$false; $sh.Window.Activate(); try{[Win32]::FlashTaskbar($sh.WindowHandle)}catch{} })
    } catch { $sh.IsRunning=$false; $em=$_.Exception.Message; $st=$_.ScriptStackTrace
        try { Complete-OperationJournalRun -Result 'Failed' -Message $em } catch {}
        try { Write-Log "[FATAL] $em`n$st" -Level 'ERROR' } catch {}
        $sh.Dispatcher.Invoke([Action]{ if($sh.Timer){$sh.Timer.Stop()}; $sh.LogBlock.Text+="`n[FATAL] $em`n$st"; $sh.StatusLabel.Text="Maintenance stopped"; $sh.StepLabel.Text="Needs attention"; $sh.InstallTitle.Text='Maintenance needs attention'; $sh.InstallContext.Text='LibreSpot stopped before the maintenance action finished. Review the live log below, then go back when you are ready to try again.'
            $sh.ProgressBar.Foreground=$global:BrushError; $sh.ProgressBar.Value=100; $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Tag="Copy full log"; $sh.CopyLogBtn.Content="Copy full log"; $sh.CopyLogBtn.Visibility="Visible"; if($sh.TitleCloseBtn){$sh.TitleCloseBtn.ToolTip="Close LibreSpot"}; if($sh.MinimizeBtn){$sh.MinimizeBtn.ToolTip="Minimize"}; $sh.Window.Topmost=$false; $sh.Window.Activate(); try{[Win32]::FlashTaskbar($sh.WindowHandle)}catch{} })
    }
}

# =============================================================================
# 17. RUNSPACE INFRASTRUCTURE
# =============================================================================
$functionNamesForWorker = @(
    'ConvertTo-PlainHashtable','ConvertTo-ConfigBoolean','ConvertTo-ConfigInt','Get-LibreSpotConfigSchemaVersion','Assert-LibreSpotConfigSchemaSupported','Normalize-LibreSpotConfig','Move-ConfigFileToQuarantine',
    'Get-LibreSpotTempRoot','New-LibreSpotTempFile','New-SpotXCustomPatchesFile','New-LibreSpotTempDirectory',
    'Update-UI','Write-Log','Write-OperationJournalEntry','Start-OperationJournalRun','Complete-OperationJournalRun','Download-FileSafe','Get-DownloadFailureHint','Get-NetworkDiagnosticCode','Get-NetworkPreflightStatus','Get-DownloaderCveExposure','Write-DownloaderCveWarningIfNeeded','Get-PowerShellSecurityContext','Write-PowerShellSecurityContext','Test-IsLanguageModeOrAppControlError','Get-QuarantineGuidance','Open-VerifiedScriptForExecution','Get-FileSha256Lower','Confirm-FileHash','Update-AssetCacheIndexEntry','Save-ToAssetCache','Get-FromAssetCache','Clear-LibreSpotCache','Expand-ArchiveSafely','Hide-SpotifyWindows','Invoke-ExternalScriptIsolated','Read-ProcessOutputDelta','Test-NetworkReady','Invoke-GitHubApiSafe','Check-ForUpdates','Compare-LibreSpotVersions','Get-LibreSpotCurrentSpotifyTarget','Get-LibreSpotCompatibilityWarnings','Write-LibreSpotCompatibilityMatrix',
    'Get-SpotXChildFailureClassification','Get-SpotXDownloadRetryPlan','Stop-SpotifyProcesses','Unlock-SpotifyUpdateFolder','Get-DesktopPath','Test-SafeRemovalTarget','Clear-DirectoryContentsSafely','Remove-PathSafely',
    'Get-SpicetifyIntegrationContext','Get-SpicetifyConfigEntries','Get-SpicetifyConfigListValue','Get-MarketplaceHealth','ConvertTo-NativeArgumentString','Remove-ConsoleEscapeSequences','Update-SpicetifyCliProgress','Write-SpicetifyCliOutputLine','Invoke-SpicetifyCli','Sync-SpicetifyListSetting',
    'Test-SpicetifyCliInstalled','Restore-SpotifyIfSpicetifyPresent','Get-SpicetifyDiagnosticSnapshot','Reapply-SavedSpicetifySetup',
    'Get-NormalizedPathString','Get-PathEntries','Set-PathEntries','Add-PathEntry','Remove-PathEntry',
    'Get-SpotXPatchVerification','Test-SpotifySessionStability',
    'Module-NukeSpotify','Module-InstallSpotX','Module-InstallSpicetifyCLI',
    'Module-InstallThemes','Download-CommunityExtensions','Module-InstallExtensions',
    'Module-InstallMarketplace','Module-InstallCustomApps','Open-SpicetifyMarketplace','Repair-Marketplace','Module-ApplySpicetify',
    'Write-MarketplaceVisibilityEvidence','Optimize-OperationJournalRetention',
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
    'TEMP_DIR','SPOTIFY_EXE_PATH','SPICETIFY_DIR','SPICETIFY_CONFIG_DIR','SPICETIFY_INTEGRATION_VERSION',
    'BACKUP_ROOT','CONFIG_DIR','CONFIG_PATH','LOG_PATH','OPERATION_JOURNAL_PATH','OPERATION_JOURNAL_MAX_BYTES','OPERATION_JOURNAL_RETAIN_BYTES','RUN_RECEIPT_PATH','CURRENT_OPERATION_ID','CURRENT_OPERATION_ACTION','CACHE_DIR',
    'BrushGreen','BrushRed','BrushMuted','BrushError',
    'EasyDefaults','ThemeData','BuiltInExtensions','CommunityExtensions','CommunityExtensionAliases','DeprecatedCommunityExtensionNames','CommunityCustomApps','CommunityThemeRepos','ThemesNeedingJS','SpotXLyricsThemes','SpotifyVersionManifest','SpotifyVersionIds','VERSION','CONFIG_SCHEMA_VERSION'
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
        StatusLabel=$ui['StatusText']; StepLabel=$ui['StepIndicator']; LastLogEventLabel=$ui['LastLogEventText']; ProgressBar=$ui['MainProgress']
        InstallTitle=$ui['InstallTitle']; InstallContext=$ui['InstallContext']
        CloseBtn=$ui['CloseBtn']; BackBtn=$ui['BtnBackToConfig']; CopyLogBtn=$ui['BtnCopyLog']; TitleCloseBtn=$ui['CloseTitleBtn']; MinimizeBtn=$ui['MinimizeBtn']; Timer=$timer
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
        # The Spotify killer/hider watcher exists so Spotify can't relaunch
        # itself mid-mutation. Read-only checks must not close the user's
        # running Spotify, and Repair/Open Marketplace deliberately launch
        # Spotify at the end — the watcher would kill the window it opened.
        if ($Action -notin @('CheckUpdates', 'RepairMarketplace')) {
            $rsW = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace()
            $rsW.ApartmentState = 'STA'; $rsW.Open(); $script:openRunspaces.Add($rsW)
            $psW = [PowerShell]::Create(); $psW.Runspace = $rsW; $script:openRunspaces.Add($psW)
            $psW.Runspace.SessionStateProxy.SetVariable('syncHash', $syncHash)
            $null = $psW.AddScript($watcherBlock.ToString()).AddArgument($syncHash); $null = $psW.BeginInvoke()
        }
    } catch {
        if ($script:activeSyncHash) { $script:activeSyncHash.IsRunning = $false }
        Clear-CompletedRunspaceResources | Out-Null
        throw
    }
}

if ($script:CliClean) {
    $window.Add_ContentRendered({
        if ($script:CliCleanAutoStarted) { return }
        $script:CliCleanAutoStarted = $true
        try {
            if (-not (Confirm-NetworkReadyForAction -Message "LibreSpot could not reach the download sources it needs. Run the clean setup again after the network issue is fixed." -Purpose "clean setup downloads")) { return }
            if (-not (Assert-RiskAcknowledged)) { return }

            if ($ui.ContainsKey('BtnInstall')) { $ui['BtnInstall'].IsEnabled = $false }
            if ($ui.ContainsKey('ModeEasy')) { $ui['ModeEasy'].IsChecked = $true }
            if ($ui.ContainsKey('ChkCleanInstall')) { $ui['ChkCleanInstall'].IsChecked = $true }
            Update-ModePresentation

            $script:InstallConfig = Normalize-LibreSpotConfig -Config (Get-InstallConfig -EasyMode $true)
            $script:InstallConfig.CleanInstall = $true
            $saveSucceeded = Save-LibreSpotConfig -Config $script:InstallConfig
            if ($saveSucceeded) {
                $script:HasSavedConfig = $true
                $script:SavedConfigMode = [string]$script:InstallConfig.Mode
                $script:HasSavedCustomConfig = $false
                Capture-CustomConfigBaseline
            } else {
                Write-Log 'Could not save clean CLI settings before setup. Continuing with the in-memory defaults.' -Level 'WARN'
            }

            Switch-ToInstallPage -Title 'Preparing recommended setup' -Context 'LibreSpot is refreshing Spotify, applying the pinned default stack, and installing the Marketplace-ready extension set.' -PrepareLabel 'Prepare' -RunLabel 'Build' -VerifyLabel 'Apply' -CompleteLabel 'Complete'
            $window.Topmost = $false
            $window.WindowState = 'Minimized'
            Start-InstallJob -Config $script:InstallConfig
        } catch {
            Reset-UiAfterLaunchFailure -Title 'Could not start clean setup' -Message "LibreSpot couldn't start the clean setup run.`n`n$($_.Exception.Message)"
        }
    })
}

# -Watch CLI exit point. Every function the watcher needs — including the
# reapply pipeline's Get-FromAssetCache / Save-ToAssetCache /
# Invoke-SpicetifyCli / Test-SpicetifyCliInstalled — is defined above this
# line, and the window has not been shown. The watcher runs non-elevated
# (the self-elevation gate skips -watch) and exits before ShowDialog.
if ($script:CliWatch) {
    $code = 0
    try { $code = Invoke-AutoReapplyWatcher }
    catch { Write-WatcherLog "Fatal: $($_.Exception.Message)" -Level 'ERROR'; $code = 1 }
    exit $code
}

# =============================================================================
# 19. LAUNCH
# =============================================================================
# DPI-aware window sizing: scale to 80% of the primary screen work area so the
# window looks proportional on 1080p, 1440p, 4K, and ultrawide monitors.
# Also respects display scaling (125%, 150%, etc.) via WPF's built-in DPI virtua-
# lization — we just need to set sensible device-independent dimensions.
try {
    $workArea = [System.Windows.SystemParameters]::WorkArea
    $targetW  = [math]::Min([math]::Round($workArea.Width * 0.78), 1400)
    $targetH  = [math]::Min([math]::Round($workArea.Height * 0.85), 960)
    # Enforce minimums so the layout doesn't collapse on very small screens
    $window.Width  = [math]::Max($targetW, 920)
    $window.Height = [math]::Max($targetH, 680)
} catch {}
$null = $window.ShowDialog()
