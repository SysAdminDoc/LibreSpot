# LibreSpot

**LibreSpot** is a sophisticated automation tool. It streamlines the process of installing a fully customized, ad-free, and themed Spotify client by orchestrating **SpotX**, **Spicetify**, and the **Comfy** theme into a single, one-click execution.

https://github.com/user-attachments/assets/0871c026-8569-4913-b2e0-dd3ae5e7e4f3

![2025-03-23 06 29 41](https://github.com/user-attachments/assets/91a14a21-4080-464a-a91d-f19b9295ba34)

## 🚀 Features

  * **Modern GUI:** A clean, dark-themed interface built with WPF/XAML featuring live logging, progress tracking, and valid clickable credits.
  * **Total Automation:** Handles the entire chain from uninstallation to final patching without user intervention.
  * **Smart Process Management:** Includes a background "Watcher" thread that ensures Spotify processes do not interfere with the installation.
  * **Ad-Blocking:** Integrates [SpotX](https://github.com/SpotX-Official/SpotX) to remove banner, video, and audio ads.
  * **Theming:** Automatically installs [Spicetify CLI](https://github.com/spicetify/cli), the [Marketplace](https://github.com/spicetify/marketplace), and the [Comfy](https://github.com/Comfy-Themes/Spicetify) theme.
  * **CSS Injection:** Injects custom CSS for glassmorphism effects and UI tweaks specific to the Comfy theme.

## 📋 Prerequisites

  * **OS:** Windows 10 or Windows 11.
  * **PowerShell:** Version 5.1 or newer.
  * **Internet Connection:** Required to download the latest components from GitHub.

## 🛠️ Usage

1.  Download the `LibreSpot.ps1` script.
2.  Right-click the file and select **Run with PowerShell**.
3.  **Administrator Privileges:** The script will automatically request administrative privileges if not already running as admin.
4.  Sit back and wait. The GUI will close automatically upon completion and launch your new Spotify client.

## ⚙️ How It Works

1.  **Elevation Check:** Self-elevates to Administrator to ensure write access to Program Files and System directories.
2.  **Cleanup:** Force kills Spotify and utilizes the `amd64fox/Uninstall-Spotify` script to remove existing installations.
3.  **SpotX Installation:** Downloads and runs the SpotX installer with the following flags:
      * `-confirm_uninstall_ms_spoti`
      * `-podcasts_off`
      * `-block_update_on`
      * `-new_theme`
4.  **Config Generation:** Temporarily launches the new Spotify client to generate the necessary `prefs` files, then kills it.
5.  **Spicetify Setup:** Installs the Spicetify CLI and the Spicetify Marketplace.
6.  **Theme Injection:**
      * Downloads the **Comfy** theme.
      * Injects custom CSS blobs (defined within the script) into `xpui\user.css` and `Themes\Comfy\user.css`.
      * Applies the config via Spicetify.

## 🔗 Credits & Resources

This project is an automation wrapper that relies on the hard work of the following open-source projects:

  * **SpotX:** [github.com/SpotX-Official/SpotX](https://github.com/SpotX-Official/SpotX)
  * **Spicetify CLI:** [github.com/spicetify](https://github.com/spicetify)
  * **Comfy Theme:** [github.com/Comfy-Themes/Spicetify](https://github.com/Comfy-Themes/Spicetify)
  * **Spotify Uninstaller:** [github.com/amd64fox/Uninstall-Spotify](https://github.com/amd64fox/Uninstall-Spotify)

## ⚠️ Disclaimer

This tool modifies the Spotify client. While widely used, client modification is technically against Spotify's Terms of Service. Use this software at your own risk. The creators of this script and incorporated scripts are not responsible for banned accounts or software instability.
