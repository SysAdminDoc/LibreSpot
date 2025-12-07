# 🎵 LibreSpot v2.0  
A modern, GUI-powered Spotify customization suite featuring **SpotX**, **Spicetify**, **Marketplace**, and the **Comfy Theme**.  
Rebuilt from the ground up into a unified installer with an elegant WPF interface, multi-threaded backend, and full EXE support.

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

## ⭐ Features at a Glance
### ✨ Beautiful WPF User Interface
- Fully themed Slate + Neon Green Maven design  
- Step indicators with animation  
- Real-time log viewer  
- Progress bar and status readouts  
- Hyperlink credits for SpotX, Spicetify, and Comfy

---

### 🔧 Hybrid Engine (EXE + PS1)
LibreSpot v2.0 automatically adapts itself depending on how it’s launched:

- **EXE Mode**  
  Compiled, packaged, no console window, ideal for end-users.

- **Script Mode**  
  Run directly from source for development or troubleshooting.

Both modes contain the same self-elevating engine.

---

### 🛠️ Full Spotify Customization Pipeline  
LibreSpot executes a structured 6-step installer:

#### **1. Cleanup Engine**
- Kills Spotify processes  
- Runs official Uninstall-Spotify script  

#### **2. SpotX Installer (Isolated Sandbox)**
- Downloads SpotX with fallback logic  
- Executes inside an isolated PowerShell session for safety  

#### **3. Prefs Generator**
- Launches and auto-closes Spotify to force config creation  

#### **4. Spicetify CLI Installer**
- Pulls latest release from GitHub API  
- Installs & configures CLI from scratch  

#### **5. Marketplace Installer**
- Installs Marketplace from latest GitHub release  
- Enables the Custom App automatically  

#### **6. Comfy Theme + CSS Injection**
- Downloads theme components  
- Injects XPUI and Comfy CSS patches  
- Applies full Spicetify theme configuration  

---

### 🧵 Multi-Threaded Architecture
- Background “Watcher” thread gracefully handles Spotify process control  
- Main installation thread keeps UI responsive  
- No more freezing, blocking, or console delays  

---

### 🔒 Robust Error Handling
- Full try/catch wrapping  
- Fatal errors displayed in GUI  
- Smart fallbacks for network/download failures  
- Clean shutdown signaling  

---

## 📦 Changelog Summary (v1.0 → v2.0)
LibreSpot evolved from:

- A plain `.bat` file  
- ASCII art output  
- Sequential console output  
- Manual SpotX behavior  
- No GUI  
- No Spicetify automation  

To a complete:

- **WPF Desktop App**
- **Compiled EXE**
- **Multi-threaded engine**
- **Automatic installers**
- **Dynamic GitHub release fetching**
- **CSS + theme injectors**
- **One-line remote launcher**

Read the full changelog inside the `Releases` tab for details.

---

## ❤️ Credits
LibreSpot integrates with the incredible work from:

- **SpotX** – https://github.com/SpotX-Official/SpotX  
- **Spicetify CLI** – https://github.com/spicetify/cli  
- **Spicetify Marketplace** – https://github.com/spicetify/marketplace  
- **Comfy Theme** – https://github.com/Comfy-Themes/Spicetify  

---

## 📜 License
LibreSpot does not modify copyrighted assets directly.  
SpotX, Spicetify, and Comfy remain under their respective licenses.

---

## 🧩 Contributions
PRs, feature ideas, and bug reports are welcome.  
Feel free to open issues or request enhancements.

