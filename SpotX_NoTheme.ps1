<#
.SYNOPSIS
    Modular Spotify Automation Tool (SpotX and Spicetify)
.DESCRIPTION
    Provides a configuration GUI to selectively install SpotX (Ad-Free) and Spicetify (Theming/Extensions).
    Requires Administrator privileges.
#>
Add-Type -AssemblyName PresentationFramework, System.Windows.Forms
$ErrorActionPreference = 'Stop'

# =============================================================================
# 1. CONFIGURATION & SETUP
# =============================================================================

# --- Hardcoded Paths and URLs ---
$global:URL_SPOTX        = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/main/run.ps1'
$global:URL_SPICETIFY_API = 'https://api.github.com/repos/spicetify/cli/releases/latest'
$global:URL_MARKETPLACE = 'https://github.com/spicetify/marketplace/releases/latest/download/marketplace.zip'
$global:TEMP_DIR         = $env:TEMP
$global:SPOTIFY_EXE_PATH = "$env:APPDATA\Spotify\Spotify.exe"
$global:SPICETIFY_DIR    = "$env:LOCALAPPDATA\spicetify"

# --- Hybrid Admin Check ---
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $scriptPath = $MyInvocation.MyCommand.Path
    if ([string]::IsNullOrEmpty($scriptPath)) { Write-Error "Script must be saved to disk to self-elevate."; Exit }
    Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`"" -Verb RunAs -ErrorAction Stop
    Exit
}

# --- Global Settings Object (All checked by default, GUI removed) ---
$global:Settings = @{
    SpotX = $true; # Always true now
    Spicetify = $true; # Always true now
    Marketplace = $true; # Always true now
}

# =============================================================================
# 2. CORE FUNCTION MODULES
# =============================================================================

# --- Logging Function ---
function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

# --- Helper: Safe File Downloader ---
function Download-FileSafe {
    param([string]$Uri, [string]$OutFile)
    Write-Log "Downloading $(Split-Path $Uri -Leaf)..."
    try {
        Import-Module BitsTransfer -ErrorAction SilentlyContinue
        Start-BitsTransfer -Source $Uri -Destination $OutFile -ErrorAction Stop
    } catch {
        Write-Log "BITS failed. Retrying with Standard WebRequest..." -Level 'WARN'
        Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing -ErrorAction Stop
    }
}

# --- Module 1: System Cleanup and Prerequisite (Now uses inline provided logic) ---
function Module-Cleanup {
    Write-Log "Starting System Cleanup and Prerequisites (Full Spotify Removal)..." -Level 'STEP'
    
    # --- Cleanup/Uninstaller Logic (No user interaction needed) ---
    $paths = @{
        RoamingFolder        = Join-Path $env:APPDATA "Spotify"
        LocalFolder          = Join-Path $env:LOCALAPPDATA "Spotify"
        RoamingFolderSpice = Join-Path $env:APPDATA "spicetify"
        LocalFolderSpice    = Join-Path $env:LOCALAPPDATA "spicetify"
        UninstallExe         = Join-Path ([System.IO.Path]::GetTempPath()) "SpotifyUninstall.exe"
        TempSearch           = Join-Path ([System.IO.Path]::GetTempPath()) "SpotX_Temp*"
        DesktopShortcut      = Join-Path $env:USERPROFILE "Desktop\Spotify.lnk"
        StartMenuShortcut    = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Spotify.lnk"
    }

    $registryKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify"
        "HKCU:\Software\Spotify"
        "HKCU:\Software\Classes\spotify"
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\ElevationPolicy\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\DragDrop\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
    )

    $registryValue = @(
        @{
            Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
            Name = "Spotify Web Helper"
        }
        @{
            Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
            Name = "Spotify"
        }
    )

    $script:removedItems = 0 # Use script scope for $removedItems
    $script:errorMessages = @() # Use script scope for $errorMessages
    $ieCachePath = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\INetCache\IE"

    function Find-ItemsToRemove {
        $foundItems = @{
            FilesFolders     = @()
            RegistryKeys     = @()
            RegistryValues   = @()
            StoreApp         = $null
            TempSearchFiles  = @()
            IeCacheFiles     = @()
        }

        $sortedItems = $paths.GetEnumerator() | Where-Object { $_.Key -ne "TempSearch" } | ForEach-Object {
            if (Test-Path $_.Value) {
                Get-Item $_.Value
            }
        } | Sort-Object @{Expression = { -not $_.PSIsContainer } }, @{Expression = { $_.Extension -ne ".lnk" } }
        
        $foundItems.FilesFolders = $sortedItems.FullName

        $foundItems.TempSearchFiles = @(Get-ChildItem -Path $paths.TempSearch -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)

        $foundItems.IeCacheFiles = @(Get-ChildItem -Path $ieCachePath -Force -Recurse -Filter "SpotifyFullSetup*" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)

        $registryKeys | ForEach-Object {
            if (Test-Path $_) { $foundItems.RegistryKeys += $_ }
        }

        $registryValue | ForEach-Object {
            if (Get-ItemProperty -Path $_.Path -Name $_.Name -ErrorAction SilentlyContinue) {
                $foundItems.RegistryValues += $_
            }
        }

        $foundItems.StoreApp = Get-AppxPackage -Name "SpotifyAB.SpotifyMusic" -ErrorAction SilentlyContinue 

        return $foundItems
    }

    function Remove-ItemSafely {
        param([string]$Path)
        
        if (Test-Path -LiteralPath $Path) {
            try {
                Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
                Write-Log "Removed: $Path"
                return 1
            }
            catch {
                $script:errorMessages += "Failed to remove $Path : $($_.Exception.Message)"
                return 0
            }
        }
        return 0
    }

    function Remove-FoundItems {
        param($foundItems)
        
        $count = 0
        
        if ($foundItems.StoreApp) {
            try {
                $ProgressPreference = 'SilentlyContinue'
                Remove-AppxPackage -Package $foundItems.StoreApp.PackageFullName -ErrorAction Stop
                Write-Log "Removed: Spotify Store version"
                $count++
            }
            catch {
                $script:errorMessages += "Failed to remove Store app: $($_.Exception.Message)"
            }
        }
        
        # Remove files/folders
        $foundItems.FilesFolders + $foundItems.TempSearchFiles + $foundItems.IeCacheFiles | ForEach-Object {
            $count += Remove-ItemSafely -Path $_
        }

        # Remove registry keys
        $foundItems.RegistryKeys | ForEach-Object {
            try {
                Remove-Item -Path $_ -Recurse -Force -ErrorAction Stop
                Write-Log "Removed: $_"
                $count++
            }
            catch {
                $script:errorMessages += "Failed to remove registry key $_ : $($_.Exception.Message)"
            }
        }

        # Remove registry values
        $foundItems.RegistryValues | ForEach-Object {
            try {
                Remove-ItemProperty -Path $_.Path -Name $_.Name -Force -ErrorAction Stop
                Write-Log "Removed: $($_.Path)\value=$($_.Name)"
                $count++
            }
            catch {
                $script:errorMessages += "Failed to remove registry value $($_.Name) : $($_.Exception.Message)"
            }
        }

        return $count
    }

    function Stop-SpotifyProcesses {
        param(
            [int]$maxAttempts = 5,
            [int]$retryDelay = 1000
        )

        for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
            $processes = Get-Process -Name "Spotify" -ErrorAction SilentlyContinue
            if (-not $processes) { break }
            Write-Log "Stopping Spotify processes (Attempt $attempt/$maxAttempts)..."

            $processes | ForEach-Object {
                try {
                    Stop-Process -Id $_.Id -Force -ErrorAction Stop
                }
                catch {
                    $script:errorMessages += "Failed to stop process Spotify (PID: $($_.Id)): $($_.Exception.Message)"
                }
            }
            Start-Sleep -Milliseconds $retryDelay
        }
        if ($processes) { Write-Log "All Spotify processes stopped." }
    }

    function Reset-TargetACLs {
        param(
            [Parameter(Mandatory)]
            $foundItems
        )
        
        # Filter out paths that don't exist before trying to reset ACLs
        $aclPaths = @($foundItems.FilesFolders + $foundItems.TempSearchFiles + $foundItems.IeCacheFiles) | Where-Object { Test-Path $_ }
        
        foreach ($path in $aclPaths) {
            try {
                # Note: icacls output suppressed for cleaner log
                $result = icacls $path /reset /T /C /Q # Added /C to continue on error
                if ($LASTEXITCODE -ne 0) {
                    throw "icacls failed with exit code $LASTEXITCODE"
                }
            }
            catch {
                Write-Log "Failed to reset ACLs for $path : $($_.Exception.Message)" -Level 'WARN'
            }
        }
    }
    
    # Execution of the uninstallation routine
    try {
        # 1. Stop all running Spotify processes before cleanup
        Stop-SpotifyProcesses -retryDelay 500

        if ($PSVersionTable.PSVersion.Major -ge 7) {
            Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue
        }

        Write-Log "Searching for old Spotify components..."
        $itemsToRemove = Find-ItemsToRemove
        $maxAttempts = 3 # Reduced to 3 for faster script execution
        $attempt = 0

        do {
            $attempt++
            Write-Log "Removal/Cleanup attempt $attempt/$maxAttempts..."
            
            # Reset access control lists (ACLs) to ensure proper file/folder access
            Reset-TargetACLs -foundItems $itemsToRemove

            # Remove all identified Spotify-related items (files, registry entries, etc.)
            $script:removedItems += Remove-FoundItems -foundItems $itemsToRemove

            # Scan again for any remaining items that need cleanup
            $itemsToRemove = Find-ItemsToRemove

            # Exit loop if maximum attempts reached to prevent infinite loops
            if ($attempt -ge $maxAttempts) {
                Write-Log "Maximum number of attempts ($maxAttempts) reached. Stopping cleanup loop." -Level 'WARN'
                break
            }
            
            if ($attempt -ge 1) { Start-Sleep -Milliseconds 1500 }
            
        } while (($itemsToRemove.FilesFolders.Count + 
                 $itemsToRemove.RegistryKeys.Count + 
                 $itemsToRemove.RegistryValues.Count + 
                 $itemsToRemove.TempSearchFiles.Count + 
                 $itemsToRemove.IeCacheFiles.Count) -gt 0 -or 
                 $itemsToRemove.StoreApp)
        
        if ($script:errorMessages.Count -gt 0) {
            Write-Log "Cleanup finished with errors. See log for details." -Level 'WARN'
        } elseif ($script:removedItems -gt 0) {
            Write-Log "Cleanup completed: removed $($script:removedItems) items." -Level 'INFO'
        } else {
            Write-Log "No Spotify traces were detected." -Level 'INFO'
        }

    } catch {
        Write-Log "A critical error occurred during Cleanup: $($_.Exception.Message)" -Level 'FATAL'
        throw "Cleanup failed."
    }
    # --- End Cleanup/Uninstaller Logic ---
    
    # Clear old Spicetify path if it exists (Redundant after the full cleanup, but kept for safety)
    if (Test-Path $global:SPICETIFY_DIR) {
        Write-Log "Clearing existing Spicetify directory (post-cleanup check)..."
        Remove-Item $global:SPICETIFY_DIR -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# --- Module 2: SpotX Installation ---
function Module-InstallSpotX {
    Write-Log "Installing SpotX (Ad-Free Client)..." -Level 'STEP'
    
    $spotxDest = "$global:TEMP_DIR\spotx_run.ps1"
    Download-FileSafe -Uri $global:URL_SPOTX -OutFile $spotxDest

    # Hardcoded parameters retained from original logic
    $spotxParams = "-confirm_uninstall_ms_spoti -confirm_spoti_recomended_over -podcasts_off -block_update_on -new_theme -adsections_off -lyrics_stat spotify"
    
    Write-Log "Running SpotX with parameters: $spotxParams"
    
    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$spotxDest`" $spotxParams" -NoNewWindow -PassThru -Wait
    
    if ($proc.ExitCode -ne 0) { throw "SpotX installation failed with error code $($proc.ExitCode)" }

    # Relaunch Spotify briefly to generate 'prefs' file needed by Spicetify
    Write-Log "Launching Spotify briefly to generate config files (Watcher will auto-close)..."
    if (Test-Path $global:SPOTIFY_EXE_PATH) { Start-Process $global:SPOTIFY_EXE_PATH }
    Start-Sleep -Seconds 4
}

# --- Module 3: Spicetify CLI Installation ---
function Module-InstallSpicetifyCLI {
    Write-Log "Installing Spicetify CLI..." -Level 'STEP'
    New-Item -Path $global:SPICETIFY_DIR -ItemType Directory -Force | Out-Null

    $architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'AMD64') { 'x64' } else { 'x32' }
    
    $latestRelease = Invoke-RestMethod -Uri $global:URL_SPICETIFY_API
    $version = $latestRelease.tag_name -replace 'v', ''
    $zipUrl = "https://github.com/spicetify/cli/releases/download/v$version/spicetify-$version-windows-$architecture.zip"
    $zipPath = "$global:TEMP_DIR\spicetify.zip"

    Download-FileSafe -Uri $zipUrl -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $global:SPICETIFY_DIR -Force
    Remove-Item $zipPath -Force

    # Ensure Spicetify.exe is discoverable in the current session
    $env:PATH = "$env:PATH;$global:SPICETIFY_DIR"

    Write-Log "Generating Spicetify Config..."
    # The config command needs to be run from the current directory if it's not in PATH yet, or called directly.
    & "$global:SPICETIFY_DIR\spicetify.exe" config --bypass-admin | Out-Null
}

# --- Module 4: Marketplace Installation ---
function Module-InstallMarketplace {
    Write-Log "Installing Spicetify Marketplace..." -Level 'STEP'
    
    $marketDir    = "$global:SPICETIFY_DIR\CustomApps\marketplace"
    $mpZipPath    = "$global:TEMP_DIR\marketplace.zip"
    $unpackedPath = "$global:TEMP_DIR\marketplace_unpacked"

    if (Test-Path $marketDir) { Remove-Item $marketDir -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -Path $marketDir -ItemType Directory -Force | Out-Null

    Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $mpZipPath

    if (Test-Path $unpackedPath) { Remove-Item $unpackedPath -Recurse -Force }
    Expand-Archive -Path $mpZipPath -DestinationPath $unpackedPath -Force

    $sourcePath = if (Test-Path "$unpackedPath\marketplace-dist") { "$unpackedPath\marketplace-dist\*" } else { "$unpackedPath\*" }
    Copy-Item -Path $sourcePath -Destination $marketDir -Recurse -Force

    Remove-Item $mpZipPath -Force
    Remove-Item $unpackedPath -Recurse -Force

    Write-Log "Enabling Marketplace in config..."
    & "$global:SPICETIFY_DIR\spicetify.exe" config custom_apps marketplace --bypass-admin
}

# --- Module 5: Spicetify Application and Finalization ---
function Module-ApplySpicetify {
    Write-Log "Applying Spicetify changes and finalization..." -Level 'STEP'
    
    # Configure Spicetify to enable injections and apply changes
    & "$global:SPICETIFY_DIR\spicetify.exe" config inject_css 1 replace_colors 1 overwrite_assets 1 inject_theme_js 1 --bypass-admin

    Write-Log "Applying changes (backup and patch)..."
    $proc = Start-Process -FilePath "$global:SPICETIFY_DIR\spicetify.exe" -ArgumentList "backup", "apply", "--bypass-admin" -NoNewWindow -PassThru -Wait
    
    if ($proc.ExitCode -ne 0) { Write-Log "Spicetify apply command exited with code $($proc.ExitCode)." -Level 'WARN' }
    else { Write-Log "Spicetify applied successfully." }

    # Clean up backups (retained from original logic)
    Remove-Item "$global:SPICETIFY_DIR\Backup" -Recurse -Force -ErrorAction SilentlyContinue
}

# =============================================================================
# 3. OPTIONS GUI (WPF/XAML) - REMOVED

# NO GUI INTERACTION NEEDED. CONTINUING TO EXECUTION FLOW.

# =============================================================================
# 4. EXECUTION FLOW (THREADING AND ORCHESTRATION)
# =============================================================================

# --- LOGIC BLOCK: WATCHER (Separate Thread) ---
$watcherBlock = [scriptblock]::Create(@'
    param($flag)
    Write-Host "[WATCHER] Spotify Killer started."
    while ($flag.IsRunning) {
        $spotify = Get-Process -Name Spotify -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($spotify) {
            Write-Host "[WATCHER] Spotify detected. Waiting briefly then terminating..."
            Start-Sleep -Seconds 3
            Stop-Process -Name Spotify -Force -ErrorAction SilentlyContinue
            Write-Host "[WATCHER] Spotify terminated."
        }
        Start-Sleep -Milliseconds 500
    }
    Write-Host "[WATCHER] Spotify Killer stopped."
'@)

# Flag to control the watcher thread
$global:WatcherFlag = [hashtable]::Synchronized(@{ IsRunning = $true })

# Start Killer Watcher (Separate Thread)
$psWatcher = [PowerShell]::Create()
$psWatcher.AddScript($watcherBlock).AddArgument($global:WatcherFlag) | Out-Null
$handleWatcher = $psWatcher.BeginInvoke()

Write-Log "--- Installation Started ---" -Level 'HEADER'

try {
    # 1. Mandatory Cleanup
    Module-Cleanup

    # 2. SpotX (Mandatory and enabled by default)
    if ($global:Settings.SpotX) {
        Module-InstallSpotX
    }

    # 3. Spicetify CLI (Enabled by default)
    if ($global:Settings.Spicetify) {
        Module-InstallSpicetifyCLI

        # 4. Spicetify Marketplace (Requires Spicetify, enabled by default)
        if ($global:Settings.Marketplace) {
            Module-InstallMarketplace
        }

        # 5. Apply Spicetify (Mandatory if Spicetify was selected)
        Module-ApplySpicetify
    }

    Write-Log "Installation complete. Launching Spotify..." -Level 'SUCCESS'
    if (Test-Path $global:SPOTIFY_EXE_PATH) { Start-Process $global:SPOTIFY_EXE_PATH }

} catch {
    Write-Log "A fatal error occurred during installation: $($_.Exception.Message)" -Level 'FATAL'
} finally {
    # STOP WATCHER
    $global:WatcherFlag.IsRunning = $false
    
    # Wait for watcher to finish and dispose of the threads
    if ($handleWatcher) {
        $psWatcher.EndInvoke($handleWatcher)
        $psWatcher.Dispose()
    }
    
    Write-Log "--- Execution Finished ---" -Level 'HEADER'
}