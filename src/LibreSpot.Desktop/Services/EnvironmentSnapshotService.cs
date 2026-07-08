using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
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
    private readonly Func<UpstreamDriftReport> _upstreamDriftProbe;
    private readonly Func<CommunityAssetDriftReport> _communityAssetDriftProbe;
    private readonly Func<AntivirusExclusionStatus> _antivirusProbe;
    private readonly Func<bool> _storeSpotifyProbe;

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
        Func<bool>? spotifyRunningProbe = null,
        Func<UpstreamDriftReport>? upstreamDriftProbe = null,
        Func<CommunityAssetDriftReport>? communityAssetDriftProbe = null,
        // Defaults to Unavailable (never shells out) so unit tests and the
        // UI-automation smoke state stay fast and deterministic. Production
        // callers pass QueryDefenderExclusionStatus to enable live detection.
        Func<AntivirusExclusionStatus>? antivirusProbe = null,
        // Same test-safe default: false (never shells out). Production callers
        // pass QueryStoreSpotifyPresent to enable live Microsoft Store detection.
        Func<bool>? storeSpotifyProbe = null)
    {
        _antivirusProbe = antivirusProbe ?? (() => AntivirusExclusionStatus.Unavailable);
        _storeSpotifyProbe = storeSpotifyProbe ?? (() => false);
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
        _upstreamDriftProbe = upstreamDriftProbe ?? UpstreamDriftService.Default.GetCachedReport;
        _communityAssetDriftProbe = communityAssetDriftProbe ?? CommunityAssetDriftService.Default.GetCachedReport;
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
        var marketplaceEvidence = ReadMarketplaceVisibilityEvidence(configDirectory);
        var assetCacheInventory = ReadAssetCacheInventory(configDirectory);
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
        var upstreamDriftReport = GetUpstreamDriftReport();
        var communityAssetDriftReport = GetCommunityAssetDriftReport();
        var antivirusStatus = GetAntivirusStatus();
        var storeSpotifyPresent = GetStoreSpotifyPresent();
        var healthReport = BuildHealthReport(
            configDirectory,
            configPath,
            spicetifyConfigPath,
            spicetifyConfig,
            marketplaceDirectory,
            marketplaceFilesPresent,
            marketplaceRegistered,
            marketplaceEvidence,
            spotifyInstalled,
            spicetifyInstalled,
            savedConfigExists,
            configFolderExists,
            autoReapplyTaskRegistered,
            upstreamDriftReport,
            communityAssetDriftReport,
            assetCacheInventory,
            antivirusStatus,
            storeSpotifyPresent);

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
            UpstreamDriftReport = upstreamDriftReport,
            CommunityAssetDriftReport = communityAssetDriftReport,
            AssetCacheInventory = assetCacheInventory,
            MarketplaceVisibilityEvidence = marketplaceEvidence,
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
        MarketplaceVisibilityEvidence? marketplaceEvidence,
        bool spotifyInstalled,
        bool spicetifyInstalled,
        bool savedConfigExists,
        bool configFolderExists,
        bool autoReapplyTaskRegistered,
        UpstreamDriftReport upstreamDriftReport,
        CommunityAssetDriftReport communityAssetDriftReport,
        AssetCacheInventoryReport assetCacheInventory,
        AntivirusExclusionStatus antivirusStatus,
        bool storeSpotifyPresent)
    {
        var watcherStatePath = Path.Combine(configDirectory, "watcher-state.json");
        var watcherState = ReadWatcherState(watcherStatePath);
        var components = new List<StackHealthComponent>
        {
            BuildSpotifyComponent(spotifyInstalled),
            BuildSpotXComponent(spotifyInstalled),
            BuildSpicetifyCliComponent(spicetifyInstalled),
            BuildSpicetifyConfigComponent(spicetifyInstalled, spicetifyConfigPath, spicetifyConfig),
            BuildMarketplaceComponent(spicetifyInstalled, marketplaceDirectory, marketplaceFilesPresent, marketplaceRegistered, marketplaceEvidence),
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
            BuildSavedProfileComponent(configPath, savedConfigExists, configFolderExists),
            BuildAssetCacheComponent(assetCacheInventory)
        };
        components.AddRange(BuildUpstreamDriftComponents(upstreamDriftReport));
        components.AddRange(BuildCommunityAssetComponents(communityAssetDriftReport));
        components.AddRange(BuildAntivirusComponents(antivirusStatus));
        components.AddRange(BuildStoreSpotifyComponents(storeSpotifyPresent));

        return new StackHealthReport(components);
    }

    // Only emits when the Microsoft Store Spotify package is present. SpotX
    // auto-removes it at install (Build-SpotXParams always passes
    // -confirm_uninstall_ms_spoti), so this is an informational heads-up, not a
    // problem to repair - it explains why the Store build disappears.
    private static IEnumerable<StackHealthComponent> BuildStoreSpotifyComponents(bool storeSpotifyPresent)
    {
        if (!storeSpotifyPresent)
        {
            yield break;
        }

        yield return new StackHealthComponent(
            "store-spotify",
            "Microsoft Store Spotify",
            "Will be replaced",
            HealthSeverity.Info,
            null,
            null,
            null,
            "The Microsoft Store version of Spotify is installed. LibreSpot patches the standard desktop build, so SpotX will remove the Store version and install the desktop build in its place during setup.",
            Array.Empty<string>());
    }

    // Only emits a component when there is an actionable finding: Defender was
    // inspectable, real-time protection is on, and the Spotify install folder
    // (where SpotX writes its patched binaries) is not already excluded. In
    // every other case - Defender unavailable/third-party AV, protection off,
    // or the folder already excluded - LibreSpot stays silent instead of
    // adding a standing informational note.
    private IEnumerable<StackHealthComponent> BuildAntivirusComponents(AntivirusExclusionStatus status)
    {
        if (!status.Queried || !status.RealtimeProtectionEnabled)
        {
            yield break;
        }

        var spotifyDirectory = Path.GetDirectoryName(_spotifyPath);
        if (string.IsNullOrWhiteSpace(spotifyDirectory) || IsPathCoveredByExclusions(spotifyDirectory, status.ExcludedPaths))
        {
            yield break;
        }

        yield return new StackHealthComponent(
            "antivirus-exclusion",
            "Antivirus exclusion",
            "Exclusion recommended",
            HealthSeverity.Warning,
            null,
            spotifyDirectory,
            null,
            "Windows Defender real-time protection is on and the Spotify folder is not excluded, so SpotX-patched files can be quarantined as a false positive (Defender classifies ad-block patches as HackTool by behavior, which code-signing does not clear). LibreSpot never changes your antivirus settings for you. To add an exclusion yourself, open PowerShell as administrator and run: " +
            $"Add-MpPreference -ExclusionPath \"{spotifyDirectory}\"",
            Array.Empty<string>());
    }

    // A folder is "covered" when it equals, or sits under, any Defender
    // exclusion path (Defender folder exclusions apply to their whole subtree).
    private static bool IsPathCoveredByExclusions(string targetDirectory, IReadOnlyList<string> exclusions)
    {
        string Normalize(string p) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(p.Trim().Trim('"')))
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        string target;
        try { target = Normalize(targetDirectory); }
        catch { return false; }

        foreach (var raw in exclusions)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string exclusion;
            try { exclusion = Normalize(raw); }
            catch { continue; }

            if (string.Equals(target, exclusion, StringComparison.OrdinalIgnoreCase) ||
                target.StartsWith(exclusion + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private AntivirusExclusionStatus GetAntivirusStatus()
    {
        try
        {
            return _antivirusProbe() ?? AntivirusExclusionStatus.Unavailable;
        }
        catch
        {
            return AntivirusExclusionStatus.Unavailable;
        }
    }

    private bool GetStoreSpotifyPresent()
    {
        try
        {
            return _storeSpotifyProbe();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Live Microsoft Store Spotify (AppX) detection. Shells out to
    /// <c>Get-AppxPackage SpotifyAB.SpotifyMusic</c> and returns false on any
    /// failure (Appx module unavailable, timeout, error) so a heads-up is never
    /// shown on a guess. Read-only: never removes the package (SpotX does that
    /// during setup).
    /// </summary>
    public static bool QueryStoreSpotifyPresent()
    {
        const string script =
            "$ErrorActionPreference='Stop';" +
            "if (Get-AppxPackage -Name 'SpotifyAB.SpotifyMusic' -ErrorAction SilentlyContinue) { 'present' } else { 'absent' }";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script }
                }
            };

            if (!process.Start())
            {
                return false;
            }

            var stdoutDrain = process.StandardOutput.ReadToEndAsync();
            var stderrDrain = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                try { process.WaitForExit(500); } catch { }
                return false;
            }

            try { Task.WaitAll(new Task[] { stdoutDrain, stderrDrain }, 500); } catch { }

            return process.ExitCode == 0 &&
                stdoutDrain.Result.Contains("present", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private UpstreamDriftReport GetUpstreamDriftReport()
    {
        try
        {
            return _upstreamDriftProbe();
        }
        catch (Exception ex)
        {
            return new UpstreamDriftReport(
                AppCatalog.UpstreamDependencyPins.Select(pin =>
                {
                    var current = NormalizeUpstreamValue(pin, pin.PinnedValue);
                    return new UpstreamDependencyState(
                        pin.Id,
                        pin.Name,
                        pin.PinnedValue,
                        current,
                        null,
                        "unknown",
                        "unavailable",
                        DateTimeOffset.UtcNow,
                        null,
                        true,
                        $"Pinned {pin.PinnedValue}; current {current}; latest unknown; drift unknown; source unavailable; cache age none. Live upstream metadata is degraded. Detail: {ex.Message}");
                }),
                DateTimeOffset.UtcNow);
        }
    }

    private CommunityAssetDriftReport GetCommunityAssetDriftReport()
    {
        try
        {
            return _communityAssetDriftProbe();
        }
        catch (Exception ex)
        {
            return new CommunityAssetDriftReport(
                CommunityAssetDriftService.Default.Pins.Select(pin =>
                    new CommunityAssetState(
                        pin.Id,
                        pin.Kind,
                        pin.Name,
                        pin.SourceUrl ?? $"https://github.com/{pin.Owner}/{pin.Repository}",
                        $"https://github.com/{pin.Owner}/{pin.Repository}.git",
                        $"refs/heads/{pin.Branch}",
                        pin.PinnedCommit,
                        pin.PinnedHash,
                        null,
                        "degraded",
                        "unavailable",
                        DateTimeOffset.UtcNow,
                        null,
                        true,
                        pin.License,
                        pin.SupportState,
                        pin.FallbackBehavior,
                        pin.NetworkBehavior,
                        pin.NetworkDetail,
                        pin.RequiresTrustReview,
                        $"Pinned commit {pin.PinnedCommit}; latest unknown; drift degraded; source unavailable; cache age none. Live community asset metadata is degraded. Detail: {ex.Message}")),
                DateTimeOffset.UtcNow);
        }
    }

    private static IEnumerable<StackHealthComponent> BuildUpstreamDriftComponents(UpstreamDriftReport report)
    {
        foreach (var dependency in report.Dependencies)
        {
            var status = dependency.IsDegraded
                ? string.IsNullOrWhiteSpace(dependency.LatestValue) ? "Latest unknown" : "Cached metadata"
                : dependency.DriftState switch
                {
                    "current" => "Current",
                    "behind" => "Upstream changed",
                    "ahead" => "Pinned ahead",
                    _ => "Latest unknown"
                };
            var severity = string.Equals(dependency.DriftState, "current", StringComparison.OrdinalIgnoreCase) &&
                !dependency.IsDegraded
                    ? HealthSeverity.Ready
                    : HealthSeverity.Info;

            yield return Component(
                $"upstream-{dependency.Id}",
                $"{dependency.Name} upstream",
                status,
                severity,
                dependency.LatestValue,
                null,
                dependency.CheckedAtUtc.LocalDateTime,
                dependency.Evidence);
        }
    }

    private static IEnumerable<StackHealthComponent> BuildCommunityAssetComponents(CommunityAssetDriftReport report)
    {
        foreach (var asset in report.Assets)
        {
            var status = CommunityAssetStatus(asset);
            var severity = CommunityAssetSeverity(asset);
            var actions = severity == HealthSeverity.Warning || string.Equals(asset.DriftState, "behind", StringComparison.OrdinalIgnoreCase)
                ? new[] { "ReviewCommunityAsset" }
                : Array.Empty<string>();

            yield return Component(
                CommunityAssetComponentId(asset),
                $"{asset.Name} community {asset.Kind}",
                status,
                severity,
                asset.LatestCommit,
                asset.SourceUrl,
                asset.CheckedAtUtc.LocalDateTime,
                asset.Evidence,
                actions);
        }
    }

    private static string CommunityAssetStatus(CommunityAssetState asset)
    {
        if (string.Equals(asset.DriftState, "missing", StringComparison.OrdinalIgnoreCase))
        {
            return "Missing upstream";
        }

        if (asset.IsDegraded)
        {
            return string.IsNullOrWhiteSpace(asset.LatestCommit) ? "Latest unknown" : "Cached metadata";
        }

        if (string.Equals(asset.DriftState, "behind", StringComparison.OrdinalIgnoreCase))
        {
            return "Upstream changed";
        }

        if (asset.RequiresTrustReview)
        {
            return "Review required";
        }

        return "Current";
    }

    private static string CommunityAssetSeverity(CommunityAssetState asset)
    {
        if (string.Equals(asset.DriftState, "missing", StringComparison.OrdinalIgnoreCase))
        {
            return HealthSeverity.Warning;
        }

        if (asset.IsDegraded ||
            string.Equals(asset.DriftState, "behind", StringComparison.OrdinalIgnoreCase) ||
            asset.RequiresTrustReview)
        {
            return HealthSeverity.Info;
        }

        return HealthSeverity.Ready;
    }

    private static string CommunityAssetComponentId(CommunityAssetState asset)
    {
        var slug = asset.Id
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray();
        return "community-" + new string(slug).Trim('-');
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
        // Current SpotX names its pre-patch backup Apps\xpui.bak; older builds used
        // Apps\xpui.spa.bak. Checking only xpui.spa.bak flagged every successful
        // install as "Unverified" because SpotX never writes that filename.
        var backupPath = Path.Combine(appsDirectory, "xpui.bak");
        var legacyBackupPath = Path.Combine(appsDirectory, "xpui.spa.bak");
        // After Spicetify applies, the packed bundle is extracted to Apps\xpui and
        // the SpotX xpui backup is consumed, but SpotX's durable native-binary
        // backups next to Spotify.exe persist and still prove a SpotX run.
        var extractedBundlePath = Path.Combine(appsDirectory, "xpui");
        var spotifyBinBackup = Path.Combine(spotifyDirectory, "Spotify.bak");
        var chromeElfBackup = Path.Combine(spotifyDirectory, "chrome_elf.dll.bak");
        var hasBundle = File.Exists(bundlePath) || Directory.Exists(extractedBundlePath);
        var hasBackup = File.Exists(backupPath) || File.Exists(legacyBackupPath);
        var hasBinBackup = File.Exists(spotifyBinBackup) || File.Exists(chromeElfBackup);

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

        if (hasBundle && (hasBackup || hasBinBackup))
        {
            return Component(
                "spotx",
                "SpotX patch",
                "Verified",
                HealthSeverity.Ready,
                null,
                appsDirectory,
                Max(GetLastChanged(bundlePath), Max(GetLastChanged(backupPath), GetLastChanged(spotifyBinBackup))),
                "The Spotify app bundle and a SpotX backup (Apps\\xpui.bak or a patched-binary backup) are present, matching SpotX's successful patch markers.");
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
                "Spotify's app bundle exists, but no SpotX backup marker (Apps\\xpui.bak or a patched-binary backup) was found.",
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
        bool registered,
        MarketplaceVisibilityEvidence? visibilityEvidence)
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
            var evidence = BuildMarketplaceEvidenceText(visibilityEvidence, "Marketplace files are present and custom_apps includes marketplace.");
            if (visibilityEvidence?.ApplySucceeded == false)
            {
                return Component(
                    "marketplace",
                    "Marketplace",
                    "Apply failed",
                    HealthSeverity.Warning,
                    visibilityEvidence.ManifestVersion,
                    marketplaceDirectory,
                    GetNewestFileChange(marketplaceDirectory),
                    evidence,
                    "Reapply",
                    "RepairMarketplace",
                    "OpenLogs");
            }

            if (visibilityEvidence?.OpenUriSucceeded == false)
            {
                return Component(
                    "marketplace",
                    "Marketplace",
                    "Open failed",
                    HealthSeverity.Warning,
                    visibilityEvidence.ManifestVersion,
                    marketplaceDirectory,
                    GetNewestFileChange(marketplaceDirectory),
                    evidence,
                    "OpenMarketplace",
                    "RepairMarketplace");
            }

            if (visibilityEvidence?.LikelyVisible == true)
            {
                return Component(
                    "marketplace",
                    "Marketplace",
                    "Likely visible",
                    HealthSeverity.Ready,
                    visibilityEvidence.ManifestVersion,
                    marketplaceDirectory,
                    GetNewestFileChange(marketplaceDirectory),
                    evidence,
                    "OpenMarketplace");
            }

            return Component(
                "marketplace",
                "Marketplace",
                "Files installed",
                HealthSeverity.Info,
                visibilityEvidence?.ManifestVersion,
                marketplaceDirectory,
                GetNewestFileChange(marketplaceDirectory),
                evidence,
                "OpenMarketplace",
                "RepairMarketplace");
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

    private static string BuildMarketplaceEvidenceText(MarketplaceVisibilityEvidence? visibilityEvidence, string fallback)
    {
        if (visibilityEvidence is null)
        {
            return fallback + " No post-apply visibility evidence has been recorded yet.";
        }

        var manifest = string.IsNullOrWhiteSpace(visibilityEvidence.ManifestVersion)
            ? "manifest version unknown"
            : $"manifest version {visibilityEvidence.ManifestVersion}";
        var apply = visibilityEvidence.ApplySucceeded.HasValue
            ? $"{visibilityEvidence.ApplyStage ?? "apply"} {(visibilityEvidence.ApplySucceeded.Value ? "succeeded" : "failed")}"
            : "apply result not recorded";
        var open = visibilityEvidence.OpenUriSucceeded.HasValue
            ? $"open URI {(visibilityEvidence.OpenUriSucceeded.Value ? "succeeded" : "failed")}"
            : "open URI not recorded";
        var spotify = visibilityEvidence.SpotifyRunningAfterOpen.HasValue
            ? visibilityEvidence.SpotifyRunningAfterOpen.Value ? "Spotify process observed after URI request" : "Spotify process not observed after URI request"
            : visibilityEvidence.LastObservedSpotifySession;

        var text =
            $"{fallback} Visibility evidence from {visibilityEvidence.Source}: {manifest}; {apply}; {open}; {spotify}; likelyVisible={visibilityEvidence.LikelyVisible}.";

        if (!string.IsNullOrWhiteSpace(visibilityEvidence.ApplyMessage))
        {
            text += $" Apply detail: {visibilityEvidence.ApplyMessage}";
        }

        if (!string.IsNullOrWhiteSpace(visibilityEvidence.OpenUriMessage))
        {
            text += $" Open detail: {visibilityEvidence.OpenUriMessage}";
        }

        return text;
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

    private static StackHealthComponent BuildAssetCacheComponent(AssetCacheInventoryReport report)
    {
        var newest = report.Entries
            .Select(entry => entry.LastVerifiedAtUtc?.LocalDateTime ?? entry.LastUsedAtUtc?.LocalDateTime ?? entry.FirstSeenAtUtc?.LocalDateTime)
            .Where(changed => changed.HasValue)
            .OrderByDescending(changed => changed)
            .FirstOrDefault();
        var summary = $"Cache entries: {report.EntryCount}; present: {report.PresentCount}; size: {FormatBytes(report.TotalBytes)}.";

        if (report.CorruptCount > 0)
        {
            return Component(
                "asset-cache",
                "Asset cache",
                report.CorruptCount == 1 ? "1 corrupt entry" : $"{report.CorruptCount} corrupt entries",
                HealthSeverity.Warning,
                null,
                report.CacheDirectory,
                newest,
                $"{summary} {report.CorruptCount} cached file(s) failed SHA256 verification and should be cleared before relying on offline installs.",
                "ClearCache");
        }

        if (report.StaleCount > 0)
        {
            return Component(
                "asset-cache",
                "Asset cache",
                report.StaleCount == 1 ? "1 stale entry" : $"{report.StaleCount} stale entries",
                HealthSeverity.Info,
                null,
                report.CacheDirectory,
                newest,
                $"{summary} Stale means an index row points at a missing file or a legacy hash file has no source label yet.",
                "ClearCache");
        }

        if (report.EntryCount == 0)
        {
            return Component(
                "asset-cache",
                "Asset cache",
                "Empty",
                HealthSeverity.Info,
                null,
                report.CacheDirectory,
                null,
                "No verified download cache entries were found yet.");
        }

        return Component(
            "asset-cache",
            "Asset cache",
            report.EntryCount == 1 ? "1 entry" : $"{report.EntryCount} entries",
            HealthSeverity.Ready,
            null,
            report.CacheDirectory,
            newest,
            summary);
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

    private static AssetCacheInventoryReport ReadAssetCacheInventory(string configDirectory)
    {
        var cacheDirectory = Path.Combine(configDirectory, "cache");
        var indexPath = Path.Combine(cacheDirectory, "asset-cache-index.json");
        var entries = new List<AssetCacheEntryState>();
        var indexedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(indexPath))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(indexPath));
                if (document.RootElement.TryGetProperty("entries", out var entryArray) &&
                    entryArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in entryArray.EnumerateArray())
                    {
                        var hash = NormalizeSha256(TryGetString(entry, "sha256"));
                        if (hash is null)
                        {
                            continue;
                        }

                        indexedHashes.Add(hash);
                        entries.Add(BuildAssetCacheEntry(
                            cacheDirectory,
                            hash,
                            TryGetString(entry, "label") ?? "Cached asset",
                            TryGetString(entry, "sourceUrl"),
                            TryGetInt64(entry, "byteSize"),
                            TryGetDateTimeOffset(entry, "firstSeenAtUtc"),
                            TryGetDateTimeOffset(entry, "lastUsedAtUtc"),
                            TryGetDateTimeOffset(entry, "lastVerifiedAtUtc"),
                            TryGetString(entry, "status"),
                            indexed: true));
                    }
                }
            }
            catch
            {
                entries.Add(new AssetCacheEntryState(
                    "asset-cache-index",
                    "Asset cache index",
                    null,
                    File.Exists(indexPath) ? new FileInfo(indexPath).Length : 0,
                    null,
                    null,
                    null,
                    "corrupt",
                    indexPath,
                    File.Exists(indexPath),
                    "asset-cache-index.json could not be parsed."));
            }
        }

        if (Directory.Exists(cacheDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(cacheDirectory))
            {
                var name = Path.GetFileName(file);
                var hash = NormalizeSha256(name);
                if (hash is null || indexedHashes.Contains(hash))
                {
                    continue;
                }

                entries.Add(BuildAssetCacheEntry(
                    cacheDirectory,
                    hash,
                    "Unindexed cached asset",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    indexed: false));
            }
        }

        return new AssetCacheInventoryReport(entries, cacheDirectory, indexPath, DateTimeOffset.UtcNow);
    }

    private static AssetCacheEntryState BuildAssetCacheEntry(
        string cacheDirectory,
        string hash,
        string label,
        string? sourceUrl,
        long? indexedByteSize,
        DateTimeOffset? firstSeenAtUtc,
        DateTimeOffset? lastUsedAtUtc,
        DateTimeOffset? lastVerifiedAtUtc,
        string? indexedStatus,
        bool indexed)
    {
        var path = Path.Combine(cacheDirectory, hash);
        if (!File.Exists(path))
        {
            var missingStatus = string.Equals(indexedStatus, "corrupt", StringComparison.OrdinalIgnoreCase)
                ? "corrupt"
                : "missing";
            return new AssetCacheEntryState(
                hash,
                label,
                sourceUrl,
                indexedByteSize ?? 0,
                firstSeenAtUtc,
                lastUsedAtUtc,
                lastVerifiedAtUtc,
                missingStatus,
                path,
                false,
                missingStatus == "corrupt"
                    ? "The cache index records this asset as corrupt and the active hash-named file is no longer present."
                    : "The cache index references this asset, but the hash-named file is missing.");
        }

        var byteSize = new FileInfo(path).Length;
        try
        {
            var actual = GetFileSha256Lower(path);
            if (!string.Equals(actual, hash, StringComparison.OrdinalIgnoreCase))
            {
                return new AssetCacheEntryState(
                    hash,
                    label,
                    sourceUrl,
                    byteSize,
                    firstSeenAtUtc,
                    lastUsedAtUtc,
                    lastVerifiedAtUtc,
                    "corrupt",
                    path,
                    true,
                    $"SHA256 mismatch: expected {hash}, observed {actual}.");
            }
        }
        catch (Exception ex)
        {
            return new AssetCacheEntryState(
                hash,
                label,
                sourceUrl,
                byteSize,
                firstSeenAtUtc,
                lastUsedAtUtc,
                lastVerifiedAtUtc,
                "corrupt",
                path,
                true,
                $"SHA256 verification failed: {ex.Message}");
        }

        return new AssetCacheEntryState(
            hash,
            label,
            sourceUrl,
            byteSize,
            firstSeenAtUtc,
            lastUsedAtUtc,
            lastVerifiedAtUtc,
            indexed ? "present" : "unindexed",
            path,
            true,
            indexed
                ? "Hash-named cache file exists and matches the expected SHA256."
                : "Hash-named cache file verifies, but no source label exists in the cache index yet.");
    }

    private static MarketplaceVisibilityEvidence? ReadMarketplaceVisibilityEvidence(string configDirectory)
    {
        var path = Path.Combine(configDirectory, "marketplace-evidence.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var schemaVersion = TryGetInt32(root, "schemaVersion") ?? 0;
            if (schemaVersion != 1)
            {
                return null;
            }

            return new MarketplaceVisibilityEvidence(
                schemaVersion,
                TryGetDateTimeOffset(root, "generatedAtUtc") ?? DateTimeOffset.MinValue,
                TryGetString(root, "source") ?? "unknown",
                TryGetBool(root, "filesPresent") ?? false,
                TryGetBool(root, "registered") ?? false,
                TryGetBool(root, "likelyVisible") ?? false,
                TryGetString(root, "marketplaceStatus") ?? "Unknown",
                TryGetString(root, "marketplacePath") ?? path,
                TryGetString(root, "manifestVersion"),
                TryGetString(root, "applyStage"),
                TryGetBool(root, "applySucceeded"),
                TryGetString(root, "applyMessage"),
                TryGetDateTimeOffset(root, "applyCompletedAtUtc"),
                TryGetBool(root, "openUriSucceeded"),
                TryGetString(root, "openUriMessage"),
                TryGetDateTimeOffset(root, "openUriRequestedAtUtc"),
                TryGetBool(root, "spotifyRunningAfterOpen"),
                TryGetString(root, "lastObservedSpotifySession") ?? "not observed",
                TryGetDateTimeOffset(root, "lastObservedAtUtc"));
        }
        catch
        {
            return null;
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

    private static bool? TryGetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var parsed)
            ? parsed
            : null;
    }

    private static long? TryGetInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var parsed)
            ? parsed
            : null;
    }

    private static DateTime? TryGetDateTime(JsonElement element, string propertyName)
    {
        // The backend writes these with Get-Date -Format 'o'; parse them
        // culture-invariantly because the UI thread runs under the user's
        // selected locale.
        var raw = TryGetString(element, propertyName);
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToLocalTime()
            : null;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement element, string propertyName)
    {
        var raw = TryGetString(element, propertyName);
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
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

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = Math.Max(0, bytes);
        var index = 0;
        var scaled = (double)value;
        while (scaled >= 1024 && index < units.Length - 1)
        {
            scaled /= 1024;
            index++;
        }

        return index == 0
            ? $"{value} {units[index]}"
            : $"{scaled:0.##} {units[index]}";
    }

    private static string? NormalizeSha256(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is { Length: 64 } && normalized.All(Uri.IsHexDigit)
            ? normalized
            : null;
    }

    private static string GetFileSha256Lower(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeUpstreamValue(UpstreamDependencyPin pin, string value)
    {
        var normalized = value.Trim();
        if (!string.IsNullOrWhiteSpace(pin.ValuePrefixToStrip) &&
            normalized.StartsWith(pin.ValuePrefixToStrip, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[pin.ValuePrefixToStrip.Length..];
        }

        return string.Equals(pin.ValueKind, "commit", StringComparison.OrdinalIgnoreCase)
            ? normalized.ToLowerInvariant()
            : normalized;
    }

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

    /// <summary>
    /// Live Windows Defender inspection for the antivirus health signal. Shells
    /// out to <c>Get-MpComputerStatus</c>/<c>Get-MpPreference</c> (Defender is
    /// the only AV with a stable query surface) and returns
    /// <see cref="AntivirusExclusionStatus.Unavailable"/> on any failure -
    /// third-party AV, missing cmdlets, timeout, or malformed output - so the
    /// caller never surfaces a guess. Read-only: never changes AV configuration.
    /// </summary>
    public static AntivirusExclusionStatus QueryDefenderExclusionStatus()
    {
        const string script =
            "$ErrorActionPreference='Stop';" +
            "$s=Get-MpComputerStatus;$p=Get-MpPreference;" +
            "[pscustomobject]@{realtime=[bool]$s.RealTimeProtectionEnabled;exclusions=@($p.ExclusionPath)}|ConvertTo-Json -Compress";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", script }
                }
            };

            if (!process.Start())
            {
                return AntivirusExclusionStatus.Unavailable;
            }

            var stdoutDrain = process.StandardOutput.ReadToEndAsync();
            var stderrDrain = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(); } catch { }
                try { process.WaitForExit(500); } catch { }
                return AntivirusExclusionStatus.Unavailable;
            }

            try { Task.WaitAll(new Task[] { stdoutDrain, stderrDrain }, 500); } catch { }

            if (process.ExitCode != 0)
            {
                return AntivirusExclusionStatus.Unavailable;
            }

            return ParseDefenderStatus(stdoutDrain.Result);
        }
        catch
        {
            return AntivirusExclusionStatus.Unavailable;
        }
    }

    internal static AntivirusExclusionStatus ParseDefenderStatus(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AntivirusExclusionStatus.Unavailable;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return AntivirusExclusionStatus.Unavailable;
            }

            var realtime = root.TryGetProperty("realtime", out var realtimeElement) &&
                realtimeElement.ValueKind == JsonValueKind.True;

            var exclusions = new List<string>();
            if (root.TryGetProperty("exclusions", out var exclusionsElement))
            {
                // ConvertTo-Json collapses a single-element array to a scalar.
                if (exclusionsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in exclusionsElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            var value = item.GetString();
                            if (!string.IsNullOrWhiteSpace(value)) { exclusions.Add(value); }
                        }
                    }
                }
                else if (exclusionsElement.ValueKind == JsonValueKind.String)
                {
                    var value = exclusionsElement.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) { exclusions.Add(value); }
                }
            }

            return new AntivirusExclusionStatus(true, realtime, exclusions);
        }
        catch
        {
            return AntivirusExclusionStatus.Unavailable;
        }
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
