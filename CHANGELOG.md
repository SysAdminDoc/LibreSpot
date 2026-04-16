# Changelog

All notable changes to LibreSpot will be documented in this file.

## [v3.2.0] - 2026-04-15

### Added
- Robust self-elevation that handles .ps1, .exe, and inline scriptblock launch contexts
- Config normalization with type-safe boolean/int parsing and corrupt config quarantine
- Custom themed dark dialogs replacing native MessageBox throughout the app
- Safe file removal system with blocklist protection against accidental deletion of system directories
- Spicetify backup/restore with staged copy and automatic rollback on failure
- Streaming process output capture for real-time SpotX log display
- Unsaved changes detection with config fingerprinting and close-window guard
- Comprehensive 8-phase Spotify uninstaller (processes, Store app, native uninstaller, filesystem, registry, scheduled tasks, firewall rules, verification)
- Centralized Spicetify CLI wrapper with consistent error handling
- Declarative extension/custom-app sync that preserves user-installed items
- PATH management utilities for clean Spicetify install/uninstall
- Per-maintenance-action context messages and completion summaries
- Dialog icon branding for the main window and themed dialogs
- Icon assets (icon.ico, icon.png, icon.svg, banner.png, multi-size icons/)

### Changed
- Rewrote install and maintenance flows for resilience (near-complete script rewrite)
- All maintenance actions now use themed confirmation dialogs with descriptive context
- Install page shows per-step labels and contextual descriptions
- Runspace infrastructure uses explicit ISS function/variable exports instead of dot-sourcing

### Fixed
- Maintenance buttons now disable correctly based on what is actually installed
- Config save uses atomic write-then-replace to prevent corruption on crash
- Close-window handler warns about in-progress setup or unsaved custom changes

## [v3.1.1] - 2026-03-27

- Fixed theme preview crash: use synchronous download
- Fixed theme preview: TLS 1.2 + ThreadPool instead of WebClient async
- Reverted custom apps/packs, kept theme preview
- Removed Statistics and Lyrics Plus custom apps (broken)

## [v3.1.0] - 2026-03-27

- Audit fixes: anti-hang, apply recovery, new options
- Fixed blank screen: let SpotX manage Spotify version compatibility
- Added live theme preview with async image loading

## [v3.0.6] - 2026-03-27

- Updated SpotX to 6070bbcf to fix blank screen on Spotify 1.2.85.519
- Compiled v3.0.6 executable
