<#
.SYNOPSIS
    LibreSpot - Comprehensive SpotX + Spicetify Installer
    Easy Mode | Custom Mode | Maintenance Mode
.DESCRIPTION
    All-in-one installer for SpotX (ad-blocking/patching) and Spicetify
    (themes/extensions/Marketplace) with full GUI configuration.
.NOTES
    Credits:
      SpotX       - github.com/SpotX-Official/SpotX
      Spicetify   - github.com/spicetify
      Marketplace - github.com/spicetify/marketplace
      Themes      - github.com/spicetify/spicetify-themes
      ohitstom    - github.com/ohitstom/spicetify-extensions
#>

# =============================================================================
# 1. INITIAL SETUP
# =============================================================================
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32 {
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    public const int SW_HIDE = 0;
    public const int SW_MINIMIZE = 6;
}
'@ -ErrorAction SilentlyContinue

$ErrorActionPreference = 'Stop'

$global:VERSION = '3.0.0'

# --- Pinned dependency versions with SHA256 verification ---
# Update these when new versions are tested. Use Maintenance > Check for Updates.
$global:PinnedReleases = @{
    SpotX = @{
        Version = '1.9'
        Url     = 'https://raw.githubusercontent.com/SpotX-Official/SpotX/refs/tags/1.9/run.ps1'
        SHA256  = '46e0a1314f18a8ae07eb7e4b66052ef558fe850d5041089c71ae85d4b64afca8'
    }
    SpicetifyCLI = @{
        Version = '2.42.8'
        SHA256  = @{
            x64   = '677437a2bfd57a07c609fd4c398c71932c2d507e3e726fdd0124d4619782d3f5'
            x32   = '0a4aacaacf0e4f8562db6f81b8e6739c8f0739be96a261003fb3ab59ee0e9a02'
            arm64 = '9b9eb7abcf944f7e4c91c76fa91e3c046c4841d5506864c42e7a9286380b8f2c'
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

$global:BrushGreen = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FF22c55e"))
$global:BrushRed   = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFef4444"))
$global:BrushMuted = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFa1a1aa"))
$global:BrushError = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString("#FFf87171"))
foreach ($b in @($global:BrushGreen,$global:BrushRed,$global:BrushMuted,$global:BrushError)) { $b.Freeze() }

$script:openRunspaces = [System.Collections.Generic.List[object]]::new()

# =============================================================================
# 2. ADMIN CHECK
# =============================================================================
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $currentPath = $MyInvocation.MyCommand.Path
    if ([string]::IsNullOrEmpty($currentPath)) { Write-Warning "Script must be saved to disk to self-elevate." }
    else { Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$currentPath`"" -Verb RunAs }
    Exit
}

# =============================================================================
# 3. DATA
# =============================================================================
$global:ThemeData = [ordered]@{
    "(None - Marketplace Only)" = @{ Schemes = @("Default") }
    "Sleek"       = @{ Schemes = @("Wealthy","Cherry","Coral","Deep","Greener","Deeper","Psycho","UltraBlack","Nord","Futura","Elementary","BladeRunner","Dracula","VantaBlack","RosePine","Eldritch","Catppuccin","AyuDark","TokyoNight") }
    "Dribbblish"  = @{ Schemes = @("base","white","dark","dracula","nord-light","nord-dark","purple","samurai","beach-sunset","gruvbox","gruvbox-material-dark","rosepine","lunar","catppuccin-latte","catppuccin-frappe","catppuccin-macchiato","catppuccin-mocha","tokyo-night","kanagawa") }
    "Ziro"        = @{ Schemes = @("blue-dark","blue-light","gray-dark","gray-light","green-dark","green-light","orange-dark","orange-light","purple-dark","purple-light","red-dark","red-light","rose-pine","rose-pine-moon","rose-pine-dawn","tokyo-night") }
    "text"        = @{ Schemes = @("Spotify","Spicetify","CatppuccinMocha","CatppuccinMacchiato","CatppuccinLatte","Dracula","Gruvbox","Kanagawa","Nord","Rigel","RosePine","RosePineMoon","RosePineDawn","Solarized","TokyoNight","TokyoNightStorm","ForestGreen","EverforestDarkHard","EverforestDarkMedium","EverforestDarkSoft") }
    "StarryNight" = @{ Schemes = @("Base","Cotton-candy","Forest","Galaxy","Orange","Sky","Sunrise") }
    "Turntable"   = @{ Schemes = @("turntable") }
    "Blackout"    = @{ Schemes = @("def") }
    "Blossom"     = @{ Schemes = @("dark") }
    "BurntSienna" = @{ Schemes = @("Base") }
    "Default"     = @{ Schemes = @("Ocean") }
    "Dreary"      = @{ Schemes = @("Psycho","Deeper","BIB","Mono","Golden","Graytone-Blue") }
    "Flow"        = @{ Schemes = @("Pink","Green","Silver","Violet","Ocean") }
    "Matte"       = @{ Schemes = @("matte","periwinkle","periwinkle-dark","porcelain","rose-pine-moon","gray-dark1","gray-dark2","gray-dark3","gray","gray-light") }
    "Nightlight"  = @{ Schemes = @("Nightlight Colors") }
    "Onepunch"    = @{ Schemes = @("dark","light","legacy") }
    "SharkBlue"   = @{ Schemes = @("Base") }
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
    SpotX_TopSearch=$false; SpotX_NewFullscreen=$false; SpotX_RightSidebarOff=$false; SpotX_RightSidebarClr=$false
    SpotX_CanvasHomeOff=$false; SpotX_HomeSubOff=$false; SpotX_DisableStartup=$true; SpotX_NoShortcut=$false; SpotX_CacheLimit=0
    Spicetify_Theme="(None - Marketplace Only)"; Spicetify_Scheme="Default"; Spicetify_Marketplace=$true
    Spicetify_Extensions=@("fullAppDisplay.js","shuffle+.js","trashbin.js")
    CleanInstall=$true; LaunchAfter=$true
}

# =============================================================================
# 4. SETTINGS PERSISTENCE
# =============================================================================
function Save-LibreSpotConfig { param([hashtable]$Config)
    try {
        if (-not (Test-Path $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }
        $json = @{}; foreach ($k in $Config.Keys) { $json[$k] = $Config[$k] }
        $json | ConvertTo-Json -Depth 3 | Set-Content -Path $global:CONFIG_PATH -Encoding UTF8 -Force
    } catch { }
}

function Load-LibreSpotConfig {
    try {
        if (Test-Path $global:CONFIG_PATH) {
            $json = Get-Content $global:CONFIG_PATH -Raw -Encoding UTF8 | ConvertFrom-Json
            $cfg = @{}; foreach ($p in $json.PSObject.Properties) {
                if ($p.Value -is [System.Object[]]) { $cfg[$p.Name] = @($p.Value) } else { $cfg[$p.Name] = $p.Value }
            }; return $cfg
        }
    } catch { }
    return $null
}

# =============================================================================
# 5. WPF XAML
# =============================================================================
$ErrorActionPreference = 'Continue'  # WPF internals generate non-terminating errors with complex templates

$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LibreSpot" Height="760" Width="920"
        WindowStyle="None" ResizeMode="NoResize" AllowsTransparency="True"
        Background="#00000000" WindowStartupLocation="CenterScreen">
    <Window.Resources>
        <!-- Rounded ProgressBar -->
        <ControlTemplate x:Key="RoundProgress" TargetType="ProgressBar">
            <Grid><Border x:Name="PART_Track" CornerRadius="3" Background="{TemplateBinding Background}" Height="6"/>
                <Border x:Name="PART_Indicator" CornerRadius="3" HorizontalAlignment="Left" Height="6" Background="{TemplateBinding Foreground}"/></Grid>
        </ControlTemplate>
        <!-- ComboBox Toggle -->
        <ControlTemplate x:Key="DarkComboBoxToggle" TargetType="ToggleButton">
            <Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="30"/></Grid.ColumnDefinitions>
                <Border x:Name="Border" Grid.ColumnSpan="2" CornerRadius="6" Background="#FF18181b" BorderBrush="#FF27272a" BorderThickness="1"/>
                <Border Grid.Column="0" CornerRadius="6,0,0,6" Background="Transparent"/>
                <Path Grid.Column="1" Fill="#FF71717a" HorizontalAlignment="Center" VerticalAlignment="Center" Data="M 0 0 L 4 4 L 8 0 Z"/>
            </Grid>
            <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Border" Property="BorderBrush" Value="#FF3f3f46"/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
        <!-- ComboBox -->
        <Style x:Key="DarkComboBox" TargetType="ComboBox">
            <Setter Property="Foreground" Value="#FFfafafa"/><Setter Property="Background" Value="#FF18181b"/><Setter Property="Height" Value="32"/><Setter Property="FontSize" Value="12"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox"><Grid>
                <ToggleButton Template="{StaticResource DarkComboBoxToggle}" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}" Focusable="False" ClickMode="Press"/>
                <ContentPresenter IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" Margin="10,0,30,0" VerticalAlignment="Center" HorizontalAlignment="Left"/>
                <Popup IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" Focusable="False" AllowsTransparency="True">
                    <Border Background="#FF18181b" BorderBrush="#FF3f3f46" BorderThickness="1" CornerRadius="6" MaxHeight="300" Margin="0,4,0,0">
                        <Border.Effect><DropShadowEffect BlurRadius="16" ShadowDepth="4" Opacity="0.4" Direction="270"/></Border.Effect>
                        <ScrollViewer><StackPanel IsItemsHost="True"/></ScrollViewer></Border>
                </Popup>
            </Grid></ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- ComboBox Item -->
        <Style x:Key="DarkComboBoxItem" TargetType="ComboBoxItem">
            <Setter Property="Foreground" Value="#FFfafafa"/><Setter Property="Background" Value="Transparent"/><Setter Property="Padding" Value="10,6"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem">
                <Border x:Name="Bd" Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}" CornerRadius="4" Margin="3,1">
                    <ContentPresenter/></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Background" Value="#FF27272a"/></Trigger>
                    <Trigger Property="IsSelected" Value="True"><Setter TargetName="Bd" Property="Background" Value="#FF166534"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- CheckBox -->
        <Style x:Key="DarkCheckBox" TargetType="CheckBox">
            <Setter Property="Foreground" Value="#FFd4d4d8"/><Setter Property="FontSize" Value="12"/><Setter Property="Margin" Value="0,4"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="CheckBox"><StackPanel Orientation="Horizontal">
                <Border x:Name="box" Width="18" Height="18" CornerRadius="4" Background="#FF18181b" BorderBrush="#FF3f3f46" BorderThickness="1.5" Margin="0,0,10,0">
                    <Path x:Name="check" Data="M 3 6 L 6 9 L 11 3" Stroke="#FF22c55e" StrokeThickness="2" Visibility="Collapsed" Margin="1,1,0,0"/></Border>
                <ContentPresenter VerticalAlignment="Center"/>
            </StackPanel><ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True"><Setter TargetName="check" Property="Visibility" Value="Visible"/><Setter TargetName="box" Property="Background" Value="#FF0a2618"/><Setter TargetName="box" Property="BorderBrush" Value="#FF22c55e"/></Trigger>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="box" Property="BorderBrush" Value="#FF52525b"/></Trigger>
            </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Action Button -->
        <Style x:Key="ActionButton" TargetType="Button">
            <Setter Property="Height" Value="38"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="FontSize" Value="13"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Foreground" Value="#FFfafafa"/><Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="6" Padding="20,0">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="border" Property="Opacity" Value="0.88"/></Trigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter TargetName="border" Property="Opacity" Value="0.3"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Mode Radio Tab -->
        <Style x:Key="ModeRadio" TargetType="RadioButton">
            <Setter Property="Foreground" Value="#FF71717a"/><Setter Property="FontSize" Value="13"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="Cursor" Value="Hand"/><Setter Property="Margin" Value="0,0,2,0"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RadioButton">
                <Grid><Border x:Name="bd" Background="Transparent" CornerRadius="8,8,0,0" Padding="20,10">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                    <Border x:Name="indicator" Height="2" VerticalAlignment="Bottom" Background="Transparent" CornerRadius="1" Margin="8,0"/>
                </Grid>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsChecked" Value="True"><Setter TargetName="bd" Property="Background" Value="#FF111113"/><Setter Property="Foreground" Value="#FFfafafa"/><Setter TargetName="indicator" Property="Background" Value="#FF22c55e"/></Trigger>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="bd" Property="Background" Value="#FF111113"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Maintenance Button -->
        <Style x:Key="MaintButton" TargetType="Button">
            <Setter Property="Height" Value="54"/><Setter Property="Background" Value="#FF18181b"/><Setter Property="Foreground" Value="#FFfafafa"/><Setter Property="FontSize" Value="12"/>
            <Setter Property="FontWeight" Value="Normal"/><Setter Property="Cursor" Value="Hand"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Margin" Value="0,3"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="8" BorderBrush="#FF27272a" BorderThickness="1"><Grid>
                    <Rectangle x:Name="accent" Fill="#FF22c55e" Width="3" HorizontalAlignment="Left" RadiusX="1.5" RadiusY="1.5" Margin="8,14" Opacity="0.4"/>
                    <ContentPresenter HorizontalAlignment="Left" VerticalAlignment="Center" Margin="24,0,16,0"/></Grid></Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="border" Property="Background" Value="#FF1f1f23"/><Setter TargetName="border" Property="BorderBrush" Value="#FF3f3f46"/><Setter TargetName="accent" Property="Opacity" Value="1"/></Trigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter TargetName="border" Property="Opacity" Value="0.3"/></Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
        <!-- Tooltip -->
        <Style TargetType="ToolTip">
            <Setter Property="Background" Value="#FF18181b"/><Setter Property="Foreground" Value="#FFd4d4d8"/><Setter Property="BorderBrush" Value="#FF3f3f46"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontSize" Value="11"/><Setter Property="Padding" Value="10,6"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ToolTip">
                <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="6" Padding="{TemplateBinding Padding}">
                    <Border.Effect><DropShadowEffect BlurRadius="12" ShadowDepth="2" Opacity="0.35" Direction="270"/></Border.Effect>
                    <ContentPresenter/></Border>
            </ControlTemplate></Setter.Value></Setter>
        </Style>
    </Window.Resources>

    <!-- Outer margin gives room for window shadow -->
    <Grid Margin="16">
        <Border CornerRadius="12" Background="#FF09090b" BorderBrush="#FF1a1a1e" BorderThickness="1" ClipToBounds="False">
            <Border.Effect><DropShadowEffect BlurRadius="28" ShadowDepth="0" Opacity="0.5" Color="#000000"/></Border.Effect>
            <Grid ClipToBounds="True">
                <!-- Top accent gradient line -->
                <Border Height="2" VerticalAlignment="Top" CornerRadius="12,12,0,0" Panel.ZIndex="2"><Border.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                        <GradientStop Color="#FF0d3320" Offset="0"/><GradientStop Color="#FF22c55e" Offset="0.35"/>
                        <GradientStop Color="#FF4ade80" Offset="0.55"/><GradientStop Color="#FF0d3320" Offset="1"/>
                    </LinearGradientBrush></Border.Background></Border>

                <Grid><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>

                    <!-- ===== TITLE BAR ===== -->
                    <Border Grid.Row="0" Background="#FF09090b" Padding="24,18,24,0">
                        <Grid>
                            <StackPanel Orientation="Horizontal" HorizontalAlignment="Left" VerticalAlignment="Center">
                                <Ellipse Width="8" Height="8" Fill="#FF22c55e" Margin="0,1,10,0" VerticalAlignment="Center">
                                    <Ellipse.Effect><DropShadowEffect BlurRadius="8" ShadowDepth="0" Opacity="0.4" Color="#FF22c55e"/></Ellipse.Effect></Ellipse>
                                <TextBlock Name="TitleText" Foreground="#FFfafafa" FontSize="18" FontWeight="Bold" VerticalAlignment="Center"/>
                                <TextBlock Text="SpotX + Spicetify" Foreground="#FF52525b" FontSize="11" FontWeight="SemiBold" VerticalAlignment="Center" Margin="12,1,0,0"/>
                            </StackPanel>
                            <StackPanel HorizontalAlignment="Right" VerticalAlignment="Center" Orientation="Horizontal">
                                <Button Name="LinkGitHub" Width="28" Height="28" Background="Transparent" BorderThickness="0" Cursor="Hand" ToolTip="View on GitHub" VerticalAlignment="Center" Margin="0,0,8,0">
                                    <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="6"><Path x:Name="ico" Fill="#FF52525b" Data="M8,0 C3.58,0 0,3.58 0,8 c0,3.54 2.29,6.53 5.47,7.59 c.4,.07 .55,-.17 .55,-.38 c0,-.19 -.01,-.82 -.01,-1.49 c-2.01,.37 -2.53,-.49 -2.69,-.94 c-.09,-.23 -.48,-.94 -.82,-1.13 c-.28,-.15 -.68,-.52 -.01,-.53 c.63,-.01 1.08,.58 1.23,.82 c.72,1.21 1.87,.87 2.33,.66 c.07,-.52 .28,-.87 .51,-1.07 c-1.78,-.2 -3.64,-.89 -3.64,-3.95 c0,-.87 .31,-1.59 .82,-2.15 c-.08,-.2 -.36,-1.02 .08,-2.12 c0,0 .67,-.21 2.2,.82 c.64,-.18 1.32,-.27 2,-.27 c.68,0 1.36,.09 2,.27 c1.53,-1.04 2.2,-.82 2.2,-.82 c.44,1.1 .16,1.92 .08,2.12 c.51,.56 .82,1.27 .82,2.15 c0,3.07 -1.87,3.75 -3.65,3.95 c.29,.25 .54,.73 .54,1.48 c0,1.07 -.01,1.93 -.01,2.2 c0,.21 .15,.46 .55,.38 A8.013,8.013,0,0,0,16,8 c0,-4.42 -3.58,-8 -8,-8z" Stretch="Uniform" Width="14" Height="14" HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#FF27272a"/><Setter TargetName="ico" Property="Fill" Value="#FFa1a1aa"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template></Button>
                                <TextBlock VerticalAlignment="Center" Margin="0,0,16,0"><Hyperlink Name="LinkSpotX" NavigateUri="https://github.com/SpotX-Official/SpotX" Foreground="#FF52525b" TextDecorations="None" FontSize="10" Cursor="Hand">SpotX</Hyperlink></TextBlock>
                                <TextBlock VerticalAlignment="Center" Margin="0,0,16,0"><Hyperlink Name="LinkSpicetify" NavigateUri="https://github.com/spicetify" Foreground="#FF52525b" TextDecorations="None" FontSize="10" Cursor="Hand">Spicetify</Hyperlink></TextBlock>
                                <Button Name="MinimizeBtn" Content="&#x2013;" Width="32" Height="28" Background="Transparent" Foreground="#FF52525b" BorderThickness="0" FontSize="12" FontWeight="Bold" Cursor="Hand">
                                    <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="6"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#FF27272a"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template></Button>
                                <Button Name="CloseTitleBtn" Content="&#x2715;" Width="32" Height="28" Background="Transparent" Foreground="#FF52525b" BorderThickness="0" FontSize="11" Cursor="Hand">
                                    <Button.Template><ControlTemplate TargetType="Button"><Border x:Name="b" Background="Transparent" CornerRadius="6"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border>
                                        <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Background" Value="#FFdc2626"/><Setter Property="Foreground" Value="#FFfafafa"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Button.Template></Button>
                            </StackPanel>
                        </Grid>
                    </Border>

                    <!-- ===== CONTENT ===== -->
                    <Grid Name="PageContainer" Grid.Row="1" Margin="24,14,24,24">
                        <!-- ===== CONFIG PAGE ===== -->
                        <Grid Name="PageConfig" Visibility="Visible"><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                            <StackPanel Grid.Row="0" Orientation="Horizontal">
                                <RadioButton Name="ModeEasy" Content="Easy Install" IsChecked="True" Style="{StaticResource ModeRadio}" GroupName="Mode"/>
                                <RadioButton Name="ModeCustom" Content="Custom Install" Style="{StaticResource ModeRadio}" GroupName="Mode"/>
                                <RadioButton Name="ModeMaint" Content="Maintenance" Style="{StaticResource ModeRadio}" GroupName="Mode"/>
                            </StackPanel>
                            <Border Grid.Row="1" Background="#FF111113" CornerRadius="0,8,8,8" Padding="20" BorderBrush="#FF1a1a1e" BorderThickness="1"><Grid>

                                <!-- ===== EASY PANEL ===== -->
                                <StackPanel Name="PanelEasy" Visibility="Visible" VerticalAlignment="Center" HorizontalAlignment="Center" MaxWidth="520">
                                    <TextBlock Text="One-Click Setup" Foreground="#FFfafafa" FontSize="22" FontWeight="Bold" HorizontalAlignment="Center" Margin="0,0,0,4"/>
                                    <TextBlock Text="Everything you need, configured and ready to go." Foreground="#FF71717a" FontSize="12" HorizontalAlignment="Center" Margin="0,0,0,24"/>

                                    <Border Background="#FF0d0d10" CornerRadius="10" Padding="24,20" BorderBrush="#FF1a1a1e" BorderThickness="1"><StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,16">
                                            <TextBlock Text="Includes" Foreground="#FF22c55e" FontSize="12" FontWeight="Bold"/>
                                        </StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,6"><Border Width="20" Height="20" CornerRadius="10" Background="#FF0a2618"><Path Data="M 5 9 L 8 12 L 14 5" Stroke="#FF22c55e" StrokeThickness="1.5" Margin="1,0,0,0"/></Border>
                                            <TextBlock Text="Fresh Spotify with SpotX ad-blocking" Foreground="#FFd4d4d8" FontSize="12" VerticalAlignment="Center" Margin="12,0,0,0"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,6"><Border Width="20" Height="20" CornerRadius="10" Background="#FF0a2618"><Path Data="M 5 9 L 8 12 L 14 5" Stroke="#FF22c55e" StrokeThickness="1.5" Margin="1,0,0,0"/></Border>
                                            <TextBlock Text="New UI theme, podcasts removed" Foreground="#FFd4d4d8" FontSize="12" VerticalAlignment="Center" Margin="12,0,0,0"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,6"><Border Width="20" Height="20" CornerRadius="10" Background="#FF0a2618"><Path Data="M 5 9 L 8 12 L 14 5" Stroke="#FF22c55e" StrokeThickness="1.5" Margin="1,0,0,0"/></Border>
                                            <TextBlock Text="Auto-updates blocked to preserve patches" Foreground="#FFd4d4d8" FontSize="12" VerticalAlignment="Center" Margin="12,0,0,0"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,6"><Border Width="20" Height="20" CornerRadius="10" Background="#FF0a2618"><Path Data="M 5 9 L 8 12 L 14 5" Stroke="#FF22c55e" StrokeThickness="1.5" Margin="1,0,0,0"/></Border>
                                            <TextBlock Text="Spicetify CLI with Marketplace" Foreground="#FFd4d4d8" FontSize="12" VerticalAlignment="Center" Margin="12,0,0,0"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,6"><Border Width="20" Height="20" CornerRadius="10" Background="#FF0a2618"><Path Data="M 5 9 L 8 12 L 14 5" Stroke="#FF22c55e" StrokeThickness="1.5" Margin="1,0,0,0"/></Border>
                                            <TextBlock Text="Extensions: Full App Display, Shuffle+, Trash Bin" Foreground="#FFd4d4d8" FontSize="12" VerticalAlignment="Center" Margin="12,0,0,0"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,6"><Border Width="20" Height="20" CornerRadius="10" Background="#FF0a2618"><Path Data="M 5 9 L 8 12 L 14 5" Stroke="#FF22c55e" StrokeThickness="1.5" Margin="1,0,0,0"/></Border>
                                            <TextBlock Text="Lyrics with static theme enabled" Foreground="#FFd4d4d8" FontSize="12" VerticalAlignment="Center" Margin="12,0,0,0"/></StackPanel>
                                    </StackPanel></Border>
                                    <TextBlock Text="Removes any existing installation first." Foreground="#FF52525b" FontSize="10" HorizontalAlignment="Center" Margin="0,14,0,0"/>
                                </StackPanel>

                                <!-- ===== CUSTOM PANEL ===== -->
                                <ScrollViewer Name="PanelCustom" Visibility="Collapsed" VerticalScrollBarVisibility="Auto"><Grid>
                                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="20"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="SpotX Options" Foreground="#FFfafafa" FontSize="14" FontWeight="Bold" Margin="0,0,0,14"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,8"><Ellipse Width="5" Height="5" Fill="#FF22c55e" VerticalAlignment="Center" Margin="0,0,8,0"/><TextBlock Text="PATCHING" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                        <CheckBox Name="ChkNewTheme" Content="Enable new UI theme" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Activates Spotify new sidebar and cover art layout"/>
                                        <CheckBox Name="ChkPodcastsOff" Content="Remove podcasts from homepage" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Hides podcast sections from home feed"/>
                                        <CheckBox Name="ChkAdSectionsOff" Content="Hide ad-like homepage sections" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Removes promotional sections"/>
                                        <CheckBox Name="ChkBlockUpdate" Content="Block Spotify auto-updates" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="Prevents Spotify from overwriting patches"/>
                                        <CheckBox Name="ChkPremium" Content="Premium user (skip ad-blocking)" Style="{StaticResource DarkCheckBox}" ToolTip="For paid users: skip ad-blocking, keep other mods"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,14,0,8"><Ellipse Width="5" Height="5" Fill="#FF22c55e" VerticalAlignment="Center" Margin="0,0,8,0"/><TextBlock Text="LYRICS" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                        <CheckBox Name="ChkLyrics" Content="Enable static lyrics theme" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                        <StackPanel Name="LyricsThemePanel" Orientation="Horizontal" Margin="28,4,0,0">
                                            <TextBlock Text="Theme:" Foreground="#FF71717a" FontSize="11" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                            <ComboBox Name="CmbLyricsTheme" Width="140" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}" SelectedIndex="0">
                                                <ComboBoxItem Content="spotify"/><ComboBoxItem Content="blueberry"/></ComboBox>
                                        </StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,14,0,8"><Ellipse Width="5" Height="5" Fill="#FF22c55e" VerticalAlignment="Center" Margin="0,0,8,0"/><TextBlock Text="UI EXPERIMENTS" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                        <CheckBox Name="ChkTopSearch" Content="Top search bar" Style="{StaticResource DarkCheckBox}" ToolTip="Move search bar to top of window"/>
                                        <CheckBox Name="ChkNewFullscreen" Content="New fullscreen mode" Style="{StaticResource DarkCheckBox}" ToolTip="Enable experimental fullscreen display"/>
                                        <CheckBox Name="ChkRightSidebarOff" Content="Disable right sidebar" Style="{StaticResource DarkCheckBox}" ToolTip="Remove the Now Playing sidebar panel"/>
                                        <CheckBox Name="ChkRightSidebarColor" Content="Right sidebar color matching" Style="{StaticResource DarkCheckBox}" ToolTip="Tint sidebar to match album cover"/>
                                        <CheckBox Name="ChkCanvasHomeOff" Content="Disable canvas on homepage" Style="{StaticResource DarkCheckBox}" ToolTip="Turn off animated canvas art on home"/>
                                        <CheckBox Name="ChkHomeSubOff" Content="Disable home subfeed chips" Style="{StaticResource DarkCheckBox}" ToolTip="Hide genre filter chips on home page"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,14,0,8"><Ellipse Width="5" Height="5" Fill="#FF22c55e" VerticalAlignment="Center" Margin="0,0,8,0"/><TextBlock Text="SYSTEM" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                        <CheckBox Name="ChkDisableStartup" Content="Disable Spotify on Windows startup" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkNoShortcut" Content="Don't create desktop shortcut" Style="{StaticResource DarkCheckBox}"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                                            <TextBlock Text="Cache limit (MB, 0 = default):" Foreground="#FFd4d4d8" FontSize="12" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                            <TextBox Name="TxtCacheLimit" Width="70" Height="30" Text="0" Background="#FF18181b" Foreground="#FFfafafa" BorderBrush="#FF27272a" BorderThickness="1" FontSize="12" Padding="8,4" VerticalContentAlignment="Center"/>
                                        </StackPanel>
                                    </StackPanel>
                                    <StackPanel Grid.Column="2">
                                        <TextBlock Text="Spicetify Options" Foreground="#FFfafafa" FontSize="14" FontWeight="Bold" Margin="0,0,0,14"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,8"><Ellipse Width="5" Height="5" Fill="#FF22c55e" VerticalAlignment="Center" Margin="0,0,8,0"/><TextBlock Text="THEME" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Theme:" Foreground="#FF71717a" FontSize="11" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                            <ComboBox Name="CmbTheme" Width="200" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,0,0,6"><TextBlock Text="Color Scheme:" Foreground="#FF71717a" FontSize="11" VerticalAlignment="Center" Margin="0,0,8,0"/>
                                            <ComboBox Name="CmbScheme" Width="180" Style="{StaticResource DarkComboBox}" ItemContainerStyle="{StaticResource DarkComboBoxItem}"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,14,0,8"><Ellipse Width="5" Height="5" Fill="#FF22c55e" VerticalAlignment="Center" Margin="0,0,8,0"/><TextBlock Text="MARKETPLACE" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                        <CheckBox Name="ChkMarketplace" Content="Install Spicetify Marketplace" IsChecked="True" Style="{StaticResource DarkCheckBox}" ToolTip="In-app store for themes and extensions"/>
                                        <TextBlock Text="Browse and install themes/extensions from within Spotify" Foreground="#FF52525b" FontSize="10" Margin="28,2,0,0"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,14,0,8"><Ellipse Width="5" Height="5" Fill="#FF22c55e" VerticalAlignment="Center" Margin="0,0,8,0"/><TextBlock Text="BUILT-IN EXTENSIONS" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                        <CheckBox Name="ChkExt_fullAppDisplay" Content="Full App Display" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_shuffle" Content="Shuffle+" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_trashbin" Content="Trash Bin" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_keyboard" Content="Keyboard Shortcuts" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_bookmark" Content="Bookmark" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_loopyLoop" Content="Loopy Loop" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_popupLyrics" Content="Pop-up Lyrics" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_autoSkipVideo" Content="Auto Skip Video" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_autoSkipExplicit" Content="Auto Skip Explicit" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkExt_webNowPlaying" Content="Web Now Playing (Rainmeter)" Style="{StaticResource DarkCheckBox}"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,14,0,8"><Ellipse Width="5" Height="5" Fill="#FF22c55e" VerticalAlignment="Center" Margin="0,0,8,0"/><TextBlock Text="INSTALL OPTIONS" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                        <CheckBox Name="ChkCleanInstall" Content="Full clean install (remove existing)" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                        <CheckBox Name="ChkLaunchAfter" Content="Launch Spotify when finished" IsChecked="True" Style="{StaticResource DarkCheckBox}"/>
                                    </StackPanel>
                                </Grid></ScrollViewer>

                                <!-- ===== MAINTENANCE PANEL ===== -->
                                <ScrollViewer Name="PanelMaint" Visibility="Collapsed" VerticalScrollBarVisibility="Auto"><StackPanel Margin="20,6">
                                    <TextBlock Text="Maintenance" Foreground="#FFfafafa" FontSize="18" FontWeight="Bold" Margin="0,0,0,4"/>
                                    <TextBlock Text="Manage your existing SpotX and Spicetify installation" Foreground="#FF52525b" FontSize="12" Margin="0,0,0,20"/>

                                    <Border Background="#FF0d0d10" CornerRadius="10" Padding="16,14" Margin="0,0,0,20" BorderBrush="#FF1a1a1e" BorderThickness="1"><StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,3"><Ellipse Width="7" Height="7" Fill="#FF71717a" VerticalAlignment="Center" Margin="0,0,10,0"/>
                                            <TextBlock Name="StatusSpotify" Text="Spotify: Checking..." Foreground="#FF71717a" FontSize="11.5"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,3"><Ellipse Width="7" Height="7" Fill="#FF71717a" VerticalAlignment="Center" Margin="0,0,10,0"/>
                                            <TextBlock Name="StatusSpotX" Text="SpotX: Checking..." Foreground="#FF71717a" FontSize="11.5"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,3"><Ellipse Width="7" Height="7" Fill="#FF71717a" VerticalAlignment="Center" Margin="0,0,10,0"/>
                                            <TextBlock Name="StatusSpicetify" Text="Spicetify: Checking..." Foreground="#FF71717a" FontSize="11.5"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,3"><Ellipse Width="7" Height="7" Fill="#FF71717a" VerticalAlignment="Center" Margin="0,0,10,0"/>
                                            <TextBlock Name="StatusMarketplace" Text="Marketplace: Checking..." Foreground="#FF71717a" FontSize="11.5"/></StackPanel>
                                        <StackPanel Orientation="Horizontal" Margin="0,3"><Ellipse Width="7" Height="7" Fill="#FF71717a" VerticalAlignment="Center" Margin="0,0,10,0"/>
                                            <TextBlock Name="StatusTheme" Text="Theme: Checking..." Foreground="#FF71717a" FontSize="11.5"/></StackPanel>
                                    </StackPanel></Border>

                                    <StackPanel Orientation="Horizontal" Margin="0,0,0,8"><Border Width="3" Height="12" CornerRadius="1.5" Background="#FF22c55e" Margin="0,0,8,0"/><TextBlock Text="BACKUP / RESTORE" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                    <Button Name="BtnBackupConfig" Style="{StaticResource MaintButton}" Content="    Backup Spicetify Config    -  Save themes, extensions, and settings"/>
                                    <Button Name="BtnRestoreConfig" Style="{StaticResource MaintButton}" Content="    Restore Spicetify Config   -  Restore from a previous backup"/>
                                    <StackPanel Orientation="Horizontal" Margin="0,14,0,8"><Border Width="3" Height="12" CornerRadius="1.5" Background="#FF22c55e" Margin="0,0,8,0"/><TextBlock Text="REPAIR / UPDATE" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                    <Button Name="BtnCheckUpdates" Style="{StaticResource MaintButton}" Content="    Check for Updates          -  Compare pinned versions against latest releases"/>
                                    <Button Name="BtnReapply" Style="{StaticResource MaintButton}" Content="    Reapply After Update       -  Reinstall SpotX + reapply Spicetify"/>
                                    <Button Name="BtnSpicetifyRestore" Style="{StaticResource MaintButton}" Content="    Restore Vanilla Spotify    -  Remove all Spicetify modifications"/>
                                    <StackPanel Orientation="Horizontal" Margin="0,14,0,8"><Border Width="3" Height="12" CornerRadius="1.5" Background="#FFef4444" Opacity="0.6" Margin="0,0,8,0"/><TextBlock Text="UNINSTALL" Foreground="#FF52525b" FontSize="10" FontWeight="Bold"/></StackPanel>
                                    <Button Name="BtnUninstallSpicetify" Style="{StaticResource MaintButton}" Content="    Uninstall Spicetify        -  Remove Spicetify completely (keeps SpotX)"/>
                                    <Button Name="BtnFullReset" Style="{StaticResource MaintButton}" Content="    Full Reset                 -  Remove everything and start fresh"/>
                                </StackPanel></ScrollViewer>
                            </Grid></Border>
                            <Grid Grid.Row="2" Margin="0,14,0,0">
                                <Button Name="BtnInstall" Content="BEGIN INSTALLATION" Foreground="#FF020617" Style="{StaticResource ActionButton}" Width="240" HorizontalAlignment="Right">
                                    <Button.Background><LinearGradientBrush StartPoint="0,0.5" EndPoint="1,0.5">
                                        <GradientStop Color="#FF16a34a" Offset="0"/><GradientStop Color="#FF22c55e" Offset="1"/>
                                    </LinearGradientBrush></Button.Background></Button>
                            </Grid>
                        </Grid>

                        <!-- ===== INSTALL PAGE ===== -->
                        <Grid Name="PageInstall" Visibility="Collapsed"><Grid.RowDefinitions><RowDefinition Height="*"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
                            <!-- Terminal-style log -->
                            <Border Grid.Row="0" CornerRadius="10" BorderBrush="#FF1a1a1e" BorderThickness="1" ClipToBounds="True"><Grid>
                                <Grid.RowDefinitions><RowDefinition Height="34"/><RowDefinition Height="*"/></Grid.RowDefinitions>
                                <Border Grid.Row="0" Background="#FF111113" Padding="14,0"><StackPanel Orientation="Horizontal" VerticalAlignment="Center">
                                    <Ellipse Width="8" Height="8" Fill="#FF27272a" Margin="0,0,6,0"/><Ellipse Width="8" Height="8" Fill="#FF27272a" Margin="0,0,6,0"/><Ellipse Width="8" Height="8" Fill="#FF27272a" Margin="0,0,14,0"/>
                                    <TextBlock Text="Output" Foreground="#FF52525b" FontSize="11" FontWeight="SemiBold"/></StackPanel></Border>
                                <Border Grid.Row="1" Background="#FF0a0a0c" Padding="14,10">
                                    <ScrollViewer Name="LogScroller" VerticalScrollBarVisibility="Auto">
                                        <TextBlock Name="LogOutput" Foreground="#FFa1a1aa" FontFamily="Cascadia Mono, Consolas, Courier New" FontSize="11.5" TextWrapping="Wrap"/>
                                    </ScrollViewer></Border>
                            </Grid></Border>
                            <!-- Status bar -->
                            <StackPanel Grid.Row="1" Margin="0,14,0,0"><Grid>
                                <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
                                    <TextBlock Name="StatusText" Text="Initializing..." Foreground="#FFfafafa" FontSize="13" FontWeight="SemiBold"/>
                                    <TextBlock Name="ElapsedTime" Text="" Foreground="#FF52525b" FontSize="11" VerticalAlignment="Center" Margin="14,0,0,0"/></StackPanel>
                                <TextBlock Name="StepIndicator" Text="Processing..." Foreground="#FF22c55e" FontSize="13" FontWeight="SemiBold" HorizontalAlignment="Right"/></Grid>
                                <ProgressBar Name="MainProgress" Height="6" Margin="0,10,0,0" Template="{StaticResource RoundProgress}" Background="#FF27272a" Foreground="#FF22c55e" Minimum="0" Maximum="100" Value="0"/></StackPanel>
                            <!-- Buttons -->
                            <StackPanel Grid.Row="2" Margin="0,14,0,0" Orientation="Horizontal" HorizontalAlignment="Right">
                                <Button Name="BtnCopyLog" Content="COPY LOG" Background="#FF27272a" Style="{StaticResource ActionButton}" Width="110" Margin="0,0,8,0" Visibility="Collapsed"/>
                                <Button Name="BtnBackToConfig" Content="BACK" Background="#FF18181b" Style="{StaticResource ActionButton}" Width="100" Margin="0,0,8,0" Visibility="Collapsed"/>
                                <Button Name="CloseBtn" Content="CLOSE" Background="#FF18181b" Style="{StaticResource ActionButton}" Width="100" Visibility="Collapsed"/></StackPanel>
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

$ui = @{}
@('LinkSpotX','LinkSpicetify','LinkGitHub','MinimizeBtn','CloseTitleBtn','PageConfig','PageInstall',
  'ModeEasy','ModeCustom','ModeMaint','PanelEasy','PanelCustom','PanelMaint','BtnInstall','LyricsThemePanel',
  'ChkNewTheme','ChkPodcastsOff','ChkAdSectionsOff','ChkBlockUpdate','ChkPremium','ChkLyrics','CmbLyricsTheme',
  'ChkTopSearch','ChkNewFullscreen','ChkRightSidebarOff','ChkRightSidebarColor','ChkCanvasHomeOff','ChkHomeSubOff',
  'ChkDisableStartup','ChkNoShortcut','TxtCacheLimit','CmbTheme','CmbScheme','ChkMarketplace',
  'ChkExt_fullAppDisplay','ChkExt_shuffle','ChkExt_trashbin','ChkExt_keyboard','ChkExt_bookmark','ChkExt_loopyLoop',
  'ChkExt_popupLyrics','ChkExt_autoSkipVideo','ChkExt_autoSkipExplicit','ChkExt_webNowPlaying',
  'ChkCleanInstall','ChkLaunchAfter',
  'StatusSpotify','StatusSpotX','StatusSpicetify','StatusMarketplace','StatusTheme',
  'BtnBackupConfig','BtnRestoreConfig','BtnCheckUpdates','BtnReapply','BtnSpicetifyRestore','BtnUninstallSpicetify','BtnFullReset',
  'LogScroller','LogOutput','StatusText','ElapsedTime','StepIndicator','MainProgress','BtnCopyLog','BtnBackToConfig','CloseBtn',
  'TitleText'
) | ForEach-Object { $el = $window.FindName($_); if ($el) { $ui[$_] = $el } }

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
$ui['CmbTheme'].SelectedIndex = 0

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
})

$ui['CmbTheme'].SelectedIndex = 0
if ($ui['CmbScheme'].Items.Count -eq 0 -and $ui['CmbTheme'].SelectedItem) {
    $tn = $ui['CmbTheme'].SelectedItem.Content
    if ($tn -and $global:ThemeData.Contains($tn)) {
        foreach ($s in $global:ThemeData[$tn].Schemes) {
            $i = New-Object System.Windows.Controls.ComboBoxItem; $i.Content = $s
            $i.Style = $window.FindResource("DarkComboBoxItem"); $ui['CmbScheme'].Items.Add($i) | Out-Null
        }; $ui['CmbScheme'].SelectedIndex = 0
    }
}

$ui['ChkLyrics'].Add_Checked({   $ui['LyricsThemePanel'].Visibility = 'Visible' })
$ui['ChkLyrics'].Add_Unchecked({ $ui['LyricsThemePanel'].Visibility = 'Collapsed' })

$premiumDependents = @('ChkPodcastsOff','ChkAdSectionsOff')
$ui['ChkPremium'].Add_Checked({   foreach ($n in $premiumDependents) { $ui[$n].IsEnabled = $false; $ui[$n].Opacity = 0.4 } })
$ui['ChkPremium'].Add_Unchecked({ foreach ($n in $premiumDependents) { $ui[$n].IsEnabled = $true;  $ui[$n].Opacity = 1.0 } })

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
    if ($savedCfg.ContainsKey('SpotX_NewFullscreen'))   { $ui['ChkNewFullscreen'].IsChecked   = [bool]$savedCfg.SpotX_NewFullscreen }
    if ($savedCfg.ContainsKey('SpotX_RightSidebarOff')) { $ui['ChkRightSidebarOff'].IsChecked = [bool]$savedCfg.SpotX_RightSidebarOff }
    if ($savedCfg.ContainsKey('SpotX_RightSidebarClr')) { $ui['ChkRightSidebarColor'].IsChecked = [bool]$savedCfg.SpotX_RightSidebarClr }
    if ($savedCfg.ContainsKey('SpotX_CanvasHomeOff'))   { $ui['ChkCanvasHomeOff'].IsChecked   = [bool]$savedCfg.SpotX_CanvasHomeOff }
    if ($savedCfg.ContainsKey('SpotX_HomeSubOff'))      { $ui['ChkHomeSubOff'].IsChecked      = [bool]$savedCfg.SpotX_HomeSubOff }
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
} catch {} }

# =============================================================================
# 7. UI EVENT HANDLERS
# =============================================================================
$lh = { param($s,$e); try { $psi = New-Object System.Diagnostics.ProcessStartInfo $e.Uri.AbsoluteUri; $psi.UseShellExecute = $true; [System.Diagnostics.Process]::Start($psi) | Out-Null } catch {} }
$ui['LinkSpotX'].Add_RequestNavigate($lh); $ui['LinkSpicetify'].Add_RequestNavigate($lh)
$ui['LinkGitHub'].Add_Click({ try { $psi = New-Object System.Diagnostics.ProcessStartInfo 'https://github.com/SysAdminDoc/LibreSpot'; $psi.UseShellExecute = $true; [System.Diagnostics.Process]::Start($psi) | Out-Null } catch {} })
if ($ui['TitleText']) { $ui['TitleText'].Text = "LibreSpot v$global:VERSION" }
$ui['BtnCopyLog'].Add_Click({ try { [System.Windows.Clipboard]::SetText($ui['LogOutput'].Text); $ui['BtnCopyLog'].Content = "COPIED!" } catch {} })
$window.Add_MouseLeftButtonDown({ $window.DragMove() })
$ui['CloseTitleBtn'].Add_Click({ $window.Close() })
$ui['MinimizeBtn'].Add_Click({ $window.WindowState = 'Minimized' })

$ui['ModeEasy'].Add_Checked({
    $ui['PanelEasy'].Visibility='Visible'; $ui['PanelCustom'].Visibility='Collapsed'; $ui['PanelMaint'].Visibility='Collapsed'
    $ui['BtnInstall'].Visibility='Visible'; $ui['BtnInstall'].Content='BEGIN INSTALLATION'
})
$ui['ModeCustom'].Add_Checked({
    $ui['PanelEasy'].Visibility='Collapsed'; $ui['PanelCustom'].Visibility='Visible'; $ui['PanelMaint'].Visibility='Collapsed'
    $ui['BtnInstall'].Visibility='Visible'; $ui['BtnInstall'].Content='BEGIN CUSTOM INSTALLATION'
})
$ui['ModeMaint'].Add_Checked({
    $ui['PanelEasy'].Visibility='Collapsed'; $ui['PanelCustom'].Visibility='Collapsed'; $ui['PanelMaint'].Visibility='Visible'
    $ui['BtnInstall'].Visibility='Collapsed'; Update-MaintenanceStatus
})
$ui['CloseBtn'].Add_Click({ $window.Close() })
$ui['BtnBackToConfig'].Add_Click({ $ui['PageInstall'].Visibility='Collapsed'; $ui['PageConfig'].Visibility='Visible'; $ui['BtnInstall'].IsEnabled=$true; $ui['BtnCopyLog'].Content='COPY LOG'; $ui['BtnCopyLog'].Visibility='Collapsed'; $window.Topmost=$false })
$window.Add_Closing({ foreach ($rs in $script:openRunspaces) { try { $rs.Dispose() } catch {} }; $script:openRunspaces.Clear() })

# =============================================================================
# 8. CONFIG BUILDER
# =============================================================================
function Get-InstallConfig { param([bool]$EasyMode = $false)
    if ($EasyMode) { $c = @{ Mode='Easy' }; foreach ($k in $global:EasyDefaults.Keys) { $c[$k]=$global:EasyDefaults[$k] }; return $c }
    $lTheme = if($ui['CmbLyricsTheme'].SelectedItem){$ui['CmbLyricsTheme'].SelectedItem.Content}else{'spotify'}
    $sTheme = if($ui['CmbTheme'].SelectedItem){$ui['CmbTheme'].SelectedItem.Content}else{'(None - Marketplace Only)'}
    $sScheme = if($ui['CmbScheme'].SelectedItem){$ui['CmbScheme'].SelectedItem.Content}else{'Default'}
    $cacheVal = 0; try { $cacheVal = [int]$ui['TxtCacheLimit'].Text } catch {}
    if ($cacheVal -lt 0) { $cacheVal = 0 }
    $exts = @(); foreach ($k in $extCheckboxMap.Keys) { if ($ui[$k].IsChecked) { $exts += $extCheckboxMap[$k] } }
    $c = @{
        Mode='Custom'; CleanInstall=[bool]$ui['ChkCleanInstall'].IsChecked; LaunchAfter=[bool]$ui['ChkLaunchAfter'].IsChecked
        SpotX_NewTheme=[bool]$ui['ChkNewTheme'].IsChecked; SpotX_PodcastsOff=[bool]$ui['ChkPodcastsOff'].IsChecked
        SpotX_AdSectionsOff=[bool]$ui['ChkAdSectionsOff'].IsChecked; SpotX_BlockUpdate=[bool]$ui['ChkBlockUpdate'].IsChecked
        SpotX_Premium=[bool]$ui['ChkPremium'].IsChecked; SpotX_DisableStartup=[bool]$ui['ChkDisableStartup'].IsChecked
        SpotX_NoShortcut=[bool]$ui['ChkNoShortcut'].IsChecked
        SpotX_LyricsEnabled=[bool]$ui['ChkLyrics'].IsChecked; SpotX_LyricsTheme=$lTheme
        SpotX_TopSearch=[bool]$ui['ChkTopSearch'].IsChecked; SpotX_NewFullscreen=[bool]$ui['ChkNewFullscreen'].IsChecked
        SpotX_RightSidebarOff=[bool]$ui['ChkRightSidebarOff'].IsChecked; SpotX_RightSidebarClr=[bool]$ui['ChkRightSidebarColor'].IsChecked
        SpotX_CanvasHomeOff=[bool]$ui['ChkCanvasHomeOff'].IsChecked; SpotX_HomeSubOff=[bool]$ui['ChkHomeSubOff'].IsChecked
        SpotX_CacheLimit=$cacheVal
        Spicetify_Theme=$sTheme; Spicetify_Scheme=$sScheme
        Spicetify_Marketplace=[bool]$ui['ChkMarketplace'].IsChecked; Spicetify_Extensions=$exts
    }
    return $c
}

function Build-SpotXParams { param($Config)
    $p = @()
    if ($Config.SpotX_NewTheme)        { $p += "-new_theme" }
    if ($Config.SpotX_PodcastsOff)     { $p += "-podcasts_off" } else { $p += "-podcasts_on" }
    if ($Config.SpotX_AdSectionsOff)   { $p += "-adsections_off" }
    if ($Config.SpotX_BlockUpdate)     { $p += "-block_update_on" } else { $p += "-block_update_off" }
    if ($Config.SpotX_Premium)         { $p += "-premium" }
    if ($Config.SpotX_DisableStartup)  { $p += "-DisableStartup" }
    if ($Config.SpotX_NoShortcut)      { $p += "-no_shortcut" }
    if ($Config.SpotX_LyricsEnabled)   { $p += "-lyrics_stat $($Config.SpotX_LyricsTheme)" }
    if ($Config.SpotX_TopSearch)       { $p += "-topsearchbar" }
    if ($Config.SpotX_NewFullscreen)   { $p += "-newFullscreenMode" }
    if ($Config.SpotX_RightSidebarOff) { $p += "-rightsidebar_off" }
    if ($Config.SpotX_RightSidebarClr) { $p += "-rightsidebarcolor" }
    if ($Config.SpotX_CanvasHomeOff)   { $p += "-canvashome_off" }
    if ($Config.SpotX_HomeSubOff)      { $p += "-homesub_off" }
    if ($Config.SpotX_CacheLimit -ge 500) { $p += "-cache_limit $($Config.SpotX_CacheLimit)" }
    return ($p -join " ")
}

# =============================================================================
# 9. MAINTENANCE
# =============================================================================
function Update-MaintenanceStatus {
    if (Test-Path $global:SPOTIFY_EXE_PATH) {
        try { $v = (Get-Item $global:SPOTIFY_EXE_PATH).VersionInfo.FileVersion; $ui['StatusSpotify'].Text = "Spotify: Installed (v$v)" }
        catch { $ui['StatusSpotify'].Text = "Spotify: Installed" }
        $ui['StatusSpotify'].Foreground = $global:BrushGreen
    } else { $ui['StatusSpotify'].Text = "Spotify: Not installed"; $ui['StatusSpotify'].Foreground = $global:BrushRed }

    $spotxFound = $false
    if (Test-Path "$env:APPDATA\Spotify\Apps\xpui.spa.bak") { $spotxFound = $true }
    if (-not $spotxFound) { try { if (Get-ChildItem (Join-Path $global:TEMP_DIR "SpotX_Temp*") -EA SilentlyContinue) { $spotxFound = $true } } catch {} }
    if ($spotxFound) { $ui['StatusSpotX'].Text = "SpotX: Patched"; $ui['StatusSpotX'].Foreground = $global:BrushGreen }
    elseif (Test-Path $global:SPOTIFY_EXE_PATH) { $ui['StatusSpotX'].Text = "SpotX: Not detected (vanilla)"; $ui['StatusSpotX'].Foreground = $global:BrushMuted }
    else { $ui['StatusSpotX'].Text = "SpotX: N/A"; $ui['StatusSpotX'].Foreground = $global:BrushMuted }

    $sExe = Join-Path $global:SPICETIFY_DIR "spicetify.exe"
    if (Test-Path $sExe) {
        try {
            $tmpOut = Join-Path $global:TEMP_DIR "spicetify_ver.txt"
            $pr = Start-Process -FilePath $sExe -ArgumentList "-v" -NoNewWindow -Wait -PassThru -RedirectStandardOutput $tmpOut -EA Stop
            $vo = if (Test-Path $tmpOut) { (Get-Content $tmpOut -Raw -EA SilentlyContinue).Trim() } else { $null }
            Remove-Item $tmpOut -Force -EA SilentlyContinue
            if ($vo) { $ui['StatusSpicetify'].Text = "Spicetify: Installed ($vo)" } else { $ui['StatusSpicetify'].Text = "Spicetify: Installed" }
        } catch { $ui['StatusSpicetify'].Text = "Spicetify: Installed" }
        $ui['StatusSpicetify'].Foreground = $global:BrushGreen
    } else { $ui['StatusSpicetify'].Text = "Spicetify: Not installed"; $ui['StatusSpicetify'].Foreground = $global:BrushMuted }

    $mp = Join-Path $global:SPICETIFY_CONFIG_DIR "CustomApps\marketplace"
    if (-not (Test-Path $mp)) { $mp = Join-Path $global:SPICETIFY_DIR "CustomApps\marketplace" }
    if (Test-Path $mp) { $ui['StatusMarketplace'].Text = "Marketplace: Installed"; $ui['StatusMarketplace'].Foreground = $global:BrushGreen }
    else { $ui['StatusMarketplace'].Text = "Marketplace: Not installed"; $ui['StatusMarketplace'].Foreground = $global:BrushMuted }

    $ini = Join-Path $global:SPICETIFY_CONFIG_DIR "config-xpui.ini"
    if ((Test-Path $ini) -and ((Get-Content $ini -Raw) -match 'current_theme\s*=\s*(.+)')) {
        $tn = $Matches[1].Trim(); if ([string]::IsNullOrWhiteSpace($tn)) { $tn = "None" }
        $ui['StatusTheme'].Text = "Theme: $tn"; $ui['StatusTheme'].Foreground = $global:BrushGreen
    } else { $ui['StatusTheme'].Text = "Theme: None"; $ui['StatusTheme'].Foreground = $global:BrushMuted }

    $si = Test-Path $sExe; $sp = Test-Path $global:SPOTIFY_EXE_PATH
    $bk = (Test-Path $global:BACKUP_ROOT) -and ((Get-ChildItem $global:BACKUP_ROOT -Directory -EA SilentlyContinue).Count -gt 0)
    $ui['BtnCheckUpdates'].IsEnabled=$true
    $pv = $global:PinnedReleases
    $ui['StatusSpotX'].ToolTip = "Pinned: SpotX v$($pv.SpotX.Version) | CLI v$($pv.SpicetifyCLI.Version) | Marketplace v$($pv.Marketplace.Version)"
    $ui['BtnBackupConfig'].IsEnabled=$si; $ui['BtnRestoreConfig'].IsEnabled=$bk; $ui['BtnReapply'].IsEnabled=$sp
    $ui['BtnSpicetifyRestore'].IsEnabled=$si; $ui['BtnUninstallSpicetify'].IsEnabled=$si; $ui['BtnFullReset'].IsEnabled=($sp -or $si)
}

$ui['BtnBackupConfig'].Add_Click({ try {
    $stamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"; $dest = Join-Path $global:BACKUP_ROOT $stamp
    New-Item -Path $dest -ItemType Directory -Force | Out-Null
    Copy-Item $global:SPICETIFY_CONFIG_DIR -Destination (Join-Path $dest "spicetify") -Recurse -Force
    $all = Get-ChildItem $global:BACKUP_ROOT -Directory | Sort-Object Name -Descending
    if ($all.Count -gt 5) { $all | Select-Object -Skip 5 | ForEach-Object { Remove-Item $_.FullName -Recurse -Force -EA SilentlyContinue } }
    [System.Windows.MessageBox]::Show("Backup saved: $stamp","Backup Complete","OK","Information"); Update-MaintenanceStatus
} catch { [System.Windows.MessageBox]::Show("Backup failed: $($_.Exception.Message)","Error","OK","Error") } })

$ui['BtnRestoreConfig'].Add_Click({ try {
    $all = Get-ChildItem $global:BACKUP_ROOT -Directory | Sort-Object Name -Descending
    if ($all.Count -eq 0) { [System.Windows.MessageBox]::Show("No backups found.","Error","OK","Error"); return }
    $list = ($all | ForEach-Object { $_.Name }) -join "`n"
    $r = [System.Windows.MessageBox]::Show("Available backups:`n`n$list`n`nRestore newest ($($all[0].Name))?","Confirm","YesNo","Question")
    if ($r -eq 'Yes') {
        $src = Join-Path $all[0].FullName "spicetify"
        if (-not (Test-Path $src)) { [System.Windows.MessageBox]::Show("Backup data missing.","Error","OK","Error"); return }
        if (Test-Path $global:SPICETIFY_CONFIG_DIR) { Remove-Item $global:SPICETIFY_CONFIG_DIR -Recurse -Force }
        Copy-Item $src -Destination $global:SPICETIFY_CONFIG_DIR -Recurse -Force
        $sExe = Join-Path $global:SPICETIFY_DIR "spicetify.exe"
        if (Test-Path $sExe) { Start-Process -FilePath $sExe -ArgumentList "backup","apply","--bypass-admin" -NoNewWindow -Wait }
        [System.Windows.MessageBox]::Show("Restored and applied.","Done","OK","Information"); Update-MaintenanceStatus
    }
} catch { [System.Windows.MessageBox]::Show("Restore failed: $($_.Exception.Message)","Error","OK","Error") } })

$ui['BtnCheckUpdates'].Add_Click({
    if (-not (Test-NetworkReady)) { [System.Windows.MessageBox]::Show("No internet connection.","Network Error","OK","Error"); return }
    Switch-ToInstallPage; Start-MaintenanceJob -Action 'CheckUpdates'
})
$ui['BtnReapply'].Add_Click({
    if (-not (Test-NetworkReady)) { [System.Windows.MessageBox]::Show("No internet connection.","Network Error","OK","Error"); return }
    $r = [System.Windows.MessageBox]::Show("Reapply SpotX + Spicetify?`nUses saved config if available.","Confirm","YesNo","Question")
    if ($r -eq 'Yes') { Switch-ToInstallPage; Start-MaintenanceJob -Action 'Reapply' }
})
$ui['BtnSpicetifyRestore'].Add_Click({
    $r = [System.Windows.MessageBox]::Show("Restore vanilla Spotify?`nRemoves Spicetify mods, keeps SpotX.","Confirm","YesNo","Question")
    if ($r -eq 'Yes') { Switch-ToInstallPage; Start-MaintenanceJob -Action 'RestoreVanilla' }
})
$ui['BtnUninstallSpicetify'].Add_Click({
    $r = [System.Windows.MessageBox]::Show("Uninstall Spicetify completely?","Confirm","YesNo","Warning")
    if ($r -eq 'Yes') { Switch-ToInstallPage; Start-MaintenanceJob -Action 'UninstallSpicetify' }
})
$ui['BtnFullReset'].Add_Click({
    $r = [System.Windows.MessageBox]::Show("FULL RESET:`n- Restore vanilla Spotify`n- Remove Spicetify`n- Remove SpotX`n- Uninstall Spotify`n- Clean all files`n`nContinue?","Full Reset","YesNo","Warning")
    if ($r -eq 'Yes') { Switch-ToInstallPage; Start-MaintenanceJob -Action 'FullReset' }
})

# =============================================================================
# 10. PAGE SWITCH + INSTALL TRIGGER
# =============================================================================
function Test-NetworkReady {
    try {
        $r = [System.Net.WebRequest]::Create("https://raw.githubusercontent.com"); $r.Timeout = 5000; $r.Method = 'HEAD'
        $resp = $r.GetResponse(); $resp.Close(); return $true
    } catch { return $false }
}

function Switch-ToInstallPage {
    $ui['PageConfig'].Visibility='Collapsed'; $ui['PageInstall'].Visibility='Visible'
    $ui['LogOutput'].Text=''; $ui['StatusText'].Text='Initializing...'; $ui['StepIndicator'].Text='Processing...'
    $ui['ElapsedTime'].Text=''; $ui['MainProgress'].Value=0; $ui['MainProgress'].Foreground=$global:BrushGreen
    $ui['CloseBtn'].Visibility='Collapsed'; $ui['BtnBackToConfig'].Visibility='Collapsed'; $ui['BtnCopyLog'].Visibility='Collapsed'; $ui['BtnCopyLog'].Content='COPY LOG'
    $window.Topmost = $true
}

$ui['BtnInstall'].Add_Click({
    if ($ui['BtnInstall'].IsEnabled -eq $false) { return }
    if (-not (Test-NetworkReady)) {
        [System.Windows.MessageBox]::Show("No internet connection detected.`nPlease check your network and try again.","Network Error","OK","Error")
        return
    }
    $ui['BtnInstall'].IsEnabled = $false
    $isEasy = $ui['ModeEasy'].IsChecked
    if ($isEasy) {
        $r = [System.Windows.MessageBox]::Show("This will remove any existing Spotify/Spicetify installation`nand perform a fresh setup with default settings.`n`nContinue?","Confirm Easy Install","YesNo","Question")
        if ($r -ne 'Yes') { $ui['BtnInstall'].IsEnabled = $true; return }
    }
    $script:InstallConfig = Get-InstallConfig -EasyMode $isEasy
    Save-LibreSpotConfig -Config $script:InstallConfig
    Switch-ToInstallPage; Start-InstallJob -Config $script:InstallConfig
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
    }
    if ($script:installStartTime) { $ui['ElapsedTime'].Text = "Elapsed: {0:mm\:ss}" -f ((Get-Date) - $script:installStartTime) }
})

# =============================================================================
# 12. HELPERS
# =============================================================================
function Update-UI { param([string]$Message,[string]$Level="INFO",[bool]$IsHeader=$false,[string]$StepText=$null)
    $ts = Get-Date -Format "HH:mm:ss"; $lt = "[$ts] [$Level] $Message`n"; $sh = $script:syncHash
    try { if ($sh) { $sh.Dispatcher.Invoke([Action]{
        $sh.LogBlock.Text += $lt; $sh.Scroller.ScrollToBottom()
        if ($IsHeader -or $Level -eq 'STEP') { $sh.StatusLabel.Text = $Message }
        if ($StepText) { $sh.StepLabel.Text = $StepText }
    }) } } catch {}
}
function Write-Log { param([string]$Message,[string]$Level='INFO'); Update-UI -Message $Message -Level $Level -IsHeader ($Level -eq 'STEP' -or $Level -eq 'HEADER') }

function Download-FileSafe { param([string]$Uri,[string]$OutFile)
    Write-Log "Downloading: $Uri"
    $headers = @{'User-Agent'='LibreSpot/3.0'}
    try { Invoke-WebRequest -Uri $Uri -OutFile $OutFile -UseBasicParsing -Headers $headers -TimeoutSec 120 -ErrorAction Stop }
    catch { Write-Log "Web request failed, trying BITS..." -Level 'WARN'
        try {
            Import-Module BitsTransfer -EA SilentlyContinue
            $bitsJob = Start-BitsTransfer -Source $Uri -Destination $OutFile -Asynchronous -EA Stop
            $deadline = (Get-Date).AddSeconds(120)
            while ($bitsJob.JobState -eq 'Transferring' -or $bitsJob.JobState -eq 'Connecting') {
                if ((Get-Date) -gt $deadline) { Remove-BitsTransfer $bitsJob -EA SilentlyContinue; throw "BITS transfer timed out (120s)" }
                Start-Sleep -Milliseconds 500
            }
            if ($bitsJob.JobState -ne 'Transferred') { $js=$bitsJob.JobState; Remove-BitsTransfer $bitsJob -EA SilentlyContinue; throw "BITS state: $js" }
            Complete-BitsTransfer $bitsJob
        } catch { throw "Download failed: $($_.Exception.Message)" } }
}
function Confirm-FileHash { param([string]$Path, [string]$ExpectedHash, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($ExpectedHash)) {
        Write-Log "  Hash verification skipped for $Label (no hash pinned)" -Level 'WARN'
        return
    }
    $actual = (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLower()
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

function Invoke-ExternalScriptIsolated { param([string]$FilePath,[string]$Arguments)
    Write-Log "Spawning: $FilePath"
    $pi = New-Object System.Diagnostics.ProcessStartInfo
    $pi.FileName="powershell.exe"; $pi.Arguments="-NoProfile -ExecutionPolicy Bypass -File `"$FilePath`" $Arguments"
    $pi.RedirectStandardOutput=$true; $pi.RedirectStandardError=$true; $pi.UseShellExecute=$false; $pi.CreateNoWindow=$true
    $p = New-Object System.Diagnostics.Process; $p.StartInfo=$pi; $null=$p.Start()
    $errTask = $p.StandardError.ReadToEndAsync()
    while (-not $p.HasExited) { $ln=$p.StandardOutput.ReadLine(); if (-not [string]::IsNullOrWhiteSpace($ln)) { Write-Log $ln -Level 'OUT' } }
    $rest=$p.StandardOutput.ReadToEnd(); if (-not [string]::IsNullOrWhiteSpace($rest)) { Write-Log $rest -Level 'OUT' }
    $p.WaitForExit()
    try { $err=$errTask.GetAwaiter().GetResult(); if (-not [string]::IsNullOrWhiteSpace($err)) { Write-Log "[STDERR] $err" -Level 'WARN' } } catch {}
    if ($p.ExitCode -ne 0) { throw "Process exited with code $($p.ExitCode)" }
}

# =============================================================================
# 13. UPDATE CHECKER
# =============================================================================
function Check-ForUpdates {
    Write-Log '=== Checking for dependency updates ===' -Level 'STEP'
    $headers = @{'User-Agent'='LibreSpot/3.0'}
    $updates = @()

    # SpotX
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/SpotX-Official/SpotX/releases/latest' -Headers $headers -TimeoutSec 15
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.SpotX.Version
        if ($latest -ne $pinned) { $updates += "SpotX: $pinned -> $latest"; Write-Log "  SpotX: $pinned -> $latest available" -Level 'WARN' }
        else { Write-Log "  SpotX: v$pinned (up to date)" }
    } catch { Write-Log "  SpotX: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Spicetify CLI
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/cli/releases/latest' -Headers $headers -TimeoutSec 15
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.SpicetifyCLI.Version
        if ($latest -ne $pinned) { $updates += "CLI: $pinned -> $latest"; Write-Log "  Spicetify CLI: $pinned -> $latest available" -Level 'WARN' }
        else { Write-Log "  Spicetify CLI: v$pinned (up to date)" }
    } catch { Write-Log "  Spicetify CLI: check failed ($($_.Exception.Message))" -Level 'WARN' }

    # Marketplace
    try {
        $rel = Invoke-RestMethod -Uri 'https://api.github.com/repos/spicetify/marketplace/releases/latest' -Headers $headers -TimeoutSec 15
        $latest = $rel.tag_name -replace '^v',''
        $pinned = $global:PinnedReleases.Marketplace.Version
        if ($latest -ne $pinned) { $updates += "Marketplace: $pinned -> $latest"; Write-Log "  Marketplace: $pinned -> $latest available" -Level 'WARN' }
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
        if ($shell -and (Test-Path $shell)) { return $shell }
    } catch {}
    return [Environment]::GetFolderPath('Desktop')
}

function Remove-PathSafely { param([string]$Path,[string]$Label)
    if ([string]::IsNullOrWhiteSpace($Path)) { return 0 }
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    try {
        $null = icacls $Path /reset /T /C /Q 2>$null
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
            try { Remove-Item $_ -Recurse -Force -EA Stop; Write-Log "  Removed on retry: $_"; $rc++ }
            catch { $survivors += $_ }
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
    $dest = Join-Path $global:TEMP_DIR "spotx_run.ps1"; Download-FileSafe -Uri $global:URL_SPOTX -OutFile $dest
    Confirm-FileHash -Path $dest -ExpectedHash $global:PinnedReleases.SpotX.SHA256 -Label "SpotX run.ps1"
    $params = Build-SpotXParams -Config $Config; Write-Log "Params: $params"
    if ($SyncHash) { $SyncHash.AllowSpotify = $true }
    Invoke-ExternalScriptIsolated -FilePath $dest -Arguments $params
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
    if ($SyncHash) { $SyncHash.AllowSpotify = $false }
}

function Module-InstallSpicetifyCLI {
    $ver = $global:PinnedReleases.SpicetifyCLI.Version
    Write-Log "Installing Spicetify CLI v$ver..." -Level 'STEP'
    New-Item -Path $global:SPICETIFY_DIR -ItemType Directory -Force | Out-Null
    $arch = switch ($env:PROCESSOR_ARCHITECTURE) { 'AMD64' {'x64'} 'ARM64' {'arm64'} default {'x32'} }
    $zip = $global:URL_SPICETIFY_FMT -f $ver, $arch
    $zp = Join-Path $global:TEMP_DIR "spicetify.zip"; Download-FileSafe -Uri $zip -OutFile $zp
    $expectedHash = $global:PinnedReleases.SpicetifyCLI.SHA256[$arch]
    Confirm-FileHash -Path $zp -ExpectedHash $expectedHash -Label "Spicetify CLI ($arch)"
    Expand-Archive -Path $zp -DestinationPath $global:SPICETIFY_DIR -Force; Remove-Item $zp -Force
    $sExe = Join-Path $global:SPICETIFY_DIR "spicetify.exe"
    if (-not (Test-Path $sExe)) { throw "spicetify.exe not found after extraction - ZIP may be corrupted" }
    $env:PATH = "$env:PATH;$global:SPICETIFY_DIR"
    $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
    if ($userPath -notlike "*spicetify*") {
        [Environment]::SetEnvironmentVariable('PATH', "$userPath;$global:SPICETIFY_DIR", 'User')
        Write-Log "Added Spicetify to user PATH."
    }
    Write-Log "Generating config..."; & "$global:SPICETIFY_DIR\spicetify.exe" config --bypass-admin | Out-Null
    Write-Log "Spicetify CLI v$ver installed."
}

function Module-InstallThemes { param($Config)
    $tn = $Config.Spicetify_Theme; if ($tn -eq '(None - Marketplace Only)') { Write-Log "No theme selected."; return }
    Write-Log "Installing theme: $tn..." -Level 'STEP'
    $tz=Join-Path $global:TEMP_DIR "themes.zip"; $tu=Join-Path $global:TEMP_DIR "themes-unpack"; $td=Join-Path $global:SPICETIFY_CONFIG_DIR "Themes"
    if (-not (Test-Path $td)) { New-Item -Path $td -ItemType Directory -Force | Out-Null }
    Download-FileSafe -Uri $global:URL_THEMES_REPO -OutFile $tz
    Confirm-FileHash -Path $tz -ExpectedHash $global:PinnedReleases.Themes.SHA256 -Label "Themes archive"
    if (Test-Path $tu) { Remove-Item $tu -Recurse -Force }; Expand-Archive -Path $tz -DestinationPath $tu -Force
    $root = Get-ChildItem $tu -Directory | Select-Object -First 1; $src = Join-Path $root.FullName $tn
    if (Test-Path $src) { $dst=Join-Path $td $tn; if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        Copy-Item $src -Destination $dst -Recurse -Force; Write-Log "Theme copied to $dst"
    } else { Write-Log "Theme '$tn' not in repo." -Level 'WARN' }
    Remove-Item $tz -Force -EA SilentlyContinue; Remove-Item $tu -Recurse -Force -EA SilentlyContinue
    if (-not (Test-Path (Join-Path $td $tn))) { return }
    $sc = $Config.Spicetify_Scheme; Write-Log "Setting theme=$tn, scheme=$sc"
    & "$global:SPICETIFY_DIR\spicetify.exe" config current_theme $tn --bypass-admin
    if ($sc -ne 'Default' -and -not [string]::IsNullOrWhiteSpace($sc)) { & "$global:SPICETIFY_DIR\spicetify.exe" config color_scheme $sc --bypass-admin }
    $needsThemeJs = @("Dribbblish","StarryNight","Turntable") -contains $tn
    $jsVal = if ($needsThemeJs) { "1" } else { "0" }
    & "$global:SPICETIFY_DIR\spicetify.exe" config inject_css 1 replace_colors 1 overwrite_assets 1 inject_theme_js $jsVal --bypass-admin
}

function Module-InstallExtensions { param($Config)
    $exts = $Config.Spicetify_Extensions; if ($exts.Count -eq 0) { Write-Log "No extensions."; return }
    Write-Log "Extensions: $($exts -join ', ')..." -Level 'STEP'
    foreach ($e in $exts) { & "$global:SPICETIFY_DIR\spicetify.exe" config extensions $e --bypass-admin; Write-Log "Enabled: $e" }
}

function Module-InstallMarketplace { param($Config)
    if (-not $Config.Spicetify_Marketplace) { Write-Log "Marketplace skipped."; return }
    Write-Log "Installing Marketplace..." -Level 'STEP'
    $ca=Join-Path $global:SPICETIFY_CONFIG_DIR "CustomApps"; if (-not (Test-Path $ca)) { $ca=Join-Path $global:SPICETIFY_DIR "CustomApps" }
    $md=Join-Path $ca "marketplace"; $mz=Join-Path $global:TEMP_DIR "mp.zip"; $mu=Join-Path $global:TEMP_DIR "mp_unpack"
    if (Test-Path $md) { Remove-Item $md -Recurse -Force -EA SilentlyContinue }; New-Item -Path $md -ItemType Directory -Force | Out-Null
    Download-FileSafe -Uri $global:URL_MARKETPLACE -OutFile $mz
    Confirm-FileHash -Path $mz -ExpectedHash $global:PinnedReleases.Marketplace.SHA256 -Label "Marketplace"
    if (Test-Path $mu) { Remove-Item $mu -Recurse -Force }; Expand-Archive -Path $mz -DestinationPath $mu -Force
    $sp = if (Test-Path (Join-Path $mu "marketplace-dist")) { Join-Path $mu "marketplace-dist\*" } else { Join-Path $mu "*" }
    Copy-Item -Path $sp -Destination $md -Recurse -Force; Remove-Item $mz -Force; Remove-Item $mu -Recurse -Force
    & "$global:SPICETIFY_DIR\spicetify.exe" config custom_apps marketplace --bypass-admin; Write-Log "Marketplace enabled."
}

function Module-ApplySpicetify { param($Config)
    Write-Log "Applying Spicetify (backup + apply)..." -Level 'STEP'
    if ($Config.Spicetify_Theme -eq '(None - Marketplace Only)') {
        & "$global:SPICETIFY_DIR\spicetify.exe" config inject_css 0 replace_colors 0 overwrite_assets 0 inject_theme_js 0 --bypass-admin
    }
    $proc = Start-Process -FilePath "$global:SPICETIFY_DIR\spicetify.exe" -ArgumentList "backup","apply","--bypass-admin" -NoNewWindow -PassThru -Wait
    if ($proc.ExitCode -ne 0) { Write-Log "Apply exited with code $($proc.ExitCode)." -Level 'WARN' }
    else { Write-Log "Spicetify applied successfully." }
    # Do NOT delete Backup folder - needed for spicetify restore
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
        $total = $steps.Count; $n = 0
        foreach ($s in $steps) { $n++
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text = "Step $n of $total : $s"; $sh.ProgressBar.Value = [int]((($n-1)/$total)*100) })
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
        @("spotx_run.ps1","spicetify.zip","themes.zip","mp.zip") | ForEach-Object {
            $tf = Join-Path $global:TEMP_DIR $_; if (Test-Path $tf) { Remove-Item $tf -Force -EA SilentlyContinue }
        }
        @("themes-unpack","mp_unpack") | ForEach-Object {
            $td = Join-Path $global:TEMP_DIR $_; if (Test-Path $td) { Remove-Item $td -Recurse -Force -EA SilentlyContinue }
        }
        Write-Log "Temp files cleaned up."
        if ($cfg.LaunchAfter -and (Test-Path $global:SPOTIFY_EXE_PATH)) { Write-Log "Launching Spotify..." -Level 'SUCCESS'; Start-Process $global:SPOTIFY_EXE_PATH }
        Write-Log "--- Installation Complete ---" -Level 'SUCCESS'; $sh.IsRunning=$false
        $sh.Dispatcher.Invoke([Action]{ $sh.ProgressBar.Value=100; $sh.StatusLabel.Text="Installation Complete"; $sh.StepLabel.Text="Done"; $sh.CloseBtn.Visibility="Visible"; $sh.CopyLogBtn.Visibility="Visible"; if($sh.Timer){$sh.Timer.Stop()}; $sh.Window.Topmost=$false; $sh.Window.Activate() })
    } catch { $sh.IsRunning=$false; $em=$_.Exception.Message; $st=$_.ScriptStackTrace
        $sh.Dispatcher.Invoke([Action]{ if($sh.Timer){$sh.Timer.Stop()}; $sh.LogBlock.Text+="`n[FATAL] $em`n$st"; $sh.StatusLabel.Text="Error"
            $sh.StepLabel.Text="Failed"; $sh.ProgressBar.Foreground=$global:BrushError; $sh.ProgressBar.Value=100; $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Visibility="Visible"; $sh.Window.Topmost=$false; $sh.Window.Activate() })
    }
}

$maintBlock = { param($sh,$action)
    $script:syncHash = $sh
    $ErrorActionPreference = 'Stop'
    try {
        if ($action -eq 'CheckUpdates') {
            Write-Log "--- Dependency Update Check ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Checking APIs..."; $sh.ProgressBar.Value=20 })
            Check-ForUpdates
            Write-Log "--- Check Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'Reapply') {
            Write-Log "--- Reapply After Update ---" -Level 'HEADER'
            if (-not (Test-Path $global:SPOTIFY_EXE_PATH)) { throw "Spotify not found at $global:SPOTIFY_EXE_PATH - install Spotify first" }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Step 1/2: SpotX"; $sh.ProgressBar.Value=25 })
            $dest=Join-Path $global:TEMP_DIR "spotx_run.ps1"; Download-FileSafe -Uri $global:URL_SPOTX -OutFile $dest
            Confirm-FileHash -Path $dest -ExpectedHash $global:PinnedReleases.SpotX.SHA256 -Label "SpotX run.ps1"
            $saved=$null; try { if (Test-Path $global:CONFIG_PATH) { $j=Get-Content $global:CONFIG_PATH -Raw -Encoding UTF8|ConvertFrom-Json; $saved=@{}
                foreach($p in $j.PSObject.Properties){$saved[$p.Name]=$p.Value} } } catch {}
            if ($saved) { $sp=Build-SpotXParams -Config $saved; Write-Log "Using saved config" } else { $sp=Build-SpotXParams -Config $global:EasyDefaults; Write-Log "Using defaults (no saved config)" -Level 'WARN' }
            $sh.AllowSpotify=$true; Invoke-ExternalScriptIsolated -FilePath $dest -Arguments $sp; $sh.AllowSpotify=$false
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Step 2/2: Spicetify"; $sh.ProgressBar.Value=70 })
            $se=Join-Path $global:SPICETIFY_DIR "spicetify.exe"
            if (Test-Path $se) { $pr=Start-Process -FilePath $se -ArgumentList "backup","apply","--bypass-admin" -NoNewWindow -PassThru -Wait
                if ($pr.ExitCode -eq 0) { Write-Log "Spicetify reapplied." } else { Write-Log "Apply code: $($pr.ExitCode)" -Level 'WARN' } }
            Write-Log "--- Reapply Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'RestoreVanilla') {
            Write-Log "--- Restore Vanilla Spotify ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring..."; $sh.ProgressBar.Value=30 })
            $se = Join-Path $global:SPICETIFY_DIR "spicetify.exe"
            if (-not (Test-Path $se)) { throw "Spicetify CLI not found" }
            $pr = Start-Process -FilePath $se -ArgumentList "restore","--bypass-admin" -NoNewWindow -PassThru -Wait
            if ($pr.ExitCode -ne 0) { Write-Log "Restore exited with code $($pr.ExitCode)." -Level 'WARN' }
            else { Write-Log "Vanilla Spotify restored successfully." }
            Write-Log "--- Restore Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'UninstallSpicetify') {
            Write-Log "--- Uninstall Spicetify ---" -Level 'HEADER'
            $se = Join-Path $global:SPICETIFY_DIR "spicetify.exe"
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Step 1/3: Restore"; $sh.ProgressBar.Value=15 })
            if (Test-Path $se) { Start-Process -FilePath $se -ArgumentList "restore","--bypass-admin" -NoNewWindow -Wait -EA SilentlyContinue; Write-Log "Spicetify mods restored." }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Step 2/3: Remove files"; $sh.ProgressBar.Value=45 })
            Remove-Item $global:SPICETIFY_CONFIG_DIR -Recurse -Force -EA SilentlyContinue; Write-Log "Removed config dir."
            Remove-Item $global:SPICETIFY_DIR -Recurse -Force -EA SilentlyContinue; Write-Log "Removed CLI dir."
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Step 3/3: Clean PATH"; $sh.ProgressBar.Value=75 })
            $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
            if ($userPath -like "*spicetify*") {
                $cleaned = ($userPath -split ';' | Where-Object { $_ -notlike '*spicetify*' }) -join ';'
                [Environment]::SetEnvironmentVariable('PATH', $cleaned, 'User')
                Write-Log "Removed Spicetify from user PATH."
            }
            Write-Log "--- Uninstall Complete ---" -Level 'SUCCESS'
        } elseif ($action -eq 'FullReset') {
            Write-Log "--- Full Reset ---" -Level 'HEADER'
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Restoring..."; $sh.ProgressBar.Value=10 })
            $se=Join-Path $global:SPICETIFY_DIR "spicetify.exe"
            if (Test-Path $se) { Start-Process -FilePath $se -ArgumentList "restore","--bypass-admin" -NoNewWindow -Wait -EA SilentlyContinue; Write-Log "Spicetify restored." }
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Removing Spicetify..."; $sh.ProgressBar.Value=30 })
            Remove-Item $global:SPICETIFY_CONFIG_DIR -Recurse -Force -EA SilentlyContinue; Remove-Item $global:SPICETIFY_DIR -Recurse -Force -EA SilentlyContinue
            $sh.Dispatcher.Invoke([Action]{ $sh.StepLabel.Text="Cleanup..."; $sh.ProgressBar.Value=50 }); Module-NukeSpotify
            $userPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
            if ($userPath -like "*spicetify*") { $cleaned = ($userPath -split ';' | Where-Object { $_ -notlike '*spicetify*' }) -join ';'; [Environment]::SetEnvironmentVariable('PATH', $cleaned, 'User'); Write-Log "Removed Spicetify from user PATH." }
            Write-Log "--- Full Reset Complete ---" -Level 'SUCCESS'
        }
        $sh.IsRunning=$false
        $sh.Dispatcher.Invoke([Action]{ $sh.ProgressBar.Value=100; $sh.StatusLabel.Text="Complete"; $sh.StepLabel.Text="Done"
            $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Visibility="Visible"; if($sh.Timer){$sh.Timer.Stop()}; $sh.Window.Topmost=$false; $sh.Window.Activate() })
    } catch { $sh.IsRunning=$false; $em=$_.Exception.Message; $st=$_.ScriptStackTrace
        $sh.Dispatcher.Invoke([Action]{ if($sh.Timer){$sh.Timer.Stop()}; $sh.LogBlock.Text+="`n[FATAL] $em`n$st"; $sh.StatusLabel.Text="Error"
            $sh.ProgressBar.Foreground=$global:BrushError; $sh.ProgressBar.Value=100; $sh.CloseBtn.Visibility="Visible"; $sh.BackBtn.Visibility="Visible"; $sh.CopyLogBtn.Visibility="Visible"; $sh.Window.Topmost=$false; $sh.Window.Activate() })
    }
}

# =============================================================================
# 17. RUNSPACE INFRASTRUCTURE
# =============================================================================
$functionNamesForWorker = @(
    'Update-UI','Write-Log','Download-FileSafe','Confirm-FileHash','Hide-SpotifyWindows','Invoke-ExternalScriptIsolated','Test-NetworkReady','Check-ForUpdates',
    'Stop-SpotifyProcesses','Unlock-SpotifyUpdateFolder','Get-DesktopPath','Remove-PathSafely',
    'Module-NukeSpotify','Module-InstallSpotX','Module-InstallSpicetifyCLI',
    'Module-InstallThemes','Module-InstallExtensions',
    'Module-InstallMarketplace','Module-ApplySpicetify',
    'Build-SpotXParams','Load-LibreSpotConfig'
)

$issMain = [System.Management.Automation.Runspaces.InitialSessionState]::CreateDefault()
foreach ($fname in $functionNamesForWorker) {
    $cmd = Get-Command -Name $fname -CommandType Function -ErrorAction Stop
    $entry = New-Object System.Management.Automation.Runspaces.SessionStateFunctionEntry($cmd.Name, $cmd.Definition)
    $null = $issMain.Commands.Add($entry)
}

$varNamesForWorker = @(
    'URL_SPOTX','URL_MARKETPLACE','URL_THEMES_REPO','URL_SPICETIFY_FMT','PinnedReleases',
    'TEMP_DIR','SPOTIFY_EXE_PATH','SPICETIFY_DIR','SPICETIFY_CONFIG_DIR',
    'BACKUP_ROOT','CONFIG_DIR','CONFIG_PATH',
    'BrushGreen','BrushRed','BrushMuted','BrushError',
    'EasyDefaults','VERSION'
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
    return [hashtable]::Synchronized(@{
        Dispatcher=$window.Dispatcher; LogBlock=$ui['LogOutput']; Scroller=$ui['LogScroller']
        StatusLabel=$ui['StatusText']; StepLabel=$ui['StepIndicator']; ProgressBar=$ui['MainProgress']
        CloseBtn=$ui['CloseBtn']; BackBtn=$ui['BtnBackToConfig']; CopyLogBtn=$ui['BtnCopyLog']; Timer=$timer
        Window=$window
        IsRunning=$true; AllowSpotify=$false; Errors=[System.Collections.Generic.List[string]]::new()
    })
}

function Start-InstallJob { param($Config)
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
}

function Start-MaintenanceJob { param([string]$Action)
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
}

# =============================================================================
# 19. LAUNCH
# =============================================================================
$null = $window.ShowDialog()
