# 🎵 LibreSpot v2.0  
A modern, GUI-powered Spotify customization suite featuring **SpotX**, **Spicetify**, **Marketplace**, and the **Comfy Theme**.  

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
  <pre><code>irm "https://tinyurl.com/librespot" | iex</code></pre>
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

