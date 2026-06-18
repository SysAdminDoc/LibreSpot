using System.IO;
using System.Diagnostics;
using System.Text.Json;
using LibreSpot.Desktop.Models;

namespace LibreSpot.Desktop.Services;

public sealed class EnvironmentSnapshotService
{
    private readonly Func<bool> _autoReapplyTaskProbe;
    private readonly string _spotifyPath;
    private readonly string _spicetifyPath;
    private readonly string _spicetifyConfigDirectory;
    private readonly string _backupDirectory;
    private readonly string _rollingLogDirectory;
    private readonly string _crashDirectory;
    private readonly Func<string?> _spotifyVersionProbe;
    private readonly Func<string?> _spicetifyVersionProbe;
    private readonly Func<bool> _spotifyRunningProbe;

    public EnvironmentSnapshotService(
        Func<bool>? autoReapplyTaskProbe = null,
        string? spotifyPath = null,
        string? spicetifyPath = null,
        string? spicetifyConfigDirectory = null,
        string? backupDirectory = null,
        string? rollingLogDirectory = null,
        string? crashDirectory = null,
        Func<string?>? spotifyVersionProbe = null,
        Func<string?>? spicetifyVersionProbe = null,
        Func<bool>? spotifyRunningProbe = null)
    {
        _autoReapplyTaskProbe = autoReapplyTaskProbe ?? IsAutoReapplyTaskRegistered;
        _spotifyPath = string.IsNullOrWhiteSpace(spotifyPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe")
            : Path.GetFullPath(spotifyPath);
        _spicetifyPath = string.IsNullOrWhiteSpace(spicetifyPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "spicetify", "spicetify.exe")
            : Path.GetFullPath(spicetifyPath);
        _spicetifyConfigDirectory = string.IsNullOrWhiteSpace(spicetifyConfigDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "spicetify")
            : Path.GetFullPath(spicetifyConfigDirectory);
        _backupDirectory = string.IsNullOrWhiteSpace(backupDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "LibreSpot_Backups")
            : Path.GetFullPath(backupDirectory);
        _rollingLogDirectory = string.IsNullOrWhiteSpace(rollingLogDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibreSpot", "logs")
            : Path.GetFullPath(rollingLogDirectory);
        _crashDirectory = string.IsNullOrWhiteSpace(crashDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LibreSpot", "crashes")
            : Path.GetFullPath(crashDirectory);
        _spotifyVersionProbe = spotifyVersionProbe ?? (() => GetFileVersion(_spotifyPath));
        _spicetifyVersionProbe = spicetifyVersionProbe ?? (() => GetFileVersion(_spicetifyPath));
        _spotifyRunningProbe = spotifyRunningProbe ?? IsSpotifyRunning;
    }

    // Snapshot probing touches the filesystem and shells out to schtasks.exe
    // (up to 1500ms). Callers on the UI thread must use this async variant so
    // the dispatcher is never blocked; the work runs on the thread pool and the
    // caller's await resumes on the original (UI) context to publish results.
    public Task<EnvironmentSnapshot> GetSnapshotAsync(string configPath) =>
        Task.Run(() => GetSnapshot(configPath));

    public EnvironmentSnapshot GetSnapshot(string configPath)
    {
        var configDirectory = ResolveConfigDirectory(configPath);
        var spicetifyConfigPath = Path.Combine(_spicetifyConfigDirectory, "config-xpui.ini");
        var spicetifyConfig = ReadSpicetifyConfigEntries(spicetifyConfigPath);
        var marketplaceDirectory = Path.Combine(_spicetifyConfigDirectory, "CustomApps", "marketplace");
        var marketplaceFilesPresent =
            File.Exists(Path.Combine(marketplaceDirectory, "extension.js")) &&
            File.Exists(Path.Combine(marketplaceDirectory, "manifest.json"));
        var marketplaceRegistered = IsSpicetifyListEntryEnabled(
            spicetifyConfig,
            "custom_apps",
            "marketplace");
        var spotifyInstalled = File.Exists(_spotifyPath);
        var spicetifyInstalled = File.Exists(_spicetifyPath);
        var savedConfigExists = !string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath);
        var configFolderExists = Directory.Exists(configDirectory);
        var autoReapplyTaskRegistered = _autoReapplyTaskProbe();
        var healthReport = BuildHealthReport(
            configDirectory,
            configPath,
            spicetifyConfigPath,
            spicetifyConfig,
            marketplaceDirectory,
            marketplaceFilesPresent,
            marketplaceRegistered,
            spotifyInstalled,
            spicetifyInstalled,
            savedConfigExists,
            configFolderExists,
            autoReapplyTaskRegistered);

        return new EnvironmentSnapshot
        {
            SpotifyInstalled = spotifyInstalled,
            SpicetifyInstalled = spicetifyInstalled,
            MarketplaceFilesPresent = marketplaceFilesPresent,
            MarketplaceRegistered = marketplaceRegistered,
            SavedConfigExists = savedConfigExists,
            ConfigFolderExists = configFolderExists,
            AutoReapplyTaskRegistered = autoReapplyTaskRegistered,
            HealthReport = healthReport,
            HostArchitecture = GetHostArchitecture(),
            ProcessArchitecture = GetProcessArchitecture()
        };
    }

    private StackHealthReport BuildHealthReport(
        string configDirectory,
        string configPath,
        string spicetifyConfigPath,
        IReadOnlyDictionary<string, string> spicetifyConfig,
        string marketplaceDirectory,
        bool marketplaceFilesPresent,
        bool marketplaceRegistered,
        bool spotifyInstalled,
        bool spicetifyInstalled,
        bool savedConfigExists,
        bool configFolderExists,
        bool autoReapplyTaskRegistered)
    {
        var watcherStatePath = Path.Combine(configDirectory, "watcher-state.json");
        var watcherState = ReadWatcherState(watcherStatePath);
        var components = new List<StackHealthComponent>
        {
            BuildSpotifyComponent(spotifyInstalled),
            BuildSpotXComponent(spotifyInstalled),
            BuildSpicetifyCliComponent(spicetifyInstalled),
            BuildSpicetifyConfigComponent(spicetifyInstalled, spicetifyConfigPath, spicetifyConfig),
            BuildMarketplaceComponent(spicetifyInstalled, marketplaceDirectory, marketplaceFilesPresent, marketplaceRegistered),
            BuildThemeComponent(spicetifyInstalled, spicetifyConfigPath, spicetifyConfig),
            BuildBackupComponent(spicetifyInstalled),
            BuildWatcherComponent(watcherStatePath, watcherState, autoReapplyTaskRegistered),
            BuildPostUpdateTriageComponent(
                watcherStatePath,
                watcherState,
                spotifyInstalled,
                spicetifyInstalled,
                autoReapplyTaskRegistered,
                marketplaceFilesPresent,
                marketplaceRegistered),
            BuildExtensionFileIntegrityComponent(spicetifyInstalled, spicetifyConfig),
            BuildLogsComponent(configDirectory),
            BuildCrashComponent(),
            BuildSavedProfileComponent(configPath, savedConfigExists, configFolderExists)
        };

        return new StackHealthReport(components);
    }

    private StackHealthComponent BuildSpotifyComponent(bool installed)
    {
        if (!installed)
        {
            return Component(
                "spotify",
                "Spotify",
                "Not installed",
                HealthSeverity.Info,
                null,
                _spotifyPath,
                null,
                "Spotify.exe was not found in the expected per-user install path.",
                "Install");
        }

        return Component(
            "spotify",
            "Spotify",
            "Detected",
            HealthSeverity.Ready,
            _spotifyVersionProbe(),
            _spotifyPath,
            GetLastChanged(_spotifyPath),
            "Spotify.exe exists and can be used as the patch target.");
    }

    private StackHealthComponent BuildSpotXComponent(bool spotifyInstalled)
    {
        var spotifyDirectory = Path.GetDirectoryName(_spotifyPath) ?? string.Empty;
        var appsDirectory = Path.Combine(spotifyDirectory, "Apps");
        var bundlePath = Path.Combine(appsDirectory, "xpui.spa");
        var backupPath = Path.Combine(appsDirectory, "xpui.spa.bak");
        var hasBundle = File.Exists(bundlePath);
        var hasBackup = File.Exists(backupPath);

        if (!spotifyInstalled)
        {
            return Component(
                "spotx",
                "SpotX patch",
                "Not checked",
                HealthSeverity.Info,
                null,
                appsDirectory,
                null,
                "SpotX patch markers are only meaningful after Spotify is installed.",
                "Install");
        }

        if (hasBundle && hasBackup)
        {
            return Component(
                "spotx",
                "SpotX patch",
                "Verified",
                HealthSeverity.Ready,
                null,
                appsDirectory,
                Max(GetLastChanged(bundlePath), GetLastChanged(backupPath)),
                "Apps\\xpui.spa and Apps\\xpui.spa.bak are both present, matching SpotX's successful patch markers.");
        }

        if (hasBundle)
        {
            return Component(
                "spotx",
                "SpotX patch",
                "Unverified",
                HealthSeverity.Warning,
                null,
                appsDirectory,
                GetLastChanged(bundlePath),
                "Spotify's app bundle exists, but the SpotX backup marker is missing.",
                "Reapply");
        }

        return Component(
            "spotx",
            "SpotX patch",
            "Bundle missing",
            HealthSeverity.Critical,
            null,
            appsDirectory,
            null,
            "Apps\\xpui.spa is missing, so patch state cannot be considered reliable.",
            "Reapply",
            "FullReset");
    }

    private StackHealthComponent BuildSpicetifyCliComponent(bool installed)
    {
        if (!installed)
        {
            return Component(
                "spicetify-cli",
                "Spicetify CLI",
                "Not installed",
                HealthSeverity.Info,
                null,
                _spicetifyPath,
                null,
                "spicetify.exe was not found in the expected per-user location.",
                "Install");
        }

        return Component(
            "spicetify-cli",
            "Spicetify CLI",
            "Detected",
            HealthSeverity.Ready,
            _spicetifyVersionProbe(),
            _spicetifyPath,
            GetLastChanged(_spicetifyPath),
            "spicetify.exe exists and maintenance actions can call it.");
    }

    private static StackHealthComponent BuildSpicetifyConfigComponent(
        bool spicetifyInstalled,
        string configPath,
        IReadOnlyDictionary<string, string> config)
    {
        if (!spicetifyInstalled)
        {
            return Component(
                "spicetify-config",
                "Spicetify config",
                "Not available",
                HealthSeverity.Info,
                null,
                configPath,
                null,
                "Spicetify config is created after the CLI is installed.",
                "Install");
        }

        if (!File.Exists(configPath))
        {
            return Component(
                "spicetify-config",
                "Spicetify config",
                "Missing",
                HealthSeverity.Warning,
                null,
                configPath,
                null,
                "config-xpui.ini is missing, so saved theme and extension state cannot be inspected.",
                "Reapply");
        }

        var currentTheme = config.TryGetValue("current_theme", out var theme) ? theme : string.Empty;
        var customApps = config.TryGetValue("custom_apps", out var apps) ? apps : string.Empty;
        var evidence = string.IsNullOrWhiteSpace(currentTheme)
            ? "config-xpui.ini exists; current_theme is not set."
            : $"config-xpui.ini exists; current_theme={currentTheme}.";
        if (!string.IsNullOrWhiteSpace(customApps))
        {
            evidence += $" custom_apps={customApps}.";
        }

        return Component(
            "spicetify-config",
            "Spicetify config",
            "Readable",
            HealthSeverity.Ready,
            null,
            configPath,
            GetLastChanged(configPath),
            evidence);
    }

    private static StackHealthComponent BuildMarketplaceComponent(
        bool spicetifyInstalled,
        string marketplaceDirectory,
        bool filesPresent,
        bool registered)
    {
        if (!spicetifyInstalled)
        {
            return Component(
                "marketplace",
                "Marketplace",
                "Not available",
                HealthSeverity.Info,
                null,
                marketplaceDirectory,
                null,
                "Marketplace is installed through Spicetify.",
                "Install");
        }

        if (filesPresent && registered)
        {
            return Component(
                "marketplace",
                "Marketplace",
                "Ready",
                HealthSeverity.Ready,
                null,
                marketplaceDirectory,
                GetNewestFileChange(marketplaceDirectory),
                "Marketplace files are present and custom_apps includes marketplace.");
        }

        if (filesPresent)
        {
            return Component(
                "marketplace",
                "Marketplace",
                "Hidden",
                HealthSeverity.Warning,
                null,
                marketplaceDirectory,
                GetNewestFileChange(marketplaceDirectory),
                "Marketplace files exist, but custom_apps does not include marketplace.",
                "RepairMarketplace");
        }

        if (registered)
        {
            return Component(
                "marketplace",
                "Marketplace",
                "Files missing",
                HealthSeverity.Warning,
                null,
                marketplaceDirectory,
                null,
                "custom_apps includes marketplace, but required files were not found.",
                "RepairMarketplace");
        }

        return Component(
            "marketplace",
            "Marketplace",
            "Not enabled",
            HealthSeverity.Warning,
            null,
            marketplaceDirectory,
            null,
            "Marketplace is not registered and required files were not found.",
            "RepairMarketplace");
    }

    private static StackHealthComponent BuildThemeComponent(
        bool spicetifyInstalled,
        string configPath,
        IReadOnlyDictionary<string, string> config)
    {
        if (!spicetifyInstalled || !File.Exists(configPath))
        {
            return Component(
                "active-theme",
                "Active theme",
                "Not available",
                HealthSeverity.Info,
                null,
                configPath,
                null,
                "Theme state is available after Spicetify writes config-xpui.ini.",
                "Install");
        }

        var theme = config.TryGetValue("current_theme", out var currentTheme) ? currentTheme : string.Empty;
        var injectCss = config.TryGetValue("inject_css", out var injectCssValue) && IsEnabledValue(injectCssValue);
        var replaceColors = config.TryGetValue("replace_colors", out var replaceColorsValue) && IsEnabledValue(replaceColorsValue);

        if (string.IsNullOrWhiteSpace(theme) || string.Equals(theme, "SpicetifyDefault", StringComparison.OrdinalIgnoreCase))
        {
            return Component(
                "active-theme",
                "Active theme",
                "Marketplace or stock",
                HealthSeverity.Ready,
                null,
                configPath,
                GetLastChanged(configPath),
                "No bundled theme is active; Marketplace-only and stock looks are valid states.");
        }

        if (!injectCss || !replaceColors)
        {
            return Component(
                "active-theme",
                "Active theme",
                "Injection disabled",
                HealthSeverity.Warning,
                null,
                configPath,
                GetLastChanged(configPath),
                $"Theme '{theme}' is selected, but inject_css or replace_colors is disabled.",
                "Reapply",
                "SafeMode");
        }

        return Component(
            "active-theme",
            "Active theme",
            "Active",
            HealthSeverity.Ready,
            null,
            configPath,
            GetLastChanged(configPath),
            $"Theme '{theme}' is selected with CSS and color replacement enabled.");
    }

    private StackHealthComponent BuildBackupComponent(bool spicetifyInstalled)
    {
        var backupCount = CountDirectories(_backupDirectory);
        if (backupCount > 0)
        {
            return Component(
                "backups",
                "Backups",
                backupCount == 1 ? "1 backup" : $"{backupCount} backups",
                HealthSeverity.Ready,
                null,
                _backupDirectory,
                GetNewestDirectoryChange(_backupDirectory),
                "At least one LibreSpot backup directory is available for restore.");
        }

        return Component(
            "backups",
            "Backups",
            "None yet",
            spicetifyInstalled ? HealthSeverity.Warning : HealthSeverity.Info,
            null,
            _backupDirectory,
            null,
            spicetifyInstalled
                ? "Spicetify is installed, but no LibreSpot backup directory is available."
                : "Backups are created after Spicetify has something to save.",
            spicetifyInstalled ? "CreateBackup" : "Install");
    }

    private static StackHealthComponent BuildWatcherComponent(string statePath, WatcherState state, bool taskRegistered)
    {
        if (!taskRegistered)
        {
            return Component(
                "auto-reapply-watcher",
                "Auto-reapply watcher",
                "Disabled",
                HealthSeverity.Info,
                state.LastKnownVersion,
                statePath,
                state.LastRunAt,
                "The watcher task is not registered; auto-reapply is opt-in.",
                "EnableAutoReapply");
        }

        if (state.LastRunAt is null)
        {
            return Component(
                "auto-reapply-watcher",
                "Auto-reapply watcher",
                "No run recorded",
                HealthSeverity.Warning,
                state.LastKnownVersion,
                statePath,
                null,
                "The scheduled task exists, but watcher-state.json has no recorded tick yet.",
                "WatchAutoReapply");
        }

        if (state.LastOutcome?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Component(
                "auto-reapply-watcher",
                "Auto-reapply watcher",
                "Last tick failed",
                HealthSeverity.Warning,
                state.LastKnownVersion,
                statePath,
                state.LastRunAt,
                state.LastOutcome,
                "WatchAutoReapply",
                "Reapply");
        }

        if (state.LastRunAt.Value < DateTime.Now.AddDays(-7))
        {
            return Component(
                "auto-reapply-watcher",
                "Auto-reapply watcher",
                "Stale",
                HealthSeverity.Warning,
                state.LastKnownVersion,
                statePath,
                state.LastRunAt,
                "The scheduled task exists, but it has not recorded a tick in more than seven days.",
                "WatchAutoReapply");
        }

        return Component(
            "auto-reapply-watcher",
            "Auto-reapply watcher",
            string.IsNullOrWhiteSpace(state.LastOutcome) ? "Active" : state.LastOutcome,
            HealthSeverity.Ready,
            state.LastKnownVersion,
            statePath,
            state.LastRunAt,
            "The scheduled task is registered and watcher state is recent.");
    }

    private StackHealthComponent BuildPostUpdateTriageComponent(
        string statePath,
        WatcherState state,
        bool spotifyInstalled,
        bool spicetifyInstalled,
        bool autoReapplyTaskRegistered,
        bool marketplaceFilesPresent,
        bool marketplaceRegistered)
    {
        var currentSpotifyVersion = _spotifyVersionProbe();
        var spicetifyVersion = _spicetifyVersionProbe();
        var lastPatchedVersion = FirstNonEmpty(state.LastAppliedSpotifyVersion, state.LastKnownVersion);
        var marketplaceReady = marketplaceFilesPresent && marketplaceRegistered;
        var spotifyVersionChanged = spotifyInstalled &&
            !string.IsNullOrWhiteSpace(currentSpotifyVersion) &&
            !string.IsNullOrWhiteSpace(lastPatchedVersion) &&
            !string.Equals(currentSpotifyVersion, lastPatchedVersion, StringComparison.OrdinalIgnoreCase);

        if (!spotifyInstalled)
        {
            return Component(
                "post-spotify-update",
                "After Spotify update",
                "Not applicable",
                HealthSeverity.Info,
                null,
                statePath,
                LatestStateChange(state),
                "Post-update drift checks start after Spotify is installed.",
                "Install");
        }

        if (string.IsNullOrWhiteSpace(lastPatchedVersion))
        {
            return Component(
                "post-spotify-update",
                "After Spotify update",
                autoReapplyTaskRegistered ? "No watcher history" : "Watcher not enabled",
                HealthSeverity.Info,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                autoReapplyTaskRegistered
                    ? "LibreSpot has not recorded a patched Spotify version yet. Run the setup or reapply once so future Spotify updates can be compared accurately."
                    : "Auto-reapply is not enabled and LibreSpot has no recorded patched Spotify version to compare against future updates.",
                spicetifyInstalled ? "Reapply" : "Install");
        }

        if (string.Equals(state.LastOutcome, "Reapplied", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(currentSpotifyVersion) &&
            string.Equals(state.LastAppliedSpotifyVersion, currentSpotifyVersion, StringComparison.OrdinalIgnoreCase))
        {
            if (!marketplaceReady && spicetifyInstalled)
            {
                return Component(
                    "post-spotify-update",
                    "After Spotify update",
                    "Marketplace still missing",
                    HealthSeverity.Warning,
                    currentSpotifyVersion,
                    statePath,
                    LatestStateChange(state),
                    $"Watcher reapplied Spotify {currentSpotifyVersion}, but Marketplace is not ready. Last successful apply: {FormatMaybe(state.LastSuccessfulApplyAt)}. Spicetify: {FormatMaybe(spicetifyVersion)}.",
                    "RepairMarketplace",
                    "OpenLogs");
            }

            return Component(
                "post-spotify-update",
                "After Spotify update",
                "Reapplied",
                HealthSeverity.Ready,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                $"Watcher reapplied the current Spotify version. Last successful apply: {FormatMaybe(state.LastSuccessfulApplyAt)}. Spicetify: {FormatMaybe(spicetifyVersion)}.");
        }

        if (string.Equals(state.LastOutcome, "DeferredSpotifyRunning", StringComparison.OrdinalIgnoreCase) || (spotifyVersionChanged && _spotifyRunningProbe()))
        {
            return Component(
                "post-spotify-update",
                "After Spotify update",
                "Close Spotify first",
                HealthSeverity.Warning,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                $"Spotify changed from {FormatMaybe(lastPatchedVersion)} to {FormatMaybe(currentSpotifyVersion)}, but the watcher deferred because Spotify was running. Close Spotify, then reapply the saved profile.",
                "Reapply",
                "OpenLogs");
        }

        if (state.LastOutcome?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) == true)
        {
            var failedDuringSpotX = state.LastOutcome.Contains("spotx", StringComparison.OrdinalIgnoreCase);
            return Component(
                "post-spotify-update",
                "After Spotify update",
                failedDuringSpotX ? "SpotX reapply failed" : "Watcher failed",
                failedDuringSpotX ? HealthSeverity.Critical : HealthSeverity.Warning,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                $"Watcher could not finish after Spotify changed from {FormatMaybe(lastPatchedVersion)} to {FormatMaybe(currentSpotifyVersion)}. {state.LastOutcome}",
                "Reapply",
                "OpenLogs");
        }

        if (string.Equals(state.LastApplyOutcome, "SpicetifyApplyRolledBack", StringComparison.OrdinalIgnoreCase) ||
            state.LastApplyError?.Contains("restored Spotify to a usable state", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Component(
                "post-spotify-update",
                "After Spotify update",
                "Spicetify rolled back",
                HealthSeverity.Warning,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                $"Spicetify apply failed after the Spotify update, but LibreSpot restored Spotify. Error: {FormatMaybe(state.LastApplyError)}",
                "Reapply",
                "RestoreVanilla",
                "OpenLogs");
        }

        if (spotifyVersionChanged)
        {
            return Component(
                "post-spotify-update",
                "After Spotify update",
                "Reapply needed",
                HealthSeverity.Warning,
                currentSpotifyVersion,
                statePath,
                LatestStateChange(state),
                $"Spotify changed from {FormatMaybe(lastPatchedVersion)} to {FormatMaybe(currentSpotifyVersion)}. Reapply the saved profile before escalating to reset.",
                "Reapply",
                "OpenLogs");
        }

        return Component(
            "post-spotify-update",
            "After Spotify update",
            "No drift",
            HealthSeverity.Ready,
            currentSpotifyVersion,
            statePath,
            LatestStateChange(state),
            $"Current Spotify version matches the last known patched version. Spicetify: {FormatMaybe(spicetifyVersion)}.");
    }

    private StackHealthComponent BuildLogsComponent(string configDirectory)
    {
        var installLogPath = Path.Combine(configDirectory, "install.log");
        var installLogChanged = GetLastChanged(installLogPath);
        var rollingLogChanged = GetNewestFileChange(_rollingLogDirectory, "librespot-*.log");
        var latest = Max(installLogChanged, rollingLogChanged);

        if (latest is null)
        {
            return Component(
                "logs",
                "Logs",
                "No logs yet",
                HealthSeverity.Info,
                null,
                installLogPath,
                null,
                "No backend install log or desktop rolling log was found yet.");
        }

        return Component(
            "logs",
            "Logs",
            "Available",
            HealthSeverity.Ready,
            null,
            installLogChanged is null ? _rollingLogDirectory : installLogPath,
            latest,
            "Backend and desktop logs are available for local troubleshooting.");
    }

    private StackHealthComponent BuildCrashComponent()
    {
        var crashFiles = GetFiles(_crashDirectory, "crash-*.log");
        var newest = crashFiles
            .Select(GetLastChanged)
            .Where(changed => changed.HasValue)
            .OrderByDescending(changed => changed)
            .FirstOrDefault();
        var recentCrashCount = crashFiles.Count(path =>
        {
            var changed = GetLastChanged(path);
            return changed.HasValue && changed.Value >= DateTime.Now.AddDays(-7);
        });

        if (recentCrashCount > 0)
        {
            return Component(
                "crash-reports",
                "Crash reports",
                recentCrashCount == 1 ? "1 recent crash" : $"{recentCrashCount} recent crashes",
                HealthSeverity.Warning,
                null,
                _crashDirectory,
                newest,
                "Recent crash reports exist; inspect them before treating the shell as stable.",
                "OpenLogs");
        }

        return Component(
            "crash-reports",
            "Crash reports",
            "No recent crashes",
            HealthSeverity.Ready,
            null,
            _crashDirectory,
            newest,
            crashFiles.Length == 0
                ? "No crash reports were found."
                : "Crash reports exist, but none are recent.");
    }

    private static StackHealthComponent BuildSavedProfileComponent(
        string configPath,
        bool savedConfigExists,
        bool configFolderExists)
    {
        if (savedConfigExists)
        {
            return Component(
                "saved-profile",
                "Saved LibreSpot profile",
                "Available",
                HealthSeverity.Ready,
                null,
                configPath,
                GetLastChanged(configPath),
                "config.json exists and can seed reapply, watcher, and custom install flows.");
        }

        return Component(
            "saved-profile",
            "Saved LibreSpot profile",
            configFolderExists ? "Not saved" : "Profile folder missing",
            HealthSeverity.Info,
            null,
            configPath,
            null,
            configFolderExists
                ? "The LibreSpot profile folder exists, but config.json has not been saved yet."
                : "The LibreSpot profile folder will be created on the first save.",
            "Install");
    }

    private StackHealthComponent BuildExtensionFileIntegrityComponent(
        bool spicetifyInstalled,
        IReadOnlyDictionary<string, string> spicetifyConfig)
    {
        var extensionsDir = Path.Combine(_spicetifyConfigDirectory, "Extensions");

        if (!spicetifyInstalled)
        {
            return Component(
                "extension-integrity",
                "Extension files",
                "Not applicable",
                HealthSeverity.Info,
                null,
                extensionsDir,
                null,
                "Extension file checks run after Spicetify is installed.");
        }

        if (!spicetifyConfig.TryGetValue("extensions", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return Component(
                "extension-integrity",
                "Extension files",
                "None registered",
                HealthSeverity.Ready,
                null,
                extensionsDir,
                null,
                "No Spicetify extensions are registered in config-xpui.ini.");
        }

        var registered = raw
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (registered.Length == 0)
        {
            return Component(
                "extension-integrity",
                "Extension files",
                "None registered",
                HealthSeverity.Ready,
                null,
                extensionsDir,
                null,
                "No Spicetify extensions are registered in config-xpui.ini.");
        }

        var invalid = registered
            .Where(ext => !IsSafeExtensionFileName(ext))
            .ToArray();
        if (invalid.Length > 0)
        {
            var label = invalid.Length == 1 ? "Invalid entry" : $"{invalid.Length} invalid entries";
            return Component(
                "extension-integrity",
                "Extension files",
                label,
                HealthSeverity.Warning,
                null,
                extensionsDir,
                null,
                "Spicetify extension entries must be plain file names under the Extensions folder; path separators, rooted paths, and invalid file-name characters are ignored.",
                "Reapply");
        }

        var missing = registered
            .Where(ext => !File.Exists(Path.Combine(extensionsDir, ext)))
            .ToArray();

        if (missing.Length > 0)
        {
            var label = missing.Length == 1 ? $"'{missing[0]}' missing" : $"{missing.Length} files missing";
            return Component(
                "extension-integrity",
                "Extension files",
                label,
                HealthSeverity.Warning,
                null,
                extensionsDir,
                null,
                $"Registered extensions are missing from disk: {string.Join(", ", missing)}. A security product (such as Microsoft Defender) may have quarantined them. Check Windows Security > Virus & threat protection > Protection history.",
                "Reapply");
        }

        return Component(
            "extension-integrity",
            "Extension files",
            "All present",
            HealthSeverity.Ready,
            null,
            extensionsDir,
            GetNewestFileChange(extensionsDir),
            $"All {registered.Length} registered extension file(s) exist on disk.");
    }

    private static bool IsSafeExtensionFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (Path.IsPathFullyQualified(value) ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar) ||
            string.Equals(value, ".", StringComparison.Ordinal) ||
            string.Equals(value, "..", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(Path.GetFileName(value), value, StringComparison.Ordinal) &&
               value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static string ResolveConfigDirectory(string configPath)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(configPath));
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    return directory;
                }
            }
            catch
            {
                // Fall back to the production profile location below.
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LibreSpot");
    }

    private static bool IsSpicetifyListEntryEnabled(
        IReadOnlyDictionary<string, string> entries,
        string key,
        string expectedValue)
    {
        if (!entries.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(expectedValue, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> ReadSpicetifyConfigEntries(string configPath)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(configPath))
        {
            return entries;
        }

        try
        {
            foreach (var line in File.ReadLines(configPath))
            {
                var trimmed = line.Trim();
                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex < 0)
                {
                    continue;
                }

                entries[trimmed[..separatorIndex].Trim()] = trimmed[(separatorIndex + 1)..].Trim();
            }
        }
        catch
        {
            return entries;
        }

        return entries;
    }

    private static StackHealthComponent Component(
        string id,
        string name,
        string status,
        string severity,
        string? detectedVersion,
        string? path,
        DateTime? lastChanged,
        string evidence,
        params string[] recommendedActionIds) =>
        new(
            id,
            name,
            status,
            severity,
            detectedVersion,
            path,
            lastChanged,
            evidence,
            Array.AsReadOnly(recommendedActionIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray()));

    private static string? GetFileVersion(string path)
    {
        try
        {
            return File.Exists(path)
                ? FileVersionInfo.GetVersionInfo(path).FileVersion
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? GetLastChanged(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.GetLastWriteTime(path);
            }

            if (Directory.Exists(path))
            {
                return Directory.GetLastWriteTime(path);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static DateTime? GetNewestFileChange(string directory, string pattern = "*")
    {
        var files = GetFiles(directory, pattern);
        return files
            .Select(GetLastChanged)
            .Where(changed => changed.HasValue)
            .OrderByDescending(changed => changed)
            .FirstOrDefault();
    }

    private static DateTime? GetNewestDirectoryChange(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }

            return Directory
                .GetDirectories(directory)
                .Select(GetLastChanged)
                .Where(changed => changed.HasValue)
                .OrderByDescending(changed => changed)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static int CountDirectories(string directory)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.GetDirectories(directory).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string[] GetFiles(string directory, string pattern)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static DateTime? Max(params DateTime?[] values) =>
        values
            .Where(value => value.HasValue)
            .OrderByDescending(value => value)
            .FirstOrDefault();

    private static bool IsEnabledValue(string value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);

    private static WatcherState ReadWatcherState(string path)
    {
        if (!File.Exists(path))
        {
            return WatcherState.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var version = TryGetString(root, "LastKnownVersion");
            var outcome = TryGetString(root, "LastOutcome");
            var lastRun = TryGetDateTime(root, "LastRunAt");
            var lastAppliedSpotifyVersion = TryGetString(root, "LastAppliedSpotifyVersion");
            var lastAttemptedSpotifyVersion = TryGetString(root, "LastAttemptedSpotifyVersion");
            var lastSuccessfulApplyAt = TryGetDateTime(root, "LastSuccessfulApplyAt");
            var lastApplyAt = TryGetDateTime(root, "LastApplyAt");
            var lastApplyOutcome = TryGetString(root, "LastApplyOutcome");
            var lastApplyError = TryGetString(root, "LastApplyError");

            return new WatcherState(
                version,
                lastRun,
                outcome,
                lastAppliedSpotifyVersion,
                lastAttemptedSpotifyVersion,
                lastSuccessfulApplyAt,
                lastApplyAt,
                lastApplyOutcome,
                lastApplyError);
        }
        catch
        {
            return WatcherState.Empty;
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }

    private static DateTime? TryGetDateTime(JsonElement element, string propertyName)
    {
        var raw = TryGetString(element, propertyName);
        return DateTime.TryParse(raw, out var parsed)
            ? parsed.ToLocalTime()
            : null;
    }

    private static DateTime? LatestStateChange(WatcherState state) =>
        Max(state.LastRunAt, state.LastSuccessfulApplyAt, state.LastApplyAt);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string FormatMaybe(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm") ?? "unknown";

    private static string FormatMaybe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value;

    private sealed record WatcherState(
        string? LastKnownVersion,
        DateTime? LastRunAt,
        string? LastOutcome,
        string? LastAppliedSpotifyVersion,
        string? LastAttemptedSpotifyVersion,
        DateTime? LastSuccessfulApplyAt,
        DateTime? LastApplyAt,
        string? LastApplyOutcome,
        string? LastApplyError)
    {
        public static WatcherState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
    }

    private static bool IsAutoReapplyTaskRegistered()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    ArgumentList =
                    {
                        "/Query",
                        "/TN",
                        @"LibreSpot\ReapplyWatcher"
                    }
                }
            };

            if (!process.Start())
            {
                return false;
            }

            var stdoutDrain = process.StandardOutput.ReadToEndAsync();
            var stderrDrain = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(1500))
            {
                try { process.Kill(); } catch { }
                try { process.WaitForExit(500); } catch { }
                return false;
            }

            try { Task.WaitAll(new Task[] { stdoutDrain, stderrDrain }, 500); } catch { }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSpotifyRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("Spotify");
            try { return processes.Length > 0; }
            finally { foreach (var p in processes) p.Dispose(); }
        }
        catch
        {
            return false;
        }
    }

    private static string GetHostArchitecture()
    {
        try
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetProcessArchitecture()
    {
        try
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }
}
