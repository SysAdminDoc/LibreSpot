# 🎵 LibreSpot v2.0  
A modern, GUI-powered Spotify customization suite featuring **SpotX**, **Spicetify**, **Marketplace**, and the **Comfy Theme**.  

<img width="900" height="611" alt="image" src="https://github.com/user-attachments/assets/657170ac-4ea7-49c1-879f-00c17049bb35" />

## Theme

[Comfy is a theme engine](https://github.com/Comfy-Themes/Spicetify) for Spotify, providing a collection of modern UI themes.  
The default theme applied is **Catppuccin**, which looks like this:

<img width="1714" height="846" alt="image" src="https://github.com/user-attachments/assets/40cef409-bad3-4ede-8347-a17a696ed091" />

Here are the other available [color themes](https://github.com/Comfy-Themes/Spicetify/tree/main/Comfy):

* Catppuccin  
* Rosé Pine  
* Mono  
* Individual  
* Comfy  
* Spotify  
* Nord  
* Everforest  
* Kanagawa  
* Houjicha  
* Kitty  
* Lunar  
* Deep  
* Velvet  
* Yami  
* Hikari

---

## ⚠️ Requirements
- **Must be run as Administrator**  
  Both the EXE and PS1 versions require elevation to properly uninstall Spotify, install SpotX, apply Spicetify themes, and write system-level changes.

- **Windows 10 / 11 recommended**  
- **Internet access** (for GitHub API, theme downloads, and installers)

---

## 🚀 Quick Install (PowerShell One-Liner)

Paste this into an elevated PowerShell window:

<div class="position-relative">
  <pre><code>irm "https://tinyurl.com/librespotbasic" | iex</code></pre>
</div>

This launches LibreSpot directly without downloading the repository.

---

## 🖥️ Demo
*(Example from v2.0 WPF Interface)*

https://github.com/user-attachments/assets/673b5f9a-7741-4d1e-929d-12102cf32635

---

## Features

### Core
- Installs **SpotX**
- Installs **Spicetify CLI**
- Installs **Spicetify Marketplace**
- Applies **Comfy theme**
- Injects XPUI + Comfy CSS patches
- Resets backups and cleans previous installs

### Engine
- Full **WPF GUI** (progress bar, log window, step indicator)
- **Multi-threaded** (UI + background watcher)
- **EXE or PS1** compatible (auto elevation)
- GitHub API release fetching for latest versions
- Error handling with on-screen reporting

---

## Downloads

- **EXE (recommended):**  
  https://github.com/SysAdminDoc/LibreSpot/releases/latest/download/LibreSpot.exe

- **Source Script:**  
  https://github.com/SysAdminDoc/LibreSpot/releases/latest/download/LibreSpot.ps1

---

## Compile LibreSpot Yourself (Optional)

If you prefer to build your own EXE, place this script in the **same folder** as the `LibreSpot.ps1` file and run it.  
It automatically installs PS2EXE, detects powershell scripts in the same directory, and compiles them into EXE with a gear icon.

```powershell
<#
.SYNOPSIS
    Batch compiles all PS1 files in the current directory to EXEs.
    Automatically installs PS2EXE and applies a Gear icon.
#>

$ErrorActionPreference = 'Stop'

Write-Host "Checking for PS2EXE module..." -ForegroundColor Cyan

if (-not (Get-Module -ListAvailable -Name "ps2exe")) {
    Write-Host "Module not found. Installing PS2EXE..." -ForegroundColor Yellow
    Install-Module -Name "ps2exe" -Scope CurrentUser -Force -SkipPublisherCheck
    Import-Module "ps2exe"
}
else {
    Write-Host "PS2EXE is already available." -ForegroundColor Green
}

$iconUrl  = "https://raw.githubusercontent.com/SysAdminDoc/LibreSpot/refs/heads/main/Images/Settings.ico"
$iconPath = "$env:TEMP\temp_gear_icon.ico"

try {
    Invoke-WebRequest -Uri $iconUrl -OutFile $iconPath -UseBasicParsing
}
catch {
    $iconPath = $null
}

$currentDir = $PSScriptRoot
if (-not $currentDir) { $currentDir = Get-Location }

$scripts = Get-ChildItem -Path $currentDir -Filter "*.ps1" |
           Where-Object { $_.Name -ne $MyInvocation.MyCommand.Name }

foreach ($script in $scripts) {
    Write-Host "`nProcessing: $($script.Name)" -ForegroundColor Magenta
    $outName = "$($script.BaseName).exe"

    $content = Get-Content -Path $script.FullName -Raw
    $isGUI = ($content -match "PresentationFramework" -or $content -match "System.Windows.Forms" -or $content -match "WPF")

    $params = @{
        InputFile  = $script.FullName
        OutputFile = $outName
        Title      = $script.BaseName
        Icon       = $iconPath
    }

    if ($isGUI) { $params.Add('noConsole', $true) }

    try {
        Invoke-PS2EXE @params | Out-Null
        Write-Host "  -> Success! Created $outName" -ForegroundColor Green
    }
    catch {
        Write-Host "  -> Failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

if (Test-Path $iconPath) { Remove-Item $iconPath -Force }
Write-Host "`nAll operations complete." -ForegroundColor Cyan
```

---

## Requirements
- Windows 10 or 11  
- Must be run **as Administrator**

---

## Notes
- EXE was compiled directly from the PS1 via PS2EXE  
- Project integrates with:
  - SpotX  
  - Spicetify CLI  
  - Spicetify Marketplace  
  - Comfy Themes  

---

## ❤️ Credits
LibreSpot integrates with the incredible work from:

- **SpotX** – https://github.com/SpotX-Official/SpotX  
- **Spicetify CLI** – https://github.com/spicetify/cli  
- **Spicetify Marketplace** – https://github.com/spicetify/marketplace  
- **Comfy Theme** – https://github.com/Comfy-Themes/Spicetify  

---

## License
All third-party components retain their original licenses. This repo distributes only automation logic and user-applied configuration.

---

## 🧩 Contributions
PRs, feature ideas, and bug reports are welcome.  
Feel free to open issues or request enhancements.

