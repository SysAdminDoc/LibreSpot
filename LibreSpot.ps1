<#
.SYNOPSIS
    Spotify Automation UI
#>

# -----------------------------------------------------------------------------
# THEME: GUI PREPARATION
# -----------------------------------------------------------------------------
Add-Type -AssemblyName PresentationFramework
$ErrorActionPreference = 'Stop'

# -----------------------------------------------------------------------------
# ORIGINAL ADMIN CHECK
# -----------------------------------------------------------------------------
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
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
# DEFINE CSS CONTENT
# -----------------------------------------------------------------------------
$cssXpui_Content = @'
/* INJECTED - XPUI */
.spotify__container--is-desktop:not(.fullscreen) body::after {
    content: "";
    position: absolute;
    right: 0;
    z-index: 999;
    backdrop-filter: brightness(2.12);
    width: 135px;
    height: 64px;
}
'@

$cssComfy_Content = @'
/* INJECTED - COMFY */
:root .global-nav .Root__main-view .main-view-container .main-entityHeader-container {
    padding: 32px;
    align-items: center;
    margin-top: 64px;
}
:root #main.Banner-Enabled .comfy-banner-frame { display: block; }
:root #main.Banner-Enabled .comfy-banner-frame .comfy-banner-image {
    position: absolute; width: 100%; height: 100%; top: 0; left: 0;
    background-size: cover; background-position: top;
    background-image: var(--image-url);
    filter: blur(var(--image-blur));
    -webkit-mask-image: linear-gradient(rgba(0, 0, 0, 0.6), rgba(0, 0, 0, 0.6));
    mask-image: linear-gradient(rgba(0, 0, 0, 0.6), rgba(0, 0, 0, 0.6));
    transition: background .5s ease;
}
:root #main.Banner-Enabled .comfy-banner-frame .comfy-banner-image:last-of-type { display: none; }
:root #main.Banner-Enabled .main-entityHeader-backgroundColor { background: none !important; }
:root #main.Banner-Enabled.Custom-Playbar-Snippet:not(.Comfy-nord-Snippet, .Comfy-nord-flat-Snippet, .Playbar-Above-Right-Panel-Snippet) .artist-artistOverview-artistOverviewContent,
:root #main.Banner-Enabled.Custom-Playbar-Snippet:not(.Comfy-nord-Snippet, .Comfy-nord-flat-Snippet, .Playbar-Above-Right-Panel-Snippet) .main-actionBarBackground-background {
    min-height: calc(100vh - min(30vh, clamp(250px, 250px + (100vw - var(--comfy-left-sidebar-width, 0px) - var(--comfy-panel-width, 0px) - 600px)/424*150, 400px)) - 128px - 12px) !important;
}
:root #main.Banner-Enabled .artist-artistOverview-artistOverviewContent,
:root #main.Banner-Enabled .main-actionBarBackground-background {
    background-image: linear-gradient(rgba(var(--spice-rgb-main-transition), var(--tracklist-gradient-opacity)) 0, var(--spice-main) var(--tracklist-gradient-height)), var(--tracklist-gradient-noise) !important;
    background-color: rgba(0, 0, 0, 0) !important;
    height: calc(100% - 250px);
    background-size: auto 100%, 300px var(--tracklist-gradient-height);
    background-repeat: repeat-x;
}
.spotify__container--is-desktop:not(.fullscreen) body::after {
    content: "";
    position: absolute;
    right: 0;
    z-index: 999;
    backdrop-filter: brightness(2.12);
    width: 135px;
    height: 64px;
}
'@

# -----------------------------------------------------------------------------
# WPF XAML DEFINITION
# -----------------------------------------------------------------------------
$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="SpotX, Spicetify, Store Installer" Height="450" Width="700"
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
                <TextBlock Text="An Advertisement-Free and Themed Spotify" Foreground="#FF22c55e" FontSize="10" FontWeight="Bold" Opacity="0.8"/>
                <TextBlock Text="LibreSpot - SpotX and Spicetify Installation" Foreground="#FFf8fafc" FontSize="20" FontWeight="SemiBold" Margin="0,5,0,0"/>
            </StackPanel>

            <StackPanel Grid.Row="0" HorizontalAlignment="Right" VerticalAlignment="Top" Margin="0,5,0,0">
                <TextBlock Text="Credits:" Foreground="#FF94a3b8" FontSize="10" FontWeight="Bold" HorizontalAlignment="Right" Margin="0,0,0,2"/>
                
                <TextBlock HorizontalAlignment="Right" FontSize="9" Margin="0,0,0,2">
                    <Hyperlink Name="LinkSpicetify" NavigateUri="https://github.com/spicetify" Foreground="#FF64748b" TextDecorations="None" Cursor="Hand">
                        github.com/spicetify
                    </Hyperlink>
                </TextBlock>

                <TextBlock HorizontalAlignment="Right" FontSize="9" Margin="0,0,0,2">
                    <Hyperlink Name="LinkSpotX" NavigateUri="https://github.com/SpotX-Official/SpotX" Foreground="#FF64748b" TextDecorations="None" Cursor="Hand">
                        github.com/SpotX-Official/SpotX
                    </Hyperlink>
                </TextBlock>

                <TextBlock HorizontalAlignment="Right" FontSize="9">
                    <Hyperlink Name="LinkComfy" NavigateUri="https://github.com/Comfy-Themes/Spicetify" Foreground="#FF64748b" TextDecorations="None" Cursor="Hand">
                        github.com/Comfy-Themes/Spicetify
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
# UI INITIALIZATION
# -----------------------------------------------------------------------------
try {
    $reader = New-Object System.Xml.XmlNodeReader ([xml]$xaml)
    $window = [Windows.Markup.XamlReader]::Load($reader)
}
catch {
    Write-Error "XAML Parsing Failed: $($_.Exception.Message)"
    Exit
}

# Element References
$logOutput     = $window.FindName("LogOutput")
$logScroller   = $window.FindName("LogScroller")
$statusText    = $window.FindName("StatusText")
$stepIndicator = $window.FindName("StepIndicator")
$mainProgress  = $window.FindName("MainProgress")
$closeBtn      = $window.FindName("CloseBtn")

# Hyperlink Logic (Open in Browser)
$linkSpicetify = $window.FindName("LinkSpicetify")
$linkSpotX     = $window.FindName("LinkSpotX")
$linkComfy     = $window.FindName("LinkComfy")

$linkHandler = { 
    param($sender, $e) 
    try { [System.Diagnostics.Process]::Start($e.Uri.AbsoluteUri) } catch {}
}

if ($linkSpicetify) { $linkSpicetify.Add_RequestNavigate($linkHandler) }
if ($linkSpotX)     { $linkSpotX.Add_RequestNavigate($linkHandler) }
if ($linkComfy)     { $linkComfy.Add_RequestNavigate($linkHandler) }

# Drag Window Event
$window.Add_MouseLeftButtonDown({ $window.DragMove() })

# Close Button Logic
$closeBtn.Add_Click({ $window.Close() })

# -----------------------------------------------------------------------------
# ANIMATED STEP INDICATOR
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
# LOGIC BLOCK: WATCHER (Browsers REMOVED)
# -----------------------------------------------------------------------------
$watcherBlock = [scriptblock]::Create(@'
    param($syncHash)

    while ($syncHash.IsRunning) {
        
        # SPOTIFY HANDLING ONLY: Detect -> Wait -> Kill
        # (Browser minimizing logic has been removed)
        
        $spotify = Get-Process -Name Spotify -ErrorAction SilentlyContinue | Select-Object -First 1
        
        if ($spotify) {
            # Give it a moment to try initializing, then kill it so installer can proceed
            for ($i = 0; $i -lt 30; $i++) {
                if (-not $syncHash.IsRunning) { break }
                Start-Sleep -Milliseconds 100
            }
            Stop-Process -Name Spotify -Force -ErrorAction SilentlyContinue
        }

        Start-Sleep -Milliseconds 500
    }
'@)


# -----------------------------------------------------------------------------
# LOGIC BLOCK: MAIN INSTALLATION
# -----------------------------------------------------------------------------
$logicBlock = [scriptblock]::Create(@'
    # PARAMETER UPDATE: Now accepting CSS strings as arguments
    param($syncHash, $PSCommandPath_Passed, $cssXpui_Passed, $cssComfy_Passed)

    $ProgressPreference = 'SilentlyContinue' 
    
    # -------------------------------------------------------------------------
    # UI UPDATE HELPER
    # -------------------------------------------------------------------------
    function Update-UI {
        param([string]$Message, [string]$Color = "White", [bool]$IsHeader = $false)
        try {
            $syncHash.Dispatcher.Invoke({
                $timestamp = Get-Date -Format "HH:mm:ss"
                $syncHash.LogBlock.Text += "[$timestamp] $Message`n"
                $syncHash.Scroller.ScrollToBottom()
                if ($IsHeader) { $syncHash.StatusLabel.Text = $Message }
            })
        } catch { }
    }

    # -------------------------------------------------------------------------
    # WRITE-HOST PROXY
    # -------------------------------------------------------------------------
    function Write-Host {
        param(
            [Parameter(Position=0)][string]$Object, 
            [Parameter(Position=1)][object]$ForegroundColor = 'White',
            [object]$BackgroundColor,
            [switch]$NoNewline
        )
        if ([string]::IsNullOrWhiteSpace($ForegroundColor)) { $ForegroundColor = 'White' }
        $isHeader = ($Object -match "\[Step \d/\d\]" -or $Object -match "Starting Unified")
        Update-UI -Message $Object -Color $ForegroundColor.ToString() -IsHeader $isHeader
    }

    # -------------------------------------------------------------------------
    # HELPER: DOWNLOAD
    # -------------------------------------------------------------------------
    function Download-FileSafe {
        param([string]$Uri, [string]$OutFile)
        $fileName = Split-Path $Uri -Leaf
        Write-Host "Downloading: $fileName" -ForegroundColor Gray
        $downloaded = $false
        try {
            Import-Module BitsTransfer -ErrorAction SilentlyContinue
            Start-BitsTransfer -Source $Uri -Destination $OutFile -ErrorAction Stop
            $downloaded = $true
        } catch {
            Write-Host "BITS failed. Retrying with Standard WebRequest..." -ForegroundColor Yellow
        }
        if (-not $downloaded) {
            try {
                Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing -ErrorAction Stop
            } catch {
                Write-Host "Download FAILED: $Uri" -ForegroundColor Red
                throw $_
            }
        }
    }

    # -------------------------------------------------------------------------
    # HELPER: ISOLATED PS RUNNER (For SpotX)
    # -------------------------------------------------------------------------
    function Invoke-ExternalScriptIsolated {
        param([string]$FilePath, [string]$Arguments)
        Write-Host "Spawning isolated process..." -ForegroundColor Gray
        
        $pinfo = New-Object System.Diagnostics.ProcessStartInfo
        $pinfo.FileName = "powershell.exe"
        $pinfo.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$FilePath`" $Arguments"
        $pinfo.RedirectStandardOutput = $true
        $pinfo.RedirectStandardError = $true
        $pinfo.UseShellExecute = $false
        $pinfo.CreateNoWindow = $true
        
        $p = New-Object System.Diagnostics.Process
        $p.StartInfo = $pinfo
        $p.Start() | Out-Null
        
        while (-not $p.HasExited) {
            $line = $p.StandardOutput.ReadLine()
            if (-not [string]::IsNullOrWhiteSpace($line)) { Write-Host $line }
            Start-Sleep -Milliseconds 20
        }
        
        $rest = $p.StandardOutput.ReadToEnd()
        if (-not [string]::IsNullOrWhiteSpace($rest)) { Write-Host $rest }
        $err = $p.StandardError.ReadToEnd()
        if (-not [string]::IsNullOrWhiteSpace($err)) { Write-Host "[STDERR] $err" -ForegroundColor Red }

        if ($p.ExitCode -ne 0) { throw "Process exited with error code $($p.ExitCode)" }
    }

    # -------------------------------------------------------------------------
    # EXECUTION BLOCK
    # -------------------------------------------------------------------------
    try {
        Write-Host "Engine initialized. Starting logic..." -ForegroundColor Gray
        $PSCommandPath = $PSCommandPath_Passed

        # 0. PRE-FLIGHT
        $ErrorActionPreference = 'Stop'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

        Write-Host "Starting Unified Spotify Automation..." -ForegroundColor Cyan
        Write-Host "Watcher active (Spotify Only)." -ForegroundColor Gray

        # 1. CLEANUP
        Write-Host "`n[Step 1/6] Cleaning up existing Spotify instances..." -ForegroundColor Green
        $syncHash.Dispatcher.Invoke({ $syncHash.StepLabel.Text = "Step 1 of 6: Cleanup" })

        Stop-Process -Name Spotify -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2

        try {
            Write-Host "Downloading and running Spotify Uninstaller..."
            $uninstallScript = Invoke-RestMethod -Uri 'https://raw.githubusercontent.com/amd64fox/Uninstall-Spotify/main/core.ps1'
            Invoke-Expression $uninstallScript
        } catch {
            Write-Host "Uninstall script error (non-fatal), proceeding..." -ForegroundColor Yellow
        }

        # 2. SPOTX
        Write-Host "`n[Step 2/6] Installing SpotX (Ad-Free Client)..." -ForegroundColor Green
        $syncHash.Dispatcher.Invoke({ $syncHash.StepLabel.Text = "Step 2 of 6: SpotX" })

        try {
            $spotxUrl = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/main/run.ps1'
            $spotxDest = "$env:TEMP\spotx_run.ps1"
            Download-FileSafe -Uri $spotxUrl -OutFile $spotxDest
            $spotxParams = "-confirm_uninstall_ms_spoti -confirm_spoti_recomended_over -podcasts_off -block_update_on -new_theme -adsections_off -lyrics_stat spotify"
            Invoke-ExternalScriptIsolated -FilePath $spotxDest -Arguments $spotxParams
        } catch {
            Write-Host "Failed to install SpotX: $($_.Exception.Message)" -ForegroundColor Red
            throw
        }

        # 3. GENERATE CONFIGS (Killer Watcher handles closing)
        Write-Host "`n[Step 3/6] Initializing Spotify to generate 'prefs' file..." -ForegroundColor Green
        $syncHash.Dispatcher.Invoke({ $syncHash.StepLabel.Text = "Step 3 of 6: Generate Config" })
        
        $spotifyExe = "$env:APPDATA\Spotify\Spotify.exe"
        if (Test-Path $spotifyExe) {
            Write-Host "Launching Spotify (Watcher will auto-close)..."
            Start-Process $spotifyExe
        } else {
            Write-Host "Spotify executable not found. Cannot proceed." -ForegroundColor Red
            throw
        }

        # 4. SPICETIFY CLI
        Write-Host "`n[Step 4/6] Installing Spicetify CLI..." -ForegroundColor Green
        $syncHash.Dispatcher.Invoke({ $syncHash.StepLabel.Text = "Step 4 of 6: Spicetify CLI" })

        $spicetifyDir = "$env:LOCALAPPDATA\spicetify"
        $architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'AMD64') { 'x64' } else { 'x32' }

        try {
            $latestRelease = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/cli/releases/latest'
            $version = $latestRelease.tag_name -replace 'v', ''
            $zipUrl = "https://github.com/spicetify/cli/releases/download/v$version/spicetify-$version-windows-$architecture.zip"
            $zipPath = "$env:TEMP\spicetify.zip"

            Download-FileSafe -Uri $zipUrl -OutFile $zipPath

            if (Test-Path $spicetifyDir) { Remove-Item $spicetifyDir -Recurse -Force -ErrorAction SilentlyContinue }
            Expand-Archive -Path $zipPath -DestinationPath $spicetifyDir -Force
            Remove-Item $zipPath -Force
            $env:PATH = "$env:PATH;$spicetifyDir"

            Write-Host "Generating Spicetify Config..."
            & "$spicetifyDir\spicetify.exe" config --bypass-admin | Out-Null
        } catch {
            Write-Host "Failed to install Spicetify CLI: $($_.Exception.Message)" -ForegroundColor Red
            throw
        }

        # 5. MARKETPLACE
        Write-Host "`n[Step 5/6] Installing Spicetify Marketplace..." -ForegroundColor Green
        $syncHash.Dispatcher.Invoke({ $syncHash.StepLabel.Text = "Step 5 of 6: Marketplace" })

        try {
            $marketDir = "$spicetifyDir\CustomApps\marketplace"
            if (Test-Path $marketDir) { Remove-Item $marketDir -Recurse -Force -ErrorAction SilentlyContinue }
            New-Item -Path $marketDir -ItemType Directory -Force | Out-Null

            $mpZipUrl = "https://github.com/spicetify/marketplace/releases/latest/download/marketplace.zip"
            $mpZipPath = "$env:TEMP\marketplace.zip"
            $unpackedPath = "$env:TEMP\marketplace_unpacked"

            Download-FileSafe -Uri $mpZipUrl -OutFile $mpZipPath

            if (Test-Path $unpackedPath) { Remove-Item $unpackedPath -Recurse -Force }
            Expand-Archive -Path $mpZipPath -DestinationPath $unpackedPath -Force

            if (Test-Path "$unpackedPath\marketplace-dist") {
                Copy-Item -Path "$unpackedPath\marketplace-dist\*" -Destination $marketDir -Recurse -Force
            } else {
                Copy-Item -Path "$unpackedPath\*" -Destination $marketDir -Recurse -Force
            }
            Remove-Item $mpZipPath -Force
            Remove-Item $unpackedPath -Recurse -Force

            Write-Host "Enabling Marketplace in config..."
            & "$spicetifyDir\spicetify.exe" config custom_apps marketplace --bypass-admin
        } catch {
            Write-Host "Marketplace installation failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }

        # 6. COMFY THEME + CSS INJECTION
        Write-Host "`n[Step 6/6] Installing Comfy Theme..." -ForegroundColor Green
        $syncHash.Dispatcher.Invoke({ $syncHash.StepLabel.Text = "Step 6 of 6: Comfy Theme" })

        $themeDir = "$spicetifyDir\Themes\Comfy"

        try {
            New-Item -Path $themeDir -ItemType Directory -Force | Out-Null
            Download-FileSafe -Uri "https://raw.githubusercontent.com/Comfy-Themes/Spicetify/main/Comfy/color.ini" -OutFile "$themeDir\color.ini"
            Download-FileSafe -Uri "https://raw.githubusercontent.com/Comfy-Themes/Spicetify/main/Comfy/user.css" -OutFile "$themeDir\user.css"
            Download-FileSafe -Uri "https://raw.githubusercontent.com/Comfy-Themes/Spicetify/main/Comfy/theme.js" -OutFile "$themeDir\theme.js"

            # -----------------------------------------------------------------
            # CSS INJECTION (Using Passed Parameters)
            # -----------------------------------------------------------------
            Write-Host "Injecting Custom CSS Rules..." -ForegroundColor Cyan
            
            function Inject-CSS {
                param($Path, $Content)
                try {
                    $parent = Split-Path $Path -Parent
                    if (-not (Test-Path $parent)) { New-Item -Path $parent -ItemType Directory -Force | Out-Null }
                    Add-Content -Path $Path -Value "`n$Content" -Force
                    Write-Host "  -> Injected: $(Split-Path $Path -Leaf)" -ForegroundColor Gray
                } catch {
                    Write-Host "  -> Failed to inject $(Split-Path $Path -Leaf): $($_.Exception.Message)" -ForegroundColor Red
                }
            }

            Inject-CSS -Path "$env:APPDATA\Spotify\Apps\xpui\user.css" -Content $cssXpui_Passed
            Inject-CSS -Path "$env:LOCALAPPDATA\spicetify\Themes\Comfy\user.css" -Content $cssComfy_Passed
            # -----------------------------------------------------------------


            Write-Host "Setting config values..."
            & "$spicetifyDir\spicetify.exe" config inject_css 1 replace_colors 1 overwrite_assets 1 inject_theme_js 1 --bypass-admin
            & "$spicetifyDir\spicetify.exe" config current_theme Comfy color_scheme Comfy --bypass-admin

            Write-Host "Resetting backups..."
            Remove-Item "$spicetifyDir\Backup" -Recurse -Force -ErrorAction SilentlyContinue

            Write-Host "Applying theme..."
            $proc = Start-Process -FilePath "$spicetifyDir\spicetify.exe" -ArgumentList "backup", "apply", "--bypass-admin" -NoNewWindow -PassThru -Wait
            if ($proc.ExitCode -eq 0) { Write-Host "Theme applied successfully." }
            else { Write-Host "Spicetify exited with code $($proc.ExitCode)." -ForegroundColor Yellow }

        } catch {
            Write-Host "Error in Step 6: $($_.Exception.Message)" -ForegroundColor Red
        }

        # COMPLETION
        Write-Host "`n========================================" -ForegroundColor Cyan
        Write-Host "       Installation Complete!" -ForegroundColor Cyan
        Write-Host "========================================"
        Write-Host "Launching Spotify..."

        if (Test-Path $spotifyExe) { Start-Process $spotifyExe }

        # STOP WATCHER
        $syncHash.IsRunning = $false

        # SIGNAL COMPLETION
        $syncHash.Dispatcher.Invoke({
            $syncHash.ProgressBar.IsIndeterminate = $false
            $syncHash.ProgressBar.Value = 100
            $syncHash.StatusLabel.Text = "Done"
            $syncHash.StepLabel.Text   = "Complete"
            $syncHash.CloseBtn.Visibility = "Visible"
            if ($syncHash.Timer -ne $null) { $syncHash.Timer.Stop() }
        })

    } catch {
        # STOP WATCHER ON ERROR
        $syncHash.IsRunning = $false

        $errMessage = $_.Exception.Message
        $syncHash.Dispatcher.Invoke({
            if ($syncHash.Timer -ne $null) { $syncHash.Timer.Stop() }
            $syncHash.LogBlock.Text += "`n[FATAL ERROR] $errMessage"
            $syncHash.StatusLabel.Text = "Error Occurred"
            $syncHash.ProgressBar.Foreground = "#FFf87171"
            $syncHash.ProgressBar.IsIndeterminate = $false
            $syncHash.ProgressBar.Value = 100
            $syncHash.CloseBtn.Visibility = "Visible"
        })
    }
'@)

# -----------------------------------------------------------------------------
# START EXECUTION
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
    IsRunning   = $true # Flag to control the watcher thread
})

$currentPath = $MyInvocation.MyCommand.Path

# START ENGINE AND WATCHER
$window.Add_Loaded({
    try {
        # 1. Start Main Logic
        $psMain = [PowerShell]::Create()
        # ARGUMENT UPDATE: Passing CSS variables here
        $psMain.AddScript($logicBlock).AddArgument($syncHash).AddArgument($currentPath).AddArgument($cssXpui_Content).AddArgument($cssComfy_Content) | Out-Null
        $handleMain = $psMain.BeginInvoke()

        # 2. Start Killer Watcher (Separate Thread)
        $psWatcher = [PowerShell]::Create()
        $psWatcher.AddScript($watcherBlock).AddArgument($syncHash) | Out-Null
        $handleWatcher = $psWatcher.BeginInvoke()
    }
    catch {
        $logOutput.Text = "FATAL: Could not start PowerShell engine: $($_.Exception.Message)"
    }
})

[void]$window.ShowDialog()