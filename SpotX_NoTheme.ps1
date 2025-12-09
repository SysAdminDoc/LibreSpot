<#
.SYNOPSIS
    LibreSpot - SpotX, Spicetify, Marketplace (No Theme)
#>

# -----------------------------------------------------------------------------
# 1. INITIAL SETUP AND GUI PREPARATION
# -----------------------------------------------------------------------------
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms
$ErrorActionPreference = 'Stop'

# Hardcoded paths and URLs
$global:URL_SPOTX         = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/main/run.ps1'
$global:URL_SPICETIFY_API = 'https://api.github.com/repos/spicetify/cli/releases/latest'
$global:URL_MARKETPLACE   = 'https://github.com/spicetify/marketplace/releases/latest/download/marketplace.zip'

$global:TEMP_DIR          = $env:TEMP
$global:SPOTIFY_EXE_PATH  = "$env:APPDATA\Spotify\Spotify.exe"
$global:SPICETIFY_DIR     = "$env:LOCALAPPDATA\spicetify"

# -----------------------------------------------------------------------------
# 2. ADMIN CHECK
# -----------------------------------------------------------------------------
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)) {
    $currentPath = $MyInvocation.MyCommand.Path
    if ([string]::IsNullOrEmpty($currentPath)) {
        Write-Warning "Script must be saved to disk to self-elevate correctly."
    }
    else {
        Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$currentPath`"" -Verb RunAs
    }
    Exit
}

# -----------------------------------------------------------------------------
# 3. WPF XAML
# -----------------------------------------------------------------------------
$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="SpotX, Spicetify Installer (No Theme)" Height="450" Width="700"
        WindowStyle="None" ResizeMode="NoResize" AllowsTransparency="True"
        Topmost="True"
        Background="#00000000" WindowStartupLocation="CenterScreen">
    
    <Border CornerRadius="8" Background="#FF0f172a" BorderBrush="#FF1e293b" BorderThickness="1">
        <Grid Margin="20">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <StackPanel Grid.Row="0" Margin="0,0,0,15" HorizontalAlignment="Left">
                <TextBlock Text="An Advertisement-Free Spotify Client" Foreground="#FF22c55e" FontSize="10" FontWeight="Bold" Opacity="0.8"/>
                <TextBlock Text="SpotX and Spicetify Installation (No Theme)" Foreground="#FFf8fafc" FontSize="20" FontWeight="SemiBold" Margin="0,5,0,0"/>
            </StackPanel>

            <StackPanel Grid.Row="0" HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,5,0,0">
                <TextBlock Text="Credits:" Foreground="#FF94a3b8" FontSize="10" FontWeight="Bold" HorizontalAlignment="Right" Margin="0,0,0,2"/>
                
                <TextBlock HorizontalAlignment="Right" FontSize="9" Margin="0,0,0,2">
                    <Hyperlink Name="LinkSpicetify" NavigateUri="https://github.com/spicetify" Foreground="#FF64748b" TextDecorations="None" Cursor="Hand">
                        github.com/spicetify
                    </Hyperlink>
                </TextBlock>

                <TextBlock HorizontalAlignment="Right" FontSize="9">
                    <Hyperlink Name="LinkSpotX" NavigateUri="https://github.com/SpotX-Official/SpotX" Foreground="#FF64748b" TextDecorations="None" Cursor="Hand">
                        github.com/SpotX-Official/SpotX
                    </Hyperlink>
                </TextBlock>
            </StackPanel>

            <Border Grid.Row="1" Background="#FF020617" CornerRadius="6" Padding="10" BorderBrush="#FF1e293b" BorderThickness="1">
                <Grid>
                    <Image Source="https://raw.githubusercontent.com/SysAdminDoc/LibreSpot/refs/heads/main/Images/spotifylogo.png" 
                           Opacity="0.1" 
                           Stretch="Uniform" 
                           VerticalAlignment="Center" 
                           HorizontalAlignment="Center"
                           RenderOptions.BitmapScalingMode="HighQuality"/>
                    
                    <ScrollViewer Name="LogScroller" VerticalScrollBarVisibility="Auto">
                        <TextBlock Name="LogOutput" Foreground="#FF94a3b8" FontFamily="Consolas, Courier New" FontSize="12" TextWrapping="Wrap"/>
                    </ScrollViewer>
                </Grid>
            </Border>

            <StackPanel Grid.Row="2" Margin="0,15,0,0">
                <Grid>
                    <TextBlock Name="StatusText" Text="Waiting..." Foreground="#FFf8fafc" FontSize="13" HorizontalAlignment="Left"/>
                    <TextBlock Name="StepIndicator" Text="Processing..." Foreground="#FF22c55e" FontSize="13" HorizontalAlignment="Right"/>
                </Grid>
                <ProgressBar Name="MainProgress" Height="4" Margin="0,8,0,0" Background="#FF1e293b" Foreground="#FF22c55e" BorderThickness="0" IsIndeterminate="True"/>
            </StackPanel>

            <Button Name="CloseBtn" Grid.Row="3" Content="CLOSE" Height="35" Width="100" Margin="0,15,0,0"
                    Background="#FF1e293b" Foreground="#FFf8fafc" BorderThickness="0"
                    FontWeight="Bold" Cursor="Hand" Visibility="Collapsed">
                <Button.Template>
                    <ControlTemplate TargetType="Button">
                        <Border Name="border" Background="{TemplateBinding Background}" CornerRadius="4">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#FF334155"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Button.Template>
            </Button>
        </Grid>
    </Border>
</Window>
"@

# -----------------------------------------------------------------------------
# 4. UI INITIALIZATION
# -----------------------------------------------------------------------------
try {
    $reader = New-Object System.Xml.XmlNodeReader ([xml]$xaml)
    $window = [Windows.Markup.XamlReader]::Load($reader)
}
catch {
    Write-Error "XAML Parsing Failed: $($_.Exception.Message)"
    Exit
}

$logOutput     = $window.FindName("LogOutput")
$logScroller   = $window.FindName("LogScroller")
$statusText    = $window.FindName("StatusText")
$stepIndicator = $window.FindName("StepIndicator")
$mainProgress  = $window.FindName("MainProgress")
$closeBtn      = $window.FindName("CloseBtn")

# Hyperlink behavior
$linkHandler = { 
    param($sender, $e) 
    try { [System.Diagnostics.Process]::Start($e.Uri.AbsoluteUri) } catch {}
}
$window.FindName("LinkSpicetify").Add_RequestNavigate($linkHandler)
$window.FindName("LinkSpotX").Add_RequestNavigate($linkHandler)

# Window drag and close
$window.Add_MouseLeftButtonDown({ $window.DragMove() })
$closeBtn.Add_Click({ $window.Close() })

# -----------------------------------------------------------------------------
# 5. ANIMATED STEP INDICATOR
# -----------------------------------------------------------------------------
$stepStates = @("Processing", "Processing.", "Processing..", "Processing...")
$stepIndex  = 0
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromMilliseconds(400)
$timer.Add_Tick({
    $stepIndex = ($stepIndex + 1) % $stepStates.Count
    if ($stepIndicator -ne $null) {
        $stepIndicator.Text = $stepStates[$stepIndex]
    }
})
$timer.Start()

# -----------------------------------------------------------------------------
# 6. HELPER FUNCTIONS (WPF-SAFE, GLOBAL)
# -----------------------------------------------------------------------------

function Update-UI {
    param(
        [string]$Message,
        [string]$Level = "INFO",
        [bool]$IsHeader = $false,
        [string]$StepText = $null
    )

    $timestamp = Get-Date -Format "HH:mm:ss"
    $logText   = "[$timestamp] [$Level] $Message`n"
    $syncHash  = $script:syncHash

    try {
        if ($syncHash -ne $null) {
            $syncHash.Dispatcher.Invoke([Action]{
                $syncHash.LogBlock.Text += $logText
                $syncHash.Scroller.ScrollToBottom()
                if ($IsHeader -or ($Level -eq 'STEP')) {
                    $syncHash.StatusLabel.Text = $Message
                }
                if ($StepText) {
                    $syncHash.StepLabel.Text = $StepText
                }
            })
        }
    }
    catch {
        # Swallow UI sync errors so worker thread never dies from logging
    }
}

function Write-Log {
    param(
        [string]$Message,
        [string]$Level = 'INFO'
    )
    $isStep = ($Level -eq 'STEP' -or $Level -eq 'HEADER')
    Update-UI -Message $Message -Level $Level -IsHeader $isStep
}

function Download-FileSafe {
    param(
        [string]$Uri,
        [string]$OutFile
    )

    Write-Log "Downloading $(Split-Path $Uri -Leaf)..."
    try {
        Import-Module BitsTransfer -ErrorAction SilentlyContinue
        Start-BitsTransfer -Source $Uri -Destination $OutFile -ErrorAction Stop
    }
    catch {
        Write-Log "BITS failed. Retrying with standard web request..." -Level 'WARN'
        Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing -ErrorAction Stop
    }
}

function Invoke-ExternalScriptIsolated {
    param(
        [string]$FilePath,
        [string]$Arguments
    )

    Write-Log "Spawning isolated process..." -Level 'INFO'
    
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName               = "powershell.exe"
    $pinfo.Arguments              = "-NoProfile -ExecutionPolicy Bypass -File `"$FilePath`" $Arguments"
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError  = $true
    $pinfo.UseShellExecute        = $false
    $pinfo.CreateNoWindow         = $true
    
    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $pinfo
    $null = $p.Start()
    
    while (-not $p.HasExited) {
        $line = $p.StandardOutput.ReadLine()
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            Write-Log $line -Level 'OUT'
        }
        Start-Sleep -Milliseconds 20
    }
    
    $rest = $p.StandardOutput.ReadToEnd()
    if (-not [string]::IsNullOrWhiteSpace($rest)) {
        Write-Log $rest -Level 'OUT'
    }

    $err = $p.StandardError.ReadToEnd()
    if (-not [string]::IsNullOrWhiteSpace($err)) {
        Write-Log "[STDERR] $err" -Level 'ERROR'
    }

    if ($p.ExitCode -ne 0) {
        throw "Process exited with error code $($p.ExitCode)"
    }
}

# -----------------------------------------------------------------------------
# 7. CLEANUP HELPERS (GLOBAL, SO RUNSPACE CAN SEE THEM)
# -----------------------------------------------------------------------------

function Find-ItemsToRemove {
    param(
        [hashtable]$Paths,
        [string[]]$RegistryKeys,
        [object[]]$RegistryValues,
        [string]$IeCachePath
    )

    $foundItems = @{
        FilesFolders    = @()
        RegistryKeys    = @()
        RegistryValues  = @()
        StoreApp        = $null
        TempSearchFiles = @()
        IeCacheFiles    = @()
    }

    $sortedItems = $Paths.GetEnumerator() |
        Where-Object { $_.Key -ne "TempSearch" } |
        ForEach-Object {
            if (Test-Path $_.Value) { Get-Item $_.Value }
        } |
        Sort-Object @{Expression = { -not $_.PSIsContainer } }, @{Expression = { $_.Extension -ne ".lnk" } }

    $foundItems.FilesFolders    = $sortedItems.FullName
    $foundItems.TempSearchFiles = @(Get-ChildItem -Path $Paths.TempSearch -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    $foundItems.IeCacheFiles    = @(Get-ChildItem -Path $IeCachePath -Force -Recurse -Filter "SpotifyFullSetup*" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)

    $RegistryKeys | ForEach-Object {
        if (Test-Path $_) {
            $foundItems.RegistryKeys += $_
        }
    }

    $RegistryValues | ForEach-Object {
        if (Get-ItemProperty -Path $_.Path -Name $_.Name -ErrorAction SilentlyContinue) {
            $foundItems.RegistryValues += $_
        }
    }

    $foundItems.StoreApp = Get-AppxPackage -Name "SpotifyAB.SpotifyMusic" -ErrorAction SilentlyContinue
    return $foundItems
}

function Remove-ItemSafely {
    param(
        [string]$Path
    )
    
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
    param(
        $foundItems
    )

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

    $foundItems.FilesFolders + $foundItems.TempSearchFiles + $foundItems.IeCacheFiles | ForEach-Object {
        $count += Remove-ItemSafely -Path $_
    }

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
        [int]$retryDelay  = 500
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

    if (-not $processes) {
        Write-Log "All Spotify processes stopped."
    }
}

function Reset-TargetACLs {
    param(
        [Parameter(Mandatory)]$foundItems
    )

    $aclPaths = @(
        $foundItems.FilesFolders +
        $foundItems.TempSearchFiles +
        $foundItems.IeCacheFiles
    ) | Where-Object { Test-Path $_ }

    foreach ($path in $aclPaths) {
        try {
            $null = icacls $path /reset /T /C /Q
            if ($LASTEXITCODE -ne 0) {
                throw "icacls failed with exit code $LASTEXITCODE"
            }
        }
        catch {
            Write-Log "Failed to reset ACLs for $path : $($_.Exception.Message)" -Level 'WARN'
        }
    }
}

# -----------------------------------------------------------------------------
# 8. INSTALLATION MODULES (SPOTX_NOTHEME LOGIC)
# -----------------------------------------------------------------------------

function Module-Cleanup {
    Write-Log "Starting System Cleanup and Prerequisites (Full Spotify Removal)..." -Level 'STEP'
    
    $paths = @{
        RoamingFolder      = Join-Path $env:APPDATA       "Spotify"
        LocalFolder        = Join-Path $env:LOCALAPPDATA  "Spotify"
        RoamingFolderSpice = Join-Path $env:APPDATA       "spicetify"
        LocalFolderSpice   = Join-Path $env:LOCALAPPDATA  "spicetify"
        UninstallExe       = Join-Path $global:TEMP_DIR   "SpotifyUninstall.exe"
        TempSearch         = Join-Path $global:TEMP_DIR   "SpotX_Temp*"
        DesktopShortcut    = Join-Path $env:USERPROFILE   "Desktop\Spotify.lnk"
        StartMenuShortcut  = Join-Path $env:APPDATA       "Microsoft\Windows\Start Menu\Programs\Spotify.lnk"
    }

    $registryKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify",
        "HKCU:\Software\Spotify",
        "HKCU:\Software\Classes\spotify",
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\ElevationPolicy\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}",
        "HKCU:\Software\Microsoft\Internet Explorer\Low Rights\DragDrop\{5C0D11B8-C5F6-4be3-AD2C-2B1A3EB94AB6}"
    )

    $registryValue = @(
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify Web Helper" }
        @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"; Name = "Spotify" }
    )

    $script:removedItems  = 0
    $script:errorMessages = @()
    $ieCachePath          = Join-Path $env:LOCALAPPDATA "Microsoft\Windows\INetCache\IE"

    try {
        Stop-SpotifyProcesses

        if ($PSVersionTable.PSVersion.Major -ge 7) {
            Import-Module Appx -UseWindowsPowerShell -WarningAction SilentlyContinue
        }

        Write-Log "Searching for old Spotify components..."
        $itemsToRemove = Find-ItemsToRemove -Paths $paths -RegistryKeys $registryKeys -RegistryValues $registryValue -IeCachePath $ieCachePath

        $maxAttempts = 3
        $attempt     = 0

        do {
            $attempt++
            Write-Log "Removal/Cleanup attempt $attempt/$maxAttempts..."

            Reset-TargetACLs -foundItems $itemsToRemove
            $script:removedItems += Remove-FoundItems -foundItems $itemsToRemove
            $itemsToRemove = Find-ItemsToRemove -Paths $paths -RegistryKeys $registryKeys -RegistryValues $registryValue -IeCachePath $ieCachePath

            if ($attempt -ge $maxAttempts) {
                Write-Log "Maximum number of attempts ($maxAttempts) reached. Stopping cleanup loop." -Level 'WARN'
                break
            }

            if ($attempt -ge 1) {
                Start-Sleep -Milliseconds 1500
            }

        } while (
            ($itemsToRemove.FilesFolders.Count +
             $itemsToRemove.RegistryKeys.Count +
             $itemsToRemove.RegistryValues.Count +
             $itemsToRemove.TempSearchFiles.Count +
             $itemsToRemove.IeCacheFiles.Count) -gt 0 -or
             $itemsToRemove.StoreApp
        )

        if ($script:errorMessages.Count -gt 0) {
            Write-Log "Cleanup finished with errors. See log for details." -Level 'WARN'
        }
        elseif ($script:removedItems -gt 0) {
            Write-Log "Cleanup completed: removed $($script:removedItems) items." -Level 'INFO'
        }
        else {
            Write-Log "No Spotify traces were detected." -Level 'INFO'
        }
    }
    catch {
        Write-Log "A critical error occurred during Cleanup: $($_.Exception.Message)" -Level 'FATAL'
        throw "Cleanup failed."
    }

    if (Test-Path $global:SPICETIFY_DIR) {
        Write-Log "Clearing existing Spicetify directory (post-cleanup check)..."
        Remove-Item $global:SPICETIFY_DIR -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Module-InstallSpotX {
    Write-Log "Installing SpotX (Ad-Free Client)..." -Level 'STEP'
    
    $spotxDest = Join-Path $global:TEMP_DIR "spotx_run.ps1"
    Download-FileSafe -Uri $global:URL_SPOTX -OutFile $spotxDest

    $spotxParams = "-confirm_uninstall_ms_spoti -confirm_spoti_recomended_over -podcasts_off -block_update_on -new_theme -adsections_off -lyrics_stat spotify"
    
    Write-Log "Running SpotX with parameters: $spotxParams"
    Invoke-ExternalScriptIsolated -FilePath $spotxDest -Arguments $spotxParams
    
    Write-Log "Launching Spotify briefly to generate config files (Watcher will auto-close)..."
    if (Test-Path $global:SPOTIFY_EXE_PATH) {
        Start-Process $global:SPOTIFY_EXE_PATH
    }
    Start-Sleep -Seconds 4
}

function Module-InstallSpicetifyCLI {
    Write-Log "Installing Spicetify CLI..." -Level 'STEP'

    New-Item -Path $global:SPICETIFY_DIR -ItemType Directory -Force | Out-Null

    $architecture  = if ($env:PROCESSOR_ARCHITECTURE -eq 'AMD64') { 'x64' } else { 'x32' }
    $latestRelease = Invoke-RestMethod -Uri $global:URL_SPICETIFY_API
    $version       = $latestRelease.tag_name -replace 'v', ''
    $zipUrl        = "https://github.com/spicetify/cli/releases/download/v$version/spicetify-$version-windows-$architecture.zip"
    $zipPath       = Join-Path $global:TEMP_DIR "spicetify.zip"

    Download-FileSafe -Uri $zipUrl -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $global:SPICETIFY_DIR -Force
    Remove-Item $zipPath -Force

    $env:PATH = "$env:PATH;$global:SPICETIFY_DIR"

    Write-Log "Generating Spicetify config..."
    & "$global:SPICETIFY_DIR\spicetify.exe" config --bypass-admin | Out-Null
}

function Module-InstallMarketplace {
    Write-Log "Installing Spicetify Marketplace..." -Level 'STEP'
    
    $marketDir    = Join-Path $global:SPICETIFY_DIR "CustomApps\marketplace"
    $mpZipPath    = Join-Path $global:TEMP_DIR "marketplace.zip"
    $unpackedPath = Join-Path $global:TEMP_DIR "marketplace_unpacked"

    if (Test-Path $marketDir) {
        Remove-Item $marketDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -Path $marketDir -ItemType Directory -Force | Out-Null

    Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $mpZipPath

    if (Test-Path $unpackedPath) {
        Remove-Item $unpackedPath -Recurse -Force
    }

    Expand-Archive -Path $mpZipPath -DestinationPath $unpackedPath -Force

    $sourcePath = if (Test-Path (Join-Path $unpackedPath "marketplace-dist")) {
        Join-Path $unpackedPath "marketplace-dist\*"
    }
    else {
        Join-Path $unpackedPath "*"
    }

    Copy-Item -Path $sourcePath -Destination $marketDir -Recurse -Force

    Remove-Item $mpZipPath -Force
    Remove-Item $unpackedPath -Recurse -Force

    Write-Log "Enabling Marketplace in config..."
    & "$global:SPICETIFY_DIR\spicetify.exe" config custom_apps marketplace --bypass-admin
}

function Module-ApplySpicetify {
    Write-Log "Applying Spicetify changes and finalization (No Theme)..." -Level 'STEP'
    
    & "$global:SPICETIFY_DIR\spicetify.exe" config inject_css 0 replace_colors 0 overwrite_assets 0 inject_theme_js 0 --bypass-admin

    Write-Log "Applying changes (backup and patch)..."
    $proc = Start-Process -FilePath "$global:SPICETIFY_DIR\spicetify.exe" -ArgumentList "backup", "apply", "--bypass-admin" -NoNewWindow -PassThru -Wait
    
    if ($proc.ExitCode -ne 0) {
        Write-Log "Spicetify apply command exited with code $($proc.ExitCode)." -Level 'WARN'
    }
    else {
        Write-Log "Spicetify applied successfully."
    }

    Remove-Item (Join-Path $global:SPICETIFY_DIR "Backup") -Recurse -Force -ErrorAction SilentlyContinue
}

# -----------------------------------------------------------------------------
# 9. THREADING SCRIPTBLOCKS
# -----------------------------------------------------------------------------

$watcherBlock = {
    param($syncHash)

    $script:syncHash = $syncHash

    while ($syncHash.IsRunning) {
        $spotify = Get-Process -Name Spotify -ErrorAction SilentlyContinue | Select-Object -First 1
        
        if ($spotify) {
            for ($i = 0; $i -lt 30; $i++) {
                if (-not $syncHash.IsRunning) { break }
                Start-Sleep -Milliseconds 100
            }
            Stop-Process -Name Spotify -Force -ErrorAction SilentlyContinue
        }

        Start-Sleep -Milliseconds 500
    }
}

$logicBlock = {
    param($syncHash)

    $script:syncHash = $syncHash

    try {
        Write-Log "--- Installation Started ---" -Level 'HEADER'

        $syncHash.Dispatcher.Invoke([Action]{ $syncHash.StepLabel.Text = "Step 1 of 5: Cleanup" })
        Module-Cleanup

        $syncHash.Dispatcher.Invoke([Action]{ $syncHash.StepLabel.Text = "Step 2 of 5: SpotX" })
        Module-InstallSpotX

        $syncHash.Dispatcher.Invoke([Action]{ $syncHash.StepLabel.Text = "Step 3 of 5: Spicetify CLI" })
        Module-InstallSpicetifyCLI

        $syncHash.Dispatcher.Invoke([Action]{ $syncHash.StepLabel.Text = "Step 4 of 5: Marketplace" })
        Module-InstallMarketplace

        $syncHash.Dispatcher.Invoke([Action]{ $syncHash.StepLabel.Text = "Step 5 of 5: Final Apply" })
        Module-ApplySpicetify

        Write-Log "Installation complete. Launching Spotify..." -Level 'SUCCESS'
        if (Test-Path $global:SPOTIFY_EXE_PATH) {
            Start-Process $global:SPOTIFY_EXE_PATH
        }

        $syncHash.IsRunning = $false
        $syncHash.Dispatcher.Invoke([Action]{
            $syncHash.ProgressBar.IsIndeterminate = $false
            $syncHash.ProgressBar.Value          = 100
            $syncHash.StatusLabel.Text           = "Done"
            $syncHash.CloseBtn.Visibility        = "Visible"
            if ($syncHash.Timer -ne $null) {
                $syncHash.Timer.Stop()
            }
        })
    }
    catch {
        $syncHash.IsRunning = $false
        $errMessage = $_.Exception.Message

        $syncHash.Dispatcher.Invoke([Action]{
            if ($syncHash.Timer -ne $null) {
                $syncHash.Timer.Stop()
            }
            $syncHash.LogBlock.Text += "`n[FATAL ERROR] $errMessage"
            $syncHash.StatusLabel.Text        = "Error Occurred"
            $syncHash.ProgressBar.Foreground  = "#FFf87171"
            $syncHash.ProgressBar.IsIndeterminate = $false
            $syncHash.ProgressBar.Value       = 100
            $syncHash.CloseBtn.Visibility     = "Visible"
        })
    }
}

# -----------------------------------------------------------------------------
# 10. PREPARE INITIALSESSIONSTATE FOR WORKER RUNSPACE
# -----------------------------------------------------------------------------
$functionNamesForWorker = @(
    'Update-UI',
    'Write-Log',
    'Download-FileSafe',
    'Invoke-ExternalScriptIsolated',
    'Find-ItemsToRemove',
    'Remove-ItemSafely',
    'Remove-FoundItems',
    'Stop-SpotifyProcesses',
    'Reset-TargetACLs',
    'Module-Cleanup',
    'Module-InstallSpotX',
    'Module-InstallSpicetifyCLI',
    'Module-InstallMarketplace',
    'Module-ApplySpicetify'
)

$issMain = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault()

foreach ($fname in $functionNamesForWorker) {
    $cmd = Get-Command -Name $fname -CommandType Function -ErrorAction Stop
    $entry = New-Object System.Management.Automation.Runspaces.SessionStateFunctionEntry($cmd.Name, $cmd.Definition)
    $null = $issMain.Commands.Add($entry)
}

$varNamesForWorker = @(
    'URL_SPOTX',
    'URL_SPICETIFY_API',
    'URL_MARKETPLACE',
    'TEMP_DIR',
    'SPOTIFY_EXE_PATH',
    'SPICETIFY_DIR'
)

foreach ($vname in $varNamesForWorker) {
    $val = (Get-Variable -Name $vname -Scope Global -ErrorAction Stop).Value
    $varEntry = New-Object System.Management.Automation.Runspaces.SessionStateVariableEntry($vname, $val, "")
    $null = $issMain.Variables.Add($varEntry)
}

$script:WorkerInitialState = $issMain

# -----------------------------------------------------------------------------
# 11. SYNC HASH AND RUNSPACE STARTUP
# -----------------------------------------------------------------------------
$syncHash = [hashtable]::Synchronized(@{
    Dispatcher  = $window.Dispatcher
    LogBlock    = $logOutput
    Scroller    = $logScroller
    StatusLabel = $statusText
    StepLabel   = $stepIndicator
    ProgressBar = $mainProgress
    CloseBtn    = $closeBtn
    Timer       = $timer
    IsRunning   = $true
})

$window.Add_Loaded({
    param($sender, $e)

    try {
        # MAIN WORKER RUNSPACE
        $rsMain = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace($script:WorkerInitialState)
        $rsMain.ApartmentState = 'STA'
        $rsMain.Open()

        $psMain = [PowerShell]::Create()
        $psMain.Runspace = $rsMain

        # Make the syncHash visible to logicBlock
        $psMain.Runspace.SessionStateProxy.SetVariable('syncHash', $syncHash)

        $null = $psMain.AddScript($logicBlock.ToString()).AddArgument($syncHash)
        $null = $psMain.BeginInvoke()

        # WATCHER RUNSPACE (simple default state)
        $rsWatcher = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace()
        $rsWatcher.ApartmentState = 'STA'
        $rsWatcher.Open()

        $psWatcher = [PowerShell]::Create()
        $psWatcher.Runspace = $rsWatcher
        $psWatcher.Runspace.SessionStateProxy.SetVariable('syncHash', $syncHash)

        $null = $psWatcher.AddScript($watcherBlock.ToString()).AddArgument($syncHash)
        $null = $psWatcher.BeginInvoke()
    }
    catch {
        $logOutput.Text = "FATAL: Could not start worker threads: $($_.Exception.Message)"
    }
})

[void]$window.ShowDialog()

